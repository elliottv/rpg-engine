using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for the engine's configuration contract (story 80): <see cref="GameEngine"/>
/// can only be built with a <see cref="GameConfig"/> instance, keeps the very instance the host
/// passed and reads it at input time (never a snapshot), so the host's user-defined options and
/// key bindings stay live.
/// </summary>
public class GameEngineConfigTests
{
    private const double FrameDt = 1.0 / 60;

    // ---------------------------------------------------------------------
    // Acceptance 1: the configuration is mandatory and the engine keeps the
    // host's own instance (there is no parameterless constructor anymore).
    // ---------------------------------------------------------------------
    /// <summary>Verifies that building an engine with a null configuration throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameEngine(null!));
    }

    /// <summary>Verifies that the engine exposes the very configuration instance it was constructed with.</summary>
    [Fact]
    public void Constructor_KeepsTheProvidedInstance()
    {
        var config = new TestGameConfig();

        using var engine = new GameEngine(config);

        Assert.Same(config, engine.Config);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: the engine reads the configuration live. The host keeps
    // mutating its instance after construction and both its own options and
    // the movement bindings are honoured on the next Update.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies that the host's configuration stays live after construction: mutating the instance
    /// the host passed is immediately visible through <c>engine.Config</c> (same instance, cast to
    /// the concrete host type), and rebinding a movement key after construction takes effect on the
    /// next <c>Update</c> — the engine never snapshots the configuration.
    /// </summary>
    [Fact]
    public void HostOptions_AreLive_AfterConstruction()
    {
        var config = new HostOptionsConfig { MasterVolume = 0.5f };

        using var engine = new GameEngine(config);
        engine.Player.Position = new Position(100, 100);

        // The host's own option is reachable through the engine: same instance, so the concrete
        // type only needs a cast.
        Assert.Equal(0.5f, ((HostOptionsConfig)engine.Config).MasterVolume);

        // Mutating the host's instance after construction is observed immediately: the engine
        // reads the configuration, it does not hold a copy of its values.
        config.MasterVolume = 0.8f;
        Assert.Equal(0.8f, ((HostOptionsConfig)engine.Config).MasterVolume);
        Assert.Same(config, engine.Config);

        // Rebinding a movement key after construction changes the engine's behaviour on the next
        // Update: W is no longer bound to any direction, so it moves nothing.
        config.UpKey = Key.Up;
        Assert.Equal(Key.Up, engine.Config.UpKey);

        engine.Input(Key.W, isPressed: true);
        engine.Input(Key.Up, isPressed: true);
        engine.Input(Key.W, isPressed: false); // hold Up only
        engine.Update(FrameDt);

        Assert.True(engine.Player.Position.Y < 100, "The up-arrow key bound after construction moves the player up.");
        Assert.Equal(100, engine.Player.Position.X, precision: 6);
        Assert.Equal(Direction.Up, engine.Player.Direction);

        // The old key W is unbound now: pressing it again changes nothing.
        engine.Input(Key.Up, isPressed: false);
        var stopped = engine.Player.Position;
        engine.Input(Key.W, isPressed: true);
        engine.Update(FrameDt);
        Assert.Equal(stopped, engine.Player.Position);
        engine.Input(Key.W, isPressed: false);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: the derived configuration's movement mapping is honoured
    // (the engine consumes the polymorphic GetMovementDirection entry point).
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies that a derived configuration can drive the engine through the polymorphic
    /// <c>GetMovementDirection</c> entry point: an override returning <see cref="Direction.Right"/>
    /// moves the player right even with no key pressed, while an override returning
    /// <see langword="null"/> leaves it stopped.
    /// </summary>
    [Fact]
    public void DerivedConfiguration_MovementMappingOverride_IsHonouredByTheEngine()
    {
        using (var engine = new GameEngine(new AlwaysRightConfig()))
        {
            engine.Player.Position = new Position(100, 100);

            // No key is pressed at all: the override alone moves the player.
            engine.Update(FrameDt);

            Assert.True(engine.Player.Position.X > 100, "The overridden mapping moves the player right.");
            Assert.Equal(100, engine.Player.Position.Y, precision: 6);
            Assert.Equal(Direction.Right, engine.Player.Direction);
        }

        using (var stoppedEngine = new GameEngine(new NeverMovingConfig()))
        {
            stoppedEngine.Player.Position = new Position(100, 100);

            stoppedEngine.Update(FrameDt);

            Assert.Equal(new Position(100, 100), stoppedEngine.Player.Position);
            Assert.NotEqual(Direction.Right, stoppedEngine.Player.Direction); // the player never faced right
        }
    }

    // ---------------------------------------------------------------------
    // Non-regression: a host configuration without custom options behaves
    // exactly like the engine always did.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies that a minimal host configuration (no user-defined options) leaves the engine's
    /// behaviour untouched: the inherited WASD movement works and the inherited defaults are
    /// readable through <c>engine.Config</c>.
    /// </summary>
    [Fact]
    public void Engine_WithoutCustomOptions_StillWorks()
    {
        using var engine = new GameEngine(new TestGameConfig());
        engine.Player.Position = new Position(100, 100);

        engine.Input(Key.D, isPressed: true);
        engine.Update(FrameDt);
        engine.Input(Key.D, isPressed: false);

        Assert.True(engine.Player.Position.X > 100, "Holding D moves the player right.");
        Assert.Equal(100, engine.Player.Position.Y, precision: 6);
        Assert.Equal(Direction.Right, engine.Player.Direction);

        // The engine reads a plain GameConfig subclass: every option comes from the base class.
        Assert.Equal(Key.W, engine.Config.UpKey);
        Assert.Equal(Key.S, engine.Config.DownKey);
        Assert.Equal(Key.A, engine.Config.LeftKey);
        Assert.Equal(Key.D, engine.Config.RightKey);
        Assert.Equal(Direction.Up, engine.Config.GetDirection(Key.W));
        Assert.Equal(Direction.Right, engine.Config.GetMovementDirection([Key.D]));
    }

    /// <summary>A host configuration with one user-defined option the engine ignores (audio volume).</summary>
    private sealed class HostOptionsConfig : GameConfig
    {
        /// <summary>Gets or sets the master audio volume, 0..1. The engine never reads this.</summary>
        public float MasterVolume { get; set; } = 1f;
    }

    /// <summary>A host configuration that replaces the mapping: any pressed-key set means "move right".</summary>
    private sealed class AlwaysRightConfig : GameConfig
    {
        /// <inheritdoc />
        public override Direction? GetMovementDirection(IEnumerable<Key> pressedKeys) => Direction.Right;
    }

    /// <summary>A host configuration that replaces the mapping: no pressed-key set ever moves the player.</summary>
    private sealed class NeverMovingConfig : GameConfig
    {
        /// <inheritdoc />
        public override Direction? GetMovementDirection(IEnumerable<Key> pressedKeys) => null;
    }
}
