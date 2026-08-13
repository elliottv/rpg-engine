using System.IO;
namespace RPGEngine.Sample.Desktop;

/// <summary>
/// Locates and materializes the committed fixture assets that the sample scene uses. The PNG
/// fixtures are committed as base64 text (<c>.png.b64</c>); this helper decodes them into real
/// <c>.png</c> files inside a temporary directory so the file-system based loaders
/// (<c>GameEngine.LoadSpriteSheet</c>, <c>GameEngine.LoadPartSpriteSheet</c> and
/// <c>TileMap.Load</c>) can be used by the desktop host.
/// </summary>
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

    /// <summary>A 576×384 armour part sheet.</summary>
    public const string PartArmour = "characters/character_part_armour.png";

    /// <summary>A 576×384 head part sheet.</summary>
    public const string PartHead = "characters/character_part_head.png";

    /// <summary>
    /// Materializes every committed fixture into a fresh temporary directory with real file
    /// extensions (<c>.png</c> instead of <c>.png.b64</c>), preserving the relative layout.
    /// </summary>
    /// <returns>A disposable handle; the caller owns and disposes the temporary directory.</returns>
    public static TempFixtureDirectory MaterializeToTempDirectory()
        => new(FindSourceRoot());

    /// <summary>Finds the repository <c>assets/</c> directory copied into the output folder.</summary>
    private static string FindSourceRoot()
    {
        // The csproj copies assets/ into the output directory under "Assets/".
        var outputAssets = Path.Combine(AppContext.BaseDirectory, "Assets");
        if (Directory.Exists(outputAssets))
        {
            return outputAssets;
        }

        throw new DirectoryNotFoundException(
            $"The committed fixture assets were not found under '{outputAssets}'. " +
            "The sample copies assets/ into its output; re-build the project.");
    }

    /// <summary>
    /// A temporary directory containing the decoded fixtures with real file extensions.
    /// </summary>
    internal sealed class TempFixtureDirectory : IDisposable
    {
        /// <summary>Gets the root of the materialized fixture directory.</summary>
        public string Root { get; }

        internal TempFixtureDirectory(string sourceRoot)
        {
            Root = Path.Combine(Path.GetTempPath(), "rpg-engine-sample-assets-" + Guid.NewGuid().ToString("N"));
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
