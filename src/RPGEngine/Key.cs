namespace RPGEngine;

/// <summary>
/// A framework-agnostic keyboard key that can be bound to an engine action
/// (for this epic, to a movement direction via <see cref="GameConfig"/>).
/// </summary>
/// <remarks>
/// <para>
/// The engine deliberately does not depend on any GUI framework's input types
/// (WPF <c>KeyEventArgs</c>, Avalonia <c>KeyEventArgs</c>, Blazor
/// <c>KeyboardEventArgs</c>) so it can run on any SkiaSharp host, including
/// WebAssembly. Host applications are responsible for translating their
/// framework's key event to a <see cref="Key"/> value before passing it to the
/// engine.
/// </para>
/// <para>
/// The translation adapters themselves are out of scope for this epic and may
/// be added later as optional extension packages.
/// </para>
/// </remarks>
public enum Key
{
    /// <summary>The A key.</summary>
    A,

    /// <summary>The B key.</summary>
    B,

    /// <summary>The C key.</summary>
    C,

    /// <summary>The D key.</summary>
    D,

    /// <summary>The E key.</summary>
    E,

    /// <summary>The F key.</summary>
    F,

    /// <summary>The G key.</summary>
    G,

    /// <summary>The H key.</summary>
    H,

    /// <summary>The I key.</summary>
    I,

    /// <summary>The J key.</summary>
    J,

    /// <summary>The K key.</summary>
    K,

    /// <summary>The L key.</summary>
    L,

    /// <summary>The M key.</summary>
    M,

    /// <summary>The N key.</summary>
    N,

    /// <summary>The O key.</summary>
    O,

    /// <summary>The P key.</summary>
    P,

    /// <summary>The Q key.</summary>
    Q,

    /// <summary>The R key.</summary>
    R,

    /// <summary>The S key.</summary>
    S,

    /// <summary>The T key.</summary>
    T,

    /// <summary>The U key.</summary>
    U,

    /// <summary>The V key.</summary>
    V,

    /// <summary>The W key.</summary>
    W,

    /// <summary>The X key.</summary>
    X,

    /// <summary>The Y key.</summary>
    Y,

    /// <summary>The Z key.</summary>
    Z,

    /// <summary>The up arrow key.</summary>
    Up,

    /// <summary>The down arrow key.</summary>
    Down,

    /// <summary>The left arrow key.</summary>
    Left,

    /// <summary>The right arrow key.</summary>
    Right,

    /// <summary>The space bar.</summary>
    Space,
}
