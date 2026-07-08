using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SystemRegisIII.WaylandForge.Ui;

namespace SystemRegisIII.Host.WaylandForge;

internal unsafe delegate uint RenderFrame(uint* pixels, int width, int height, int stridePixels, ulong frameIndex, ForgeInput input, PointerState pointer, TextInputEvent textInput, ScrollInputEvent scrollInput);
internal delegate void RawKeyInput(uint keyCode, uint keySerial, bool pressed);

internal static unsafe class WaylandWindow
{
    private static RenderFrame? s_render;
    private static RawKeyInput? s_input;

    public static int Run(int width, int height, string title, RenderFrame render, RawKeyInput input)
    {
        s_render = render;
        s_input = input;
        try
        {
            return Native.waylandforge_run(width, height, title, &RenderThunk, &InputThunk);
        }
        finally
        {
            s_render = null;
            s_input = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint RenderThunk(
        uint* pixels,
        int width,
        int height,
        int stridePixels,
        ulong frameIndex,
        uint inputMask,
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
        var pointer = new PointerState(pointerX, pointerY, (PointerButtons)pointerButtons, pointerInside != 0);
        var textInput = new TextInputEvent(keyCode, keySerial, keyState != 0);
        var scrollInput = new ScrollInputEvent(scrollDelta, scrollSerial);
        return s_render?.Invoke(pixels, width, height, stridePixels, frameIndex, (ForgeInput)inputMask, pointer, textInput, scrollInput) ?? 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void InputThunk(uint keyCode, uint keySerial, uint keyState)
    {
        s_input?.Invoke(keyCode, keySerial, keyState != 0);
    }
}

internal static unsafe partial class Native
{
    [LibraryImport("waylandforge_native", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int waylandforge_run(
        int width,
        int height,
        string title,
        delegate* unmanaged[Cdecl]<uint*, int, int, int, ulong, uint, int, int, uint, uint, uint, uint, uint, int, uint, uint> render,
        delegate* unmanaged[Cdecl]<uint, uint, uint, void> input);
}
