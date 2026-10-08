using System.Collections.Generic;
using Godot;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Data;

namespace Hallonkriget.Game.View;

/// <summary>
/// En byggnad. Fas 2 ritar platshållare: ett litet hus i byggnadens färg med namnet på, som
/// implementationsplanen säger. Hönshuset har sin riktiga bild från stilprovet. Byggplatser ritas
/// som stolpar och en stomme som växer med bygget.
///
/// Noden står vid fotavtryckets nederkant, så att den sorteras rätt mot personerna.
/// </summary>
public partial class BuildingView : Node2D
{
    private const int T = WorldView.Tile;

    public Building Building { get; }
    private readonly GameData _data;
    private Sprite2D? _art;
    private BuildingStage _stage;
    private int _work = -1;
    private bool _selected;

    /// <summary>Färgen på väggarna efter vad byggnaden gör. Bara utseende, inte speldata.</summary>
    private static readonly Dictionary<string, string> Group = new()
    {
        ["stugan"] = "hem", ["mangardsbyggnaden"] = "hem", ["boden"] = "hem", ["kafferepet"] = "hem",
        ["bygdegarden"] = "hem", ["lanthandeln"] = "hem", ["mjolkpallen"] = "hem",
        ["skogshuggarkojan"] = "ravara", ["stenrojarboden"] = "ravara", ["brunnen"] = "ravara", ["akern"] = "ravara",
        ["angen"] = "ravara", ["fiskeboden"] = "ravara", ["barplockarstugan"] = "ravara", ["skrotboden"] = "ravara",
        ["bikuporna"] = "ravara",
        ["honshuset"] = "djur", ["gashagen"] = "djur", ["grisstian"] = "djur", ["ladugarden"] = "djur", ["farhagen"] = "djur",
        ["kvarnen"] = "mat", ["bagarstugan"] = "mat", ["koket"] = "mat", ["mejeristugan"] = "mat", ["syltkoket"] = "mat",
        ["slakteriet"] = "mat", ["rokeriet"] = "mat", ["bryggstugan"] = "mat", ["rosteriet"] = "mat",
        ["logen"] = "krig", ["vedtraven"] = "krig", ["hundkojan"] = "krig", ["hundgarden"] = "krig",
    };

    private static readonly Dictionary<string, Color> Walls = new()
    {
        ["hem"] = new Color("e9dcc0"),     // knutvirke
        ["ravara"] = new Color("c9b48a"),  // halm och trä
        ["djur"] = new Color("d8c9a4"),
        ["mat"] = new Color("e2c79a"),
        ["krig"] = new Color("a9a39a"),    // granit
        ["hantverk"] = new Color("b8a58a"), // patinerat trä
    };

    public BuildingView(Building building, GameData data)
    {
        Building = building;
        _data = data;
        Name = $"{building.Def.Id}_{building.Id}";
        var o = building.Origin;
        Position = new Vector2((o.X + building.Def.Width / 2f) * T, (o.Y + building.Def.Height) * T);
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    public void Sync()
    {
        if (Building.Stage == _stage && Building.WorkDone == _work) return;
        _stage = Building.Stage;
        _work = Building.WorkDone;
        if (_stage == BuildingStage.Done && _art is null && Building.Def.Id == "honshuset")
        {
            // Bilden är 256×352; fotavtrycket är de nedersta 256 pixlarna.
            _art = new Sprite2D
            {
                Texture = GD.Load<Texture2D>("res://art/buildings/honshus@2x.png"),
                Centered = false,
                Offset = new Vector2(-T, -352) * 2,
                Scale = new Vector2(0.5f, 0.5f),
            };
            AddChild(_art);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var def = Building.Def;
        float w = def.Width * T, h = def.Height * T;
        var ink = new Color("2b2119");
        var font = ThemeDB.FallbackFont;

        if (_selected)
            DrawArc(new Vector2(0, -h / 2), Mathf.Max(w, h) * 0.62f, 0, Mathf.Tau, 48, new Color(0.95f, 0.85f, 0.4f, 0.9f), 6);

        // Fotavtrycket som en mörkare fläck på marken.
        DrawRect(new Rect2(-w / 2 + 6, -h + 6, w - 12, h - 12), new Color(0.3f, 0.25f, 0.18f, 0.18f));

        float progress = def.BuildTicks == 0 ? 1 : Building.WorkDone / (float)def.BuildTicks;
        if (Building.Stage == BuildingStage.Construction)
        {
            DrawConstruction(w, h, progress, ink);
        }
        else if (_art is null)
        {
            DrawHouse(w, h, ink);
        }

        // Namnet på en lapp under huset.
        string label = def.Name;
        int size = def.Width == 1 ? 22 : 28;
        var textSize = font.GetStringSize(label, HorizontalAlignment.Center, -1, size);
        var lapp = new Rect2(-textSize.X / 2 - 8, -26 - size, textSize.X + 16, size + 12);
        DrawRect(lapp, new Color(0.97f, 0.94f, 0.85f, 0.92f));
        DrawRect(lapp, new Color(ink, 0.6f), false, 2);
        DrawString(font, new Vector2(-textSize.X / 2, -26 - 6), label, HorizontalAlignment.Left, -1, size, ink);
    }

    private void DrawHouse(float w, float h, Color ink)
    {
        var def = Building.Def;
        var wall = Walls[Group.TryGetValue(def.Id, out var g) ? g : "hantverk"];
        // Torpets tak är falurött, Storgårdens grått; byggnader för båda får tegel.
        var roof = def.Faction switch
        {
            FactionRule.Storgarden => new Color("8f897b"),
            FactionRule.Torpet => new Color("a8402c"),
            _ => new Color("9b5a3c"),
        };
        float margin = 14;
        float wallTop = -h * 0.62f;
        var wallRect = new Rect2(-w / 2 + margin, wallTop, w - 2 * margin, h * 0.62f - margin);
        DrawRect(wallRect, wall);
        DrawRect(wallRect, ink, false, 3);
        // Taket: en trapets som sticker upp ovanför fotavtrycket, som i snett ovanifrån.
        float rise = h * 0.55f;
        var roofPts = new[]
        {
            new Vector2(-w / 2 + margin - 10, wallTop + 4),
            new Vector2(-w / 2 + margin + w * 0.12f, wallTop - rise),
            new Vector2(w / 2 - margin - w * 0.12f, wallTop - rise),
            new Vector2(w / 2 - margin + 10, wallTop + 4),
        };
        DrawColoredPolygon(roofPts, roof);
        DrawPolyline(new[] { roofPts[0], roofPts[1], roofPts[2], roofPts[3], roofPts[0] }, ink, 3);
        // Dörren mitt på, mot dörrutan under huset.
        float dw = Mathf.Min(34, w * 0.22f);
        var door = new Rect2(-dw / 2, -margin - dw * 1.5f, dw, dw * 1.5f);
        DrawRect(door, new Color("5a4a3b"));
        DrawRect(door, ink, false, 2);
    }

    private void DrawConstruction(float w, float h, float progress, Color ink)
    {
        var wood = new Color("8f6a45");
        float margin = 16;
        float left = -w / 2 + margin, right = w / 2 - margin, bottom = -margin, top = -h * 0.62f;
        // Stolpar i hörnen, som växer med bygget, och en stomme när halva bygget är klart.
        float postTop = bottom + (top - bottom) * Mathf.Clamp(progress * 2, 0.15f, 1f);
        foreach (float x in new[] { left, right })
        {
            DrawLine(new Vector2(x, bottom), new Vector2(x, postTop), wood, 8);
            DrawLine(new Vector2(x, bottom), new Vector2(x, postTop), ink, 2);
        }
        if (progress > 0.5f)
        {
            DrawLine(new Vector2(left, top), new Vector2(right, top), wood, 7);
            DrawLine(new Vector2(left, top), new Vector2(0, top - h * 0.55f * (progress - 0.5f) * 2), wood, 6);
            DrawLine(new Vector2(right, top), new Vector2(0, top - h * 0.55f * (progress - 0.5f) * 2), wood, 6);
        }
        // Hur mycket material som kommit, som högar framför bygget.
        int delivered = 0;
        foreach (var c in Building.Def.Cost) delivered += c.Count - Building.MaterialNeeded(c.Good);
        for (int i = 0; i < delivered; i++)
            DrawRect(new Rect2(left + 4 + (i % 8) * 12, bottom - 14 - (i / 8) * 8, 10, 6), i % 2 == 0 ? new Color("c9a56f") : new Color("a29c93"));
        // Förloppet som en tunn stapel.
        var bar = new Rect2(left, bottom + 6, right - left, 8);
        DrawRect(bar, new Color(0.95f, 0.92f, 0.82f, 0.9f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * progress, bar.Size.Y)), new Color("6c7d3a"));
        DrawRect(bar, ink, false, 1.5f);
    }
}
