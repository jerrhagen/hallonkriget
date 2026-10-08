using System.IO;
using Godot;
using Hallonkriget.Game.Net;
using Hallonkriget.Game.View;
using Hallonkriget.Sim.Advice;

namespace Hallonkriget.Game.Ui;

/// <summary>
/// Sixten (eller Major) i nedre högra hörnet, med pratbubblan. Rådgivaren i sim/Advice bestämmer
/// vad som sägs och när; här ritas det. Pratet kan slås av med knappen, varningarna inte.
/// </summary>
public partial class AdvisorCorner : CanvasLayer
{
    /// <summary>Hur länge bubblan syns, i sekunder.</summary>
    private const double BubbleSeconds = 14;

    private LocalMatch? _match;
    private Advisor? _advisor;
    private Remark? _last;
    private double _shown;
    private PanelContainer _bubble = null!;
    private Label _text = null!;
    private Label _name = null!;
    private TextureRect _portrait = null!;
    private Button _mute = null!;

    /// <summary>Kopplar rådgivaren till matchen. Kan göras innan noden är i trädet.</summary>
    public void Attach(LocalMatch match)
    {
        if (_match is not null) _match.Ticked -= OnTick;
        _match = match;
        var faction = match.State.Players[match.LocalPlayer].Faction;
        var lines = AdvisorLines.Parse(File.ReadAllText(Path.Combine(GameFiles.DataDir, "radgivare.json")), faction);
        bool chatter = _advisor?.Chatter ?? true;
        _advisor = new Advisor(lines, match.LocalPlayer) { Chatter = chatter };
        _last = null;
        match.Ticked += OnTick;
        if (IsInsideTree()) Refresh();
    }

    private void OnTick()
    {
        if (_advisor!.Update(_match!.State) is not { } remark) return;
        _last = remark;
        _shown = 0;
        if (IsInsideTree()) Refresh();
    }

    public override void _Ready()
    {
        Layer = 5;
        var root = new Control { Theme = Style.Build(), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var corner = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        corner.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        corner.GrowHorizontal = Control.GrowDirection.Begin;
        corner.GrowVertical = Control.GrowDirection.Begin;
        corner.OffsetRight = -8;
        corner.OffsetBottom = -8;
        root.AddChild(corner);

        _bubble = new PanelContainer { CustomMinimumSize = new Vector2(330, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        corner.AddChild(_bubble);
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(310, 0) };
        _bubble.AddChild(_text);

        var right = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        corner.AddChild(right);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(112, 112),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        right.AddChild(_portrait);
        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        right.AddChild(_name);
        _mute = new Button { ToggleMode = true, TooltipText = "Slår av pratet. Varningarna kommer ändå." };
        _mute.Toggled += on =>
        {
            if (_advisor is not null) _advisor.Chatter = !on;
            Refresh();
        };
        right.AddChild(_mute);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_last is null || !_bubble.Visible || _match is null || _match.Speed == 0) return;
        _shown += delta;
        if (_shown > BubbleSeconds) _bubble.Visible = false;
    }

    private void Refresh()
    {
        if (_advisor is null) return;
        bool sixten = _advisor.Name == "Sixten";
        _portrait.Texture = GD.Load<Texture2D>(sixten ? "res://art/ui/sixten@2x.png" : "res://art/ui/major@2x.png");
        _name.Text = _advisor.Name;
        _mute.Text = _advisor.Chatter ? "Prat: på" : "Prat: av";
        _mute.SetPressedNoSignal(!_advisor.Chatter);
        _bubble.Visible = _last is not null && _shown <= BubbleSeconds;
        if (_last is null) return;
        _text.Text = _last.Text;
        bool warning = _last.Warning is not null;
        _bubble.AddThemeStyleboxOverride("panel", Style.Box(warning ? new Color("f6dccb") : Style.Paper, warning ? 3 : 2, 10));
        _text.AddThemeColorOverride("font_color", warning ? Style.Red.Darkened(0.3f) : Style.Ink);
    }
}
