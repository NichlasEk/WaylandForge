using System.Buffers.Binary;
using System.Diagnostics;

const int frameWidth = 320;
const int frameHeight = 224;
const uint frameMagic = 0x58454657; // WFEX
const byte stepCommand = (byte)'S';

ProbeOptions options = ProbeOptions.Parse(args);
Stream input = Console.OpenStandardInput();
Stream output = Console.OpenStandardOutput();
var frame = new uint[frameWidth * frameHeight];
var header = new byte[32];
var command = new byte[5];
var probe = new TargetProbe(options);
ulong frameIndex = 0;

while (ReadExact(input, command))
{
    if (command[0] != stepCommand)
    {
        break;
    }

    uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
    probe.EnsureStarted();
    RenderFrame(frame, frameWidth, frameHeight, frameIndex, buttons, probe);

    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), frameMagic);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), frameWidth);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), frameHeight);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), frameWidth);
    BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16), frameIndex);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), frame.Length * sizeof(uint));
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), 0);
    output.Write(header);
    output.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(frame.AsSpan()));
    output.Flush();

    frameIndex++;
}

probe.Dispose();

static bool ReadExact(Stream stream, Span<byte> buffer)
{
    int offset = 0;
    while (offset < buffer.Length)
    {
        int read = stream.Read(buffer[offset..]);
        if (read == 0)
        {
            return false;
        }
        offset += read;
    }
    return true;
}

static void RenderFrame(uint[] frame, int width, int height, ulong frameIndex, uint buttons, TargetProbe probe)
{
    uint background = probe.State switch
    {
        ProbeState.Running => 0xff0a1710,
        ProbeState.Exited => 0xff1a1510,
        ProbeState.Failed => 0xff190b0f,
        _ => 0xff0b1017,
    };

    Array.Fill(frame, background);
    int phase = (int)(frameIndex % 96);
    uint accent = probe.State switch
    {
        ProbeState.Running => 0xff4de28a,
        ProbeState.Exited => 0xffffbf62,
        ProbeState.Failed => 0xffff5c8a,
        _ => 0xff62b7ff,
    };

    DrawBorder(frame, width, height, accent);
    DrawBar(frame, width, 22, 32, 276, 22, accent, phase / 95.0);
    DrawStatusBlocks(frame, width, probe, frameIndex, buttons);
}

static void DrawStatusBlocks(uint[] frame, int width, TargetProbe probe, ulong frameIndex, uint buttons)
{
    uint on = probe.State == ProbeState.Running ? 0xff4de28a : 0xffff5c8a;
    uint off = 0xff26313b;
    for (int i = 0; i < 12; i++)
    {
        bool active = i switch
        {
            0 => probe.State == ProbeState.Running,
            1 => probe.State == ProbeState.Exited,
            2 => probe.State == ProbeState.Failed,
            _ => (buttons & (1u << i)) != 0,
        };
        FillRect(frame, width, 30 + i * 22, 76, 14, 14, active ? on : off);
    }

    int pulseX = 30 + (int)(frameIndex % 260);
    FillRect(frame, width, pulseX, 124, 28, 8, 0xff62b7ff);
    if (probe.ExitCode is int code)
    {
        int count = Math.Clamp(Math.Abs(code), 1, 10);
        for (int i = 0; i < count; i++)
        {
            FillRect(frame, width, 30 + i * 12, 158, 8, 24, 0xffffbf62);
        }
    }
}

static void DrawBorder(uint[] frame, int width, int height, uint color)
{
    FillRect(frame, width, 0, 0, width, 3, color);
    FillRect(frame, width, 0, height - 3, width, 3, color);
    FillRect(frame, width, 0, 0, 3, height, color);
    FillRect(frame, width, width - 3, 0, 3, height, color);
}

static void DrawBar(uint[] frame, int width, int x, int y, int w, int h, uint color, double amount)
{
    FillRect(frame, width, x, y, w, h, 0xff151f28);
    FillRect(frame, width, x, y, Math.Clamp((int)Math.Round(w * amount), 1, w), h, color);
}

static void FillRect(uint[] frame, int width, int x, int y, int w, int h, uint color)
{
    int height = frame.Length / width;
    for (int py = Math.Max(0, y); py < Math.Min(height, y + h); py++)
    {
        for (int px = Math.Max(0, x); px < Math.Min(width, x + w); px++)
        {
            frame[(py * width) + px] = color;
        }
    }
}

internal enum ProbeState
{
    Starting,
    Running,
    Exited,
    Failed,
}

internal sealed class TargetProbe : IDisposable
{
    private readonly ProbeOptions _options;
    private Process? _process;
    private bool _started;

    public TargetProbe(ProbeOptions options)
    {
        _options = options;
    }

    public ProbeState State { get; private set; } = ProbeState.Starting;
    public int? ExitCode => _process is { HasExited: true } process ? process.ExitCode : null;

    public void EnsureStarted()
    {
        if (_started)
        {
            if (_process is { HasExited: true } && State == ProbeState.Running)
            {
                State = ProbeState.Exited;
                Console.Error.WriteLine($"target exited with code {_process.ExitCode}");
            }
            return;
        }

        _started = true;
        if (string.IsNullOrWhiteSpace(_options.Target))
        {
            State = ProbeState.Failed;
            Console.Error.WriteLine("missing --target for process probe");
            return;
        }

        try
        {
            var start = new ProcessStartInfo
            {
                FileName = _options.Target,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
            };
            if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
            {
                start.WorkingDirectory = _options.WorkingDirectory;
            }
            foreach (string arg in _options.TargetArgs)
            {
                start.ArgumentList.Add(arg);
            }

            _process = Process.Start(start);
            if (_process is null)
            {
                State = ProbeState.Failed;
                Console.Error.WriteLine("failed to start target process");
                return;
            }

            _process.OutputDataReceived += (_, e) => Relay("stdout", e.Data);
            _process.ErrorDataReceived += (_, e) => Relay("stderr", e.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            State = ProbeState.Running;
            Console.Error.WriteLine($"started target pid={_process.Id}: {_options.Target}");
        }
        catch (Exception ex)
        {
            State = ProbeState.Failed;
            Console.Error.WriteLine("target launch failed: " + ex.Message);
        }
    }

    public void Dispose()
    {
        if (_process is not { HasExited: false } process)
        {
            _process?.Dispose();
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have exited between the HasExited check and Kill.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void Relay(string stream, string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            Console.Error.WriteLine($"{stream}: {line}");
        }
    }
}

internal sealed record ProbeOptions(string Target, string WorkingDirectory, string[] TargetArgs)
{
    public static ProbeOptions Parse(string[] args)
    {
        string target = string.Empty;
        string cwd = string.Empty;
        var targetArgs = new List<string>();
        bool passthrough = false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (passthrough)
            {
                targetArgs.Add(arg);
                continue;
            }
            switch (arg)
            {
                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--cwd" when i + 1 < args.Length:
                    cwd = args[++i];
                    break;
                case "--":
                    passthrough = true;
                    break;
                default:
                    targetArgs.Add(arg);
                    break;
            }
        }
        return new ProbeOptions(target, cwd, targetArgs.ToArray());
    }
}
