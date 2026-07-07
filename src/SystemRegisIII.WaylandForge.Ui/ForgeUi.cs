namespace SystemRegisIII.WaylandForge.Ui;

public readonly record struct UiColors(
    uint Text,
    uint MutedText,
    uint Surface,
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

public sealed class UiTheme
{
    public static UiTheme Default { get; } = new(
        new UiButtonStyle(
            BorderThickness: 1,
            PaddingX: 6,
            PaddingY: 4,
            new UiColors(
                Text: 0xffe8edf2,
                MutedText: 0xff91a1ad,
                Surface: 0xff181d22,
                SurfaceHot: 0xff28333d,
                SurfaceActive: 0xff355c7d,
                Border: 0xff39424c,
                BorderHot: 0xff91a1ad,
                BorderActive: 0xff82cfff,
                Accent: 0xffffc857)));

    public UiTheme(UiButtonStyle button)
    {
        Button = button;
    }

    public UiButtonStyle Button { get; }
}

public readonly record struct UiButtonResult(bool Hovered, bool Pressed, bool Clicked);

public sealed class UiContext
{
    private readonly SoftwareCanvas _canvas;
    private readonly UiTheme _theme;
    private PointerState _pointer;
    private PointerState _previousPointer;

    public UiContext(SoftwareCanvas canvas, UiTheme theme)
    {
        _canvas = canvas;
        _theme = theme;
    }

    public void BeginFrame(PointerState pointer, PointerState previousPointer)
    {
        _pointer = pointer;
        _previousPointer = previousPointer;
    }

    public UiButtonResult Button(RectI rect, string label, bool active = false)
    {
        UiButtonStyle style = _theme.Button;
        bool hovered = _pointer.IsInside && rect.Contains(_pointer.X, _pointer.Y);
        bool pressed = hovered && _pointer.LeftPressed;
        bool clicked = hovered && _pointer.LeftPressed && !_previousPointer.LeftPressed;

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
}
