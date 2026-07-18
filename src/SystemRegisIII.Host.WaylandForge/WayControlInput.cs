using SystemRegisIII.WayControlProtocol;

namespace SystemRegisIII.Host.WaylandForge;

internal sealed class WayControlInput : IDisposable
{
    private const int StickThreshold = 12_000;
    private readonly WayControlHub _hub = new();
    private readonly Dictionary<string, DeviceState> _states = new(StringComparer.Ordinal);
    private string _fault = string.Empty;
    private WcpControl? _lastActivatedControl;

    public WayControlInput()
    {
        if (!OperatingSystem.IsLinux()) return;
        try
        {
            _hub.AddBackend(new LinuxEvdevBackend());
        }
        catch (Exception exception)
        {
            _fault = exception.Message;
        }
    }

    public int DeviceCount => _hub.Devices.Count();

    public string Status => _fault.Length > 0
        ? "FAULT " + _fault
        : DeviceCount == 0 ? "NO CONTROLLER" : $"{DeviceCount} CONNECTED";

    public string DeviceSummary
    {
        get
        {
            WcpDeviceInfo? device = _hub.Devices.FirstOrDefault();
            return device is null ? "NONE" : $"{device.Bus} {device.Name}";
        }
    }

    public void Poll()
    {
        if (_fault.Length > 0) return;
        try
        {
            foreach (WcpEvent inputEvent in _hub.Poll())
                Apply(inputEvent);
        }
        catch (Exception exception)
        {
            _states.Clear();
            _fault = exception.Message;
        }
    }

    public bool IsActive(WcpControl control) => _states.Values.Any(state => IsActive(state, control));

    public bool TryConsumeActivatedControl(out WcpControl control)
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

    public void BeginCapture() => _lastActivatedControl = null;

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

    public void Dispose() => _hub.Dispose();

    private sealed class DeviceState
    {
        public HashSet<WcpControl> Buttons { get; } = [];
        public Dictionary<WcpControl, int> Axes { get; } = [];
    }
}
