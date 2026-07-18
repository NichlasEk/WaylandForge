using System.Runtime.InteropServices;

namespace SystemRegisIII.WayControlProtocol;

public sealed class WayControlHub : IDisposable
{
    private readonly List<IWayControlBackend> _backends = [];
    private readonly List<WcpEvent> _events = [];
    private bool _disposed;

    public IReadOnlyList<IWayControlBackend> Backends => _backends;

    public IEnumerable<WcpDeviceInfo> Devices => _backends.SelectMany(backend => backend.Devices);

    public void AddBackend(IWayControlBackend backend)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(backend);
        _backends.Add(backend);
    }

    public ReadOnlySpan<WcpEvent> Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _events.Clear();
        foreach (IWayControlBackend backend in _backends)
            backend.Poll(_events);
        return CollectionsMarshal.AsSpan(_events);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (IWayControlBackend backend in _backends)
            backend.Dispose();
        _backends.Clear();
        _events.Clear();
    }
}
