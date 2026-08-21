using RPGEngine.Sprites;
using RPGEngine.Tiled;
using SkiaSharp;

namespace RPGEngine;

/// <summary>
/// The root of the engine. It owns the game state (player, characters, map and configuration),
/// the spritesheet registry and the pressed-keys state, and exposes the game-loop entry points
/// <see cref="Update"/>, <see cref="Render"/>, <see cref="Input"/> and the asset-loading methods
/// used by the host application.
/// </summary>
/// <remarks>
/// <para>
/// The game loop is written by the host: each frame the host calls <see cref="Update"/> with its
/// own elapsed time (<c>dt</c>, in seconds) to advance the simulation, then <see cref="Render"/>
/// with the same <c>dt</c> to draw the frame onto its canvas. The engine never runs its own loop
/// and never blocks.
/// </para>
/// <para>
/// Rendering issues SkiaSharp canvas/image drawing operations only; it never rasterizes the
/// final output to a CPU bitmap. When the host passes a GPU-backed <see cref="SKCanvas"/> (e.g.
/// the surface of a SkiaSharp GL view or a WebAssembly <c>SKSurface</c> created from a
/// <c>GRContext</c>), the drawing is hardware accelerated. The engine does not depend on any
/// CPU-only API for the visible output.
/// </para>
/// <para>
/// The camera is internal to the engine: <see cref="Render"/> follows the player and clamps the
/// viewport inside the map. When the map is smaller than the canvas on an axis it is centered
/// in that axis and the area around it is filled with black (the map background), so the map is
/// never letterboxed with transparent or leftover pixels. If a public camera API becomes
/// necessary later it can be extracted without breaking this class's API.
/// </para>
/// <para>
/// When a map is set, <see cref="Render"/> clears the whole canvas to black first, then draws
/// the map's below-player layers, then every NPC, then the player, and finally the map's
/// <c>above_player</c> layers (tile layers declaring the Tiled <c>above_player</c> custom
/// property) so those tiles appear on top of the player. Without a map the canvas is left
/// untouched and only the characters are drawn.
/// </para>
/// <para>
/// Movement input combines every held bound key into a single 8-direction vector: each key that
/// is bound to a movement direction contributes its unit delta, opposite keys cancel
/// (<c>W</c>+<c>S</c> or <c>A</c>+<c>D</c>), and the resulting direction can be diagonal
/// (<c>W</c>+<c>D</c> resolves to up-right). When no bound key is held the player stops and its
/// animation snaps back to the standing frame.
/// </para>
/// <para>
/// The engine is <see cref="IDisposable"/>: it owns the assigned map and disposes it when
/// <see cref="Map"/> is replaced or when the engine itself is disposed (a <see cref="TileMap"/>
/// is disposable because it prerenders each tile layer into an <see cref="SKImage"/> on load).
/// </para>
/// <para>
/// Tile sets are not loaded through the engine: a <see cref="TileMap"/> owns the tilesets that
/// its layers reference (they are created when the map is loaded). Standalone tilesets can be
/// loaded directly through the <c>TileSet.Load</c> factories in <c>RPGEngine.Tiled</c>.
/// </para>
/// </remarks>
public sealed class GameEngine : IDisposable
{
    private readonly SpriteSheetManager _spriteSheetManager = new();
    private readonly List<Character> _characters = [];
    private readonly HashSet<Key> _pressedKeys = [];
    private TileMap? _map;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEngine"/> class with default state: a
    /// fresh <see cref="Player"/>, an empty <see cref="Characters"/> list, a
    /// <see cref="GameConfig"/> with the default WASD bindings, no map and an empty spritesheet
    /// registry.
    /// </summary>
    public GameEngine()
    {
        Player = new Player();
        Config = new GameConfig();
    }

    /// <summary>Gets the player character. The camera always follows the player.</summary>
    public Player Player { get; }

    /// <summary>
    /// Gets the mutable list of NPC characters present in the game world. The player is never in
    /// this list (it is rendered separately, on top). Items added to or removed from the list are
    /// taken into account on the next <see cref="Render"/>.
    /// </summary>
    public IList<Character> Characters => _characters;

    /// <summary>
    /// Gets or sets the tile map to be displayed, or <see langword="null"/> when no map is loaded.
    /// When the value is changed, the next <see cref="Render"/> uses the new map immediately.
    /// </summary>
    /// <remarks>
    /// The engine owns the assigned map: replacing the value disposes the previous map (a
    /// <see cref="TileMap"/> is <see cref="IDisposable"/>), and <see cref="Dispose"/> releases
    /// the current map. Hosts that load maps directly and never assign them through this property
    /// are responsible for disposing those maps themselves.
    /// </remarks>
    public TileMap? Map
    {
        get => _map;
        set
        {
            if (!ReferenceEquals(_map, value))
            {
                _map?.Dispose();
                _map = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the configuration values used by the engine. The engine reads the
    /// configuration at input time and never caches a snapshot, so updates take effect
    /// immediately.
    /// </summary>
    public GameConfig Config { get; set; }

    /// <summary>
    /// Reports a key event to the engine. A value of <see langword="true"/> for
    /// <paramref name="isPressed"/> presses the key (key-down); <see langword="false"/> releases
    /// it (key-up). The engine keeps a set of currently pressed keys; the movement direction is
    /// derived from that set in <see cref="Update"/> via <see cref="GameConfig"/>.
    /// </summary>
    /// <param name="key">The framework-agnostic key that was pressed or released.</param>
    /// <param name="isPressed"><see langword="true"/> for a key-down event, <see langword="false"/> for a key-up event.</param>
    /// <remarks>
    /// Host applications are responsible for translating their framework's key events
    /// (WPF/Avalonia <c>KeyEventArgs</c>, Blazor <c>KeyboardEventArgs</c>) to a <see cref="Key"/>
    /// value before calling this method; see the <see cref="Key"/> documentation.
    /// </remarks>
    public void Input(Key key, bool isPressed)
    {
        if (isPressed)
        {
            _pressedKeys.Add(key);
        }
        else
        {
            _pressedKeys.Remove(key);
        }
    }

    /// <summary>
    /// Advances the simulation by <paramref name="dt"/> seconds: resolves the movement direction
    /// from the currently pressed keys, moves the player, clamps it inside the map, and advances
    /// the walk-cycle animation of the player and every NPC.
    /// </summary>
    /// <param name="dt">The elapsed time in seconds since the previous frame.</param>
    public void Update(double dt)
    {
        var direction = Config.GetMovementDirection(_pressedKeys);
        if (direction.HasValue)
        {
            Player.Move(direction.Value, speedFactor: 1, dt);
        }

        if (Map is not null)
        {
            ClampPlayerToMap();
        }

        Player.Character.Update(dt);
        foreach (var character in _characters)
        {
            character.Update(dt);
        }
    }

    /// <summary>
    /// Draws one frame onto <paramref name="canvas"/>. When a map is set the canvas is cleared to
    /// black first (the black background behind/around a map smaller than the canvas), then the
    /// map's below-player layers are drawn, then every NPC, then the player on top, and finally
    /// the map's <c>above_player</c> layers so those tiles appear above the player. The camera
    /// follows the player, centers a map smaller than the canvas, and is clamped so the viewport
    /// stays inside the map; the canvas size is read from the canvas clip bounds so the view
    /// adapts to the current surface size automatically.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto (CPU or GPU backed; see the class remarks).</param>
    /// <param name="dt">The elapsed time in seconds since the previous frame (reserved for future animation timing).</param>
    public void Render(SKCanvas canvas, double dt)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var bounds = canvas.LocalClipBounds;
        var canvasWidth = Math.Max(0, (int)Math.Ceiling(bounds.Width));
        var canvasHeight = Math.Max(0, (int)Math.Ceiling(bounds.Height));

        var origin = ComputeCameraOrigin(canvasWidth, canvasHeight);
        var viewport = new SKRect(
            (float)origin.X,
            (float)origin.Y,
            (float)(origin.X + canvasWidth),
            (float)(origin.Y + canvasHeight));

        // When a map is set the whole canvas is cleared to black first: this is the black
        // background behind/around a map that is smaller than the canvas. Without a map the
        // canvas is left untouched (characters only), so hosts keep full control of the backdrop.
        if (Map is not null)
        {
            canvas.Clear(SKColors.Black);
        }

        // Draw everything in world coordinates and let the translate apply the camera: the map
        // draws its tiles at world positions, and each character is drawn at its world position.
        // Draw order is: below-player map layers -> each NPC -> the player -> above-player map
        // layers, so tiles marked with the Tiled above_player property appear on top of the player.
        canvas.Save();
        try
        {
            canvas.Translate((float)-origin.X, (float)-origin.Y);

            if (Map is not null)
            {
                Map.Draw(canvas, viewport);
            }

            foreach (var character in _characters)
            {
                character.Draw(canvas, character.Position, dt, _spriteSheetManager);
            }

            Player.Character.Draw(canvas, Player.Position, dt, _spriteSheetManager);

            if (Map is not null)
            {
                Map.DrawAbovePlayer(canvas, viewport);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Releases the resources owned by the engine: the current map (if any) is disposed, which
    /// releases its prerendered layer images. Replacing the map through <see cref="Map"/> already
    /// disposes the previous map, so hosts only need to call this when the engine itself is being
    /// torn down. This method is safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        // Assigning null through the setter disposes the current map and clears the reference.
        Map = null;
    }

    /// <summary>
    /// Loads a full character spritesheet from <paramref name="path"/> and registers it under
    /// <paramref name="name"/> so characters can reference it by name (see
    /// <see cref="Character.SpriteSheets"/>).
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="path">The path to an image file (PNG or other SkiaSharp-supported format).</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    public void LoadSpriteSheet(string name, string path) => _spriteSheetManager.Load(name, path);

    /// <summary>
    /// Loads a full character spritesheet from <paramref name="stream"/> and registers it under
    /// <paramref name="name"/> so characters can reference it by name. This is the
    /// file-system-free entry point (e.g. WebAssembly builds where assets are fetched over HTTP).
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>The caller remains the owner of <paramref name="stream"/>; it is not disposed here.</remarks>
    public void LoadSpriteSheet(string name, Stream stream) => _spriteSheetManager.Load(name, stream);

    /// <summary>
    /// Loads a <em>part</em> character spritesheet of layer <paramref name="partType"/> from
    /// <paramref name="path"/> and registers it under <paramref name="name"/> so characters can
    /// reference it by name (see <see cref="Character.SpriteSheets"/>). Part sheets are composed
    /// in the fixed RPG Maker MZ order described by the character compositor; see
    /// <c>CharacterSpriteCompositor</c> and the <c>docs/Architecture.md</c> ordering table.
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="path">The path to an image file (PNG or other SkiaSharp-supported format).</param>
    /// <param name="partType">The character layer the sheet provides.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>
    /// This is the <see cref="SpriteSheetManager.LoadPart(string, string, CharacterPartType)"/> entry
    /// point of the engine: the part sheet becomes visible to every character configured with a
    /// <see cref="SpriteSheetRef"/> pointing at <paramref name="name"/>.
    /// </remarks>
    public void LoadPartSpriteSheet(string name, string path, CharacterPartType partType)
        => _spriteSheetManager.LoadPart(name, path, partType);

    /// <summary>
    /// Loads a <em>part</em> character spritesheet of layer <paramref name="partType"/> from
    /// <paramref name="stream"/> and registers it under <paramref name="name"/>. This is the
    /// file-system-free entry point (e.g. WebAssembly builds where assets are fetched over HTTP).
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <param name="partType">The character layer the sheet provides.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>The caller remains the owner of <paramref name="stream"/>; it is not disposed here.</remarks>
    public void LoadPartSpriteSheet(string name, Stream stream, CharacterPartType partType)
        => _spriteSheetManager.LoadPart(name, stream, partType);

    /// <summary>
    /// Asynchronously loads a full character spritesheet from <paramref name="stream"/> and
    /// registers it under <paramref name="name"/> so characters can reference it by name. This
    /// is the asynchronous counterpart of <see cref="LoadSpriteSheet(string, Stream)"/> for
    /// streams that only support asynchronous reads (e.g. certain network/browser streams).
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <returns>A task that completes when the sheet is loaded and registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>The caller remains the owner of <paramref name="stream"/>; it is not disposed here.</remarks>
    public Task LoadSpriteSheetAsync(string name, Stream stream)
        => _spriteSheetManager.LoadAsync(name, stream);

    /// <summary>
    /// Asynchronously loads a <em>part</em> character spritesheet of layer
    /// <paramref name="partType"/> from <paramref name="stream"/> and registers it under
    /// <paramref name="name"/> so characters can reference it by name. This is the asynchronous
    /// counterpart of <see cref="LoadPartSpriteSheet(string, Stream, CharacterPartType)"/> for
    /// streams that only support asynchronous reads (e.g. certain network/browser streams).
    /// </summary>
    /// <param name="name">The unique name used to reference the sheet.</param>
    /// <param name="stream">A stream containing the encoded image (PNG or other SkiaSharp-supported format).</param>
    /// <param name="partType">The character layer the sheet provides.</param>
    /// <returns>A task that completes when the sheet is loaded and registered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty after trimming, the image cannot be decoded, or its
    /// dimensions do not form a valid 12×8 grid (positive width divisible by <see cref="SpriteSheet.Columns"/> and positive height divisible by <see cref="SpriteSheet.Rows"/>).
    /// </exception>
    /// <exception cref="InvalidOperationException">A sheet named <paramref name="name"/> is already loaded.</exception>
    /// <remarks>The caller remains the owner of <paramref name="stream"/>; it is not disposed here.</remarks>
    public Task LoadPartSpriteSheetAsync(string name, Stream stream, CharacterPartType partType)
        => _spriteSheetManager.LoadPartAsync(name, stream, partType);

    /// <summary>
    /// Computes the camera origin: the world pixel position that maps to the canvas' top-left
    /// corner. The desired origin centers the player; it is then clamped so the viewport stays
    /// inside the map (<c>origin ∈ [0, max(0, PixelSize - canvasSize)]</c> per axis). When the
    /// map is smaller than the canvas on an axis, half the difference is subtracted so the map is
    /// centered and the origin becomes negative (<c>origin = clamp(desired, 0, max) - max(0,
    /// (canvasSize - PixelSize) / 2)</c> per axis). When no map is set the origin is <c>(0, 0)</c>.
    /// </summary>
    /// <param name="canvasWidth">The width of the canvas in pixels.</param>
    /// <param name="canvasHeight">The height of the canvas in pixels.</param>
    /// <returns>The camera origin in world pixel coordinates.</returns>
    /// <remarks>
    /// Internal so the test project can assert the camera contract directly; the camera is not
    /// part of the public API of this story.
    /// </remarks>
    internal Position ComputeCameraOrigin(int canvasWidth, int canvasHeight)
    {
        if (Map is null)
        {
            return new Position(0, 0);
        }

        // The maximum viewport origin keeps the view inside the map: 0 when the map is smaller
        // than the canvas on an axis (the map cannot scroll on that axis), otherwise
        // PixelSize - canvasSize.
        var maxX = Math.Max(0, Map.PixelWidth - canvasWidth);
        var maxY = Math.Max(0, Map.PixelHeight - canvasHeight);

        // When the map is smaller than the canvas on an axis it is centered by shifting the
        // origin by half the difference, producing a negative origin. When the map fills (or
        // exceeds) the canvas the offset is 0 and the behavior is exactly the follow + clamp
        // described above.
        var offsetX = Math.Max(0, (canvasWidth - Map.PixelWidth) / 2.0);
        var offsetY = Math.Max(0, (canvasHeight - Map.PixelHeight) / 2.0);

        var desiredX = Player.Position.X - (canvasWidth / 2.0);
        var desiredY = Player.Position.Y - (canvasHeight / 2.0);

        return new Position(
            Math.Clamp(desiredX, 0, maxX) - offsetX,
            Math.Clamp(desiredY, 0, maxY) - offsetY);
    }

    /// <summary>
    /// Clamps the player's top-left position so its sprite stays inside the map bounds. The
    /// sprite size is resolved from the player's configured spritesheet (see
    /// <see cref="Character.GetSpriteSize"/>); when no sheet is configured it falls back to the
    /// 48×48 default. <c>x ∈ [0, PixelWidth - spriteWidth]</c>, <c>y ∈ [0, PixelHeight - spriteHeight]</c>.
    /// Called after every move while a map is set.
    /// </summary>
    private void ClampPlayerToMap()
    {
        var (spriteWidth, spriteHeight) = Player.Character.GetSpriteSize(_spriteSheetManager);
        var maxX = Math.Max(0, Map!.PixelWidth - spriteWidth);
        var maxY = Math.Max(0, Map!.PixelHeight - spriteHeight);

        var position = Player.Position;
        Player.Position = new Position(
            Math.Clamp(position.X, 0, maxX),
            Math.Clamp(position.Y, 0, maxY));
    }
}
