namespace RPGEngine;

/// <summary>
/// The engine's transitional default configuration: a minimal <see cref="GameConfig"/> subclass that
/// only inherits the WASD movement bindings, used by the parameterless <see cref="GameEngine"/>
/// constructor.
/// </summary>
/// <remarks>
/// <see cref="GameConfig"/> is abstract, so the engine needs a concrete implementation as long as it
/// can be built without a host configuration. This type is the bridge for the transition described
/// by the <c>GameConfig evolution</c> epic: once the host is required to pass its own configuration
/// instance to the <see cref="GameEngine"/> constructor, the parameterless constructor and this type
/// are removed. It is internal because hosts always define their own configuration type and must
/// never reference the engine's placeholder.
/// </remarks>
internal sealed class DefaultGameConfig : GameConfig
{
}
