extern alias SaturnEmulator;

using HostCore = SystemRegisIII.Core;
using SaturnBus = SaturnEmulator::SystemRegisIII.Core.Core.Bus;
using SaturnCd = SaturnEmulator::SystemRegisIII.Core.Core.CdBlock;
using SaturnCpu = SaturnEmulator::SystemRegisIII.Core.Core.Cpu.Sh2;
using SaturnInput = SaturnEmulator::SystemRegisIII.Core.Host.Input;
using SaturnMemory = SaturnEmulator::SystemRegisIII.Core.Core.Memory;
using SaturnScu = SaturnEmulator::SystemRegisIII.Core.Core.Scu;
using SaturnSmpc = SaturnEmulator::SystemRegisIII.Core.Core.Smpc;
using SaturnTrace = SaturnEmulator::SystemRegisIII.Core.Tools.TraceViewer;
using SaturnSystem = SaturnEmulator::SystemRegisIII.Core.Core;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class SaturnBringupCore : HostCore.ISystemCore
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 224;
    private const int InstructionsPerHostFrame = 2_000;
    private const int VBlankIntervalInstructions = 1_000_000;

    private readonly uint[] _frame = new uint[FrameWidth * FrameHeight];
    private readonly string[] _biosCandidates =
    [
        Environment.GetEnvironmentVariable("WAYLANDFORGE_SATURN_BIOS") ?? string.Empty,
        "/home/nichlas/WaylandForge/local/saturn/bios.bin",
        "/home/nichlas/SystemRegisIII/local/saturn_bios.bin",
        "/home/nichlas/SystemRegisIII/local/bios.bin",
        "/home/nichlas/SystemRegisIII/bios.bin",
    ];

    private SaturnRuntime? _runtime;
    private string? _biosPath;
    private string _fault = string.Empty;
    private ulong _frameIndex;
    private long _instructionIndex;
    private HostCore.SaturnButtons _lastButtons;

    public ulong FrameIndex => _frameIndex;

    public void Reset()
    {
        _runtime = null;
        _fault = string.Empty;
        _frameIndex = 0;
        _instructionIndex = 0;
        _lastButtons = HostCore.SaturnButtons.None;
    }

    public void StepFrame(HostCore.IInputSource input, HostCore.IFrameSink frameSink)
    {
        _lastButtons = input.Poll().Buttons;
        EnsureRuntime();

        if (_runtime is not null && string.IsNullOrEmpty(_fault))
        {
            StepRuntime();
        }

        RenderDiagnosticFrame();
        frameSink.Present(_frame, FrameWidth, FrameHeight, FrameWidth);
        _frameIndex++;
    }

    private void EnsureRuntime()
    {
        if (_runtime is not null || !string.IsNullOrEmpty(_fault))
        {
            return;
        }

        _biosPath = _biosCandidates.FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (_biosPath is null)
        {
            _fault = "BIOS MISSING";
            return;
        }

        try
        {
            byte[] biosBytes = File.ReadAllBytes(_biosPath);
            var bios = new SaturnMemory.BiosImage(Path.GetFileName(_biosPath), biosBytes);
            var trace = new SaturnTrace.RingTraceEventSink(2048);
            var systemMap = SaturnSystem.SaturnSystemMap.CreateBringup(
                bios,
                new SaturnSystem.SaturnBringupOptions
                {
                    SimulateSlaveReady = true,
                    DigitalPadState = MapInput(_lastButtons),
                });
            var masterInternalBus = new SaturnCpu.Sh2InternalRegisterBus(systemMap.Bus, SaturnCpu.Sh2CpuRole.Master);
            var slaveInternalBus = new SaturnCpu.Sh2InternalRegisterBus(systemMap.Bus, SaturnCpu.Sh2CpuRole.Slave);
            var master = new SaturnCpu.Sh2Cpu("Master SH-2", masterInternalBus, resetVectorAddress: 0x0000_0000, trace);
            var slave = new SaturnCpu.Sh2Cpu("Slave SH-2", slaveInternalBus, resetVectorAddress: 0x0000_0008, trace);
            master.Reset();
            slave.Reset();

            _runtime = new SaturnRuntime(
                bios.Name,
                systemMap,
                master,
                slave,
                systemMap.Stubs.OfType<SaturnSmpc.SmpcRegisterBusDevice>().Single(),
                systemMap.Stubs.OfType<SaturnScu.ScuRegisterBusDevice>().Single(),
                trace);
        }
        catch (Exception ex)
        {
            _fault = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void StepRuntime()
    {
        SaturnRuntime runtime = _runtime!;
        int vblankOutOffset = VBlankIntervalInstructions / 2;

        try
        {
            for (int i = 0; i < InstructionsPerHostFrame; i++)
            {
                while (runtime.Smpc.TryConsumeInterrupt())
                {
                    runtime.Scu.RaiseSmpc();
                }

                if (_instructionIndex > 0 && _instructionIndex % VBlankIntervalInstructions == 0)
                {
                    runtime.Scu.RaiseVBlankIn();
                }
                else if (_instructionIndex > 0 && _instructionIndex % VBlankIntervalInstructions == vblankOutOffset)
                {
                    runtime.Scu.RaiseVBlankOut();
                }

                DeliverPendingInterrupt(runtime);
                runtime.Master.StepInstruction();

                if (runtime.Smpc.SlaveSh2Enabled)
                {
                    if (!runtime.SlaveWasEnabled)
                    {
                        runtime.Slave.Reset();
                    }

                    runtime.Slave.StepInstruction();
                }

                runtime.SlaveWasEnabled = runtime.Smpc.SlaveSh2Enabled;
                _instructionIndex++;
            }
        }
        catch (Exception ex)
        {
            _fault = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static void DeliverPendingInterrupt(SaturnRuntime runtime)
    {
        if (runtime.Scu.HasPendingVBlankIn)
        {
            if (runtime.Master.RequestInterrupt(15, 0x40))
            {
                runtime.Scu.AcknowledgeVBlankIn();
            }
        }
        else if (runtime.Scu.HasPendingVBlankOut)
        {
            if (runtime.Master.RequestInterrupt(14, 0x41))
            {
                runtime.Scu.AcknowledgeVBlankOut();
            }
        }
        else if (runtime.Scu.HasPendingSmpc && runtime.Master.RequestInterrupt(8, 0x47))
        {
            runtime.Scu.AcknowledgeSmpc();
        }
    }

    private void RenderDiagnosticFrame()
    {
        FillBackground();
        int y = 14;
        DrawText(14, y, "SYSTEMREGISIII SATURN", 0xffffffff); y += 18;
        DrawText(14, y, "MAIN CORE BRINGUP", 0xff7cc7ff); y += 18;
        DrawText(14, y, $"FRAME {_frameIndex}", 0xff91a1ad); y += 18;

        if (_runtime is null)
        {
            DrawText(14, y, _fault, 0xffff5c8a); y += 18;
            DrawText(14, y, "SET WAYLANDFORGE_SATURN_BIOS", 0xffffd166); y += 18;
            DrawText(14, y, "OR PLACE LOCAL/SATURN/BIOS.BIN", 0xffffd166);
            return;
        }

        SaturnRuntime runtime = _runtime;
        DrawText(14, y, "BIOS " + runtime.BiosName.ToUpperInvariant(), 0xff91a1ad); y += 18;
        DrawText(14, y, $"MPC 0X{runtime.Master.Registers.ProgramCounter:X8}", 0xffffffff); y += 18;
        DrawText(14, y, $"MSR 0X{runtime.Master.Registers.StatusRegister:X8}", 0xff91a1ad); y += 18;
        DrawText(14, y, $"SPC 0X{runtime.Slave.Registers.ProgramCounter:X8}", 0xffffffff); y += 18;
        DrawText(14, y, $"SMPC CMD 0X{runtime.Smpc.LastCommand:X2}", 0xff91a1ad); y += 18;
        DrawText(14, y, $"SMPC IRQ {runtime.Smpc.PendingInterrupts}", 0xff91a1ad); y += 18;
        DrawText(14, y, $"INPUT {_lastButtons}", 0xff7cc7ff); y += 18;
        DrawText(14, y, $"INSTR {_instructionIndex}", 0xff91a1ad); y += 18;
        if (!string.IsNullOrEmpty(_fault))
        {
            DrawText(14, y, "FAULT " + _fault.ToUpperInvariant(), 0xffff5c8a);
        }
    }

    private void FillBackground()
    {
        for (int y = 0; y < FrameHeight; y++)
        {
            int row = y * FrameWidth;
            uint g = (uint)(18 + (y * 32 / FrameHeight));
            for (int x = 0; x < FrameWidth; x++)
            {
                uint pulse = (uint)(((x / 16) + (y / 14) + (int)(_frameIndex / 18)) & 1);
                uint b = 28 + pulse * 14;
                _frame[row + x] = 0xff000000u | (0x0fu << 16) | (g << 8) | b;
            }
        }

        int scan = (int)(_frameIndex % FrameHeight);
        for (int x = 0; x < FrameWidth; x++)
        {
            _frame[(scan * FrameWidth) + x] = 0xff29465cu;
        }
    }

    private void DrawText(int x, int y, string text, uint color)
    {
        int cursor = x;
        foreach (char raw in text)
        {
            char ch = char.ToUpperInvariant(raw);
            if (ch == ' ')
            {
                cursor += 6;
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
                        PutPixel(cursor + gx, y + gy, color);
                    }
                }
            }

            cursor += 6;
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

    private static SaturnInput.SaturnInputState MapInput(HostCore.SaturnButtons buttons)
    {
        SaturnInput.SaturnInputState state = SaturnInput.SaturnInputState.None;
        if (buttons.HasFlag(HostCore.SaturnButtons.Up)) state |= SaturnInput.SaturnInputState.Up;
        if (buttons.HasFlag(HostCore.SaturnButtons.Down)) state |= SaturnInput.SaturnInputState.Down;
        if (buttons.HasFlag(HostCore.SaturnButtons.Left)) state |= SaturnInput.SaturnInputState.Left;
        if (buttons.HasFlag(HostCore.SaturnButtons.Right)) state |= SaturnInput.SaturnInputState.Right;
        if (buttons.HasFlag(HostCore.SaturnButtons.Start)) state |= SaturnInput.SaturnInputState.Start;
        if (buttons.HasFlag(HostCore.SaturnButtons.A)) state |= SaturnInput.SaturnInputState.A;
        if (buttons.HasFlag(HostCore.SaturnButtons.B)) state |= SaturnInput.SaturnInputState.B;
        if (buttons.HasFlag(HostCore.SaturnButtons.C)) state |= SaturnInput.SaturnInputState.C;
        if (buttons.HasFlag(HostCore.SaturnButtons.X)) state |= SaturnInput.SaturnInputState.X;
        if (buttons.HasFlag(HostCore.SaturnButtons.Y)) state |= SaturnInput.SaturnInputState.Y;
        if (buttons.HasFlag(HostCore.SaturnButtons.Z)) state |= SaturnInput.SaturnInputState.Z;
        return state;
    }

    private static ReadOnlySpan<byte> Glyph(char ch) => ch switch
    {
        'A' => [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        'B' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        'C' => [0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111],
        'D' => [0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110],
        'E' => [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        'F' => [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        'G' => [0b01111, 0b10000, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111],
        'H' => [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        'I' => [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111],
        'L' => [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
        'M' => [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        'N' => [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
        'O' => [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        'P' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
        'R' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
        'S' => [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
        'T' => [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        'U' => [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        'V' => [0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b01010, 0b00100],
        'W' => [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010],
        'X' => [0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b01010, 0b10001],
        'Y' => [0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
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
        ':' => [0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b00000],
        '.' => [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100],
        '/' => [0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000],
        '-' => [0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000],
        '_' => [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b11111],
        _ => [0b11111, 0b00001, 0b00110, 0b00100, 0b00100, 0b00000, 0b00100],
    };

    private sealed record SaturnRuntime(
        string BiosName,
        SaturnSystem.SaturnSystemMap SystemMap,
        SaturnCpu.Sh2Cpu Master,
        SaturnCpu.Sh2Cpu Slave,
        SaturnSmpc.SmpcRegisterBusDevice Smpc,
        SaturnScu.ScuRegisterBusDevice Scu,
        SaturnTrace.RingTraceEventSink Trace)
    {
        public bool SlaveWasEnabled { get; set; }
    }
}
