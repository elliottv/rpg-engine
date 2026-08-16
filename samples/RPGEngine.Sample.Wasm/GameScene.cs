using RPGEngine.Sprites;
using RPGEngine.Tiled;
using SkiaSharp;

namespace RPGEngine.Sample.Wasm;

/// <summary>
/// Builds the canonical sample scene for the WebAssembly host through the **async**
/// file-system-free entry points of the engine (story 26): the map is loaded with
/// <see cref="TileMap.LoadAsync(Stream, Uri, TiledAssetFetcherAsync)"/> using a
/// <see cref="TiledAssetFetcherAsync"/>, and every spritesheet is loaded with
/// <see cref="GameEngine.LoadSpriteSheetAsync"/> / <see cref="GameEngine.LoadPartSpriteSheetAsync"/>.
/// This proves the WebAssembly host can use the async loading API (which never performs a
/// blocking synchronous read of the caller's stream) and that the rendering code paths work
/// unchanged on WebAssembly.
/// </summary>
/// <remarks>
/// The committed PNG fixtures are stored as base64 text (<c>.png.b64</c>); the asset fetcher
/// below fetches the text over HTTP and decodes it to the real PNG bytes, so the same committed
/// bytes feed both hosts. The HTTP fetches are awaited end-to-end, so nothing blocks the
/// single-threaded WebAssembly runtime.
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
    /// them over HTTP from <c>wwwroot/assets</c>, exclusively through the engine's async loading
    /// entry points (<c>TileMap.LoadAsync</c>, <c>LoadSpriteSheetAsync</c> and
    /// <c>LoadPartSpriteSheetAsync</c>).
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> configured with the host base address.</param>
    /// <param name="baseUrl">The URL of the <c>assets/</c> directory (e.g. <c>assets/</c>).</param>
    public static async Task<GameScene> LoadAsync(HttpClient http, string baseUrl)
    {
        // An async fetcher delegate used by TileMap.LoadAsync for the external TSX and its image.
        // Every fetch is awaited, so no sync-over-async occurs on the WebAssembly runtime.
        TiledAssetFetcherAsync fetcher = async uri =>
        {
            var name = uri.AbsolutePath.Split('/').LastOrDefault() ?? string.Empty;
            return name switch
            {
                "tiles.tsx" => System.Text.Encoding.UTF8.GetBytes(await http.GetStringAsync(baseUrl + "tiles.tsx").ConfigureAwait(false)),
                "tiles.png" => DecodeBase64Png(await http.GetStringAsync(baseUrl + "tiles.png.b64").ConfigureAwait(false)),
                _ => throw new FileNotFoundException($"Unexpected asset URI '{uri}'."),
            };
        };

        using var mapStream = new MemoryStream(
            await http.GetByteArrayAsync(baseUrl + "map.tmx").ConfigureAwait(false),
            writable: false);

        var engine = new GameEngine
        {
            Map = await TileMap.LoadAsync(
                mapStream,
                new Uri("https://local/" + baseUrl + "map.tmx"),
                fetcher).ConfigureAwait(false),
        };

        // Player: a single full sheet, character slot 1.
        await engine.LoadSpriteSheetAsync("hero", await FetchPngStreamAsync(http, baseUrl, "characters/character_full.png.b64").ConfigureAwait(false)).ConfigureAwait(false);
        engine.Player.Position = PlayerPosition;
        engine.Player.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));

        // NPC 1 "villager": body + face + hair1 part sheets, character slot 2.
        await engine.LoadPartSpriteSheetAsync("villager_body", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_body.png.b64").ConfigureAwait(false), CharacterPartType.Body).ConfigureAwait(false);
        await engine.LoadPartSpriteSheetAsync("villager_face", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_face.png.b64").ConfigureAwait(false), CharacterPartType.Face).ConfigureAwait(false);
        await engine.LoadPartSpriteSheetAsync("villager_hair1", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_hair1.png.b64").ConfigureAwait(false), CharacterPartType.Hair1).ConfigureAwait(false);
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
        await engine.LoadPartSpriteSheetAsync("guard_body", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_body.png.b64").ConfigureAwait(false), CharacterPartType.Body).ConfigureAwait(false);
        await engine.LoadPartSpriteSheetAsync("guard_face", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_face.png.b64").ConfigureAwait(false), CharacterPartType.Face).ConfigureAwait(false);
        await engine.LoadPartSpriteSheetAsync("guard_armour", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_armour.png.b64").ConfigureAwait(false), CharacterPartType.Armour).ConfigureAwait(false);
        await engine.LoadPartSpriteSheetAsync("guard_head", await FetchPngStreamAsync(http, baseUrl, "characters/character_part_head.png.b64").ConfigureAwait(false), CharacterPartType.Head).ConfigureAwait(false);
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

    /// <summary>
    /// Fetches a committed <c>.png.b64</c> fixture over HTTP and returns a read-only memory
    /// stream of the decoded PNG bytes for the engine's stream-based loaders.
    /// </summary>
    private static async Task<MemoryStream> FetchPngStreamAsync(HttpClient http, string baseUrl, string file)
    {
        var pngBytes = DecodeBase64Png(await http.GetStringAsync(baseUrl + file).ConfigureAwait(false));
        return new MemoryStream(pngBytes, writable: false);
    }

    private static byte[] DecodeBase64Png(string base64) => Convert.FromBase64String(base64);
}
