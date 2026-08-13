# SpriteSheetType

Namespace: `RPGEngine.Sprites` — the kind of an RPG Maker MZ spritesheet.

## Values

| Value | Meaning |
| --- | --- |
| `Full` | A complete character sheet: every layer (body, armour, face, hair, head) is baked into one image. |
| `Part` | A single character layer (see `CharacterPartType`) that is composed with other part sheets to build a complete character. |

## Example

```csharp
var manager = new SpriteSheetManager();

// Load() creates a Full sheet; LoadPart() creates a Part sheet.
using (var stream = File.OpenRead("assets/characters/character_full.png"))
{
    var full = manager.Load("hero", stream);
    Console.WriteLine(full.Type); // SpriteSheetType.Full
}

using (var stream = File.OpenRead("assets/characters/character_part_body.png"))
{
    var part = manager.LoadPart("body", stream, CharacterPartType.Body);
    Console.WriteLine(part.Type);      // SpriteSheetType.Part
    Console.WriteLine(part.PartType);  // CharacterPartType.Body
}
```
