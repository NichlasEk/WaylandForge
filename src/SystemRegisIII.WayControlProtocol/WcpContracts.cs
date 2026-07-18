namespace SystemRegisIII.WayControlProtocol;

public enum WcpBus : ushort
{
    Unknown = 0,
    Usb = 3,
    Bluetooth = 5,
    Virtual = 6,
}

public enum WcpControl : ushort
{
    None = 0,
    South = 1,
    East = 2,
    West = 3,
    North = 4,
    LeftShoulder = 5,
    RightShoulder = 6,
    LeftTriggerButton = 7,
    RightTriggerButton = 8,
    Select = 9,
    Start = 10,
    Guide = 11,
    LeftStickButton = 12,
    RightStickButton = 13,
    DpadUp = 14,
    DpadDown = 15,
    DpadLeft = 16,
    DpadRight = 17,
    LeftX = 32,
    LeftY = 33,
    RightX = 34,
    RightY = 35,
    LeftTrigger = 36,
    RightTrigger = 37,
    LeftStickUp = 48,
    LeftStickDown = 49,
    LeftStickLeft = 50,
    LeftStickRight = 51,
    RightStickUp = 52,
    RightStickDown = 53,
    RightStickLeft = 54,
    RightStickRight = 55,
}

public enum WcpEventKind : ushort
{
    DeviceConnected = 1,
    DeviceDisconnected = 2,
    Button = 3,
    Axis = 4,
    SyncLost = 5,
}

public sealed record WcpDeviceInfo(
    string Id,
    string Name,
    WcpBus Bus,
    ushort VendorId,
    ushort ProductId,
    ushort Version,
    string Backend,
    string NativePath);

public readonly record struct WcpEvent(
    ulong Sequence,
    long TimestampMicroseconds,
    string DeviceId,
    WcpEventKind Kind,
    WcpControl Control,
    int Value);

public interface IWayControlBackend : IDisposable
{
    string Name { get; }

    IReadOnlyCollection<WcpDeviceInfo> Devices { get; }

    void Poll(ICollection<WcpEvent> events);
}
