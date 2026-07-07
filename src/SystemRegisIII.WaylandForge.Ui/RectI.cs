namespace SystemRegisIII.WaylandForge.Ui;

public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y)
    {
        return x >= X && x < Right && y >= Y && y < Bottom;
    }
}
