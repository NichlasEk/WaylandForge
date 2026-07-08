using System.Runtime.InteropServices;

namespace SystemRegisIII.Host.WaylandForge;

internal static unsafe class Program
{
    private const int Width = 640;
    private const int Height = 480;

    public static int Main()
    {
        Console.WriteLine("WaylandForge M1: double-buffered wl_shm host, software UI, configurable input state.");
        Console.WriteLine("If no window appears, run this inside a Wayland session with WAYLAND_DISPLAY set.");

        var app = new ForgeApp();
        bool disposed = false;
        void DisposeApp()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            app.Dispose();
        }

        Console.CancelKeyPress += (_, _) => DisposeApp();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeApp();
        PosixSignalRegistration? sigInt = null;
        PosixSignalRegistration? sigTerm = null;
        PosixSignalRegistration? sigHup = null;
        if (OperatingSystem.IsLinux())
        {
            sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                DisposeApp();
                Environment.Exit(130);
            });
            sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                DisposeApp();
                Environment.Exit(143);
            });
            sigHup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
            {
                context.Cancel = true;
                DisposeApp();
                Environment.Exit(129);
            });
        }

        int result = WaylandWindow.Run(Width, Height, "WaylandForge M1", app.Render, app.RawKeyInput);
        if (result != 0)
        {
            Console.Error.WriteLine($"WaylandForge exited with native error {result}.");
        }

        DisposeApp();
        sigInt?.Dispose();
        sigTerm?.Dispose();
        sigHup?.Dispose();
        return result;
    }
}
