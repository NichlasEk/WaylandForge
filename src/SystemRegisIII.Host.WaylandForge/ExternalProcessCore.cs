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
    private string _fallbackDllPath = string.Empty;
    private WfexLimits _wfexLimits = WfexLimits.Default;
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
    private readonly byte[] _header = new byte[32];
    private readonly byte[] _stepCommandBuffer = new byte[29];
    private readonly byte[] _inputHeader = new byte[48];
    private int _pointerX;
    private int _pointerY;
    private uint _pointerButtons;
    private bool _pointerInside;

    public ExternalProcessCore(UiExternalCoreConfig config, string fallbackDllPath)
    {
        Configure(config, fallbackDllPath);
    }

    public ulong FrameIndex { get; private set; }
    public string Name => string.IsNullOrWhiteSpace(_command) ? "EXTERNAL DUMMY" : ExternalName();
    public string Mode => _mode;
    public string PointerDriver => _pointerDriver;
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
        _wfexLimits = new WfexLimits(config.MaximumFrameWidth, config.MaximumFrameHeight, config.MaximumFrameBytes);
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

        ReadWfexFrame(stdout, frameSink);
    }

    private void StepWfexFile(IFrameSink frameSink)
    {
        EnsureStarted();
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

        SaturnInputState inputState = input.Poll();
        SendInputState(inputState);

        if (!WaitForSocketData(_frame.Length == 0 ? 2000 : 0))
        {
            if (_frame.Length > 0 && _lastWidth > 0 && _lastHeight > 0)
            {
                frameSink.Present(_frame, _lastWidth, _lastHeight, _lastStride);
                return;
            }
            throw new TimeoutException("External WF core did not produce an initial frame.");
        }

        int oldTimeout = _socketStream.ReadTimeout;
        _socketStream.ReadTimeout = _frame.Length == 0 ? 2000 : 500;
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
        WfexStreamReader.ReadExactly(stream, _header);
        WfexFrameHeader frameHeader = WfexFrameHeader.Parse(_header, _wfexLimits);
        FrameIndex = frameHeader.FrameIndex;

        if (_frame.Length != frameHeader.PixelCount)
        {
            _frame = new uint[frameHeader.PixelCount];
        }

        WfexStreamReader.ReadExactly(stream, MemoryMarshal.AsBytes(_frame.AsSpan()));
        _lastWidth = frameHeader.Width;
        _lastHeight = frameHeader.Height;
        _lastStride = frameHeader.StridePixels;
        frameSink.Present(_frame, frameHeader.Width, frameHeader.Height, frameHeader.StridePixels);
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
        CloseTransport();
        Process? process = _process;
        _process = null;
        if (process is null)
        {
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
        DeleteSocketPath();
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

    private void PresentLastFrame(IFrameSink frameSink)
    {
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
