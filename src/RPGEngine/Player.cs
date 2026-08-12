namespace RPGEngine;

/// <summary>
/// Represents the player controlled by the user. It is composed of a
/// <see cref="Character"/> which represents the player in the game world,
/// and it will be moved when the engine receives input events (later story).
/// </summary>
public sealed class Player
{
    /// <summary>Gets the <see cref="Character"/> that represents the player in the game world.</summary>
    public Character Character { get; }

    /// <summary>Initializes a new instance of the <see cref="Player"/> class.</summary>
    public Player()
    {
        Character = new Character();
    }
}
