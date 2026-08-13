namespace RPGEngine.Tiled;

/// <summary>
/// Loads, validates and registers standalone Tiled tilesets (<c>.tsx</c>) by unique name.
/// </summary>
/// <remarks>
/// <para>
/// This is the backing store for <c>GameEngine.LoadTileSet</c>. It is independent from the
/// tilesets that a <see cref="TileMap"/> loads internally: a map owns its own tilesets and
/// never consults this registry.
/// </para>
/// <para>
/// Names are case-sensitive and trimmed on registration, lookup and duplicate checks. Loading
/// a name that is already registered throws <see cref="InvalidOperationException"/>; looking
/// up an unknown name throws <see cref="KeyNotFoundException"/>.
/// </para>
/// </remarks>
public sealed class TileSetManager
{
    private readonly Dictionary<string, TileSet> _tileSets = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads the Tiled tileset (<c>.tsx</c>) at <paramref name="path"/> and registers it under
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the tileset (trimmed).</param>
    /// <param name="path">The path to a Tiled <c>.tsx</c> tileset file.</param>
    /// <returns>The loaded and registered <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty after trimming.</exception>
    /// <exception cref="InvalidOperationException">A tileset named <paramref name="name"/> is already registered.</exception>
    public TileSet Load(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        var tileSet = TileSet.Load(path);
        _tileSets.Add(trimmedName, tileSet);
        return tileSet;
    }

    /// <summary>
    /// Loads a Tiled tileset (<c>.tsx</c>) from <paramref name="stream"/> and registers it under
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the tileset (trimmed).</param>
    /// <param name="stream">A stream containing the Tiled <c>.tsx</c> tileset content.</param>
    /// <returns>The loaded and registered <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty after trimming.</exception>
    /// <exception cref="InvalidOperationException">A tileset named <paramref name="name"/> is already registered.</exception>
    /// <remarks>
    /// Because a stream has no file-system location, a relative image <c>source</c> declared by
    /// the tileset is resolved against <see cref="Environment.CurrentDirectory"/>. The caller
    /// remains the owner of <paramref name="stream"/>.
    /// </remarks>
    public TileSet Load(string name, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        // Build a file URI for the current directory so TileSet.Load can resolve the tileset's
        // image source (relative paths resolve under the current directory, absolute paths are
        // used as-is) and read it from the local file system.
        var baseUri = new Uri(Path.GetFullPath(Environment.CurrentDirectory) + Path.DirectorySeparatorChar);
        var tileSet = TileSet.Load(stream, baseUri, uri => File.ReadAllBytes(uri.LocalPath));
        _tileSets.Add(trimmedName, tileSet);
        return tileSet;
    }

    /// <summary>
    /// Returns the tileset registered under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the tileset to look up (trimmed).</param>
    /// <returns>The registered <see cref="TileSet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty after trimming.</exception>
    /// <exception cref="KeyNotFoundException">No tileset named <paramref name="name"/> is registered.</exception>
    public TileSet Get(string name)
    {
        var trimmedName = ValidateName(name);

        if (!_tileSets.TryGetValue(trimmedName, out var tileSet))
        {
            throw new KeyNotFoundException($"No tileset named '{trimmedName}' is registered.");
        }

        return tileSet;
    }

    /// <summary>
    /// Returns whether a tileset is registered under <paramref name="name"/> (case-sensitive, trimmed).
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <returns><see langword="true"/> when a tileset with that name is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _tileSets.ContainsKey(name.Trim());
    }

    private void EnsureNameAvailable(string name)
    {
        if (_tileSets.ContainsKey(name))
        {
            throw new InvalidOperationException($"A tileset named '{name}' is already registered.");
        }
    }

    private static string ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Tileset name must not be empty after trimming.", nameof(name));
        }

        return trimmed;
    }
}
