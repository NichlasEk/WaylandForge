using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading.Channels;
using SystemRegisIII.Core;

const int frameWidth = 320;
const int frameHeight = 224;
const uint frameMagic = 0x58454657; // WFEX
const byte stepCommand = (byte)'S';

Stream input = Console.OpenStandardInput();
Stream output = Console.OpenStandardOutput();
WfexNegotiatedSession negotiatedSession = WfexNegotiation.NegotiateProducerFromEnvironment(
    input, output, new WfexLimits(frameWidth, frameHeight, frameWidth * frameHeight * sizeof(uint)));
using WfexSharedRegion? sharedRegion = OpenSharedRegion(negotiatedSession, input, output);
var frame = new uint[frameWidth * frameHeight];
var header = new byte[WfexV2FrameHeader.BaseHeaderSize];
var command = new byte[5];
ulong frameIndex = 0;
int blobX = frameWidth / 2;
int blobY = frameHeight / 2;

if (negotiatedSession.PresentationMode == WfexPresentationModes.LatestFrame)
{
    if (sharedRegion is null) throw new InvalidDataException("WFEX latest-frame mode requires shared memory.");
    RunLatestFrame(input, output, sharedRegion, frame, frameWidth, frameHeight, ref blobX, ref blobY);
    return;
}

while (ReadExact(input, command))
{
    if (command[0] != stepCommand)
    {
        break;
    }

    uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
    RenderFrame(frame, frameWidth, frameHeight, frameIndex, buttons, ref blobX, ref blobY);

    if (sharedRegion is not null)
    {
        WfexSharedNotification notification = sharedRegion.Publish(
            frame, frameWidth, frameHeight, frameWidth, frameIndex,
            frameIndex * 16_666_667UL, 16_666_667UL);
        notification.Write(output);
    }
    else if (negotiatedSession.MajorVersion >= 2)
    {
        WfexV2FrameHeader.CreateRaw(frameWidth, frameHeight, frameIndex, frameIndex * 16_666_667UL, 16_666_667UL).Write(header);
        output.Write(header.AsSpan(0, WfexV2FrameHeader.BaseHeaderSize));
        output.Write(MemoryMarshalBytes(frame));
    }
    else
    {
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), frameMagic);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), frameWidth);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), frameHeight);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), frameWidth);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16), frameIndex);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), frame.Length * sizeof(uint));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), 0);
        output.Write(header.AsSpan(0, WfexFrameHeader.Size));
        output.Write(MemoryMarshalBytes(frame));
    }
    output.Flush();

    frameIndex++;
}

static bool ReadExact(Stream stream, Span<byte> buffer)
{
    int offset = 0;
    while (offset < buffer.Length)
    {
        int read = stream.Read(buffer[offset..]);
        if (read == 0)
        {
            return false;
        }
        offset += read;
    }
    return true;
}

static void RunLatestFrame(
    Stream input,
    Stream output,
    WfexSharedRegion sharedRegion,
    uint[] frame,
    int width,
    int height,
    ref int blobX,
    ref int blobY)
{
    var inputs = Channel.CreateBounded<uint>(new BoundedChannelOptions(256)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait,
    });
    var reader = Task.Run(() =>
    {
        var command = new byte[5];
        try
        {
            while (ReadExact(input, command) && command[0] == stepCommand)
            {
                uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
                inputs.Writer.WriteAsync(buttons).AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            inputs.Writer.TryComplete();
        }
    });
    uint currentButtons = 0;
    ulong frameIndex = 0;
    long nextTick = Stopwatch.GetTimestamp();
    long tickTicks = Math.Max(1, Stopwatch.Frequency / 60);
    while (!reader.IsCompleted || inputs.Reader.TryPeek(out _))
    {
        if (inputs.Reader.TryRead(out uint buttons)) currentButtons = buttons;
        RenderFrame(frame, width, height, frameIndex, currentButtons, ref blobX, ref blobY);
        WfexSharedNotification notification = sharedRegion.Publish(
            frame, width, height, width, frameIndex,
            frameIndex * 16_666_667UL, 16_666_667UL);
        notification.Write(output);
        output.Flush();
        frameIndex++;
        nextTick += tickTicks;
        long remaining = nextTick - Stopwatch.GetTimestamp();
        if (remaining > 0)
        {
            int sleepMilliseconds = (int)(remaining * 1000 / Stopwatch.Frequency);
            if (sleepMilliseconds > 0) Thread.Sleep(sleepMilliseconds);
            while (Stopwatch.GetTimestamp() < nextTick) Thread.SpinWait(32);
        }
        else if (remaining < -tickTicks * 4)
        {
            nextTick = Stopwatch.GetTimestamp();
        }
    }
}

static void RenderFrame(uint[] frame, int width, int height, ulong frameIndex, uint buttons, ref int blobX, ref int blobY)
{
    MoveBlob(buttons, width, height, ref blobX, ref blobY);
    int framePhase = (int)(frameIndex & 0x7fffffff);

    for (int y = 0; y < height; y++)
    {
        int row = y * width;
        for (int x = 0; x < width; x++)
        {
            uint wave = (uint)((Math.Sin((x + framePhase * 3) * 0.035) + 1.0) * 42.0);
            uint r = (uint)((x * 255) / (width - 1));
            uint g = (uint)((y * 255) / (height - 1));
            uint b = (uint)((framePhase * 2 + x / 3 + y / 2) & 0xff);
            r = Math.Min(255u, r + wave);
            if (buttons != 0)
            {
                g = Math.Min(255u, g + 28);
                b = Math.Min(255u, b + 24);
            }
            frame[row + x] = 0xff000000u | (r << 16) | (g << 8) | b;
        }
    }

    DrawBlob(frame, width, height, blobX, blobY, buttons);
}

static void MoveBlob(uint buttons, int width, int height, ref int x, ref int y)
{
    const uint up = 1u << 1;
    const uint down = 1u << 2;
    const uint left = 1u << 3;
    const uint right = 1u << 4;
    const uint start = 1u << 5;
    int speed = (buttons & start) != 0 ? 6 : 3;

    if ((buttons & left) != 0) x -= speed;
    if ((buttons & right) != 0) x += speed;
    if ((buttons & up) != 0) y -= speed;
    if ((buttons & down) != 0) y += speed;

    x = Math.Clamp(x, 14, width - 15);
    y = Math.Clamp(y, 14, height - 15);
}

static void DrawBlob(uint[] frame, int width, int height, int centerX, int centerY, uint buttons)
{
    const uint a = 1u << 6;
    const uint b = 1u << 7;
    const uint c = 1u << 8;
    const uint x = 1u << 9;
    const uint y = 1u << 10;
    const uint z = 1u << 11;

    int radius = (buttons & c) != 0 ? 20 : 13;
    if ((buttons & z) != 0)
    {
        radius = 8;
    }

    uint color = 0xffffd35a;
    if ((buttons & a) != 0) color = 0xff57f287;
    if ((buttons & b) != 0) color = 0xffff5e9c;
    if ((buttons & x) != 0) color = 0xff62b7ff;
    if ((buttons & y) != 0) color = 0xffd790ff;

    FillCircle(frame, width, height, centerX, centerY, radius + 4, 0xff05070a);
    FillCircle(frame, width, height, centerX, centerY, radius, color);
    FillRect(frame, width, height, centerX - 5, centerY - 5, 4, 4, 0xffffffff);
    FillRect(frame, width, height, centerX + 2, centerY - 5, 4, 4, 0xffffffff);
}

static void FillCircle(uint[] frame, int width, int height, int centerX, int centerY, int radius, uint color)
{
    int r2 = radius * radius;
    for (int y = -radius; y <= radius; y++)
    {
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y <= r2)
            {
                PutPixel(frame, width, height, centerX + x, centerY + y, color);
            }
        }
    }
}

static void FillRect(uint[] frame, int width, int height, int x, int y, int w, int h, uint color)
{
    for (int py = Math.Max(0, y); py < Math.Min(height, y + h); py++)
    {
        for (int px = Math.Max(0, x); px < Math.Min(width, x + w); px++)
        {
            frame[(py * width) + px] = color;
        }
    }
}

static void PutPixel(uint[] frame, int width, int height, int x, int y, uint color)
{
    if ((uint)x >= width || (uint)y >= height)
    {
        return;
    }
    frame[(y * width) + x] = color;
}

static ReadOnlySpan<byte> MemoryMarshalBytes(uint[] frame)
{
    return System.Runtime.InteropServices.MemoryMarshal.AsBytes(frame.AsSpan());
}

static WfexSharedRegion? OpenSharedRegion(WfexNegotiatedSession session, Stream input, Stream output)
{
    if ((session.Capabilities & WfexCapabilities.SharedMemorySlots) == 0) return null;
    WfexSharedSetup setup = WfexSharedSetup.Read(input);
    WfexSharedRegion region = WfexSharedRegion.OpenProducer(setup, session.Limits);
    WfexSharedSetupAck.Write(output);
    return region;
}
