using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;

bool legacyResolution = string.Equals(
    Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_LEGACY_320"), "1", StringComparison.Ordinal);
int Width = legacyResolution ? 320 : 400;
int Height = legacyResolution ? 224 : 280;
const uint FrameMagic = 0x58454657; // WFEX
const byte StepCommand = (byte)'S';

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
using var audio = StormaktMusicLoop.TryStartDefault();
var game = new StormaktGame(Width, Height, SpritePack.LoadDefault(), audio);
audio?.Trigger(StormaktSound.Deploy);
var command = new byte[5];
var header = new byte[32];
var frame = new uint[Width * Height];
ulong frameIndex = 0;

while (ReadExact(input, command))
{
    if (command[0] != StepCommand)
    {
        break;
    }

    uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
    game.Step(buttons);
    game.Render(frame, frameIndex);

    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), FrameMagic);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), Width);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), Height);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12), Width);
    BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16), frameIndex);
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), frame.Length * sizeof(uint));
    BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), 0);
    output.Write(header);
    output.Write(MemoryMarshal.AsBytes(frame.AsSpan()));
    output.Flush();
    frameIndex++;
}

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

internal sealed class StormaktGame
{
    private static readonly string[] CampaignNames =
    [
        "STORA BÄLT",
        "SKÅNSKA SKUGGOR",
        "ÖRESUNDS JÄRNKRONA",
        "TIONDE VÄRLDEN",
        "SNAPPHANENS ED",
        "KÖPENHAMNS RING",
    ];
    private const int FlythroughFrames = 60 * 60;
    private const int BossArrivalFrame = 3_300;
    private const int BossPhaseOneHealth = 450;
    private const int BossPhaseTwoThreshold = 293;
    private const int BossPhaseThreeThreshold = 113;
    private const int BossCannonOffset = 54;
    private const int BossDockOffset = 68;
    private const int GlimmingeMaxHealth = 720;
    private const int GlimmingePhaseTwoHealth = 420;
    private const int GlimmingeBurningHealth = 210;
    private const uint Up = 1u << 1;
    private const uint Down = 1u << 2;
    private const uint Left = 1u << 3;
    private const uint Right = 1u << 4;
    private const uint Start = 1u << 5;
    private const uint Fire = 1u << 6;
    private const uint AltFire = 1u << 7;
    private const uint Slow = 1u << 8;

    private readonly int _width;
    private readonly int _height;
    private readonly bool _invincibleTestMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_INVINCIBLE"), "1", StringComparison.Ordinal);
    private readonly bool _developerMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DEVELOPER_MODE"), "1", StringComparison.Ordinal);
    private readonly SpritePack? _sprites;
    private readonly StormaktMusicLoop? _audio;
    private Random _random = new(3020);
    private readonly List<Shot> _shots = [];
    private readonly List<Enemy> _enemies = [];
    private readonly List<EnemyShot> _enemyShots = [];
    private readonly List<GroundTarget> _groundTargets = [];
    private readonly List<AnchorHazard> _anchorHazards = [];
    private readonly List<CrystalSpear> _crystalSpears = [];
    private readonly Star[] _stars;
    private static readonly RadioCard[] RadioCards =
    [
        new(180, 360, false, "EBBA GRIP", "HÅLL KURSEN", "BÄLTET STÅR", StormaktVoice.EbbaStoraBalt, "portrait_ebba"),
        new(900, 300, true, "RASMUS", "SVENSKE FARTØJ", "LÆG BI NU", StormaktVoice.RasmusLaggBi, "portrait_rasmus"),
        new(1_380, 420, true, "KUNG CHRISTIAN", "BRUDT SEGL", "KRONENS FLÅDE", StormaktVoice.ChristianBrutetSegl, "portrait_christian"),
        new(1_800, 330, false, "EBBA GRIP", "SIGILLET BRUTET", "FLOTTAN NÄST", StormaktVoice.EbbaSvararChristian, "portrait_ebba"),
        new(2_220, 300, true, "KUNG CHRISTIAN", "DU TRUER MIG", "BLÅFRAKKE?", StormaktVoice.ChristianHotMotKarl, "portrait_christian"),
        new(2_700, 330, true, "KUNG CHRISTIAN", "KÆMP SOM KONGE", "FOR SATAN!", StormaktVoice.ChristianForSatan, "portrait_christian"),
        new(BossArrivalFrame, 300, true, "FOGDE RASMUS", "KRONENS TIENDE", "TAGER ALT", StormaktVoice.RasmusKronensTiende, "portrait_rasmus"),
    ];
    private static readonly RadioCard[] SkanskaRadioCards =
    [
        new(90, 330, false, "EBBA GRIP", "OKÄND SIGNAL", "HÅLL KURSEN", StormaktVoice.EbbaSkanskaSignal, "portrait_ebba"),
        new(900, 360, true, "SÖREN SVARTKRUT", "NI FÄRDAS I", "VÅR SVARTA SKOG", StormaktVoice.SorenSvartaSkogen, "portrait_soren", true),
        new(1_500, 330, false, "EBBA GRIP", "SÖREN SVARTKRUT", "KAPARE OCH VÄG", StormaktVoice.EbbaIdentifierarSoren, "portrait_ebba"),
        new(2_280, 300, true, "SÖREN SVARTKRUT", "FOGDEKONVOJ", "FÖRÖVER", StormaktVoice.SorenFogdekonvoj, "portrait_soren", true),
        new(3_600, 480, true, "BIRGITTE BILLE", "JEG ER BIRGITTE", "JERN BØJER IKKE", StormaktVoice.BirgitteGlimmingeIntro, "portrait_birgitte"),
    ];
    private static readonly RadioCard BirgittePhaseTwoRadio =
        new(0, 420, true, "BIRGITTE BILLE", "FOLD BORENE UD!", "MAL HAM TIL STØV", StormaktVoice.BirgitteGlimmingeBor, "portrait_birgitte");
    private static readonly RadioCard BirgitteDeathRadio =
        new(0, 330, true, "BIRGITTE BILLE", "FALDER IKKE!", "DANMARK REJSER", StormaktVoice.BirgitteGlimmingeFalder, "portrait_birgitte");
    private static readonly RadioCard RasmusPhaseTwoRadio =
        new(0, 330, true, "FOGDE RASMUS", "HVAD LAVER I?", "STOP SKYDNINGEN!", StormaktVoice.RasmusBossFornaekter, "portrait_rasmus");
    private static readonly RadioCard RasmusPhaseThreeRadio =
        new(0, 330, true, "FOGDE RASMUS", "MINE KANONER!", "HOLD LINJEN!", StormaktVoice.RasmusBossPanik, "portrait_rasmus");
    private static readonly RadioCard RasmusDeathRadio =
        new(0, 300, true, "FOGDE RASMUS", "SVENSK FRÆKHED!", "NEEEEEJ!", StormaktVoice.RasmusBossUndergang, "portrait_rasmus");
    private static readonly RadioCard RasmusDeathOathRadio =
        new(0, 300, true, "FOGDE RASMUS", "DENNE GANG KARL", "HVER GANG!", StormaktVoice.RasmusBossEfterspel, "portrait_rasmus");
    private static readonly EnemyWave[] EnemyWaves =
    [
        new(240, 720, 180, 0, 2),
        new(720, 1_500, 150, 1, 3),
        new(1_500, 2_280, 120, 0, 3),
        new(2_280, 3_060, 150, 2, 2),
        new(3_060, 3_240, 105, 1, 3),
    ];
    private static readonly EnemyWave[] SkanskaEnemyWaves =
    [
        new(240, 1_080, 150, 4, 2),
        new(1_080, 2_220, 120, 4, 3),
        new(2_220, 3_420, 105, 5, 3),
    ];
    private int _shipX;
    private int _shipY;
    private int _cooldown;
    private int _altCooldown;
    private int _score;
    private int _lives = 3;
    private int _heat;
    private int _missionFrame;
    private int _invulnerabilityFrames;
    private BossState? _boss;
    private SorenRivalState? _sorenRival;
    private GlimmingeState? _glimminge;
    private bool _stageClear;
    private int _stageClearAge;
    private uint _previousButtons;
    private bool _gameOver;
    private bool _paused;
    private bool _inLevelSelect;
    private bool _inLevelPreview;
    private int _previewLevel;
    private int _levelId;
    private int _levelSelection;
    private int _lockedLevelNoticeFrames;
    private RadioCard? _bossRadioCard;
    private int _bossRadioAge;

    public StormaktGame(int width, int height, SpritePack? sprites, StormaktMusicLoop? audio)
    {
        _width = width;
        _height = height;
        _stars = new Star[width <= 320 ? 92 : 128];
        _sprites = sprites;
        _audio = audio;
        Reset();
        _inLevelSelect = true;
        _audio?.SwitchMusic(StormaktMusicTrack.Menu);
    }

    public void Step(uint buttons)
    {
        if (_inLevelPreview)
        {
            StepLevelPreview(buttons);
            return;
        }
        if (_inLevelSelect)
        {
            StepLevelSelect(buttons);
            return;
        }
        if (_gameOver)
        {
            if (Pressed(buttons, Start))
            {
                Reset();
                _audio?.Trigger(StormaktSound.Deploy);
            }
            _previousButtons = buttons;
            return;
        }
        if (_stageClear)
        {
            if (Pressed(buttons, Start))
            {
                Reset();
                _audio?.Trigger(StormaktSound.Deploy);
            }
            else
            {
                _stageClearAge++;
                StepStars();
            }
            _previousButtons = buttons;
            return;
        }

        if (Pressed(buttons, Start))
        {
            _paused = !_paused;
            _audio?.SetPaused(_paused);
            _previousButtons = buttons;
            return;
        }
        if (_paused)
        {
            _previousButtons = buttons;
            return;
        }

        StepRadio();

        int speed = (buttons & Slow) != 0 ? (_width <= 320 ? 2 : 3) : (_width <= 320 ? 4 : 5);
        if ((buttons & Left) != 0) _shipX -= speed;
        if ((buttons & Right) != 0) _shipX += speed;
        if ((buttons & Up) != 0) _shipY -= speed;
        if ((buttons & Down) != 0) _shipY += speed;
        _shipX = Math.Clamp(_shipX, 22, _width - 22);
        _shipY = Math.Clamp(_shipY, 48, _height - 18);

        _cooldown = Math.Max(0, _cooldown - 1);
        _altCooldown = Math.Max(0, _altCooldown - 1);
        _heat = Math.Max(0, _heat - 1);
        _invulnerabilityFrames = Math.Max(0, _invulnerabilityFrames - 1);
        if ((buttons & Fire) != 0 && _cooldown == 0)
        {
            _shots.Add(new Shot(_shipX - 4, _shipY - 12, 0, -7, 0xffffd66b, 3));
            _shots.Add(new Shot(_shipX + 4, _shipY - 12, 0, -7, 0xffffd66b, 3));
            _cooldown = 6;
            _heat = Math.Min(120, _heat + 7);
            _audio?.Trigger(StormaktSound.TwinCannon);
        }
        if ((buttons & AltFire) != 0 && _altCooldown == 0)
        {
            _shots.Add(new Shot(_shipX - 11, _shipY - 5, -2, -5, 0xff7fc7ff, 5));
            _shots.Add(new Shot(_shipX + 11, _shipY - 5, 2, -5, 0xff7fc7ff, 5));
            _altCooldown = 18;
            _heat = Math.Min(120, _heat + 15);
            _audio?.Trigger(StormaktSound.Broadside);
        }

        StepShots();
        StepEnemyShots();
        StepEnemies();
        StepGroundTargets();
        StepSorenRival();
        StepGlimminge();
        StepBoss();
        StepAnchorHazards();
        StepCrystalSpears();
        SpawnEnemies();
        SpawnGroundEncounters();
        SpawnBoss();
        StepLevelTimeline();
        StepStars();
        _previousButtons = buttons;
    }

    public void Render(uint[] frame, ulong frameIndex)
    {
        Clear(frame, 0xff061018);
        DrawSky(frame);
        DrawNebula(frame);
        if (_levelId != 1)
        {
            DrawStars(frame);
        }
        if (_inLevelSelect)
        {
            DrawLevelSelect(frame);
            return;
        }
        if (_inLevelPreview)
        {
            DrawLevelPreview(frame);
            return;
        }
        DrawBeltRuins(frame);
        DrawSorenBackgroundPass(frame);
        DrawGroundTargets(frame);
        DrawSorenRival(frame);
        DrawGlimminge(frame);
        DrawBoss(frame);
        DrawShots(frame);
        DrawEnemyShots(frame);
        DrawAnchorHazards(frame);
        DrawCrystalSpears(frame);
        DrawEnemies(frame);
        DrawShip(frame);
        DrawBorder(frame);
        DrawHud(frame);
        DrawBossHud(frame);
        DrawBossIntroduction(frame);
        DrawMissionTitle(frame);
        DrawRadio(frame);
        DrawStageClear(frame);
        DrawPause(frame);
        if (_gameOver)
        {
            int panelX = (_width - 168) / 2;
            int panelY = (_height - 42) / 2;
            DrawRect(frame, panelX, panelY, 168, 42, 0xdd090b10);
            DrawText(frame, panelX + 16, panelY + 10, "FLOTTAN FÖLL", 0xffff6b7f);
            DrawText(frame, panelX + 22, panelY + 26, "START ÅTERKALLAR", 0xffffd66b);
        }
    }

    private void Reset()
    {
        _shots.Clear();
        _enemies.Clear();
        _enemyShots.Clear();
        _groundTargets.Clear();
        _anchorHazards.Clear();
        _crystalSpears.Clear();
        _random = new Random(_levelId == 1 ? 3202 : 3020);
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i] = new Star(_random.Next(_width), _random.Next(_height), 1 + _random.Next(3), _random.Next(50, 180));
        }
        _shipX = _width / 2;
        _shipY = _height - 36;
        _cooldown = 0;
        _altCooldown = 0;
        _score = 0;
        _lives = 3;
        _heat = 0;
        _missionFrame = 0;
        _invulnerabilityFrames = 0;
        _boss = null;
        _sorenRival = null;
        _glimminge = null;
        _stageClear = false;
        _stageClearAge = 0;
        _previousButtons = 0;
        _gameOver = false;
        _paused = false;
        _bossRadioCard = null;
        _bossRadioAge = 0;
        _audio?.SetPaused(false);
        _audio?.SwitchMusic(_levelId == 1 ? StormaktMusicTrack.Skanska : StormaktMusicTrack.Combat);
    }

    private void StartLevel(int levelId)
    {
        _levelId = levelId;
        Reset();
        _inLevelSelect = false;
        _inLevelPreview = false;
        _audio?.Trigger(StormaktSound.Deploy);
    }

    private bool Pressed(uint buttons, uint button) => (buttons & button) != 0 && (_previousButtons & button) == 0;

    private void StepLevelSelect(uint buttons)
    {
        if (Pressed(buttons, Up))
        {
            _levelSelection = (_levelSelection + CampaignNames.Length - 1) % CampaignNames.Length;
            _audio?.Trigger(StormaktSound.Deploy);
        }
        if (Pressed(buttons, Down))
        {
            _levelSelection = (_levelSelection + 1) % CampaignNames.Length;
            _audio?.Trigger(StormaktSound.Deploy);
        }
        if (Pressed(buttons, Start))
        {
            if (_levelSelection == 0 || (_levelSelection == 1 && _developerMode))
            {
                StartLevel(_levelSelection);
            }
            else
            {
                if (_developerMode)
                {
                    _previewLevel = _levelSelection;
                    _inLevelSelect = false;
                    _inLevelPreview = true;
                    _audio?.Trigger(StormaktSound.Deploy);
                }
                else
                {
                    _lockedLevelNoticeFrames = 90;
                }
            }
        }
        _lockedLevelNoticeFrames = Math.Max(0, _lockedLevelNoticeFrames - 1);
        _previousButtons = buttons;
    }

    private void StepLevelPreview(uint buttons)
    {
        if (Pressed(buttons, Start) || Pressed(buttons, Fire))
        {
            _inLevelPreview = false;
            _inLevelSelect = true;
            _audio?.SwitchMusic(StormaktMusicTrack.Menu);
            _audio?.Trigger(StormaktSound.Deploy);
        }
        _previousButtons = buttons;
    }

    private void StepRadio()
    {
        if (_bossRadioCard is RadioCard bossCard)
        {
            _bossRadioAge++;
            if (_bossRadioAge >= bossCard.DurationFrames)
            {
                _bossRadioCard = null;
                _bossRadioAge = 0;
            }
        }
        ReadOnlySpan<RadioCard> cards = _levelId == 1 ? SkanskaRadioCards : RadioCards;
        foreach (RadioCard card in cards)
        {
            if (_missionFrame == card.StartFrame)
            {
                if (card.Voice is StormaktVoice voice)
                {
                    _audio?.TriggerVoice(voice);
                }
            }
        }
        _missionFrame++;
    }

    private void ActivateBossRadio(RadioCard card)
    {
        _bossRadioCard = card;
        _bossRadioAge = 0;
        if (card.Voice is StormaktVoice voice)
        {
            _audio?.TriggerVoice(voice);
        }
    }

    private void StepLevelTimeline()
    {
        if (_levelId == 1 && _missionFrame == 2_700 && _sorenRival is null)
        {
            _sorenRival = new SorenRivalState
            {
                X = _width / 2.0,
                Y = 68,
                Health = 140,
            };
            _enemies.Clear();
            _audio?.Trigger(StormaktSound.Deploy);
        }
    }

    private void StepShots()
    {
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            Shot shot = _shots[i];
            shot.X += shot.Vx;
            shot.Y += shot.Vy;
            if (shot.Y < -8 || shot.X < -8 || shot.X >= _width + 8)
            {
                _shots.RemoveAt(i);
            }
            else
            {
                _shots[i] = shot;
            }
        }
    }

    private void StepEnemyShots()
    {
        for (int index = _enemyShots.Count - 1; index >= 0; index--)
        {
            EnemyShot shot = _enemyShots[index];
            shot.X += shot.Vx;
            shot.Y += shot.Vy;
            if (shot.Y < -10 || shot.Y > _height + 10 || shot.X < -10 || shot.X > _width + 10)
            {
                _enemyShots.RemoveAt(index);
                continue;
            }
            if (Math.Abs(_shipX - shot.X) < 7 && Math.Abs(_shipY - shot.Y) < 7)
            {
                _enemyShots.RemoveAt(index);
                DamageShip();
                continue;
            }
            _enemyShots[index] = shot;
        }
    }

    private void StepGroundTargets()
    {
        for (int index = _groundTargets.Count - 1; index >= 0; index--)
        {
            GroundTarget target = _groundTargets[index];
            target.Y++;
            target.Age++;

            if (target.CollapseFrames > 0)
            {
                target.Y += 2;
                target.CollapseFrames--;
                if (target.CollapseFrames == 0 || target.Y > _height + 42)
                {
                    _groundTargets.RemoveAt(index);
                }
                else
                {
                    _groundTargets[index] = target;
                }
                continue;
            }

            if (target.Type == GroundTargetType.Turret && target.Enabled && target.Y > 24 && target.Y < _height - 50)
            {
                int cycle = target.Age % 180;
                if (cycle is 88 or 96 or 104)
                {
                    FireGroundShot(target.X, target.Y);
                }
            }

            bool destroyed = false;
            int halfWidth = target.Type == GroundTargetType.BridgeSpan ? 42 : 10;
            int halfHeight = target.Type == GroundTargetType.BridgeSpan ? 9 : 11;
            for (int shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex--)
            {
                Shot shot = _shots[shotIndex];
                if (Math.Abs(shot.X - target.X) <= halfWidth && Math.Abs(shot.Y - target.Y) <= halfHeight)
                {
                    target.Health -= shot.Power;
                    _shots.RemoveAt(shotIndex);
                    if (target.Health <= 0)
                    {
                        destroyed = true;
                    }
                    break;
                }
            }

            if (destroyed)
            {
                _score += target.Type switch
                {
                    GroundTargetType.Turret => 450,
                    GroundTargetType.EnergyNode => 250,
                    GroundTargetType.SignalBeacon => 300,
                    _ => 180,
                };
                if (target.Type == GroundTargetType.BridgeSpan)
                {
                    CollapseBridgeGroup(target.Group);
                    _audio?.Trigger(StormaktSound.Broadside);
                    continue;
                }
                if (target.Type == GroundTargetType.EnergyNode)
                {
                    DisableLinkedTurret(target.Group);
                }
                _groundTargets.RemoveAt(index);
                _audio?.Trigger(StormaktSound.EnemyExplosion);
                continue;
            }
            if (target.Y > _height + 42)
            {
                _groundTargets.RemoveAt(index);
                continue;
            }
            _groundTargets[index] = target;
        }
    }

    private void CollapseBridgeGroup(int group)
    {
        for (int index = 0; index < _groundTargets.Count; index++)
        {
            GroundTarget target = _groundTargets[index];
            if (target.Group == group)
            {
                _groundTargets[index] = target with { Enabled = false, CollapseFrames = 45 };
            }
        }
    }

    private void FireGroundShot(int x, int y)
    {
        double dx = _shipX - x;
        double dy = _shipY - y;
        double length = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        _enemyShots.Add(new EnemyShot(x, y + 7, dx / length * 2.15, dy / length * 2.15, 0));
        _audio?.Trigger(StormaktSound.TwinCannon);
    }

    private void DisableLinkedTurret(int group)
    {
        for (int index = 0; index < _groundTargets.Count; index++)
        {
            GroundTarget target = _groundTargets[index];
            if (target.Group == group && target.Type == GroundTargetType.Turret)
            {
                _groundTargets[index] = target with { Enabled = false };
            }
        }
    }

    private void DamageShip()
    {
        if (_invincibleTestMode)
        {
            _heat = Math.Max(_heat, 40);
            return;
        }
        if (_invulnerabilityFrames > 0)
        {
            return;
        }
        _lives--;
        _heat = 120;
        _invulnerabilityFrames = 90;
        _audio?.Trigger(StormaktSound.HullHit);
        if (_lives <= 0)
        {
            _gameOver = true;
        }
    }

    private void SpawnBoss()
    {
        if (_levelId == 1)
        {
            if (_missionFrame == 3_600 && _glimminge is null)
            {
                _glimminge = new GlimmingeState
                {
                    X = _width / 2.0,
                    Y = -72.0,
                    Health = GlimmingeMaxHealth,
                    Phase = 1,
                };
                _enemyShots.Clear();
                _audio?.SwitchMusic(StormaktMusicTrack.Boss);
                _audio?.Trigger(StormaktSound.Deploy);
            }
            return;
        }
        if (_levelId != 0)
        {
            return;
        }
        if (_missionFrame == BossArrivalFrame && _boss is null)
        {
            _boss = new BossState
            {
                X = _width / 2.0,
                Y = -58.0,
                Health = BossPhaseOneHealth,
                LeftCannonHealth = 70,
                RightCannonHealth = 70,
                Phase = 1,
            };
            _audio?.Trigger(StormaktSound.Deploy);
            _audio?.SwitchMusic(StormaktMusicTrack.Boss);
        }
    }

    private void StepSorenRival()
    {
        if (_sorenRival is not SorenRivalState rival)
        {
            return;
        }
        rival.Age++;
        if (rival.Interrupted)
        {
            rival.InterruptAge++;
            rival.X += 5.5;
            rival.Y -= 0.8;
            if (rival.InterruptAge >= 90 || rival.X > _width + 48)
            {
                _sorenRival = null;
            }
            return;
        }

        bool decoyPhase = rival.Age >= 330 || rival.Health <= 82;
        if (!decoyPhase)
        {
            int dashSide = (rival.Age / 90 & 1) == 0 ? 1 : -1;
            double targetX = _width / 2.0 + dashSide * (_width / 3.2);
            rival.X += Math.Clamp(targetX - rival.X, -5.2, 5.2);
            rival.Y = 67 + Math.Sin(rival.Age * 0.055) * 9.0;
            if (rival.Age % 72 == 48)
            {
                FireEnemyShot((int)rival.X, (int)rival.Y + 12, 3, 2.55);
            }
        }
        else
        {
            rival.X = _width / 2.0 + Math.Sin(rival.Age * 0.052) * (_width / 3.4);
            rival.Y = 70 + Math.Cos(rival.Age * 0.037) * 14.0;
            if (rival.Age % 54 == 32)
            {
                FireEnemyShot((int)rival.X - 8, (int)rival.Y + 10, 3, 2.35);
                FireEnemyShot((int)rival.X + 8, (int)rival.Y + 10, 3, 2.35);
            }
        }

        for (int shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex--)
        {
            Shot shot = _shots[shotIndex];
            if (Math.Abs(shot.X - rival.X) <= 22 && Math.Abs(shot.Y - rival.Y) <= 18)
            {
                rival.Health -= shot.Power;
                _shots.RemoveAt(shotIndex);
            }
        }
        if (rival.Health <= 49 || _missionFrame >= 3_420)
        {
            rival.Interrupted = true;
            rival.InterruptAge = 0;
            _enemyShots.Clear();
            _audio?.Trigger(StormaktSound.Broadside);
        }
    }

    private void StepGlimminge()
    {
        if (_glimminge is not GlimmingeState boss)
        {
            return;
        }
        boss.Age++;
        boss.PhaseAge++;
        if (boss.BurningTransitionAge is > 0 and < 60)
        {
            boss.BurningTransitionAge++;
        }
        if (boss.Phase == 3)
        {
            if (boss.PhaseAge is 1 or 35 or 70 or 110 or 155 or 210 or 270)
            {
                _audio?.Trigger(boss.PhaseAge >= 155 ? StormaktSound.Broadside : StormaktSound.EnemyExplosion);
            }
            if (boss.PhaseAge >= 300)
            {
                _glimminge = null;
                _stageClear = true;
                _stageClearAge = 0;
                _enemyShots.Clear();
                _crystalSpears.Clear();
            }
            return;
        }

        if (boss.Age <= 150)
        {
            boss.Y = Math.Min(64.0, boss.Y + 0.92);
            return;
        }
        double sweep = _width <= 320 ? 42.0 : 55.0;
        if (boss.Phase == 1)
        {
            boss.X = _width / 2.0 + Math.Sin((boss.Age - 150) * 0.011) * sweep;
        }
        else
        {
            double phaseTwoX = _width / 2.0 + Math.Sin(boss.PhaseAge * 0.018) * sweep;
            if (boss.PhaseAge < 60)
            {
                double t = boss.PhaseAge / 60.0;
                double eased = t * t * (3.0 - 2.0 * t);
                boss.X = boss.PhaseTransitionX + (phaseTwoX - boss.PhaseTransitionX) * eased;
            }
            else
            {
                boss.X = phaseTwoX;
            }
        }
        bool shieldBraced = boss.Phase == 1 && boss.PhaseAge % 84 is >= 24 and <= 58;

        for (int shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex--)
        {
            Shot shot = _shots[shotIndex];
            if (Math.Abs(shot.X - boss.X) <= 56 && Math.Abs(shot.Y - boss.Y) <= 32)
            {
                if (shieldBraced)
                {
                    _shots.RemoveAt(shotIndex);
                    continue;
                }
                int previousHealth = boss.Health;
                boss.Health -= shot.Power;
                _shots.RemoveAt(shotIndex);
                if (boss.Phase == 2 && previousHealth > GlimmingeBurningHealth && boss.Health <= GlimmingeBurningHealth)
                {
                    boss.BurningTransitionAge = 1;
                }
                if (boss.Phase == 1 && boss.Health <= GlimmingePhaseTwoHealth)
                {
                    boss.Health = GlimmingePhaseTwoHealth;
                    boss.Phase = 2;
                    boss.PhaseAge = 0;
                    boss.PhaseTransitionX = boss.X;
                    _enemyShots.Clear();
                    ActivateBossRadio(BirgittePhaseTwoRadio);
                    _audio?.Trigger(StormaktSound.Broadside);
                }
                else if (boss.Phase == 2 && boss.Health <= 0)
                {
                    boss.Health = 0;
                    boss.Phase = 3;
                    boss.PhaseAge = 0;
                    _score += 7_500;
                    _shots.Clear();
                    _enemyShots.Clear();
                    _enemies.Clear();
                    _crystalSpears.Clear();
                    ActivateBossRadio(BirgitteDeathRadio);
                    _audio?.Trigger(StormaktSound.Broadside);
                    break;
                }
            }
        }

        if (boss.Phase == 1 && boss.PhaseAge % 84 == 42)
        {
            FireGlimmingeWall(boss);
        }
        if (boss.Phase == 1 && boss.PhaseAge % 240 == 120)
        {
            SpawnGlimmingeEscorts(boss, 2);
        }
        else if (boss.Phase == 2)
        {
            bool burningStage = boss.Health <= GlimmingeBurningHealth;
            int drillInterval = burningStage ? 42 : 66;
            int spearInterval = burningStage ? 90 : 150;
            if (boss.PhaseAge % drillInterval == 24)
            {
                FireEnemyShot((int)boss.X - 38, (int)boss.Y + 25, 4, 2.05);
                FireEnemyShot((int)boss.X + 38, (int)boss.Y + 25, 4, 2.05);
            }
            if (boss.PhaseAge % spearInterval == 60)
            {
                int span = Math.Max(1, _width - 96);
                int spearX = 48 + ((boss.PhaseAge / spearInterval * 97 + 41) % span);
                _crystalSpears.Add(new CrystalSpear(spearX, 0));
            }
            if (burningStage && boss.PhaseAge % 72 == 12)
            {
                FireGlimmingeEmbers(boss);
            }
            int escortInterval = burningStage ? 120 : 180;
            if (boss.PhaseAge % escortInterval == 80)
            {
                SpawnGlimmingeEscorts(boss, burningStage ? 3 : 2);
            }
        }
    }

    private void SpawnGlimmingeEscorts(GlimmingeState boss, int count)
    {
        for (int index = 0; index < count; index++)
        {
            int side = (index & 1) == 0 ? -1 : 1;
            int spread = count == 3 && index == 2 ? 0 : side * 58;
            _enemies.Add(new Enemy(
                Math.Clamp((int)Math.Round(boss.X) + spread, 20, _width - 20),
                (int)Math.Round(boss.Y) + 25 - index * 8,
                1,
                11,
                12,
                0xff3a3030,
                6,
                index * 1.7,
                boss.PhaseAge + index * 37,
                spread,
                false));
        }
        _audio?.Trigger(StormaktSound.Deploy);
    }

    private void FireGlimmingeWall(GlimmingeState boss)
    {
        int gap = 1 + (boss.PhaseAge / 84) % 5;
        for (int column = 0; column < 7; column++)
        {
            if (column == gap || column == gap + 1)
            {
                continue;
            }
            double x = 28 + column * ((_width - 56) / 6.0);
            _enemyShots.Add(new EnemyShot(x, boss.Y + 24, 0, 1.65, 6));
        }
        _audio?.Trigger(StormaktSound.Broadside);
    }

    private void FireGlimmingeEmbers(GlimmingeState boss)
    {
        int x = (int)Math.Round(boss.X);
        int y = (int)Math.Round(boss.Y) + 15;
        for (int index = 0; index < 8; index++)
        {
            double angle = Math.PI / 8.0 + index * Math.PI / 4.0;
            _enemyShots.Add(new EnemyShot(x, y, Math.Cos(angle) * 1.45, Math.Sin(angle) * 1.45, 4));
        }
        _audio?.Trigger(StormaktSound.EnemyExplosion);
    }

    private void StepCrystalSpears()
    {
        for (int index = _crystalSpears.Count - 1; index >= 0; index--)
        {
            CrystalSpear spear = _crystalSpears[index];
            spear.Age++;
            if (spear.Age >= 48)
            {
                int y = 18 + (spear.Age - 48) * 5;
                if (Math.Abs(_shipX - spear.X) < 11 && Math.Abs(_shipY - y) < 22)
                {
                    DamageShip();
                }
                if (y > _height + 30)
                {
                    _crystalSpears.RemoveAt(index);
                    continue;
                }
            }
            _crystalSpears[index] = spear;
        }
    }

    private void StepBoss()
    {
        BossState? boss = _boss;
        if (boss is null)
        {
            return;
        }

        boss.Age++;
        boss.PhaseAge++;
        if (boss.Age <= 180)
        {
            boss.Y = Math.Min(58.0, boss.Y + 0.66);
        }
        else if (boss.Phase == 3)
        {
            StepBossPhaseThreeMovement(boss);
        }
        else if (boss.Phase == 4)
        {
            boss.Y = 58.0;
        }
        else
        {
            double sweep = _width <= 320 ? 54.0 : 68.0;
            boss.X = (_width / 2.0) + Math.Sin((boss.Age - 180) * 0.012) * sweep;
        }

        for (int shotIndex = _shots.Count - 1; shotIndex >= 0; shotIndex--)
        {
            Shot shot = _shots[shotIndex];
            int bossX = (int)Math.Round(boss.X);
            int bossY = (int)Math.Round(boss.Y);
            bool hit = false;
            if (boss.Phase < 4 && boss.Age >= 500 && boss.LeftCannonHealth > 0 && Math.Abs(shot.X - (bossX - BossCannonOffset)) <= 13 && Math.Abs(shot.Y - (bossY + 7)) <= 14)
            {
                boss.LeftCannonHealth -= shot.Power;
                hit = true;
                if (boss.LeftCannonHealth <= 0)
                {
                    _score += 1_200;
                    _audio?.Trigger(StormaktSound.EnemyExplosion);
                }
            }
            else if (boss.Phase < 4 && boss.Age >= 500 && boss.RightCannonHealth > 0 && Math.Abs(shot.X - (bossX + BossCannonOffset)) <= 13 && Math.Abs(shot.Y - (bossY + 7)) <= 14)
            {
                boss.RightCannonHealth -= shot.Power;
                hit = true;
                if (boss.RightCannonHealth <= 0)
                {
                    _score += 1_200;
                    _audio?.Trigger(StormaktSound.EnemyExplosion);
                }
            }
            else if (boss.Age >= 500 && boss.Phase == 1 && Math.Abs(shot.X - bossX) <= 52 && Math.Abs(shot.Y - bossY) <= 29)
            {
                boss.Health -= shot.Power;
                hit = true;
                if (boss.Health <= BossPhaseTwoThreshold)
                {
                    boss.Health = BossPhaseTwoThreshold;
                    boss.Phase = 2;
                    boss.PhaseAge = 0;
                    boss.LeftDockHealth = 50;
                    boss.RightDockHealth = 50;
                    _enemyShots.Clear();
                    _anchorHazards.Clear();
                    ActivateBossRadio(RasmusPhaseTwoRadio);
                    _audio?.Trigger(StormaktSound.Broadside);
                }
            }
            else if (boss.Phase == 2 && boss.LeftDockHealth > 0 &&
                Math.Abs(shot.X - (bossX - BossDockOffset)) <= 11 && Math.Abs(shot.Y - (bossY + 17)) <= 13)
            {
                boss.LeftDockHealth -= shot.Power;
                hit = true;
                if (boss.LeftDockHealth <= 0)
                {
                    _score += 900;
                    _audio?.Trigger(StormaktSound.EnemyExplosion);
                }
            }
            else if (boss.Phase == 2 && boss.RightDockHealth > 0 &&
                Math.Abs(shot.X - (bossX + BossDockOffset)) <= 11 && Math.Abs(shot.Y - (bossY + 17)) <= 13)
            {
                boss.RightDockHealth -= shot.Power;
                hit = true;
                if (boss.RightDockHealth <= 0)
                {
                    _score += 900;
                    _audio?.Trigger(StormaktSound.EnemyExplosion);
                }
            }
            else if (boss.Phase == 2 && Math.Abs(shot.X - bossX) <= 28 && Math.Abs(shot.Y - (bossY + 12)) <= 24)
            {
                hit = true;
                if (IsBossCoreVulnerable(boss))
                {
                    boss.Health -= shot.Power;
                    if (boss.Health <= BossPhaseThreeThreshold)
                    {
                        boss.Health = BossPhaseThreeThreshold;
                        boss.Phase = 3;
                        boss.PhaseAge = 0;
                        boss.PhaseTransitionX = boss.X;
                        boss.PhaseTransitionY = boss.Y;
                        boss.RushX = boss.X;
                        _enemyShots.Clear();
                        _anchorHazards.Clear();
                        ActivateBossRadio(RasmusPhaseThreeRadio);
                        _audio?.Trigger(StormaktSound.Broadside);
                    }
                }
            }
            else if (boss.Phase == 3 && boss.PhaseAge >= 180 &&
                Math.Abs(shot.X - bossX) <= 46 && Math.Abs(shot.Y - bossY) <= 30)
            {
                boss.Health -= Math.Max(1, (shot.Power + 1) / 2);
                hit = true;
                if (boss.Health <= 0)
                {
                    boss.Health = 0;
                    boss.Phase = 4;
                    boss.PhaseAge = 0;
                    _score += 5_000;
                    _shots.Clear();
                    _enemyShots.Clear();
                    _anchorHazards.Clear();
                    ActivateBossRadio(RasmusDeathRadio);
                    _audio?.Trigger(StormaktSound.Broadside);
                    break;
                }
            }
            if (hit)
            {
                _shots.RemoveAt(shotIndex);
            }
        }

        if (boss.Phase == 1 && boss.Age >= 520)
        {
            int attackFrame = boss.Age - 520;
            if (attackFrame % 90 == 0)
            {
                FireBossFans(boss);
            }
            if (attackFrame % 240 == 30)
            {
                int anchorIndex = attackFrame / 240;
                int span = Math.Max(1, _width - 144);
                _anchorHazards.Add(new AnchorHazard(72 + ((anchorIndex * 83) % span), 0));
            }
        }
        else if (boss.Phase == 2 && boss.PhaseAge >= 180)
        {
            StepBossPhaseTwoAttacks(boss);
        }
        else if (boss.Phase == 3 && boss.PhaseAge >= 180)
        {
            StepBossPhaseThreeAttacks(boss);
        }
        else if (boss.Phase == 4)
        {
            StepBossDeath(boss);
        }
    }

    private static bool IsBossCoreVulnerable(BossState boss)
    {
        if (boss.Phase != 2 || boss.PhaseAge < 180)
        {
            return false;
        }
        int cycle = (boss.PhaseAge - 180) % 300;
        bool docksDestroyed = boss.LeftDockHealth <= 0 && boss.RightDockHealth <= 0;
        return docksDestroyed ? cycle is >= 105 and < 265 : cycle is >= 165 and < 225;
    }

    private void FireBossFans(BossState boss)
    {
        int y = (int)Math.Round(boss.Y) + 13;
        if (boss.LeftCannonHealth > 0)
        {
            FireBossFan((int)Math.Round(boss.X) - BossCannonOffset, y, -0.22);
        }
        if (boss.RightCannonHealth > 0)
        {
            FireBossFan((int)Math.Round(boss.X) + BossCannonOffset, y, 0.22);
        }
        _audio?.Trigger(StormaktSound.Broadside);
    }

    private void FireBossFan(int x, int y, double bias)
    {
        for (int index = -2; index <= 2; index++)
        {
            double vx = bias + index * 0.48;
            double vy = 1.72 + (2 - Math.Abs(index)) * 0.16;
            _enemyShots.Add(new EnemyShot(x, y, vx, vy, 0));
        }
    }

    private void StepBossPhaseTwoAttacks(BossState boss)
    {
        int attackFrame = boss.PhaseAge - 180;
        int cycle = attackFrame % 300;
        if (cycle is 0 or 18 or 36)
        {
            FireSealRing(boss, cycle / 18, attackFrame / 300);
            if (cycle == 0)
            {
                _audio?.Trigger(StormaktSound.Broadside);
            }
        }
        if (cycle is 90 or 102 or 114)
        {
            if (boss.LeftCannonHealth > 0)
            {
                FireAimedBossShot((int)Math.Round(boss.X) - BossCannonOffset, (int)Math.Round(boss.Y) + 13, -0.12);
            }
            if (boss.RightCannonHealth > 0)
            {
                FireAimedBossShot((int)Math.Round(boss.X) + BossCannonOffset, (int)Math.Round(boss.Y) + 13, 0.12);
            }
            _audio?.Trigger(StormaktSound.TwinCannon);
        }
    }

    private void FireSealRing(BossState boss, int ringIndex, int volley)
    {
        double gapAngle = Math.PI / 2.0 + ((volley % 3) - 1) * 0.58;
        double speed = 1.05 + ringIndex * 0.28;
        int x = (int)Math.Round(boss.X);
        int y = (int)Math.Round(boss.Y) + 15;
        for (int index = 0; index < 28; index++)
        {
            double angle = index * Math.PI * 2.0 / 28.0;
            double difference = Math.Abs(Math.Atan2(Math.Sin(angle - gapAngle), Math.Cos(angle - gapAngle)));
            if (difference < 0.40)
            {
                continue;
            }
            _enemyShots.Add(new EnemyShot(x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 1));
        }
    }

    private void FireAimedBossShot(int x, int y, double spread)
    {
        double dx = _shipX - x;
        double dy = _shipY - y;
        double length = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        double vx = dx / length;
        double vy = dy / length;
        _enemyShots.Add(new EnemyShot(x, y, (vx - vy * spread) * 2.45, (vy + vx * spread) * 2.45, 2));
    }

    private void StepBossPhaseThreeMovement(BossState boss)
    {
        const double homeY = 58.0;
        const int retreatFrames = 90;
        double rushY = _height - 74.0;
        double rushDistance = rushY - homeY;
        if (boss.PhaseAge < retreatFrames)
        {
            double t = boss.PhaseAge / (double)retreatFrames;
            double eased = t * t * (3.0 - 2.0 * t);
            boss.X = boss.PhaseTransitionX + ((_width / 2.0) - boss.PhaseTransitionX) * eased;
            boss.Y = boss.PhaseTransitionY + (homeY - boss.PhaseTransitionY) * eased;
            boss.RushX = boss.X;
            return;
        }
        if (boss.PhaseAge < 180)
        {
            boss.X = _width / 2.0;
            boss.Y = homeY;
            boss.RushX = boss.X;
            return;
        }

        int attackFrame = boss.PhaseAge - 180;
        if (attackFrame is 0 or 130)
        {
            boss.RushX = boss.X;
        }
        boss.X = boss.RushX;
        if (attackFrame is >= 48 and < 82)
        {
            boss.Y = homeY + (attackFrame - 48) * (rushDistance / 34.0);
        }
        else if (attackFrame is >= 82 and < 120)
        {
            boss.Y = rushY - (attackFrame - 82) * (rushDistance / 38.0);
        }
        else if (attackFrame is >= 178 and < 212)
        {
            boss.Y = homeY + (attackFrame - 178) * (rushDistance / 34.0);
        }
        else if (attackFrame is >= 212 and < 250)
        {
            boss.Y = rushY - (attackFrame - 212) * (rushDistance / 38.0);
        }
        else
        {
            boss.Y = homeY;
            if (attackFrame > 250)
            {
                boss.X = (_width / 2.0) + Math.Sin((attackFrame - 250) * 0.018) * 34.0;
                boss.RushX = boss.X;
            }
        }

        if (boss.Y > 95 && Math.Abs(_shipX - boss.X) < 48 && Math.Abs(_shipY - boss.Y) < 34)
        {
            DamageShip();
        }
    }

    private void StepBossPhaseThreeAttacks(BossState boss)
    {
        int attackFrame = boss.PhaseAge - 180;
        if (attackFrame % 8 == 0)
        {
            double angle = attackFrame * 0.115;
            int x = (int)Math.Round(boss.X);
            int y = (int)Math.Round(boss.Y) + 16;
            _enemyShots.Add(new EnemyShot(x, y, Math.Cos(angle) * 1.32, Math.Sin(angle) * 1.32, 4));
            _enemyShots.Add(new EnemyShot(x, y, -Math.Cos(angle) * 1.32, -Math.Sin(angle) * 1.32, 4));
        }
        if (attackFrame is 48 or 178)
        {
            _audio?.Trigger(StormaktSound.Broadside);
        }
        bool rushing = attackFrame is >= 48 and < 120 or >= 178 and < 250;
        if (rushing && attackFrame % 10 == 0)
        {
            int x = (int)Math.Round(boss.X);
            int y = (int)Math.Round(boss.Y) + 18;
            _enemyShots.Add(new EnemyShot(x - 46, y, -0.75, 2.15, 3));
            _enemyShots.Add(new EnemyShot(x + 46, y, 0.75, 2.15, 3));
        }
    }

    private void StepBossDeath(BossState boss)
    {
        if (boss.PhaseAge == 300)
        {
            ActivateBossRadio(RasmusDeathOathRadio);
        }
        if (boss.PhaseAge is 1 or 30 or 60 or 90 or 120 or 155 or 210 or 270 or 330 or 390 or 480 or 540 or 600)
        {
            bool heavy = boss.PhaseAge is 155 or 330 or 390 or 600;
            _audio?.Trigger(heavy ? StormaktSound.Broadside : StormaktSound.EnemyExplosion);
            if (boss.PhaseAge == 600)
            {
                _audio?.DuckMusic(2_600);
            }
        }
        if (boss.PhaseAge < 660)
        {
            return;
        }
        _boss = null;
        _stageClear = true;
        _stageClearAge = 0;
        _enemyShots.Clear();
        _shots.Clear();
    }

    private void StepAnchorHazards()
    {
        for (int index = _anchorHazards.Count - 1; index >= 0; index--)
        {
            AnchorHazard anchor = _anchorHazards[index];
            anchor.Age++;
            if (anchor.Age >= 48)
            {
                double y = _height - 15 - (anchor.Age - 48) * 4.2;
                if (Math.Abs(_shipX - anchor.X) < 10 && Math.Abs(_shipY - y) < 15)
                {
                    DamageShip();
                }
                if (y < -24)
                {
                    _anchorHazards.RemoveAt(index);
                    continue;
                }
            }
            _anchorHazards[index] = anchor;
        }
    }

    private void StepEnemies()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _enemies[i];
            if (enemy.Kind == 3)
            {
                GroundTarget? bridge = FindActiveBridge(enemy.BridgeGroup);
                if (bridge is GroundTarget cover)
                {
                    int desiredX = cover.X + enemy.EscortOffset;
                    int desiredY = cover.Y - 27 - Math.Abs(enemy.EscortOffset) / 5;
                    enemy.X += Math.Clamp(desiredX - enemy.X, -2, 2);
                    enemy.Y += Math.Clamp(desiredY - enemy.Y, -2, 2);
                }
                else
                {
                    enemy.Breakaway = true;
                    enemy.Y += 2;
                    enemy.X += enemy.EscortOffset < 0 ? -2 : 2;
                }
            }
            else
            {
                enemy.Y += enemy.Speed;
                enemy.Phase += 0.08;
                int wobble = (int)(Math.Sin(enemy.Phase) * 2.0);
                enemy.X += wobble;
            }

            if (enemy.Kind is 4 or 5 or 6 && enemy.Y > 24 && enemy.Y < _height - 64)
            {
                int attackPeriod = enemy.Kind switch { 4 => 150, 5 => 210, _ => 120 };
                int fireFrame = enemy.Kind switch { 4 => 96, 5 => 132, _ => 64 };
                int attackCycle = (_missionFrame + enemy.BridgeGroup) % attackPeriod;
                if (attackCycle == fireFrame)
                {
                    int shotKind = enemy.Kind switch { 4 => 5, 5 => 7, 6 => 6, _ => 0 };
                    double shotSpeed = enemy.Kind switch { 4 => 2.35, 6 => 2.55, _ => 1.85 };
                    FireEnemyShot(enemy.X, enemy.Y + enemy.Radius, shotKind, shotSpeed);
                }
            }

            bool removed = false;
            for (int s = _shots.Count - 1; s >= 0; s--)
            {
                Shot shot = _shots[s];
                if (Math.Abs(shot.X - enemy.X) <= enemy.Radius + 3 && Math.Abs(shot.Y - enemy.Y) <= enemy.Radius + 3)
                {
                    enemy.Health -= shot.Power;
                    _shots.RemoveAt(s);
                    if (enemy.Health <= 0)
                    {
                        _score += enemy.Radius * 10;
                        _enemies.RemoveAt(i);
                        _audio?.Trigger(StormaktSound.EnemyExplosion);
                        removed = true;
                    }
                    break;
                }
            }
            if (removed)
            {
                continue;
            }

            if (Math.Abs(_shipX - enemy.X) < enemy.Radius + 8 && Math.Abs(_shipY - enemy.Y) < enemy.Radius + 8)
            {
                _enemies.RemoveAt(i);
                DamageShip();
                continue;
            }

            if (enemy.Y > _height + 20)
            {
                _enemies.RemoveAt(i);
            }
            else
            {
                _enemies[i] = enemy;
            }
        }
    }

    private GroundTarget? FindActiveBridge(int group)
    {
        foreach (GroundTarget target in _groundTargets)
        {
            if (target.Group == group && target.Type == GroundTargetType.BridgeSpan && target.CollapseFrames == 0)
            {
                return target;
            }
        }
        return null;
    }

    private void SpawnEnemies()
    {
        int endFrame = _levelId == 1 ? 2_700 : BossArrivalFrame;
        if (_missionFrame >= endFrame)
        {
            return;
        }
        int timelineFrame = _missionFrame;
        ReadOnlySpan<EnemyWave> waves = _levelId == 1 ? SkanskaEnemyWaves : EnemyWaves;
        foreach (EnemyWave wave in waves)
        {
            if (timelineFrame < wave.StartFrame || timelineFrame >= wave.EndFrame ||
                (timelineFrame - wave.StartFrame) % wave.IntervalFrames != 0)
            {
                continue;
            }

            int volley = (timelineFrame - wave.StartFrame) / wave.IntervalFrames;
            SpawnFormation(wave, volley);
        }
    }

    private void SpawnFormation(EnemyWave wave, int volley)
    {
        int centerSpan = Math.Max(1, _width - 124);
        int center = 62 + ((volley * 71 + wave.Kind * 43) % centerSpan);
        int radius = wave.Kind == 2 ? 12 : wave.Kind is 1 or 5 ? 10 : 8;
        int speed = wave.Kind is 2 or 5 ? 1 : 2;
        int health = wave.Kind == 2 ? 8 : wave.Kind is 1 or 5 ? 6 : 4;
        uint color = wave.Kind switch
        {
            0 => 0xffa71930,
            1 => 0xffc51f35,
            4 => 0xff2e4939,
            5 => 0xff6f4a32,
            _ => 0xff7f1727,
        };
        for (int index = 0; index < wave.Count; index++)
        {
            int offset = (index - ((wave.Count - 1) / 2)) * 28;
            int y = -radius - (Math.Abs(index - wave.Count / 2) * 12);
            double phase = ((volley * 17 + index * 29 + wave.Kind * 11) % 100) * Math.PI / 50.0;
            _enemies.Add(new Enemy(
                Math.Clamp(center + offset, 20, _width - 20), y, speed, radius, health, color, wave.Kind, phase,
                wave.Kind is 4 or 5 ? volley * 31 + index * 47 : 0, 0, false));
        }
    }

    private void FireEnemyShot(int x, int y, int kind, double speed)
    {
        double dx = _shipX - x;
        double dy = _shipY - y;
        double length = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
        _enemyShots.Add(new EnemyShot(x, y, dx / length * speed, dy / length * speed, kind));
        _audio?.Trigger(StormaktSound.TwinCannon);
    }

    private void SpawnGroundEncounters()
    {
        if (_levelId == 1)
        {
            if (_missionFrame is 900 or 2_100 or 3_000)
            {
                int margin = Math.Min(64, Math.Max(28, _width / 5));
                int x = _missionFrame == 2_100 ? _width - margin : margin;
                _groundTargets.Add(new GroundTarget(
                    x, -24, GroundTargetType.SignalBeacon, 10, _missionFrame, 0, true, 0));
            }
            return;
        }
        if (_levelId != 0)
        {
            return;
        }
        if (_missionFrame >= BossArrivalFrame)
        {
            return;
        }
        int timelineFrame = _missionFrame;
        if (timelineFrame is not (720 or 1_920 or 2_760))
        {
            return;
        }

        bool left = timelineFrame != 1_920;
        int group = _missionFrame * 2;
        SpawnBridgeGroup(left ? 52 : _width - 52, left, group);
        if (timelineFrame == 2_760)
        {
            SpawnBridgeGroup(_width - 52, false, group + 1);
        }
    }

    private void SpawnBridgeGroup(int x, bool left, int group)
    {
        _groundTargets.Add(new GroundTarget(x, -22, GroundTargetType.BridgeSpan, 24, group, 0, true, 0));
        _groundTargets.Add(new GroundTarget(x, -29, GroundTargetType.Turret, 16, group, -48, true, 0));
        _groundTargets.Add(new GroundTarget(x + (left ? 25 : -25), -13, GroundTargetType.EnergyNode, 7, group, 0, true, 0));
        _enemies.Add(new Enemy(x - 19, -54, 0, 11, 8, 0xff7f1727, 3, 0.0, group, -19, false));
        _enemies.Add(new Enemy(x + 19, -58, 0, 11, 8, 0xff7f1727, 3, Math.PI, group, 19, false));
    }

    private void StepStars()
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            Star star = _stars[i];
            star = star with { Y = star.Y + star.Speed };
            if (star.Y >= _height)
            {
                star = star with { X = _random.Next(_width), Y = 0, Speed = 1 + _random.Next(3), Brightness = _random.Next(50, 180) };
            }
            _stars[i] = star;
        }
    }

    private void DrawSky(uint[] frame)
    {
        string backgroundName = _levelId == 1
            ? (_width <= 320 ? "skanska_background" : "skanska_background_wide")
            : (_width <= 320 ? "stora_balt_background" : "stora_balt_background_wide");
        if (_sprites?.TryGet(backgroundName, out Sprite background) == true)
        {
            int scroll = (_missionFrame / 3) % background.Height;
            DrawSprite(frame, background, 0, scroll - background.Height);
            DrawSprite(frame, background, 0, scroll);
            return;
        }
        for (int y = 0; y < _height; y++)
        {
            int row = y * _width;
            uint r = (uint)((_levelId == 1 ? 8 : 4) + y / 18);
            uint g = (uint)((_levelId == 1 ? 11 : 13) + y / (_levelId == 1 ? 12 : 8));
            uint b = (uint)((_levelId == 1 ? 16 : 24) + y / (_levelId == 1 ? 5 : 3));
            for (int x = 0; x < _width; x++)
            {
                uint fog = (uint)((Math.Sin(x * 0.025 + _missionFrame * 0.004 + y * 0.04) + 1.0) * 6.0);
                double auroraCenter = 48.0 + Math.Sin(x * 0.023 + _missionFrame * 0.0015) * 15.0;
                uint aurora = (uint)Math.Max(0.0, 11.0 - Math.Abs(y - auroraCenter) * 0.7);
                frame[row + x] = 0xff000000u |
                    (Math.Min(255u, r + fog) << 16) |
                    (Math.Min(255u, g + fog + aurora) << 8) |
                    Math.Min(255u, b + fog + aurora * 2);
            }
        }
    }

    private void DrawNebula(uint[] frame)
    {
        string backgroundName = _levelId == 1
            ? (_width <= 320 ? "skanska_background" : "skanska_background_wide")
            : (_width <= 320 ? "stora_balt_background" : "stora_balt_background_wide");
        if (_sprites?.TryGet(backgroundName, out _) == true)
        {
            return;
        }
        double scroll = _missionFrame * 0.0028;
        for (int y = 22; y < _height - 20; y += 2)
        {
            for (int x = 0; x < _width; x += 2)
            {
                double gas = Math.Sin(x * 0.019 + scroll) + Math.Sin(y * 0.047 - scroll * 0.7) +
                    Math.Sin((x + y) * 0.011 + scroll * 0.35);
                if (gas > 1.25)
                {
                    uint color = _levelId == 1
                        ? (((x + y + _missionFrame / 8) % 17) < 3 ? 0xff6b3034 : 0xff24483a)
                        : (((x + y + _missionFrame / 8) % 17) < 3 ? 0xff6a3b3b : 0xff28545d);
                    BlendPixel(frame, x, y, color, 38);
                    BlendPixel(frame, x + 1, y, color, 24);
                    BlendPixel(frame, x, y + 1, color, 24);
                }
            }
        }
    }

    private void DrawBeltRuins(uint[] frame)
    {
        if (_levelId == 1)
        {
            DrawSkanskaScenery(frame);
            return;
        }
        if (_sprites?.TryGet("belt_asteroids_generated", out Sprite asteroids) == true &&
            _sprites.TryGet("swedish_wreck_generated", out Sprite wreck) &&
            _sprites.TryGet("bridge_arch_left_generated", out Sprite leftArch) &&
            _sprites.TryGet("bridge_arch_right_generated", out Sprite rightArch))
        {
            int asteroidTravel = (_missionFrame / 3) + 90;
            int asteroidCycle = Math.DivRem(asteroidTravel, 430, out int asteroidPosition);
            int asteroidY = asteroidPosition - 80;
            int asteroidX = (asteroidCycle & 1) == 0 ? 18 : _width - asteroids.Width - 18;
            DrawSprite(frame, asteroids, asteroidX, asteroidY);

            int generatedWreckY = ((_missionFrame * 3 / 4 + 170) % 760) - 120;
            DrawSprite(frame, wreck, _width - 110, generatedWreckY - wreck.Height / 2);

            int generatedBridgeY = ((_missionFrame * 3 / 4 + 510) % 980) - 180;
            DrawSprite(frame, leftArch, -18, generatedBridgeY - leftArch.Height / 2);
            DrawSprite(frame, rightArch, _width - rightArch.Width + 18, generatedBridgeY + 8 - rightArch.Height / 2);
            return;
        }
        for (int index = 0; index < 7; index++)
        {
            int y = ((index * 83 + _missionFrame / 3) % 310) - 50;
            int x = 18 + ((index * 97) % 284);
            uint stone = index % 2 == 0 ? 0xff29343d : 0xff35424a;
            FillCircle(frame, x, y, 3 + index % 5, stone);
            DrawLine(frame, x - 10, y + 4, x + 12, y - 5, 0xff33424c);
        }

        int wreckY = ((_missionFrame * 3 / 4 + 170) % 760) - 120;
        DrawDistantWreck(frame, 246, wreckY);

        int bridgeY = ((_missionFrame * 3 / 4 + 510) % 980) - 180;
        DrawBrokenBridge(frame, bridgeY);
    }

    private void DrawSkanskaScenery(uint[] frame)
    {
        string backgroundName = _width <= 320 ? "skanska_background" : "skanska_background_wide";
        bool generatedBackground = _sprites?.TryGet(backgroundName, out _) == true;
        int pineY = ((_missionFrame / 2 + 80) % (_height + 180)) - 90;
        int kilnY = ((_missionFrame / 2 + 250) % (_height + 220)) - 110;
        int wreckY = ((_missionFrame / 2 + 430) % (_height + 260)) - 130;
        if (_sprites?.TryGet("skanska_crystal_pines", out Sprite pines) == true &&
            _sprites.TryGet("skanska_kiln_moon", out Sprite kiln) &&
            _sprites.TryGet("skanska_mining_wreck", out Sprite wreck))
        {
            DrawSprite(frame, pines, 4, pineY - pines.Height / 2);
            DrawSprite(frame, kiln, _width - kiln.Width - 7, kilnY - kiln.Height / 2);
            DrawSprite(frame, wreck, 10, wreckY - wreck.Height / 2);
            return;
        }
        if (!generatedBackground)
        {
            for (int index = 0; index < 8; index++)
            {
                int y = ((index * 71 + _missionFrame / 3) % (_height + 100)) - 50;
                int x = 18 + ((index * 109) % Math.Max(1, _width - 36));
                uint crystal = (index & 1) == 0 ? 0xff24463b : 0xff342f35;
                FillTriangle(frame, x, y - 20, x - 9, y + 15, x + 9, y + 15, crystal);
                FillTriangle(frame, x, y - 10, x - 15, y + 21, x + 15, y + 21, 0xff192f2a);
                DrawLine(frame, x, y - 17, x, y + 18, 0xff4b7a5d);
            }
        }
        int kilnX = _width - 38;
        FillTriangle(frame, 24, pineY - 34, 7, pineY + 34, 41, pineY + 34, 0xff192f2a);
        DrawLine(frame, 24, pineY - 31, 24, pineY + 31, 0xff4b7a5d);
        FillCircle(frame, kilnX, kilnY, 22, 0xff24282a);
        FillCircle(frame, kilnX, kilnY, 14, 0xff15191a);
        FillCircle(frame, kilnX - 4, kilnY + 3, 4, 0xff9a4f2d);
        BlendPixel(frame, kilnX - 4, kilnY + 3, 0xffff8a4a, 110);
        DrawRect(frame, 8, wreckY - 15, 39, 30, 0xff171d1b);
        DrawLine(frame, 10, wreckY + 12, 44, wreckY - 12, 0xff79523a);
    }

    private void DrawSorenBackgroundPass(uint[] frame)
    {
        if (_levelId != 1 || _missionFrame < 540 || _missionFrame >= 780)
        {
            return;
        }
        int age = _missionFrame - 540;
        double t = age / 239.0;
        double eased = t * t * (3.0 - 2.0 * t);
        int x = (int)Math.Round(-42 + (_width + 84) * eased);
        int y = 72 + (int)Math.Round(Math.Sin(t * Math.PI) * 24.0);
        string spriteName = age is >= 72 and < 180 ? "soren_corsair_boost" : "soren_corsair";
        if (_sprites?.TryGet(spriteName, out Sprite corsair) == true)
        {
            DrawSprite(frame, corsair, x - corsair.Width / 2, y - corsair.Height / 2);
            return;
        }
        FillTriangle(frame, x, y - 17, x - 18, y + 16, x + 18, y + 16, 0xff171b19);
        DrawLine(frame, x - 14, y + 8, x + 14, y + 8, 0xff87583a);
        PutPixel(frame, x, y - 3, 0xff65c58a);
    }

    private void DrawDistantWreck(uint[] frame, int x, int y)
    {
        uint iron = 0xff27333d;
        uint fadedBlue = 0xff244862;
        FillTriangle(frame, x, y - 22, x - 14, y + 20, x + 11, y + 17, iron);
        FillTriangle(frame, x - 2, y - 6, x - 30, y + 12, x - 8, y + 16, fadedBlue);
        DrawLine(frame, x - 25, y + 11, x + 15, y + 17, 0xff6d5935);
        DrawCrown(frame, x - 3, y + 4, 0xff74683f);
        DrawLine(frame, x + 8, y - 2, x + 25, y - 18, 0xff443b37);
    }

    private void DrawBrokenBridge(uint[] frame, int y)
    {
        uint iron = 0xff303943;
        uint copper = 0xff654632;
        DrawRect(frame, -8, y, 91, 14, iron);
        DrawRect(frame, 237, y + 8, 91, 14, iron);
        DrawLine(frame, 0, y - 18, 82, y, copper);
        DrawLine(frame, 238, y + 8, 319, y - 12, copper);
        for (int x = 8; x < 82; x += 18)
        {
            DrawLine(frame, x, y, x + 9, y + 13, 0xff18242c);
        }
        for (int x = 244; x < 319; x += 18)
        {
            DrawLine(frame, x, y + 9, x + 9, y + 21, 0xff18242c);
        }
        DrawText(frame, 14, y + 3, "3020", 0xff78664b);
    }

    private void DrawStars(uint[] frame)
    {
        foreach (Star star in _stars)
        {
            uint c = 0xff000000u | ((uint)star.Brightness << 16) | ((uint)star.Brightness << 8) | (uint)Math.Min(255, star.Brightness + 30);
            PutPixel(frame, star.X, star.Y, c);
            if (star.Speed > 2)
            {
                PutPixel(frame, star.X, star.Y - 1, 0xff405060);
            }
        }
    }

    private void DrawBorder(uint[] frame)
    {
        DrawRect(frame, 0, 0, _width, 16, 0xff101820);
        DrawRect(frame, 0, _height - 12, _width, 12, 0xff101820);
        DrawLine(frame, 0, 16, _width - 1, 16, 0xffd6b25e);
        DrawLine(frame, 0, _height - 13, _width - 1, _height - 13, 0xff8a6b38);
    }

    private void DrawShip(uint[] frame)
    {
        string playerSpriteName = _heat > 80 ? "player_hot" : "player";
        if (_sprites?.TryGet(playerSpriteName, out Sprite player) == true ||
            _sprites?.TryGet("player", out player) == true)
        {
            DrawSprite(frame, player, _shipX - (player.Width / 2), _shipY - (player.Height / 2) - 16);
            if ((_previousButtons & (Up | Down | Left | Right)) != 0)
            {
                DrawPlayerThrust(frame);
            }
            return;
        }

        uint hull = _heat > 80 ? 0xffff8a4a : 0xffc69c58;
        uint brass = 0xffffd66b;
        uint blue = 0xff1f5d9a;
        uint dark = 0xff16202a;
        uint copper = 0xff6c4a2a;

        DrawRect(frame, _shipX - 16, _shipY - 1, 5, 17, dark);
        DrawRect(frame, _shipX + 11, _shipY - 1, 5, 17, dark);
        DrawRect(frame, _shipX - 15, _shipY + 1, 3, 12, 0xff9f6b38);
        DrawRect(frame, _shipX + 12, _shipY + 1, 3, 12, 0xff9f6b38);
        FillTriangle(frame, _shipX, _shipY - 18, _shipX - 10, _shipY + 11, _shipX + 10, _shipY + 11, hull);
        FillTriangle(frame, _shipX, _shipY - 9, _shipX - 23, _shipY + 7, _shipX - 8, _shipY + 14, blue);
        FillTriangle(frame, _shipX, _shipY - 9, _shipX + 23, _shipY + 7, _shipX + 8, _shipY + 14, blue);
        DrawLine(frame, _shipX - 22, _shipY + 7, _shipX - 8, _shipY + 14, brass);
        DrawLine(frame, _shipX + 22, _shipY + 7, _shipX + 8, _shipY + 14, brass);
        DrawRect(frame, _shipX - 6, _shipY - 4, 12, 8, blue);
        DrawCrown(frame, _shipX - 4, _shipY - 2, brass);
        DrawCrown(frame, _shipX + 2, _shipY - 2, brass);
        DrawCrown(frame, _shipX - 1, _shipY + 2, brass);
        DrawRect(frame, _shipX - 2, _shipY - 21, 4, 8, brass);
        DrawRect(frame, _shipX - 3, _shipY - 22, 6, 2, 0xffffec9a);
        DrawRect(frame, _shipX - 12, _shipY + 12, 5, 5, _heat > 80 ? 0xffff6b4a : 0xff2fbfff);
        DrawRect(frame, _shipX + 7, _shipY + 12, 5, 5, _heat > 80 ? 0xffff6b4a : 0xff2fbfff);
        PutPixel(frame, _shipX - 17, _shipY - 4, 0xffd8e6f0);
        PutPixel(frame, _shipX + 17, _shipY - 4, 0xffd8e6f0);
        PutPixel(frame, _shipX - 18, _shipY - 6, 0xffb7c7d6);
        PutPixel(frame, _shipX + 18, _shipY - 6, 0xffb7c7d6);
        DrawRect(frame, _shipX - 19, _shipY + 4, 4, 3, copper);
        DrawRect(frame, _shipX + 15, _shipY + 4, 4, 3, copper);
    }

    private void DrawPlayerThrust(uint[] frame)
    {
        int pulse = (_missionFrame / 3) % 3;
        int length = 3 + pulse;
        uint outer = _heat > 80 ? 0xff8bdfff : 0xff2fbfff;
        uint core = _heat > 80 ? 0xffffffff : 0xffd7f7ff;
        DrawPlayerEngineThrust(frame, _shipX - 7, length, outer, core);
        DrawPlayerEngineThrust(frame, _shipX + 5, length, outer, core);
    }

    private void DrawPlayerEngineThrust(uint[] frame, int engineX, int length, uint outer, uint core)
    {
        int nozzleY = _shipY + 16;
        FillTriangle(frame, engineX - 1, nozzleY, engineX + 1, nozzleY, engineX, nozzleY + length, outer);
        DrawLine(frame, engineX, nozzleY, engineX, nozzleY + Math.Max(2, length - 2), core);
    }

    private void DrawSorenRival(uint[] frame)
    {
        if (_sorenRival is not SorenRivalState rival)
        {
            return;
        }
        int x = (int)Math.Round(rival.X);
        int y = (int)Math.Round(rival.Y);
        bool decoyPhase = rival.Age >= 330 || rival.Health <= 82;
        if (!rival.Interrupted && decoyPhase)
        {
            DrawSorenDecoy(frame, x - 68, y + 17, rival.Age);
            DrawSorenDecoy(frame, x + 68, y - 9, rival.Age + 11);
        }
        if (!rival.Interrupted && !decoyPhase && rival.Age % 90 is >= 22 and <= 58)
        {
            DrawSorenDecoy(frame, x - 18, y + 5, rival.Age);
            DrawSorenDecoy(frame, x - 36, y + 10, rival.Age + 7);
        }

        string spriteName = rival.Interrupted || rival.Health <= 70
            ? "soren_corsair_damaged"
            : rival.Age % 90 is >= 22 and <= 58 ? "soren_corsair_boost" : "soren_corsair";
        if (_sprites?.TryGet(spriteName, out Sprite corsair) == true)
        {
            DrawSprite(frame, corsair, x - corsair.Width / 2, y - corsair.Height / 2);
        }
        else
        {
            FillTriangle(frame, x, y - 18, x - 20, y + 16, x + 20, y + 16, 0xff171b19);
            DrawLine(frame, x - 15, y + 8, x + 15, y + 8, 0xffa66b3f);
            PutPixel(frame, x, y - 3, 0xff65c58a);
        }

        if (!rival.Interrupted)
        {
            int barWidth = Math.Max(0, 92 * rival.Health / 140);
            DrawRect(frame, (_width - 100) / 2, 19, 100, 7, 0xff101513);
            DrawRect(frame, (_width - 92) / 2, 21, barWidth, 3, 0xff65c58a);
        }
    }

    private void DrawSorenDecoy(uint[] frame, int x, int y, int phase)
    {
        if (_sprites?.TryGet("soren_radar_decoy", out Sprite decoy) == true)
        {
            DrawSprite(frame, decoy, x - decoy.Width / 2, y - decoy.Height / 2);
            return;
        }
        uint signal = (phase / 5 & 1) == 0 ? 0xff2f7650 : 0xff315f47;
        DrawLine(frame, x, y - 13, x - 14, y + 11, signal);
        DrawLine(frame, x, y - 13, x + 14, y + 11, signal);
        DrawLine(frame, x - 14, y + 11, x + 14, y + 11, 0xff4d4938);
        PutPixel(frame, x, y - 2, 0xff65c58a);
    }

    private void DrawGlimminge(uint[] frame)
    {
        if (_glimminge is not GlimmingeState boss)
        {
            return;
        }
        int x = (int)Math.Round(boss.X);
        int y = (int)Math.Round(boss.Y);
        int deathFall = boss.Phase == 3 ? boss.PhaseAge / 8 : 0;
        uint iron = boss.Phase == 3 ? 0xff242628 : 0xff34383a;
        uint darkIron = 0xff161a1c;
        uint red = 0xff8f2635;
        uint white = 0xffd8d2c5;
        uint crystal = 0xff536f69;

        if (_sprites is not null &&
            _sprites.TryGet("glimminge_jarn", out Sprite intact) &&
            _sprites.TryGet("glimminge_shield_braced", out Sprite shield) &&
            _sprites.TryGet("glimminge_jarn_damaged", out Sprite damaged) &&
            _sprites.TryGet("glimminge_burning", out Sprite burning) &&
            _sprites.TryGet("glimminge_wreck", out Sprite wreck))
        {
            if (boss.Phase == 1)
            {
                int shieldCycle = boss.PhaseAge % 84;
                int shieldAlpha = shieldCycle switch
                {
                    < 18 => 0,
                    < 30 => (shieldCycle - 18) * 255 / 12,
                    <= 52 => 255,
                    < 68 => (68 - shieldCycle) * 255 / 16,
                    _ => 0,
                };
                DrawSprite(frame, intact, x - intact.Width / 2, y - intact.Height / 2);
                if (shieldAlpha > 0)
                {
                    DrawSpriteAlpha(frame, shield, x - shield.Width / 2, y - shield.Height / 2, (uint)shieldAlpha);
                }
            }
            else if (boss.Phase == 2 && boss.PhaseAge < 60)
            {
                DrawSprite(frame, intact, x - intact.Width / 2, y - intact.Height / 2);
                DrawSpriteAlpha(frame, damaged, x - damaged.Width / 2, y - damaged.Height / 2,
                    (uint)(boss.PhaseAge * 255 / 60));
            }
            else if (boss.Phase == 2 && boss.BurningTransitionAge is > 0 and < 60)
            {
                DrawSprite(frame, damaged, x - damaged.Width / 2, y - damaged.Height / 2);
                DrawSpriteAlpha(frame, burning, x - burning.Width / 2, y - burning.Height / 2,
                    (uint)(boss.BurningTransitionAge * 255 / 60));
            }
            else if (boss.Phase == 2)
            {
                Sprite current = boss.Health <= GlimmingeBurningHealth ? burning : damaged;
                DrawSprite(frame, current, x - current.Width / 2, y - current.Height / 2);
            }
            else
            {
                int wreckAlpha = Math.Min(255, boss.PhaseAge * 255 / 60);
                int wreckY = y - wreck.Height / 2 + Math.Min(22, deathFall);
                DrawSprite(frame, burning, x - burning.Width / 2, y - burning.Height / 2);
                DrawSpriteAlpha(frame, wreck, x - wreck.Width / 2, wreckY, (uint)wreckAlpha);
            }
            if (boss.Phase == 2)
            {
                uint drillAlpha = (uint)Math.Min(255, boss.PhaseAge * 255 / 60);
                DrawGlimmingeDrill(frame, x - 43, y + 29, -1, boss.PhaseAge, crystal, drillAlpha);
                DrawGlimmingeDrill(frame, x + 43, y + 29, 1, boss.PhaseAge, crystal, drillAlpha);
            }
            return;
        }

        DrawRect(frame, x - 54 - deathFall / 4, y - 25 + deathFall, 108, 45, iron);
        FillTriangle(frame, x, y + 39 + deathFall, x - 58 - deathFall / 4, y + 12 + deathFall,
            x + 58 + deathFall / 4, y + 12 + deathFall, darkIron);
        for (int tower = -2; tower <= 2; tower++)
        {
            int towerX = x + tower * 22;
            int towerFall = boss.Phase == 3 ? deathFall + Math.Abs(tower) * 5 : 0;
            DrawRect(frame, towerX - 7, y - 39 + towerFall, 15, 28, tower == 0 ? 0xff262b2e : iron);
            FillTriangle(frame, towerX, y - 49 + towerFall, towerX - 8, y - 38 + towerFall,
                towerX + 8, y - 38 + towerFall, darkIron);
        }
        DrawRect(frame, x - 50, y - 4 + deathFall, 100, 7, red);
        DrawRect(frame, x - 3, y - 24 + deathFall, 7, 45, white);
        DrawRect(frame, x - 17, y - 12 + deathFall, 34, 24, 0xff1c2022);
        DrawLine(frame, x - 15, y + 9 + deathFall, x, y - 9 + deathFall, 0xff9a6741);
        DrawLine(frame, x, y - 9 + deathFall, x + 15, y + 9 + deathFall, 0xff9a6741);

        if (boss.Phase == 2)
        {
            DrawGlimmingeDrill(frame, x - 42, y + 25, -1, boss.PhaseAge, crystal);
            DrawGlimmingeDrill(frame, x + 42, y + 25, 1, boss.PhaseAge, crystal);
        }
        if (boss.Phase == 3)
        {
            DrawGlimmingeDeathBlocks(frame, boss, x, y, deathFall);
        }
    }

    private void DrawGlimmingeDeathBlocks(uint[] frame, GlimmingeState boss, int x, int y, int deathFall)
    {
        for (int block = 0; block < 6; block++)
        {
            int blockX = x - 55 + block * 21 + ((block & 1) == 0 ? -deathFall / 3 : deathFall / 3);
            int blockY = y + 16 + deathFall + block * 3;
            DrawRect(frame, blockX, blockY, 16, 10, block % 3 == 0 ? 0xff5b3030 : 0xff292d2f);
        }
    }

    private void DrawGlimmingeDrill(uint[] frame, int x, int y, int direction, int age, uint crystal, uint alpha = 255)
    {
        if (_sprites?.TryGet("glimminge_drill_turret", out Sprite drill) == true)
        {
            int drawX = x - drill.Width / 2;
            int drawY = y - drill.Height / 2 + (age / 4 & 3);
            if (direction < 0)
            {
                if (alpha >= 255)
                {
                    DrawSpriteFlippedX(frame, drill, drawX, drawY);
                }
                else
                {
                    DrawSpriteFlippedXAlpha(frame, drill, drawX, drawY, alpha);
                }
            }
            else
            {
                DrawSpriteAlpha(frame, drill, drawX, drawY, alpha);
            }
            return;
        }
        int pulse = age / 4 & 3;
        FillTriangle(frame, x + direction * (19 + pulse), y, x - direction * 5, y - 8,
            x - direction * 5, y + 8, crystal);
        DrawLine(frame, x - direction * 4, y, x + direction * (17 + pulse), y, 0xffb87949);
    }

    private void DrawCrystalSpears(uint[] frame)
    {
        foreach (CrystalSpear spear in _crystalSpears)
        {
            if (spear.Age < 48)
            {
                uint warning = (spear.Age / 5 & 1) == 0 ? 0xff65c58a : 0xffa66b3f;
                DrawLine(frame, spear.X, 18, spear.X, _height - 14, warning);
                DrawLine(frame, spear.X - 8, _height - 23, spear.X + 8, _height - 23, warning);
                continue;
            }
            int y = 18 + (spear.Age - 48) * 5;
            if (_sprites?.TryGet("glimminge_crystal_spear", out Sprite crystalSpear) == true)
            {
                DrawSprite(frame, crystalSpear, spear.X - crystalSpear.Width / 2, y - crystalSpear.Height / 2);
                continue;
            }
            FillTriangle(frame, spear.X, y + 22, spear.X - 9, y - 13, spear.X + 9, y - 13, 0xff29463f);
            DrawLine(frame, spear.X, y - 10, spear.X, y + 18, 0xff78a389);
        }
    }

    private void DrawBoss(uint[] frame)
    {
        BossState? boss = _boss;
        if (boss is null)
        {
            return;
        }
        int x = (int)Math.Round(boss.X);
        int y = (int)Math.Round(boss.Y);
        string generatedSpriteName = boss.Phase == 1 ? "boss_kronens_tiende" : "boss_kronens_tiende_damaged";
        if (_sprites?.TryGet(generatedSpriteName, out Sprite generatedBoss) == true)
        {
            DrawSprite(frame, generatedBoss, x - generatedBoss.Width / 2, y - generatedBoss.Height / 2);
            if (_sprites.TryGet("boss_broadside_cannon", out Sprite broadsideCannon))
            {
                if (boss.LeftCannonHealth > 0)
                {
                    DrawSprite(frame, broadsideCannon, x - BossCannonOffset - broadsideCannon.Width / 2, y + 7 - broadsideCannon.Height / 2);
                }
                if (boss.RightCannonHealth > 0)
                {
                    DrawSpriteFlippedX(frame, broadsideCannon, x + BossCannonOffset - broadsideCannon.Width / 2, y + 7 - broadsideCannon.Height / 2);
                }
            }
            if (boss.LeftCannonHealth <= 0)
            {
                DrawGeneratedBossCannonWreck(frame, x - BossCannonOffset, y + 7, false);
            }
            if (boss.RightCannonHealth <= 0)
            {
                DrawGeneratedBossCannonWreck(frame, x + BossCannonOffset, y + 7, true);
            }
            DrawBossPhaseAttachments(frame, boss, x, y);
            DrawBossFinalEffects(frame, boss, x, y);
            return;
        }
        uint red = boss.Phase == 1 ? 0xff8f1f31 : 0xffb02a3e;
        uint darkRed = 0xff571724;
        uint white = 0xfff2eee4;
        uint iron = 0xff202932;
        uint brass = 0xffc39a52;

        FillTriangle(frame, x, y + 34, x - 55, y - 21, x + 55, y - 21, darkRed);
        DrawRect(frame, x - 52, y - 18, 104, 31, red);
        DrawRect(frame, x - 37, y - 24, 74, 8, iron);
        DrawRect(frame, x - 5, y - 23, 10, 51, white);
        DrawRect(frame, x - 48, y - 6, 96, 8, white);
        DrawRect(frame, x - 18, y - 17, 36, 25, darkRed);
        DrawCrown(frame, x - 9, y - 12, brass);
        DrawCrown(frame, x + 1, y - 12, brass);
        DrawCrown(frame, x - 4, y - 5, brass);
        FillCircle(frame, x, y + 25, 12, iron);
        FillCircle(frame, x, y + 25, 8, brass);
        DrawRect(frame, x - 2, y + 17, 4, 17, white);
        DrawLine(frame, x - 53, y - 18, x - 67, y - 31, brass);
        DrawLine(frame, x + 53, y - 18, x + 67, y - 31, brass);

        DrawBossCannon(frame, x - BossCannonOffset, y + 7, boss.LeftCannonHealth, red, white, iron);
        DrawBossCannon(frame, x + BossCannonOffset, y + 7, boss.RightCannonHealth, red, white, iron);
        for (int link = 0; link < 4; link++)
        {
            FillCircle(frame, x - 61, y - 4 + link * 8, 2, brass);
            FillCircle(frame, x + 61, y - 4 + link * 8, 2, brass);
        }
        DrawBossPhaseAttachments(frame, boss, x, y);
        DrawBossFinalEffects(frame, boss, x, y);
    }

    private void DrawBossFinalEffects(uint[] frame, BossState boss, int x, int y)
    {
        if (boss.Phase < 3 || (boss.Phase == 3 && boss.PhaseAge < 90))
        {
            return;
        }
        DrawCircleOutline(frame, x, y + 15, 11 + ((_missionFrame / 5) & 1), 0xffff6b4a);

        if (boss.Phase == 3 && boss.PhaseAge >= 180)
        {
            int attackFrame = boss.PhaseAge - 180;
            bool warning = attackFrame is >= 0 and < 48 or >= 130 and < 178;
            if (warning)
            {
                uint color = (attackFrame & 7) < 4 ? 0xffff6b62 : 0xff7f2632;
                int warningX = (int)Math.Round(boss.RushX);
                DrawLine(frame, warningX - 3, y + 25, warningX - 3, _height - 14, color);
                DrawLine(frame, warningX + 3, y + 25, warningX + 3, _height - 14, color);
                DrawLine(frame, warningX - 14, _height - 29, warningX + 14, _height - 29, color);
            }
        }
        if (boss.Phase == 4)
        {
            DrawBossDeathEffects(frame, boss, x, y);
        }
    }

    private void DrawBossDeathEffects(uint[] frame, BossState boss, int x, int y)
    {
        (int X, int Y, int Start)[] blasts =
        [
            (-42, 5, 0),
            (38, -7, 30),
            (-18, -18, 60),
            (21, 18, 90),
            (0, 4, 120),
            (-51, -11, 190),
            (48, 12, 245),
            (-27, 20, 300),
            (29, -16, 355),
            (0, 7, 405),
            (-44, 16, 470),
            (43, -13, 535),
            (0, 3, 600),
        ];
        foreach ((int offsetX, int offsetY, int start) in blasts)
        {
            int age = boss.PhaseAge - start;
            if (age < 0 || age >= 42)
            {
                continue;
            }
            int radius = Math.Min(13, 2 + age / 3);
            FillCircle(frame, x + offsetX, y + offsetY, radius, 0xffb33b2e);
            FillCircle(frame, x + offsetX, y + offsetY, Math.Max(1, radius - 4), 0xffff8a34);
            PutPixel(frame, x + offsetX, y + offsetY, 0xffffec9a);
        }
        if (boss.PhaseAge is >= 150 and < 210)
        {
            int pulseAge = boss.PhaseAge - 150;
            int radius = pulseAge < 30 ? 8 + pulseAge : 38 - (pulseAge - 30) / 2;
            FillCircle(frame, x, y + 5, radius, 0xffe05a2a);
            FillCircle(frame, x, y + 5, Math.Max(3, radius - 12), 0xffffd66b);
            FillCircle(frame, x, y + 5, Math.Max(2, radius - 23), 0xffffffff);
        }
    }

    private void DrawBossPhaseAttachments(uint[] frame, BossState boss, int x, int y)
    {
        if (boss.Phase != 2 && !(boss.Phase == 3 && boss.PhaseAge < 90))
        {
            return;
        }
        DrawLine(frame, x - 52, y + 12, x - BossDockOffset, y + 17, 0xff8a6b38);
        DrawLine(frame, x + 52, y + 12, x + BossDockOffset, y + 17, 0xff8a6b38);
        DrawDockTower(frame, x - BossDockOffset, y + 17, boss.LeftDockHealth);
        DrawDockTower(frame, x + BossDockOffset, y + 17, boss.RightDockHealth);

        bool vulnerable = IsBossCoreVulnerable(boss);
        uint seal = vulnerable ? 0xffff8a4a : 0xff7894a5;
        int pulse = ((_missionFrame / 4) & 1) == 0 ? 8 : 10;
        DrawCircleOutline(frame, x, y + 15, pulse, seal);
        DrawCircleOutline(frame, x, y + 15, pulse + 3, vulnerable ? 0xffd6b25e : 0xff344d5c);
    }

    private void DrawDockTower(uint[] frame, int x, int y, int health)
    {
        string assetName = health <= 0 ? "boss_dock_turret_wreck" : "boss_dock_turret";
        if (_sprites?.TryGet(assetName, out Sprite dockTurret) == true)
        {
            DrawSprite(frame, dockTurret, x - dockTurret.Width / 2, y - dockTurret.Height / 2);
            if (health is > 0 and < 25)
            {
                DrawLine(frame, x - 5, y - 5, x + 4, y + 5, 0xffff6b4a);
            }
            return;
        }
        if (health <= 0)
        {
            DrawLine(frame, x - 7, y - 8, x + 7, y + 8, 0xff8d4938);
            DrawLine(frame, x + 7, y - 8, x - 7, y + 8, 0xff3a3031);
            PutPixel(frame, x, y + 11, 0xffff8a4a);
            return;
        }
        DrawRect(frame, x - 8, y - 8, 16, 16, 0xff29343d);
        DrawRect(frame, x - 6, y - 6, 12, 12, 0xff8f2635);
        DrawRect(frame, x - 2, y - 9, 4, 18, 0xfff2eee4);
        DrawRect(frame, x - 2, y + 7, 5, 9, 0xffc39a52);
        if (health < 25)
        {
            DrawLine(frame, x - 5, y - 5, x + 4, y + 5, 0xffff6b4a);
        }
    }

    private void DrawGeneratedBossCannonWreck(uint[] frame, int x, int y, bool flipped)
    {
        if (_sprites?.TryGet("boss_broadside_cannon_wreck", out Sprite wreck) == true)
        {
            FillCircle(frame, x, y, 15, 0xff171c20);
            int drawX = x - wreck.Width / 2;
            int drawY = y - wreck.Height / 2;
            if (flipped)
            {
                DrawSpriteFlippedX(frame, wreck, drawX, drawY);
            }
            else
            {
                DrawSprite(frame, wreck, drawX, drawY);
            }
            PutPixel(frame, x, y + 7, 0xffff8a4a);
            return;
        }
        DrawBossCannon(frame, x, y, 0, 0, 0, 0xff202932);
    }

    private void DrawCircleOutline(uint[] frame, int centerX, int centerY, int radius, uint color)
    {
        int previousX = centerX + radius;
        int previousY = centerY;
        for (int step = 1; step <= 20; step++)
        {
            double angle = step * Math.PI * 2.0 / 20.0;
            int x = centerX + (int)Math.Round(Math.Cos(angle) * radius);
            int y = centerY + (int)Math.Round(Math.Sin(angle) * radius);
            DrawLine(frame, previousX, previousY, x, y, color);
            previousX = x;
            previousY = y;
        }
    }

    private void DrawBossCannon(uint[] frame, int x, int y, int health, uint red, uint white, uint iron)
    {
        if (health <= 0)
        {
            FillCircle(frame, x, y, 11, 0xff25282b);
            DrawLine(frame, x - 8, y - 8, x + 8, y + 8, 0xff8d4938);
            DrawLine(frame, x + 8, y - 8, x - 8, y + 8, 0xff8d4938);
            return;
        }
        FillCircle(frame, x, y, 12, iron);
        DrawRect(frame, x - 9, y - 7, 18, 14, red);
        DrawRect(frame, x - 3, y - 11, 6, 22, white);
        DrawRect(frame, x - 2, y + 7, 5, 14, 0xffc39a52);
    }

    private void DrawAnchorHazards(uint[] frame)
    {
        foreach (AnchorHazard anchor in _anchorHazards)
        {
            if (anchor.Age < 48)
            {
                uint warning = (anchor.Age & 7) < 4 ? 0xffc51f35 : 0xff622630;
                DrawLine(frame, anchor.X, 20, anchor.X, _height - 14, warning);
                DrawLine(frame, anchor.X - 8, _height - 24, anchor.X + 8, _height - 24, warning);
                continue;
            }
            int y = (int)Math.Round(_height - 15 - (anchor.Age - 48) * 4.2);
            for (int link = 0; link < 6; link++)
            {
                FillCircle(frame, anchor.X, y + link * 7, 3, 0xff8a6b38);
                PutPixel(frame, anchor.X, y + link * 7, 0xffe0bd73);
            }
            FillTriangle(frame, anchor.X, y - 10, anchor.X - 9, y + 5, anchor.X + 9, y + 5, 0xff343d44);
        }
    }

    private void DrawGroundTargets(uint[] frame)
    {
        foreach (GroundTarget target in _groundTargets)
        {
            if (target.CollapseFrames > 0)
            {
                DrawCollapsingGroundTarget(frame, target);
                continue;
            }
            if (target.Type == GroundTargetType.BridgeSpan)
            {
                string bridgeName = target.Health < 17 ? "bridge_span_damaged_generated" : "bridge_span_generated";
                if (_sprites?.TryGet(bridgeName, out Sprite bridge) == true)
                {
                    DrawSprite(frame, bridge, target.X - bridge.Width / 2, target.Y - bridge.Height / 2);
                    if (target.Health < 9)
                    {
                        DrawLine(frame, target.X - 20, target.Y - 20, target.X + 13, target.Y + 22, 0xffff6b4a);
                    }
                    continue;
                }
                uint hull = target.Health < 9 ? 0xff57403c : target.Health < 17 ? 0xff45434a : 0xff343e47;
                DrawRect(frame, target.X - 43, target.Y - 9, 86, 18, hull);
                DrawLine(frame, target.X - 42, target.Y - 8, target.X + 41, target.Y - 8, 0xff785c3b);
                DrawLine(frame, target.X - 42, target.Y + 8, target.X + 41, target.Y + 8, 0xff1a252d);
                for (int x = target.X - 35; x < target.X + 35; x += 18)
                {
                    DrawLine(frame, x, target.Y - 7, x + 10, target.Y + 7, 0xff222d35);
                }
                if (target.Health < 17)
                {
                    DrawLine(frame, target.X - 8, target.Y - 8, target.X + 4, target.Y + 8, 0xffb36b4a);
                }
                if (target.Health < 9)
                {
                    DrawLine(frame, target.X - 31, target.Y + 7, target.X - 18, target.Y - 8, 0xffff6b4a);
                    DrawLine(frame, target.X + 17, target.Y - 8, target.X + 32, target.Y + 7, 0xffff6b4a);
                }
                continue;
            }

            if (target.Type == GroundTargetType.EnergyNode)
            {
                uint pulse = ((_missionFrame / 6) & 1) == 0 ? 0xffff6b62 : 0xffc51f35;
                if (_sprites?.TryGet("bridge_node_generated", out Sprite node) == true)
                {
                    DrawSprite(frame, node, target.X - node.Width / 2, target.Y - node.Height / 2);
                    FillCircle(frame, target.X, target.Y, 2, pulse);
                    continue;
                }
                FillCircle(frame, target.X, target.Y, 7, 0xff272f36);
                FillCircle(frame, target.X, target.Y, 3, pulse);
                DrawLine(frame, target.X - 12, target.Y, target.X + 12, target.Y, 0xff8f2635);
                continue;
            }

            if (target.Type == GroundTargetType.SignalBeacon)
            {
                string beaconName = target.Health <= 5 ? "skanska_signal_beacon_damaged" : "skanska_signal_beacon";
                if (_sprites?.TryGet(beaconName, out Sprite beacon) == true)
                {
                    DrawSprite(frame, beacon, target.X - beacon.Width / 2, target.Y - beacon.Height / 2);
                    continue;
                }
                uint signal = ((target.Age / 6) & 1) == 0 ? 0xff65c58a : 0xff2f7650;
                uint copper = target.Health <= 5 ? 0xff6b4230 : 0xffa66b3f;
                DrawRect(frame, target.X - 8, target.Y + 2, 17, 7, 0xff171d1b);
                DrawLine(frame, target.X - 6, target.Y + 8, target.X, target.Y - 12, copper);
                DrawLine(frame, target.X + 6, target.Y + 8, target.X, target.Y - 12, copper);
                FillCircle(frame, target.X, target.Y - 12, 4, 0xff102019);
                FillCircle(frame, target.X, target.Y - 12, 2, signal);
                DrawLine(frame, target.X - 13, target.Y - 12, target.X - 7, target.Y - 12, signal);
                DrawLine(frame, target.X + 7, target.Y - 12, target.X + 13, target.Y - 12, signal);
                continue;
            }

            uint turret = target.Enabled ? 0xff8f2635 : 0xff3b4145;
            string cannonName = target.Enabled ? "enemy_bridge_cannon" : "enemy_bridge_cannon_wreck";
            if (_sprites?.TryGet(cannonName, out Sprite cannon) == true)
            {
                DrawSprite(frame, cannon, target.X - cannon.Width / 2, target.Y - cannon.Height / 2);
                int generatedCycle = target.Age % 180;
                if (target.Enabled && generatedCycle is >= 40 and < 88 && target.Y > 24 && target.Y < _height - 50)
                {
                    uint lockColor = (generatedCycle & 7) < 4 ? 0xffc51f35 : 0xff6f2631;
                    DrawLine(frame, target.X, target.Y + 16, _shipX, _shipY, lockColor);
                    DrawRect(frame, target.X - 2, target.Y - 19, 5, 3, 0xffff6b62);
                }
                continue;
            }
            if (_sprites?.TryGet("bridge_turret_generated", out Sprite generatedTurret) == true)
            {
                DrawSprite(frame, generatedTurret, target.X - generatedTurret.Width / 2, target.Y - generatedTurret.Height / 2);
                if (!target.Enabled)
                {
                    DrawLine(frame, target.X - 8, target.Y - 7, target.X + 8, target.Y + 8, 0xff30363a);
                }
                int generatedCycle = target.Age % 180;
                if (target.Enabled && generatedCycle is >= 40 and < 88 && target.Y > 24 && target.Y < _height - 50)
                {
                    uint lockColor = (generatedCycle & 7) < 4 ? 0xffc51f35 : 0xff6f2631;
                    DrawLine(frame, target.X, target.Y + 12, _shipX, _shipY, lockColor);
                    DrawRect(frame, target.X - 2, target.Y - 15, 5, 3, 0xffff6b62);
                }
                continue;
            }
            FillCircle(frame, target.X, target.Y, 10, 0xff252d34);
            DrawRect(frame, target.X - 7, target.Y - 6, 14, 12, turret);
            DrawLine(frame, target.X, target.Y, target.X, target.Y + 16, target.Enabled ? 0xfff2eee4 : 0xff62696c);
            int cycle = target.Age % 180;
            if (target.Enabled && cycle is >= 40 and < 88 && target.Y > 24 && target.Y < _height - 50)
            {
                uint lockColor = (cycle & 7) < 4 ? 0xffc51f35 : 0xff6f2631;
                DrawLine(frame, target.X, target.Y + 8, _shipX, _shipY, lockColor);
                DrawRect(frame, target.X - 2, target.Y - 11, 5, 3, 0xffff6b62);
            }
        }
    }

    private void DrawCollapsingGroundTarget(uint[] frame, GroundTarget target)
    {
        int fall = 45 - target.CollapseFrames;
        int direction = (target.Group & 1) == 0 ? 1 : -1;
        int x = target.X + direction * fall / 3;
        uint ember = (fall & 5) < 3 ? 0xffff8a4a : 0xffb34c38;
        if (target.Type == GroundTargetType.BridgeSpan)
        {
            if (_sprites?.TryGet("bridge_debris_slab", out Sprite slab) == true &&
                _sprites.TryGet("bridge_debris_rail", out Sprite rail) &&
                _sprites.TryGet("bridge_debris_machine", out Sprite machine))
            {
                DrawSprite(frame, slab, x - 44, target.Y - 16 + fall / 4);
                DrawSprite(frame, rail, x - 5, target.Y - 12 + fall / 2);
                DrawSprite(frame, machine, x + 22, target.Y - 8 + fall * 2 / 3);
                PutPixel(frame, x - 10, target.Y + fall / 2, ember);
                PutPixel(frame, x + 18, target.Y + 5 + fall / 2, ember);
                return;
            }
            DrawRect(frame, x - 43, target.Y - 8, 25, 13, 0xff4b3c3d);
            DrawRect(frame, x - 12, target.Y - 4 + fall / 5, 27, 12, 0xff393a40);
            DrawRect(frame, x + 22, target.Y - 10 + fall / 3, 21, 11, 0xff503b38);
            PutPixel(frame, x - 15, target.Y + fall / 2, ember);
            PutPixel(frame, x + 19, target.Y + 5 + fall / 2, ember);
        }
        else
        {
            if (target.Type == GroundTargetType.Turret &&
                _sprites?.TryGet("enemy_bridge_cannon_wreck", out Sprite cannonWreck) == true)
            {
                DrawSprite(frame, cannonWreck, x - cannonWreck.Width / 2, target.Y - cannonWreck.Height / 2 + fall / 2);
                DrawLine(frame, x - 7, target.Y - 5, x + 8, target.Y + 7, ember);
                return;
            }
            FillCircle(frame, x, target.Y, target.Type == GroundTargetType.Turret ? 8 : 5, 0xff44383a);
            DrawLine(frame, x - 7, target.Y - 5, x + 8, target.Y + 7, ember);
        }
    }

    private void DrawShots(uint[] frame)
    {
        foreach (Shot shot in _shots)
        {
            if (_sprites is not null)
            {
                string name = shot.Power > 4 ? "shot_broadside" : "shot_blue";
                if (_sprites.TryGet(name, out Sprite sprite))
                {
                    DrawSprite(frame, sprite, shot.X - (sprite.Width / 2), shot.Y - (sprite.Height / 2));
                    continue;
                }
            }

            DrawRect(frame, shot.X - 1, shot.Y - 4, 3, 8, shot.Color);
            PutPixel(frame, shot.X, shot.Y - 5, 0xffffffff);
        }
    }

    private void DrawEnemyShots(uint[] frame)
    {
        foreach (EnemyShot shot in _enemyShots)
        {
            int x = (int)Math.Round(shot.X);
            int y = (int)Math.Round(shot.Y);
            string? generatedShotName = shot.Kind switch
            {
                0 => "enemy_shot_red",
                1 or 4 => "enemy_shot_seal",
                2 => "enemy_shot_white",
                3 => "soren_copper_shot",
                5 => "skanska_shot_signal",
                6 => "glimminge_shot_iron",
                7 => "fogde_convoy_shot",
                _ => null,
            };
            if (generatedShotName is not null && _sprites?.TryGet(generatedShotName, out Sprite generatedShot) == true)
            {
                DrawSprite(frame, generatedShot, x - generatedShot.Width / 2, y - generatedShot.Height / 2);
                continue;
            }
            uint outer = shot.Kind switch
            {
                1 => 0xffd6b25e,
                2 => 0xfff2eee4,
                3 => 0xff554039,
                4 => 0xffb34c38,
                5 => 0xff2f7650,
                6 => 0xff4b5358,
                _ => 0xffc51f35,
            };
            uint core = shot.Kind switch
            {
                1 => 0xffffec9a,
                2 => 0xffff6b62,
                3 => 0xffff8a4a,
                4 => 0xffffc46b,
                5 => 0xff9affbd,
                6 => 0xffd3c6a4,
                _ => 0xffffd6b0,
            };
            int radius = shot.Kind is 1 or 4 ? 2 : 3;
            FillCircle(frame, x, y, radius, outer);
            PutPixel(frame, x, y, core);
            PutPixel(frame, x, y - radius - 1, core);
        }
    }

    private void DrawEnemies(uint[] frame)
    {
        foreach (Enemy enemy in _enemies)
        {
            DrawEnemy(frame, enemy);
        }
    }

    private void DrawEnemy(uint[] frame, Enemy enemy)
    {
        uint brass = 0xffd6b25e;
        uint dark = 0xff18202a;
        uint danishRed = 0xffc51f35;
        uint danishDark = 0xff7f1727;
        uint danishWhite = 0xfff2eee4;
        if (_sprites is not null && enemy.Kind == 3)
        {
            string sloopName = enemy.Breakaway ? "fogde_sloop_breakaway" : "fogde_sloop";
            if (_sprites.TryGet(sloopName, out Sprite sloopSprite))
            {
                DrawSprite(frame, sloopSprite, enemy.X - sloopSprite.Width / 2, enemy.Y - sloopSprite.Height / 2);
                return;
            }
        }
        if (_sprites is not null && enemy.Kind != 3 && enemy.Kind < 4)
        {
            string spriteName = enemy.Kind switch
            {
                1 => "enemy_crown",
                2 => "enemy_tax_seal",
                _ => "enemy_guard",
            };
            if (_sprites.TryGet(spriteName, out Sprite sprite))
            {
                DrawSprite(frame, sprite, enemy.X - (sprite.Width / 2), enemy.Y - (sprite.Height / 2));
                return;
            }
        }

        if (enemy.Kind == 4)
        {
            int attackCycle = (_missionFrame + enemy.BridgeGroup) % 150;
            bool revealed = attackCycle is >= 78 and <= 108;
            if (_sprites is not null && _sprites.TryGet("snapphane_mist_drone", out Sprite mistDrone))
            {
                if (revealed)
                {
                    DrawSprite(frame, mistDrone, enemy.X - mistDrone.Width / 2, enemy.Y - mistDrone.Height / 2);
                }
                return;
            }
            uint hull = revealed ? 0xff253b32 : 0xff111916;
            uint copper = revealed ? 0xffa66b3f : 0xff3b3028;
            uint signal = 0xff65c58a;
            if (revealed)
            {
                FillTriangle(frame, enemy.X, enemy.Y - enemy.Radius, enemy.X - enemy.Radius, enemy.Y + enemy.Radius,
                    enemy.X + enemy.Radius, enemy.Y + enemy.Radius, hull);
                DrawLine(frame, enemy.X - enemy.Radius + 2, enemy.Y + 4, enemy.X + enemy.Radius - 2, enemy.Y + 4, copper);
                DrawRect(frame, enemy.X - 3, enemy.Y - 4, 6, 7, 0xff0d1211);
            }
            else
            {
                DrawLine(frame, enemy.X - 5, enemy.Y + 3, enemy.X, enemy.Y - 5, hull);
                DrawLine(frame, enemy.X, enemy.Y - 5, enemy.X + 5, enemy.Y + 3, hull);
            }
            if (revealed)
            {
                PutPixel(frame, enemy.X, enemy.Y - 2, signal);
                BlendPixel(frame, enemy.X, enemy.Y - 2, signal, 140);
            }
        }
        else if (enemy.Kind == 5)
        {
            string bargeName = enemy.Health <= 3 ? "fogde_convoy_barge_damaged" : "fogde_convoy_barge";
            if (_sprites?.TryGet(bargeName, out Sprite barge) == true)
            {
                DrawSprite(frame, barge, enemy.X - barge.Width / 2, enemy.Y - barge.Height / 2);
                return;
            }
            DrawRect(frame, enemy.X - enemy.Radius, enemy.Y - 7, enemy.Radius * 2 + 1, 15, 0xff2b3033);
            FillTriangle(frame, enemy.X, enemy.Y + enemy.Radius, enemy.X - enemy.Radius, enemy.Y + 5,
                enemy.X + enemy.Radius, enemy.Y + 5, 0xff171c20);
            DrawRect(frame, enemy.X - enemy.Radius + 2, enemy.Y - 5, enemy.Radius * 2 - 3, 5, danishRed);
            DrawRect(frame, enemy.X - 2, enemy.Y - 7, 4, 15, danishWhite);
            DrawRect(frame, enemy.X - 7, enemy.Y + 1, 5, 4, 0xffb88745);
            DrawRect(frame, enemy.X + 3, enemy.Y + 1, 5, 4, 0xffb88745);
        }
        else if (enemy.Kind == 6)
        {
            int attackCycle = (_missionFrame + enemy.BridgeGroup) % 120;
            string ravenName = enemy.Health <= 5
                ? "glimminge_iron_raven_damaged"
                : attackCycle is >= 45 and <= 76 ? "glimminge_iron_raven_attack" : "glimminge_iron_raven";
            if (_sprites?.TryGet(ravenName, out Sprite raven) == true)
            {
                DrawSprite(frame, raven, enemy.X - raven.Width / 2, enemy.Y - raven.Height / 2);
                return;
            }
            FillTriangle(frame, enemy.X, enemy.Y - 14, enemy.X - 12, enemy.Y + 11,
                enemy.X + 12, enemy.Y + 11, 0xff202527);
            DrawRect(frame, enemy.X - 11, enemy.Y + 3, 22, 4, danishRed);
            DrawRect(frame, enemy.X - 2, enemy.Y - 9, 4, 18, danishWhite);
        }
        else if (enemy.Kind == 3)
        {
            DrawFogdeSloop(frame, enemy, brass, danishRed, danishWhite, dark);
        }
        else if (enemy.Kind == 1)
        {
            FillCircle(frame, enemy.X, enemy.Y, enemy.Radius, danishRed);
            DrawRect(frame, enemy.X - enemy.Radius + 2, enemy.Y - 2, enemy.Radius * 2 - 4, 4, danishWhite);
            DrawRect(frame, enemy.X - 3, enemy.Y - enemy.Radius + 2, 4, enemy.Radius * 2 - 4, danishWhite);
            DrawRect(frame, enemy.X - enemy.Radius + 1, enemy.Y - enemy.Radius + 1, enemy.Radius * 2 - 2, 1, brass);
        }
        else if (enemy.Kind == 2)
        {
            FillCircle(frame, enemy.X, enemy.Y, enemy.Radius, danishDark);
            DrawRect(frame, enemy.X - enemy.Radius + 3, enemy.Y - 4, enemy.Radius * 2 - 6, 8, danishRed);
            DrawLine(frame, enemy.X - enemy.Radius + 2, enemy.Y - enemy.Radius + 2, enemy.X + enemy.Radius - 2, enemy.Y + enemy.Radius - 2, danishWhite);
            DrawLine(frame, enemy.X + enemy.Radius - 2, enemy.Y - enemy.Radius + 2, enemy.X - enemy.Radius + 2, enemy.Y + enemy.Radius - 2, danishWhite);
            DrawRect(frame, enemy.X - 2, enemy.Y - enemy.Radius - 6, 4, 6, brass);
            DrawRect(frame, enemy.X - 1, enemy.Y - enemy.Radius - 9, 3, 4, danishRed);
        }
        else
        {
            FillCircle(frame, enemy.X, enemy.Y, enemy.Radius, enemy.Color);
            DrawRect(frame, enemy.X - 4, enemy.Y - enemy.Radius - 6, 8, 8, dark);
            DrawRect(frame, enemy.X - 2, enemy.Y - enemy.Radius - 9, 4, 4, 0xffd6b25e);
            DrawRect(frame, enemy.X - enemy.Radius - 3, enemy.Y - 1, 6, 3, brass);
            DrawRect(frame, enemy.X + enemy.Radius - 3, enemy.Y - 1, 6, 3, brass);
            DrawRect(frame, enemy.X - enemy.Radius + 2, enemy.Y - 1, enemy.Radius * 2 - 4, 3, danishWhite);
            DrawRect(frame, enemy.X - 3, enemy.Y - enemy.Radius + 2, 3, enemy.Radius * 2 - 4, danishWhite);
        }

        PutPixel(frame, enemy.X - 3, enemy.Y - 2, 0xffffc46b);
        PutPixel(frame, enemy.X + 3, enemy.Y - 2, 0xffffc46b);
        DrawRect(frame, enemy.X - 2, enemy.Y + enemy.Radius - 1, 4, 3, 0xff101820);
    }

    private void DrawFogdeSloop(uint[] frame, Enemy enemy, uint brass, uint red, uint white, uint dark)
    {
        uint hull = enemy.Breakaway ? 0xffa52a3d : 0xff751b2c;
        FillTriangle(frame, enemy.X, enemy.Y + 13, enemy.X - 10, enemy.Y - 8, enemy.X + 10, enemy.Y - 8, hull);
        DrawRect(frame, enemy.X - 13, enemy.Y - 5, 26, 7, red);
        DrawRect(frame, enemy.X - 2, enemy.Y - 9, 4, 18, white);
        DrawRect(frame, enemy.X - 12, enemy.Y - 2, 24, 3, white);
        DrawRect(frame, enemy.X - 4, enemy.Y - 13, 8, 5, dark);
        DrawCrown(frame, enemy.X - 2, enemy.Y - 12, brass);
        PutPixel(frame, enemy.X - 7, enemy.Y + 4, 0xffff8a4a);
        PutPixel(frame, enemy.X + 7, enemy.Y + 4, 0xffff8a4a);
        if (!enemy.Breakaway)
        {
            DrawLine(frame, enemy.X - 10, enemy.Y + 10, enemy.X + 10, enemy.Y + 10, 0xff8ba8b8);
        }
    }

    private void DrawHud(uint[] frame)
    {
        DrawText(frame, 6, 5, "KARL CCLV", 0xffffd66b);
        DrawText(frame, (_width - 78) / 2, 5, "STORMAKT 3020", 0xff7fc7ff);
        DrawText(frame, _width - 116, 5, "POÄNG " + _score.ToString("000000"), 0xff7fc7ff);
        DrawText(frame, 6, _height - 9, "LIV " + _lives, 0xffff6b7f);
        DrawText(frame, (_width - 114) / 2, _height - 9, "Z ELD  X BREDSIDA", 0xffb7c7d6);
        int heatX = _width - 52;
        DrawRect(frame, heatX, _height - 8, 44, 4, 0xff2a3440);
        DrawRect(frame, heatX, _height - 8, Math.Clamp(_heat, 0, 120) * 44 / 120, 4, _heat > 80 ? 0xffff6b4a : 0xffffd66b);
    }

    private void DrawBossHud(uint[] frame)
    {
        if (_glimminge is GlimmingeState glimminge && glimminge.Age >= 150 && glimminge.Phase < 3)
        {
            int glimmingeHudX = (_width - 214) / 2;
            DrawRect(frame, glimmingeHudX, 18, 214, 16, 0xff090e0e);
            DrawText(frame, glimmingeHudX + 8, 21, "GLIMMINGE JÄRN", 0xffd8d2c5);
            DrawRect(frame, glimmingeHudX + 111, 23, 94, 5, 0xff24292b);
            DrawRect(frame, glimmingeHudX + 111, 23, Math.Clamp(glimminge.Health, 0, GlimmingeMaxHealth) * 94 / GlimmingeMaxHealth, 5,
                glimminge.Phase == 1 ? 0xff8f2635 : 0xffa66b3f);
            return;
        }
        BossState? boss = _boss;
        if (boss is null || boss.Age < 500)
        {
            return;
        }
        int hudX = (_width - 206) / 2;
        DrawRect(frame, hudX, 18, 206, 16, 0xff080f18);
        DrawText(frame, hudX + 10, 21, "KRONENS TIENDE", 0xffffd6b0);
        DrawRect(frame, hudX + 101, 23, 96, 5, 0xff2a3036);
        DrawRect(frame, hudX + 101, 23, Math.Clamp(boss.Health, 0, BossPhaseOneHealth) * 96 / BossPhaseOneHealth, 5, 0xffc51f35);
        if (boss.LeftCannonHealth <= 0)
        {
            DrawText(frame, hudX + 3, 27, "V", 0xff62696c);
        }
        if (boss.RightCannonHealth <= 0)
        {
            DrawText(frame, hudX + 191, 27, "H", 0xff62696c);
        }
    }

    private void DrawBossIntroduction(uint[] frame)
    {
        if (_glimminge is GlimmingeState glimminge && glimminge.Age is >= 55 and < 150)
        {
            int glimmingePanelX = (_width - 230) / 2;
            DrawRect(frame, glimmingePanelX, 78, 230, 40, 0xff090d0d);
            DrawLine(frame, glimmingePanelX, 78, glimmingePanelX + 229, 78, 0xff8f2635);
            DrawLine(frame, glimmingePanelX, 117, glimmingePanelX + 229, 117, 0xffa66b3f);
            DrawText(frame, glimmingePanelX + 43, 88, "GLIMMINGE JÄRN", 0xffd8d2c5);
            DrawText(frame, glimmingePanelX + 49, 103, "FOGDEGALJON", 0xffa66b3f);
            return;
        }
        BossState? boss = _boss;
        if (boss is null)
        {
            return;
        }
        if (boss.Age is >= 320 and < 500)
        {
            int panelX = (_width - 230) / 2;
            DrawRect(frame, panelX, 78, 230, 40, 0xff090d12);
            DrawLine(frame, panelX, 78, panelX + 229, 78, 0xffc51f35);
            DrawLine(frame, panelX, 117, panelX + 229, 117, 0xffd6b25e);
            DrawText(frame, panelX + 46, 88, "KRONENS TIENDE", 0xffffd6b0);
            DrawText(frame, panelX + 71, 103, "FOGDESKEPP", 0xfff2eee4);
        }
        else if (boss.Phase == 2 && boss.PhaseAge < 180)
        {
            int panelX = (_width - 184) / 2;
            DrawRect(frame, panelX, 88, 184, 27, 0xff160b0e);
            DrawText(frame, panelX + 20, 98, "BROFOGDENS VREDE", 0xffff6b62);
        }
        else if (boss.Phase == 3 && boss.PhaseAge is >= 90 and < 180)
        {
            int panelX = (_width - 162) / 2;
            DrawRect(frame, panelX, 88, 162, 27, 0xff160b0e);
            DrawText(frame, panelX + 31, 98, "TIONDET BRISTER", 0xffff8a4a);
        }
    }

    private void DrawStageClear(uint[] frame)
    {
        if (!_stageClear)
        {
            return;
        }
        int panelX = (_width - 196) / 2;
        if (_levelId == 1)
        {
            DrawGlimmingeResultWreck(frame, panelX + 196, 67);
        }
        else
        {
            DrawSnapphaneSilhouette(frame, panelX + 196, 67);
        }
        int reveal = Math.Min(150, _stageClearAge);
        int width = 196 * reveal / 150;
        DrawRect(frame, panelX, 86, width, 45, 0xff080d12);
        if (_stageClearAge >= 45)
        {
            DrawLine(frame, panelX, 86, panelX + 195, 86, 0xffd6b25e);
            DrawLine(frame, panelX, 130, panelX + 195, 130, 0xff2f74c9);
            if (_levelId == 1)
            {
                DrawText(frame, panelX + 39, 99, "SKUGGORNA SVARAR", 0xffffd66b);
                DrawText(frame, panelX + 42, 116, "SIGNALEN FUNNEN", 0xff65c58a);
            }
            else
            {
                DrawText(frame, panelX + 51, 99, "BÄLTET ÄR ÖPPET", 0xffffd66b);
                DrawText(frame, panelX + 39, 116, "KRONARKIV SÄKRAT", 0xff9bd4dc);
            }
        }
    }

    private void DrawGlimmingeResultWreck(uint[] frame, int x, int y)
    {
        int settle = Math.Min(18, _stageClearAge / 5);
        if (_sprites?.TryGet("glimminge_wreck", out Sprite wreck) == true)
        {
            DrawSprite(frame, wreck, x - wreck.Width / 2, y - wreck.Height / 2 + settle);
        }
        else
        {
            DrawRect(frame, x - 34, y - 8 + settle, 28, 15, 0xff292d2f);
            DrawRect(frame, x - 2, y - 3 + settle, 24, 13, 0xff3a3030);
            DrawRect(frame, x + 25, y + 2 + settle, 17, 11, 0xff202527);
            DrawLine(frame, x - 29, y - 7 + settle, x - 8, y + 5 + settle, 0xff8f2635);
        }
        if (_stageClearAge is >= 70 and < 105 && (_stageClearAge / 5 & 1) == 0)
        {
            FillCircle(frame, x + 7, y - 8 + settle, 2, 0xff65c58a);
        }
    }

    private void DrawSnapphaneSilhouette(uint[] frame, int x, int y)
    {
        int flicker = ((_stageClearAge / 7) & 1) == 0 ? 0 : 2;
        uint blackCopper = flicker == 0 ? 0xff171719 : 0xff251d1a;
        FillTriangle(frame, x, y - 15, x - 17, y + 12, x + 17, y + 12, blackCopper);
        FillTriangle(frame, x - 5, y - 3, x - 25, y + 8, x - 8, y + 14, 0xff2a201c);
        FillTriangle(frame, x + 5, y - 3, x + 25, y + 8, x + 8, y + 14, 0xff2a201c);
        DrawLine(frame, x - 20, y + 9, x + 20, y + 9, 0xff765139);
        DrawRect(frame, x - 3, y - 5, 6, 5, 0xff315b42);
        PutPixel(frame, x - 8, y + 13, 0xff7a4d31);
        PutPixel(frame, x + 8, y + 13, 0xff7a4d31);
    }

    private void DrawMissionTitle(uint[] frame)
    {
        if (_missionFrame < 15 || _missionFrame >= 165)
        {
            return;
        }

        int edge = Math.Min(_missionFrame - 14, 165 - _missionFrame);
        int y = 73 - Math.Min(12, edge);
        int panelX = (_width - 272) / 2;
        DrawRect(frame, panelX, y, 272, 39, 0xff080f18);
        DrawLine(frame, panelX, y, panelX + 271, y, 0xff8a6b38);
        DrawLine(frame, panelX, y + 38, panelX + 271, y + 38, 0xff2f74c9);
        if (_levelId == 1)
        {
            DrawText(frame, panelX + 92, y + 8, "DEN SVARTA SKOGEN", 0xffffd66b);
            DrawText(frame, panelX + 88, y + 22, "SKÅNSKA SKUGGOR", 0xff65c58a);
        }
        else
        {
            DrawText(frame, panelX + 95, y + 8, "ÅTERTÅGET ÖVER", 0xffffd66b);
            DrawText(frame, panelX + 73, y + 22, "STORA BÄLT NEBULOSAN", 0xff9bd4dc);
        }
    }

    private void DrawLevelSelect(uint[] frame)
    {
        int panelWidth = Math.Min(340, _width - 24);
        int panelX = (_width - panelWidth) / 2;
        bool legacy = _height <= 224;
        int panelTop = legacy ? 67 : 103;
        int panelBottom = _height - 12;
        int listY = legacy ? 82 : 120;
        int rowHeight = legacy ? 18 : 21;
        string logoName = legacy ? "stormakt_logo_legacy" : "stormakt_logo_wide";
        if (_sprites?.TryGet(logoName, out Sprite logo) == true)
        {
            DrawSprite(frame, logo, (_width - logo.Width) / 2, legacy ? 0 : 1);
        }
        else
        {
            DrawText(frame, (_width - 78) / 2, legacy ? 22 : 45, "STORMAKT 3020", 0xffffd66b);
        }
        DrawRect(frame, panelX, panelTop, panelWidth, panelBottom - panelTop + 1, 0xff080d12);
        DrawLine(frame, panelX, panelTop, panelX + panelWidth - 1, panelTop, 0xffffd66b);
        DrawLine(frame, panelX, panelBottom, panelX + panelWidth - 1, panelBottom, 0xff2f74c9);
        DrawLine(frame, panelX + 12, panelTop + 8, panelX + 102, panelTop + 8, 0xff8a6b38);
        DrawLine(frame, panelX + panelWidth - 103, panelTop + 8, panelX + panelWidth - 13, panelTop + 8, 0xff8a6b38);
        DrawText(frame, (_width - 72) / 2, panelTop + 4, "VÄLJ FÄLTTÅG", 0xff9bd4dc);

        for (int index = 0; index < CampaignNames.Length; index++)
        {
            string status = index == 0 ? "STRID" : _developerMode ? "DEV" : "LÅST";
            DrawLevelOption(frame, panelX + 12, listY + index * rowHeight, panelWidth - 24,
                rowHeight - 2, index, $"{index + 1}  {CampaignNames[index]}", status);
        }

        string footer = _lockedLevelNoticeFrames > 0 ? "FÄLTTÅGET ÄR LÅST" :
            _developerMode ? "UTVECKLARLÄGE  ALLT UPPLÅST" : "UPP NER VÄLJ  START";
        DrawText(frame, (_width - footer.Length * 6) / 2, panelBottom - 9, footer,
            _lockedLevelNoticeFrames > 0 ? 0xff65c58a : 0xffb7c7d6);
    }

    private void DrawLevelOption(uint[] frame, int x, int y, int width, int height, int index, string title, string status)
    {
        bool selected = _levelSelection == index;
        uint border = selected ? 0xffffd66b : 0xff344d5c;
        uint fill = selected ? 0xff172536 : 0xff0d151d;
        DrawRect(frame, x, y, width, height, fill);
        DrawLine(frame, x, y, x + width - 1, y, border);
        DrawLine(frame, x, y + height - 1, x + width - 1, y + height - 1, border);
        DrawRect(frame, x + 5, y + 4, 3, Math.Max(3, height - 8), selected ? 0xffffd66b : 0xff293d4b);
        int textY = y + (height - 7) / 2;
        DrawText(frame, x + 14, textY, title, selected ? 0xffffffff : 0xff91a7b5);
        uint statusColor = status == "LÅST" ? 0xff697680 : status == "DEV" ? 0xff65c58a : 0xff7fc7ff;
        DrawText(frame, x + width - status.Length * 6 - 8, textY, status, statusColor);
    }

    private void DrawLevelPreview(uint[] frame)
    {
        int panelWidth = Math.Min(330, _width - 28);
        const int panelHeight = 126;
        int x = (_width - panelWidth) / 2;
        int y = (_height - panelHeight) / 2;
        DrawRect(frame, x, y, panelWidth, panelHeight, 0xee080d12);
        DrawLine(frame, x, y, x + panelWidth - 1, y, 0xffffd66b);
        DrawLine(frame, x, y + panelHeight - 1, x + panelWidth - 1, y + panelHeight - 1, 0xff2f74c9);
        DrawText(frame, x + 14, y + 13, $"FÄLTTÅG {_previewLevel + 1}", 0xff9bd4dc);
        DrawText(frame, x + 14, y + 32, CampaignNames[_previewLevel], 0xffffd66b);
        DrawLine(frame, x + 14, y + 48, x + panelWidth - 15, y + 48, 0xff344d5c);
        DrawText(frame, x + 14, y + 61, "UTVECKLARPREVIEW", 0xff65c58a);
        DrawText(frame, x + 14, y + 78, "TIDSLINJE OCH BOSS EJ BYGGDA", 0xffb7c7d6);
        DrawText(frame, x + 14, y + 103, "START ELLER ELD  ÅTER", 0xffffffff);
    }

    private void DrawPause(uint[] frame)
    {
        if (!_paused)
        {
            return;
        }
        const int width = 176;
        const int height = 42;
        int x = (_width - width) / 2;
        int y = (_height - height) / 2;
        DrawRect(frame, x, y, width, height, 0xee080d12);
        DrawLine(frame, x, y, x + width - 1, y, 0xffffd66b);
        DrawLine(frame, x, y + height - 1, x + width - 1, y + height - 1, 0xff2f74c9);
        DrawText(frame, x + 73, y + 9, "PAUS", 0xffffd66b);
        DrawText(frame, x + 34, y + 25, "START FORTSÄTTER", 0xffdce8f2);
    }

    private void DrawRadio(uint[] frame)
    {
        if (_bossRadioCard is RadioCard bossCard && _bossRadioAge < bossCard.DurationFrames)
        {
            DrawRadioCard(frame, bossCard, _bossRadioAge);
            return;
        }
        ReadOnlySpan<RadioCard> cards = _levelId == 1 ? SkanskaRadioCards : RadioCards;
        foreach (RadioCard card in cards)
        {
            int elapsed = _missionFrame - card.StartFrame;
            if (elapsed < 0 || elapsed >= card.DurationFrames)
            {
                continue;
            }
            DrawRadioCard(frame, card, elapsed);
            return;
        }
    }

    private void DrawRadioCard(uint[] frame, RadioCard card, int elapsed)
    {
        const int width = 138;
        const int height = 48;
        int slide = Math.Min(8, Math.Min(elapsed + 1, card.DurationFrames - elapsed));
        int x = card.Enemy ? _width - (width * slide / 8) - 2 : -width + (width * slide / 8) + 2;
        const int y = 18;
        uint frameColor = card.Snapphane ? 0xff87583a : card.Enemy ? 0xffc92f42 : 0xff2f74c9;
        uint lampColor = card.Snapphane ? 0xff65c58a : card.Enemy ? 0xfff4f1e8 : 0xffffd66b;
        uint uniformColor = card.Snapphane ? 0xff27332b : card.Enemy ? 0xff8f1f31 : 0xff245ca5;

        DrawRect(frame, x, y, width, height, 0xff081019);
        DrawLine(frame, x, y, x + width - 1, y, frameColor);
        DrawLine(frame, x, y + height - 1, x + width - 1, y + height - 1, frameColor);
        DrawLine(frame, x, y, x, y + height - 1, frameColor);
        DrawLine(frame, x + width - 1, y, x + width - 1, y + height - 1, frameColor);
        DrawRect(frame, x + 2, y + 2, 38, 38, 0xff162331);
        bool speaking = elapsed >= 8 && elapsed < card.DurationFrames - 20 && ((elapsed / 8) & 1) != 0;
        string portraitName = card.PortraitBase + (speaking ? "_speak" : "_neutral");
        if (_sprites?.TryGet(portraitName, out Sprite portrait) == true)
        {
            DrawSprite(frame, portrait, x + 2 + (38 - portrait.Width) / 2, y + 2 + (38 - portrait.Height) / 2);
        }
        else
        {
            DrawRadioPortrait(frame, x + 21, y + 21, uniformColor, lampColor, card.Enemy);
        }
        DrawRect(frame, x + 42, y + 3, 93, 9, frameColor);
        DrawRect(frame, x + 44, y + 5, 3, 3, lampColor);
        DrawText(frame, x + 50, y + 4, card.Speaker, 0xffffffff);
        DrawText(frame, x + 43, y + 18, card.Line1, 0xffdce8f2);
        DrawText(frame, x + 43, y + 30, card.Line2, 0xffdce8f2);
        for (int scanY = y + 2; scanY < y + height - 1; scanY += 4)
        {
            for (int scanX = x + 2; scanX <= x + 39; scanX++)
            {
                BlendPixel(frame, scanX, scanY, 0xff081019, 54);
            }
        }
    }

    private void DrawRadioPortrait(uint[] frame, int x, int y, uint uniform, uint accent, bool enemy)
    {
        FillCircle(frame, x, y - 5, 8, 0xffd2a477);
        DrawRect(frame, x - 9, y + 3, 18, 13, uniform);
        DrawRect(frame, x - 5, y - 13, 10, 3, enemy ? 0xfff4f1e8 : 0xffffd66b);
        DrawRect(frame, x - 8, y - 10, 16, 2, uniform);
        DrawRect(frame, x - 4, y - 5, 2, 2, 0xff101820);
        DrawRect(frame, x + 2, y - 5, 2, 2, 0xff101820);
        DrawRect(frame, x - 2, y, 5, 1, 0xff713947);
        DrawRect(frame, x - 8, y + 7, 4, 2, accent);
        DrawRect(frame, x + 4, y + 7, 4, 2, accent);
    }

    private void DrawCrown(uint[] frame, int x, int y, uint color)
    {
        PutPixel(frame, x, y + 1, color);
        PutPixel(frame, x + 1, y, color);
        PutPixel(frame, x + 2, y + 1, color);
        PutPixel(frame, x + 3, y, color);
        PutPixel(frame, x + 4, y + 1, color);
        DrawRect(frame, x, y + 2, 5, 2, color);
        PutPixel(frame, x + 2, y + 1, 0xffffec9a);
    }

    private void DrawSprite(uint[] frame, Sprite sprite, int x, int y)
    {
        for (int sy = 0; sy < sprite.Height; sy++)
        {
            int py = y + sy;
            if ((uint)py >= _height)
            {
                continue;
            }

            for (int sx = 0; sx < sprite.Width; sx++)
            {
                int px = x + sx;
                if ((uint)px >= _width)
                {
                    continue;
                }

                uint src = sprite.Pixels[(sy * sprite.Width) + sx];
                uint alpha = src >> 24;
                if (alpha == 0)
                {
                    continue;
                }

                int index = (py * _width) + px;
                if (alpha >= 250)
                {
                    frame[index] = src;
                    continue;
                }

                uint dst = frame[index];
                uint inv = 255 - alpha;
                uint r = (((src >> 16) & 0xff) * alpha + ((dst >> 16) & 0xff) * inv) / 255;
                uint g = (((src >> 8) & 0xff) * alpha + ((dst >> 8) & 0xff) * inv) / 255;
                uint b = ((src & 0xff) * alpha + (dst & 0xff) * inv) / 255;
                frame[index] = 0xff000000u | (r << 16) | (g << 8) | b;
            }
        }
    }

    private void DrawSpriteAlpha(uint[] frame, Sprite sprite, int x, int y, uint opacity)
    {
        opacity = Math.Min(255u, opacity);
        for (int sy = 0; sy < sprite.Height; sy++)
        {
            int py = y + sy;
            if ((uint)py >= _height)
            {
                continue;
            }
            for (int sx = 0; sx < sprite.Width; sx++)
            {
                int px = x + sx;
                if ((uint)px >= _width)
                {
                    continue;
                }
                uint src = sprite.Pixels[(sy * sprite.Width) + sx];
                uint alpha = (src >> 24) * opacity / 255;
                if (alpha == 0)
                {
                    continue;
                }
                BlendPixel(frame, px, py, src, alpha);
            }
        }
    }

    private void DrawSpriteFlippedXAlpha(uint[] frame, Sprite sprite, int x, int y, uint opacity)
    {
        opacity = Math.Min(255u, opacity);
        for (int sy = 0; sy < sprite.Height; sy++)
        {
            int py = y + sy;
            if ((uint)py >= _height)
            {
                continue;
            }
            for (int sx = 0; sx < sprite.Width; sx++)
            {
                int px = x + sx;
                if ((uint)px >= _width)
                {
                    continue;
                }
                uint src = sprite.Pixels[(sy * sprite.Width) + (sprite.Width - 1 - sx)];
                uint alpha = (src >> 24) * opacity / 255;
                if (alpha == 0)
                {
                    continue;
                }
                BlendPixel(frame, px, py, src, alpha);
            }
        }
    }

    private void DrawSpriteFlippedX(uint[] frame, Sprite sprite, int x, int y)
    {
        for (int sy = 0; sy < sprite.Height; sy++)
        {
            int py = y + sy;
            if ((uint)py >= _height)
            {
                continue;
            }

            for (int sx = 0; sx < sprite.Width; sx++)
            {
                int px = x + sx;
                if ((uint)px >= _width)
                {
                    continue;
                }

                uint src = sprite.Pixels[(sy * sprite.Width) + (sprite.Width - 1 - sx)];
                uint alpha = src >> 24;
                if (alpha == 0)
                {
                    continue;
                }

                int index = py * _width + px;
                if (alpha == 255)
                {
                    frame[index] = src;
                    continue;
                }

                uint dst = frame[index];
                uint inv = 255 - alpha;
                uint r = (((src >> 16) & 0xff) * alpha + ((dst >> 16) & 0xff) * inv) / 255;
                uint g = (((src >> 8) & 0xff) * alpha + ((dst >> 8) & 0xff) * inv) / 255;
                uint b = ((src & 0xff) * alpha + (dst & 0xff) * inv) / 255;
                frame[index] = 0xff000000u | (r << 16) | (g << 8) | b;
            }
        }
    }

    private void Clear(uint[] frame, uint color) => Array.Fill(frame, color);

    private void DrawText(uint[] frame, int x, int y, string text, uint color)
    {
        int cursor = x;
        foreach (char raw in text)
        {
            char ch = char.ToUpperInvariant(raw);
            if (ch == ' ')
            {
                cursor += 4;
                continue;
            }
            ReadOnlySpan<byte> glyph = Glyph(ch);
            for (int gy = 0; gy < glyph.Length; gy++)
            {
                byte row = glyph[gy];
                for (int gx = 0; gx < 5; gx++)
                {
                    if ((row & (1 << (4 - gx))) != 0)
                    {
                        PutPixel(frame, cursor + gx, y + gy, color);
                    }
                }
            }
            cursor += 6;
        }
    }

    private static ReadOnlySpan<byte> Glyph(char ch) => ch switch
    {
        'A' => [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        'Å' => [0b00100, 0b01010, 0b01110, 0b10001, 0b11111, 0b10001, 0b10001],
        'Ä' => [0b01010, 0b00000, 0b01110, 0b10001, 0b11111, 0b10001, 0b10001],
        'Æ' => [0b01111, 0b10100, 0b10100, 0b11110, 0b10100, 0b10100, 0b10111],
        'B' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        'C' => [0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111],
        'D' => [0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110],
        'E' => [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        'F' => [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        'G' => [0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111],
        'H' => [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        'I' => [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111],
        'J' => [0b00111, 0b00010, 0b00010, 0b00010, 0b10010, 0b10010, 0b01100],
        'K' => [0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001],
        'L' => [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
        'M' => [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        'N' => [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
        'O' => [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        'Ö' => [0b01010, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110],
        'Ø' => [0b01111, 0b10011, 0b10101, 0b10101, 0b11001, 0b10001, 0b11110],
        'P' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
        'R' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
        'S' => [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
        'T' => [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        'U' => [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        'V' => [0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b01010, 0b00100],
        'W' => [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001],
        'X' => [0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b01010, 0b10001],
        'Y' => [0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        'Z' => [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
        '0' => [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        '1' => [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        '2' => [0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111],
        '3' => [0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110],
        '4' => [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        '5' => [0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110],
        '6' => [0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        '7' => [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        '8' => [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        '9' => [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110],
        _ => [0b11111, 0b00001, 0b00110, 0b00100, 0b00100, 0b00000, 0b00100],
    };

    private void DrawLine(uint[] frame, int x0, int y0, int x1, int y1, uint color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            PutPixel(frame, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void FillTriangle(uint[] frame, int x0, int y0, int x1, int y1, int x2, int y2, uint color)
    {
        int minX = Math.Max(0, Math.Min(x0, Math.Min(x1, x2)));
        int maxX = Math.Min(_width - 1, Math.Max(x0, Math.Max(x1, x2)));
        int minY = Math.Max(0, Math.Min(y0, Math.Min(y1, y2)));
        int maxY = Math.Min(_height - 1, Math.Max(y0, Math.Max(y1, y2)));
        int area = Edge(x0, y0, x1, y1, x2, y2);
        if (area == 0)
        {
            return;
        }
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int w0 = Edge(x1, y1, x2, y2, x, y);
                int w1 = Edge(x2, y2, x0, y0, x, y);
                int w2 = Edge(x0, y0, x1, y1, x, y);
                if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0))
                {
                    PutPixel(frame, x, y, color);
                }
            }
        }
    }

    private static int Edge(int ax, int ay, int bx, int by, int px, int py) => (px - ax) * (by - ay) - (py - ay) * (bx - ax);

    private void FillCircle(uint[] frame, int centerX, int centerY, int radius, uint color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= r2)
                {
                    PutPixel(frame, centerX + x, centerY + y, color);
                }
            }
        }
    }

    private void DrawRect(uint[] frame, int x, int y, int w, int h, uint color)
    {
        for (int py = Math.Max(0, y); py < Math.Min(_height, y + h); py++)
        {
            int row = py * _width;
            for (int px = Math.Max(0, x); px < Math.Min(_width, x + w); px++)
            {
                frame[row + px] = color;
            }
        }
    }

    private void PutPixel(uint[] frame, int x, int y, uint color)
    {
        if ((uint)x >= _width || (uint)y >= _height)
        {
            return;
        }
        frame[(y * _width) + x] = color;
    }

    private void BlendPixel(uint[] frame, int x, int y, uint color, uint alpha)
    {
        if ((uint)x >= _width || (uint)y >= _height)
        {
            return;
        }
        int index = (y * _width) + x;
        uint destination = frame[index];
        uint inverse = 255 - alpha;
        uint r = (((color >> 16) & 0xff) * alpha + ((destination >> 16) & 0xff) * inverse) / 255;
        uint g = (((color >> 8) & 0xff) * alpha + ((destination >> 8) & 0xff) * inverse) / 255;
        uint b = ((color & 0xff) * alpha + (destination & 0xff) * inverse) / 255;
        frame[index] = 0xff000000u | (r << 16) | (g << 8) | b;
    }

    private record struct Shot(int X, int Y, int Vx, int Vy, uint Color, int Power);
    private record struct EnemyShot(double X, double Y, double Vx, double Vy, int Kind);
    private record struct AnchorHazard(int X, int Age);
    private record struct CrystalSpear(int X, int Age);
    private record struct Enemy(
        int X,
        int Y,
        int Speed,
        int Radius,
        int Health,
        uint Color,
        int Kind,
        double Phase,
        int BridgeGroup,
        int EscortOffset,
        bool Breakaway);
    private record struct GroundTarget(
        int X,
        int Y,
        GroundTargetType Type,
        int Health,
        int Group,
        int Age,
        bool Enabled,
        int CollapseFrames);
    private readonly record struct Star(int X, int Y, int Speed, int Brightness);
    private readonly record struct RadioCard(
        int StartFrame,
        int DurationFrames,
        bool Enemy,
        string Speaker,
        string Line1,
        string Line2,
        StormaktVoice? Voice,
        string PortraitBase,
        bool Snapphane = false);
    private readonly record struct EnemyWave(
        int StartFrame,
        int EndFrame,
        int IntervalFrames,
        int Kind,
        int Count);

    private enum GroundTargetType
    {
        BridgeSpan,
        Turret,
        EnergyNode,
        SignalBeacon,
    }

    private sealed class SorenRivalState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Health { get; set; }
        public int Age { get; set; }
        public bool Interrupted { get; set; }
        public int InterruptAge { get; set; }
    }

    private sealed class GlimmingeState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Health { get; set; }
        public int Age { get; set; }
        public int Phase { get; set; }
        public int PhaseAge { get; set; }
        public int BurningTransitionAge { get; set; }
        public double PhaseTransitionX { get; set; }
    }

    private sealed class BossState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Health { get; set; }
        public int LeftCannonHealth { get; set; }
        public int RightCannonHealth { get; set; }
        public int LeftDockHealth { get; set; }
        public int RightDockHealth { get; set; }
        public int Age { get; set; }
        public int Phase { get; set; }
        public int PhaseAge { get; set; }
        public double RushX { get; set; }
        public double PhaseTransitionX { get; set; }
        public double PhaseTransitionY { get; set; }
    }
}

internal sealed class SpritePack
{
    private const uint Magic = 0x41534657; // WFSA
    private readonly Dictionary<string, Sprite> _sprites;

    private SpritePack(Dictionary<string, Sprite> sprites)
    {
        _sprites = sprites;
    }

    public static SpritePack? LoadDefault()
    {
        string[] paths =
        [
            Path.Combine(Environment.CurrentDirectory, "assets", "stormakt3020", "stormakt3020.wfsa"),
            Path.Combine(AppContext.BaseDirectory, "assets", "stormakt3020", "stormakt3020.wfsa"),
        ];

        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                return Load(path);
            }
        }

        return null;
    }

    public bool TryGet(string name, out Sprite sprite) => _sprites.TryGetValue(name, out sprite);

    private static SpritePack Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> fixedHeader = stackalloc byte[12];
        ReadExactly(stream, fixedHeader);
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader);
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid Stormakt sprite pack: {path}");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader[4..]);
        if (version != 1)
        {
            throw new InvalidDataException($"Unsupported Stormakt sprite pack version: {version}");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(fixedHeader[8..]);
        Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
        byte[] entryHeader = new byte[44];
        for (int i = 0; i < count; i++)
        {
            ReadExactly(stream, entryHeader);
            int nameLength = Array.IndexOf(entryHeader, (byte)0, 0, 32);
            if (nameLength < 0)
            {
                nameLength = 32;
            }

            string name = Encoding.ASCII.GetString(entryHeader, 0, nameLength);
            int width = BinaryPrimitives.ReadInt32LittleEndian(entryHeader.AsSpan(32));
            int height = BinaryPrimitives.ReadInt32LittleEndian(entryHeader.AsSpan(36));
            int byteLength = BinaryPrimitives.ReadInt32LittleEndian(entryHeader.AsSpan(40));
            if (width <= 0 || height <= 0 || byteLength != width * height * sizeof(uint))
            {
                throw new InvalidDataException($"Invalid Stormakt sprite entry: {name}");
            }

            byte[] bytes = new byte[byteLength];
            ReadExactly(stream, bytes);
            uint[] pixels = new uint[width * height];
            MemoryMarshal.Cast<byte, uint>(bytes).CopyTo(pixels);
            sprites[name] = new Sprite(width, height, pixels);
        }

        return new SpritePack(sprites);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}

internal readonly record struct Sprite(int Width, int Height, uint[] Pixels);
