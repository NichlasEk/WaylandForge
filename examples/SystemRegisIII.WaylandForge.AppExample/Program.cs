using SystemRegisIII.WaylandForge.App;
using SystemRegisIII.WaylandForge.Ui;

return ForgeApplicationHost.Run(
    new ExampleApplication(),
    new ForgeWindowOptions(720, 480, "WaylandForge App Example"));

file sealed class ExampleApplication : IForgeApplication
{
    private PointerState _previousPointer;

    public void Render(in ForgeFrame frame)
    {
        var ui = new UiContext(frame.Canvas, UiTheme.Default);
        ui.BeginFrame(frame.Pointer, _previousPointer, frame.TextInput, frame.ScrollInput);

        frame.Canvas.Clear(0xff10161c);
        RectI panel = ui.Panel(new RectI(24, 24, Math.Max(240, frame.Canvas.Width - 48), 112), "WAYLANDFORGE APP");
        ui.Text(panel.X, panel.Y, "Standalone host: window + input + software canvas", UiTextKind.Accent);
        ui.Text(panel.X, panel.Y + 24, $"Frame {frame.FrameIndex}  Pointer {frame.Pointer.X},{frame.Pointer.Y}");
        _previousPointer = frame.Pointer;
    }
}
