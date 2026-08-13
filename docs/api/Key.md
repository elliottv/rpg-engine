# Key

Namespace: `RPGEngine` — a framework-agnostic keyboard key.

`Key` can be bound to an engine action (for this epic, to a movement direction via
`GameConfig`).

## Remarks

The engine deliberately does not depend on any GUI framework's input types (WPF `KeyEventArgs`,
Avalonia `KeyEventArgs`, Blazor `KeyboardEventArgs`) so it can run on any SkiaSharp host,
including WebAssembly. Host applications translate their framework's key event to a `Key` value
before passing it to `GameEngine.Input`.

## Values

| Value | Meaning |
| --- | --- |
| `A` … `Z` | The letter keys. |
| `Up`, `Down`, `Left`, `Right` | The arrow keys. |
| `Space` | The space bar. |

## Example: host translation

```csharp
// WPF
private static Key TranslateKey(System.Windows.Input.Key key) => key switch
{
    System.Windows.Input.Key.W => Key.W,
    System.Windows.Input.Key.A => Key.A,
    System.Windows.Input.Key.S => Key.S,
    System.Windows.Input.Key.D => Key.D,
    System.Windows.Input.Key.Up => Key.Up,
    System.Windows.Input.Key.Down => Key.Down,
    System.Windows.Input.Key.Left => Key.Left,
    System.Windows.Input.Key.Right => Key.Right,
    System.Windows.Input.Key.Space => Key.Space,
    _ => throw new ArgumentOutOfRangeException(nameof(key)),
};
```

```csharp
// Blazor (KeyboardEventArgs.Key is the browser's key string)
private static Key TranslateKey(string key) => key switch
{
    "w" or "W" => Key.W,
    "a" or "A" => Key.A,
    "s" or "S" => Key.S,
    "d" or "D" => Key.D,
    "ArrowUp" => Key.Up,
    "ArrowDown" => Key.Down,
    "ArrowLeft" => Key.Left,
    "ArrowRight" => Key.Right,
    " " => Key.Space,
    _ => throw new ArgumentOutOfRangeException(nameof(key)),
};
```
