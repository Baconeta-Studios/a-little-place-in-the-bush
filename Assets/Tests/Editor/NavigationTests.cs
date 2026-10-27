using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode tests for the walkable grid and pathfinder, on a small test floor:
/// an 8 x 3 rectangle with a wall in the middle that has a gap at the top.
/// These live in an Editor folder (Assembly-CSharp-Editor) because they need the game scripts.
/// </summary>
public class NavigationTests
{
    private readonly List<GameObject> _created = new();
    private WalkableGrid _grid;
    private GridPathfinder _pathfinder;

    [SetUp]
    public void SetUp()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Assume.That(obstacleLayer, Is.GreaterThanOrEqualTo(0), "Needs the Obstacle physics layer.");

        GameObject world = Make("TestWorld");
        var grid = world.AddComponent<Grid>();
        grid.cellSize = new Vector3(0.25f, 0.25f, 0f);

        GameObject floor = Make("TestFloor");
        var area = floor.AddComponent<PolygonCollider2D>();
        area.isTrigger = true;
        area.points = new[] { new Vector2(-4f, 0f), new Vector2(4f, 0f), new Vector2(4f, -3f), new Vector2(-4f, -3f) };

        // Wall at x = 0 from the bottom up to y = -0.6, leaving a gap along the back of the floor.
        AddObstacle(new Vector2(0f, -3f), new Vector2(0.5f, 2.4f), obstacleLayer);

        _grid = world.AddComponent<WalkableGrid>();
        var serialized = new SerializedObject(_grid);
        serialized.FindProperty("walkableArea").objectReferenceValue = area;
        serialized.FindProperty("obstacleMask").intValue = 1 << obstacleLayer;
        serialized.FindProperty("clearance").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _pathfinder = world.AddComponent<GridPathfinder>();
        _grid.Rebuild();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject created in _created) Object.DestroyImmediate(created);
        _created.Clear();
    }

    [Test]
    public void Grid_MarksFloorWalkableAndObstaclesBlocked()
    {
        Assert.IsTrue(_grid.IsWalkable(new Vector2(-2f, -1.5f)));
        Assert.IsFalse(_grid.IsWalkable(new Vector2(0f, -2f)), "Inside the wall.");
        Assert.IsFalse(_grid.IsWalkable(new Vector2(-2f, 1f)), "Above the floor.");
        Assert.IsTrue(_grid.IsWalkable(new Vector2(0f, -0.2f)), "The gap at the back.");
    }

    [Test]
    public void Path_GoesAroundTheWall()
    {
        var path = new List<Vector2>();
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector2(-2f, -2.5f), new Vector2(2f, -2.5f), path));
        Assert.That(Vector2.Distance(path[^1], new Vector2(2f, -2.5f)), Is.LessThan(0.01f));
        AssertWalkablePath(new Vector2(-2f, -2.5f), path);
        Assert.IsTrue(path.Exists(p => p.y > -0.7f), "Should pass through the gap at the back.");
    }

    [Test]
    public void Path_IsSmoothedWhenThereIsLineOfSight()
    {
        var path = new List<Vector2>();
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector2(-3.5f, -2.5f), new Vector2(-1f, -0.5f), path));
        Assert.AreEqual(1, path.Count, "A clear straight walk needs no waypoints.");
    }

    [Test]
    public void BlockedTarget_WalksToTheClosestWalkablePoint()
    {
        var path = new List<Vector2>();
        Vector2 inWall = new(0f, -2f);
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector2(-2f, -2f), inWall, path));
        Assert.IsTrue(_grid.IsWalkable(path[^1]));
        Assert.That(Vector2.Distance(path[^1], inWall), Is.LessThan(0.8f), "Next to the wall (touching edges count as blocked).");
        Assert.Less(path[^1].x, 0f, "Stops on the gecko's side of the wall, not behind it.");
    }

    [Test]
    public void TargetOffTheFloor_IsClampedOntoIt()
    {
        var path = new List<Vector2>();
        Assert.IsTrue(_pathfinder.TryFindPath(new Vector2(-2f, -2f), new Vector2(-2f, 3f), path));
        Assert.IsTrue(_grid.IsWalkable(path[^1]));
        Assert.That(path[^1].y, Is.GreaterThan(-0.5f), "Should stop at the back edge of the floor.");
    }

    [Test]
    public void FencedOffTarget_IsUnreachableButStillWalksClose()
    {
        // Close the gap at the back: the right side is now cut off.
        AddObstacle(new Vector2(0f, -0.7f), new Vector2(0.5f, 1f), LayerMask.NameToLayer("Obstacle"));
        _grid.Rebuild();

        Vector2 from = new(-2f, -2f), to = new(2f, -2f);
        Assert.IsFalse(_pathfinder.IsReachable(from, to));

        var path = new List<Vector2>();
        Assert.IsTrue(_pathfinder.TryFindPath(from, to, path));
        Assert.Less(path[^1].x, 0f, "Stops on the near side of the wall.");
        AssertWalkablePath(from, path);
    }

    [Test]
    public void StartOffTheGrid_StepsOntoItFirst()
    {
        var path = new List<Vector2>();
        Vector2 inWall = new(0f, -2f);
        Assert.IsTrue(_pathfinder.TryFindPath(inWall, new Vector2(-2f, -2f), path));
        Assert.IsTrue(_grid.IsWalkable(path[0]));
    }

    [Test]
    public void StartOffTheGrid_StepsOffTowardsTheTarget()
    {
        var path = new List<Vector2>();
        Vector2 inWall = new(0f, -2f);
        Assert.IsTrue(_pathfinder.TryFindPath(inWall, new Vector2(2f, -2f), path));
        Assert.Greater(path[0].x, 0f, "Steps out of the wall on the target's side.");
        Assert.That(Vector2.Distance(path[0], inWall), Is.LessThan(0.8f), "Only as far as the edge of the floor (touching edges count as blocked).");
        Assert.AreEqual(2, path.Count, "Then straight to the target, not round through the gap.");
    }

    private void AssertWalkablePath(Vector2 from, List<Vector2> path)
    {
        Vector2 previous = from;
        foreach (Vector2 point in path)
        {
            Assert.IsTrue(_grid.HasLineOfSight(previous, point), $"Segment {previous} -> {point} crosses blocked cells.");
            previous = point;
        }
    }

    private void AddObstacle(Vector2 bottomCentre, Vector2 size, int layer)
    {
        GameObject obstacle = Make("TestObstacle");
        obstacle.layer = layer;
        obstacle.transform.position = bottomCentre;
        var box = obstacle.AddComponent<BoxCollider2D>();
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f);
    }

    private GameObject Make(string name)
    {
        var created = new GameObject(name);
        _created.Add(created);
        return created;
    }
}
