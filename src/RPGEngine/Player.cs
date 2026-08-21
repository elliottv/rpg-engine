using RPGEngine.Sprites;

namespace RPGEngine;

/// <summary>
/// Represents the player character controlled by the user.
/// </summary>
/// <remarks>
/// <para>
/// The player is <em>composed</em> of a <see cref="Character"/> (composition, no inheritance)
/// which carries all of the in-world state: position, facing direction, movement speed and the
/// list of spritesheet references. <see cref="Player"/> is a thin wrapper that forwards state
/// access and movement to that character, so configuring <see cref="SpriteSheets"/> or moving
/// the player is exactly equivalent to configuring/moving the underlying <see cref="Character"/>.
/// </para>
/// <para>
/// The player does not listen to input itself. The engine (<see cref="GameEngine"/>) owns the
/// pressed-keys state and, in its update loop, calls <c>Move(direction, 1, dt)</c> with the
/// direction derived from <see cref="GameConfig"/>. This keeps <see cref="Player"/> a thin,
/// easily testable wrapper.
/// </para>
/// </remarks>
public sealed class Player
{
    /// <summary>
    /// The default movement speed, in tiles per second, of a player created by the parameterless
    /// constructor: 2 tiles/s, i.e. the tile-unit equivalent of the previous 96 px/s with 48 px
    /// tiles (two 48px map tiles every second). This is a gameplay tuning constant and is
    /// expected to be adjusted later.
    /// </summary>
    public const double DefaultBaseSpeed = 2;

    /// <summary>Gets or sets the <see cref="Character"/> that represents the player in the game world.</summary>
    /// <remarks>
    /// All other members of <see cref="Player"/> forward to this character, so replacing it
    /// replaces the player's position, direction, speed and spritesheets as well.
    /// </remarks>
    public Character Character { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class with its own
    /// <see cref="Character"/> using the default <see cref="DefaultBaseSpeed"/>.
    /// </summary>
    public Player()
    {
        Character = new Character { BaseSpeed = DefaultBaseSpeed };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class wrapping the provided
    /// <see cref="Character"/>.
    /// </summary>
    /// <param name="character">The character that represents the player in the game world.</param>
    public Player(Character character)
    {
        Character = character;
    }

    /// <summary>
    /// Gets or sets the top-left world position of the player's sprite, in tiles. Forwards to
    /// <see cref="Character.Position"/>.
    /// </summary>
    public Position Position
    {
        get => Character.Position;
        set => Character.Position = value;
    }

    /// <summary>
    /// Gets or sets the direction the player is facing. Forwards to
    /// <see cref="Character.Direction"/>.
    /// </summary>
    public Direction Direction
    {
        get => Character.Direction;
        set => Character.Direction = value;
    }

    /// <summary>
    /// Gets the mutable list of spritesheet references used to render the player. Each entry
    /// pairs a loaded sheet name with the 1-based character index (1..8) within that sheet.
    /// Forwards to <see cref="Character.SpriteSheets"/>: the returned list is the same instance,
    /// so entries added through the player are immediately visible on the underlying character
    /// (e.g. <c>player.SpriteSheets.Add(new SpriteSheetRef("hero_body", 3))</c>).
    /// </summary>
    public IList<SpriteSheetRef> SpriteSheets => Character.SpriteSheets;

    /// <summary>
    /// Moves the player in <paramref name="direction"/> by <c>BaseSpeed * speedFactor * dt</c>
    /// tiles and sets the facing direction. Forwards to
    /// <see cref="Character.Move(Direction, double, double)"/>.
    /// </summary>
    /// <param name="direction">The direction to face and move towards.</param>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(Direction direction, double speedFactor = 1, double dt = 1)
        => Character.Move(direction, speedFactor, dt);

    /// <summary>
    /// Moves the player in its current facing direction. Forwards to
    /// <see cref="Character.Move(double, double)"/>.
    /// </summary>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(double speedFactor = 1, double dt = 1)
        => Character.Move(speedFactor, dt);
}
