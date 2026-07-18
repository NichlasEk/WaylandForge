using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SystemRegisIII.WayControlProtocol;

public sealed partial class LinuxEvdevBackend : IWayControlBackend
{
    private const int OpenReadOnly = 0;
    private const int OpenNonBlocking = 0x800;
    private const int OpenCloseOnExec = 0x80000;
    private const int ErrorAgain = 11;
    private const int ErrorInterrupted = 4;
    private const ushort EventSync = 0;
    private const ushort EventKey = 1;
    private const ushort EventAbsolute = 3;
    private const ushort SyncDropped = 3;
    private const int ButtonJoystick = 0x120;
    private const int ButtonGamepad = 0x130;
    private const int ButtonDpadUp = 0x220;
    private const int ButtonDpadRight = 0x223;

    private readonly string _deviceRoot;
    private readonly string _sysClassRoot;
    private readonly TimeSpan _scanInterval;
    private readonly Dictionary<string, EvdevDevice> _devices = new(StringComparer.Ordinal);
    private long _lastScanTimestamp;
    private ulong _sequence;
    private bool _disposed;

    public LinuxEvdevBackend(
        string deviceRoot = "/dev/input",
        string sysClassRoot = "/sys/class/input",
        TimeSpan? scanInterval = null)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("The evdev backend requires Linux.");
        _deviceRoot = deviceRoot;
        _sysClassRoot = sysClassRoot;
        _scanInterval = scanInterval ?? TimeSpan.FromSeconds(1);
    }

    public string Name => "linux-evdev";

    public IReadOnlyCollection<WcpDeviceInfo> Devices => _devices.Values.Select(device => device.Info).ToArray();

    public void Poll(ICollection<WcpEvent> events)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(events);

        long now = Stopwatch.GetTimestamp();
        if (_lastScanTimestamp == 0 || Stopwatch.GetElapsedTime(_lastScanTimestamp, now) >= _scanInterval)
        {
            Scan(events);
            _lastScanTimestamp = now;
        }

        List<string>? disconnected = null;
        foreach ((string path, EvdevDevice device) in _devices)
        {
            if (!ReadDevice(device, events))
                (disconnected ??= []).Add(path);
        }

        if (disconnected is null) return;
        foreach (string path in disconnected)
            RemoveDevice(path, events);
    }

    private void Scan(ICollection<WcpEvent> events)
    {
        HashSet<string> visible = new(StringComparer.Ordinal);
        if (Directory.Exists(_deviceRoot))
        {
            foreach (string path in Directory.EnumerateFiles(_deviceRoot, "event*").Order(StringComparer.Ordinal))
            {
                visible.Add(path);
                if (_devices.ContainsKey(path)) continue;
                EvdevDevice? device = TryOpenDevice(path);
                if (device is null) continue;
                _devices.Add(path, device);
                events.Add(NewEvent(device.Info.Id, WcpEventKind.DeviceConnected, WcpControl.None, 0));
            }
        }

        foreach (string path in _devices.Keys.Where(path => !visible.Contains(path)).ToArray())
            RemoveDevice(path, events);
    }

    private EvdevDevice? TryOpenDevice(string path)
    {
        int descriptor = Native.open(path, OpenReadOnly | OpenNonBlocking | OpenCloseOnExec);
        if (descriptor < 0) return null;

        try
        {
            byte[] eventBits = new byte[32];
            byte[] keyBits = new byte[96];
            if (Native.ioctl(descriptor, ReadBitsRequest(0, eventBits.Length), eventBits) < 0 ||
                !HasBit(eventBits, EventKey) ||
                Native.ioctl(descriptor, ReadBitsRequest(EventKey, keyBits.Length), keyBits) < 0)
                return null;

            bool gameController = HasAnyBit(keyBits, ButtonJoystick, ButtonGamepad + 15) ||
                HasAnyBit(keyBits, ButtonDpadUp, ButtonDpadRight);
            if (!gameController) return null;

            string eventName = Path.GetFileName(path);
            string sysDevice = Path.Combine(_sysClassRoot, eventName, "device");
            string name = ReadText(Path.Combine(sysDevice, "name")) ?? eventName;
            string unique = ReadText(Path.Combine(sysDevice, "uniq")) ?? string.Empty;
            string physical = ReadText(Path.Combine(sysDevice, "phys")) ?? string.Empty;
            ushort busId = ReadHex16(Path.Combine(sysDevice, "id", "bustype"));
            ushort vendor = ReadHex16(Path.Combine(sysDevice, "id", "vendor"));
            ushort product = ReadHex16(Path.Combine(sysDevice, "id", "product"));
            ushort version = ReadHex16(Path.Combine(sysDevice, "id", "version"));
            WcpBus bus = busId switch
            {
                (ushort)WcpBus.Usb => WcpBus.Usb,
                (ushort)WcpBus.Bluetooth => WcpBus.Bluetooth,
                (ushort)WcpBus.Virtual => WcpBus.Virtual,
                _ => WcpBus.Unknown,
            };
            string idMaterial = string.Join('|', busId, vendor, product, version, unique, physical, name);
            string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idMaterial)))[..32].ToLowerInvariant();
            var info = new WcpDeviceInfo(id, name, bus, vendor, product, version, Name, path);
            var device = new EvdevDevice(descriptor, info);
            descriptor = -1;
            return device;
        }
        finally
        {
            if (descriptor >= 0) Native.close(descriptor);
        }
    }

    private bool ReadDevice(EvdevDevice device, ICollection<WcpEvent> events)
    {
        int eventSize = IntPtr.Size * 2 + 8;
        byte[] buffer = device.ReadBuffer;
        for (int batch = 0; batch < 8; batch++)
        {
            nint count = Native.read(device.Descriptor, buffer, (nuint)buffer.Length);
            if (count < 0)
            {
                int error = Marshal.GetLastPInvokeError();
                if (error == ErrorAgain) return true;
                if (error == ErrorInterrupted) continue;
                return false;
            }
            if (count == 0) return false;

            int bytesRead = checked((int)count);
            int typeOffset = IntPtr.Size * 2;
            for (int offset = 0; offset + eventSize <= bytesRead; offset += eventSize)
            {
                ReadOnlySpan<byte> inputEvent = buffer.AsSpan(offset, eventSize);
                ushort type = BinaryPrimitives.ReadUInt16LittleEndian(inputEvent[typeOffset..]);
                ushort code = BinaryPrimitives.ReadUInt16LittleEndian(inputEvent[(typeOffset + 2)..]);
                int value = BinaryPrimitives.ReadInt32LittleEndian(inputEvent[(typeOffset + 4)..]);
                if (type == EventSync && code == SyncDropped)
                {
                    events.Add(NewEvent(device.Info.Id, WcpEventKind.SyncLost, WcpControl.None, 0));
                    continue;
                }
                if (type == EventKey && TryMapButton(code, out WcpControl button))
                {
                    events.Add(NewEvent(device.Info.Id, WcpEventKind.Button, button, value == 0 ? 0 : 1));
                    continue;
                }
                if (type == EventAbsolute && TryMapAxis(code, out WcpControl axis))
                {
                    if (axis is WcpControl.DpadLeft or WcpControl.DpadRight)
                        EmitHat(events, device, horizontal: true, value);
                    else if (axis is WcpControl.DpadUp or WcpControl.DpadDown)
                        EmitHat(events, device, horizontal: false, value);
                    else
                        events.Add(NewEvent(device.Info.Id, WcpEventKind.Axis, axis, NormalizeAxis(device, code, value)));
                }
            }
            if (bytesRead < buffer.Length) break;
        }
        return true;
    }

    private void EmitHat(ICollection<WcpEvent> events, EvdevDevice device, bool horizontal, int value)
    {
        int previous = horizontal ? device.HatX : device.HatY;
        int current = Math.Sign(value);
        if (previous == current) return;
        WcpControl negative = horizontal ? WcpControl.DpadLeft : WcpControl.DpadUp;
        WcpControl positive = horizontal ? WcpControl.DpadRight : WcpControl.DpadDown;
        if (previous < 0) events.Add(NewEvent(device.Info.Id, WcpEventKind.Button, negative, 0));
        if (previous > 0) events.Add(NewEvent(device.Info.Id, WcpEventKind.Button, positive, 0));
        if (current < 0) events.Add(NewEvent(device.Info.Id, WcpEventKind.Button, negative, 1));
        if (current > 0) events.Add(NewEvent(device.Info.Id, WcpEventKind.Button, positive, 1));
        if (horizontal) device.HatX = current;
        else device.HatY = current;
    }

    private static int NormalizeAxis(EvdevDevice device, ushort code, int value)
    {
        if (!device.AxisRanges.TryGetValue(code, out AxisRange range))
        {
            byte[] info = new byte[24];
            if (Native.ioctl(device.Descriptor, ReadAbsoluteRequest(code), info) >= 0)
            {
                range = new AxisRange(
                    BinaryPrimitives.ReadInt32LittleEndian(info.AsSpan(4)),
                    BinaryPrimitives.ReadInt32LittleEndian(info.AsSpan(8)),
                    BinaryPrimitives.ReadInt32LittleEndian(info.AsSpan(16)));
            }
            else
            {
                range = new AxisRange(short.MinValue, short.MaxValue, 0);
            }
            device.AxisRanges.Add(code, range);
        }

        if (range.Maximum <= range.Minimum) return 0;
        long center2 = (long)range.Minimum + range.Maximum;
        long value2 = (long)value * 2;
        if (Math.Abs(value2 - center2) <= (long)range.Flat * 2) return 0;
        long normalized = ((long)value - range.Minimum) * 65535 / (range.Maximum - range.Minimum) - 32768;
        return (int)Math.Clamp(normalized, short.MinValue, short.MaxValue);
    }

    private void RemoveDevice(string path, ICollection<WcpEvent> events)
    {
        if (!_devices.Remove(path, out EvdevDevice? device)) return;
        Native.close(device.Descriptor);
        events.Add(NewEvent(device.Info.Id, WcpEventKind.DeviceDisconnected, WcpControl.None, 0));
    }

    private WcpEvent NewEvent(string deviceId, WcpEventKind kind, WcpControl control, int value) =>
        new(++_sequence, Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency, deviceId, kind, control, value);

    private static bool TryMapButton(ushort code, out WcpControl control)
    {
        control = code switch
        {
            0x130 => WcpControl.South,
            0x131 => WcpControl.East,
            0x133 => WcpControl.North,
            0x134 => WcpControl.West,
            0x136 => WcpControl.LeftShoulder,
            0x137 => WcpControl.RightShoulder,
            0x138 => WcpControl.LeftTriggerButton,
            0x139 => WcpControl.RightTriggerButton,
            0x13a => WcpControl.Select,
            0x13b => WcpControl.Start,
            0x13c => WcpControl.Guide,
            0x13d => WcpControl.LeftStickButton,
            0x13e => WcpControl.RightStickButton,
            0x220 => WcpControl.DpadUp,
            0x221 => WcpControl.DpadDown,
            0x222 => WcpControl.DpadLeft,
            0x223 => WcpControl.DpadRight,
            _ => WcpControl.None,
        };
        return control != WcpControl.None;
    }

    private static bool TryMapAxis(ushort code, out WcpControl control)
    {
        control = code switch
        {
            0x00 => WcpControl.LeftX,
            0x01 => WcpControl.LeftY,
            0x02 => WcpControl.LeftTrigger,
            0x03 => WcpControl.RightX,
            0x04 => WcpControl.RightY,
            0x05 => WcpControl.RightTrigger,
            0x10 => WcpControl.DpadLeft,
            0x11 => WcpControl.DpadUp,
            _ => WcpControl.None,
        };
        return control != WcpControl.None;
    }

    private static bool HasAnyBit(byte[] bits, int first, int last)
    {
        for (int bit = first; bit <= last; bit++)
            if (HasBit(bits, bit)) return true;
        return false;
    }

    private static bool HasBit(byte[] bits, int bit) =>
        bit >= 0 && bit / 8 < bits.Length && (bits[bit / 8] & (1 << (bit % 8))) != 0;

    private static nuint ReadBitsRequest(int eventType, int length) =>
        IoRead('E', 0x20 + eventType, length);

    private static nuint ReadAbsoluteRequest(int axis) => IoRead('E', 0x40 + axis, 24);

    private static nuint IoRead(char type, int number, int size) =>
        ((nuint)2 << 30) | ((nuint)size << 16) | ((nuint)type << 8) | (uint)number;

    private static string? ReadText(string path)
    {
        try
        {
            string text = File.ReadAllText(path).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static ushort ReadHex16(string path) =>
        ushort.TryParse(ReadText(path), System.Globalization.NumberStyles.HexNumber, null, out ushort value) ? value : (ushort)0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (EvdevDevice device in _devices.Values)
            Native.close(device.Descriptor);
        _devices.Clear();
    }

    private sealed class EvdevDevice(int descriptor, WcpDeviceInfo info)
    {
        public int Descriptor { get; } = descriptor;
        public WcpDeviceInfo Info { get; } = info;
        public byte[] ReadBuffer { get; } = new byte[(IntPtr.Size * 2 + 8) * 64];
        public Dictionary<ushort, AxisRange> AxisRanges { get; } = [];
        public int HatX { get; set; }
        public int HatY { get; set; }
    }

    private readonly record struct AxisRange(int Minimum, int Maximum, int Flat);

    private static partial class Native
    {
        [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int open(string path, int flags);

        [LibraryImport("libc", SetLastError = true)]
        internal static partial nint read(int descriptor, [Out] byte[] buffer, nuint count);

        [LibraryImport("libc", SetLastError = true)]
        internal static partial int ioctl(int descriptor, nuint request, [In, Out] byte[] data);

        [LibraryImport("libc", SetLastError = true)]
        internal static partial int close(int descriptor);
    }
}
