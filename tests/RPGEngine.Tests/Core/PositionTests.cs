using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="Position"/> (story 4: core primitives — Direction and Position).
/// </summary>
public class PositionTests
{
    /// <summary>Verifies <c>Position + Vector2</c> adds the components and returns a new position.</summary>
    [Fact]
    public void AddVector2_ReturnsExpectedPosition()
    {
        var position = new Position(10, 20) + new Vector2(3, -4);

        Assert.Equal(new Position(13, 16), position);
    }

    /// <summary>Verifies <c>Position - Vector2</c> subtracts the components and returns a new position.</summary>
    [Fact]
    public void SubtractVector2_ReturnsExpectedPosition()
    {
        var position = new Position(10, 20) - new Vector2(3, -4);

        Assert.Equal(new Position(7, 24), position);
    }

    /// <summary>Verifies <c>Position - Position</c> returns the component-wise difference as a <see cref="Vector2"/>.</summary>
    [Fact]
    public void SubtractPosition_ReturnsExpectedVector()
    {
        var delta = new Position(10, 20) - new Position(4, 6);

        Assert.Equal(new Vector2(6, 14), delta);
    }

    /// <summary>Verifies <c>==</c>/<c>!=</c> use record value equality with exact comparison.</summary>
    [Fact]
    public void Equality_UsesValueSemantics()
    {
        Assert.Equal(new Position(1, 2), new Position(1, 2));
        Assert.NotEqual(new Position(1, 2), new Position(1, 3));

        Assert.True(new Position(1, 2) == new Position(1, 2));
        Assert.True(new Position(1, 2) != new Position(2, 2));
    }

    /// <summary>Verifies <see cref="Position.WithOffset"/> returns a new position offset by the given deltas.</summary>
    [Fact]
    public void WithOffset_ReturnsExpectedPosition()
    {
        var offset = new Position(10, 20).WithOffset(2.5, -3.5);

        Assert.Equal(new Position(12.5, 16.5), offset);
    }

    /// <summary>Verifies <see cref="Position.ToTile"/> floors correctly for positive and negative pixel positions.</summary>
    [Theory]
    [InlineData(95, 95, 48, 1, 1)]
    [InlineData(0, 0, 48, 0, 0)]
    [InlineData(47, 47, 48, 0, 0)]
    [InlineData(48, 48, 48, 1, 1)]
    [InlineData(-1, -1, 48, -1, -1)]
    [InlineData(-48, -48, 48, -1, -1)]
    [InlineData(-49, -49, 48, -2, -2)]
    public void ToTile_FloorsCorrectly(double x, double y, int tileSize, int expectedTileX, int expectedTileY)
    {
        var (tileX, tileY) = new Position(x, y).ToTile(tileSize);

        Assert.Equal(expectedTileX, tileX);
        Assert.Equal(expectedTileY, tileY);
    }

    /// <summary>Verifies <see cref="Position.DistanceTo"/> computes the Euclidean distance.</summary>
    [Fact]
    public void DistanceTo_IsCorrect()
    {
        // A 3-4-5 right triangle.
        var distance = new Position(0, 0).DistanceTo(new Position(3, 4));

        Assert.Equal(5, distance, precision: 10);
    }
}
