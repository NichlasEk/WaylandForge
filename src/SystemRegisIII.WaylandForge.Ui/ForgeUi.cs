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
        new UiTextStyle(1, new UiColors(0xffe8edf2, 0xff91a1ad, 0xff181d22, 0xff111318, 0xff28333d, 0xff355c7d, 0xff39424c, 0xff91a1ad, 0xff82cfff, 0xffffc857)));

    public static UiTheme HighContrast { get; } = new(
        name: "HIGH",
        new UiButtonStyle(1, 6, 4, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)),
        new UiPanelStyle(1, 8, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)),
        new UiTextStyle(1, new UiColors(0xffffffff, 0xffb8c7d9, 0xff05070a, 0xff000000, 0xff1c2b38, 0xff114b72, 0xff7aa9d6, 0xffffffff, 0xff8fd3ff, 0xffffe66d)));

    public static UiTheme Warm { get; } = new(
        name: "WARM",
        new UiButtonStyle(1, 6, 4, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)),
        new UiPanelStyle(1, 8, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)),
        new UiTextStyle(1, new UiColors(0xfffff4df, 0xffc9a987, 0xff201a17, 0xff151110, 0xff342923, 0xff574035, 0xff6b5548, 0xffc79d70, 0xffffc48a, 0xffffb15f)));

    public static IReadOnlyList<UiTheme> BuiltIns { get; } = [Default, HighContrast, Warm];

    public UiTheme(string name, UiButtonStyle button, UiPanelStyle panel, UiTextStyle text)
    {
        Name = name;
        Button = button;
        Panel = panel;
        Text = text;
    }

    public string Name { get; }
    public UiButtonStyle Button { get; }
    public UiPanelStyle Panel { get; }
    public UiTextStyle Text { get; }
}

public readonly record struct UiButtonResult(bool Hovered, bool Pressed, bool Clicked);

public readonly record struct UiId(string Value);

internal sealed class UiWidgetState
{
    public bool IsOpen { get; set; } = true;
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
        _hot = null;
        _pointer = pointer;
        _previousPointer = previousPointer;
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
