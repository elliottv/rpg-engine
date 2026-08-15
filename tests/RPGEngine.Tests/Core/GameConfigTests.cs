using RPGEngine;
using Xunit;

namespace RPGEngine.Tests.Core;

/// <summary>
/// Acceptance tests for <see cref="GameConfig"/> and <see cref="Key"/>
/// (story 8: GameConfig — movement key mapping with WASD defaults).
/// </summary>
public class GameConfigTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: defaults are W/S/A/D and GetDirection returns
    // Up/Down/Left/Right respectively.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the default bindings are W/S/A/D and that GetDirection maps them to Up/Down/Left/Right.</summary>
    [Fact]
    public void Defaults_AreWAsd_AndGetDirectionReturnsExpectedDirections()
    {
        var config = new GameConfig();

        Assert.Equal(Key.W, config.UpKey);
        Assert.Equal(Key.S, config.DownKey);
        Assert.Equal(Key.A, config.LeftKey);
        Assert.Equal(Key.D, config.RightKey);

        Assert.Equal(Direction.Up, config.GetDirection(Key.W));
        Assert.Equal(Direction.Down, config.GetDirection(Key.S));
        Assert.Equal(Direction.Left, config.GetDirection(Key.A));
        Assert.Equal(Direction.Right, config.GetDirection(Key.D));
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: reassigning a key takes effect immediately; the old key
    // is no longer mapped.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that reassigning UpKey to Z makes GetDirection(Z) return Up immediately and the old key W return null.</summary>
    [Fact]
    public void ReassigningUpKey_TakesEffectImmediately()
    {
        var config = new GameConfig();

        config.UpKey = Key.Z;

        Assert.Equal(Direction.Up, config.GetDirection(Key.Z));
        Assert.Null(config.GetDirection(Key.W));
    }

    /// <summary>Verifies that reassigning any direction key takes effect immediately and unbinds the previous key.</summary>
    [Theory]
    [InlineData("UpKey", Key.P, Direction.Up, Key.W)]
    [InlineData("DownKey", Key.P, Direction.Down, Key.S)]
    [InlineData("LeftKey", Key.P, Direction.Left, Key.A)]
    [InlineData("RightKey", Key.P, Direction.Right, Key.D)]
    public void ReassigningAnyDirectionKey_TakesEffectImmediately(
        string propertyName,
        Key newKey,
        Direction expectedDirection,
        Key previousKey)
    {
        var config = new GameConfig();

        switch (propertyName)
        {
            case "UpKey":
                config.UpKey = newKey;
                break;
            case "DownKey":
                config.DownKey = newKey;
                break;
            case "LeftKey":
                config.LeftKey = newKey;
                break;
            case "RightKey":
                config.RightKey = newKey;
                break;
        }

        Assert.Equal(expectedDirection, config.GetDirection(newKey));
        Assert.Null(config.GetDirection(previousKey));
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: assigning a key already used by another direction throws
    // ArgumentException and leaves the config unchanged.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that binding an already-used key throws ArgumentException and leaves the configuration unchanged.</summary>
    [Theory]
    [InlineData("UpKey", Key.S, Direction.Down)]    // S is bound to Down
    [InlineData("UpKey", Key.A, Direction.Left)]    // A is bound to Left
    [InlineData("DownKey", Key.W, Direction.Up)]    // W is bound to Up
    [InlineData("DownKey", Key.D, Direction.Right)] // D is bound to Right
    [InlineData("LeftKey", Key.W, Direction.Up)]    // W is bound to Up
    [InlineData("LeftKey", Key.S, Direction.Down)]  // S is bound to Down
    [InlineData("RightKey", Key.S, Direction.Down)] // S is bound to Down
    [InlineData("RightKey", Key.A, Direction.Left)] // A is bound to Left
    public void AssigningKeyAlreadyUsedByAnotherDirection_ThrowsAndLeavesConfigUnchanged(
        string propertyName,
        Key conflictingKey,
        Direction originalDirection)
    {
        var config = new GameConfig();

        var exception = Record.Exception(() => Set(config, propertyName, conflictingKey));

        Assert.IsType<ArgumentException>(exception);

        // The configuration must be left exactly as it was.
        Assert.Equal(Key.W, config.UpKey);
        Assert.Equal(Key.S, config.DownKey);
        Assert.Equal(Key.A, config.LeftKey);
        Assert.Equal(Key.D, config.RightKey);

        Assert.Equal(Direction.Up, config.GetDirection(Key.W));
        Assert.Equal(Direction.Down, config.GetDirection(Key.S));
        Assert.Equal(Direction.Left, config.GetDirection(Key.A));
        Assert.Equal(Direction.Right, config.GetDirection(Key.D));

        // The conflicting key keeps its original binding: nothing changed.
        Assert.Equal(originalDirection, config.GetDirection(conflictingKey));
    }

    /// <summary>Verifies that setting a property to its current value is a harmless no-op (does not throw).</summary>
    [Theory]
    [InlineData("UpKey", Key.W)]
    [InlineData("DownKey", Key.S)]
    [InlineData("LeftKey", Key.A)]
    [InlineData("RightKey", Key.D)]
    public void AssigningSameKeyToItsOwnProperty_IsANoOp(string propertyName, Key ownKey)
    {
        var config = new GameConfig();

        Set(config, propertyName, ownKey); // must not throw

        Assert.Equal(Direction.Up, config.GetDirection(Key.W));
        Assert.Equal(Direction.Down, config.GetDirection(Key.S));
        Assert.Equal(Direction.Left, config.GetDirection(Key.A));
        Assert.Equal(Direction.Right, config.GetDirection(Key.D));
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: GetDirection on an unmapped key returns null.
    // ---------------------------------------------------------------------
    /// <summary>Verifies GetDirection returns null for keys that are not mapped to any movement direction.</summary>
    [Theory]
    [InlineData(Key.Z)]
    [InlineData(Key.Up)]
    [InlineData(Key.Down)]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    [InlineData(Key.Space)]
    public void GetDirection_OnUnmappedKey_ReturnsNull(Key unmappedKey)
    {
        var config = new GameConfig();

        Assert.Null(config.GetDirection(unmappedKey));
    }

    // ---------------------------------------------------------------------
    // Additional coverage: the Key enum exposes exactly the documented set
    // (A–Z, arrow keys, Space) and every movement binding stays unique.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the Key enum contains the full documented set: A–Z, Up, Down, Left, Right and Space.</summary>
    [Fact]
    public void KeyEnum_ContainsDocumentedSet()
    {
        var expected = new[]
        {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
            Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
            Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
            Key.Up, Key.Down, Key.Left, Key.Right, Key.Space,
        };

        Assert.Equal(expected, Enum.GetValues<Key>());
    }

    /// <summary>Verifies that after rebinding, no two directions ever share a key.</summary>
    [Fact]
    public void Bindings_AlwaysRemainUnique()
    {
        var config = new GameConfig();

        config.UpKey = Key.Up;
        config.DownKey = Key.Down;
        config.LeftKey = Key.Left;
        config.RightKey = Key.Right;

        Assert.Equal(Direction.Up, config.GetDirection(Key.Up));
        Assert.Equal(Direction.Down, config.GetDirection(Key.Down));
        Assert.Equal(Direction.Left, config.GetDirection(Key.Left));
        Assert.Equal(Direction.Right, config.GetDirection(Key.Right));
        Assert.Null(config.GetDirection(Key.W));
        Assert.Null(config.GetDirection(Key.S));
        Assert.Null(config.GetDirection(Key.A));
        Assert.Null(config.GetDirection(Key.D));
    }

    // ---------------------------------------------------------------------
    // Acceptance 5 (story 21): GetMovementDirection combines the held bound
    // keys into a single 8-direction vector.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a single bound key resolves to its cardinal direction (W → Up).</summary>
    [Fact]
    public void GetMovementDirection_SingleKey_ReturnsItsCardinalDirection()
    {
        var config = new GameConfig();

        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.W]));
    }

    /// <summary>Verifies two perpendicular keys combine into a diagonal (W+D → UpRight).</summary>
    [Fact]
    public void GetMovementDirection_TwoPerpendicularKeys_ReturnsDiagonal()
    {
        var config = new GameConfig();

        Assert.Equal(Direction.UpRight, config.GetMovementDirection([Key.W, Key.D]));
    }

    /// <summary>Verifies opposite keys cancel out and produce no movement (W+S → null).</summary>
    [Fact]
    public void GetMovementDirection_OppositeKeys_CancelToNull()
    {
        var config = new GameConfig();

        Assert.Null(config.GetMovementDirection([Key.W, Key.S]));
        Assert.Null(config.GetMovementDirection([Key.A, Key.D]));
    }

    /// <summary>Verifies a cancelling horizontal pair leaves the vertical direction (W+A+D → Up).</summary>
    [Fact]
    public void GetMovementDirection_ThreeKeysWithCancellingHorizontalPair_ReturnsVertical()
    {
        var config = new GameConfig();

        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.W, Key.A, Key.D]));
    }

    /// <summary>Verifies an empty pressed set produces no movement.</summary>
    [Fact]
    public void GetMovementDirection_EmptySet_ReturnsNull()
    {
        var config = new GameConfig();

        Assert.Null(config.GetMovementDirection(Array.Empty<Key>()));
    }

    /// <summary>Verifies keys not bound to any movement direction are ignored.</summary>
    [Fact]
    public void GetMovementDirection_UnmappedKeysAreIgnored()
    {
        var config = new GameConfig();

        Assert.Null(config.GetMovementDirection([Key.Space]));
        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.W, Key.Space]));
    }

    /// <summary>Verifies diagonal resolution respects rebinding (Z+D → UpRight after UpKey = Z).</summary>
    [Fact]
    public void GetMovementDirection_RespectsRebinding()
    {
        var config = new GameConfig();
        config.UpKey = Key.Z;

        Assert.Equal(Direction.Up, config.GetMovementDirection([Key.Z]));
        Assert.Equal(Direction.UpRight, config.GetMovementDirection([Key.Z, Key.D]));
    }

    /// <summary>Verifies GetMovementDirection rejects a null pressed-keys argument.</summary>
    [Fact]
    public void GetMovementDirection_NullArgument_ThrowsArgumentNullException()
    {
        var config = new GameConfig();

        Assert.Throws<ArgumentNullException>(() => config.GetMovementDirection(null!));
    }

    private static void Set(GameConfig config, string propertyName, Key key)
    {
        switch (propertyName)
        {
            case "UpKey":
                config.UpKey = key;
                break;
            case "DownKey":
                config.DownKey = key;
                break;
            case "LeftKey":
                config.LeftKey = key;
                break;
            case "RightKey":
                config.RightKey = key;
                break;
            default:
                throw new ArgumentException($"Unknown property '{propertyName}'.", nameof(propertyName));
        }
    }
}
