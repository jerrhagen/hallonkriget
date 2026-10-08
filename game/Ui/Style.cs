using Godot;

namespace Hallonkriget.Game.Ui;

/// <summary>
/// Gränssnittets utseende tills det riktiga ritas: papper, bläck och emaljskyltar.
/// Designdokumentet vill ha en sliten trälåda med emaljskyltar; det kommer i fas 5.
/// </summary>
public static class Style
{
    public static readonly Color Ink = new("2b2119");
    public static readonly Color Paper = new("f4ecd9");
    public static readonly Color PaperDark = new("e4d6b6");
    public static readonly Color Wood = new("8f6a45");
    public static readonly Color Red = new("a8402c");
    public static readonly Color Green = new("6c7d3a");
    public static readonly Color Enamel = new("5d7ea6");

    public static Theme Build()
    {
        var theme = new Theme { DefaultFontSize = 15 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", Paper);
        theme.SetColor("font_focus_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color(Ink, 0.4f));
        theme.SetStylebox("normal", "Button", Box(PaperDark, 1.5f));
        theme.SetStylebox("hover", "Button", Box(new Color("efe2c2"), 2));
        theme.SetStylebox("pressed", "Button", Box(Wood, 2));
        theme.SetStylebox("disabled", "Button", Box(new Color(PaperDark, 0.5f), 1));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetStylebox("panel", "PanelContainer", Box(Paper, 3, 10));
        theme.SetConstant("separation", "VBoxContainer", 4);
        theme.SetConstant("separation", "HBoxContainer", 4);
        theme.SetConstant("h_separation", "GridContainer", 4);
        theme.SetConstant("v_separation", "GridContainer", 4);
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, float border, int padding = 5)
    {
        var box = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = Ink,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        box.SetBorderWidthAll((int)Mathf.Ceil(border));
        box.SetContentMarginAll(padding);
        return box;
    }

    public static Label Title(string text) => new() { Text = text, ThemeTypeVariation = "", LabelSettings = new LabelSettings { FontSize = 20, FontColor = Ink } };

    public static Label Small(string text) => new() { Text = text, LabelSettings = new LabelSettings { FontSize = 13, FontColor = new Color(Ink, 0.8f) }, AutowrapMode = TextServer.AutowrapMode.WordSmart };
}
