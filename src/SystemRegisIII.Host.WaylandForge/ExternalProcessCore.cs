using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class ExternalProcessCore : ISystemCore, IDisposable
{
    private const uint FrameMagic = 0x58454657; // WFEX
    private const byte StepCommand = (byte)'S';
    private readonly object _logLock = new();
    private readonly Queue<string> _stderrTail = new();
    private string _mode = "stdio";
    private string _command = string.Empty;
    private string _args = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _env = string.Empty;
    private string _wfexPath = string.Empty;
    private string _fallbackDllPath = string.Empty;
    private Process? _process;
    private Stream? _wfexStream;
    private DateTime _startedAtUtc;
    private int? _lastExitCode;
    private int _lastWidth;
    private int _lastHeight;
    private int _lastStride;
    private uint[] _frame = [];
    private uint[] _pendingFrame = [];
    private readonly byte[] _header = new byte[32];
    private readonly byte[] _stepCommandBuffer = new byte[5];

    public ExternalProcessCore(UiExternalCoreConfig config, string fallbackDllPath)
    {
        Configure(config, fallbackDllPath);
    }

    public ulong FrameIndex { get; private set; }
    public string Name => string.IsNullOrWhiteSpace(_command) ? "EXTERNAL DUMMY" : Path.GetFileName(_command).ToUpperInvariant();
    public string Mode => _mode;
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

    public void Configure(UiExternalCoreConfig config, string fallbackDllPath)
    {
        if (IsRunning)
        {
            Stop();
        }
        _mode = config.Mode.Trim().ToLowerInvariant() == "wfex_file" ? "wfex_file" : "stdio";
        _command = config.Command.Trim();
        _args = config.Args.Trim();
        _workingDirectory = config.WorkingDirectory.Trim();
        _env = config.Env.Trim();
        _wfexPath = config.WfexPath.Trim();
        _fallbackDllPath = fallbackDllPath;
    }

    public void Reset()
    {
        Stop();
        FrameIndex = 0;
        _lastExitCode = null;
        LastError = string.Empty;
        Array.Clear(_frame);
    }

    public void StepFrame(IInputSource input, IFrameSink frameSink)
    {
        try
        {
            if (_mode == "wfex_file")
            {
                StepWfexFile(frameSink);
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

    private void StepStdio(IInputSource input, IFrameSink frameSink)
    {
        EnsureStarted();

        SaturnInputState inputState = input.Poll();
        _stepCommandBuffer[0] = StepCommand;
        BinaryPrimitives.WriteUInt32LittleEndian(_stepCommandBuffer.AsSpan(1), (uint)inputState.Buttons);

        Stream stdin = _process!.StandardInput.BaseStream;
        Stream stdout = _process.StandardOutput.BaseStream;
        stdin.Write(_stepCommandBuffer);
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

    private void ReadWfexFrame(Stream stream, IFrameSink frameSink)
    {
        ReadExact(stream, _header);
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(0));
        if (magic != FrameMagic)
        {
            throw new InvalidOperationException("External core returned an invalid frame header.");
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(4));
        int height = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(8));
        int stride = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(12));
        FrameIndex = BinaryPrimitives.ReadUInt64LittleEndian(_header.AsSpan(16));
        int byteCount = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(24));

        if (width <= 0 || height <= 0 || stride < width || byteCount != width * height * sizeof(uint))
        {
            throw new InvalidOperationException("External core returned invalid frame dimensions.");
        }

        if (_frame.Length != width * height)
        {
            _frame = new uint[width * height];
        }

        ReadExact(stream, MemoryMarshal.AsBytes(_frame.AsSpan()));
        _lastWidth = width;
        _lastHeight = height;
        _lastStride = stride;
        frameSink.Present(_frame, width, height, stride);
    }

    private bool TryReadWfexFrame(Stream stream, IFrameSink frameSink)
    {
        long startPosition = stream.CanSeek ? stream.Position : -1;
        int waitMs = _frame.Length == 0 ? 2000 : 2;
        if (!TryReadExact(stream, _header, waitMs))
        {
            ResetStreamPosition(stream, startPosition);
            return false;
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(0));
        if (magic != FrameMagic)
        {
            throw new InvalidOperationException("External core returned an invalid frame header.");
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(4));
        int height = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(8));
        int stride = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(12));
        ulong frameIndex = BinaryPrimitives.ReadUInt64LittleEndian(_header.AsSpan(16));
        int byteCount = BinaryPrimitives.ReadInt32LittleEndian(_header.AsSpan(24));

        if (width <= 0 || height <= 0 || stride < width || byteCount != width * height * sizeof(uint))
        {
            throw new InvalidOperationException("External core returned invalid frame dimensions.");
        }

        if (_pendingFrame.Length != width * height)
        {
            _pendingFrame = new uint[width * height];
        }

        if (!TryReadExact(stream, MemoryMarshal.AsBytes(_pendingFrame.AsSpan()), 200))
        {
            ResetStreamPosition(stream, startPosition);
            return false;
        }

        (_frame, _pendingFrame) = (_pendingFrame, _frame);
        FrameIndex = frameIndex;
        _lastWidth = width;
        _lastHeight = height;
        _lastStride = stride;
        frameSink.Present(_frame, width, height, stride);
        return true;
    }

    private void EnsureStarted()
    {
        if (IsRunning)
        {
            return;
        }

        string command = _command;
        string args = _args;
        string wfexPath = ResolveWfexPath();
        if (string.IsNullOrWhiteSpace(command))
        {
            if (!File.Exists(_fallbackDllPath))
            {
                throw new FileNotFoundException("External dummy core is not built.", _fallbackDllPath);
            }
            command = "dotnet";
            args = QuoteArgument(_fallbackDllPath);
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
        _wfexStream?.Dispose();
        _wfexStream = null;
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

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException("External core process ended before a full frame was received.");
            }
            offset += read;
        }
    }

    private static bool TryReadExact(Stream stream, Span<byte> buffer, int timeoutMs)
    {
        Stopwatch wait = Stopwatch.StartNew();
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                if (wait.ElapsedMilliseconds >= timeoutMs)
                {
                    return false;
                }
                Thread.Sleep(1);
                continue;
            }
            offset += read;
        }
        return true;
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

    private string ResolveWfexPath()
    {
        if (!string.IsNullOrWhiteSpace(_wfexPath))
        {
            return _wfexPath;
        }
        return Path.Combine(Path.GetTempPath(), "waylandforge-external.wfex");
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
