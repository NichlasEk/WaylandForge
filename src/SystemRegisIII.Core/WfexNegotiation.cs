using System.Buffers.Binary;

namespace SystemRegisIII.Core;

[Flags]
public enum WfexCapabilities : ulong
{
    None = 0,
    RawFrameRecords = 1UL << 0,
}

[Flags]
public enum WfexPixelFormats : uint
{
    None = 0,
    Argb8888 = 1U << 0,
}

[Flags]
public enum WfexPresentationModes : uint
{
    None = 0,
    DeterministicLockstep = 1U << 0,
    LatestFrame = 1U << 1,
}

public readonly record struct WfexHandshakeRecord(
    uint Magic,
    ushort MajorVersion,
    ushort MinorVersion,
    WfexCapabilities RequiredCapabilities,
    WfexCapabilities Capabilities,
    WfexLimits Limits,
    WfexPixelFormats PixelFormats,
    WfexPresentationModes PresentationModes)
{
    public const uint ProducerMagic = 0x32584657; // WFX2
    public const uint HostMagic = 0x32414657; // WFA2
    public const ushort CurrentMajorVersion = 2;
    public const ushort CurrentMinorVersion = 0;
    public const int Size = 48;

    public static WfexHandshakeRecord CreateProducerHello(WfexLimits limits) => new(
        ProducerMagic,
        CurrentMajorVersion,
        CurrentMinorVersion,
        WfexCapabilities.RawFrameRecords,
        WfexCapabilities.RawFrameRecords,
        limits,
        WfexPixelFormats.Argb8888,
        WfexPresentationModes.DeterministicLockstep);

    public void Write(Span<byte> destination)
    {
        if (destination.Length < Size) throw new ArgumentException($"WFEX handshake requires {Size} bytes.", nameof(destination));
        destination[..Size].Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], MajorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], MinorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], Size);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[12..], (ulong)RequiredCapabilities);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[20..], (ulong)Capabilities);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], checked((uint)Limits.MaximumWidth));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[32..], checked((uint)Limits.MaximumHeight));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[36..], checked((uint)Limits.MaximumPayloadBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(destination[40..], (uint)PixelFormats);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[44..], (uint)PresentationModes);
    }

    public static WfexHandshakeRecord Parse(ReadOnlySpan<byte> source, uint expectedMagic)
    {
        if (source.Length < Size) throw new InvalidDataException("WFEX handshake is truncated.");
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (magic != expectedMagic) throw new InvalidDataException($"Invalid WFEX handshake magic 0x{magic:X8}.");
        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(source[6..]);
        ushort recordSize = BinaryPrimitives.ReadUInt16LittleEndian(source[8..]);
        if (recordSize != Size) throw new InvalidDataException($"Unsupported WFEX handshake size {recordSize}.");
        if (major != CurrentMajorVersion) throw new InvalidDataException($"Unsupported WFEX major version {major}.");

        uint maximumWidth = BinaryPrimitives.ReadUInt32LittleEndian(source[28..]);
        uint maximumHeight = BinaryPrimitives.ReadUInt32LittleEndian(source[32..]);
        uint maximumPayload = BinaryPrimitives.ReadUInt32LittleEndian(source[36..]);
        if (maximumWidth is 0 or > int.MaxValue || maximumHeight is 0 or > int.MaxValue || maximumPayload is 0 or > int.MaxValue)
            throw new InvalidDataException("WFEX handshake contains invalid frame limits.");

        return new WfexHandshakeRecord(
            magic,
            major,
            minor,
            (WfexCapabilities)BinaryPrimitives.ReadUInt64LittleEndian(source[12..]),
            (WfexCapabilities)BinaryPrimitives.ReadUInt64LittleEndian(source[20..]),
            new WfexLimits((int)maximumWidth, (int)maximumHeight, (int)maximumPayload),
            (WfexPixelFormats)BinaryPrimitives.ReadUInt32LittleEndian(source[40..]),
            (WfexPresentationModes)BinaryPrimitives.ReadUInt32LittleEndian(source[44..]));
    }
}

public readonly record struct WfexNegotiatedSession(
    ushort MajorVersion,
    ushort MinorVersion,
    WfexCapabilities Capabilities,
    WfexLimits Limits,
    WfexPixelFormats PixelFormat,
    WfexPresentationModes PresentationMode)
{
    public static WfexNegotiatedSession Version1(WfexLimits limits) => new(
        1, 0, WfexCapabilities.RawFrameRecords, limits,
        WfexPixelFormats.Argb8888, WfexPresentationModes.DeterministicLockstep);

    public string DiagnosticLabel => MajorVersion == 1
        ? "V1 RAW LOCKSTEP"
        : $"V{MajorVersion}.{MinorVersion} RAW LOCKSTEP";
}

public static class WfexNegotiation
{
    public const string PolicyEnvironmentVariable = "WAYLANDFORGE_WFEX_POLICY";
    public static WfexCapabilities HostCapabilities => WfexCapabilities.RawFrameRecords;

    public static WfexNegotiatedSession AcceptProducerHello(
        WfexHandshakeRecord hello,
        WfexLimits hostLimits,
        out WfexHandshakeRecord response)
    {
        WfexCapabilities unknownRequired = hello.RequiredCapabilities & ~HostCapabilities;
        if (unknownRequired != WfexCapabilities.None)
            throw new InvalidDataException($"WFEX producer requires unsupported capabilities: {unknownRequired}.");
        WfexCapabilities selectedCapabilities = hello.Capabilities & HostCapabilities;
        if ((selectedCapabilities & WfexCapabilities.RawFrameRecords) == 0 ||
            (hello.PixelFormats & WfexPixelFormats.Argb8888) == 0 ||
            (hello.PresentationModes & WfexPresentationModes.DeterministicLockstep) == 0)
            throw new InvalidDataException("WFEX producer does not offer the mandatory raw ARGB8888 lockstep baseline.");

        var limits = new WfexLimits(
            Math.Min(hostLimits.MaximumWidth, hello.Limits.MaximumWidth),
            Math.Min(hostLimits.MaximumHeight, hello.Limits.MaximumHeight),
            Math.Min(hostLimits.MaximumPayloadBytes, hello.Limits.MaximumPayloadBytes));
        response = new WfexHandshakeRecord(
            WfexHandshakeRecord.HostMagic,
            WfexHandshakeRecord.CurrentMajorVersion,
            Math.Min(WfexHandshakeRecord.CurrentMinorVersion, hello.MinorVersion),
            WfexCapabilities.None,
            selectedCapabilities,
            limits,
            WfexPixelFormats.Argb8888,
            WfexPresentationModes.DeterministicLockstep);
        return new WfexNegotiatedSession(
            response.MajorVersion, response.MinorVersion, selectedCapabilities, limits,
            response.PixelFormats, response.PresentationModes);
    }

    public static WfexNegotiatedSession NegotiateProducerFromEnvironment(
        Stream input,
        Stream output,
        WfexLimits producerLimits)
    {
        string policy = Environment.GetEnvironmentVariable(PolicyEnvironmentVariable)?.Trim().ToLowerInvariant() ?? "v1";
        if (policy == "v1") return WfexNegotiatedSession.Version1(producerLimits);
        if (policy is not ("prefer-v2" or "require-v2"))
            throw new InvalidDataException($"Unknown WFEX policy '{policy}'.");

        byte[] buffer = new byte[WfexHandshakeRecord.Size];
        WfexHandshakeRecord.CreateProducerHello(producerLimits).Write(buffer);
        output.Write(buffer);
        output.Flush();
        WfexStreamReader.ReadExactly(input, buffer);
        WfexHandshakeRecord accept = WfexHandshakeRecord.Parse(buffer, WfexHandshakeRecord.HostMagic);
        if ((accept.Capabilities & WfexCapabilities.RawFrameRecords) == 0 ||
            accept.PixelFormats != WfexPixelFormats.Argb8888 ||
            accept.PresentationModes != WfexPresentationModes.DeterministicLockstep)
            throw new InvalidDataException("WFEX host selected an unsupported baseline.");
        if (accept.Limits.MaximumWidth > producerLimits.MaximumWidth ||
            accept.Limits.MaximumHeight > producerLimits.MaximumHeight ||
            accept.Limits.MaximumPayloadBytes > producerLimits.MaximumPayloadBytes)
            throw new InvalidDataException("WFEX host selected limits beyond the producer offer.");
        return new WfexNegotiatedSession(
            accept.MajorVersion, accept.MinorVersion, accept.Capabilities, accept.Limits,
            accept.PixelFormats, accept.PresentationModes);
    }
}
