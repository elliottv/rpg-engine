# Character

Namespace: `RPGEngine` — a character present in the game world (the player or an NPC).

`Character` holds the position (in **tiles**), facing direction (8 directions, cardinal +
diagonal), movement speed (in tiles per second), the walk-cycle animation state and the list of
spritesheet references used to render it.

## Remarks

- Spritesheets are referenced with `SpriteSheetRef`, which pairs a loaded sheet name with the
  **1-based character index (1..8)** to use within that sheet. Rendering resolves those
  references through the engine's `SpriteSheetManager` at draw time.
- A character uses either a single *full* sheet or one or more *part* sheets. Parts are composed
  in the fixed RPG Maker MZ order regardless of the order of entries in `SpriteSheets`; mixing
  full and part sheets (or using more than one full sheet) throws `InvalidOperationException` at
  draw time. A `SpriteSheetRef` whose `CharacterIndex` is outside 1..8 is rejected when used.
- All world coordinates are in tiles. Pixels are produced only at the canvas boundary (the
  engine multiplies by the map's tile size when rendering).

## Properties

### `Position Position`

Gets or sets the world position of the character's **feet** — the *middle-bottom* of the
sprite — in tiles. The sprite is rendered above and centered on this point, so a position of
`(8.5, 8.5)` means the character stands with its feet at the centre of tile `(8, 8)`. In world
pixels the sprite's top-left is at `(pos.X*ts - w/2, pos.Y*ts - h)` where `ts` is the map's
tile size and `w`/`h` the sprite's pixel size.

```csharp
// Feet (middle-bottom) at the centre of tile (3, 4): the sprite is drawn above and centered
// on this point.
var character = new Character { Position = new Position(3.5, 4.5) };
```

### `Direction Direction`

Gets or sets the direction the character is facing.

```csharp
var character = new Character { Direction = Direction.Down };
```

### `double BaseSpeed`

Gets or sets the movement speed of the character in **tiles per second**.

```csharp
var character = new Character { BaseSpeed = 2 };
```

### `bool IsMoving`

Gets whether the character is currently moving **autonomously** — started with
`StartMoving(direction)` and not yet stopped with `StopMoving()`. While `true`, every
`Update(dt)` moves the character towards its current `Direction` by `BaseSpeed * dt` tiles.

This is independent of the engine's key-driven player movement: it targets characters the host
drives itself (e.g. NPCs in `GameEngine.Characters`). Do **not** combine `StartMoving` on the
player's character with the engine's key-driven player movement — `GameEngine.Update` calls
`Player.Character.Update(dt)` each frame, so both displacements would add up.

```csharp
var npc = new Character { BaseSpeed = 2 };
npc.StartMoving(Direction.Right);
Console.WriteLine(npc.IsMoving); // True
npc.StopMoving();
Console.WriteLine(npc.IsMoving); // False
```

### `double AnimationCycleSpeed`

Gets or sets the movement speed (tiles/s) at which the walk cycle completes exactly one full
cycle per second. Defaults to **2** (matching the player's default `BaseSpeed`).

The walk cycle is the bounce `0 → 1 → 2 → 1`, i.e. 4 frame steps. The time per frame is
`secondsPerFrame = AnimationCycleSpeed / (BaseSpeed * FramesPerCycle)`, so at
`BaseSpeed == AnimationCycleSpeed == 2` one frame lasts 0.25 s (4 frames/s = 1 cycle/s).
Doubling the movement speed doubles the cycle rate; halving it halves it. Raising this property
(the reference speed) slows the animation relative to movement speed.

```csharp
var character = new Character { BaseSpeed = 2, AnimationCycleSpeed = 2 };
// One full walk cycle (0 → 1 → 2 → 1) completes every second.
```

### `IList<SpriteSheetRef> SpriteSheets`

Gets the mutable list of spritesheet references used to render the character. Each entry pairs a
loaded sheet name with the 1-based character index (1..8). The order of the entries is
irrelevant for part sheets — they are composed in the fixed RPG Maker MZ order.

```csharp
var character = new Character();
character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
character.SpriteSheets.Add(new SpriteSheetRef("cape", CharacterIndex: 4));
```

## Methods

### `void Move(Direction direction, double speedFactor = 1, double dt = 1)`

Moves the character in `direction` by `BaseSpeed * speedFactor * dt` tiles and sets the facing
direction. When `speedFactor` is zero the character only turns to face the direction without
moving. `dt` defaults to 1, so `Move(d, factor)` moves `BaseSpeed * factor` tiles (per-second
semantics).

```csharp
var character = new Character { BaseSpeed = 2 };
character.Move(Direction.Right, speedFactor: 1, dt: 0.5); // 1 tile right
```

### `void Move(double speedFactor = 1, double dt = 1)`

Moves the character in its current facing direction. See the overload above for semantics.

```csharp
var character = new Character { Direction = Direction.Up, BaseSpeed = 1 };
character.Move(dt: 1); // 1 tile up
```

### `void StartMoving(Direction direction)`

Starts **autonomous movement**: the character faces `direction` and moves towards it on every
`Update(dt)` — exactly like the player does while a movement key is held — until
`StopMoving()` is called. The character starts moving on the *next* `Update`: this method only
sets the facing direction and the `IsMoving` state, and never changes `Position` itself. The
engine's update loop calls `Update(dt)` on every character each frame, so a started character
moves automatically.

Autonomous movement is **not** collision-resolved by the engine (collision resolution applies
to the player only), so hosts that move NPCs with `StartMoving` are responsible for keeping
them in bounds themselves. Do not combine `StartMoving` on the player's character with the
engine's key-driven player movement.

```csharp
var npc = new Character { BaseSpeed = 2, Position = new Position(3, 4) };
npc.StartMoving(Direction.Right); // faces right and begins moving on the next Update
```

### `void StopMoving()`

Stops autonomous movement started with `StartMoving()`. The character stays where it is and the
walk-cycle animation snaps back to the standing frame on the next `Update`. Calling it when the
character is not moving is a no-op (idempotent).

```csharp
npc.StopMoving(); // the NPC stops; the walk cycle snaps to the standing frame on the next Update
```

## Example: configuring a character with a sheet name + index

```csharp
var character = new Character
{
    Position = new Position(2, 2),
    Direction = Direction.Down,
    BaseSpeed = 2,
};

character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
character.SpriteSheets.Add(new SpriteSheetRef("cape", CharacterIndex: 4));

Console.WriteLine(character.SpriteSheets.Count); // 2
```

## Example: an NPC that patrols with StartMoving / StopMoving

`StartMoving`/`StopMoving` give hosts a simple way to drive NPCs autonomously through the
engine's update loop: the NPC is added to `GameEngine.Characters` (so the engine calls
`Update(dt)` on it every frame), and the host periodically switches its direction or stops it.

```csharp
// An NPC that walks right for 2 seconds, then left for 2 seconds, forever.
var npc = new Character { BaseSpeed = 2, Position = new Position(3, 4) };
npc.SpriteSheets.Add(new SpriteSheetRef("villager", CharacterIndex: 2));
engine.Characters.Add(npc);

npc.StartMoving(Direction.Right); // begins moving right on the next engine.Update

// ... a few engine.Update(dt) calls later, after ~2 seconds ...
npc.StopMoving();                 // stays put; walk cycle snaps to the standing frame
npc.StartMoving(Direction.Left);  // turns around and starts moving left
```

> Note: `Character.Update(dt)` is internal — hosts do not call it directly. The engine calls it
> on the player and every NPC in `GameEngine.Characters` each frame, which is what makes a
> started character move automatically.
