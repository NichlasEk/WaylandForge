namespace SystemRegisIII.Host.WaylandForge;

[Flags]
internal enum PointerButtons : uint
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
}

internal readonly record struct PointerState(int X, int Y, PointerButtons Buttons, bool IsInside)
{
    public bool LeftPressed => Buttons.HasFlag(PointerButtons.Left);
}
