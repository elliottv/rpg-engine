using RPGEngine.Sprites;
using SkiaSharp;
using Xunit;

namespace RPGEngine.Tests;

/// <summary>
/// Acceptance tests for <see cref="Character"/>: movement, direction, walk-cycle animation and
/// the RPG Maker MZ sprite part composition (story 5: Character — position, movement, direction
/// and sprite part composition).
/// </summary>
/// <remarks>
/// The composition tests render a character into an offscreen <see cref="SKBitmap"/> using the
/// seeded sheets from <see cref="CharacterTestHelper"/> and assert the pixel color of the
/// top-most part at a known overlapping coordinate. All generated sheets are fully opaque 48×48
/// cells (the head used for the <c>$</c>-rule tests is deliberately fully transparent so the
/// hair layer behind it can be observed), so the whole sprite is uniformly the color of the
/// last-drawn part; checking the centre and both corners guards against accidental offsets.
/// </remarks>
public class CharacterTests
{
    private const int StandingFrame = 1; // the frame a fresh/stopped character renders at

    // ---------------------------------------------------------------------
    // Acceptance 1: Move with speedFactor == 0 changes only Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that Move with speedFactor 0 only turns the character and does not move it.</summary>
    [Fact]
    public void Move_WithZeroSpeedFactor_ChangesOnlyDirection()
    {
        var character = new Character
        {
            Position = new Position(10, 20),
            Direction = Direction.Down,
        };

        character.Move(Direction.Right, speedFactor: 0);

        Assert.Equal(Direction.Right, character.Direction);
        Assert.Equal(new Position(10, 20), character.Position);
    }

    // ---------------------------------------------------------------------
    // Acceptance 2: Move(direction, factor, dt) moves exactly
    // BaseSpeed * factor * dt pixels in the right axis/direction and sets Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(direction, factor, dt) moves exactly BaseSpeed × factor × dt pixels along the correct axis and direction, and sets Direction.</summary>
    [Theory]
    [InlineData(Direction.Down, 0.0, 100.0)]
    [InlineData(Direction.Up, 0.0, -100.0)]
    [InlineData(Direction.Left, -100.0, 0.0)]
    [InlineData(Direction.Right, 100.0, 0.0)]
    public void Move_WithDirectionFactorAndDt_MovesExactly(
        Direction direction,
        double expectedX,
        double expectedY)
    {
        var character = new Character { BaseSpeed = 100, Position = new Position(0, 0) };

        // BaseSpeed * factor * dt = 100 * 2 * 0.5 = 100 pixels.
        character.Move(direction, speedFactor: 2, dt: 0.5);

        Assert.Equal(expectedX, character.Position.X);
        Assert.Equal(expectedY, character.Position.Y);
        Assert.Equal(direction, character.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 3: the direction-less Move reuses the previous Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies that Move(speedFactor, dt) without a direction reuses the character's previous Direction.</summary>
    [Fact]
    public void Move_WithoutDirection_ReusesPreviousDirection()
    {
        var character = new Character { BaseSpeed = 100, Direction = Direction.Up };

        character.Move(speedFactor: 1, dt: 1);

        Assert.Equal(new Position(0, -100), character.Position);
        Assert.Equal(Direction.Up, character.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: Update advances frames only while moving.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the walk-cycle animation advances 0 → 1 → 2 → 1 → 0 only while the character
    /// moves, and snaps back to the standing frame (1) once it stops.
    /// </summary>
    [Fact]
    public void Update_AdvancesWalkCycleOnlyWhileMoving()
    {
        var character = new Character { BaseSpeed = 1 };

        // A fresh character stands on the middle (standing) frame and stays there when idle.
        Assert.Equal(StandingFrame, character.AnimationFrame);
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);

        // Walk cycle while moving: 0 → 1 → 2 → 1 → 0.
        MoveAndUpdate(character, Direction.Down);
        Assert.Equal(0, character.AnimationFrame);

        MoveAndUpdate(character, Direction.Down);
        Assert.Equal(1, character.AnimationFrame);

        MoveAndUpdate(character, Direction.Down);
        Assert.Equal(2, character.AnimationFrame);

        MoveAndUpdate(character, Direction.Down);
        Assert.Equal(1, character.AnimationFrame);

        MoveAndUpdate(character, Direction.Down);
        Assert.Equal(0, character.AnimationFrame);

        // Once the character stops moving, Update snaps back to the standing frame.
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5a: a single full sheet renders the expected cell for the
    // configured CharacterIndex.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a character configured with a single full sheet renders the exact cell of its CharacterIndex.</summary>
    [Fact]
    public void Draw_SingleFullSheet_WithCharacterIndex_RendersExpectedCell()
    {
        var manager = CreateManager(
            (Name: "hero", PartType: null, Seed: 0, Transparent: false));
        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        character.Move(Direction.Down, speedFactor: 0); // face down without moving

        using var bitmap = Render(character, manager);

        var expected = CharacterTestHelper.SpriteColor(seed: 0, characterIndex: 1, Direction.Down, StandingFrame);
        AssertPixel(bitmap, expected);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5b: parts without head compose in the fixed order, with the
    // per-direction hair2 adjustment (hair2 on top when facing Up, body visible
    // when facing Down). The entries are added out of composition order to also
    // prove the list order is irrelevant.
    // ---------------------------------------------------------------------
    /// <summary>Verifies part composition without a head: body on top facing down, hair2 drawn on top facing up (the per-direction adjustment), regardless of list order.</summary>
    [Fact]
    public void Draw_PartsWithoutHead_Hair2OnTopWhenUp_BodyOnTopWhenDown()
    {
        var manager = CreateManager(
            (Name: "body", PartType: CharacterPartType.Body, Seed: 1, Transparent: false),
            (Name: "face", PartType: CharacterPartType.Face, Seed: 2, Transparent: false),
            (Name: "hair2", PartType: CharacterPartType.Hair2, Seed: 3, Transparent: false));

        var character = new Character();
        // Deliberately not in composition order: hair2, face, body is the draw order.
        character.SpriteSheets.Add(new SpriteSheetRef("body", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("face", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("hair2", 1));

        // Facing down: fixed order hair2 → face → body, so body is on top.
        character.Move(Direction.Down, speedFactor: 0);
        using (var down = Render(character, manager))
        {
            var expectedDown = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Down, StandingFrame);
            AssertPixel(down, expectedDown);
        }

        // Facing up: the second hair2 crop is drawn in front (the per-direction adjustment).
        character.Move(Direction.Up, speedFactor: 0);
        using (var up = Render(character, manager))
        {
            var expectedUp = CharacterTestHelper.SpriteColor(seed: 3, characterIndex: 1, Direction.Up, StandingFrame);
            AssertPixel(up, expectedUp);
        }
    }

    // ---------------------------------------------------------------------
    // Acceptance 5c/5d: the "$" prefix rule. A head sheet whose name contains
    // '$' hides the hair layers; one whose name does not shows them. The head
    // fixture is fully transparent so the hair behind it is observable.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a head sheet whose name contains '$' hides the hair layers (hair2 is not drawn).</summary>
    [Fact]
    public void Draw_HeadNameWithDollar_HidesHair()
    {
        var manager = CreateManager(
            (Name: "body", PartType: CharacterPartType.Body, Seed: 1, Transparent: false),
            (Name: "hair2", PartType: CharacterPartType.Hair2, Seed: 3, Transparent: false),
            (Name: "head$", PartType: CharacterPartType.Head, Seed: 4, Transparent: true));

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("body", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("hair2", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("head$", 1));

        // Facing up: even though hair2 would be drawn in front (per-direction adjustment),
        // showHair is false so hair2 is skipped and the body shows through the transparent head.
        character.Move(Direction.Up, speedFactor: 0);
        using var bitmap = Render(character, manager);

        var expected = CharacterTestHelper.SpriteColor(seed: 1, characterIndex: 1, Direction.Up, StandingFrame); // body
        AssertPixel(bitmap, expected);
    }

    /// <summary>Verifies a head sheet whose name does not contain '$' keeps the hair layers shown.</summary>
    [Fact]
    public void Draw_HeadNameWithoutDollar_ShowsHair()
    {
        var manager = CreateManager(
            (Name: "body", PartType: CharacterPartType.Body, Seed: 1, Transparent: false),
            (Name: "hair2", PartType: CharacterPartType.Hair2, Seed: 3, Transparent: false),
            (Name: "head", PartType: CharacterPartType.Head, Seed: 4, Transparent: true));

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("body", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("hair2", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("head", 1));

        // Facing up: showHair is true, so hair2 is drawn behind the body and again in front
        // (the per-direction adjustment); the transparent head lets it show through.
        character.Move(Direction.Up, speedFactor: 0);
        using var bitmap = Render(character, manager);

        var expected = CharacterTestHelper.SpriteColor(seed: 3, characterIndex: 1, Direction.Up, StandingFrame); // hair2
        AssertPixel(bitmap, expected);
    }

    // ---------------------------------------------------------------------
    // Acceptance 5e: two entries referencing the same sheet with different
    // CharacterIndex values render different sprites.
    // ---------------------------------------------------------------------
    /// <summary>Verifies the CharacterIndex selects which character within a shared sheet is rendered.</summary>
    [Fact]
    public void Draw_SameSheet_DifferentCharacterIndex_RendersDifferentSprites()
    {
        var manager = CreateManager(
            (Name: "hero", PartType: null, Seed: 0, Transparent: false));

        var first = new Character();
        first.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        first.Move(Direction.Down, speedFactor: 0);

        var second = new Character();
        second.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 2));
        second.Move(Direction.Down, speedFactor: 0);

        using var firstBitmap = Render(first, manager);
        using var secondBitmap = Render(second, manager);

        var expectedFirst = CharacterTestHelper.SpriteColor(seed: 0, characterIndex: 1, Direction.Down, StandingFrame);
        var expectedSecond = CharacterTestHelper.SpriteColor(seed: 0, characterIndex: 2, Direction.Down, StandingFrame);

        AssertPixel(firstBitmap, expectedFirst);
        AssertPixel(secondBitmap, expectedSecond);
        Assert.NotEqual(expectedFirst, expectedSecond);
    }

    // ---------------------------------------------------------------------
    // Acceptance 6: mixed full+part list throws InvalidOperationException at
    // draw time.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a character mixing a full and a part sheet throws InvalidOperationException at draw time.</summary>
    [Fact]
    public void Draw_MixedFullAndPartSheets_ThrowsInvalidOperationException()
    {
        var manager = CreateManager(
            (Name: "hero", PartType: null, Seed: 0, Transparent: false),
            (Name: "body", PartType: CharacterPartType.Body, Seed: 1, Transparent: false));

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("hero", 1));
        character.SpriteSheets.Add(new SpriteSheetRef("body", 1));

        using var bitmap = new SKBitmap(CharacterTestHelper.CellSize, CharacterTestHelper.CellSize);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<InvalidOperationException>(
            () => character.Draw(canvas, new Position(0, 0), dt: 1, manager));
    }

    // ---------------------------------------------------------------------
    // Acceptance 7: a SpriteSheetRef with CharacterIndex outside 1..8 is
    // rejected when used.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a SpriteSheetRef whose CharacterIndex is outside 1..8 throws ArgumentOutOfRangeException when drawn.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Draw_InvalidCharacterIndex_ThrowsArgumentOutOfRangeException(int characterIndex)
    {
        var manager = CreateManager(
            (Name: "hero", PartType: null, Seed: 0, Transparent: false));

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("hero", characterIndex));

        using var bitmap = new SKBitmap(CharacterTestHelper.CellSize, CharacterTestHelper.CellSize);
        using var canvas = new SKCanvas(bitmap);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => character.Draw(canvas, new Position(0, 0), dt: 1, manager));
    }

    // ---------------------------------------------------------------------
    // Additional coverage: a character with no sprite sheets draws nothing.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a character with an empty SpriteSheets list draws nothing (no exception, transparent output).</summary>
    [Fact]
    public void Draw_NoSpriteSheets_DrawsNothing()
    {
        var manager = new SpriteSheetManager();
        var character = new Character();

        using var bitmap = Render(character, manager);

        // Nothing was drawn: every pixel stays fully transparent.
        Assert.Equal(0, bitmap.GetPixel(24, 24).Alpha);
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
        Assert.Equal(0, bitmap.GetPixel(47, 47).Alpha);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Moves the character one step and updates it, advancing the walk cycle.</summary>
    private static void MoveAndUpdate(Character character, Direction direction)
    {
        character.Move(direction, speedFactor: 1, dt: 1);
        character.Update(dt: 1);
    }

    /// <summary>Loads the sheets described by the given tuples and returns a fresh manager.</summary>
    /// <remarks>
    /// Each tuple is (Name, PartType, Seed, Transparent); <c>PartType</c> is null for a full sheet.
    /// </remarks>
    private static SpriteSheetManager CreateManager(
        params (string Name, CharacterPartType? PartType, int Seed, bool Transparent)[] sheets)
    {
        var manager = new SpriteSheetManager();
        foreach (var (name, partType, seed, transparent) in sheets)
        {
            using var stream = CharacterTestHelper.CreateSheetStream(seed, transparent);
            if (partType is null)
            {
                manager.Load(name, stream);
            }
            else
            {
                manager.LoadPart(name, stream, partType.Value);
            }
        }

        return manager;
    }

    /// <summary>Renders the character into a fresh 48×48 bitmap at the origin.</summary>
    private static SKBitmap Render(Character character, SpriteSheetManager manager)
    {
        var bitmap = new SKBitmap(CharacterTestHelper.CellSize, CharacterTestHelper.CellSize);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            character.Draw(canvas, new Position(0, 0), dt: 1, manager);
        }

        return bitmap;
    }

    /// <summary>Asserts the centre and both corners of the sprite have the expected color.</summary>
    private static void AssertPixel(SKBitmap bitmap, SKColor expected)
    {
        Assert.Equal(expected, bitmap.GetPixel(24, 24));
        Assert.Equal(expected, bitmap.GetPixel(0, 0));
        Assert.Equal(expected, bitmap.GetPixel(47, 47));
    }
}
