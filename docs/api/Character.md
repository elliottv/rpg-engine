# Character

Namespace: `RPGEngine` — a character present in the game world (the player or an NPC).

`Character` holds the position, facing direction (8 directions, cardinal + diagonal), movement
speed, the walk-cycle animation state and the list of spritesheet references used to render it.

## Remarks

- Spritesheets are referenced with `SpriteSheetRef`, which pairs a loaded sheet name with the
  **1-based character index (1..8)** to use within that sheet. Rendering resolves those
  references through the engine's `SpriteSheetManager` at draw time.
- A character uses either a single *full* sheet or one or more *part* sheets. Parts are composed
  in the fixed RPG Maker MZ order regardless of the order of entries in `SpriteSheets`; mixing
  full and part sheets (or using more than one full sheet) throws `InvalidOperationException` at
  draw time. A `SpriteSheetRef` whose `CharacterIndex` is outside 1..8 is rejected when used.

## Properties

### `Position Position`

Gets or sets the top-left world position of the character's sprite, in pixels.

```csharp
var character = new Character { Position = new Position(144, 192) };
```

### `Direction Direction`

Gets or sets the direction the character is facing.

```csharp
var character = new Character { Direction = Direction.Down };
```

### `double BaseSpeed`

Gets or sets the movement speed of the character in pixels per second.

```csharp
var character = new Character { BaseSpeed = 96 };
```

### `double AnimationCycleSpeed`

Gets or sets the movement speed (px/s) at which the walk cycle completes exactly one full cycle
per second. Defaults to 96 (matching the player's default `BaseSpeed`).

The walk cycle is the bounce `0 → 1 → 2 → 1`, i.e. 4 frame steps. The time per frame is
`secondsPerFrame = AnimationCycleSpeed / (BaseSpeed * FramesPerCycle)`, so at
`BaseSpeed == AnimationCycleSpeed == 96` one frame lasts 0.25 s (4 frames/s = 1 cycle/s).
Doubling the movement speed doubles the cycle rate; halving it halves it. Raising this property
(the reference speed) slows the animation relative to movement speed.

```csharp
var character = new Character { BaseSpeed = 96, AnimationCycleSpeed = 96 };
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

Moves the character in `direction` by `BaseSpeed * speedFactor * dt` pixels and sets the facing
direction. When `speedFactor` is zero the character only turns to face the direction without
moving. `dt` defaults to 1, so `Move(d, factor)` moves `BaseSpeed * factor` pixels (per-second
semantics).

```csharp
var character = new Character { BaseSpeed = 96 };
character.Move(Direction.Right, speedFactor: 1, dt: 0.5); // 48 px right
```

### `void Move(double speedFactor = 1, double dt = 1)`

Moves the character in its current facing direction. See the overload above for semantics.

```csharp
var character = new Character { Direction = Direction.Up, BaseSpeed = 48 };
character.Move(dt: 1); // 48 px up
```

## Example: configuring a character with a sheet name + index

```csharp
var character = new Character
{
    Position = new Position(96, 96),
    Direction = Direction.Down,
    BaseSpeed = 96,
};

character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
character.SpriteSheets.Add(new SpriteSheetRef("cape", CharacterIndex: 4));

Console.WriteLine(character.SpriteSheets.Count); // 2
```
