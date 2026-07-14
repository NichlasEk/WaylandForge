namespace SystemRegisIII.Core;

[Flags]
public enum SaturnButtons : uint
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
    DeveloperSave = 1 << 19,
    DeveloperLoad = 1 << 20,
}

public readonly record struct SaturnInputState(SaturnButtons Buttons)
{
    public bool Escape => Buttons.HasFlag(SaturnButtons.Escape);
}

public interface IFrameSink
{
    void Present(ReadOnlySpan<uint> argb8888, int width, int height, int stridePixels);
}

public interface IInputSource
{
    SaturnInputState Poll();
}

public interface ISystemCore
{
    void Reset();
    void StepFrame(IInputSource input, IFrameSink frameSink);
}
