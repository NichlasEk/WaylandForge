namespace SystemRegisIII.Host.WaylandForge;

internal sealed unsafe class SoftwareCanvas
{
    private uint* _pixels;
    private int _width;
    private int _height;
    private int _stridePixels;

    public void Bind(uint* pixels, int width, int height, int stridePixels)
    {
        _pixels = pixels;
        _width = width;
        _height = height;
        _stridePixels = stridePixels;
    }

    public void Clear(uint color)
    {
        FillRect(0, 0, _width, _height, color);
    }

    public void FillRect(int x, int y, int width, int height, uint color)
    {
        Clip(ref x, ref y, ref width, ref height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        for (int row = 0; row < height; row++)
        {
            uint* dst = _pixels + ((y + row) * _stridePixels) + x;
            for (int col = 0; col < width; col++)
            {
                dst[col] = color;
            }
        }
    }

    public void DrawRect(int x, int y, int width, int height, uint color)
    {
        DrawLine(x, y, x + width - 1, y, color);
        DrawLine(x, y + height - 1, x + width - 1, y + height - 1, color);
        DrawLine(x, y, x, y + height - 1, color);
        DrawLine(x + width - 1, y, x + width - 1, y + height - 1, color);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, uint color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PutPixel(x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    public void DrawText(int x, int y, string text, uint color, int scale = 1)
    {
        int cursor = x;
        foreach (char ch in text)
        {
            if (ch == ' ')
            {
                cursor += 4 * scale;
                continue;
            }

            DrawGlyph(cursor, y, char.ToUpperInvariant(ch), color, scale);
            cursor += 6 * scale;
        }
    }

    private void DrawGlyph(int x, int y, char ch, uint color, int scale)
    {
        ReadOnlySpan<byte> glyph = Font5x7.Get(ch);
        for (int row = 0; row < glyph.Length; row++)
        {
            byte bits = glyph[row];
            for (int col = 0; col < 5; col++)
            {
                if ((bits & (1 << (4 - col))) != 0)
                {
                    FillRect(x + col * scale, y + row * scale, scale, scale, color);
                }
            }
        }
    }

    public void PutPixel(int x, int y, uint color)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
        {
            return;
        }

        _pixels[(y * _stridePixels) + x] = color;
    }

    private void Clip(ref int x, ref int y, ref int width, ref int height)
    {
        if (x < 0)
        {
            width += x;
            x = 0;
        }

        if (y < 0)
        {
            height += y;
            y = 0;
        }

        if (x + width > _width)
        {
            width = _width - x;
        }

        if (y + height > _height)
        {
            height = _height - y;
        }
    }
}
