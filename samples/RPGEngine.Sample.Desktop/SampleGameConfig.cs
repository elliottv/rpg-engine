namespace RPGEngine.Sample.Desktop;

/// <summary>
/// The desktop sample host's configuration: the host's own <see cref="GameConfig"/> subclass,
/// which the engine requires (there is no parameterless <see cref="GameEngine"/> constructor and
/// the engine never creates a configuration of its own).
/// </summary>
/// <remarks>
/// The class is empty because the sample has no user-defined options: it inherits the engine's
/// WASD movement bindings, the <c>GetDirection</c> / <c>GetMovementDirection</c> mapping and the
/// "one key, one action" rule unchanged. A real game declares its own options here (master audio
/// volume, GUI/interaction key binds, …) and passes the instance to
/// <c>new GameEngine(config)</c>; see <c>docs/api/GameConfig.md</c>.
/// </remarks>
internal sealed class SampleGameConfig : GameConfig
{
}
