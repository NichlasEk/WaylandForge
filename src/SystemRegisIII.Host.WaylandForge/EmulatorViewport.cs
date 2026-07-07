using SystemRegisIII.WaylandForge.Ui;

namespace SystemRegisIII.Host.WaylandForge;

internal enum ViewportScaleMode
{
    Fit,
    Integer,
    Stretch,
}

internal sealed class EmulatorViewport
{
    public ViewportScaleMode ScaleMode { get; set; } = ViewportScaleMode.Fit;

    public RectI ContentRect { get; private set; }

    public void Draw(SoftwareCanvas canvas, RectI area, ReadOnlySpan<uint> frame, int sourceWidth, int sourceHeight, int sourceStride)
    {
        canvas.FillRect(area.X, area.Y, area.Width, area.Height, 0xff090b0d);
        canvas.DrawRect(area.X, area.Y, area.Width, area.Height, 0xff2c343c);

        if (sourceWidth <= 0 || sourceHeight <= 0 || frame.IsEmpty)
        {
            ContentRect = new RectI(area.X, area.Y, 0, 0);
            return;
        }

        ContentRect = CalculateContentRect(area, sourceWidth, sourceHeight);
        canvas.BlitArgbScaled(frame, sourceWidth, sourceHeight, sourceStride, ContentRect.X, ContentRect.Y, ContentRect.Width, ContentRect.Height);
        canvas.DrawRect(ContentRect.X - 1, ContentRect.Y - 1, ContentRect.Width + 2, ContentRect.Height + 2, 0xffd7e0e7);
    }

    private RectI CalculateContentRect(RectI area, int sourceWidth, int sourceHeight)
    {
        if (ScaleMode == ViewportScaleMode.Stretch)
        {
            return area;
        }

        double scale = Math.Min(area.Width / (double)sourceWidth, area.Height / (double)sourceHeight);
        if (ScaleMode == ViewportScaleMode.Integer)
        {
            scale = Math.Max(1, Math.Floor(scale));
        }

        int width = Math.Max(1, Math.Min(area.Width, (int)Math.Floor(sourceWidth * scale)));
        int height = Math.Max(1, Math.Min(area.Height, (int)Math.Floor(sourceHeight * scale)));
        int x = area.X + (area.Width - width) / 2;
        int y = area.Y + (area.Height - height) / 2;

        return new RectI(x, y, width, height);
    }
}
