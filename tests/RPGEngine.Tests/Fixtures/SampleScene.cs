using RPGEngine.Sprites;
using RPGEngine.Tests.Fixtures;
using RPGEngine.Tiled;

namespace RPGEngine.Tests.Fixtures;

/// <summary>
/// Builds the canonical sample scene shared by the desktop and WebAssembly sample hosts and by
/// the end-to-end tests. The scene loads the committed fixture map, a full character sheet for
/// the player and a couple of part sheets for two NPCs, so every host and the tests exercise the
/// exact same configuration.
/// </summary>
/// <remarks>
/// <para>
/// The scene layout (documented in <c>docs/Architecture.md</c> and mirrored by both samples):
/// </para>
/// <list type="bullet">
/// <item>Map: the committed 16×12 orthogonal map (48px tiles, 768×576 px world).</item>
/// <item>Player: <c>hero</c> full sheet, character index 1, at (6, 6) tiles — on the sand path.</item>
/// <item>NPC "villager": <c>body</c> + <c>face</c> + <c>hair1</c> part sheets, character index 2, at (3, 4) tiles.</item>
/// <item>NPC "guard": <c>body</c> + <c>face</c> + <c>armour</c> + <c>head</c> part sheets, character index 3, at (11, 8) tiles.</item>
/// </list>
/// </remarks>
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
    /// fixtures (see <see cref="FixtureAssets.MaterializeToTempDirectory"/>). The assets root
    /// must contain <c>map.tmx</c>, <c>tiles.tsx</c>, <c>tiles.png</c> and the decoded
    /// <c>characters/*.png</c> files.
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

        // NPC 1 "villager": body + face + hair1 part sheets, character slot 2. The NPC speed is
        // 1 tile/s (the tile-unit equivalent of the previous 48 px/s at 48px tiles).
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

        // NPC 2 "guard": body + face + armour + head part sheets, character slot 3. The NPC
        // speed is 1 tile/s (the tile-unit equivalent of the previous 48 px/s at 48px tiles).
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
