namespace SystemRegisIII.WaylandForge.Ui;

[Flags]
public enum PointerButtons : uint
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
}

public readonly record struct PointerState(int X, int Y, PointerButtons Buttons, bool IsInside)
{
    public bool LeftPressed => Buttons.HasFlag(PointerButtons.Left);
}
