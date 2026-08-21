using RPGEngine.Sprites;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="Player"/> (story 9): the player is a thin wrapper that
/// composes a <see cref="Character"/> and forwards state access and movement to it. It does
/// not listen to input itself; the engine drives it via <c>Move(direction, 1, dt)</c>.
/// </summary>
public class PlayerTests
{
    // ---------------------------------------------------------------------
    // Acceptance 1: the parameterless constructor creates its own Character
    // (with the default BaseSpeed), and Character is settable.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the parameterless constructor creates a non-null Character with the default BaseSpeed.</summary>
    [Fact]
    public void ParameterlessConstructor_CreatesOwnCharacterWithDefaultSpeed()
    {
        var player = new Player();

        Assert.NotNull(player.Character);
        Assert.Equal(Player.DefaultBaseSpeed, player.Character.BaseSpeed);
    }

    /// <summary>Verifies Player.Character can be replaced with another Character instance.</summary>
    [Fact]
    public void Character_IsSettable()
    {
        var player = new Player();
        var character = new Character();

        player.Character = character;

        Assert.Same(character, player.Character);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: Position/Direction read/write forward to Character.
    // ---------------------------------------------------------------------
    /// <summary>Verifies writing Player.Position updates the underlying Character and reading it returns the character's value.</summary>
    [Fact]
    public void Position_ReadWriteForwardsToCharacter()
    {
        var player = new Player();
        var character = player.Character;

        player.Position = new Position(12, 34);

        Assert.Equal(new Position(12, 34), character.Position);
        Assert.Equal(character.Position, player.Position);
    }

    /// <summary>Verifies writing Player.Direction updates the underlying Character and reading it returns the character's value.</summary>
    [Fact]
    public void Direction_ReadWriteForwardsToCharacter()
    {
        var player = new Player();
        var character = player.Character;

        player.Direction = Direction.Left;

        Assert.Equal(Direction.Left, character.Direction);
        Assert.Equal(character.Direction, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: SpriteSheets is the same list instance as
    // Character.SpriteSheets, so adding a SpriteSheetRef (with a character
    // index 1..8) via the player is visible on the character.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Player.SpriteSheets returns the exact same list instance as Character.SpriteSheets.</summary>
    [Fact]
    public void SpriteSheets_IsSameInstanceAsCharacterSpriteSheets()
    {
        var player = new Player();

        Assert.Same(player.Character.SpriteSheets, player.SpriteSheets);
    }

    /// <summary>Verifies a SpriteSheetRef added through Player.SpriteSheets is visible on the underlying Character.</summary>
    [Fact]
    public void SpriteSheets_AddViaPlayer_IsVisibleOnCharacter()
    {
        var player = new Player();

        player.SpriteSheets.Add(new SpriteSheetRef("hero_body", CharacterIndex: 3));

        Assert.Contains(new SpriteSheetRef("hero_body", 3), player.Character.SpriteSheets);
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: Move(direction, factor, dt) moves the underlying Character
    // by BaseSpeed * factor * dt and updates its Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(direction, factor, dt) moves the underlying Character exactly BaseSpeed × factor × dt tiles along the right axis and updates its Direction.</summary>
    [Theory]
    [InlineData(Direction.Down, 0.0, 100.0)]
    [InlineData(Direction.Up, 0.0, -100.0)]
    [InlineData(Direction.Left, -100.0, 0.0)]
    [InlineData(Direction.Right, 100.0, 0.0)]
    public void Move_WithDirectionFactorAndDt_MovesUnderlyingCharacter(
        Direction direction,
        double expectedX,
        double expectedY)
    {
        var player = new Player { Position = new Position(0, 0) };
        player.Character.BaseSpeed = 100;

        // BaseSpeed * factor * dt = 100 * 2 * 0.5 = 100 tiles.
        player.Move(direction, speedFactor: 2, dt: 0.5);

        Assert.Equal(expectedX, player.Position.X);
        Assert.Equal(expectedY, player.Position.Y);
        Assert.Equal(direction, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5: Move(factor, dt) without a direction uses the Character's
    // previous direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(speedFactor, dt) without a direction reuses the underlying Character's previous Direction.</summary>
    [Fact]
    public void Move_WithoutDirection_ReusesPreviousDirection()
    {
        var player = new Player { Direction = Direction.Up };
        player.Character.BaseSpeed = 100;

        player.Move(speedFactor: 1, dt: 1);

        Assert.Equal(new Position(0, -100), player.Position);
        Assert.Equal(Direction.Up, player.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: driving the player the way the engine will — derive a
    // direction from Config and call Move(direction, 1, dt) each frame at
    // dt = 1/60 — tracks the expected path.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a player driven like the engine (Config-derived direction, Move each frame at dt = 1/60) tracks the expected path.</summary>
    [Fact]
    public void DrivenLikeTheEngine_TracksExpectedPath()
    {
        var engine = new GameEngine();
        var player = engine.Player;
        var config = engine.Config;

        const double dt = 1.0 / 60;
        var right = config.GetDirection(Key.D)!.Value;
        var down = config.GetDirection(Key.S)!.Value;

        // Move right for one second (60 frames at 1/60 s) → 2 tiles to the right (the new
        // default BaseSpeed of 2 tiles/s, the tile-unit equivalent of the previous 96 px/s).
        for (var frame = 0; frame < 60; frame++)
        {
            player.Move(right, speedFactor: 1, dt);
        }

        Assert.Equal(2, player.Position.X, precision: 6);
        Assert.Equal(0, player.Position.Y, precision: 6);

        // Then move down for half a second (30 frames) → 1 tile down.
        for (var frame = 0; frame < 30; frame++)
        {
            player.Move(down, speedFactor: 1, dt);
        }

        Assert.Equal(2, player.Position.X, precision: 6);
        Assert.Equal(1, player.Position.Y, precision: 6);
    }
}
