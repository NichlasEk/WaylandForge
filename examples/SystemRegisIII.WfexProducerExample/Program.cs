using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SystemRegisIII.Core;

const int Width = 64;
const int Height = 48;
const byte StepCommand = (byte)'S';
const string Policy = "WAYLANDFORGE_WFEX_POLICY";

string mode = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "v1";
if (mode is not ("v1" or "v2-raw" or "v2-shm"))
    throw new ArgumentException("Usage: wfex-producer-example [v1|v2-raw|v2-shm]");

Stream input = Console.OpenStandardInput();
Stream output = Console.OpenStandardOutput();
var limits = new WfexLimits(Width, Height, Width * Height * sizeof(uint));
WfexNegotiatedSession session;
if (mode == "v1")
{
    session = WfexNegotiatedSession.Version1(limits);
}
else
{
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Policy)))
        Environment.SetEnvironmentVariable(Policy, "require-v2");
    WfexCapabilities offeredCapabilities = WfexCapabilities.RawFrameRecords | WfexCapabilities.VersionedFrameRecords;
    if (mode == "v2-shm") offeredCapabilities |= WfexCapabilities.SharedMemorySlots;
    session = WfexNegotiation.NegotiateProducerFromEnvironment(
        input, output, limits, offeredCapabilities,
        WfexPresentationModes.DeterministicLockstep);
}

bool sharedSelected = (session.Capabilities & WfexCapabilities.SharedMemorySlots) != 0;
if (mode == "v2-raw" && sharedSelected)
    throw new InvalidDataException("The host selected shared memory for the v2-raw example; configure frame_transport=raw.");
if (mode == "v2-shm" && !sharedSelected)
    throw new InvalidDataException("The v2-shm example requires frame_transport=require-shm.");

using WfexSharedRegion? shared = OpenShared(session, input, output);
var command = new byte[5];
var pixels = new uint[Width * Height];
var header = new byte[WfexV2FrameHeader.BaseHeaderSize];
ulong frameIndex = 0;

while (ReadExactly(input, command) && command[0] == StepCommand)
{
    uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
    Render(pixels, frameIndex, buttons);
    if (shared is not null)
    {
        shared.Publish(
            pixels, Width, Height, Width, frameIndex,
            frameIndex * 16_666_667UL, 16_666_667UL).Write(output);
    }
    else if (session.MajorVersion >= 2)
    {
        WfexV2FrameHeader.CreateRaw(
            Width, Height, frameIndex,
            frameIndex * 16_666_667UL, 16_666_667UL).Write(header);
        output.Write(header);
        output.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
    }
    else
    {
        WriteV1Header(header, frameIndex, pixels.Length * sizeof(uint));
        output.Write(header.AsSpan(0, WfexFrameHeader.Size));
        output.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
    }
    output.Flush();
    frameIndex++;
}

static WfexSharedRegion? OpenShared(WfexNegotiatedSession session, Stream input, Stream output)
{
    if ((session.Capabilities & WfexCapabilities.SharedMemorySlots) == 0) return null;
    WfexSharedRegion region = WfexSharedRegion.OpenProducer(WfexSharedSetup.Read(input), session.Limits);
    WfexSharedSetupAck.Write(output);
    return region;
}

static bool ReadExactly(Stream stream, Span<byte> buffer)
{
    int offset = 0;
    while (offset < buffer.Length)
    {
        int read = stream.Read(buffer[offset..]);
        if (read == 0) return false;
        offset += read;
    }
    return true;
}

static void WriteV1Header(Span<byte> header, ulong frameIndex, int payloadBytes)
{
    header[..WfexFrameHeader.Size].Clear();
    BinaryPrimitives.WriteUInt32LittleEndian(header, WfexFrameHeader.Magic);
    BinaryPrimitives.WriteInt32LittleEndian(header[4..], Width);
    BinaryPrimitives.WriteInt32LittleEndian(header[8..], Height);
    BinaryPrimitives.WriteInt32LittleEndian(header[12..], Width);
    BinaryPrimitives.WriteUInt64LittleEndian(header[16..], frameIndex);
    BinaryPrimitives.WriteInt32LittleEndian(header[24..], payloadBytes);
}

static void Render(Span<uint> pixels, ulong frameIndex, uint buttons)
{
    for (int y = 0; y < Height; y++)
    {
        for (int x = 0; x < Width; x++)
        {
            bool bright = ((x / 8 + y / 8 + (int)frameIndex) & 1) == 0;
            uint accent = buttons == 0 ? 0x002060a0u : 0x0080c040u;
            pixels[y * Width + x] = 0xff000000u | accent | (bright ? 0x00202020u : 0u);
        }
    }
}
