namespace SystemRegisIII.Core;

public readonly record struct SaturnInputState(bool Escape);

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
