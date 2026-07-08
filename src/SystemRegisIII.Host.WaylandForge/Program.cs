namespace SystemRegisIII.Host.WaylandForge;

internal static unsafe class Program
{
    private const int Width = 640;
    private const int Height = 480;

    public static int Main()
    {
        Console.WriteLine("WaylandForge M1: double-buffered wl_shm host, software UI, configurable input state.");
        Console.WriteLine("If no window appears, run this inside a Wayland session with WAYLAND_DISPLAY set.");

        using var app = new ForgeApp();
        int result = WaylandWindow.Run(Width, Height, "WaylandForge M1", app.Render);
        if (result != 0)
        {
            Console.Error.WriteLine($"WaylandForge exited with native error {result}.");
        }

        return result;
    }
}
