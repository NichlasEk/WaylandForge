using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class ForgeInputSource : IInputSource
{
    private SaturnInputState _state;

    public void Update(ForgeInput input)
    {
        const ForgeInput coreMask =
            ForgeInput.Escape |
            ForgeInput.Up |
            ForgeInput.Down |
            ForgeInput.Left |
            ForgeInput.Right |
            ForgeInput.Start |
            ForgeInput.A |
            ForgeInput.B |
            ForgeInput.C |
            ForgeInput.X |
            ForgeInput.Y |
            ForgeInput.Z;

        _state = new SaturnInputState((SaturnButtons)(uint)(input & coreMask));
    }

    public SaturnInputState Poll() => _state;
}
