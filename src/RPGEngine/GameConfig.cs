namespace RPGEngine;

/// <summary>
/// The configuration object of the engine. For this epic it only holds the
/// keyboard keys used for movement, which default to WASD
/// (<see cref="Key.W"/>, <see cref="Key.S"/>, <see cref="Key.A"/>, <see cref="Key.D"/>).
/// </summary>
/// <remarks>
/// <para>
/// The engine reads <see cref="GameConfig"/> at input time and never caches a
/// snapshot of it, so changes to the values are taken into account immediately.
/// No change-notification event mechanism is required for this epic because
/// <c>Input()</c> is invoked once per key event and consults the configuration
/// directly.
/// </para>
/// <para>
/// The movement keys must be unique: no two directions may be bound to the same
/// <see cref="Key"/>. Assigning a key that is already bound to another movement
/// direction throws <see cref="ArgumentException"/> and leaves the configuration
/// unchanged, which prevents ambiguous input (e.g. moving up and down with the
/// same key). A later epic may relax this rule, for example for multi-touch
/// input, but not now.
/// </para>
/// </remarks>
public sealed class GameConfig
{
    private Key _upKey = Key.W;
    private Key _downKey = Key.S;
    private Key _leftKey = Key.A;
    private Key _rightKey = Key.D;

    /// <summary>
    /// Gets or sets the key used to move up. Defaults to <see cref="Key.W"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is already bound to another movement direction.
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
    /// The value is already bound to another movement direction.
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
    /// The value is already bound to another movement direction.
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
    /// The value is already bound to another movement direction.
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
    public Direction? GetDirection(Key key) =>
        key == _upKey ? Direction.Up :
        key == _downKey ? Direction.Down :
        key == _leftKey ? Direction.Left :
        key == _rightKey ? Direction.Right :
        null;

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="key"/> is
    /// already bound to a movement direction other than
    /// <paramref name="direction"/>.
    /// </summary>
    /// <param name="key">The key being assigned.</param>
    /// <param name="direction">The direction the key is being assigned to.</param>
    /// <param name="paramName">The name of the property-setter value parameter.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is already bound to another movement direction.
    /// </exception>
    private void ThrowIfKeyAlreadyBoundToAnotherDirection(Key key, Direction direction, string paramName)
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
    }
}
