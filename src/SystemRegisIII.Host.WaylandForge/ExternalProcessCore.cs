using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class ExternalProcessCore : ISystemCore, IDisposable
{
    private const uint InputMagic = 0x4e494657; // WFIN
    private const byte StepCommand = (byte)'S';
    private const byte ControllerPointerStepCommand = (byte)'Q';
    private readonly object _logLock = new();
    private readonly object _inputLock = new();
    private readonly Queue<string> _stderrTail = new();
    private string _mode = "stdio";
    private string _command = string.Empty;
    private string _args = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _env = string.Empty;
    private string _wfexPath = string.Empty;
    private string _socketPath = string.Empty;
    private string _pointerDriver = "absolute";
    private string _protocolPolicy = "v1";
    private string _frameTransport = "raw";
    private string _frameCodec = "raw";
    private string _presentationMode = "lockstep";
    private string _sharedMemoryDirectory = string.Empty;
    private string _fallbackDllPath = string.Empty;
    private WfexLimits _configuredWfexLimits = WfexLimits.Default;
    private WfexLimits _wfexLimits = WfexLimits.Default;
    private WfexNegotiatedSession _negotiatedSession = WfexNegotiatedSession.Version1(WfexLimits.Default);
    private bool _protocolNegotiated;
    private bool _protocolFallback;
    private WfexSharedRegion? _sharedRegion;
    private Process? _process;
    private Stream? _wfexStream;
    private Socket? _socketListener;
    private Socket? _socket;
    private NetworkStream? _socketStream;
    private DateTime _startedAtUtc;
    private int? _lastExitCode;
    private bool _startBlockedAfterExit;
    private int _lastWidth;
    private int _lastHeight;
    private int _lastStride;
    private uint[] _frame = [];
    private uint[] _pendingFrame = [];
    private byte[] _encodedFrame = [];
    private readonly byte[] _header = new byte[32];
    private readonly byte[] _v2Header = new byte[WfexV2FrameHeader.MaximumHeaderSize];
    private readonly byte[] _stepCommandBuffer = new byte[29];
    private readonly byte[] _inputHeader = new byte[48];
    private int _pointerX;
    private int _pointerY;
    private uint _pointerButtons;
    private bool _pointerInside;
    private bool _hasV2Sequence;
    private ulong _lastV2FrameIndex;
    private ulong _v2FramesReceived;
    private ulong _v2SequenceErrors;
    private ulong _lastPresentationTimestampNanoseconds;
    private ulong _lastNominalDurationNanoseconds;
    private ulong _sharedPayloadBytesAvoided;
    private ulong _compressedFrames;
    private ulong _compressedWireBytes;
    private ulong _compressedDecodedBytes;
    private bool _lastFrameUsesSharedMemory;
    private readonly object _latestFrameLock = new();
    private Thread? _latestFrameReader;
    private volatile bool _stopLatestFrameReader;
    private Exception? _latestFrameError;
    private uint[] _latestFrame = [];
    private int _latestWidth;
    private int _latestHeight;
    private int _latestStride;
    private ulong _latestGeneration;
    private ulong _presentedGeneration;
    private ulong _latestSimulationIndex;
    private ulong _latestPresentedIndex;
    private ulong _latestDroppedFrames;
    private long _latestReceivedTimestamp;
    private double _latestReceiveToPresentMilliseconds;

    public ExternalProcessCore(UiExternalCoreConfig config, string fallbackDllPath)
    {
        Configure(config, fallbackDllPath);
    }

    public ulong FrameIndex { get; private set; }
    public string Name => string.IsNullOrWhiteSpace(_command) ? "EXTERNAL DUMMY" : ExternalName();
    public string Mode => _mode;
    public string PointerDriver => _pointerDriver;
    public string ProtocolPolicy => _protocolPolicy;
    public string ProtocolStatus => !_protocolNegotiated
        ? $"{_protocolPolicy.ToUpperInvariant()} · PENDING"
        : _protocolFallback
            ? "V1 RAW LOCKSTEP · FALLBACK"
            : _negotiatedSession.DiagnosticLabel;
    public string ProtocolLimits => $"{_wfexLimits.MaximumWidth}X{_wfexLimits.MaximumHeight} · {_wfexLimits.MaximumPayloadBytes / (1024 * 1024)} MIB";
    public string ProtocolTransport => _sharedRegion is null ? "RAW STREAM" : "SHM X2 + CONTROL";
    public string ProtocolFrameStatus => _negotiatedSession.MajorVersion < 2
        ? $"V1 · FRAME {FrameIndex}"
        : $"V2 · RX {_v2FramesReceived} · SEQERR {_v2SequenceErrors}";
    public string ProtocolTiming => _negotiatedSession.MajorVersion < 2 || _v2FramesReceived == 0
        ? "-"
        : $"{_lastPresentationTimestampNanoseconds / 1_000_000.0:0.000} MS · {_lastNominalDurationNanoseconds / 1_000_000.0:0.000} MS";
    public string ProtocolCopySavings => _sharedRegion is not null
        ? $"{_sharedPayloadBytesAvoided / (1024.0 * 1024.0):0.0} MIB"
        : _compressedFrames > 0
            ? $"{Math.Max(0.0, (double)_compressedDecodedBytes - _compressedWireBytes) / (1024.0 * 1024.0):0.0} MIB"
            : "-";
    public string ProtocolCodec => _sharedRegion is not null
        ? "RAW SHM"
        : _compressedFrames > 0
            ? $"PACKRLE · {_compressedWireBytes * 100.0 / _compressedDecodedBytes:0.0}% WIRE"
            : (_negotiatedSession.Capabilities & WfexCapabilities.PackedRleFrameRecords) != 0
                ? "PACKRLE · WAITING"
                : "RAW ARGB";
    public string ProtocolPacing => _negotiatedSession.PresentationMode != WfexPresentationModes.LatestFrame
        ? "LOCKSTEP"
        : $"LATEST · SIM {_latestSimulationIndex} · SHOW {_latestPresentedIndex} · DROP {_latestDroppedFrames} · {_latestReceiveToPresentMilliseconds:0.00} MS";
    public bool IsRunning => _process is { HasExited: false };
    public int? ExitCode => _process is { HasExited: true } process ? process.ExitCode : _lastExitCode;
    public string Status => IsRunning ? "RUNNING" : ExitCode is int code ? $"EXIT {code}" : "STOPPED";
    public string LastError { get; private set; } = string.Empty;
    public IReadOnlyList<string> StderrTail
    {
        get
        {
            lock (_logLock)
            {
                return _stderrTail.ToArray();
            }
        }
    }

    private string ExternalName()
    {
        if (string.Equals(Path.GetFileName(_command), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? dll = SplitArguments(_args).FirstOrDefault(static arg => arg.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(dll))
            {
                return Path.GetFileNameWithoutExtension(dll).ToUpperInvariant();
            }
        }

        return Path.GetFileName(_command).ToUpperInvariant();
    }

    public void Configure(UiExternalCoreConfig config, string fallbackDllPath)
    {
        if (IsRunning)
        {
            Stop();
        }
        _mode = config.Mode.Trim().ToLowerInvariant() switch
        {
            "wfex_file" => "wfex_file",
            "wfcore_socket" => "wfcore_socket",
            _ => "stdio",
        };
        _command = config.Command.Trim();
        _args = config.Args.Trim();
        _workingDirectory = config.WorkingDirectory.Trim();
        _env = config.Env.Trim();
        _wfexPath = config.WfexPath.Trim();
        _socketPath = config.SocketPath.Trim();
        _pointerDriver = string.IsNullOrWhiteSpace(config.PointerDriver) ? "absolute" : config.PointerDriver.Trim().ToLowerInvariant();
        _protocolPolicy = config.ProtocolPolicy.Trim().ToLowerInvariant() switch
        {
            "prefer-v2" => "prefer-v2",
            "require-v2" => "require-v2",
            _ => "v1",
        };
        _configuredWfexLimits = new WfexLimits(config.MaximumFrameWidth, config.MaximumFrameHeight, config.MaximumFrameBytes);
        _frameTransport = config.FrameTransport.Trim().ToLowerInvariant() switch
        {
            "prefer-shm" => "prefer-shm",
            "require-shm" => "require-shm",
            _ => "raw",
        };
        _frameCodec = config.FrameCodec.Trim().ToLowerInvariant() switch
        {
            "prefer-packrle" => "prefer-packrle",
            "require-packrle" => "require-packrle",
            _ => "raw",
        };
        _presentationMode = config.PresentationMode.Equals("latest-frame", StringComparison.OrdinalIgnoreCase)
            ? "latest-frame"
            : "lockstep";
        _sharedMemoryDirectory = config.SharedMemoryDirectory.Trim();
        FrameIndex = 0;
        _lastWidth = 0;
        _lastHeight = 0;
        _lastStride = 0;
        _lastFrameUsesSharedMemory = false;
        Array.Clear(_frame);
        ResetProtocolNegotiation();
        _fallbackDllPath = fallbackDllPath;
        _startBlockedAfterExit = false;
    }

    public void Reset()
    {
        Stop();
        FrameIndex = 0;
        _lastExitCode = null;
        _startBlockedAfterExit = false;
        LastError = string.Empty;
        Array.Clear(_frame);
        _lastWidth = 0;
        _lastHeight = 0;
        _lastStride = 0;
        _lastFrameUsesSharedMemory = false;
        ResetProtocolNegotiation();
    }

    public void StepFrame(IInputSource input, IFrameSink frameSink)
    {
        try
        {
            if (TryHoldExitedProcessFrame(frameSink))
            {
                return;
            }

            if (_mode == "wfex_file")
            {
                StepWfexFile(frameSink);
            }
            else if (_mode == "wfcore_socket")
            {
                StepWfCoreSocket(input, frameSink);
            }
            else
            {
                StepStdio(input, frameSink);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AddStderrLine("HOST: " + ex.Message);
            Stop();
            throw;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public void PushInputNow(SaturnInputState inputState, uint rawKeyCode = 0, uint rawKeySerial = 0, bool rawKeyPressed = false)
    {
        if (_mode != "wfcore_socket" || _socket is null)
        {
            return;
        }

        SendInputState(inputState, rawKeyCode, rawKeySerial, rawKeyPressed);
    }

    public void SetPointerState(int x, int y, uint buttons, bool inside)
    {
        lock (_inputLock)
        {
            _pointerX = x;
            _pointerY = y;
            _pointerButtons = buttons;
            _pointerInside = inside;
        }
    }

    private void StepStdio(IInputSource input, IFrameSink frameSink)
    {
        EnsureStarted();

        SaturnInputState inputState = input.Poll();
        _stepCommandBuffer[0] = StepCommand;
        BinaryPrimitives.WriteUInt32LittleEndian(_stepCommandBuffer.AsSpan(1), (uint)inputState.Buttons);

        Stream stdin = _process!.StandardInput.BaseStream;
        Stream stdout = _process.StandardOutput.BaseStream;
        EnsureProtocolNegotiated(stdout, stdin);
        if (_pointerDriver == "stormakt_rts")
        {
            _stepCommandBuffer[0] = ControllerPointerStepCommand;
            BinaryPrimitives.WriteUInt32LittleEndian(_stepCommandBuffer.AsSpan(5), (uint)inputState.ControllerButtons);
            BinaryPrimitives.WriteInt16LittleEndian(_stepCommandBuffer.AsSpan(9), inputState.LeftX);
            BinaryPrimitives.WriteInt16LittleEndian(_stepCommandBuffer.AsSpan(11), inputState.LeftY);
            lock (_inputLock)
            {
                BinaryPrimitives.WriteInt32LittleEndian(_stepCommandBuffer.AsSpan(13), _pointerX);
                BinaryPrimitives.WriteInt32LittleEndian(_stepCommandBuffer.AsSpan(17), _pointerY);
                BinaryPrimitives.WriteUInt32LittleEndian(_stepCommandBuffer.AsSpan(21), _pointerButtons);
                BinaryPrimitives.WriteUInt32LittleEndian(_stepCommandBuffer.AsSpan(25), _pointerInside ? 1u : 0u);
            }
            stdin.Write(_stepCommandBuffer);
        }
        else
        {
            _stepCommandBuffer[0] = StepCommand;
            stdin.Write(_stepCommandBuffer.AsSpan(0, 5));
        }
        stdin.Flush();

        if (_negotiatedSession.PresentationMode == WfexPresentationModes.LatestFrame)
            PresentLatestFrame(frameSink);
        else
            ReadWfexFrame(stdout, frameSink);
    }

    private void StepWfexFile(IFrameSink frameSink)
    {
        EnsureStarted();
        EnsureFileProtocolPolicy();
        _wfexStream ??= OpenWfexReadStream();
        try
        {
            if (!TryReadWfexFrame(_wfexStream, frameSink))
            {
                if (_frame.Length > 0 && _lastWidth > 0 && _lastHeight > 0)
                {
                    frameSink.Present(_frame, _lastWidth, _lastHeight, _lastStride);
                    return;
                }
                throw new EndOfStreamException("External WFEX stream did not produce a full frame.");
            }
        }
        catch (EndOfStreamException) when (_frame.Length > 0 && _lastWidth > 0 && _lastHeight > 0)
        {
            frameSink.Present(_frame, _lastWidth, _lastHeight, _lastStride);
        }
    }

    private void StepWfCoreSocket(IInputSource input, IFrameSink frameSink)
    {
        EnsureStarted();
        _socketStream ??= OpenWfCoreSocketStream();
        EnsureProtocolNegotiated(_socketStream, _socketStream);

        SaturnInputState inputState = input.Poll();
        SendInputState(inputState);

        if (!WaitForSocketData(_lastWidth == 0 ? 2000 : 0))
        {
            if (_lastWidth > 0 && _lastHeight > 0)
            {
                if (_frame.Length > 0 && !_lastFrameUsesSharedMemory)
                    frameSink.Present(_frame, _lastWidth, _lastHeight, _lastStride);
                return;
            }
            throw new TimeoutException("External WF core did not produce an initial frame.");
        }

        int oldTimeout = _socketStream.ReadTimeout;
        _socketStream.ReadTimeout = _lastWidth == 0 ? 2000 : 500;
        try
        {
            ReadWfexFrame(_socketStream, frameSink);
        }
        finally
        {
            _socketStream.ReadTimeout = oldTimeout;
        }
    }

    private void SendInputState(SaturnInputState inputState, uint rawKeyCode = 0, uint rawKeySerial = 0, bool rawKeyPressed = false)
    {
        lock (_inputLock)
        {
            PrepareInputHeader(inputState, rawKeyCode, rawKeySerial, rawKeyPressed);
            TrySendInputHeader();
        }
    }

    private void PrepareInputHeader(SaturnInputState inputState, uint rawKeyCode, uint rawKeySerial, bool rawKeyPressed)
    {
        Array.Clear(_inputHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(0), InputMagic);
        BinaryPrimitives.WriteInt32LittleEndian(_inputHeader.AsSpan(4), _inputHeader.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(8), (uint)inputState.Buttons);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(12), rawKeyCode);
        BinaryPrimitives.WriteUInt64LittleEndian(_inputHeader.AsSpan(16), FrameIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(24), rawKeySerial);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(28), rawKeyPressed ? 1u : 0u);
        BinaryPrimitives.WriteInt32LittleEndian(_inputHeader.AsSpan(32), _pointerX);
        BinaryPrimitives.WriteInt32LittleEndian(_inputHeader.AsSpan(36), _pointerY);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(40), _pointerButtons);
        BinaryPrimitives.WriteUInt32LittleEndian(_inputHeader.AsSpan(44), _pointerInside ? 1u : 0u);
    }

    private bool WaitForSocketData(int timeoutMs)
    {
        Socket? socket = _socket;
        if (socket is null)
        {
            return false;
        }

        Stopwatch wait = Stopwatch.StartNew();
        while (socket.Available == 0)
        {
            if (wait.ElapsedMilliseconds >= timeoutMs)
            {
                return false;
            }
            Thread.Sleep(1);
        }
        return true;
    }

    private void TrySendInputHeader()
    {
        Socket? socket = _socket;
        if (socket is null)
        {
            return;
        }

        int oldSendTimeout = socket.SendTimeout;
        try
        {
            socket.SendTimeout = 2;
            int offset = 0;
            while (offset < _inputHeader.Length)
            {
                int written = socket.Send(_inputHeader.AsSpan(offset), SocketFlags.None);
                if (written <= 0)
                {
                    return;
                }
                offset += written;
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.WouldBlock or SocketError.IOPending or SocketError.NoBufferSpaceAvailable or SocketError.TimedOut)
        {
        }
        finally
        {
            socket.SendTimeout = oldSendTimeout;
        }
    }

    private void ReadWfexFrame(Stream stream, IFrameSink frameSink)
    {
        if (_sharedRegion is not null)
        {
            ReadSharedFrame(stream, frameSink);
            return;
        }
        FrameRecordInfo frameHeader = _negotiatedSession.MajorVersion >= 2
            ? ReadV2FrameHeader(stream)
            : ReadV1FrameHeader(stream);

        if (_frame.Length != frameHeader.PixelCount)
        {
            _frame = new uint[frameHeader.PixelCount];
        }

        if (frameHeader.Codec == WfexV2PayloadCodec.PackedRleArgb8888)
        {
            if ((_negotiatedSession.Capabilities & WfexCapabilities.PackedRleFrameRecords) == 0)
                throw new InvalidDataException("WFEX producer sent PACKRLE without negotiating it.");
            if (_encodedFrame.Length < frameHeader.PayloadBytes)
                _encodedFrame = new byte[WfexPackedRle.MaximumEncodedBytes(frameHeader.PixelCount)];
            Span<byte> encodedPayload = _encodedFrame.AsSpan(0, frameHeader.PayloadBytes);
            WfexStreamReader.ReadExactly(stream, encodedPayload);
            WfexPackedRle.Decode(encodedPayload, _frame);
            _compressedFrames++;
            _compressedWireBytes += (uint)frameHeader.PayloadBytes;
            _compressedDecodedBytes += (uint)frameHeader.DecodedPayloadBytes;
        }
        else
        {
            if (_frameCodec == "require-packrle" && frameHeader.IsV2)
                throw new InvalidDataException("WFEX PACKRLE was required but the producer sent a raw frame.");
            WfexStreamReader.ReadExactly(stream, MemoryMarshal.AsBytes(_frame.AsSpan()));
        }
        if (frameHeader.IsV2) CommitV2Sequence(frameHeader);
        FrameIndex = frameHeader.FrameIndex;
        _lastFrameUsesSharedMemory = false;
        _lastWidth = frameHeader.Width;
        _lastHeight = frameHeader.Height;
        _lastStride = frameHeader.StridePixels;
        frameSink.Present(_frame, frameHeader.Width, frameHeader.Height, frameHeader.StridePixels);
    }

    private void ReadSharedFrame(Stream controlStream, IFrameSink frameSink)
    {
        WfexSharedNotification notification = WfexSharedNotification.Read(controlStream);
        ReadOnlySpan<uint> pixels = _sharedRegion!.AcquirePixels(notification, out WfexSharedFrameMetadata metadata);
        try
        {
            ulong recordBytes = metadata.PayloadBytes >= 0
                ? (ulong)WfexV2FrameHeader.BaseHeaderSize + (uint)metadata.PayloadBytes
                : 0;
            var candidate = new WfexV2FrameHeader(
                WfexV2FrameHeader.CurrentMinorVersion,
                WfexV2FrameHeader.BaseHeaderSize,
                WfexV2FrameFlags.FullFrame,
                WfexV2PayloadCodec.RawArgb8888,
                metadata.Width,
                metadata.Height,
                metadata.StridePixels,
                metadata.PayloadBytes,
                metadata.FrameIndex,
                metadata.PresentationTimestampNanoseconds,
                metadata.NominalDurationNanoseconds,
                recordBytes);
            candidate.Write(_v2Header);
            WfexV2FrameHeader validated = WfexV2FrameHeader.Parse(
                _v2Header.AsSpan(0, WfexV2FrameHeader.BaseHeaderSize), _wfexLimits);
            if (pixels.Length != validated.PixelCount)
                throw new InvalidDataException("WFEX shared-memory pixel span does not match its metadata.");
            ValidateV2Sequence(validated.FrameIndex);
            frameSink.Present(pixels, validated.Width, validated.Height, validated.StridePixels);
            var record = new FrameRecordInfo(
                validated.Width, validated.Height, validated.StridePixels, validated.FrameIndex,
                validated.PayloadBytes, true, validated.PresentationTimestampNanoseconds,
                validated.NominalDurationNanoseconds);
            CommitV2Sequence(record);
            _sharedPayloadBytesAvoided += (uint)validated.PayloadBytes;
            FrameIndex = validated.FrameIndex;
            _lastFrameUsesSharedMemory = true;
            _lastWidth = validated.Width;
            _lastHeight = validated.Height;
            _lastStride = validated.StridePixels;
        }
        finally
        {
            _sharedRegion.ReleasePixels(notification);
        }
    }

    private void StartLatestFrameReader(Stream controlStream)
    {
        _stopLatestFrameReader = false;
        _latestFrameError = null;
        _latestFrameReader = new Thread(() => LatestFrameReaderLoop(controlStream))
        {
            IsBackground = true,
            Name = "WFEX latest-frame reader",
        };
        _latestFrameReader.Start();
    }

    private void LatestFrameReaderLoop(Stream controlStream)
    {
        try
        {
            while (!_stopLatestFrameReader)
            {
                WfexSharedNotification notification = WfexSharedNotification.Read(controlStream);
                ReadOnlySpan<uint> pixels = _sharedRegion!.AcquirePixels(notification, out WfexSharedFrameMetadata metadata);
                try
                {
                    WfexV2FrameHeader validated = ValidateSharedMetadata(metadata, pixels.Length);
                    lock (_latestFrameLock)
                    {
                        ValidateV2Sequence(validated.FrameIndex);
                        if (_latestGeneration != _presentedGeneration) _latestDroppedFrames++;
                        if (_latestFrame.Length != pixels.Length) _latestFrame = new uint[pixels.Length];
                        pixels.CopyTo(_latestFrame);
                        var record = new FrameRecordInfo(
                            validated.Width, validated.Height, validated.StridePixels, validated.FrameIndex,
                            validated.PayloadBytes, true, validated.PresentationTimestampNanoseconds,
                            validated.NominalDurationNanoseconds);
                        CommitV2Sequence(record);
                        _sharedPayloadBytesAvoided += (uint)validated.PayloadBytes;
                        _latestWidth = validated.Width;
                        _latestHeight = validated.Height;
                        _latestStride = validated.StridePixels;
                        _latestSimulationIndex = validated.FrameIndex;
                        _latestReceivedTimestamp = Stopwatch.GetTimestamp();
                        _latestGeneration++;
                        Monitor.PulseAll(_latestFrameLock);
                    }
                }
                finally
                {
                    _sharedRegion.ReleasePixels(notification);
                }
            }
        }
        catch (Exception ex) when (_stopLatestFrameReader && ex is EndOfStreamException or IOException or ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            lock (_latestFrameLock)
            {
                _latestFrameError = ex;
                Monitor.PulseAll(_latestFrameLock);
            }
        }
    }

    private WfexV2FrameHeader ValidateSharedMetadata(WfexSharedFrameMetadata metadata, int pixelCount)
    {
        ulong recordBytes = metadata.PayloadBytes >= 0
            ? (ulong)WfexV2FrameHeader.BaseHeaderSize + (uint)metadata.PayloadBytes
            : 0;
        var candidate = new WfexV2FrameHeader(
            WfexV2FrameHeader.CurrentMinorVersion,
            WfexV2FrameHeader.BaseHeaderSize,
            WfexV2FrameFlags.FullFrame,
            WfexV2PayloadCodec.RawArgb8888,
            metadata.Width,
            metadata.Height,
            metadata.StridePixels,
            metadata.PayloadBytes,
            metadata.FrameIndex,
            metadata.PresentationTimestampNanoseconds,
            metadata.NominalDurationNanoseconds,
            recordBytes);
        candidate.Write(_v2Header);
        WfexV2FrameHeader validated = WfexV2FrameHeader.Parse(
            _v2Header.AsSpan(0, WfexV2FrameHeader.BaseHeaderSize), _wfexLimits);
        if (pixelCount != validated.PixelCount)
            throw new InvalidDataException("WFEX shared-memory pixel span does not match its metadata.");
        return validated;
    }

    private void PresentLatestFrame(IFrameSink frameSink)
    {
        lock (_latestFrameLock)
        {
            if (_latestGeneration == 0 && _latestFrameError is null)
                Monitor.Wait(_latestFrameLock, 2000);
            if (_latestFrameError is not null)
                throw new InvalidDataException($"WFEX latest-frame reader failed: {_latestFrameError.Message}", _latestFrameError);
            if (_latestGeneration == 0)
                throw new TimeoutException("WFEX latest-frame producer did not publish an initial frame within 2000 ms.");
            frameSink.Present(_latestFrame, _latestWidth, _latestHeight, _latestStride);
            _presentedGeneration = _latestGeneration;
            _latestPresentedIndex = _latestSimulationIndex;
            _latestReceiveToPresentMilliseconds = Stopwatch.GetElapsedTime(_latestReceivedTimestamp).TotalMilliseconds;
            FrameIndex = _latestPresentedIndex;
            _lastWidth = _latestWidth;
            _lastHeight = _latestHeight;
            _lastStride = _latestStride;
            _lastFrameUsesSharedMemory = false;
        }
    }

    private FrameRecordInfo ReadV1FrameHeader(Stream stream)
    {
        WfexStreamReader.ReadExactly(stream, _header);
        WfexFrameHeader header = WfexFrameHeader.Parse(_header, _wfexLimits);
        return new FrameRecordInfo(
            header.Width, header.Height, header.StridePixels, header.FrameIndex,
            header.PayloadBytes, false, 0, 0,
            WfexV2PayloadCodec.RawArgb8888, header.PayloadBytes);
    }

    private FrameRecordInfo ReadV2FrameHeader(Stream stream)
    {
        WfexStreamReader.ReadExactly(stream, _v2Header.AsSpan(0, WfexV2FrameHeader.BaseHeaderSize));
        ushort headerSize = BinaryPrimitives.ReadUInt16LittleEndian(_v2Header.AsSpan(8));
        bool hasSupportedExtendedHeader = headerSize > WfexV2FrameHeader.BaseHeaderSize &&
            headerSize <= WfexV2FrameHeader.MaximumHeaderSize &&
            (headerSize & 7) == 0;
        if (hasSupportedExtendedHeader)
        {
            WfexStreamReader.ReadExactly(
                stream,
                _v2Header.AsSpan(WfexV2FrameHeader.BaseHeaderSize, headerSize - WfexV2FrameHeader.BaseHeaderSize));
        }
        WfexV2FrameHeader header = WfexV2FrameHeader.Parse(
            _v2Header.AsSpan(0, hasSupportedExtendedHeader ? headerSize : WfexV2FrameHeader.BaseHeaderSize),
            _wfexLimits);
        ValidateV2Sequence(header.FrameIndex);
        return new FrameRecordInfo(
            header.Width, header.Height, header.StridePixels, header.FrameIndex,
            header.PayloadBytes, true, header.PresentationTimestampNanoseconds,
            header.NominalDurationNanoseconds, header.Codec, header.DecodedPayloadBytes);
    }

    private void ValidateV2Sequence(ulong frameIndex)
    {
        if (WfexV2Sequence.IsExpected(_hasV2Sequence, _lastV2FrameIndex, frameIndex, out ulong expected)) return;
        _v2SequenceErrors++;
        throw new InvalidDataException($"WFEX v2 frame sequence error: expected {expected}, received {frameIndex}.");
    }

    private void CommitV2Sequence(FrameRecordInfo frame)
    {
        _hasV2Sequence = true;
        _lastV2FrameIndex = frame.FrameIndex;
        _v2FramesReceived++;
        _lastPresentationTimestampNanoseconds = frame.PresentationTimestampNanoseconds;
        _lastNominalDurationNanoseconds = frame.NominalDurationNanoseconds;
    }

    private bool TryReadWfexFrame(Stream stream, IFrameSink frameSink)
    {
        long startPosition = stream.CanSeek ? stream.Position : -1;
        int waitMs = _frame.Length == 0 ? 2000 : 2;
        if (!WfexStreamReader.TryReadExactly(stream, _header, waitMs))
        {
            ResetStreamPosition(stream, startPosition);
            return false;
        }

        WfexFrameHeader frameHeader = WfexFrameHeader.Parse(_header, _wfexLimits);

        if (_pendingFrame.Length != frameHeader.PixelCount)
        {
            _pendingFrame = new uint[frameHeader.PixelCount];
        }

        if (!WfexStreamReader.TryReadExactly(stream, MemoryMarshal.AsBytes(_pendingFrame.AsSpan()), 200))
        {
            ResetStreamPosition(stream, startPosition);
            return false;
        }

        (_frame, _pendingFrame) = (_pendingFrame, _frame);
        FrameIndex = frameHeader.FrameIndex;
        _lastWidth = frameHeader.Width;
        _lastHeight = frameHeader.Height;
        _lastStride = frameHeader.StridePixels;
        frameSink.Present(_frame, frameHeader.Width, frameHeader.Height, frameHeader.StridePixels);
        return true;
    }

    private void EnsureStarted()
    {
        if (IsRunning)
        {
            return;
        }
        if (_process is { HasExited: true } process)
        {
            _lastExitCode = process.ExitCode;
            _process = null;
            CloseTransport();
            process.Dispose();
            _startBlockedAfterExit = true;
        }
        if (_startBlockedAfterExit)
        {
            throw new InvalidOperationException("External core exited. Press RESTART to launch it again.");
        }

        string command = _command;
        string args = _args;
        string wfexPath = ResolveWfexPath();
        string socketPath = ResolveSocketPath();
        if (string.IsNullOrWhiteSpace(command))
        {
            if (!File.Exists(_fallbackDllPath))
            {
                throw new FileNotFoundException("External dummy core is not built.", _fallbackDllPath);
            }
            command = "dotnet";
            args = QuoteArgument(_fallbackDllPath);
        }

        if (_mode == "wfcore_socket")
        {
            StartSocketListener(socketPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (_mode == "wfex_file")
        {
            startInfo.Environment["OPENTYRIAN_WFEX_PATH"] = wfexPath;
        }
        if (_mode == "wfcore_socket")
        {
            startInfo.Environment["WFCORE_SOCKET"] = socketPath;
            startInfo.Environment["OPENTYRIAN_WFCORE"] = "1";
        }
        foreach ((string key, string value) in SplitEnvironment(_env))
        {
            startInfo.Environment[key] = value;
        }
        startInfo.Environment[WfexNegotiation.PolicyEnvironmentVariable] = _protocolPolicy;
        if (!string.IsNullOrWhiteSpace(_workingDirectory))
        {
            startInfo.WorkingDirectory = _workingDirectory;
        }
        foreach (string arg in SplitArguments(args))
        {
            startInfo.ArgumentList.Add(arg);
        }

        _startedAtUtc = DateTime.UtcNow;
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start external core process.");
        ResetProtocolNegotiation();
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                AddStderrLine(e.Data);
            }
        };
        _process.BeginErrorReadLine();
    }

    private void Stop()
    {
        bool readerOwnsProcessOutput = _latestFrameReader is not null;
        if (!readerOwnsProcessOutput) CloseTransport();
        Process? process = _process;
        _process = null;
        if (process is null)
        {
            CloseTransport();
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(250))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1000);
                }
            }
            if (process.HasExited)
            {
                _lastExitCode = process.ExitCode;
            }
        }
        finally
        {
            process.Dispose();
            if (readerOwnsProcessOutput) CloseTransport();
        }
    }

    private void CloseTransport()
    {
        _wfexStream?.Dispose();
        _wfexStream = null;
        _socketStream?.Dispose();
        _socketStream = null;
        _socket?.Dispose();
        _socket = null;
        _socketListener?.Dispose();
        _socketListener = null;
        _stopLatestFrameReader = true;
        if (_latestFrameReader is not null && _latestFrameReader != Thread.CurrentThread)
            _latestFrameReader.Join(500);
        _latestFrameReader = null;
        _sharedRegion?.Dispose();
        _sharedRegion = null;
        DeleteSocketPath();
    }

    private void ResetProtocolNegotiation()
    {
        _wfexLimits = _configuredWfexLimits;
        _negotiatedSession = WfexNegotiatedSession.Version1(_configuredWfexLimits);
        _protocolNegotiated = false;
        _protocolFallback = false;
        _hasV2Sequence = false;
        _lastV2FrameIndex = 0;
        _v2FramesReceived = 0;
        _v2SequenceErrors = 0;
        _lastPresentationTimestampNanoseconds = 0;
        _lastNominalDurationNanoseconds = 0;
        _sharedPayloadBytesAvoided = 0;
        _compressedFrames = 0;
        _compressedWireBytes = 0;
        _compressedDecodedBytes = 0;
        _lastFrameUsesSharedMemory = false;
        lock (_latestFrameLock)
        {
            _latestFrameError = null;
            _latestFrame = [];
            _latestWidth = 0;
            _latestHeight = 0;
            _latestStride = 0;
            _latestGeneration = 0;
            _presentedGeneration = 0;
            _latestSimulationIndex = 0;
            _latestPresentedIndex = 0;
            _latestDroppedFrames = 0;
            _latestReceivedTimestamp = 0;
            _latestReceiveToPresentMilliseconds = 0;
        }
    }

    private void EnsureFileProtocolPolicy()
    {
        if (_protocolNegotiated) return;
        if (_protocolPolicy == "require-v2" || _frameTransport == "require-shm" || _frameCodec == "require-packrle")
            throw new InvalidOperationException("WFEX v2 negotiation requires an interactive stdio or Unix-socket control channel.");
        _protocolFallback = _protocolPolicy == "prefer-v2";
        _protocolNegotiated = true;
    }

    private void EnsureProtocolNegotiated(Stream producerOutput, Stream producerInput)
    {
        if (_protocolNegotiated) return;
        if (_protocolPolicy == "v1")
        {
            if (_frameTransport == "require-shm")
                throw new InvalidOperationException("WFEX shared memory requires WFEX v2 negotiation.");
            if (_frameCodec == "require-packrle")
                throw new InvalidOperationException("WFEX PACKRLE requires WFEX v2 negotiation.");
            _protocolNegotiated = true;
            return;
        }

        byte[] buffer = new byte[WfexHandshakeRecord.Size];
        int received = ReadHandshakeWithTimeout(producerOutput, buffer, 2000);
        if (received == 0 && _protocolPolicy == "prefer-v2")
        {
            if (_frameTransport == "require-shm")
                throw new TimeoutException("WFEX shared memory was required but the producer did not offer a v2 handshake.");
            if (_frameCodec == "require-packrle")
                throw new TimeoutException("WFEX PACKRLE was required but the producer did not offer a v2 handshake.");
            _protocolFallback = true;
            _protocolNegotiated = true;
            AddStderrLine("HOST: WFEX v2 hello not offered; using v1 fallback.");
            return;
        }
        if (received == 0)
            throw new TimeoutException("WFEX v2 was required but the producer did not send a handshake within 2000 ms.");
        if (received != buffer.Length)
            throw new InvalidDataException($"WFEX v2 producer sent a truncated handshake ({received}/{buffer.Length} bytes).");

        WfexHandshakeRecord hello = WfexHandshakeRecord.Parse(buffer, WfexHandshakeRecord.ProducerMagic);
        WfexCapabilities enabledCapabilities = WfexCapabilities.RawFrameRecords | WfexCapabilities.VersionedFrameRecords;
        bool producerOffersShared = (hello.Capabilities & WfexCapabilities.SharedMemorySlots) != 0;
        if (_frameTransport != "raw" && producerOffersShared)
            enabledCapabilities |= WfexCapabilities.SharedMemorySlots;
        else if (_frameCodec != "raw")
            enabledCapabilities |= WfexCapabilities.PackedRleFrameRecords;
        WfexPresentationModes requestedPresentationMode = _presentationMode == "latest-frame"
            ? WfexPresentationModes.LatestFrame
            : WfexPresentationModes.DeterministicLockstep;
        _negotiatedSession = WfexNegotiation.AcceptProducerHello(
            hello, _configuredWfexLimits, out WfexHandshakeRecord response, enabledCapabilities, requestedPresentationMode);
        bool sharedMemorySelected = (_negotiatedSession.Capabilities & WfexCapabilities.SharedMemorySlots) != 0;
        if (_frameTransport == "require-shm" && !sharedMemorySelected)
            throw new InvalidDataException("WFEX shared memory was required but the producer did not offer it.");
        if (_frameCodec == "require-packrle" && sharedMemorySelected)
            throw new InvalidOperationException("WFEX require-packrle cannot be combined with a selected shared-memory transport.");
        if (_frameCodec == "require-packrle" &&
            (_negotiatedSession.Capabilities & WfexCapabilities.PackedRleFrameRecords) == 0)
            throw new InvalidDataException("WFEX PACKRLE was required but the producer did not offer it.");
        if (sharedMemorySelected)
        {
            try
            {
                _sharedRegion = WfexSharedRegion.CreateHost(
                    _negotiatedSession.Limits,
                    string.IsNullOrWhiteSpace(_sharedMemoryDirectory) ? null : _sharedMemoryDirectory);
            }
            catch (Exception ex) when (_frameTransport == "prefer-shm")
            {
                AddStderrLine($"HOST: shared-memory setup unavailable ({ex.Message}); using streamed v2 records.");
                enabledCapabilities &= ~WfexCapabilities.SharedMemorySlots;
                if (_frameCodec != "raw") enabledCapabilities |= WfexCapabilities.PackedRleFrameRecords;
                _negotiatedSession = WfexNegotiation.AcceptProducerHello(
                    hello, _configuredWfexLimits, out response, enabledCapabilities,
                    WfexPresentationModes.DeterministicLockstep);
                sharedMemorySelected = false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"WFEX shared memory was required but region creation failed: {ex.Message}", ex);
            }
        }
        response.Write(buffer);
        producerInput.Write(buffer);
        producerInput.Flush();
        _wfexLimits = _negotiatedSession.Limits;
        if (sharedMemorySelected)
        {
            ConfigureSharedMemory(producerOutput, producerInput);
            if (_negotiatedSession.PresentationMode == WfexPresentationModes.LatestFrame)
                StartLatestFrameReader(producerOutput);
        }
        _protocolNegotiated = true;
        AddStderrLine($"HOST: negotiated {_negotiatedSession.DiagnosticLabel}.");
    }

    private void ConfigureSharedMemory(Stream producerOutput, Stream producerInput)
    {
        WfexSharedRegion region = _sharedRegion ?? throw new InvalidOperationException("WFEX shared-memory region was not prepared.");
        region.Setup.Write(producerInput);
        producerInput.Flush();
        byte[] acknowledgement = new byte[WfexSharedSetupAck.Size];
        int received = ReadHandshakeWithTimeout(producerOutput, acknowledgement, 2000);
        if (received != acknowledgement.Length)
            throw new TimeoutException($"WFEX shared-memory setup acknowledgement was incomplete ({received}/{acknowledgement.Length} bytes).");
        using var ackStream = new MemoryStream(acknowledgement, writable: false);
        WfexSharedSetupAck.Read(ackStream);
        region.UnlinkBackingFile();
    }

    private static int ReadHandshakeWithTimeout(Stream stream, byte[] buffer, int timeoutMilliseconds)
    {
        return WfexStreamReader.ReadUpToWithTimeoutAsync(stream, buffer, timeoutMilliseconds).GetAwaiter().GetResult();
    }

    private bool TryHoldExitedProcessFrame(IFrameSink frameSink)
    {
        if (_startBlockedAfterExit)
        {
            PresentLastFrame(frameSink);
            return true;
        }

        Process? process = _process;
        if (process is not { HasExited: true })
        {
            return false;
        }

        _lastExitCode = process.ExitCode;
        AddStderrLine($"HOST: external core exited with code {_lastExitCode.Value}.");
        _process = null;
        CloseTransport();
        process.Dispose();
        _startBlockedAfterExit = true;
        PresentLastFrame(frameSink);
        return true;
    }

    private readonly record struct FrameRecordInfo(
        int Width,
        int Height,
        int StridePixels,
        ulong FrameIndex,
        int PayloadBytes,
        bool IsV2,
        ulong PresentationTimestampNanoseconds,
        ulong NominalDurationNanoseconds,
        WfexV2PayloadCodec Codec = WfexV2PayloadCodec.RawArgb8888,
        int DecodedPayloadBytes = 0)
    {
        public int PixelCount => (DecodedPayloadBytes == 0 ? PayloadBytes : DecodedPayloadBytes) / sizeof(uint);
    }

    private void PresentLastFrame(IFrameSink frameSink)
    {
        if (_lastFrameUsesSharedMemory) return;
        if (_frame.Length > 0 && _lastWidth > 0 && _lastHeight > 0)
        {
            frameSink.Present(_frame, _lastWidth, _lastHeight, _lastStride);
            return;
        }
        if (_pendingFrame.Length > 0 && _lastWidth > 0 && _lastHeight > 0)
        {
            frameSink.Present(_pendingFrame, _lastWidth, _lastHeight, _lastStride);
        }
    }

    private static void ResetStreamPosition(Stream stream, long position)
    {
        if (position >= 0 && stream.CanSeek)
        {
            stream.Position = position;
        }
    }

    private Stream OpenWfexReadStream()
    {
        string path = ResolveWfexPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("external_core.wfex_path is required for wfex_file mode.");
        }

        Stopwatch wait = Stopwatch.StartNew();
        while (!File.Exists(path) && wait.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(10);
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("External WFEX stream was not created.", path);
        }
        while (IsStaleRegularFile(path) && wait.ElapsedMilliseconds < 2000)
        {
            Thread.Sleep(10);
        }
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }

    private bool IsStaleRegularFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length > 0 && info.LastWriteTimeUtc < _startedAtUtc;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private NetworkStream OpenWfCoreSocketStream()
    {
        if (_socket is null)
        {
            Stopwatch wait = Stopwatch.StartNew();
            while (wait.ElapsedMilliseconds < 2000)
            {
                try
                {
                    _socket = _socketListener?.Accept();
                    if (_socket is not null)
                    {
                        _socket.Blocking = true;
                        break;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    Thread.Sleep(1);
                }
            }
        }

        if (_socket is null)
        {
            throw new TimeoutException("External WF core did not connect to the host socket.");
        }

        return new NetworkStream(_socket, ownsSocket: false);
    }

    private void StartSocketListener(string path)
    {
        DeleteSocketPath(path);
        _socketListener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
        {
            Blocking = false,
        };
        _socketListener.Bind(new UnixDomainSocketEndPoint(path));
        _socketListener.Listen(1);
    }

    private string ResolveWfexPath()
    {
        if (!string.IsNullOrWhiteSpace(_wfexPath))
        {
            return _wfexPath;
        }
        return Path.Combine(Path.GetTempPath(), "waylandforge-external.wfex");
    }

    private string ResolveSocketPath()
    {
        if (!string.IsNullOrWhiteSpace(_socketPath))
        {
            return _socketPath;
        }
        return Path.Combine(Path.GetTempPath(), "waylandforge-wfcore.sock");
    }

    private void DeleteSocketPath() => DeleteSocketPath(ResolveSocketPath());

    private static void DeleteSocketPath(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void AddStderrLine(string line)
    {
        lock (_logLock)
        {
            _stderrTail.Enqueue(line.Length > 120 ? line[..120] : line);
            while (_stderrTail.Count > 8)
            {
                _stderrTail.Dequeue();
            }
        }
    }

    private static IEnumerable<string> SplitArguments(string args)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < args.Length; i++)
        {
            char ch = args[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result;
    }

    private static IEnumerable<(string Key, string Value)> SplitEnvironment(string env)
    {
        foreach (string item in env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equals = item.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }
            yield return (item[..equals].Trim(), item[(equals + 1)..].Trim());
        }
    }

    private static string QuoteArgument(string arg)
    {
        return "\"" + arg.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
