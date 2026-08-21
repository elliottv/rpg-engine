using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace RPGEngine.Sample.Desktop;

/// <summary>
/// The main window of the desktop sample host. It materializes the committed fixtures, builds
/// the canonical sample scene, runs a 60 Hz game loop that calls
/// <c>GameEngine.Update(dt)</c> and <c>GameEngine.Render(canvas, dt)</c>, and forwards WPF key
/// events to <c>GameEngine.Input(Key, isPressed)</c>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly GameEngine _engine;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _lastFrameTime;

    public MainWindow()
    {
        InitializeComponent();

        // Materialize the committed fixture assets (decodes the .b64 PNGs to real files in a
        // temporary directory) and build the exact scene the tests and the WASM host use.
        using var fixtures = FixtureAssets.MaterializeToTempDirectory();
        _engine = SampleScene.Create(fixtures.Root);

        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) =>
        {
            // The engine owns the map (a TileMap is IDisposable: it holds the prerendered
            // layer images), so releasing the engine releases the map too.
            CompositionTarget.Rendering -= OnRendering;
            _engine.Dispose();
        };
    }

    /// <summary>Runs the engine's update step on the UI thread at the compositor's frame rate.</summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _stopwatch.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastFrameTime, 0, 0.1);
        _lastFrameTime = now;

        _engine.Update(dt);
        Canvas.InvalidateVisual();
    }

    /// <summary>Draws one frame through the engine onto the SkiaSharp surface.</summary>
    private void OnPaintSurface(object? sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);
        _engine.Render(canvas, dt: 1.0 / 60);
    }

    /// <summary>Translates a WPF key to the engine's framework-agnostic <see cref="Key"/> and presses it.</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (TranslateKey(e.Key) is Key key)
        {
            _engine.Input(key, isPressed: true);
        }
    }

    /// <summary>Translates a WPF key to the engine's framework-agnostic <see cref="Key"/> and releases it.</summary>
    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (TranslateKey(e.Key) is Key key)
        {
            _engine.Input(key, isPressed: false);
        }
    }

    /// <summary>Maps WPF <see cref="Key"/> values to the engine <see cref="Key"/> values.</summary>
    private static Key? TranslateKey(System.Windows.Input.Key key) => key switch
    {
        System.Windows.Input.Key.A => Key.A,
        System.Windows.Input.Key.B => Key.B,
        System.Windows.Input.Key.C => Key.C,
        System.Windows.Input.Key.D => Key.D,
        System.Windows.Input.Key.E => Key.E,
        System.Windows.Input.Key.F => Key.F,
        System.Windows.Input.Key.G => Key.G,
        System.Windows.Input.Key.H => Key.H,
        System.Windows.Input.Key.I => Key.I,
        System.Windows.Input.Key.J => Key.J,
        System.Windows.Input.Key.K => Key.K,
        System.Windows.Input.Key.L => Key.L,
        System.Windows.Input.Key.M => Key.M,
        System.Windows.Input.Key.N => Key.N,
        System.Windows.Input.Key.O => Key.O,
        System.Windows.Input.Key.P => Key.P,
        System.Windows.Input.Key.Q => Key.Q,
        System.Windows.Input.Key.R => Key.R,
        System.Windows.Input.Key.S => Key.S,
        System.Windows.Input.Key.T => Key.T,
        System.Windows.Input.Key.U => Key.U,
        System.Windows.Input.Key.V => Key.V,
        System.Windows.Input.Key.W => Key.W,
        System.Windows.Input.Key.X => Key.X,
        System.Windows.Input.Key.Y => Key.Y,
        System.Windows.Input.Key.Z => Key.Z,
        System.Windows.Input.Key.Up => Key.Up,
        System.Windows.Input.Key.Down => Key.Down,
        System.Windows.Input.Key.Left => Key.Left,
        System.Windows.Input.Key.Right => Key.Right,
        System.Windows.Input.Key.Space => Key.Space,
        _ => null,
    };
}
