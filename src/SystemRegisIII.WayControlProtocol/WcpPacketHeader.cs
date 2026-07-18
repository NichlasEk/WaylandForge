using System.Buffers.Binary;

namespace SystemRegisIII.WayControlProtocol;

public enum WcpPacketType : ushort
{
    Hello = 1,
    Welcome = 2,
    DeviceConnected = 3,
    DeviceDisconnected = 4,
    Input = 5,
    Snapshot = 6,
    Error = 7,
}

public readonly record struct WcpPacketHeader(
    ushort Major,
    ushort Minor,
    WcpPacketType Type,
    ushort Flags,
    int PacketSize,
    ulong Sequence,
    long TimestampMicroseconds)
{
    public const uint Magic = 0x31504357; // WCP1
    public const int Size = 32;
    public const ushort CurrentMajor = 1;
    public const ushort CurrentMinor = 0;

    public static bool TryRead(ReadOnlySpan<byte> source, out WcpPacketHeader header)
    {
        header = default;
        if (source.Length < Size || BinaryPrimitives.ReadUInt32LittleEndian(source) != Magic)
            return false;

        ushort major = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        ushort minor = BinaryPrimitives.ReadUInt16LittleEndian(source[6..]);
        var type = (WcpPacketType)BinaryPrimitives.ReadUInt16LittleEndian(source[8..]);
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(source[10..]);
        int packetSize = BinaryPrimitives.ReadInt32LittleEndian(source[12..]);
        ulong sequence = BinaryPrimitives.ReadUInt64LittleEndian(source[16..]);
        long timestamp = BinaryPrimitives.ReadInt64LittleEndian(source[24..]);
        if (major == 0 || packetSize < Size)
            return false;

        header = new WcpPacketHeader(major, minor, type, flags, packetSize, sequence, timestamp);
        return true;
    }

    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < Size || Major == 0 || PacketSize < Size)
            return false;

        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], Major);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], Minor);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], (ushort)Type);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], Flags);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], PacketSize);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(destination[24..], TimestampMicroseconds);
        return true;
    }
}
