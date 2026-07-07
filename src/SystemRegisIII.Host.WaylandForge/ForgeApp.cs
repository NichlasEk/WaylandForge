using SystemRegisIII.WaylandForge.Ui;
using System.Diagnostics;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class ForgeApp : IDisposable
{
    private const int TopBarHeight = 28;
    private const int StatusBarHeight = 24;
    private const int SidePanelWidth = 184;
    private const int MinimumWidthForSidePanel = 560;

    private readonly SoftwareCanvas _canvas = new();
    private readonly FakeSaturnCore _core = new();
    private readonly ForgeInputSource _inputSource = new();
    private readonly FrameStore _frameStore = new();
    private readonly EmulatorViewport _viewport = new();
    private readonly FrameClock _clock = new();
    private readonly UiContext _ui;
    private readonly UiFilePicker _filePicker = new();
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
    private string? _romPath;
    private int _themeIndex;

    public ForgeApp()
    {
        _ui = new UiContext(_canvas, UiTheme.Default);
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
        DrawFilePicker(layout);
    }

    public void Dispose()
    {
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
            _filePickerWindow.IsOpen = true;
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

    private void DrawFilePicker(ForgeLayout layout)
    {
        if (!_filePickerWindow.IsOpen)
        {
            return;
        }

        int width = Math.Min(980, Math.Max(360, layout.Width - (layout.HasSidePanel ? SidePanelWidth + 80 : 48)));
        int height = Math.Min(680, Math.Max(320, layout.Height - 96));
        RectI preferredRect = new((layout.Width - width) / 2, (layout.Height - height) / 2, width, height);
        RectI bounds = new(0, TopBarHeight, layout.Width, Math.Max(1, layout.Height - TopBarHeight - StatusBarHeight));
        UiWindowResult window = _ui.BeginWindow(new UiId("filepicker.window"), _filePickerWindow, preferredRect, bounds, "ROM PICKER");
        if (!window.IsOpen || window.Closed)
        {
            return;
        }

        UiFilePickerResult result = _filePicker.Draw(_ui, window.Content, "");
        if (result.Accepted && result.SelectedPath is not null)
        {
            _romPath = result.SelectedPath;
            _filePickerWindow.IsOpen = false;
        }
        else if (result.Cancelled)
        {
            _filePickerWindow.IsOpen = false;
        }
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
            _viewport.ScaleMode = ViewportScaleMode.Fit;
        }
        row = row.Next(48, out RectI intRect);
        if (_ui.ToggleButton(intRect, "INT", _viewport.ScaleMode == ViewportScaleMode.Integer))
        {
            _viewport.ScaleMode = ViewportScaleMode.Integer;
        }
        row = row.Next(48, out RectI stretchRect);
        if (_ui.ToggleButton(stretchRect, "STR", _viewport.ScaleMode == ViewportScaleMode.Stretch))
        {
            _viewport.ScaleMode = ViewportScaleMode.Stretch;
        }
    }

    private void HandleHostShortcuts(ForgeInput input)
    {
        if (Pressed(input, ForgeInput.ScaleFit))
        {
            _viewport.ScaleMode = ViewportScaleMode.Fit;
        }
        else if (Pressed(input, ForgeInput.ScaleInteger))
        {
            _viewport.ScaleMode = ViewportScaleMode.Integer;
        }
        else if (Pressed(input, ForgeInput.ScaleStretch))
        {
            _viewport.ScaleMode = ViewportScaleMode.Stretch;
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
        _themeIndex = (_themeIndex + 1) % UiTheme.BuiltIns.Count;
        _ui.Theme = UiTheme.BuiltIns[_themeIndex];
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
