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
    private ForgeInput _lastInput;
    private ForgeInput _previousInput;
    private PointerState _pointer;
    private PointerState _previousPointer;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private ulong _hostFrameIndex;

    public void Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input, PointerState pointer)
    {
        Update(input, pointer, frameIndex, width, height);

        _canvas.Bind(pixels, width, height, stridePixels);
        Draw(width, height);
    }

    private void Update(ForgeInput input, PointerState pointer, ulong frameIndex, int width, int height)
    {
        _clock.Tick();
        _lastInput = input;
        _pointer = pointer;
        _surfaceWidth = width;
        _surfaceHeight = height;
        _hostFrameIndex = frameIndex;
        HandleHostShortcuts(input);

        _inputSource.Update(input);
        _core.StepFrame(_inputSource, _frameStore);
        _previousInput = input;
        _previousPointer = pointer;
    }

    private void Draw(int width, int height)
    {
        _canvas.Clear(0xff111318);
        var layout = ForgeLayout.Calculate(width, height);

        DrawChrome(layout);
        DrawViewport(layout);
        if (layout.HasSidePanel)
        {
            DrawDebugPanel(layout);
        }
        DrawScaleToggles(layout);
        DrawStatusBar(layout);
    }

    public void Dispose()
    {
    }

    private void DrawChrome(ForgeLayout layout)
    {
        _canvas.FillRect(0, 0, layout.Width, TopBarHeight, 0xff1f252b);
        _canvas.FillRect(0, layout.Height - StatusBarHeight, layout.Width, StatusBarHeight, 0xff1f252b);
        if (layout.HasSidePanel)
        {
            _canvas.FillRect(layout.SidePanelX, TopBarHeight, SidePanelWidth, layout.Height - TopBarHeight - StatusBarHeight, 0xff181d22);
        }

        _canvas.DrawRect(0, 0, layout.Width, layout.Height, 0xff39424c);
        _canvas.DrawLine(0, TopBarHeight, layout.Width, TopBarHeight, 0xff39424c);
        _canvas.DrawLine(0, layout.Height - StatusBarHeight - 1, layout.Width, layout.Height - StatusBarHeight - 1, 0xff39424c);
        if (layout.HasSidePanel)
        {
            _canvas.DrawLine(layout.SidePanelX - 1, TopBarHeight, layout.SidePanelX - 1, layout.Height - StatusBarHeight, 0xff39424c);
        }

        _canvas.DrawText(12, 10, "WAYLANDFORGE", 0xffe8edf2, 2);
        if (layout.Width >= 520)
        {
            _canvas.DrawText(214, 11, "CPU UI / SHM / DOUBLE BUFFER", 0xff91a1ad);
        }
        if (layout.Width >= 760)
        {
            _canvas.DrawText(layout.Width - 174, 11, $"FRAME {_hostFrameIndex}", 0xff91a1ad);
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
        int x = layout.SidePanelX + 16;
        int y = 48;

        _canvas.DrawText(x, y, "HOST", 0xffe8edf2, 2);
        y += 28;
        DrawMetric(x, y, "BACKEND", "WAYLAND"); y += 18;
        DrawMetric(x, y, "BUFFER", "WL_SHM X2"); y += 18;
        DrawMetric(x, y, "FORMAT", "ARGB8888"); y += 18;
        DrawMetric(x, y, "CORE", "FAKE SATURN"); y += 18;
        DrawMetric(x, y, "SOURCE", $"{_frameStore.Width}X{_frameStore.Height}"); y += 18;
        DrawMetric(x, y, "SCALE", _viewport.ScaleMode.ToString().ToUpperInvariant()); y += 18;
        DrawMetric(x, y, "FPS", _clock.FramesPerSecond.ToString("0.0")); y += 18;
        DrawMetric(x, y, "MS", _clock.FrameMilliseconds.ToString("0.00")); y += 18;
        DrawMetric(x, y, "PTR", _pointer.IsInside ? $"{_pointer.X},{_pointer.Y}" : "OUT"); y += 18;
        DrawMetric(x, y, "MBTN", _pointer.Buttons.ToString().ToUpperInvariant()); y += 18;
        DrawMetric(x, y, "FRAME", _hostFrameIndex.ToString()); y += 26;

        _canvas.DrawText(x, y, "INPUT", 0xffe8edf2, 2);
        y += 28;
        DrawInputLamp(x, y, "UP", ForgeInput.Up); y += 16;
        DrawInputLamp(x, y, "DOWN", ForgeInput.Down); y += 16;
        DrawInputLamp(x, y, "LEFT", ForgeInput.Left); y += 16;
        DrawInputLamp(x, y, "RIGHT", ForgeInput.Right); y += 16;
        DrawInputLamp(x, y, "START", ForgeInput.Start); y += 16;
        DrawInputLamp(x, y, "A B C", ForgeInput.A | ForgeInput.B | ForgeInput.C); y += 16;
        DrawInputLamp(x, y, "X Y Z", ForgeInput.X | ForgeInput.Y | ForgeInput.Z);

        _canvas.DrawText(x, layout.Height - 78, "ESC CLOSES", 0xffffc857);
        _canvas.DrawText(x, layout.Height - 60, "NO GTK QT SDL", 0xff91a1ad);
    }

    private void DrawStatusBar(ForgeLayout layout)
    {
        string inputText = _lastInput == ForgeInput.None ? "INPUT: NONE" : $"INPUT: {_lastInput}";
        _canvas.DrawText(12, layout.Height - 16, inputText.ToUpperInvariant(), 0xffc7d1d9);
        if (layout.Width >= 760)
        {
            _canvas.DrawText(layout.Width - 378, layout.Height - 16, "1/2/3 OR CLICK SCALE", 0xff91a1ad);
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
        DrawScaleButton(new RectI(x, y, 48, 15), "FIT", ViewportScaleMode.Fit);
        DrawScaleButton(new RectI(x + 52, y, 48, 15), "INT", ViewportScaleMode.Integer);
        DrawScaleButton(new RectI(x + 104, y, 48, 15), "STR", ViewportScaleMode.Stretch);
    }

    private void DrawScaleButton(RectI rect, string label, ViewportScaleMode mode)
    {
        bool active = _viewport.ScaleMode == mode;
        bool hover = _pointer.IsInside && Contains(rect, _pointer.X, _pointer.Y);
        uint fill = active ? 0xff355c7d : hover ? 0xff28333d : 0xff181d22;
        uint border = active ? 0xff82cfff : hover ? 0xff91a1ad : 0xff39424c;
        uint text = active ? 0xffe8edf2 : 0xff91a1ad;

        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, fill);
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, border);
        _canvas.DrawText(rect.X + 6, rect.Y + 4, label, text);
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

        if (PointerClicked())
        {
            if (TryHitScaleMode(_pointer.X, _pointer.Y, out ViewportScaleMode mode))
            {
                _viewport.ScaleMode = mode;
            }
        }
    }

    private bool Pressed(ForgeInput input, ForgeInput button)
    {
        return (input & button) != 0 && (_previousInput & button) == 0;
    }

    private bool PointerClicked()
    {
        return _pointer.IsInside && _pointer.LeftPressed && !_previousPointer.LeftPressed;
    }

    private bool TryHitScaleMode(int x, int y, out ViewportScaleMode mode)
    {
        var layout = ForgeLayout.Calculate(_surfaceWidth, _surfaceHeight);
        if (layout.Width < 420)
        {
            mode = default;
            return false;
        }

        int buttonY = layout.Height - 21;
        int buttonX = layout.Width >= 720 ? layout.Width - 206 : layout.Width - 194;
        if (Contains(new RectI(buttonX, buttonY, 48, 15), x, y))
        {
            mode = ViewportScaleMode.Fit;
            return true;
        }
        if (Contains(new RectI(buttonX + 52, buttonY, 48, 15), x, y))
        {
            mode = ViewportScaleMode.Integer;
            return true;
        }
        if (Contains(new RectI(buttonX + 104, buttonY, 48, 15), x, y))
        {
            mode = ViewportScaleMode.Stretch;
            return true;
        }

        mode = default;
        return false;
    }

    private static bool Contains(RectI rect, int x, int y)
    {
        return x >= rect.X && x < rect.Right && y >= rect.Y && y < rect.Bottom;
    }

    private void DrawMetric(int x, int y, string label, string value)
    {
        _canvas.DrawText(x, y, label, 0xff70808d);
        _canvas.DrawText(x + 72, y, value, 0xffc7d1d9);
    }

    private void DrawInputLamp(int x, int y, string label, ForgeInput bit)
    {
        bool active = (_lastInput & bit) != 0;
        uint color = active ? 0xff58d68d : 0xff39424c;
        _canvas.FillRect(x, y + 2, 8, 8, color);
        _canvas.DrawRect(x, y + 2, 8, 8, 0xff70808d);
        _canvas.DrawText(x + 16, y, label, active ? 0xffe8edf2 : 0xff91a1ad);
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
