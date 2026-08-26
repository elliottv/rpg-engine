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
/// The player exposes a movement-state machine through <see cref="OnMove"/>: it fires whenever
/// the player <em>starts moving</em> (idle &#8594; moving), <em>stops moving</em> (moving &#8594;
/// idle, via <see cref="Stop"/> or a collision stop reported by the engine), or <em>changes
/// direction while moving</em>. The event is raised for both manual (key) movement and auto-walk
/// movement, so hosts can react to the player's movement state without polling the position every
/// frame. A collision stop raises <see cref="OnMove"/> with
/// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/> even while the
/// movement key stays pressed against the wall.
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
    // event machinery reported, used to detect direction changes and to report the facing
    // direction in OnMove when the player stops.
    private bool _isMoving;
    private Direction _lastDirection = Direction.Down;

    /// <summary>
    /// Occurs when the player's movement state changes: it starts moving (idle &#8594; moving),
    /// stops moving (moving &#8594; idle, via <see cref="Stop"/> or a collision stop reported by
    /// the engine), or changes direction while moving. The event carries the new state
    /// (<see cref="PlayerMoveEventArgs.IsMoving"/>) and the player's current facing direction
    /// (<see cref="PlayerMoveEventArgs.Direction"/>). It is raised for both manual (key)
    /// movement and auto-walk movement. A collision stop fires with
    /// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/> even while
    /// the movement key stays pressed against the wall.
    /// </summary>
    public event EventHandler<PlayerMoveEventArgs>? OnMove;

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
    /// <see cref="OnMove"/> reports the correct facing direction.
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
    /// <see cref="OnMove"/> fires with <see cref="PlayerMoveEventArgs.IsMoving"/> set to
    /// <see langword="true"/> when the player starts moving (idle &#8594; moving) or changes
    /// direction while moving.
    /// </para>
    /// <para>
    /// With <paramref name="speedFactor"/> equal to zero the player only turns to face
    /// <paramref name="direction"/>: <see cref="OnMove"/> fires on a direction change carrying
    /// the current movement state (<see langword="true"/> when the player was already moving,
    /// <see langword="false"/> when idle).
    /// </para>
    /// </remarks>
    /// <param name="direction">The direction to face and move towards.</param>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(Direction direction, double speedFactor = 1, double dt = 1)
    {
        var wasMoving = _isMoving;
        var previousDirection = _lastDirection;

        // Face the requested direction first so the character, the event state and the reported
        // facing all agree.
        Direction = direction;

        if (speedFactor == 0)
        {
            // Only turns: the movement state is unchanged; raise OnMove on a direction change
            // with the current moving state.
            if (direction != previousDirection)
            {
                OnMove?.Invoke(this, new PlayerMoveEventArgs(_isMoving, direction));
            }

            return;
        }

        Character.Move(direction, speedFactor, dt);

        _isMoving = true;
        if (!wasMoving || direction != previousDirection)
        {
            OnMove?.Invoke(this, new PlayerMoveEventArgs(true, direction));
        }
    }

    /// <summary>
    /// Moves the player in its current facing direction. Forwards to
    /// <see cref="Character.Move(double, double)"/>.
    /// </summary>
    /// <param name="speedFactor">A multiplier applied to the character's <see cref="Character.BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(double speedFactor = 1, double dt = 1)
        => Move(Direction, speedFactor, dt);

    /// <summary>
    /// Transitions the player to idle (stops moving) and raises <see cref="OnMove"/> with
    /// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/>. When the player
    /// is already idle this method is a no-op and does not raise the event. The engine calls it
    /// when there is no key input and no auto-walk target.
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
        OnMove?.Invoke(this, new PlayerMoveEventArgs(false, _lastDirection));
    }

    /// <summary>
    /// Records that the player moved and raises <see cref="OnMove"/> on state transitions,
    /// mirroring the state-machine behavior of <see cref="Move(Direction, double, double)"/>.
    /// </summary>
    /// <remarks>
    /// This is the internal bridge the engine uses for movement it resolves itself (the
    /// axis-separated collision resolution of <see cref="GameEngine"/> and the auto-walk
    /// movement), which cannot go through <see cref="Move(Direction, double, double)"/> because
    /// that method applies the whole displacement at once. It faces the player, marks it as
    /// moving and raises <see cref="OnMove"/> with <see cref="PlayerMoveEventArgs.IsMoving"/>
    /// set to <see langword="true"/> when the player starts moving or changes direction while
    /// moving.
    /// </remarks>
    /// <param name="direction">The direction the player moved in (and now faces).</param>
    internal void ReportMovement(Direction direction)
    {
        var wasMoving = _isMoving;
        var previousDirection = _lastDirection;

        Direction = direction;
        _isMoving = true;

        if (!wasMoving || direction != previousDirection)
        {
            OnMove?.Invoke(this, new PlayerMoveEventArgs(true, direction));
        }
    }

    /// <summary>
    /// Records that the player attempted to move in <paramref name="direction"/> but was fully
    /// blocked (e.g. by a solid tile or the map edge): the player faces <paramref name="direction"/>
    /// and, when it was moving, transitions to idle and raises <see cref="OnMove"/> with
    /// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/>. When the player was
    /// already idle and the direction is unchanged, this is a no-op (no repeated events while the key
    /// is held against the wall).
    /// </summary>
    /// <remarks>
    /// This is the internal bridge the engine uses to report a collision stop: unlike
    /// <see cref="ReportMovement"/> it marks the player as idle (the displacement was fully blocked,
    /// so the player is not moving), so <see cref="OnMove"/> reflects the actual movement state even
    /// while a movement key stays pressed against a wall. Mirroring
    /// <see cref="Move(Direction, double, double)"/> with <c>speedFactor: 0</c>, a turn while already
    /// idle and facing <paramref name="direction"/> raises the event with
    /// <see cref="PlayerMoveEventArgs.IsMoving"/> set to <see langword="false"/>.
    /// </remarks>
    /// <param name="direction">The direction the player tried to move in (and now faces).</param>
    internal void ReportBlockedMove(Direction direction)
    {
        var wasMoving = _isMoving;
        var previousDirection = _lastDirection;

        Direction = direction;
        _isMoving = false;

        if (wasMoving || direction != previousDirection)
        {
            OnMove?.Invoke(this, new PlayerMoveEventArgs(false, direction));
        }
    }
}
