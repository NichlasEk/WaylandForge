using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

internal sealed class StormaktMusicLoop : IDisposable
{
    private const int SampleRate = 48_000;
    private const int Channels = 2;
    private const int PacketFrames = 2_048;
    private const int TrackCrossfadeFrames = SampleRate / 2;
    private const int HeaderBytes = 24;
    private const string DefaultSocketPath = "/tmp/waylandforge-audio.sock";

    private float[] _samples;
    private readonly float[]? _menuSamples;
    private readonly float[] _combatSamples;
    private readonly float[]? _skanskaSamples;
    private readonly float[]? _bossSamples;
    private readonly Dictionary<StormaktSound, LoadedEffect> _effects;
    private readonly Dictionary<StormaktVoice, LoadedEffect> _voices;
    private readonly ConcurrentQueue<StormaktSound> _pendingEffects = new();
    private readonly ConcurrentQueue<StormaktVoice> _pendingVoices = new();
    private readonly ConcurrentQueue<StormaktMusicTrack> _pendingTracks = new();
    private readonly ConcurrentQueue<int> _pendingMusicDucks = new();
    private readonly List<ActiveEffect> _activeEffects = [];
    private ActiveEffect? _activeVoice;
    private int _musicDuckFrames;
    private int _totalFrames;
    private int _crossfadeFrames;
    private float[]? _transitionSamples;
    private int _transitionFrame;
    private StormaktMusicTrack _currentTrack = StormaktMusicTrack.Combat;
    private StormaktMusicTrack? _transitionTrack;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private volatile bool _connectedOnce;
    private volatile bool _paused;

    private StormaktMusicLoop(
        float[] samples,
        float[]? menuSamples,
        float[]? skanskaSamples,
        float[]? bossSamples,
        Dictionary<StormaktSound, LoadedEffect> effects,
        Dictionary<StormaktVoice, LoadedEffect> voices,
        string socketPath)
    {
        _samples = samples;
        _menuSamples = menuSamples;
        _combatSamples = samples;
        _skanskaSamples = skanskaSamples;
        _bossSamples = bossSamples;
        _effects = effects;
        _voices = voices;
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
                string musicDirectory = Path.Combine(Path.GetDirectoryName(path)!, "music");
                string ironMarchPath = Path.Combine(musicDirectory, "tre-kronors-jarnmarsch-loop-v1.wav");
                string originalMenuPath = Path.Combine(musicDirectory, "marsch-mot-kopenhamn-v1.wav");
                string menuPath = File.Exists(ironMarchPath) ? ironMarchPath : originalMenuPath;
                float[]? menuSamples = File.Exists(menuPath) ? LoadPcm16StereoWav(menuPath) : null;
                string skanskaPath = Path.Combine(musicDirectory, "skanska-skuggor-loop-v1.wav");
                float[]? skanskaSamples = File.Exists(skanskaPath) ? LoadPcm16StereoWav(skanskaPath) : null;
                string loopedBossPath = Path.Combine(musicDirectory, "kronans-sista-salva-loop-v2.wav");
                string originalBossPath = Path.Combine(musicDirectory, "kronans-sista-salva-v1.wav");
                string bossPath = File.Exists(loopedBossPath) ? loopedBossPath : originalBossPath;
                float[]? bossSamples = File.Exists(bossPath) ? LoadPcm16StereoWav(bossPath) : null;
                Dictionary<StormaktSound, LoadedEffect> effects = LoadEffects(path);
                Dictionary<StormaktVoice, LoadedEffect> voices = LoadVoices(path);
                string socketPath = Environment.GetEnvironmentVariable("WAYLANDFORGE_AUDIO_SOCKET") ?? DefaultSocketPath;
                string menuDescription = menuSamples is null ? "missing" : $"ready ({menuSamples.Length / Channels / SampleRate}s)";
                string skanskaDescription = skanskaSamples is null ? "missing" : $"ready ({skanskaSamples.Length / Channels / SampleRate}s)";
                string bossDescription = bossSamples is null ? "missing" : $"ready ({bossSamples.Length / Channels / SampleRate}s)";
                Console.Error.WriteLine($"Stormakt audio: loaded {Path.GetFileName(path)} ({samples.Length / Channels / SampleRate}s), " +
                    $"menu march={menuDescription}, Skanska score={skanskaDescription}, boss score={bossDescription}, " +
                    $"{effects.Count} effects and {voices.Count} radio voices.");
                return new StormaktMusicLoop(samples, menuSamples, skanskaSamples, bossSamples, effects, voices, socketPath);
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

    public void Trigger(StormaktSound sound) => _pendingEffects.Enqueue(sound);

    public void TriggerVoice(StormaktVoice voice) => _pendingVoices.Enqueue(voice);

    public void SwitchMusic(StormaktMusicTrack track) => _pendingTracks.Enqueue(track);

    public void DuckMusic(int milliseconds) => _pendingMusicDucks.Enqueue(Math.Max(0, milliseconds) * SampleRate / 1_000);

    public void SetPaused(bool paused) => _paused = paused;

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
            if (bufferedSeconds > 0.12)
            {
                _stop.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(12));
                continue;
            }

            FillPacket(packet, frameIndex, PacketFrames);
            int accepted = TrySendPacket(packet, PacketFrames);
            if (accepted >= 0)
            {
                if (!_paused)
                {
                    frameIndex = AdvanceFrameIndex(frameIndex, accepted);
                    AdvanceTrackTransition(ref frameIndex, accepted);
                }
                acceptedFrames += accepted;
                if (!announcedConnection)
                {
                    Console.Error.WriteLine($"Stormakt audio: streaming to {_socketPath}.");
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
        if (_paused)
        {
            payload[..(frames * Channels * sizeof(float))].Clear();
            return;
        }
        while (_pendingEffects.TryDequeue(out StormaktSound sound))
        {
            if (_effects.TryGetValue(sound, out LoadedEffect? effect) && effect is not null)
            {
                if (_activeEffects.Count >= 32)
                {
                    _activeEffects.RemoveAt(0);
                }
                _activeEffects.Add(new ActiveEffect(effect));
            }
        }
        while (_pendingVoices.TryDequeue(out StormaktVoice voice))
        {
            if (_voices.TryGetValue(voice, out LoadedEffect? effect) && effect is not null)
            {
                _activeVoice = new ActiveEffect(effect);
            }
        }
        while (_pendingTracks.TryDequeue(out StormaktMusicTrack track))
        {
            float[]? requestedSamples = track switch
            {
                StormaktMusicTrack.Menu => _menuSamples,
                StormaktMusicTrack.Combat => _combatSamples,
                StormaktMusicTrack.Skanska => _skanskaSamples,
                StormaktMusicTrack.Boss => _bossSamples,
                _ => null,
            };
            if (requestedSamples is not null && _currentTrack != track && _transitionTrack != track)
            {
                _transitionSamples = requestedSamples;
                _transitionFrame = 0;
                _transitionTrack = track;
                Console.Error.WriteLine($"Stormakt audio: crossfading to {track.ToString().ToLowerInvariant()} score.");
            }
        }
        while (_pendingMusicDucks.TryDequeue(out int duckFrames))
        {
            _musicDuckFrames = Math.Max(_musicDuckFrames, duckFrames);
            Console.Error.WriteLine($"Stormakt audio: ducking music for {duckFrames * 1_000 / SampleRate}ms.");
        }

        for (int outputFrame = 0; outputFrame < frames; outputFrame++)
        {
            ReadLoopSample(_samples, frameIndex, _totalFrames, _crossfadeFrames, out float left, out float right);
            if (_transitionSamples is not null)
            {
                int incomingTotalFrames = _transitionSamples.Length / Channels;
                int incomingCrossfadeFrames = Math.Min(SampleRate / 2, incomingTotalFrames / 8);
                int incomingFrame = NormalizeFrame(_transitionFrame + outputFrame, incomingTotalFrames, incomingCrossfadeFrames);
                ReadLoopSample(_transitionSamples, incomingFrame, incomingTotalFrames, incomingCrossfadeFrames,
                    out float incomingLeft, out float incomingRight);
                float incomingWeight = Math.Min(1.0f, (_transitionFrame + outputFrame + 1) / (float)TrackCrossfadeFrames);
                float currentWeight = 1.0f - incomingWeight;
                left = left * currentWeight + incomingLeft * incomingWeight;
                right = right * currentWeight + incomingRight * incomingWeight;
            }

            float musicGain = _activeVoice is not null ? 0.34f : _musicDuckFrames > 0 ? 0.22f : 0.68f;
            left *= musicGain;
            right *= musicGain;
            _musicDuckFrames = Math.Max(0, _musicDuckFrames - 1);
            for (int voiceIndex = _activeEffects.Count - 1; voiceIndex >= 0; voiceIndex--)
            {
                ActiveEffect voice = _activeEffects[voiceIndex];
                left += voice.Effect.Samples[voice.Frame * Channels] * voice.Effect.Gain;
                right += voice.Effect.Samples[(voice.Frame * Channels) + 1] * voice.Effect.Gain;
                voice.Frame++;
                if (voice.Frame >= voice.Effect.Samples.Length / Channels)
                {
                    _activeEffects.RemoveAt(voiceIndex);
                }
            }
            if (_activeVoice is not null)
            {
                left += _activeVoice.Effect.Samples[_activeVoice.Frame * Channels] * _activeVoice.Effect.Gain;
                right += _activeVoice.Effect.Samples[(_activeVoice.Frame * Channels) + 1] * _activeVoice.Effect.Gain;
                _activeVoice.Frame++;
                if (_activeVoice.Frame >= _activeVoice.Effect.Samples.Length / Channels)
                {
                    _activeVoice = null;
                }
            }
            left = Math.Clamp(left, -0.98f, 0.98f);
            right = Math.Clamp(right, -0.98f, 0.98f);

            int byteOffset = outputFrame * Channels * sizeof(float);
            BinaryPrimitives.WriteSingleLittleEndian(payload[byteOffset..], left);
            BinaryPrimitives.WriteSingleLittleEndian(payload[(byteOffset + sizeof(float))..], right);

            frameIndex = AdvanceFrameIndex(frameIndex, 1);
        }
    }

    private void AdvanceTrackTransition(ref int frameIndex, int acceptedFrames)
    {
        if (_transitionSamples is null || _transitionTrack is null)
        {
            return;
        }
        _transitionFrame += acceptedFrames;
        if (_transitionFrame < TrackCrossfadeFrames)
        {
            return;
        }

        _samples = _transitionSamples;
        _totalFrames = _samples.Length / Channels;
        _crossfadeFrames = Math.Min(SampleRate / 2, _totalFrames / 8);
        frameIndex = NormalizeFrame(_transitionFrame, _totalFrames, _crossfadeFrames);
        _currentTrack = _transitionTrack.Value;
        Console.Error.WriteLine($"Stormakt audio: {_currentTrack.ToString().ToLowerInvariant()} score active.");
        _transitionSamples = null;
        _transitionTrack = null;
        _transitionFrame = 0;
    }

    private static void ReadLoopSample(
        float[] samples,
        int frameIndex,
        int totalFrames,
        int crossfadeFrames,
        out float left,
        out float right)
    {
        left = samples[frameIndex * Channels];
        right = samples[(frameIndex * Channels) + 1];
        if (frameIndex < totalFrames - crossfadeFrames)
        {
            return;
        }
        int headFrame = frameIndex - (totalFrames - crossfadeFrames);
        float headWeight = headFrame / (float)crossfadeFrames;
        float tailWeight = 1.0f - headWeight;
        left = left * tailWeight + samples[headFrame * Channels] * headWeight;
        right = right * tailWeight + samples[(headFrame * Channels) + 1] * headWeight;
    }

    private static int NormalizeFrame(int frameIndex, int totalFrames, int crossfadeFrames)
    {
        while (frameIndex >= totalFrames)
        {
            frameIndex = crossfadeFrames + (frameIndex - totalFrames);
        }
        return frameIndex;
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
            Console.Error.WriteLine($"Stormakt audio debug: {message}");
        }
    }

    private static Dictionary<StormaktSound, LoadedEffect> LoadEffects(string soundtrackPath)
    {
        string soundtrackDirectory = Path.GetDirectoryName(soundtrackPath)!;
        string parentDirectory = Path.GetDirectoryName(soundtrackDirectory) ?? soundtrackDirectory;
        string[] directories =
        [
            Path.Combine(soundtrackDirectory, "sfx"),
            Path.Combine(parentDirectory, "sfx"),
            Path.Combine(Environment.CurrentDirectory, "assets", "stormakt3020", "sfx"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "stormakt3020", "sfx")),
        ];
        string? sfxDirectory = directories.FirstOrDefault(Directory.Exists);
        if (sfxDirectory is null)
        {
            return [];
        }

        (StormaktSound Sound, string File, float Gain)[] entries =
        [
            (StormaktSound.TwinCannon, "twin-cannon.wav", 0.30f),
            (StormaktSound.Broadside, "broadside.wav", 0.52f),
            (StormaktSound.EnemyExplosion, "enemy-explosion.wav", 0.48f),
            (StormaktSound.HullHit, "hull-hit.wav", 0.58f),
            (StormaktSound.Deploy, "deploy-chime.wav", 0.42f),
        ];

        Dictionary<StormaktSound, LoadedEffect> effects = [];
        foreach ((StormaktSound sound, string file, float gain) in entries)
        {
            string path = Path.Combine(sfxDirectory, file);
            if (File.Exists(path))
            {
                effects[sound] = new LoadedEffect(LoadPcm16StereoWav(path), gain);
            }
        }
        return effects;
    }

    private static Dictionary<StormaktVoice, LoadedEffect> LoadVoices(string soundtrackPath)
    {
        string soundtrackDirectory = Path.GetDirectoryName(soundtrackPath)!;
        string parentDirectory = Path.GetDirectoryName(soundtrackDirectory) ?? soundtrackDirectory;
        string[] directories =
        [
            Path.Combine(soundtrackDirectory, "radio", "voices"),
            Path.Combine(parentDirectory, "radio", "voices"),
            Path.Combine(Environment.CurrentDirectory, "assets", "stormakt3020", "radio", "voices"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "stormakt3020", "radio", "voices")),
        ];
        string? voiceDirectory = directories.FirstOrDefault(Directory.Exists);
        if (voiceDirectory is null)
        {
            return [];
        }

        (StormaktVoice Voice, string File, float Gain)[] entries =
        [
            (StormaktVoice.EbbaSkanskaSignal, "ebba-skanska-signal-sv-radio.wav", 0.72f),
            (StormaktVoice.SorenSvartaSkogen, "soren-svarta-skogen-sv-radio.wav", 0.74f),
            (StormaktVoice.EbbaIdentifierarSoren, "ebba-identifierar-soren-sv-radio.wav", 0.72f),
            (StormaktVoice.SorenFogdekonvoj, "soren-fogdekonvoj-sv-radio.wav", 0.74f),
            (StormaktVoice.EbbaStoraBalt, "ebba-stora-balt-sv-radio.wav", 0.72f),
            (StormaktVoice.RasmusLaggBi, "rasmus-lagg-bi-da-radio.wav", 0.74f),
            (StormaktVoice.ChristianBrutetSegl, "christian-brutet-segl-da-radio.wav", 0.98f),
            (StormaktVoice.RasmusKronensTiende, "rasmus-kronens-tiende-da-radio.wav", 0.74f),
            (StormaktVoice.EbbaSvararChristian, "ebba-svarar-christian-sv-radio.wav", 0.72f),
            (StormaktVoice.ChristianHotMotKarl, "christian-hot-mot-karl-da-radio.wav", 0.98f),
            (StormaktVoice.ChristianForSatan, "christian-for-satan-da-radio.wav", 0.98f),
            (StormaktVoice.RasmusBossFornaekter, "rasmus-boss-fornaekter-da-radio.wav", 0.82f),
            (StormaktVoice.RasmusBossPanik, "rasmus-boss-panik-da-radio.wav", 0.88f),
            (StormaktVoice.RasmusBossUndergang, "rasmus-boss-undergang-da-radio.wav", 0.96f),
        ];
        Dictionary<StormaktVoice, LoadedEffect> voices = [];
        foreach ((StormaktVoice voice, string file, float gain) in entries)
        {
            string path = Path.Combine(voiceDirectory, file);
            if (File.Exists(path))
            {
                voices[voice] = new LoadedEffect(LoadPcm16StereoWav(path), gain);
            }
        }
        return voices;
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

    private sealed class ActiveEffect(LoadedEffect effect)
    {
        public LoadedEffect Effect { get; } = effect;
        public int Frame { get; set; }
    }

    private sealed record LoadedEffect(float[] Samples, float Gain);
}

internal enum StormaktSound
{
    TwinCannon,
    Broadside,
    EnemyExplosion,
    HullHit,
    Deploy,
}

internal enum StormaktVoice
{
    EbbaSkanskaSignal,
    SorenSvartaSkogen,
    EbbaIdentifierarSoren,
    SorenFogdekonvoj,
    EbbaStoraBalt,
    RasmusLaggBi,
    ChristianBrutetSegl,
    RasmusKronensTiende,
    EbbaSvararChristian,
    ChristianHotMotKarl,
    ChristianForSatan,
    RasmusBossFornaekter,
    RasmusBossPanik,
    RasmusBossUndergang,
}

internal enum StormaktMusicTrack
{
    Menu,
    Combat,
    Skanska,
    Boss,
}
