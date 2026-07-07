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
    private readonly UiWindowState _filePickerWindow = new();
    private readonly UiWindowState _settingsWindow = new();
    private readonly List<AppWindow> _windowOrder = [AppWindow.Settings, AppWindow.Rom];
    private string? _romPath;
    private int _themeIndex;
    private bool _configDirty;
    private double _lastConfigSaveSeconds;
    private bool _resizingTileSplit;
    private int _tileResizeStartX;
    private int _tileResizeStartWidth;

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
    }

    private void DrawViewport(ForgeLayout layout)
    {
        var area = new RectI(layout.ViewAreaX, layout.ViewAreaY, layout.ViewAreaW, layout.ViewAreaH);
        _viewport.Draw(_canvas, area, _frameStore.Pixels, _frameStore.Width, _frameStore.Height, _frameStore.StridePixels);

        RectI content = _viewport.ContentRect;
        if (content.Bottom + 18 < layout.Height - StatusBarHeight)
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
        AppWindow? inputWindow = HitTestTopWindow();
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
            }
        }
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
            BringToFront(AppWindow.Rom);
        }
        if (window.Dragging)
        {
            MarkConfigDirty();
        }
        if (!window.IsOpen || window.Closed)
        {
            MarkConfigDirty();
            return;
        }

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
            BringToFront(AppWindow.Settings);
        }
        if (window.Dragging)
        {
            MarkConfigDirty();
        }
        if (!window.IsOpen || window.Closed)
        {
            MarkConfigDirty();
            return;
        }

        using IDisposable inputScope = _ui.PushInputEnabled(inputEnabled);
        RectI content = window.Content;
        _ui.Panel(content);
        var column = new UiColumn(content.X + 10, content.Y + 10, Math.Max(1, content.Width - 20), 8);

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

        _ui.Text(column.X, column.NextY, "HOST", UiTextKind.Muted); column = column with { NextY = column.NextY + 16 };
        DrawMetric(column.X, column.NextY, "FPS", _clock.FramesPerSecond.ToString("0.0")); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "FRAME MS", _clock.FrameMilliseconds.ToString("0.0")); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "DRAW MS", _clock.DrawMilliseconds.ToString("0.0")); column = column with { NextY = column.NextY + 28 };

        _ui.Text(column.X, column.NextY, "WINDOWS", UiTextKind.Muted); column = column with { NextY = column.NextY + 16 };
        DrawMetric(column.X, column.NextY, "MODE", _config.WindowMode.ToString().ToUpperInvariant()); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "ACTIVE", TopOpenWindow()?.ToString().ToUpperInvariant() ?? "-"); column = column with { NextY = column.NextY + 18 };
        DrawMetric(column.X, column.NextY, "ROM", _romPath is null ? "NONE" : TruncateMiddle(_romPath, 28));
    }



    private void HandleTileEdit(ForgeLayout layout)
    {
        if (_config.WindowMode != UiWindowMode.Tiled || !_settingsWindow.IsOpen || !IsTileEditModifierDown())
        {
            _resizingTileSplit = false;
            return;
        }

        RectI work = TileWorkArea(layout);
        RectI settings = TiledWindowRect(layout, AppWindow.Settings);
        UiWindowConfig settingsConfig = _config.Window("settings");
        bool settingsLeft = string.Equals(settingsConfig.Slot, "left", StringComparison.OrdinalIgnoreCase);
        int dividerX = settingsLeft ? settings.Right : settings.X;
        RectI divider = new(dividerX - 5, work.Y, 10, work.Height);

        if (_pointer.IsInside && divider.Contains(_pointer.X, _pointer.Y) && _pointer.LeftPressed && !_previousPointer.LeftPressed)
        {
            _resizingTileSplit = true;
            _tileResizeStartX = _pointer.X;
            _tileResizeStartWidth = Math.Max(260, settings.Width);
        }

        if (!_pointer.LeftPressed)
        {
            _resizingTileSplit = false;
        }

        if (_resizingTileSplit)
        {
            int delta = _pointer.X - _tileResizeStartX;
            int width = settingsLeft ? _tileResizeStartWidth + delta : _tileResizeStartWidth - delta;
            settingsConfig.Width = Math.Clamp(width, 220, Math.Max(220, work.Width - 260));
            MarkConfigDirty();
        }

        if (_settingsWindow.IsDragging && _settingsWindow.Rect is RectI dragged)
        {
            string newSlot = dragged.X + dragged.Width / 2 < work.X + work.Width / 2 ? "left" : "right";
            if (!string.Equals(settingsConfig.Slot, newSlot, StringComparison.OrdinalIgnoreCase))
            {
                settingsConfig.Slot = newSlot;
                MarkConfigDirty();
            }
        }
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
        UiWindowConfig settingsConfig = _config.Window("settings");
        bool settingsOpen = _settingsWindow.IsOpen;
        int defaultSettingsWidth = Math.Min(360, Math.Max(260, work.Width / 3));
        int settingsWidth = settingsOpen ? Math.Clamp(settingsConfig.Width > 0 ? settingsConfig.Width : defaultSettingsWidth, 220, Math.Max(220, work.Width - 260)) : 0;
        int gap = settingsOpen ? 8 : 0;
        bool settingsLeft = string.Equals(settingsConfig.Slot, "left", StringComparison.OrdinalIgnoreCase);

        if (window == AppWindow.Settings)
        {
            return settingsLeft
                ? new RectI(work.X, work.Y, settingsWidth, work.Height)
                : new RectI(work.Right - settingsWidth, work.Y, settingsWidth, work.Height);
        }

        if (window == AppWindow.Rom)
        {
            return settingsLeft
                ? new RectI(work.X + settingsWidth + gap, work.Y, Math.Max(240, work.Width - settingsWidth - gap), work.Height)
                : new RectI(work.X, work.Y, Math.Max(240, work.Width - settingsWidth - gap), work.Height);
        }

        throw new ArgumentOutOfRangeException(nameof(window));
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
        AppWindow.Rom => _filePickerWindow,
        AppWindow.Settings => _settingsWindow,
        _ => throw new ArgumentOutOfRangeException(nameof(window)),
    };

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
        ApplyWindowConfig(AppWindow.Rom, "rom_picker");
        ApplyWindowConfig(AppWindow.Settings, "settings");
        _windowOrder.Clear();
        _windowOrder.AddRange(Enum.GetValues<AppWindow>().OrderBy(window => _config.Window(WindowKey(window)).Order));
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
        SnapshotWindow(AppWindow.Rom, "rom_picker");
        SnapshotWindow(AppWindow.Settings, "settings");
        for (int i = 0; i < _windowOrder.Count; i++)
        {
            _config.Window(WindowKey(_windowOrder[i])).Order = i * 10;
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

    private static string WindowKey(AppWindow window) => window switch
    {
        AppWindow.Rom => "rom_picker",
        AppWindow.Settings => "settings",
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


    private enum AppWindow
    {
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
