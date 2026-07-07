namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class ForgeApp : IDisposable
{
    private readonly SoftwareCanvas _canvas = new();
    private ForgeInput _lastInput;

    public void Render(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input)
    {
        _lastInput = input;
        _canvas.Bind(pixels, width, height, stridePixels);

        _canvas.Clear(0xff111318);
        DrawChrome(width, height, frameIndex);
        DrawViewport(width, height, frameIndex);
        DrawDebugPanel(width, height, frameIndex);
        DrawStatusBar(width, height, input);
    }

    public void Dispose()
    {
    }

    private void DrawChrome(int width, int height, ulong frameIndex)
    {
        _canvas.FillRect(0, 0, width, 28, 0xff1f252b);
        _canvas.FillRect(0, height - 24, width, 24, 0xff1f252b);
        _canvas.FillRect(width - 184, 28, 184, height - 52, 0xff181d22);
        _canvas.DrawRect(0, 0, width, height, 0xff39424c);
        _canvas.DrawLine(0, 28, width, 28, 0xff39424c);
        _canvas.DrawLine(0, height - 25, width, height - 25, 0xff39424c);
        _canvas.DrawLine(width - 185, 28, width - 185, height - 24, 0xff39424c);

        _canvas.DrawText(12, 10, "WAYLANDFORGE", 0xffe8edf2, 2);
        _canvas.DrawText(214, 11, "CPU UI / SHM / DOUBLE BUFFER", 0xff91a1ad);
        _canvas.DrawText(width - 174, 11, $"FRAME {frameIndex}", 0xff91a1ad);
    }

    private void DrawViewport(int width, int height, ulong frameIndex)
    {
        int panelX = width - 184;
        int barTop = 28;
        int barBottom = 24;
        int pad = 16;
        int areaX = pad;
        int areaY = barTop + pad;
        int areaW = panelX - pad * 2;
        int areaH = height - barTop - barBottom - pad * 2;

        const int sourceW = 320;
        const int sourceH = 224;
        int scale = Math.Max(1, Math.Min(areaW / sourceW, areaH / sourceH));
        int viewW = sourceW * scale;
        int viewH = sourceH * scale;
        int viewX = areaX + (areaW - viewW) / 2;
        int viewY = areaY + (areaH - viewH) / 2;

        _canvas.FillRect(areaX, areaY, areaW, areaH, 0xff090b0d);
        _canvas.DrawRect(areaX, areaY, areaW, areaH, 0xff2c343c);

        for (int y = 0; y < sourceH; y++)
        {
            for (int x = 0; x < sourceW; x++)
            {
                uint r = (uint)(((ulong)x + frameIndex * 2) & 0xff);
                uint g = (uint)((y * 255) / (sourceH - 1));
                uint b = (uint)(((x / 12) ^ (y / 10) ^ (int)(frameIndex / 8)) & 1) == 0 ? 0x36u : 0xc8u;
                uint color = 0xff000000u | (r << 16) | (g << 8) | b;
                _canvas.FillRect(viewX + x * scale, viewY + y * scale, scale, scale, color);
            }
        }

        _canvas.DrawRect(viewX - 1, viewY - 1, viewW + 2, viewH + 2, 0xffd7e0e7);
        _canvas.DrawText(viewX, viewY + viewH + 8, "EMULATOR VIEWPORT 320X224 INTEGER SCALE", 0xff91a1ad);
    }

    private void DrawDebugPanel(int width, int height, ulong frameIndex)
    {
        int x = width - 168;
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

        _canvas.DrawText(x, height - 78, "ESC CLOSES", 0xffffc857);
        _canvas.DrawText(x, height - 60, "NO GTK QT SDL", 0xff91a1ad);
    }

    private void DrawStatusBar(int width, int height, ForgeInput input)
    {
        string inputText = input == ForgeInput.None ? "INPUT: NONE" : $"INPUT: {input}";
        _canvas.DrawText(12, height - 16, inputText.ToUpperInvariant(), 0xffc7d1d9);
        _canvas.DrawText(width - 235, height - 16, "WAYLAND FRAME CALLBACK DRIVEN", 0xff91a1ad);
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
}
