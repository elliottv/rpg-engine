using DotTiled;
using DotTiled.Serialization;

namespace RPGEngine.Tiled;

/// <summary>
/// A registry of named <see cref="TileSet"/>s. This is the backing store for
/// <c>GameEngine.LoadTileSet</c> and is independent from the tilesets that a
/// <see cref="TileMap"/> loads internally.
/// </summary>
/// <remarks>
/// Each tileset is registered under a unique name. Registering a duplicate name throws
/// <see cref="InvalidOperationException"/>; looking up an unknown name throws
/// <see cref="KeyNotFoundException"/>.
/// </remarks>
public sealed class TileSetManager
{
    private readonly Dictionary<string, TileSet> _tileSets = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads the Tiled tileset (<c>.tsx</c>) at <paramref name="path"/> and registers it under
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name to register the tileset under.</param>
    /// <param name="path">The path to a Tiled <c>.tsx</c> tileset file.</param>
    /// <returns>The loaded and registered <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A tileset named <paramref name="name"/> is already registered.</exception>
    public TileSet Load(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        var dotTiledTileset = Loader.Default().LoadTileset(fullPath);

        var tileSet = TileSet.FromDotTiled(dotTiledTileset, dotTiledTileset.FirstGID.GetValueOr(0u), baseDirectory);
        return Register(name, tileSet);
    }

    /// <summary>
    /// Loads a Tiled tileset (<c>.tsx</c>) from <paramref name="stream"/> and registers it under
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name to register the tileset under.</param>
    /// <param name="stream">A stream containing Tiled <c>.tsx</c> tileset content.</param>
    /// <returns>The loaded and registered <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A tileset named <paramref name="name"/> is already registered.</exception>
    /// <remarks>
    /// Because a stream has no file-system location, a relative image <c>source</c> declared by
    /// the tileset is resolved against <see cref="Environment.CurrentDirectory"/>. The caller
    /// remains the owner of <paramref name="stream"/>.
    /// </remarks>
    public TileSet Load(string name, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();
        var dotTiledTileset = ParseTilesetContent(content);

        var tileSet = TileSet.FromDotTiled(
            dotTiledTileset,
            dotTiledTileset.FirstGID.GetValueOr(0u),
            Environment.CurrentDirectory);
        return Register(name, tileSet);
    }

    /// <summary>
    /// Returns the tileset registered under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the tileset to look up.</param>
    /// <returns>The registered <see cref="TileSet"/>.</returns>
    /// <exception cref="KeyNotFoundException">No tileset is registered under <paramref name="name"/>.</exception>
    public TileSet Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_tileSets.TryGetValue(name, out var tileSet))
        {
            throw new KeyNotFoundException($"No tileset named '{name}' is registered.");
        }

        return tileSet;
    }

    /// <summary>
    /// Returns whether a tileset is registered under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <returns><see langword="true"/> when a tileset with that name is registered; otherwise <see langword="false"/>.</returns>
    public bool Contains(string name) =>
        name is not null && _tileSets.ContainsKey(name);

    private TileSet Register(string name, TileSet tileSet)
    {
        if (!_tileSets.TryAdd(name, tileSet))
        {
            throw new InvalidOperationException($"A tileset named '{name}' is already registered.");
        }

        return tileSet;
    }

    private static Tileset ParseTilesetContent(string content)
    {
        using var reader = new TilesetReader(
            content,
            externalTilesetResolver: source => throw new NotSupportedException(
                $"External tileset '{source}' is not supported when loading a tileset from a stream."),
            externalTemplateResolver: source => throw new NotSupportedException(
                $"External template '{source}' is not supported when loading a tileset from a stream."),
            customTypeResolver: _ => Optional.Empty);
        return reader.ReadTileset();
    }
}
