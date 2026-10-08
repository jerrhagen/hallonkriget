using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Hallonkriget.Game.Net;
using Hallonkriget.Game.View;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Buildings;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Game.Ui;

/// <summary>
/// Gränssnittet, som i KaM: en panel till vänster med flikarna Bygg, Förråd och Folk, och en
/// rad överst med klockan och hastigheten. Klickar man på en byggnad visar panelen den: lager,
/// arbetare, recept, bygdegårdens kö och lanthandelns byten. Allt spelaren gör blir kommandon.
/// </summary>
public partial class Hud : CanvasLayer
{
    public enum Tool { None, Build, Path }

    private enum Tab { Build, Stock, People }

    private LocalMatch _match = null!;
    private WorldView _world = null!;
    private Overlay _overlay = null!;
    private Tab _tab = Tab.Build;
    private int _selected = -1;
    private string _contentKey = "";
    private double _refresh;

    private VBoxContainer _content = null!;
    private Label _clock = null!, _people = null!;
    private readonly Dictionary<Tab, Button> _tabButtons = new();
    private readonly Dictionary<int, Button> _speedButtons = new();

    public Tool Current { get; private set; }
    public BuildingDef? Placing { get; private set; }

    /// <summary>Spara och ladda; sätts av scenen.</summary>
    public Action? SaveRequested, LoadRequested;

    private GameState State => _match.State;
    private GameData Data => _match.Data;
    private byte Me => _match.LocalPlayer;

    public void Attach(LocalMatch match, WorldView world)
    {
        _match = match;
        _world = world;
        _selected = -1;
        _contentKey = "";
        Current = Tool.None;
        Placing = null;
        if (_overlay is not null && IsInstanceValid(_overlay)) _overlay.QueueFree();
        _overlay = new Overlay(this) { Name = "Overlay", ZIndex = 50 };
        _world.AddChild(_overlay);
        RefreshContent();
    }

    public override void _Ready()
    {
        Layer = 5;
        var root = new Control { Theme = Style.Build(), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Överst: klockan, hastigheten, folket och sparknapparna.
        var bar = new PanelContainer { Position = new Vector2(8, 8) };
        root.AddChild(bar);
        var row = new HBoxContainer();
        bar.AddChild(row);
        _clock = new Label { CustomMinimumSize = new Vector2(150, 0) };
        row.AddChild(_clock);
        foreach (var (speed, text) in new[] { (0, "Paus"), (1, "1×"), (2, "2×") })
        {
            var b = new Button { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(48, 0) };
            int s = speed;
            b.Pressed += () => _match.Speed = s;
            _speedButtons[speed] = b;
            row.AddChild(b);
        }
        row.AddChild(new VSeparator());
        _people = new Label { CustomMinimumSize = new Vector2(220, 0) };
        row.AddChild(_people);
        row.AddChild(new VSeparator());
        var save = new Button { Text = "Spara" };
        save.Pressed += () => SaveRequested?.Invoke();
        row.AddChild(save);
        var load = new Button { Text = "Ladda" };
        load.Pressed += () => LoadRequested?.Invoke();
        row.AddChild(load);

        // Panelen till vänster.
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(330, 0) };
        panel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        panel.OffsetTop = 60;
        panel.OffsetBottom = -8;
        panel.OffsetLeft = 8;
        panel.OffsetRight = 338;
        root.AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        var tabs = new HBoxContainer();
        column.AddChild(tabs);
        foreach (var (tab, text) in new[] { (Tab.Build, "Bygg"), (Tab.Stock, "Förråd"), (Tab.People, "Folk") })
        {
            var b = new Button { Text = text, ToggleMode = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var t = tab;
            b.Pressed += () => ShowTab(t);
            _tabButtons[tab] = b;
            tabs.AddChild(b);
        }
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_content);
        ShowTab(Tab.Build);
    }

    public override void _Process(double delta)
    {
        if (_match is null) return;
        _refresh -= delta;
        if (_refresh > 0) return;
        _refresh = 0.25;
        _clock.Text = ClockText(State.TickCount);
        foreach (var (speed, b) in _speedButtons) b.SetPressedNoSignal(_match.Speed == speed);
        int pop = State.Population(Me);
        var mine = State.People.Where(p => p.Owner == Me).ToList();
        int mood = mine.Count == 0 ? 0 : (int)mine.Average(p => p.Mood);
        _people.Text = $"Folk {pop}/{State.Beds(Me)}   Humör {mood}   Gav upp {State.Players[Me].GaveUp}";
        RefreshContent();
    }

    public static string ClockText(int tick)
    {
        string[] days = { "Måndag", "Tisdag", "Onsdag", "Torsdag", "Fredag", "Lördag", "Söndag" };
        long inDay = (long)(tick % GameClock.WeekTicks) * 7 % GameClock.WeekTicks;
        int minutes = (int)(inDay * 24 * 60 / GameClock.WeekTicks);
        return $"{days[GameClock.Weekday(tick)]} {minutes / 60:00}:{minutes % 60:00}";
    }

    // ---- Flikar och innehåll ----

    private void ShowTab(Tab tab)
    {
        _tab = tab;
        _selected = -1;
        foreach (var (t, b) in _tabButtons) b.SetPressedNoSignal(t == tab);
        _contentKey = "";
        RefreshContent();
    }

    public void Select(int buildingId)
    {
        _selected = buildingId;
        foreach (var (_, b) in _tabButtons) b.SetPressedNoSignal(false);
        _world.SetSelected(buildingId);
        _contentKey = "";
        RefreshContent();
    }

    /// <summary>Bygger om panelens innehåll när det som visas har ändrats, inte varje bildruta.</summary>
    private void RefreshContent()
    {
        if (_match is null || _content is null) return;
        string key = _selected >= 0 ? BuildingKey(State.Buildings[_selected]) : _tab switch
        {
            Tab.Build => $"bygg{Current}{Placing?.Id}",
            Tab.Stock => "förråd" + string.Join(",", StoredTotals()),
            _ => "folk" + PeopleKey(),
        };
        if (key == _contentKey) return;
        _contentKey = key;
        foreach (var child in _content.GetChildren()) child.QueueFree();
        if (_selected >= 0) BuildInspector(State.Buildings[_selected]);
        else if (_tab == Tab.Build) BuildBuildTab();
        else if (_tab == Tab.Stock) BuildStockTab();
        else BuildPeopleTab();
    }

    private void Add(Control c) => _content.AddChild(c);

    private Button AddButton(string text, Action action, bool pressed = false, bool disabled = false, string tooltip = "")
    {
        var b = new Button { Text = text, ToggleMode = pressed, ButtonPressed = pressed, Disabled = disabled, TooltipText = tooltip,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        b.Pressed += action;
        Add(b);
        return b;
    }

    private void BuildBuildTab()
    {
        Add(Style.Title("Bygg"));
        Add(Style.Small("Välj en byggnad och klicka på kartan. Högerklick avbryter. Stigar dras med musen."));
        AddButton("Stig", () => SetTool(Current == Tool.Path ? Tool.None : Tool.Path), pressed: Current == Tool.Path);
        var grid = new GridContainer { Columns = 2 };
        Add(grid);
        var faction = State.Players[Me].Faction;
        foreach (var def in Data.Buildings.Where(b => b.Buildable && b.AllowedFor(faction)))
        {
            string cost = string.Join(", ", def.Cost.Select(c => $"{c.Count} {Data.Goods[c.Good].Name.ToLowerInvariant()}"));
            var b = new Button
            {
                Text = def.Name, ToggleMode = true, ButtonPressed = Placing == def, ClipText = true,
                CustomMinimumSize = new Vector2(150, 30), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = $"{def.Name}, {def.Width}×{def.Height}\nKostar {cost}\n{Describe(def)}",
            };
            var d = def;
            b.Pressed += () => StartPlacing(Placing == d ? null : d);
            grid.AddChild(b);
        }
    }

    /// <summary>Vad byggnaden gör, i en rad: "Timmer → Brädor ×2" och så vidare.</summary>
    private string Describe(BuildingDef def)
    {
        var parts = new List<string>();
        if (def.Worker is { } w) parts.Add("Arbetare: " + Data.Professions[Data.ProfessionIndex(w)].Name);
        foreach (var r in def.Recipes.Take(6)) parts.Add(RecipeText(r));
        if (def.IsStorage) parts.Add($"Förråd för {def.Storage} varor");
        if (def.Table > 0) parts.Add($"Bordet rymmer {def.Table} portioner");
        if (def.School is not null) parts.Add("Utbildar folk mot kaffe och verktyg");
        if (def.Gather is { } g) parts.Add($"Behöver {TerrainName(g.Terrain)} inom {g.Radius} rutor");
        if (def.NextTo is { } t) parts.Add($"Ska ligga intill {TerrainName(t)}");
        if (def.FieldWidth > 0) parts.Add("Odlingsrutor till höger om huset");
        return string.Join("\n", parts);
    }

    private string RecipeText(Recipe r)
    {
        string In(GoodAmount a) => a.Count > 1 ? $"{Data.Goods[a.Good].Name} ×{a.Count}" : Data.Goods[a.Good].Name;
        string left = r.In.Length == 0 ? "" : string.Join(" + ", r.In.Select(In)) + " → ";
        return left + string.Join(" + ", r.Out.Select(In));
    }

    public static string TerrainName(Terrain t) => t switch
    {
        Terrain.Clearing => "glänta",
        Terrain.Stony => "stenig mark",
        Terrain.Forest => "granskog",
        Terrain.Meadow => "äng",
        Terrain.Bog => "myr",
        Terrain.Water => "vatten",
        Terrain.ScrapHeap => "skrothög",
        Terrain.RaspberryThicket => "hallonsnår",
        Terrain.PlumTree => "plommonträd",
        Terrain.Road => "landsväg",
        _ => "åker",
    };

    private int[] StoredTotals()
    {
        var totals = new int[Data.Goods.Count];
        foreach (var b in State.Buildings)
            if (b.Owner == Me && b.Def.IsStorage && b.Stage == BuildingStage.Done)
                for (int g = 0; g < totals.Length; g++) totals[g] += b.OutputCount(g);
        return totals;
    }

    private void BuildStockTab()
    {
        Add(Style.Title("Förråd"));
        Add(Style.Small("Det som ligger i stugan och bodarna."));
        var totals = StoredTotals();
        var produced = State.Players[Me].Produced;
        var grid = new GridContainer { Columns = 3 };
        Add(grid);
        for (int g = 0; g < totals.Length; g++)
        {
            if (totals[g] == 0 && produced[g] == 0) continue;
            grid.AddChild(new Label { Text = Data.Goods[g].Name, CustomMinimumSize = new Vector2(150, 0) });
            grid.AddChild(new Label { Text = totals[g].ToString(), CustomMinimumSize = new Vector2(50, 0), HorizontalAlignment = HorizontalAlignment.Right });
            grid.AddChild(Style.Small(produced[g] > 0 ? $"gjort {produced[g]}" : ""));
        }
    }

    private string PeopleKey()
    {
        var mine = State.People.Where(p => p.Owner == Me).ToList();
        return $"{mine.Count}{State.Beds(Me)}{mine.Count(p => p.Job == PersonJob.Resting)}{mine.Sum(p => p.Mood) / Math.Max(1, mine.Count) / 5}" +
               string.Join(",", State.Buildings.Where(b => b.Owner == Me && b.Def.School is not null).Select(b => b.TrainingQueue.Count));
    }

    private void BuildPeopleTab()
    {
        Add(Style.Title("Folk"));
        var mine = State.People.Where(p => p.Owner == Me).ToList();
        Add(new Label { Text = $"{State.Population(Me)} personer, {State.Beds(Me)} sovplatser" });
        Add(new Label { Text = $"Bärare {mine.Count(p => p.Role == PersonRole.Carrier)}, hantlangare {mine.Count(p => p.Role == PersonRole.Laborer)}, arbetare {mine.Count(p => p.Role == PersonRole.Worker)}" });
        Add(new Label { Text = $"Humör i snitt {(mine.Count == 0 ? 0 : (int)mine.Average(p => p.Mood))}, {mine.Count(p => p.Job == PersonJob.Resting)} vilar hemma" });
        var jobless = mine.Where(p => p.Role == PersonRole.Worker && p.Job == PersonJob.Idle).GroupBy(p => p.Profession).ToList();
        if (jobless.Count > 0)
            Add(Style.Small("Utan arbetsplats: " + string.Join(", ", jobless.Select(g => $"{ProfessionName(g.Key)} ×{g.Count()}"))));
        var empty = State.Buildings.Where(b => b.Owner == Me && b.Stage == BuildingStage.Done && b.Def.Worker is not null && b.WorkerId < 0).ToList();
        if (empty.Count > 0)
            Add(Style.Small("Saknar arbetare: " + string.Join(", ", empty.Select(b => b.Def.Name))));

        Add(new HSeparator());
        var schools = State.Buildings.Where(b => b.Owner == Me && b.Def.School is not null && b.Stage == BuildingStage.Done).ToList();
        if (schools.Count == 0) Add(Style.Small("Bygg en bygdegård för att utbilda folk."));
        foreach (var school in schools)
        {
            AddButton($"{school.Def.Name}: {school.TrainingQueue.Count} i kön", () => Select(school.Id));
        }
    }

    private string ProfessionName(string id) => id.Length == 0 ? "" : Data.Professions[Data.ProfessionIndex(id)].Name;

    // ---- En byggnad ----

    private string BuildingKey(Building b)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(b.Id).Append(b.Stage).Append(b.WorkDone * 20 / Math.Max(1, b.Def.BuildTicks)).Append(b.HasWorker).Append(b.WorkerId)
          .Append(b.SelectedRecipe).Append(b.CurrentRecipe).Append(b.Training).Append(b.TrainingQueue.Count);
        for (int g = 0; g < Data.Goods.Count; g++)
            sb.Append(b.InputCount(g)).Append('/').Append(b.OutputCount(g)).Append(b.IsBlocked(g) ? 'x' : '.').Append(b.MaterialNeeded(g));
        return sb.ToString();
    }

    private void BuildInspector(Building b)
    {
        var def = b.Def;
        AddButton("← Tillbaka", () => ShowTab(_tab));
        Add(Style.Title(def.Name));
        if (b.Owner != Me)
        {
            Add(Style.Small("Grannens."));
            return;
        }

        if (b.Stage == BuildingStage.Construction)
        {
            Add(new Label { Text = $"Byggplats, {b.WorkDone * 100 / Math.Max(1, def.BuildTicks)} procent byggt" });
            foreach (var c in def.Cost)
                Add(new Label { Text = $"{Data.Goods[c.Good].Name}: {c.Count - b.MaterialNeeded(c.Good)} av {c.Count}" });
            Add(Style.Small("Bärarna hämtar materialet, hantlangarna bygger."));
            return;
        }

        Add(Style.Small(Describe(def)));
        if (def.Worker is { } worker)
        {
            string name = ProfessionName(worker);
            Add(new Label
            {
                Text = b.HasWorker ? $"{name} arbetar här." : b.WorkerId >= 0 ? $"{name} är ute." : $"Ingen {name.ToLowerInvariant()} än. Utbilda en i bygdegården.",
            });
        }

        if (def.IsStorage) StockList(b, output: true);
        else if (def.School is not null) SchoolPanel(b);
        else if (def.IsTrade) TradePanel(b);
        else
        {
            if (def.Recipes.Length > 1) RecipePanel(b);
            if (b.CurrentRecipe >= 0) Add(Style.Small($"Gör nu: {RecipeText(def.Recipes[b.CurrentRecipe])}"));
            StockList(b, output: false);
        }
    }

    private void RecipePanel(Building b)
    {
        Add(new HSeparator());
        Add(new Label { Text = "Tillverka" });
        AddButton("Turas om", () => _match.Submit(CommandType.SelectRecipe, b.Id, -1), pressed: b.SelectedRecipe < 0);
        for (int r = 0; r < b.Def.Recipes.Length; r++)
        {
            int recipe = r;
            AddButton(RecipeText(b.Def.Recipes[r]), () => _match.Submit(CommandType.SelectRecipe, b.Id, recipe), pressed: b.SelectedRecipe == r);
        }
    }

    /// <summary>Inlager med spärrar och utlager. Kafferepets bord och bygdegårdens kaffe också.</summary>
    private void StockList(Building b, bool output)
    {
        Add(new HSeparator());
        var inputs = new List<int>();
        for (int g = 0; g < Data.Goods.Count; g++)
        {
            bool used = b.Def.Accepts.Contains(g) || b.Def.Recipes.Any(r => r.In.Any(a => a.Good == g));
            if (!output && used) inputs.Add(g);
        }
        if (inputs.Count > 0)
        {
            Add(new Label { Text = b.Def.Table > 0 ? $"På bordet ({b.InputTotal} av {b.Def.Table})" : "Inlager" });
            foreach (int g in inputs)
            {
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = Data.Goods[g].Name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
                row.AddChild(new Label { Text = b.InputCount(g).ToString(), CustomMinimumSize = new Vector2(30, 0) });
                bool blocked = b.IsBlocked(g);
                var block = new Button { Text = blocked ? "Spärrad" : "Öppen", ToggleMode = true, ButtonPressed = blocked, CustomMinimumSize = new Vector2(80, 0),
                    TooltipText = "En spärrad vara begärs inte hit." };
                int good = g;
                block.Pressed += () => _match.Submit(CommandType.BlockGood, b.Id, good, blocked ? 0 : 1);
                row.AddChild(block);
                Add(row);
            }
        }
        var outs = Enumerable.Range(0, Data.Goods.Count).Where(g => b.OutputCount(g) > 0).ToList();
        if (outs.Count > 0 || !output)
        {
            Add(new Label { Text = output ? $"Lager ({b.StoredTotal} av {b.Def.Storage})" : "Utlager" });
            if (outs.Count == 0) Add(Style.Small("Tomt."));
            var grid = new GridContainer { Columns = 2 };
            Add(grid);
            foreach (int g in outs)
            {
                grid.AddChild(new Label { Text = Data.Goods[g].Name, CustomMinimumSize = new Vector2(200, 0) });
                grid.AddChild(new Label { Text = b.OutputCount(g).ToString() });
            }
        }
    }

    private void SchoolPanel(Building b)
    {
        Add(new HSeparator());
        Add(Style.Small("En person kostar en kopp kaffe (eller två surrogat) och, för de flesta yrken, ett verktyg. Det tar 30 sekunder."));
        Add(new Label { Text = "Kön" });
        if (b.TrainingQueue.Count == 0) Add(Style.Small("Tom."));
        for (int i = 0; i < b.TrainingQueue.Count; i++)
            Add(new Label { Text = (i == 0 && b.Training >= 0 ? "Utbildas: " : $"{i + 1}. ") + b.TrainingQueue[i].Name });
        if (b.TrainingQueue.Count > (b.Training >= 0 ? 1 : 0))
            AddButton("Ta bort den sista", () => _match.Submit(CommandType.CancelTraining, b.Id));
        Add(Style.Small($"Har: kaffe {b.InputCount(Data.GoodIndex("kaffe"))}, surrogat {b.InputCount(Data.GoodIndex("surrogatkaffe"))}, verktyg {b.InputCount(Data.GoodIndex("verktyg"))}"));
        Add(new HSeparator());
        Add(new Label { Text = "Utbilda" });
        var grid = new GridContainer { Columns = 2 };
        Add(grid);
        var faction = State.Players[Me].Faction;
        // Bärare och hantlangare först, sedan yrkena till de byggnader som spelaren kan bygga.
        foreach (var p in Data.Professions)
        {
            bool useful = p.Role != PersonRole.Worker || Data.Buildings.Any(d => d.Worker == p.Id && d.AllowedFor(faction));
            if (!useful) continue;
            var button = new Button { Text = p.Name, ClipText = true, CustomMinimumSize = new Vector2(148, 28), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Disabled = b.TrainingQueue.Count >= b.Def.School!.QueueLimit, TooltipText = p.Tool >= 0 ? "Kaffe och ett verktyg" : "Bara kaffe" };
            int index = p.Index;
            button.Pressed += () => _match.Submit(CommandType.Train, b.Id, index);
            grid.AddChild(button);
        }
    }

    private void TradePanel(Building b)
    {
        Add(new HSeparator());
        Add(Style.Small("Välj vad som ska köpas och med vad. Bärarna bär dit betalningen. Stängt på söndagar."));
        var faction = State.Players[Me].Faction;
        AddButton("Handla inget", () => _match.Submit(CommandType.SelectRecipe, b.Id, -1), pressed: b.SelectedRecipe < 0);
        for (int r = 0; r < b.Def.Recipes.Length; r++)
        {
            var recipe = b.Def.Recipes[r];
            if (recipe.Faction == FactionRule.Torpet && faction != Faction.Torpet) continue;
            if (recipe.Faction == FactionRule.Storgarden && faction != Faction.Storgarden) continue;
            int index = r;
            string text = $"{Data.Goods[recipe.Out[0].Good].Name} för {string.Join(" + ", recipe.In.Select(a => $"{a.Count} {Data.Goods[a.Good].Name.ToLowerInvariant()}"))}";
            AddButton(text, () => _match.Submit(CommandType.SelectRecipe, b.Id, index), pressed: b.SelectedRecipe == r);
        }
        StockList(b, output: false);
    }

    // ---- Verktyg på kartan ----

    private void SetTool(Tool tool)
    {
        Current = tool;
        if (tool != Tool.Build) Placing = null;
        _contentKey = "";
        _overlay.QueueRedraw();
    }

    public void StartPlacing(BuildingDef? def)
    {
        Placing = def;
        Current = def is null ? Tool.None : Tool.Build;
        _contentKey = "";
        _overlay.QueueRedraw();
    }

    /// <summary>Övre vänstra rutan när musen pekar på en ruta: byggnaden centreras på muspekaren.</summary>
    public TilePoint OriginFor(BuildingDef def, TilePoint mouse) => new(mouse.X - (def.Width - 1) / 2, mouse.Y - (def.Height - 1) / 2);

    public TilePoint Hover { get; private set; }

    public bool CanPlaceHere(BuildingDef def, TilePoint origin) => State.CanPlace(Me, def, origin);

    public bool CanPathHere(TilePoint p) =>
        State.Map.Inside(p) && State.Map.CanLayPath(p) && State.Map.PathAt(p) == PathState.None &&
        (State.Map.OwnerAt(p) == GameMap.NoOwner || State.Map.OwnerAt(p) == Me);

    public void MouseMoved(TilePoint tile, bool leftHeld)
    {
        if (tile == Hover) return;
        Hover = tile;
        if (Current == Tool.Path && leftHeld && CanPathHere(tile)) _match.Submit(CommandType.PlanPath, tile.X, tile.Y);
        _overlay.QueueRedraw();
    }

    /// <summary>Vänsterklick på kartan. Returnerar true om klicket användes.</summary>
    public bool LeftClick(TilePoint tile, bool shift)
    {
        switch (Current)
        {
            case Tool.Build when Placing is not null:
            {
                var origin = OriginFor(Placing, tile);
                if (!CanPlaceHere(Placing, origin)) return true;
                _match.Submit(CommandType.PlaceBuilding, Placing.Index, origin.X, origin.Y);
                if (!shift) StartPlacing(null);
                return true;
            }
            case Tool.Path:
                if (CanPathHere(tile)) _match.Submit(CommandType.PlanPath, tile.X, tile.Y);
                return true;
        }
        if (!State.Map.Inside(tile)) return false;
        int occupant = State.Map.OccupantAt(tile);
        if (occupant > 0)
        {
            Select(occupant - 1);
            return true;
        }
        foreach (var b in State.Buildings)
        {
            if (b.Entrance != tile && !(b.Def.FieldWidth > 0 && Building.FieldTiles(b.Def, b.Origin).Contains(tile))) continue;
            Select(b.Id);
            return true;
        }
        if (_selected >= 0)
        {
            _world.SetSelected(-1);
            ShowTab(_tab);
        }
        return false;
    }

    /// <summary>Högerklick eller Escape: avbryt verktyget, annars stäng byggnaden.</summary>
    public void Cancel()
    {
        if (Current != Tool.None)
        {
            SetTool(Tool.None);
            return;
        }
        if (_selected >= 0)
        {
            _world.SetSelected(-1);
            ShowTab(_tab);
        }
    }

    /// <summary>Byggnadens skugga under muspekaren, och stigverktygets markering.</summary>
    private sealed partial class Overlay : Node2D
    {
        private readonly Hud _hud;

        public Overlay(Hud hud) => _hud = hud;

        public override void _Draw()
        {
            const int T = WorldView.Tile;
            var hover = _hud.Hover;
            if (_hud.Current == Tool.Path)
            {
                var ok = _hud.CanPathHere(hover);
                DrawRect(new Rect2(hover.X * T + 8, hover.Y * T + 8, T - 16, T - 16), ok ? new Color(0.5f, 0.35f, 0.15f, 0.45f) : new Color(0.7f, 0.1f, 0.1f, 0.35f));
                return;
            }
            if (_hud.Current != Tool.Build || _hud.Placing is not { } def) return;
            var origin = _hud.OriginFor(def, hover);
            bool can = _hud.CanPlaceHere(def, origin);
            var fill = can ? new Color(0.42f, 0.55f, 0.25f, 0.45f) : new Color(0.75f, 0.15f, 0.1f, 0.45f);
            DrawRect(new Rect2(origin.X * T, origin.Y * T, def.Width * T, def.Height * T), fill);
            DrawRect(new Rect2(origin.X * T, origin.Y * T, def.Width * T, def.Height * T), Style.Ink, false, 4);
            foreach (var p in Building.FieldTiles(def, origin))
                DrawRect(new Rect2(p.X * T + 4, p.Y * T + 4, T - 8, T - 8), new Color(fill, 0.3f));
            var door = Building.EntranceFor(def, origin);
            DrawCircle(new Vector2((door.X + 0.5f) * T, (door.Y + 0.5f) * T), 18, new Color(Style.Ink, 0.7f));
            var font = ThemeDB.FallbackFont;
            DrawString(font, new Vector2(origin.X * T, origin.Y * T - 12), def.Name, HorizontalAlignment.Left, -1, 34, Style.Ink);
            if (def.Gather is { } g)
                DrawArc(new Vector2((door.X + 0.5f) * T, (door.Y + 0.5f) * T), g.Radius * T, 0, Mathf.Tau, 64, new Color(Style.Ink, 0.4f), 4);
        }
    }
}
