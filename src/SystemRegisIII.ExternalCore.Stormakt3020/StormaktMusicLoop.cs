using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

internal sealed class StormaktMusicLoop : IDisposable
{
    private const int SampleRate = 48_000;
    private const int Channels = 2;
    private const int PacketFrames = 4_096;
    private const int HeaderBytes = 24;
    private const string DefaultSocketPath = "/tmp/waylandforge-audio.sock";

    private readonly float[] _samples;
    private readonly int _totalFrames;
    private readonly int _crossfadeFrames;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private volatile bool _connectedOnce;

    private StormaktMusicLoop(float[] samples, string socketPath)
    {
        _samples = samples;
        _totalFrames = samples.Length / Channels;
        _crossfadeFrames = Math.Min(SampleRate / 2, _totalFrames / 8);
        _socketPath = socketPath;
        _thread = new Thread(StreamLoop)
        {
            IsBackground = true,
            Name = "Stormakt 3020 music",
        };
        _thread.Start();
    }

    public static StormaktMusicLoop? TryStartDefault()
    {
        string? enabled = Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_MUSIC");
        if (string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? overridePath = Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_MUSIC_PATH");
        string[] paths = string.IsNullOrWhiteSpace(overridePath)
            ?
            [
                Path.Combine(Environment.CurrentDirectory, "assets", "stormakt3020", "stormakt-over-oresund-v1.wav"),
                Path.Combine(AppContext.BaseDirectory, "assets", "stormakt3020", "stormakt-over-oresund-v1.wav"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "stormakt3020", "stormakt-over-oresund-v1.wav")),
            ]
            : [overridePath];

        foreach (string path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                float[] samples = LoadPcm16StereoWav(path);
                string socketPath = Environment.GetEnvironmentVariable("WAYLANDFORGE_AUDIO_SOCKET") ?? DefaultSocketPath;
                Console.Error.WriteLine($"Stormakt music: loaded {Path.GetFileName(path)} ({samples.Length / Channels / SampleRate}s).");
                return new StormaktMusicLoop(samples, socketPath);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Stormakt music disabled: {exception.Message}");
                return null;
            }
        }

        Console.Error.WriteLine("Stormakt music disabled: soundtrack WAV not found.");
        return null;
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (_thread.Join(TimeSpan.FromSeconds(2)))
        {
            if (_connectedOnce)
            {
                _ = TryClearBuffer();
            }
            _stop.Dispose();
        }
    }

    private void StreamLoop()
    {
        byte[] packet = new byte[HeaderBytes + (PacketFrames * Channels * sizeof(float))];
        int frameIndex = 0;
        long acceptedFrames = 0;
        Stopwatch clock = Stopwatch.StartNew();
        bool announcedConnection = false;
        bool needsClear = true;

        while (!_stop.IsCancellationRequested)
        {
            if (needsClear)
            {
                if (!TryClearBuffer())
                {
                    _stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    continue;
                }
                needsClear = false;
                acceptedFrames = 0;
                clock.Restart();
            }

            double bufferedSeconds = (acceptedFrames / (double)SampleRate) - clock.Elapsed.TotalSeconds;
            if (bufferedSeconds > 0.55)
            {
                _stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(12));
                continue;
            }

            FillPacket(packet, frameIndex, PacketFrames);
            int accepted = TrySendPacket(packet, PacketFrames);
            if (accepted >= 0)
            {
                frameIndex = AdvanceFrameIndex(frameIndex, accepted);
                acceptedFrames += accepted;
                if (!announcedConnection)
                {
                    Console.Error.WriteLine($"Stormakt music: streaming to {_socketPath}.");
                    announcedConnection = true;
                    _connectedOnce = true;
                }
                if (accepted < PacketFrames)
                {
                    _stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(12));
                }
                continue;
            }

            announcedConnection = false;
            needsClear = true;
            acceptedFrames = 0;
            clock.Restart();
            _stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
        }
    }

    private void FillPacket(byte[] packet, int frameIndex, int frames)
    {
        Span<byte> header = packet.AsSpan(0, HeaderBytes);
        "WFAU"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], SampleRate);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], Channels);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], (uint)frames);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], (uint)(frames * Channels * sizeof(float)));

        Span<byte> payload = packet.AsSpan(HeaderBytes);
        for (int outputFrame = 0; outputFrame < frames; outputFrame++)
        {
            float left = _samples[frameIndex * Channels];
            float right = _samples[(frameIndex * Channels) + 1];
            if (frameIndex >= _totalFrames - _crossfadeFrames)
            {
                int headFrame = frameIndex - (_totalFrames - _crossfadeFrames);
                float headWeight = headFrame / (float)_crossfadeFrames;
                float tailWeight = 1.0f - headWeight;
                left = (left * tailWeight) + (_samples[headFrame * Channels] * headWeight);
                right = (right * tailWeight) + (_samples[(headFrame * Channels) + 1] * headWeight);
            }

            int byteOffset = outputFrame * Channels * sizeof(float);
            BinaryPrimitives.WriteSingleLittleEndian(payload[byteOffset..], left);
            BinaryPrimitives.WriteSingleLittleEndian(payload[(byteOffset + sizeof(float))..], right);

            frameIndex = AdvanceFrameIndex(frameIndex, 1);
        }
    }

    private int AdvanceFrameIndex(int frameIndex, int frames)
    {
        for (int index = 0; index < frames; index++)
        {
            frameIndex++;
            if (frameIndex >= _totalFrames)
            {
                frameIndex = _crossfadeFrames;
            }
        }
        return frameIndex;
    }

    private int TrySendPacket(byte[] packet, int frames)
    {
        try
        {
            using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            {
                SendTimeout = 1_000,
                ReceiveTimeout = 1_000,
            };
            socket.Connect(new UnixDomainSocketEndPoint(_socketPath));
            int sent = 0;
            while (sent < packet.Length)
            {
                int sentNow = socket.Send(packet.AsSpan(sent), SocketFlags.None);
                if (sentNow == 0)
                {
                    return -1;
                }
                sent += sentNow;
            }

            byte[] response = new byte[128];
            int received = 0;
            while (received < response.Length)
            {
                int receivedNow = socket.Receive(response.AsSpan(received), SocketFlags.None);
                if (receivedNow == 0)
                {
                    break;
                }
                received += receivedNow;
                if (response.AsSpan(0, received).Contains((byte)'\n'))
                {
                    break;
                }
            }
            string text = Encoding.ASCII.GetString(response, 0, received);
            if (!text.StartsWith($"OK WFAU frames={frames} accepted=", StringComparison.Ordinal))
            {
                DebugFailure($"unexpected audio response: {text.Trim()}");
                return -1;
            }
            int acceptedStart = text.IndexOf("accepted=", StringComparison.Ordinal) + "accepted=".Length;
            int acceptedEnd = text.IndexOf(' ', acceptedStart);
            if (acceptedEnd < 0)
            {
                acceptedEnd = text.IndexOf('\n', acceptedStart);
            }
            if (acceptedEnd < 0 ||
                !int.TryParse(text.AsSpan(acceptedStart, acceptedEnd - acceptedStart), out int accepted) ||
                accepted < 0 || accepted > frames)
            {
                DebugFailure($"invalid accepted frame count: {text.Trim()}");
                return -1;
            }
            return accepted;
        }
        catch (SocketException exception)
        {
            DebugFailure(exception.Message);
            return -1;
        }
        catch (IOException exception)
        {
            DebugFailure(exception.Message);
            return -1;
        }
    }

    private bool TryClearBuffer()
    {
        try
        {
            using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            {
                SendTimeout = 1_000,
                ReceiveTimeout = 1_000,
            };
            socket.Connect(new UnixDomainSocketEndPoint(_socketPath));
            socket.Send("CLEAR\n"u8, SocketFlags.None);
            byte[] response = new byte[64];
            int count = socket.Receive(response, SocketFlags.None);
            string text = Encoding.ASCII.GetString(response, 0, count);
            return text.StartsWith("OK CLEAR", StringComparison.Ordinal) ||
                text.StartsWith("ERR UNKNOWN", StringComparison.Ordinal);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void DebugFailure(string message)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_MUSIC_DEBUG"), "1", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Stormakt music debug: {message}");
        }
    }

    private static float[] LoadPcm16StereoWav(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
        {
            throw new InvalidDataException("soundtrack is not a RIFF file");
        }

        _ = reader.ReadUInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
        {
            throw new InvalidDataException("soundtrack is not a WAVE file");
        }

        ushort format = 0;
        ushort channels = 0;
        uint sampleRate = 0;
        ushort bitsPerSample = 0;
        byte[]? pcm = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int chunkSize = checked((int)reader.ReadUInt32());
            long nextChunk = stream.Position + chunkSize + (chunkSize & 1);
            if (nextChunk > stream.Length)
            {
                throw new InvalidDataException("soundtrack contains a truncated WAV chunk");
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                pcm = reader.ReadBytes(chunkSize);
            }

            stream.Position = nextChunk;
        }

        if (format != 1 || channels != Channels || sampleRate != SampleRate || bitsPerSample != 16 || pcm is null || pcm.Length % (Channels * sizeof(short)) != 0)
        {
            throw new InvalidDataException("soundtrack must be 48 kHz stereo PCM16 WAV");
        }

        float[] samples = new float[pcm.Length / sizeof(short)];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(index * sizeof(short))) / 32768.0f;
        }
        return samples;
    }
}
