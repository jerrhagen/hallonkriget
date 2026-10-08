using System.Linq;
using Godot;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Game.Ui;

/// <summary>
/// Slutscenen, designdokumentet (Seger och förlust): de två gubbarna vid gärdsgården, en kopp på
/// stolpen för varje soldat som vinnaren har kvar, och berättarrösten som sammanfattar kriget.
/// Den visas när GameState har en vinnare, och spelet pausas.
/// </summary>
public partial class EndScene : CanvasLayer
{
    private Picture _picture = null!;
    private Label _narrator = null!;

    public override void _Ready()
    {
        Layer = 20;
        var shade = new ColorRect { Color = new Color(0.1f, 0.08f, 0.06f, 0.55f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);

        var panel = new PanelContainer { Theme = Style.Build() };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(760, 560);
        panel.Position = new Vector2(-380, -280);
        AddChild(panel);
        var column = new VBoxContainer();
        panel.AddChild(column);
        _picture = new Picture { CustomMinimumSize = new Vector2(740, 330), ClipContents = true };
        column.AddChild(_picture);
        _narrator = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(740, 0) };
        column.AddChild(_narrator);
    }

    public void Show(GameState state, GameData data, byte me)
    {
        var winner = state.Players[state.Winner];
        var loser = state.Players.First(p => p.Id != winner.Id);
        int cups = state.People.Count(p => p.Owner == winner.Id && p.Role == PersonRole.Soldier && p.Unit >= 0 && data.Units[p.Unit].Recruits > 0);
        _picture.Set(winner.Faction, cups);

        string Name(Faction f) => f == Faction.Torpet ? "Torpet" : "Storgården";
        string Man(Faction f) => f == Faction.Torpet ? "Algot" : "Holger";
        int minutes = state.TickCount / (60 * GameState.TicksPerSecond);
        int lost = state.Players.Sum(p => p.SoldiersLost), animals = state.Players.Sum(p => p.AnimalsLost);
        int taken = state.Players.Sum(p => p.BuildingsTaken);
        string verdict = winner.Id == me ? "Segern är er." : "Grannen har vunnit.";
        _narrator.Text =
            $"Hallonkriget är över. Efter {minutes} minuter står {Name(winner.Faction)} som segrare, och {Name(loser.Faction)} har inga förråd kvar. " +
            $"{lost} soldater gav upp och gick hem, {animals} djur sprang tillbaka till sina hagar, och {taken} byggnader bytte ägare. " +
            $"Nu står {Man(winner.Faction)} och {Man(loser.Faction)} vid gärdsgården igen och dricker kaffe, {cups} koppar på stolpen. " +
            $"Ingen nämner hallonen. {verdict}";
    }

    /// <summary>Gärdsgården, gubbarna och kopparna, ritade med streck i bilderbokens färger.</summary>
    private sealed partial class Picture : Control
    {
        private Faction _winner;
        private int _cups;

        public void Set(Faction winner, int cups)
        {
            _winner = winner;
            _cups = cups;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var ink = Style.Ink;
            var size = Size;
            // Himmel, äng och en kulle bakom.
            DrawRect(new Rect2(Vector2.Zero, size), new Color("e9e1c8"));
            DrawRect(new Rect2(0, size.Y * 0.62f, size.X, size.Y * 0.38f), new Color("a7b26a"));
            DrawCircle(new Vector2(size.X * 0.75f, size.Y * 0.95f), size.X * 0.45f, new Color("94a05c"));
            DrawRect(new Rect2(0, size.Y * 0.62f, size.X, size.Y * 0.38f), new Color(ink, 0.08f));

            // Gärdsgården tvärs över bilden: stolpar och snedställda slanor.
            float ground = size.Y * 0.8f;
            var wood = new Color("8f6a45");
            for (float x = 30; x < size.X; x += 90)
            {
                DrawLine(new Vector2(x, ground), new Vector2(x, ground - 120), wood, 9);
                DrawLine(new Vector2(x, ground), new Vector2(x, ground - 120), ink, 2);
                DrawLine(new Vector2(x - 40, ground - 20), new Vector2(x + 50, ground - 100), wood, 6);
            }
            DrawLine(new Vector2(0, ground - 70), new Vector2(size.X, ground - 70), wood, 7);

            // Gubbarna på var sin sida, vinnaren till vänster.
            DrawOldMan(new Vector2(size.X * 0.36f, ground + 20), _winner == Faction.Torpet);
            DrawOldMan(new Vector2(size.X * 0.64f, ground + 20), _winner != Faction.Torpet);

            // En kopp för varje soldat vinnaren har kvar, i rader på slanan.
            int shown = Mathf.Min(_cups, 40);
            for (int i = 0; i < shown; i++)
            {
                var at = new Vector2(30 + (i % 20) * 34, ground - 84 - (i / 20) * 26);
                DrawRect(new Rect2(at, new Vector2(18, 14)), new Color("f5efe0"));
                DrawRect(new Rect2(at, new Vector2(18, 14)), ink, false, 2);
                DrawArc(at + new Vector2(20, 7), 5, -Mathf.Pi / 2, Mathf.Pi / 2, 8, ink, 2);
            }
        }

        /// <summary>Algot med blå keps och skägg, Holger med grå hatt och mustasch.</summary>
        private void DrawOldMan(Vector2 feet, bool algot)
        {
            var ink = Style.Ink;
            var coat = algot ? new Color("6f7f5a") : new Color("5e5a63");
            var body = new Rect2(feet.X - 34, feet.Y - 150, 68, 110);
            DrawRect(body, coat);
            DrawRect(body, ink, false, 3);
            DrawLine(new Vector2(feet.X - 16, feet.Y - 40), new Vector2(feet.X - 18, feet.Y), ink, 8);
            DrawLine(new Vector2(feet.X + 16, feet.Y - 40), new Vector2(feet.X + 18, feet.Y), ink, 8);
            var head = new Vector2(feet.X, feet.Y - 178);
            DrawCircle(head, 28, new Color("e8b796"));
            DrawArc(head, 28, 0, Mathf.Tau, 32, ink, 3);
            if (algot)
            {
                DrawColoredPolygon(new[] { head + new Vector2(-22, 6), head + new Vector2(22, 6), head + new Vector2(0, 46) }, new Color("d8d2c4"));
                DrawRect(new Rect2(head + new Vector2(-30, -32), new Vector2(60, 16)), new Color("3d5a80"));
                DrawRect(new Rect2(head + new Vector2(0, -22), new Vector2(40, 7)), new Color("3d5a80"));
            }
            else
            {
                DrawLine(head + new Vector2(-16, 10), head + new Vector2(16, 10), new Color("8a8173"), 6);
                DrawRect(new Rect2(head + new Vector2(-40, -26), new Vector2(80, 8)), new Color("8f897b"));
                DrawRect(new Rect2(head + new Vector2(-24, -52), new Vector2(48, 28)), new Color("8f897b"));
            }
            // Kaffekoppen i handen.
            var hand = new Vector2(feet.X + (algot ? 40 : -40), feet.Y - 110);
            DrawRect(new Rect2(hand - new Vector2(9, 7), new Vector2(18, 14)), new Color("f5efe0"));
            DrawRect(new Rect2(hand - new Vector2(9, 7), new Vector2(18, 14)), ink, false, 2);
        }
    }
}
