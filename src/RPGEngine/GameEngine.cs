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
/// All world coordinates (<see cref="Player.Position"/>, <see cref="Character.Position"/>,
/// camera origins and <see cref="SurfaceToWorld"/>/<see cref="WorldToSurface"/>) are expressed
/// in <em>tiles</em> (double). Pixels are produced only at the canvas boundary: <see cref="Render"/>
/// multiplies the tile positions by the map's tile size to place sprites and the camera viewport,
/// and the tile size of a map is read from <c>TileMap.TileWidth</c> (48 when no map is set).
/// </para>
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
/// never letterboxed with transparent or leftover pixels. <see cref="SurfaceToWorld"/> and
/// <see cref="WorldToSurface"/> expose the same follow + clamp camera for the given canvas size,
/// translating host-surface (canvas) coordinates to world coordinates and back — the foundation
/// the "click to move" and "GUI around game objects" features will build on.
/// </para>
/// <para>
/// When a map is set, <see cref="Render"/> clears the whole canvas to black first, then draws
/// the map's below-player layers, then all characters (the NPCs in <see cref="Characters"/> and
/// the player) sorted by <see cref="Character.Position"/> Y ascending, and finally the map's
/// <c>above_player</c> layers (tile layers declaring the Tiled <c>above_player</c> custom
/// property) so those tiles appear on top of every character. Within the character pass a
/// character with a higher Y (lower on the screen, closer to the viewer) is drawn last and
/// appears on top of the others, so the player may be drawn behind an NPC whose Y is higher.
/// Without a map the canvas is left untouched and only the characters (Y-sorted) are drawn.
/// </para>
/// <para>
/// Movement input combines every held bound key into a single 8-direction vector: each key that
/// is bound to a movement direction contributes its unit delta, opposite keys cancel
/// (<c>W</c>+<c>S</c> or <c>A</c>+<c>D</c>), and the resulting direction can be diagonal
/// (<c>W</c>+<c>D</c> resolves to up-right). When no bound key is held the player stops and its
/// animation snaps back to the standing frame.
/// </para>
/// <para>
/// Click input drives the player with <em>auto-walk</em>: <see cref="Click"/> converts a
/// host-surface click on the main canvas (using the canvas size recorded by the most recent
/// <see cref="Render"/> call) to a world position, computes an A* tile path from the player's
/// tile to the clicked tile over the non-solid tiles (see <c>AStarPathfinder</c>), and queues
/// those tiles. Each <see cref="Update"/> then moves the player toward the center of the next
/// waypoint at <see cref="Player.DefaultBaseSpeed"/>, popping waypoints as they are reached and
/// calling <see cref="Player.Stop"/> when the path completes. Clicking a solid tile or an
/// unreachable target cancels the walk without moving; a click that yields a path <em>replaces</em>
/// the current walk even mid-walk.
/// </para>
/// <para>
/// Input precedence during auto-walk: a <strong>key press</strong> (<c>isPressed == true</c> in
/// <see cref="Input"/>) cancels the auto-walk path, a key <strong>release</strong> does not,
/// and while a bound movement key is held the auto-walk does not advance (manual key movement
/// takes priority). A <see cref="Click"/> always replaces the path unless the new target is
/// invalid (solid / no path), in which case it cancels the walk.
/// </para>
/// <para>
/// When a map is set, every character's displacement is resolved with <em>axis-separated
/// movement and per-axis slide-to-boundary clamping</em> against the map's solid tiles: each
/// axis is applied in turn and, when a character's collision footprint would overlap a solid
/// tile or leave the map (the map edge is solid), it slides to the <em>closest legal position on
/// that axis</em>, so the leading edge of the footprint stops <em>exactly</em> at the near edge
/// of the first blocking solid tile (or at the map edge). The footprint is a <em>fixed
/// 0.5×0.5-tile (24×24 px at 48 px tiles) box representing the lower body of the
/// character sprite</em>, anchored at the feet (<see cref="Player.Position"/> is the
/// middle-bottom of the sprite; the middle of the feet sits at the bottom-centre of the box,
/// <c>(12, 24)</c> when the box's origin is its upper-left). The box is independent of the
/// rendered sprite size, so a 1-tile-wide corridor always fits regardless of the spritesheet's
/// cell size, and the feet always stop exactly at the solid tile's edge whether the tile is
/// below, above or beside the character. The player's key-driven and auto-walk movement and
/// every character's autonomous movement (the <c>StartMoving</c>/<c>Update</c> path, e.g. NPCs
/// in <see cref="Characters"/>) are all resolved with the same shared resolver, so NPCs stop at
/// solid tiles and at the map edge instead of walking through the world. Clamping keeps that
/// 0.5×0.5 box inside the map for the player (the player-only post-move safety net). This
/// blocks characters at solid tiles and prevents diagonal corner-cutting; a <em>cardinal</em> move slides a blocked axis to the exact boundary (the feet
/// stop exactly at the solid tile's edge, matching click-to-move ("colliding with its feet"),
/// with no one-frame-step gap and no floating-point overshoot accumulation), while a
/// <em>diagonal</em> move is <em>all-or-nothing</em>: it is applied only when the full
/// displacement is clear on both axes, so a diagonal into a wall where only one axis is free
/// stops the player entirely instead of sliding along the free axis. The resolver re-validates
/// the resulting footprint as a safety net and
/// refuses a displacement that would still overlap a solid tile (only possible when the starting
/// footprint was already illegal, e.g. embedded in a wall), so key movement never moves the
/// player through a solid tile. The auto-walk (see <see cref="Click(double, double)"/>) resolves
/// each displacement the same way, so it never crosses a solid corner either: a blocked auto-walk
/// step cancels the walk instead of moving the player into the wall. When a move
/// is <em>fully blocked</em> (no net displacement after the axis-separated resolution, e.g.
/// walking straight into a wall, into a corner, or a diagonal whose single free axis is also
/// stopped), the engine reports a <em>collision stop</em>
/// to the player (<see cref="Player.ReportBlockedMove(Direction)"/>), so <see cref="Player.OnStopMoving"/>
/// fires even while the movement key is held against the wall. A key move that starts from idle
/// fires <see cref="Player.OnStartMoving"/> <em>before</em> the displacement is applied, and a
/// fully blocked move fires <see cref="Player.OnStopMoving"/> right after (start then stop in
/// the same frame when the very first move is blocked); while the key stays held against the same
/// wall nothing more fires. <see cref="Player.OnStartMoving"/> also fires when the movement
/// direction changes while the player is already moving (e.g. pressing a second key makes the
/// effective direction a diagonal), so remote clients that mirror the player learn the new facing
/// direction. See <c>docs/Architecture.md</c> for the collision model.
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
/// <para>
/// A minimap can be rendered on a separate surface with <see cref="RenderMinimap"/>: it draws
/// the map's prerendered tile layers (both below- and above-player layers, in file order
/// bottom → top, so it shows the full picture), a green dot for the player and a yellow dot
/// for each NPC in <see cref="Characters"/>. A <c>zoomLevel</c> of <c>1.0</c> fits the whole
/// map into the minimap canvas; values above <c>1</c> zoom in and the view pans around the
/// player's dot, clamped to the map edges like the main camera; values between <c>0</c> and
/// <c>1</c> zoom out further. When a map is set the minimap clears its canvas to black first
/// (like <see cref="Render"/>), then draws the map and the dots on top, so the unused margins
/// are black.
/// </para>
/// </remarks>
public sealed class GameEngine : IDisposable
{
    private readonly SpriteSheetManager _spriteSheetManager = new();
    private readonly List<Character> _characters = [];
    private readonly HashSet<Key> _pressedKeys = [];
    private TileMap? _map;

    // The queue of target tile waypoints the auto-walk is following (the A* path produced by
    // Click). The head of the queue is the tile the player is currently walking toward; each
    // tile is popped when the player reaches its center. A key press or an invalid click clears
    // the queue (see the input-precedence rules in the class remarks).
    private readonly Queue<(int X, int Y)> _autoWalkPath = new();

    // Whether Player.OnStartMoving has already been raised for the current auto-walk step (the
    // current waypoint leg). ReportAutoWalkStep fires once per step boundary; this flag prevents
    // it from firing on the intermediate frames between two waypoints. Reset when a new path is
    // set, when the walk is cancelled, or when the walk completes/stops.
    private bool _autoWalkStepStarted;

    // The direction of the last key move that ended in a collision stop (a fully blocked move).
    // While it matches the direction the engine keeps resolving, the player is resting idle
    // against that wall, so OnStartMoving is not re-reported on every frame; a direction change
    // (or a fresh start after the player stopped) clears it so the new attempt is reported.
    private Direction? _blockedMoveDirection;

    // The canvas size (in pixels) of the most recent Render call, stored so the future
    // click-to-move story can translate host-surface coordinates with the same camera without
    // further API churn.
    private double _lastCanvasWidth;
    private double _lastCanvasHeight;

    // The radius in canvas pixels of each minimap dot (the green player dot and the yellow NPC
    // dots). Dots are small markers on top of the minimap map, not full sprites.
    private const float MinimapDotRadius = 3f;

    // The character collision footprint is defined once on MovementCollisionResolver (the
    // footprint authority): the fixed 0.5x0.5-tile (24x24 px at 48 px tiles) lower-body box
    // anchored at the feet. Player.Position (the middle-bottom of the sprite) is the middle of
    // the feet at the bottom-centre of the box: the box spans x in [pos.X - 0.25, pos.X + 0.25]
    // and y in [pos.Y - 0.5, pos.Y] in tiles. The box is independent of the rendered sprite
    // size, so a 1-tile-wide corridor always fits regardless of the spritesheet's cell size, and
    // the feet always stop exactly at the solid tile's edge. Player and characters share this
    // footprint, so autonomous NPC movement collides with the world exactly like the player.

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
    /// this list; it is rendered alongside the NPCs in <see cref="Render"/>'s Y-sorted character
    /// pass, so it may be drawn behind an NPC whose <see cref="Character.Position"/> Y is higher.
    /// Items added to or removed from the list are taken into account on the next
    /// <see cref="Render"/>.
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
    /// Gets the width in pixels of the canvas from the most recent <see cref="Render"/> call
    /// (0 before the first render). Internal so the future click-to-move story can translate
    /// host-surface coordinates without further API churn.
    /// </summary>
    internal double LastCanvasWidth => _lastCanvasWidth;

    /// <summary>
    /// Gets the height in pixels of the canvas from the most recent <see cref="Render"/> call
    /// (0 before the first render). Internal so the future click-to-move story can translate
    /// host-surface coordinates without further API churn.
    /// </summary>
    internal double LastCanvasHeight => _lastCanvasHeight;

    /// <summary>
    /// Gets a snapshot of the tiles the auto-walk is currently following, in order from the tile
    /// after the player's current tile to the clicked target tile. Internal so tests can assert
    /// that <see cref="Click"/> computed the expected path and that the path is replaced or
    /// cancelled by the input-precedence rules.
    /// </summary>
    internal IReadOnlyList<(int X, int Y)> AutoWalkPath => _autoWalkPath.ToArray();

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

            // Input precedence: a key press cancels any in-progress auto-walk. The path is
            // replaced by the next Click, or the player simply stops on the next Update when no
            // movement key is held. A key release does not cancel the walk.
            _autoWalkPath.Clear();
        }
        else
        {
            _pressedKeys.Remove(key);
        }
    }

    /// <summary>
    /// Reports a click on the <em>main</em> game canvas to the engine, in host-surface (canvas)
    /// coordinates — the same coordinate space as <see cref="SurfaceToWorld"/>. The engine
    /// converts the click to a world position using the canvas size recorded by the most recent
    /// <see cref="Render"/> call, computes an A* tile path from the player's tile to the clicked
    /// tile over the non-solid tiles, and queues it for auto-walk (see the class remarks).
    /// </summary>
    /// <param name="surfaceX">The horizontal surface coordinate in pixels.</param>
    /// <param name="surfaceY">The vertical surface coordinate in pixels.</param>
    /// <remarks>
    /// <para>
    /// If no <see cref="Render"/> has happened yet (the recorded canvas size is zero) the click
    /// is ignored. Without a map the click cancels any in-progress auto-walk and does nothing
    /// else, since there is no grid to path over.
    /// </para>
    /// <para>
    /// The target tile is <c>floor(world)</c> of the converted position. Clicking a solid tile
    /// (<see cref="Tiled.TileMap.IsSolid"/>) cancels any current auto-walk without moving.
    /// Otherwise the engine computes
    /// <c>AStarPathfinder.FindPath(playerTile, targetTile, (x, y) =&gt; !Map.IsSolid(x, y), Map.Width, Map.Height)</c>;
    /// an empty path (start == goal, or the target is unreachable) also cancels the walk without
    /// moving. A valid path <em>replaces</em> the current auto-walk path, even mid-walk.
    /// </para>
    /// </remarks>
    public void Click(double surfaceX, double surfaceY)
    {
        // Unknown canvas size: no Render has happened yet, so the surface coordinates cannot be
        // translated with the camera. The click is ignored.
        if (_lastCanvasWidth <= 0 || _lastCanvasHeight <= 0)
        {
            return;
        }

        if (Map is null)
        {
            // Nothing to path over without a map: cancel any in-progress auto-walk.
            CancelAutoWalk();
            return;
        }

        var world = SurfaceToWorld(surfaceX, surfaceY, _lastCanvasWidth, _lastCanvasHeight);
        (int X, int Y) targetTile = ((int)Math.Floor(world.X), (int)Math.Floor(world.Y));

        // A solid target is invalid: cancel any in-progress auto-walk and do not move.
        if (Map.IsSolid(targetTile.X, targetTile.Y))
        {
            CancelAutoWalk();
            return;
        }

        var path = AStarPathfinder.FindPath(
            Player.Position.ToTile(),
            targetTile,
            isWalkable: (x, y) => !Map.IsSolid(x, y),
            Map.Width,
            Map.Height);

        // An empty path means no movement is needed or the target is unreachable: cancel the
        // walk and do not move.
        if (path.Count == 0)
        {
            CancelAutoWalk();
            return;
        }

        // A valid click always replaces the current auto-walk path, even mid-walk. The new path
        // starts a fresh auto-walk: the first step's OnStartMoving has not been raised yet, and
        // any previous collision stop is no longer the current state.
        _autoWalkStepStarted = false;
        _blockedMoveDirection = null;
        _autoWalkPath.Clear();
        foreach (var tile in path)
        {
            _autoWalkPath.Enqueue(tile);
        }
    }

    /// <summary>
    /// Advances the simulation by <paramref name="dt"/> seconds: resolves the movement direction
    /// from the currently pressed keys, moves the player (resolving collisions against the map's
    /// solid tiles with axis-separated movement when a map is set), clamps it inside the map,
    /// advances every character's autonomous movement (the <c>StartMoving</c>/<c>Update</c> path)
    /// against the map's solid tiles and the map edge, and advances the walk-cycle animation of
    /// the player and every NPC.
    /// </summary>
    /// <param name="dt">The elapsed time in seconds since the previous frame.</param>
    public void Update(double dt)
    {
        // Advance the map's animation clock once per frame so animated tiles progress with game
        // time. Ordering within Update is not significant (animations are independent of the
        // movement/collision resolution below).
        Map?.UpdateAnimations(dt);

        var direction = Config.GetMovementDirection(_pressedKeys);

        // Manual key movement takes priority over auto-walk: while a bound movement key is held
        // the auto-walk does not advance (a key press has already cancelled the path, see Input).
        // Otherwise, when a path is queued, the player auto-walks toward the next waypoint; when
        // there is no key input and no auto-walk target, the player stops.
        if (direction.HasValue)
        {
            MovePlayerWithCollisionResolution(direction.Value, dt);
        }
        else if (!TryAdvanceAutoWalk(dt))
        {
            // All movement keys released and no auto-walk target: the player stops (raises
            // Player.OnStopMoving). The collision-stop state is cleared so a future key press
            // is reported as a fresh start.
            _blockedMoveDirection = null;
            Player.Stop();
        }

        if (Map is not null)
        {
            ClampPlayerToMap();
        }

        // Every character's autonomous movement (the StartMoving/Update path) is resolved
        // against the map's solid tiles exactly like the player's key-driven movement. The
        // player's character never uses StartMoving, so this only affects NPC autonomous
        // movement; the player's key-driven and auto-walk movement is unchanged above.
        Player.Character.Update(dt, Map);
        foreach (var character in _characters)
        {
            character.Update(dt, Map);
        }
    }

    /// <summary>
    /// Draws one frame onto <paramref name="canvas"/>. When a map is set the canvas is cleared to
    /// black first (the black background behind/around a map smaller than the canvas), then the
    /// map's below-player layers are drawn, then all characters (the NPCs in
    /// <see cref="Characters"/> and the player) sorted by <see cref="Character.Position"/> Y
    /// ascending, and finally the map's <c>above_player</c> layers so those tiles appear above
    /// every character. Within the character pass a character with a higher Y (lower on the
    /// screen, closer to the viewer) is drawn last and appears on top of the others, so the
    /// player may be drawn behind an NPC whose Y is higher. The camera follows the player,
    /// centers a map smaller than the canvas, and is clamped so the viewport stays inside the
    /// map; the canvas size is read from the canvas clip bounds so the view adapts to the current
    /// surface size automatically. The camera origin is computed in tiles and converted to a
    /// pixel viewport for the map (a pure pixel renderer) and to pixel screen positions for the
    /// characters.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto (CPU or GPU backed; see the class remarks).</param>
    /// <param name="dt">The elapsed time in seconds since the previous frame (reserved for future animation timing).</param>
    public void Render(SKCanvas canvas, double dt)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var bounds = canvas.LocalClipBounds;
        var canvasWidth = Math.Max(0, (int)Math.Ceiling(bounds.Width));
        var canvasHeight = Math.Max(0, (int)Math.Ceiling(bounds.Height));

        // Record the canvas size so the future click-to-move story can translate host-surface
        // coordinates with the same camera used here, without further API churn.
        _lastCanvasWidth = canvasWidth;
        _lastCanvasHeight = canvasHeight;

        var ts = Map?.TileWidth ?? 48;
        var origin = ComputeCameraOrigin(canvasWidth, canvasHeight);

        // The pixel viewport of the camera, derived from the tile origin. TileMap.Draw /
        // DrawAbovePlayer stay pure pixel renderers and receive this pixel rect.
        var viewport = new SKRect(
            (float)(origin.X * ts),
            (float)(origin.Y * ts),
            (float)(origin.X * ts + canvasWidth),
            (float)(origin.Y * ts + canvasHeight));

        // When a map is set the whole canvas is cleared to black first: this is the black
        // background behind/around a map that is smaller than the canvas. Without a map the
        // canvas is left untouched (characters only), so hosts keep full control of the backdrop.
        if (Map is not null)
        {
            canvas.Clear(SKColors.Black);
        }

        // Draw everything through a single camera translation in pixels (origin * ts): the map
        // blits its prerendered pixel layers in world pixels, and each character is drawn at its
        // world pixel feet position (pos * ts). The compositor anchors each sprite at its
        // middle-bottom, so a character's sprite top-left is at
        // (pos.X*ts - w/2 - origin.X*ts, pos.Y*ts - h - origin.Y*ts) in world pixels and its
        // feet (anchor) at (pos.X*ts - origin.X*ts, pos.Y*ts - origin.Y*ts).
        // Draw order is: below-player map layers -> every character (NPCs and the player) sorted
        // by Y ascending (higher Y drawn last, on top) -> above-player map layers, so tiles
        // marked with the Tiled above_player property appear on top of every character.
        canvas.Save();
        try
        {
            canvas.Translate((float)(-origin.X * ts), (float)(-origin.Y * ts));

            if (Map is not null)
            {
                Map.Draw(canvas, viewport);
            }

            // Draw every character (NPCs + the player) sorted by Y ascending, so a character with
            // a higher Y (lower on screen) is drawn last and appears on top. OrderBy is stable:
            // equal-Y characters keep their relative order (NPCs in Characters-list order, then
            // the player), so the draw order is deterministic.
            var charactersInRenderOrder = _characters.Append(Player.Character).OrderBy(c => c.Position.Y);
            foreach (var character in charactersInRenderOrder)
            {
                character.Draw(canvas, character.Position.ToPixels(ts), dt, _spriteSheetManager);
            }

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
    /// Draws a minimap of the current map onto <paramref name="canvas"/>, a surface separate from
    /// the main game canvas. When <see cref="Map"/> is set it first clears the whole canvas to
    /// black (like <see cref="Render"/>), then renders the map's prerendered tile layers (both below-
    /// and above-player layers, in file order bottom → top — a minimap shows the
    /// full picture), a green dot for the player and a yellow dot for each NPC in
    /// <see cref="Characters"/>. The canvas size is read from the canvas clip bounds, the same
    /// convention as <see cref="Render"/>.
    /// </summary>
    /// <param name="canvas">The minimap surface to draw onto (separate from the main game canvas).</param>
    /// <param name="zoomLevel">
    /// The zoom relative to the &quot;fit the whole map&quot; view: <c>1.0</c> (the default
    /// callers pass when no zoom is wanted) fits the entire map into the canvas; a value
    /// greater than <c>1</c> zooms in (the map is drawn larger than the canvas and the view pans
    /// around the player's dot like the main camera, clamped to the map edges); a value between
    /// <c>0</c> and <c>1</c> zooms out further. A value of zero or less throws
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="zoomLevel"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// When <see cref="Map"/> is <see langword="null"/> this method is a no-op: nothing is drawn
    /// and the canvas is left untouched.
    /// </para>
    /// <para>
    /// Layout: the base fit scale is
    /// <c>min(canvasWidth / Map.PixelWidth, canvasHeight / Map.PixelHeight)</c> and the effective
    /// scale is <c>baseFit * zoomLevel</c>, so the aspect ratio is preserved by construction.
    /// When the whole (scaled) map fits in the canvas it is centered and drawn entirely; when
    /// zoomed in, the visible region is <c>(canvasWidth / scale, canvasHeight / scale)</c> map
    /// pixels, centered on the player's position in map pixels
    /// (<c>Player.Position * ts</c>) and clamped so it stays inside the map bounds (edge-clamped
    /// like the main camera).
    /// </para>
    /// <para>
    /// When <see cref="Map"/> is set the method first clears the whole canvas to black (like
    /// <see cref="Render"/>), so a map smaller than the canvas is centered on a black background
    /// and the unused margins are black, then draws the map and the dots on top. It is a
    /// pure render: it never mutates engine state.
    /// </para>
    /// </remarks>
    public void RenderMinimap(SKCanvas canvas, double zoomLevel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(zoomLevel, 0);

        if (Map is null)
        {
            // No map: draw nothing and leave the canvas untouched.
            return;
        }

        // When a map is set the whole canvas is cleared to black first: this is the black
        // background behind/around a map that is smaller than the canvas, mirroring Render.
        canvas.Clear(SKColors.Black);

        var bounds = canvas.LocalClipBounds;
        var canvasWidth = Math.Max(0, bounds.Width);
        var canvasHeight = Math.Max(0, bounds.Height);
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            // A zero-size minimap surface has nothing to draw onto.
            return;
        }

        var ts = Map.TileWidth;
        var mapWidth = Map.PixelWidth;
        var mapHeight = Map.PixelHeight;

        // The base fit scale fits the whole map into the canvas; the effective scale applies the
        // zoom. Aspect ratio is preserved by construction (one scale for both axes).
        var baseFit = Math.Min(canvasWidth / mapWidth, canvasHeight / mapHeight);
        var scale = baseFit * zoomLevel;

        var scaledWidth = mapWidth * scale;
        var scaledHeight = mapHeight * scale;

        // The visible region size, in map pixels, that maps to the full canvas.
        var visibleWidth = canvasWidth / scale;
        var visibleHeight = canvasHeight / scale;

        // Positions are in tiles, so dots (and the view centre) are scaled by the tile size.
        var playerPixelX = Player.Position.X * ts;
        var playerPixelY = Player.Position.Y * ts;

        // The camera origin in map pixels, mirroring the main camera (see ComputeCameraOrigin):
        // the desired origin centres the player in the visible region, is clamped so the region
        // stays inside the map, and when the map is smaller than the visible region on an axis
        // the origin is shifted by half the difference so the map is centered on that axis (the
        // leftover canvas is black, cleared before drawing).
        var maxOriginX = Math.Max(0, mapWidth - visibleWidth);
        var maxOriginY = Math.Max(0, mapHeight - visibleHeight);
        var centerOffsetX = Math.Max(0, (canvasWidth - scaledWidth) / (2.0 * scale));
        var centerOffsetY = Math.Max(0, (canvasHeight - scaledHeight) / (2.0 * scale));
        var originX = Math.Clamp(playerPixelX - (visibleWidth / 2.0), 0, maxOriginX) - centerOffsetX;
        var originY = Math.Clamp(playerPixelY - (visibleHeight / 2.0), 0, maxOriginY) - centerOffsetY;

        // Draw every prerendered layer image in file order (bottom → top), both below- and
        // above-player layers, so the minimap shows the full picture. For each layer the source
        // rect is the visible region intersected with the layer bounds (in map pixels) and the
        // destination rect is that region mapped to the canvas by the effective scale and the
        // pan/center origin.
        using var layerPaint = new SKPaint { IsAntialias = false };
        using var animatedPaint = new SKPaint { IsAntialias = false };
        for (var layerIndex = 0; layerIndex < Map.Layers.Count; layerIndex++)
        {
            var layer = Map.Layers[layerIndex];
            var image = Map.PrerenderedLayerImages[layerIndex];
            if (image is null)
            {
                continue;
            }

            var sourceLeft = Math.Max(originX, 0);
            var sourceTop = Math.Max(originY, 0);
            var sourceRight = Math.Min(originX + visibleWidth, mapWidth);
            var sourceBottom = Math.Min(originY + visibleHeight, mapHeight);
            if (sourceLeft >= sourceRight || sourceTop >= sourceBottom)
            {
                continue;
            }

            var source = new SKRect(
                (float)sourceLeft,
                (float)sourceTop,
                (float)sourceRight,
                (float)sourceBottom);
            var destination = new SKRect(
                (float)((sourceLeft - originX) * scale),
                (float)((sourceTop - originY) * scale),
                (float)((sourceRight - originX) * scale),
                (float)((sourceBottom - originY) * scale));

            canvas.DrawImage(image, source, destination, layerPaint);

            // Draw the layer's animated cells after its prerendered blit so the minimap is not
            // left with holes where animated tiles were excluded from the prerender. The cell is
            // mapped the same way the layer blits are: source = the cell in map pixels,
            // destination = ((x*ts - originX) * scale, ...), and the layer's flip flags and
            // opacity are applied to match the static tiles of the same layer.
            animatedPaint.Color = SKColors.White.WithAlpha((byte)Math.Round(layer.Opacity * 255f));
            foreach (var cell in Map.GetAnimatedCells(layerIndex))
            {
                var cellLeft = cell.X * ts;
                var cellTop = cell.Y * ts;
                var cellRight = cellLeft + ts;
                var cellBottom = cellTop + ts;

                // Cull the cell against the visible region (same bounds as the layer blits).
                if (cellLeft >= sourceRight || cellRight <= sourceLeft ||
                    cellTop >= sourceBottom || cellBottom <= sourceTop)
                {
                    continue;
                }

                var cellDestination = new SKRect(
                    (float)((cellLeft - originX) * scale),
                    (float)((cellTop - originY) * scale),
                    (float)((cellRight - originX) * scale),
                    (float)((cellBottom - originY) * scale));

                using var tileImage = cell.TileSet.GetTileImage((int)Map.GetAnimatedTileId(cell));
                var flags = layer.GetTileFlags(cell.X, cell.Y);
                TileMap.DrawTile(canvas, tileImage, cellDestination, flags, animatedPaint);
            }
        }

        // The player dot (green) and each NPC dot (yellow), drawn as small filled circles at
        // their world positions converted to map pixels then scaled to the canvas. Dots whose
        // centre lies outside the visible region are skipped.
        DrawMinimapDot(canvas, playerPixelX, playerPixelY, originX, originY, scale, visibleWidth, visibleHeight, SKColors.Green);
        foreach (var character in _characters)
        {
            DrawMinimapDot(canvas, character.Position.X * ts, character.Position.Y * ts, originX, originY, scale, visibleWidth, visibleHeight, SKColors.Yellow);
        }
    }

    /// <summary>
    /// Translates a host-surface (canvas) coordinate, in pixels, to a world coordinate, in
    /// tiles, using the same camera as <see cref="Render"/> (follow + clamp) for the given
    /// canvas size: <c>world = (surfaceX / ts + origin.X, surfaceY / ts + origin.Y)</c>, where
    /// <c>ts</c> is the map's tile width (48 when no map is set) and <c>origin</c> is the camera
    /// origin computed by <see cref="ComputeCameraOrigin"/>.
    /// </summary>
    /// <param name="surfaceX">The horizontal surface coordinate in pixels.</param>
    /// <param name="surfaceY">The vertical surface coordinate in pixels.</param>
    /// <param name="canvasWidth">The width of the canvas in pixels.</param>
    /// <param name="canvasHeight">The height of the canvas in pixels.</param>
    /// <returns>The world position in tiles under the given surface point.</returns>
    /// <remarks>
    /// This is the inverse of <see cref="WorldToSurface"/> within floating-point tolerance.
    /// Hosts use it to translate mouse clicks into world coordinates for "click to move".
    /// </remarks>
    public Position SurfaceToWorld(double surfaceX, double surfaceY, double canvasWidth, double canvasHeight)
    {
        var ts = Map?.TileWidth ?? 48;
        var origin = ComputeCameraOrigin((int)canvasWidth, (int)canvasHeight);
        return new Position(surfaceX / ts + origin.X, surfaceY / ts + origin.Y);
    }

    /// <summary>
    /// Translates a world coordinate, in tiles, to a host-surface (canvas) coordinate, in
    /// pixels, using the same camera as <see cref="Render"/> (follow + clamp) for the given
    /// canvas size: <c>surface = ((world.X - origin.X) * ts, (world.Y - origin.Y) * ts)</c>,
    /// where <c>ts</c> is the map's tile width (48 when no map is set) and <c>origin</c> is the
    /// camera origin computed by <see cref="ComputeCameraOrigin"/>.
    /// </summary>
    /// <param name="worldPosition">The world position in tiles.</param>
    /// <param name="canvasWidth">The width of the canvas in pixels.</param>
    /// <param name="canvasHeight">The height of the canvas in pixels.</param>
    /// <returns>The surface position in pixels where the world position appears.</returns>
    /// <remarks>
    /// This is the inverse of <see cref="SurfaceToWorld"/> within floating-point tolerance.
    /// Hosts use it to position GUI elements around game objects.
    /// </remarks>
    public Position WorldToSurface(Position worldPosition, double canvasWidth, double canvasHeight)
    {
        var ts = Map?.TileWidth ?? 48;
        var origin = ComputeCameraOrigin((int)canvasWidth, (int)canvasHeight);
        return new Position((worldPosition.X - origin.X) * ts, (worldPosition.Y - origin.Y) * ts);
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
    /// Returns whether a spritesheet is registered under <paramref name="name"/>. Full sheets
    /// (loaded with <see cref="LoadSpriteSheet(string, Stream)"/>) and part sheets (loaded with
    /// <see cref="LoadPartSpriteSheet(string, Stream, CharacterPartType)"/>) share one registry,
    /// so both are visible to this check. This is the safe way to test whether a name is already
    /// in use without catching the <see cref="KeyNotFoundException"/> thrown by the render path.
    /// </summary>
    /// <param name="name">The name of the sheet to look up.</param>
    /// <returns>
    /// <see langword="true"/> if a full or part sheet is registered under <paramref name="name"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The check is case-sensitive and trims surrounding whitespace from <paramref name="name"/>
    /// before the lookup, matching how sheets are registered.
    /// </remarks>
    public bool SpriteSheetExists(string name) => _spriteSheetManager.Contains(name);

    /// <summary>
    /// Computes the camera origin: the world position (in tiles) that maps to the canvas' top-left
    /// corner. Let <c>ts</c> be the map's tile width. The desired origin centers the player
    /// (<c>desired = player.Position - (canvasWidth / (2*ts), canvasHeight / (2*ts))</c>); it is
    /// then clamped so the viewport stays inside the map
    /// (<c>origin ∈ [0, max(0, Map.Width - canvasWidth / ts)]</c> per axis). When the map is
    /// smaller than the canvas on an axis, half the difference is subtracted so the map is
    /// centered and the origin becomes negative (<c>origin = clamp(desired, 0, max) -
    /// max(0, (canvasSize - Map.PixelSize) / (2*ts))</c> per axis). When no map is set the origin
    /// is <c>(0, 0)</c>.
    /// </summary>
    /// <param name="canvasWidth">The width of the canvas in pixels.</param>
    /// <param name="canvasHeight">The height of the canvas in pixels.</param>
    /// <returns>The camera origin in world tile coordinates.</returns>
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

        var ts = Map.TileWidth;

        // The maximum viewport origin (in tiles) keeps the view inside the map: 0 when the map
        // is smaller than the canvas on an axis (the map cannot scroll on that axis), otherwise
        // Map.Width - canvasWidth / ts.
        var maxX = Math.Max(0, Map.Width - canvasWidth / (double)ts);
        var maxY = Math.Max(0, Map.Height - canvasHeight / (double)ts);

        // When the map is smaller than the canvas on an axis it is centered by shifting the
        // origin by half the difference (in tiles), producing a negative origin. When the map
        // fills (or exceeds) the canvas the offset is 0 and the behavior is exactly the
        // follow + clamp described above.
        var offsetX = Math.Max(0, (canvasWidth - Map.PixelWidth) / (2.0 * ts));
        var offsetY = Math.Max(0, (canvasHeight - Map.PixelHeight) / (2.0 * ts));

        var desiredX = Player.Position.X - (canvasWidth / (2.0 * ts));
        var desiredY = Player.Position.Y - (canvasHeight / (2.0 * ts));

        return new Position(
            Math.Clamp(desiredX, 0, maxX) - offsetX,
            Math.Clamp(desiredY, 0, maxY) - offsetY);
    }

    /// <summary>
    /// Draws a single minimap dot as a small filled circle at the given map-pixel position,
    /// converted to canvas coordinates with the minimap's scale and pan/center origin. A dot
    /// whose centre lies outside the visible region (in map pixels) is skipped — it would
    /// be off-canvas, and "dots outside the visible region are skipped".
    /// </summary>
    /// <param name="canvas">The minimap canvas to draw onto.</param>
    /// <param name="mapPixelX">The dot's X position in map pixels.</param>
    /// <param name="mapPixelY">The dot's Y position in map pixels.</param>
    /// <param name="originX">The minimap camera origin X in map pixels (the visible region's left).</param>
    /// <param name="originY">The minimap camera origin Y in map pixels (the visible region's top).</param>
    /// <param name="scale">The effective minimap scale (canvas pixels per map pixel).</param>
    /// <param name="visibleWidth">The visible region width in map pixels.</param>
    /// <param name="visibleHeight">The visible region height in map pixels.</param>
    /// <param name="color">The dot color (green for the player, yellow for NPCs).</param>
    private void DrawMinimapDot(
        SKCanvas canvas,
        double mapPixelX,
        double mapPixelY,
        double originX,
        double originY,
        double scale,
        double visibleWidth,
        double visibleHeight,
        SKColor color)
    {
        if (mapPixelX < originX || mapPixelX >= originX + visibleWidth ||
            mapPixelY < originY || mapPixelY >= originY + visibleHeight)
        {
            return;
        }

        using var paint = new SKPaint { Color = color, IsAntialias = false };
        canvas.DrawCircle(
            (float)((mapPixelX - originX) * scale),
            (float)((mapPixelY - originY) * scale),
            MinimapDotRadius,
            paint);
    }

    /// <summary>
    /// Advances the auto-walk by one frame: moves the player toward the center of the next
    /// waypoint tile at <see cref="Player.DefaultBaseSpeed"/> (tile units), popping waypoints as
    /// they are reached and calling <see cref="Player.Stop"/> when the queue empties. Each
    /// auto-walk step (a waypoint leg) begins with <see cref="Player.OnStartMoving"/> via
    /// <see cref="Player.ReportAutoWalkStep(Direction)"/> <em>before</em> that step's position
    /// update: on the first frame of the walk and every time a waypoint is reached while another
    /// remains. The last step's completion stops the player (<see cref="Player.OnStopMoving"/>).
    /// When a map is set the displacement is resolved with the same per-axis slide-to-boundary
    /// clamping as key movement (see <see cref="MovementCollisionResolver"/>), so the auto-walk
    /// never moves the player through a solid tile: if the direct displacement toward the
    /// waypoint is clamped (e.g. the player is not tile-centred and the first segment would cross
    /// a solid corner), the path is cancelled and the player stops (<see cref="Player.OnStopMoving"/>)
    /// rather than walking through the wall.
    /// </summary>
    /// <param name="dt">The elapsed time in seconds since the previous frame.</param>
    /// <returns>
    /// <see langword="true"/> when the auto-walk advanced the player this frame (including the
    /// frame on which the path completed or was cancelled and the player stopped);
    /// <see langword="false"/> when there is no path to advance, so the caller knows to stop the
    /// player itself.
    /// </returns>
    private bool TryAdvanceAutoWalk(double dt)
    {
        if (_autoWalkPath.Count == 0)
        {
            return false;
        }

        var (nextX, nextY) = _autoWalkPath.Peek();
        var target = new Position(nextX + 0.5, nextY + 0.5);
        var toTarget = target - Player.Position;
        var distance = Math.Sqrt((toTarget.X * toTarget.X) + (toTarget.Y * toTarget.Y));
        var step = Player.Character.BaseSpeed * dt;

        if (distance <= step)
        {
            // The player reaches this waypoint: snap to its center and pop it. This snap completes
            // the current step's movement.
            Player.Position = target;
            _autoWalkPath.Dequeue();

            if (_autoWalkPath.Count == 0)
            {
                // The path is complete: the last auto-walk step is reached, so the player stops
                // (raises Player.OnStopMoving).
                _autoWalkStepStarted = false;
                Player.Stop();
                return true;
            }

            // A waypoint was reached and another remains: the next auto-walk step begins now,
            // before any of its displacements (which happen on the following frames).
            var (nextTargetX, nextTargetY) = _autoWalkPath.Peek();
            var nextDirection = DirectionFromVector(new Position(nextTargetX + 0.5, nextTargetY + 0.5) - Player.Position);
            Player.ReportAutoWalkStep(nextDirection);
            _autoWalkStepStarted = true;
            return true;
        }

        // Move toward the waypoint center by at most one step, without overshooting. The move is
        // resolved against the map's solid tiles exactly like key movement (per-axis
        // slide-to-boundary clamping, see <see cref="MovementCollisionResolver"/>). The A* path is
        // over non-solid tiles, so a clear move is returned unchanged; when the move is clamped
        // the path is not directly traversable from the player's current (possibly
        // non-tile-centred) position, so the walk is cancelled and the player is not displaced at
        // all, rather than sliding through (or getting stuck at) a wall.
        var before = Player.Position;
        var direction = DirectionFromVector(toTarget);
        var move = toTarget * (step / distance);
        var destination = before + move;

        // A new auto-walk step begins before its first displacement. This branch runs on every
        // frame between two waypoints, so the start is only reported once per step (the flag is
        // set when the step begins and stays set until the next waypoint is reached).
        if (!_autoWalkStepStarted)
        {
            Player.ReportAutoWalkStep(direction);
            _autoWalkStepStarted = true;
        }

        if (Map is null)
        {
            Player.Position = destination;
        }
        else
        {
            Player.Position = MovementCollisionResolver.Resolve(
                before,
                move.X,
                move.Y,
                Map,
                halfWidth: MovementCollisionResolver.CollisionBoxHalfWidth,
                heightAboveFeet: MovementCollisionResolver.CollisionBoxHeightAboveFeet);
        }

        if (Player.Position != destination)
        {
            // The direct displacement was clamped (blocked by a solid tile / the map edge): the
            // waypoint cannot be reached from the current position without crossing a solid tile,
            // so cancel the walk, leave the player where they were and stop them (raises
            // Player.OnStopMoving). The step that was just started is cancelled with the walk.
            Player.Position = before;
            CancelAutoWalk();
            Player.Stop();
            return true;
        }

        return true;
    }

    /// <summary>
    /// Cancels any in-progress auto-walk by clearing the waypoint queue and resetting the
    /// auto-walk step state (the current step's <see cref="Player.OnStartMoving"/> was not
    /// reported, or no longer applies). The player itself stops on the next <see cref="Update"/>
    /// (no input and no path &#8594; <see cref="Player.Stop"/>) unless it was stopped by the caller
    /// (a blocked auto-walk step stops it immediately).
    /// </summary>
    private void CancelAutoWalk()
    {
        _autoWalkStepStarted = false;
        _autoWalkPath.Clear();
    }

    /// <summary>
    /// Returns the <see cref="Direction"/> closest to <paramref name="vector"/>: the vector is
    /// normalized and the direction whose unit delta has the largest dot product with it wins,
    /// mirroring <see cref="GameConfig.GetMovementDirection(System.Collections.Generic.IEnumerable{Key})"/>'s
    /// quantization. Used by the auto-walk to face the waypoint it is moving toward.
    /// </summary>
    /// <param name="vector">The movement vector (never the zero vector when called).</param>
    /// <returns>The closest of the eight <see cref="Direction"/> values.</returns>
    private static Direction DirectionFromVector(Vector2 vector)
    {
        var length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        if (length <= 0)
        {
            return Direction.Down;
        }

        var normalized = new Vector2(vector.X / length, vector.Y / length);

        Direction? best = null;
        var bestDot = double.NegativeInfinity;
        foreach (var direction in Enum.GetValues<Direction>())
        {
            var delta = direction.Delta();
            var dot = (normalized.X * delta.X) + (normalized.Y * delta.Y);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = direction;
            }
        }

        return best!.Value;
    }

    /// <summary>
    /// Moves the player in <paramref name="direction"/> by <c>BaseSpeed * dt</c> tiles and, when
    /// a map is set, resolves collisions against the map's solid tiles. A <em>cardinal</em>
    /// (single-axis) move uses <em>per-axis slide-to-boundary clamping</em> (see
    /// <see cref="MovementCollisionResolver.Resolve"/>): if the player's collision footprint
    /// (the fixed 0.5×0.5-tile lower-body box anchored at the feet, see
    /// <see cref="PlayerFootprintOverlapsSolid"/>) would overlap a solid tile or leave the map
    /// (the map edge is solid, see <see cref="Tiled.TileMap.IsSolid"/>), the axis slides to the
    /// <em>closest legal position</em> so the leading edge stops exactly at the near edge of the
    /// first blocking solid tile (or at the map edge). A <em>diagonal</em> (both-axis) move is
    /// <em>all-or-nothing</em> (see <see cref="MovementCollisionResolver.ResolveDiagonal"/>):
    /// it is applied only when the full displacement is clear on both axes, otherwise the player
    /// stays put — a diagonal into a wall where only one axis is free stops the player entirely
    /// instead of sliding along the free axis. Because a blocked cardinal axis slides to the exact
    /// boundary instead of reverting the whole step, the feet stop exactly at the solid tile's
    /// edge, matching click-to-move ("colliding with its feet") with no one-frame-step gap and no
    /// floating-point overshoot accumulation. Without a map the displacement is applied directly,
    /// matching <see cref="Character.Move(Direction, double, double)"/>.
    /// </summary>
    /// <param name="direction">The direction to face and move towards.</param>
    /// <param name="dt">The elapsed time in seconds since the previous frame.</param>
    /// <remarks>
    /// The resolver re-validates the resulting footprint as a safety net and refuses a
    /// displacement that would still overlap a solid tile (only possible when the starting
    /// footprint was already illegal, e.g. left embedded in a wall by an external teleport), so
    /// key movement never moves the player through a solid tile while a move that clears the
    /// overlap (escaping the wall) is still allowed.
    /// The movement events are raised at a deterministic time relative to the displacement:
    /// <see cref="Player.ReportMovement(Direction)"/> is called <em>before</em> the position is
    /// updated, so <see cref="Player.OnStartMoving"/> fires when the player starts moving in a new
    /// direction (idle &#8594; moving, or a direction change while moving &#8212; e.g. pressing a second key so
    /// the effective direction becomes a diagonal) and observes the pre-move position.
    /// After the displacement is resolved, a move with no net displacement (fully blocked by solid
    /// tiles or the map edge on every axis) is reported as a <em>collision stop</em> through
    /// <see cref="Player.ReportBlockedMove(Direction)"/>, so <see cref="Player.OnStopMoving"/>
    /// fires even while the movement key stays held. A key move that starts from idle and is
    /// immediately fully blocked therefore fires <see cref="Player.OnStartMoving"/> then
    /// <see cref="Player.OnStopMoving"/> in the same frame. While the key stays held against the
    /// same wall, the start is not re-reported (the player is already resting idle against it), so
    /// the stop fires exactly once. A move that actually displaced the player reports only the
    /// start (a diagonal whose full displacement was clear counts as movement because both axes
    /// changed the position).
    /// This method drives the player; every character's autonomous movement (the
    /// <c>StartMoving</c>/<c>Update</c> path, e.g. NPCs in <see cref="Characters"/>) is resolved
    /// with the same shared resolver by <see cref="Update(double)"/>, so all characters collide
    /// with the world exactly like the player.
    /// </remarks>
    private void MovePlayerWithCollisionResolution(Direction direction, double dt)
    {
        // The engine resolves the displacement itself (axis by axis) instead of calling
        // Player.Move, which would move both axes at once. The position is captured before the
        // resolution so a fully blocked move (no net displacement) can be reported as a
        // collision stop below.
        var before = Player.Position;
        var delta = direction.Delta() * (Player.Character.BaseSpeed * dt);

        // Report the start of movement BEFORE the position update. When the player is already
        // resting idle against a wall in the same direction (the previous frame ended in a
        // collision stop and the key is still held), ReportMovement is skipped so OnStartMoving
        // does not re-fire every frame; a direction change (or a fresh start after the player
        // stopped) clears the resting state and reports a new start.
        if (!_blockedMoveDirection.HasValue || _blockedMoveDirection.Value != direction)
        {
            Player.ReportMovement(direction); // OnStartMoving (idle -> moving, or a direction change) BEFORE the position update
        }

        Player.Position = Map is null
            ? Player.Position + delta
            : MovementCollisionResolver.ResolveDisplacement(
                Player.Position,
                delta.X,
                delta.Y,
                Map,
                MovementCollisionResolver.CollisionBoxHalfWidth,
                MovementCollisionResolver.CollisionBoxHeightAboveFeet);

        if (Player.Position == before)
        {
            // The player is fully blocked (no net displacement after the axis-separated
            // resolution, e.g. walking straight into a wall or into a corner): report the
            // collision stop, so Player.OnStopMoving fires with the blocked direction. Remember
            // the blocked direction so the same held key does not re-report start/stop every frame.
            Player.ReportBlockedMove(direction);
            _blockedMoveDirection = direction;
        }
        else
        {
            // The player actually displaced: it is no longer resting against a wall, so the next
            // blocked move (or a fresh start) is reported normally.
            _blockedMoveDirection = null;
        }
    }

    /// <summary>
    /// Returns whether the player's collision footprint with its feet (middle-bottom) at
    /// <paramref name="position"/> overlaps a solid tile or leaves the map. The footprint is the
    /// <em>fixed 0.5×0.5-tile lower-body box</em> (see
    /// <see cref="MovementCollisionResolver.CollisionBoxHalfWidth"/> and
    /// <see cref="MovementCollisionResolver.CollisionBoxHeightAboveFeet"/>): it is half a tile
    /// wide and half a tile tall regardless of the rendered sprite size, and it is anchored so
    /// the feet (the sprite's middle-bottom) sit at <paramref name="position"/> with the middle
    /// of the feet at the bottom-centre of the box. The map edge (which
    /// <see cref="Tiled.TileMap.IsSolid"/> treats as solid) remains solid. The overlap is tested with <see cref="Tiled.TileMap.IsAreaSolid"/>, whose
    /// tile-boundary semantics (a footprint that ends exactly on a tile boundary does not overlap
    /// the next tile) define the exact boundary the per-axis slide-to-boundary clamp in
    /// <see cref="MovePlayerWithCollisionResolution(Direction, double)"/> stops at. This
    /// predicate delegates to <see cref="MovementCollisionResolver.FootprintOverlaps(RPGEngine.Position, RPGEngine.Tiled.TileMap, double, double)"/>
    /// so the engine and the resolver always share one footprint definition.
    /// </summary>
    private bool PlayerFootprintOverlapsSolid(Position position)
        => MovementCollisionResolver.FootprintOverlaps(
            position,
            Map!,
            MovementCollisionResolver.CollisionBoxHalfWidth,
            MovementCollisionResolver.CollisionBoxHeightAboveFeet);

    /// <summary>
    /// Clamps the player's feet position so its collision footprint (the fixed 0.5×0.5-tile
    /// lower-body box, see the class remarks) stays inside the map bounds. The feet are clamped
    /// to <c>x ∈ [0.25, max(0.25, Map.Width - 0.25)]</c> and
    /// <c>y ∈ [0.5, max(0.5, Map.Height)]</c>, all in tiles — the 0.5×0.5 box's left/right
    /// edges stay at or inside the horizontal map edges and its top edge stays at or below the
    /// top map edge, while the bottom edge (the feet) never goes below the bottom edge. For the
    /// default 48×48 sprite with 48 px tiles this is <c>x ∈ [0.25, Map.Width - 0.25]</c>,
    /// <c>y ∈ [0.5, Map.Height]</c>. Called after every move while a map is set. This is the
    /// player-only post-move safety net; NPCs are kept in bounds by the solid map edge (see
    /// <see cref="Tiled.TileMap.IsSolid"/>) through their resolved autonomous movement.
    /// </summary>
    private void ClampPlayerToMap()
    {
        // The fixed 0.5x0.5-tile lower-body box (see the class remarks and the shared collision
        // constants on MovementCollisionResolver) is clamped inside the map: its left/right
        // edges stay within x in [0, Width] and its top edge (the box extends
        // CollisionBoxHeightAboveFeet above the feet) stays at or below the top map edge, so the
        // feet never go below the bottom edge (the feet are the box's bottom edge). For the
        // default 48x48 sprite with 48 px tiles this is x in [0.25, Map.Width - 0.25],
        // y in [0.5, Map.Height].
        var minX = MovementCollisionResolver.CollisionBoxHalfWidth;
        var maxX = Math.Max(minX, Map!.Width - MovementCollisionResolver.CollisionBoxHalfWidth);
        var minY = MovementCollisionResolver.CollisionBoxHeightAboveFeet;
        var maxY = Math.Max(minY, Map.Height);

        var position = Player.Position;
        Player.Position = new Position(
            Math.Clamp(position.X, minX, maxX),
            Math.Clamp(position.Y, minY, maxY));
    }
}
