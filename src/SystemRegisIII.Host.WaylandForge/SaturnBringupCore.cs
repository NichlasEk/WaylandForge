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
using SaturnVdp1 = SaturnEmulator::SystemRegisIII.Core.Core.Vdp1;
using SaturnVdp2 = SaturnEmulator::SystemRegisIII.Core.Core.Vdp2;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class SaturnBringupCore : HostCore.ISystemCore, IDisposable
{
    private const int FrameWidth = 320;
    private const int FrameHeight = 224;
    private const int InstructionsPerHostFrame = 10_000;
    private const int VBlankIntervalInstructions = 100_000;

    private readonly uint[] _frame = new uint[FrameWidth * FrameHeight];
    private readonly uint[] _vdp1Frame = new uint[FrameWidth * FrameHeight];
    private readonly uint[] _transparentRows = new uint[FrameHeight];
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
    private long _vblankInCount;
    private long _vblankOutCount;
    private long _smpcInterruptCount;
    private HostCore.SaturnButtons _lastButtons;
    private string? _discPath;
    private bool _hasVideoFrame;
    private bool _hasVdp1Frame;

    public ulong FrameIndex => _frameIndex;
    public SaturnCoreStatus Status => CreateStatus();

    public void LoadDisc(string? path)
    {
        string? normalized = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        if (string.Equals(_discPath, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _discPath = normalized;
        Reset();
    }

    public void Reset()
    {
        _runtime?.DiscImage?.Dispose();
        _runtime = null;
        _fault = string.Empty;
        _frameIndex = 0;
        _instructionIndex = 0;
        _vblankInCount = 0;
        _vblankOutCount = 0;
        _smpcInterruptCount = 0;
        _lastButtons = HostCore.SaturnButtons.None;
        _hasVideoFrame = false;
        _hasVdp1Frame = false;
        Array.Clear(_frame);
        Array.Clear(_vdp1Frame);
    }

    public void Dispose() => Reset();

    public void StepFrame(HostCore.IInputSource input, HostCore.IFrameSink frameSink)
    {
        _lastButtons = input.Poll().Buttons;
        EnsureRuntime();

        if (_runtime is not null && string.IsNullOrEmpty(_fault))
        {
            _runtime.Smpc.SetDigitalPadState(MapInput(_lastButtons));
            StepRuntime();
        }

        if (!_hasVideoFrame)
        {
            RenderDiagnosticFrame();
        }
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

        SaturnCd.IDiscImage? discImage = null;
        try
        {
            byte[] biosBytes = File.ReadAllBytes(_biosPath);
            var bios = new SaturnMemory.BiosImage(Path.GetFileName(_biosPath), biosBytes);
            var trace = new SaturnTrace.RingTraceEventSink(2048);
            discImage = OpenDiscImage(_discPath);
            var systemMap = SaturnSystem.SaturnSystemMap.CreateBringup(
                bios,
                new SaturnSystem.SaturnBringupOptions
                {
                    SimulateScspCommandAck = true,
                    DiscImage = discImage,
                    MountedDiscInitialStatus = discImage is null ? null : SaturnCd.CdBlockDriveStatus.Standby,
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
                trace,
                discImage);
            discImage = null;
        }
        catch (Exception ex)
        {
            discImage?.Dispose();
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
                    TryRenderVdp1Frame(runtime);
                    runtime.Scu.RaiseVBlankIn();
                    _vblankInCount++;
                }
                else if (_instructionIndex > 0 && _instructionIndex % VBlankIntervalInstructions == vblankOutOffset)
                {
                    runtime.Scu.RaiseVBlankOut();
                    _vblankOutCount++;
                }

                if (DeliverPendingInterrupt(runtime))
                {
                    _smpcInterruptCount++;
                }
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

    private void TryRenderVdp1Frame(SaturnRuntime runtime)
    {
        IReadOnlyList<SaturnVdp1.Vdp1Command> commands = ReadVdp1CommandChain(runtime.SystemMap.Vdp1Area.Snapshot.Span);
        uint[] vdp2Frame = SaturnVdp2.Vdp2TilemapRenderer.Render(
            runtime.SystemMap.Vdp2Vram.Snapshot.Span,
            runtime.SystemMap.Vdp2Cram.Snapshot.Span,
            runtime.SystemMap.Vdp2Registers.Snapshot.Span,
            FrameWidth,
            FrameHeight);
        bool hasCompletePrimitives = commands.Any(static command => command.End) &&
            commands.Any(static command =>
                !command.Skip && command.CommandCode <= 0x7 &&
                (command.CommandCode >= 0x4 ||
                 (command.CharacterWidth > 0 && command.CharacterHeight > 0)));
        if (hasCompletePrimitives)
        {
            SaturnVdp1.Vdp1RenderResult rendered = SaturnVdp1.Vdp1SoftwareRenderer.Render(
                runtime.SystemMap.Vdp1Area.Snapshot.Span,
                runtime.SystemMap.Vdp2Cram.Snapshot.Span,
                commands,
                _transparentRows,
                FrameWidth,
                FrameHeight);
            if (rendered.DrawnPixels > 0)
            {
                rendered.Frame.BgraPixels.Span.CopyTo(_vdp1Frame);
                _hasVdp1Frame = true;
            }
        }

        vdp2Frame.AsSpan().CopyTo(_frame);

        if (_hasVdp1Frame)
        {
            for (int i = 0; i < _frame.Length; i++)
            {
                if ((_vdp1Frame[i] & 0xFF00_0000u) != 0)
                {
                    _frame[i] = _vdp1Frame[i];
                }
            }
        }

        _hasVideoFrame = _hasVdp1Frame || runtime.SystemMap.Vdp2Registers.WriteCount > 0;
    }

    private static IReadOnlyList<SaturnVdp1.Vdp1Command> ReadVdp1CommandChain(ReadOnlySpan<byte> vram)
    {
        var commands = new List<SaturnVdp1.Vdp1Command>(256);
        var visited = new HashSet<uint>();
        uint address = 0;
        uint? returnAddress = null;

        while (commands.Count < 256 && address <= vram.Length - 0x20 && visited.Add(address))
        {
            SaturnVdp1.Vdp1Command command = SaturnVdp1.Vdp1Command.Read(vram, address);
            commands.Add(command);
            if (command.End)
            {
                break;
            }

            uint sequentialAddress = address + 0x20;
            switch (command.JumpMode)
            {
                case 0:
                    address = sequentialAddress;
                    break;
                case 1:
                    address = command.LinkAddress;
                    break;
                case 2:
                    returnAddress ??= sequentialAddress;
                    address = command.LinkAddress;
                    break;
                case 3 when returnAddress is uint target:
                    address = target;
                    returnAddress = null;
                    break;
                default:
                    address = sequentialAddress;
                    break;
            }
        }

        return commands;
    }

    private static SaturnCd.IDiscImage? OpenDiscImage(string? path)
    {
        if (path is null)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Saturn disc image not found.", path);
        }

        return Path.GetExtension(path).Equals(".cue", StringComparison.OrdinalIgnoreCase)
            ? new SaturnCd.CueDiscImage(path)
            : new SaturnCd.RawDiscImage(path);
    }

    private static bool DeliverPendingInterrupt(SaturnRuntime runtime)
    {
        if (runtime.Scu.HasPendingVBlankIn)
        {
            if (runtime.Master.RequestInterrupt(15, 0x40))
            {
                runtime.Scu.AcknowledgeVBlankIn();
                return false;
            }
        }
        else if (runtime.Scu.HasPendingVBlankOut)
        {
            if (runtime.Master.RequestInterrupt(14, 0x41))
            {
                runtime.Scu.AcknowledgeVBlankOut();
                return false;
            }
        }
        else if (runtime.Scu.HasPendingSmpc && runtime.Master.RequestInterrupt(8, 0x47))
        {
            runtime.Scu.AcknowledgeSmpc();
            return true;
        }

        return false;
    }

    private SaturnCoreStatus CreateStatus()
    {
        if (_runtime is null)
        {
            return new SaturnCoreStatus(
                _biosPath ?? "-",
                false,
                _fault,
                _frameIndex,
                _instructionIndex,
                0,
                0,
                0,
                0,
                0,
                _vblankInCount,
                _vblankOutCount,
                _smpcInterruptCount,
                _lastButtons.ToString(),
                _hasVideoFrame,
                VdpDebugStatus.Empty,
                VdpDebugStatus.Empty,
                VdpDebugStatus.Empty,
                VdpDebugStatus.Empty,
                CdBlockStatus.Empty);
        }

        SaturnRuntime runtime = _runtime;
        return new SaturnCoreStatus(
            runtime.BiosName,
            true,
            _fault,
            _frameIndex,
            _instructionIndex,
            runtime.Master.Registers.ProgramCounter,
            runtime.Master.Registers.StatusRegister,
            runtime.Slave.Registers.ProgramCounter,
            runtime.Smpc.LastCommand,
            runtime.Smpc.PendingInterrupts,
            _vblankInCount,
            _vblankOutCount,
            _smpcInterruptCount,
            _lastButtons.ToString(),
            _hasVideoFrame,
            VdpDebugStatus.From("VDP1", runtime.SystemMap.Vdp1Area),
            VdpDebugStatus.From("VDP2", runtime.SystemMap.Vdp2Vram),
            VdpDebugStatus.From("CRAM", runtime.SystemMap.Vdp2Cram),
            VdpDebugStatus.From("REGS", runtime.SystemMap.Vdp2Registers),
            CdBlockStatus.From(runtime.SystemMap.CdBlock));
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

        int rightX = 178;
        int rightY = 14;
        DrawText(rightX, rightY, "VDP TELEMETRY", 0xffffffff); rightY += 18;
        DrawDebugDevice(rightX, ref rightY, "VDP1", runtime.SystemMap.Vdp1Area);
        DrawDebugDevice(rightX, ref rightY, "VDP2", runtime.SystemMap.Vdp2Vram);
        DrawDebugDevice(rightX, ref rightY, "CRAM", runtime.SystemMap.Vdp2Cram);
        DrawDebugDevice(rightX, ref rightY, "REGS", runtime.SystemMap.Vdp2Registers);
        rightY += 4;
        DrawText(rightX, rightY, "CRAM PALETTE", 0xff91a1ad);
        DrawCramPalette(runtime.SystemMap.Vdp2Cram, rightX, rightY + 14);

        if (!string.IsNullOrEmpty(_fault))
        {
            DrawText(14, y, "FAULT " + _fault.ToUpperInvariant(), 0xffff5c8a);
        }
    }

    private void DrawDebugDevice(
        int x,
        ref int y,
        string label,
        SaturnBus.DebugMemoryBusDevice device)
    {
        string last = device.LastWriteOffset is uint offset ? $"0X{offset:X5}" : "-";
        DrawText(x, y, $"{label} W {device.WriteCount}", device.WriteCount > 0 ? 0xff7cc7ff : 0xff91a1ad);
        y += 14;
        DrawText(x, y, $"LAST {last}", 0xff91a1ad);
        y += 18;
    }

    private void DrawCramPalette(SaturnBus.DebugMemoryBusDevice cram, int x, int y)
    {
        const int columns = 16;
        const int swatch = 7;
        const int gap = 1;
        for (int i = 0; i < 64; i++)
        {
            int px = x + (i % columns) * (swatch + gap);
            int py = y + (i / columns) * (swatch + gap);
            uint color = SaturnColorToArgb(cram.ReadBigEndianWord((uint)(i * 2)));
            FillRect(px, py, swatch, swatch, color);
        }
    }

    private static uint SaturnColorToArgb(ushort color)
    {
        uint r = (uint)((color >> 10) & 0x1f);
        uint g = (uint)((color >> 5) & 0x1f);
        uint b = (uint)(color & 0x1f);
        r = (r << 3) | (r >> 2);
        g = (g << 3) | (g >> 2);
        b = (b << 3) | (b >> 2);
        return 0xff000000u | (r << 16) | (g << 8) | b;
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

    private void FillRect(int x, int y, int width, int height, uint color)
    {
        int minY = Math.Max(0, y);
        int maxY = Math.Min(FrameHeight, y + height);
        int minX = Math.Max(0, x);
        int maxX = Math.Min(FrameWidth, x + width);
        for (int py = minY; py < maxY; py++)
        {
            int row = py * FrameWidth;
            for (int px = minX; px < maxX; px++)
            {
                _frame[row + px] = color;
            }
        }
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
        SaturnTrace.RingTraceEventSink Trace,
        SaturnCd.IDiscImage? DiscImage)
    {
        public bool SlaveWasEnabled { get; set; }
    }
}

internal readonly record struct SaturnCoreStatus(
    string BiosName,
    bool HasRuntime,
    string Fault,
    ulong FrameIndex,
    long InstructionIndex,
    uint MasterPc,
    uint MasterSr,
    uint SlavePc,
    byte SmpcLastCommand,
    int SmpcPendingInterrupts,
    long VBlankInCount,
    long VBlankOutCount,
    long SmpcInterruptCount,
    string Input,
    bool HasVideoFrame,
    VdpDebugStatus Vdp1,
    VdpDebugStatus Vdp2,
    VdpDebugStatus Cram,
    VdpDebugStatus Vdp2Registers,
    CdBlockStatus CdBlock);

internal readonly record struct VdpDebugStatus(
    string Label,
    long ReadCount,
    long WriteCount,
    uint? LastReadOffset,
    uint? LastWriteOffset)
{
    public static VdpDebugStatus Empty => new("-", 0, 0, null, null);

    public static VdpDebugStatus From(string label, SaturnBus.DebugMemoryBusDevice device) =>
        new(label, device.ReadCount, device.WriteCount, device.LastReadOffset, device.LastWriteOffset);
}

internal readonly record struct CdBlockStatus(
    bool HasDisc,
    string DiscName,
    long SectorCount,
    byte AuthenticationType,
    bool AuthStartupCompleted,
    byte LastCommand,
    ushort Cr1,
    ushort Cr2,
    ushort Cr3,
    ushort Cr4,
    ushort ResponseCr1,
    ushort ResponseCr2,
    ushort ResponseCr3,
    ushort ResponseCr4)
{
    public static CdBlockStatus Empty => new(false, "-", 0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static CdBlockStatus From(SaturnCd.CdBlockRegisterBusDevice cdBlock) =>
        new(
            cdBlock.HasDisc,
            cdBlock.DiscName ?? "-",
            cdBlock.DiscSectorCount,
            cdBlock.AuthenticationType,
            cdBlock.AuthStartupCompleted,
            cdBlock.LastCommandCode,
            cdBlock.LastCommandCr1,
            cdBlock.LastCommandCr2,
            cdBlock.LastCommandCr3,
            cdBlock.LastCommandCr4,
            cdBlock.ResponseCr1,
            cdBlock.ResponseCr2,
            cdBlock.ResponseCr3,
            cdBlock.ResponseCr4);
}
