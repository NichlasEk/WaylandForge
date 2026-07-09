using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices;

const int Width = 320;
const int Height = 224;
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
    private readonly SpritePack? _sprites;
    private readonly StormaktMusicLoop? _audio;
    private readonly Random _random = new(3020);
    private readonly List<Shot> _shots = [];
    private readonly List<Enemy> _enemies = [];
    private readonly Star[] _stars = new Star[92];
    private int _shipX;
    private int _shipY;
    private int _cooldown;
    private int _altCooldown;
    private int _spawnTimer;
    private int _score;
    private int _lives = 3;
    private int _heat;
    private uint _previousButtons;
    private bool _gameOver;

    public StormaktGame(int width, int height, SpritePack? sprites, StormaktMusicLoop? audio)
    {
        _width = width;
        _height = height;
        _sprites = sprites;
        _audio = audio;
        Reset();
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i] = new Star(_random.Next(width), _random.Next(height), 1 + _random.Next(3), _random.Next(50, 180));
        }
    }

    public void Step(uint buttons)
    {
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

        int speed = (buttons & Slow) != 0 ? 2 : 4;
        if ((buttons & Left) != 0) _shipX -= speed;
        if ((buttons & Right) != 0) _shipX += speed;
        if ((buttons & Up) != 0) _shipY -= speed;
        if ((buttons & Down) != 0) _shipY += speed;
        _shipX = Math.Clamp(_shipX, 14, _width - 14);
        _shipY = Math.Clamp(_shipY, 28, _height - 18);

        _cooldown = Math.Max(0, _cooldown - 1);
        _altCooldown = Math.Max(0, _altCooldown - 1);
        _heat = Math.Max(0, _heat - 1);
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
        StepEnemies();
        SpawnEnemies();
        StepStars();
        _previousButtons = buttons;
    }

    public void Render(uint[] frame, ulong frameIndex)
    {
        Clear(frame, 0xff061018);
        DrawSky(frame, frameIndex);
        DrawStars(frame);
        DrawBorder(frame);
        DrawShots(frame);
        DrawEnemies(frame);
        DrawShip(frame);
        DrawHud(frame);
        if (_gameOver)
        {
            DrawRect(frame, 76, 92, 168, 42, 0xdd090b10);
            DrawText(frame, 92, 102, "FLOTTAN FÖLL", 0xffff6b7f);
            DrawText(frame, 98, 118, "START ÅTERKALLAR", 0xffffd66b);
        }
    }

    private void Reset()
    {
        _shots.Clear();
        _enemies.Clear();
        _shipX = _width / 2;
        _shipY = _height - 36;
        _cooldown = 0;
        _altCooldown = 0;
        _spawnTimer = 20;
        _score = 0;
        _lives = 3;
        _heat = 0;
        _previousButtons = 0;
        _gameOver = false;
    }

    private bool Pressed(uint buttons, uint button) => (buttons & button) != 0 && (_previousButtons & button) == 0;

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

    private void StepEnemies()
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _enemies[i];
            enemy.Y += enemy.Speed;
            enemy.Phase += 0.08;
            int wobble = (int)(Math.Sin(enemy.Phase) * 2.0);
            enemy.X += wobble;

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
                _lives--;
                _heat = 120;
                _audio?.Trigger(StormaktSound.HullHit);
                if (_lives <= 0)
                {
                    _gameOver = true;
                }
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

    private void SpawnEnemies()
    {
        _spawnTimer--;
        if (_spawnTimer > 0)
        {
            return;
        }

        int radius = _random.Next(7, 13);
        int x = _random.Next(24, _width - 24);
        int speed = _random.Next(1, 4);
        int health = radius < 10 ? 4 : 7;
        int kind = _random.Next(3);
        uint color = kind switch
        {
            0 => 0xffa71930,
            1 => 0xffc51f35,
            _ => 0xff7f1727,
        };
        _enemies.Add(new Enemy(x, -radius, speed, radius, health, color, kind, _random.NextDouble() * Math.PI * 2.0));
        _spawnTimer = Math.Max(12, 34 - _score / 450);
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

    private void DrawSky(uint[] frame, ulong frameIndex)
    {
        for (int y = 0; y < _height; y++)
        {
            int row = y * _width;
            uint r = (uint)(4 + y / 18);
            uint g = (uint)(13 + y / 8);
            uint b = (uint)(24 + y / 3);
            for (int x = 0; x < _width; x++)
            {
                uint fog = (uint)((Math.Sin((x + (double)frameIndex * 2.0) * 0.025 + y * 0.04) + 1.0) * 8.0);
                frame[row + x] = 0xff000000u | ((r + fog) << 16) | ((g + fog) << 8) | Math.Min(255u, b + fog);
            }
        }
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
        if (_sprites?.TryGet("player", out Sprite player) == true)
        {
            DrawSprite(frame, player, _shipX - (player.Width / 2), _shipY - (player.Height / 2));
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
        if (_sprites is not null)
        {
            string spriteName = enemy.Kind switch
            {
                1 => "enemy_crown",
                2 => "enemy_caroline",
                _ => "enemy_guard",
            };
            if (_sprites.TryGet(spriteName, out Sprite sprite))
            {
                DrawSprite(frame, sprite, enemy.X - (sprite.Width / 2), enemy.Y - (sprite.Height / 2));
                return;
            }
        }

        if (enemy.Kind == 1)
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

    private void DrawHud(uint[] frame)
    {
        DrawText(frame, 6, 5, "KARL CCLV", 0xffffd66b);
        DrawText(frame, 76, 5, "STORMAKT 3020", 0xff7fc7ff);
        DrawText(frame, 204, 5, "POÄNG " + _score.ToString("000000"), 0xff7fc7ff);
        DrawText(frame, 6, _height - 9, "LIV " + _lives, 0xffff6b7f);
        DrawText(frame, 62, _height - 9, "Z ELD  X BREDSIDA", 0xffb7c7d6);
        DrawRect(frame, 268, _height - 8, 44, 4, 0xff2a3440);
        DrawRect(frame, 268, _height - 8, Math.Clamp(_heat, 0, 120) * 44 / 120, 4, _heat > 80 ? 0xffff6b4a : 0xffffd66b);
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

    private record struct Shot(int X, int Y, int Vx, int Vy, uint Color, int Power);
    private record struct Enemy(int X, int Y, int Speed, int Radius, int Health, uint Color, int Kind, double Phase);
    private readonly record struct Star(int X, int Y, int Speed, int Brightness);
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
