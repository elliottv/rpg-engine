using RPGEngine.Sprites;
using SkiaSharp;

namespace RPGEngine;

/// <summary>
/// A character present in the game world (the player or an NPC). It holds the position,
/// facing direction, movement speed, the walk-cycle animation state and the list of
/// spritesheet references used to render it.
/// </summary>
/// <remarks>
/// <para>
/// Spritesheets are referenced with <see cref="SpriteSheetRef"/>, which pairs a loaded sheet
/// name with the 1-based character index (1..8) to use within that sheet. Rendering resolves
/// those references through the <see cref="SpriteSheetManager"/> supplied by the engine at
/// draw time (see <see cref="Draw"/>); the actual drawing is delegated to an internal
/// <see cref="CharacterSpriteCompositor"/> (composition over inheritance).
/// </para>
/// <para>
/// A character uses either a single <em>full</em> sheet or one or more <em>part</em> sheets.
/// Parts are composed in the fixed RPG Maker MZ order regardless of the order of entries in
/// <see cref="SpriteSheets"/>; mixing full and part sheets, or using more than one full sheet,
/// throws <see cref="InvalidOperationException"/> at draw time. A <see cref="SpriteSheetRef"/>
/// whose <see cref="SpriteSheetRef.CharacterIndex"/> is outside 1..8 is rejected when used.
/// </para>
/// </remarks>
public sealed class Character
{
    /// <summary>The middle column of the 3-frame walk cycle: the standing frame in RPG Maker MZ.</summary>
    private const int StandingFrame = 1;

    private readonly CharacterSpriteCompositor _compositor = new();
    private readonly List<SpriteSheetRef> _spriteSheets = [];

    private Position _lastUpdatePosition;
    private int _animationFrame = StandingFrame;

    // The walk cycle is a bounce: 0 → 1 → 2 → 1 → 0 → ... Starting from the standing frame (1)
    // the first step goes toward 0, then the bounce alternates direction at each end.
    private int _frameStep = -1;

    /// <summary>Gets or sets the top-left world position of the character's sprite, in pixels.</summary>
    public Position Position { get; set; }

    /// <summary>Gets or sets the direction the character is facing.</summary>
    public Direction Direction { get; set; }

    /// <summary>Gets or sets the movement speed of the character in pixels per second.</summary>
    public double BaseSpeed { get; set; }

    /// <summary>
    /// Gets the mutable list of spritesheet references used to render the character. Each entry
    /// pairs a loaded sheet name with the 1-based character index (1..8) within that sheet.
    /// The order of the entries is irrelevant for part sheets: they are composed in the fixed
    /// RPG Maker MZ order (see <see cref="CharacterSpriteCompositor"/>).
    /// </summary>
    public IList<SpriteSheetRef> SpriteSheets => _spriteSheets;

    /// <summary>
    /// Gets the current walk-cycle animation frame (0..2). The middle frame (1) is the standing
    /// frame. This accessor is internal so tests can verify animation advancement.
    /// </summary>
    internal int AnimationFrame => _animationFrame;

    /// <summary>Initializes a new instance of the <see cref="Character"/> class at the origin, facing down.</summary>
    public Character()
    {
        _lastUpdatePosition = Position;
    }

    /// <summary>
    /// Moves the character in <paramref name="direction"/> by <c>BaseSpeed * speedFactor * dt</c>
    /// pixels and sets the facing direction.
    /// </summary>
    /// <param name="direction">The direction to face and move towards.</param>
    /// <param name="speedFactor">A multiplier applied to <see cref="BaseSpeed"/>. When zero the
    /// character only turns to face <paramref name="direction"/> without moving.</param>
    /// <param name="dt">The elapsed time in seconds (defaults to 1, so calling
    /// <c>Move(d, factor)</c> moves <c>BaseSpeed * factor</c> pixels, i.e. per-second semantics).</param>
    public void Move(Direction direction, double speedFactor = 1, double dt = 1)
    {
        Direction = direction;

        if (speedFactor == 0)
        {
            return;
        }

        var delta = direction.Delta() * (BaseSpeed * speedFactor * dt);
        Position = Position + delta;
    }

    /// <summary>
    /// Moves the character in its current facing direction. See
    /// <see cref="Move(Direction, double, double)"/> for the movement semantics.
    /// </summary>
    /// <param name="speedFactor">A multiplier applied to <see cref="BaseSpeed"/>.</param>
    /// <param name="dt">The elapsed time in seconds.</param>
    public void Move(double speedFactor = 1, double dt = 1) => Move(Direction, speedFactor, dt);

    /// <summary>
    /// Advances the walk-cycle animation when the character moved since the previous update,
    /// otherwise snaps the animation back to the standing frame. Called by the engine's update
    /// loop once per frame.
    /// </summary>
    /// <param name="dt">The elapsed time in seconds. Animation timing tuning is out of scope for
    /// this story, so <paramref name="dt"/> is accepted for the engine-loop contract but does not
    /// affect the frame advancement (one frame per update while moving).</param>
    internal void Update(double dt)
    {
        var moved = Position != _lastUpdatePosition;
        _lastUpdatePosition = Position;

        if (moved)
        {
            AdvanceFrame();
        }
        else
        {
            _animationFrame = StandingFrame;
            _frameStep = -1;
        }
    }

    /// <summary>
    /// Draws the character at <paramref name="screenPosition"/> (its world position minus the
    /// camera origin). The spritesheet references are resolved through
    /// <paramref name="spriteSheetManager"/>, which the engine supplies at draw time.
    /// </summary>
    /// <param name="canvas">The canvas to draw onto.</param>
    /// <param name="screenPosition">The top-left screen position of the 48×48 sprite.</param>
    /// <param name="dt">The elapsed time in seconds (reserved for future animation timing).</param>
    /// <param name="spriteSheetManager">The manager that resolves the referenced sheet names.</param>
    /// <exception cref="InvalidOperationException">
    /// The <see cref="SpriteSheets"/> list mixes full and part sheets, or contains more than one
    /// full sheet.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A <see cref="SpriteSheetRef"/> has a <see cref="SpriteSheetRef.CharacterIndex"/> outside 1..8.
    /// </exception>
    internal void Draw(SKCanvas canvas, Position screenPosition, double dt, SpriteSheetManager spriteSheetManager)
    {
        _compositor.Draw(canvas, screenPosition, _spriteSheets, Direction, _animationFrame, spriteSheetManager);
    }

    private void AdvanceFrame()
    {
        if (_frameStep > 0)
        {
            _animationFrame++;
            if (_animationFrame == 2)
            {
                // Reached the top of the bounce; the next step goes back down.
                _frameStep = -1;
            }
        }
        else
        {
            _animationFrame--;
            if (_animationFrame == 0)
            {
                // Reached the bottom of the bounce; the next step goes back up.
                _frameStep = 1;
            }
        }
    }
}
