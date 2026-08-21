using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="Position"/> (story 4: core primitives — Direction and Position,
/// updated by story 37: tile-based world coordinates).
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

    /// <summary>
    /// Verifies <see cref="Position.ToTile"/> floors the tile-unit position to the containing
    /// cell. Positions are already in tiles, so the floor is applied directly; negative values
    /// round toward negative infinity (acceptance criterion 3: (8.5, 8.5) → (8, 8) and
    /// (-1.5, -1.5) → (-2, -2)).
    /// </summary>
    [Theory]
    [InlineData(8.5, 8.5, 8, 8)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0.9, 0.9, 0, 0)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(1.5, 1.5, 1, 1)]
    [InlineData(-1, -1, -1, -1)]
    [InlineData(-1.5, -1.5, -2, -2)]
    [InlineData(-0.1, -0.1, -1, -1)]
    public void ToTile_FloorsToContainingCell(double x, double y, int expectedTileX, int expectedTileY)
    {
        var (tileX, tileY) = new Position(x, y).ToTile();

        Assert.Equal(expectedTileX, tileX);
        Assert.Equal(expectedTileY, tileY);
    }

    /// <summary>
    /// Verifies <see cref="Position.ToPixels"/> converts tile coordinates to pixels by
    /// multiplying by the tile size (acceptance criterion 3: (8.5, 8.5).ToPixels(48) →
    /// (408, 408)).
    /// </summary>
    [Theory]
    [InlineData(8.5, 8.5, 48, 408, 408)]
    [InlineData(0, 0, 48, 0, 0)]
    [InlineData(1, 2, 32, 32, 64)]
    [InlineData(-1.5, 2.5, 48, -72, 120)]
    public void ToPixels_MultipliesByTileSize(double x, double y, int tileSize, double expectedX, double expectedY)
    {
        var pixels = new Position(x, y).ToPixels(tileSize);

        Assert.Equal(expectedX, pixels.X);
        Assert.Equal(expectedY, pixels.Y);
    }

    /// <summary>Verifies <see cref="Position.ToPixels"/> rejects a non-positive tile size.</summary>
    [Fact]
    public void ToPixels_NonPositiveTileSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(1, 1).ToPixels(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(1, 1).ToPixels(-1));
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
