using System;
using System.Collections.Generic;
using System.IO;
using Hallonkriget.Sim;
using Hallonkriget.Sim.Ai;
using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Data;
using Hallonkriget.Sim.Map;
using SimReplay = Hallonkriget.Sim.Replay.Replay;

namespace Hallonkriget.Game.Net;

/// <summary>
/// En match på den här datorn. Gränssnittet ändrar aldrig spelet direkt: ett klick blir ett
/// kommando i kön, och kommandona körs i nästa tick. I enspelarläge är "nätverket" bara den här
/// kön. Allt som körs spelas in i en repris, som också är sparfilen.
/// </summary>
public sealed class LocalMatch
{
    public const double TickSeconds = 1.0 / GameState.TicksPerSecond;

    /// <summary>Högst så många tick per bildruta, så att en långsam dator inte hamnar i en spiral.</summary>
    private const int MaxTicksPerFrame = 20;

    public GameState State { get; private set; }
    public GameData Data { get; }
    public SimReplay Replay { get; private set; }
    public byte LocalPlayer { get; }

    /// <summary>0 är paus, 1 normal och 2 dubbel hastighet.</summary>
    public int Speed { get; set; } = 1;

    /// <summary>Hur långt mot nästa tick vi har kommit, 0–1. Vyn ritar mittemellan.</summary>
    public float Alpha => (float)(_accumulator / TickSeconds);

    /// <summary>Efter varje tick, så att vyn kan spara läget att rita från.</summary>
    public event Action? Ticked;

    /// <summary>En datorspelare som spelar en byggordning åt någon, till exempel i en visning.</summary>
    public BuildOrderPlayer? Autopilot { get; set; }

    private readonly List<Command> _pending = new();
    private readonly List<Command> _tickCommands = new();
    private double _accumulator;

    public LocalMatch(GameData data, MapDef map, ulong seed, Faction faction)
    {
        Data = data;
        LocalPlayer = 0;
        var setup = MatchSetup.OnMap(seed, map, data, (faction, false));
        State = GameState.NewMatch(setup);
        Replay = new SimReplay(seed, map, setup.Players, data.Fingerprint);
    }

    private LocalMatch(GameData data, SimReplay replay, GameState state)
    {
        Data = data;
        Replay = replay;
        State = state;
        LocalPlayer = 0;
    }

    /// <summary>Ett kommando från spelaren. Det körs i nästa tick.</summary>
    public void Submit(CommandType type, int a = 0, int b = 0, int c = 0) =>
        _pending.Add(new Command(0, LocalPlayer, type, a, b, c));

    /// <summary>Kör de tick som hunnit gå sedan förra bildrutan.</summary>
    public void Update(double delta)
    {
        if (Speed == 0) return;
        _accumulator += delta * Speed;
        int ticks = 0;
        while (_accumulator >= TickSeconds && ticks < MaxTicksPerFrame)
        {
            Step();
            _accumulator -= TickSeconds;
            ticks++;
        }
        if (ticks == MaxTicksPerFrame) _accumulator = 0;
    }

    /// <summary>Ett tick, med de kommandon som väntar.</summary>
    public void Step()
    {
        _tickCommands.Clear();
        foreach (var c in _pending) _tickCommands.Add(c with { Tick = State.TickCount });
        _pending.Clear();
        if (Autopilot is not null) _tickCommands.AddRange(Autopilot.Next(State));
        Replay.Record(State.TickCount, _tickCommands);
        State.Tick(_tickCommands);
        Replay.AfterTick(State);
        Ticked?.Invoke();
    }

    /// <summary>Spolar fram utan att rita, till exempel för en skärmbild.</summary>
    public void FastForward(int ticks)
    {
        for (int i = 0; i < ticks; i++) Step();
    }

    public void Save(string path) => File.WriteAllBytes(path, Replay.ToBytes());

    /// <summary>Läser en sparfil och spelar upp den till där den sparades.</summary>
    public static LocalMatch Load(string path, GameData data)
    {
        var replay = SimReplay.FromBytes(File.ReadAllBytes(path));
        var state = replay.Play(data);
        return new LocalMatch(data, replay, state);
    }
}
