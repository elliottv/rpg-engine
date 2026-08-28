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
/// <para>
/// The player exposes a movement-state machine through two events: <see cref="OnStartMoving"/>
/// fires <em>before</em> the position is updated, exactly when the player <em>begins</em> moving
/// (idle &#8594; moving for key movement, and once per auto-walk step for click-to-move), and
/// never on direction changes while moving or on every frame. <see cref="OnStopMoving"/> fires
/// when the player stops moving: every movement key is released, the last auto-walk step is
/// reached, or the player is blocked by a collision. Both events carry only the facing
/// direction (<see cref="PlayerMoveEventArgs.Direction"/>), which is all a host needs to mirror
/// the player on other clients via <c>Character.StartMoving</c> / <c>Character.StopMoving</c>.
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

    private Character _character = null!;

    // The player's movement-state machine. _isMoving tracks whether the engine (or a direct
    // Move call) is currently moving the player; _lastDirection is the last facing direction the
    // event machinery reported, used to report the facing direction in OnStopMoving when the
    // player stops.
    private bool _isMoving;
    private Direction _lastDirection = Direction.Down;

    /// <summary>
    /// Occurs when the player starts moving: from idle &#8594; moving for key movement, and once
    /// per auto-walk step for click-to-move. The event is raised <em>before</em> the position is
    /// updated and carries the direction the player is moving in
    /// (<see cref="PlayerMoveEventArgs.Direction"/>). It is not raised on direction changes
    /// while moving and not on every frame.
    /// </summary>
    public event EventHandler<PlayerMoveEventArgs>? OnStartMoving;

    /// <summary>
    /// Occurs when the player stops moving: when every movement key is released, when the last
    /// auto-walk step is reached, or when the player is blocked by a collision. The event carries
    /// the direction the player was last moving in
    /// (<see cref="PlayerMoveEventArgs.Direction"/>).
    /// </summary>
    public event EventHandler<PlayerMoveEventArgs>? OnStopMoving;

    /// <summary>Gets or sets the <see cref="Character"/> that represents the player in the game world.</summary>
    /// <remarks>
    /// All other members of <see cref="Player"/> forward to this character, so replacing it
    /// replaces the player's position, direction, speed and spritesheets as well. The movement
    /// event state is re-synchronized to the new character's facing direction.
    /// </remarks>
    public Character Character
    {
        get => _character;
        set
        {
            _character = value;
            _lastDirection = _character.Direction;
        }
    }

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
    /// Gets or sets the world position of the player's feet, in tiles. The feet are the
    /// <em>middle-bottom</em> of the sprite: the sprite is rendered above and centered on this
    /// point. Forwards to <see cref="Character.Position"/>.
    /// </summary>
    public Position Position
    {
        get => Character.Position;
        set => Character.Position = value;
    }

    /// <summary>
    /// Gets or sets the direction the player is facing. Forwards to
    /// <see cref="Character.Direction"/> and keeps the movement event state in sync so
    /// <see cref="OnStartMoving"/> / <see cref="OnStopMoving"/> report the correct facing
    /// direction.
    /// </summary>
    public Direction Direction
    {
        get => Character.Direction;
        set
        {
            Character.Direction = value;
            _lastDirection = value;
        }
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
    /// <remarks>
    /// <para>
    /// This method also drives the movement-state machine: with <paramref name="speedFactor"/>
    /// greater than zero the player is considered <em>moving</em> (it actually moves), so
    /// <see cref="OnStartMoving"/> fires <em>before</em> the displacement when the player starts
    /// moving (idle &#8594; moving). A move while already moving (same or new direction) raises
    /// nothing: a direction change while moving is not a start, so it is not reported.
    /// </para>
    /// <para>
    /// With <paramref name="speedFactor"/> equal to zero the player only turns to face
    /// <paramref name="direction"/>: no event is raised (a turn is neither a start nor a stop).
    /// </para>
    /// </remarks>
    /// <param name="direction">The direction to face and move towards.</param>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(Direction direction, double speedFactor = 1, double dt = 1)
    {
        Direction = direction;
        if (speedFactor == 0)
        {
            // Turn only: no movement, no event.
            return;
        }

        // Raise OnStartMoving BEFORE the displacement so handlers observe the pre-move position.
        if (!_isMoving)
        {
            OnStartMoving?.Invoke(this, new PlayerMoveEventArgs(direction));
        }

        Character.Move(direction, speedFactor, dt); // the position update
        _isMoving = true;
    }

    /// <summary>
    /// Moves the player in its current facing direction. Forwards to
    /// <see cref="Character.Move(double, double)"/> and uses the same event semantics as
    /// <see cref="Move(Direction, double, double)"/>.
    /// </summary>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(double speedFactor = 1, double dt = 1)
        => Move(Direction, speedFactor, dt);

    /// <summary>
    /// Transitions the player to idle (stops moving) and raises <see cref="OnStopMoving"/> with
    /// the direction the player was last moving in. When the player is already idle this method
    /// is a no-op and does not raise the event. The engine calls it when there is no key input
    /// and no auto-walk target.
    /// </summary>
    /// <remarks>
    /// Stopping does not change the facing direction: the player keeps facing the direction it
    /// was last moving, and that direction is reported in the event.
    /// </remarks>
    public void Stop()
    {
        if (!_isMoving)
        {
            return;
        }

        _isMoving = false;
        OnStopMoving?.Invoke(this, new PlayerMoveEventArgs(_lastDirection));
    }

    /// <summary>
    /// Records that the player moved and raises <see cref="OnStartMoving"/> only on the idle &#8594;
    /// moving transition, mirroring the start semantics of <see cref="Move(Direction, double, double)"/>.
    /// </summary>
    /// <remarks>
    /// This is the internal bridge the engine uses for movement it resolves itself (the
    /// axis-separated collision resolution of <see cref="GameEngine"/>), which cannot go through
    /// <see cref="Move(Direction, double, double)"/> because that method applies the whole
    /// displacement at once. It faces the player and marks it as moving; <see cref="OnStartMoving"/>
    /// is raised <em>before</em> the engine applies the displacement, and only when the player was
    /// idle (a direction change while moving raises nothing). The engine calls it before applying
    /// the displacement.
    /// </remarks>
    /// <param name="direction">The direction the player moved in (and now faces).</param>
    internal void ReportMovement(Direction direction)
    {
        var wasMoving = _isMoving;

        Direction = direction;
        _isMoving = true;

        if (!wasMoving)
        {
            OnStartMoving?.Invoke(this, new PlayerMoveEventArgs(direction));
        }
    }

    /// <summary>
    /// Records that an auto-walk step began and raises <see cref="OnStartMoving"/>.
    /// </summary>
    /// <remarks>
    /// This is the internal bridge the engine uses for auto-walk (click-to-move) movement. Each
    /// auto-walk step is a new start: the player faces <paramref name="direction"/>, is marked as
    /// moving and <see cref="OnStartMoving"/> is raised <em>every call</em>, once per step
    /// boundary, before that step's position update. <see cref="GameEngine"/> calls it exactly
    /// once per waypoint leg.
    /// </remarks>
    /// <param name="direction">The direction of the new auto-walk step (and the new facing direction).</param>
    internal void ReportAutoWalkStep(Direction direction)
    {
        Direction = direction;
        _isMoving = true;
        OnStartMoving?.Invoke(this, new PlayerMoveEventArgs(direction));
    }

    /// <summary>
    /// Records that the player attempted to move in <paramref name="direction"/> but was fully
    /// blocked (e.g. by a solid tile or the map edge): the player faces <paramref name="direction"/>
    /// and, when it was moving, transitions to idle and raises <see cref="OnStopMoving"/> (the
    /// collision stop). When the player was already idle this is a no-op: no event is raised.
    /// </summary>
    /// <remarks>
    /// This is the internal bridge the engine uses to report a collision stop: unlike
    /// <see cref="ReportMovement"/> it marks the player as idle (the displacement was fully blocked,
    /// so the player is not moving). The engine calls it after resolving a key move whose net
    /// displacement is zero, so <see cref="OnStopMoving"/> fires with the blocked direction even
    /// while a movement key stays pressed against a wall. The engine is responsible for not
    /// re-reporting the same blocked direction on every frame (see
    /// <see cref="GameEngine.MovePlayerWithCollisionResolution(Direction, double)"/>).
    /// </remarks>
    /// <param name="direction">The direction the player tried to move in (and now faces).</param>
    internal void ReportBlockedMove(Direction direction)
    {
        Direction = direction;
        if (!_isMoving)
        {
            return;
        }

        _isMoving = false;
        OnStopMoving?.Invoke(this, new PlayerMoveEventArgs(direction));
    }
}
