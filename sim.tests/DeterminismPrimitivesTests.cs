using Hallonkriget.Sim.Commands;
using Hallonkriget.Sim.Determinism;

namespace Hallonkriget.Sim.Tests;

public class FixedTests
{
    [Fact]
    public void ArithmeticIsExact()
    {
        var a = Fixed.FromRatio(3, 2);
        var b = Fixed.FromInt(2);
        Assert.Equal(Fixed.FromInt(3), a * b);
        Assert.Equal(Fixed.FromRatio(3, 4), a / b);
        Assert.Equal(Fixed.FromRatio(7, 2), a + b);
        Assert.Equal(Fixed.FromRatio(-1, 2), a - b);
    }

    [Fact]
    public void FloorRoundsDown()
    {
        Assert.Equal(1, Fixed.FromRatio(3, 2).Floor);
        Assert.Equal(-2, Fixed.FromRatio(-3, 2).Floor);
    }

    [Fact]
    public void LengthOfThreeFourIsFive()
    {
        Assert.Equal(Fixed.FromInt(5), Fixed.Length(Fixed.FromInt(3), Fixed.FromInt(4)));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(15L, 3L)]
    [InlineData(16L, 4L)]
    [InlineData(1_000_000_000_000L, 1_000_000L)]
    [InlineData(long.MaxValue, 3_037_000_499L)]
    public void IntegerSqrt(long value, long expected)
    {
        Assert.Equal(expected, IntMath.Sqrt(value));
    }
}

public class RngTests
{
    [Fact]
    public void SequenceIsFixed()
    {
        // Första talen för fröet 1 med xorshift64 (13, 7, 17). Ska vara lika på alla plattformar.
        var rng = new Rng(1);
        Assert.Equal(1082269761UL, rng.NextULong());
        Assert.Equal(1152992998833853505UL, rng.NextULong());
    }

    [Fact]
    public void NextStaysInRange()
    {
        var rng = new Rng(123);
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.Range(-5, 5);
            Assert.InRange(v, -5, 5);
        }
    }

    [Fact]
    public void ZeroSeedDoesNotGetStuck()
    {
        var rng = new Rng(0);
        Assert.NotEqual(0UL, rng.NextULong());
    }
}

public class CommandCodecTests
{
    [Fact]
    public void RoundTrip()
    {
        var commands = new List<Command>
        {
            new(0, 0, CommandType.SpawnWalker, 10, 20),
            new(123_456, 3, CommandType.MoveWalker, 7, -4, int.MaxValue),
            new(5, 255, CommandType.None, int.MinValue),
        };
        Assert.Equal(commands, CommandCodec.Decode(CommandCodec.Encode(commands)));
    }

    [Fact]
    public void TypicalCommandIsSmall()
    {
        // Designdokumentet räknar med 8–16 byte per kommando.
        var bytes = CommandCodec.Encode(new[] { new Command(36_000, 1, CommandType.MoveWalker, 120, 64, 80) });
        Assert.InRange(bytes.Length - 1, 5, 16);
    }
}
