using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.Json;

bool legacyResolution = string.Equals(
    Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_LEGACY_320"), "1", StringComparison.Ordinal);
int Width = legacyResolution ? 320 : 400;
int Height = legacyResolution ? 224 : 280;
const uint FrameMagic = 0x58454657; // WFEX
const byte StepCommand = (byte)'S';
const byte PointerStepCommand = (byte)'P';

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
using var audio = StormaktMusicLoop.TryStartDefault();
var game = new StormaktGame(Width, Height, SpritePack.LoadDefault(), audio);
audio?.Trigger(StormaktSound.Deploy);
var command = new byte[21];
var header = new byte[32];
var frame = new uint[Width * Height];
ulong frameIndex = 0;

while (true)
{
    int marker = input.ReadByte();
    if (marker < 0)
    {
        break;
    }
    command[0] = (byte)marker;
    bool pointerProtocol = command[0] == PointerStepCommand;
    if (command[0] != StepCommand && !pointerProtocol)
    {
        break;
    }
    int commandLength = pointerProtocol ? 21 : 5;
    if (!ReadExact(input, command.AsSpan(1, commandLength - 1)))
    {
        break;
    }

    uint buttons = BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(1));
    var pointer = pointerProtocol
        ? new RtsPointer(
            BinaryPrimitives.ReadInt32LittleEndian(command.AsSpan(5)),
            BinaryPrimitives.ReadInt32LittleEndian(command.AsSpan(9)),
            BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(13)),
            BinaryPrimitives.ReadUInt32LittleEndian(command.AsSpan(17)) != 0)
        : default;
    game.Step(buttons, pointer);
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

internal readonly record struct RtsPointer(int X, int Y, uint Buttons, bool Inside);

internal sealed class StormaktGame
{
    private static readonly string[] CampaignNames =
    [
        "STORA BÄLT",
        "SKÅNSKA SKUGGOR",
        "ÖRESUNDS JÄRNKRONA",
        "SILVERKROPPEN",
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
    private const int RtsSalvagedSilverGoal = 1_200;
    private const int DungeonFirstSigilMask = 1 << 8;
    private const uint Up = 1u << 1;
    private const uint Down = 1u << 2;
    private const uint Left = 1u << 3;
    private const uint Right = 1u << 4;
    private const uint Start = 1u << 5;
    private const uint Fire = 1u << 6;
    private const uint AltFire = 1u << 7;
    private const uint Slow = 1u << 8;
    private const uint QuickPotion = 1u << 9;
    private const uint DropItem = 1u << 10;

    private readonly int _width;
    private readonly int _height;
    private readonly bool _invincibleTestMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_INVINCIBLE"), "1", StringComparison.Ordinal);
    private readonly bool _developerMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DEVELOPER_MODE"), "1", StringComparison.Ordinal);
    private readonly bool _rtsEvacuationTestMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_RTS_EVAC_TEST"), "1", StringComparison.Ordinal);
    private readonly bool _dungeonTestMode = string.Equals(
        Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_TEST"), "1", StringComparison.Ordinal);
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
    private static readonly RadioCard RtsSilverRadio =
        new(0, 390, false, "EBBA GRIP", "MASSIVT SILVER", "LANDA KARL", StormaktVoice.EbbaSilverBody, "portrait_ebba");
    private static readonly RadioCard RtsSteamRadio =
        new(0, 330, false, "EBBA GRIP", "ÅNGAN HÅLLER", "RES KROSSEN", StormaktVoice.EbbaSteamOnline, "portrait_ebba");
    private static readonly RadioCard RtsClaimRadio =
        new(0, 390, true, "FOGDE RASMUS", "KRONENS SØLV!", "STANDS!", StormaktVoice.RasmusSilverClaim, "portrait_rasmus");
    private static readonly RadioCard RtsPowderRadio =
        new(0, 330, false, "EBBA GRIP", "KRUTSVIN!", "SKJUT STUBINEN", StormaktVoice.EbbaPowderWarning, "portrait_ebba");
    private static readonly RadioCard RtsOrganRadio =
        new(0, 330, true, "FOGDE RASMUS", "ORGELVOGN FREM!", "SPIL DEM I GRUS", StormaktVoice.RasmusOrganOrder, "portrait_rasmus");
    private static readonly RadioCard RtsMooseRadio =
        new(0, 330, false, "EBBA GRIP", "ÄLGARNA STAMPAR", "GE DEM RIKTNING", StormaktVoice.EbbaMooseReady, "portrait_ebba");
    private static readonly RadioCard RtsVictoryRadio =
        new(0, 540, false, "EBBA GRIP", "SILVERLASTEN FULL", "TULLHUSET SLAGET", StormaktVoice.EbbaRtsVictory, "portrait_ebba");
    private static readonly RadioCard RtsWaitMinerRadio =
        new(0, 390, false, "EBBA GRIP", "VÄNTA KARL!", "EN FOGDE SAKNAS", StormaktVoice.EbbaRtsWaitMiner, "portrait_ebba");
    private static readonly RadioCard RtsTempleRadio =
        new(0, 330, false, "EBBA GRIP", "ETT TEMPEL...", "LEMMINKÄINENS?", StormaktVoice.EbbaRtsTemple, "portrait_ebba");
    private static readonly RadioCard DungeonDescentRadio =
        new(0, 450, false, "EBBA GRIP", "GÅ NER SJÄLV", "ARBETARNA VÄGRAR", null, "portrait_ebba");
    private static readonly RadioCard DungeonDepthTwoWarningRadio =
        new(0, 510, false, "EBBA GRIP", "ÄR DU SÄKER?", "TELEMETRIN ÄR DÖD", StormaktVoice.EbbaDungeonTelemetry, "portrait_ebba");
    private static readonly RadioCard DungeonNearDeathRadio =
        new(0, 420, false, "EBBA GRIP", "NÄRA ÖGAT", "TA DET LUGNT NU", StormaktVoice.EbbaDungeonNearDeath, "portrait_ebba");
    private static readonly RadioCard DungeonFirstSigilRadio =
        new(0, 450, false, "EBBA GRIP", "JAG HÖR FOGDEN", "TVÅ SIGILL KVAR", null, "portrait_ebba");
    private static readonly RadioCard[] DungeonLoreCards =
    [
        new(0, 330, false, "RISTNING", "SVANEN SJUNGER", "UNDER SVART VATTEN", null, "portrait_ebba"),
        new(0, 330, false, "RISTNING", "SOLEN BRÖTS", "NYCKELN MINNS", null, "portrait_ebba"),
        new(0, 360, false, "RISTNING", "HERDEN SER EJ", "MEN FÅREN VET", null, "portrait_ebba"),
    ];
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
    private RtsState? _rts;
    private DungeonState? _dungeon;
    private bool _stageClear;
    private int _stageClearAge;
    private uint _previousButtons;
    private bool _gameOver;
    private bool _paused;
    private bool _inLevelSelect;
    private bool _inLevelPreview;
    private bool _inSilverkroppenSelect;
    private int _silverkroppenSelection;
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

    public void Step(uint buttons, RtsPointer pointer = default)
    {
        if (_inSilverkroppenSelect)
        {
            StepSilverkroppenSelect(buttons);
            return;
        }
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

        if (_levelId == 3)
        {
            if (_dungeon is not null) StepDungeon(buttons, pointer);
            else StepRts(buttons, pointer);
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
        if (_inSilverkroppenSelect)
        {
            DrawSky(frame);
            DrawNebula(frame);
            DrawSilverkroppenSelect(frame);
            return;
        }
        if (_levelId == 3 && !_inLevelSelect && !_inLevelPreview)
        {
            if (_dungeon is not null) DrawDungeon(frame);
            else
            {
                DrawRts(frame);
                DrawStageClear(frame);
            }
            DrawPause(frame);
            return;
        }
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
        _random = new Random(_levelId switch { 1 => 3202, 3 => 3404, _ => 3020 });
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
        _rts = _levelId == 3
            ? new RtsState
            {
                MapWidth = _width + (_width <= 320 ? 400 : 500),
                MapHeight = _height - 28,
                CursorX = 74,
                CursorY = _height - 82,
                LandingX = 74,
                LandingY = _height - 82,
            }
            : null;
        _dungeon = null;
        if (_rts is RtsState resetRts)
        {
            resetRts.Fortress = new RtsFortress(resetRts.MapWidth - 170, resetRts.MapHeight / 2);
            string[] forestProps = ["rts_spruce_tall", "rts_spruce_bent", "rts_pine_dead", "rts_spruce_crystal",
                "rts_moss_boulders", "rts_silver_outcrop", "rts_forest_shrub", "rts_forest_stump"];
            string[] frontierProps = ["rts_dk_barricade", "rts_dk_lantern", "rts_dk_tripod", "rts_dk_signpost",
                "rts_dk_crates", "rts_dk_minecart", "rts_dk_scorched", "rts_dk_wagon_rut"];
            for (int y = 42; y < resetRts.MapHeight - 18; y += 38)
            {
                for (int x = 22; x < resetRts.MapWidth - 24; x += 44)
                {
                    int jitterX = (x * 17 + y * 31) % 19 - 9;
                    int jitterY = (x * 29 + y * 13) % 13 - 6;
                    int propX = x + jitterX;
                    int propY = y + jitterY;
                    if (DistanceSquared(propX, propY, resetRts.LandingX, resetRts.LandingY) < 72 * 72 ||
                        Math.Abs(propX - SilverVeinWorldX(propY)) < 25)
                    {
                        continue;
                    }
                    bool danishFront = propX > resetRts.MapWidth - 270;
                    string[] palette = danishFront ? frontierProps : forestProps;
                    int hash = Math.Abs(propX * 7 + propY * 11);
                    int index = hash % palette.Length;
                    bool clearFort = danishFront &&
                        DistanceSquared(propX, propY, resetRts.Fortress!.X, resetRts.Fortress.Y) < 92 * 92;
                    bool clearRoad = danishFront && Math.Abs(propX - (resetRts.MapWidth - 74)) < 24;
                    bool chosen = danishFront ? hash % 2 == 0 : hash % 3 == 0;
                    if (chosen && !clearFort && !clearRoad)
                    {
                        string sprite = palette[index];
                        int blockRadius = sprite switch
                        {
                            "rts_spruce_tall" or "rts_spruce_bent" or "rts_pine_dead" or "rts_spruce_crystal" => 11,
                            "rts_moss_boulders" or "rts_silver_outcrop" or "rts_dk_barricade" => 14,
                            "rts_dk_crates" or "rts_dk_minecart" => 10,
                            _ => 0,
                        };
                        resetRts.Props.Add(new RtsProp(propX, propY, sprite, sprite == "rts_dk_wagon_rut", blockRadius));
                    }
                }
            }
        }
        _stageClear = false;
        _stageClearAge = 0;
        _previousButtons = 0;
        _gameOver = false;
        _paused = false;
        _bossRadioCard = null;
        _bossRadioAge = 0;
        _audio?.SetPaused(false);
        if (_rtsEvacuationTestMode && _rts is RtsState evacuationTest)
        {
            evacuationTest.Buildings.Add(new RtsBuilding(evacuationTest.LandingX + 122, evacuationTest.LandingY - 38, RtsBuildingType.SilverCrusher));
            evacuationTest.Units.Add(new RtsUnit(evacuationTest.LandingX + 190, evacuationTest.LandingY - 70, RtsUnitType.CaroleanSquad));
            evacuationTest.Units.Add(new RtsUnit(evacuationTest.LandingX + 235, evacuationTest.LandingY - 20, RtsUnitType.MooseCarolean));
            evacuationTest.Miners.Add(new RtsMiner(evacuationTest.LandingX + 155, evacuationTest.LandingY + 18, 17));
            evacuationTest.SalvagedSilver = RtsSalvagedSilverGoal;
            evacuationTest.SilverGoalReached = true;
            if (evacuationTest.Fortress is not null) evacuationTest.Fortress.Health = 0;
            BeginRtsEvacuation(evacuationTest);
        }
        if (_dungeonTestMode && _rts is not null) BeginDungeon();
        _audio?.SwitchMusic(_levelId switch
        {
            1 => StormaktMusicTrack.Skanska,
            3 => StormaktMusicTrack.Rts,
            _ => StormaktMusicTrack.Combat,
        });
    }

    private void StartLevel(int levelId, bool fresh = false)
    {
        _levelId = levelId;
        Reset();
        if (levelId == 3 && !fresh && TryLoadDungeonSave("autosave", out DungeonState? resumed))
        {
            _dungeon = resumed;
            _bossRadioCard = null;
            _bossRadioAge = 0;
            _audio?.SwitchMusic(StormaktMusicTrack.Dungeon);
        }
        _inLevelSelect = false;
        _inLevelPreview = false;
        _inSilverkroppenSelect = false;
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
            if (_levelSelection == 3 && _developerMode)
            {
                _inLevelSelect = false;
                _inSilverkroppenSelect = true;
                _silverkroppenSelection = File.Exists(DungeonSavePath("autosave")) ? 1 : 0;
                _audio?.Trigger(StormaktSound.Deploy);
                _previousButtons = buttons;
                return;
            }
            if (_levelSelection == 0 || (_developerMode && _levelSelection is 1 or 3))
            {
                StartLevel(_levelSelection, fresh: (buttons & Slow) != 0);
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

    private void StepSilverkroppenSelect(uint buttons)
    {
        const int choices = 6;
        if (Pressed(buttons, Up))
        {
            _silverkroppenSelection = (_silverkroppenSelection + choices - 1) % choices;
            _audio?.Trigger(StormaktSound.Deploy);
        }
        if (Pressed(buttons, Down))
        {
            _silverkroppenSelection = (_silverkroppenSelection + 1) % choices;
            _audio?.Trigger(StormaktSound.Deploy);
        }
        if (Pressed(buttons, AltFire) || Pressed(buttons, Fire))
        {
            _inSilverkroppenSelect = false;
            _inLevelSelect = true;
            _audio?.Trigger(StormaktSound.Deploy);
            _previousButtons = buttons;
            return;
        }
        if (Pressed(buttons, Start))
        {
            if (_silverkroppenSelection == 0)
            {
                StartLevel(3, fresh: true);
            }
            else if (_silverkroppenSelection == 1)
            {
                bool hasSave = TryLoadDungeonSave("autosave", out _);
                StartLevel(3, fresh: !hasSave);
                if (!hasSave) BeginDungeon();
            }
            else if (_silverkroppenSelection == 2)
            {
                StartLevel(3, fresh: true);
                BeginDungeon();
            }
            else
            {
                _lockedLevelNoticeFrames = 90;
                _audio?.Trigger(StormaktSound.HullHit);
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

    private void StepRts(uint buttons, RtsPointer pointer)
    {
        if (_rts is not RtsState rts)
        {
            return;
        }
        rts.Age++;
        rts.TowerPlacementMode = rts.BuildStage >= 4 && (buttons & Slow) != 0;
        rts.LandingAge = Math.Min(180, rts.LandingAge + 1);
        rts.PlacementPulse = Math.Max(0, rts.PlacementPulse - 1);
        _missionFrame++;
        if (_bossRadioCard is RadioCard activeRtsRadio && ++_bossRadioAge >= activeRtsRadio.DurationFrames)
        {
            _bossRadioCard = null;
            _bossRadioAge = 0;
        }
        if (rts.EvacuationAge > 0)
        {
            StepRtsEvacuation(rts);
            return;
        }
        if (rts.Age == 20)
        {
            ActivateBossRadio(RtsSilverRadio);
        }

        rts.SilverPulse = Math.Max(0, rts.SilverPulse - 1);
        StepRtsMiners(rts);
        StepRtsProduction(rts);
        StepRtsUnits(rts);
        if (rts.CombatStarted)
        {
            rts.CombatAge++;
            SpawnRtsEnemyWaves(rts);
            StepRtsCombat(rts);
        }

        if (rts.LandingAge < 120)
        {
            return;
        }

        StepRtsMouse(rts, pointer);

        int cursorSpeed = (buttons & Slow) != 0 ? 1 : 3;
        if ((buttons & Left) != 0) rts.CursorX -= cursorSpeed;
        if ((buttons & Right) != 0) rts.CursorX += cursorSpeed;
        if ((buttons & Up) != 0) rts.CursorY -= cursorSpeed;
        if ((buttons & Down) != 0) rts.CursorY += cursorSpeed;
        rts.CursorX = Math.Clamp(rts.CursorX, 10, rts.MapWidth - 11);
        rts.CursorY = Math.Clamp(rts.CursorY, 28, rts.MapHeight - 12);

        int cursorScreenX = rts.CursorX - rts.CameraX;
        if (cursorScreenX < 54)
        {
            rts.CameraX = Math.Max(0, rts.CursorX - 54);
        }
        else if (cursorScreenX > _width - 54)
        {
            rts.CameraX = Math.Min(rts.MapWidth - _width, rts.CursorX - (_width - 54));
        }

        if (Pressed(buttons, Fire) || rts.MousePrimaryPressed)
        {
            int buildX = (rts.CursorX / 12) * 12;
            int buildY = (rts.CursorY / 12) * 12;
            bool towerOrder = rts.BuildStage >= 4 && (buttons & Slow) != 0;
            bool valid = towerOrder ? IsValidRtsTowerPlacement(rts, buildX, buildY) :
                rts.BuildStage < 4 && IsValidRtsPlacement(rts, buildX, buildY);
            rts.LastPlacementValid = valid;
            rts.PlacementPulse = 30;
            if (valid && rts.BuildStage == 0 && rts.Silver >= 200)
            {
                rts.Buildings.Add(new RtsBuilding(buildX, buildY, RtsBuildingType.SteamPlant));
                rts.Silver -= 200;
                rts.BuildStage = 1;
                _audio?.Trigger(StormaktSound.RtsBuild);
                ActivateBossRadio(RtsSteamRadio);
            }
            else if (valid && rts.BuildStage == 1)
            {
                rts.Buildings.Add(new RtsBuilding(buildX, buildY, RtsBuildingType.SilverCrusher));
                for (int miner = 0; miner < 3; miner++)
                {
                    rts.Miners.Add(new RtsMiner(buildX + (miner - 1) * 7, buildY + 15 + miner * 3, miner * 17));
                }
                rts.BuildStage = 2;
                _audio?.Trigger(StormaktSound.RtsBuild);
            }
            else if (valid && rts.BuildStage == 2 && rts.Silver >= 150)
            {
                rts.Buildings.Add(new RtsBuilding(buildX, buildY, RtsBuildingType.Barracks));
                rts.Silver -= 150;
                rts.BuildStage = 3;
                _audio?.Trigger(StormaktSound.RtsBuild);
            }
            else if (valid && rts.BuildStage == 3 && rts.Silver >= 250)
            {
                rts.Buildings.Add(new RtsBuilding(buildX, buildY, RtsBuildingType.AnimalHall));
                rts.Silver -= 250;
                rts.BuildStage = 4;
                _audio?.Trigger(StormaktSound.RtsBuild);
            }
            else if (towerOrder && valid && rts.Silver >= 120)
            {
                rts.Buildings.Add(new RtsBuilding(buildX, buildY, RtsBuildingType.DefenseTower));
                rts.Silver -= 120;
                _audio?.Trigger(StormaktSound.RtsBuild);
            }
            else if (!towerOrder && rts.BuildStage >= 4 && TryHandleRtsCommand(rts))
            {
                _audio?.Trigger(StormaktSound.Deploy);
            }
            else
            {
                _audio?.Trigger(StormaktSound.HullHit);
            }
        }
        if (Pressed(buttons, AltFire))
        {
            if (rts.BuildStage >= 4)
            {
                foreach (RtsUnit unit in rts.Units)
                {
                    unit.Selected = false;
                    unit.TargetX = unit.X;
                    unit.TargetY = unit.Y;
                }
            }
            else
            {
                rts.CursorX = rts.LandingX;
                rts.CursorY = rts.LandingY;
                rts.CameraX = 0;
            }
        }
    }

    private void StepRtsMouse(RtsState rts, RtsPointer pointer)
    {
        const uint mouseLeft = 1u << 0;
        const uint mouseRight = 1u << 1;
        bool left = pointer.Inside && (pointer.Buttons & mouseLeft) != 0;
        bool previousLeft = (rts.PreviousMouseButtons & mouseLeft) != 0;
        bool right = pointer.Inside && (pointer.Buttons & mouseRight) != 0;
        bool previousRight = (rts.PreviousMouseButtons & mouseRight) != 0;
        rts.MousePrimaryPressed = left && !previousLeft;

        if (pointer.Inside)
        {
            rts.MouseWorldX = Math.Clamp(pointer.X + rts.CameraX, 0, rts.MapWidth - 1);
            rts.MouseWorldY = Math.Clamp(pointer.Y, 18, rts.MapHeight - 1);
            rts.CursorX = rts.MouseWorldX;
            rts.CursorY = rts.MouseWorldY;
        }
        if (right && !previousRight)
        {
            rts.MouseSelecting = true;
            rts.MouseDragStartX = rts.MouseWorldX;
            rts.MouseDragStartY = rts.MouseWorldY;
        }
        else if (!right && previousRight && rts.MouseSelecting)
        {
            int leftX = Math.Min(rts.MouseDragStartX, rts.MouseWorldX);
            int rightX = Math.Max(rts.MouseDragStartX, rts.MouseWorldX);
            int topY = Math.Min(rts.MouseDragStartY, rts.MouseWorldY);
            int bottomY = Math.Max(rts.MouseDragStartY, rts.MouseWorldY);
            bool click = rightX - leftX < 6 && bottomY - topY < 6;
            RtsUnit? clickedUnit = click
                ? rts.Units.FirstOrDefault(unit =>
                    DistanceSquared(unit.X, unit.Y, rts.MouseWorldX, rts.MouseWorldY) <= 15 * 15)
                : null;
            if (click && clickedUnit is null)
            {
                foreach (RtsUnit unit in rts.Units) unit.Selected = false;
            }
            else
            {
                foreach (RtsUnit unit in rts.Units)
                {
                    unit.Selected = click
                        ? ReferenceEquals(unit, clickedUnit)
                        : unit.X >= leftX && unit.X <= rightX && unit.Y >= topY && unit.Y <= bottomY;
                }
                _audio?.Trigger(StormaktSound.RtsUnitReady);
            }
            rts.MouseSelecting = false;
        }
        rts.PreviousMouseButtons = pointer.Inside ? pointer.Buttons : 0;
    }

    private void IssueRtsFormationOrder(RtsState rts, int targetX, int targetY)
    {
        int selectedIndex = 0;
        foreach (RtsUnit unit in rts.Units.Where(candidate => candidate.Selected))
        {
            unit.TargetX = Math.Clamp(targetX + (selectedIndex % 3 - 1) * 14, 12, rts.MapWidth - 13);
            unit.TargetY = Math.Clamp(targetY + (selectedIndex / 3) * 12, 30, rts.MapHeight - 14);
            selectedIndex++;
        }
        if (selectedIndex > 0) _audio?.Trigger(StormaktSound.Deploy);
    }

    private void StepRtsMiners(RtsState rts)
    {
        RtsBuilding? crusher = rts.Buildings.FirstOrDefault(building => building.Type == RtsBuildingType.SilverCrusher);
        if (crusher is null || !RtsHasPower(rts)) return;
        foreach (RtsMiner miner in rts.Miners)
        {
            double targetX = miner.Carrying ? rts.LandingX : crusher.X;
            double targetY = miner.Carrying ? rts.LandingY + 18 : crusher.Y + 12;
            miner.TargetX = targetX;
            miner.TargetY = targetY;
            double dx = targetX - miner.X;
            double dy = targetY - miner.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            miner.Moving = distance >= 1.0;
            if (miner.Moving)
            {
                const double speed = 0.82;
                (miner.X, miner.Y) = MoveRtsAroundTerrain(rts, miner.X, miner.Y,
                    dx / distance * Math.Min(speed, distance), dy / distance * Math.Min(speed, distance), 5);
                continue;
            }
            if (miner.Carrying)
            {
                miner.Carrying = false;
                miner.LoadAge = 0;
                rts.Silver += 40;
                rts.SalvagedSilver += 40;
                rts.SilverGoalReached |= rts.SalvagedSilver >= RtsSalvagedSilverGoal;
                rts.SilverPulse = 30;
                _score += 40;
                _audio?.Trigger(StormaktSound.RtsUnitReady);
            }
            else if (++miner.LoadAge >= 60)
            {
                miner.Carrying = true;
                miner.LoadAge = 0;
            }
        }
    }

    private bool IsValidRtsPlacement(RtsState rts, int x, int y)
    {
        if (rts.BuildStage >= 4 || y < 42 || y > rts.MapHeight - 28 || x < 24 || x > rts.MapWidth - 25)
        {
            return false;
        }
        foreach (RtsBuilding building in rts.Buildings)
        {
            if (Math.Abs(building.X - x) < 38 && Math.Abs(building.Y - y) < 34)
            {
                return false;
            }
        }
        if (rts.Props.Any(prop => prop.BlockRadius > 0 &&
            DistanceSquared(x, y, prop.X, prop.Y) < (18 + prop.BlockRadius) * (18 + prop.BlockRadius)))
        {
            return false;
        }
        if (rts.BuildStage == 0)
        {
            double dx = x - rts.LandingX;
            double dy = y - rts.LandingY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance is >= 48 and <= 126;
        }
        if (rts.BuildStage == 1)
        {
            return Math.Abs(x - SilverVeinWorldX(y)) <= 28;
        }
        foreach (RtsBuilding building in rts.Buildings)
        {
            double dx = x - building.X;
            double dy = y - building.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= 112)
            {
                return true;
            }
        }
        return false;
    }

    private static int SilverVeinWorldX(int worldY) =>
        155 + worldY / 2 + (int)Math.Round(Math.Sin(worldY * 0.09) * 9.0);

    private bool IsValidRtsTowerPlacement(RtsState rts, int x, int y)
    {
        if (y < 38 || y > rts.MapHeight - 24 || x < 20 || x > rts.MapWidth - 21)
        {
            return false;
        }
        bool inBuildRadius = false;
        foreach (RtsBuilding building in rts.Buildings)
        {
            double dx = x - building.X;
            double dy = y - building.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 34)
            {
                return false;
            }
            inBuildRadius |= distance <= 112;
        }
        return inBuildRadius;
    }

    private bool TryHandleRtsCommand(RtsState rts)
    {
        foreach (RtsBuilding building in rts.Buildings)
        {
            if (Math.Abs(building.X - rts.CursorX) > 20 || Math.Abs(building.Y - rts.CursorY) > 18)
            {
                continue;
            }
            if (building.Type == RtsBuildingType.Barracks && rts.BarracksTimer == 0 && rts.Silver >= 100)
            {
                rts.Silver -= 100;
                rts.BarracksTimer = 90;
                return true;
            }
            if (building.Type == RtsBuildingType.AnimalHall && rts.AnimalHallTimer == 0 && rts.Silver >= 180)
            {
                rts.Silver -= 180;
                rts.AnimalHallTimer = 150;
                return true;
            }
            return false;
        }

        RtsUnit? picked = null;
        foreach (RtsUnit unit in rts.Units)
        {
            if (Math.Abs(unit.X - rts.CursorX) <= 15 && Math.Abs(unit.Y - rts.CursorY) <= 15)
            {
                picked = unit;
                break;
            }
        }
        if (picked is not null)
        {
            foreach (RtsUnit unit in rts.Units)
            {
                unit.Selected = unit.Type == picked.Type &&
                    Math.Abs(unit.X - picked.X) <= 72 && Math.Abs(unit.Y - picked.Y) <= 72;
            }
            return true;
        }

        bool ordered = false;
        int selectedIndex = 0;
        foreach (RtsUnit unit in rts.Units)
        {
            if (!unit.Selected)
            {
                continue;
            }
            unit.TargetX = Math.Clamp(rts.CursorX + (selectedIndex % 3 - 1) * 14, 12, rts.MapWidth - 13);
            unit.TargetY = Math.Clamp(rts.CursorY + (selectedIndex / 3) * 12, 30, rts.MapHeight - 14);
            selectedIndex++;
            ordered = true;
        }
        return ordered;
    }

    private void StepRtsProduction(RtsState rts)
    {
        if (!RtsHasPower(rts))
        {
            return;
        }
        if (rts.BarracksTimer > 0 && --rts.BarracksTimer == 0)
        {
            RtsBuilding barracks = rts.Buildings.First(building => building.Type == RtsBuildingType.Barracks);
            rts.Units.Add(new RtsUnit(barracks.X - 8, barracks.Y + 24, RtsUnitType.CaroleanSquad));
            rts.CombatStarted = true;
            _audio?.Trigger(StormaktSound.RtsUnitReady);
        }
        if (rts.AnimalHallTimer > 0 && --rts.AnimalHallTimer == 0)
        {
            RtsBuilding hall = rts.Buildings.First(building => building.Type == RtsBuildingType.AnimalHall);
            rts.Units.Add(new RtsUnit(hall.X + 8, hall.Y + 27, RtsUnitType.MooseCarolean));
            rts.CombatStarted = true;
            _audio?.Trigger(StormaktSound.RtsUnitReady);
            ActivateBossRadio(RtsMooseRadio);
        }
    }

    private void StepRtsUnits(RtsState rts)
    {
        foreach (RtsUnit unit in rts.Units)
        {
            double dx = unit.TargetX - unit.X;
            double dy = unit.TargetY - unit.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 0.75)
            {
                unit.X = unit.TargetX;
                unit.Y = unit.TargetY;
                continue;
            }
            double speed = unit.Type == RtsUnitType.MooseCarolean ? 1.45 : 0.82;
            (unit.X, unit.Y) = MoveRtsAroundTerrain(rts, unit.X, unit.Y,
                dx / distance * Math.Min(speed, distance), dy / distance * Math.Min(speed, distance), 7);
        }
    }

    private void SpawnRtsEnemyWaves(RtsState rts)
    {
        if (rts.CombatAge is 600 or 1_800)
        {
            SpawnRtsWave(rts, RtsEnemyType.TollStormer, rts.CombatAge == 600 ? 3 : 4, 58);
            if (rts.CombatAge == 600) ActivateBossRadio(RtsClaimRadio);
        }
        else if (rts.CombatAge is 960 or 2_160)
        {
            SpawnRtsWave(rts, RtsEnemyType.CoinMastiff, rts.CombatAge == 960 ? 2 : 3, 92);
        }
        else if (rts.CombatAge is 1_320 or 2_520)
        {
            SpawnRtsWave(rts, RtsEnemyType.LedgerPikeman, rts.CombatAge == 1_320 ? 3 : 4, 126);
        }
        else if (rts.CombatAge is 1_680 or 2_880)
        {
            SpawnRtsWave(rts, RtsEnemyType.PowderBoar, rts.CombatAge == 1_680 ? 1 : 2, 76);
            if (rts.CombatAge == 1_680) ActivateBossRadio(RtsPowderRadio);
        }
        else if (rts.CombatAge is 2_040 or 3_240)
        {
            SpawnRtsWave(rts, RtsEnemyType.OrganWagon, 1, 118);
            if (rts.CombatAge == 2_040) ActivateBossRadio(RtsOrganRadio);
        }
        else if (rts.CombatAge >= 3_600 && rts.CombatAge % 360 == 0 && rts.Fortress is { Health: > 0 })
        {
            RtsEnemyType type = ((rts.CombatAge / 360) % 3) switch
            {
                0 => RtsEnemyType.TollStormer,
                1 => RtsEnemyType.LedgerPikeman,
                _ => RtsEnemyType.CoinMastiff,
            };
            SpawnRtsWave(rts, type, 3 + rts.CombatAge / 1_800, 72 + rts.CombatAge / 120 % 90);
        }
    }

    private void SpawnRtsWave(RtsState rts, RtsEnemyType type, int count, int yBase)
    {
        int roadX = rts.MapWidth - 74;
        for (int index = 0; index < count; index++)
        {
            int health = type switch
            {
                RtsEnemyType.CoinMastiff => 34,
                RtsEnemyType.LedgerPikeman => 58,
                RtsEnemyType.PowderBoar => 48,
                RtsEnemyType.OrganWagon => 130,
                _ => 42,
            };
            rts.Enemies.Add(new RtsEnemy(roadX + (index % 2) * 12 - 6, yBase + index * 19, type, health));
        }
        _audio?.Trigger(StormaktSound.RtsRaidHorn);
    }

    private void StepRtsCombat(RtsState rts)
    {
        foreach (RtsUnit unit in rts.Units)
        {
            unit.Cooldown = Math.Max(0, unit.Cooldown - 1);
            RtsEnemy? target = rts.Enemies
                .Where(enemy => enemy.Health > 0)
                .OrderBy(enemy => DistanceSquared(unit.X, unit.Y, enemy.X, enemy.Y))
                .FirstOrDefault();
            double range = unit.Type == RtsUnitType.MooseCarolean ? 22 : 64;
            if (target is not null && DistanceSquared(unit.X, unit.Y, target.X, target.Y) <= range * range && unit.Cooldown == 0)
            {
                target.Health -= unit.Type == RtsUnitType.MooseCarolean ? 22 : 9;
                unit.Cooldown = unit.Type == RtsUnitType.MooseCarolean ? 42 : 34;
                _audio?.Trigger(unit.Type == RtsUnitType.MooseCarolean ? StormaktSound.RtsMooseCharge : StormaktSound.RtsCaroleanVolley);
            }
            else if (unit.Cooldown == 0 && rts.Fortress is { Health: > 0 } fortress &&
                DistanceSquared(unit.X, unit.Y, fortress.X, fortress.Y) <= (range + 32) * (range + 32))
            {
                int damage = unit.Type == RtsUnitType.MooseCarolean ? 18 : 8;
                if (fortress.LeftSealHealth > 0) fortress.LeftSealHealth -= damage;
                else if (fortress.RightSealHealth > 0) fortress.RightSealHealth -= damage;
                else fortress.Health -= damage;
                fortress.GateOpen = fortress.LeftSealHealth <= 0 && fortress.RightSealHealth <= 0;
                unit.Cooldown = unit.Type == RtsUnitType.MooseCarolean ? 42 : 34;
                _audio?.Trigger(unit.Type == RtsUnitType.MooseCarolean ? StormaktSound.RtsMooseCharge : StormaktSound.RtsCaroleanVolley);
            }
        }

        foreach (RtsBuilding tower in rts.Buildings.Where(building => building.Type == RtsBuildingType.DefenseTower))
        {
            tower.Cooldown = Math.Max(0, tower.Cooldown - 1);
            if (!RtsHasPower(rts))
            {
                continue;
            }
            RtsEnemy? target = rts.Enemies
                .Where(enemy => enemy.Health > 0 && DistanceSquared(tower.X, tower.Y, enemy.X, enemy.Y) <= 88 * 88)
                .OrderBy(enemy => DistanceSquared(tower.X, tower.Y, enemy.X, enemy.Y))
                .FirstOrDefault();
            if (target is not null && tower.Cooldown == 0)
            {
                target.Health -= 14;
                tower.Cooldown = 32;
                _audio?.Trigger(StormaktSound.RtsTowerFire);
            }
        }

        for (int index = rts.Enemies.Count - 1; index >= 0; index--)
        {
            RtsEnemy enemy = rts.Enemies[index];
            enemy.Moving = false;
            if (enemy.Health <= 0)
            {
                rts.Enemies.RemoveAt(index);
                rts.Silver += 8;
                _score += 60;
                continue;
            }
            enemy.Cooldown = Math.Max(0, enemy.Cooldown - 1);
            RtsUnit? nearbyUnit = rts.Units
                .Where(unit => unit.Health > 0)
                .OrderBy(unit => DistanceSquared(enemy.X, enemy.Y, unit.X, unit.Y))
                .FirstOrDefault();
            RtsBuilding? powerTarget = rts.Buildings.FirstOrDefault(building => building.Type == RtsBuildingType.SteamPlant);
            double targetX = powerTarget?.X ?? rts.LandingX;
            double targetY = powerTarget?.Y ?? rts.LandingY;
            if (nearbyUnit is not null && DistanceSquared(enemy.X, enemy.Y, nearbyUnit.X, nearbyUnit.Y) <= 34 * 34)
            {
                targetX = nearbyUnit.X;
                targetY = nearbyUnit.Y;
            }
            double dx = targetX - enemy.X;
            double dy = targetY - enemy.Y;
            double distance = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));
            if (enemy.Type == RtsEnemyType.PowderBoar && distance <= 40)
            {
                enemy.FuseAge++;
                if (enemy.FuseAge == 1) _audio?.Trigger(StormaktSound.RtsPowderFuse);
                if (enemy.FuseAge >= 45)
                {
                    if (!_invincibleTestMode && nearbyUnit is not null && DistanceSquared(enemy.X, enemy.Y, nearbyUnit.X, nearbyUnit.Y) <= 42 * 42)
                    {
                        nearbyUnit.Health -= 48;
                    }
                    if (!_invincibleTestMode && powerTarget is not null && DistanceSquared(enemy.X, enemy.Y, powerTarget.X, powerTarget.Y) <= 48 * 48)
                    {
                        powerTarget.Health -= 58;
                    }
                    enemy.Health = 0;
                    _audio?.Trigger(StormaktSound.RtsPowderExplosion);
                }
                continue;
            }
            if (enemy.Type == RtsEnemyType.OrganWagon && distance <= 118)
            {
                if (enemy.Cooldown == 0)
                {
                    if (!_invincibleTestMode && powerTarget is not null)
                    {
                        powerTarget.Health -= 12;
                    }
                    else if (!_invincibleTestMode)
                    {
                        rts.KarlHealth -= 12;
                    }
                    enemy.Cooldown = 90;
                    _audio?.Trigger(StormaktSound.RtsOrganVolley);
                }
                continue;
            }
            if (distance <= 15)
            {
                if (enemy.Cooldown == 0)
                {
                    int damage = enemy.Type switch { RtsEnemyType.CoinMastiff => 7, RtsEnemyType.LedgerPikeman => 5, _ => 4 };
                    if (!_invincibleTestMode && nearbyUnit is not null && targetX == nearbyUnit.X && targetY == nearbyUnit.Y)
                    {
                        nearbyUnit.Health -= damage;
                    }
                    else if (!_invincibleTestMode && powerTarget is not null)
                    {
                        powerTarget.Health -= damage;
                    }
                    else if (!_invincibleTestMode)
                    {
                        rts.KarlHealth -= damage;
                    }
                    enemy.Cooldown = 40;
                }
            }
            else
            {
                double speed = enemy.Type switch
                {
                    RtsEnemyType.CoinMastiff => 0.88,
                    RtsEnemyType.LedgerPikeman => 0.40,
                    RtsEnemyType.PowderBoar => 0.64,
                    RtsEnemyType.OrganWagon => 0.27,
                    _ => 0.54,
                };
                speed *= 0.91 + (enemy.AnimationPhase % 7) * 0.025;
                (enemy.X, enemy.Y) = MoveRtsAroundTerrain(rts, enemy.X, enemy.Y,
                    dx / distance * speed, dy / distance * speed, 7);
                enemy.Moving = true;
            }
        }
        rts.Units.RemoveAll(unit => unit.Health <= 0);
        rts.Buildings.RemoveAll(building => building.Health <= 0);
        if (rts.Fortress is { Health: <= 0 } defeatedFortress && rts.SilverGoalReached)
        {
            defeatedFortress.Health = 0;
            BeginRtsEvacuation(rts);
        }
        if (rts.KarlHealth <= 0)
        {
            _gameOver = true;
        }
    }

    private void BeginRtsEvacuation(RtsState rts)
    {
        if (rts.EvacuationAge > 0) return;
        rts.EvacuationAge = 1;
        rts.Enemies.Clear();
        foreach (RtsUnit unit in rts.Units) unit.Selected = false;
        ActivateBossRadio(RtsVictoryRadio);
    }

    private void StepRtsEvacuation(RtsState rts)
    {
        int age = rts.EvacuationAge++;
        if (age < 650)
        {
            int targetCamera = Math.Clamp(rts.LandingX - _width / 2, 0, rts.MapWidth - _width);
            rts.CameraX += Math.Sign(targetCamera - rts.CameraX) * Math.Min(4, Math.Abs(targetCamera - rts.CameraX));
        }
        if (age >= 45 && age < 340)
        {
            foreach (RtsUnit unit in rts.Units)
            {
                double dx = rts.LandingX - unit.X;
                double dy = rts.LandingY - unit.Y;
                double distance = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
                double baseSpeed = unit.Type == RtsUnitType.MooseCarolean ? 2.05 : 1.35;
                double speed = Math.Max(baseSpeed, distance / Math.Max(30, 330 - age));
                unit.TargetX = rts.LandingX;
                unit.TargetY = rts.LandingY;
                unit.X += dx / distance * speed;
                unit.Y += dy / distance * speed;
            }
            foreach (RtsMiner miner in rts.Miners)
            {
                double dx = rts.LandingX - miner.X;
                double dy = rts.LandingY - miner.Y;
                double distance = Math.Max(1.0, Math.Sqrt(dx * dx + dy * dy));
                miner.TargetX = rts.LandingX;
                miner.TargetY = rts.LandingY;
                double speed = Math.Max(1.15, distance / Math.Max(30, 330 - age));
                miner.X += dx / distance * speed;
                miner.Y += dy / distance * speed;
            }
            rts.Units.RemoveAll(unit => DistanceSquared(unit.X, unit.Y, rts.LandingX, rts.LandingY) < 12 * 12);
            rts.Miners.RemoveAll(miner => DistanceSquared(miner.X, miner.Y, rts.LandingX, rts.LandingY) < 11 * 11);
        }
        if (age == 350)
        {
            rts.Units.Clear();
            rts.Miners.Clear();
            _audio?.Trigger(StormaktSound.RtsEngineIgnition);
        }
        if (age == 560) ActivateBossRadio(RtsWaitMinerRadio);
        if (age >= 650)
        {
            RtsBuilding? crusher = rts.Buildings.FirstOrDefault(building => building.Type == RtsBuildingType.SilverCrusher);
            if (crusher is not null)
            {
                int targetCamera = Math.Clamp(crusher.X - _width / 2, 0, rts.MapWidth - _width);
                rts.CameraX += Math.Sign(targetCamera - rts.CameraX) * Math.Min(3, Math.Abs(targetCamera - rts.CameraX));
            }
        }
        if (age == 930) ActivateBossRadio(RtsTempleRadio);
        if (age == 1_280) BeginDungeon();
    }

    private void BeginDungeon()
    {
        _stageClear = false;
        _bossRadioCard = DungeonDescentRadio;
        _bossRadioAge = 0;
        _dungeon = new DungeonState
        {
            RoomWidth = 720,
            RoomHeight = 440,
            KarlX = 92,
            KarlY = 228,
            TargetX = 92,
            TargetY = 228,
            Facing = DungeonFacing.South,
            Health = 100,
            MaxHealth = 100,
            Depth = 1,
            DoorHealth = 3,
        };
        SeedDungeonInventory(_dungeon);
        SeedDungeonEncounter(_dungeon);
        if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_DEPTH2_TEST"), "1", StringComparison.Ordinal))
        {
            BeginDungeonDepthTwo(_dungeon);
            if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_BOSS_TEST"), "1", StringComparison.Ordinal))
            {
                _dungeon.KarlX = 920;
                _dungeon.KarlY = 390;
                _dungeon.TargetX = 920;
                _dungeon.TargetY = 390;
                _dungeon.CameraX = 720;
                _dungeon.CameraY = 250;
            }
            if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_BOSS_DEATH_TEST"), "1", StringComparison.Ordinal))
            {
                DungeonEnemy fogde = _dungeon.Enemies.First(enemy => enemy.Type == DungeonEnemyType.SilverFogde);
                fogde.Health = 0;
                fogde.State = DungeonEnemyState.Dead;
                DefeatDungeonEnemy(_dungeon, fogde);
                _dungeon.KarlX = 1080;
                _dungeon.KarlY = 390;
                _dungeon.TargetX = 1080;
                _dungeon.TargetY = 390;
            }
            if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_DEPTH3_TEST"), "1", StringComparison.Ordinal))
            {
                _dungeon.DepthThreeOpen = true;
                BeginDungeonDepthThree(_dungeon);
                if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_SHEPHERD_TEST"), "1", StringComparison.Ordinal))
                {
                    _dungeon.KarlX = 1160; _dungeon.KarlY = 450;
                    _dungeon.TargetX = 1160; _dungeon.TargetY = 450;
                }
                if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_SHEPHERD_DEATH_TEST"), "1", StringComparison.Ordinal))
                {
                    DungeonEnemy shepherd = _dungeon.Enemies.First(enemy => enemy.Type == DungeonEnemyType.BlindShepherd);
                    shepherd.Health = 0; shepherd.State = DungeonEnemyState.Dead;
                    DefeatDungeonEnemy(_dungeon, shepherd);
                    _dungeon.KarlX = 1180; _dungeon.KarlY = 450;
                    _dungeon.TargetX = 1180; _dungeon.TargetY = 450;
                }
                if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_TEMPLE_TEST"), "1", StringComparison.Ordinal))
                {
                    _dungeon.TempleOpen = true;
                    BeginDungeonDepthFour(_dungeon);
                    if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_SHADOW_TEST"), "1", StringComparison.Ordinal))
                    {
                        _dungeon.KarlX = 910; _dungeon.KarlY = 380;
                        _dungeon.TargetX = 910; _dungeon.TargetY = 380;
                    }
                    if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_SIGIL_TEST"), "1", StringComparison.Ordinal))
                    {
                        DungeonEnemy shadow = _dungeon.Enemies.First(enemy => enemy.Type == DungeonEnemyType.LemminkainenShadow);
                        shadow.Health = 0; shadow.State = DungeonEnemyState.Dead; shadow.StateAge = 120; shadow.Phase = 1;
                        DefeatDungeonEnemy(_dungeon, shadow);
                        _dungeon.KarlX = 1280; _dungeon.KarlY = 380;
                        _dungeon.TargetX = 1280; _dungeon.TargetY = 380;
                        if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_SIGIL_COMPLETE_TEST"), "1", StringComparison.Ordinal))
                        {
                            foreach (DungeonEnemy guard in _dungeon.Enemies.Where(enemy => enemy.Depth == 4 && enemy.Zone == 44))
                            {
                                guard.Health = 0; guard.State = DungeonEnemyState.Dead; guard.StateAge = 120;
                                DefeatDungeonEnemy(_dungeon, guard);
                            }
                            _dungeon.KarlX = 1580; _dungeon.KarlY = 380;
                            _dungeon.TargetX = 1580; _dungeon.TargetY = 380;
                        }
                    }
                    if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_TINCTURE_TEST"), "1", StringComparison.Ordinal))
                    {
                        _dungeon.Health = 35;
                        DungeonItem tincture = AddDungeonItem(_dungeon, 14, -1, -1, DungeonEquipmentSlot.None,
                            DungeonItemRarity.Carolean);
                        tincture.OnGround = true; tincture.WorldX = _dungeon.KarlX + 32; tincture.WorldY = _dungeon.KarlY;
                    }
                }
            }
        }
        if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_DEATH_TEST"), "1", StringComparison.Ordinal))
            _dungeon.Health = 0;
        if (string.Equals(Environment.GetEnvironmentVariable("WAYLANDFORGE_STORMAKT_DUNGEON_ORB_TEST"), "1", StringComparison.Ordinal))
            _dungeon.Health = 45;
        _audio?.SwitchMusic(StormaktMusicTrack.Dungeon);
        WriteDungeonSave(_dungeon, "autosave");
    }

    private void StepDungeon(uint buttons, RtsPointer pointer)
    {
        DungeonState dungeon = _dungeon!;
        const uint mouseLeft = 1u;
        const uint mouseRight = 2u;
        uint pointerButtons = pointer.Inside ? pointer.Buttons : 0;
        bool leftPointerPressed = pointer.Inside && (pointerButtons & mouseLeft) != 0 &&
            (dungeon.PreviousMouseButtons & mouseLeft) == 0;
        bool leftPointerHeld = pointer.Inside && (pointerButtons & mouseLeft) != 0;
        bool leftPointerReleased = pointer.Inside && (pointerButtons & mouseLeft) == 0 &&
            (dungeon.PreviousMouseButtons & mouseLeft) != 0;
        bool rightPointerPressed = pointer.Inside && (pointerButtons & mouseRight) != 0 &&
            (dungeon.PreviousMouseButtons & mouseRight) == 0;
        // Always consume releases before any death, hit-stop, radio or menu
        // early-return. Otherwise one interrupted click can latch forever.
        dungeon.PreviousMouseButtons = pointerButtons;
        dungeon.Age++;
        if (_bossRadioCard is RadioCard active && ++_bossRadioAge >= active.DurationFrames)
        {
            _bossRadioCard = null;
            _bossRadioAge = 0;
        }
        if (dungeon.Health <= 0 && dungeon.DeathAge == 0)
        {
            dungeon.DeathAge = 1;
            dungeon.HurtAge = 90;
            dungeon.TargetX = dungeon.KarlX;
            dungeon.TargetY = dungeon.KarlY;
            dungeon.AttackAge = 0;
        }
        if (dungeon.DeathAge > 0)
        {
            dungeon.DeathAge++;
            if (dungeon.DeathAge >= 90) RestartDungeonAfterDeath(dungeon);
            return;
        }
        if (dungeon.LevelPulse > 0) dungeon.LevelPulse--;
        if (dungeon.Age < 150) return;
        if (dungeon.Age == 150) WriteDungeonSave(dungeon, "autosave");
        if (Pressed(buttons, AltFire))
        {
            dungeon.InventoryOpen = !dungeon.InventoryOpen;
            dungeon.ViewingStash = false;
            dungeon.SelectedItemId = 0;
            dungeon.InspectedItemId = 0;
            if (!dungeon.InventoryOpen) WriteDungeonSave(dungeon, "autosave");
            return;
        }
        if (dungeon.InventoryOpen)
        {
            StepDungeonInventory(dungeon, buttons, pointer);
            return;
        }
        if (dungeon.HitStop > 0)
        {
            dungeon.HitStop--;
            return;
        }
        if (Pressed(buttons, QuickPotion)) ConsumeQuickDungeonPotion(dungeon);
        dungeon.Guarding = (buttons & Slow) != 0 && dungeon.AttackAge == 0;
        if (dungeon.HurtAge > 0) dungeon.HurtAge--;
        bool interacted = false;
        if (Pressed(buttons, Fire))
        {
            DungeonItem? ground = dungeon.Items.Where(item => item.OnGround)
                .OrderBy(item => DistanceSquared(item.WorldX, item.WorldY, dungeon.KarlX, dungeon.KarlY)).FirstOrDefault();
            if (ground is not null && DistanceSquared(ground.WorldX, ground.WorldY, dungeon.KarlX, dungeon.KarlY) < 34 * 34)
            {
                StoreDungeonPickup(dungeon, ground);
                interacted = true;
            }
            else if (DistanceSquared(470, 300, dungeon.KarlX, dungeon.KarlY) < 48 * 48)
            {
                dungeon.InventoryOpen = true;
                dungeon.ViewingStash = true;
                dungeon.SelectedItemId = 0;
                dungeon.InspectedItemId = 0;
                interacted = true;
            }
            else if (dungeon.Depth == 1 && dungeon.DoorReady && dungeon.DescentWarningPlayed &&
                _bossRadioCard is null && DistanceSquared(650, 218, dungeon.KarlX, dungeon.KarlY) < 48 * 48)
            {
                BeginDungeonDepthTwo(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 2 && DistanceSquared(60, 410, dungeon.KarlX, dungeon.KarlY) < 48 * 48)
            {
                ReturnToDungeonDepthOne(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 2 && dungeon.DepthThreeOpen &&
                DistanceSquared(1190, 390, dungeon.KarlX, dungeon.KarlY) < 52 * 52)
            {
                BeginDungeonDepthThree(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 3 && DistanceSquared(60, 240, dungeon.KarlX, dungeon.KarlY) < 48 * 48)
            {
                ReturnToDungeonDepthTwo(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 3 && dungeon.TempleOpen &&
                DistanceSquared(1320, 450, dungeon.KarlX, dungeon.KarlY) < 54 * 54)
            {
                BeginDungeonDepthFour(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 4 && DistanceSquared(60, 250, dungeon.KarlX, dungeon.KarlY) < 48 * 48)
            {
                ReturnToDungeonDepthThree(dungeon);
                interacted = true;
            }
            else if (dungeon.Depth == 3)
            {
                (int X, int Y)[] steles = [(390, 520), (930, 430), (1180, 120)];
                int lore = Array.FindIndex(steles, stele =>
                    DistanceSquared(stele.X, stele.Y, dungeon.KarlX, dungeon.KarlY) < 48 * 48);
                if (lore >= 0)
                {
                    ActivateBossRadio(DungeonLoreCards[lore]);
                    dungeon.LoreMask |= 1 << lore;
                    interacted = true;
                    WriteDungeonSave(dungeon, "autosave");
                }
            }
            if (!interacted) StartDungeonAttack(dungeon, pointer);
        }

        if (rightPointerPressed) StartDungeonAttack(dungeon, pointer);
        if (leftPointerPressed || leftPointerHeld || leftPointerReleased)
        {
            double worldX = pointer.X + dungeon.CameraX;
            double worldY = pointer.Y + dungeon.CameraY;
            DungeonItem? clickedLoot = dungeon.Items.Where(item => item.OnGround &&
                    DistanceSquared(item.WorldX, item.WorldY, worldX, worldY) < 18 * 18)
                .OrderBy(item => DistanceSquared(item.WorldX, item.WorldY, worldX, worldY)).FirstOrDefault();
            DungeonChest? clickedChest = clickedLoot is null ? dungeon.Chests.Where(chest => chest.Depth == dungeon.Depth &&
                    DistanceSquared(chest.X, chest.Y, worldX, worldY) < 28 * 28)
                .OrderBy(chest => DistanceSquared(chest.X, chest.Y, worldX, worldY)).FirstOrDefault() : null;
            bool clickedDoor = dungeon.Depth == 1 && dungeon.DoorReady &&
                DistanceSquared(650, 218, worldX, worldY) < 44 * 44;
            bool clickedReturnDoor = dungeon.Depth == 2 && DistanceSquared(60, 410, worldX, worldY) < 44 * 44;
            bool clickedDeepDoor = dungeon.Depth == 2 && dungeon.DepthThreeOpen &&
                DistanceSquared(1190, 390, worldX, worldY) < 54 * 54;
            bool clickedDepthThreeReturn = dungeon.Depth == 3 && DistanceSquared(60, 240, worldX, worldY) < 48 * 48;
            bool clickedTemple = dungeon.Depth == 3 && dungeon.TempleOpen && DistanceSquared(1320, 450, worldX, worldY) < 58 * 58;
            bool clickedTempleReturn = dungeon.Depth == 4 && DistanceSquared(60, 250, worldX, worldY) < 48 * 48;
            dungeon.PendingPickupItemId = clickedLoot?.Id ?? 0;
            dungeon.PendingChestId = clickedChest?.Id ?? 0;
            dungeon.PendingDoorEntry = clickedDoor;
            dungeon.PendingReturnEntry = clickedReturnDoor;
            dungeon.PendingDepthThreeEntry = clickedDeepDoor;
            dungeon.PendingDepthThreeReturn = clickedDepthThreeReturn;
            dungeon.PendingTempleEntry = clickedTemple;
            dungeon.PendingTempleReturn = clickedTempleReturn;
            (double targetX, double targetY) = clickedLoot is null
                ? (clickedChest?.X ?? (clickedDoor ? 650 : clickedReturnDoor ? 60 : clickedDeepDoor ? 1190 : clickedDepthThreeReturn ? 60 : clickedTemple ? 1320 : clickedTempleReturn ? 60 : worldX),
                    clickedChest?.Y ?? (clickedDoor ? 218 : clickedReturnDoor ? 410 : clickedDeepDoor ? 390 : clickedDepthThreeReturn ? 240 : clickedTemple ? 450 : clickedTempleReturn ? 250 : worldY))
                : FindDungeonPickupApproach(dungeon, clickedLoot);
            dungeon.TargetX = Math.Clamp(targetX, 28, dungeon.RoomWidth - 29);
            dungeon.TargetY = Math.Clamp(targetY, 48, dungeon.RoomHeight - 29);
        }
        if (dungeon.PendingPickupItemId != 0)
        {
            DungeonItem? pending = dungeon.Items.FirstOrDefault(item => item.Id == dungeon.PendingPickupItemId && item.OnGround);
            if (pending is null) dungeon.PendingPickupItemId = 0;
            else if (DistanceSquared(pending.WorldX, pending.WorldY, dungeon.KarlX, dungeon.KarlY) < 38 * 38)
            {
                if (StoreDungeonPickup(dungeon, pending)) dungeon.PendingPickupItemId = 0;
            }
        }
        if (dungeon.PendingChestId != 0)
        {
            DungeonChest? chest = dungeon.Chests.FirstOrDefault(candidate => candidate.Id == dungeon.PendingChestId);
            if (chest is null || chest.Open) dungeon.PendingChestId = 0;
            else if (DistanceSquared(chest.X, chest.Y, dungeon.KarlX, dungeon.KarlY) < 38 * 38)
            {
                OpenDungeonChest(dungeon, chest);
                dungeon.PendingChestId = 0;
            }
        }

        StepDungeonTempleAmbush(dungeon);
        StepDungeonCombat(dungeon);
        StepDungeonTempleSigil(dungeon);
        StepDungeonSilverVents(dungeon);
        StepDungeonCurse(dungeon);
        double moveX = 0;
        double moveY = 0;
        if ((buttons & Left) != 0) moveX--;
        if ((buttons & Right) != 0) moveX++;
        if ((buttons & Up) != 0) moveY--;
        if ((buttons & Down) != 0) moveY++;
        if (moveX != 0 || moveY != 0)
        {
            dungeon.TargetX = dungeon.KarlX;
            dungeon.TargetY = dungeon.KarlY;
        }
        else
        {
            double dx = dungeon.TargetX - dungeon.KarlX;
            double dy = dungeon.TargetY - dungeon.KarlY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 2)
            {
                moveX = dx / distance;
                moveY = dy / distance;
            }
        }
        double length = Math.Sqrt(moveX * moveX + moveY * moveY);
        dungeon.Moving = false;
        if (length > 0 && dungeon.AttackAge == 0 && dungeon.HurtAge == 0)
        {
            moveX /= length;
            moveY /= length;
            double speed = (buttons & Slow) != 0 ? 1.05 : 1.65;
            double beforeX = dungeon.KarlX;
            double beforeY = dungeon.KarlY;
            MoveDungeonKarl(dungeon, moveX * speed, moveY * speed);
            double travelledX = dungeon.KarlX - beforeX;
            double travelledY = dungeon.KarlY - beforeY;
            double travelled = Math.Sqrt(travelledX * travelledX + travelledY * travelledY);
            dungeon.Moving = travelled > 0.05;
            dungeon.GaitDistance += travelled;
            dungeon.Facing = Math.Abs(moveX) > Math.Abs(moveY)
                ? moveX < 0 ? DungeonFacing.West : DungeonFacing.East
                : moveY < 0 ? DungeonFacing.North : DungeonFacing.South;
        }
        dungeon.CameraX = Math.Clamp((int)Math.Round(dungeon.KarlX - _width / 2.0), 0, dungeon.RoomWidth - _width);
        dungeon.CameraY = Math.Clamp((int)Math.Round(dungeon.KarlY - _height / 2.0), 0, dungeon.RoomHeight - _height);
        if (dungeon.Depth == 1 && dungeon.DoorReady && dungeon.PendingDoorEntry && dungeon.DescentWarningPlayed &&
            _bossRadioCard is null && DistanceSquared(650, 218, dungeon.KarlX, dungeon.KarlY) < 34 * 34)
            BeginDungeonDepthTwo(dungeon);
        else if (dungeon.Depth == 2 && dungeon.PendingReturnEntry &&
            DistanceSquared(60, 410, dungeon.KarlX, dungeon.KarlY) < 34 * 34)
            ReturnToDungeonDepthOne(dungeon);
        else if (dungeon.Depth == 2 && dungeon.PendingDepthThreeEntry && dungeon.DepthThreeOpen &&
            DistanceSquared(1190, 390, dungeon.KarlX, dungeon.KarlY) < 42 * 42)
            BeginDungeonDepthThree(dungeon);
        else if (dungeon.Depth == 3 && dungeon.PendingDepthThreeReturn &&
            DistanceSquared(60, 240, dungeon.KarlX, dungeon.KarlY) < 38 * 38)
            ReturnToDungeonDepthTwo(dungeon);
        else if (dungeon.Depth == 3 && dungeon.PendingTempleEntry && dungeon.TempleOpen &&
            DistanceSquared(1320, 450, dungeon.KarlX, dungeon.KarlY) < 44 * 44)
            BeginDungeonDepthFour(dungeon);
        else if (dungeon.Depth == 4 && dungeon.PendingTempleReturn &&
            DistanceSquared(60, 250, dungeon.KarlX, dungeon.KarlY) < 38 * 38)
            ReturnToDungeonDepthThree(dungeon);
    }

    private void ConsumeDungeonHealthTincture(DungeonState dungeon, DungeonItem tincture)
    {
        dungeon.Health = Math.Min(dungeon.MaxHealth, dungeon.Health + 35);
        dungeon.Items.Remove(tincture);
        _audio?.Trigger(StormaktSound.Deploy);
        WriteDungeonSave(dungeon, "autosave");
    }

    private void ConsumeQuickDungeonPotion(DungeonState dungeon)
    {
        if (dungeon.Health >= dungeon.MaxHealth) return;
        DungeonItem? tincture = dungeon.Items.Where(item => item.Definition == 14 && item.QuickSlot >= 0)
            .OrderBy(item => item.QuickSlot).FirstOrDefault();
        if (tincture is not null) ConsumeDungeonHealthTincture(dungeon, tincture);
    }

    private static bool StoreDungeonPickup(DungeonState dungeon, DungeonItem item)
    {
        item.OnGround = false;
        item.InStash = false;
        item.QuickSlot = -1;
        bool stored = item.Definition == 14 && TryPlaceFirstPotionBelt(dungeon, item) || TryPlaceFirstFree(dungeon, item);
        if (stored)
        {
            WriteDungeonSave(dungeon, "autosave");
            return true;
        }
        item.OnGround = true;
        item.QuickSlot = -1;
        return false;
    }

    private static bool TryPlaceFirstPotionBelt(DungeonState dungeon, DungeonItem item)
    {
        if (item.Definition != 14) return false;
        for (int slot = 0; slot < 4; slot++)
        {
            if (dungeon.Items.Any(candidate => candidate.Id != item.Id && candidate.QuickSlot == slot)) continue;
            item.QuickSlot = slot;
            item.GridX = -1;
            item.GridY = -1;
            item.Equipped = DungeonEquipmentSlot.None;
            item.OnGround = false;
            item.InStash = false;
            return true;
        }
        return false;
    }

    private void DropDungeonItem(DungeonState dungeon, DungeonItem item)
    {
        (double facingX, double facingY) = DungeonFacingVector(dungeon.Facing);
        double dropX = dungeon.KarlX + facingX * 34;
        double dropY = dungeon.KarlY + facingY * 34;
        if (DungeonBlocked(dungeon, dropX, dropY))
            (dropX, dropY) = FindNearestDungeonFloor(dungeon, dungeon.KarlX, dungeon.KarlY);
        item.OnGround = true;
        item.InStash = false;
        item.QuickSlot = -1;
        item.Equipped = DungeonEquipmentSlot.None;
        item.GridX = -1;
        item.GridY = -1;
        item.WorldX = dropX;
        item.WorldY = dropY;
        dungeon.SelectedItemId = 0;
        dungeon.InspectedItemId = 0;
        _audio?.Trigger(StormaktSound.Deploy);
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void BeginDungeonDepthTwo(DungeonState dungeon)
    {
        dungeon.Depth = 2;
        dungeon.RoomWidth = 1280;
        dungeon.RoomHeight = 820;
        dungeon.KarlX = 120;
        dungeon.KarlY = 410;
        dungeon.TargetX = dungeon.KarlX;
        dungeon.TargetY = dungeon.KarlY;
        dungeon.CameraX = 0;
        dungeon.CameraY = 270;
        dungeon.RoomClear = false;
        dungeon.RoomClearSaved = false;
        dungeon.DoorReady = false;
        dungeon.Enemies.Clear();
        AddDungeonEnemy(dungeon, 330, 330, DungeonEnemyType.Stormer, 58, 1);
        AddDungeonEnemy(dungeon, 390, 470, DungeonEnemyType.Pikeman, 76, 1);
        AddDungeonEnemy(dungeon, 610, 250, DungeonEnemyType.Stormer, 64, 2);
        AddDungeonEnemy(dungeon, 690, 360, DungeonEnemyType.Pikeman, 82, 2);
        AddDungeonEnemy(dungeon, 780, 545, DungeonEnemyType.Stormer, 64, 2);
        AddDungeonEnemy(dungeon, 960, 270, DungeonEnemyType.Pikeman, 88, 3);
        AddDungeonEnemy(dungeon, 1030, 510, DungeonEnemyType.Stormer, 70, 3);
        AddDungeonEnemy(dungeon, 1120, 450, DungeonEnemyType.Pikeman, 92, 3);
        AddDungeonEnemy(dungeon, 1060, 390, DungeonEnemyType.SilverFogde, 240, 3);
        dungeon.Chests.Clear();
        dungeon.Chests.Add(new DungeonChest(1, 465, 205) { Depth = 2 });
        dungeon.Chests.Add(new DungeonChest(2, 815, 650) { Depth = 2 });
        dungeon.Chests.Add(new DungeonChest(3, 1165, 170) { Depth = 2 });
        dungeon.MaxZoneReached = 1;
        WriteDungeonSave(dungeon, "autosave");
    }

    private void RestartDungeonAfterDeath(DungeonState fallen)
    {
        var restarted = new DungeonState
        {
            Age = 150,
            RoomWidth = 720,
            RoomHeight = 440,
            KarlX = 92,
            KarlY = 228,
            TargetX = 92,
            TargetY = 228,
            Facing = DungeonFacing.South,
            Health = Math.Max(100, fallen.MaxHealth),
            MaxHealth = Math.Max(100, fallen.MaxHealth),
            Power = 35,
            Depth = 1,
            DoorHealth = 0,
            DoorReady = true,
            DescentWarningPlayed = true,
            Level = fallen.Level,
            Experience = fallen.Experience,
            NextItemId = fallen.NextItemId,
            NextEnemyId = fallen.NextEnemyId,
        };
        foreach (DungeonItem item in fallen.Items.Where(item => !item.OnGround))
        {
            item.OnGround = false;
            restarted.Items.Add(item);
        }
        if (restarted.Items.Count == 0) SeedDungeonInventory(restarted);
        SeedDungeonEncounter(restarted);
        _dungeon = restarted;
        ActivateBossRadio(DungeonNearDeathRadio);
        WriteDungeonSave(restarted, "autosave");
    }

    private static void ReturnToDungeonDepthOne(DungeonState dungeon)
    {
        dungeon.Depth = 1;
        dungeon.RoomWidth = 720;
        dungeon.RoomHeight = 440;
        dungeon.KarlX = 610;
        dungeon.KarlY = 218;
        dungeon.TargetX = 610;
        dungeon.TargetY = 218;
        dungeon.CameraX = 320;
        dungeon.CameraY = 78;
        dungeon.DoorHealth = 0;
        dungeon.DoorReady = true;
        dungeon.DescentWarningPlayed = true;
        dungeon.PendingDoorEntry = false;
        dungeon.PendingReturnEntry = false;
        dungeon.RoomClear = false;
        dungeon.RoomClearSaved = false;
        dungeon.Enemies.RemoveAll(enemy => enemy.Depth == 1);
        dungeon.Chests.RemoveAll(chest => chest.Depth == 1);
        dungeon.Items.RemoveAll(item => item.OnGround);
        SeedDungeonEncounter(dungeon);
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void BeginDungeonDepthThree(DungeonState dungeon)
    {
        dungeon.Depth = 3;
        dungeon.RoomWidth = 1400;
        dungeon.RoomHeight = 900;
        dungeon.KarlX = 115;
        dungeon.KarlY = 240;
        dungeon.TargetX = 115;
        dungeon.TargetY = 240;
        dungeon.CameraX = 0;
        dungeon.CameraY = 100;
        dungeon.PendingDepthThreeEntry = false;
        if (dungeon.Enemies.All(enemy => enemy.Depth != 3))
        {
            AddDungeonEnemy(dungeon, 330, 250, DungeonEnemyType.UndeadMiner, 72, 31);
            AddDungeonEnemy(dungeon, 410, 430, DungeonEnemyType.UndeadMiner, 72, 31);
            AddDungeonEnemy(dungeon, 570, 650, DungeonEnemyType.TuonelaGuard, 105, 32);
            AddDungeonEnemy(dungeon, 650, 290, DungeonEnemyType.UndeadMiner, 80, 32);
            AddDungeonEnemy(dungeon, 760, 480, DungeonEnemyType.TuonelaGuard, 110, 32);
            AddDungeonEnemy(dungeon, 900, 690, DungeonEnemyType.UndeadMiner, 88, 33);
            AddDungeonEnemy(dungeon, 980, 250, DungeonEnemyType.TuonelaGuard, 120, 33);
            AddDungeonEnemy(dungeon, 1080, 520, DungeonEnemyType.UndeadMiner, 92, 33);
            AddDungeonEnemy(dungeon, 1190, 330, DungeonEnemyType.TuonelaGuard, 125, 34);
            AddDungeonEnemy(dungeon, 1260, 590, DungeonEnemyType.UndeadMiner, 96, 34);
            AddDungeonEnemy(dungeon, 1240, 450, DungeonEnemyType.BlindShepherd, 340, 34);
            dungeon.Chests.Add(new DungeonChest(31, 430, 150) { Depth = 3 });
            dungeon.Chests.Add(new DungeonChest(32, 790, 760) { Depth = 3 });
            dungeon.Chests.Add(new DungeonChest(33, 1120, 160) { Depth = 3 });
        }
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void ReturnToDungeonDepthTwo(DungeonState dungeon)
    {
        dungeon.Depth = 2;
        dungeon.RoomWidth = 1280;
        dungeon.RoomHeight = 820;
        dungeon.KarlX = 1140;
        dungeon.KarlY = 390;
        dungeon.TargetX = 1140;
        dungeon.TargetY = 390;
        dungeon.CameraX = 880;
        dungeon.CameraY = 250;
        dungeon.PendingDepthThreeReturn = false;
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void BeginDungeonDepthFour(DungeonState dungeon)
    {
        dungeon.Depth = 4; dungeon.RoomWidth = 1760; dungeon.RoomHeight = 760;
        dungeon.KarlX = 115; dungeon.KarlY = 250; dungeon.TargetX = 115; dungeon.TargetY = 250;
        dungeon.CameraX = 0; dungeon.CameraY = 110; dungeon.PendingTempleEntry = false;
        if (!dungeon.Enemies.Any(enemy => enemy.Depth == 4))
        {
            AddDungeonEnemy(dungeon, 420, 220, DungeonEnemyType.TuonelaGuard, 145, 41);
            AddDungeonEnemy(dungeon, 540, 500, DungeonEnemyType.UndeadMiner, 105, 41);
            AddDungeonEnemy(dungeon, 720, 310, DungeonEnemyType.TuonelaGuard, 160, 42);
            AddDungeonEnemy(dungeon, 870, 620, DungeonEnemyType.UndeadMiner, 115, 42);
            AddDungeonEnemy(dungeon, 1010, 380, DungeonEnemyType.TuonelaGuard, 180, 43);
            dungeon.Chests.Add(new DungeonChest(41, 610, 135) { Depth = 4 });
            dungeon.Chests.Add(new DungeonChest(42, 1010, 665) { Depth = 4 });
        }
        if (!dungeon.Enemies.Any(enemy => enemy.Depth == 4 && enemy.Type == DungeonEnemyType.LemminkainenShadow))
        {
            AddDungeonEnemy(dungeon, 1040, 380, DungeonEnemyType.LemminkainenShadow, 420, 43);
            dungeon.Enemies.Last().Phase = 0;
        }
        EnsureDungeonFirstSigilEncounter(dungeon);
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void EnsureDungeonFirstSigilEncounter(DungeonState dungeon)
    {
        if (dungeon.Enemies.All(enemy => enemy.Depth != 4 || enemy.Zone != 44))
        {
            AddDungeonEnemy(dungeon, 1325, 250, DungeonEnemyType.TuonelaGuard, 175, 44);
            AddDungeonEnemy(dungeon, 1435, 520, DungeonEnemyType.UndeadMiner, 125, 44);
            AddDungeonEnemy(dungeon, 1550, 300, DungeonEnemyType.TuonelaGuard, 190, 44);
        }
        if (dungeon.Chests.All(chest => chest.Id != 43))
            dungeon.Chests.Add(new DungeonChest(43, 1515, 610) { Depth = 4 });
    }

    private void StepDungeonTempleAmbush(DungeonState dungeon)
    {
        if (dungeon.Depth != 4) return;
        DungeonEnemy? shadow = dungeon.Enemies.FirstOrDefault(enemy =>
            enemy.Depth == 4 && enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.State != DungeonEnemyState.Dead);
        if (shadow is null || shadow.Phase != 0 || dungeon.KarlX < 900) return;
        shadow.Phase = 1;
        shadow.X = Math.Max(760, dungeon.KarlX - 82);
        shadow.Y = Math.Clamp(dungeon.KarlY + 24, 100, dungeon.RoomHeight - 70);
        shadow.State = DungeonEnemyState.Telegraph;
        shadow.StateAge = 0;
        shadow.SpecialSerial = 0;
        dungeon.TargetX = dungeon.KarlX;
        dungeon.TargetY = dungeon.KarlY;
        _audio?.Trigger(StormaktSound.DungeonSilverShatter);
        WriteDungeonSave(dungeon, "autosave");
    }

    private void StepDungeonTempleSigil(DungeonState dungeon)
    {
        if (dungeon.Depth != 4 || (dungeon.LoreMask & DungeonFirstSigilMask) != 0) return;
        bool shadowDefeated = dungeon.Enemies.Any(enemy => enemy.Depth == 4 &&
            enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.State == DungeonEnemyState.Dead);
        bool courtyardClear = dungeon.Enemies.Where(enemy => enemy.Depth == 4 && enemy.Zone == 44)
            .All(enemy => enemy.State == DungeonEnemyState.Dead);
        if (!shadowDefeated || !courtyardClear ||
            DistanceSquared(1580, 380, dungeon.KarlX, dungeon.KarlY) >= 48 * 48) return;
        dungeon.LoreMask |= DungeonFirstSigilMask;
        dungeon.Curse = Math.Max(0, dungeon.Curse - 35);
        DungeonItem fragment = AddDungeonItem(dungeon, 11, -1, -1, DungeonEquipmentSlot.None,
            DungeonItemRarity.Runic);
        fragment.OnGround = true;
        fragment.WorldX = 1580;
        fragment.WorldY = 410;
        ActivateBossRadio(DungeonFirstSigilRadio);
        _audio?.Trigger(StormaktSound.DungeonSilverShatter);
        WriteDungeonSave(dungeon, "autosave");
    }

    private static void ReturnToDungeonDepthThree(DungeonState dungeon)
    {
        dungeon.Depth = 3; dungeon.RoomWidth = 1400; dungeon.RoomHeight = 900;
        dungeon.KarlX = 1260; dungeon.KarlY = 450; dungeon.TargetX = 1260; dungeon.TargetY = 450;
        dungeon.CameraX = 1000; dungeon.CameraY = 310; dungeon.PendingTempleReturn = false;
        WriteDungeonSave(dungeon, "autosave");
    }

    private void OpenDungeonChest(DungeonState dungeon, DungeonChest chest)
    {
        if (chest.Open) return;
        chest.Open = true;
        chest.OpenAge = 1;
        _audio?.Trigger(StormaktSound.Deploy);
        int[] definitions = chest.Id switch
        {
            1 => [5, 10], 2 => [2, 9], 3 => [6, 11],
            31 => [10, 11], 32 => [6, 10], 41 => [10, 11], 42 => [4, 12], _ => [11, 9],
        };
        for (int index = 0; index < definitions.Length; index++)
        {
            DungeonItem loot = AddDungeonItem(dungeon, definitions[index], -1, -1, DungeonEquipmentSlot.None,
                chest.Id is 3 or >= 31 ? DungeonItemRarity.Runic : DungeonItemRarity.Silverbound);
            loot.OnGround = true;
            loot.WorldX = chest.X + (index == 0 ? -13 : 13);
            loot.WorldY = chest.Y + 16;
        }
        WriteDungeonSave(dungeon, "autosave");
    }

    private static int DungeonZone(DungeonState dungeon) => dungeon.Depth switch
    {
        2 => DungeonZoneFromX(dungeon.KarlX),
        3 => dungeon.KarlX < 500 ? 31 : dungeon.KarlX < 850 ? 32 : dungeon.KarlX < 1150 ? 33 : 34,
        4 => dungeon.KarlX < 600 ? 41 : dungeon.KarlX < 900 ? 42 : dungeon.KarlX < 1200 ? 43 : 44,
        _ => 0,
    };
    private static int DungeonZoneFromX(double x) => x < 520 ? 1 : x < 900 ? 2 : 3;

    private static readonly (int X, int Y)[] DungeonSilverVents =
        [(410, 320), (655, 590), (900, 220), (1110, 610)];
    private static readonly (int X, int Y)[] DungeonBlackWaters =
        [(360, 350), (720, 610), (1040, 360)];
    private static readonly (int X, int Y)[] DungeonHolyShrines =
        [(500, 150), (900, 740)];
    private static readonly (int X, int Y)[] DungeonTempleWaters =
        [(350, 410), (775, 185), (990, 505), (1370, 150), (1660, 570)];
    private static readonly (int X, int Y)[] DungeonTempleShrines =
        [(210, 600), (660, 600), (1090, 165), (1580, 380)];
    private static readonly (int X, int Y)[] DungeonTempleGuardianStatues =
        [(1275, 190), (1660, 190)];
    private static readonly (int X, int Y)[] DungeonTempleClosedSarcophagi =
        [(1290, 645), (1650, 650)];
    private static readonly (int X, int Y)[] DungeonTempleOpenSarcophagi =
        [(1450, 135)];

    private static void StepDungeonCurse(DungeonState dungeon)
    {
        if (dungeon.Depth is not (3 or 4)) return;
        (int X, int Y)[] waters = dungeon.Depth == 3 ? DungeonBlackWaters : DungeonTempleWaters;
        (int X, int Y)[] shrines = dungeon.Depth == 3 ? DungeonHolyShrines : DungeonTempleShrines;
        bool inWater = waters.Any(water =>
            DistanceSquared(water.X, water.Y, dungeon.KarlX, dungeon.KarlY) < 72 * 72);
        bool inLight = shrines.Any(shrine =>
            DistanceSquared(shrine.X, shrine.Y, dungeon.KarlX, dungeon.KarlY) < 74 * 74);
        if (dungeon.Age % 10 == 0)
        {
            if (inLight) dungeon.Curse = Math.Max(0, dungeon.Curse - 4);
            else if (inWater) ApplyDungeonCurse(dungeon, 3);
            else if (dungeon.Curse > 0 && dungeon.Age % 30 == 0) dungeon.Curse--;
        }
        if (dungeon.Curse >= 100 && dungeon.Age % 30 == 0)
        {
            ApplyDungeonDamage(dungeon, 6);
            dungeon.HurtAge = 12;
        }
    }

    private static int DungeonArmor(DungeonState dungeon) => dungeon.Items
        .Where(item => item.Equipped != DungeonEquipmentSlot.None)
        .Sum(item => DungeonItemDefinitions[item.Definition].Armor + (int)item.Rarity * 2);

    private static int DungeonCurseWard(DungeonState dungeon) => Math.Min(75, dungeon.Items
        .Where(item => item.Equipped != DungeonEquipmentSlot.None)
        .Sum(item => DungeonItemDefinitions[item.Definition].CurseWard));

    private static int DungeonRequiredLevel(DungeonItem item) => Math.Max(
        DungeonItemDefinitions[item.Definition].RequiredLevel,
        item.Rarity >= DungeonItemRarity.Runic ? 2 : 1);

    private static void ApplyDungeonDamage(DungeonState dungeon, int rawDamage)
    {
        int reduction = Math.Min(rawDamage - 1, DungeonArmor(dungeon) / 12);
        dungeon.Health = Math.Max(0, dungeon.Health - Math.Max(1, rawDamage - reduction));
    }

    private static void ApplyDungeonCurse(DungeonState dungeon, int rawCurse)
    {
        int applied = Math.Max(1, rawCurse * (100 - DungeonCurseWard(dungeon)) / 100);
        dungeon.Curse = Math.Min(100, dungeon.Curse + applied);
    }

    private static void StepDungeonSilverVents(DungeonState dungeon)
    {
        if (dungeon.Depth != 2) return;
        if (dungeon.ForcedVentAge > 0) dungeon.ForcedVentAge--;
        foreach (DungeonSilverWave wave in dungeon.SilverWaves)
        {
            wave.Age++;
            wave.X += wave.Dx * 3.2;
            wave.Y += wave.Dy * 3.2;
            if (!wave.HitKarl && DistanceSquared(wave.X, wave.Y, dungeon.KarlX, dungeon.KarlY) < 22 * 22)
            {
                wave.HitKarl = true;
                ApplyDungeonDamage(dungeon, 10 + wave.Phase * 2);
                dungeon.HurtAge = 14;
            }
        }
        dungeon.SilverWaves.RemoveAll(wave => wave.Age > 95 ||
            wave.X < 20 || wave.X > dungeon.RoomWidth - 20 || wave.Y < 40 || wave.Y > dungeon.RoomHeight - 20);
        foreach (DungeonChest chest in dungeon.Chests)
            if (chest.OpenAge > 0 && chest.OpenAge < 45) chest.OpenAge++;
        int zone = DungeonZone(dungeon);
        if (zone > dungeon.MaxZoneReached)
        {
            dungeon.MaxZoneReached = zone;
            WriteDungeonSave(dungeon, "autosave");
        }
        for (int index = 0; index < DungeonSilverVents.Length; index++)
        {
            (int ventX, int ventY) = DungeonSilverVents[index];
            int cycle = (dungeon.Age + index * 43) % 180;
            bool forced = dungeon.ForcedVentAge > 0;
            if ((!forced && (cycle is < 54 or >= 102)) || (forced ? dungeon.ForcedVentAge : cycle) % 15 != 0) continue;
            if (DistanceSquared(ventX, ventY, dungeon.KarlX, dungeon.KarlY) < 54 * 54)
            {
                ApplyDungeonDamage(dungeon, 4);
                dungeon.HurtAge = Math.Max(dungeon.HurtAge, 9);
            }
            foreach (DungeonEnemy enemy in dungeon.Enemies.Where(enemy => enemy.State != DungeonEnemyState.Dead &&
                DistanceSquared(ventX, ventY, enemy.X, enemy.Y) < 58 * 58))
            {
                enemy.Health -= 6;
                enemy.State = enemy.Health <= 0 ? DungeonEnemyState.Dead : DungeonEnemyState.Stagger;
                enemy.StateAge = 0;
                if (enemy.State == DungeonEnemyState.Dead) DefeatDungeonEnemy(dungeon, enemy);
            }
        }
    }

    private bool StepSilverFogdeSpecial(DungeonState dungeon, DungeonEnemy fogde)
    {
        fogde.Phase = fogde.Health <= fogde.MaxHealth * 3 / 10 ? 3 :
            fogde.Health <= fogde.MaxHealth * 6 / 10 ? 2 : 1;
        if (fogde.SpecialKind == 0)
        {
            if (fogde.SpecialCooldown > 0) { fogde.SpecialCooldown--; return false; }
            fogde.SpecialSerial++;
            fogde.SpecialKind = fogde.Phase >= 2 && fogde.SpecialSerial % 2 == 0 ? 2 : 1;
            fogde.SpecialAge = 1;
            fogde.State = DungeonEnemyState.Telegraph;
            return true;
        }
        fogde.SpecialAge++;
        fogde.State = fogde.SpecialAge < 34 ? DungeonEnemyState.Telegraph : DungeonEnemyState.Attack;
        if (fogde.SpecialAge == 36)
        {
            _audio?.Trigger(StormaktSound.DungeonSilverWave);
            if (fogde.SpecialKind == 1)
            {
                double dx = dungeon.KarlX - fogde.X;
                double dy = dungeon.KarlY - fogde.Y;
                double length = Math.Max(0.1, Math.Sqrt(dx * dx + dy * dy));
                double angle = Math.Atan2(dy, dx);
                int count = fogde.Phase == 1 ? 1 : fogde.Phase == 2 ? 3 : 5;
                for (int index = 0; index < count; index++)
                {
                    double spread = (index - (count - 1) / 2.0) * 0.18;
                    dungeon.SilverWaves.Add(new DungeonSilverWave(fogde.X, fogde.Y,
                        Math.Cos(angle + spread), Math.Sin(angle + spread), fogde.Phase));
                }
            }
            else dungeon.ForcedVentAge = fogde.Phase == 2 ? 75 : 105;
        }
        if (fogde.SpecialAge <= 72) return true;
        fogde.SpecialKind = 0;
        fogde.SpecialAge = 0;
        fogde.SpecialCooldown = fogde.Phase switch { 1 => 150, 2 => 105, _ => 72 };
        fogde.State = DungeonEnemyState.Approach;
        fogde.StateAge = 0;
        return false;
    }

    private void StartDungeonAttack(DungeonState dungeon, RtsPointer pointer)
    {
        if (pointer.Inside)
        {
            double aimX = pointer.X + dungeon.CameraX - dungeon.KarlX;
            double aimY = pointer.Y + dungeon.CameraY - dungeon.KarlY;
            dungeon.Facing = Math.Abs(aimX) > Math.Abs(aimY)
                ? aimX < 0 ? DungeonFacing.West : DungeonFacing.East
                : aimY < 0 ? DungeonFacing.North : DungeonFacing.South;
        }
        if (dungeon.AttackAge > 0)
        {
            if (dungeon.AttackAge >= 10) dungeon.AttackQueued = true;
            return;
        }
        dungeon.AttackAge = 1;
        dungeon.AttackSerial++;
        dungeon.AttackCombo = dungeon.AttackCombo % 3 + 1;
        dungeon.TargetX = dungeon.KarlX;
        dungeon.TargetY = dungeon.KarlY;
        dungeon.Guarding = false;
        bool hammer = dungeon.Items.Any(item => item.Equipped == DungeonEquipmentSlot.MainHand && item.Definition == 3);
        _audio?.Trigger(hammer ? StormaktSound.DungeonHammerImpact : StormaktSound.DungeonSwordSlash);
    }

    private void StepDungeonCombat(DungeonState dungeon)
    {
        bool hammer = dungeon.Items.Any(item => item.Equipped == DungeonEquipmentSlot.MainHand && item.Definition == 3);
        int contactAge = hammer ? 14 : 9;
        int endAge = hammer ? 31 : 23;
        if (dungeon.AttackAge > 0)
        {
            if (dungeon.AttackAge == contactAge)
            {
                (double fx, double fy) = DungeonFacingVector(dungeon.Facing);
                int weaponDamage = dungeon.Items.Where(item => item.Equipped == DungeonEquipmentSlot.MainHand)
                    .Select(item => DungeonItemDefinitions[item.Definition].Damage + item.PowerRoll / 3)
                    .DefaultIfEmpty(8).Max() + Math.Max(0, dungeon.Level - 1) * 2;
                foreach (DungeonEnemy enemy in dungeon.Enemies.Where(enemy => enemy.Depth == dungeon.Depth &&
                    enemy.State != DungeonEnemyState.Dead))
                {
                    double dx = enemy.X - dungeon.KarlX;
                    double dy = enemy.Y - dungeon.KarlY;
                    double range = hammer ? 53 : 47;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > range || distance < 0.1 || (dx * fx + dy * fy) / distance < 0.22 || enemy.LastHitSerial == dungeon.AttackSerial) continue;
                    enemy.LastHitSerial = dungeon.AttackSerial;
                    enemy.Health -= weaponDamage;
                    enemy.State = enemy.Health <= 0 ? DungeonEnemyState.Dead : DungeonEnemyState.Stagger;
                    enemy.StateAge = 0;
                    if (enemy.State == DungeonEnemyState.Dead) DefeatDungeonEnemy(dungeon, enemy);
                    dungeon.Power = Math.Min(100, dungeon.Power + 5);
                    dungeon.HitStop = hammer ? 4 : 2;
                    _audio?.Trigger(hammer ? StormaktSound.DungeonHammerImpact : StormaktSound.DungeonSwordHit);
                }
                if (dungeon.Depth == 1 && dungeon.RoomClear && !dungeon.DoorReady)
                {
                    double doorDx = 650 - dungeon.KarlX;
                    double doorDy = 218 - dungeon.KarlY;
                    double doorDistance = Math.Sqrt(doorDx * doorDx + doorDy * doorDy);
                    if (doorDistance < 58 && doorDistance > 0.1 && (doorDx * fx + doorDy * fy) / doorDistance > 0.2)
                    {
                        dungeon.DoorHealth = Math.Max(0, dungeon.DoorHealth - (hammer ? 2 : 1));
                        _audio?.Trigger(hammer ? StormaktSound.DungeonHammerImpact : StormaktSound.DungeonSwordHit);
                        if (dungeon.DoorHealth == 0)
                        {
                            dungeon.DoorReady = true;
                            dungeon.DescentWarningPlayed = true;
                            dungeon.PendingDoorEntry = false;
                            ActivateBossRadio(DungeonDepthTwoWarningRadio);
                            WriteDungeonSave(dungeon, "autosave");
                        }
                    }
                }
            }
            dungeon.AttackAge++;
            if (dungeon.AttackAge > endAge)
            {
                dungeon.AttackAge = 0;
                if (dungeon.AttackQueued)
                {
                    dungeon.AttackQueued = false;
                    dungeon.AttackAge = 1;
                    dungeon.AttackSerial++;
                    dungeon.AttackCombo = dungeon.AttackCombo % 3 + 1;
                }
            }
        }

        foreach (DungeonEnemy enemy in dungeon.Enemies.Where(enemy => enemy.Depth == dungeon.Depth))
        {
            if (enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.Phase == 0) continue;
            enemy.StateAge++;
            if (enemy.State == DungeonEnemyState.Dead)
            {
                if (enemy.Type == DungeonEnemyType.SilverFogde && enemy.StateAge == 80 && !dungeon.DepthThreeOpen)
                {
                    dungeon.DepthThreeOpen = true;
                    _audio?.Trigger(StormaktSound.DungeonSilverShatter);
                    WriteDungeonSave(dungeon, "autosave");
                }
                if (enemy.Type == DungeonEnemyType.BlindShepherd && enemy.StateAge == 90 && !dungeon.TempleOpen)
                {
                    dungeon.TempleOpen = true;
                    WriteDungeonSave(dungeon, "autosave");
                }
                continue;
            }
            if (enemy.State == DungeonEnemyState.Stagger)
            {
                if (enemy.StateAge > 18) { enemy.State = DungeonEnemyState.Approach; enemy.StateAge = 0; }
                continue;
            }
            if (enemy.Type == DungeonEnemyType.SilverFogde && StepSilverFogdeSpecial(dungeon, enemy)) continue;
            double dx = dungeon.KarlX - enemy.X;
            double dy = dungeon.KarlY - enemy.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            int currentZone = DungeonZone(dungeon);
            if (dungeon.Depth >= 2 && enemy.Zone != currentZone && distance > 180) continue;
            enemy.FacingLeft = dx < 0;
            double attackRange = enemy.Type switch
            {
                DungeonEnemyType.Pikeman => 54, DungeonEnemyType.SilverFogde => 48,
                DungeonEnemyType.TuonelaGuard => 82, DungeonEnemyType.BlindShepherd => 92,
                DungeonEnemyType.LemminkainenShadow => 74,
                DungeonEnemyType.UndeadMiner => 38, _ => 35,
            };
            if (enemy.State == DungeonEnemyState.Approach)
            {
                if (distance <= attackRange)
                {
                    enemy.State = DungeonEnemyState.Telegraph;
                    enemy.StateAge = 0;
                }
                else if (distance > 0.1)
                {
                    // Advance in weighted steps instead of homing straight at Karl every frame.
                    // Each soldier has his own cadence and yields to nearby comrades.
                    int stridePhase = (enemy.StateAge + enemy.Id * 13) % 54;
                    double cadence = stridePhase >= 47 ? 0.0 : stridePhase is < 5 or > 40 ? 0.55 : 1.0;
                    double speed = (enemy.Type switch
                    {
                        DungeonEnemyType.Pikeman => 0.27, DungeonEnemyType.SilverFogde => 0.22,
                        DungeonEnemyType.TuonelaGuard => 0.24, DungeonEnemyType.BlindShepherd => 0.18,
                        DungeonEnemyType.LemminkainenShadow => 0.34,
                        DungeonEnemyType.UndeadMiner => 0.30, _ => 0.39,
                    }) * cadence;
                    double desiredX = dx / distance * speed;
                    double desiredY = dy / distance * speed;
                    foreach (DungeonEnemy other in dungeon.Enemies)
                    {
                        if (other.Id == enemy.Id || other.State == DungeonEnemyState.Dead) continue;
                        double apartX = enemy.X - other.X;
                        double apartY = enemy.Y - other.Y;
                        double apartSquared = apartX * apartX + apartY * apartY;
                        if (apartSquared is > 0.01 and < 28 * 28)
                        {
                            double apart = Math.Sqrt(apartSquared);
                            double push = (28 - apart) / 28.0 * 0.24;
                            desiredX += apartX / apart * push;
                            desiredY += apartY / apart * push;
                        }
                    }
                    enemy.VelocityX += (desiredX - enemy.VelocityX) * 0.18;
                    enemy.VelocityY += (desiredY - enemy.VelocityY) * 0.18;
                    double beforeX = enemy.X;
                    double beforeY = enemy.Y;
                    MoveDungeonEnemy(dungeon, enemy, enemy.VelocityX, enemy.VelocityY);
                    enemy.GaitDistance += Math.Sqrt((enemy.X - beforeX) * (enemy.X - beforeX) + (enemy.Y - beforeY) * (enemy.Y - beforeY));
                }
            }
            else if (enemy.State == DungeonEnemyState.Telegraph && enemy.StateAge >
                (enemy.Type switch
                {
                    DungeonEnemyType.Pikeman => 30, DungeonEnemyType.SilverFogde => 42,
                    DungeonEnemyType.TuonelaGuard => 34, DungeonEnemyType.BlindShepherd => 52,
                    DungeonEnemyType.LemminkainenShadow => 30,
                    DungeonEnemyType.UndeadMiner => 26, _ => 22,
                }))
            {
                enemy.State = DungeonEnemyState.Attack;
                enemy.StateAge = 0;
            }
            else if (enemy.State == DungeonEnemyState.Attack)
            {
                int impact = enemy.Type switch
                {
                    DungeonEnemyType.Pikeman => 12, DungeonEnemyType.SilverFogde => 18,
                    DungeonEnemyType.TuonelaGuard => 15, DungeonEnemyType.BlindShepherd => 24,
                    DungeonEnemyType.LemminkainenShadow => 13,
                    DungeonEnemyType.UndeadMiner => 10, _ => 8,
                };
                if (enemy.StateAge == impact && distance <= attackRange + 9)
                {
                    if (dungeon.Guarding)
                    {
                        enemy.State = DungeonEnemyState.Stagger;
                        enemy.StateAge = 0;
                        dungeon.Power = Math.Min(100, dungeon.Power + 10);
                        dungeon.HitStop = 2;
                        _audio?.Trigger(StormaktSound.DungeonParry);
                    }
                    else
                    {
                        int damage = enemy.Type switch
                        {
                            DungeonEnemyType.Pikeman => 11, DungeonEnemyType.SilverFogde => 18,
                            DungeonEnemyType.TuonelaGuard => 12, DungeonEnemyType.BlindShepherd => 19,
                            DungeonEnemyType.LemminkainenShadow => 16,
                            DungeonEnemyType.UndeadMiner => 9, _ => 7,
                        };
                        ApplyDungeonDamage(dungeon, damage);
                        if (enemy.Type == DungeonEnemyType.TuonelaGuard) ApplyDungeonCurse(dungeon, 8);
                        if (enemy.Type == DungeonEnemyType.BlindShepherd) ApplyDungeonCurse(dungeon, 20);
                        if (enemy.Type == DungeonEnemyType.LemminkainenShadow) ApplyDungeonCurse(dungeon, 12);
                        dungeon.HurtAge = 18;
                        dungeon.HitStop = 3;
                    }
                }
                if (enemy.State == DungeonEnemyState.Attack && enemy.StateAge > impact + 8)
                {
                    enemy.State = DungeonEnemyState.Recover;
                    enemy.StateAge = 0;
                }
            }
            else if (enemy.State == DungeonEnemyState.Recover && enemy.StateAge > 24)
            {
                if (enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.SpecialSerial++ % 3 == 2)
                {
                    // The shadow slips sideways after every third assault;
                    // enough to unsettle without teleporting onto Karl.
                    double side = enemy.SpecialSerial % 2 == 0 ? 72 : -72;
                    double destinationX = Math.Clamp(enemy.X + side, 70, dungeon.RoomWidth - 70);
                    if (!DungeonBlocked(dungeon, destinationX, enemy.Y)) enemy.X = destinationX;
                }
                enemy.State = DungeonEnemyState.Approach;
                enemy.StateAge = 0;
            }
            if (enemy.State != DungeonEnemyState.Approach)
            {
                enemy.VelocityX *= 0.65;
                enemy.VelocityY *= 0.65;
            }
        }
        List<DungeonEnemy> roomEnemies = dungeon.Enemies.Where(enemy => enemy.Depth == dungeon.Depth).ToList();
        dungeon.RoomClear = roomEnemies.Count > 0 && roomEnemies.All(enemy => enemy.State == DungeonEnemyState.Dead);
        if (dungeon.RoomClear && !dungeon.RoomClearSaved)
        {
            dungeon.RoomClearSaved = true;
            WriteDungeonSave(dungeon, "autosave");
        }
    }

    private static void DropDungeonEnemyLoot(DungeonState dungeon, DungeonEnemy enemy)
    {
        if (enemy.LootDropped) return;
        enemy.LootDropped = true;
        int definition = enemy.Type switch
        {
            DungeonEnemyType.SilverFogde => 11,
            DungeonEnemyType.BlindShepherd => 12,
            DungeonEnemyType.LemminkainenShadow => 11,
            DungeonEnemyType.TuonelaGuard => 10,
            DungeonEnemyType.Pikeman => 4,
            _ => enemy.Id % 2 == 0 ? 7 : 8,
        };
        DungeonItemRarity rarity = enemy.Type switch
        {
            DungeonEnemyType.SilverFogde => DungeonItemRarity.Unique,
            DungeonEnemyType.BlindShepherd => DungeonItemRarity.Unique,
            DungeonEnemyType.LemminkainenShadow => DungeonItemRarity.Unique,
            DungeonEnemyType.TuonelaGuard => DungeonItemRarity.Runic,
            DungeonEnemyType.Pikeman => DungeonItemRarity.Silverbound,
            _ => DungeonItemRarity.Iron,
        };
        DungeonItem loot = AddDungeonItem(dungeon, definition, -1, -1, DungeonEquipmentSlot.None, rarity);
        loot.OnGround = true;
        loot.WorldX = enemy.X;
        loot.WorldY = enemy.Y + 7;
        if (DungeonBlocked(dungeon, loot.WorldX, loot.WorldY))
        {
            (loot.WorldX, loot.WorldY) = FindNearestDungeonFloor(dungeon, enemy.X, enemy.Y);
        }
        int tinctureChance = dungeon.Health * 100 / Math.Max(1, dungeon.MaxHealth) < 45 ? 42 : 22;
        if (enemy.Type is DungeonEnemyType.SilverFogde or DungeonEnemyType.BlindShepherd or DungeonEnemyType.LemminkainenShadow)
            tinctureChance = Math.Max(tinctureChance, 55);
        if (Random.Shared.Next(100) < tinctureChance)
        {
            DungeonItem tincture = AddDungeonItem(dungeon, 14, -1, -1, DungeonEquipmentSlot.None,
                DungeonItemRarity.Carolean);
            tincture.OnGround = true;
            tincture.WorldX = loot.WorldX + 15;
            tincture.WorldY = loot.WorldY + 4;
            if (DungeonBlocked(dungeon, tincture.WorldX, tincture.WorldY))
                (tincture.WorldX, tincture.WorldY) = FindNearestDungeonFloor(dungeon, enemy.X, enemy.Y);
        }
    }

    private static void DefeatDungeonEnemy(DungeonState dungeon, DungeonEnemy enemy)
    {
        DropDungeonEnemyLoot(dungeon, enemy);
        if (enemy.ExperienceAwarded) return;
        if (enemy.Type == DungeonEnemyType.BlindShepherd)
        {
            DungeonItem key = AddDungeonItem(dungeon, 13, -1, -1, DungeonEquipmentSlot.None, DungeonItemRarity.Unique);
            key.OnGround = true; key.WorldX = enemy.X + 18; key.WorldY = enemy.Y + 8;
        }
        enemy.ExperienceAwarded = true;
        dungeon.Experience += enemy.Type switch
        {
            DungeonEnemyType.SilverFogde => 180,
            DungeonEnemyType.BlindShepherd => 260,
            DungeonEnemyType.LemminkainenShadow => 340,
            DungeonEnemyType.TuonelaGuard => 65,
            DungeonEnemyType.UndeadMiner => 40,
            DungeonEnemyType.Pikeman => 45,
            _ => 30,
        };
        while (dungeon.Experience >= dungeon.Level * 100)
        {
            dungeon.Experience -= dungeon.Level * 100;
            dungeon.Level++;
            dungeon.MaxHealth += 15;
            dungeon.Health = dungeon.MaxHealth;
            dungeon.LevelPulse = 120;
        }
    }

    private static (double X, double Y) FindDungeonPickupApproach(DungeonState dungeon, DungeonItem item)
    {
        (double X, double Y) best = (item.WorldX, item.WorldY);
        double bestDistance = double.MaxValue;
        for (int index = 0; index < 16; index++)
        {
            double angle = index * Math.PI * 2 / 16;
            double x = item.WorldX + Math.Cos(angle) * 27;
            double y = item.WorldY + Math.Sin(angle) * 27;
            if (DungeonBlocked(dungeon, x, y)) continue;
            double distance = DistanceSquared(x, y, dungeon.KarlX, dungeon.KarlY);
            if (distance < bestDistance) { best = (x, y); bestDistance = distance; }
        }
        return best;
    }

    private static (double X, double Y) FindNearestDungeonFloor(DungeonState dungeon, double originX, double originY)
    {
        for (int radius = 12; radius <= 60; radius += 12)
        for (int index = 0; index < 16; index++)
        {
            double angle = index * Math.PI * 2 / 16;
            double x = originX + Math.Cos(angle) * radius;
            double y = originY + Math.Sin(angle) * radius;
            if (!DungeonBlocked(dungeon, x, y)) return (x, y);
        }
        return (originX, originY);
    }

    private static (double X, double Y) DungeonFacingVector(DungeonFacing facing) => facing switch
    {
        DungeonFacing.North => (0, -1), DungeonFacing.East => (1, 0),
        DungeonFacing.West => (-1, 0), _ => (0, 1),
    };

    private static void MoveDungeonEnemy(DungeonState dungeon, DungeonEnemy enemy, double dx, double dy)
    {
        bool recoveringFromObstacle = DungeonBlocked(dungeon, enemy.X, enemy.Y);
        if (recoveringFromObstacle || !DungeonBlocked(dungeon, enemy.X + dx, enemy.Y)) enemy.X += dx;
        recoveringFromObstacle = DungeonBlocked(dungeon, enemy.X, enemy.Y);
        if (recoveringFromObstacle || !DungeonBlocked(dungeon, enemy.X, enemy.Y + dy)) enemy.Y += dy;
    }

    private static void MoveDungeonKarl(DungeonState dungeon, double dx, double dy)
    {
        // Asset updates can make an old save start inside a newly enlarged
        // obstacle. Let that actor walk out, while still preventing entry from
        // every valid floor position.
        bool recoveringFromObstacle = DungeonBlocked(dungeon, dungeon.KarlX, dungeon.KarlY);
        double nextX = dungeon.KarlX + dx;
        if (recoveringFromObstacle || !DungeonBlocked(dungeon, nextX, dungeon.KarlY)) dungeon.KarlX = nextX;
        recoveringFromObstacle = DungeonBlocked(dungeon, dungeon.KarlX, dungeon.KarlY);
        double nextY = dungeon.KarlY + dy;
        if (recoveringFromObstacle || !DungeonBlocked(dungeon, dungeon.KarlX, nextY)) dungeon.KarlY = nextY;
    }

    private static bool DungeonBlocked(DungeonState dungeon, double x, double y)
    {
        if (x < 30 || x > dungeon.RoomWidth - 30 || y < 52 || y > dungeon.RoomHeight - 30) return true;
        if (dungeon.Depth == 2)
        {
            // Each visible timber is a separate horizontal obstacle. The old
            // implementation joined each pair into one tall invisible wall,
            // blocking the clearly visible passage between the sprites.
            ReadOnlySpan<(int X, int Y)> timbers =
            [
                (285, 175), (285, 245), (535, 470), (535, 610),
                (795, 165), (795, 310), (1030, 545), (1030, 675),
            ];
            foreach ((int timberX, int timberY) in timbers)
                if (Math.Abs(x - timberX) < 34 && Math.Abs(y - timberY) < 12) return true;
            return false;
        }
        if (dungeon.Depth == 3)
        {
            foreach ((int obstacleX, int obstacleY) in new[] { (300, 120), (610, 410), (850, 180), (1080, 650) })
                if (Math.Abs(x - obstacleX) < 35 && Math.Abs(y - obstacleY) < 22) return true;
            foreach ((int obstacleX, int obstacleY) in new[] { (260, 650), (700, 250), (1010, 580), (1220, 210) })
                if (Math.Abs(x - obstacleX) < 30 && Math.Abs(y - obstacleY) < 24) return true;
            return false;
        }
        if (dungeon.Depth == 4)
        {
            bool shadowDefeated = dungeon.Enemies.Any(enemy => enemy.Depth == 4 &&
                enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.State == DungeonEnemyState.Dead);
            if (!shadowDefeated && x is > 1145 and < 1185) return true;
            foreach ((int obstacleX, int obstacleY) in new[] { (455, 360), (810, 500), (1050, 275) })
                if (Math.Abs(x - obstacleX) < 35 && Math.Abs(y - obstacleY) < 22) return true;
            foreach ((int obstacleX, int obstacleY) in new[] { (300, 650), (700, 110), (1110, 600) })
                if (Math.Abs(x - obstacleX) < 30 && Math.Abs(y - obstacleY) < 24) return true;
            foreach ((int obstacleX, int obstacleY) in DungeonTempleGuardianStatues)
                if (Math.Abs(x - obstacleX) < 44 && Math.Abs(y - obstacleY) < 62) return true;
            if (Math.Abs(x - 1580) < 60 && Math.Abs(y - 380) < 46) return true;
            foreach ((int obstacleX, int obstacleY) in DungeonTempleClosedSarcophagi)
                if (Math.Abs(x - obstacleX) < 62 && Math.Abs(y - obstacleY) < 48) return true;
            foreach ((int obstacleX, int obstacleY) in DungeonTempleOpenSarcophagi)
                if (Math.Abs(x - obstacleX) < 66 && Math.Abs(y - obstacleY) < 56) return true;
            return false;
        }
        bool leftPillar = x > 253 && x < 307 && y > 124 && y < 212;
        bool rightStore = x > 438 && x < 507 && y > 268 && y < 340;
        return leftPillar || rightStore;
    }

    private static void WriteDungeonSave(DungeonState dungeon, string slot)
    {
        ValidateDungeonInventory(dungeon);
        try
        {
            string stateRoot = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
            string directory = Path.Combine(stateRoot, "waylandforge", "stormakt3020", "saves");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, slot + ".json");
            string temporary = path + ".tmp";
            string backup = path + ".bak";
            var snapshot = new DungeonSave(11, dungeon.Age, dungeon.RoomWidth, dungeon.RoomHeight,
                dungeon.KarlX, dungeon.KarlY, dungeon.TargetX, dungeon.TargetY, dungeon.Facing,
                dungeon.Health, dungeon.MaxHealth, dungeon.Power, dungeon.NextItemId,
                dungeon.Items.Select(item => new DungeonItemSave(item.Id, item.Definition, item.GridX, item.GridY,
                    item.Equipped, item.Rarity, item.PowerRoll, item.OnGround, item.InStash, item.WorldX, item.WorldY,
                    item.QuickSlot)).ToList(),
                dungeon.NextEnemyId, dungeon.AttackSerial, dungeon.AttackCombo, dungeon.RoomClear, dungeon.DoorReady,
                dungeon.Depth, dungeon.DoorHealth, dungeon.DescentWarningPlayed, dungeon.MaxZoneReached,
                dungeon.Level, dungeon.Experience, dungeon.DepthThreeOpen, dungeon.ForcedVentAge,
                dungeon.Curse, dungeon.TempleOpen, dungeon.LoreMask,
                dungeon.Enemies.Select(enemy => new DungeonEnemySave(enemy.Id, enemy.X, enemy.Y, enemy.Type,
                    enemy.Health, enemy.MaxHealth, enemy.State, enemy.StateAge, enemy.LastHitSerial, enemy.FacingLeft,
                    enemy.LootDropped, enemy.Zone, enemy.ExperienceAwarded, enemy.Phase, enemy.SpecialKind,
                    enemy.SpecialAge, enemy.SpecialCooldown, enemy.SpecialSerial, enemy.Depth)).ToList(),
                dungeon.Chests.Select(chest => new DungeonChestSave(chest.Id, chest.X, chest.Y, chest.Open,
                    chest.OpenAge, chest.Depth)).ToList());
            File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot));
            if (File.Exists(path)) File.Copy(path, backup, true);
            File.Move(temporary, path, true);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Stormakt save warning: {exception.Message}");
        }
    }

    private static void ValidateDungeonInventory(DungeonState dungeon)
    {
        if (dungeon.Items.Select(item => item.Id).Distinct().Count() != dungeon.Items.Count)
            throw new InvalidOperationException("Dungeon inventory contains duplicate item ids.");
        if (dungeon.Items.Where(item => item.Equipped != DungeonEquipmentSlot.None)
            .GroupBy(item => item.Equipped).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Dungeon inventory contains duplicate equipment slots.");
        if (dungeon.Items.Where(item => item.QuickSlot >= 0).Any(item => item.Definition != 14 || item.QuickSlot >= 4) ||
            dungeon.Items.Where(item => item.QuickSlot >= 0).GroupBy(item => item.QuickSlot).Any(group => group.Count() > 1))
            throw new InvalidOperationException("Dungeon potion belt contains an invalid quick slot.");
        foreach (DungeonItem item in dungeon.Items)
        {
            int owners = (item.OnGround ? 1 : 0) + (item.Equipped != DungeonEquipmentSlot.None ? 1 : 0) +
                (item.QuickSlot >= 0 ? 1 : 0) +
                (!item.OnGround && item.Equipped == DungeonEquipmentSlot.None && item.QuickSlot < 0 && item.GridX >= 0 ? 1 : 0);
            if (owners != 1) throw new InvalidOperationException($"Dungeon item {item.Id} has invalid ownership.");
            if (item.GridX >= 0 && !CanPlaceDungeonItem(dungeon, item, item.GridX, item.GridY))
                throw new InvalidOperationException($"Dungeon item {item.Id} overlaps or exceeds its container.");
        }
        if (dungeon.Enemies.Select(enemy => enemy.Id).Distinct().Count() != dungeon.Enemies.Count)
            throw new InvalidOperationException("Dungeon encounter contains duplicate enemy ids.");
    }

    private static bool TryLoadDungeonSave(string slot, out DungeonState? dungeon)
    {
        dungeon = null;
        string path = DungeonSavePath(slot);
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                DungeonSave save = JsonSerializer.Deserialize<DungeonSave>(File.ReadAllText(candidate));
                if (save.Schema is < 1 or > 11 || save.RoomWidth < 320 || save.RoomHeight < 220) continue;
                dungeon = new DungeonState
                {
                    Age = save.Age,
                    RoomWidth = save.RoomWidth,
                    RoomHeight = save.RoomHeight,
                    KarlX = save.KarlX,
                    KarlY = save.KarlY,
                    TargetX = save.TargetX,
                    TargetY = save.TargetY,
                    Facing = save.Facing,
                    Health = save.Schema >= 2 ? save.Health : 100,
                    MaxHealth = save.Schema >= 2 ? save.MaxHealth : 100,
                    Power = save.Schema >= 2 ? save.Power : 35,
                    NextItemId = save.Schema >= 2 ? save.NextItemId : 1,
                    NextEnemyId = save.Schema >= 3 ? save.NextEnemyId : 1,
                    AttackSerial = save.Schema >= 3 ? save.AttackSerial : 0,
                    AttackCombo = save.Schema >= 3 ? save.AttackCombo : 0,
                    RoomClear = save.Schema >= 3 && save.RoomClear,
                    RoomClearSaved = save.Schema >= 3 && save.RoomClear,
                    DoorReady = save.Schema >= 4 && save.DoorReady,
                    Depth = save.Schema >= 5 ? save.Depth : 1,
                    DoorHealth = save.Schema >= 5 ? save.DoorHealth : save.DoorReady ? 0 : 3,
                    DescentWarningPlayed = save.Schema >= 6 ? save.DescentWarningPlayed : save.DoorReady,
                    MaxZoneReached = save.Schema >= 7 ? save.MaxZoneReached : save.Depth == 2 ? DungeonZoneFromX(save.KarlX) : 0,
                    Level = save.Schema >= 8 ? Math.Max(1, save.Level) : 1,
                    Experience = save.Schema >= 8 ? Math.Max(0, save.Experience) : 0,
                    DepthThreeOpen = save.Schema >= 9 && save.DepthThreeOpen,
                    ForcedVentAge = save.Schema >= 9 ? save.ForcedVentAge : 0,
                    Curse = save.Schema >= 10 ? save.Curse : 0,
                    TempleOpen = save.Schema >= 10 && save.TempleOpen,
                    LoreMask = save.Schema >= 10 ? save.LoreMask : 0,
                };
                if (save.Schema >= 2 && save.Items is not null)
                {
                    bool templeKeyLoaded = false;
                    foreach (DungeonItemSave item in save.Items)
                    {
                        if (item.Definition < 0 || item.Definition >= DungeonItemDefinitions.Length) continue;
                        if (item.Definition == 13 && templeKeyLoaded) continue;
                        if (item.Definition == 13) templeKeyLoaded = true;
                        dungeon.Items.Add(new DungeonItem
                        {
                            Id = item.Id, Definition = item.Definition, GridX = item.GridX, GridY = item.GridY,
                            Equipped = item.Equipped, Rarity = item.Rarity, PowerRoll = item.PowerRoll,
                            OnGround = item.OnGround, InStash = item.InStash, WorldX = item.WorldX, WorldY = item.WorldY,
                            QuickSlot = save.Schema >= 11 ? item.QuickSlot : -1,
                        });
                    }
                }
                else SeedDungeonInventory(dungeon);
                if (save.Schema >= 3 && save.Enemies is not null)
                {
                    foreach (DungeonEnemySave enemy in save.Enemies)
                    {
                        var loaded = new DungeonEnemy(enemy.X, enemy.Y, enemy.Type, enemy.MaxHealth)
                        {
                            Id = enemy.Id, Health = enemy.Health, State = enemy.State, StateAge = enemy.StateAge,
                            LastHitSerial = enemy.LastHitSerial, FacingLeft = enemy.FacingLeft,
                            LootDropped = save.Schema >= 4 && enemy.LootDropped,
                            Zone = save.Schema >= 7 ? enemy.Zone : save.Depth == 2 ? DungeonZoneFromX(enemy.X) : 0,
                            ExperienceAwarded = save.Schema >= 8 && enemy.ExperienceAwarded,
                            Phase = save.Schema >= 9 ? enemy.Phase : 1,
                            SpecialKind = save.Schema >= 9 ? enemy.SpecialKind : 0,
                            SpecialAge = save.Schema >= 9 ? enemy.SpecialAge : 0,
                            SpecialCooldown = save.Schema >= 9 ? enemy.SpecialCooldown : 0,
                            SpecialSerial = save.Schema >= 9 ? enemy.SpecialSerial : 0,
                            Depth = save.Schema >= 10 ? enemy.Depth : enemy.Type is DungeonEnemyType.Stormer or
                                DungeonEnemyType.Pikeman or DungeonEnemyType.SilverFogde ? 2 : save.Depth,
                        };
                        dungeon.Enemies.Add(loaded);
                    }
                }
                else SeedDungeonEncounter(dungeon);
                if (save.Schema < 7 && dungeon.Depth == 2 &&
                    dungeon.Enemies.All(enemy => enemy.Type != DungeonEnemyType.SilverFogde))
                {
                    AddDungeonEnemy(dungeon, 1060, 390, DungeonEnemyType.SilverFogde, 240, 3);
                    dungeon.RoomClear = false;
                    dungeon.RoomClearSaved = false;
                }
                if (save.Schema >= 5 && save.Chests is not null)
                    foreach (DungeonChestSave chest in save.Chests)
                        dungeon.Chests.Add(new DungeonChest(chest.Id, chest.X, chest.Y)
                            { Open = chest.Open, OpenAge = save.Schema >= 7 ? chest.OpenAge : chest.Open ? 45 : 0,
                              Depth = save.Schema >= 10 ? chest.Depth : chest.Id <= 3 ? 2 : save.Depth });
                if (dungeon.Depth == 4)
                {
                    dungeon.RoomWidth = Math.Max(1760, dungeon.RoomWidth);
                    dungeon.RoomHeight = Math.Max(760, dungeon.RoomHeight);
                    if (!dungeon.Enemies.Any(enemy => enemy.Depth == 4 && enemy.Type == DungeonEnemyType.LemminkainenShadow))
                    {
                        AddDungeonEnemy(dungeon, 1040, 380, DungeonEnemyType.LemminkainenShadow, 420, 43);
                        dungeon.Enemies.Last().Phase = 0;
                    }
                    if (dungeon.Chests.All(chest => chest.Id != 41))
                        dungeon.Chests.Add(new DungeonChest(41, 610, 135) { Depth = 4 });
                    if (dungeon.Chests.All(chest => chest.Id != 42))
                        dungeon.Chests.Add(new DungeonChest(42, 1010, 665) { Depth = 4 });
                    EnsureDungeonFirstSigilEncounter(dungeon);
                }
                dungeon.CameraX = Math.Clamp((int)Math.Round(dungeon.KarlX - 200), 0, Math.Max(0, dungeon.RoomWidth - 400));
                dungeon.CameraY = Math.Clamp((int)Math.Round(dungeon.KarlY - 140), 0, Math.Max(0, dungeon.RoomHeight - 280));
                return true;
            }
            catch (JsonException)
            {
                // Try the backup before giving up.
            }
            catch (IOException)
            {
                // A temporarily unavailable save must not block a fresh campaign.
            }
        }
        return false;
    }

    private static string DungeonSavePath(string slot)
    {
        string stateRoot = Environment.GetEnvironmentVariable("XDG_STATE_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        return Path.Combine(stateRoot, "waylandforge", "stormakt3020", "saves", slot + ".json");
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return dx * dx + dy * dy;
    }

    private static (double X, double Y) MoveRtsAroundTerrain(
        RtsState rts, double x, double y, double stepX, double stepY, double radius)
    {
        if (!RtsTerrainBlocked(rts, x + stepX, y + stepY, radius)) return (x + stepX, y + stepY);
        double sideX = -stepY;
        double sideY = stepX;
        if (!RtsTerrainBlocked(rts, x + sideX, y + sideY, radius)) return (x + sideX, y + sideY);
        if (!RtsTerrainBlocked(rts, x - sideX, y - sideY, radius)) return (x - sideX, y - sideY);
        return (x, y);
    }

    private static bool RtsTerrainBlocked(RtsState rts, double x, double y, double radius) =>
        rts.Props.Any(prop => prop.BlockRadius > 0 &&
            DistanceSquared(x, y, prop.X, prop.Y) < (radius + prop.BlockRadius) * (radius + prop.BlockRadius));

    private static bool RtsHasPower(RtsState rts)
    {
        int total = rts.Buildings.Any(building => building.Type == RtsBuildingType.SteamPlant) ? 100 : 0;
        int used = rts.Buildings.Sum(building => building.Type switch
        {
            RtsBuildingType.SilverCrusher => 40,
            RtsBuildingType.Barracks => 20,
            RtsBuildingType.AnimalHall => 30,
            RtsBuildingType.DefenseTower => 10,
            _ => 0,
        });
        return total >= used;
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

    private void DrawDungeon(uint[] frame)
    {
        DungeonState dungeon = _dungeon!;
        DrawRect(frame, 0, 0, _width, _height, 0xff080b0a);
        string floorName = dungeon.Depth == 4 ? "dungeon_temple_floor" :
            dungeon.Depth >= 3 ? "dungeon_cursed_floor" : "dungeon_floor_wet";
        if (_sprites?.TryGet(floorName, out Sprite floor) == true)
        {
            int startX = -(dungeon.CameraX % floor.Width);
            int startY = 18 - (dungeon.CameraY % floor.Height);
            for (int y = startY; y < _height; y += floor.Height)
            for (int x = startX; x < _width; x += floor.Width)
                DrawSprite(frame, floor, x, y);
        }
        if (dungeon.Depth == 1)
        {
            DrawDungeonSprite(frame, dungeon, "dungeon_wall_timber", 105, 67);
            DrawDungeonSprite(frame, dungeon, "dungeon_wall_timber", 205, 67);
            DrawDungeonSprite(frame, dungeon, "dungeon_wall_corner", 280, 144);
            string doorSprite = dungeon.DoorReady ? "dungeon_wood_door_broken" :
                dungeon.DoorHealth <= 1 ? "dungeon_wood_door_damaged" : "dungeon_wood_door";
            DrawDungeonSprite(frame, dungeon, doorSprite, 650, 218);
            DrawDungeonSprite(frame, dungeon, "dungeon_rails", 380, 360);
            DrawDungeonSprite(frame, dungeon, "dungeon_chain_lift", 88, 228);
            DrawDungeonSprite(frame, dungeon, "dungeon_supply", 470, 300);
            DrawDungeonSprite(frame, dungeon, "dungeon_descent_pit", 598, 126);
        }
        else if (dungeon.Depth == 2)
        {
            DrawDungeonSprite(frame, dungeon, "dungeon_wood_door_broken", 60, 410);
            foreach ((int x, int y) in new[] { (285, 175), (285, 245), (535, 470), (535, 610),
                (795, 165), (795, 310), (1030, 545), (1030, 675) })
                DrawDungeonSprite(frame, dungeon, "dungeon_wall_timber", x, y);
            for (int index = 0; index < DungeonSilverVents.Length; index++)
            {
                (int x, int y) = DungeonSilverVents[index];
                DrawDungeonSprite(frame, dungeon, "dungeon_silver_vent", x, y);
                int cycle = (dungeon.Age + index * 43) % 180;
                if (_sprites?.TryGet("dungeon_silver_mist", out Sprite vapor) == true && cycle is >= 30 and < 102)
                {
                    int alpha = cycle < 54 ? 70 + (cycle - 30) * 5 : cycle < 84 ? 210 : 210 - (cycle - 84) * 11;
                    DrawSpriteAlpha(frame, vapor, x - dungeon.CameraX - vapor.Width / 2,
                        y - dungeon.CameraY - vapor.Height / 2 - 7, (uint)Math.Clamp(alpha, 40, 220));
                }
            }
            foreach ((int x, int y) in new[] { (355, 555), (680, 180), (910, 520), (1180, 315) })
                DrawDungeonSprite(frame, dungeon, "dungeon_silver_mist", x, y);
            foreach (DungeonChest chest in dungeon.Chests.Where(chest => chest.Depth == 2))
            {
                int chestY = (int)Math.Round(chest.Y) - (chest.OpenAge is > 0 and < 12 ? chest.OpenAge % 3 : 0);
                DrawDungeonSprite(frame, dungeon, chest.Open ? "dungeon_chest_open" : "dungeon_chest_closed",
                    (int)Math.Round(chest.X), chestY);
            }
            DrawDungeonSprite(frame, dungeon,
                dungeon.DepthThreeOpen ? "dungeon_gruva3_portal" : "dungeon_gruva3_wall", 1190, 390);
        }
        else if (dungeon.Depth == 3)
        {
            DrawDungeonSprite(frame, dungeon, "dungeon_gruva3_portal", 60, 240);
            foreach ((int x, int y) in DungeonBlackWaters) DrawDungeonSprite(frame, dungeon, "dungeon_black_water", x, y);
            foreach ((int x, int y) in DungeonHolyShrines) DrawDungeonSprite(frame, dungeon, "dungeon_holy_shrine", x, y);
            foreach ((int x, int y) in new[] { (300, 120), (610, 410), (850, 180), (1080, 650) })
                DrawDungeonSprite(frame, dungeon, "dungeon_runic_arch", x, y);
            foreach ((int x, int y) in new[] { (260, 650), (700, 250), (1010, 580), (1220, 210) })
                DrawDungeonSprite(frame, dungeon, "dungeon_twisted_silver", x, y);
            foreach ((int x, int y) in new[] { (450, 720), (820, 120), (1140, 730) })
                DrawDungeonSprite(frame, dungeon, "dungeon_side_chamber", x, y);
            foreach ((int x, int y) in new[] { (390, 520), (930, 430), (1180, 120) })
                DrawDungeonSprite(frame, dungeon, "dungeon_lore_stele", x, y);
            foreach (DungeonChest chest in dungeon.Chests.Where(chest => chest.Depth == 3))
                DrawDungeonSprite(frame, dungeon, chest.Open ? "dungeon_chest_open" : "dungeon_cursed_chest",
                    (int)chest.X, (int)chest.Y);
            if (dungeon.TempleOpen) DrawDungeonSprite(frame, dungeon, "dungeon_temple_stairs", 1320, 450);
        }
        else
        {
            DrawDungeonSprite(frame, dungeon, "dungeon_temple_stairs", 60, 250);
            foreach ((int x, int y) in DungeonTempleWaters)
                DrawDungeonSprite(frame, dungeon, "dungeon_black_water", x, y);
            foreach ((int x, int y) in DungeonTempleShrines)
                DrawDungeonSprite(frame, dungeon,
                    x == 1580 ? "dungeon_temple_altar" : "dungeon_holy_shrine", x, y);
            foreach ((int x, int y) in new[] { (455, 360), (810, 500), (1050, 275) })
                DrawDungeonSprite(frame, dungeon, "dungeon_runic_arch", x, y);
            foreach ((int x, int y) in new[] { (300, 650), (700, 110), (1110, 600) })
                DrawDungeonSprite(frame, dungeon, "dungeon_twisted_silver", x, y);
            foreach ((int x, int y) in new[] { (520, 680), (900, 105) })
                DrawDungeonSprite(frame, dungeon, "dungeon_side_chamber", x, y);
            foreach ((int x, int y) in DungeonTempleGuardianStatues)
                DrawDungeonSprite(frame, dungeon, "dungeon_temple_guardian", x, y);
            foreach ((int x, int y) in DungeonTempleClosedSarcophagi)
                DrawDungeonSprite(frame, dungeon, "dungeon_temple_sarc_closed", x, y);
            foreach ((int x, int y) in DungeonTempleOpenSarcophagi)
                DrawDungeonSprite(frame, dungeon, "dungeon_temple_sarc_open", x, y);
            foreach (DungeonChest chest in dungeon.Chests.Where(chest => chest.Depth == 4))
                DrawDungeonSprite(frame, dungeon, chest.Open ? "dungeon_chest_open" : "dungeon_cursed_chest",
                    (int)chest.X, (int)chest.Y);
            bool shadowDefeated = dungeon.Enemies.Any(enemy => enemy.Depth == 4 &&
                enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.State == DungeonEnemyState.Dead);
            DrawDungeonSprite(frame, dungeon, shadowDefeated ? "dungeon_temple_gate_open" : "dungeon_temple_gate_closed",
                1140, 380);
            DrawDungeonSprite(frame, dungeon, "dungeon_temple_gate_closed", 1705, 380);
            int sigilX = 1580 - dungeon.CameraX;
            int sigilY = 380 - dungeon.CameraY;
            bool firstSigilActive = (dungeon.LoreMask & DungeonFirstSigilMask) != 0;
            DrawCircleOutline(frame, sigilX, sigilY, firstSigilActive ? 34 : 27,
                firstSigilActive ? 0xffffd66b : 0xff73b8d0);
            if (!shadowDefeated)
            {
                int barrierX = 1165 - dungeon.CameraX;
                for (int worldY = 65; worldY < dungeon.RoomHeight - 35; worldY += 18)
                {
                    int y = worldY - dungeon.CameraY;
                    DrawLine(frame, barrierX, y, barrierX, y + 10,
                        (dungeon.Age / 6 + worldY / 18) % 2 == 0 ? 0xff73b8d0 : 0xffdce8f2);
                }
            }
        }

        foreach (DungeonSilverWave wave in dungeon.SilverWaves)
        {
            int waveX = (int)Math.Round(wave.X) - dungeon.CameraX;
            int waveY = (int)Math.Round(wave.Y) - dungeon.CameraY;
            uint color = wave.Age / 3 % 2 == 0 ? 0xffdce8f2 : 0xff73b8d0;
            DrawLine(frame, waveX - 7, waveY + 3, waveX, waveY - 5, color);
            DrawLine(frame, waveX, waveY - 5, waveX + 7, waveY + 3, color);
        }

        foreach (DungeonItem item in dungeon.Items.Where(candidate => candidate.OnGround))
        {
            DungeonItemDefinition definition = DungeonItemDefinitions[item.Definition];
            int itemX = (int)Math.Round(item.WorldX) - dungeon.CameraX;
            int itemY = (int)Math.Round(item.WorldY) - dungeon.CameraY;
            DrawCircleOutline(frame, itemX, itemY, 9, DungeonRarityColor(item.Rarity));
            if (_sprites?.TryGet(definition.Sprite, out Sprite icon) == true)
                DrawSprite(frame, icon, itemX - icon.Width / 2, itemY - icon.Height / 2);
        }

        foreach (DungeonEnemy enemy in dungeon.Enemies.Where(enemy => enemy.Depth == dungeon.Depth &&
            (enemy.Type != DungeonEnemyType.LemminkainenShadow || enemy.Phase != 0)).OrderBy(enemy => enemy.Y))
        {
            int enemyX = (int)Math.Round(enemy.X) - dungeon.CameraX;
            bool stepping = enemy.State == DungeonEnemyState.Approach && Math.Abs(enemy.VelocityX) + Math.Abs(enemy.VelocityY) > 0.08;
            int foot = (int)(enemy.GaitDistance / 5.5) & 3;
            int enemyY = (int)Math.Round(enemy.Y) - dungeon.CameraY - (stepping && foot is 1 or 3 ? 1 : 0);
            string prefix = enemy.Type switch
            {
                DungeonEnemyType.Pikeman => "dk_pikeman",
                DungeonEnemyType.SilverFogde => "dk_silver_fogde",
                DungeonEnemyType.UndeadMiner => "undead_miner",
                DungeonEnemyType.TuonelaGuard => "tuonela_guard",
                DungeonEnemyType.BlindShepherd => "blind_shepherd",
                DungeonEnemyType.LemminkainenShadow => "lemminkainen_shadow",
                _ => "dk_stormer",
            };
            string enemyName = enemy.State switch
            {
                DungeonEnemyState.Dead when enemy.Type == DungeonEnemyType.SilverFogde => "dk_silver_fogde_death",
                DungeonEnemyState.Dead when enemy.Type == DungeonEnemyType.BlindShepherd => "blind_shepherd_death",
                DungeonEnemyState.Dead when enemy.Type == DungeonEnemyType.LemminkainenShadow => "lemminkainen_shadow_hit",
                DungeonEnemyState.Telegraph when enemy.Type == DungeonEnemyType.LemminkainenShadow => "lemminkainen_shadow_split",
                DungeonEnemyState.Attack when enemy.Type == DungeonEnemyType.SilverFogde && enemy.SpecialKind > 0 => "dk_silver_fogde_slam",
                DungeonEnemyState.Attack or DungeonEnemyState.Telegraph => prefix + "_attack",
                DungeonEnemyState.Stagger or DungeonEnemyState.Dead => prefix + "_hit",
                DungeonEnemyState.Approach when enemy.Type == DungeonEnemyType.BlindShepherd => "blind_shepherd_idle",
                DungeonEnemyState.Approach => stepping && foot is 1 or 2 ? prefix + "_walk" : prefix + "_idle",
                _ => prefix + "_idle",
            };
            if (enemy.State == DungeonEnemyState.Telegraph)
                DrawCircleOutline(frame, enemyX, enemyY - 4,
                    enemy.Type switch { DungeonEnemyType.Pikeman => 22, DungeonEnemyType.SilverFogde => 29, _ => 17 },
                    enemy.StateAge / 4 % 2 == 0 ? 0xffffc45c : 0xffb92f3f);
            if (_sprites?.TryGet(enemyName, out Sprite enemySprite) == true)
            {
                int drawX = enemyX - enemySprite.Width / 2;
                int drawY = enemyY - enemySprite.Height + 10;
                if (enemy.FacingLeft) DrawSpriteFlippedX(frame, enemySprite, drawX, drawY);
                else DrawSprite(frame, enemySprite, drawX, drawY);
            }
            if (enemy.Health > 0 && enemy.Health < enemy.MaxHealth)
            {
                DrawRect(frame, enemyX - 15, enemyY - 36, 30, 3, 0xff26161a);
                DrawRect(frame, enemyX - 15, enemyY - 36, 30 * enemy.Health / enemy.MaxHealth, 2, 0xffbf3348);
            }
        }

        DungeonEnemy? fogde = dungeon.Depth == 2
            ? dungeon.Enemies.FirstOrDefault(enemy => enemy.Type == DungeonEnemyType.SilverFogde &&
                enemy.State != DungeonEnemyState.Dead && DungeonZone(dungeon) == 3)
            : null;
        DungeonEnemy? shepherd = dungeon.Depth == 3
            ? dungeon.Enemies.FirstOrDefault(enemy => enemy.Type == DungeonEnemyType.BlindShepherd &&
                enemy.State != DungeonEnemyState.Dead && DungeonZone(dungeon) == 34)
            : null;
        DungeonEnemy? shadow = dungeon.Depth == 4
            ? dungeon.Enemies.FirstOrDefault(enemy => enemy.Type == DungeonEnemyType.LemminkainenShadow &&
                enemy.State != DungeonEnemyState.Dead && enemy.Phase != 0 && DungeonZone(dungeon) == 43)
            : null;
        DungeonEnemy? activeBoss = fogde ?? shepherd ?? shadow;
        if (activeBoss is not null)
        {
            int barWidth = Math.Min(220, _width - 100);
            int barX = (_width - barWidth) / 2;
            DrawRect(frame, barX, 20, barWidth, 27, 0xee0b0d10);
            string bossName = fogde is not null ? "DEN FÖRSILVRADE GRUVFOGDEN" :
                shepherd is not null ? "DEN BLINDE HERDEN" : "LEMMINKÄINENS SKUGGA";
            DrawText(frame, barX + 8, 24, bossName, 0xffdce8f2);
            DrawRect(frame, barX + 2, 37, barWidth - 4, 7, 0xff20171d);
            DrawRect(frame, barX + 3, 38, (barWidth - 6) * activeBoss.Health / activeBoss.MaxHealth, 5, 0xffb7c7d6);
        }

        int karlX = (int)Math.Round(dungeon.KarlX) - dungeon.CameraX;
        int karlY = (int)Math.Round(dungeon.KarlY) - dungeon.CameraY;
        string karlName;
        // Drive the cycle from travelled world distance so speed changes do
        // not slide the feet. Roughly 34 px per full cycle matches Karl's
        // long, deliberate Carolean stride at both normal and Slow speed.
        int gait = (int)(dungeon.GaitDistance / 8.5) & 3;
        bool hammer = dungeon.Items.Any(item => item.Equipped == DungeonEquipmentSlot.MainHand && item.Definition == 3);
        if (dungeon.HurtAge > 0) karlName = "karl_hit";
        else if (dungeon.Guarding) karlName = dungeon.Facing == DungeonFacing.North ? "karl_parry_n" : "karl_parry";
        else if (dungeon.AttackAge > 0 && hammer) karlName = "karl_hammer_impact";
        else if (dungeon.AttackAge > 0 && dungeon.Facing is DungeonFacing.East or DungeonFacing.West)
            karlName = dungeon.AttackAge < 9 ? "karl_slash_e_windup" : "karl_slash_e_contact";
        else if (dungeon.AttackAge > 0 && dungeon.Facing == DungeonFacing.North)
            karlName = dungeon.AttackAge switch
            {
                < 8 => "karl_slash_n_windup", < 13 => "karl_slash_n_contact", _ => "karl_slash_n_follow",
            };
        else if (dungeon.AttackAge > 0) karlName = dungeon.AttackAge switch
        {
            < 8 => "karl_slash_windup", < 13 => "karl_slash_contact", _ => "karl_slash_follow",
        };
        else if (dungeon.Age < 150) karlName = dungeon.Age < 95 ? "dungeon_karl_kneel" : "dungeon_karl_s_idle";
        else if (dungeon.Facing == DungeonFacing.North) karlName = dungeon.Moving && gait is 1 or 3 ? "dungeon_karl_n_walk" : "dungeon_karl_n_idle";
        else if (dungeon.Facing is DungeonFacing.East or DungeonFacing.West) karlName = dungeon.Moving && gait is 1 or 3 ? "dungeon_karl_e_walk" : "dungeon_karl_e_idle";
        else karlName = !dungeon.Moving || gait is 0 or 2 ? "dungeon_karl_s_idle" :
            gait == 1 ? "dungeon_karl_s_walk_a" : "dungeon_karl_s_walk_b";
        if (_sprites?.TryGet(karlName, out Sprite karl) == true)
        {
            int drawX = karlX - karl.Width / 2;
            // Combat cells include weapon arcs and impact debris below or
            // around the body. Anchor the boots, not the alpha bounds, so
            // Karl stays planted while his coat is free to flare in the pose.
            int footAnchor = karlName switch
            {
                "karl_slash_windup" or "karl_slash_contact" or "karl_slash_follow" or "karl_parry" => 20,
                "karl_slash_e_windup" or "karl_slash_e_contact" => 22,
                "karl_slash_n_windup" or "karl_slash_n_contact" or "karl_slash_n_follow" or "karl_parry_n" => 16,
                "karl_hammer_impact" => 30,
                "karl_hit" => 25,
                _ => 12,
            };
            int drawY = karlY - karl.Height + footAnchor;
            if (dungeon.Facing == DungeonFacing.West) DrawSpriteFlippedX(frame, karl, drawX, drawY);
            else DrawSprite(frame, karl, drawX, drawY);
        }

        DrawRect(frame, 0, 0, _width, 18, 0xee080d12);
        DrawText(frame, 6, 5, dungeon.Depth switch
        {
            1 => "GRUVA I  ÖVERGIVNA ORTEN",
            2 => "GRUVA II  SILVERÅNGORNA",
            3 => "GRUVA III  DEN FÖRBANNADE GRUVAN",
            _ => "GRUVA IV  LEMMINKÄINENS TEMPEL",
        }, 0xffffd66b);
        DrawText(frame, _width - 78, 5, "AUTOSPAR", 0xff65c58a);
        DrawDungeonHealth(frame, dungeon);
        DrawDungeonPotionBelt(frame, dungeon);
        if (dungeon.Depth == 3)
        {
            int curseY = _bossRadioCard is not null ? 92 : activeBoss is null ? 22 : 52;
            DrawText(frame, 108, curseY, "FÖRBANNELSE", 0xffc589ff);
            DrawRect(frame, 108, curseY + 10, 84, 6, 0xff17111d);
            DrawRect(frame, 110, curseY + 12, 80 * dungeon.Curse / 100, 2,
                dungeon.Curse >= 80 ? 0xffff6b62 : 0xff9867c5);
        }
        int experienceY = activeBoss is null ? 22 : 49;
        int experienceX = _width - 104;
        DrawText(frame, experienceX, experienceY, $"NIVÅ {dungeon.Level:00}", 0xffffd66b);
        DrawRect(frame, experienceX + 47, experienceY + 1, 52, 5, 0xff121923);
        DrawRect(frame, experienceX + 48, experienceY + 2,
            50 * dungeon.Experience / Math.Max(1, dungeon.Level * 100), 3, 0xff73b8d0);
        DrawRect(frame, 0, _height - 14, _width, 14, 0xee080d12);
        string objective = dungeon.Age < 150 ? "KARL SÄNKS NER" : "HÖGERKLICK HUGG  SLOW PARERA";
        if (dungeon.RoomClear)
            objective = dungeon.Depth == 1
                ? dungeon.DoorReady
                    ? _bossRadioCard is null ? "KLICKA PÅ GÅNGEN ELLER FIRE FÖR ATT GÅ NER" : "LYSSNA PÅ EBBA"
                    : "HUGG SÖNDER TRÄPORTEN I ÖSTER"
                : "GRUVA II SÄKRAD  SÖK I KISTORNA";
        if (dungeon.Depth == 3 && dungeon.TempleOpen) objective = "TEMPELTRAPPAN ÄR ÖPPEN I ÖSTER";
        if (dungeon.Depth == 4)
        {
            bool shadowDefeated = dungeon.Enemies.Any(enemy => enemy.Depth == 4 &&
                enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.State == DungeonEnemyState.Dead);
            bool shadowAwake = dungeon.Enemies.Any(enemy => enemy.Depth == 4 &&
                enemy.Type == DungeonEnemyType.LemminkainenShadow && enemy.Phase != 0 && enemy.State != DungeonEnemyState.Dead);
            bool courtyardClear = dungeon.Enemies.Where(enemy => enemy.Depth == 4 && enemy.Zone == 44)
                .All(enemy => enemy.State == DungeonEnemyState.Dead);
            bool firstSigilActive = (dungeon.LoreMask & DungeonFirstSigilMask) != 0;
            objective = firstSigilActive ? "FÖRSTA SIGILLET VÄCKT  TVÅ ÅTERSTÅR" :
                shadowDefeated && dungeon.KarlX < 1200 ? "GÅ GENOM RUNPORTEN" :
                shadowDefeated && !courtyardClear ? "BRYT VAKTEN KRING FÖRSTA SIGILLET" :
                shadowDefeated ? "STIG IN I SIGILLET" :
                shadowAwake ? "ÖVERLEV LEMMINKÄINENS SKUGGA" : "FÖLJ RUNORNA MOT DEN FÖRSEGLADE PORTEN";
        }
        DrawText(frame, 7, _height - 10, objective, 0xffdce8f2);
        if (_bossRadioCard is RadioCard radio && _bossRadioAge < radio.DurationFrames)
        {
            DrawRadioCard(frame, radio, _bossRadioAge);
        }
        if (dungeon.InventoryOpen) DrawDungeonInventory(frame, dungeon);
        if (dungeon.LevelPulse > 0 && dungeon.LevelPulse / 8 % 2 == 0)
            DrawText(frame, (_width - 72) / 2, 48, "NY NIVÅ!", 0xffffd66b);
        if (dungeon.DeathAge > 0)
        {
            int alpha = Math.Clamp(dungeon.DeathAge * 3, 40, 210);
            DrawRect(frame, 0, 18, _width, _height - 32, ((uint)alpha << 24) | 0x0026070c);
            DrawText(frame, (_width - 66) / 2, _height / 2, "KARL FALLER", 0xffffc4c4);
        }
    }

    private static readonly DungeonItemDefinition[] DungeonItemDefinitions =
    [
        new("rapier", "OFFICERSVÄRJA", "loot_rapier", 1, 3, DungeonEquipmentSlot.MainHand, 12, 0, 0, 1),
        new("saber", "KAROLINSK SABEL", "loot_saber", 1, 3, DungeonEquipmentSlot.MainHand, 16, 0, 0, 1),
        new("axe", "SVARTJÄRNSYXA", "loot_axe", 2, 3, DungeonEquipmentSlot.MainHand, 22, 0, 0, 1),
        new("hammer", "GRUVHAMMARE", "loot_hammer", 2, 3, DungeonEquipmentSlot.MainHand, 25, 0, 0, 1),
        new("spear", "SILVERSPJUT", "loot_spear", 1, 4, DungeonEquipmentSlot.MainHand, 18, 0, 0, 2),
        new("pistol", "FOGDEPISTOL", "loot_pistol", 2, 2, DungeonEquipmentSlot.OffHand, 14, 0, 0, 1),
        new("cuirass", "BLÅROCKSHARNESK", "loot_cuirass", 2, 3, DungeonEquipmentSlot.Chest, 0, 18, 8, 2),
        new("helmet", "GRUVHJÄLM", "loot_helmet", 2, 2, DungeonEquipmentSlot.Head, 0, 8, 5, 1),
        new("gauntlets", "GULA KRAGHANDSKE", "loot_gauntlets", 2, 2, DungeonEquipmentSlot.Gloves, 0, 4, 0, 1),
        new("boots", "SOTSTÖVLAR", "loot_boots", 2, 2, DungeonEquipmentSlot.Boots, 0, 5, 0, 1),
        new("ring", "SILVERRUNRING", "loot_ring", 1, 1, DungeonEquipmentSlot.RingLeft, 2, 2, 10, 1),
        new("relic", "TEMPELSIGILL", "loot_relic", 2, 2, DungeonEquipmentSlot.Relic, 4, 4, 18, 2),
        new("sun_disc", "LEMMINKÄINENS SOLSKÄRVA", "loot_sun_disc", 2, 2, DungeonEquipmentSlot.Relic, 8, 8, 35, 3),
        new("temple_key", "TUONELAS TEMPELNYCKEL", "loot_temple_key", 1, 2, DungeonEquipmentSlot.Relic, 5, 6, 25, 3),
        new("health_tincture", "KAROLINSK HÄLSOTINKTUR", "loot_health_tincture", 1, 1, DungeonEquipmentSlot.None, 0, 0, 0, 1),
    ];

    private static void SeedDungeonInventory(DungeonState dungeon)
    {
        AddDungeonItem(dungeon, 0, -1, -1, DungeonEquipmentSlot.MainHand, DungeonItemRarity.Carolean);
        AddDungeonItem(dungeon, 6, -1, -1, DungeonEquipmentSlot.Chest, DungeonItemRarity.Carolean);
        AddDungeonItem(dungeon, 7, 0, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Iron);
        AddDungeonItem(dungeon, 3, 2, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Carolean);
        AddDungeonItem(dungeon, 8, 4, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Iron);
        AddDungeonItem(dungeon, 9, 6, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Iron);
        AddDungeonItem(dungeon, 10, 8, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Silverbound);
        AddDungeonItem(dungeon, 11, 8, 2, DungeonEquipmentSlot.None, DungeonItemRarity.Runic);
        DungeonItem groundSaber = AddDungeonItem(dungeon, 1, -1, -1, DungeonEquipmentSlot.None, DungeonItemRarity.Silverbound);
        groundSaber.OnGround = true;
        groundSaber.WorldX = 358;
        groundSaber.WorldY = 225;
        DungeonItem stashAxe = AddDungeonItem(dungeon, 2, 0, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Carolean);
        stashAxe.InStash = true;
        DungeonItem stashPistol = AddDungeonItem(dungeon, 5, 2, 0, DungeonEquipmentSlot.None, DungeonItemRarity.Silverbound);
        stashPistol.InStash = true;
    }

    private static void SeedDungeonEncounter(DungeonState dungeon)
    {
        if (dungeon.Enemies.Any(enemy => enemy.Depth == 1)) return;
        AddDungeonEnemy(dungeon, 225, 240, DungeonEnemyType.Stormer, 42);
        AddDungeonEnemy(dungeon, 475, 244, DungeonEnemyType.Pikeman, 58);
        AddDungeonEnemy(dungeon, 548, 174, DungeonEnemyType.Stormer, 42);
    }

    private static void AddDungeonEnemy(DungeonState dungeon, double x, double y, DungeonEnemyType type, int health,
        int zone = 0)
    {
        var enemy = new DungeonEnemy(x, y, type, health)
            { Id = dungeon.NextEnemyId++, Zone = zone, Depth = dungeon.Depth };
        dungeon.Enemies.Add(enemy);
    }

    private static DungeonItem AddDungeonItem(DungeonState dungeon, int definition, int gridX, int gridY,
        DungeonEquipmentSlot equipped, DungeonItemRarity rarity)
    {
        var item = new DungeonItem
        {
            Id = dungeon.NextItemId++,
            Definition = definition,
            GridX = gridX,
            GridY = gridY,
            Equipped = equipped,
            Rarity = rarity,
            PowerRoll = 3 + definition * 2,
            QuickSlot = -1,
        };
        dungeon.Items.Add(item);
        return item;
    }

    private void StepDungeonInventory(DungeonState dungeon, uint buttons, RtsPointer pointer)
    {
        if (Pressed(buttons, Left)) dungeon.InventoryCursorX = Math.Max(0, dungeon.InventoryCursorX - 1);
        if (Pressed(buttons, Right)) dungeon.InventoryCursorX = Math.Min(9, dungeon.InventoryCursorX + 1);
        if (Pressed(buttons, Up)) dungeon.InventoryCursorY = Math.Max(0, dungeon.InventoryCursorY - 1);
        if (Pressed(buttons, Down)) dungeon.InventoryCursorY = Math.Min(5, dungeon.InventoryCursorY + 1);

        const uint mouseLeft = 1u;
        const uint mouseRight = 2u;
        bool leftPressed = pointer.Inside && (pointer.Buttons & mouseLeft) != 0 &&
            (dungeon.PreviousInventoryMouseButtons & mouseLeft) == 0;
        bool rightPressed = pointer.Inside && (pointer.Buttons & mouseRight) != 0 &&
            (dungeon.PreviousInventoryMouseButtons & mouseRight) == 0;
        int cell = _width <= 320 ? 14 : 18;
        int gridX = _width <= 320 ? 154 : 184;
        int gridY = 52;
        int beltX = 18;
        int beltCell = _width <= 320 ? 18 : 25;
        int beltY = _height - beltCell - 5;
        DungeonItem? focusedItem = dungeon.SelectedItemId != 0
            ? dungeon.Items.FirstOrDefault(item => item.Id == dungeon.SelectedItemId)
            : dungeon.InspectedItemId != 0
                ? dungeon.Items.FirstOrDefault(item => item.Id == dungeon.InspectedItemId)
                : DungeonItemAt(dungeon, dungeon.InventoryCursorX, dungeon.InventoryCursorY);
        (int X, int Y, int Width, int Height) dropRect = DungeonDropRect();
        if (focusedItem is not null && (Pressed(buttons, DropItem) ||
            leftPressed && pointer.X >= dropRect.X && pointer.X < dropRect.X + dropRect.Width &&
            pointer.Y >= dropRect.Y && pointer.Y < dropRect.Y + dropRect.Height))
        {
            DropDungeonItem(dungeon, focusedItem);
            dungeon.PreviousInventoryMouseButtons = pointer.Inside ? pointer.Buttons : 0;
            return;
        }
        if (_width > 320 && !dungeon.ViewingStash && pointer.Inside && pointer.Y >= beltY && pointer.Y < beltY + beltCell &&
            pointer.X >= beltX && pointer.X < beltX + beltCell * 4)
        {
            int slot = (pointer.X - beltX) / beltCell;
            DungeonItem? beltItem = dungeon.Items.FirstOrDefault(item => item.QuickSlot == slot);
            if (leftPressed)
            {
                dungeon.SelectedItemId = 0;
                dungeon.InspectedItemId = beltItem?.Id ?? 0;
            }
            if (rightPressed && beltItem is not null) TryPlaceFirstFree(dungeon, beltItem);
        }
        if (!dungeon.ViewingStash && leftPressed &&
            TryDungeonEquipmentSlotAt(pointer.X, pointer.Y, out DungeonEquipmentSlot equipmentSlot))
        {
            DungeonItem? equipped = dungeon.Items.FirstOrDefault(item => item.Equipped == equipmentSlot);
            dungeon.SelectedItemId = 0;
            dungeon.InspectedItemId = equipped?.Id ?? 0;
        }
        if (pointer.Inside && pointer.X >= gridX && pointer.Y >= gridY)
        {
            int x = (pointer.X - gridX) / cell;
            int y = (pointer.Y - gridY) / cell;
            if (x is >= 0 and < 10 && y is >= 0 and < 6)
            {
                dungeon.InventoryCursorX = x;
                dungeon.InventoryCursorY = y;
                if (leftPressed)
                {
                    dungeon.InspectedItemId = 0;
                    InventoryPickOrPlace(dungeon, x, y);
                }
                if (rightPressed)
                {
                    dungeon.InspectedItemId = 0;
                    DungeonItem? item = DungeonItemAt(dungeon, x, y);
                    if (item is not null)
                    {
                        if (dungeon.ViewingStash)
                        {
                            item.InStash = false;
                            if (!TryPlaceFirstFree(dungeon, item)) item.InStash = true;
                        }
                        else EquipDungeonItem(dungeon, item);
                    }
                }
            }
        }
        if (Pressed(buttons, Fire)) InventoryPickOrPlace(dungeon, dungeon.InventoryCursorX, dungeon.InventoryCursorY);
        if (Pressed(buttons, Slow))
        {
            DungeonItem? item = DungeonItemAt(dungeon, dungeon.InventoryCursorX, dungeon.InventoryCursorY);
            if (item is not null) EquipDungeonItem(dungeon, item);
        }
        dungeon.PreviousInventoryMouseButtons = pointer.Inside ? pointer.Buttons : 0;
    }

    private static DungeonItem? DungeonItemAt(DungeonState dungeon, int x, int y) => dungeon.Items.FirstOrDefault(item =>
    {
        if (item.Equipped != DungeonEquipmentSlot.None || item.OnGround || item.QuickSlot >= 0 ||
            item.InStash != dungeon.ViewingStash || item.GridX < 0) return false;
        DungeonItemDefinition definition = DungeonItemDefinitions[item.Definition];
        return x >= item.GridX && x < item.GridX + definition.Width && y >= item.GridY && y < item.GridY + definition.Height;
    });

    private static bool TryDungeonEquipmentSlotAt(int pointerX, int pointerY, out DungeonEquipmentSlot slot)
    {
        DungeonEquipmentSlot[] slots = [DungeonEquipmentSlot.Head, DungeonEquipmentSlot.Chest,
            DungeonEquipmentSlot.Gloves, DungeonEquipmentSlot.Boots, DungeonEquipmentSlot.Belt,
            DungeonEquipmentSlot.Amulet, DungeonEquipmentSlot.RingLeft, DungeonEquipmentSlot.RingRight,
            DungeonEquipmentSlot.MainHand, DungeonEquipmentSlot.OffHand, DungeonEquipmentSlot.Relic];
        for (int index = 0; index < slots.Length; index++)
        {
            int x = 18 + index % 2 * 62;
            int y = 50 + index / 2 * 27;
            if (pointerX >= x && pointerX < x + 54 && pointerY >= y && pointerY < y + 23)
            {
                slot = slots[index];
                return true;
            }
        }
        slot = DungeonEquipmentSlot.None;
        return false;
    }

    private static void InventoryPickOrPlace(DungeonState dungeon, int x, int y)
    {
        dungeon.InspectedItemId = 0;
        if (dungeon.SelectedItemId == 0)
        {
            dungeon.SelectedItemId = DungeonItemAt(dungeon, x, y)?.Id ?? 0;
            return;
        }
        DungeonItem? selected = dungeon.Items.FirstOrDefault(item => item.Id == dungeon.SelectedItemId);
        if (selected is null) { dungeon.SelectedItemId = 0; return; }
        if (CanPlaceDungeonItem(dungeon, selected, x, y))
        {
            selected.GridX = x;
            selected.GridY = y;
            selected.Equipped = DungeonEquipmentSlot.None;
            selected.QuickSlot = -1;
            dungeon.SelectedItemId = 0;
        }
    }

    private static bool CanPlaceDungeonItem(DungeonState dungeon, DungeonItem item, int x, int y)
    {
        DungeonItemDefinition definition = DungeonItemDefinitions[item.Definition];
        if (x < 0 || y < 0 || x + definition.Width > 10 || y + definition.Height > 6) return false;
        return !dungeon.Items.Any(other => other.Id != item.Id && other.Equipped == DungeonEquipmentSlot.None &&
            !other.OnGround && other.QuickSlot < 0 && other.InStash == item.InStash && other.GridX >= 0 && RectanglesOverlap(x, y, definition.Width, definition.Height, other.GridX, other.GridY,
                DungeonItemDefinitions[other.Definition].Width, DungeonItemDefinitions[other.Definition].Height));
    }

    private static bool RectanglesOverlap(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh) =>
        ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    private static void EquipDungeonItem(DungeonState dungeon, DungeonItem item)
    {
        if (item.Definition == 14)
        {
            TryPlaceFirstPotionBelt(dungeon, item);
            dungeon.SelectedItemId = 0;
            dungeon.InspectedItemId = 0;
            return;
        }
        DungeonEquipmentSlot slot = DungeonItemDefinitions[item.Definition].Slot;
        if (slot == DungeonEquipmentSlot.None || dungeon.Level < DungeonRequiredLevel(item)) return;
        DungeonItem? previous = dungeon.Items.FirstOrDefault(candidate => candidate.Equipped == slot);
        if (previous is not null && !TryPlaceFirstFree(dungeon, previous)) return;
        item.Equipped = slot;
        item.GridX = -1;
        item.GridY = -1;
        item.QuickSlot = -1;
        dungeon.SelectedItemId = 0;
        dungeon.InspectedItemId = 0;
    }

    private static bool TryPlaceFirstFree(DungeonState dungeon, DungeonItem item)
    {
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 10; x++)
        if (CanPlaceDungeonItem(dungeon, item, x, y))
        {
            item.Equipped = DungeonEquipmentSlot.None;
            item.QuickSlot = -1;
            item.GridX = x;
            item.GridY = y;
            return true;
        }
        return false;
    }

    private void DrawDungeonHealth(uint[] frame, DungeonState dungeon)
    {
        const int healthX = 4;
        const int healthY = 20;
        const int powerX = 61;
        const int powerY = 31;
        if (_sprites?.TryGet("ui_health_orb", out Sprite healthOrb) == true)
            DrawSprite(frame, healthOrb, healthX, healthY);
        if (_sprites?.TryGet("ui_power_orb", out Sprite powerOrb) == true)
            DrawSprite(frame, powerOrb, powerX, powerY);
        double healthFill = Math.Clamp(dungeon.Health / (double)Math.Max(1, dungeon.MaxHealth), 0, 1);
        // These are local glass centers inside the trimmed sprites. Keeping
        // them relative to the sprite origin prevents the liquid mask from
        // drifting away when either ornament is moved in the HUD.
        DrawOrbDepletion(frame, healthX + 25, healthY + 24, 15, 16, healthFill, 0xff16070a, 0xffb84750);
        double powerFill = Math.Clamp(dungeon.Power / 100.0, 0, 1);
        DrawOrbDepletion(frame, powerX + 18, powerY + 20, 11, 11, powerFill, 0xff050b16, 0xff4f83c6);
    }

    private void DrawDungeonPotionBelt(uint[] frame, DungeonState dungeon)
    {
        int slotSize = _width <= 320 ? 21 : 25;
        int x = 4;
        int y = _height - slotSize - 17;
        DrawText(frame, x, y - 9, "Q TINKTUR", 0xffdce8f2);
        for (int slot = 0; slot < 4; slot++)
        {
            int slotX = x + slot * slotSize;
            DrawRect(frame, slotX, y, slotSize - 2, slotSize - 2, 0xdd111920);
            DrawRect(frame, slotX + 2, y + 2, slotSize - 6, slotSize - 6, 0xff192631);
            DungeonItem? tincture = dungeon.Items.FirstOrDefault(item => item.Definition == 14 && item.QuickSlot == slot);
            if (tincture is not null && _sprites?.TryGet("loot_health_tincture", out Sprite icon) == true)
                DrawSprite(frame, icon, slotX + (slotSize - icon.Width) / 2 - 1, y + (slotSize - icon.Height) / 2 - 1);
            DrawText(frame, slotX + 3, y + slotSize - 8, (slot + 1).ToString(), 0xff697f8d);
        }
    }

    private void DrawOrbDepletion(uint[] frame, int centerX, int centerY, int radiusX, int radiusY,
        double fillFraction, uint emptyColor, uint surfaceColor)
    {
        if (fillFraction >= 0.999) return;
        // Solve the area of a circular segment, not a linear height. Thus 25%
        // life leaves exactly 25% of the visible orb interior filled.
        double targetEmptyArea = 1.0 - Math.Clamp(fillFraction, 0, 1);
        double low = -1, high = 1;
        for (int iteration = 0; iteration < 24; iteration++)
        {
            double middle = (low + high) * 0.5;
            double cumulative = (middle * Math.Sqrt(Math.Max(0, 1 - middle * middle)) +
                Math.Asin(middle) + Math.PI / 2) / Math.PI;
            if (cumulative < targetEmptyArea) low = middle; else high = middle;
        }
        int surfaceY = centerY + (int)Math.Round((low + high) * 0.5 * radiusY);
        for (int y = centerY - radiusY + 1; y < surfaceY; y++)
        {
            double normalizedY = (y - centerY) / (double)radiusY;
            int halfWidth = (int)Math.Floor(radiusX * Math.Sqrt(Math.Max(0, 1 - normalizedY * normalizedY)));
            if (halfWidth > 0) DrawLine(frame, centerX - halfWidth, y, centerX + halfWidth, y, emptyColor);
        }
        double surfaceNormalized = (surfaceY - centerY) / (double)radiusY;
        int surfaceHalfWidth = (int)Math.Floor(radiusX * Math.Sqrt(Math.Max(0, 1 - surfaceNormalized * surfaceNormalized)));
        if (surfaceHalfWidth > 3 && fillFraction > 0.01)
            DrawLine(frame, centerX - surfaceHalfWidth + 2, surfaceY, centerX + surfaceHalfWidth - 2, surfaceY, surfaceColor);
    }

    private void DrawDungeonInventory(uint[] frame, DungeonState dungeon)
    {
        DrawRect(frame, 0, 18, _width, _height - 18, 0xff080d12);
        DrawLine(frame, 6, 20, _width - 7, 20, 0xffffd66b);
        DrawLine(frame, 6, _height - 2, _width - 7, _height - 2, 0xff2f74c9);
        if (_sprites?.TryGet("ui_inventory_corner", out Sprite corner) == true) DrawSprite(frame, corner, 5, 20);
        if (_sprites?.TryGet("ui_inventory_divider", out Sprite divider) == true)
            DrawSprite(frame, divider, (_width - divider.Width) / 2, 20);
        if (_sprites?.TryGet(dungeon.ViewingStash ? "ui_stash_crest" : "ui_carolean_silhouette", out Sprite silhouette) == true)
            DrawSpriteAlpha(frame, silhouette, 47 - silhouette.Width / 2, 72, 105);
        DrawText(frame, 18, 29, "KARL CCLV", 0xffffd66b);
        (int X, int Y, int Width, int Height) dropRect = DungeonDropRect();
        DrawRect(frame, dropRect.X, dropRect.Y, dropRect.Width, dropRect.Height, 0xff351b1f);
        DrawRect(frame, dropRect.X + 1, dropRect.Y + 1, dropRect.Width - 2, dropRect.Height - 2, 0xff171115);
        DrawText(frame, dropRect.X + 7, dropRect.Y + 5, "SLÄNG", 0xffff9c8f);
        DrawText(frame, _width <= 320 ? 154 : 184, 29, dungeon.ViewingStash ? "FÖRRÅD 10X6" : "RYGGSÄCK 10X6", 0xff9bd4dc);
        int cell = _width <= 320 ? 14 : 18;
        int gridX = _width <= 320 ? 154 : 184;
        int gridY = 52;
        if (_sprites?.TryGet("ui_backpack_grid", out Sprite backpackPlate) == true)
            DrawSpriteAlpha(frame, backpackPlate, gridX + (cell * 10 - backpackPlate.Width) / 2,
                gridY + (cell * 6 - backpackPlate.Height) / 2, 72);
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 10; x++)
        {
            bool cursorCell = x == dungeon.InventoryCursorX && y == dungeon.InventoryCursorY;
            uint color = cursorCell ? 0xff26323a : 0xff17212a;
            DrawRect(frame, gridX + x * cell, gridY + y * cell, cell - 1, cell - 1, color);
            if (cursorCell)
                DrawRect(frame, gridX + x * cell + 2, gridY + y * cell + 2, cell - 5, cell - 5, 0xff202b32);
        }
        foreach (DungeonItem item in dungeon.Items.Where(candidate => candidate.Equipped == DungeonEquipmentSlot.None &&
            !candidate.OnGround && candidate.QuickSlot < 0 && candidate.InStash == dungeon.ViewingStash && candidate.GridX >= 0))
        {
            DungeonItemDefinition definition = DungeonItemDefinitions[item.Definition];
            int x = gridX + item.GridX * cell;
            int y = gridY + item.GridY * cell;
            if (_sprites?.TryGet(definition.Sprite, out Sprite icon) == true)
                DrawSprite(frame, icon, x + (definition.Width * cell - icon.Width) / 2, y + (definition.Height * cell - icon.Height) / 2);
            if (item.Id == dungeon.SelectedItemId)
            {
                int right = x + definition.Width * cell - 3;
                int bottom = y + definition.Height * cell - 3;
                DrawLine(frame, x + 3, y + 3, x + 8, y + 3, 0xffdce8f2);
                DrawLine(frame, x + 3, y + 3, x + 3, y + 8, 0xffdce8f2);
                DrawLine(frame, right - 5, bottom, right, bottom, 0xffdce8f2);
                DrawLine(frame, right, bottom - 5, right, bottom, 0xffdce8f2);
            }
        }

        if (_width > 320 && !dungeon.ViewingStash)
        {
            int beltCell = _width <= 320 ? 18 : 25;
            int beltX = 18;
            int beltY = _height - beltCell - 5;
            DrawText(frame, beltX, beltY - 9, "SNABBBÄLTE  Q DRICK", 0xff9bd4dc);
            for (int slot = 0; slot < 4; slot++)
            {
                int x = beltX + slot * beltCell;
                DrawRect(frame, x, beltY, beltCell - 2, beltCell - 2, 0xff151e25);
                DungeonItem? tincture = dungeon.Items.FirstOrDefault(item => item.QuickSlot == slot);
                if (tincture is not null && _sprites?.TryGet("loot_health_tincture", out Sprite icon) == true)
                    DrawSprite(frame, icon, x + (beltCell - icon.Width) / 2 - 1, beltY + (beltCell - icon.Height) / 2 - 1);
            }
        }

        string[] slotLabels = ["HUV", "BRÖ", "HAN", "STÖ", "BÄL", "AMU", "R 1", "R 2", "HND", "OFF", "REL"];
        DungeonEquipmentSlot[] slots = [DungeonEquipmentSlot.Head, DungeonEquipmentSlot.Chest, DungeonEquipmentSlot.Gloves,
            DungeonEquipmentSlot.Boots, DungeonEquipmentSlot.Belt, DungeonEquipmentSlot.Amulet, DungeonEquipmentSlot.RingLeft,
            DungeonEquipmentSlot.RingRight, DungeonEquipmentSlot.MainHand, DungeonEquipmentSlot.OffHand, DungeonEquipmentSlot.Relic];
        for (int index = 0; index < slots.Length; index++)
        {
            int column = index % 2;
            int row = index / 2;
            int x = 18 + column * 62;
            int y = 50 + row * 27;
            DrawRect(frame, x, y, 54, 23, 0xff151e25);
            if (_sprites?.TryGet("ui_item_slot", out Sprite slotFrame) == true)
                DrawSpriteAlpha(frame, slotFrame, x + 23, y - 3, 150);
            DrawText(frame, x + 3, y + 8, slotLabels[index], 0xff697f8d);
            DungeonItem? equipped = dungeon.Items.FirstOrDefault(item => item.Equipped == slots[index]);
            if (equipped is not null && _sprites?.TryGet(DungeonItemDefinitions[equipped.Definition].Sprite, out Sprite icon) == true)
            {
                DrawSprite(frame, icon, x + 31, y + 1);
                if (equipped.Id == dungeon.InspectedItemId)
                {
                    DrawLine(frame, x, y, x + 53, y, 0xffffd66b);
                    DrawLine(frame, x, y + 22, x + 53, y + 22, 0xffffd66b);
                    DrawLine(frame, x, y, x, y + 22, 0xffffd66b);
                    DrawLine(frame, x + 53, y, x + 53, y + 22, 0xffffd66b);
                }
            }
        }
        DungeonItem? focused = dungeon.SelectedItemId != 0
            ? dungeon.Items.FirstOrDefault(item => item.Id == dungeon.SelectedItemId)
            : dungeon.InspectedItemId != 0
                ? dungeon.Items.FirstOrDefault(item => item.Id == dungeon.InspectedItemId)
                : DungeonItemAt(dungeon, dungeon.InventoryCursorX, dungeon.InventoryCursorY);
        int infoY = gridY + cell * 6 + 8;
        if (focused is not null)
        {
            DungeonItemDefinition definition = DungeonItemDefinitions[focused.Definition];
            int line = _width <= 320 ? 10 : 13;
            int damage = definition.Damage + (definition.Slot == DungeonEquipmentSlot.MainHand ? focused.PowerRoll / 3 : 0);
            int armor = definition.Armor + (int)focused.Rarity * 2;
            DrawText(frame, gridX, infoY, definition.Name, DungeonRarityColor(focused.Rarity));
            DrawText(frame, gridX, infoY + line, $"SKADA {damage:00}  RUST {armor:00}", 0xffdce8f2);
            DrawText(frame, gridX, infoY + line * 2, $"KRAFT {focused.PowerRoll:00}  VÄRN {definition.CurseWard:00}%", 0xffdce8f2);
            if (focused.Equipped != DungeonEquipmentSlot.None)
            {
                DrawText(frame, gridX, infoY + line * 3, "UTRUSTAD", 0xff9bd4dc);
            }
            else if (definition.Slot != DungeonEquipmentSlot.None)
            {
                DungeonItem? compared = dungeon.Items.FirstOrDefault(item => item.Equipped == definition.Slot);
                DungeonItemDefinition comparedDefinition = compared is null
                    ? default : DungeonItemDefinitions[compared.Definition];
                int comparedDamage = compared is null ? 0 : comparedDefinition.Damage +
                    (comparedDefinition.Slot == DungeonEquipmentSlot.MainHand ? compared.PowerRoll / 3 : 0);
                int comparedArmor = compared is null ? 0 : comparedDefinition.Armor + (int)compared.Rarity * 2;
                int comparedPower = compared?.PowerRoll ?? 0;
                int comparedWard = compared is null ? 0 : comparedDefinition.CurseWard;
                int secondColumn = gridX + (_width <= 320 ? 78 : 104);
                DrawDungeonStatDelta(frame, gridX, infoY + line * 3, "SKADA", damage - comparedDamage);
                DrawDungeonStatDelta(frame, secondColumn, infoY + line * 3, "RUST", armor - comparedArmor);
                DrawDungeonStatDelta(frame, gridX, infoY + line * 4, "KRAFT", focused.PowerRoll - comparedPower);
                DrawDungeonStatDelta(frame, secondColumn, infoY + line * 4, "VÄRN", definition.CurseWard - comparedWard);
            }
            uint requirementColor = dungeon.Level >= DungeonRequiredLevel(focused) ? 0xff9bd4dc : 0xffff6b62;
            DrawText(frame, gridX, infoY + line * 5, focused.Definition == 14 ? "HÖGERKLICK TILL SNABBBÄLTE" :
                $"KRÄVER NIVÅ {DungeonRequiredLevel(focused):00}", requirementColor);
            DrawText(frame, gridX, infoY + line * 6, $"TOTAL RUST {DungeonArmor(dungeon):00}  FÖRB {DungeonCurseWard(dungeon):00}%", 0xffaeb8bd);
        }
        string inventoryHelp = dungeon.ViewingStash ? "X STÄNG  HÖGERKLICK TA" : _width <= 320
            ? "X STÄNG  Z FLYTTA  D SLÄNG"
            : "X STÄNG  Z FLYTTA  D SLÄNG  HÖGERKLICK UTRUSTA/BÄLTE";
        DrawText(frame, 18, _height - 10, inventoryHelp, 0xffb7c7d6);
    }

    private void DrawDungeonStatDelta(uint[] frame, int x, int y, string label, int delta)
    {
        uint color = delta > 0 ? 0xff65c58a : delta < 0 ? 0xffff6b62 : 0xff82929d;
        string sign = delta > 0 ? "+" : delta < 0 ? "-" : "=";
        DrawText(frame, x, y, $"{sign}{Math.Abs(delta):00} {label}", color);
    }

    private (int X, int Y, int Width, int Height) DungeonDropRect() => (_width - 62, 27, 54, 17);

    private static uint DungeonRarityColor(DungeonItemRarity rarity) => rarity switch
    {
        DungeonItemRarity.Carolean => 0xffffd66b,
        DungeonItemRarity.Silverbound => 0xff9bd4dc,
        DungeonItemRarity.Runic => 0xff65c58a,
        DungeonItemRarity.Unique => 0xffc589ff,
        _ => 0xffaeb8bd,
    };

    private void DrawDungeonSprite(uint[] frame, DungeonState dungeon, string name, int worldX, int worldY)
    {
        if (_sprites?.TryGet(name, out Sprite sprite) != true) return;
        DrawSprite(frame, sprite, worldX - dungeon.CameraX - sprite.Width / 2,
            worldY - dungeon.CameraY - sprite.Height / 2);
    }

    private void DrawRts(uint[] frame)
    {
        if (_rts is not RtsState rts)
        {
            return;
        }
        DrawRect(frame, 0, 0, _width, _height, 0xff08110e);
        DrawRect(frame, 0, 18, _width, _height - 32, 0xff101b16);
        if (_sprites?.TryGet("rts_forest_floor", out Sprite floor) == true)
        {
            int startX = -(rts.CameraX % floor.Width);
            for (int y = 18; y < _height - 14; y += floor.Height)
            {
                for (int x = startX; x < _width; x += floor.Width) DrawSprite(frame, floor, x, y);
            }
        }

        for (int worldY = 34; worldY < rts.MapHeight; worldY += 26)
        {
            int worldX = SilverVeinWorldX(worldY);
            int x = worldX - rts.CameraX;
            string veinName = worldY % 104 == 34 ? "rts_vein_node" :
                worldY % 78 == 60 ? "rts_vein_branch" :
                worldY % 52 == 34 ? "rts_vein_curve" : "rts_vein_straight";
            if (x >= -24 && x <= _width + 24 && _sprites?.TryGet(veinName, out Sprite vein) == true)
            {
                if ((worldY / 26 & 1) == 0) DrawSprite(frame, vein, x - vein.Width / 2, worldY - vein.Height / 2);
                else DrawSpriteFlippedX(frame, vein, x - vein.Width / 2, worldY - vein.Height / 2);
            }
        }

        int roadX = rts.MapWidth - 74 - rts.CameraX;
        if (_sprites?.TryGet("rts_frontier_road_intact", out Sprite roadIntact) == true &&
            _sprites.TryGet("rts_frontier_road_churned", out Sprite roadChurned))
        {
            for (int y = 18, section = 0; y < rts.MapHeight; y += roadIntact.Height, section++)
            {
                Sprite tile = section % 3 == 1 ? roadChurned : roadIntact;
                DrawSprite(frame, tile, roadX - tile.Width / 2, y);
            }
            // Fixed roadside lamps breathe very gently. Nothing travels along
            // the road: moving highlights read as an invisible vehicle here.
            uint markerGlow = (rts.Age / 14 & 1) == 0 ? 0xff9c453d : 0xff6d302d;
            for (int markerY = 48; markerY < rts.MapHeight; markerY += 64)
            {
                PutPixel(frame, roadX - 19, markerY, markerGlow);
                PutPixel(frame, roadX + 19, markerY + 28, markerGlow);
            }
        }
        else
        {
            DrawRect(frame, roadX - 15, 18, 31, rts.MapHeight - 6, 0xff27251f);
        }

        var drawables = new List<(double Y, int Kind, object? Entity)>(
            rts.Props.Count + rts.Buildings.Count + rts.Units.Count + rts.Enemies.Count + rts.Miners.Count + 2);
        drawables.AddRange(rts.Props.Select(prop => ((double)prop.Y, 0, (object?)prop)));
        drawables.AddRange(rts.Buildings.Select(building => ((double)building.Y, 1, (object?)building)));
        drawables.AddRange(rts.Units.Select(unit => (unit.Y, 2, (object?)unit)));
        drawables.AddRange(rts.Enemies.Select(enemy => (enemy.Y, 3, (object?)enemy)));
        drawables.AddRange(rts.Miners.Select(miner => (miner.Y, 4, (object?)miner)));
        if (rts.Fortress is RtsFortress fortress) drawables.Add((fortress.Y, 5, fortress));
        drawables.Add((rts.LandingY, 6, null));
        drawables.Sort(static (left, right) =>
        {
            int yOrder = left.Y.CompareTo(right.Y);
            return yOrder != 0 ? yOrder : left.Kind.CompareTo(right.Kind);
        });
        foreach ((_, int kind, object? entity) in drawables)
        {
            switch (kind)
            {
                case 0: DrawRtsProp(frame, rts, (RtsProp)entity!); break;
                case 1: DrawRtsBuilding(frame, rts, (RtsBuilding)entity!); break;
                case 2: DrawRtsUnit(frame, rts, (RtsUnit)entity!); break;
                case 3: DrawRtsEnemy(frame, rts, (RtsEnemy)entity!); break;
                case 4: DrawRtsMiner(frame, rts, (RtsMiner)entity!); break;
                case 5: DrawRtsFortress(frame, rts, (RtsFortress)entity!); break;
                default: DrawRtsLandedKarl(frame, rts); break;
            }
        }
        if (rts.EvacuationAge >= 690)
        {
            RtsBuilding? crusher = rts.Buildings.FirstOrDefault(building => building.Type == RtsBuildingType.SilverCrusher);
            if (crusher is not null) DrawRtsTempleCrack(frame, rts, crusher.X, crusher.Y + 17);
        }
        if (rts.EvacuationAge == 0 && rts.LandingAge >= 120 && (rts.BuildStage < 4 || rts.TowerPlacementMode))
        {
            int buildX = (rts.CursorX / 12) * 12;
            int buildY = (rts.CursorY / 12) * 12;
            int screenX = buildX - rts.CameraX;
            bool valid = rts.TowerPlacementMode ? IsValidRtsTowerPlacement(rts, buildX, buildY) :
                IsValidRtsPlacement(rts, buildX, buildY);
            uint ghost = valid ? 0xff65c58a : 0xffff6b62;
            string previewName = rts.TowerPlacementMode ? "rts_tower_idle" : rts.BuildStage switch
            {
                0 => "rts_steam_idle",
                1 => "rts_crusher_idle",
                2 => "rts_barracks",
                _ => "rts_animal_hall",
            };
            int halfWidth = 16;
            int halfHeight = 12;
            if (_sprites?.TryGet(previewName, out Sprite preview) == true)
            {
                DrawSpriteAlpha(frame, preview, screenX - preview.Width / 2, buildY - preview.Height / 2, 145);
                halfWidth = preview.Width / 2 + 2;
                halfHeight = preview.Height / 2 + 2;
            }
            DrawLine(frame, screenX - halfWidth, buildY - halfHeight, screenX + halfWidth, buildY - halfHeight, ghost);
            DrawLine(frame, screenX - halfWidth, buildY + halfHeight, screenX + halfWidth, buildY + halfHeight, ghost);
            DrawLine(frame, screenX - halfWidth, buildY - halfHeight, screenX - halfWidth, buildY + halfHeight, ghost);
            DrawLine(frame, screenX + halfWidth, buildY - halfHeight, screenX + halfWidth, buildY + halfHeight, ghost);
        }

        if (rts.EvacuationAge == 0 && rts.LandingAge >= 120)
        {
            int cursorX = rts.CursorX - rts.CameraX;
            uint cursor = (rts.Age / 5 & 1) == 0 ? 0xffffd66b : 0xff7fc7ff;
            DrawLine(frame, cursorX - 7, rts.CursorY - 7, cursorX - 2, rts.CursorY - 7, cursor);
            DrawLine(frame, cursorX + 2, rts.CursorY - 7, cursorX + 7, rts.CursorY - 7, cursor);
            DrawLine(frame, cursorX - 7, rts.CursorY + 7, cursorX - 2, rts.CursorY + 7, cursor);
            DrawLine(frame, cursorX + 2, rts.CursorY + 7, cursorX + 7, rts.CursorY + 7, cursor);
        }
        if (rts.MouseSelecting)
        {
            int left = Math.Min(rts.MouseDragStartX, rts.MouseWorldX) - rts.CameraX;
            int right = Math.Max(rts.MouseDragStartX, rts.MouseWorldX) - rts.CameraX;
            int top = Math.Min(rts.MouseDragStartY, rts.MouseWorldY);
            int bottom = Math.Max(rts.MouseDragStartY, rts.MouseWorldY);
            uint selection = (rts.Age / 4 & 1) == 0 ? 0xffffd66b : 0xff9bd4dc;
            DrawLine(frame, left, top, right, top, selection);
            DrawLine(frame, left, bottom, right, bottom, selection);
            DrawLine(frame, left, top, left, bottom, selection);
            DrawLine(frame, right, top, right, bottom, selection);
        }

        DrawRect(frame, 0, 0, _width, 18, 0xff080d12);
        DrawText(frame, 6, 5, "SILVERKROPPEN", 0xffffd66b);
        int powerUsed = rts.Buildings.Sum(building => building.Type switch
        {
            RtsBuildingType.SilverCrusher => 40,
            RtsBuildingType.Barracks => 20,
            RtsBuildingType.AnimalHall => 30,
            RtsBuildingType.DefenseTower => 10,
            _ => 0,
        });
        int powerTotal = rts.Buildings.Any(building => building.Type == RtsBuildingType.SteamPlant) ? 100 : 0;
        string economy = $"K {powerUsed:000}/{powerTotal:000} S {rts.Silver:0000} B {rts.SalvagedSilver:0000}/{RtsSalvagedSilverGoal}";
        DrawText(frame, _width - economy.Length * 6 - 5, 5, economy,
            rts.SilverPulse > 0 ? 0xffffffff : 0xff9bd4dc);
        DrawRect(frame, 0, _height - 14, _width, 14, 0xff080d12);
        string objective = rts.EvacuationAge > 0 ? rts.EvacuationAge switch
        {
            < 350 => "EVAKUERING TILL KARL",
            < 560 => "MOTORER STARTAR",
            < 850 => "EN GRUVFOGDE SAKNAS",
            _ => "SIGNAL UNDER GRUVAN",
        } : rts.LandingAge < 120 ? "KARL CCLV LANDAR" :
            rts.BuildStage == 0 ? "Z PLACERA ÅNGKRAFTVERK" :
            rts.BuildStage == 1 ? "Z PLACERA SILVERKROSS VID ÅDERN" : "SILVERBRYTNING AKTIV";
        if (rts.EvacuationAge == 0) objective = rts.BuildStage switch
        {
            2 => "Z PLACERA KAROLINERBARACK",
            3 => "Z PLACERA DJURHALL",
            >= 4 when rts.SilverGoalReached => "SILVERMÅL NÅTT - HÅLL LINJEN",
            >= 4 when rts.BarracksTimer > 0 => $"KAROLINER {rts.BarracksTimer:000}",
            >= 4 when rts.AnimalHallTimer > 0 => $"ÄLGKAROLIN {rts.AnimalHallTimer:000}",
            >= 4 when rts.TowerPlacementMode => "SLOW+Z PLACERA FÖRSVARSTORN",
            >= 4 => "Z VÄLJ / PRODUCERA  SLOW+Z TORN",
            _ => objective,
        };
        DrawText(frame, 7, _height - 10, objective, rts.LandingAge < 120 ? 0xff9bd4dc : 0xffffd66b);
        if (_gameOver)
        {
            int panelX = (_width - 190) / 2;
            int panelY = (_height - 42) / 2;
            DrawRect(frame, panelX, panelY, 190, 42, 0xee120b0d);
            DrawText(frame, panelX + 31, panelY + 9, "KRIGSKASSAN FÖLL", 0xffff6b7f);
            DrawText(frame, panelX + 37, panelY + 26, "START ÅTERKALLAR", 0xffffd66b);
        }
        if (_bossRadioCard is RadioCard rtsRadio && _bossRadioAge < rtsRadio.DurationFrames)
        {
            DrawRadioCard(frame, rtsRadio, _bossRadioAge);
        }
    }

    private void DrawRtsBuilding(uint[] frame, RtsState rts, RtsBuilding building)
    {
        int x = building.X - rts.CameraX;
        int y = building.Y;
        string generatedName = building.Type switch
        {
            // Keep the physical plant on one stable frame. The generated
            // working concept changes its actual chimney geometry, so steam
            // is animated separately below instead of morphing the building.
            RtsBuildingType.SteamPlant => "rts_steam_idle",
            RtsBuildingType.SilverCrusher => "rts_crusher_idle",
            RtsBuildingType.Barracks => "rts_barracks",
            RtsBuildingType.AnimalHall => "rts_animal_hall",
            RtsBuildingType.DefenseTower => building.Cooldown > 24 ? "rts_tower_fire" : "rts_tower_idle",
            _ => "rts_barracks",
        };
        if (_sprites?.TryGet(generatedName, out Sprite generated) == true)
        {
            DrawSprite(frame, generated, x - generated.Width / 2, y - generated.Height / 2);
            if (building.Type == RtsBuildingType.SteamPlant)
            {
                for (int puff = 0; puff < 4; puff++)
                {
                    int phase = (rts.Age * 2 + puff * 13) % 48;
                    int steamX = x + 7 + (int)Math.Round(Math.Sin((rts.Age + puff * 17) * 0.11) * 2.0);
                    int steamY = y - 19 - phase / 2;
                    int radius = 1 + phase / 16;
                    uint color = phase < 28 ? 0xffc3cec8 : 0xff7f9189;
                    FillCircle(frame, steamX, steamY, radius, color);
                }
            }
            else if (building.Type == RtsBuildingType.SilverCrusher)
            {
                int bite = (rts.Age / 6 & 1) == 0 ? 0 : 2;
                uint jaw = (rts.Age / 4 & 1) == 0 ? 0xffd6ded7 : 0xff8fa69c;
                DrawLine(frame, x - 7 + bite, y + 4, x - 1, y + 8, jaw);
                DrawLine(frame, x + 7 - bite, y + 4, x + 1, y + 8, jaw);
                if (rts.Age % 18 < 5)
                {
                    PutPixel(frame, x, y + 7, 0xffffffff);
                    PutPixel(frame, x - 2, y + 9, 0xffb9d6cc);
                    PutPixel(frame, x + 2, y + 9, 0xff65c58a);
                }
                for (int puff = 0; puff < 2; puff++)
                {
                    int phase = (rts.Age + puff * 11) % 32;
                    FillCircle(frame, x + 8 + puff * 3, y - 17 - phase / 3,
                        1 + phase / 16, phase < 20 ? 0xffaebdb6 : 0xff697b73);
                }
            }
            if (building.Type == RtsBuildingType.Barracks)
            {
                DrawRtsProductionBar(frame, x, y + generated.Height / 2 + 1, rts.BarracksTimer, 90);
            }
            else if (building.Type == RtsBuildingType.AnimalHall)
            {
                DrawRtsProductionBar(frame, x, y + generated.Height / 2 + 1, rts.AnimalHallTimer, 150);
            }
            return;
        }
        if (building.Type == RtsBuildingType.SteamPlant)
        {
            DrawRect(frame, x - 15, y - 11, 31, 23, 0xff252b29);
            FillCircle(frame, x - 6, y, 8, 0xff6d4935);
            FillCircle(frame, x - 6, y, 5, 0xff171d1b);
            DrawRect(frame, x + 4, y - 14, 8, 22, 0xff3f4642);
            DrawLine(frame, x - 14, y + 11, x + 15, y + 11, 0xffffd66b);
            int steamY = y - 18 - (rts.Age / 3 % 12);
            FillCircle(frame, x + 8, steamY, 2, 0xffb7c7c0);
            return;
        }
        if (building.Type == RtsBuildingType.SilverCrusher)
        {
            DrawRect(frame, x - 16, y - 12, 33, 25, 0xff303534);
            DrawRect(frame, x - 12, y - 8, 24, 8, 0xff6f7775);
            DrawLine(frame, x - 11, y - 7, x + 11, y - 1, 0xffd6ded7);
            FillCircle(frame, x, y + 6, 7, 0xff7a5238);
            FillCircle(frame, x, y + 6, 3, 0xff151b18);
            PutPixel(frame, x + 4, y - 5, 0xff65c58a);
            return;
        }
        if (building.Type == RtsBuildingType.Barracks)
        {
            DrawRect(frame, x - 17, y - 12, 35, 25, 0xff28343b);
            DrawRect(frame, x - 13, y - 8, 26, 17, 0xff244f91);
            DrawRect(frame, x - 4, y - 10, 9, 20, 0xff151d24);
            DrawLine(frame, x - 13, y - 8, x + 13, y - 8, 0xffffd66b);
            DrawLine(frame, x - 11, y + 11, x - 4, y - 2, 0xffb7c7d6);
            DrawLine(frame, x + 11, y + 11, x + 4, y - 2, 0xffb7c7d6);
            DrawRtsProductionBar(frame, x, y + 15, rts.BarracksTimer, 90);
            return;
        }
        if (building.Type == RtsBuildingType.DefenseTower)
        {
            FillCircle(frame, x, y + 4, 11, 0xff29343d);
            DrawRect(frame, x - 7, y - 8, 15, 18, 0xff244f91);
            DrawLine(frame, x, y - 7, x + 13, y - 15, 0xffffd66b);
            FillCircle(frame, x + 13, y - 15, 2, 0xffb7c7d6);
            if (building.Cooldown > 20)
            {
                PutPixel(frame, x + 14, y - 16, 0xffffffff);
            }
            return;
        }
        DrawRect(frame, x - 20, y - 14, 41, 29, 0xff2b3432);
        DrawRect(frame, x - 16, y - 10, 33, 21, 0xff315b42);
        DrawRect(frame, x - 7, y - 12, 15, 24, 0xff151d1a);
        DrawLine(frame, x - 18, y - 13, x, y - 22, 0xff8a6b38);
        DrawLine(frame, x, y - 22, x + 18, y - 13, 0xff8a6b38);
        DrawLine(frame, x - 6, y - 14, x - 12, y - 21, 0xffffd66b);
        DrawLine(frame, x + 6, y - 14, x + 12, y - 21, 0xffffd66b);
        DrawRtsProductionBar(frame, x, y + 17, rts.AnimalHallTimer, 150);
    }

    private void DrawRtsProp(uint[] frame, RtsState rts, RtsProp prop)
    {
        if (_sprites?.TryGet(prop.Sprite, out Sprite sprite) != true) return;
        int x = prop.X - rts.CameraX - sprite.Width / 2;
        int y = prop.GroundPatch ? prop.Y - sprite.Height / 2 : prop.Y - sprite.Height;
        if (x + sprite.Width < 0 || x >= _width) return;
        DrawSprite(frame, sprite, x, y);
    }

    private void DrawRtsProductionBar(uint[] frame, int x, int y, int timer, int total)
    {
        if (timer <= 0)
        {
            return;
        }
        DrawRect(frame, x - 15, y, 31, 4, 0xff101820);
        DrawRect(frame, x - 14, y + 1, (total - timer) * 29 / total, 2, 0xffffd66b);
    }

    private void DrawRtsUnit(uint[] frame, RtsState rts, RtsUnit unit)
    {
        int x = (int)Math.Round(unit.X) - rts.CameraX;
        int y = (int)Math.Round(unit.Y);
        if (rts.EvacuationAge > 0)
        {
            double rampDistance = Math.Sqrt(DistanceSquared(unit.X, unit.Y, rts.LandingX, rts.LandingY));
            if (rampDistance < 42) y -= (int)Math.Round(Math.Sin((42 - rampDistance) / 42 * Math.PI) * 9);
        }
        if (unit.Selected)
        {
            DrawCircleOutline(frame, x, y + 3, unit.Type == RtsUnitType.MooseCarolean ? 14 : 11, 0xffffd66b);
        }
        string generatedName;
        if (unit.Type == RtsUnitType.CaroleanSquad)
        {
            generatedName = unit.Cooldown > 27 ? "rts_carolean_fire" :
                unit.Cooldown > 12 ? "rts_carolean_reload" : "rts_carolean_ready";
        }
        else
        {
            double movement = DistanceSquared(unit.X, unit.Y, unit.TargetX, unit.TargetY);
            generatedName = unit.Cooldown > 33 ? "rts_moose_fire" :
                movement > 20 ? "rts_moose_charge" : "rts_moose_ready";
        }
        if (_sprites?.TryGet(generatedName, out Sprite generated) == true)
        {
            bool moving = DistanceSquared(unit.X, unit.Y, unit.TargetX, unit.TargetY) > 1.0;
            int step = moving ? ((rts.Age + unit.AnimationPhase) / 5 & 1) : 0;
            int drawX = x - generated.Width / 2;
            int drawY = y - generated.Height / 2 - step;
            if (moving && unit.TargetX < unit.X)
            {
                DrawRtsWalkingSprite(frame, generated, drawX, drawY, true, rts.Age + unit.AnimationPhase);
            }
            else if (moving)
            {
                DrawRtsWalkingSprite(frame, generated, drawX, drawY, false, rts.Age + unit.AnimationPhase);
            }
            else
            {
                DrawSprite(frame, generated, drawX, drawY);
            }
            return;
        }
        if (unit.Type == RtsUnitType.CaroleanSquad)
        {
            for (int soldier = 0; soldier < 4; soldier++)
            {
                int sx = x + (soldier % 2) * 8 - 4;
                int sy = y + (soldier / 2) * 8 - 4;
                FillCircle(frame, sx, sy - 3, 2, 0xffd2a477);
                DrawRect(frame, sx - 2, sy - 1, 5, 6, 0xff244f91);
                DrawLine(frame, sx + 3, sy, sx + 7, sy - 5, 0xffb7c7d6);
                PutPixel(frame, sx, sy + 3, 0xffffd66b);
            }
            return;
        }
        uint moose = 0xff574334;
        FillCircle(frame, x, y + 3, 8, moose);
        DrawRect(frame, x - 3, y - 8, 7, 12, 0xff244f91);
        FillCircle(frame, x, y - 10, 4, moose);
        DrawLine(frame, x - 2, y - 12, x - 9, y - 18, 0xff8a6b38);
        DrawLine(frame, x + 2, y - 12, x + 9, y - 18, 0xff8a6b38);
        DrawLine(frame, x - 9, y - 18, x - 13, y - 15, 0xff8a6b38);
        DrawLine(frame, x + 9, y - 18, x + 13, y - 15, 0xff8a6b38);
        DrawRect(frame, x - 3, y - 4, 7, 8, 0xff244f91);
        PutPixel(frame, x, y - 5, 0xffffd66b);
    }

    private void DrawRtsMiner(uint[] frame, RtsState rts, RtsMiner miner)
    {
        string name = miner.Carrying
            ? ((rts.Age + miner.AnimationPhase) / 8 & 1) == 0 ? "rts_miner_loaded_a" : "rts_miner_loaded_b"
            : ((rts.Age + miner.AnimationPhase) / 8 & 1) == 0 ? "rts_miner_empty_a" : "rts_miner_empty_b";
        if (_sprites?.TryGet(name, out Sprite sprite) != true) return;
        int x = (int)Math.Round(miner.X) - rts.CameraX - sprite.Width / 2;
        int y = (int)Math.Round(miner.Y) - sprite.Height / 2;
        if (rts.EvacuationAge > 0)
        {
            double rampDistance = Math.Sqrt(DistanceSquared(miner.X, miner.Y, rts.LandingX, rts.LandingY));
            if (rampDistance < 38) y -= (int)Math.Round(Math.Sin((38 - rampDistance) / 38 * Math.PI) * 7);
        }
        if (miner.TargetX < miner.X) DrawSpriteFlippedX(frame, sprite, x, y);
        else DrawSprite(frame, sprite, x, y);
    }

    private void DrawRtsFortress(uint[] frame, RtsState rts, RtsFortress fortress)
    {
        int x = fortress.X - rts.CameraX;
        if (x < -120 || x > _width + 120) return;
        string name = fortress.Health <= 0 ? "rts_toldhus_wreck" :
            fortress.Health < 220 ? "rts_toldhus_damaged" :
            fortress.GateOpen ? "rts_toldhus_open" : "rts_toldhus_intact";
        if (_sprites?.TryGet(name, out Sprite sprite) == true)
        {
            DrawSprite(frame, sprite, x - sprite.Width / 2, fortress.Y - sprite.Height / 2);
        }
        if (fortress.Health > 0)
        {
            DrawRect(frame, x - 45, fortress.Y + 42, 91, 5, 0xff101318);
            DrawRect(frame, x - 44, fortress.Y + 43, Math.Max(0, fortress.Health) * 89 / 600, 3, 0xffa92b3f);
        }
    }

    private void DrawRtsEnemy(uint[] frame, RtsState rts, RtsEnemy enemy)
    {
        int x = (int)Math.Round(enemy.X) - rts.CameraX;
        int y = (int)Math.Round(enemy.Y);
        if (x < -18 || x > _width + 18)
        {
            return;
        }
        string generatedName = enemy.Type switch
        {
            RtsEnemyType.TollStormer => enemy.Cooldown > 28 ? "rts_toll_attack" : "rts_toll_ready",
            RtsEnemyType.LedgerPikeman => enemy.Cooldown > 28 ? "rts_pike_attack" : "rts_pike_ready",
            RtsEnemyType.CoinMastiff => enemy.Cooldown > 28 ? "rts_mastiff_attack" : "rts_mastiff_ready",
            RtsEnemyType.PowderBoar => enemy.FuseAge > 0 ? "rts_boar_fuse" : "rts_boar_ready",
            RtsEnemyType.OrganWagon => enemy.Cooldown > 72 ? "rts_organ_fire" : "rts_organ_ready",
            _ => "rts_toll_ready",
        };
        if (_sprites?.TryGet(generatedName, out Sprite generated) == true)
        {
            bool attacking = enemy.Cooldown > 0 || enemy.FuseAge > 0;
            bool walking = enemy.Moving && !attacking;
            int step = walking ? ((rts.Age + enemy.AnimationPhase) / 5 & 1) : 0;
            int sway = walking ? ((rts.Age + enemy.AnimationPhase) / 10 % 3) - 1 : 0;
            int drawX = x - generated.Width / 2 + sway;
            int drawY = y - generated.Height / 2 - step;
            bool sourceAlreadyFacesLeft = enemy.Type is RtsEnemyType.TollStormer or RtsEnemyType.PowderBoar;
            if (walking)
            {
                DrawRtsWalkingSprite(frame, generated, drawX, drawY,
                    !sourceAlreadyFacesLeft, rts.Age + enemy.AnimationPhase);
            }
            else if (sourceAlreadyFacesLeft)
            {
                DrawSprite(frame, generated, drawX, drawY);
            }
            else
            {
                DrawSpriteFlippedX(frame, generated, drawX, drawY);
            }
            if (enemy.Type == RtsEnemyType.PowderBoar && enemy.FuseAge > 0)
            {
                uint fuse = (enemy.FuseAge / 4 & 1) == 0 ? 0xffffd66b : 0xffff6b4a;
                DrawCircleOutline(frame, x, y + 2, 12 + enemy.FuseAge / 7, fuse);
            }
            return;
        }
        if (enemy.Type == RtsEnemyType.TollStormer)
        {
            FillCircle(frame, x, y - 5, 3, 0xffd2a477);
            DrawRect(frame, x - 4, y - 2, 9, 10, 0xff8f2635);
            DrawRect(frame, x - 1, y - 2, 3, 10, 0xfff2eee4);
            DrawLine(frame, x + 4, y, x + 10, y - 6, 0xff8a6b38);
        }
        else if (enemy.Type == RtsEnemyType.LedgerPikeman)
        {
            DrawRect(frame, x - 5, y - 5, 11, 14, 0xff7f1727);
            DrawRect(frame, x - 1, y - 5, 3, 14, 0xfff2eee4);
            DrawLine(frame, x + 5, y + 7, x - 7, y - 17, 0xffc5b27a);
            FillCircle(frame, x, y - 8, 3, 0xffd2a477);
        }
        else if (enemy.Type == RtsEnemyType.CoinMastiff)
        {
            FillCircle(frame, x, y + 1, 7, 0xff4a292c);
            DrawRect(frame, x - 7, y - 2, 15, 5, 0xff8f2635);
            DrawRect(frame, x - 1, y - 5, 3, 11, 0xfff2eee4);
            PutPixel(frame, x - 4, y - 2, 0xffff6b62);
            PutPixel(frame, x + 4, y - 2, 0xffff6b62);
        }
        else if (enemy.Type == RtsEnemyType.PowderBoar)
        {
            FillCircle(frame, x, y + 2, 8, 0xff50352d);
            DrawRect(frame, x - 7, y - 4, 15, 9, 0xff8f2635);
            DrawRect(frame, x - 2, y - 5, 4, 11, 0xfff2eee4);
            DrawLine(frame, x + 5, y - 5, x + 9, y - 12, 0xff8a6b38);
            uint fuse = (enemy.FuseAge / 5 & 1) == 0 ? 0xffffd66b : 0xffff6b4a;
            PutPixel(frame, x + 9, y - 13, fuse);
            if (enemy.FuseAge > 0)
            {
                DrawCircleOutline(frame, x, y + 2, 10 + enemy.FuseAge / 6, fuse);
            }
        }
        else
        {
            DrawRect(frame, x - 13, y - 9, 27, 19, 0xff34383a);
            DrawRect(frame, x - 10, y - 6, 21, 13, 0xff8f2635);
            DrawRect(frame, x - 2, y - 8, 5, 17, 0xfff2eee4);
            for (int barrel = -1; barrel <= 1; barrel++)
            {
                DrawLine(frame, x + barrel * 6, y - 7, x + barrel * 6, y - 17, 0xff8a6b38);
            }
            FillCircle(frame, x - 9, y + 10, 4, 0xff202527);
            FillCircle(frame, x + 9, y + 10, 4, 0xff202527);
        }
        if (enemy.Health < 20)
        {
            PutPixel(frame, x, y + 10, 0xffff8a4a);
        }
    }

    private void DrawRtsLandedKarl(uint[] frame, RtsState rts)
    {
        int x = rts.LandingX - rts.CameraX;
        double t = Math.Min(1.0, rts.LandingAge / 120.0);
        double eased = t * t * (3.0 - 2.0 * t);
        int lift = rts.EvacuationAge >= 350 ? Math.Min(24, (rts.EvacuationAge - 350) / 5) : 0;
        if (rts.EvacuationAge >= 560) lift = 24;
        int vibration = rts.EvacuationAge is >= 350 and < 560 ? ((rts.EvacuationAge / 3 & 1) == 0 ? -1 : 1) : 0;
        int y = (int)Math.Round(-62 + (rts.LandingY + 62) * eased) - lift + vibration;
        if (rts.LandingAge >= 105)
        {
            if (_sprites?.TryGet("rts_karl_landing_pad", out Sprite pad) == true)
            {
                DrawSprite(frame, pad, x - pad.Width / 2, rts.LandingY - pad.Height / 2);
            }
            else
            {
                DrawRect(frame, x - 32, rts.LandingY - 26, 64, 52, 0xff171d1b);
            }
        }
        if (_sprites?.TryGet("player", out Sprite player) == true)
        {
            DrawSprite(frame, player, x - player.Width / 2, y - player.Height / 2);
        }
        else
        {
            FillTriangle(frame, x, y - 25, x - 20, y + 23, x + 20, y + 23, 0xff244f91);
            DrawLine(frame, x - 18, y + 18, x + 18, y + 18, 0xffffd66b);
        }
        if (rts.LandingAge is >= 90 and < 150)
        {
            int dust = (rts.LandingAge - 90) / 3;
            DrawCircleOutline(frame, x, rts.LandingY + 20, 8 + dust, 0xff6f7775);
        }
        if (rts.EvacuationAge is >= 350 and < 650)
        {
            int flame = 7 + (rts.EvacuationAge / 3 & 3);
            FillTriangle(frame, x - 8, y + 22, x - 4, y + 22, x - 6, y + 22 + flame, 0xffffd66b);
            FillTriangle(frame, x + 4, y + 22, x + 8, y + 22, x + 6, y + 22 + flame, 0xffff8a4a);
            DrawCircleOutline(frame, x, rts.LandingY + 20, 18 + rts.EvacuationAge % 18, 0xff6f7775);
        }
    }

    private void DrawRtsTempleCrack(uint[] frame, RtsState rts, int worldX, int worldY)
    {
        int x = worldX - rts.CameraX;
        int growth = Math.Min(34, (rts.EvacuationAge - 690) / 4);
        DrawLine(frame, x, worldY, x - growth / 3, worldY + growth / 3, 0xff030705);
        DrawLine(frame, x - growth / 3, worldY + growth / 3, x + growth / 4, worldY + growth, 0xff030705);
        DrawLine(frame, x - 2, worldY + 5, x + growth / 2, worldY + growth / 2, 0xff172823);
        uint glint = (rts.EvacuationAge / 7 & 1) == 0 ? 0xff65c58a : 0xff9bd4dc;
        PutPixel(frame, x + 2, worldY + growth - 2, glint);
        PutPixel(frame, x - 3, worldY + growth - 5, glint);
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
        else if (_levelId == 3)
        {
            DrawRtsVictorySilver(frame, panelX + 196, 67);
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
            else if (_levelId == 3)
            {
                DrawText(frame, panelX + 42, 99, "UNDER SILVERÅDERN", 0xffffd66b);
                DrawText(frame, panelX + 34, 116, "NEDSTIGNING VÄNTAR", 0xff65c58a);
            }
            else
            {
                DrawText(frame, panelX + 51, 99, "BÄLTET ÄR ÖPPET", 0xffffd66b);
                DrawText(frame, panelX + 39, 116, "KRONARKIV SÄKRAT", 0xff9bd4dc);
            }
        }
    }

    private void DrawRtsVictorySilver(uint[] frame, int x, int y)
    {
        if (_sprites?.TryGet("rts_vein_node", out Sprite silver) == true)
        {
            DrawSprite(frame, silver, x - silver.Width / 2, y - silver.Height / 2);
        }
        int pulse = (_stageClearAge / 5) & 1;
        uint glint = pulse == 0 ? 0xffffffff : 0xff9bd4dc;
        DrawLine(frame, x - 18, y, x - 10, y, glint);
        DrawLine(frame, x + 10, y, x + 18, y, glint);
        DrawLine(frame, x, y - 20, x, y - 13, glint);
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

    private void DrawSilverkroppenSelect(uint[] frame)
    {
        int panelWidth = Math.Min(340, _width - 24);
        int panelX = (_width - panelWidth) / 2;
        int panelTop = _height <= 224 ? 28 : 50;
        int rowHeight = _height <= 224 ? 27 : 33;
        int panelBottom = panelTop + 28 + rowHeight * 6;
        DrawRect(frame, panelX, panelTop, panelWidth, panelBottom - panelTop, 0xf2080d12);
        DrawLine(frame, panelX, panelTop, panelX + panelWidth - 1, panelTop, 0xffffd66b);
        DrawLine(frame, panelX, panelBottom - 1, panelX + panelWidth - 1, panelBottom - 1, 0xff2f74c9);
        DrawText(frame, panelX + 91, panelTop + 9, "SILVERKROPPEN", 0xffffd66b);
        string[] titles =
        [
            "FÄLTSLAGET  SILVERKROPPEN",
            "GRUVA I  FORTSÄTT",
            "GRUVA I  BÖRJA OM",
            "GRUVA II  DJUPGRUVAN",
            "GRUVA III  FÖRBANNADE GRUVAN",
            "LEMMINKÄINENS TEMPEL",
        ];
        for (int index = 0; index < titles.Length; index++)
        {
            bool available = index is 0 or 1 or 2;
            string status = index == 0 ? "RTS" : index == 1
                ? File.Exists(DungeonSavePath("autosave")) ? "FORTSÄTT" : "NY"
                : index == 2 ? "NYTT" : "LÅST";
            DrawLevelOption(frame, panelX + 12, panelTop + 25 + index * rowHeight, panelWidth - 24,
                rowHeight - 3, index, titles[index], status, _silverkroppenSelection == index);
            if (!available && _silverkroppenSelection == index)
            {
                DrawText(frame, panelX + panelWidth - 50, panelTop + 34 + index * rowHeight, "LÅST", 0xff697680);
            }
        }
        string footer = _lockedLevelNoticeFrames > 0 ? "DJUPET HAR INTE ÖPPNATS ÄN" : "START VÄLJ  ELD TILLBAKA";
        DrawText(frame, (_width - footer.Length * 6) / 2, Math.Min(_height - 9, panelBottom + 5), footer,
            _lockedLevelNoticeFrames > 0 ? 0xffff6b62 : 0xffb7c7d6);
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
            string status = index == 3 && File.Exists(DungeonSavePath("autosave")) ? "FORTSÄTT" :
                index == 0 ? "STRID" : _developerMode ? "DEV" : "LÅST";
            DrawLevelOption(frame, panelX + 12, listY + index * rowHeight, panelWidth - 24,
                rowHeight - 2, index, $"{index + 1}  {CampaignNames[index]}", status);
        }

        string footer = _lockedLevelNoticeFrames > 0 ? "FÄLTTÅGET ÄR LÅST" :
            _levelSelection == 3 && File.Exists(DungeonSavePath("autosave")) ? "START FORTSÄTT  SLOW+START NYTT" :
            _developerMode ? "UTVECKLARLÄGE  ALLT UPPLÅST" : "UPP NER VÄLJ  START";
        DrawText(frame, (_width - footer.Length * 6) / 2, panelBottom - 9, footer,
            _lockedLevelNoticeFrames > 0 ? 0xff65c58a : 0xffb7c7d6);
    }

    private void DrawLevelOption(uint[] frame, int x, int y, int width, int height, int index, string title, string status,
        bool? forceSelected = null)
    {
        bool selected = forceSelected ?? _levelSelection == index;
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
        int height = _dungeon is null ? 42 : 92;
        int x = (_width - width) / 2;
        int y = (_height - height) / 2;
        DrawRect(frame, x, y, width, height, 0xee080d12);
        DrawLine(frame, x, y, x + width - 1, y, 0xffffd66b);
        DrawLine(frame, x, y + height - 1, x + width - 1, y + height - 1, 0xff2f74c9);
        DrawText(frame, x + 73, y + 9, "PAUS", 0xffffd66b);
        if (_dungeon is null)
        {
            DrawText(frame, x + 34, y + 25, "START FORTSÄTTER", 0xffdce8f2);
        }
        else
        {
            DrawText(frame, x + 28, y + 25, "AUTOSAVE  GRUVA I", 0xff65c58a);
            DrawText(frame, x + 28, y + 39, "LÄGE 1   TOMT", 0xff9bd4dc);
            DrawText(frame, x + 28, y + 52, "LÄGE 2   TOMT", 0xff9bd4dc);
            DrawText(frame, x + 28, y + 65, "LÄGE 3   TOMT", 0xff9bd4dc);
            DrawText(frame, x + 28, y + 78, "START FORTSÄTTER", 0xffdce8f2);
        }
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

    private void DrawRtsWalkingSprite(uint[] frame, Sprite sprite, int x, int y, bool flipped, int phase)
    {
        int stride = (phase / 5 & 1) == 0 ? -1 : 1;
        int legTop = sprite.Height * 2 / 3;
        for (int sy = 0; sy < sprite.Height; sy++)
        {
            int py = y + sy;
            if ((uint)py >= _height) continue;
            for (int sx = 0; sx < sprite.Width; sx++)
            {
                int sourceX = flipped ? sprite.Width - 1 - sx : sx;
                uint src = sprite.Pixels[sy * sprite.Width + sourceX];
                uint alpha = src >> 24;
                if (alpha == 0) continue;
                int footShift = sy >= legTop
                    ? (sx < sprite.Width / 2 ? stride : -stride)
                    : 0;
                int px = x + sx + footShift;
                if ((uint)px >= _width) continue;
                BlendPixel(frame, px, py, src, alpha);
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
        'Q' => [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
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

    private enum RtsBuildingType
    {
        SteamPlant,
        SilverCrusher,
        Barracks,
        AnimalHall,
        DefenseTower,
    }

    private enum RtsUnitType
    {
        CaroleanSquad,
        MooseCarolean,
    }

    private enum RtsEnemyType
    {
        TollStormer,
        LedgerPikeman,
        CoinMastiff,
        PowderBoar,
        OrganWagon,
    }

    private enum DungeonFacing
    {
        South,
        North,
        East,
        West,
    }

    private enum DungeonEquipmentSlot
    {
        None,
        Head,
        Chest,
        Gloves,
        Boots,
        Belt,
        Amulet,
        RingLeft,
        RingRight,
        MainHand,
        OffHand,
        Relic,
    }

    private enum DungeonItemRarity { Iron, Carolean, Silverbound, Runic, Unique }

    private enum DungeonEnemyType { Stormer, Pikeman, SilverFogde, UndeadMiner, TuonelaGuard, BlindShepherd, LemminkainenShadow }
    private enum DungeonEnemyState { Approach, Telegraph, Attack, Recover, Stagger, Dead }

    private readonly record struct DungeonItemDefinition(string Key, string Name, string Sprite, int Width, int Height,
        DungeonEquipmentSlot Slot, int Damage, int Armor, int CurseWard, int RequiredLevel);

    private sealed class DungeonItem
    {
        public ulong Id { get; set; }
        public int Definition { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public DungeonEquipmentSlot Equipped { get; set; }
        public DungeonItemRarity Rarity { get; set; }
        public int PowerRoll { get; set; }
        public bool OnGround { get; set; }
        public bool InStash { get; set; }
        public int QuickSlot { get; set; } = -1;
        public double WorldX { get; set; }
        public double WorldY { get; set; }
    }

    private sealed class DungeonEnemy
    {
        public DungeonEnemy(double x, double y, DungeonEnemyType type, int health)
        {
            X = x;
            Y = y;
            Type = type;
            Health = health;
            MaxHealth = health;
        }

        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public DungeonEnemyType Type { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public DungeonEnemyState State { get; set; }
        public int StateAge { get; set; }
        public int LastHitSerial { get; set; }
        public bool FacingLeft { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double GaitDistance { get; set; }
        public bool LootDropped { get; set; }
        public int Zone { get; set; }
        public bool ExperienceAwarded { get; set; }
        public int Phase { get; set; } = 1;
        public int SpecialKind { get; set; }
        public int SpecialAge { get; set; }
        public int SpecialCooldown { get; set; }
        public int SpecialSerial { get; set; }
        public int Depth { get; set; }
    }

    private sealed class DungeonSilverWave(double x, double y, double dx, double dy, int phase)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Dx { get; } = dx;
        public double Dy { get; } = dy;
        public int Phase { get; } = phase;
        public int Age { get; set; }
        public bool HitKarl { get; set; }
    }

    private sealed class DungeonChest(int id, double x, double y)
    {
        public int Id { get; } = id;
        public double X { get; } = x;
        public double Y { get; } = y;
        public bool Open { get; set; }
        public int OpenAge { get; set; }
        public int Depth { get; set; }
    }

    private sealed class DungeonState
    {
        public int Age { get; set; }
        public int RoomWidth { get; set; }
        public int RoomHeight { get; set; }
        public int CameraX { get; set; }
        public int CameraY { get; set; }
        public double KarlX { get; set; }
        public double KarlY { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public DungeonFacing Facing { get; set; }
        public bool Moving { get; set; }
        public double GaitDistance { get; set; }
        public uint PreviousMouseButtons { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Power { get; set; } = 35;
        public List<DungeonItem> Items { get; } = [];
        public ulong NextItemId { get; set; } = 1;
        public bool InventoryOpen { get; set; }
        public int InventoryCursorX { get; set; }
        public int InventoryCursorY { get; set; }
        public ulong SelectedItemId { get; set; }
        public ulong InspectedItemId { get; set; }
        public uint PreviousInventoryMouseButtons { get; set; }
        public bool ViewingStash { get; set; }
        public List<DungeonEnemy> Enemies { get; } = [];
        public int NextEnemyId { get; set; } = 1;
        public int AttackAge { get; set; }
        public int AttackSerial { get; set; }
        public int AttackCombo { get; set; }
        public bool AttackQueued { get; set; }
        public bool Guarding { get; set; }
        public int HitStop { get; set; }
        public int HurtAge { get; set; }
        public bool RoomClear { get; set; }
        public bool RoomClearSaved { get; set; }
        public bool DoorReady { get; set; }
        public ulong PendingPickupItemId { get; set; }
        public int PendingChestId { get; set; }
        public int Depth { get; set; } = 1;
        public int DoorHealth { get; set; } = 3;
        public List<DungeonChest> Chests { get; } = [];
        public bool DescentWarningPlayed { get; set; }
        public bool PendingDoorEntry { get; set; }
        public bool PendingReturnEntry { get; set; }
        public int MaxZoneReached { get; set; }
        public int DeathAge { get; set; }
        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int LevelPulse { get; set; }
        public bool DepthThreeOpen { get; set; }
        public int ForcedVentAge { get; set; }
        public List<DungeonSilverWave> SilverWaves { get; } = [];
        public bool PendingDepthThreeEntry { get; set; }
        public bool PendingDepthThreeReturn { get; set; }
        public int Curse { get; set; }
        public bool TempleOpen { get; set; }
        public bool PendingTempleEntry { get; set; }
        public bool PendingTempleReturn { get; set; }
        public int LoreMask { get; set; }
    }

    private readonly record struct DungeonSave(int Schema, int Age, int RoomWidth, int RoomHeight,
        double KarlX, double KarlY, double TargetX, double TargetY, DungeonFacing Facing,
        int Health, int MaxHealth, int Power, ulong NextItemId, List<DungeonItemSave>? Items,
        int NextEnemyId, int AttackSerial, int AttackCombo, bool RoomClear, bool DoorReady,
        int Depth, int DoorHealth, bool DescentWarningPlayed, int MaxZoneReached, int Level, int Experience,
        bool DepthThreeOpen, int ForcedVentAge, int Curse, bool TempleOpen, int LoreMask,
        List<DungeonEnemySave>? Enemies,
        List<DungeonChestSave>? Chests);
    private readonly record struct DungeonItemSave(ulong Id, int Definition, int GridX, int GridY,
        DungeonEquipmentSlot Equipped, DungeonItemRarity Rarity, int PowerRoll, bool OnGround, bool InStash,
        double WorldX, double WorldY, int QuickSlot);
    private readonly record struct DungeonEnemySave(int Id, double X, double Y, DungeonEnemyType Type,
        int Health, int MaxHealth, DungeonEnemyState State, int StateAge, int LastHitSerial, bool FacingLeft,
        bool LootDropped, int Zone, bool ExperienceAwarded, int Phase, int SpecialKind, int SpecialAge,
        int SpecialCooldown, int SpecialSerial, int Depth);
    private readonly record struct DungeonChestSave(int Id, double X, double Y, bool Open, int OpenAge, int Depth);

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

    private sealed class RtsState
    {
        public int Age { get; set; }
        public int MapWidth { get; set; }
        public int MapHeight { get; set; }
        public int CameraX { get; set; }
        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public int LandingX { get; set; }
        public int LandingY { get; set; }
        public int LandingAge { get; set; }
        public int BuildStage { get; set; }
        public int Silver { get; set; } = 600;
        public int SilverPulse { get; set; }
        public List<RtsBuilding> Buildings { get; } = [];
        public List<RtsUnit> Units { get; } = [];
        public List<RtsEnemy> Enemies { get; } = [];
        public List<RtsMiner> Miners { get; } = [];
        public List<RtsProp> Props { get; } = [];
        public int BarracksTimer { get; set; }
        public int AnimalHallTimer { get; set; }
        public bool LastPlacementValid { get; set; }
        public int PlacementPulse { get; set; }
        public int CombatAge { get; set; }
        public int KarlHealth { get; set; } = 500;
        public bool TowerPlacementMode { get; set; }
        public bool CombatStarted { get; set; }
        public int MouseWorldX { get; set; }
        public int MouseWorldY { get; set; }
        public int MouseDragStartX { get; set; }
        public int MouseDragStartY { get; set; }
        public bool MouseSelecting { get; set; }
        public uint PreviousMouseButtons { get; set; }
        public bool MousePrimaryPressed { get; set; }
        public int SalvagedSilver { get; set; }
        public bool SilverGoalReached { get; set; }
        public int EvacuationAge { get; set; }
        public RtsFortress? Fortress { get; set; }
    }

    private sealed class RtsBuilding
    {
        public RtsBuilding(int x, int y, RtsBuildingType type)
        {
            X = x;
            Y = y;
            Type = type;
            Health = type switch
            {
                RtsBuildingType.SteamPlant => 380,
                RtsBuildingType.DefenseTower => 180,
                _ => 300,
            };
        }

        public int X { get; }
        public int Y { get; }
        public RtsBuildingType Type { get; }
        public int Health { get; set; }
        public int Cooldown { get; set; }
    }

    private sealed class RtsEnemy
    {
        public RtsEnemy(double x, double y, RtsEnemyType type, int health)
        {
            X = x;
            Y = y;
            Type = type;
            Health = health;
            AnimationPhase = ((int)Math.Round(x * 3 + y * 5) + (int)type * 17) & 63;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public RtsEnemyType Type { get; }
        public int Health { get; set; }
        public int Cooldown { get; set; }
        public int FuseAge { get; set; }
        public int AnimationPhase { get; }
        public bool Moving { get; set; } = true;
    }

    private sealed class RtsMiner(double x, double y, int animationPhase)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double TargetX { get; set; } = x;
        public double TargetY { get; set; } = y;
        public bool Carrying { get; set; } = true;
        public bool Moving { get; set; }
        public int LoadAge { get; set; }
        public int AnimationPhase { get; } = animationPhase;
    }

    private sealed record RtsProp(int X, int Y, string Sprite, bool GroundPatch, int BlockRadius);

    private sealed class RtsFortress(int x, int y)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public int Health { get; set; } = 600;
        public int LeftSealHealth { get; set; } = 120;
        public int RightSealHealth { get; set; } = 120;
        public bool GateOpen { get; set; }
    }

    private sealed class RtsUnit
    {
        public RtsUnit(double x, double y, RtsUnitType type)
        {
            X = x;
            Y = y;
            TargetX = x;
            TargetY = y;
            Type = type;
            Health = type == RtsUnitType.MooseCarolean ? 160 : 100;
            AnimationPhase = ((int)Math.Round(x * 7 + y * 3) + (int)type * 19) & 63;
        }

        public double X { get; set; }
        public double Y { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public RtsUnitType Type { get; }
        public bool Selected { get; set; }
        public int Health { get; set; }
        public int Cooldown { get; set; }
        public int AnimationPhase { get; }
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
