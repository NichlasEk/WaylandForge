using System.Diagnostics;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class FrameClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private double _lastSeconds;
    private double _fpsWindowStart;
    private int _fpsWindowFrames;
    private double _drawMilliseconds;

    public double DeltaSeconds { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public double FramesPerSecond { get; private set; }
    public double FrameMilliseconds => DeltaSeconds * 1000.0;
    public double DrawMilliseconds => _drawMilliseconds;

    public void Tick()
    {
        ElapsedSeconds = _watch.Elapsed.TotalSeconds;
        DeltaSeconds = _lastSeconds == 0 ? 1.0 / 60.0 : Math.Max(0.0, ElapsedSeconds - _lastSeconds);
        _lastSeconds = ElapsedSeconds;

        _fpsWindowFrames++;
        double window = ElapsedSeconds - _fpsWindowStart;
        if (window >= 0.5)
        {
            FramesPerSecond = _fpsWindowFrames / window;
            _fpsWindowFrames = 0;
            _fpsWindowStart = ElapsedSeconds;
        }
    }

    public void RecordDraw(double milliseconds)
    {
        _drawMilliseconds = _drawMilliseconds == 0 ? milliseconds : _drawMilliseconds * 0.85 + milliseconds * 0.15;
    }
}
