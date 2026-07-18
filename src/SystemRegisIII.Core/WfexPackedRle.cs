using System.Buffers.Binary;

namespace SystemRegisIII.Core;

public static class WfexPackedRle
{
    public const int MaximumChunkPixels = 32_768;
    private const ushort RepeatFlag = 0x8000;
    private const ushort CountMask = 0x7fff;

    public static int MaximumEncodedBytes(int pixelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelCount);
        return checked(checked(pixelCount * sizeof(uint)) + checked((pixelCount + MaximumChunkPixels - 1) / MaximumChunkPixels) * sizeof(ushort));
    }

    public static int Encode(ReadOnlySpan<uint> pixels, ref byte[] destination)
    {
        if (pixels.IsEmpty) throw new ArgumentException("WFEX PACKRLE requires at least one pixel.", nameof(pixels));
        int maximumBytes = MaximumEncodedBytes(pixels.Length);
        if (destination.Length < maximumBytes) destination = new byte[maximumBytes];
        Span<byte> output = destination;
        int sourceIndex = 0;
        int outputIndex = 0;
        while (sourceIndex < pixels.Length)
        {
            int runLength = CountRun(pixels, sourceIndex);
            if (runLength >= 3)
            {
                WriteToken(output, ref outputIndex, repeat: true, runLength);
                BinaryPrimitives.WriteUInt32LittleEndian(output[outputIndex..], pixels[sourceIndex]);
                outputIndex += sizeof(uint);
                sourceIndex += runLength;
                continue;
            }

            int literalStart = sourceIndex;
            sourceIndex += runLength;
            while (sourceIndex < pixels.Length && sourceIndex - literalStart < MaximumChunkPixels)
            {
                runLength = CountRun(pixels, sourceIndex);
                if (runLength >= 3) break;
                sourceIndex += Math.Min(runLength, MaximumChunkPixels - (sourceIndex - literalStart));
            }
            int literalCount = sourceIndex - literalStart;
            WriteToken(output, ref outputIndex, repeat: false, literalCount);
            foreach (uint pixel in pixels.Slice(literalStart, literalCount))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(output[outputIndex..], pixel);
                outputIndex += sizeof(uint);
            }
        }
        return outputIndex;
    }

    public static void Decode(ReadOnlySpan<byte> encoded, Span<uint> destination)
    {
        if (destination.IsEmpty) throw new ArgumentException("WFEX PACKRLE requires a non-empty destination.", nameof(destination));
        int inputIndex = 0;
        int outputIndex = 0;
        while (inputIndex < encoded.Length)
        {
            if (encoded.Length - inputIndex < sizeof(ushort))
                throw new InvalidDataException("WFEX PACKRLE payload ends inside a token.");
            ushort token = BinaryPrimitives.ReadUInt16LittleEndian(encoded[inputIndex..]);
            inputIndex += sizeof(ushort);
            int count = (token & CountMask) + 1;
            if (count > destination.Length - outputIndex)
                throw new InvalidDataException("WFEX PACKRLE token exceeds the decoded frame size.");
            if ((token & RepeatFlag) != 0)
            {
                if (encoded.Length - inputIndex < sizeof(uint))
                    throw new InvalidDataException("WFEX PACKRLE repeat token has no pixel value.");
                uint pixel = BinaryPrimitives.ReadUInt32LittleEndian(encoded[inputIndex..]);
                inputIndex += sizeof(uint);
                destination.Slice(outputIndex, count).Fill(pixel);
                outputIndex += count;
                continue;
            }

            int literalBytes = checked(count * sizeof(uint));
            if (encoded.Length - inputIndex < literalBytes)
                throw new InvalidDataException("WFEX PACKRLE literal token is truncated.");
            for (int index = 0; index < count; index++)
            {
                destination[outputIndex++] = BinaryPrimitives.ReadUInt32LittleEndian(encoded[inputIndex..]);
                inputIndex += sizeof(uint);
            }
        }
        if (outputIndex != destination.Length)
            throw new InvalidDataException($"WFEX PACKRLE decoded {outputIndex} pixels; expected {destination.Length}.");
    }

    private static int CountRun(ReadOnlySpan<uint> pixels, int start)
    {
        int limit = Math.Min(pixels.Length, start + MaximumChunkPixels);
        int end = start + 1;
        while (end < limit && pixels[end] == pixels[start]) end++;
        return end - start;
    }

    private static void WriteToken(Span<byte> output, ref int outputIndex, bool repeat, int count)
    {
        if (count is < 1 or > MaximumChunkPixels) throw new ArgumentOutOfRangeException(nameof(count));
        ushort token = (ushort)((repeat ? RepeatFlag : 0) | (count - 1));
        BinaryPrimitives.WriteUInt16LittleEndian(output[outputIndex..], token);
        outputIndex += sizeof(ushort);
    }
}
