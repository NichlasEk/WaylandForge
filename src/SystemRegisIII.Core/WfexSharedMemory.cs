using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;

namespace SystemRegisIII.Core;

public readonly record struct WfexSharedSetup(
    ushort SlotCount,
    uint SlotStrideBytes,
    uint MaximumPayloadBytes,
    ulong RegionBytes,
    ulong Nonce,
    string Path)
{
    public const uint Magic = 0x32534657; // WFS2
    public const ushort HeaderSize = 48;
    public const ushort ControlSize = 64;
    public const ushort SlotHeaderSize = 64;
    public const int MaximumPathBytes = 1024;

    public void Write(Stream stream)
    {
        byte[] path = Encoding.UTF8.GetBytes(Path);
        if (path.Length is 0 or > MaximumPathBytes) throw new InvalidDataException("WFEX shared-memory path length is invalid.");
        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], HeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], SlotCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], ControlSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[12..], SlotHeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], SlotStrideBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], MaximumPayloadBytes);
        BinaryPrimitives.WriteUInt64LittleEndian(header[24..], RegionBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(header[32..], (uint)path.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(header[40..], Nonce);
        stream.Write(header);
        stream.Write(path);
    }

    public static WfexSharedSetup Read(Stream stream)
    {
        byte[] header = new byte[HeaderSize];
        WfexStreamReader.ReadExactly(stream, header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic) throw new InvalidDataException("Invalid WFEX shared-memory setup magic.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4)) != HeaderSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != ControlSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) != SlotHeaderSize)
            throw new InvalidDataException("Unsupported WFEX shared-memory setup layout.");
        ushort slots = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
        uint slotStride = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16));
        uint maximumPayload = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20));
        ulong regionBytes = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(24));
        uint pathLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(32));
        ulong nonce = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(40));
        uint expectedSlotStride;
        ulong expectedRegionBytes;
        try
        {
            expectedSlotStride = checked((uint)SlotHeaderSize + maximumPayload);
            expectedRegionBytes = checked((ulong)ControlSize + (ulong)slots * expectedSlotStride);
        }
        catch (OverflowException)
        {
            throw new InvalidDataException("WFEX shared-memory setup arithmetic overflowed.");
        }
        if (slots != 2 || maximumPayload == 0 || pathLength is 0 or > MaximumPathBytes ||
            slotStride != expectedSlotStride || regionBytes != expectedRegionBytes || regionBytes > long.MaxValue)
            throw new InvalidDataException("Invalid WFEX shared-memory setup values.");
        byte[] path = new byte[pathLength];
        WfexStreamReader.ReadExactly(stream, path);
        return new WfexSharedSetup(slots, slotStride, maximumPayload, regionBytes, nonce, Encoding.UTF8.GetString(path));
    }
}

public readonly record struct WfexSharedNotification(ushort SlotIndex, ulong Sequence)
{
    public const uint Magic = 0x32524657; // WFR2
    public const ushort Size = 16;

    public void Write(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[Size];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..], Size);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[6..], SlotIndex);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer[8..], Sequence);
        stream.Write(buffer);
    }

    public static WfexSharedNotification Read(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[Size];
        WfexStreamReader.ReadExactly(stream, buffer);
        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != Magic || BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..]) != Size)
            throw new InvalidDataException("Invalid WFEX shared-memory frame notification.");
        return new WfexSharedNotification(
            BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..]),
            BinaryPrimitives.ReadUInt64LittleEndian(buffer[8..]));
    }
}

public readonly record struct WfexSharedFrameMetadata(
    ulong Sequence,
    ulong FrameIndex,
    ulong PresentationTimestampNanoseconds,
    ulong NominalDurationNanoseconds,
    int Width,
    int Height,
    int StridePixels,
    int PayloadBytes)
{
    public int PixelCount => PayloadBytes / sizeof(uint);
}

public sealed unsafe class WfexSharedRegion : IDisposable
{
    private const uint ControlMagic = 0x4d534657; // WFSM
    private const ushort LayoutVersion = 1;
    private const int StateFree = 0;
    private const int StateWriting = 1;
    private const int StateReady = 2;
    private const int StateReading = 3;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private string? _backingPath;
    private ulong _nextPublishSequence;
    private byte* _pointer;
    private bool _pointerAcquired;
    private bool _disposed;

    private WfexSharedRegion(MemoryMappedFile mapping, MemoryMappedViewAccessor view, WfexSharedSetup setup, string? backingPath)
    {
        _mapping = mapping;
        _view = view;
        Setup = setup;
        _backingPath = backingPath;
        byte* pointer = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        _pointer = pointer + view.PointerOffset;
        _pointerAcquired = true;
    }

    public WfexSharedSetup Setup { get; }

    public static WfexSharedRegion CreateHost(WfexLimits limits, string? runtimeDirectory = null)
    {
        const ushort slots = 2;
        uint payload = checked((uint)limits.MaximumPayloadBytes);
        uint slotStride = checked((uint)WfexSharedSetup.SlotHeaderSize + payload);
        ulong regionBytes = checked((ulong)WfexSharedSetup.ControlSize + (ulong)slots * slotStride);
        if (regionBytes > long.MaxValue) throw new InvalidOperationException("WFEX shared-memory region is too large.");
        string directory = runtimeDirectory ?? (Directory.Exists("/dev/shm") ? "/dev/shm" : Path.GetTempPath());
        Directory.CreateDirectory(directory);
        CleanupStaleRegions(directory);
        string path = Path.Combine(directory, $"waylandforge-wfex-{Environment.ProcessId}-{Guid.NewGuid():N}");
        ulong nonce = RandomNonce();
        var setup = new WfexSharedSetup(slots, slotStride, payload, regionBytes, nonce, path);

        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.ReadWrite | FileShare.Delete,
        };
        if (!OperatingSystem.IsWindows())
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = new FileStream(path, fileOptions))
        {
            file.SetLength((long)regionBytes);
        }
        MemoryMappedFile? mapping = null;
        MemoryMappedViewAccessor? view = null;
        WfexSharedRegion? region = null;
        try
        {
            mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, (long)regionBytes, MemoryMappedFileAccess.ReadWrite);
            view = mapping.CreateViewAccessor(0, (long)regionBytes, MemoryMappedFileAccess.ReadWrite);
            region = new WfexSharedRegion(mapping, view, setup, path);
            region.InitializeControl();
            return region;
        }
        catch
        {
            if (region is not null) region.Dispose();
            else
            {
                view?.Dispose();
                mapping?.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
            throw;
        }
    }

    public static WfexSharedRegion OpenProducer(WfexSharedSetup setup, WfexLimits negotiatedLimits)
    {
        if (setup.MaximumPayloadBytes > negotiatedLimits.MaximumPayloadBytes)
            throw new InvalidDataException("WFEX shared-memory payload exceeds negotiated limits.");
        string fullPath = Path.GetFullPath(setup.Path);
        if (!Path.GetFileName(fullPath).StartsWith("waylandforge-wfex-", StringComparison.Ordinal))
            throw new InvalidDataException("WFEX shared-memory path does not use the required private prefix.");
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists || fileInfo.LinkTarget is not null || (ulong)fileInfo.Length != setup.RegionBytes)
            throw new InvalidDataException("WFEX shared-memory backing file type or size is invalid.");
        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode mode = File.GetUnixFileMode(fullPath);
            UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((mode & forbidden) != 0) throw new InvalidDataException("WFEX shared-memory file permissions are too broad.");
        }
        MemoryMappedFile? mapping = null;
        MemoryMappedViewAccessor? view = null;
        WfexSharedRegion? region = null;
        try
        {
            mapping = MemoryMappedFile.CreateFromFile(fullPath, FileMode.Open, null, (long)setup.RegionBytes, MemoryMappedFileAccess.ReadWrite);
            view = mapping.CreateViewAccessor(0, (long)setup.RegionBytes, MemoryMappedFileAccess.ReadWrite);
            region = new WfexSharedRegion(mapping, view, setup, null);
            region.ValidateControl();
            File.Delete(fullPath);
            return region;
        }
        catch
        {
            if (region is not null) region.Dispose();
            else
            {
                view?.Dispose();
                mapping?.Dispose();
            }
            throw;
        }
    }

    public WfexSharedNotification Publish(
        uint[] pixels,
        int width,
        int height,
        int stridePixels,
        ulong frameIndex,
        ulong timestampNanoseconds,
        ulong nominalDurationNanoseconds,
        int timeoutMilliseconds = 2000)
    {
        int payloadBytes = checked(pixels.Length * sizeof(uint));
        if (payloadBytes > Setup.MaximumPayloadBytes || pixels.Length != checked(width * height) || stridePixels != width)
            throw new InvalidDataException("Frame does not fit the negotiated WFEX shared-memory slot.");
        ulong sequence = _nextPublishSequence;
        ushort slot = (ushort)(sequence % Setup.SlotCount);
        long offset = SlotOffset(slot);
        Stopwatch wait = Stopwatch.StartNew();
        while (_view.ReadInt32(offset) != StateFree)
        {
            if (wait.ElapsedMilliseconds >= timeoutMilliseconds)
                throw new TimeoutException($"WFEX shared-memory slot {slot} did not become free.");
            Thread.Sleep(1);
        }

        _view.Write(offset, StateWriting);
        _view.Write(offset + 8, sequence);
        _view.Write(offset + 16, frameIndex);
        _view.Write(offset + 24, timestampNanoseconds);
        _view.Write(offset + 32, nominalDurationNanoseconds);
        _view.Write(offset + 40, width);
        _view.Write(offset + 44, height);
        _view.Write(offset + 48, stridePixels);
        _view.Write(offset + 52, payloadBytes);
        pixels.AsSpan().CopyTo(new Span<uint>(
            _pointer + offset + WfexSharedSetup.SlotHeaderSize,
            pixels.Length));
        Thread.MemoryBarrier();
        _view.Write(offset, StateReady);
        Thread.MemoryBarrier();
        _nextPublishSequence = unchecked(sequence + 1);
        return new WfexSharedNotification(slot, sequence);
    }

    public WfexSharedFrameMetadata Peek(WfexSharedNotification notification)
    {
        long offset = CheckedReadySlot(notification);
        return new WfexSharedFrameMetadata(
            _view.ReadUInt64(offset + 8),
            _view.ReadUInt64(offset + 16),
            _view.ReadUInt64(offset + 24),
            _view.ReadUInt64(offset + 32),
            _view.ReadInt32(offset + 40),
            _view.ReadInt32(offset + 44),
            _view.ReadInt32(offset + 48),
            _view.ReadInt32(offset + 52));
    }

    public void Consume(WfexSharedNotification notification, uint[] destination)
    {
        ReadOnlySpan<uint> pixels = AcquirePixels(notification, out _);
        try
        {
            if (destination.Length != pixels.Length)
                throw new InvalidDataException("WFEX shared-memory destination size does not match the published frame.");
            pixels.CopyTo(destination);
        }
        finally
        {
            ReleasePixels(notification);
        }
    }

    public ReadOnlySpan<uint> AcquirePixels(WfexSharedNotification notification, out WfexSharedFrameMetadata metadata)
    {
        long offset = CheckedReadySlot(notification);
        metadata = Peek(notification);
        if (metadata.PayloadBytes < 0 || metadata.PayloadBytes % sizeof(uint) != 0 ||
            metadata.PayloadBytes > Setup.MaximumPayloadBytes)
            throw new InvalidDataException("WFEX shared-memory payload size is invalid.");
        _view.Write(offset, StateReading);
        Thread.MemoryBarrier();
        return new ReadOnlySpan<uint>(
            _pointer + offset + WfexSharedSetup.SlotHeaderSize,
            metadata.PayloadBytes / sizeof(uint));
    }

    public void ReleasePixels(WfexSharedNotification notification)
    {
        if (notification.SlotIndex >= Setup.SlotCount) throw new InvalidDataException("WFEX shared-memory release has an invalid slot index.");
        long offset = SlotOffset(notification.SlotIndex);
        if (_view.ReadInt32(offset) != StateReading || _view.ReadUInt64(offset + 8) != notification.Sequence)
            throw new InvalidDataException("WFEX shared-memory release does not match the reading slot.");
        Thread.MemoryBarrier();
        _view.Write(offset, StateFree);
    }

    public void UnlinkBackingFile()
    {
        string? path = Interlocked.Exchange(ref _backingPath, null);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pointerAcquired)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _pointerAcquired = false;
            _pointer = null;
        }
        _view.Dispose();
        _mapping.Dispose();
        UnlinkBackingFile();
    }

    private void InitializeControl()
    {
        _view.Write(0, ControlMagic);
        _view.Write(4, LayoutVersion);
        _view.Write(6, Setup.SlotCount);
        _view.Write(8, (uint)WfexSharedSetup.ControlSize);
        _view.Write(12, (uint)WfexSharedSetup.SlotHeaderSize);
        _view.Write(16, Setup.SlotStrideBytes);
        _view.Write(20, Setup.MaximumPayloadBytes);
        _view.Write(24, Setup.RegionBytes);
        _view.Write(32, Setup.Nonce);
        Thread.MemoryBarrier();
    }

    private void ValidateControl()
    {
        Thread.MemoryBarrier();
        if (_view.ReadUInt32(0) != ControlMagic || _view.ReadUInt16(4) != LayoutVersion ||
            _view.ReadUInt16(6) != Setup.SlotCount || _view.ReadUInt32(8) != WfexSharedSetup.ControlSize ||
            _view.ReadUInt32(12) != WfexSharedSetup.SlotHeaderSize || _view.ReadUInt32(16) != Setup.SlotStrideBytes ||
            _view.ReadUInt32(20) != Setup.MaximumPayloadBytes || _view.ReadUInt64(24) != Setup.RegionBytes ||
            _view.ReadUInt64(32) != Setup.Nonce)
            throw new InvalidDataException("WFEX shared-memory control block does not match setup.");
    }

    private long CheckedReadySlot(WfexSharedNotification notification)
    {
        if (notification.SlotIndex >= Setup.SlotCount) throw new InvalidDataException("WFEX shared-memory notification has an invalid slot index.");
        long offset = SlotOffset(notification.SlotIndex);
        Thread.MemoryBarrier();
        if (_view.ReadInt32(offset) != StateReady || _view.ReadUInt64(offset + 8) != notification.Sequence)
            throw new InvalidDataException("WFEX shared-memory slot state or sequence does not match its notification.");
        return offset;
    }

    private long SlotOffset(ushort slot) => checked((long)WfexSharedSetup.ControlSize + (long)slot * Setup.SlotStrideBytes);

    private static ulong RandomNonce()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static void CleanupStaleRegions(string directory)
    {
        const string prefix = "waylandforge-wfex-";
        try
        {
            foreach (string path in Directory.EnumerateFiles(directory, prefix + "*"))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.LinkTarget is not null) continue;
                    string suffix = info.Name[prefix.Length..];
                    int separator = suffix.IndexOf('-');
                    if (separator <= 0 || !int.TryParse(suffix[..separator], out int processId)) continue;
                    bool processAlive;
                    try
                    {
                        using Process process = Process.GetProcessById(processId);
                        processAlive = !process.HasExited;
                    }
                    catch (ArgumentException)
                    {
                        processAlive = false;
                    }
                    if (!processAlive) File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public static class WfexSharedSetupAck
{
    public const uint Magic = 0x41534657; // WFSA
    public const uint Size = 8;

    public static void Write(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[(int)Size];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..], Size);
        stream.Write(buffer);
        stream.Flush();
    }

    public static void Read(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[(int)Size];
        WfexStreamReader.ReadExactly(stream, buffer);
        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != Magic || BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]) != Size)
            throw new InvalidDataException("Invalid WFEX shared-memory setup acknowledgement.");
    }
}
