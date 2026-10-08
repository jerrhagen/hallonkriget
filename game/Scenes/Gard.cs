using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Hallonkriget.Game.Net;
using Hallonkriget.Game.Ui;
using Hallonkriget.Game.View;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Map;

namespace Hallonkriget.Game;

/// <summary>
/// Spelet i fas 2: en gård att bygga på kartan hemmanet, utan strid.
///
/// Styrning: pilar eller WASD flyttar, mushjulet zoomar, mellersta musknappen drar kartan.
/// Mellanslag pausar, 1 och 2 väljer hastighet. Vänsterklick väljer och bygger, högerklick eller Escape avbryter.
/// F5 sparar och F9 laddar snabbsparet.
///
/// Från kommandoraden, efter "--": --karta=hemmanet, --lager=storgarden, --byggordning=namn spelar
/// en byggordning åt spelaren, --spola=minuter spolar fram, --skarmbild=fil.png tar en bild och
/// avslutar, --zoom=0.5 och --kamera=x,y (rutor) ställer kameran, --utan-papper, --provspara, --filma=bilder
/// (en bild var tredje bildruta till skarmbild_000.png och framåt), --hastighet=2.
/// För skärmbilder: --valj=bygdegarden (eller byggnadsnummer) öppnar en byggnad, --bygga=id och --mus=x,y visar en byggnad som ska placeras.
/// </summary>
public partial class Gard : Node2D
{
    private LocalMatch _match = null!;
    private WorldView _world = null!;
    private Camera2D _camera = null!;
    private Hud _hud = null!;
    private AdvisorCorner _advisor = new() { Name = "Radgivare" };
    private CanvasLayer _paper = null!;
    private Dictionary<string, string> _args = new();
    private string? _screenshot;
    private int _framesUntilScreenshot = 30;
    private bool _dragging;
    private int _filmFrames, _filmed, _frameCounter;

    public LocalMatch Match => _match;
    public WorldView World => _world;
    public Camera2D Camera => _camera;

    public override void _Ready()
    {
        _args = ParseArgs();
        var data = GameFiles.LoadData();
        string mapId = _args.GetValueOrDefault("karta", "hemmanet");
        var faction = _args.GetValueOrDefault("lager") == "storgarden" ? Faction.Storgarden : Faction.Torpet;
        _match = new LocalMatch(data, GameFiles.LoadMap(mapId), 1958_07_14UL, faction);
        if (_args.TryGetValue("byggordning", out var order))
            _match.Autopilot = new Hallonkriget.Sim.Ai.BuildOrderPlayer(GameFiles.LoadBuildOrder(order, data), 0);
        _advisor.Attach(_match);
        if (_args.TryGetValue("spola", out var minutes))
            _match.FastForward((int)(float.Parse(minutes, CultureInfo.InvariantCulture) * 60 * GameState.TicksPerSecond));

        _world = new WorldView { Name = "Varld" };
        AddChild(_world);
        _world.Attach(_match);

        _camera = new Camera2D { Name = "Kamera" };
        AddChild(_camera);
        var home = _match.State.Buildings[0];
        _camera.Position = WorldView.TileCenter(home.Entrance) + new Vector2(3 * WorldView.Tile, 0);
        _camera.Zoom = Vector2.One * 0.5f;
        if (_args.TryGetValue("kamera", out var cam))
        {
            var xy = cam.Split(',');
            _camera.Position = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture),
                float.Parse(xy[1], CultureInfo.InvariantCulture)) * WorldView.Tile;
        }
        if (_args.TryGetValue("zoom", out var z)) _camera.Zoom = Vector2.One * float.Parse(z, CultureInfo.InvariantCulture);

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);
        _hud.Attach(_match, _world);
        AddChild(_advisor);
        _hud.SaveRequested = Save;
        _hud.LoadRequested = Load;
        if (_args.TryGetValue("valj", out var sel))
            _hud.Select(int.TryParse(sel, out int id) ? id : _match.State.Buildings.First(b => b.Def.Id == sel).Id);
        if (_args.TryGetValue("bygga", out var place)) _hud.StartPlacing(data.Building(place));
        if (_args.TryGetValue("mus", out var mouse))
        {
            var xy = mouse.Split(',');
            _hud.MouseMoved(new TilePoint(int.Parse(xy[0]), int.Parse(xy[1])), false);
        }

        if (_args.ContainsKey("provspara")) TrySaveAndLoad();

        AddPaper();
        _paper.Visible = !_args.ContainsKey("utan-papper");
        if (_args.TryGetValue("skarmbild", out var shot)) _screenshot = shot;
        if (_args.TryGetValue("filma", out var film)) _filmFrames = int.Parse(film);
        if (_args.TryGetValue("hastighet", out var speed)) _match.Speed = int.Parse(speed);
        if (_args.TryGetValue("vanta", out var w)) _framesUntilScreenshot = int.Parse(w);
    }

    /// <summary>Byter till en annan match, när ett sparat spel laddas.</summary>
    public void Replace(LocalMatch match)
    {
        _match = match;
        _world.Attach(match);
        _hud.Attach(match, _world);
        _advisor.Attach(match);
    }

    private static string QuickSave => System.IO.Path.Combine(GameFiles.SaveDir, "snabbspar.hkr");

    private void Save()
    {
        System.IO.Directory.CreateDirectory(GameFiles.SaveDir);
        _match.Save(QuickSave);
        GD.Print($"Sparat: {QuickSave}");
    }

    /// <summary>--provspara: sparar, laddar och jämför kontrollsumman. Skriver resultatet.</summary>
    private void TrySaveAndLoad()
    {
        ulong before = _match.State.Hash();
        int tick = _match.State.TickCount;
        Save();
        Load();
        GD.Print(_match.State.Hash() == before && _match.State.TickCount == tick
            ? $"Provsparning: samma tillstånd efter laddning, tick {tick}"
            : "Provsparning: FEL, tillståndet skiljer sig efter laddning");
    }

    private void Load()
    {
        if (!System.IO.File.Exists(QuickSave)) return;
        try
        {
            var loaded = LocalMatch.Load(QuickSave, _match.Data);
            loaded.Speed = 0;
            Replace(loaded);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"Kunde inte ladda {QuickSave}: {ex.Message}");
        }
    }

    public override void _Process(double delta)
    {
        _match.Update(delta);

        var pan = Vector2.Zero;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A)) pan.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) pan.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)) pan.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)) pan.Y += 1;
        _camera.Position += pan * 700 * (float)delta / _camera.Zoom.X;
        ClampCamera();

        if (_screenshot is not null && _filmFrames > 0)
        {
            if (--_framesUntilScreenshot > 0) return;
            // --filma=bilder: en bild var tredje bildruta, numrerade, sedan avslutas spelet.
            if (_frameCounter++ % 3 == 0)
            {
                var path = _screenshot.Replace(".png", $"_{_filmed++:D3}.png");
                GetViewport().GetTexture().GetImage().SavePng(path);
                if (_filmed >= _filmFrames) GetTree().Quit();
            }
            return;
        }
        if (_screenshot is not null && --_framesUntilScreenshot <= 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_screenshot);
            GD.Print($"Skärmbild sparad: {_screenshot}");
            GetTree().Quit();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Space) _match.Speed = _match.Speed == 0 ? 1 : 0;
            if (key.Keycode == Key.Key1) _match.Speed = 1;
            if (key.Keycode == Key.Key2) _match.Speed = 2;
            if (key.Keycode is Key.Plus or Key.KpAdd or Key.Equal) ZoomBy(1.25f);
            if (key.Keycode is Key.Minus or Key.KpSubtract) ZoomBy(0.8f);
            if (key.Keycode == Key.F12) _paper.Visible = !_paper.Visible;
            if (key.Keycode == Key.Escape) _hud.Cancel();
            if (key.Keycode == Key.F5) Save();
            if (key.Keycode == Key.F9) Load();
        }
        if (e is InputEventMouseButton mb)
        {
            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp) ZoomBy(1.1f);
            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown) ZoomBy(1 / 1.1f);
            if (mb.ButtonIndex == MouseButton.Middle) _dragging = mb.Pressed;
            if (mb.Pressed && mb.ButtonIndex == MouseButton.Left) _hud.LeftClick(MouseTile(), mb.ShiftPressed);
            if (mb.Pressed && mb.ButtonIndex == MouseButton.Right) _hud.Cancel();
        }
        if (e is InputEventMouseMotion motion)
        {
            if (_dragging) _camera.Position -= motion.Relative / _camera.Zoom.X;
            _hud.MouseMoved(MouseTile(), (motion.ButtonMask & MouseButtonMask.Left) != 0);
        }
    }

    private TilePoint MouseTile() => WorldView.TileAt(GetGlobalMousePosition());


    private void ZoomBy(float factor)
    {
        // 0,5 är normal zoom (64 px per ruta på skärmen), 2 är närmast.
        float z = Mathf.Clamp(_camera.Zoom.X * factor, 0.2f, 2.0f);
        _camera.Zoom = new Vector2(z, z);
    }

    private void ClampCamera()
    {
        var map = _match.State.Map;
        var size = new Vector2(map.Width, map.Height) * WorldView.Tile;
        _camera.Position = _camera.Position.Clamp(Vector2.Zero, size);
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
