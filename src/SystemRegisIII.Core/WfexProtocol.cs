using System.Buffers.Binary;
using System.Diagnostics;

namespace SystemRegisIII.Core;

public readonly record struct WfexLimits(int MaximumWidth, int MaximumHeight, int MaximumPayloadBytes)
{
    public static WfexLimits Default { get; } = new(8_192, 8_192, 256 * 1024 * 1024);
}

public enum WfexValidationError
{
    None = 0,
    HeaderTooShort,
    InvalidMagic,
    InvalidWidth,
    InvalidHeight,
    DimensionLimitExceeded,
    UnsupportedStride,
    ArithmeticOverflow,
    InvalidPayloadLength,
    PayloadLimitExceeded,
}

public readonly record struct WfexFrameHeader(
    int Width,
    int Height,
    int StridePixels,
    ulong FrameIndex,
    int PayloadBytes,
    int Reserved)
{
    public const uint Magic = 0x58454657; // WFEX
    public const int Size = 32;

    public int PixelCount => PayloadBytes / sizeof(uint);

    public static bool TryParse(
        ReadOnlySpan<byte> source,
        out WfexFrameHeader header,
        out WfexValidationError error,
        WfexLimits? limits = null)
    {
        header = default;
        error = WfexValidationError.None;
        WfexLimits activeLimits = limits ?? WfexLimits.Default;
        if (source.Length < Size)
        {
            error = WfexValidationError.HeaderTooShort;
            return false;
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(source) != Magic)
        {
            error = WfexValidationError.InvalidMagic;
            return false;
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(source[4..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(source[8..]);
        int stride = BinaryPrimitives.ReadInt32LittleEndian(source[12..]);
        ulong frameIndex = BinaryPrimitives.ReadUInt64LittleEndian(source[16..]);
        int payloadBytes = BinaryPrimitives.ReadInt32LittleEndian(source[24..]);
        int reserved = BinaryPrimitives.ReadInt32LittleEndian(source[28..]);
        if (width <= 0)
        {
            error = WfexValidationError.InvalidWidth;
            return false;
        }
        if (height <= 0)
        {
            error = WfexValidationError.InvalidHeight;
            return false;
        }
        if (activeLimits.MaximumWidth <= 0 || activeLimits.MaximumHeight <= 0 || activeLimits.MaximumPayloadBytes <= 0 ||
            width > activeLimits.MaximumWidth || height > activeLimits.MaximumHeight)
        {
            error = WfexValidationError.DimensionLimitExceeded;
            return false;
        }
        if (stride != width)
        {
            error = WfexValidationError.UnsupportedStride;
            return false;
        }

        int expectedPayloadBytes;
        try
        {
            expectedPayloadBytes = checked(checked(width * height) * sizeof(uint));
        }
        catch (OverflowException)
        {
            error = WfexValidationError.ArithmeticOverflow;
            return false;
        }
        if (payloadBytes != expectedPayloadBytes)
        {
            error = WfexValidationError.InvalidPayloadLength;
            return false;
        }
        if (payloadBytes > activeLimits.MaximumPayloadBytes)
        {
            error = WfexValidationError.PayloadLimitExceeded;
            return false;
        }

        header = new WfexFrameHeader(width, height, stride, frameIndex, payloadBytes, reserved);
        return true;
    }

    public static WfexFrameHeader Parse(ReadOnlySpan<byte> source, WfexLimits? limits = null)
    {
        if (TryParse(source, out WfexFrameHeader header, out WfexValidationError error, limits))
            return header;
        throw new InvalidDataException($"Invalid WFEX v1 header: {error}.");
    }
}

public static class WfexStreamReader
{
    public static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
                throw new EndOfStreamException("WFEX stream ended before a complete record was received.");
            offset += read;
        }
    }

    public static bool TryReadExactly(Stream stream, Span<byte> buffer, int timeoutMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMilliseconds);
        Stopwatch wait = Stopwatch.StartNew();
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                if (wait.ElapsedMilliseconds >= timeoutMilliseconds)
                    return false;
                Thread.Sleep(1);
                continue;
            }
            offset += read;
        }
        return true;
    }

    public static async Task<int> ReadUpToWithTimeoutAsync(
        Stream stream,
        Memory<byte> buffer,
        int timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMilliseconds);
        int offset = 0;
        try
        {
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer[offset..], timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                offset += read;
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
        }
        return offset;
    }
}
