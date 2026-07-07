using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class ForgeInputSource : IInputSource
{
    private SaturnInputState _state;

    public void Update(ForgeInput input)
    {
        _state = new SaturnInputState((SaturnButtons)(uint)input);
    }

    public SaturnInputState Poll() => _state;
}
