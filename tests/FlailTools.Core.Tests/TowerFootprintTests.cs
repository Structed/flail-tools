using FlailTools.Core.Mapping;
using Structed.Inkwell.Mapping;

namespace FlailTools.Core.Tests;

/// <summary>
/// The contract that lets a tower be redrawn in any shape without rearranging its insides.
/// </summary>
public sealed class TowerFootprintTests
{
    private static readonly MapPoint Centre = new(160, 130);

    /// <summary>Ten shapes in the book, plus the plain outline a shape pinned to text falls back to.</summary>
    public static TheoryData<int?> Faces => [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, null];

    [Theory]
    [MemberData(nameof(Faces))]
    public void EveryShapeLeavesTheSameFloorClear(int? face)
    {
        (_, MapPolygon outline) = TowerFootprints.For(face, 0);

        // Fixtures and stairwells are laid out once, for this disc. Every shape has to keep it whole
        // or a Bone Keep would need its own furniture.
        for (int step = 0; step < 512; step++)
        {
            MapPoint edge = Centre + MapPoint.FromAngle(step * Math.Tau / 512, TowerFootprints.Usable);

            Assert.True(
                outline.Contains(edge),
                FormattableString.Invariant($"The floor is cut into at {edge.X},{edge.Y}."));
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void EveryShapeFitsTheSpaceAFloorIsGiven(int? face)
    {
        for (int floor = 0; floor < 6; floor++)
        {
            MapBounds bounds = TowerFootprints.For(face, floor).Outline.Bounds;

            // Clear of the floor number on the left, the canvas on the right, and the caption above.
            Assert.InRange(bounds.MinX, 72, 320);
            Assert.InRange(bounds.MaxX, 0, 248);
            Assert.InRange(bounds.MinY, 52, 320);
            Assert.InRange(bounds.MaxY, 0, 206);
        }
    }

    [Fact]
    public void EveryShapeIsDrawnDifferentlyFromTheRest()
    {
        int?[] faces = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, null];
        List<(string Name, MapPolygon Outline)> drawn = [.. faces.Select(face => TowerFootprints.For(face, 0))];

        Assert.Equal(drawn.Count, drawn.Select(shape => shape.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            from first in drawn
            from second in drawn
            where !string.Equals(first.Name, second.Name, StringComparison.Ordinal)
            select (first, second),
            pair => Assert.NotEqual(pair.first.Outline.Points, pair.second.Outline.Points));
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void OnlyASpireTwistsAsItRises(int? face)
    {
        (string name, MapPolygon ground) = TowerFootprints.For(face, 0);
        MapPolygon third = TowerFootprints.For(face, 3).Outline;

        if (name == "spire")
        {
            Assert.NotEqual(ground.Points, third.Points);
        }
        else
        {
            Assert.Equal(ground.Points, third.Points);
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void TheDoorwayLandsOnTheWallAtTheFootOfThePlan(int? face)
    {
        MapPolygon outline = TowerFootprints.For(face, 0).Outline;
        MapPoint door = TowerFootprints.Doorway(outline);

        Assert.Equal(Centre.X, door.X);
        Assert.True(door.Y > Centre.Y + TowerFootprints.Usable, "The door should be outside the usable floor.");
        Assert.True(outline.DistanceToEdge(door) < 0.001, "The door should be cut into the wall, not floating.");
    }
}
