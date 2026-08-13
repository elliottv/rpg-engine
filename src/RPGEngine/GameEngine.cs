using RPGEngine.Tiled;

namespace RPGEngine;

/// <summary>
/// The root of the engine. Owns the game state (player, characters, map and
/// configuration) and, in later stories, exposes the game-loop entry points
/// (<c>Update</c>, <c>Render</c>, <c>Input</c>, asset loading) used by the host.
/// </summary>
public sealed class GameEngine
{
    private readonly List<Character> _characters = [];

    /// <summary>
    /// Gets the player character. The camera will always follow the player (later story).
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// Gets the characters present in the game world, excluding the player.
    /// When the list is updated, it is taken into account by the engine.
    /// </summary>
    public IReadOnlyList<Character> Characters => _characters;

    /// <summary>
    /// Gets the tile map to be displayed, or <see langword="null"/> when no map is loaded.
    /// When it is changed, the rendering is updated (later story).
    /// </summary>
    public TileMap? Map { get; }

    /// <summary>
    /// Gets the configuration values used by the engine. When the values are updated,
    /// they are taken into account by the engine.
    /// </summary>
    public GameConfig Config { get; }

    /// <summary>Initializes a new instance of the <see cref="GameEngine"/> class with default state.</summary>
    public GameEngine()
    {
        Player = new Player();
        Map = null;
        Config = new GameConfig();
    }
}
