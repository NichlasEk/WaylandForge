using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SystemRegisIII.WaylandForge.Ui;

namespace SystemRegisIII.WaylandForge.App;

public readonly record struct ForgeWindowOptions(int Width, int Height, string Title)
{
    public static ForgeWindowOptions Default => new(960, 640, "WaylandForge App");
}

public readonly record struct ForgeKeyEvent(uint KeyCode, uint Serial, bool Pressed);

public readonly record struct ForgeFrame(
    SoftwareCanvas Canvas,
    ulong FrameIndex,
    uint ActionMask,
    PointerState Pointer,
    TextInputEvent TextInput,
    ScrollInputEvent ScrollInput);

public interface IForgeApplication
{
    void Render(in ForgeFrame frame);

    void Key(in ForgeKeyEvent input)
    {
    }

    bool HideCursor => false;
}

public static unsafe class ForgeApplicationHost
{
    private static IForgeApplication? s_application;
    private static readonly SoftwareCanvas Canvas = new();

    public static int Run(IForgeApplication application, ForgeWindowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (s_application is not null)
        {
            throw new InvalidOperationException("Only one WaylandForge application can run in a process.");
        }

        ForgeWindowOptions window = options ?? ForgeWindowOptions.Default;
        if (window.Width <= 0 || window.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Window dimensions must be positive.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(window.Title);

        s_application = application;
        try
        {
            return Native.waylandforge_run(window.Width, window.Height, window.Title, &RenderThunk, &KeyThunk);
        }
        finally
        {
            s_application = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint RenderThunk(
        uint* pixels,
        int width,
        int height,
        int stridePixels,
        ulong frameIndex,
        uint actionMask,
        int pointerX,
        int pointerY,
        uint pointerButtons,
        uint pointerInside,
        uint keyCode,
        uint keySerial,
        uint keyState,
        int scrollDelta,
        uint scrollSerial)
    {
        IForgeApplication? application = s_application;
        if (application is null)
        {
            return 0;
        }

        Canvas.Bind(pixels, width, height, stridePixels);
        var frame = new ForgeFrame(
            Canvas,
            frameIndex,
            actionMask,
            new PointerState(pointerX, pointerY, (PointerButtons)pointerButtons, pointerInside != 0),
            new TextInputEvent(keyCode, keySerial, keyState != 0),
            new ScrollInputEvent(scrollDelta, scrollSerial));
        application.Render(frame);
        return application.HideCursor ? 1u : 0u;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void KeyThunk(uint keyCode, uint keySerial, uint keyState)
    {
        var input = new ForgeKeyEvent(keyCode, keySerial, keyState != 0);
        s_application?.Key(input);
    }
}

internal static unsafe partial class Native
{
    [LibraryImport("waylandforge_native", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int waylandforge_run(
        int width,
        int height,
        string title,
        delegate* unmanaged[Cdecl]<uint*, int, int, int, ulong, uint, int, int, uint, uint, uint, uint, uint, int, uint, uint> render,
        delegate* unmanaged[Cdecl]<uint, uint, uint, void> input);
}
