using SystemRegisIII.WaylandForge.Ui;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using SystemRegisIII.Core;
using SystemRegisIII.WayControlProtocol;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class ForgeApp : IDisposable
{
    private const int TopBarHeight = 28;
    private const int StatusBarHeight = 24;
    private const int SidePanelWidth = 184;
    private const int MinimumWidthForSidePanel = 560;
    private const string DefaultConfigPath = "config/waylandforge.ui.toml";
    private const string LocalConfigPath = "config/waylandforge.ui.local.toml";
    private const string AudioSocketPath = "/tmp/waylandforge-audio.sock";
    private const uint AllKeysReleasedCode = uint.MaxValue;

    private readonly SoftwareCanvas _canvas = new();
    private readonly FakeSaturnCore _fakeCore = new();
    private readonly SaturnBringupCore _saturnCore = new();
    private readonly ExternalProcessCore _externalCore;
    private readonly ExternalProcessCore _externalCore2;
    private readonly ExternalProcessCore _externalCore3;
    private ISystemCore _core;
    private readonly ForgeInputSource _inputSource = new();
    private readonly WayControlInput _wayControlInput = new();
    private readonly FrameStore _frameStore = new();
    private readonly EmulatorViewport _viewport = new();
    private readonly FrameClock _clock = new();
    private readonly UiContext _ui;
    private readonly UiFilePicker _filePicker = new();
    private readonly UiConfig _config;
    private ForgeInput _lastInput;
    private ForgeInput _previousInput;
    private PointerState _pointer;
    private PointerState _previousPointer;
    private TextInputEvent _textInput;
    private ScrollInputEvent _scrollInput;
    private ulong _hostFrameIndex;
    private bool _paused;
    private bool _stepRequested;
    private readonly UiWindowState _viewportWindow = new() { IsOpen = true };
    private readonly UiWindowState _filePickerWindow = new();
    private readonly UiWindowState _settingsWindow = new();
    private readonly UiWindowState _styleWindow = new();
    private readonly UiWindowState _inputWindow = new();
    private readonly List<AppWindow> _windowOrder = [AppWindow.Style, AppWindow.Settings, AppWindow.Input, AppWindow.Rom];
    private readonly List<AppWindow> _tileOrder = [AppWindow.Viewport, AppWindow.Rom, AppWindow.Settings, AppWindow.Style, AppWindow.Input];
    private readonly HashSet<uint> _pressedKeys = [];
    private string? _romPath;
    private AppWindow _focusedTile = AppWindow.Viewport;
    private AppWindow? _fullscreenTile;
    private InputBinding? _capturingBinding;
    private InputBinding? _capturingControllerBinding;
    private uint _handledKeySerial;
    private int _themeIndex;
    private bool _configDirty;
    private string _coreFault = string.Empty;
    private double _lastConfigSaveSeconds;
    private bool _resizingTileSplit;
    private readonly List<TileResizeHandle> _tileResizeHandles = new();
    private AppWindow? _tileDragWindow;
    private RectI _tileDragRect;
    private int _tileResizeStartX;
    private int _tileResizeStartY;
    private int _lastRenderWidth;
    private int _lastRenderHeight;
    private int _lastSentAudioVolume = -1;
    private double _lastAudioVolumeAttemptSeconds;
    private double _lastAudioStatusAttemptSeconds;
    private ulong _observedWcpSequence;
    private double _wcpToFrameMilliseconds = -1;
    private string _audioStatus = "OFFLINE";
    private bool _externalPointerInitialized;
    private int _externalPointerX;
    private int _externalPointerY;
    private int _lastExternalPointerHostX;
    private int _lastExternalPointerHostY;

    public ForgeApp()
    {
        _ui = new UiContext(_canvas, UiTheme.Default);
        _config = UiConfig.Load(DefaultConfigPath, LocalConfigPath);
        _externalCore = new ExternalProcessCore(_config.ExternalCore, ResolveExternalDummyCorePath());
        _externalCore2 = new ExternalProcessCore(_config.ExternalCore2, ResolveExternalDummyCorePath());
        _externalCore3 = new ExternalProcessCore(_config.ExternalCore3, ResolveExternalDummyCorePath());
        _core = _saturnCore;
        ApplyConfig();
    }

    public uint Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input, PointerState pointer, TextInputEvent textInput, ScrollInputEvent scrollInput)
    {
        _lastRenderWidth = width;
        _lastRenderHeight = height;
        Update(input, pointer, textInput, scrollInput, frameIndex);

        _canvas.Bind(pixels, width, height, stridePixels);
        long drawStart = Stopwatch.GetTimestamp();
        Draw(width, height);
        _clock.RecordDraw(Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds);
        _previousPointer = pointer;
        return ShouldHideNativeCursor() ? 1u : 0u;
    }

    public void RawKeyInput(uint keyCode, uint keySerial, bool pressed)
    {
        ProcessRawKey(new TextInputEvent(keyCode, keySerial, pressed));
        PushCurrentInputState(keyCode, keySerial, pressed);
    }

    private void Update(ForgeInput input, PointerState pointer, TextInputEvent textInput, ScrollInputEvent scrollInput, ulong frameIndex)
    {
        _clock.Tick();
        ProcessRawKey(textInput);
        bool newWcpEvent = _wayControlInput.TryGetLatestEvent(_observedWcpSequence, out ulong wcpSequence, out long wcpTimestampMicroseconds);
        if (newWcpEvent) _observedWcpSequence = wcpSequence;
        if (_capturingControllerBinding is InputBinding capture && _wayControlInput.TryConsumeActivatedControl(out WcpControl control))
        {
            ActiveInputProfile().ControllerBindings[capture.Id] = FormatControllerControl(control);
            _capturingControllerBinding = null;
            MarkConfigDirty();
        }
        ForgeInput hostInput = MapInputFromPressedKeys(_config.Input) | MapInputFromController(_config.Input);
        ForgeInput controllerCoreInput = MapInputFromController(ActiveInputProfile());
        ForgeInput coreInput = MapInputFromPressedKeys(ActiveInputProfile()) | controllerCoreInput;
        _lastInput = coreInput;
        _pointer = pointer;
        _textInput = textInput;
        _scrollInput = scrollInput;
        _hostFrameIndex = frameIndex;
        HandleHostShortcuts(hostInput);

        _inputSource.Update(coreInput, controllerCoreInput, _wayControlInput.LeftX, _wayControlInput.LeftY);
        SyncExternalPointerState();
        if (!_paused || _stepRequested || _frameStore.Pixels.IsEmpty)
        {
            StepActiveCore();
            if (newWcpEvent)
                _wcpToFrameMilliseconds = Math.Max(0, (StopwatchMicroseconds() - wcpTimestampMicroseconds) / 1_000.0);
            _stepRequested = false;
        }

        SaveConfigIfDue();
        SyncAudioVolumeIfDue();
        SyncAudioStatusIfDue();
        _previousInput = hostInput;
    }

    private void Draw(int width, int height)
    {
        _ui.BeginFrame(_pointer, _previousPointer, _textInput, _scrollInput);
        _canvas.Clear(_ui.Theme.Panel.Colors.Panel);
        var layout = ForgeLayout.Calculate(width, height);
        if (_fullscreenTile is AppWindow fullscreen && !WindowState(fullscreen).IsOpen)
        {
            _fullscreenTile = null;
        }
        UpdateTileHoverFocus(layout);

        if (_fullscreenTile is not null)
        {
            DrawViewport(layout);
            DrawChildWindows(layout);
            return;
        }

        DrawChrome(layout);
        DrawToolbar(layout);
        DrawViewport(layout);
        if (layout.HasSidePanel)
        {
            DrawDebugPanel(layout);
        }
        DrawScaleToggles(layout);
        DrawStatusBar(layout);
        DrawChildWindows(layout);
        DrawGlobalTileBling(layout);
    }

    private void ProcessRawKey(TextInputEvent textInput)
    {
        if (textInput.Serial == 0 || textInput.Serial == _handledKeySerial)
        {
            return;
        }

        _handledKeySerial = textInput.Serial;
        if (!textInput.Pressed && textInput.KeyCode == AllKeysReleasedCode)
        {
            _pressedKeys.Clear();
            return;
        }

        if (textInput.Pressed)
        {
            _pressedKeys.Add(textInput.KeyCode);
            if (_capturingBinding is InputBinding binding && !IsReservedMappingKey(textInput.KeyCode))
            {
                SetInputBinding(binding, textInput.KeyCode);
                _capturingBinding = null;
            }
        }
        else
        {
            _pressedKeys.Remove(textInput.KeyCode);
        }
    }

    private ForgeInput MapInputFromPressedKeys(UiInputConfig inputConfig)
    {
        ForgeInput input = ForgeInput.None;
        foreach (InputBinding binding in InputBindings)
        {
            foreach (uint keyCode in BoundKeyCodes(binding.Id, inputConfig))
            {
                if (_pressedKeys.Contains(keyCode))
                {
                    input |= binding.Bit;
                    break;
                }
            }
        }
        return input;
    }

    private ForgeInput MapInputFromController(UiInputConfig inputConfig)
    {
        ForgeInput input = ForgeInput.None;
        foreach (InputBinding binding in InputBindings)
        {
            foreach (WcpControl control in BoundControllerControls(binding.Id, inputConfig))
            {
                if (_wayControlInput.IsActive(control))
                {
                    input |= binding.Bit;
                    break;
                }
            }
        }
        return input;
    }

    public void Dispose()
    {
        SaveConfig(force: false);
        _saturnCore.Dispose();
        _externalCore.Dispose();
        _externalCore2.Dispose();
        _externalCore3.Dispose();
        _wayControlInput.Dispose();
    }

    private void DrawChrome(ForgeLayout layout)
    {
        UiColors colors = _ui.Theme.Button.Colors;
        _canvas.FillRect(0, 0, layout.Width, TopBarHeight, colors.Surface);
        _canvas.FillRect(0, layout.Height - StatusBarHeight, layout.Width, StatusBarHeight, colors.Surface);
        if (layout.HasSidePanel)
        {
            _canvas.FillRect(layout.SidePanelX, TopBarHeight, SidePanelWidth, layout.Height - TopBarHeight - StatusBarHeight, colors.Panel);
        }

        _canvas.DrawRect(0, 0, layout.Width, layout.Height, colors.Border);
        _canvas.DrawLine(0, TopBarHeight, layout.Width, TopBarHeight, colors.Border);
        _canvas.DrawLine(0, layout.Height - StatusBarHeight - 1, layout.Width, layout.Height - StatusBarHeight - 1, colors.Border);
        if (layout.HasSidePanel)
        {
            _canvas.DrawLine(layout.SidePanelX - 1, TopBarHeight, layout.SidePanelX - 1, layout.Height - StatusBarHeight, colors.Border);
        }

        _ui.Text(12, 10, "WAYLANDFORGE", scale: 2);
        if (layout.Width >= 520)
        {
            _ui.Text(214, 11, "CPU UI / SHM / DOUBLE BUFFER", UiTextKind.Muted);
        }
        if (layout.Width >= 900)
        {
            _ui.Text(layout.Width - 174, 11, $"FRAME {_hostFrameIndex}", UiTextKind.Muted);
        }
    }

    private void DrawToolbar(ForgeLayout layout)
    {
        if (layout.Width < 720)
        {
            return;
        }

        var row = new UiRow(402, 6, 17, 6);
        string runLabel = _paused ? "RUN" : "PAUSE";
        row = row.Next(50, out RectI runRect);
        if (_ui.Button(runRect, runLabel, !_paused).Clicked)
        {
            _paused = !_paused;
        }

        row = row.Next(50, out RectI resetRect);
        if (_ui.Button(resetRect, "RESET").Clicked)
        {
            _core.Reset();
            StepActiveCore();
        }

        row = row.Next(42, out RectI stepRect);
        if (_ui.Button(stepRect, "STEP").Clicked)
        {
            _paused = true;
            _stepRequested = true;
        }

        row = row.Next(50, out RectI themeRect);
        if (_ui.Button(themeRect, _ui.Theme.Name).Clicked)
        {
            NextTheme();
        }

        row = row.Next(42, out RectI extRect);
        if (_ui.Button(new UiId("toolbar.ext"), extRect, "EXT", ReferenceEquals(_core, _externalCore)).Clicked)
        {
            ToggleExternalCore();
        }

        row = row.Next(50, out RectI ext2Rect);
        if (_ui.Button(new UiId("toolbar.ext2"), ext2Rect, "EXT2", ReferenceEquals(_core, _externalCore2)).Clicked)
        {
            ToggleExternalCore2();
        }

        row = row.Next(50, out RectI ext3Rect);
        if (_ui.Button(new UiId("toolbar.ext3"), ext3Rect, "EXT3", ReferenceEquals(_core, _externalCore3)).Clicked)
        {
            ToggleExternalCore3();
        }

        row = row.Next(50, out RectI coreRect);
        if (_ui.Button(new UiId("toolbar.core"), coreRect, "CORE", _viewportWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Viewport);
        }

        row = row.Next(42, out RectI romRect);
        if (_ui.Button(new UiId("toolbar.rom"), romRect, "ROM", _filePickerWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Rom);
        }

        row = row.Next(44, out RectI settingsRect);
        if (_ui.Button(new UiId("toolbar.settings"), settingsRect, "SET", _settingsWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Settings);
        }

        row = row.Next(60, out RectI styleRect);
        if (_ui.Button(new UiId("toolbar.style"), styleRect, "BLING", _styleWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Style);
        }

        row = row.Next(56, out RectI inputRect);
        if (_ui.Button(new UiId("toolbar.input"), inputRect, "INPUT", _inputWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Input);
        }

        DrawAudioVolume(layout);
    }

    private void DrawAudioVolume(ForgeLayout layout)
    {
        if (layout.Width < 1060)
        {
            return;
        }

        int x = layout.Width - 354;
        int y = 7;
        _ui.Text(x, y + 2, "VOL", UiTextKind.Muted);
        RectI sliderRect = new(x + 36, y, 116, 15);
        UiSliderResult slider = _ui.Slider(new UiId("toolbar.audio.volume"), sliderRect, _config.Audio.Volume, 0, 100);
        if (slider.Changed)
        {
            SetAudioVolume(slider.Value);
        }
        _ui.Text(x + 160, y + 2, _config.Audio.Volume.ToString("000", System.Globalization.CultureInfo.InvariantCulture), UiTextKind.Muted);
    }

    private void DrawViewport(ForgeLayout layout)
    {
        if (_fullscreenTile is AppWindow fullscreen && fullscreen != AppWindow.Viewport)
        {
            return;
        }
        if (_config.WindowMode == UiWindowMode.Tiled && !_viewportWindow.IsOpen)
        {
            return;
        }

        RectI area = _config.WindowMode == UiWindowMode.Tiled
            ? TiledWindowRect(layout, AppWindow.Viewport)
            : new RectI(layout.ViewAreaX, layout.ViewAreaY, layout.ViewAreaW, layout.ViewAreaH);
        if (_config.WindowMode == UiWindowMode.Tiled)
        {
            _viewportWindow.Rect = area;
            if (_pointer.IsInside && area.Contains(_pointer.X, _pointer.Y) && _pointer.LeftPressed && !_previousPointer.LeftPressed)
            {
                _focusedTile = AppWindow.Viewport;
            }
            uint border = _focusedTile == AppWindow.Viewport ? _ui.Theme.Button.Colors.BorderHot : _ui.Theme.Button.Colors.Border;
            _canvas.DrawRect(area.X, area.Y, area.Width, area.Height, border);
            area = new RectI(area.X + 1, area.Y + 1, Math.Max(1, area.Width - 2), Math.Max(1, area.Height - 2));
        }
        _viewport.Draw(_canvas, area, _frameStore.Pixels, _frameStore.Width, _frameStore.Height, _frameStore.StridePixels);

        RectI content = _viewport.ContentRect;
        if (content.Bottom + 18 < layout.Height - StatusBarHeight && area.Height >= 80)
        {
            _canvas.DrawText(content.X, content.Bottom + 8, $"EMULATOR VIEWPORT {_frameStore.Width}X{_frameStore.Height} {_viewport.ScaleMode.ToString().ToUpperInvariant()}", 0xff91a1ad);
        }
    }

    private void DrawDebugPanel(ForgeLayout layout)
    {
        RectI content = _ui.Panel(
            new RectI(layout.SidePanelX + 8, TopBarHeight + 8, SidePanelWidth - 16, layout.Height - TopBarHeight - StatusBarHeight - 16),
            "DEBUG");
        using (UiScrollArea scroll = _ui.BeginScrollArea(new UiId("debug.scroll"), content, 700))
        {
            var column = new UiColumn(scroll.Content.X, scroll.Content.Y, scroll.Content.Width, 5);

            if (_ui.Collapsible(new UiId("debug.host"), ref column, "HOST", 222, out RectI hostSection))
            {
                int x = hostSection.X;
                int y = hostSection.Y;
                DrawMetric(x, y, "BACKEND", "WAYLAND"); y += 18;
                DrawMetric(x, y, "BUFFER", "WL_SHM X2"); y += 18;
                DrawMetric(x, y, "FORMAT", "ARGB8888"); y += 18;
                DrawMetric(x, y, "CORE", CoreName()); y += 18;
                DrawMetric(x, y, "RUN", _paused ? "PAUSED" : "RUNNING"); y += 18;
                DrawMetric(x, y, "SOURCE", $"{_frameStore.Width}X{_frameStore.Height}"); y += 18;
                DrawMetric(x, y, "CFRAME", CoreFrameIndex().ToString()); y += 18;
                DrawMetric(x, y, "SCALE", _viewport.ScaleMode.ToString().ToUpperInvariant()); y += 18;
                DrawMetric(x, y, "FPS", _clock.FramesPerSecond.ToString("0.0")); y += 18;
                DrawMetric(x, y, "FRAME MS", _clock.FrameMilliseconds.ToString("0.0")); y += 18;
                DrawMetric(x, y, "HZ", (_clock.FrameMilliseconds > 0 ? 1000.0 / _clock.FrameMilliseconds : 0).ToString("0.0")); y += 18;
                DrawMetric(x, y, "DRAW MS", _clock.DrawMilliseconds.ToString("0.0")); y += 18;
            }

            if (ReferenceEquals(_core, _saturnCore))
            {
                if (_ui.Collapsible(new UiId("debug.saturn"), ref column, "CORE STATUS", 426, out RectI saturnSection))
                {
                    DrawSaturnCoreStatus(saturnSection);
                }
            }
            else if (_ui.Collapsible(new UiId("debug.external"), ref column, "EXT CORE", 302, out RectI externalSection))
            {
                int x = externalSection.X;
                int y = externalSection.Y;
                ExternalProcessCore external = ActiveExternalCore();
                DrawMetric(x, y, "STATUS", external.Status); y += 18;
                DrawMetric(x, y, "MODE", external.Mode.ToUpperInvariant()); y += 18;
                DrawMetric(x, y, "WFEX", external.ProtocolStatus); y += 18;
                DrawMetric(x, y, "LIMIT", external.ProtocolLimits); y += 18;
                DrawMetric(x, y, "TRANSPORT", external.ProtocolTransport); y += 18;
                DrawMetric(x, y, "RECORD", external.ProtocolFrameStatus); y += 18;
                DrawMetric(x, y, "MEDIA", external.ProtocolTiming); y += 18;
                DrawMetric(x, y, "SAVED", external.ProtocolCopySavings); y += 18;
                DrawMetric(x, y, "CMD", ExternalCommandLabel()); y += 18;
                DrawMetric(x, y, "FAULT", string.IsNullOrEmpty(_coreFault) ? "-" : TruncateMiddle(_coreFault, 18)); y += 20;
                RectI restartRect = new(x, y, 86, 18);
                if (_ui.Button(new UiId("external.restart"), restartRect, "RESTART").Clicked)
                {
                    RestartExternalCore();
                }
                y += 26;
                _ui.Text(x, y, "STDERR", UiTextKind.Muted); y += 14;
                foreach (string line in external.StderrTail.TakeLast(4))
                {
                    _ui.Text(x, y, TruncateMiddle(line, 22), UiTextKind.Muted);
                    y += 14;
                }
            }

            if (_ui.Collapsible(new UiId("debug.audio"), ref column, "AUDIO", 88, out RectI audioSection))
            {
                int x = audioSection.X;
                int y = audioSection.Y;
                DrawMetric(x, y, "VOL", _config.Audio.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)); y += 18;
                DrawMetric(x, y, "DAEMON", TruncateMiddle(_audioStatus, 22)); y += 18;
                DrawMetric(x, y, "SYNC", _lastSentAudioVolume == _config.Audio.Volume ? "OK" : "PENDING"); y += 18;
            }

            if (_ui.Collapsible(new UiId("debug.input"), ref column, "INPUT", 216, out RectI inputSection))
            {
                int x = inputSection.X;
                int y = inputSection.Y;
                UiInputConfig inputProfile = ActiveInputProfile();
                string profileLabel = ActiveInputProfileLabel();
                DrawMetric(x, y, "PTR", _pointer.IsInside ? $"{_pointer.X},{_pointer.Y}" : "OUT"); y += 18;
                DrawMetric(x, y, "MBTN", _pointer.Buttons.ToString().ToUpperInvariant()); y += 18;
                DrawMetric(x, y, "MAP", profileLabel); y += 20;
                DrawMetric(x, y, "WCP", _wayControlInput.Status); y += 18;
                DrawMetric(x, y, "WCP>FRAME", _wcpToFrameMilliseconds < 0 ? "-" : $"{_wcpToFrameMilliseconds:0.0} MS"); y += 18;
                DrawInputLamp(x, y, "UP", ForgeInput.Up, inputProfile); y += 16;
                DrawInputLamp(x, y, "DOWN", ForgeInput.Down, inputProfile); y += 16;
                DrawInputLamp(x, y, "LEFT", ForgeInput.Left, inputProfile); y += 16;
                DrawInputLamp(x, y, "RIGHT", ForgeInput.Right, inputProfile); y += 16;
                DrawInputLamp(x, y, "START", ForgeInput.Start, inputProfile); y += 16;
                DrawInputLamp(x, y, "A", ForgeInput.A, inputProfile); y += 16;
                DrawInputLamp(x, y, "B", ForgeInput.B, inputProfile); y += 16;
                DrawInputLamp(x, y, "C", ForgeInput.C, inputProfile); y += 16;
                DrawInputLamp(x, y, "X/Y/Z", ForgeInput.X | ForgeInput.Y | ForgeInput.Z, inputProfile);
            }

            if (_ui.Collapsible(new UiId("debug.style"), ref column, "STYLE", 136, out RectI styleSection))
            {
                int x = styleSection.X;
                int y = styleSection.Y;
                DrawMetric(x, y, "THEME", _ui.Theme.Name); y += 18;
                DrawMetric(x, y, "BORDER", _ui.Theme.Button.BorderThickness.ToString()); y += 18;
                DrawMetric(x, y, "HOT", _ui.Hot ?? "-"); y += 18;
                DrawMetric(x, y, "ACTIVE", _ui.Active ?? "-"); y += 18;
                DrawMetric(x, y, "FOCUS", _ui.Focused ?? "-"); y += 22;
                _ui.Text(x, y, "LABEL", UiTextKind.Muted); y += 12;
                _ui.TextBox(new UiId("style.label"), new RectI(x, y, styleSection.Width - 4, 18), "saturn", "label", new UiTextBoxOptions(MaxLength: 18)); y += 24;
                _ui.Text(x, y, "NUM", UiTextKind.Muted); y += 12;
                _ui.TextBox(new UiId("style.numeric"), new RectI(x, y, 72, 18), "320", "0", new UiTextBoxOptions(Numeric: true, MaxLength: 6));
                _canvas.FillRect(x + 112, y + 4, 18, 10, _ui.Theme.Button.Colors.Accent);
                _canvas.DrawRect(x + 112, y + 4, 18, 10, _ui.Theme.Button.Colors.BorderHot);
            }
        }

        _ui.Text(content.X, layout.Height - 78, "ESC IS INPUT", UiTextKind.Accent);
        _ui.Text(content.X, layout.Height - 60, "NO GTK QT SDL", UiTextKind.Muted);
    }

    private void DrawStatusBar(ForgeLayout layout)
    {
        string inputText = _lastInput == ForgeInput.None ? "INPUT: NONE" : $"INPUT: {_lastInput}";
        _ui.Text(12, layout.Height - 16, inputText.ToUpperInvariant());
        if (!string.IsNullOrEmpty(_romPath) && layout.Width >= 620)
        {
            _ui.Text(210, layout.Height - 16, TruncateMiddle(_romPath, 46), UiTextKind.Muted);
        }
        if (layout.Width >= 900)
        {
            _ui.Text(layout.Width - 610, layout.Height - 16, "F11 FULL TILE  SUPER+ARROWS FOCUS  SUPER+SHIFT+ARROWS SWAP", UiTextKind.Muted);
        }
    }

    private void DrawSaturnCoreStatus(RectI section)
    {
        SaturnCoreStatus status = _saturnCore.Status;
        int x = section.X;
        int y = section.Y;
        DrawMetric(x, y, "STATUS", status.HasRuntime ? "RUNNING" : "WAITING"); y += 18;
        DrawMetric(x, y, "BIOS", TruncateMiddle(status.BiosName, 18)); y += 18;
        DrawMetric(x, y, "FAULT", string.IsNullOrEmpty(status.Fault) ? "-" : TruncateMiddle(status.Fault, 18)); y += 18;
        DrawMetric(x, y, "FRAME", status.FrameIndex.ToString()); y += 18;
        DrawMetric(x, y, "INSTR", status.InstructionIndex.ToString()); y += 20;
        DrawMetric(x, y, "VIDEO", status.HasVideoFrame ? "VDP1" : "DIAG"); y += 20;

        _ui.Text(x, y, "SH2", UiTextKind.Muted); y += 14;
        DrawMetric(x, y, "M PC", FormatHex(status.MasterPc, 8)); y += 18;
        DrawMetric(x, y, "M SR", FormatHex(status.MasterSr, 8)); y += 18;
        DrawMetric(x, y, "S PC", FormatHex(status.SlavePc, 8)); y += 20;

        _ui.Text(x, y, "IRQ/SMPC", UiTextKind.Muted); y += 14;
        DrawMetric(x, y, "VBI", status.VBlankInCount.ToString()); y += 18;
        DrawMetric(x, y, "VBO", status.VBlankOutCount.ToString()); y += 18;
        DrawMetric(x, y, "SMPC", FormatHex(status.SmpcLastCommand, 2)); y += 18;
        DrawMetric(x, y, "SIRQ", status.SmpcInterruptCount.ToString()); y += 18;
        DrawMetric(x, y, "PAD", TruncateMiddle(status.Input.ToUpperInvariant(), 18)); y += 20;

        _ui.Text(x, y, "VDP", UiTextKind.Muted); y += 14;
        DrawVdpStatus(x, ref y, status.Vdp1);
        DrawVdpStatus(x, ref y, status.Vdp2);
        DrawVdpStatus(x, ref y, status.Cram);
        DrawVdpStatus(x, ref y, status.Vdp2Registers);
        y += 2;

        _ui.Text(x, y, "CD BLOCK", UiTextKind.Muted); y += 14;
        DrawMetric(x, y, "DISC", status.CdBlock.HasDisc ? "YES" : "NO"); y += 18;
        DrawMetric(x, y, "AUTH", $"{FormatHex(status.CdBlock.AuthenticationType, 2)} {(status.CdBlock.AuthStartupCompleted ? "OK" : "WAIT")}"); y += 18;
        DrawMetric(x, y, "CMD", FormatHex(status.CdBlock.LastCommand, 2)); y += 18;
        DrawMetric(x, y, "CR1", FormatHex(status.CdBlock.Cr1, 4)); y += 18;
        DrawMetric(x, y, "CR2", FormatHex(status.CdBlock.Cr2, 4)); y += 18;
        DrawMetric(x, y, "RCR1", FormatHex(status.CdBlock.ResponseCr1, 4)); y += 18;
        DrawMetric(x, y, "RCR2", FormatHex(status.CdBlock.ResponseCr2, 4)); y += 18;
    }

    private void DrawVdpStatus(int x, ref int y, VdpDebugStatus status)
    {
        DrawMetric(x, y, status.Label, status.WriteCount.ToString()); y += 18;
        DrawMetric(x, y, "LAST", status.LastWriteOffset is uint offset ? FormatHex(offset, 5) : "-"); y += 18;
    }

    private void DrawChildWindows(ForgeLayout layout)
    {
        HandleTileEdit(layout);
        AppWindow? inputWindow = CapturedInputWindow() ?? HitTestTopWindow();
        foreach (AppWindow window in _windowOrder.ToArray())
        {
            if (_fullscreenTile is AppWindow fullscreen && fullscreen != window)
            {
                continue;
            }
            bool inputEnabled = inputWindow is null || inputWindow == window;
            bool active = IsTopOpenWindow(window);
            switch (window)
            {
                case AppWindow.Rom:
                    DrawFilePicker(layout, active, inputEnabled);
                    break;
                case AppWindow.Settings:
                    DrawSettingsWindow(layout, active, inputEnabled);
                    break;
                case AppWindow.Style:
                    DrawStyleWindow(layout, active, inputEnabled);
                    break;
                case AppWindow.Input:
                    DrawInputWindow(layout, active, inputEnabled);
                    break;
            }
        }
        DrawTileResizePreview(layout);
        DrawTileDropPreview(layout);
    }

    private void DrawFilePicker(ForgeLayout layout, bool active, bool inputEnabled)
    {
        if (!_filePickerWindow.IsOpen)
        {
            return;
        }

        RectI preferredRect = PreferredWindowRect(layout, AppWindow.Rom);
        bool tileEdit = IsTileEditModifierDown();
        bool movable = _config.WindowMode != UiWindowMode.Tiled || tileEdit;
        if (!movable || (_config.WindowMode == UiWindowMode.Tiled && !_filePickerWindow.IsDragging))
        {
            _filePickerWindow.Rect = preferredRect;
        }
        UiWindowResult window = _ui.BeginWindow(new UiId("filepicker.window"), _filePickerWindow, preferredRect, ChildWindowBounds(layout), "ROM PICKER", active, inputEnabled, movable);
        if (window.Activated)
        {
            _focusedTile = AppWindow.Rom;
            BringToFront(AppWindow.Rom);
        }
        HandleTileWindowDrag(layout, AppWindow.Rom, window.Rect, window.Dragging);
        if (!window.IsOpen || window.Closed)
        {
            MarkConfigDirty();
            return;
        }

        DrawBlingWindowBorder(window.Rect, 0);

        using (_ui.PushInputEnabled(inputEnabled))
        {
            UiFilePickerResult result = _filePicker.Draw(_ui, window.Content, "");
            if (result.Accepted && result.SelectedPath is not null)
            {
                LoadSaturnDisc(result.SelectedPath);
                _filePickerWindow.IsOpen = false;
                MarkConfigDirty();
            }
            else if (result.Cancelled)
            {
                _filePickerWindow.IsOpen = false;
                MarkConfigDirty();
            }
        }
    }

    private void DrawSettingsWindow(ForgeLayout layout, bool active, bool inputEnabled)
    {
        if (!_settingsWindow.IsOpen)
        {
            return;
        }

        RectI preferredRect = PreferredWindowRect(layout, AppWindow.Settings);
        bool tileEdit = IsTileEditModifierDown();
        bool movable = _config.WindowMode != UiWindowMode.Tiled || tileEdit;
        if (!movable || (_config.WindowMode == UiWindowMode.Tiled && !_settingsWindow.IsDragging))
        {
            _settingsWindow.Rect = preferredRect;
        }
        UiWindowResult window = _ui.BeginWindow(new UiId("settings.window"), _settingsWindow, preferredRect, ChildWindowBounds(layout), "DISPLAY SETTINGS", active, inputEnabled, movable);
        if (window.Activated)
        {
            _focusedTile = AppWindow.Settings;
            BringToFront(AppWindow.Settings);
        }
        HandleTileWindowDrag(layout, AppWindow.Settings, window.Rect, window.Dragging);
        if (!window.IsOpen || window.Closed)
        {
            MarkConfigDirty();
            return;
        }

        using IDisposable inputScope = _ui.PushInputEnabled(inputEnabled);
        RectI content = window.Content;
        _ui.Panel(content);
        using UiScrollArea scroll = _ui.BeginScrollArea(new UiId("settings.scroll"), content, 330);
        var column = new UiColumn(scroll.Content.X + 10, scroll.Content.Y + 10, Math.Max(1, scroll.Content.Width - 20), 8);

        _ui.Text(column.X, column.NextY, "WINDOW MODE", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var modeRow = new UiRow(column.X, column.NextY, 18, 6);
        modeRow = modeRow.Next(62, out RectI tiledRect);
        if (_ui.ToggleButton(new UiId("settings.mode.tiled"), tiledRect, "TILE", _config.WindowMode == UiWindowMode.Tiled))
        {
            SetWindowMode(UiWindowMode.Tiled);
        }
        modeRow = modeRow.Next(62, out RectI floatRect);
        if (_ui.ToggleButton(new UiId("settings.mode.float"), floatRect, "FLOAT", _config.WindowMode == UiWindowMode.Floating))
        {
            SetWindowMode(UiWindowMode.Floating);
        }
        modeRow = modeRow.Next(58, out RectI mixedRect);
        if (_ui.ToggleButton(new UiId("settings.mode.mixed"), mixedRect, "MIX", _config.WindowMode == UiWindowMode.Mixed))
        {
            SetWindowMode(UiWindowMode.Mixed);
        }
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "SCALE", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var scaleRow = new UiRow(column.X, column.NextY, 18, 6);
        scaleRow = scaleRow.Next(58, out RectI fitRect);
        if (_ui.ToggleButton(new UiId("settings.scale.fit"), fitRect, "FIT", _viewport.ScaleMode == ViewportScaleMode.Fit))
        {
            SetScaleMode(ViewportScaleMode.Fit);
        }
        scaleRow = scaleRow.Next(58, out RectI intRect);
        if (_ui.ToggleButton(new UiId("settings.scale.int"), intRect, "INT", _viewport.ScaleMode == ViewportScaleMode.Integer))
        {
            SetScaleMode(ViewportScaleMode.Integer);
        }
        scaleRow = scaleRow.Next(58, out RectI stretchRect);
        if (_ui.ToggleButton(new UiId("settings.scale.str"), stretchRect, "STR", _viewport.ScaleMode == ViewportScaleMode.Stretch))
        {
            SetScaleMode(ViewportScaleMode.Stretch);
        }
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "THEME", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var themeRow = new UiRow(column.X, column.NextY, 18, 6);
        for (int i = 0; i < UiTheme.BuiltIns.Count; i++)
        {
            UiTheme theme = UiTheme.BuiltIns[i];
            themeRow = themeRow.Next(58, out RectI themeRect);
            if (_ui.ToggleButton(new UiId("settings.theme." + theme.Name), themeRect, theme.Name, _themeIndex == i))
            {
                SetTheme(i);
            }
        }
        column = column with { NextY = column.NextY + 36 };

        _ui.Text(column.X, column.NextY, "VISUAL", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var visualRow = new UiRow(column.X, column.NextY, 18, 6);
        visualRow = visualRow.Next(78, out RectI styleOpenRect);
        if (_ui.Button(new UiId("settings.style.open"), styleOpenRect, "STYLE", _styleWindow.IsOpen).Clicked)
        {
            ToggleWindow(AppWindow.Style);
        }
        visualRow = visualRow.Next(78, out RectI blingRect);
        if (_ui.ToggleButton(new UiId("settings.bling"), blingRect, "BLING", _config.Style.Bling))
        {
            SetBling(!_config.Style.Bling);
        }
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "HOST", UiTextKind.Muted); column = column with { NextY = column.NextY + 16 };
        DrawMetric(column.X, column.NextY, "FPS", _clock.FramesPerSecond.ToString("0.0")); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "FRAME MS", _clock.FrameMilliseconds.ToString("0.0")); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "DRAW MS", _clock.DrawMilliseconds.ToString("0.0")); column = column with { NextY = column.NextY + 28 };

        _ui.Text(column.X, column.NextY, "WINDOWS", UiTextKind.Muted); column = column with { NextY = column.NextY + 16 };
        DrawMetric(column.X, column.NextY, "MODE", _config.WindowMode.ToString().ToUpperInvariant()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "ACTIVE", _focusedTile.ToString().ToUpperInvariant()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "ROM", _romPath is null ? "NONE" : TruncateMiddle(_romPath, 28));
        DrawBlingWindowBorder(window.Rect, 11);
    }

    private void DrawStyleWindow(ForgeLayout layout, bool active, bool inputEnabled)
    {
        if (!_styleWindow.IsOpen)
        {
            return;
        }

        RectI preferredRect = PreferredWindowRect(layout, AppWindow.Style);
        bool tileEdit = IsTileEditModifierDown();
        bool movable = _config.WindowMode != UiWindowMode.Tiled || tileEdit;
        if (!movable || (_config.WindowMode == UiWindowMode.Tiled && !_styleWindow.IsDragging))
        {
            _styleWindow.Rect = preferredRect;
        }

        UiWindowResult window = _ui.BeginWindow(new UiId("style.window"), _styleWindow, preferredRect, ChildWindowBounds(layout), "STYLE EDITOR", active, inputEnabled, movable);
        if (window.Activated)
        {
            _focusedTile = AppWindow.Style;
            BringToFront(AppWindow.Style);
        }
        HandleTileWindowDrag(layout, AppWindow.Style, window.Rect, window.Dragging);
        if (!window.IsOpen || window.Closed)
        {
            MarkConfigDirty();
            return;
        }

        using IDisposable inputScope = _ui.PushInputEnabled(inputEnabled);
        RectI content = window.Content;
        _ui.Panel(content);
        using UiScrollArea scroll = _ui.BeginScrollArea(new UiId("style.scroll"), content, 380);
        var column = new UiColumn(scroll.Content.X + 10, scroll.Content.Y + 10, Math.Max(1, scroll.Content.Width - 20), 8);

        _ui.Text(column.X, column.NextY, "EFFECTS", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var effectRow = new UiRow(column.X, column.NextY, 18, 6);
        effectRow = effectRow.Next(74, out RectI blingRect);
        if (_ui.ToggleButton(new UiId("style.bling"), blingRect, "BLING", _config.Style.Bling))
        {
            SetBling(!_config.Style.Bling);
        }
        effectRow = effectRow.Next(98, out RectI rainbowRect);
        if (_ui.ToggleButton(new UiId("style.rainbow"), rainbowRect, "BORDER FX", _config.Style.RainbowBorders))
        {
            _config.Style.RainbowBorders = !_config.Style.RainbowBorders;
            MarkConfigDirty();
        }
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "BORDER EFFECT", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var effectModeRow = new UiRow(column.X, column.NextY, 18, 6);
        DrawBorderEffectToggle(ref effectModeRow, "still", "STILL");
        DrawBorderEffectToggle(ref effectModeRow, "glow", "GLOW");
        DrawBorderEffectToggle(ref effectModeRow, "flow", "FLOW");
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "BUTTON STYLE", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var styleRow = new UiRow(column.X, column.NextY, 18, 6);
        DrawButtonStyleToggle(ref styleRow, "flat", "FLAT");
        DrawButtonStyleToggle(ref styleRow, "edge", "EDGE");
        DrawButtonStyleToggle(ref styleRow, "loud", "LOUD");
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "EFFECT SPEED", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var speedRow = new UiRow(column.X, column.NextY, 18, 6);
        DrawLevelToggle(ref speedRow, "style.speed", 1, "SLOW", _config.Style.EffectSpeed, value => _config.Style.EffectSpeed = value);
        DrawLevelToggle(ref speedRow, "style.speed", 2, "CALM", _config.Style.EffectSpeed, value => _config.Style.EffectSpeed = value);
        DrawLevelToggle(ref speedRow, "style.speed", 3, "MOVE", _config.Style.EffectSpeed, value => _config.Style.EffectSpeed = value);
        DrawLevelToggle(ref speedRow, "style.speed", 4, "FAST", _config.Style.EffectSpeed, value => _config.Style.EffectSpeed = value);
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "EFFECT STRENGTH", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var strengthRow = new UiRow(column.X, column.NextY, 18, 6);
        DrawLevelToggle(ref strengthRow, "style.strength", 1, "LOW", _config.Style.EffectStrength, value => _config.Style.EffectStrength = value);
        DrawLevelToggle(ref strengthRow, "style.strength", 2, "MID", _config.Style.EffectStrength, value => _config.Style.EffectStrength = value);
        DrawLevelToggle(ref strengthRow, "style.strength", 3, "HIGH", _config.Style.EffectStrength, value => _config.Style.EffectStrength = value);
        DrawLevelToggle(ref strengthRow, "style.strength", 4, "LOUD", _config.Style.EffectStrength, value => _config.Style.EffectStrength = value);
        column = column with { NextY = column.NextY + 34 };

        _ui.Text(column.X, column.NextY, "BORDER", UiTextKind.Muted); column = column with { NextY = column.NextY + 14 };
        var borderRow = new UiRow(column.X, column.NextY, 18, 6);
        for (int thickness = 1; thickness <= 4; thickness++)
        {
            borderRow = borderRow.Next(38, out RectI borderRect);
            if (_ui.ToggleButton(new UiId("style.border." + thickness), borderRect, thickness.ToString(), _config.Style.BorderThickness == thickness))
            {
                _config.Style.BorderThickness = thickness;
                MarkConfigDirty();
            }
        }
        column = column with { NextY = column.NextY + 38 };

        _ui.Text(column.X, column.NextY, "PREVIEW", UiTextKind.Muted); column = column with { NextY = column.NextY + 16 };
        DrawButtonPreview(new RectI(column.X, column.NextY, Math.Min(210, column.Width), 52));
        column = column with { NextY = column.NextY + 64 };

        DrawMetric(column.X, column.NextY, "TOML", "[ui]"); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "BLING", _config.Style.Bling.ToString().ToLowerInvariant()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "EFFECT", _config.Style.BorderEffect.ToUpperInvariant()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "SPEED", _config.Style.EffectSpeed.ToString()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "POWER", _config.Style.EffectStrength.ToString());

        DrawBlingWindowBorder(window.Rect, 23);
    }

    private void DrawInputWindow(ForgeLayout layout, bool active, bool inputEnabled)
    {
        if (!_inputWindow.IsOpen)
        {
            return;
        }

        RectI preferredRect = PreferredWindowRect(layout, AppWindow.Input);
        bool tileEdit = IsTileEditModifierDown();
        bool movable = _config.WindowMode != UiWindowMode.Tiled || tileEdit;
        if (!movable || (_config.WindowMode == UiWindowMode.Tiled && !_inputWindow.IsDragging))
        {
            _inputWindow.Rect = preferredRect;
        }

        UiWindowResult window = _ui.BeginWindow(new UiId("input.window"), _inputWindow, preferredRect, ChildWindowBounds(layout), "INPUT MAPPER", active, inputEnabled, movable);
        if (window.Activated)
        {
            _focusedTile = AppWindow.Input;
            BringToFront(AppWindow.Input);
        }
        HandleTileWindowDrag(layout, AppWindow.Input, window.Rect, window.Dragging);
        if (!window.IsOpen || window.Closed)
        {
            _capturingBinding = null;
            _capturingControllerBinding = null;
            MarkConfigDirty();
            return;
        }

        using IDisposable inputScope = _ui.PushInputEnabled(inputEnabled);
        RectI content = window.Content;
        _ui.Panel(content);
        int contentHeight = 140 + InputBindings.Length * 24;
        using UiScrollArea scroll = _ui.BeginScrollArea(new UiId("input.scroll"), content, contentHeight);
        var column = new UiColumn(scroll.Content.X + 10, scroll.Content.Y + 10, Math.Max(1, scroll.Content.Width - 20), 6);

        UiInputConfig inputProfile = ActiveInputProfile();
        string profileLabel = ActiveInputProfileLabel();

        _ui.Text(column.X, column.NextY, "PROFILE", UiTextKind.Muted);
        _ui.Text(column.X + 72, column.NextY, profileLabel, UiTextKind.Accent);
        column = column with { NextY = column.NextY + 18 };
        _ui.Text(column.X, column.NextY, "KEYBOARD + WCP GAMEPAD FEED THE SAME CORE ACTIONS.", UiTextKind.Muted);
        column = column with { NextY = column.NextY + 24 };

        _ui.Text(column.X, column.NextY, "WCP", UiTextKind.Muted);
        _ui.Text(column.X + 72, column.NextY, TruncateMiddle(_wayControlInput.DeviceSummary, Math.Max(12, (column.Width - 80) / 6)), UiTextKind.Accent);
        column = column with { NextY = column.NextY + 18 };

        int actionWidth = Math.Min(104, Math.Max(58, column.Width / 5));
        int laneWidth = Math.Max(132, (column.Width - actionWidth - 8) / 2);
        int keyLaneX = column.X + actionWidth + 8;
        int controllerLaneX = keyLaneX + laneWidth;
        _ui.Text(column.X, column.NextY, "ACTION", UiTextKind.Muted);
        _ui.Text(keyLaneX, column.NextY, "KEY", UiTextKind.Muted);
        _ui.Text(controllerLaneX, column.NextY, "WCP CONTROL", UiTextKind.Muted);
        column = column with { NextY = column.NextY + 16 };

        foreach (InputBinding binding in InputBindings)
        {
            bool capturingKey = _capturingBinding?.Id == binding.Id;
            bool capturingController = _capturingControllerBinding?.Id == binding.Id;
            bool activeBinding = (_lastInput & binding.Bit) != 0;
            RectI rowRect = new(column.X, column.NextY - 2, Math.Max(1, column.Width - 8), 20);
            uint border = capturingKey || capturingController ? _ui.Theme.Button.Colors.Accent : activeBinding ? _ui.Theme.Button.Colors.BorderHot : _ui.Theme.Button.Colors.Border;
            _canvas.DrawRect(rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, border);
            _ui.Text(column.X + 6, column.NextY + 3, InputBindingLabel(binding), activeBinding ? UiTextKind.Accent : UiTextKind.Normal);
            int keyButtonX = keyLaneX + laneWidth - 96;
            int controllerButtonX = controllerLaneX + laneWidth - 96;
            int keyChars = Math.Max(5, (keyButtonX - keyLaneX - 8) / 6);
            int controllerChars = Math.Max(5, (controllerButtonX - controllerLaneX - 8) / 6);
            string keyText = capturingKey ? "PRESS KEY" : TruncateMiddle(BindingDisplay(binding.Id, inputProfile), keyChars);
            string controllerText = capturingController ? "PRESS CONTROL" : TruncateMiddle(ControllerBindingDisplay(binding.Id, inputProfile), controllerChars);
            _ui.Text(keyLaneX, column.NextY + 3, keyText, capturingKey ? UiTextKind.Accent : UiTextKind.Muted);
            _ui.Text(controllerLaneX, column.NextY + 3, controllerText, capturingController ? UiTextKind.Accent : UiTextKind.Muted);

            if (_ui.Button(new UiId("input.map." + binding.Id), new RectI(keyButtonX, column.NextY, 40, 17), capturingKey ? "..." : "MAP", capturingKey).Clicked)
            {
                _capturingBinding = capturingKey ? null : binding;
                _capturingControllerBinding = null;
            }
            if (_ui.Button(new UiId("input.clear." + binding.Id), new RectI(keyButtonX + 44, column.NextY, 48, 17), "CLEAR").Clicked)
            {
                inputProfile.Bindings[binding.Id] = string.Empty;
                if (_capturingBinding?.Id == binding.Id)
                {
                    _capturingBinding = null;
                }
                MarkConfigDirty();
            }
            if (_ui.Button(new UiId("controller.map." + binding.Id), new RectI(controllerButtonX, column.NextY, 40, 17), capturingController ? "..." : "MAP", capturingController).Clicked)
            {
                _capturingControllerBinding = capturingController ? null : binding;
                _capturingBinding = null;
                if (!capturingController) _wayControlInput.BeginCapture();
            }
            if (_ui.Button(new UiId("controller.clear." + binding.Id), new RectI(controllerButtonX + 44, column.NextY, 48, 17), "CLEAR").Clicked)
            {
                inputProfile.ControllerBindings[binding.Id] = string.Empty;
                if (_capturingControllerBinding?.Id == binding.Id)
                    _capturingControllerBinding = null;
                MarkConfigDirty();
            }
            column = column with { NextY = column.NextY + 24 };
        }

        column = column with { NextY = column.NextY + 8 };
        _ui.Text(column.X, column.NextY, "TOML", UiTextKind.Muted);
        _ui.Text(column.X + 72, column.NextY, "CONFIG/WAYLANDFORGE.UI.LOCAL.TOML", UiTextKind.Muted);
        DrawBlingWindowBorder(window.Rect, 31);
    }

    private void DrawButtonStyleToggle(ref UiRow row, string value, string label)
    {
        row = row.Next(58, out RectI rect);
        if (_ui.ToggleButton(new UiId("style.button." + value), rect, label, string.Equals(_config.Style.ButtonStyle, value, StringComparison.OrdinalIgnoreCase)))
        {
            _config.Style.ButtonStyle = value;
            MarkConfigDirty();
        }
    }

    private void DrawBorderEffectToggle(ref UiRow row, string value, string label)
    {
        row = row.Next(58, out RectI rect);
        if (_ui.ToggleButton(new UiId("style.effect." + value), rect, label, string.Equals(_config.Style.BorderEffect, value, StringComparison.OrdinalIgnoreCase)))
        {
            _config.Style.BorderEffect = value;
            MarkConfigDirty();
        }
    }

    private void DrawLevelToggle(ref UiRow row, string idPrefix, int value, string label, int current, Action<int> set)
    {
        row = row.Next(50, out RectI rect);
        if (_ui.ToggleButton(new UiId(idPrefix + "." + value), rect, label, current == value))
        {
            set(value);
            MarkConfigDirty();
        }
    }

    private void DrawButtonPreview(RectI rect)
    {
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, _ui.Theme.Button.Colors.Border);
        RectI a = new(rect.X + 10, rect.Y + 14, 72, 24);
        RectI b = new(a.Right + 10, a.Y, 88, 24);
        DrawStyledButtonPreview(a, "NORMAL", false);
        DrawStyledButtonPreview(b, "ACTIVE", true);
    }

    private void DrawStyledButtonPreview(RectI rect, string label, bool active)
    {
        UiColors colors = _ui.Theme.Button.Colors;
        uint surface = active ? colors.Accent : colors.Surface;
        if (string.Equals(_config.Style.ButtonStyle, "loud", StringComparison.OrdinalIgnoreCase))
        {
            surface = active ? SoftFadeColor(2, colors.SurfaceActive, 0.22) : 0xff263746;
        }
        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, surface);
        int thickness = Math.Clamp(_config.Style.BorderThickness, 1, 4);
        for (int i = 0; i < thickness; i++)
        {
            uint border = string.Equals(_config.Style.ButtonStyle, "loud", StringComparison.OrdinalIgnoreCase) ? SoftFadeColor(0, colors.BorderHot, 0.28) : colors.BorderHot;
            _canvas.DrawRect(rect.X + i, rect.Y + i, rect.Width - i * 2, rect.Height - i * 2, border);
        }
        if (string.Equals(_config.Style.ButtonStyle, "edge", StringComparison.OrdinalIgnoreCase))
        {
            _canvas.DrawLine(rect.X + 2, rect.Bottom - 3, rect.Right - 3, rect.Bottom - 3, colors.Accent);
        }
        _canvas.DrawText(rect.X + 8, rect.Y + 8, label, colors.Text);
    }

    private void DrawBlingWindowBorder(RectI rect, int seed)
    {
        if (!_config.Style.Bling || !_config.Style.RainbowBorders)
        {
            return;
        }

        string effect = _config.Style.BorderEffect.Trim().ToLowerInvariant();
        if (effect == "flow")
        {
            DrawFlowBorder(rect, seed, animated: true);
        }
        else if (effect == "still")
        {
            DrawFlowBorder(rect, seed, animated: false);
        }
        else
        {
            DrawGlowBorder(rect, seed);
        }
    }

    private void DrawGlobalTileBling(ForgeLayout layout)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !_config.Style.Bling || !_config.Style.RainbowBorders)
        {
            return;
        }

        foreach (AppWindow window in OpenTiledWindows())
        {
            if (_fullscreenTile is AppWindow fullscreen && fullscreen != window)
            {
                continue;
            }
            RectI rect = TiledWindowRect(layout, window);
            DrawBlingWindowBorder(rect, 97 + (int)window * 13);
        }
    }

    private void DrawGlowBorder(RectI rect, int seed)
    {
        uint baseColor = _ui.Theme.Button.Colors.BorderHot;
        int thickness = Math.Clamp(_config.Style.BorderThickness, 1, 4);
        double speed = EffectSpeedFactor();
        double strength = EffectStrengthFactor();
        double breathe = (0.08 + 0.035 * strength) + (0.025 * strength) * (Math.Sin((((double)_hostFrameIndex * speed) + seed * 17.0) / 1440.0) + 1.0) * 0.5;
        uint color = WithAlpha(SoftFadeColor(seed, baseColor, 0.16 + 0.10 * strength), (int)Math.Round(70 + 28 * strength));
        uint haze = WithAlpha(SoftFadeColor(seed + 3, baseColor, breathe), (int)Math.Round(20 + 18 * strength));

        for (int i = thickness + 3; i >= thickness; i--)
        {
            RectI expanded = Expand(rect, i);
            _canvas.BlendRect(expanded.X, expanded.Y, expanded.Width, 1, haze);
            _canvas.BlendRect(expanded.X, expanded.Bottom - 1, expanded.Width, 1, haze);
            _canvas.BlendRect(expanded.X, expanded.Y, 1, expanded.Height, haze);
            _canvas.BlendRect(expanded.Right - 1, expanded.Y, 1, expanded.Height, haze);
        }

        for (int i = 0; i < thickness + 1; i++)
        {
            RectI expanded = Expand(rect, i + 1);
            _canvas.BlendRect(expanded.X, expanded.Y, expanded.Width, 1, color);
            _canvas.BlendRect(expanded.X, expanded.Bottom - 1, expanded.Width, 1, color);
            _canvas.BlendRect(expanded.X, expanded.Y, 1, expanded.Height, color);
            _canvas.BlendRect(expanded.Right - 1, expanded.Y, 1, expanded.Height, color);
        }
    }

    private void DrawFlowBorder(RectI rect, int seed, bool animated)
    {
        int thickness = Math.Clamp(_config.Style.BorderThickness, 1, 4) + 1;
        int segment = 28;
        uint baseColor = _ui.Theme.Button.Colors.BorderHot;
        RectI expanded = Expand(rect, thickness);
        int perimeter = Math.Max(1, expanded.Width * 2 + expanded.Height * 2 - 4);
        double phase = animated ? (_hostFrameIndex * EffectSpeedFactor() / 7200.0) : 0.0;
        phase += seed * 0.013;

        for (int layer = 0; layer < thickness; layer++)
        {
            RectI r = Expand(rect, layer + 1);
            int alpha = Math.Max(28, (int)Math.Round(66 + 44 * EffectStrengthFactor()) - layer * 20);
            DrawFlowEdge(r.X, r.Y, r.Width, 1, 0, perimeter, segment, phase, baseColor, alpha);
            DrawFlowEdge(r.Right - 1, r.Y, 1, r.Height, r.Width, perimeter, segment, phase, baseColor, alpha);
            DrawFlowEdge(r.X, r.Bottom - 1, r.Width, 1, r.Width + r.Height, perimeter, segment, phase, baseColor, alpha, reverse: true);
            DrawFlowEdge(r.X, r.Y, 1, r.Height, r.Width * 2 + r.Height, perimeter, segment, phase, baseColor, alpha, reverse: true);
        }
    }

    private void DrawFlowEdge(int x, int y, int width, int height, int distanceStart, int perimeter, int segment, double phase, uint baseColor, int alpha, bool reverse = false)
    {
        int length = Math.Max(width, height);
        for (int pos = 0; pos < length; pos += segment)
        {
            int run = Math.Min(segment, length - pos);
            int distance = distanceStart + (reverse ? length - pos : pos);
            double progress = ((distance / (double)perimeter) + phase) % 1.0;
            uint color = WithAlpha(GradientBorderColor(progress, baseColor), alpha);
            if (width >= height)
            {
                _canvas.BlendRect(x + pos, y, run, height, color);
            }
            else
            {
                _canvas.BlendRect(x, y + pos, width, run, color);
            }
        }
    }

    private uint SoftFadeColor(int offset, uint baseColor, double strength)
    {
        double t = (((double)_hostFrameIndex * EffectSpeedFactor() / 5400.0) + offset * 0.04) % 1.0;
        uint faded = GradientBorderColor(t, baseColor);
        return LerpColor(baseColor, faded, Math.Clamp(strength, 0.0, 1.0));
    }

    private uint GradientBorderColor(double progress, uint baseColor)
    {
        ReadOnlySpan<uint> colors = stackalloc uint[] { 0xff6f8fb5, 0xff7f77b8, 0xff9b6f9f, 0xff6faaa0, 0xffb0916a };
        double scaled = PositiveModulo(progress, 1.0) * colors.Length;
        int index = (int)Math.Floor(scaled) % colors.Length;
        int next = (index + 1) % colors.Length;
        double local = SmoothStep(scaled - index);
        return LerpColor(baseColor, LerpColor(colors[index], colors[next], local), 0.52);
    }

    private double EffectSpeedFactor()
    {
        return _config.Style.EffectSpeed switch
        {
            2 => 0.65,
            3 => 1.0,
            4 => 1.6,
            _ => 0.32,
        };
    }

    private double EffectStrengthFactor()
    {
        return _config.Style.EffectStrength switch
        {
            2 => 0.7,
            3 => 1.0,
            4 => 1.35,
            _ => 0.38,
        };
    }

    private static double SmoothStep(double value)
    {
        value = Math.Clamp(value, 0.0, 1.0);
        return value * value * (3.0 - 2.0 * value);
    }

    private static double PositiveModulo(double value, double divisor)
    {
        double result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static RectI Expand(RectI rect, int amount)
    {
        return new RectI(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
    }

    private static uint WithAlpha(uint color, int alpha)
    {
        return ((uint)Math.Clamp(alpha, 0, 255) << 24) | (color & 0x00ffffff);
    }

    private static double DistanceToRect(int x, int y, RectI rect)
    {
        int dx = Math.Max(Math.Max(rect.X - x, 0), x - rect.Right);
        int dy = Math.Max(Math.Max(rect.Y - y, 0), y - rect.Bottom);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static uint LerpColor(uint from, uint to, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        int a = LerpChannel((from >> 24) & 0xff, (to >> 24) & 0xff, amount);
        int r = LerpChannel((from >> 16) & 0xff, (to >> 16) & 0xff, amount);
        int g = LerpChannel((from >> 8) & 0xff, (to >> 8) & 0xff, amount);
        int b = LerpChannel(from & 0xff, to & 0xff, amount);
        return (uint)((a << 24) | (r << 16) | (g << 8) | b);
    }

    private static int LerpChannel(uint from, uint to, double amount)
    {
        return (int)Math.Round(from + (to - from) * amount);
    }



    private void HandleTileEdit(ForgeLayout layout)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !IsTileEditModifierDown())
        {
            _resizingTileSplit = false;
            return;
        }

        AppWindow[] open = OpenTiledWindows();
        if (open.Length < 2)
        {
            _resizingTileSplit = false;
            return;
        }

        RectI work = TileWorkArea(layout);
        if (_pointer.IsInside && _pointer.LeftPressed && !_previousPointer.LeftPressed)
        {
            _tileResizeHandles.Clear();
            _tileResizeHandles.AddRange(HitTileTreeSplitters(work, open));
            if (_tileResizeHandles.Count > 0)
            {
                _resizingTileSplit = true;
                _tileResizeStartX = _pointer.X;
                _tileResizeStartY = _pointer.Y;
            }
        }

        if (!_pointer.LeftPressed)
        {
            _resizingTileSplit = false;
            _tileResizeHandles.Clear();
        }

        if (!_resizingTileSplit)
        {
            return;
        }

        bool changed = false;
        foreach (TileResizeHandle handle in _tileResizeHandles)
        {
            int delta = handle.Vertical ? _pointer.X - _tileResizeStartX : _pointer.Y - _tileResizeStartY;
            double newRatio = Math.Clamp(handle.StartRatio + delta / (double)handle.Span, 0.12, 0.88);
            changed |= UpdateTileRootRatio(handle.Path, newRatio);
        }

        if (changed)
        {
            MarkConfigDirty();
        }
    }

    private List<TileResizeHandle> HitTileTreeSplitters(RectI work, AppWindow[] open)
    {
        var candidates = new List<TileResizeCandidate>();
        TileNode? root = ParseTileRoot(_config.Layout.Root);
        if (root is null)
        {
            return [];
        }

        CollectTileTreeSplitters(root, work, open.ToHashSet(), string.Empty, candidates);
        List<TileResizeCandidate> directHits = candidates
            .Where(candidate => Expand(candidate.Divider, 4).Contains(_pointer.X, _pointer.Y))
            .ToList();
        if (directHits.Count == 0)
        {
            return [];
        }

        const int cornerReach = 28;
        bool hasVertical = directHits.Any(candidate => candidate.Handle.Vertical);
        bool hasHorizontal = directHits.Any(candidate => !candidate.Handle.Vertical);
        foreach (TileResizeCandidate candidate in candidates)
        {
            if ((candidate.Handle.Vertical && hasVertical) || (!candidate.Handle.Vertical && hasHorizontal))
            {
                continue;
            }

            if (DistanceToRect(_pointer.X, _pointer.Y, candidate.Divider) <= cornerReach)
            {
                directHits.Add(candidate);
            }
        }

        return directHits
            .Select(candidate => candidate.Handle)
            .GroupBy(handle => handle.Path)
            .Select(group => group.First())
            .ToList();
    }

    private void CollectTileTreeSplitters(TileNode node, RectI rect, HashSet<AppWindow> open, string path, List<TileResizeCandidate> candidates)
    {
        const int gap = 8;
        const int grip = 10;
        if (node.Window is not null || node.First is null || node.Second is null)
        {
            return;
        }

        bool firstActive = node.First.ContainsAny(open);
        bool secondActive = node.Second.ContainsAny(open);
        if (!firstActive && !secondActive)
        {
            return;
        }
        if (firstActive && !secondActive)
        {
            CollectTileTreeSplitters(node.First, rect, open, path + "0", candidates);
            return;
        }
        if (!firstActive && secondActive)
        {
            CollectTileTreeSplitters(node.Second, rect, open, path + "1", candidates);
            return;
        }

        double nodeRatio = Math.Clamp(node.Ratio, 0.12, 0.88);
        RectI first;
        RectI second;
        RectI divider;
        if (node.Axis == TileSplitAxis.Horizontal)
        {
            int firstWidth = Math.Clamp((int)Math.Round((rect.Width - gap) * nodeRatio), 120, Math.Max(120, rect.Width - gap - 120));
            first = new RectI(rect.X, rect.Y, firstWidth, rect.Height);
            second = new RectI(rect.X + firstWidth + gap, rect.Y, Math.Max(1, rect.Width - firstWidth - gap), rect.Height);
            divider = new RectI(first.Right - grip / 2, rect.Y, Math.Max(grip, second.X - first.Right + grip), rect.Height);
            candidates.Add(new TileResizeCandidate(
                new TileResizeHandle(path, Vertical: true, nodeRatio, Math.Max(1, rect.Width - gap)),
                divider));
        }
        else
        {
            int firstHeight = Math.Clamp((int)Math.Round((rect.Height - gap) * nodeRatio), 100, Math.Max(100, rect.Height - gap - 100));
            first = new RectI(rect.X, rect.Y, rect.Width, firstHeight);
            second = new RectI(rect.X, rect.Y + firstHeight + gap, rect.Width, Math.Max(1, rect.Height - firstHeight - gap));
            divider = new RectI(rect.X, first.Bottom - grip / 2, rect.Width, Math.Max(grip, second.Y - first.Bottom + grip));
            candidates.Add(new TileResizeCandidate(
                new TileResizeHandle(path, Vertical: false, nodeRatio, Math.Max(1, rect.Height - gap)),
                divider));
        }

        CollectTileTreeSplitters(node.First, first, open, path + "0", candidates);
        CollectTileTreeSplitters(node.Second, second, open, path + "1", candidates);
    }

    private bool UpdateTileRootRatio(string path, double ratio)
    {
        TileNode? root = ParseTileRoot(_config.Layout.Root);
        if (root is null || !UpdateTileRootRatio(root, path, 0, ratio))
        {
            return false;
        }
        _config.Layout.Root = FormatTileRoot(root);
        return true;
    }

    private static bool UpdateTileRootRatio(TileNode node, string path, int depth, double ratio)
    {
        if (depth == path.Length)
        {
            if (node.Window is not null)
            {
                return false;
            }
            node.Ratio = ratio;
            return true;
        }

        TileNode? next = path[depth] == '0' ? node.First : node.Second;
        return next is not null && UpdateTileRootRatio(next, path, depth + 1, ratio);
    }

    private bool IsTileEditModifierDown()
    {
        return (_lastInput & (ForgeInput.Super | ForgeInput.Shift)) == (ForgeInput.Super | ForgeInput.Shift);
    }

    private void UpdateTileHoverFocus(ForgeLayout layout)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !_pointer.IsInside || _pointer.LeftPressed || _resizingTileSplit || _tileDragWindow is not null)
        {
            return;
        }

        AppWindow? hovered = HitTestTile(layout);
        if (hovered is AppWindow window)
        {
            _focusedTile = window;
        }
    }

    private AppWindow? HitTestTile(ForgeLayout layout)
    {
        if (_fullscreenTile is AppWindow fullscreen && WindowState(fullscreen).IsOpen)
        {
            return TiledWindowRect(layout, fullscreen).Contains(_pointer.X, _pointer.Y) ? fullscreen : null;
        }

        foreach (AppWindow window in OpenTiledWindows().Reverse())
        {
            if (TiledWindowRect(layout, window).Contains(_pointer.X, _pointer.Y))
            {
                return window;
            }
        }

        return null;
    }

    private RectI PreferredWindowRect(ForgeLayout layout, AppWindow window)
    {
        if (_config.WindowMode == UiWindowMode.Tiled)
        {
            return TiledWindowRect(layout, window);
        }

        return FloatingWindowRect(layout, window);
    }

    private RectI FloatingWindowRect(ForgeLayout layout, AppWindow window)
    {
        return window switch
        {
            AppWindow.Rom => new RectI(
                (layout.Width - Math.Min(980, Math.Max(360, layout.Width - (layout.HasSidePanel ? SidePanelWidth + 80 : 48)))) / 2,
                (layout.Height - Math.Min(680, Math.Max(320, layout.Height - 96))) / 2,
                Math.Min(980, Math.Max(360, layout.Width - (layout.HasSidePanel ? SidePanelWidth + 80 : 48))),
                Math.Min(680, Math.Max(320, layout.Height - 96))),
            AppWindow.Style => new RectI(
                Math.Max(16, layout.Width - Math.Min(360, Math.Max(300, layout.Width - 72)) - (layout.HasSidePanel ? SidePanelWidth + 28 : 28)),
                TopBarHeight + 40,
                Math.Min(360, Math.Max(300, layout.Width - 72)),
                Math.Min(360, Math.Max(280, layout.Height - 112))),
            AppWindow.Settings => new RectI(
                Math.Max(16, layout.Width - Math.Min(360, Math.Max(300, layout.Width - 72)) - (layout.HasSidePanel ? SidePanelWidth + 28 : 28)),
                TopBarHeight + 40,
                Math.Min(360, Math.Max(300, layout.Width - 72)),
                Math.Min(330, Math.Max(260, layout.Height - 112))),
            AppWindow.Input => new RectI(
                Math.Max(16, layout.Width - Math.Min(420, Math.Max(320, layout.Width - 72)) - (layout.HasSidePanel ? SidePanelWidth + 28 : 28)),
                TopBarHeight + 68,
                Math.Min(420, Math.Max(320, layout.Width - 72)),
                Math.Min(520, Math.Max(300, layout.Height - 112))),
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };
    }

    private RectI TiledWindowRect(ForgeLayout layout, AppWindow window)
    {
        if (_fullscreenTile == window && WindowState(window).IsOpen)
        {
            return FullWindowRect(layout);
        }
        RectI work = TileWorkArea(layout);
        AppWindow[] open = OpenTiledWindows();
        if (!open.Contains(window))
        {
            return work;
        }

        Dictionary<AppWindow, RectI> rects = ComputeTileRects(work, open);
        return rects.TryGetValue(window, out RectI rect) ? rect : LegacyTiledWindowRect(work, open, window);
    }

    private Dictionary<AppWindow, RectI> ComputeTileRects(RectI work, AppWindow[] open)
    {
        var rects = new Dictionary<AppWindow, RectI>();
        TileNode? root = ParseTileRoot(_config.Layout.Root);
        if (root is null)
        {
            return rects;
        }

        LayoutTileNode(root, work, open.ToHashSet(), rects);
        return rects;
    }

    private RectI LegacyTiledWindowRect(RectI work, AppWindow[] open, AppWindow window)
    {
        int index = Array.IndexOf(open, window);
        const int gap = 8;
        if (index < 0 || open.Length == 1)
        {
            return work;
        }

        if (open.Length == 2)
        {
            int defaultPrimaryWidth = open.Contains(AppWindow.Rom) ? (int)Math.Round(work.Width * 0.64) : (work.Width - gap) / 2;
            int primaryWidth = TilePrimaryWidth(open[0], work, defaultPrimaryWidth);
            return index == 0
                ? new RectI(work.X, work.Y, primaryWidth, work.Height)
                : new RectI(work.X + primaryWidth + gap, work.Y, Math.Max(220, work.Width - primaryWidth - gap), work.Height);
        }

        int primaryHeight = TilePrimaryHeight(open[0], work, (int)Math.Round(work.Height * 0.60));
        if (index == 0)
        {
            return new RectI(work.X, work.Y, work.Width, primaryHeight);
        }

        RectI rest = new(work.X, work.Y + primaryHeight + gap, work.Width, Math.Max(160, work.Height - primaryHeight - gap));
        if (open.Length == 3)
        {
            return StackPairTileRect(rest, index - 1, open[1], gap);
        }
        return GridTileRect(rest, index - 1, open.Length - 1, gap);
    }

    private static bool LayoutTileNode(TileNode node, RectI rect, HashSet<AppWindow> open, Dictionary<AppWindow, RectI> rects)
    {
        const int gap = 8;
        if (node.Window is AppWindow leaf)
        {
            if (!open.Contains(leaf))
            {
                return false;
            }
            rects[leaf] = rect;
            return true;
        }

        if (node.First is null || node.Second is null)
        {
            return false;
        }

        bool firstActive = node.First.ContainsAny(open);
        bool secondActive = node.Second.ContainsAny(open);
        if (!firstActive && !secondActive)
        {
            return false;
        }
        if (firstActive && !secondActive)
        {
            return LayoutTileNode(node.First, rect, open, rects);
        }
        if (!firstActive && secondActive)
        {
            return LayoutTileNode(node.Second, rect, open, rects);
        }

        double ratio = Math.Clamp(node.Ratio, 0.12, 0.88);
        if (node.Axis == TileSplitAxis.Horizontal)
        {
            int firstWidth = Math.Clamp((int)Math.Round((rect.Width - gap) * ratio), 120, Math.Max(120, rect.Width - gap - 120));
            RectI first = new(rect.X, rect.Y, firstWidth, rect.Height);
            RectI second = new(rect.X + firstWidth + gap, rect.Y, Math.Max(1, rect.Width - firstWidth - gap), rect.Height);
            LayoutTileNode(node.First, first, open, rects);
            LayoutTileNode(node.Second, second, open, rects);
        }
        else
        {
            int firstHeight = Math.Clamp((int)Math.Round((rect.Height - gap) * ratio), 100, Math.Max(100, rect.Height - gap - 100));
            RectI first = new(rect.X, rect.Y, rect.Width, firstHeight);
            RectI second = new(rect.X, rect.Y + firstHeight + gap, rect.Width, Math.Max(1, rect.Height - firstHeight - gap));
            LayoutTileNode(node.First, first, open, rects);
            LayoutTileNode(node.Second, second, open, rects);
        }
        return true;
    }

    private static TileNode? ParseTileRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var parser = new TileRootParser(root);
        return parser.Parse();
    }

    private int TilePrimaryWidth(AppWindow window, RectI work, int fallback)
    {
        UiWindowConfig config = _config.Window(WindowKey(window));
        int preferred = config.Width > 0 ? config.Width : fallback;
        return Math.Clamp(preferred, 240, Math.Max(240, work.Width - 240));
    }

    private int TilePrimaryHeight(AppWindow window, RectI work, int fallback)
    {
        UiWindowConfig config = _config.Window(WindowKey(window));
        int preferred = config.Height > 0 ? config.Height : fallback;
        return Math.Clamp(preferred, 180, Math.Max(180, work.Height - 180));
    }

    private int TileStackWidth(AppWindow window, RectI area, int fallback)
    {
        UiWindowConfig config = _config.Window(WindowKey(window));
        int preferred = config.Width > 0 ? config.Width : fallback;
        return Math.Clamp(preferred, 220, Math.Max(220, area.Width - 220));
    }

    private RectI StackPairTileRect(RectI area, int index, AppWindow firstStackWindow, int gap)
    {
        int firstWidth = TileStackWidth(firstStackWindow, area, (area.Width - gap) / 2);
        return index == 0
            ? new RectI(area.X, area.Y, firstWidth, area.Height)
            : new RectI(area.X + firstWidth + gap, area.Y, Math.Max(220, area.Width - firstWidth - gap), area.Height);
    }

    private AppWindow[] OpenTiledWindows()
    {
        return _tileOrder.Where(window => WindowState(window).IsOpen).ToArray();
    }

    private void HandleTileWindowDrag(ForgeLayout layout, AppWindow window, RectI draggedRect, bool dragging)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !IsTileEditModifierDown())
        {
            if (_tileDragWindow == window)
            {
                _tileDragWindow = null;
            }
            return;
        }

        if (dragging)
        {
            _tileDragWindow = window;
            _tileDragRect = draggedRect;
            return;
        }

        if (_tileDragWindow == window && !_pointer.LeftPressed)
        {
            CommitTileDropOrder(layout, window, _tileDragRect);
            _tileDragWindow = null;
        }
    }

    private void CommitTileDropOrder(ForgeLayout layout, AppWindow window, RectI draggedRect)
    {
        AppWindow[] open = OpenTiledWindows();
        if (open.Length < 2 || !open.Contains(window))
        {
            return;
        }

        int? swapIndex = TileDropSwapIndex(layout, window, draggedRect, open);
        if (swapIndex is int targetIndex)
        {
            SwapTileWindow(window, open[targetIndex]);
            return;
        }

        MoveTileWindow(window, TileDropIndex(layout, draggedRect, open.Length));
    }

    private void DrawTileResizePreview(ForgeLayout layout)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !IsTileEditModifierDown())
        {
            return;
        }

        AppWindow[] open = OpenTiledWindows();
        if (open.Length < 2)
        {
            return;
        }

        bool hot = HitTileTreeSplitters(TileWorkArea(layout), open).Count > 0;
        uint idleColor = WithAlpha(_ui.Theme.Button.Colors.BorderActive, 42);
        uint hotColor = WithAlpha(_ui.Theme.Button.Colors.BorderActive, _resizingTileSplit ? 150 : 96);
        foreach (RectI divider in TileSplitterRects(layout, open))
        {
            bool dividerHot = _resizingTileSplit || Expand(divider, 8).Contains(_pointer.X, _pointer.Y);
            uint color = dividerHot ? hotColor : idleColor;
            _canvas.BlendRect(divider.X, divider.Y, divider.Width, divider.Height, color);
            if (dividerHot || hot)
            {
                DrawTileResizeGrip(divider, dividerHot ? hotColor : idleColor);
            }
        }
    }

    private void DrawTileResizeGrip(RectI divider, uint color)
    {
        bool vertical = divider.Height >= divider.Width;
        if (vertical)
        {
            int x = divider.X + divider.Width / 2 - 3;
            DrawGripDot(x, divider.Y + 10, color);
            DrawGripDot(x, divider.Y + divider.Height / 2 - 3, color);
            DrawGripDot(x, divider.Bottom - 17, color);
        }
        else
        {
            int y = divider.Y + divider.Height / 2 - 3;
            DrawGripDot(divider.X + 10, y, color);
            DrawGripDot(divider.X + divider.Width / 2 - 3, y, color);
            DrawGripDot(divider.Right - 17, y, color);
        }
    }

    private void DrawGripDot(int x, int y, uint color)
    {
        _canvas.BlendRect(x, y, 7, 7, color);
        _canvas.DrawRect(x, y, 7, 7, _ui.Theme.Button.Colors.BorderHot);
    }

    private IEnumerable<RectI> TileSplitterRects(ForgeLayout layout, AppWindow[] open)
    {
        const int grip = 6;
        for (int i = 0; i < open.Length; i++)
        {
            RectI a = TiledWindowRect(layout, open[i]);
            for (int j = i + 1; j < open.Length; j++)
            {
                RectI b = TiledWindowRect(layout, open[j]);
                int y0 = Math.Max(a.Y, b.Y);
                int y1 = Math.Min(a.Bottom, b.Bottom);
                if (a.Right <= b.X && y1 - y0 > 24)
                {
                    yield return new RectI(a.Right - grip / 2, y0, Math.Max(grip, b.X - a.Right + grip), y1 - y0);
                }
                else if (b.Right <= a.X && y1 - y0 > 24)
                {
                    yield return new RectI(b.Right - grip / 2, y0, Math.Max(grip, a.X - b.Right + grip), y1 - y0);
                }

                int x0 = Math.Max(a.X, b.X);
                int x1 = Math.Min(a.Right, b.Right);
                if (a.Bottom <= b.Y && x1 - x0 > 24)
                {
                    yield return new RectI(x0, a.Bottom - grip / 2, x1 - x0, Math.Max(grip, b.Y - a.Bottom + grip));
                }
                else if (b.Bottom <= a.Y && x1 - x0 > 24)
                {
                    yield return new RectI(x0, b.Bottom - grip / 2, x1 - x0, Math.Max(grip, a.Y - b.Bottom + grip));
                }
            }
        }
    }

    private void DrawTileDropPreview(ForgeLayout layout)
    {
        if (_tileDragWindow is not AppWindow dragged || !_pointer.LeftPressed || _config.WindowMode != UiWindowMode.Tiled || !IsTileEditModifierDown())
        {
            return;
        }

        AppWindow[] open = OpenTiledWindows();
        if (open.Length < 2 || !open.Contains(dragged))
        {
            return;
        }

        RectI targetRect;
        int? swapIndex = TileDropSwapIndex(layout, dragged, _tileDragRect, open);
        if (swapIndex is int targetIndex)
        {
            targetRect = TiledWindowRect(layout, open[targetIndex]);
        }
        else
        {
            targetRect = PreviewTileRect(layout, TileDropIndex(layout, _tileDragRect, open.Length), open.Length);
        }

        uint color = WithAlpha(_ui.Theme.Button.Colors.BorderActive, 150);
        for (int i = 0; i < 3; i++)
        {
            RectI r = Expand(targetRect, i + 2);
            _canvas.BlendRect(r.X, r.Y, r.Width, 1, color);
            _canvas.BlendRect(r.X, r.Bottom - 1, r.Width, 1, color);
            _canvas.BlendRect(r.X, r.Y, 1, r.Height, color);
            _canvas.BlendRect(r.Right - 1, r.Y, 1, r.Height, color);
        }
    }

    private RectI PreviewTileRect(ForgeLayout layout, int index, int openCount)
    {
        RectI work = TileWorkArea(layout);
        const int gap = 8;
        if (openCount == 2)
        {
            int primaryWidth = TilePrimaryWidth(_tileOrder[0], work, (int)Math.Round(work.Width * 0.64));
            return index == 0
                ? new RectI(work.X, work.Y, primaryWidth, work.Height)
                : new RectI(work.X + primaryWidth + gap, work.Y, Math.Max(220, work.Width - primaryWidth - gap), work.Height);
        }

        int primaryHeight = TilePrimaryHeight(_tileOrder[0], work, (int)Math.Round(work.Height * 0.60));
        if (index == 0)
        {
            return new RectI(work.X, work.Y, work.Width, primaryHeight);
        }

        RectI rest = new(work.X, work.Y + primaryHeight + gap, work.Width, Math.Max(160, work.Height - primaryHeight - gap));
        AppWindow[] open = OpenTiledWindows();
        if (openCount == 3 && open.Length >= 2)
        {
            return StackPairTileRect(rest, index - 1, open[1], gap);
        }
        return GridTileRect(rest, index - 1, openCount - 1, gap);
    }

    private int? TileDropSwapIndex(ForgeLayout layout, AppWindow dragged, RectI draggedRect, AppWindow[] open)
    {
        int pointerX = _pointer.IsInside ? _pointer.X : draggedRect.X + draggedRect.Width / 2;
        int pointerY = _pointer.IsInside ? _pointer.Y : draggedRect.Y + draggedRect.Height / 2;
        int centerX = draggedRect.X + draggedRect.Width / 2;
        int centerY = draggedRect.Y + draggedRect.Height / 2;

        for (int i = 0; i < open.Length; i++)
        {
            AppWindow target = open[i];
            if (target == dragged)
            {
                continue;
            }

            RectI targetRect = TiledWindowRect(layout, target);
            if (targetRect.Contains(pointerX, pointerY) || targetRect.Contains(centerX, centerY))
            {
                return i;
            }
        }

        return null;
    }

    private int TileDropIndex(ForgeLayout layout, RectI draggedRect, int openCount)
    {
        RectI work = TileWorkArea(layout);
        int centerX = draggedRect.X + draggedRect.Width / 2;
        int centerY = draggedRect.Y + draggedRect.Height / 2;

        if (openCount == 2)
        {
            return centerX < work.X + work.Width / 2 ? 0 : 1;
        }

        int primaryBottom = work.Y + Math.Clamp((int)Math.Round(work.Height * 0.60), 220, Math.Max(220, work.Height - 180));
        if (centerY < primaryBottom)
        {
            return 0;
        }

        const int gap = 8;
        RectI rest = new(work.X, primaryBottom + gap, work.Width, Math.Max(160, work.Height - (primaryBottom - work.Y) - gap));
        int restCount = Math.Max(1, openCount - 1);
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(restCount)));
        int rows = Math.Max(1, (int)Math.Ceiling(restCount / (double)columns));
        int column = Math.Clamp((centerX - rest.X) * columns / Math.Max(1, rest.Width), 0, columns - 1);
        int row = Math.Clamp((centerY - rest.Y) * rows / Math.Max(1, rest.Height), 0, rows - 1);
        return 1 + Math.Clamp(row * columns + column, 0, restCount - 1);
    }

    private void MoveTileWindow(AppWindow window, int targetIndex)
    {
        _tileOrder.Remove(window);
        targetIndex = Math.Clamp(targetIndex, 0, _tileOrder.Count);
        _tileOrder.Insert(targetIndex, window);
        MarkConfigDirty();
    }

    private void SwapTileWindow(AppWindow first, AppWindow second)
    {
        int firstIndex = _tileOrder.IndexOf(first);
        int secondIndex = _tileOrder.IndexOf(second);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex == secondIndex)
        {
            return;
        }

        (_tileOrder[firstIndex], _tileOrder[secondIndex]) = (_tileOrder[secondIndex], _tileOrder[firstIndex]);
        MarkConfigDirty();
    }

    private static RectI GridTileRect(RectI area, int index, int count, int gap)
    {
        int columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        int rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        int row = index / columns;
        int column = index % columns;
        int cellWidth = Math.Max(1, (area.Width - gap * (columns - 1)) / columns);
        int cellHeight = Math.Max(1, (area.Height - gap * (rows - 1)) / rows);
        int x = area.X + column * (cellWidth + gap);
        int y = area.Y + row * (cellHeight + gap);
        int width = column == columns - 1 ? Math.Max(1, area.Right - x) : cellWidth;
        int height = row == rows - 1 ? Math.Max(1, area.Bottom - y) : cellHeight;
        return new RectI(x, y, width, height);
    }


    private static RectI TileDividerRect(RectI work, RectI settings, string slot)
    {
        return slot switch
        {
            "left" => new RectI(settings.Right - 5, work.Y, 10, work.Height),
            "top" => new RectI(work.X, settings.Bottom - 5, work.Width, 10),
            "bottom" => new RectI(work.X, settings.Y - 5, work.Width, 10),
            _ => new RectI(settings.X - 5, work.Y, 10, work.Height),
        };
    }

    private static string NearestTileSlot(RectI work, RectI rect)
    {
        int centerX = rect.X + rect.Width / 2;
        int centerY = rect.Y + rect.Height / 2;
        int left = Math.Abs(centerX - work.X);
        int right = Math.Abs(work.Right - centerX);
        int top = Math.Abs(centerY - work.Y);
        int bottom = Math.Abs(work.Bottom - centerY);
        int min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (min == top)
        {
            return "top";
        }
        if (min == bottom)
        {
            return "bottom";
        }
        return min == left ? "left" : "right";
    }

    private static string NormalizeTileSlot(string slot)
    {
        return slot.Trim().ToLowerInvariant() switch
        {
            "left" => "left",
            "top" => "top",
            "bottom" => "bottom",
            _ => "right",
        };
    }

    private RectI TileWorkArea(ForgeLayout layout)
    {
        int pad = layout.Width >= 520 ? 16 : 8;
        int right = layout.HasSidePanel ? layout.SidePanelX - pad : layout.Width - pad;
        int x = pad;
        int y = TopBarHeight + pad;
        int bottom = layout.Height - StatusBarHeight - pad;
        return new RectI(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    private static RectI FullWindowRect(ForgeLayout layout)
    {
        return new RectI(0, 0, layout.Width, layout.Height);
    }

    private RectI ChildWindowBounds(ForgeLayout layout)
    {
        return new RectI(0, TopBarHeight, layout.Width, Math.Max(1, layout.Height - TopBarHeight - StatusBarHeight));
    }

    private void OpenWindow(AppWindow window)
    {
        WindowState(window).IsOpen = true;
        BringToFront(window);
        MarkConfigDirty();
    }

    private void ToggleWindow(AppWindow window)
    {
        UiWindowState state = WindowState(window);
        state.IsOpen = !state.IsOpen;
        if (!state.IsOpen && _fullscreenTile == window)
        {
            _fullscreenTile = null;
        }
        if (state.IsOpen)
        {
            BringToFront(window);
        }
        MarkConfigDirty();
    }

    private UiWindowState WindowState(AppWindow window) => window switch
    {
        AppWindow.Viewport => _viewportWindow,
        AppWindow.Rom => _filePickerWindow,
        AppWindow.Settings => _settingsWindow,
        AppWindow.Style => _styleWindow,
        AppWindow.Input => _inputWindow,
        _ => throw new ArgumentOutOfRangeException(nameof(window)),
    };

    private AppWindow? CapturedInputWindow()
    {
        if (_tileDragWindow is AppWindow window && _pointer.LeftPressed && IsTileEditModifierDown() && WindowState(window).IsOpen)
        {
            return window;
        }

        return null;
    }

    private AppWindow? HitTestTopWindow()
    {
        if (!_pointer.IsInside)
        {
            return null;
        }

        for (int i = _windowOrder.Count - 1; i >= 0; i--)
        {
            AppWindow window = _windowOrder[i];
            UiWindowState state = WindowState(window);
            if (state.IsOpen && state.Rect is RectI rect && rect.Contains(_pointer.X, _pointer.Y))
            {
                return window;
            }
        }

        return null;
    }

    private bool IsTopOpenWindow(AppWindow window)
    {
        return TopOpenWindow() == window;
    }

    private AppWindow? TopOpenWindow()
    {
        for (int i = _windowOrder.Count - 1; i >= 0; i--)
        {
            AppWindow window = _windowOrder[i];
            if (WindowState(window).IsOpen)
            {
                return window;
            }
        }

        return null;
    }

    private void BringToFront(AppWindow window)
    {
        if (window == AppWindow.Viewport)
        {
            MarkConfigDirty();
            return;
        }

        _windowOrder.Remove(window);
        _windowOrder.Add(window);
        MarkConfigDirty();
    }


    private void ApplyConfig()
    {
        SetTheme(FindThemeIndex(_config.Theme), markDirty: false);
        SetScaleMode(ParseScaleMode(_config.Scale), markDirty: false);
        _externalCore.Configure(_config.ExternalCore, ResolveExternalDummyCorePath());
        _externalCore2.Configure(_config.ExternalCore2, ResolveExternalDummyCorePath());
        _externalCore3.Configure(_config.ExternalCore3, ResolveExternalDummyCorePath());
        _romPath = string.IsNullOrWhiteSpace(_config.Saturn.LastDisc) ? null : _config.Saturn.LastDisc;
        _saturnCore.LoadDisc(_romPath);
        ApplyWindowConfig(AppWindow.Viewport, "viewport");
        ApplyWindowConfig(AppWindow.Rom, "rom_picker");
        ApplyWindowConfig(AppWindow.Settings, "settings");
        ApplyWindowConfig(AppWindow.Style, "style_editor");
        ApplyWindowConfig(AppWindow.Input, "input_mapper");
        AppWindow[] persistedOrder = Enum.GetValues<AppWindow>().OrderBy(window => _config.Window(WindowKey(window)).Order).ToArray();
        AppWindow[] layoutOrder = TileRootLeaves(_config.Layout.Root).ToArray();
        if (layoutOrder.Length == 0)
        {
            layoutOrder = persistedOrder;
        }
        _windowOrder.Clear();
        _windowOrder.AddRange(persistedOrder.Where(window => window != AppWindow.Viewport));
        _tileOrder.Clear();
        _tileOrder.AddRange(NormalizeTileOrder(layoutOrder));
    }

    private void ApplyWindowConfig(AppWindow window, string key)
    {
        UiWindowConfig windowConfig = _config.Window(key);
        UiWindowState state = WindowState(window);
        state.IsOpen = windowConfig.Open;
        if (windowConfig.HasRect)
        {
            state.Rect = windowConfig.ToRect();
        }
    }

    private void SnapshotConfig()
    {
        _config.Theme = _ui.Theme.Name.ToLowerInvariant();
        _config.Scale = FormatScaleMode(_viewport.ScaleMode);
        _config.Saturn.LastDisc = _romPath ?? string.Empty;
        SnapshotWindow(AppWindow.Viewport, "viewport");
        SnapshotWindow(AppWindow.Rom, "rom_picker");
        SnapshotWindow(AppWindow.Settings, "settings");
        SnapshotWindow(AppWindow.Style, "style_editor");
        SnapshotWindow(AppWindow.Input, "input_mapper");
        for (int i = 0; i < _tileOrder.Count; i++)
        {
            _config.Window(WindowKey(_tileOrder[i])).Order = i * 10;
        }
        AppWindow[] rootOrder = TileRootLeaves(_config.Layout.Root).Distinct().ToArray();
        if (!rootOrder.SequenceEqual(_tileOrder))
        {
            _config.Layout.Root = FormatTileRoot(_tileOrder);
        }
    }

    private void SnapshotWindow(AppWindow window, string key)
    {
        _config.Window(key).FromWindow(WindowState(window));
    }

    private void MarkConfigDirty()
    {
        _configDirty = true;
    }

    private void SaveConfigIfDue()
    {
        if (!_configDirty || _clock.ElapsedSeconds - _lastConfigSaveSeconds < 0.5)
        {
            return;
        }
        SaveConfig(force: false);
    }

    private void SaveConfig(bool force)
    {
        if (!_configDirty && !force)
        {
            return;
        }
        SnapshotConfig();
        _config.Save(LocalConfigPath);
        _configDirty = false;
        _lastConfigSaveSeconds = _clock.ElapsedSeconds;
    }

    private void SetTheme(int index, bool markDirty = true)
    {
        _themeIndex = Math.Clamp(index, 0, UiTheme.BuiltIns.Count - 1);
        _ui.Theme = UiTheme.BuiltIns[_themeIndex];
        if (markDirty)
        {
            MarkConfigDirty();
        }
    }

    private void SetScaleMode(ViewportScaleMode scaleMode, bool markDirty = true)
    {
        _viewport.ScaleMode = scaleMode;
        if (markDirty)
        {
            MarkConfigDirty();
        }
    }

    private void SetWindowMode(UiWindowMode mode)
    {
        _config.WindowMode = mode;
        MarkConfigDirty();
    }

    private void SetAudioVolume(int volume)
    {
        volume = Math.Clamp(volume, 0, 100);
        if (_config.Audio.Volume == volume)
        {
            return;
        }

        _config.Audio.Volume = volume;
        _lastSentAudioVolume = -1;
        MarkConfigDirty();
    }

    private void SyncAudioVolumeIfDue()
    {
        if (_lastSentAudioVolume == _config.Audio.Volume)
        {
            return;
        }
        if (_clock.ElapsedSeconds - _lastAudioVolumeAttemptSeconds < 0.15)
        {
            return;
        }

        SendAudioVolume();
    }

    private void SendAudioVolume()
    {
        _lastAudioVolumeAttemptSeconds = _clock.ElapsedSeconds;
        if (TrySendAudioCommand($"SET_VOLUME {_config.Audio.Volume}\n", out _))
        {
            _lastSentAudioVolume = _config.Audio.Volume;
        }
        else
        {
            _lastSentAudioVolume = -1;
        }
    }

    private void SyncAudioStatusIfDue()
    {
        if (_clock.ElapsedSeconds - _lastAudioStatusAttemptSeconds < 1.0)
        {
            return;
        }

        _lastAudioStatusAttemptSeconds = _clock.ElapsedSeconds;
        if (TrySendAudioCommand("STATUS\n", out string response))
        {
            _audioStatus = response.Trim();
        }
        else
        {
            _audioStatus = "OFFLINE";
        }
    }

    private static bool TrySendAudioCommand(string commandText, out string responseText)
    {
        responseText = string.Empty;
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.SendTimeout = 100;
            socket.ReceiveTimeout = 100;
            socket.Connect(new UnixDomainSocketEndPoint(AudioSocketPath));
            byte[] command = Encoding.ASCII.GetBytes(commandText);
            socket.Send(command);
            byte[] response = new byte[192];
            int count = socket.Receive(response);
            responseText = Encoding.ASCII.GetString(response, 0, count);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SetBling(bool enabled)
    {
        _config.Style.Bling = enabled;
        if (enabled && !_styleWindow.IsOpen)
        {
            _styleWindow.IsOpen = true;
            BringToFront(AppWindow.Style);
        }
        MarkConfigDirty();
    }

    private static int FindThemeIndex(string themeName)
    {
        for (int i = 0; i < UiTheme.BuiltIns.Count; i++)
        {
            if (string.Equals(UiTheme.BuiltIns[i].Name, themeName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    private static ViewportScaleMode ParseScaleMode(string scale) => scale.Trim().ToLowerInvariant() switch
    {
        "integer" or "int" => ViewportScaleMode.Integer,
        "stretch" or "str" => ViewportScaleMode.Stretch,
        _ => ViewportScaleMode.Fit,
    };

    private static string FormatScaleMode(ViewportScaleMode scaleMode) => scaleMode switch
    {
        ViewportScaleMode.Integer => "integer",
        ViewportScaleMode.Stretch => "stretch",
        _ => "fit",
    };

    private static IEnumerable<AppWindow> TileRootLeaves(string root)
    {
        int i = 0;
        while (i < root.Length)
        {
            if (char.IsLetter(root[i]) || root[i] == '_')
            {
                int start = i;
                i++;
                while (i < root.Length && (char.IsLetterOrDigit(root[i]) || root[i] == '_'))
                {
                    i++;
                }

                string token = root[start..i];
                if (TryWindowFromKey(token, out AppWindow window))
                {
                    yield return window;
                }
                continue;
            }
            i++;
        }
    }

    private static string FormatTileRoot(IEnumerable<AppWindow> windows)
    {
        AppWindow[] order = windows.Distinct().ToArray();
        if (order.Length == 0)
        {
            return string.Empty;
        }
        if (order.Length == 1)
        {
            return WindowKey(order[0]);
        }
        if (order.Length == 2)
        {
            return $"h({WindowKey(order[0])},{WindowKey(order[1])},0.64)";
        }

        string stack = WindowKey(order[^1]);
        for (int i = order.Length - 2; i >= 1; i--)
        {
            stack = $"h({WindowKey(order[i])},{stack},0.50)";
        }
        return $"v({WindowKey(order[0])},{stack},0.62)";
    }

    private static string FormatTileRoot(TileNode node)
    {
        if (node.Window is AppWindow window)
        {
            return WindowKey(window);
        }
        string axis = node.Axis == TileSplitAxis.Horizontal ? "h" : "v";
        string first = node.First is null ? string.Empty : FormatTileRoot(node.First);
        string second = node.Second is null ? string.Empty : FormatTileRoot(node.Second);
        return $"{axis}({first},{second},{node.Ratio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    private static bool TryWindowFromKey(string key, out AppWindow window)
    {
        switch (key.Trim().ToLowerInvariant())
        {
            case "viewport":
                window = AppWindow.Viewport;
                return true;
            case "rom_picker":
            case "rom":
                window = AppWindow.Rom;
                return true;
            case "settings":
                window = AppWindow.Settings;
                return true;
            case "style_editor":
            case "style":
                window = AppWindow.Style;
                return true;
            case "input_mapper":
            case "input":
                window = AppWindow.Input;
                return true;
            default:
                window = default;
                return false;
        }
    }

    private static AppWindow[] NormalizeTileOrder(AppWindow[] persistedOrder)
    {
        AppWindow[] fallback = [AppWindow.Viewport, AppWindow.Rom, AppWindow.Settings, AppWindow.Style, AppWindow.Input];
        if (persistedOrder.Length == 0)
        {
            return fallback;
        }

        var result = new List<AppWindow>();
        if (!persistedOrder.Contains(AppWindow.Viewport))
        {
            result.Add(AppWindow.Viewport);
        }
        result.AddRange(persistedOrder);
        foreach (AppWindow window in fallback)
        {
            if (!result.Contains(window))
            {
                result.Add(window);
            }
        }
        return result.ToArray();
    }

    private static string WindowKey(AppWindow window) => window switch
    {
        AppWindow.Viewport => "viewport",
        AppWindow.Rom => "rom_picker",
        AppWindow.Settings => "settings",
        AppWindow.Style => "style_editor",
        AppWindow.Input => "input_mapper",
        _ => throw new ArgumentOutOfRangeException(nameof(window)),
    };

    private UiInputConfig ActiveInputProfile()
    {
        if (ReferenceEquals(_core, _externalCore2))
        {
            return _config.CoreInput("external_core2");
        }
        if (ReferenceEquals(_core, _externalCore3))
        {
            return _config.CoreInput("external_core3");
        }
        if (ReferenceEquals(_core, _externalCore))
        {
            return _config.CoreInput("external_core");
        }
        return _config.Input;
    }

    private string ActiveInputProfileLabel()
    {
        if (ReferenceEquals(_core, _externalCore2))
        {
            return "RAPTOR";
        }
        if (ReferenceEquals(_core, _externalCore3))
        {
            return "STORMAKT 3020";
        }
        if (ReferenceEquals(_core, _externalCore))
        {
            return "OPENTYRIAN";
        }
        return "HOST";
    }

    private IEnumerable<uint> BoundKeyCodes(string actionId)
    {
        return BoundKeyCodes(actionId, _config.Input);
    }

    private IEnumerable<uint> BoundKeyCodes(string actionId, UiInputConfig inputConfig)
    {
        bool hasBinding = inputConfig.Bindings.TryGetValue(actionId, out string? value);
        if (!hasBinding && !ReferenceEquals(inputConfig, _config.Input))
        {
            hasBinding = _config.Input.Bindings.TryGetValue(actionId, out value);
        }
        if (!hasBinding || string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseKeyCode(token, out uint keyCode))
            {
                yield return keyCode;
            }
        }
    }

    private string BindingDisplay(string actionId, UiInputConfig inputConfig)
    {
        bool hasBinding = inputConfig.Bindings.TryGetValue(actionId, out string? value);
        if (!hasBinding && !ReferenceEquals(inputConfig, _config.Input))
        {
            hasBinding = _config.Input.Bindings.TryGetValue(actionId, out value);
        }
        if (!hasBinding || string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }
        return value.ToUpperInvariant();
    }

    private IEnumerable<WcpControl> BoundControllerControls(string actionId, UiInputConfig inputConfig)
    {
        bool hasBinding = inputConfig.ControllerBindings.TryGetValue(actionId, out string? value);
        if (!hasBinding && !ReferenceEquals(inputConfig, _config.Input))
        {
            hasBinding = _config.Input.ControllerBindings.TryGetValue(actionId, out value);
        }
        if (!hasBinding || string.IsNullOrWhiteSpace(value)) yield break;

        foreach (string token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse(token, ignoreCase: true, out WcpControl control) && control != WcpControl.None)
                yield return control;
        }
    }

    private string ControllerBindingDisplay(string actionId, UiInputConfig inputConfig)
    {
        bool hasBinding = inputConfig.ControllerBindings.TryGetValue(actionId, out string? value);
        if (!hasBinding && !ReferenceEquals(inputConfig, _config.Input))
        {
            hasBinding = _config.Input.ControllerBindings.TryGetValue(actionId, out value);
        }
        return !hasBinding || string.IsNullOrWhiteSpace(value) ? "-" : value.ToUpperInvariant();
    }

    private static string FormatControllerControl(WcpControl control) => control.ToString().ToLowerInvariant();

    private void SetInputBinding(InputBinding binding, uint keyCode)
    {
        ActiveInputProfile().Bindings[binding.Id] = FormatKeyName(keyCode);
        MarkConfigDirty();
    }

    private string InputBindingLabel(InputBinding binding)
    {
        if (ReferenceEquals(_core, _externalCore2))
        {
            return binding.Id switch
            {
                "a" => "A FIRE",
                "b" => "B ALT/RMB",
                "c" => "C SPACE",
                "x" => "X SHIFT",
                "y" => "Y SPACE",
                "z" => "Z UNUSED",
                "start" => "START/OK",
                "escape" => "ESC DISABLED",
                _ => binding.Label,
            };
        }

        if (ReferenceEquals(_core, _externalCore3))
        {
            return binding.Id switch
            {
                "a" => "A FIRE",
                "b" => "B BROADSIDE",
                "c" => "C HOLD",
                "x" => "X SLOW",
                "start" => "START",
                _ => binding.Label,
            };
        }

        if (ReferenceEquals(_core, _externalCore))
        {
            return binding.Id switch
            {
                "a" => "A FIRE",
                "b" => "B REAR",
                "c" => "C MODE",
                "x" => "X",
                "y" => "Y",
                "z" => "Z",
                _ => binding.Label,
            };
        }

        return binding.Label;
    }

    private static bool IsReservedMappingKey(uint keyCode)
    {
        return keyCode == 1;
    }

    private static bool TryParseKeyCode(string token, out uint keyCode)
    {
        string normalized = token.Trim().ToLowerInvariant();
        if (KeyNameToCode.TryGetValue(normalized, out keyCode))
        {
            return true;
        }
        if (normalized.StartsWith("key:", StringComparison.Ordinal) &&
            uint.TryParse(normalized[4..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out keyCode))
        {
            return true;
        }
        if (uint.TryParse(normalized, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out keyCode))
        {
            return true;
        }

        keyCode = 0;
        return false;
    }

    private static string FormatKeyName(uint keyCode)
    {
        return KeyCodeToName.TryGetValue(keyCode, out string? name) ? name : "key:" + keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void DrawScaleToggles(ForgeLayout layout)
    {
        if (layout.Width < 420)
        {
            return;
        }

        int y = layout.Height - 21;
        int x = layout.Width >= 720 ? layout.Width - 206 : layout.Width - 194;
        var row = new UiRow(x, y, 15, 4);
        row = row.Next(48, out RectI fitRect);
        if (_ui.ToggleButton(fitRect, "FIT", _viewport.ScaleMode == ViewportScaleMode.Fit))
        {
            SetScaleMode(ViewportScaleMode.Fit);
        }
        row = row.Next(48, out RectI intRect);
        if (_ui.ToggleButton(intRect, "INT", _viewport.ScaleMode == ViewportScaleMode.Integer))
        {
            SetScaleMode(ViewportScaleMode.Integer);
        }
        row = row.Next(48, out RectI stretchRect);
        if (_ui.ToggleButton(stretchRect, "STR", _viewport.ScaleMode == ViewportScaleMode.Stretch))
        {
            SetScaleMode(ViewportScaleMode.Stretch);
        }
    }

    private void HandleHostShortcuts(ForgeInput input)
    {
        if (Pressed(input, ForgeInput.TileFullscreen))
        {
            ToggleFocusedTileFullscreen();
            return;
        }

        if (HandleTileKeyboardShortcuts(input))
        {
            return;
        }

        if (Pressed(input, ForgeInput.ScaleFit))
        {
            SetScaleMode(ViewportScaleMode.Fit);
        }
        else if (Pressed(input, ForgeInput.ScaleInteger))
        {
            SetScaleMode(ViewportScaleMode.Integer);
        }
        else if (Pressed(input, ForgeInput.ScaleStretch))
        {
            SetScaleMode(ViewportScaleMode.Stretch);
        }
        else if (Pressed(input, ForgeInput.ThemeNext))
        {
            NextTheme();
        }

    }

    private void ToggleFocusedTileFullscreen()
    {
        if (_config.WindowMode != UiWindowMode.Tiled)
        {
            return;
        }

        ForgeLayout layout = ForgeLayout.Calculate(Math.Max(1, _lastRenderWidth), Math.Max(1, _lastRenderHeight));
        AppWindow target = HitTestTile(layout) ?? _focusedTile;
        if (!WindowState(target).IsOpen)
        {
            target = AppWindow.Viewport;
        }

        _fullscreenTile = _fullscreenTile == target ? null : target;
        _focusedTile = target;
        _pressedKeys.Clear();
        PushCurrentInputState();
    }

    private void PushCurrentInputState(uint rawKeyCode = 0, uint rawKeySerial = 0, bool rawKeyPressed = false)
    {
        ForgeInput controllerInput = MapInputFromController(ActiveInputProfile());
        ForgeInput mappedInput = MapInputFromPressedKeys(ActiveInputProfile()) | controllerInput;
        _lastInput = mappedInput;
        _inputSource.Update(mappedInput, controllerInput, _wayControlInput.LeftX, _wayControlInput.LeftY);
        SyncExternalPointerState();
        if (_core is ExternalProcessCore external)
        {
            external.PushInputNow(_inputSource.Poll(), rawKeyCode, rawKeySerial, rawKeyPressed);
        }
    }

    private void SyncExternalPointerState()
    {
        if (_core is not ExternalProcessCore external)
        {
            return;
        }

        RectI content = _viewport.ContentRect;
        int sourceWidth = Math.Max(1, _frameStore.Width);
        int sourceHeight = Math.Max(1, _frameStore.Height);
        int coreX = 0;
        int coreY = 0;
        string pointerDriver = external.PointerDriver;
        if (pointerDriver == "none")
        {
            external.SetPointerState(0, 0, 0, false);
            return;
        }

        bool hasContent = content.Width > 0 && content.Height > 0;
        bool insideContent = _pointer.IsInside && hasContent && content.Contains(_pointer.X, _pointer.Y);
        bool insideStormaktTile = pointerDriver == "stormakt_rts" &&
            _pointer.IsInside && hasContent && _viewportWindow.Rect is RectI viewportTile &&
            viewportTile.Contains(_pointer.X, _pointer.Y);
        bool capturePointer = pointerDriver is "capture" or "raptor";
        bool inside = insideContent || insideStormaktTile || (capturePointer && _pointer.IsInside && hasContent);
        if (inside && pointerDriver == "raptor")
        {
            int relativeX = Math.Clamp(_pointer.X - content.X, 0, content.Width - 1);
            int relativeY = Math.Clamp(_pointer.Y - content.Y, 0, content.Height - 1);
            int absoluteX = Math.Clamp(relativeX * sourceWidth / content.Width, 0, sourceWidth - 1);
            int absoluteY = Math.Clamp(relativeY * sourceHeight / content.Height, 0, sourceHeight - 1);
            if (!_externalPointerInitialized)
            {
                _externalPointerX = absoluteX;
                _externalPointerY = absoluteY;
                _externalPointerInitialized = true;
            }
            else
            {
                int deltaX = _pointer.X - _lastExternalPointerHostX;
                int deltaY = _pointer.Y - _lastExternalPointerHostY;
                _externalPointerX = Math.Clamp(_externalPointerX + deltaX * sourceWidth / Math.Max(1, content.Width), 0, sourceWidth - 1);
                _externalPointerY = Math.Clamp(_externalPointerY + deltaY * sourceHeight / Math.Max(1, content.Height), 0, sourceHeight - 1);
            }
            _lastExternalPointerHostX = _pointer.X;
            _lastExternalPointerHostY = _pointer.Y;
            coreX = _externalPointerX;
            coreY = _externalPointerY;
        }
        else if (inside)
        {
            _externalPointerInitialized = false;
            int relativeX = Math.Clamp(_pointer.X - content.X, 0, content.Width - 1);
            int relativeY = Math.Clamp(_pointer.Y - content.Y, 0, content.Height - 1);
            coreX = Math.Clamp(relativeX * sourceWidth / content.Width, 0, sourceWidth - 1);
            coreY = Math.Clamp(relativeY * sourceHeight / content.Height, 0, sourceHeight - 1);
        }
        uint buttons = inside || capturePointer ? (uint)_pointer.Buttons : 0u;
        external.SetPointerState(coreX, coreY, buttons, inside);
    }

    private bool ShouldHideNativeCursor()
    {
        if (_fullscreenTile is not null)
        {
            return true;
        }
        if (_core is not ExternalProcessCore external || external.PointerDriver != "raptor")
        {
            return false;
        }
        return _pointer.IsInside && _viewport.ContentRect.Contains(_pointer.X, _pointer.Y);
    }

    private bool HandleTileKeyboardShortcuts(ForgeInput input)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || (input & ForgeInput.Super) == 0)
        {
            return false;
        }

        TileDirection? direction = Pressed(input, ForgeInput.Left) ? TileDirection.Left :
            Pressed(input, ForgeInput.Right) ? TileDirection.Right :
            Pressed(input, ForgeInput.Up) ? TileDirection.Up :
            Pressed(input, ForgeInput.Down) ? TileDirection.Down :
            null;
        if (direction is not TileDirection dir)
        {
            return false;
        }

        AppWindow? target = FindDirectionalTile(_focusedTile, dir);
        if (target is null)
        {
            return true;
        }

        if ((input & ForgeInput.Shift) != 0)
        {
            SwapTileWindow(_focusedTile, target.Value);
        }
        _focusedTile = target.Value;
        BringToFront(_focusedTile);
        return true;
    }

    private AppWindow? FindDirectionalTile(AppWindow from, TileDirection direction)
    {
        AppWindow[] open = OpenTiledWindows();
        if (!open.Contains(from))
        {
            return open.FirstOrDefault();
        }

        ForgeLayout layout = ForgeLayout.Calculate(Math.Max(1, _lastRenderWidth), Math.Max(1, _lastRenderHeight));
        RectI fromRect = TiledWindowRect(layout, from);
        int fromX = fromRect.X + fromRect.Width / 2;
        int fromY = fromRect.Y + fromRect.Height / 2;
        AppWindow? best = null;
        double bestScore = double.MaxValue;
        foreach (AppWindow candidate in open)
        {
            if (candidate == from)
            {
                continue;
            }

            RectI candidateRect = TiledWindowRect(layout, candidate);
            int candidateX = candidateRect.X + candidateRect.Width / 2;
            int candidateY = candidateRect.Y + candidateRect.Height / 2;
            int dx = candidateX - fromX;
            int dy = candidateY - fromY;
            double score = direction switch
            {
                TileDirection.Left when dx < 0 => -dx + Math.Abs(dy) * 0.35,
                TileDirection.Right when dx > 0 => dx + Math.Abs(dy) * 0.35,
                TileDirection.Up when dy < 0 => -dy + Math.Abs(dx) * 0.35,
                TileDirection.Down when dy > 0 => dy + Math.Abs(dx) * 0.35,
                _ => double.MaxValue,
            };
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private bool Pressed(ForgeInput input, ForgeInput button)
    {
        return (input & button) != 0 && (_previousInput & button) == 0;
    }

    private void DrawMetric(int x, int y, string label, string value)
    {
        _ui.Text(x, y, label, UiTextKind.Muted);
        _ui.Text(x + 72, y, value);
    }

    private static string FormatHex(uint value, int digits) =>
        "0X" + value.ToString("X" + digits.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> SplitCommandArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            yield break;
        }

        var builder = new StringBuilder();
        bool quoted = false;
        foreach (char ch in args)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private void DrawInputLamp(int x, int y, string label, ForgeInput bit)
    {
        bool active = (_lastInput & bit) != 0;
        UiColors colors = _ui.Theme.Button.Colors;
        uint color = active ? colors.Accent : colors.Border;
        _canvas.FillRect(x, y + 2, 8, 8, color);
        _canvas.DrawRect(x, y + 2, 8, 8, colors.BorderHot);
        _ui.Text(x + 16, y, label, active ? UiTextKind.Normal : UiTextKind.Muted);
    }

    private void DrawInputLamp(int x, int y, string label, ForgeInput bit, UiInputConfig inputProfile)
    {
        DrawInputLamp(x, y, label, bit);
        string keys = InputKeysForBits(bit, inputProfile);
        _ui.Text(x + 72, y, TruncateMiddle(keys, 14), UiTextKind.Muted);
    }

    private string InputKeysForBits(ForgeInput bits, UiInputConfig inputProfile)
    {
        List<string> keys = [];
        foreach (InputBinding binding in InputBindings)
        {
            if ((binding.Bit & bits) == 0)
            {
                continue;
            }

            string value = BindingDisplay(binding.Id, inputProfile);
            if (value != "-")
            {
                keys.Add(value);
            }
        }

        return keys.Count == 0 ? "-" : string.Join("/", keys);
    }

    private void NextTheme()
    {
        SetTheme((_themeIndex + 1) % UiTheme.BuiltIns.Count);
    }

    private void ToggleExternalCore()
    {
        ToggleExternalCore(_externalCore);
    }

    private void LoadSaturnDisc(string path)
    {
        _romPath = Path.GetFullPath(path);
        foreach (ExternalProcessCore external in ExternalCores())
        {
            external.Reset();
        }

        _saturnCore.LoadDisc(_romPath);
        _core = _saturnCore;
        _coreFault = string.Empty;
        _paused = false;
        _stepRequested = false;
        StepActiveCore();
    }

    private void ToggleExternalCore2()
    {
        ToggleExternalCore(_externalCore2);
    }

    private void ToggleExternalCore3()
    {
        ToggleExternalCore(_externalCore3);
    }

    private void ToggleExternalCore(ExternalProcessCore target)
    {
        if (ReferenceEquals(_core, target))
        {
            target.Reset();
            _core = _fakeCore;
        }
        else
        {
            _fakeCore.Reset();
            foreach (ExternalProcessCore external in ExternalCores())
            {
                if (!ReferenceEquals(target, external))
                {
                    external.Reset();
                }
            }
            _core = target;
        }

        _coreFault = string.Empty;
        _core.Reset();
        StepActiveCore();
    }

    private void RestartExternalCore()
    {
        ExternalProcessCore external = ActiveExternalCore();
        _coreFault = string.Empty;
        external.Reset();
        if (!ReferenceEquals(_core, external))
        {
            _core = external;
        }
        StepActiveCore();
    }

    private void StepActiveCore()
    {
        try
        {
            _core.StepFrame(_inputSource, _frameStore);
            _coreFault = string.Empty;
        }
        catch (Exception ex)
        {
            _coreFault = ex.Message;
            _paused = true;
            _stepRequested = false;
        }
    }

    private string ExternalCommandLabel()
    {
        UiExternalCoreConfig config = ActiveExternalCoreConfig();
        if (string.Equals(Path.GetFileName(config.Command), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? dll = SplitCommandArguments(config.Args).FirstOrDefault(static arg => arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(dll))
            {
                return TruncateMiddle(Path.GetFileNameWithoutExtension(dll), 18);
            }
        }
        return string.IsNullOrWhiteSpace(config.Command)
            ? "BUILTIN DUMMY"
            : TruncateMiddle(Path.GetFileName(config.Command), 18);
    }

    private ExternalProcessCore ActiveExternalCore()
    {
        if (ReferenceEquals(_core, _externalCore3))
        {
            return _externalCore3;
        }
        return ReferenceEquals(_core, _externalCore2) ? _externalCore2 : _externalCore;
    }

    private UiExternalCoreConfig ActiveExternalCoreConfig()
    {
        if (ReferenceEquals(_core, _externalCore3))
        {
            return _config.ExternalCore3;
        }
        return ReferenceEquals(_core, _externalCore2) ? _config.ExternalCore2 : _config.ExternalCore;
    }

    private ExternalProcessCore[] ExternalCores()
    {
        return [_externalCore, _externalCore2, _externalCore3];
    }

    private string CoreName()
    {
        return _core switch
        {
            SaturnBringupCore => "SATURN BRINGUP",
            FakeSaturnCore => "FAKE SATURN",
            ExternalProcessCore external => external.Name,
            _ => _core.GetType().Name.ToUpperInvariant(),
        };
    }

    private ulong CoreFrameIndex()
    {
        return _core switch
        {
            SaturnBringupCore saturn => saturn.FrameIndex,
            FakeSaturnCore fake => fake.FrameIndex,
            ExternalProcessCore external => external.FrameIndex,
            _ => 0,
        };
    }

    private static string ResolveExternalDummyCorePath()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "SystemRegisIII.ExternalCore.Dummy.dll");
        if (File.Exists(local))
        {
            return local;
        }

        string configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent?.Name ?? "Debug";
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../SystemRegisIII.ExternalCore.Dummy/bin",
            configuration,
            "net8.0",
            "SystemRegisIII.ExternalCore.Dummy.dll"));
    }

    private static string TruncateMiddle(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        int keep = Math.Max(2, (maxChars - 3) / 2);
        return text[..keep] + "..." + text[^keep..];
    }

    private static long StopwatchMicroseconds()
    {
        long timestamp = Stopwatch.GetTimestamp();
        return timestamp / Stopwatch.Frequency * 1_000_000 +
            timestamp % Stopwatch.Frequency * 1_000_000 / Stopwatch.Frequency;
    }


    private static readonly InputBinding[] InputBindings =
    [
        new("escape", "ESCAPE", ForgeInput.Escape),
        new("up", "UP", ForgeInput.Up),
        new("down", "DOWN", ForgeInput.Down),
        new("left", "LEFT", ForgeInput.Left),
        new("right", "RIGHT", ForgeInput.Right),
        new("start", "START", ForgeInput.Start),
        new("a", "A", ForgeInput.A),
        new("b", "B", ForgeInput.B),
        new("c", "C", ForgeInput.C),
        new("x", "X", ForgeInput.X),
        new("y", "Y", ForgeInput.Y),
        new("z", "Z", ForgeInput.Z),
        new("developer_save", "DEV SAVE", ForgeInput.DeveloperSave),
        new("developer_load", "DEV LOAD", ForgeInput.DeveloperLoad),
        new("scale_fit", "SCALE FIT", ForgeInput.ScaleFit),
        new("scale_integer", "SCALE INT", ForgeInput.ScaleInteger),
        new("scale_stretch", "SCALE STR", ForgeInput.ScaleStretch),
        new("theme_next", "THEME", ForgeInput.ThemeNext),
        new("tile_fullscreen", "FULL TILE", ForgeInput.TileFullscreen),
        new("shift", "SHIFT", ForgeInput.Shift),
        new("super", "SUPER", ForgeInput.Super),
    ];

    private static readonly Dictionary<string, uint> KeyNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = 1,
        ["escape"] = 1,
        ["1"] = 2,
        ["2"] = 3,
        ["3"] = 4,
        ["4"] = 5,
        ["5"] = 6,
        ["6"] = 7,
        ["7"] = 8,
        ["8"] = 9,
        ["9"] = 10,
        ["0"] = 11,
        ["q"] = 16,
        ["w"] = 17,
        ["e"] = 18,
        ["r"] = 19,
        ["t"] = 20,
        ["y"] = 21,
        ["u"] = 22,
        ["i"] = 23,
        ["o"] = 24,
        ["p"] = 25,
        ["enter"] = 28,
        ["return"] = 28,
        ["leftctrl"] = 29,
        ["ctrl"] = 29,
        ["a"] = 30,
        ["s"] = 31,
        ["d"] = 32,
        ["f"] = 33,
        ["g"] = 34,
        ["h"] = 35,
        ["j"] = 36,
        ["k"] = 37,
        ["l"] = 38,
        ["leftshift"] = 42,
        ["shift"] = 42,
        ["z"] = 44,
        ["x"] = 45,
        ["c"] = 46,
        ["v"] = 47,
        ["b"] = 48,
        ["n"] = 49,
        ["m"] = 50,
        ["rightshift"] = 54,
        ["leftalt"] = 56,
        ["alt"] = 56,
        ["space"] = 57,
        ["f1"] = 59,
        ["f2"] = 60,
        ["f3"] = 61,
        ["f4"] = 62,
        ["f5"] = 63,
        ["f6"] = 64,
        ["f7"] = 65,
        ["f8"] = 66,
        ["f9"] = 67,
        ["f10"] = 68,
        ["f11"] = 87,
        ["f12"] = 88,
        ["home"] = 102,
        ["up"] = 103,
        ["pageup"] = 104,
        ["left"] = 105,
        ["right"] = 106,
        ["end"] = 107,
        ["down"] = 108,
        ["pagedown"] = 109,
        ["insert"] = 110,
        ["delete"] = 111,
        ["leftmeta"] = 125,
        ["super"] = 125,
        ["rightmeta"] = 126,
    };

    private static readonly Dictionary<uint, string> KeyCodeToName = KeyNameToCode
        .GroupBy(static pair => pair.Value)
        .ToDictionary(static group => group.Key, static group => group.First().Key);

    private readonly record struct InputBinding(string Id, string Label, ForgeInput Bit);
    private readonly record struct TileResizeHandle(string Path, bool Vertical, double StartRatio, int Span);
    private readonly record struct TileResizeCandidate(TileResizeHandle Handle, RectI Divider);

    private enum TileDirection
    {
        Left,
        Right,
        Up,
        Down,
    }

    private enum TileSplitAxis
    {
        Horizontal,
        Vertical,
    }

    private sealed class TileNode
    {
        public AppWindow? Window { get; init; }
        public TileSplitAxis Axis { get; init; }
        public TileNode? First { get; init; }
        public TileNode? Second { get; init; }
        public double Ratio { get; set; } = 0.5;

        public bool ContainsAny(HashSet<AppWindow> windows)
        {
            if (Window is AppWindow window)
            {
                return windows.Contains(window);
            }
            return (First?.ContainsAny(windows) ?? false) || (Second?.ContainsAny(windows) ?? false);
        }
    }

    private sealed class TileRootParser
    {
        private readonly string _text;
        private int _index;

        public TileRootParser(string text)
        {
            _text = text;
        }

        public TileNode? Parse()
        {
            TileNode? node = ParseNode();
            SkipTrivia();
            return node;
        }

        private TileNode? ParseNode()
        {
            SkipTrivia();
            string token = ParseToken();
            if (token.Length == 0)
            {
                return null;
            }

            SkipTrivia();
            if ((token == "h" || token == "v") && TryConsume('('))
            {
                TileNode? first = ParseNode();
                if (!TryConsume(',')) return null;
                TileNode? second = ParseNode();
                double ratio = 0.5;
                if (TryConsume(','))
                {
                    ratio = ParseRatio(0.5);
                }
                if (!TryConsume(')') || first is null || second is null)
                {
                    return null;
                }
                return new TileNode { Axis = token == "h" ? TileSplitAxis.Horizontal : TileSplitAxis.Vertical, First = first, Second = second, Ratio = ratio };
            }

            return TryWindowFromKey(token, out AppWindow window) ? new TileNode { Window = window } : null;
        }

        private string ParseToken()
        {
            SkipTrivia();
            int start = _index;
            while (_index < _text.Length && (char.IsLetterOrDigit(_text[_index]) || _text[_index] == '_'))
            {
                _index++;
            }
            return _text[start.._index].Trim().ToLowerInvariant();
        }

        private double ParseRatio(double fallback)
        {
            SkipTrivia();
            int start = _index;
            while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
            {
                _index++;
            }
            return double.TryParse(_text[start.._index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ratio) ? ratio : fallback;
        }

        private bool TryConsume(char expected)
        {
            SkipTrivia();
            if (_index >= _text.Length || _text[_index] != expected)
            {
                return false;
            }
            _index++;
            return true;
        }

        private void SkipTrivia()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }
    }

    private enum AppWindow
    {
        Viewport,
        Style,
        Settings,
        Rom,
        Input,
    }

    private readonly record struct ForgeLayout(
        int Width,
        int Height,
        bool HasSidePanel,
        int SidePanelX,
        int ViewAreaX,
        int ViewAreaY,
        int ViewAreaW,
        int ViewAreaH)
    {
        public static ForgeLayout Calculate(int width, int height)
        {
            bool hasSidePanel = width >= MinimumWidthForSidePanel;
            int sidePanelX = hasSidePanel ? width - SidePanelWidth : width;
            int pad = width >= 520 ? 16 : 8;
            int viewAreaX = pad;
            int viewAreaY = TopBarHeight + pad;
            int viewAreaW = Math.Max(1, sidePanelX - pad * 2);
            int viewAreaH = Math.Max(1, height - TopBarHeight - StatusBarHeight - pad * 2);

            return new ForgeLayout(
                width,
                height,
                hasSidePanel,
                sidePanelX,
                viewAreaX,
                viewAreaY,
                viewAreaW,
                viewAreaH);
        }
    }
}
