using System.IO;
using RPGEngine.Sprites;
using RPGEngine.Tiled;

namespace RPGEngine.Sample.Desktop;

/// <summary>
/// Builds the canonical sample scene shared by the desktop and WebAssembly sample hosts: the
/// committed fixture map, a full character sheet for the player and part sheets for two NPCs.
/// The desktop host loads everything through the file-system based loaders after materializing
/// the committed fixtures into a temporary directory (see <see cref="FixtureAssets"/>).
/// </summary>
internal static class SampleScene
{
    /// <summary>Tile size in pixels of the fixture map and the RPG Maker MZ sheets.</summary>
    public const int TileSize = 48;

    /// <summary>Player world position in tiles (on the sand path of the fixture map).</summary>
    public static readonly Position PlayerPosition = new(6, 6);

    /// <summary>First NPC world position in tiles (a villager made of body/face/hair1 parts).</summary>
    public static readonly Position VillagerPosition = new(3, 4);

    /// <summary>Second NPC world position in tiles (a guard made of body/face/armour/head parts).</summary>
    public static readonly Position GuardPosition = new(11, 8);

    /// <summary>
    /// Builds the engine for the sample scene from a directory that contains the materialized
    /// fixtures (see <see cref="FixtureAssets.MaterializeToTempDirectory"/>).
    /// </summary>
    /// <param name="assetsRoot">Directory containing the materialized fixtures.</param>
    public static GameEngine Create(string assetsRoot)
    {
        var engine = new GameEngine
        {
            Map = TileMap.Load(Path.Combine(assetsRoot, FixtureAssets.MapFile)),
        };

        // Player: a single full sheet, character slot 1.
        engine.LoadSpriteSheet("hero", Path.Combine(assetsRoot, FixtureAssets.FullSheet));
        engine.Player.Position = PlayerPosition;
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // NPC 1 "villager": body + face + hair1 part sheets, character slot 2.
        // NPC speed is 1 tile/s (the tile-unit equivalent of the previous 48 px/s at 48px tiles).
        var villager = new Character
        {
            Position = VillagerPosition,
            BaseSpeed = 1,
        };
        engine.LoadPartSpriteSheet("villager_body", Path.Combine(assetsRoot, FixtureAssets.PartBody), CharacterPartType.Body);
        engine.LoadPartSpriteSheet("villager_face", Path.Combine(assetsRoot, FixtureAssets.PartFace), CharacterPartType.Face);
        engine.LoadPartSpriteSheet("villager_hair1", Path.Combine(assetsRoot, FixtureAssets.PartHair1), CharacterPartType.Hair1);
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_face", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_hair1", CharacterIndex: 2));
        engine.Characters.Add(villager);

        // NPC 2 "guard": body + face + armour + head part sheets, character slot 3.
        // NPC speed is 1 tile/s (the tile-unit equivalent of the previous 48 px/s at 48px tiles).
        var guard = new Character
        {
            Position = GuardPosition,
            BaseSpeed = 1,
        };
        engine.LoadPartSpriteSheet("guard_body", Path.Combine(assetsRoot, FixtureAssets.PartBody), CharacterPartType.Body);
        engine.LoadPartSpriteSheet("guard_face", Path.Combine(assetsRoot, FixtureAssets.PartFace), CharacterPartType.Face);
        engine.LoadPartSpriteSheet("guard_armour", Path.Combine(assetsRoot, FixtureAssets.PartArmour), CharacterPartType.Armour);
        engine.LoadPartSpriteSheet("guard_head", Path.Combine(assetsRoot, FixtureAssets.PartHead), CharacterPartType.Head);
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_body", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_face", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_armour", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_head", CharacterIndex: 3));
        engine.Characters.Add(guard);

        return engine;
    }
}
