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
collision-resolved and auto-walk movement through its internal `ReportMovement` /
`ReportAutoWalkStep` bridges, and reports a fully blocked move through its internal
`ReportBlockedMove` bridge).

The player exposes a **movement-state machine** through two events:

- **`OnStartMoving`** fires *exactly* when the player **begins** moving and **before the
  position is updated**:
  - move-by-key: on the first frame movement starts (the frame a pressed key takes effect,
    idle → moving);
  - click-to-move: each time a new auto-walk step begins (the first step, and every time the
    next waypoint is reached while another remains);
  - it does **not** fire at any other time — no direction-change-while-moving events, no
    per-frame events.
- **`OnStopMoving`** fires when the player stops moving:
  - move-by-key: every movement key is released (the player goes idle);
  - click-to-move: the last auto-walk step is reached (the path completes);
  - the player is blocked by a collision (a fully blocked move).

Both events carry only the facing direction (`PlayerMoveEventArgs.Direction`), which is all a
host needs to mirror the player on other clients via `Character.StartMoving` /
`Character.StopMoving`.

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

Gets or sets the world position of the player's **feet** — the *middle-bottom* of the
sprite — in tiles. The sprite is rendered above and centered on this point. Forwards to
`Character.Position`.

```csharp
player.Position = new Position(6, 6); // feet at the centre of tile (6, 6)
```

### `Direction Direction`

Gets or sets the direction the player is facing. Forwards to `Character.Direction` and keeps
the movement event state in sync so `OnStartMoving` / `OnStopMoving` report the correct facing
direction.

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

### `event EventHandler<PlayerMoveEventArgs>? OnStartMoving`

Occurs when the player **starts moving**, **before the position is updated**:

- **Move-by-key**: on the first frame movement starts (the frame a pressed key takes effect,
  idle → moving). A move while already moving — same direction *or* a direction change — raises
  nothing, and a speed-factor-zero `Move` (a turn only) raises nothing either.
- **Click-to-move** (auto-walk): each time a new auto-walk step begins — the first step, and
  every time the next waypoint is reached while another remains. `OnStartMoving` therefore fires
  once **per auto-walk step** (once per waypoint in the path), and the event is raised before
  that step's displacement is applied.

The event carries the direction the player is moving in (`PlayerMoveEventArgs.Direction`).

```csharp
var player = new Player();
player.OnStartMoving += (_, e) =>
{
    Console.WriteLine($"started moving {e.Direction}");
};

player.Move(Direction.Right, speedFactor: 1, dt: 1); // prints "started moving Right"
player.Move(Direction.Down, speedFactor: 1, dt: 1);  // direction change while moving: nothing
```

### `event EventHandler<PlayerMoveEventArgs>? OnStopMoving`

Occurs when the player **stops moving**:

- **Move-by-key**: every movement key is released (the player goes idle, via `Stop()`).
- **Click-to-move**: the last auto-walk step is reached (the path completes).
- **Collision stop**: the player is fully blocked by a solid tile or the map edge. The engine
  reports the fully blocked move, so `OnStopMoving` fires **even while the movement key is
  still held** — the stop is reported exactly once, and holding the key against the same wall
  does not raise the event again. From idle, a move that is immediately fully blocked fires
  `OnStartMoving` then `OnStopMoving` in the same frame. The reported direction is the
  direction the player tried to move in (the player turns to face the wall).

The event carries the direction the player was last moving in (`PlayerMoveEventArgs.Direction`).
Stopping does not change the facing direction.

```csharp
var player = new Player();
player.OnStopMoving += (_, e) =>
{
    Console.WriteLine($"stopped moving {e.Direction}");
};

player.Move(Direction.Right, speedFactor: 1, dt: 1); // starts moving Right
player.Stop();                                       // prints "stopped moving Right"

// Collision stop while the movement key is held: the engine reports the fully blocked move.
// With D held against a solid tile (or the map edge), OnStopMoving fires (Right) exactly once
// when the player is blocked; keeping D held against the same wall raises nothing more.
```

### `sealed record PlayerMoveEventArgs(Direction Direction)`

The event arguments of `OnStartMoving` / `OnStopMoving`: the facing direction of the player.
For a start it is the direction the player is moving in; for a stop it is the direction the
player was last moving in.

```csharp
void OnPlayerMoved(object? sender, PlayerMoveEventArgs e)
{
    Console.WriteLine($"facing {e.Direction}");
}
```

## Methods

### `void Move(Direction direction, double speedFactor = 1, double dt = 1)`

Moves the player in `direction` by `BaseSpeed * speedFactor * dt` tiles and sets the facing
direction. Forwards to `Character.Move`.

This method also drives the movement-state machine: with `speedFactor` greater than zero the
player is considered *moving* (it actually moves), so `OnStartMoving` fires **before** the
displacement when the player starts moving (idle → moving). A move while already moving (same
or new direction) raises nothing. With `speedFactor == 0` the player only turns: no event is
raised (a turn is neither a start nor a stop).

```csharp
player.Move(Direction.Right, dt: 1.0 / 60);
player.Move(Direction.Up, speedFactor: 0); // turn only: no event
```

### `void Move(double speedFactor = 1, double dt = 1)`

Moves the player in its current facing direction. Forwards to `Character.Move` and uses the
same event semantics as `Move(direction, speedFactor, dt)`.

```csharp
player.Move(dt: 1.0 / 60);
```

### `void Stop()`

Transitions the player to idle (stops moving) and raises `OnStopMoving` with the direction the
player was last moving in. When the player is already idle this is a no-op and does not raise
the event. The engine calls it when there is no key input and no auto-walk target. Stopping
does not change the facing direction: the player keeps facing the direction it was last moving,
and that direction is reported in the event.

```csharp
player.Move(Direction.Right, speedFactor: 1, dt: 1);
player.Stop(); // raises OnStopMoving with Direction = Right
player.Stop(); // already idle: no event
```

## Example: subscribing to OnStartMoving / OnStopMoving for key and auto-walk movement

```csharp
var player = new Player();
var moveLog = new List<string>();
player.OnStartMoving += (_, e) => moveLog.Add($"start {e.Direction}");
player.OnStopMoving += (_, e) => moveLog.Add($"stop {e.Direction}");

// Manual movement: the engine calls Move (or drives the player directly).
player.Move(Direction.Right, speedFactor: 1, dt: 1);  // "start Right"
player.Stop();                                         // "stop Right"

// Auto-walk (via GameEngine.Click) raises the same events through the engine's internal
// ReportAutoWalkStep bridge — one "start" per step and a single "stop" at completion.
Console.WriteLine(string.Join(", ", moveLog));
```
