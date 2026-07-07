using SystemRegisIII.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class FakeSaturnCore : ISystemCore
{
    public const int FrameWidth = 320;
    public const int FrameHeight = 224;

    private readonly uint[] _frame = new uint[FrameWidth * FrameHeight];
    private ulong _frameIndex;
    private int _blobX = FrameWidth / 2;
    private int _blobY = FrameHeight / 2;

    public void Reset()
    {
        _frameIndex = 0;
        _blobX = FrameWidth / 2;
        _blobY = FrameHeight / 2;
        Array.Clear(_frame);
    }

    public void StepFrame(IInputSource input, IFrameSink frameSink)
    {
        SaturnInputState inputState = input.Poll();
        RenderTestFrame(inputState);
        frameSink.Present(_frame, FrameWidth, FrameHeight, FrameWidth);
        _frameIndex++;
    }

    private void RenderTestFrame(SaturnInputState inputState)
    {
        bool boost = inputState.Buttons != SaturnButtons.None;
        MoveBlob(inputState.Buttons);

        for (int y = 0; y < FrameHeight; y++)
        {
            int row = y * FrameWidth;
            for (int x = 0; x < FrameWidth; x++)
            {
                uint r = (uint)(((ulong)x + _frameIndex * 2) & 0xff);
                uint g = (uint)((y * 255) / (FrameHeight - 1));
                uint checker = (uint)(((x / 12) ^ (y / 10) ^ (int)(_frameIndex / 8)) & 1);
                uint b = checker == 0 ? 0x36u : 0xc8u;

                if (boost)
                {
                    r = Math.Min(255u, r + 36);
                    b = Math.Min(255u, b + 28);
                }

                _frame[row + x] = 0xff000000u | (r << 16) | (g << 8) | b;
            }
        }

        DrawBlob(inputState.Buttons);
    }

    private void MoveBlob(SaturnButtons buttons)
    {
        int speed = buttons.HasFlag(SaturnButtons.Start) ? 5 : 2;

        if (buttons.HasFlag(SaturnButtons.Left))
        {
            _blobX -= speed;
        }
        if (buttons.HasFlag(SaturnButtons.Right))
        {
            _blobX += speed;
        }
        if (buttons.HasFlag(SaturnButtons.Up))
        {
            _blobY -= speed;
        }
        if (buttons.HasFlag(SaturnButtons.Down))
        {
            _blobY += speed;
        }

        _blobX = Math.Clamp(_blobX, 12, FrameWidth - 13);
        _blobY = Math.Clamp(_blobY, 12, FrameHeight - 13);
    }

    private void DrawBlob(SaturnButtons buttons)
    {
        int radius = buttons.HasFlag(SaturnButtons.C) ? 18 : 12;
        if (buttons.HasFlag(SaturnButtons.Z))
        {
            radius = 8;
        }

        uint body = 0xffffc857;
        if (buttons.HasFlag(SaturnButtons.A))
        {
            body = 0xff58d68d;
        }
        if (buttons.HasFlag(SaturnButtons.B))
        {
            body = 0xffff5c8a;
        }
        if (buttons.HasFlag(SaturnButtons.X))
        {
            body = 0xff6ab0ff;
        }
        if (buttons.HasFlag(SaturnButtons.Y))
        {
            body = 0xffd58cff;
        }

        uint ring = buttons.HasFlag(SaturnButtons.Start) ? 0xffffffff : 0xff111318;

        FillCircle(_blobX, _blobY, radius + 3, ring);
        FillCircle(_blobX, _blobY, radius, body);

        if (buttons.HasFlag(SaturnButtons.X) || buttons.HasFlag(SaturnButtons.Y) || buttons.HasFlag(SaturnButtons.Z))
        {
            FillRect(_blobX - radius, _blobY - 1, radius * 2 + 1, 3, 0xff111318);
            FillRect(_blobX - 1, _blobY - radius, 3, radius * 2 + 1, 0xff111318);
        }

        FillRect(_blobX - 4, _blobY - 4, 3, 3, 0xffffffff);
        FillRect(_blobX + 2, _blobY - 4, 3, 3, 0xffffffff);
    }

    private void FillCircle(int centerX, int centerY, int radius, uint color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= r2)
                {
                    PutPixel(centerX + x, centerY + y, color);
                }
            }
        }
    }

    private void FillRect(int x, int y, int width, int height, uint color)
    {
        for (int py = Math.Max(0, y); py < Math.Min(FrameHeight, y + height); py++)
        {
            int row = py * FrameWidth;
            for (int px = Math.Max(0, x); px < Math.Min(FrameWidth, x + width); px++)
            {
                _frame[row + px] = color;
            }
        }
    }

    private void PutPixel(int x, int y, uint color)
    {
        if ((uint)x >= FrameWidth || (uint)y >= FrameHeight)
        {
            return;
        }

        _frame[(y * FrameWidth) + x] = color;
    }
}
