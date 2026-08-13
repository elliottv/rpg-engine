namespace RPGEngine;

/// <summary>
/// Represents a keyboard key that can be bound to an engine action.
/// The full set of supported keys is completed by later stories; this story
/// only needs the movement keys (WASD) used by the default <see cref="GameConfig"/>.
/// </summary>
public enum Key
{
    /// <summary>The W key (bound to moving up by default).</summary>
    W,

    /// <summary>The A key (bound to moving left by default).</summary>
    A,

    /// <summary>The S key (bound to moving down by default).</summary>
    S,

    /// <summary>The D key (bound to moving right by default).</summary>
    D,
}
