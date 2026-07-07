namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class ForgeApp : IDisposable
{
    private const int TopBarHeight = 28;
    private const int StatusBarHeight = 24;
    private const int SidePanelWidth = 184;
    private const int MinimumWidthForSidePanel = 560;

    private readonly SoftwareCanvas _canvas = new();
    private ForgeInput _lastInput;

    public void Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input)
    {
        _lastInput = input;
        _canvas.Bind(pixels, width, height, stridePixels);

        _canvas.Clear(0xff111318);
        var layout = ForgeLayout.Calculate(width, height);

        DrawChrome(layout, frameIndex);
        DrawViewport(layout, frameIndex);
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

    private void DrawViewport(ForgeLayout layout, ulong frameIndex)
    {
        const int sourceW = 320;
        const int sourceH = 224;

        _canvas.FillRect(layout.ViewAreaX, layout.ViewAreaY, layout.ViewAreaW, layout.ViewAreaH, 0xff090b0d);
        _canvas.DrawRect(layout.ViewAreaX, layout.ViewAreaY, layout.ViewAreaW, layout.ViewAreaH, 0xff2c343c);

        if (layout.ViewportW <= 0 || layout.ViewportH <= 0)
        {
            return;
        }

        for (int y = 0; y < layout.ViewportH; y++)
        {
            int sourceY = y * sourceH / layout.ViewportH;
            for (int x = 0; x < layout.ViewportW; x++)
            {
                int sourceX = x * sourceW / layout.ViewportW;
                uint r = (uint)(((ulong)sourceX + frameIndex * 2) & 0xff);
                uint g = (uint)((sourceY * 255) / (sourceH - 1));
                uint b = (uint)(((sourceX / 12) ^ (sourceY / 10) ^ (int)(frameIndex / 8)) & 1) == 0 ? 0x36u : 0xc8u;
                uint color = 0xff000000u | (r << 16) | (g << 8) | b;
                _canvas.PutPixel(layout.ViewportX + x, layout.ViewportY + y, color);
            }
        }

        _canvas.DrawRect(layout.ViewportX - 1, layout.ViewportY - 1, layout.ViewportW + 2, layout.ViewportH + 2, 0xffd7e0e7);
        if (layout.ViewportY + layout.ViewportH + 18 < layout.Height - StatusBarHeight)
        {
            _canvas.DrawText(layout.ViewportX, layout.ViewportY + layout.ViewportH + 8, "EMULATOR VIEWPORT 320X224 FIT SCALE", 0xff91a1ad);
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
        int ViewAreaH,
        int ViewportX,
        int ViewportY,
        int ViewportW,
        int ViewportH)
    {
        public static ForgeLayout Calculate(int width, int height)
        {
            const int sourceW = 320;
            const int sourceH = 224;

            bool hasSidePanel = width >= MinimumWidthForSidePanel;
            int sidePanelX = hasSidePanel ? width - SidePanelWidth : width;
            int pad = width >= 520 ? 16 : 8;
            int viewAreaX = pad;
            int viewAreaY = TopBarHeight + pad;
            int viewAreaW = Math.Max(1, sidePanelX - pad * 2);
            int viewAreaH = Math.Max(1, height - TopBarHeight - StatusBarHeight - pad * 2);

            double scale = Math.Min(viewAreaW / (double)sourceW, viewAreaH / (double)sourceH);
            int viewportW = Math.Max(1, (int)Math.Floor(sourceW * scale));
            int viewportH = Math.Max(1, (int)Math.Floor(sourceH * scale));
            int viewportX = viewAreaX + (viewAreaW - viewportW) / 2;
            int viewportY = viewAreaY + (viewAreaH - viewportH) / 2;

            return new ForgeLayout(
                width,
                height,
                hasSidePanel,
                sidePanelX,
                viewAreaX,
                viewAreaY,
                viewAreaW,
                viewAreaH,
                viewportX,
                viewportY,
                viewportW,
                viewportH);
        }
    }
}
