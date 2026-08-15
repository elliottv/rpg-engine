using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="Direction"/> and <see cref="DirectionExtensions"/>
/// (stories 4 and 21: core primitives — Direction and Position, plus 8-direction support).
/// </summary>
public class DirectionTests
{
    /// <summary>
    /// Verifies <see cref="DirectionExtensions.Delta"/> returns the correct screen-space unit
    /// vector for every direction. Cardinal values are exact; diagonal values are normalized
    /// (each component is ±√½) so their magnitude is 1 and diagonal movement is exactly as fast
    /// as cardinal movement.
    /// </summary>
    [Theory]
    [InlineData(Direction.Down, 0, 1, false)]
    [InlineData(Direction.Left, -1, 0, false)]
    [InlineData(Direction.Right, 1, 0, false)]
    [InlineData(Direction.Up, 0, -1, false)]
    [InlineData(Direction.DownLeft, -1, 1, true)]
    [InlineData(Direction.DownRight, 1, 1, true)]
    [InlineData(Direction.UpLeft, -1, -1, true)]
    [InlineData(Direction.UpRight, 1, -1, true)]
    public void Delta_IsCorrect_ForAllDirections(Direction direction, int signX, int signY, bool isDiagonal)
    {
        var delta = direction.Delta();

        if (isDiagonal)
        {
            var component = Math.Sqrt(0.5);
            Assert.Equal(signX * component, delta.X, precision: 9);
            Assert.Equal(signY * component, delta.Y, precision: 9);
        }
        else
        {
            Assert.Equal(signX, delta.X);
            Assert.Equal(signY, delta.Y);
        }

        // Every delta is a unit vector, so diagonal movement is no faster than cardinal movement.
        Assert.Equal(1, Magnitude(delta), precision: 9);
    }

    /// <summary>Verifies <see cref="DirectionExtensions.Opposite"/> returns the opposite direction for every direction.</summary>
    [Theory]
    [InlineData(Direction.Down, Direction.Up)]
    [InlineData(Direction.Left, Direction.Right)]
    [InlineData(Direction.Right, Direction.Left)]
    [InlineData(Direction.Up, Direction.Down)]
    [InlineData(Direction.DownLeft, Direction.UpRight)]
    [InlineData(Direction.DownRight, Direction.UpLeft)]
    [InlineData(Direction.UpLeft, Direction.DownRight)]
    [InlineData(Direction.UpRight, Direction.DownLeft)]
    public void Opposite_IsCorrect_ForAllDirections(Direction direction, Direction expected)
    {
        Assert.Equal(expected, direction.Opposite());
    }

    /// <summary>
    /// Verifies <see cref="DirectionExtensions.RowIndex"/> equals the RPG Maker MZ sprite-sheet
    /// row (0/1/2/3) for Down/Left/Right/Up and falls back to the horizontal component's row for
    /// diagonals (DownLeft/UpLeft → 1, DownRight/UpRight → 2) so a diagonally-facing character
    /// renders with the side-view row.
    /// </summary>
    [Theory]
    [InlineData(Direction.Down, 0)]
    [InlineData(Direction.Left, 1)]
    [InlineData(Direction.Right, 2)]
    [InlineData(Direction.Up, 3)]
    [InlineData(Direction.DownLeft, 1)]
    [InlineData(Direction.DownRight, 2)]
    [InlineData(Direction.UpLeft, 1)]
    [InlineData(Direction.UpRight, 2)]
    public void RowIndex_EqualsSpriteSheetRow_ForAllDirections(Direction direction, int expectedRow)
    {
        Assert.Equal(expectedRow, direction.RowIndex());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.IsHorizontal"/> distinguishes horizontal from vertical and diagonal directions.</summary>
    [Theory]
    [InlineData(Direction.Down, false)]
    [InlineData(Direction.Left, true)]
    [InlineData(Direction.Right, true)]
    [InlineData(Direction.Up, false)]
    [InlineData(Direction.DownLeft, false)]
    [InlineData(Direction.DownRight, false)]
    [InlineData(Direction.UpLeft, false)]
    [InlineData(Direction.UpRight, false)]
    public void IsHorizontal_IsCorrect_ForAllDirections(Direction direction, bool expected)
    {
        Assert.Equal(expected, direction.IsHorizontal());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.IsVertical"/> distinguishes vertical from horizontal and diagonal directions.</summary>
    [Theory]
    [InlineData(Direction.Down, true)]
    [InlineData(Direction.Left, false)]
    [InlineData(Direction.Right, false)]
    [InlineData(Direction.Up, true)]
    [InlineData(Direction.DownLeft, false)]
    [InlineData(Direction.DownRight, false)]
    [InlineData(Direction.UpLeft, false)]
    [InlineData(Direction.UpRight, false)]
    public void IsVertical_IsCorrect_ForAllDirections(Direction direction, bool expected)
    {
        Assert.Equal(expected, direction.IsVertical());
    }

    /// <summary>Verifies <see cref="DirectionExtensions.IsDiagonal"/> is true for the four diagonals and false for the cardinals.</summary>
    [Theory]
    [InlineData(Direction.Down, false)]
    [InlineData(Direction.Left, false)]
    [InlineData(Direction.Right, false)]
    [InlineData(Direction.Up, false)]
    [InlineData(Direction.DownLeft, true)]
    [InlineData(Direction.DownRight, true)]
    [InlineData(Direction.UpLeft, true)]
    [InlineData(Direction.UpRight, true)]
    public void IsDiagonal_IsCorrect_ForAllDirections(Direction direction, bool expected)
    {
        Assert.Equal(expected, direction.IsDiagonal());
    }

    /// <summary>Returns the Euclidean magnitude of <paramref name="v"/>.</summary>
    private static double Magnitude(Vector2 v) => Math.Sqrt((v.X * v.X) + (v.Y * v.Y));
}
