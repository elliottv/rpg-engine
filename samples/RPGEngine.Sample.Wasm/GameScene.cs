using RPGEngine.Sprites;
using RPGEngine.Tiled;
using SkiaSharp;

namespace RPGEngine.Sample.Wasm;

/// <summary>
/// Builds the canonical sample scene for the WebAssembly host through the file-system-free
/// (stream/URL) entry points of the engine: the map is loaded with
/// <see cref="TileMap.Load(Stream, Uri, TiledAssetFetcher)"/> and every spritesheet is loaded
/// with the <c>LoadSpriteSheet(name, stream)</c> / <c>LoadPartSpriteSheet(name, stream, type)</c>
/// overloads. This proves the engine's rendering code paths work unchanged on WebAssembly.
/// </summary>
/// <remarks>
/// The committed PNG fixtures are stored as base64 text (<c>.png.b64</c>); the asset fetcher
/// below fetches the text over HTTP and decodes it to the real PNG bytes, so the same committed
/// bytes feed both hosts.
/// </remarks>
public sealed class GameScene
{
    /// <summary>Tile size in pixels of the fixture map and the RPG Maker MZ sheets.</summary>
    public const int TileSize = 48;

    /// <summary>Player world position (on the sand path of the fixture map).</summary>
    public static readonly Position PlayerPosition = new(6 * TileSize, 6 * TileSize);

    /// <summary>First NPC world position (a villager made of body/face/hair1 parts).</summary>
    public static readonly Position VillagerPosition = new(3 * TileSize, 4 * TileSize);

    /// <summary>Second NPC world position (a guard made of body/face/armour/head parts).</summary>
    public static readonly Position GuardPosition = new(11 * TileSize, 8 * TileSize);

    private readonly GameEngine _engine;

    private GameScene(GameEngine engine) => _engine = engine;

    /// <summary>Gets the engine instance backing this scene.</summary>
    public GameEngine Engine => _engine;

    /// <summary>
    /// Loads the scene from the committed fixture assets using <paramref name="http"/> to fetch
    /// them over HTTP from <c>wwwroot/assets</c>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured with the host base address.</param>
    /// <param name="baseUrl">The URL of the <c>assets/</c> directory (e.g. <c>assets/</c>).</param>
    public static async Task<GameScene> LoadAsync(HttpClient http, string baseUrl)
    {
        // A synchronous fetcher delegate used by TileMap.Load for the external TSX and its image.
        // The underlying HttpClient calls are completed before the map is built, so there is no
        // sync-over-async on the single-threaded WebAssembly runtime.
        var mapXml = await http.GetStringAsync(baseUrl + "map.tmx").ConfigureAwait(false);
        var tilesetXml = await http.GetStringAsync(baseUrl + "tiles.tsx").ConfigureAwait(false);
        var tilesPng = DecodeBase64Png(await http.GetStringAsync(baseUrl + "tiles.png.b64").ConfigureAwait(false));

        byte[] Fetcher(Uri uri)
        {
            var name = uri.AbsolutePath.Split('/').LastOrDefault() ?? string.Empty;
            return name switch
            {
                "tiles.tsx" => System.Text.Encoding.UTF8.GetBytes(tilesetXml),
                "tiles.png" => tilesPng,
                _ => throw new FileNotFoundException($"Unexpected asset URI '{uri}'."),
            };
        }

        using var mapStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mapXml), writable: false);
        var engine = new GameEngine
        {
            Map = TileMap.Load(mapStream, new Uri("https://local/"+baseUrl+"map.tmx"), Fetcher),
        };

        // Player: a single full sheet, character slot 1.
        await LoadSpriteSheetAsync(http, baseUrl, engine, "hero", "characters/character_full.png.b64").ConfigureAwait(false);
        engine.Player.Position = PlayerPosition;
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // NPC 1 "villager": body + face + hair1 part sheets, character slot 2.
        await LoadPartSheetAsync(http, baseUrl, engine, "villager_body", "characters/character_part_body.png.b64", CharacterPartType.Body).ConfigureAwait(false);
        await LoadPartSheetAsync(http, baseUrl, engine, "villager_face", "characters/character_part_face.png.b64", CharacterPartType.Face).ConfigureAwait(false);
        await LoadPartSheetAsync(http, baseUrl, engine, "villager_hair1", "characters/character_part_hair1.png.b64", CharacterPartType.Hair1).ConfigureAwait(false);
        var villager = new Character
        {
            Position = VillagerPosition,
            BaseSpeed = 48,
        };
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_body", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_face", CharacterIndex: 2));
        villager.SpriteSheets.Add(new SpriteSheetRef("villager_hair1", CharacterIndex: 2));
        engine.Characters.Add(villager);

        // NPC 2 "guard": body + face + armour + head part sheets, character slot 3.
        await LoadPartSheetAsync(http, baseUrl, engine, "guard_body", "characters/character_part_body.png.b64", CharacterPartType.Body).ConfigureAwait(false);
        await LoadPartSheetAsync(http, baseUrl, engine, "guard_face", "characters/character_part_face.png.b64", CharacterPartType.Face).ConfigureAwait(false);
        await LoadPartSheetAsync(http, baseUrl, engine, "guard_armour", "characters/character_part_armour.png.b64", CharacterPartType.Armour).ConfigureAwait(false);
        await LoadPartSheetAsync(http, baseUrl, engine, "guard_head", "characters/character_part_head.png.b64", CharacterPartType.Head).ConfigureAwait(false);
        var guard = new Character
        {
            Position = GuardPosition,
            BaseSpeed = 48,
        };
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_body", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_face", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_armour", CharacterIndex: 3));
        guard.SpriteSheets.Add(new SpriteSheetRef("guard_head", CharacterIndex: 3));
        engine.Characters.Add(guard);

        return new GameScene(engine);
    }

    private static async Task LoadSpriteSheetAsync(HttpClient http, string baseUrl, GameEngine engine, string name, string file)
    {
        using var stream = new MemoryStream(DecodeBase64Png(await http.GetStringAsync(baseUrl + file).ConfigureAwait(false)), writable: false);
        engine.LoadSpriteSheet(name, stream);
    }

    private static async Task LoadPartSheetAsync(HttpClient http, string baseUrl, GameEngine engine, string name, string file, CharacterPartType partType)
    {
        using var stream = new MemoryStream(DecodeBase64Png(await http.GetStringAsync(baseUrl + file).ConfigureAwait(false)), writable: false);
        engine.LoadPartSpriteSheet(name, stream, partType);
    }

    private static byte[] DecodeBase64Png(string base64) => Convert.FromBase64String(base64);
}
