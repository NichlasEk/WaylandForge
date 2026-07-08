using SystemRegisIII.WaylandForge.Ui;
using System.Diagnostics;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class ForgeApp : IDisposable
{
    private const int TopBarHeight = 28;
    private const int StatusBarHeight = 24;
    private const int SidePanelWidth = 184;
    private const int MinimumWidthForSidePanel = 560;
    private const string DefaultConfigPath = "config/waylandforge.ui.toml";
    private const string LocalConfigPath = "config/waylandforge.ui.local.toml";

    private readonly SoftwareCanvas _canvas = new();
    private readonly FakeSaturnCore _core = new();
    private readonly ForgeInputSource _inputSource = new();
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
    private readonly List<AppWindow> _windowOrder = [AppWindow.Style, AppWindow.Settings, AppWindow.Rom];
    private readonly List<AppWindow> _tileOrder = [AppWindow.Viewport, AppWindow.Rom, AppWindow.Settings, AppWindow.Style];
    private string? _romPath;
    private AppWindow _focusedTile = AppWindow.Viewport;
    private int _themeIndex;
    private bool _configDirty;
    private double _lastConfigSaveSeconds;
    private bool _resizingTileSplit;
    private readonly List<TileResizeHandle> _tileResizeHandles = new();
    private AppWindow? _tileDragWindow;
    private RectI _tileDragRect;
    private int _tileResizeStartX;
    private int _tileResizeStartY;

    public ForgeApp()
    {
        _ui = new UiContext(_canvas, UiTheme.Default);
        _config = UiConfig.Load(DefaultConfigPath, LocalConfigPath);
        ApplyConfig();
    }

    public void Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input, PointerState pointer, TextInputEvent textInput, ScrollInputEvent scrollInput)
    {
        Update(input, pointer, textInput, scrollInput, frameIndex);

        _canvas.Bind(pixels, width, height, stridePixels);
        long drawStart = Stopwatch.GetTimestamp();
        Draw(width, height);
        _clock.RecordDraw(Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds);
        _previousPointer = pointer;
    }

    private void Update(ForgeInput input, PointerState pointer, TextInputEvent textInput, ScrollInputEvent scrollInput, ulong frameIndex)
    {
        _clock.Tick();
        _lastInput = input;
        _pointer = pointer;
        _textInput = textInput;
        _scrollInput = scrollInput;
        _hostFrameIndex = frameIndex;
        HandleHostShortcuts(input);

        _inputSource.Update(input);
        if (!_paused || _stepRequested || _frameStore.Pixels.IsEmpty)
        {
            _core.StepFrame(_inputSource, _frameStore);
            _stepRequested = false;
        }

        SaveConfigIfDue();
        _previousInput = input;
    }

    private void Draw(int width, int height)
    {
        _ui.BeginFrame(_pointer, _previousPointer, _textInput, _scrollInput);
        _canvas.Clear(_ui.Theme.Panel.Colors.Panel);
        var layout = ForgeLayout.Calculate(width, height);

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
    }

    public void Dispose()
    {
        SaveConfig(force: false);
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
            _core.StepFrame(_inputSource, _frameStore);
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
    }

    private void DrawViewport(ForgeLayout layout)
    {
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
        using (UiScrollArea scroll = _ui.BeginScrollArea(new UiId("debug.scroll"), content, 520))
        {
            var column = new UiColumn(scroll.Content.X, scroll.Content.Y, scroll.Content.Width, 5);

            if (_ui.Collapsible(new UiId("debug.host"), ref column, "HOST", 222, out RectI hostSection))
            {
                int x = hostSection.X;
                int y = hostSection.Y;
                DrawMetric(x, y, "BACKEND", "WAYLAND"); y += 18;
                DrawMetric(x, y, "BUFFER", "WL_SHM X2"); y += 18;
                DrawMetric(x, y, "FORMAT", "ARGB8888"); y += 18;
                DrawMetric(x, y, "CORE", "FAKE SATURN"); y += 18;
                DrawMetric(x, y, "RUN", _paused ? "PAUSED" : "RUNNING"); y += 18;
                DrawMetric(x, y, "SOURCE", $"{_frameStore.Width}X{_frameStore.Height}"); y += 18;
                DrawMetric(x, y, "CFRAME", _core.FrameIndex.ToString()); y += 18;
                DrawMetric(x, y, "SCALE", _viewport.ScaleMode.ToString().ToUpperInvariant()); y += 18;
                DrawMetric(x, y, "FPS", _clock.FramesPerSecond.ToString("0.0")); y += 18;
                DrawMetric(x, y, "FRAME MS", _clock.FrameMilliseconds.ToString("0.0")); y += 18;
                DrawMetric(x, y, "HZ", (_clock.FrameMilliseconds > 0 ? 1000.0 / _clock.FrameMilliseconds : 0).ToString("0.0")); y += 18;
                DrawMetric(x, y, "DRAW MS", _clock.DrawMilliseconds.ToString("0.0")); y += 18;
            }

            if (_ui.Collapsible(new UiId("debug.input"), ref column, "INPUT", 142, out RectI inputSection))
            {
                int x = inputSection.X;
                int y = inputSection.Y;
                DrawMetric(x, y, "PTR", _pointer.IsInside ? $"{_pointer.X},{_pointer.Y}" : "OUT"); y += 18;
                DrawMetric(x, y, "MBTN", _pointer.Buttons.ToString().ToUpperInvariant()); y += 20;
                DrawInputLamp(x, y, "UP", ForgeInput.Up); y += 16;
                DrawInputLamp(x, y, "DOWN", ForgeInput.Down); y += 16;
                DrawInputLamp(x, y, "LEFT", ForgeInput.Left); y += 16;
                DrawInputLamp(x, y, "RIGHT", ForgeInput.Right); y += 16;
                DrawInputLamp(x, y, "START", ForgeInput.Start); y += 16;
                DrawInputLamp(x, y, "A B C", ForgeInput.A | ForgeInput.B | ForgeInput.C); y += 16;
                DrawInputLamp(x, y, "X Y Z", ForgeInput.X | ForgeInput.Y | ForgeInput.Z);
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

        _ui.Text(content.X, layout.Height - 78, "ESC CLOSES", UiTextKind.Accent);
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
            _ui.Text(layout.Width - 438, layout.Height - 16, "1/2/3 SCALE  T THEME", UiTextKind.Muted);
        }
    }

    private void DrawChildWindows(ForgeLayout layout)
    {
        HandleTileEdit(layout);
        AppWindow? inputWindow = CapturedInputWindow() ?? HitTestTopWindow();
        foreach (AppWindow window in _windowOrder.ToArray())
        {
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
                _romPath = result.SelectedPath;
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
            _ => throw new ArgumentOutOfRangeException(nameof(window)),
        };
    }

    private RectI TiledWindowRect(ForgeLayout layout, AppWindow window)
    {
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

        if (HitTileTreeSplitters(TileWorkArea(layout), open).Count == 0 && !_resizingTileSplit)
        {
            return;
        }

        uint color = WithAlpha(_ui.Theme.Button.Colors.BorderActive, _resizingTileSplit ? 130 : 72);
        foreach (RectI divider in TileSplitterRects(layout, open))
        {
            if (_resizingTileSplit || divider.Contains(_pointer.X, _pointer.Y))
            {
                _canvas.BlendRect(divider.X, divider.Y, divider.Width, divider.Height, color);
            }
        }
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
        _windowOrder.Remove(window);
        _windowOrder.Add(window);
        MarkConfigDirty();
    }


    private void ApplyConfig()
    {
        SetTheme(FindThemeIndex(_config.Theme), markDirty: false);
        SetScaleMode(ParseScaleMode(_config.Scale), markDirty: false);
        ApplyWindowConfig(AppWindow.Viewport, "viewport");
        ApplyWindowConfig(AppWindow.Rom, "rom_picker");
        ApplyWindowConfig(AppWindow.Settings, "settings");
        ApplyWindowConfig(AppWindow.Style, "style_editor");
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
        SnapshotWindow(AppWindow.Viewport, "viewport");
        SnapshotWindow(AppWindow.Rom, "rom_picker");
        SnapshotWindow(AppWindow.Settings, "settings");
        SnapshotWindow(AppWindow.Style, "style_editor");
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
            default:
                window = default;
                return false;
        }
    }

    private static AppWindow[] NormalizeTileOrder(AppWindow[] persistedOrder)
    {
        AppWindow[] fallback = [AppWindow.Viewport, AppWindow.Rom, AppWindow.Settings, AppWindow.Style];
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
        _ => throw new ArgumentOutOfRangeException(nameof(window)),
    };

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

    private bool Pressed(ForgeInput input, ForgeInput button)
    {
        return (input & button) != 0 && (_previousInput & button) == 0;
    }

    private void DrawMetric(int x, int y, string label, string value)
    {
        _ui.Text(x, y, label, UiTextKind.Muted);
        _ui.Text(x + 72, y, value);
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

    private void NextTheme()
    {
        SetTheme((_themeIndex + 1) % UiTheme.BuiltIns.Count);
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


    private readonly record struct TileResizeHandle(string Path, bool Vertical, double StartRatio, int Span);
    private readonly record struct TileResizeCandidate(TileResizeHandle Handle, RectI Divider);

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
