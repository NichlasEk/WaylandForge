using System.Diagnostics;
using SystemRegisIII.WayControlProtocol;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    Span<byte> packet = stackalloc byte[WcpPacketHeader.Size];
    var written = new WcpPacketHeader(1, 0, WcpPacketType.Input, 3, WcpPacketHeader.Size, 42, 123_456);
    if (!written.TryWrite(packet) || !WcpPacketHeader.TryRead(packet, out WcpPacketHeader read) || read != written)
        throw new InvalidOperationException("WCP header round-trip failed.");
    packet[0] ^= 0xff;
    if (WcpPacketHeader.TryRead(packet, out _))
        throw new InvalidOperationException("WCP accepted an invalid magic value.");
    using (var backend = new LinuxEvdevBackend("/tmp/wcp-self-test-no-devices", "/tmp/wcp-self-test-no-sysfs"))
    {
        var events = new List<WcpEvent>();
        backend.Poll(events);
        if (events.Count != 0 || backend.WaitForEvents(0))
            throw new InvalidOperationException("WCP empty poll self-test failed.");
        Stopwatch wait = Stopwatch.StartNew();
        if (backend.WaitForEvents(20) || wait.ElapsedMilliseconds < 10)
            throw new InvalidOperationException("WCP kernel wait did not block.");
    }
    Console.WriteLine("WCP SELF-TEST OK");
    return;
}

bool once = args.Contains("--once", StringComparer.Ordinal);
using var hub = new WayControlHub();
hub.AddBackend(new LinuxEvdevBackend());

Console.WriteLine("WCP PROBE 1.0 · DIRECT LINUX EVDEV · CTRL+C TO STOP");
do
{
    foreach (WcpEvent inputEvent in hub.Poll())
    {
        if (inputEvent.Kind == WcpEventKind.DeviceConnected)
        {
            WcpDeviceInfo? device = hub.Devices.FirstOrDefault(candidate => candidate.Id == inputEvent.DeviceId);
            if (device is not null)
            {
                Console.WriteLine($"+ {device.Id} {device.Bus} {device.VendorId:x4}:{device.ProductId:x4} {device.Name} [{device.NativePath}]");
                continue;
            }
        }

        Console.WriteLine($"{inputEvent.Sequence,8} {inputEvent.DeviceId} {inputEvent.Kind,-18} {inputEvent.Control,-20} {inputEvent.Value,7}");
    }

    if (!once) Thread.Sleep(4);
}
while (!once);
