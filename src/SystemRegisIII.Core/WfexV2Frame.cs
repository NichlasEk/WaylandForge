using System.Buffers.Binary;

namespace SystemRegisIII.Core;

[Flags]
public enum WfexV2FrameFlags : ushort
{
    None = 0,
    FullFrame = 1 << 0,
}

public enum WfexV2PayloadCodec : uint
{
    RawArgb8888 = 1,
    PackedRleArgb8888 = 2,
}

public enum WfexV2FrameValidationError
{
    None = 0,
    HeaderTooShort,
    InvalidMagic,
    UnsupportedMajorVersion,
    InvalidHeaderSize,
    UnsupportedCodec,
    MissingFullFrameFlag,
    InvalidWidth,
    InvalidHeight,
    DimensionLimitExceeded,
    UnsupportedStride,
    ArithmeticOverflow,
    InvalidPayloadLength,
    PayloadLimitExceeded,
    InvalidRecordLength,
    InvalidNominalDuration,
}

public readonly record struct WfexV2FrameHeader(
    ushort MinorVersion,
    ushort HeaderSize,
    WfexV2FrameFlags Flags,
    WfexV2PayloadCodec Codec,
    int Width,
    int Height,
    int StridePixels,
    int PayloadBytes,
    ulong FrameIndex,
    ulong PresentationTimestampNanoseconds,
    ulong NominalDurationNanoseconds,
    ulong RecordBytes)
{
    public const uint Magic = 0x32464657; // WFF2
    public const ushort MajorVersion = 2;
    public const ushort CurrentMinorVersion = 0;
    public const ushort BaseHeaderSize = 64;
    public const ushort MaximumHeaderSize = 4096;

    public int PixelCount => checked(Width * Height);
    public int DecodedPayloadBytes => checked(PixelCount * sizeof(uint));

    public static WfexV2FrameHeader CreateRaw(
        int width,
        int height,
        ulong frameIndex,
        ulong presentationTimestampNanoseconds,
        ulong nominalDurationNanoseconds)
    {
        int payloadBytes = checked(checked(width * height) * sizeof(uint));
        return new WfexV2FrameHeader(
            CurrentMinorVersion,
            BaseHeaderSize,
            WfexV2FrameFlags.FullFrame,
            WfexV2PayloadCodec.RawArgb8888,
            width,
            height,
            width,
            payloadBytes,
            frameIndex,
            presentationTimestampNanoseconds,
            nominalDurationNanoseconds,
            checked((ulong)BaseHeaderSize + (uint)payloadBytes));
    }

    public static WfexV2FrameHeader CreatePackedRle(
        int width,
        int height,
        int encodedPayloadBytes,
        ulong frameIndex,
        ulong presentationTimestampNanoseconds,
        ulong nominalDurationNanoseconds)
    {
        int pixelCount = checked(width * height);
        if (encodedPayloadBytes <= 0 || encodedPayloadBytes > WfexPackedRle.MaximumEncodedBytes(pixelCount))
            throw new ArgumentOutOfRangeException(nameof(encodedPayloadBytes));
        return new WfexV2FrameHeader(
            CurrentMinorVersion,
            BaseHeaderSize,
            WfexV2FrameFlags.FullFrame,
            WfexV2PayloadCodec.PackedRleArgb8888,
            width,
            height,
            width,
            encodedPayloadBytes,
            frameIndex,
            presentationTimestampNanoseconds,
            nominalDurationNanoseconds,
            checked((ulong)BaseHeaderSize + (uint)encodedPayloadBytes));
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length < HeaderSize) throw new ArgumentException($"WFEX v2 frame header requires {HeaderSize} bytes.", nameof(destination));
        destination[..HeaderSize].Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], MajorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], MinorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], HeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], (ushort)Flags);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], (uint)Codec);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], Width);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], Height);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], StridePixels);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..], PayloadBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[32..], FrameIndex);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[40..], PresentationTimestampNanoseconds);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[48..], NominalDurationNanoseconds);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[56..], RecordBytes);
    }

    public static bool TryParse(
        ReadOnlySpan<byte> source,
        out WfexV2FrameHeader header,
        out WfexV2FrameValidationError error,
        WfexLimits? limits = null)
    {
        header = default;
        error = WfexV2FrameValidationError.None;
        WfexLimits activeLimits = limits ?? WfexLimits.Default;
        if (source.Length < BaseHeaderSize) return Fail(WfexV2FrameValidationError.HeaderTooShort, out error);
        if (BinaryPrimitives.ReadUInt32LittleEndian(source) != Magic) return Fail(WfexV2FrameValidationError.InvalidMagic, out error);
        if (BinaryPrimitives.ReadUInt16LittleEndian(source[4..]) != MajorVersion) return Fail(WfexV2FrameValidationError.UnsupportedMajorVersion, out error);

        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(source[6..]);
        ushort headerSize = BinaryPrimitives.ReadUInt16LittleEndian(source[8..]);
        WfexV2FrameFlags flags = (WfexV2FrameFlags)BinaryPrimitives.ReadUInt16LittleEndian(source[10..]);
        WfexV2PayloadCodec codec = (WfexV2PayloadCodec)BinaryPrimitives.ReadUInt32LittleEndian(source[12..]);
        int width = BinaryPrimitives.ReadInt32LittleEndian(source[16..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(source[20..]);
        int stride = BinaryPrimitives.ReadInt32LittleEndian(source[24..]);
        int payloadBytes = BinaryPrimitives.ReadInt32LittleEndian(source[28..]);
        ulong frameIndex = BinaryPrimitives.ReadUInt64LittleEndian(source[32..]);
        ulong timestamp = BinaryPrimitives.ReadUInt64LittleEndian(source[40..]);
        ulong nominalDuration = BinaryPrimitives.ReadUInt64LittleEndian(source[48..]);
        ulong recordBytes = BinaryPrimitives.ReadUInt64LittleEndian(source[56..]);

        if (headerSize < BaseHeaderSize || headerSize > MaximumHeaderSize || (headerSize & 7) != 0)
            return Fail(WfexV2FrameValidationError.InvalidHeaderSize, out error);
        if (source.Length < headerSize) return Fail(WfexV2FrameValidationError.HeaderTooShort, out error);
        if (codec is not (WfexV2PayloadCodec.RawArgb8888 or WfexV2PayloadCodec.PackedRleArgb8888))
            return Fail(WfexV2FrameValidationError.UnsupportedCodec, out error);
        if ((flags & WfexV2FrameFlags.FullFrame) == 0) return Fail(WfexV2FrameValidationError.MissingFullFrameFlag, out error);
        if (width <= 0) return Fail(WfexV2FrameValidationError.InvalidWidth, out error);
        if (height <= 0) return Fail(WfexV2FrameValidationError.InvalidHeight, out error);
        if (activeLimits.MaximumWidth <= 0 || activeLimits.MaximumHeight <= 0 || activeLimits.MaximumPayloadBytes <= 0 ||
            width > activeLimits.MaximumWidth || height > activeLimits.MaximumHeight)
            return Fail(WfexV2FrameValidationError.DimensionLimitExceeded, out error);
        if (stride != width) return Fail(WfexV2FrameValidationError.UnsupportedStride, out error);

        int pixelCount;
        int expectedPayload;
        try
        {
            pixelCount = checked(width * height);
            expectedPayload = checked(pixelCount * sizeof(uint));
        }
        catch (OverflowException) { return Fail(WfexV2FrameValidationError.ArithmeticOverflow, out error); }
        if (expectedPayload > activeLimits.MaximumPayloadBytes) return Fail(WfexV2FrameValidationError.PayloadLimitExceeded, out error);
        if (codec == WfexV2PayloadCodec.RawArgb8888 && payloadBytes != expectedPayload)
            return Fail(WfexV2FrameValidationError.InvalidPayloadLength, out error);
        if (codec == WfexV2PayloadCodec.PackedRleArgb8888 &&
            (payloadBytes <= 0 || payloadBytes > WfexPackedRle.MaximumEncodedBytes(pixelCount)))
            return Fail(WfexV2FrameValidationError.InvalidPayloadLength, out error);
        if (recordBytes != checked((ulong)headerSize + (uint)payloadBytes)) return Fail(WfexV2FrameValidationError.InvalidRecordLength, out error);
        if (nominalDuration == 0) return Fail(WfexV2FrameValidationError.InvalidNominalDuration, out error);

        header = new WfexV2FrameHeader(
            minor, headerSize, flags, codec, width, height, stride, payloadBytes,
            frameIndex, timestamp, nominalDuration, recordBytes);
        return true;
    }

    public static WfexV2FrameHeader Parse(ReadOnlySpan<byte> source, WfexLimits? limits = null)
    {
        if (TryParse(source, out WfexV2FrameHeader header, out WfexV2FrameValidationError error, limits)) return header;
        throw new InvalidDataException($"Invalid WFEX v2 frame header: {error}.");
    }

    private static bool Fail(WfexV2FrameValidationError value, out WfexV2FrameValidationError error)
    {
        error = value;
        return false;
    }
}

public static class WfexV2Sequence
{
    public static bool IsExpected(bool hasPrevious, ulong previous, ulong candidate, out ulong expected)
    {
        expected = hasPrevious ? unchecked(previous + 1) : candidate;
        return candidate == expected;
    }
}
