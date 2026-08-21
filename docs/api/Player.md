# Player

Namespace: `RPGEngine` — represents the player character controlled by the user.

## Remarks

The player is **composed** of a `Character` (composition, no inheritance) which carries all of
the in-world state: position (in tiles), facing direction, movement speed (in tiles per second)
and the list of spritesheet references. `Player` is a thin wrapper that forwards state access
and movement to that character, so configuring `SpriteSheets` or moving the player is exactly
equivalent to configuring/moving the underlying `Character`.

The player does not listen to input itself. The engine (`GameEngine`) owns the pressed-keys
state and calls `Move(direction, 1, dt)` in its update loop.

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
speed and spritesheets as well.

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

Gets or sets the direction the player is facing. Forwards to `Character.Direction`.

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

## Methods

### `void Move(Direction direction, double speedFactor = 1, double dt = 1)`

Moves the player in `direction` by `BaseSpeed * speedFactor * dt` tiles and sets the facing
direction. Forwards to `Character.Move`.

```csharp
player.Move(Direction.Right, dt: 1.0 / 60);
```

### `void Move(double speedFactor = 1, double dt = 1)`

Moves the player in its current facing direction. Forwards to `Character.Move`.

```csharp
player.Move(dt: 1.0 / 60);
```

## Example: configuring the player with a sheet name + index

```csharp
var player = new Player();
player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

// The list is the underlying character's list.
Console.WriteLine(ReferenceEquals(player.Character.SpriteSheets, player.SpriteSheets)); // True
```
