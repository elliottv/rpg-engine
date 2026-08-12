namespace RPGEngine;

/// <summary>
/// Contains the configuration values used by the engine.
/// For now it only holds the keys used for movement, which default to WASD.
/// When the values are updated at runtime, they are taken into account by the engine.
/// </summary>
public sealed class GameConfig
{
    /// <summary>Gets or sets the key used to move up. Defaults to <see cref="Key.W"/>.</summary>
    public Key MoveUp { get; set; } = Key.W;

    /// <summary>Gets or sets the key used to move down. Defaults to <see cref="Key.S"/>.</summary>
    public Key MoveDown { get; set; } = Key.S;

    /// <summary>Gets or sets the key used to move left. Defaults to <see cref="Key.A"/>.</summary>
    public Key MoveLeft { get; set; } = Key.A;

    /// <summary>Gets or sets the key used to move right. Defaults to <see cref="Key.D"/>.</summary>
    public Key MoveRight { get; set; } = Key.D;
}
