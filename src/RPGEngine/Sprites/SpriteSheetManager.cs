using SkiaSharp;

namespace RPGEngine.Sprites;

/// <summary>
/// Loads, validates and registers RPG Maker MZ spritesheets by unique name.
/// </summary>
/// <remarks>
/// <para>
/// Every loaded sheet is decoded exactly once with <see cref="SKImage.FromEncodedData(Stream)"/>
/// and kept alive for the lifetime of the sheet; <see cref="SpriteSheet.GetSprite"/> then crops
/// from that decoded image without re-decoding or re-encoding.
/// </para>
/// <para>
/// The sheet kind is never guessed from the file name. <see cref="Load(string, string)"/> and
/// <see cref="Load(string, Stream)"/> load <em>full</em> sheets; <see cref="LoadPart(string, string, CharacterPartType)"/>
/// and <see cref="LoadPart(string, Stream, CharacterPartType)"/> load <em>part</em> sheets of the
/// given layer. The kind and part type are stored on the returned <see cref="SpriteSheet"/>.
/// </para>
/// <para>
/// Names are case-sensitive and trimmed on registration, lookup and duplicate checks. Loading a
/// name that is already registered throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public sealed class SpriteSheetManager
{
    private readonly Dictionary<string, SpriteSheet> _sheets = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads the <em>full</em> character spritesheet at <paramref name="path"/> and registers it
    /// under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet (trimmed).</param>
    /// <param name="path">The path to an image file (PNG or other SkiaSharp-supported format).</param>
    /// <returns>The loaded <see cref="SpriteSheet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions are not exactly <see cref="SpriteSheet.SheetWidth"/>×<see cref="SpriteSheet.SheetHeight"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    public SpriteSheet Load(string name, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        using var stream = File.OpenRead(path);
        return Register(trimmedName, SpriteSheetType.Full, null, Decode(stream, trimmedName, path));
    }

    /// <summary>
    /// Loads the <em>full</em> character spritesheet from <paramref name="stream"/> and registers
    /// it under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet (trimmed).</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <returns>The loaded <see cref="SpriteSheet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions are not exactly <see cref="SpriteSheet.SheetWidth"/>×<see cref="SpriteSheet.SheetHeight"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>
    /// The caller remains the owner of <paramref name="stream"/>; it is not disposed here. This
    /// overload is the file-system-free entry point (e.g. WebAssembly builds where assets are
    /// fetched over HTTP).
    /// </remarks>
    public SpriteSheet Load(string name, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        return Register(trimmedName, SpriteSheetType.Full, null, Decode(stream, trimmedName, "<stream>"));
    }

    /// <summary>
    /// Loads the <em>part</em> spritesheet of layer <paramref name="partType"/> at
    /// <paramref name="path"/> and registers it under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet (trimmed).</param>
    /// <param name="path">The path to an image file (PNG or other SkiaSharp-supported format).</param>
    /// <param name="partType">The character layer the sheet provides.</param>
    /// <returns>The loaded <see cref="SpriteSheet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions are not exactly <see cref="SpriteSheet.SheetWidth"/>×<see cref="SpriteSheet.SheetHeight"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    public SpriteSheet LoadPart(string name, string path, CharacterPartType partType)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        using var stream = File.OpenRead(path);
        return Register(trimmedName, SpriteSheetType.Part, partType, Decode(stream, trimmedName, path));
    }

    /// <summary>
    /// Loads the <em>part</em> spritesheet of layer <paramref name="partType"/> from
    /// <paramref name="stream"/> and registers it under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet (trimmed).</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <param name="partType">The character layer the sheet provides.</param>
    /// <returns>The loaded <see cref="SpriteSheet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions are not exactly <see cref="SpriteSheet.SheetWidth"/>×<see cref="SpriteSheet.SheetHeight"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>
    /// The caller remains the owner of <paramref name="stream"/>; it is not disposed here.
    /// </remarks>
    public SpriteSheet LoadPart(string name, Stream stream, CharacterPartType partType)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var trimmedName = ValidateName(name);
        EnsureNameAvailable(trimmedName);

        return Register(trimmedName, SpriteSheetType.Part, partType, Decode(stream, trimmedName, "<stream>"));
    }

    /// <summary>
    /// Returns the sheet registered under <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The unique name of the sheet (trimmed).</param>
    /// <returns>The registered <see cref="SpriteSheet"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty after trimming.</exception>
    /// <exception cref="KeyNotFoundException">No sheet named <paramref name="name"/> is loaded.</exception>
    public SpriteSheet Get(string name)
    {
        var trimmedName = ValidateName(name);

        if (!_sheets.TryGetValue(trimmedName, out var sheet))
        {
            throw new KeyNotFoundException($"No spritesheet named '{trimmedName}' is loaded.");
        }

        return sheet;
    }

    /// <summary>
    /// Returns whether a sheet is registered under <paramref name="name"/> (case-sensitive, trimmed).
    /// </summary>
    /// <param name="name">The unique name of the sheet.</param>
    /// <returns><see langword="true"/> if a sheet with that name is loaded; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _sheets.ContainsKey(name.Trim());
    }

    private void EnsureNameAvailable(string name)
    {
        if (_sheets.ContainsKey(name))
        {
            throw new InvalidOperationException($"A spritesheet named '{name}' is already loaded.");
        }
    }

    private SpriteSheet Register(string name, SpriteSheetType type, CharacterPartType? partType, SKImage source)
    {
        var sheet = new SpriteSheet(name, type, partType, source);
        _sheets.Add(name, sheet);
        return sheet;
    }

    private static string ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Spritesheet name must not be empty after trimming.", nameof(name));
        }

        return trimmed;
    }

    private static SKImage Decode(Stream stream, string name, string sourceDescription)
    {
        var image = SKImage.FromEncodedData(stream)
            ?? throw new ArgumentException($"The image '{sourceDescription}' could not be decoded as a spritesheet.");

        // Capture the dimensions before any disposal: the decoded image must stay alive while
        // its native properties are read (accessing it inside the exception message after
        // Dispose() would be a native use-after-free).
        var width = image.Width;
        var height = image.Height;
        if (width != SpriteSheet.SheetWidth || height != SpriteSheet.SheetHeight)
        {
            image.Dispose();
            throw new ArgumentException(
                $"Spritesheet '{name}' must be {SpriteSheet.SheetWidth}×{SpriteSheet.SheetHeight} pixels " +
                $"(RPG Maker MZ full or part sheet), but was {width}×{height}.");
        }

        return image;
    }
}
