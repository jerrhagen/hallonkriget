using System;
using System.Collections.Generic;
using Godot;

namespace Hallonkriget.Game;

/// <summary>
/// Fas 0:s stilprov: en bit äng, ett hönshus, två pysslingar som bär ägg, och pappret över allt.
/// Kontrollfrågan är om det ser ut som en bilderbok.
///
/// Styrning: mushjul eller +/- zoomar, piltangenter eller WASD flyttar, mellanslag slår av
/// och på pappret.
///
/// Från kommandoraden, efter "--": --skarmbild=fil.png tar en bild och avslutar,
/// --zoom=1.0 sätter zoomen, --kamera=x,y flyttar kameran, --utan-papper stänger av pappret.
/// </summary>
public partial class Stilprov : Node2D
{
    private const int Tile = 128;          // 1×-grafik: 128 px per ruta
    private const int MapW = 26, MapH = 16;
    private static readonly Vector2I HonshusTile = new(11, 6); // övre vänstra rutan i fotavtrycket

    private Camera2D _camera = null!;
    private CanvasLayer _paper = null!;
    private readonly List<Walker> _walkers = new();

    private string? _screenshotPath;
    private int _framesUntilScreenshot = 45;

    public override void _Ready()
    {
        var args = ParseArgs();
        AddGround();
        var path = LoadPath(out int ramp);
        AddDecal("res://art/terrain/stig_prov@2x.png", StigOrigin);
        AddTone();

        var objects = new Node2D { Name = "Objekt", YSortEnabled = true };
        AddChild(objects);
        AddHonshus(objects);

        // Två pysslingar på stigen: en bär ägg från hönstrappan åt höger, en hämtar från vänster.
        AddWalker(objects, path[ramp..], rampAtStart: true, startAt: 0.1f);
        AddWalker(objects, path[..(ramp + 1)], rampAtStart: false, startAt: 0.55f);

        _camera = new Camera2D { Position = HonshusFoot() + new Vector2(60, -60) };
        AddChild(_camera);
        if (args.TryGetValue("kamera", out var cam))
        {
            var xy = cam.Split(',');
            _camera.Position = new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                                           float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        _camera.Zoom = Vector2.One * (args.TryGetValue("zoom", out var z) ? float.Parse(z, System.Globalization.CultureInfo.InvariantCulture) : 1.0f);

        AddHelpText();
        AddPaper();
        _paper.Visible = !args.ContainsKey("utan-papper");

        if (args.TryGetValue("skarmbild", out var shot)) _screenshotPath = shot;
        if (args.TryGetValue("vanta", out var w)) _framesUntilScreenshot = int.Parse(w);
    }

    public override void _Process(double delta)
    {
        foreach (var w in _walkers) w.Update((float)delta);

        var pan = Vector2.Zero;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A)) pan.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) pan.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)) pan.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)) pan.Y += 1;
        _camera.Position += pan * 600 * (float)delta / _camera.Zoom.X;

        if (_screenshotPath is not null && --_framesUntilScreenshot <= 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
            GD.Print($"Skärmbild sparad: {_screenshotPath}");
            GetTree().Quit();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Space) _paper.Visible = !_paper.Visible;
            if (key.Keycode is Key.Plus or Key.KpAdd or Key.Equal) ZoomBy(1.25f);
            if (key.Keycode is Key.Minus or Key.KpSubtract) ZoomBy(0.8f);
        }
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) ZoomBy(1.1f);
            if (mb.ButtonIndex == MouseButton.WheelDown) ZoomBy(1 / 1.1f);
        }
    }

    private void ZoomBy(float factor)
    {
        // 0,5 är normal zoom i spelet (64 px per ruta på skärmen), 2 är närmast.
        float z = Mathf.Clamp(_camera.Zoom.X * factor, 0.35f, 2.0f);
        _camera.Zoom = new Vector2(z, z);
    }

    private static Vector2 StigOrigin;

    private static Vector2[] LoadPath(out int ramp)
    {
        var json = System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://art/terrain/stig_prov.json")).RootElement;
        var o = json.GetProperty("origin");
        StigOrigin = new Vector2(o[0].GetSingle(), o[1].GetSingle());
        var pts = new List<Vector2>();
        foreach (var p in json.GetProperty("points").EnumerateArray())
            pts.Add(new Vector2(p[0].GetSingle(), p[1].GetSingle()));
        ramp = json.GetProperty("ramp_index").GetInt32();
        return pts.ToArray();
    }

    /// <summary>En bild i 2× som läggs ut i världen med övre vänstra hörnet vid position.</summary>
    private void AddDecal(string texture, Vector2 position)
    {
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>(texture),
            Centered = false,
            Position = position,
            Scale = new Vector2(0.5f, 0.5f),
        });
    }

    /// <summary>Storskalig ton som multipliceras över marken, så att ängen inte blir en tapet.</summary>
    private void AddTone()
    {
        var tex = GD.Load<Texture2D>("res://art/terrain/ang_ton.png");
        AddChild(new Sprite2D
        {
            Name = "Ton",
            Texture = tex,
            Centered = false,
            Scale = new Vector2(MapW * Tile / (float)tex.GetWidth(), MapH * Tile / (float)tex.GetHeight()),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul },
        });
    }

    private static Vector2 HonshusFoot() =>
        new((HonshusTile.X + 1) * Tile, (HonshusTile.Y + 2) * Tile);

    private void AddGround()
    {
        // 2×-grafiken skalas ner till hälften, så att den är skarp även inzoomad.
        var tileSet = new TileSet { TileSize = new Vector2I(Tile * 2, Tile * 2) };
        var source = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>("res://art/terrain/ang_atlas@2x.png"),
            TextureRegionSize = new Vector2I(Tile * 2, Tile * 2),
        };
        for (int v = 0; v < 4; v++) source.CreateTile(new Vector2I(v, 0));
        int sourceId = tileSet.AddSource(source);

        var layer = new TileMapLayer { Name = "Mark", TileSet = tileSet, Scale = new Vector2(0.5f, 0.5f) };
        AddChild(layer);
        for (int y = 0; y < MapH; y++)
        for (int x = 0; x < MapW; x++)
        {
            // Samma variant på samma ruta varje gång, men utan synligt mönster.
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            int variant = (int)((h >> 7) % 10) switch { < 4 => 0, < 7 => 1, < 9 => 2, _ => 3 };
            layer.SetCell(new Vector2I(x, y), sourceId, new Vector2I(variant, 0));
        }
    }

    private void AddHonshus(Node2D parent)
    {
        // Bilden är 256×352; fotavtrycket är de nedersta 256 pixlarna. Noden står vid
        // fotavtryckets nederkant, så att den sorteras rätt mot pysslingarna.
        var sprite = new Sprite2D
        {
            Name = "Honshus",
            Texture = GD.Load<Texture2D>("res://art/buildings/honshus@2x.png"),
            Centered = false,
            Offset = new Vector2(-Tile, -352) * 2,
            Position = HonshusFoot(),
            Scale = new Vector2(0.5f, 0.5f),
        };
        parent.AddChild(sprite);
    }

    private void AddWalker(Node2D parent, Vector2[] path, bool rampAtStart, float startAt)
    {
        var p = new Pyssling { Name = "Pyssling", Position = path[0] };
        parent.AddChild(p);
        _walkers.Add(new Walker(p, path, rampAtStart, startAt));
    }

    private void AddHelpText()
    {
        var layer = new CanvasLayer { Layer = 5 };
        AddChild(layer);
        layer.AddChild(new Label
        {
            Text = "Hallonkriget · stilprov\nhjul: zoom   pilar: flytta   mellanslag: papper av/på",
            Position = new Vector2(24, 20),
            Modulate = new Color(0.17f, 0.13f, 0.1f, 0.85f),
        });
    }

    private void AddPaper()
    {
        _paper = new CanvasLayer { Name = "Papper", Layer = 10 };
        AddChild(_paper);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/papper.gdshader") };
        material.SetShaderParameter("paper_texture", GD.Load<Texture2D>("res://art/ui/papper.png"));
        var rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _paper.AddChild(rect);
    }

    /// <summary>
    /// En pyssling som går fram och tillbaka längs stigen, 1,2 rutor per sekund.
    /// Vid hönstrappan tar den ett ägg, i andra änden lämnar den det.
    /// </summary>
    private sealed class Walker
    {
        private const float Speed = 1.2f * Tile;
        private readonly Pyssling _p;
        private readonly Vector2[] _path;
        private readonly bool _rampAtStart;
        private int _target;
        private int _step = 1;
        private float _pause;

        public Walker(Pyssling p, Vector2[] path, bool rampAtStart, float startAt)
        {
            _p = p;
            _path = path;
            _rampAtStart = rampAtStart;
            int start = (int)(startAt * (path.Length - 1));
            _target = start + 1;
            p.Position = path[start];
            // Halvvägs från trappan har den ägget med sig, på väg tillbaka inte.
            p.SetCarryingEgg(rampAtStart);
        }

        public void Update(float dt)
        {
            if (_pause > 0)
            {
                _pause -= dt;
                _p.SetWalking(0);
                return;
            }
            float budget = Speed * dt;
            while (budget > 0)
            {
                var to = _path[_target] - _p.Position;
                float dist = to.Length();
                if (dist <= budget)
                {
                    _p.Position = _path[_target];
                    budget -= dist;
                    if (_target == _path.Length - 1 || _target == 0)
                    {
                        bool atRamp = (_target == 0) == _rampAtStart;
                        _p.SetCarryingEgg(atRamp);
                        _step = -_step;
                        _pause = 0.8f;
                        _target += _step;
                        return;
                    }
                    _target += _step;
                    continue;
                }
                _p.Position += to / dist * budget;
                _p.SetWalking(to.X >= 0 ? 1 : -1);
                budget = 0;
            }
        }
    }

    private static Dictionary<string, string> ParseArgs()
    {
        var result = new Dictionary<string, string>();
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var a = arg.TrimStart('-');
            int eq = a.IndexOf('=');
            if (eq < 0) result[a] = "";
            else result[a[..eq]] = a[(eq + 1)..];
        }
        return result;
    }
}
