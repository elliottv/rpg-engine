using RPGEngine;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Smoke tests that verify a freshly created <see cref="GameEngine"/> exposes a sane default state.
/// </summary>
public class GameEngineSmokeTests
{
    /// <summary>
    /// Verifies that a new engine has a non-null player, an empty (non-null) character list,
    /// a configuration with the default WASD bindings and no loaded map.
    /// </summary>
    [Fact]
    public void NewGameEngine_HasExpectedDefaults()
    {
        var engine = new GameEngine();

        Assert.NotNull(engine.Player);
        Assert.NotNull(engine.Characters);
        Assert.Empty(engine.Characters);

        Assert.NotNull(engine.Config);
        Assert.Equal(Key.W, engine.Config.UpKey);
        Assert.Equal(Key.S, engine.Config.DownKey);
        Assert.Equal(Key.A, engine.Config.LeftKey);
        Assert.Equal(Key.D, engine.Config.RightKey);

        Assert.Null(engine.Map);
    }
}
