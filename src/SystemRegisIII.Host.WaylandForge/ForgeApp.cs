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
    private ForgeInput _lastInput;

    public void Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input)
    {
        _lastInput = input;
        _inputSource.Update(input);
        _core.StepFrame(_inputSource, _frameStore);

        _canvas.Bind(pixels, width, height, stridePixels);

        _canvas.Clear(0xff111318);
        var layout = ForgeLayout.Calculate(width, height);

        DrawChrome(layout, frameIndex);
        DrawViewport(layout);
        if (layout.HasSidePanel)
        {
            DrawDebugPanel(layout, frameIndex);
        }
        DrawStatusBar(layout, input);
    }

    public void Dispose()
    {
    }

    private void DrawChrome(ForgeLayout layout, ulong frameIndex)
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
            _canvas.DrawText(layout.Width - 174, 11, $"FRAME {frameIndex}", 0xff91a1ad);
        }
    }

    private void DrawViewport(ForgeLayout layout)
    {
        var area = new RectI(layout.ViewAreaX, layout.ViewAreaY, layout.ViewAreaW, layout.ViewAreaH);
        _viewport.Draw(_canvas, area, _frameStore.Pixels, _frameStore.Width, _frameStore.Height, _frameStore.StridePixels);

        RectI content = _viewport.ContentRect;
        if (content.Bottom + 18 < layout.Height - StatusBarHeight)
        {
            _canvas.DrawText(content.X, content.Bottom + 8, $"EMULATOR VIEWPORT {_frameStore.Width}X{_frameStore.Height} FIT SCALE", 0xff91a1ad);
        }
    }

    private void DrawDebugPanel(ForgeLayout layout, ulong frameIndex)
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
        DrawMetric(x, y, "FRAME", frameIndex.ToString()); y += 26;

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

    private void DrawStatusBar(ForgeLayout layout, ForgeInput input)
    {
        string inputText = input == ForgeInput.None ? "INPUT: NONE" : $"INPUT: {input}";
        _canvas.DrawText(12, layout.Height - 16, inputText.ToUpperInvariant(), 0xffc7d1d9);
        if (layout.Width >= 520)
        {
            _canvas.DrawText(layout.Width - 235, layout.Height - 16, "WAYLAND FRAME CALLBACK DRIVEN", 0xff91a1ad);
        }
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
