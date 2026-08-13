using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="Direction"/> and <see cref="DirectionExtensions"/>
/// (story 4: core primitives — Direction and Position).
/// </summary>
public class DirectionTests
{
    /// <summary>Verifies <see cref="DirectionExtensions.Delta"/> returns the correct screen-space unit vector for every direction.</summary>
    [Theory]
    [InlineData(Direction.Down, 0, 1)]
    [InlineData(Direction.Left, -1, 0)]
    [InlineData(Direction.Right, 1, 0)]
    [InlineData(Direction.Up, 0, -1)]
    public void Delta_IsCorrect_ForAllDirections(Direction direction, double expectedX, double expectedY)
    {
        var delta = direction.Delta();

        Assert.Equal(expectedX, delta.X);
        Assert.Equal(expectedY, delta.Y);
    }

    /// <summary>Verifies <see cref="DirectionExtensions.Opposite"/> returns the opposite direction for every direction.</summary>
    [Theory]
    [InlineData(Direction.Down, Direction.Up)]
    [InlineData(Direction.Left, Direction.Right)]
    [InlineData(Direction.Right, Direction.Left)]
    [InlineData(Direction.Up, Direction.Down)]
    public void Opposite_IsCorrect_ForAllDirections(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Opposite());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.RowIndex"/> equals the RPG Maker MZ sprite-sheet row (0/1/2/3) for Down/Left/Right/Up.</summary>
    [Theory]
    [InlineData(Direction.Down, 0)]
    [InlineData(Direction.Left, 1)]
    [InlineData(Direction.Right, 2)]
    [InlineData(Direction.Up, 3)]
    public void RowIndex_EqualsSpriteSheetRow_ForAllDirections(Direction direction, int expectedRow)
    {
        Assert.Equal(expectedRow, direction.RowIndex());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.IsHorizontal"/> distinguishes horizontal from vertical directions.</summary>
    [Theory]
    [InlineData(Direction.Down, false)]
    [InlineData(Direction.Left, true)]
    [InlineData(Direction.Right, true)]
    [InlineData(Direction.Up, false)]
    public void IsHorizontal_IsCorrect_ForAllDirections(Direction direction, bool expected)
    {
        Assert.Equal(expected, direction.IsHorizontal());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.IsVertical"/> distinguishes vertical from horizontal directions.</summary>
    [Theory]
    [InlineData(Direction.Down, true)]
    [InlineData(Direction.Left, false)]
    [InlineData(Direction.Right, false)]
    [InlineData(Direction.Up, true)]
    public void IsVertical_IsCorrect_ForAllDirections(Direction direction, bool expected)
    {
        Assert.Equal(expected, direction.IsVertical());
    }
}
