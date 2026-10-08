using Godot;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.People;

namespace Hallonkriget.Game.View;

/// <summary>
/// En person: pysslingen från stilprovet, med det hen bär på ryggen. Tills yrkena har egna hattar
/// skiljs hantlangare och arbetare åt med färgen. Positionen räknas fram vid varje tick och ritas
/// mittemellan två tick, så att rörelsen blir mjuk fast simuleringen går i tio steg per sekund.
/// </summary>
public partial class PersonView : Node2D
{
    public Person Person { get; }
    private readonly GameData _data;
    private readonly Pyssling _doll;
    private readonly Parcel _parcel;
    private Vector2 _from, _to;
    private int _carrying = -2;

    public PersonView(Person person, GameData data)
    {
        Person = person;
        _data = data;
        Name = $"person_{person.Id}";
        _doll = new Pyssling { CarryingEgg = false };
        AddChild(_doll);
        _parcel = new Parcel { Position = new Vector2(-14, -66), Visible = false };
        AddChild(_parcel);
        _pennant = new Pennant { Position = new Vector2(0, -70), Visible = false };
        AddChild(_pennant);
        UpdateRole();
    }

    private readonly Pennant _pennant;
    private PersonRole _role = (PersonRole)255;
    private int _unit = -2;

    /// <summary>Färgen efter roll. En soldat får en vimpel i lägrets färg med enhetens namn.</summary>
    private void UpdateRole()
    {
        if (Person.Role == _role && Person.Unit == _unit) return;
        _role = Person.Role;
        _unit = Person.Unit;
        Modulate = Person.Role switch
        {
            PersonRole.Laborer => new Color(0.92f, 0.86f, 0.74f),
            PersonRole.Worker => new Color(0.84f, 0.9f, 1.0f),
            PersonRole.Recruit => new Color(0.95f, 0.88f, 0.88f),
            _ => Colors.White,
        };
        _pennant.Visible = Person.Unit >= 0;
        if (Person.Unit >= 0) _pennant.Set(_data.Units[Person.Unit].Name, Person.Owner);
    }

    /// <summary>Var personen står enligt simuleringen just nu, i världskoordinater (fötterna).</summary>
    private Vector2 SimPosition()
    {
        var from = WorldView.TileCenter(Person.From);
        var to = WorldView.TileCenter(Person.Tile);
        if (Person.StepTotal <= 0 || Person.StepProgress >= Person.StepTotal) return to;
        return from.Lerp(to, Person.StepProgress / (float)Person.StepTotal);
    }

    public void Sync(bool snap)
    {
        _from = snap ? SimPosition() : _to;
        _to = SimPosition();
        if (snap) Position = _to;
        Visible = !Person.Inside;
        UpdateRole();
        _pennant.Medal = Person.Medal;

        if (Person.Carrying != _carrying)
        {
            _carrying = Person.Carrying;
            bool egg = _carrying >= 0 && _data.Goods[_carrying].Id == "agg";
            _doll.SetCarryingEgg(egg);
            _parcel.Visible = _carrying >= 0 && !egg;
            if (_parcel.Visible) _parcel.Set(_data.Goods[_carrying]);
        }
    }

    public void Interpolate(float alpha)
    {
        if (!Visible) return;
        var pos = _from.Lerp(_to, alpha);
        var d = _to - _from;
        _doll.SetWalking(d.LengthSquared() < 0.01f ? 0 : (d.X >= 0 ? 1 : -1));
        Position = pos;
    }

    /// <summary>Vimpeln över en soldat: spelarens röd, grannens blå, med enhetens namn och en medalj.</summary>
    private sealed partial class Pennant : Node2D
    {
        private string _label = "";
        private Color _color;
        private bool _medal;

        public bool Medal
        {
            set
            {
                if (_medal == value) return;
                _medal = value;
                QueueRedraw();
            }
        }

        public void Set(string unit, byte owner)
        {
            _label = unit.Length > 6 ? unit[..6] : unit;
            _color = owner == 0 ? new Color("a8402c") : new Color("3d5a80");
            QueueRedraw();
        }

        public override void _Draw()
        {
            var ink = new Color("2b2119");
            DrawLine(new Vector2(0, 0), new Vector2(0, -34), ink, 3);
            var flag = new[] { new Vector2(0, -34), new Vector2(64, -26), new Vector2(0, -16) };
            DrawColoredPolygon(flag, _color);
            DrawPolyline(new[] { flag[0], flag[1], flag[2] }, ink, 2);
            DrawString(ThemeDB.FallbackFont, new Vector2(4, -38), _label, HorizontalAlignment.Left, -1, 16, ink);
            if (_medal) DrawCircle(new Vector2(-8, -20), 6, new Color("d9b44a"));
        }
    }

    /// <summary>En bylte på ryggen med varans namn, tills varorna har egna ikoner.</summary>
    private sealed partial class Parcel : Node2D
    {
        private string _label = "";
        private Color _color;

        public void Set(GoodDef good)
        {
            _label = good.Name.Length > 4 ? good.Name[..4] : good.Name;
            // Samma vara får alltid samma färg.
            _color = Color.FromHsv((good.Index * 0.137f) % 1f, 0.35f, 0.85f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var ink = new Color("2b2119");
            var r = new Rect2(-4, -4, 36, 22);
            DrawRect(r, _color);
            DrawRect(r, ink, false, 2);
            DrawString(ThemeDB.FallbackFont, new Vector2(-1, 13), _label, HorizontalAlignment.Left, -1, 14, ink);
        }
    }
}
