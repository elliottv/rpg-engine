# Player

Namespace: `RPGEngine` — represents the player character controlled by the user.

## Remarks

The player is **composed** of a `Character` (composition, no inheritance) which carries all of
the in-world state: position (in tiles), facing direction, movement speed (in tiles per second)
and the list of spritesheet references. `Player` is a thin wrapper that forwards state access
and movement to that character, so configuring `SpriteSheets` or moving the player is exactly
equivalent to configuring/moving the underlying `Character`.

The player does not listen to input itself. The engine (`GameEngine`) owns the pressed-keys
state and calls `Move(direction, 1, dt)` in its update loop (or drives the player directly for
collision-resolved and auto-walk movement through its internal `ReportMovement` bridge).

The player exposes a **movement-state machine** through the `OnMove` event: it fires whenever
the player *starts moving* (idle → moving), *stops moving* (moving → idle, via `Stop()`), or
*changes direction while moving*. The event is raised for both manual (key) movement and
auto-walk movement, so hosts can react to the player's movement state without polling the
position every frame.

## Fields

### `const double DefaultBaseSpeed = 2`

The default movement speed in **tiles per second** of a player created by the parameterless
constructor (2 tiles/s, the tile-unit equivalent of the previous 96 px/s with 48 px tiles: two
48px map tiles every second).

## Constructors

### `Player()` — creates its own `Character` with `DefaultBaseSpeed`

```csharp
var player = new Player();
```

### `Player(Character character)` — wraps the provided character

```csharp
var character = new Character { BaseSpeed = 1 };
var player = new Player(character);
```

## Properties

### `Character Character`

Gets or sets the `Character` that represents the player in the game world. All other members of
`Player` forward to this character, so replacing it replaces the player's position, direction,
speed and spritesheets as well. The movement event state is re-synchronized to the new
character's facing direction.

```csharp
var player = new Player();
player.Character.Position = new Position(6, 6);
```

### `Position Position`

Gets or sets the top-left world position of the player's sprite, in tiles. Forwards to
`Character.Position`.

```csharp
player.Position = new Position(6, 6);
```

### `Direction Direction`

Gets or sets the direction the player is facing. Forwards to `Character.Direction` and keeps
the movement event state in sync so `OnMove` reports the correct facing direction.

```csharp
player.Direction = Direction.Up;
```

### `IList<SpriteSheetRef> SpriteSheets`

Gets the mutable list of spritesheet references used to render the player. Each entry pairs a
loaded sheet name with the 1-based character index (1..8). Forwards to `Character.SpriteSheets`:
the returned list is the same instance, so entries added through the player are immediately
visible on the underlying character.

```csharp
player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
```

## Events

### `event EventHandler<PlayerMoveEventArgs>? OnMove`

Occurs when the player's movement state changes: it **starts moving** (idle → moving), **stops
moving** (moving → idle, via `Stop()`), or **changes direction while moving**. The event
carries the new state (`PlayerMoveEventArgs.IsMoving`) and the player's current facing direction
(`PlayerMoveEventArgs.Direction`). It is raised for both manual (key) movement and auto-walk
movement.

```csharp
var player = new Player();
player.OnMove += (_, e) =>
{
    Console.WriteLine(e.IsMoving ? "moving" : "stopped");
    Console.WriteLine($"facing {e.Direction}");
};

player.Move(Direction.Right, speedFactor: 1, dt: 1); // prints "moving" / "facing Right"
player.Stop();                                       // prints "stopped" / "facing Right"
```

### `sealed record PlayerMoveEventArgs(bool IsMoving, Direction Direction)`

The event arguments of `OnMove`: whether the player is now moving (`IsMoving`) and its current
facing direction (`Direction`). For a speed-factor-zero turn (see `Move`), `IsMoving` reflects
the movement state at the moment of the turn (moving or idle).

```csharp
void OnPlayerMove(object? sender, PlayerMoveEventArgs e)
{
    Console.WriteLine($"IsMoving = {e.IsMoving}, Direction = {e.Direction}");
}
```

## Methods

### `void Move(Direction direction, double speedFactor = 1, double dt = 1)`

Moves the player in `direction` by `BaseSpeed * speedFactor * dt` tiles and sets the facing
direction. Forwards to `Character.Move`.

This method also drives the movement-state machine: with `speedFactor` greater than zero the
player is considered *moving* (it actually moves), so `OnMove` fires with `IsMoving = true`
when the player starts moving or changes direction while moving. With `speedFactor == 0` the
player only turns: `OnMove` fires on a direction change carrying the current movement state
(`true` when the player was already moving, `false` when idle).

```csharp
player.Move(Direction.Right, dt: 1.0 / 60);
player.Move(Direction.Up, speedFactor: 0); // turn only: OnMove with the current moving state
```

### `void Move(double speedFactor = 1, double dt = 1)`

Moves the player in its current facing direction. Forwards to `Character.Move`.

```csharp
player.Move(dt: 1.0 / 60);
```

### `void Stop()`

Transitions the player to idle (stops moving) and raises `OnMove` with `IsMoving = false`. When
the player is already idle this is a no-op and does not raise the event. The engine calls it when
there is no key input and no auto-walk target. Stopping does not change the facing direction: the
player keeps facing the direction it was last moving, and that direction is reported in the event.

```csharp
player.Move(Direction.Right, speedFactor: 1, dt: 1);
player.Stop(); // raises OnMove with IsMoving = false, Direction = Right
player.Stop(); // already idle: no event
```

## Example: subscribing to OnMove for both key and auto-walk movement

```csharp
var player = new Player();
var moveLog = new List<PlayerMoveEventArgs>();
player.OnMove += (_, e) => moveLog.Add(e);

// Manual movement: the engine calls Move (or drives the player directly).
player.Move(Direction.Right, speedFactor: 1, dt: 1);  // IsMoving = true
player.Stop();                                         // IsMoving = false

// Auto-walk (via GameEngine.Click) raises the same events through the engine's
// internal ReportMovement bridge, so the log above is identical in shape.
Console.WriteLine(string.Join(", ", moveLog));
```
