namespace SystemRegisIII.WaylandForge.Ui;

public readonly record struct UiColors(
    uint Text,
    uint MutedText,
    uint Surface,
    uint Panel,
    uint SurfaceHot,
    uint SurfaceActive,
    uint Border,
    uint BorderHot,
    uint BorderActive,
    uint Accent);

public readonly record struct UiButtonStyle(
    int BorderThickness,
    int PaddingX,
    int PaddingY,
    UiColors Colors);

public readonly record struct UiPanelStyle(int BorderThickness, int Padding, UiColors Colors);

public readonly record struct UiTextStyle(int Scale, UiColors Colors);

public readonly record struct UiTextBoxStyle(
    int BorderThickness,
    int PaddingX,
    int PaddingY,
    int CursorWidth,
    UiColors Colors);

public sealed class UiTheme
{
    public static UiTheme Default { get; } = new(
        name: "DARK",
        new UiButtonStyle(
            BorderThickness: 1,
            PaddingX: 6,
            PaddingY: 4,
            new UiColors(
                Text: 0xffe8edf2,
                MutedText: 0xff91a1ad,
                Surface: 0xff181d22,
                Panel: 0xff111318,
                SurfaceHot: 0xff28333d,
                SurfaceActive: 0xff355c7d,
                Border: 0xff39424c,
                BorderHot: 0xff91a1ad,
                BorderActive: 0xff82cfff,
                Accent: 0xffffc857)),
        new UiPanelStyle(1, 8, new UiColors(0xffe8edf2, 0xff91a1ad, 0xff181d22, 0xff111318, 0xff28333d, 0xff355c7d, 0xff39424c, 0xff91a1ad, 0xff82cfff, 0xffffc857)),
        new UiTextStyle(1, new UiColors(0xffe8edf2, 0xff91a1ad, 0xff181d22, 0xff111318, 0xff28333d, 0xff355c7d, 0xff39424c, 0xff91a1ad, 0xff82cfff, 0xffffc857)),
        new UiTextBoxStyle(1, 6, 4, 1, new UiColors(0xffe8edf2, 0xff91a1ad, 0xff101419, 0xff111318, 0xff18222c, 0xff20364a, 0xff39424c, 0xff91a1ad, 0xff82cfff, 0xffffc857)));

    public static UiTheme HighContrast { get; } = new(
        name: "HIGH",
        new UiButtonStyle(1, 6, 4, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)),
        new UiPanelStyle(1, 8, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)),
        new UiTextStyle(1, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)),
        new UiTextBoxStyle(1, 6, 4, 1, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)));

    public static UiTheme Warm { get; } = new(
        name: "WARM",
        new UiButtonStyle(1, 6, 4, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)),
        new UiPanelStyle(1, 8, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)),
        new UiTextStyle(1, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)),
        new UiTextBoxStyle(1, 6, 4, 1, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)));

    public static IReadOnlyList<UiTheme> BuiltIns { get; } = [Default, HighContrast, Warm];

    public UiTheme(string name, UiButtonStyle button, UiPanelStyle panel, UiTextStyle text, UiTextBoxStyle textBox)
    {
        Name = name;
        Button = button;
        Panel = panel;
        Text = text;
        TextBox = textBox;
    }

    public string Name { get; }
    public UiButtonStyle Button { get; }
    public UiPanelStyle Panel { get; }
    public UiTextStyle Text { get; }
    public UiTextBoxStyle TextBox { get; }
}

public readonly record struct UiButtonResult(bool Hovered, bool Pressed, bool Clicked);
public readonly record struct UiSliderResult(int Value, bool Hovered, bool Dragging, bool Changed);
public readonly record struct UiTextBoxOptions(bool ReadOnly = false, bool Numeric = false, bool Password = false, int MaxLength = 32);
public readonly record struct UiTextBoxResult(string Text, bool Hovered, bool Focused, bool Changed, bool Submitted);
public readonly record struct TextInputEvent(uint KeyCode, uint Serial, bool Pressed = true);
public readonly record struct ScrollInputEvent(int Delta, uint Serial);
public readonly record struct UiScrollArea(RectI Content, IDisposable Clip) : IDisposable
{
    public void Dispose()
    {
        Clip.Dispose();
    }
}

public sealed class UiWindowState
{
    public bool IsOpen { get; set; }
    public RectI? Rect { get; set; }
    public bool IsDragging { get; set; }
    public int DragStartX { get; set; }
    public int DragStartY { get; set; }
    public int DragWindowX { get; set; }
    public int DragWindowY { get; set; }
}

public readonly record struct UiWindowResult(bool IsOpen, bool Closed, bool Dragging, bool Activated, RectI Rect, RectI Content);
public readonly record struct UiId(string Value);

internal sealed class UiWidgetState
{
    public bool IsOpen { get; set; } = true;
    public string Text { get; set; } = string.Empty;
    public int ScrollOffset { get; set; }
    public bool IsDraggingScroll { get; set; }
    public int ScrollDragStartY { get; set; }
    public int ScrollDragStartOffset { get; set; }
}

public sealed class UiContext
{
    private readonly SoftwareCanvas _canvas;
    private readonly Dictionary<string, UiWidgetState> _state = [];
    private string? _hot;
    private string? _active;
    private string? _focused;
    private PointerState _pointer;
    private PointerState _previousPointer;
    private TextInputEvent _textInput;
    private ScrollInputEvent _scrollInput;
    private bool _inputEnabled = true;
    private readonly Stack<bool> _inputEnabledStack = new();
    private uint _handledTextSerial;
    private uint _handledScrollSerial;

    public UiContext(SoftwareCanvas canvas, UiTheme theme)
    {
        _canvas = canvas;
        Theme = theme;
    }

    public UiTheme Theme { get; set; }
    public string? Hot => _hot;
    public string? Active => _active;
    public string? Focused => _focused;

    public void BeginFrame(PointerState pointer, PointerState previousPointer)
    {
        BeginFrame(pointer, previousPointer, default);
    }

    public void BeginFrame(PointerState pointer, PointerState previousPointer, TextInputEvent textInput)
    {
        BeginFrame(pointer, previousPointer, textInput, default);
    }

    public void BeginFrame(PointerState pointer, PointerState previousPointer, TextInputEvent textInput, ScrollInputEvent scrollInput)
    {
        _hot = null;
        _pointer = pointer;
        _previousPointer = previousPointer;
        _textInput = textInput;
        _scrollInput = scrollInput;
        _inputEnabled = true;
        _inputEnabledStack.Clear();
    }

    public IDisposable PushInputEnabled(bool enabled)
    {
        _inputEnabledStack.Push(_inputEnabled);
        _inputEnabled = _inputEnabled && enabled;
        return new InputScope(this);
    }

    public UiButtonResult Button(RectI rect, string label, bool active = false)
    {
        return Button(new UiId(label), rect, label, active);
    }

    public UiButtonResult Button(UiId id, RectI rect, string label, bool active = false)
    {
        UiButtonStyle style = Theme.Button;
        bool hovered = _inputEnabled && _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
        bool pressed = hovered && _pointer.LeftPressed;
        bool clicked = hovered && _pointer.LeftPressed && !_previousPointer.LeftPressed;
        if (hovered)
        {
            _hot = id.Value;
        }
        if (pressed)
        {
            _active = id.Value;
        }
        if (clicked)
        {
            _focused = id.Value;
        }

        uint fill = active ? style.Colors.SurfaceActive : hovered ? style.Colors.SurfaceHot : style.Colors.Surface;
        uint border = active ? style.Colors.BorderActive : hovered ? style.Colors.BorderHot : style.Colors.Border;
        uint text = active ? style.Colors.Text : style.Colors.MutedText;

        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, fill);
        for (int i = 0; i < Math.Max(1, style.BorderThickness); i++)
        {
            _canvas.DrawRect(rect.X + i, rect.Y + i, rect.Width - i * 2, rect.Height - i * 2, border);
        }

        int textX = rect.X + style.PaddingX;
        int textY = rect.Y + style.PaddingY;
        if (pressed)
        {
            textY++;
        }
        _canvas.DrawText(textX, textY, label, text);

        return new UiButtonResult(hovered, pressed, clicked);
    }

    public bool ToggleButton(RectI rect, string label, bool active)
    {
        return Button(rect, label, active).Clicked;
    }

    public bool ToggleButton(UiId id, RectI rect, string label, bool active)
    {
        return Button(id, rect, label, active).Clicked;
    }

    public UiSliderResult Slider(UiId id, RectI rect, int value, int min, int max)
    {
        if (max <= min)
        {
            max = min + 1;
        }

        value = Math.Clamp(value, min, max);
        bool hovered = _inputEnabled && _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
        bool started = hovered && _pointer.LeftPressed && !_previousPointer.LeftPressed;
        if (started)
        {
            _active = id.Value;
            _focused = id.Value;
        }

        bool dragging = _inputEnabled && _pointer.LeftPressed && (_active == id.Value || hovered);
        bool changed = false;
        if (dragging)
        {
            _active = id.Value;
            int trackX = rect.X + 4;
            int trackWidth = Math.Max(1, rect.Width - 8);
            int clampedX = Math.Clamp(_pointer.X, trackX, trackX + trackWidth);
            int next = min + (int)Math.Round((clampedX - trackX) * (double)(max - min) / trackWidth);
            next = Math.Clamp(next, min, max);
            changed = next != value;
            value = next;
        }
        if (hovered)
        {
            _hot = id.Value;
        }

        UiButtonStyle style = Theme.Button;
        uint fill = dragging ? style.Colors.SurfaceActive : hovered ? style.Colors.SurfaceHot : style.Colors.Surface;
        uint border = dragging ? style.Colors.BorderActive : hovered ? style.Colors.BorderHot : style.Colors.Border;
        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, fill);
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, border);

        int lineY = rect.Y + rect.Height / 2;
        int lineX = rect.X + 5;
        int lineW = Math.Max(1, rect.Width - 10);
        _canvas.DrawLine(lineX, lineY, lineX + lineW, lineY, style.Colors.Border);

        int knobX = lineX + (int)Math.Round((value - min) * (double)lineW / (max - min));
        _canvas.FillRect(knobX - 2, rect.Y + 3, 5, Math.Max(1, rect.Height - 6), style.Colors.Accent);
        _canvas.DrawRect(knobX - 3, rect.Y + 2, 7, Math.Max(1, rect.Height - 4), border);

        return new UiSliderResult(value, hovered, dragging, changed);
    }

    public RectI Panel(RectI rect, string? title = null)
    {
        UiPanelStyle style = Theme.Panel;
        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, style.Colors.Panel);
        for (int i = 0; i < Math.Max(1, style.BorderThickness); i++)
        {
            _canvas.DrawRect(rect.X + i, rect.Y + i, rect.Width - i * 2, rect.Height - i * 2, style.Colors.Border);
        }

        if (!string.IsNullOrEmpty(title))
        {
            _canvas.DrawText(rect.X + style.Padding, rect.Y + style.Padding, title, style.Colors.Text, 2);
        }

        return new RectI(
            rect.X + style.Padding,
            rect.Y + style.Padding + (string.IsNullOrEmpty(title) ? 0 : 26),
            Math.Max(1, rect.Width - style.Padding * 2),
            Math.Max(1, rect.Height - style.Padding * 2 - (string.IsNullOrEmpty(title) ? 0 : 26)));
    }

    public UiWindowResult BeginWindow(UiId id, UiWindowState state, RectI preferredRect, RectI bounds, string title, bool active = true, bool inputEnabled = true, bool movable = true)
    {
        const int titleBarHeight = 24;
        RectI rect = state.Rect ?? preferredRect;
        rect = rect with { Width = preferredRect.Width, Height = preferredRect.Height };
        rect = ClampWindow(rect, bounds);
        state.Rect = rect;

        RectI titleBar = new(rect.X, rect.Y, rect.Width, titleBarHeight);
        RectI closeRect = new(rect.Right - 28, rect.Y + 4, 20, 16);
        bool activated = inputEnabled && _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y) && _pointer.LeftPressed && !_previousPointer.LeftPressed;
        if (inputEnabled && movable)
        {
            HandleWindowDrag(id, state, bounds, titleBar, closeRect, rect);
        }
        else
        {
            state.IsDragging = false;
        }
        rect = state.Rect ?? rect;
        titleBar = new(rect.X, rect.Y, rect.Width, titleBarHeight);
        closeRect = new(rect.Right - 28, rect.Y + 4, 20, 16);

        DrawWindowBackplate(rect);
        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, Theme.Panel.Colors.Panel);
        uint titleFill = active ? Theme.Button.Colors.Surface : Theme.Panel.Colors.Panel;
        _canvas.FillRect(titleBar.X, titleBar.Y, titleBar.Width, titleBar.Height, state.IsDragging ? Theme.Button.Colors.SurfaceActive : titleFill);
        _canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, active ? Theme.Button.Colors.BorderHot : Theme.Button.Colors.Border);
        _canvas.DrawLine(rect.X, titleBar.Bottom, rect.Right - 1, titleBar.Bottom, Theme.Button.Colors.Border);
        Text(titleBar.X + 10, titleBar.Y + 8, title, state.IsDragging ? UiTextKind.Accent : UiTextKind.Normal);

        UiButtonResult closeButton = Button(new UiId(id.Value + ".close"), closeRect, "X");
        bool closed = inputEnabled && closeButton.Clicked;
        if (closed)
        {
            state.IsOpen = false;
            state.IsDragging = false;
        }

        RectI content = new(rect.X, titleBar.Bottom, rect.Width, Math.Max(1, rect.Height - titleBarHeight));
        return new UiWindowResult(state.IsOpen, closed, state.IsDragging, activated, rect, content);
    }

    private void HandleWindowDrag(UiId id, UiWindowState state, RectI bounds, RectI titleBar, RectI closeRect, RectI rect)
    {
        bool titleHovered = _pointer.IsInside && titleBar.Contains(_pointer.X, _pointer.Y) && !closeRect.Contains(_pointer.X, _pointer.Y);
        if (titleHovered)
        {
            _hot = id.Value + ".title";
        }

        if (titleHovered && _pointer.LeftPressed && !_previousPointer.LeftPressed)
        {
            _active = id.Value + ".title";
            _focused = id.Value;
            state.IsDragging = true;
            state.DragStartX = _pointer.X;
            state.DragStartY = _pointer.Y;
            state.DragWindowX = rect.X;
            state.DragWindowY = rect.Y;
        }

        if (!_pointer.LeftPressed)
        {
            state.IsDragging = false;
            return;
        }

        if (state.IsDragging)
        {
            _active = id.Value + ".title";
            int x = state.DragWindowX + _pointer.X - state.DragStartX;
            int y = state.DragWindowY + _pointer.Y - state.DragStartY;
            state.Rect = ClampWindow(rect with { X = x, Y = y }, bounds);
        }
    }

    private void DrawWindowBackplate(RectI rect)
    {
        _canvas.FillRect(rect.X - 9, rect.Y + 7, rect.Width + 18, rect.Height + 10, 0xff050607);
        _canvas.FillRect(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8, 0xff233241);
    }

    private static RectI ClampWindow(RectI rect, RectI bounds)
    {
        int minVisible = Math.Min(96, Math.Max(32, rect.Width / 4));
        int minX = bounds.X - (rect.Width - minVisible);
        int maxX = bounds.Right - minVisible;
        int minY = bounds.Y;
        int maxY = Math.Max(bounds.Y, bounds.Bottom - 24);
        return rect with
        {
            X = Math.Clamp(rect.X, minX, maxX),
            Y = Math.Clamp(rect.Y, minY, maxY),
        };
    }

    public void Text(int x, int y, string text, UiTextKind kind = UiTextKind.Normal, int? scale = null)
    {
        UiTextStyle style = Theme.Text;
        uint color = kind switch
        {
            UiTextKind.Muted => style.Colors.MutedText,
            UiTextKind.Accent => style.Colors.Accent,
            _ => style.Colors.Text,
        };
        _canvas.DrawText(x, y, text, color, scale ?? style.Scale);
    }

    public bool Collapsible(UiId id, ref UiColumn column, string title, int openHeight, out RectI content)
    {
        UiWidgetState state = GetState(id);
        int headerHeight = 17;
        column = column.Next(headerHeight, out RectI header);
        string glyph = state.IsOpen ? "-" : "+";
        if (Button(id, header, $"{glyph} {title}", state.IsOpen).Clicked)
        {
            state.IsOpen = !state.IsOpen;
        }

        if (!state.IsOpen)
        {
            content = new RectI(header.X, header.Bottom, header.Width, 0);
            return false;
        }

        column = column.Next(openHeight, out content);
        return true;
    }

    public UiTextBoxResult TextBox(UiId id, RectI rect, string initialValue = "", string placeholder = "", UiTextBoxOptions options = default)
    {
        UiWidgetState state = GetState(id);
        if (state.Text.Length == 0 && initialValue.Length > 0)
        {
            state.Text = initialValue;
        }

        UiTextBoxStyle style = Theme.TextBox;
        bool hovered = _inputEnabled && _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
        bool clicked = hovered && _pointer.LeftPressed && !_previousPointer.LeftPressed;
        if (hovered)
        {
            _hot = id.Value;
        }
        if (clicked && !options.ReadOnly)
        {
            _focused = id.Value;
        }
        else if (_inputEnabled && _pointer.IsInside && _pointer.LeftPressed && !_previousPointer.LeftPressed && !hovered && _focused == id.Value)
        {
            _focused = null;
        }

        bool focused = _focused == id.Value;
        bool changed = false;
        bool submitted = false;
        if (_inputEnabled && focused && !options.ReadOnly && _textInput.Pressed && _textInput.Serial != 0 && _textInput.Serial != _handledTextSerial)
        {
            _handledTextSerial = _textInput.Serial;
            changed = ApplyTextInput(state, options, out submitted);
        }

        uint fill = focused ? style.Colors.SurfaceActive : hovered ? style.Colors.SurfaceHot : style.Colors.Surface;
        uint border = focused ? style.Colors.BorderActive : hovered ? style.Colors.BorderHot : style.Colors.Border;
        _canvas.FillRect(rect.X, rect.Y, rect.Width, rect.Height, fill);
        for (int i = 0; i < Math.Max(1, style.BorderThickness); i++)
        {
            _canvas.DrawRect(rect.X + i, rect.Y + i, rect.Width - i * 2, rect.Height - i * 2, border);
        }

        string display = options.Password && state.Text.Length > 0
            ? new string('*', state.Text.Length)
            : state.Text;
        uint textColor = state.Text.Length == 0 ? style.Colors.MutedText : style.Colors.Text;
        _canvas.DrawText(rect.X + style.PaddingX, rect.Y + style.PaddingY, display.Length == 0 ? placeholder : display, textColor);

        if (focused)
        {
            int cursorX = Math.Min(rect.Right - 4, rect.X + style.PaddingX + display.Length * 6 + 1);
            _canvas.FillRect(cursorX, rect.Y + 4, style.CursorWidth, rect.Height - 8, style.Colors.Accent);
        }

        return new UiTextBoxResult(state.Text, hovered, focused, changed, submitted);
    }

    public void SetText(UiId id, string text)
    {
        GetState(id).Text = text;
    }

    public string GetText(UiId id)
    {
        return GetState(id).Text;
    }

    public UiScrollArea BeginScrollArea(UiId id, RectI viewport, int contentHeight)
    {
        UiWidgetState state = GetState(id);
        bool hovered = _inputEnabled && _pointer.IsInside && viewport.Contains(_pointer.X, _pointer.Y);
        int maxOffset = Math.Max(0, contentHeight - viewport.Height);
        if (hovered)
        {
            _hot = id.Value;
        }
        if (hovered && _scrollInput.Serial != 0 && _scrollInput.Serial != _handledScrollSerial)
        {
            _handledScrollSerial = _scrollInput.Serial;
            state.ScrollOffset = Math.Clamp(state.ScrollOffset + _scrollInput.Delta, 0, maxOffset);
        }
        state.ScrollOffset = Math.Clamp(state.ScrollOffset, 0, maxOffset);

        if (maxOffset > 0)
        {
            int thumbHeight = Math.Max(12, viewport.Height * viewport.Height / contentHeight);
            int trackTravel = Math.Max(1, viewport.Height - thumbHeight);
            int trackX = viewport.Right - 7;
            int thumbY = viewport.Y + state.ScrollOffset * Math.Max(1, viewport.Height - thumbHeight) / maxOffset;
            RectI track = new(trackX, viewport.Y, 6, viewport.Height);
            RectI thumb = new(trackX, thumbY, 6, thumbHeight);
            bool thumbHovered = _inputEnabled && _pointer.IsInside && thumb.Contains(_pointer.X, _pointer.Y);
            bool trackHovered = _inputEnabled && _pointer.IsInside && track.Contains(_pointer.X, _pointer.Y);
            string scrollId = id.Value + ".scrollbar";

            if (thumbHovered || state.IsDraggingScroll)
            {
                _hot = scrollId;
            }

            if (_pointer.LeftPressed && !_previousPointer.LeftPressed && thumbHovered)
            {
                _active = scrollId;
                state.IsDraggingScroll = true;
                state.ScrollDragStartY = _pointer.Y;
                state.ScrollDragStartOffset = state.ScrollOffset;
            }
            else if (_pointer.LeftPressed && !_previousPointer.LeftPressed && trackHovered)
            {
                _active = scrollId;
                state.ScrollOffset = Math.Clamp((_pointer.Y - viewport.Y - thumbHeight / 2) * maxOffset / trackTravel, 0, maxOffset);
                state.IsDraggingScroll = true;
                state.ScrollDragStartY = _pointer.Y;
                state.ScrollDragStartOffset = state.ScrollOffset;
            }

            if (state.IsDraggingScroll)
            {
                if (_pointer.LeftPressed)
                {
                    _active = scrollId;
                    int deltaY = _pointer.Y - state.ScrollDragStartY;
                    state.ScrollOffset = Math.Clamp(state.ScrollDragStartOffset + deltaY * maxOffset / trackTravel, 0, maxOffset);
                }
                else
                {
                    state.IsDraggingScroll = false;
                }
            }

            thumbY = viewport.Y + state.ScrollOffset * trackTravel / maxOffset;
            _canvas.FillRect(track.X, track.Y, track.Width, track.Height, Theme.Button.Colors.Surface);
            uint thumbColor = state.IsDraggingScroll
                ? Theme.Button.Colors.BorderActive
                : thumbHovered || hovered
                    ? Theme.Button.Colors.BorderHot
                    : Theme.Button.Colors.Border;
            _canvas.FillRect(track.X, thumbY, track.Width, thumbHeight, thumbColor);
        }
        else
        {
            state.IsDraggingScroll = false;
        }

        IDisposable clip = _canvas.PushClip(viewport);
        var content = new RectI(viewport.X, viewport.Y - state.ScrollOffset, Math.Max(1, viewport.Width - (maxOffset > 0 ? 8 : 0)), contentHeight);
        return new UiScrollArea(content, clip);
    }


    private void PopInputEnabled()
    {
        _inputEnabled = _inputEnabledStack.Count > 0 ? _inputEnabledStack.Pop() : true;
    }

    private sealed class InputScope : IDisposable
    {
        private UiContext? _context;

        public InputScope(UiContext context)
        {
            _context = context;
        }

        public void Dispose()
        {
            _context?.PopInputEnabled();
            _context = null;
        }
    }

    private bool ApplyTextInput(UiWidgetState state, UiTextBoxOptions options, out bool submitted)
    {
        submitted = false;
        switch (_textInput.KeyCode)
        {
            case 14:
                if (state.Text.Length > 0)
                {
                    state.Text = state.Text[..^1];
                    return true;
                }
                return false;
            case 28:
                submitted = true;
                return false;
            default:
                char? ch = TryMapKey(_textInput.KeyCode);
                if (ch is null || state.Text.Length >= Math.Max(1, options.MaxLength))
                {
                    return false;
                }
                if (options.Numeric && !(char.IsDigit(ch.Value) || ch.Value == '.' || ch.Value == '-'))
                {
                    return false;
                }
                state.Text += ch.Value;
                return true;
        }
    }

    private static char? TryMapKey(uint keyCode) => keyCode switch
    {
        >= 16 and <= 25 => "qwertyuiop"[(int)keyCode - 16],
        >= 30 and <= 38 => "asdfghjkl"[(int)keyCode - 30],
        >= 44 and <= 50 => "zxcvbnm"[(int)keyCode - 44],
        >= 2 and <= 10 => "123456789"[(int)keyCode - 2],
        11 => '0',
        12 => '-',
        13 => '=',
        39 => ';',
        40 => '\'',
        51 => ',',
        52 => '.',
        53 => '/',
        57 => ' ',
        _ => null,
    };

    private UiWidgetState GetState(UiId id)
    {
        if (!_state.TryGetValue(id.Value, out UiWidgetState? state))
        {
            state = new UiWidgetState();
            _state[id.Value] = state;
        }

        return state;
    }
}

public enum UiTextKind
{
    Normal,
    Muted,
    Accent,
}

public readonly record struct UiRow(int NextX, int Y, int Height, int Gap)
{
    public UiRow Next(int width, out RectI rect)
    {
        rect = new RectI(NextX, Y, width, Height);
        return this with { NextX = NextX + width + Gap };
    }
}

public readonly record struct UiColumn(int X, int NextY, int Width, int Gap)
{
    public UiColumn Next(int height, out RectI rect)
    {
        rect = new RectI(X, NextY, Width, height);
        return this with { NextY = NextY + height + Gap };
    }
}
