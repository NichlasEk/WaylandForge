using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class FrameStore : IFrameSink
{
    private uint[] _pixels = [];

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int StridePixels { get; private set; }
    public ReadOnlySpan<uint> Pixels => _pixels;

    public void Present(ReadOnlySpan<uint> argb8888, int width, int height, int stridePixels)
    {
        if (width <= 0 || height <= 0 || stridePixels < width)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Invalid frame dimensions.");
        }

        int required = width * height;
        if (_pixels.Length != required)
        {
            _pixels = new uint[required];
        }

        for (int y = 0; y < height; y++)
        {
            argb8888.Slice(y * stridePixels, width).CopyTo(_pixels.AsSpan(y * width, width));
        }

        Width = width;
        Height = height;
        StridePixels = width;
    }
}
