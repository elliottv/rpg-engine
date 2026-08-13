# CharacterPartType

Namespace: `RPGEngine.Sprites` — the layer of a character that an RPG Maker MZ part spritesheet
provides.

## Values

| Value | Meaning |
| --- | --- |
| `Body` | The character's body. |
| `Armour` | The character's armour layer. |
| `Face` | The character's face. |
| `FaceHair` | The hair attached to the face layer. |
| `Hair1` | The first (base) hair layer. |
| `Hair2` | The second (overlay) hair layer. |
| `Head` | The character's head. |

The parts are composed in the fixed order documented in [Architecture](../Architecture.md).

## Example

```csharp
// Load a couple of part sheets and reference them by sheet name + character index.
engine.LoadPartSpriteSheet("body", "assets/characters/character_part_body.png", CharacterPartType.Body);
engine.LoadPartSpriteSheet("face", "assets/characters/character_part_face.png", CharacterPartType.Face);
engine.LoadPartSpriteSheet("hair1", "assets/characters/character_part_hair1.png", CharacterPartType.Hair1);

var npc = new Character();
npc.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 2));
npc.SpriteSheets.Add(new SpriteSheetRef("face", CharacterIndex: 2));
npc.SpriteSheets.Add(new SpriteSheetRef("hair1", CharacterIndex: 2));
```
