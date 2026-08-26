using RPGEngine.Sprites;
using RPGEngine.Tiled;
using RPGEngine.Tests.Tiled;
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
    private const double FrameDt = 1.0 / 60;

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
    // BaseSpeed * factor * dt tiles in the right axis/direction and sets Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(direction, factor, dt) moves exactly BaseSpeed × factor × dt tiles along the correct axis and direction, and sets Direction.</summary>
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

        // BaseSpeed * factor * dt = 100 * 2 * 0.5 = 100 tiles.
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
    // Acceptance 2b (story 21): diagonal movement moves the normalized distance
    // (magnitude 1, not √2) and sets the diagonal Direction.
    // ---------------------------------------------------------------------
    /// <summary>Verifies Move(DownRight, 1, 1) at BaseSpeed 100 moves exactly (100·√½, 100·√½) tiles and sets Direction to DownRight.</summary>
    [Fact]
    public void Move_Diagonal_MovesNormalizedDistance_AndSetsDirection()
    {
        var character = new Character { BaseSpeed = 100, Position = new Position(0, 0) };

        character.Move(Direction.DownRight, speedFactor: 1, dt: 1);

        // DownRight = (+√½, +√½); 100 tiles of travel split evenly across the two axes.
        var component = 100 * Math.Sqrt(0.5);
        Assert.Equal(component, character.Position.X, precision: 9);
        Assert.Equal(component, character.Position.Y, precision: 9);
        Assert.Equal(Direction.DownRight, character.Direction);
    }

    // ---------------------------------------------------------------------
    // Acceptance 4: Update advances frames only while moving.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the walk-cycle animation is time-based and speed-scaled: at
    /// BaseSpeed == AnimationCycleSpeed == 2 (tiles/s, the new defaults) the cycle completes
    /// one full 4-frame cycle (<c>0 → 1 → 2 → 1</c>) per second. A single one-second
    /// <see cref="Character.Update(double, RPGEngine.Tiled.TileMap)"/> after moving advances exactly 4 frames and lands
    /// back on the standing frame.
    /// </summary>
    [Fact]
    public void Update_AtDefaultSpeed_MoveOnceThenOneSecondUpdate_CompletesOneCycle()
    {
        var character = new Character { BaseSpeed = 2 };

        // A fresh character stands on the middle (standing) frame and stays there when idle.
        Assert.Equal(StandingFrame, character.AnimationFrame);
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);

        // Move once, then update for one full second: 4 frames (0.25 s each) advance through
        // 0 → 1 → 2 → 1 and land back on the standing frame.
        character.Move(Direction.Down, speedFactor: 1, dt: 1);
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    /// <summary>
    /// Verifies the walk-cycle advances frames per second proportionally to BaseSpeed (tiles/s):
    /// the frame sequence over one second is revealed by moving + updating at the exact per-frame
    /// duration. 2 tiles/s is the new default equivalent of the previous 96 px/s at 48px tiles.
    /// </summary>
    [Theory]
    [InlineData(2, new[] { 0, 1, 2, 1 })]                 // 0.25 s/frame → 4 frames/s → 1 cycle/s
    [InlineData(4, new[] { 0, 1, 2, 1, 0, 1, 2, 1 })]    // 0.125 s/frame → 8 frames/s → 2 cycles/s
    [InlineData(1, new[] { 0, 1 })]                       // 0.5 s/frame → 2 frames/s → 1/2 cycle/s
    public void Update_WalkCycle_AdvancesFramesPerSecondScaledByBaseSpeed(double baseSpeed, int[] expectedFrames)
    {
        var character = new Character { BaseSpeed = baseSpeed };
        var secondsPerFrame = 1.0 / expectedFrames.Length;

        var actualFrames = new int[expectedFrames.Length];
        for (var i = 0; i < expectedFrames.Length; i++)
        {
            MoveAndUpdate(character, Direction.Down, secondsPerFrame);
            actualFrames[i] = character.AnimationFrame;
        }

        Assert.Equal(expectedFrames, actualFrames);
    }

    /// <summary>Verifies the animation snaps back to the standing frame as soon as the character stops moving.</summary>
    [Fact]
    public void Update_WhenNotMoving_SnapsToStandingFrame()
    {
        var character = new Character { BaseSpeed = 2 };

        // Advance the cycle while moving.
        MoveAndUpdate(character, Direction.Down, dt: 0.25);
        Assert.Equal(0, character.AnimationFrame);

        // Stop moving: the next update sees no movement and snaps back to the standing frame.
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    /// <summary>Verifies AnimationCycleSpeed is configurable: doubling it halves the cycle rate relative to BaseSpeed.</summary>
    [Fact]
    public void Update_AnimationCycleSpeed_IsConfigurable()
    {
        // secondsPerFrame = AnimationCycleSpeed / (BaseSpeed * FramesPerCycle)
        //                 = 4 / (2 * 4) = 0.5 s/frame → only 2 frames per second.
        var character = new Character { BaseSpeed = 2, AnimationCycleSpeed = 4 };

        MoveAndUpdate(character, Direction.Down, dt: 0.5);
        Assert.Equal(0, character.AnimationFrame);

        MoveAndUpdate(character, Direction.Down, dt: 0.5);
        Assert.Equal(StandingFrame, character.AnimationFrame);

        // A single one-second update also completes the (2-frame) half-speed cycle.
        character.Move(Direction.Down, speedFactor: 1, dt: 1);
        character.Update(dt: 1);
        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    // ---------------------------------------------------------------------
    // Acceptance (story 49): StartMoving / StopMoving / IsMoving autonomous
    // movement. A started character moves towards its direction on every
    // Update (exactly like the player while a movement key is held) until it
    // is stopped; the walk cycle advances while moving and snaps back to the
    // standing frame once stopped.
    // ---------------------------------------------------------------------
    /// <summary>Verifies StartMoving sets the Direction and IsMoving but does not move the position until Update is called.</summary>
    [Fact]
    public void StartMoving_SetsDirectionAndIsMoving_DoesNotMoveUntilUpdate()
    {
        var character = new Character
        {
            Position = new Position(10, 20),
            Direction = Direction.Down,
            BaseSpeed = 2,
        };

        character.StartMoving(Direction.Right);

        // StartMoving only faces the character and flags it as moving; the position is
        // untouched until the next Update applies the autonomous displacement.
        Assert.Equal(Direction.Right, character.Direction);
        Assert.True(character.IsMoving);
        Assert.Equal(new Position(10, 20), character.Position);

        character.Update(dt: 1);

        Assert.True(character.IsMoving);
        Assert.NotEqual(new Position(10, 20), character.Position);
    }

    /// <summary>Verifies StartMoving(Right) then Update(1) at BaseSpeed 2 moves exactly 2 tiles right, and a further Update(0.5) moves 1 more.</summary>
    [Fact]
    public void StartMoving_ThenUpdate_MovesExactlyBaseSpeedTimesDt()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(0, 0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 1); // 2 tiles right

        Assert.Equal(new Position(2, 0), character.Position);

        character.Update(dt: 0.5); // 1 more tile right

        Assert.Equal(new Position(3, 0), character.Position);
        Assert.Equal(Direction.Right, character.Direction);
        Assert.True(character.IsMoving);
    }

    /// <summary>Verifies StopMoving sets IsMoving to false and subsequent Update calls no longer move the position.</summary>
    [Fact]
    public void StopMoving_SetsIsMovingFalse_AndNoFurtherMovement()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(0, 0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 1);
        Assert.Equal(new Position(2, 0), character.Position);

        character.StopMoving();
        Assert.False(character.IsMoving);

        // The character stays where it is for as long as it is not started again.
        character.Update(dt: 1);
        Assert.Equal(new Position(2, 0), character.Position);
        character.Update(dt: 1);
        Assert.Equal(new Position(2, 0), character.Position);
    }

    /// <summary>Verifies changing course: StartMoving(Down) then StartMoving(Left) moves left from the new position and faces left.</summary>
    [Fact]
    public void StartMoving_ChangeCourse_MovesFromNewPositionInNewDirection()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(0, 0) };

        character.StartMoving(Direction.Down);
        character.Update(dt: 1);
        Assert.Equal(new Position(0, 2), character.Position);

        character.StartMoving(Direction.Left);
        character.Update(dt: 1);

        // Moved left from (0, 2): 2 tiles left, Y unchanged.
        Assert.Equal(new Position(-2, 2), character.Position);
        Assert.Equal(Direction.Left, character.Direction);
        Assert.True(character.IsMoving);
    }

    /// <summary>Verifies the walk cycle advances while moving and snaps to the standing frame after StopMoving + Update.</summary>
    [Fact]
    public void Update_WhileMoving_AdvancesWalkCycle_AndSnapsToStandingFrameOnStop()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(0, 0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 0.25); // 0.5 tiles moved, one 0.25 s frame due

        Assert.Equal(0, character.AnimationFrame);

        character.StopMoving();
        character.Update(dt: 1);

        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    /// <summary>Verifies the BaseSpeed 0 defensive case: StartMoving + Update does not move and the animation stays on the standing frame.</summary>
    [Fact]
    public void StartMoving_WithZeroBaseSpeed_DoesNotMoveAndStaysOnStandingFrame()
    {
        var character = new Character { BaseSpeed = 0, Position = new Position(4, 4) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 1);

        Assert.Equal(new Position(4, 4), character.Position);
        Assert.Equal(StandingFrame, character.AnimationFrame);
        Assert.True(character.IsMoving);
    }

    /// <summary>Verifies a fresh character's Update never moves it and StartMoving/StopMoving are idempotent (no throw, IsMoving stays correct).</summary>
    [Fact]
    public void FreshCharacter_UpdateNeverMoves_AndStartStopAreIdempotent()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(5, 5) };

        // A fresh character (IsMoving false) never moves on Update.
        Assert.False(character.IsMoving);
        character.Update(dt: 1);
        Assert.Equal(new Position(5, 5), character.Position);

        // StartMoving is idempotent: calling it again while already moving just re-faces.
        character.StartMoving(Direction.Right);
        Assert.True(character.IsMoving);
        Assert.Equal(Direction.Right, character.Direction);

        character.StartMoving(Direction.Left);
        Assert.True(character.IsMoving);
        Assert.Equal(Direction.Left, character.Direction);

        // StopMoving is idempotent: calling it twice keeps IsMoving false and never throws.
        character.StopMoving();
        Assert.False(character.IsMoving);
        character.StopMoving();
        Assert.False(character.IsMoving);
    }

    // ---------------------------------------------------------------------
    // Acceptance (story 64): autonomous movement (StartMoving/Update) is
    // collision-resolved against the map's solid tiles and the map edge when a
    // map is supplied, sharing the player's footprint and the same per-axis
    // slide-to-boundary (cardinal) / all-or-nothing (diagonal) semantics. With
    // no map the displacement stays raw (the historical behavior).
    // ---------------------------------------------------------------------
    /// <summary>Verifies StartMoving(Right) + Update(dt, map) against a solid column at x=2 from X=0.5 stops the feet at X = 2.0 - 0.25 = 1.75, and a further update keeps it there.</summary>
    [Fact]
    public void StartMoving_WithMap_StopsAtSolidColumnBoundary()
    {
        // 4x4 map: a "walls" collision layer with a solid column at x=2 for every row.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
        });
        var map = TileMap.Load(fixture.MapPath);
        var character = new Character { BaseSpeed = 2, Position = new Position(0.5, 1.0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 0.5, map); // exactly 1 tile right, still clear of the wall

        Assert.Equal(new Position(1.5, 1.0), character.Position);

        // The next step would push the box into the solid tile: the right edge stops exactly at
        // the tile's left edge (x = 2.0), so the feet reach X = 2.0 - 0.25 = 1.75.
        character.Update(dt: 0.5, map);
        Assert.Equal(new Position(1.75, 1.0), character.Position);
        Assert.True(character.Position.X + 0.25 <= 2.0 + 1e-9, "The footprint must never overlap the solid tile.");

        // A further update keeps it there.
        character.Update(dt: 0.5, map);
        Assert.Equal(new Position(1.75, 1.0), character.Position);
    }

    /// <summary>Verifies StartMoving + Update(dt, map) on a map with no collision layer moves exactly BaseSpeed * dt tiles (map present, no solid tiles — regression).</summary>
    [Fact]
    public void StartMoving_WithMap_NoCollisionLayer_MovesExactly()
    {
        // A 4x4 map with a ground layer only (no collision layer).
        using var fixture = CreateFilledMapFixture(4, 4);
        var map = TileMap.Load(fixture.MapPath);
        var character = new Character { BaseSpeed = 2, Position = new Position(1.0, 1.0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 1, map); // exactly BaseSpeed * dt = 2 tiles right

        Assert.Equal(new Position(3.0, 1.0), character.Position);
        Assert.True(character.IsMoving);
    }

    /// <summary>Verifies a diagonal autonomous move into a wall where only X is blocked stops the character entirely (no slide along the free Y axis): diagonal movement is all-or-nothing.</summary>
    [Fact]
    public void StartMoving_WithMap_DiagonalIntoWall_StopsEntirely()
    {
        // 5x5 map: a "walls" collision layer with a solid column at x=3 for every row.
        var gids = new uint[25];
        for (var y = 0; y < 5; y++)
        {
            gids[(y * 5) + 3] = 1;
        }

        using var fixture = CreateCollisionMapFixture(5, 5, gids);
        var map = TileMap.Load(fixture.MapPath);
        var character = new Character { BaseSpeed = 2, Position = new Position(2.75, 4.0) };

        // Start flush against the left edge of the wall column (the fixed 0.5x0.5 box's right
        // edge is exactly at x=3). Moving DownRight, X is blocked while Y is free.
        character.StartMoving(Direction.DownRight);
        character.Update(dt: 0.5, map);

        // Diagonal is all-or-nothing: the character stops entirely, no slide along Y.
        Assert.Equal(new Position(2.75, 4.0), character.Position);

        // A further update keeps it there.
        character.Update(dt: 0.5, map);
        Assert.Equal(new Position(2.75, 4.0), character.Position);
    }

    /// <summary>Verifies StartMoving(Left) near the left map edge stops the character at the edge (never leaves the map): the left edge of the 0.5x0.5 box stops at x = 0, so the feet stop at X = 0.25.</summary>
    [Fact]
    public void StartMoving_WithMap_StopsAtLeftMapEdge()
    {
        // A 2x2 map with a ground layer only (no collision layer): only the map edge is solid.
        using var fixture = CreateFilledMapFixture(2, 2);
        var map = TileMap.Load(fixture.MapPath);
        var character = new Character { BaseSpeed = 2, Position = new Position(0.5, 1.5) };

        character.StartMoving(Direction.Left);

        // A single large step: the box's left edge (feet X - 0.25) stops at the left map edge.
        character.Update(dt: 10, map);

        Assert.Equal(0.25, character.Position.X, precision: 9);
        Assert.True(character.Position.X - 0.25 >= 0.0 - 1e-9, "The footprint must never leave the map.");
    }

    /// <summary>Verifies a fully blocked autonomous character: Position is unchanged and the walk cycle snaps to the standing frame on the next Update.</summary>
    [Fact]
    public void StartMoving_WithMap_FullyBlocked_SnapsToStandingFrame()
    {
        // 4x4 map: a "walls" collision layer with a solid column at x=2 for every row.
        using var fixture = CreateCollisionMapFixture(4, 4, new uint[]
        {
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
            0, 0, 1, 0,
        });
        var map = TileMap.Load(fixture.MapPath);
        var character = new Character { BaseSpeed = 2, Position = new Position(0.5, 1.0) };
        character.StartMoving(Direction.Right);

        // Walk into the wall: after enough updates the character is blocked at the boundary.
        for (var frame = 0; frame < 300; frame++)
        {
            character.Update(FrameDt, map);
        }

        Assert.Equal(1.75, character.Position.X, precision: 9);

        // Now fully blocked: a further update leaves the position unchanged and the walk cycle
        // (which detects movement via Position != _lastUpdatePosition) snaps to the standing frame.
        var before = character.Position;
        character.Update(FrameDt, map);
        Assert.Equal(before, character.Position);
        Assert.Equal(StandingFrame, character.AnimationFrame);
    }

    /// <summary>Verifies Update(dt) with no map keeps the raw movement behavior: the autonomous displacement is applied directly (no collision resolution).</summary>
    [Fact]
    public void Update_WithoutMap_MovesRawDisplacement()
    {
        var character = new Character { BaseSpeed = 2, Position = new Position(0, 0) };

        character.StartMoving(Direction.Right);
        character.Update(dt: 1); // no map: exactly 2 tiles right, raw

        Assert.Equal(new Position(2, 0), character.Position);
        Assert.True(character.IsMoving);
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
    // Acceptance (story 23): a character using a 936×864 sheet (78×108 cells)
    // renders a 78×108 sprite at its position; part composition works on the
    // derived cell size.
    // ---------------------------------------------------------------------
    /// <summary>Verifies a character with a single full 936×864 sheet renders a 78×108 sprite at its position.</summary>
    [Fact]
    public void Draw_LargeFullSheet_Renders78x108SpriteAtPosition()
    {
        var manager = new SpriteSheetManager();
        using (var stream = CharacterTestHelper.CreateSheetStream(seed: 0, width: 936, height: 864))
        {
            manager.Load("hero", stream);
        }

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        character.Move(Direction.Down, speedFactor: 0);

        using var bitmap = Render(character, manager, width: 78, height: 108);

        var expected = CharacterTestHelper.SpriteColor(seed: 0, characterIndex: 1, Direction.Down, StandingFrame);
        AssertPixel(bitmap, expected, width: 78, height: 108);
    }

    /// <summary>Verifies 936×864 part sheets still compose on the derived 78×108 cell size (hair2 on top when facing up).</summary>
    [Fact]
    public void Draw_LargePartSheets_ComposeOnDerivedCellSize()
    {
        var manager = new SpriteSheetManager();
        using (var bodyStream = CharacterTestHelper.CreateSheetStream(seed: 1, width: 936, height: 864))
        {
            manager.LoadPart("body", bodyStream, CharacterPartType.Body);
        }
        using (var hairStream = CharacterTestHelper.CreateSheetStream(seed: 3, width: 936, height: 864))
        {
            manager.LoadPart("hair2", hairStream, CharacterPartType.Hair2);
        }

        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("body", CharacterIndex: 1));
        character.SpriteSheets.Add(new SpriteSheetRef("hair2", CharacterIndex: 1));

        // Facing up: the per-direction adjustment draws hair2 over the body on the 78×108 sprite.
        character.Move(Direction.Up, speedFactor: 0);
        using var bitmap = Render(character, manager, width: 78, height: 108);

        var expected = CharacterTestHelper.SpriteColor(seed: 3, characterIndex: 1, Direction.Up, StandingFrame); // hair2
        AssertPixel(bitmap, expected, width: 78, height: 108);
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
    // Acceptance (story 50): the sprite is anchored at its middle-bottom (the
    // feet). Position means where the character stands; the sprite is rendered
    // above and centered on that point.
    // ---------------------------------------------------------------------
    /// <summary>
    /// Verifies the sprite's bottom-centre sits exactly at the anchor (the feet): drawing a
    /// character whose feet are at (48, 72) places the 48×48 sprite from top-left (24, 24) to
    /// bottom-right (72, 72), with the pixel just above the anchor still part of the sprite and
    /// nothing drawn at or below the anchor.
    /// </summary>
    [Fact]
    public void Draw_AnchorsSpriteMiddleBottom_FeetAtPosition()
    {
        var manager = CreateManager(
            (Name: "hero", PartType: null, Seed: 0, Transparent: false));
        var character = new Character();
        character.SpriteSheets.Add(new SpriteSheetRef("hero", CharacterIndex: 1));
        character.Move(Direction.Down, speedFactor: 0); // face down without moving

        // A bitmap larger than the sprite so the anchor point itself is inside the canvas.
        using var bitmap = new SKBitmap(96, 96);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            // The sprite's middle-bottom (feet) sits at (48, 72): top-left (24, 24).
            character.Draw(canvas, new Position(48, 72), dt: 1, manager);
        }

        var expected = CharacterTestHelper.SpriteColor(seed: 0, characterIndex: 1, Direction.Down, StandingFrame);

        // The bottom-centre of the drawn sprite is exactly at the anchor: the pixel above the
        // anchor is the sprite, and nothing is drawn at or below the anchor (the feet).
        Assert.Equal(expected, bitmap.GetPixel(48, 71));
        Assert.Equal(0, bitmap.GetPixel(48, 72).Alpha);
        Assert.Equal(0, bitmap.GetPixel(48, 73).Alpha);

        // The sprite is centered horizontally on the anchor and spans top-left (24,24) to
        // bottom-right (72,72).
        Assert.Equal(expected, bitmap.GetPixel(47, 71));
        Assert.Equal(expected, bitmap.GetPixel(49, 71));
        Assert.Equal(expected, bitmap.GetPixel(24, 24));
        Assert.Equal(expected, bitmap.GetPixel(71, 71));
        Assert.Equal(0, bitmap.GetPixel(23, 23).Alpha); // nothing to the left/above the sprite
        Assert.Equal(0, bitmap.GetPixel(72, 72).Alpha); // nothing to the right of / below it
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>Moves the character one step (dt = 1) and updates it, advancing the walk cycle.</summary>
    private static void MoveAndUpdate(Character character, Direction direction)
        => MoveAndUpdate(character, direction, dt: 1);

    /// <summary>Moves the character for the given <paramref name="dt"/> and updates it, advancing the walk cycle.</summary>
    private static void MoveAndUpdate(Character character, Direction direction, double dt)
    {
        character.Move(direction, speedFactor: 1, dt: dt);
        character.Update(dt: dt);
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

    /// <summary>
    /// Renders the character into a fresh bitmap of the requested size, anchoring the sprite's
    /// middle-bottom (feet) at <c>(width/2, height)</c> so a sprite of the same size as the bitmap
    /// exactly fills it (its top-left lands at <c>(0, 0)</c>).
    /// </summary>
    private static SKBitmap Render(
        Character character,
        SpriteSheetManager manager,
        int width = CharacterTestHelper.CellSize,
        int height = CharacterTestHelper.CellSize)
    {
        var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            character.Draw(canvas, new Position(width / 2.0, height), dt: 1, manager);
        }

        return bitmap;
    }

    /// <summary>Asserts the centre and both corners of the sprite have the expected color.</summary>
    private static void AssertPixel(
        SKBitmap bitmap,
        SKColor expected,
        int width = CharacterTestHelper.CellSize,
        int height = CharacterTestHelper.CellSize)
    {
        Assert.Equal(expected, bitmap.GetPixel(width / 2, height / 2));
        Assert.Equal(expected, bitmap.GetPixel(0, 0));
        Assert.Equal(expected, bitmap.GetPixel(width - 1, height - 1));
    }

    // ---------------------------------------------------------------------
    // Helpers for the collision-resolved autonomous movement tests: build
    // map fixtures (a filled walkable ground layer and an optional "walls"
    // collision layer) exactly like the engine collision tests, so the
    // character-under-test resolves against the same Tiled collision model.
    // ---------------------------------------------------------------------

    /// <summary>Creates a map fixture filled with red tiles in a single "ground" layer.</summary>
    private static TiledTestFixture CreateFilledMapFixture(int width, int height)
        => new(width, height, new[] { FilledLayer(width, height) });

    /// <summary>
    /// Creates a map fixture with a filled "ground" layer (walkable) and a "walls" collision
    /// layer declaring the Tiled <c>is_collision</c> bool property set to <c>true</c>.
    /// </summary>
    private static TiledTestFixture CreateCollisionMapFixture(int width, int height, uint[] collisionGids)
        => new(
            width,
            height,
            new[]
            {
                FilledLayer(width, height),
                new TileLayerSpec(
                    "walls",
                    collisionGids,
                    Properties: new[] { new FixtureProperty("is_collision", "bool", "true") }),
            });

    /// <summary>Builds a fully filled single-layer spec for a map of the given size.</summary>
    private static TileLayerSpec FilledLayer(int width, int height)
        => new("ground", Enumerable.Repeat(1u, width * height).ToArray());
}
