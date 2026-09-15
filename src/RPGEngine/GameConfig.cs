namespace RPGEngine;

/// <summary>
/// The base class of a game's configuration. The engine's own options are the four movement keys,
/// which default to WASD (<see cref="Key.W"/>, <see cref="Key.S"/>, <see cref="Key.A"/>,
/// <see cref="Key.D"/>); a host derives its own configuration type
/// (<c>sealed class MyGameConfig : GameConfig</c>) and adds the options of the subsystems the
/// engine deliberately does not implement (master audio volume, GUI/interaction key binds, …) as
/// ordinary properties.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameConfig"/> is abstract because it has no standalone use: every host defines its
/// own configuration type, and <c>sealed class MyGameConfig : GameConfig { }</c> is already a
/// valid, complete configuration. The engine only ever reads the movement key properties,
/// <see cref="GetDirection(Key)"/> and <see cref="GetMovementDirection(IEnumerable{Key})"/>; every
/// other member of a derived class (audio volume, GUI key binds, …) is plain data the engine
/// ignores.
/// </para>
/// <para>
/// The engine reads the configuration at input time and never caches a snapshot of it, so changes
/// to the values are taken into account immediately. No change-notification event mechanism is
/// required for this epic because <c>Input()</c> is invoked once per key event and consults the
/// configuration directly.
/// </para>
/// <para>
/// The movement keys must be unique: no two directions may be bound to the same
/// <see cref="Key"/>. Assigning a key that is already bound to another movement direction throws
/// <see cref="ArgumentException"/> and leaves the configuration unchanged, which prevents ambiguous
/// input (e.g. moving up and down with the same key). A later epic may relax this rule, for example
/// for multi-touch input, but not now.
/// </para>
/// <para>
/// The "one key, one action" rule is not limited to movement. A derived configuration that adds its
/// own bindings (an interact/GUI key, a gamepad button, …) is responsible for keeping those unique
/// as well, using the protected helpers <see cref="ThrowIfKeyAlreadyBoundToMovement"/>,
/// <see cref="ThrowIfKeyAlreadyBoundToAnotherDirection"/> and <see cref="ReservedKeys"/>. The rule
/// then holds in both directions: a custom binding refuses a key already bound to a movement
/// direction, and the inherited movement key setters refuse a key the derived class reserves.
/// </para>
/// <para>
/// A derived class is free to be <c>sealed</c>, to validate its own values in its own setters, and
/// to override <see cref="GetDirection(Key)"/> / <see cref="GetMovementDirection(IEnumerable{Key})"/>
/// to extend the key-to-direction mapping (gamepad bindings, extra keys) without the engine having
/// to know about it.
/// </para>
/// </remarks>
public abstract class GameConfig
{
    private Key _upKey = Key.W;
    private Key _downKey = Key.S;
    private Key _leftKey = Key.A;
    private Key _rightKey = Key.D;

    /// <summary>
    /// Initializes a new configuration of the derived game configuration type with the default WASD
    /// movement bindings.
    /// </summary>
    /// <remarks>
    /// This is the extension point of the class: a derived configuration gets the engine defaults
    /// for free and only has to initialize its own options.
    /// </remarks>
    protected GameConfig()
    {
    }

    /// <summary>
    /// Gets or sets the key used to move up. Defaults to <see cref="Key.W"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is already bound to another movement direction, or it is listed in
    /// <see cref="ReservedKeys"/> (a key the derived configuration uses for one of its own
    /// bindings).
    /// </exception>
    public Key UpKey
    {
        get => _upKey;
        set
        {
            ThrowIfKeyAlreadyBoundToAnotherDirection(value, Direction.Up, nameof(value));
            _upKey = value;
        }
    }

    /// <summary>
    /// Gets or sets the key used to move down. Defaults to <see cref="Key.S"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is already bound to another movement direction, or it is listed in
    /// <see cref="ReservedKeys"/> (a key the derived configuration uses for one of its own
    /// bindings).
    /// </exception>
    public Key DownKey
    {
        get => _downKey;
        set
        {
            ThrowIfKeyAlreadyBoundToAnotherDirection(value, Direction.Down, nameof(value));
            _downKey = value;
        }
    }

    /// <summary>
    /// Gets or sets the key used to move left. Defaults to <see cref="Key.A"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is already bound to another movement direction, or it is listed in
    /// <see cref="ReservedKeys"/> (a key the derived configuration uses for one of its own
    /// bindings).
    /// </exception>
    public Key LeftKey
    {
        get => _leftKey;
        set
        {
            ThrowIfKeyAlreadyBoundToAnotherDirection(value, Direction.Left, nameof(value));
            _leftKey = value;
        }
    }

    /// <summary>
    /// Gets or sets the key used to move right. Defaults to <see cref="Key.D"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is already bound to another movement direction, or it is listed in
    /// <see cref="ReservedKeys"/> (a key the derived configuration uses for one of its own
    /// bindings).
    /// </exception>
    public Key RightKey
    {
        get => _rightKey;
        set
        {
            ThrowIfKeyAlreadyBoundToAnotherDirection(value, Direction.Right, nameof(value));
            _rightKey = value;
        }
    }

    /// <summary>
    /// Returns the movement direction currently bound to <paramref name="key"/>,
    /// or <see langword="null"/> when the key is not bound to any direction.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>
    /// The <see cref="Direction"/> the key is currently bound to, or
    /// <see langword="null"/> when it is not bound. The result always reflects
    /// the current values of the movement key properties: the engine reads the
    /// configuration at input time and never uses a cached snapshot.
    /// </returns>
    /// <remarks>
    /// This is an extension point: a derived configuration can override it to add bindings the
    /// engine does not know about (gamepad buttons, extra keys, a numpad, …) on top of the inherited
    /// WASD movement keys. The base implementation must stay the single source of truth for the four
    /// movement key properties, so an override that only adds bindings should fall back to
    /// <c>base.GetDirection(key)</c> for the keys it does not handle. Because
    /// <see cref="GetMovementDirection(IEnumerable{Key})"/> resolves every pressed key through this
    /// method, extending it here extends key movement as a whole.
    /// </remarks>
    public virtual Direction? GetDirection(Key key) =>
        key == _upKey ? Direction.Up :
        key == _downKey ? Direction.Down :
        key == _leftKey ? Direction.Left :
        key == _rightKey ? Direction.Right :
        null;

    /// <summary>
    /// Returns the movement direction to use for the given set of currently pressed keys, or
    /// <see langword="null"/> when no movement should happen.
    /// </summary>
    /// <param name="pressedKeys">The keys currently held down (e.g. the engine's pressed-keys set).</param>
    /// <returns>
    /// The movement direction, or <see langword="null"/> when no key is bound to a movement
    /// direction or the bound directions cancel out (e.g. Up+Down or Left+Right held together).
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="Direction"/> is a continuous 2-D vector, but key input always maps to the
    /// eight canonical unit directions: every pressed key bound to a movement direction (via
    /// <see cref="GetDirection(Key)"/>) contributes its own canonical unit vector; the vectors
    /// are summed, normalized and snapped to the nearest of the eight canonical directions in
    /// <see cref="Direction.All"/> by dot product (<see cref="DirectionExtensions.Nearest8"/>).
    /// A non-null result is therefore always one of the eight canonical unit directions. This is
    /// what makes diagonal movement work: <c>W</c>+<c>D</c> resolves to
    /// <see cref="Direction.UpRight"/>, while <c>W</c>+<c>A</c>+<c>D</c> resolves to
    /// <see cref="Direction.Up"/> because A and D cancel. The configuration is read at input time
    /// and never cached, so rebinding takes effect immediately.
    /// </para>
    /// <para>
    /// This is the entry point the engine consumes, and it is virtual so a derived configuration can
    /// replace the whole key-to-direction mapping (e.g. drive movement from a gamepad or an
    /// analogue stick) without any engine change. The override contract is:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// return <see langword="null"/> to mean "no movement input" — the engine then stops the player
    /// (or, with no auto-walk queued, keeps it stopped) or advances the click-to-move auto-walk;
    /// </description></item>
    /// <item><description>
    /// a non-null result must be a unit direction vector (like every canonical
    /// <see cref="Direction"/>, or <c>Direction.Normalized</c> for a computed vector), because the
    /// engine multiplies it by the speed and the elapsed time to move the player.
    /// </description></item>
    /// </list>
    /// <para>
    /// An override that only adds keys should combine them with the base mapping (typically by
    /// overriding <see cref="GetDirection(Key)"/> and calling <c>base.GetMovementDirection(...)</c>
    /// when none of its own keys is held) so the inherited WASD defaults keep working.
    /// </para>
    /// </remarks>
    public virtual Direction? GetMovementDirection(IEnumerable<Key> pressedKeys)
    {
        ArgumentNullException.ThrowIfNull(pressedKeys);

        // Sum the canonical unit vectors of every held bound key. Each canonical direction is its
        // own unit vector, so this replaces the old sum of Direction.Delta() deltas 1:1.
        var sumX = 0d;
        var sumY = 0d;
        var hasBoundKey = false;

        foreach (var key in pressedKeys)
        {
            var direction = GetDirection(key);
            if (direction.HasValue)
            {
                sumX += direction.Value.X;
                sumY += direction.Value.Y;
                hasBoundKey = true;
            }
        }

        if (!hasBoundKey || (sumX == 0 && sumY == 0))
        {
            return null;
        }

        // Snap the summed vector to the nearest canonical direction (Nearest8 normalizes it
        // first and picks the largest dot product). For key input the sum always lands exactly
        // on one of the eight canonical directions, but the dot product makes the quantization
        // robust for arbitrary vectors.
        return new Direction(sumX, sumY).Nearest8();
    }

    /// <summary>
    /// Gets the keys this configuration reserves for bindings of its own, such as an
    /// interact/GUI key or a gamepad button. The engine never reads this property; it only feeds the
    /// movement key setters, which refuse a reserved key (<see cref="ArgumentException"/>) so that
    /// binding a movement direction to a key a custom action already uses is impossible.
    /// </summary>
    /// <value>
    /// The keys the derived configuration reserves for its own bindings. The default is an empty
    /// sequence, which leaves the behaviour of the movement keys untouched.
    /// </value>
    /// <remarks>
    /// A derived configuration overrides this property so that the "one key, one action" rule holds
    /// in both directions: its own bindings reject a movement key (via
    /// <see cref="ThrowIfKeyAlreadyBoundToMovement"/>) and the inherited movement key setters reject
    /// its keys (via <see cref="ThrowIfKeyAlreadyBoundToAnotherDirection"/>). A typical override is
    /// <c>protected override IEnumerable&lt;Key&gt; ReservedKeys =&gt; [InteractKey];</c>. Keep the
    /// result stable: it is evaluated on every movement key assignment.
    /// </remarks>
    protected virtual IEnumerable<Key> ReservedKeys => [];

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> is
    /// already bound to a movement direction other than
    /// <paramref name="direction"/>.
    /// </summary>
    /// <param name="key">The key being assigned.</param>
    /// <param name="direction">The direction the key is being assigned to.</param>
    /// <param name="paramName">The name of the property-setter value parameter.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is already bound to another movement direction, or it is listed in
    /// <see cref="ReservedKeys"/>.
    /// </exception>
    /// <remarks>
    /// The four movement key setters call this hook before assigning, so a rejected assignment
    /// leaves the configuration unchanged. It is protected so that a derived configuration can
    /// enforce the same uniqueness rule for its own bindings.
    /// </remarks>
    protected void ThrowIfKeyAlreadyBoundToAnotherDirection(Key key, Direction direction, string paramName)
    {
        var alreadyBound = (key == _upKey && direction != Direction.Up)
            || (key == _downKey && direction != Direction.Down)
            || (key == _leftKey && direction != Direction.Left)
            || (key == _rightKey && direction != Direction.Right);

        if (alreadyBound)
        {
            throw new ArgumentException(
                $"The key '{key}' is already bound to another movement direction; " +
                "each movement direction must use a distinct key.",
                paramName);
        }

        if (ReservedKeys.Contains(key))
        {
            throw new ArgumentException(
                $"The key '{key}' is reserved by this configuration for one of its own bindings; " +
                "each key can only be bound to one action.",
                paramName);
        }
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> is bound to any movement
    /// direction.
    /// </summary>
    /// <param name="key">The key being assigned to a non-movement binding.</param>
    /// <param name="paramName">The name of the property-setter value parameter.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is bound to one of the four movement directions.
    /// </exception>
    /// <remarks>
    /// The counterpart of <see cref="ThrowIfKeyAlreadyBoundToAnotherDirection"/> for the derived
    /// class's own bindings: a configuration that adds an "interact"/GUI key calls this hook in its
    /// setter before assigning, so the key cannot be one that moves the player and the assignment is
    /// rejected without changing the configuration. Together with
    /// <see cref="ReservedKeys"/> this keeps the "one key, one action" rule symmetric in both
    /// directions.
    /// </remarks>
    protected void ThrowIfKeyAlreadyBoundToMovement(Key key, string paramName)
    {
        var boundToMovement = key == _upKey || key == _downKey || key == _leftKey || key == _rightKey;

        if (boundToMovement)
        {
            throw new ArgumentException(
                $"The key '{key}' is already bound to a movement direction; " +
                "each key can only be bound to one action.",
                paramName);
        }
    }
}
