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
