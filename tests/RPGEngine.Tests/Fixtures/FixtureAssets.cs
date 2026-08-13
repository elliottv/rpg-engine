using System.Text;

namespace RPGEngine.Tests.Fixtures;

/// <summary>
/// Locates and decodes the committed fixture assets that the sample hosts and the end-to-end
/// tests share. The PNG fixtures are committed as base64 text (<c>.png.b64</c>) so they can be
/// version-controlled and reviewed as text; every method here decodes them to the real PNG bytes.
/// </summary>
/// <remarks>
/// <para>
/// The fixtures live in <c>assets/</c> at the repository root and are copied into the test
/// output directory as <c>Fixtures/</c> by the test project file. This keeps the runtime
/// independent of the checkout location.
/// </para>
/// <para>
/// The committed map (<see cref="MapFile"/>) is a 16×12 orthogonal map of 48px tiles; the
/// committed sheets are all 576×384 RPG Maker MZ sheets (12×8 cells, 8 characters). See
/// <c>docs/Architecture.md</c> and <c>assets/README.md</c> for the exact layout.
/// </para>
/// </remarks>
internal static class FixtureAssets
{
    /// <summary>The Tiled map file (TMX) committed in <c>assets/</c>.</summary>
    public const string MapFile = "map.tmx";

    /// <summary>The external tileset file (TSX) referenced by <see cref="MapFile"/>.</summary>
    public const string TilesetFile = "tiles.tsx";

    /// <summary>The tileset image decoded by the TSX (relative to <see cref="TilesetFile"/>).</summary>
    public const string TilesImage = "tiles.png";

    /// <summary>The full 576×384 character sheet (all 8 character slots).</summary>
    public const string FullSheet = "characters/character_full.png";

    /// <summary>A 576×384 body part sheet.</summary>
    public const string PartBody = "characters/character_part_body.png";

    /// <summary>A 576×384 face part sheet.</summary>
    public const string PartFace = "characters/character_part_face.png";

    /// <summary>A 576×384 hair1 part sheet.</summary>
    public const string PartHair1 = "characters/character_part_hair1.png";

    /// <summary>A 576×384 hair2 part sheet.</summary>
    public const string PartHair2 = "characters/character_part_hair2.png";

    /// <summary>A 576×384 face-hair part sheet.</summary>
    public const string PartFaceHair = "characters/character_part_face_hair.png";

    /// <summary>A 576×384 armour part sheet.</summary>
    public const string PartArmour = "characters/character_part_armour.png";

    /// <summary>A 576×384 head part sheet.</summary>
    public const string PartHead = "characters/character_part_head.png";

    /// <summary>
    /// Gets the directory that contains the committed fixtures in the test output
    /// (…/bin/…/Fixtures).
    /// </summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Returns the absolute path of a committed fixture, e.g. <see cref="MapFile"/> or
    /// <see cref="PartBody"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The fixture is not present in the output directory.</exception>
    public static string FilePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fixture '{relativePath}' was not found under '{Root}'. " +
                "The assets are copied from assets/ by the test project file.", path);
        }

        return path;
    }

    /// <summary>
    /// Decodes a committed <c>.png.b64</c> fixture (e.g. <see cref="FullSheet"/>) into the raw
    /// PNG bytes.
    /// </summary>
    public static byte[] DecodePng(string relativePath)
        => Convert.FromBase64String(File.ReadAllText(FilePath(relativePath + ".b64")));

    /// <summary>Decodes a committed <c>.png.b64</c> fixture into a read-only memory stream.</summary>
    public static MemoryStream DecodePngStream(string relativePath)
        => new(DecodePng(relativePath), writable: false);

    /// <summary>
    /// Materializes every committed fixture into a fresh temporary directory with real file
    /// extensions (<c>.png</c> instead of <c>.png.b64</c>), preserving the relative layout, so
    /// the file-system based loaders (<see cref="RPGEngine.Tiled.TileMap.Load(string)"/>,
    /// <c>GameEngine.LoadSpriteSheet</c>, <c>GameEngine.LoadPartSpriteSheet</c>) can be used.
    /// </summary>
    /// <returns>A disposable handle; the caller owns and disposes the temporary directory.</returns>
    public static TempFixtureDirectory MaterializeToTempDirectory()
        => new(Root);

    /// <summary>
    /// A temporary directory containing the decoded fixtures with real file extensions.
    /// </summary>
    internal sealed class TempFixtureDirectory : IDisposable
    {
        /// <summary>Gets the root of the materialized fixture directory.</summary>
        public string Root { get; }

        internal TempFixtureDirectory(string sourceRoot)
        {
            Root = Path.Combine(Path.GetTempPath(), "rpg-engine-fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            CopyTree(sourceRoot, Root);
        }

        /// <summary>Returns the absolute path of a materialized fixture (e.g. <see cref="MapFile"/>).</summary>
        public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; a leftover temp directory is harmless.
            }
        }

        private static void CopyTree(string source, string destination)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(destination, relative);

                if (relative.EndsWith(".b64", StringComparison.Ordinal))
                {
                    // Remove the trailing .b64 so "tiles.png.b64" becomes "tiles.png".
                    target = Path.ChangeExtension(target, null);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var bytes = Convert.FromBase64String(File.ReadAllText(file));
                    File.WriteAllBytes(target, bytes);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, overwrite: true);
                }
            }
        }
    }
}
