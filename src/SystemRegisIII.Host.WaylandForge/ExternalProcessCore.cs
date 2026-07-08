using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class ExternalProcessCore : ISystemCore, IDisposable
{
    private const uint FrameMagic = 0x58454657; // WFEX
    private const byte StepCommand = (byte)'S';
    private readonly string _dllPath;
    private Process? _process;
    private uint[] _frame = [];
    private readonly byte[] _header = new byte[32];
    private readonly byte[] _command = new byte[5];

    public ExternalProcessCore(string dllPath)
    {
        _dllPath = dllPath;
    }

    public ulong FrameIndex { get; private set; }
    public string Name => "EXTERNAL DUMMY";
    public bool IsRunning => _process is { HasExited: false };

    public void Reset()
    {
        Stop();
        FrameIndex = 0;
        Array.Clear(_frame);
    }

    public void StepFrame(IInputSource input, IFrameSink frameSink)
    {
        EnsureStarted();

        SaturnInputState inputState = input.Poll();
        _command[0] = StepCommand;
        BinaryPrimitives.WriteUInt32LittleEndian(_command.AsSpan(1), (uint)inputState.Buttons);

        Stream stdin = _process!.StandardInput.BaseStream;
        Stream stdout = _process.StandardOutput.BaseStream;
        stdin.Write(_command);
        stdin.Flush();

        ReadExact(stdout, _header);
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

        ReadExact(stdout, MemoryMarshal.AsBytes(_frame.AsSpan()));
        frameSink.Present(_frame, width, height, stride);
    }

    public void Dispose()
    {
        Stop();
    }

    private void EnsureStarted()
    {
        if (IsRunning)
        {
            return;
        }

        if (!File.Exists(_dllPath))
        {
            throw new FileNotFoundException("External dummy core is not built.", _dllPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(_dllPath);

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start external core process.");
    }

    private void Stop()
    {
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
}
