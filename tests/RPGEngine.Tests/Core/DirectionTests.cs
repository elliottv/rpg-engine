using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="Direction"/> and <see cref="DirectionExtensions"/> after the
/// Direction rework (story 74): <see cref="Direction"/> is an immutable 2-D vector value type
/// whose eight canonical unit directions (<see cref="Direction.All"/>) preserve the old 8
/// directions, and <see cref="DirectionExtensions.Nearest8"/>/<see cref="DirectionExtensions.RowIndex"/>/
/// <see cref="DirectionExtensions.Opposite"/> adapt it to the 8-direction sprite/key model.
/// </summary>
public class DirectionTests
{
    /// <summary>The exact √½ diagonal component shared with the library's canonical diagonals.</summary>
    private const double RootHalf = 0.7071067811865476;

    /// <summary>
    /// Verifies the eight canonical unit directions have exactly the documented components:
    /// cardinals are exact axis vectors and diagonals are <c>(±√½, ±√½)</c>
    /// (normalized, so diagonal movement is as fast as cardinal movement).
    /// </summary>
    [Theory]
    [MemberData(nameof(CanonicalComponents))]
    public void CanonicalStatics_HaveExactUnitComponents(Direction direction, double expectedX, double expectedY)
    {
        Assert.Equal(expectedX, direction.X);
        Assert.Equal(expectedY, direction.Y);

        // Every canonical direction is a unit vector.
        Assert.Equal(1, direction.Length, precision: 12);
        Assert.False(direction.IsZero);
    }

    /// <summary>
    /// Verifies <see cref="Direction.All"/> contains exactly the eight canonical unit directions,
    /// in the historical enum order (Down, Left, Right, Up, DownLeft, DownRight, UpLeft, UpRight).
    /// </summary>
    [Fact]
    public void All_ContainsExactlyTheEightCanonicalDirectionsInHistoricalOrder()
    {
        Assert.Equal(
            new[]
            {
                Direction.Down,
                Direction.Left,
                Direction.Right,
                Direction.Up,
                Direction.DownLeft,
                Direction.DownRight,
                Direction.UpLeft,
                Direction.UpRight,
            },
            Direction.All);

        // All eight are distinct and unit-length.
        Assert.Equal(8, Direction.All.Distinct().Count());
        Assert.All(Direction.All, d => Assert.Equal(1, d.Length, precision: 12));
    }

    /// <summary>Verifies vector negation / <see cref="DirectionExtensions.Opposite"/>: canonical opposites and exact continuous negation.</summary>
    [Fact]
    public void Negation_AndOpposite_AreExact()
    {
        Assert.Equal(Direction.Up, -Direction.Down); // negation of Down is Up
        Assert.Equal(Direction.Up, Direction.Down.Opposite());
        Assert.Equal(Direction.Down, Direction.Up.Opposite());
        Assert.Equal(Direction.Right, Direction.Left.Opposite());
        Assert.Equal(Direction.Left, Direction.Right.Opposite());

        // Diagonals flip both signs (DownLeft <-> UpRight, DownRight <-> UpLeft).
        Assert.Equal(Direction.UpRight, Direction.DownLeft.Opposite());
        Assert.Equal(Direction.DownLeft, Direction.UpRight.Opposite());
        Assert.Equal(Direction.UpLeft, Direction.DownRight.Opposite());
        Assert.Equal(Direction.DownRight, Direction.UpLeft.Opposite());

        // operator - negates each component exactly, so it stays well-defined for continuous vectors.
        Assert.Equal(new Direction(-0.6, 0.8), -new Direction(0.6, -0.8));
        Assert.Equal(new Direction(-0.6, 0.8), new Direction(0.6, -0.8).Opposite());
        Assert.Equal(new Direction(0, 0), -new Direction(0, 0));
    }

    /// <summary>
    /// Verifies <see cref="DirectionExtensions.Nearest8"/> snaps continuous vectors to the closest
    /// canonical unit direction and maps the zero vector to <see cref="Direction.Down"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Nearest8Cases))]
    public void Nearest8_SnapsToExpectedCanonicalDirection(Direction input, Direction expected)
    {
        Assert.Equal(expected, input.Nearest8());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.Nearest8"/> leaves each canonical direction unchanged.</summary>
    [Fact]
    public void Nearest8_LeavesCanonicalDirectionsUnchanged()
    {
        foreach (var canonical in Direction.All)
        {
            Assert.Equal(canonical, canonical.Nearest8());
        }
    }

    /// <summary>
    /// Verifies <see cref="DirectionExtensions.RowIndex"/> maps the canonical directions to the
    /// RPG Maker MZ sheet rows 0/1/2/3/1/2/1/2 (diagonals fall back to their horizontal
    /// component's row) and maps continuous vectors to the row of their nearest canonical direction.
    /// </summary>
    [Theory]
    [MemberData(nameof(RowIndexCases))]
    public void RowIndex_IsCorrect(Direction direction, int expectedRow)
    {
        Assert.Equal(expectedRow, direction.RowIndex());
    }

    /// <summary>
    /// Verifies <see cref="Direction.Length"/>, <see cref="Direction.IsZero"/> and
    /// <see cref="Direction.Normalized"/>: the zero vector is zero (and throws on
    /// <c>Normalized</c>), and a non-unit vector normalizes to unit length with the same direction.
    /// </summary>
    [Fact]
    public void Length_IsZero_AndNormalized()
    {
        var zero = new Direction(0, 0);
        Assert.True(zero.IsZero);
        Assert.Equal(0, zero.Length);
        Assert.Throws<InvalidOperationException>(() => zero.Normalized);

        var vector = new Direction(3, 4);
        Assert.False(vector.IsZero);
        Assert.Equal(5, vector.Length, precision: 12);

        var normalized = vector.Normalized;
        Assert.Equal(1, normalized.Length, precision: 12);
        Assert.Equal(0.6, normalized.X, precision: 12);
        Assert.Equal(0.8, normalized.Y, precision: 12);
    }

    /// <summary>
    /// Verifies <see cref="Direction.ToString"/> returns the canonical name for each canonical
    /// direction and the component pair for a continuous direction.
    /// </summary>
    [Fact]
    public void ToString_ReturnsCanonicalNameForCanonicalDirections()
    {
        Assert.Equal("Down", Direction.Down.ToString());
        Assert.Equal("Left", Direction.Left.ToString());
        Assert.Equal("Right", Direction.Right.ToString());
        Assert.Equal("Up", Direction.Up.ToString());
        Assert.Equal("DownLeft", Direction.DownLeft.ToString());
        Assert.Equal("DownRight", Direction.DownRight.ToString());
        Assert.Equal("UpLeft", Direction.UpLeft.ToString());
        Assert.Equal("UpRight", Direction.UpRight.ToString());

        // A continuous (non-canonical) vector prints its components.
        Assert.Equal("(0.6, 0.8)", new Direction(0.6, 0.8).ToString());

        // A vector numerically equal to a canonical diagonal prints the canonical name (the
        // components are bit-exact with the canonical constant).
        Assert.Equal("UpRight", new Direction(RootHalf, -RootHalf).ToString());
    }

    /// <summary>The canonical directions and their exact components.</summary>
    public static TheoryData<Direction, double, double> CanonicalComponents => new()
    {
        { Direction.Down, 0, 1 },
        { Direction.Left, -1, 0 },
        { Direction.Right, 1, 0 },
        { Direction.Up, 0, -1 },
        { Direction.DownLeft, -RootHalf, RootHalf },
        { Direction.DownRight, RootHalf, RootHalf },
        { Direction.UpLeft, -RootHalf, -RootHalf },
        { Direction.UpRight, RootHalf, -RootHalf },
    };

    /// <summary>Continuous vectors and the canonical direction <see cref="DirectionExtensions.Nearest8"/> must snap them to.</summary>
    public static TheoryData<Direction, Direction> Nearest8Cases => new()
    {
        { new Direction(0.9, 0.2), Direction.Right },
        { new Direction(-0.9, 0.2), Direction.Left },
        { new Direction(0.03, -0.99), Direction.Up },
        { new Direction(-0.03, -0.99), Direction.Up },
        { new Direction(0.9, -0.2), Direction.Right },
        { new Direction(-0.9, -0.2), Direction.Left },
        { new Direction(0.2, 0.9), Direction.Down },
        { new Direction(0.2, -0.9), Direction.Up },
        { new Direction(0, 0), Direction.Down }, // the zero vector snaps to Down
    };

    /// <summary>Directions and their expected RPG Maker MZ sprite-sheet rows (canonical and continuous).</summary>
    public static TheoryData<Direction, int> RowIndexCases => new()
    {
        { Direction.Down, 0 },
        { Direction.Left, 1 },
        { Direction.Right, 2 },
        { Direction.Up, 3 },
        { Direction.DownLeft, 1 },
        { Direction.DownRight, 2 },
        { Direction.UpLeft, 1 },
        { Direction.UpRight, 2 },
        // A continuous vector maps to the row of its nearest canonical direction.
        { new Direction(0.9, 0.2), 2 },        // -> Right
        { new Direction(-0.9, 0.2), 1 },       // -> Left
        { new Direction(0.03, -0.99), 3 },     // -> Up
        { new Direction(0.2, 0.9), 0 },        // -> Down
    };
}
