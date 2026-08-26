namespace RPGEngine.Tiled;

/// <summary>
/// A single frame of an animated tile: the local tile ID to show and how long to show it,
/// mirroring a Tiled <c>&lt;frame&gt;</c> element.
/// </summary>
/// <param name="TileId">The local tile ID (within the owning tileset) to display for this frame.</param>
/// <param name="DurationMs">How long the frame is displayed, in milliseconds.</param>
internal readonly record struct TileAnimationFrame(uint TileId, int DurationMs);

/// <summary>
/// The frame sequence of an animated tile, as declared by a Tiled
/// <c>&lt;tile&gt;&lt;animation&gt;</c> block. The animation loops forever; the current frame is
/// derived from the engine's animation clock by <see cref="GetFrameTileId"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is internal to the Tiled namespace: animation data is parsed from the tileset and
/// honoured by rendering, but is not exposed through the public API.
/// </para>
/// <para>
/// When the total duration is zero or negative (e.g. every frame declares a zero duration, or
/// the sequence is empty), the animation is treated as a single static frame: the first frame is
/// shown forever. Empty sequences never occur in practice (a <see cref="TileAnimation"/> is only
/// created for a Tiled tile that declares at least one frame), but the defensive fallback keeps
/// the clock safe for any input.
/// </para>
/// </remarks>
internal sealed class TileAnimation
{
    /// <summary>Gets the frames of the animation, in the order they appear in the file.</summary>
    public IReadOnlyList<TileAnimationFrame> Frames { get; }

    /// <summary>
    /// Gets the total duration of one full cycle in milliseconds: the sum of every frame's
    /// <see cref="TileAnimationFrame.DurationMs"/>.
    /// </summary>
    public int TotalDurationMs { get; }

    internal TileAnimation(IReadOnlyList<TileAnimationFrame> frames)
    {
        Frames = frames ?? throw new ArgumentNullException(nameof(frames));

        var total = 0;
        foreach (var frame in frames)
        {
            total += frame.DurationMs;
        }

        TotalDurationMs = total;
    }

    /// <summary>
    /// Returns the local tile ID of the frame that should be displayed after
    /// <paramref name="elapsedSeconds"/> of animation time have passed. The clock is looped over
    /// the full cycle (<c>elapsedMs % TotalDurationMs</c>) and the frames are walked in file
    /// order, so a frame with duration <c>D</c> is shown from its start offset for exactly
    /// <c>D</c> milliseconds of each cycle.
    /// </summary>
    /// <param name="elapsedSeconds">The animation clock in seconds (monotonic, never reset).</param>
    /// <returns>The local tile ID of the current frame.</returns>
    /// <remarks>
    /// The scan is O(frames); animation sequences in Tiled maps are short, so this is fine for a
    /// per-frame lookup.
    /// </remarks>
    internal uint GetFrameTileId(double elapsedSeconds)
    {
        if (TotalDurationMs <= 0 || Frames.Count == 0)
        {
            // Defensive: no measurable cycle (or no frames) -> show the first frame forever.
            return Frames.Count > 0 ? Frames[0].TileId : 0;
        }

        var elapsedMs = elapsedSeconds * 1000.0;
        var cycleOffset = elapsedMs % TotalDurationMs;

        var accumulated = 0;
        foreach (var frame in Frames)
        {
            accumulated += frame.DurationMs;
            if (cycleOffset < accumulated)
            {
                return frame.TileId;
            }
        }

        // Floating-point edge: when the modulo lands exactly on the total (or the last frame's
        // boundary), fall back to the final frame of the cycle.
        return Frames[^1].TileId;
    }
}
