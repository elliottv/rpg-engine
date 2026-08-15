# GameConfig

Namespace: `RPGEngine` — the configuration object of the engine.

`GameConfig` holds the keyboard keys used for movement, which default to WASD
(`Key.W`, `Key.S`, `Key.A`, `Key.D`).

## Remarks

- The engine reads `GameConfig` at input time and never caches a snapshot, so changes take
  effect immediately.
- The movement keys must be unique: no two directions may be bound to the same `Key`. Assigning
  a key that is already bound to another movement direction throws `ArgumentException` and leaves
  the configuration unchanged.

## Properties

### `Key UpKey` — defaults to `Key.W`

Gets or sets the key used to move up.

```csharp
var config = new GameConfig();
config.UpKey = Key.Up; // use the up-arrow key instead
```

### `Key DownKey` — defaults to `Key.S`

Gets or sets the key used to move down.

```csharp
var config = new GameConfig();
config.DownKey = Key.Down;
```

### `Key LeftKey` — defaults to `Key.A`

Gets or sets the key used to move left.

```csharp
var config = new GameConfig();
config.LeftKey = Key.Left;
```

### `Key RightKey` — defaults to `Key.D`

Gets or sets the key used to move right.

```csharp
var config = new GameConfig();
config.RightKey = Key.Right;
```

## Methods

### `Direction? GetDirection(Key key)`

Returns the movement direction currently bound to `key`, or `null` when the key is not bound to
any direction. The result always reflects the current property values.

```csharp
var config = new GameConfig();
Direction? up = config.GetDirection(Key.W);   // Direction.Up
Direction? none = config.GetDirection(Key.Q); // null
```

### `Direction? GetMovementDirection(IEnumerable<Key> pressedKeys)`

Returns the movement direction to use for the given set of currently pressed keys, or `null`
when no movement should happen (no bound key, or the bound directions cancel out, e.g. Up+Down
or Left+Right held together).

Every pressed key bound to a movement direction contributes its unit delta; the deltas are
summed, normalized and quantized to the **nearest of the eight `Direction` values** by dot
product against each direction's unit delta. This is what makes diagonal movement work: `W`+`D`
resolves to `UpRight`, while `W`+`A`+`D` resolves to `Up` because A and D cancel.

```csharp
var config = new GameConfig();
Direction? diagonal = config.GetMovementDirection([Key.W, Key.D]); // UpRight
Direction? cancelled = config.GetMovementDirection([Key.W, Key.S]); // null
```

## Example: rebinding and uniqueness

```csharp
var config = new GameConfig();

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
