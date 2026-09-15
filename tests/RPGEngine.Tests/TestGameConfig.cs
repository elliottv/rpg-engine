namespace RPGEngine.Tests;

/// <summary>
/// The test suite's game configuration: the minimal possible <see cref="GameConfig"/> subclass
/// (<c>sealed class TestGameConfig : GameConfig { }</c>), which inherits the WASD movement bindings,
/// the <c>GetDirection</c> / <c>GetMovementDirection</c> mapping and the "one key, one action" rule
/// unchanged.
/// </summary>
/// <remarks>
/// <see cref="GameConfig"/> is abstract, so a concrete configuration type is needed to build a
/// configuration or an engine at all. Tests that need user-defined options define a local subclass
/// of their own instead.
/// </remarks>
public sealed class TestGameConfig : GameConfig
{
}
