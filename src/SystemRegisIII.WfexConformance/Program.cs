using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using SystemRegisIII.Core;

RunSelfTest();
if (args.Contains("--benchmark", StringComparer.Ordinal)) RunBenchmark();

static void RunSelfTest()
{
    byte[] valid = Header(400, 280, 400, 42, 400 * 280 * sizeof(uint));
    Require(WfexFrameHeader.TryParse(valid, out WfexFrameHeader parsed, out WfexValidationError error), $"valid header failed: {error}");
    Require(parsed == new WfexFrameHeader(400, 280, 400, 42, 448_000, 0), "valid header fields changed");
    Require(parsed.PixelCount == 112_000, "pixel count is wrong");

    Expect(valid.AsSpan(0, 31), WfexValidationError.HeaderTooShort);
    byte[] badMagic = (byte[])valid.Clone();
    badMagic[0] ^= 0xff;
    Expect(badMagic, WfexValidationError.InvalidMagic);
    Expect(Header(0, 280, 0, 0, 0), WfexValidationError.InvalidWidth);
    Expect(Header(400, -1, 400, 0, 0), WfexValidationError.InvalidHeight);
    Expect(Header(8_193, 1, 8_193, 0, 8_193 * sizeof(uint)), WfexValidationError.DimensionLimitExceeded);
    Expect(Header(400, 280, 404, 0, 448_000), WfexValidationError.UnsupportedStride);
    Expect(Header(400, 280, 400, 0, 447_996), WfexValidationError.InvalidPayloadLength);
    Expect(Header(int.MaxValue, 2, int.MaxValue, 0, 0), WfexValidationError.ArithmeticOverflow,
        new WfexLimits(int.MaxValue, int.MaxValue, int.MaxValue));
    Expect(valid, WfexValidationError.PayloadLimitExceeded, new WfexLimits(400, 280, 1_000));

    byte[] reserved = (byte[])valid.Clone();
    BinaryPrimitives.WriteInt32LittleEndian(reserved.AsSpan(28), 17);
    Require(WfexFrameHeader.TryParse(reserved, out parsed, out error) && parsed.Reserved == 17,
        "v1 reserved field compatibility changed");

    bool threw = false;
    try { WfexFrameHeader.Parse(badMagic); }
    catch (InvalidDataException) { threw = true; }
    Require(threw, "Parse did not reject malformed input");

    byte[] fragmentedResult = new byte[valid.Length];
    WfexStreamReader.ReadExactly(new FragmentedStream(valid, 3), fragmentedResult);
    Require(fragmentedResult.AsSpan().SequenceEqual(valid), "fragmented record changed in transit");

    threw = false;
    try { WfexStreamReader.ReadExactly(new MemoryStream(valid, 0, 17, false), new byte[valid.Length]); }
    catch (EndOfStreamException) { threw = true; }
    Require(threw, "truncated record was accepted");

    Stopwatch stalled = Stopwatch.StartNew();
    Require(!WfexStreamReader.TryReadExactly(new EmptyStream(), new byte[4], 5), "stalled producer was accepted");
    Require(stalled.ElapsedMilliseconds < 250, "stalled producer timeout was not bounded");

    byte[] handshake = new byte[WfexHandshakeRecord.Size];
    WfexHandshakeRecord.CreateProducerHello(new WfexLimits(400, 280, 448_000)).Write(handshake);
    WfexHandshakeRecord hello = WfexHandshakeRecord.Parse(handshake, WfexHandshakeRecord.ProducerMagic);
    Require(hello.MajorVersion == 2 && hello.PixelFormats == WfexPixelFormats.Argb8888, "producer hello changed");
    Require(hello.PresentationModes == (WfexPresentationModes.DeterministicLockstep | WfexPresentationModes.LatestFrame),
        "producer did not offer both presentation modes");
    WfexNegotiatedSession session = WfexNegotiation.AcceptProducerHello(
        hello, new WfexLimits(320, 240, 307_200), out WfexHandshakeRecord accept);
    Require(session.Limits == new WfexLimits(320, 240, 307_200), "negotiated limits were not intersected");
    Require((session.Capabilities & WfexCapabilities.SharedMemorySlots) != 0, "shared-memory capability was not selected");
    WfexCapabilities rawCapabilities = WfexCapabilities.RawFrameRecords | WfexCapabilities.VersionedFrameRecords;
    WfexNegotiatedSession rawSession = WfexNegotiation.AcceptProducerHello(
        hello, WfexLimits.Default, out _, rawCapabilities);
    Require((rawSession.Capabilities & WfexCapabilities.SharedMemorySlots) == 0, "raw policy selected shared memory");
    WfexNegotiatedSession latestSession = WfexNegotiation.AcceptProducerHello(
        hello, WfexLimits.Default, out WfexHandshakeRecord latestAccept,
        WfexNegotiation.HostCapabilities, WfexPresentationModes.LatestFrame);
    Require(latestSession.PresentationMode == WfexPresentationModes.LatestFrame &&
        latestAccept.PresentationModes == WfexPresentationModes.LatestFrame,
        "latest-frame presentation was not selected explicitly");
    threw = false;
    try
    {
        WfexNegotiation.AcceptProducerHello(
            hello, WfexLimits.Default, out _, rawCapabilities, WfexPresentationModes.LatestFrame);
    }
    catch (InvalidDataException) { threw = true; }
    Require(threw, "latest-frame presentation was accepted without shared memory");
    accept.Write(handshake);
    Require(WfexHandshakeRecord.Parse(handshake, WfexHandshakeRecord.HostMagic) == accept, "host accept did not roundtrip");

    byte[] badVersion = (byte[])handshake.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(badVersion.AsSpan(4), 3);
    ExpectHandshakeFailure(badVersion, WfexHandshakeRecord.HostMagic, "unsupported major version");
    byte[] badSize = (byte[])handshake.Clone();
    BinaryPrimitives.WriteUInt16LittleEndian(badSize.AsSpan(8), 47);
    ExpectHandshakeFailure(badSize, WfexHandshakeRecord.HostMagic, "unsupported record size");
    WfexHandshakeRecord incompatible = hello with { RequiredCapabilities = (WfexCapabilities)(1UL << 63) };
    threw = false;
    try { WfexNegotiation.AcceptProducerHello(incompatible, WfexLimits.Default, out _); }
    catch (InvalidDataException) { threw = true; }
    Require(threw, "unknown mandatory capability was accepted");

    using var pipeOut = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
    using var pipeIn = new AnonymousPipeClientStream(PipeDirection.In, pipeOut.ClientSafePipeHandle);
    Require(WfexStreamReader.ReadUpToWithTimeoutAsync(pipeIn, new byte[4], 10).GetAwaiter().GetResult() == 0,
        "pipe handshake timeout was not bounded");
    pipeOut.Write([1, 2, 3, 4]);
    pipeOut.Flush();
    byte[] afterTimeout = new byte[4];
    WfexStreamReader.ReadExactly(pipeIn, afterTimeout);
    Require(afterTimeout.AsSpan().SequenceEqual(new byte[] { 1, 2, 3, 4 }), "pipe was poisoned after handshake timeout");

    byte[] v2 = new byte[WfexV2FrameHeader.BaseHeaderSize];
    WfexV2FrameHeader expectedV2 = WfexV2FrameHeader.CreateRaw(400, 280, 42, 700_000_014, 16_666_667);
    expectedV2.Write(v2);
    Require(WfexV2FrameHeader.Parse(v2) == expectedV2, "v2 frame header did not roundtrip");
    byte[] fragmentedV2 = new byte[v2.Length];
    WfexStreamReader.ReadExactly(new FragmentedStream(v2, 5), fragmentedV2);
    Require(WfexV2FrameHeader.Parse(fragmentedV2) == expectedV2, "fragmented v2 header changed in transit");
    byte[] extended = new byte[72];
    (expectedV2 with { HeaderSize = 72, RecordBytes = 72UL + (uint)expectedV2.PayloadBytes }).Write(extended);
    extended[64] = 0xa5;
    Require(WfexV2FrameHeader.Parse(extended).HeaderSize == 72, "optional v2 header extension was rejected");

    ExpectV2Mutation(v2, bytes => bytes[0] ^= 0xff, WfexV2FrameValidationError.InvalidMagic);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 3), WfexV2FrameValidationError.UnsupportedMajorVersion);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 63), WfexV2FrameValidationError.InvalidHeaderSize);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 99), WfexV2FrameValidationError.UnsupportedCodec);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0), WfexV2FrameValidationError.MissingFullFrameFlag);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 0), WfexV2FrameValidationError.InvalidWidth);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), -1), WfexV2FrameValidationError.InvalidHeight);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 8_193), WfexV2FrameValidationError.DimensionLimitExceeded);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 404), WfexV2FrameValidationError.UnsupportedStride);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 447_996), WfexV2FrameValidationError.InvalidPayloadLength);
    ExpectV2(v2, WfexV2FrameValidationError.PayloadLimitExceeded, new WfexLimits(400, 280, 1_000));
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(56), 1), WfexV2FrameValidationError.InvalidRecordLength);
    ExpectV2Mutation(v2, bytes => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(48), 0), WfexV2FrameValidationError.InvalidNominalDuration);
    byte[] overflowV2 = (byte[])v2.Clone();
    BinaryPrimitives.WriteInt32LittleEndian(overflowV2.AsSpan(16), int.MaxValue);
    BinaryPrimitives.WriteInt32LittleEndian(overflowV2.AsSpan(20), 2);
    BinaryPrimitives.WriteInt32LittleEndian(overflowV2.AsSpan(24), int.MaxValue);
    ExpectV2(overflowV2, WfexV2FrameValidationError.ArithmeticOverflow, new WfexLimits(int.MaxValue, int.MaxValue, int.MaxValue));

    Require(WfexV2Sequence.IsExpected(false, 0, 77, out ulong sequenceExpected) && sequenceExpected == 77, "first v2 sequence was rejected");
    Require(WfexV2Sequence.IsExpected(true, 77, 78, out sequenceExpected) && sequenceExpected == 78, "next v2 sequence was rejected");
    Require(!WfexV2Sequence.IsExpected(true, 77, 77, out sequenceExpected) && sequenceExpected == 78, "duplicate v2 sequence was accepted");
    Require(WfexV2Sequence.IsExpected(true, ulong.MaxValue, 0, out sequenceExpected) && sequenceExpected == 0, "v2 sequence wrap policy changed");

    var sharedLimits = new WfexLimits(4, 4, 64);
    string staleSharedPath = Path.Combine(Path.GetTempPath(), "waylandforge-wfex-2147483647-conformance-stale");
    File.WriteAllBytes(staleSharedPath, [0]);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(staleSharedPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    using WfexSharedRegion sharedHost = WfexSharedRegion.CreateHost(sharedLimits, Path.GetTempPath());
    Require(!File.Exists(staleSharedPath), "stale shared-memory backing file was not cleaned on host creation");
    using var setupStream = new MemoryStream();
    sharedHost.Setup.Write(setupStream);
    setupStream.Position = 0;
    WfexSharedSetup setupRoundtrip = WfexSharedSetup.Read(setupStream);
    Require(setupRoundtrip == sharedHost.Setup, "shared-memory setup did not roundtrip");
    byte[] overflowingSetup = setupStream.ToArray();
    BinaryPrimitives.WriteUInt32LittleEndian(overflowingSetup.AsSpan(20), uint.MaxValue);
    bool setupOverflowRejected = false;
    try { WfexSharedSetup.Read(new MemoryStream(overflowingSetup, false)); }
    catch (InvalidDataException) { setupOverflowRejected = true; }
    Require(setupOverflowRejected, "overflowing shared-memory setup was accepted");
    string sharedPath = sharedHost.Setup.Path;
    if (!OperatingSystem.IsWindows())
        Require(File.GetUnixFileMode(sharedPath) == (UnixFileMode.UserRead | UnixFileMode.UserWrite), "shared-memory permissions are not user-private");
    using WfexSharedRegion sharedProducer = WfexSharedRegion.OpenProducer(sharedHost.Setup, sharedLimits);
    sharedHost.UnlinkBackingFile();
    Require(!File.Exists(sharedPath), "shared-memory backing path remained linked after both sides mapped it");
    uint[] sharedPixels = Enumerable.Range(0, 16).Select(static value => 0xff000000u | (uint)value).ToArray();
    bool badSlotRejected = false;
    try { sharedHost.Peek(new WfexSharedNotification(2, 0)); }
    catch (InvalidDataException) { badSlotRejected = true; }
    Require(badSlotRejected, "out-of-range shared-memory slot was accepted");
    WfexSharedNotification published = sharedProducer.Publish(sharedPixels, 4, 4, 4, 9, 150_000_003, 16_666_667);
    using var notificationStream = new MemoryStream();
    published.Write(notificationStream);
    notificationStream.Position = 0;
    WfexSharedNotification receivedNotification = WfexSharedNotification.Read(notificationStream);
    Require(receivedNotification == published, "shared-memory notification did not roundtrip");
    WfexSharedFrameMetadata sharedMetadata = sharedHost.Peek(receivedNotification);
    Require(sharedMetadata.FrameIndex == 9 && sharedMetadata.PixelCount == 16, "shared-memory metadata changed");
    uint[] sharedDestination = new uint[16];
    sharedHost.Consume(receivedNotification, sharedDestination);
    Require(sharedDestination.AsSpan().SequenceEqual(sharedPixels), "shared-memory pixels changed");
    for (ulong index = 10; index < 14; index++)
    {
        WfexSharedNotification next = sharedProducer.Publish(sharedPixels, 4, 4, 4, index, index * 16_666_667, 16_666_667);
        Require(next.SlotIndex == next.Sequence % 2, "shared-memory two-slot alternation changed");
        sharedHost.Consume(next, sharedDestination);
    }
    WfexSharedNotification busy0 = sharedProducer.Publish(sharedPixels, 4, 4, 4, 14, 14 * 16_666_667UL, 16_666_667);
    WfexSharedNotification busy1 = sharedProducer.Publish(sharedPixels, 4, 4, 4, 15, 15 * 16_666_667UL, 16_666_667);
    bool slotTimedOut = false;
    try { sharedProducer.Publish(sharedPixels, 4, 4, 4, 16, 16 * 16_666_667UL, 16_666_667, 5); }
    catch (TimeoutException) { slotTimedOut = true; }
    Require(slotTimedOut, "busy shared-memory slots did not apply backpressure");
    sharedHost.Consume(busy0, sharedDestination);
    sharedHost.Consume(busy1, sharedDestination);
    WfexSharedNotification afterBusy = sharedProducer.Publish(sharedPixels, 4, 4, 4, 16, 16 * 16_666_667UL, 16_666_667);
    Require(afterBusy.Sequence == busy1.Sequence + 1, "shared-memory timeout skipped a publication sequence");
    sharedHost.Consume(afterBusy, sharedDestination);
    using var ackStream = new MemoryStream();
    WfexSharedSetupAck.Write(ackStream);
    ackStream.Position = 0;
    WfexSharedSetupAck.Read(ackStream);
    byte[] badAck = ackStream.ToArray();
    badAck[0] ^= 0xff;
    bool badAckRejected = false;
    try { WfexSharedSetupAck.Read(new MemoryStream(badAck, false)); }
    catch (InvalidDataException) { badAckRejected = true; }
    Require(badAckRejected, "invalid shared-memory acknowledgement was accepted");

    using WfexSharedRegion nonceHost = WfexSharedRegion.CreateHost(sharedLimits, Path.GetTempPath());
    bool nonceRejected = false;
    try { using WfexSharedRegion ignored = WfexSharedRegion.OpenProducer(nonceHost.Setup with { Nonce = nonceHost.Setup.Nonce + 1 }, sharedLimits); }
    catch (InvalidDataException) { nonceRejected = true; }
    Require(nonceRejected, "mismatched shared-memory nonce was accepted");
    if (!OperatingSystem.IsWindows())
    {
        using WfexSharedRegion permissionHost = WfexSharedRegion.CreateHost(sharedLimits, Path.GetTempPath());
        File.SetUnixFileMode(permissionHost.Setup.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        bool broadPermissionsRejected = false;
        try { using WfexSharedRegion ignored = WfexSharedRegion.OpenProducer(permissionHost.Setup, sharedLimits); }
        catch (InvalidDataException) { broadPermissionsRejected = true; }
        Require(broadPermissionsRejected, "broad shared-memory permissions were accepted");
    }
    Console.WriteLine("WFEX CONFORMANCE OK · 68 CASES");
}

static void RunBenchmark()
{
    byte[] header = Header(400, 280, 400, 42, 448_000);
    for (int index = 0; index < 20_000; index++) WfexFrameHeader.Parse(header);
    const int iterations = 2_000_000;
    var timer = new Stopwatch();
    long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
    timer.Start();
    long checksum = 0;
    for (int index = 0; index < iterations; index++)
        checksum += WfexFrameHeader.Parse(header).PayloadBytes;
    timer.Stop();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
    double nanoseconds = timer.Elapsed.TotalMilliseconds * 1_000_000 / iterations;
    Console.WriteLine($"HEADER PARSE · {nanoseconds:0.0} NS · {allocated} B ALLOC · CHECK {checksum}");
    CopyBenchmark(320, 224, 4_000);
    CopyBenchmark(400, 280, 4_000);
}

static void CopyBenchmark(int width, int height, int iterations)
{
    uint[] source = new uint[width * height];
    uint[] destination = new uint[source.Length];
    Stopwatch timer = Stopwatch.StartNew();
    for (int index = 0; index < iterations; index++) source.AsSpan().CopyTo(destination);
    timer.Stop();
    double bytes = (double)source.Length * sizeof(uint) * iterations;
    Console.WriteLine($"COPY {width}X{height} · {bytes / timer.Elapsed.TotalSeconds / 1_000_000_000:0.00} GB/S · {timer.Elapsed.TotalMilliseconds / iterations:0.000} MS/FRAME");
}

static byte[] Header(int width, int height, int stride, ulong frameIndex, int payloadBytes)
{
    byte[] header = new byte[WfexFrameHeader.Size];
    BinaryPrimitives.WriteUInt32LittleEndian(header, WfexFrameHeader.Magic);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), width);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), height);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), stride);
    BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16), frameIndex);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), payloadBytes);
    return header;
}

static void Expect(ReadOnlySpan<byte> header, WfexValidationError expected, WfexLimits? limits = null)
{
    bool accepted = WfexFrameHeader.TryParse(header, out _, out WfexValidationError actual, limits);
    Require(!accepted && actual == expected, $"expected {expected}, got {(accepted ? "accepted" : actual)}");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void ExpectHandshakeFailure(byte[] record, uint expectedMagic, string description)
{
    bool threw = false;
    try { WfexHandshakeRecord.Parse(record, expectedMagic); }
    catch (InvalidDataException) { threw = true; }
    Require(threw, description);
}

static void ExpectV2Mutation(byte[] valid, Action<byte[]> mutate, WfexV2FrameValidationError expected)
{
    byte[] changed = (byte[])valid.Clone();
    mutate(changed);
    ExpectV2(changed, expected);
}

static void ExpectV2(ReadOnlySpan<byte> header, WfexV2FrameValidationError expected, WfexLimits? limits = null)
{
    bool accepted = WfexV2FrameHeader.TryParse(header, out _, out WfexV2FrameValidationError actual, limits);
    Require(!accepted && actual == expected, $"expected v2 {expected}, got {(accepted ? "accepted" : actual)}");
}

sealed class FragmentedStream(byte[] source, int fragmentSize) : MemoryStream(source, false)
{
    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, fragmentSize)]);
}

sealed class EmptyStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => 0;
    public override long Position { get => 0; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override int Read(Span<byte> buffer) => 0;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
