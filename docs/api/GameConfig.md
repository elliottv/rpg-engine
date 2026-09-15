# GameConfig

Namespace: `RPGEngine` — the abstract base class of a game's configuration.

`GameConfig` is **abstract**: it cannot be instantiated. Every game defines its own configuration
type by deriving from it (`sealed class MyGameConfig : GameConfig { }` is already a valid, complete
configuration) and adds the options of the subsystems the engine deliberately does not implement —
audio volume, GUI/interaction key binds, gamepad bindings, … — as ordinary properties.

An instance of the derived type is **mandatory**: it must be passed to the
`GameEngine(GameConfig)` constructor ([GameEngine.md](GameEngine.md)), which keeps that very
instance and reads it at input time. The engine never creates a configuration of its own.

The engine's own options are the four movement keys, which default to WASD (`Key.W`, `Key.S`,
`Key.A`, `Key.D`). The engine only ever reads those keys, `GetDirection` and `GetMovementDirection`;
everything else in a derived configuration is plain live data the engine ignores.

## What a derived class inherits

| Member | Inherited behaviour |
| --- | --- |
| `Key UpKey` / `DownKey` / `LeftKey` / `RightKey` | The WASD defaults, and the uniqueness rule: assigning a key already bound to another movement direction — or one listed in `ReservedKeys` — throws `ArgumentException` and leaves the configuration unchanged. |
| `Direction? GetDirection(Key key)` | The key → direction lookup of the four movement keys (`null` when the key is not bound). |
| `Direction? GetMovementDirection(IEnumerable<Key> pressedKeys)` | The combination of all held bound keys into one of the eight canonical directions (`null` when nothing moves). |
| The protected hooks | `ThrowIfKeyAlreadyBoundToAnotherDirection`, `ThrowIfKeyAlreadyBoundToMovement` and `ReservedKeys`, so a derived class can keep its own bindings unique too. |

A derived class is free to be `sealed`, to validate its own values and to override
`GetDirection` / `GetMovementDirection` to extend or replace the key-to-direction mapping.

## Remarks

- `GameConfig` is abstract rather than merely unsealed so that the type system enforces what the
  engine needs (an instance of a *derived* configuration) with a compile-time error instead of a
  runtime check; the engine never creates a configuration of its own once the host passes its
  instance.
- The engine reads the configuration **at input time and never caches a snapshot**, so changes take
  effect immediately.
- The "one key, one action" rule holds in both directions: a custom binding refuses a key bound to a
  movement direction, and a movement key refuses a key the derived class reserves (see
  `ReservedKeys`).
- The mapping is polymorphic: the engine consumes `GetMovementDirection`, so an override is
  honoured without any engine change. It must return `null` for "no movement input" (the engine then
  stops the player or advances the click-to-move auto-walk) and a **unit** direction vector
  otherwise.

## Constructors

### `protected GameConfig()`

Initializes a new configuration of the derived type with the default WASD movement bindings. This is
the extension point of the class: a derived configuration gets the engine defaults for free and only
has to initialize its own options.

```csharp
// A host configuration: a derived class cannot be built directly (GameConfig is abstract).
public sealed class MyGameConfig : GameConfig
{
}

var config = new MyGameConfig(); // WASD bindings, inherited
```

## Properties

### `Key UpKey` — defaults to `Key.W`

Gets or sets the key used to move up.

```csharp
var config = new MyGameConfig();
config.UpKey = Key.Up; // use the up-arrow key instead
```

### `Key DownKey` — defaults to `Key.S`

Gets or sets the key used to move down.

```csharp
var config = new MyGameConfig();
config.DownKey = Key.Down;
```

### `Key LeftKey` — defaults to `Key.A`

Gets or sets the key used to move left.

```csharp
var config = new MyGameConfig();
config.LeftKey = Key.Left;
```

### `Key RightKey` — defaults to `Key.D`

Gets or sets the key used to move right.

```csharp
var config = new MyGameConfig();
config.RightKey = Key.Right;
```

## Methods

### `Direction? GetDirection(Key key)`

Returns the movement direction currently bound to `key`, or `null` when the key is not bound to
any direction. The result always reflects the current property values. This method is **virtual**: a
derived configuration can override it to add bindings the engine does not know about (gamepad
buttons, extra keys, a numpad, …); an override that only adds keys should fall back to
`base.GetDirection(key)` so the inherited WASD keys keep working.

```csharp
var config = new MyGameConfig();
Direction? up = config.GetDirection(Key.W);   // Direction.Up
Direction? none = config.GetDirection(Key.Q); // null
```

### `Direction? GetMovementDirection(IEnumerable<Key> pressedKeys)`

Returns the movement direction to use for the given set of currently pressed keys, or `null`
when no movement should happen (no bound key, or the bound directions cancel out, e.g. Up+Down
or Left+Right held together).

`Direction` is a continuous 2-D vector (see `Direction.md`), but key input always maps to the
eight canonical unit directions: every pressed key bound to a movement direction (resolved through
`GetDirection`) contributes its own canonical unit vector; the vectors are summed, normalized and
snapped to the **nearest of the eight canonical directions in `Direction.All`** by dot product
(`DirectionExtensions.Nearest8`). A non-null result is therefore always one of the eight canonical
directions. This is what makes diagonal movement work: `W`+`D` resolves to `UpRight`, while
`W`+`A`+`D` resolves to `Up` because A and D cancel.

This is the entry point the engine consumes, and it is **virtual**, so a derived configuration can
replace the whole mapping (e.g. drive movement from a gamepad or an analogue stick). The override
contract is:

- return `null` to mean "no movement input" — the engine then stops the player or keeps it stopped
  (or advances the click-to-move auto-walk);
- a non-null result must be a **unit** direction vector, because the engine multiplies it by the
  speed and the elapsed time to move the player.

```csharp
var config = new MyGameConfig();
Direction? diagonal = config.GetMovementDirection([Key.W, Key.D]); // UpRight
Direction? cancelled = config.GetMovementDirection([Key.W, Key.S]); // null
```

## Protected extension hooks

### `protected virtual IEnumerable<Key> ReservedKeys`

Gets the keys this configuration reserves for bindings of its own (an interact/GUI key, a gamepad
button, …). The default is empty, which leaves the movement keys untouched. Override it so the
inherited movement key setters reject a key a custom action already uses — the other half of the
"one key, one action" rule:

```csharp
protected override IEnumerable<Key> ReservedKeys => [InteractKey];
```

### `protected void ThrowIfKeyAlreadyBoundToAnotherDirection(Key key, Direction direction, string paramName)`

Throws `ArgumentException` when `key` is already bound to another movement direction **or** is
listed in `ReservedKeys`. The movement key setters call it before assigning, so a rejected
assignment leaves the configuration unchanged. It is protected so a derived configuration can
enforce the same uniqueness rule for its own bindings.

### `protected void ThrowIfKeyAlreadyBoundToMovement(Key key, string paramName)`

Throws `ArgumentException` when `key` is bound to **any** movement direction. A derived
configuration that adds a non-movement binding calls it in its setter before assigning, so the key
cannot be one that moves the player and the rejected assignment leaves the configuration unchanged.

## Example: rebinding and uniqueness

```csharp
// The game's configuration: a user-defined subclass (GameConfig is abstract).
public sealed class MyGameConfig : GameConfig
{
}

var config = new MyGameConfig();

// Defaults are WASD.
Console.WriteLine(config.GetDirection(Key.W)); // Up

// Rebinding takes effect immediately.
config.UpKey = Key.Up;
Console.WriteLine(config.GetDirection(Key.Up)); // Up
Console.WriteLine(config.GetDirection(Key.W));  // null

// A key already bound to another direction is rejected and leaves config unchanged.
try
{
    config.DownKey = Key.Up; // throws ArgumentException
}
catch (ArgumentException)
{
}
```

## Example: a game configuration with user-defined options and bindings

The derived class adds the options of the subsystems the engine does not implement (here: the master
audio volume and the GUI/interaction key) and keeps its own binding unique with the protected hooks.

```csharp
/// <summary>My game's configuration: engine movement keys plus options the engine knows nothing about.</summary>
public sealed class MyGameConfig : GameConfig
{
    /// <summary>Gets or sets the master audio volume, 0..1. The engine never reads this.</summary>
    public float MasterVolume { get; set; } = 1f;

    private Key _interactKey = Key.E;

    /// <summary>Gets or sets the key that opens the dialogue/GUI panel.</summary>
    /// <exception cref="ArgumentException">The key is already bound to a movement direction.</exception>
    public Key InteractKey
    {
        get => _interactKey;
        set
        {
            ThrowIfKeyAlreadyBoundToMovement(value, nameof(value));
            _interactKey = value;
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<Key> ReservedKeys => [InteractKey];
}

// The derived configuration inherits the engine defaults and adds its own options.
var config = new MyGameConfig { MasterVolume = 0.5f };
Direction? up = config.GetMovementDirection([Key.W]); // Direction.Up (inherited)
config.UpKey = Key.E;       // throws ArgumentException: E is reserved for InteractKey
config.InteractKey = Key.W; // throws ArgumentException: W moves up
```

## Example: replacing the mapping (polymorphism)

```csharp
// A configuration that replaces the whole key-to-direction mapping: any pressed-key set means
// "move right" (the engine stops the player when the mapping returns null).
public sealed class AlwaysRightConfig : GameConfig
{
    public override Direction? GetMovementDirection(IEnumerable<Key> pressedKeys) => Direction.Right;
}

GameConfig config = new AlwaysRightConfig();
Console.WriteLine(config.GetMovementDirection(Array.Empty<Key>())); // Right
```

## See also

- [Key.md](Key.md) — the framework-agnostic key enum and host key translation.
- [Direction.md](Direction.md) — the direction vector and the eight canonical directions.
- [GameEngine.md](GameEngine.md) — the engine that reads the configuration.
