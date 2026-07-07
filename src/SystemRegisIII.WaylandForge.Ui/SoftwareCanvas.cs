namespace SystemRegisIII.WaylandForge.Ui;

public sealed unsafe class SoftwareCanvas
{
    private uint* _pixels;
    private int _width;
    private int _height;
    private int _stridePixels;
    private RectI _clip;
    private readonly Stack<RectI> _clipStack = new();

    public int Width => _width;
    public int Height => _height;

    public void Bind(uint* pixels, int width, int height, int stridePixels)
    {
        _pixels = pixels;
        _width = width;
        _height = height;
        _stridePixels = stridePixels;
        _clip = new RectI(0, 0, width, height);
        _clipStack.Clear();
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

    public void BlendRect(int x, int y, int width, int height, uint color)
    {
        Clip(ref x, ref y, ref width, ref height);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        uint alpha = color >> 24;
        if (alpha == 0)
        {
            return;
        }

        if (alpha == 255)
        {
            FillRect(x, y, width, height, color);
            return;
        }

        uint invAlpha = 255 - alpha;
        uint srcR = (color >> 16) & 0xff;
        uint srcG = (color >> 8) & 0xff;
        uint srcB = color & 0xff;
        for (int row = 0; row < height; row++)
        {
            uint* dst = _pixels + ((y + row) * _stridePixels) + x;
            for (int col = 0; col < width; col++)
            {
                uint dest = dst[col];
                uint r = (srcR * alpha + ((dest >> 16) & 0xff) * invAlpha) / 255;
                uint g = (srcG * alpha + ((dest >> 8) & 0xff) * invAlpha) / 255;
                uint b = (srcB * alpha + (dest & 0xff) * invAlpha) / 255;
                dst[col] = 0xff000000 | (r << 16) | (g << 8) | b;
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

    public void BlitArgbScaled(
        ReadOnlySpan<uint> source,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        int destX,
        int destY,
        int destWidth,
        int destHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || sourceStride < sourceWidth || destWidth <= 0 || destHeight <= 0)
        {
            return;
        }

        int clippedX = destX;
        int clippedY = destY;
        int clippedW = destWidth;
        int clippedH = destHeight;
        Clip(ref clippedX, ref clippedY, ref clippedW, ref clippedH);
        if (clippedW <= 0 || clippedH <= 0)
        {
            return;
        }

        for (int y = 0; y < clippedH; y++)
        {
            int destRow = clippedY + y;
            int sourceY = (destRow - destY) * sourceHeight / destHeight;
            int sourceRow = sourceY * sourceStride;
            uint* dst = _pixels + (destRow * _stridePixels) + clippedX;
            long sourceXFixed = ((long)(clippedX - destX) * sourceWidth << 16) / destWidth;
            long sourceXStep = ((long)sourceWidth << 16) / destWidth;

            for (int x = 0; x < clippedW; x++)
            {
                int sourceX = (int)(sourceXFixed >> 16);
                dst[x] = source[sourceRow + sourceX];
                sourceXFixed += sourceXStep;
            }
        }
    }

    public IDisposable PushClip(RectI rect)
    {
        _clipStack.Push(_clip);
        int x = Math.Max(_clip.X, rect.X);
        int y = Math.Max(_clip.Y, rect.Y);
        int right = Math.Min(_clip.Right, rect.Right);
        int bottom = Math.Min(_clip.Bottom, rect.Bottom);
        _clip = new RectI(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        return new ClipScope(this);
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
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height || !_clip.Contains(x, y))
        {
            return;
        }

        _pixels[(y * _stridePixels) + x] = color;
    }

    private void Clip(ref int x, ref int y, ref int width, ref int height)
    {
        if (x < _clip.X)
        {
            width -= _clip.X - x;
            x = _clip.X;
        }

        if (y < _clip.Y)
        {
            height -= _clip.Y - y;
            y = _clip.Y;
        }

        if (x + width > _clip.Right)
        {
            width = _clip.Right - x;
        }

        if (y + height > _clip.Bottom)
        {
            height = _clip.Bottom - y;
        }
    }

    private void PopClip()
    {
        _clip = _clipStack.Count > 0 ? _clipStack.Pop() : new RectI(0, 0, _width, _height);
    }

    private sealed class ClipScope : IDisposable
    {
        private SoftwareCanvas? _canvas;

        public ClipScope(SoftwareCanvas canvas)
        {
            _canvas = canvas;
        }

        public void Dispose()
        {
            _canvas?.PopClip();
            _canvas = null;
        }
    }
}
