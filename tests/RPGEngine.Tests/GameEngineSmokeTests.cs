using RPGEngine;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Smoke tests that verify a freshly created <see cref="GameEngine"/> exposes a sane default state.
/// </summary>
public class GameEngineSmokeTests
{
    /// <summary>
    /// Verifies that a new engine, built with the test configuration, has a non-null player, an
    /// empty (non-null) character list, no loaded map and exposes the inherited WASD bindings
    /// through <see cref="GameEngine.Config"/>.
    /// </summary>
    [Fact]
    public void NewGameEngine_HasExpectedDefaults()
    {
        // The engine is built with the host's configuration instance, which is mandatory (there is
        // no parameterless constructor). The test configuration adds no options of its own, so the
        // engine behaves exactly like the engine default.
        var config = new TestGameConfig();
        var engine = new GameEngine(config);

        Assert.NotNull(engine.Player);
        Assert.NotNull(engine.Characters);
        Assert.Empty(engine.Characters);

        // The engine keeps the very configuration instance it was given...
        Assert.Same(config, engine.Config);

        // ...whose inherited WASD movement bindings are the engine's own options.
        Assert.Equal(Key.W, engine.Config.UpKey);
        Assert.Equal(Key.S, engine.Config.DownKey);
        Assert.Equal(Key.A, engine.Config.LeftKey);
        Assert.Equal(Key.D, engine.Config.RightKey);

        Assert.Null(engine.Map);
    }
}
