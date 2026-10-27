using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode tests for build mode's placement rules. The test floor is built far from the origin so
/// whatever scene is open (the Cafe) can't get in the way:
///
///   back   +------------------- gap -------------------+
///          |            |wall|                          |
///          | spawn      |wall|                  Counter |
///   front  +--------------------------------------------+
/// </summary>
public class PlacementTests
{
    private static readonly Vector2 Origin = new(500f, 500f);

    private readonly List<Object> _created = new();
    private BuildArea _area;
    private PlaceableDefinition _chair;

    [SetUp]
    public void SetUp()
    {
        int obstacle = LayerMask.NameToLayer("Obstacle");
        Assume.That(obstacle, Is.GreaterThanOrEqualTo(0), "Needs the Obstacle physics layer.");

        GameObject world = Make("TestBuildWorld");
        world.AddComponent<Grid>().cellSize = new Vector3(0.25f, 0.25f, 0f);

        GameObject floor = Make("TestFloor");
        var polygon = floor.AddComponent<PolygonCollider2D>();
        polygon.isTrigger = true;
        polygon.points = new[] { At(-4f, 0.5f), At(4f, 0.5f), At(4f, -4f), At(-4f, -4f) };

        // A wall down the middle with a gap at the back (y -1.2 to 0.5).
        GameObject wall = Make("Wall");
        wall.layer = obstacle;
        wall.transform.position = At(0f, -4f);
        var box = wall.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.5f, 2.8f);
        box.offset = new Vector2(0f, 1.4f);

        GameObject spawn = Make("Spawn");
        spawn.transform.position = At(-3f, -3f);
        var spawnPoint = spawn.AddComponent<SpawnPoint>();
        SetValue(spawnPoint, "isDefault", true);

        GameObject counter = Make("Counter");
        counter.transform.position = At(3f, -2.5f);
        GameObject stand = Make("StandPoint");
        stand.transform.SetParent(counter.transform, false);
        stand.transform.position = At(3f, -3f);
        stand.AddComponent<StandPoint>();

        var grid = world.AddComponent<WalkableGrid>();
        var serialized = new SerializedObject(grid);
        serialized.FindProperty("walkableArea").objectReferenceValue = polygon;
        serialized.FindProperty("obstacleMask").intValue = 1 << obstacle;
        serialized.FindProperty("clearance").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        world.AddComponent<GridPathfinder>();
        _area = world.AddComponent<BuildArea>();
        grid.Rebuild();

        _chair = Definition(new Vector2(0.9f, 0.4f));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object created in _created)
            if (created) Object.DestroyImmediate(created);
        _created.Clear();
    }

    [Test]
    public void OpenGround_IsValid()
    {
        PlacementCheck check = PlacementValidator.Check(_area, _chair, At(-2f, -3f));
        Assert.IsTrue(check.ok, check.reason);
    }

    [Test]
    public void OffTheFloor_IsInvalid()
    {
        PlacementCheck check = PlacementValidator.Check(_area, _chair, At(-2f, 1f));
        Assert.IsFalse(check.ok);
        StringAssert.Contains("open ground", check.reason);
    }

    [Test]
    public void OverlappingSomething_IsInvalid()
    {
        PlacementCheck check = PlacementValidator.Check(_area, _chair, At(0f, -3f));
        Assert.IsFalse(check.ok);
        StringAssert.Contains("too close to the wall", check.reason);
    }

    [Test]
    public void CoveringAStandPoint_IsInvalid()
    {
        PlacementCheck check = PlacementValidator.Check(_area, _chair, At(3f, -3.2f));
        Assert.IsFalse(check.ok);
        StringAssert.Contains("the counter", check.reason);
    }

    [Test]
    public void CoveringTheSpawnPoint_IsInvalid()
    {
        PlacementCheck check = PlacementValidator.Check(_area, _chair, At(-3f, -3.2f));
        Assert.IsFalse(check.ok);
        StringAssert.Contains("where the gecko arrives", check.reason);
    }

    [Test]
    public void BlockingThePath_IsInvalidAndLeavesTheGridAsItWas()
    {
        // Fills the gap at the back: the counter's stand point can no longer be reached.
        PlaceableDefinition hedge = Definition(new Vector2(1.2f, 1.55f));
        Vector2 inGap = At(0f, -1.05f);
        PlacementCheck check = PlacementValidator.Check(_area, hedge, inGap);
        Assert.IsFalse(check.ok);
        StringAssert.Contains("block the path to the counter", check.reason);

        Assert.IsTrue(_area.Grid.IsWalkable(At(0f, -0.3f)), "The what-if block must be cleared after checking.");
    }

    [Test]
    public void MovingSomething_DoesNotCollideWithItself()
    {
        PlacedObject placed = BuildArea.CreateInstance(_chair, null);
        _created.Add(placed.gameObject);
        placed.Setup(_chair, 1);
        placed.transform.position = At(-2f, -3f);
        Physics2D.SyncTransforms();

        Assert.IsFalse(PlacementValidator.Check(_area, _chair, At(-2f, -3f)).ok, "Something else can't go on top of it.");
        Assert.IsTrue(PlacementValidator.Check(_area, _chair, At(-2f, -3f), placed).ok, "But it can stay where it is.");
    }

    private PlaceableDefinition Definition(Vector2 footprint)
    {
        var definition = ScriptableObject.CreateInstance<PlaceableDefinition>();
        definition.id = "placeable.test";
        definition.displayName = "Test thing";
        definition.footprintSize = footprint;
        _created.Add(definition);
        return definition;
    }

    private static Vector2 At(float x, float y) => Origin + new Vector2(x, y);

    private GameObject Make(string name)
    {
        var created = new GameObject(name);
        _created.Add(created);
        return created;
    }

    private static void SetValue(Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
