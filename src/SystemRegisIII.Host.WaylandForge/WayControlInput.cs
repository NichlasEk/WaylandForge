using SystemRegisIII.WayControlProtocol;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class WayControlInput : IDisposable
{
    private const int StickThreshold = 12_000;
    private readonly WayControlHub _hub = new();
    private readonly IWayControlWaitBackend? _waitBackend;
    private readonly object _gate = new();
    private readonly Dictionary<string, DeviceState> _states = new(StringComparer.Ordinal);
    private Thread? _worker;
    private volatile bool _stopping;
    private string _fault = string.Empty;
    private string _deviceSummary = "NONE";
    private int _deviceCount;
    private long _activeControls;
    private int _leftAxes;
    private long _latestEventSequence;
    private long _latestEventTimestampMicroseconds;
    private WcpControl? _lastActivatedControl;

    public WayControlInput()
    {
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            var backend = new LinuxEvdevBackend();
            _hub.AddBackend(backend);
            _waitBackend = backend;
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "WayControlProtocol",
            };
            _worker.Start();
        }
        catch (Exception exception)
        {
            _fault = exception.Message;
        }
    }

    public int DeviceCount
    {
        get { lock (_gate) return _deviceCount; }
    }

    public string Status
    {
        get
        {
            lock (_gate)
                return _fault.Length > 0 ? "FAULT " + _fault : _deviceCount == 0 ? "NO CONTROLLER" : $"{_deviceCount} CONNECTED";
        }
    }

    public string DeviceSummary
    {
        get { lock (_gate) return _deviceSummary; }
    }

    private void WorkerLoop()
    {
        bool firstPass = true;
        while (!_stopping)
        {
            try
            {
                if (!firstPass)
                {
                    if (_waitBackend is not null) _waitBackend.WaitForEvents(1_000);
                    else Thread.Sleep(4);
                }
                firstPass = false;
                ReadOnlySpan<WcpEvent> events = _hub.Poll();
                bool devicesChanged = false;
                ulong latestSequence = 0;
                long latestTimestamp = 0;
                lock (_gate)
                {
                    foreach (WcpEvent inputEvent in events)
                    {
                        Apply(inputEvent);
                        devicesChanged |= inputEvent.Kind is WcpEventKind.DeviceConnected or WcpEventKind.DeviceDisconnected;
                        if (inputEvent.Kind is WcpEventKind.Button or WcpEventKind.Axis)
                        {
                            latestSequence = inputEvent.Sequence;
                            latestTimestamp = inputEvent.TimestampMicroseconds;
                        }
                    }
                    if (events.Length > 0)
                    {
                        PublishActiveControls();
                        if (latestSequence != 0)
                        {
                            Interlocked.Exchange(ref _latestEventTimestampMicroseconds, latestTimestamp);
                            Interlocked.Exchange(ref _latestEventSequence, unchecked((long)latestSequence));
                        }
                    }
                }

                if (devicesChanged)
                {
                    WcpDeviceInfo[] devices = _hub.Devices.ToArray();
                    lock (_gate)
                    {
                        _deviceCount = devices.Length;
                        _deviceSummary = devices.Length == 0 ? "NONE" : $"{devices[0].Bus} {devices[0].Name}";
                    }
                }
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _states.Clear();
                    Interlocked.Exchange(ref _activeControls, 0);
                    Volatile.Write(ref _leftAxes, 0);
                    _fault = exception.Message;
                }
                return;
            }
        }
    }

    public bool IsActive(WcpControl control) =>
        (ushort)control < 64 && ((ulong)Interlocked.Read(ref _activeControls) & (1UL << (ushort)control)) != 0;

    public short LeftX => unchecked((short)(Volatile.Read(ref _leftAxes) & 0xffff));

    public short LeftY => unchecked((short)((Volatile.Read(ref _leftAxes) >> 16) & 0xffff));

    public bool TryGetLatestEvent(ulong observedSequence, out ulong sequence, out long timestampMicroseconds)
    {
        sequence = unchecked((ulong)Interlocked.Read(ref _latestEventSequence));
        timestampMicroseconds = Interlocked.Read(ref _latestEventTimestampMicroseconds);
        return sequence != 0 && sequence != observedSequence;
    }

    public bool TryConsumeActivatedControl(out WcpControl control)
    {
        lock (_gate)
        {
            if (_lastActivatedControl is WcpControl activated)
            {
                control = activated;
                _lastActivatedControl = null;
                return true;
            }
            control = WcpControl.None;
            return false;
        }
    }

    public void BeginCapture()
    {
        lock (_gate) _lastActivatedControl = null;
    }

    private void Apply(WcpEvent inputEvent)
    {
        if (inputEvent.Kind is WcpEventKind.DeviceDisconnected or WcpEventKind.SyncLost)
        {
            _states.Remove(inputEvent.DeviceId);
            return;
        }

        if (!_states.TryGetValue(inputEvent.DeviceId, out DeviceState? state))
        {
            state = new DeviceState();
            _states.Add(inputEvent.DeviceId, state);
        }

        if (inputEvent.Kind == WcpEventKind.Button)
        {
            if (inputEvent.Value == 0) state.Buttons.Remove(inputEvent.Control);
            else
            {
                state.Buttons.Add(inputEvent.Control);
                _lastActivatedControl = inputEvent.Control;
            }
        }
        else if (inputEvent.Kind == WcpEventKind.Axis)
        {
            state.Axes[inputEvent.Control] = inputEvent.Value;
            if (Math.Abs(inputEvent.Value) >= 20_000)
                _lastActivatedControl = AxisDirection(inputEvent.Control, inputEvent.Value);
        }
    }

    private static bool IsActive(DeviceState state, WcpControl control)
    {
        return control switch
        {
            WcpControl.LeftStickUp => Axis(state, WcpControl.LeftY) < -StickThreshold,
            WcpControl.LeftStickDown => Axis(state, WcpControl.LeftY) > StickThreshold,
            WcpControl.LeftStickLeft => Axis(state, WcpControl.LeftX) < -StickThreshold,
            WcpControl.LeftStickRight => Axis(state, WcpControl.LeftX) > StickThreshold,
            WcpControl.RightStickUp => Axis(state, WcpControl.RightY) < -StickThreshold,
            WcpControl.RightStickDown => Axis(state, WcpControl.RightY) > StickThreshold,
            WcpControl.RightStickLeft => Axis(state, WcpControl.RightX) < -StickThreshold,
            WcpControl.RightStickRight => Axis(state, WcpControl.RightX) > StickThreshold,
            WcpControl.LeftTrigger => Axis(state, WcpControl.LeftTrigger) > StickThreshold,
            WcpControl.RightTrigger => Axis(state, WcpControl.RightTrigger) > StickThreshold,
            _ => Pressed(state, control),
        };
    }

    private void PublishActiveControls()
    {
        ulong active = 0;
        short leftX = 0;
        short leftY = 0;
        foreach (DeviceState state in _states.Values)
        {
            foreach (WcpControl button in state.Buttons)
                AddControl(ref active, button);
            foreach (WcpControl control in DirectionalControls)
                if (IsActive(state, control)) AddControl(ref active, control);
            if (IsActive(state, WcpControl.LeftTrigger)) AddControl(ref active, WcpControl.LeftTrigger);
            if (IsActive(state, WcpControl.RightTrigger)) AddControl(ref active, WcpControl.RightTrigger);
            int stateLeftX = Axis(state, WcpControl.LeftX);
            int stateLeftY = Axis(state, WcpControl.LeftY);
            if (Math.Abs(stateLeftX) > Math.Abs((int)leftX)) leftX = (short)Math.Clamp(stateLeftX, short.MinValue, short.MaxValue);
            if (Math.Abs(stateLeftY) > Math.Abs((int)leftY)) leftY = (short)Math.Clamp(stateLeftY, short.MinValue, short.MaxValue);
        }
        Interlocked.Exchange(ref _activeControls, unchecked((long)active));
        Volatile.Write(ref _leftAxes, (ushort)leftX | (leftY << 16));
    }

    private static void AddControl(ref ulong active, WcpControl control)
    {
        if ((ushort)control < 64) active |= 1UL << (ushort)control;
    }

    private static WcpControl? AxisDirection(WcpControl axis, int value) => axis switch
    {
        WcpControl.LeftX => value < 0 ? WcpControl.LeftStickLeft : WcpControl.LeftStickRight,
        WcpControl.LeftY => value < 0 ? WcpControl.LeftStickUp : WcpControl.LeftStickDown,
        WcpControl.RightX => value < 0 ? WcpControl.RightStickLeft : WcpControl.RightStickRight,
        WcpControl.RightY => value < 0 ? WcpControl.RightStickUp : WcpControl.RightStickDown,
        WcpControl.LeftTrigger => WcpControl.LeftTrigger,
        WcpControl.RightTrigger => WcpControl.RightTrigger,
        _ => null,
    };

    private static bool Pressed(DeviceState state, WcpControl control) => state.Buttons.Contains(control);

    private static int Axis(DeviceState state, WcpControl control) =>
        state.Axes.TryGetValue(control, out int value) ? value : 0;

    public void Dispose()
    {
        _stopping = true;
        _worker?.Join();
        _hub.Dispose();
    }

    private sealed class DeviceState
    {
        public HashSet<WcpControl> Buttons { get; } = [];
        public Dictionary<WcpControl, int> Axes { get; } = [];
    }

    private static readonly WcpControl[] DirectionalControls =
    [
        WcpControl.LeftStickUp,
        WcpControl.LeftStickDown,
        WcpControl.LeftStickLeft,
        WcpControl.LeftStickRight,
        WcpControl.RightStickUp,
        WcpControl.RightStickDown,
        WcpControl.RightStickLeft,
        WcpControl.RightStickRight,
    ];
}
