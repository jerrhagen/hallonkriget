using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Hallonkriget.Game;

/// <summary>
/// Pysslingen som klippdocka: sju delar från art/people/pyssling_rig.json, sammansatta
/// i ritordning och animerade med en AnimationPlayer i 12 bilder per sekund.
/// Nodens position är fötterna.
/// </summary>
public partial class Pyssling : Node2D
{
    private const string ArtDir = "res://art/people/";
    private const int Fps = 12;

    [Export] public bool CarryingEgg { get; set; } = true;

    private readonly Dictionary<string, Node2D> _parts = new();
    private Node2D _rig = null!;
    private AnimationPlayer _player = null!;

    public override void _Ready()
    {
        var rig = JsonDocument.Parse(FileAccess.GetFileAsString(ArtDir + "pyssling_rig.json")).RootElement;
        var feet = ReadVector(rig.GetProperty("feet"));
        var pivots = rig.GetProperty("pivots");

        _rig = new Node2D { Name = "Rig" };
        AddChild(_rig);

        // Platt struktur: ritordningen är nodordningen. Delarna som sitter på kroppen
        // följer med i animationen genom att deras positioner nycklas tillsammans med kroppens.
        foreach (var part in rig.GetProperty("order").EnumerateArray())
        {
            string name = part.GetString()!;
            var pivot = ReadVector(pivots.GetProperty(name));
            var node = new Node2D { Name = name, Position = pivot - feet };
            node.AddChild(new Sprite2D
            {
                // 2×-bilden skalas ner, så att figuren är skarp även inzoomad.
                Texture = GD.Load<Texture2D>($"{ArtDir}pyssling_{name}@2x.png"),
                Centered = false,
                Position = -pivot,
                Scale = new Vector2(0.5f, 0.5f),
            });
            _rig.AddChild(node);
            _parts[name] = node;
        }
        _parts["agg"].Visible = CarryingEgg;

        _player = new AnimationPlayer { Name = "AnimationPlayer" };
        AddChild(_player);
        var library = new AnimationLibrary();
        library.AddAnimation("ga", BuildWalk());
        library.AddAnimation("vila", BuildIdle());
        _player.AddAnimationLibrary("", library);
        _player.Play("vila");
    }

    /// <summary>Gå åt höger (1) eller vänster (-1). Noll betyder stå still.</summary>
    public void SetWalking(int direction)
    {
        if (direction != 0) _rig.Scale = new Vector2(direction, 1);
        string anim = direction == 0 ? "vila" : "ga";
        if (_player.CurrentAnimation != anim) _player.Play(anim);
    }

    public void SetCarryingEgg(bool carrying)
    {
        CarryingEgg = carrying;
        if (_parts.TryGetValue("agg", out var egg)) egg.Visible = carrying;
    }

    private Animation BuildWalk()
    {
        // Åtta bilder per steg-par. Värdena räknas fram här men spelas upp stegvis,
        // så att rörelsen blir hackig som i tecknad film.
        const int frames = 8;
        var anim = NewAnimation(frames);
        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Fps;
            float phase = i / (float)frames * Mathf.Tau;
            float s = Mathf.Sin(phase);
            float bob = -2.0f * (1 - Mathf.Abs(s));

            Key(anim, "ben_fram", "rotation", t, 0.5f * s);
            Key(anim, "ben_bak", "rotation", t, -0.5f * s);
            Key(anim, "arm_fram", "rotation", t, -0.55f * s);
            Key(anim, "arm_bak", "rotation", t, 0.55f * s);
            Key(anim, "huvud", "rotation", t, 0.06f * Mathf.Sin(phase * 2));
            Key(anim, "agg", "rotation", t, -0.08f * Mathf.Sin(phase * 2 + 0.6f));
            foreach (var upper in new[] { "kropp", "huvud", "arm_fram", "arm_bak", "agg" })
                Key(anim, upper, "position", t, BasePosition(upper) + new Vector2(0, bob));
        }
        return anim;
    }

    private Animation BuildIdle()
    {
        // Andas: kroppen sjunker en pixel och huvudet nickar lite, fyra bilder i sekunden.
        const int frames = 12;
        var anim = NewAnimation(frames);
        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)Fps;
            float breath = i < frames / 2 ? 0 : 1;
            foreach (var part in _parts.Keys)
            {
                Key(anim, part, "rotation", t, part == "huvud" ? (i is >= 3 and < 6 ? 0.08f : 0f) : 0f);
                bool upper = part is "kropp" or "huvud" or "arm_fram" or "arm_bak" or "agg";
                Key(anim, part, "position", t, BasePosition(part) + new Vector2(0, upper ? breath : 0));
            }
        }
        return anim;
    }

    private readonly Dictionary<string, Vector2> _basePositions = new();

    private Vector2 BasePosition(string part)
    {
        if (!_basePositions.TryGetValue(part, out var pos))
        {
            pos = _parts[part].Position;
            _basePositions[part] = pos;
        }
        return pos;
    }

    private static Animation NewAnimation(int frames)
    {
        return new Animation { Length = frames / (float)Fps, LoopMode = Animation.LoopModeEnum.Linear };
    }

    private void Key(Animation anim, string part, string property, float time, Variant value)
    {
        var path = new NodePath($"Rig/{part}:{property}");
        int track = anim.FindTrack(path, Animation.TrackType.Value);
        if (track < 0)
        {
            track = anim.AddTrack(Animation.TrackType.Value);
            anim.TrackSetPath(track, path);
            anim.ValueTrackSetUpdateMode(track, Animation.UpdateMode.Discrete);
        }
        anim.TrackInsertKey(track, time, value);
    }

    private static Vector2 ReadVector(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle());
}
