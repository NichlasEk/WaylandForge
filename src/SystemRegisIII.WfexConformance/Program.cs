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
    WfexNegotiatedSession session = WfexNegotiation.AcceptProducerHello(
        hello, new WfexLimits(320, 240, 307_200), out WfexHandshakeRecord accept);
    Require(session.Limits == new WfexLimits(320, 240, 307_200), "negotiated limits were not intersected");
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
    Console.WriteLine("WFEX CONFORMANCE OK · 44 CASES");
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
