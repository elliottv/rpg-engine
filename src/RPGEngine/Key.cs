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
    /// <summary>No key.</summary>
    None,
    
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

    /// <summary>The gamepad 0 button.</summary>
    Gamepad0,

    /// <summary>The gamepad 1 button.</summary>
    Gamepad1,

    /// <summary>The gamepad 2 button.</summary>
    Gamepad2,

    /// <summary>The gamepad 3 button.</summary>
    Gamepad3,

    /// <summary>The gamepad 4 button.</summary>
    Gamepad4,

    /// <summary>The gamepad 5 button.</summary>
    Gamepad5,

    /// <summary>The gamepad 6 button.</summary>
    Gamepad6,

    /// <summary>The gamepad 7 button.</summary>
    Gamepad7,

    /// <summary>The gamepad 8 button.</summary>
    Gamepad8,

    /// <summary>The gamepad 9 button.</summary>
    Gamepad9,

    /// <summary>The gamepad 10 button.</summary>
    Gamepad10,

    /// <summary>The gamepad 11 button.</summary>
    Gamepad11,

    /// <summary>The gamepad 12 button.</summary>
    Gamepad12,

    /// <summary>The gamepad 13 button.</summary>
    Gamepad13,

    /// <summary>The gamepad 14 button.</summary>
    Gamepad14,

    /// <summary>The gamepad 15 button.</summary>
    Gamepad15,

    /// <summary>The gamepad 16 button.</summary>
    Gamepad16,

    /// <summary>The gamepad 17 button.</summary>
    Gamepad17,

    /// <summary>The gamepad 18 button.</summary>
    Gamepad18,

    /// <summary>The gamepad 19 button.</summary>
    Gamepad19,
}
