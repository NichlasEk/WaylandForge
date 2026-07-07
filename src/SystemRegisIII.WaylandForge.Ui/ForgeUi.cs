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
public readonly record struct UiTextBoxOptions(bool ReadOnly = false, bool Numeric = false, bool Password = false, int MaxLength = 32);
public readonly record struct UiTextBoxResult(string Text, bool Hovered, bool Focused, bool Changed, bool Submitted);
public readonly record struct TextInputEvent(uint KeyCode, uint Serial);
public readonly record struct ScrollInputEvent(int Delta, uint Serial);
public readonly record struct UiScrollArea(RectI Content, IDisposable Clip) : IDisposable
{
    public void Dispose()
    {
        Clip.Dispose();
    }
}

public readonly record struct UiId(string Value);

internal sealed class UiWidgetState
{
    public bool IsOpen { get; set; } = true;
    public string Text { get; set; } = string.Empty;
    public int ScrollOffset { get; set; }
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
    }

    public UiButtonResult Button(RectI rect, string label, bool active = false)
    {
        return Button(new UiId(label), rect, label, active);
    }

    public UiButtonResult Button(UiId id, RectI rect, string label, bool active = false)
    {
        UiButtonStyle style = Theme.Button;
        bool hovered = _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
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
        bool hovered = _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
        bool clicked = hovered && _pointer.LeftPressed && !_previousPointer.LeftPressed;
        if (hovered)
        {
            _hot = id.Value;
        }
        if (clicked && !options.ReadOnly)
        {
            _focused = id.Value;
        }
        else if (_pointer.IsInside && _pointer.LeftPressed && !_previousPointer.LeftPressed && !hovered && _focused == id.Value)
        {
            _focused = null;
        }

        bool focused = _focused == id.Value;
        bool changed = false;
        bool submitted = false;
        if (focused && !options.ReadOnly && _textInput.Serial != 0 && _textInput.Serial != _handledTextSerial)
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
        bool hovered = _pointer.IsInside && viewport.Contains(_pointer.X, _pointer.Y);
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
            int trackX = viewport.Right - 5;
            _canvas.FillRect(trackX, viewport.Y, 4, viewport.Height, Theme.Button.Colors.Surface);
            int thumbHeight = Math.Max(12, viewport.Height * viewport.Height / contentHeight);
            int thumbY = viewport.Y + state.ScrollOffset * Math.Max(1, viewport.Height - thumbHeight) / maxOffset;
            _canvas.FillRect(trackX, thumbY, 4, thumbHeight, hovered ? Theme.Button.Colors.BorderHot : Theme.Button.Colors.Border);
        }

        IDisposable clip = _canvas.PushClip(viewport);
        var content = new RectI(viewport.X, viewport.Y - state.ScrollOffset, Math.Max(1, viewport.Width - (maxOffset > 0 ? 8 : 0)), contentHeight);
        return new UiScrollArea(content, clip);
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
