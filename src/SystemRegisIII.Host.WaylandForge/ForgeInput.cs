namespace SystemRegisIII.Host.WaylandForge;

[Flags]
internal enum ForgeInput : uint
{
    None = 0,
    Escape = 1 << 0,
    Up = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3,
    Right = 1 << 4,
    Start = 1 << 5,
    A = 1 << 6,
    B = 1 << 7,
    C = 1 << 8,
    X = 1 << 9,
    Y = 1 << 10,
    Z = 1 << 11,
    ScaleFit = 1 << 12,
    ScaleInteger = 1 << 13,
    ScaleStretch = 1 << 14,
    ThemeNext = 1 << 15,
}
