using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SystemRegisIII.Host.WaylandForge;

internal static unsafe class Program
{
    private const int Width = 640;
    private const int Height = 480;

    public static int Main()
    {
        Console.WriteLine("WaylandForge M0: wl_shm framebuffer, gradient, frame callbacks, ESC quits.");
        Console.WriteLine("If no window appears, run this inside a Wayland session with WAYLAND_DISPLAY set.");

        int result = Native.waylandforge_run(Width, Height, "WaylandForge M0", &RenderFrame);
        if (result != 0)
        {
            Console.Error.WriteLine($"WaylandForge exited with native error {result}.");
        }

        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void RenderFrame(uint* pixels, int width, int height, int stridePixels, ulong frameIndex)
    {
        uint t = (uint)(frameIndex * 3);

        for (int y = 0; y < height; y++)
        {
            uint* row = pixels + (y * stridePixels);
            uint gy = (uint)((y * 255) / Math.Max(1, height - 1));

            for (int x = 0; x < width; x++)
            {
                uint rx = (uint)((x * 255) / Math.Max(1, width - 1));
                uint b = (uint)((x + y + t) & 0xff);
                row[x] = 0xff000000u | ((rx ^ t) << 16) | (gy << 8) | b;
            }
        }
    }
}

internal static unsafe partial class Native
{
    [LibraryImport("waylandforge_native", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int waylandforge_run(
        int width,
        int height,
        string title,
        delegate* unmanaged[Cdecl]<uint*, int, int, int, ulong, void> render);
}
