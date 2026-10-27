using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor helpers for build mode:
/// - Tools > Bush > Create Placeholder Placeables: a simple prefab for every placeable that has none.
/// - Tools > Bush > Set Up Build Mode In This Scene: session, HUD and BuildArea, and turns the props
///   already standing in World/Static into the area's starting layout so they can be moved.
/// </summary>
public static class BuildModeTools
{
    private const string PrefabFolder = "Assets/Prefabs/Placeables";
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";
    private const string InputActionsPath = "Assets/Input/BushInput.inputactions";
    private const string PlaceholderSpritePath = "Assets/Images/WalkTestSquareBottom.png";

    // Placeholder look per placeable: visual height and colour. Anything else gets a default.
    private static readonly Dictionary<string, (float height, Color color)> Looks = new()
    {
        ["placeable.chair"] = (1.1f, new Color(0.62f, 0.44f, 0.28f)),
        ["placeable.cafe_table"] = (0.9f, new Color(0.72f, 0.55f, 0.36f)),
        ["placeable.bench"] = (0.7f, new Color(0.5f, 0.36f, 0.22f)),
        ["placeable.potted_fern"] = (1.3f, new Color(0.35f, 0.58f, 0.3f)),
        ["placeable.flower_barrel"] = (0.9f, new Color(0.78f, 0.42f, 0.45f)),
        ["placeable.lantern_post"] = (2.0f, new Color(0.95f, 0.8f, 0.4f)),
    };

    [MenuItem("Tools/Bush/Create Placeholder Placeables", priority = 30)]
    public static void CreatePlaceholderPlaceables()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database)
        {
            EditorUtility.DisplayDialog("Placeholder placeables", "Run Tools > Bush > Import Design Data first.", "OK");
            return;
        }

        EnsureFolder(PrefabFolder);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        int created = 0;
        foreach (PlaceableDefinition placeable in database.placeables.Where(p => p && !p.prefab))
        {
            GameObject root = BuildPlaceholder(placeable, sprite);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{placeable.id}.prefab");
            placeable.prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            EditorUtility.SetDirty(placeable);
            created++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log(created == 0 ? "[Bush] Every placeable already has a prefab." : $"[Bush] Created {created} placeholder placeable prefab(s) in {PrefabFolder}.");
    }

    /// <summary>The prop pattern: root at the floor contact point (footprint set at runtime), Visual child, SeatPoints.</summary>
    private static GameObject BuildPlaceholder(PlaceableDefinition placeable, Sprite sprite)
    {
        (float height, Color color) look = Looks.TryGetValue(placeable.id, out var known) ? known : (1f, new Color(0.6f, 0.5f, 0.4f));
        var root = new GameObject(placeable.id) { layer = LayerMask.NameToLayer("Obstacle") };
        var footprint = root.AddComponent<BoxCollider2D>();
        footprint.size = placeable.footprintSize;
        footprint.offset = new Vector2(0f, placeable.footprintSize.y * 0.5f);
        root.AddComponent<PlacedObject>();

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(Mathf.Max(0.3f, placeable.footprintSize.x), look.height, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite ? sprite : null;
        renderer.color = look.color;
        renderer.sortingLayerName = "World";
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // Seats spread across the width, at sitting height.
        for (int i = 0; i < placeable.seats; i++)
        {
            var seat = new GameObject($"Seat{i + 1}");
            seat.transform.SetParent(root.transform, false);
            float x = placeable.seats == 1 ? 0f : Mathf.Lerp(-0.3f, 0.3f, i / (float)(placeable.seats - 1)) * placeable.footprintSize.x;
            seat.transform.localPosition = new Vector3(x, look.height * 0.45f, 0f);
            seat.AddComponent<SeatPoint>();
        }
        return root;
    }

    [MenuItem("Tools/Bush/Set Up Build Mode In This Scene", priority = 41)]
    public static void SetUpBuildMode()
    {
        GameObject world = Find("World");
        GameObject systems = Find("Systems");
        if (!world || !world.GetComponent<WalkableGrid>() || !systems)
        {
            EditorUtility.DisplayDialog("Set up build mode",
                "This needs World (with a WalkableGrid) and Systems. Run Tools > Bush > Set Up Area Scene first. Nothing was changed.", "OK");
            return;
        }

        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database)
        {
            EditorUtility.DisplayDialog("Set up build mode", "Run Tools > Bush > Import Design Data first, so the GameDatabase exists. Nothing was changed.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Set Up Build Mode");
        int group = Undo.GetCurrentGroup();
        var log = new List<string>();

        // The session (one per game, created by whichever scene starts first) and the HUD buttons.
        var bootstrap = systems.GetComponent<GameSessionBootstrap>();
        if (!bootstrap) { bootstrap = Undo.AddComponent<GameSessionBootstrap>(systems); log.Add("Added GameSessionBootstrap to Systems."); }
        SetField(bootstrap, "database", database);
        if (!systems.GetComponent<GameHud>()) { Undo.AddComponent<GameHud>(systems); log.Add("Added GameHud to Systems."); }

        // The build area on World, placing into World/Placed.
        Transform placed = world.transform.Find("Placed");
        if (!placed)
        {
            var placedObject = new GameObject("Placed");
            Undo.RegisterCreatedObjectUndo(placedObject, "Create Placed");
            placedObject.transform.SetParent(world.transform, false);
            placed = placedObject.transform;
        }
        var area = world.GetComponent<BuildArea>();
        if (!area) { area = Undo.AddComponent<BuildArea>(world); log.Add("Added BuildArea to World."); }
        SetField(area, "placedParent", placed);
        var mode = world.GetComponent<BuildMode>();
        if (!mode) mode = Undo.AddComponent<BuildMode>(world);
        SetField(mode, "actions", AssetDatabase.LoadAssetAtPath<Object>(InputActionsPath));

        // Props standing in World/Static that match a placeable become the starting layout.
        Transform statics = world.transform.Find("Static");
        if (statics)
        {
            var serialized = new SerializedObject(area);
            SerializedProperty layout = serialized.FindProperty("startingLayout");
            foreach (Transform prop in statics.Cast<Transform>().ToList())
            {
                PlaceableDefinition match = MatchPlaceable(database, prop.name);
                if (!match) continue;
                int index = layout.arraySize;
                layout.InsertArrayElementAtIndex(index);
                SerializedProperty entry = layout.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("placeable").objectReferenceValue = match;
                entry.FindPropertyRelative("position").vector2Value = prop.position;
                entry.FindPropertyRelative("flipped").boolValue = prop.lossyScale.x < 0f;
                log.Add($"{prop.name} is now a movable {match.id} in the starting layout (the scene object was removed).");
                Undo.DestroyObjectImmediate(prop.gameObject);
            }
            serialized.ApplyModifiedProperties();
        }

        if (database.placeables.Any(p => p && !p.prefab)) log.Add("Some placeables have no prefab: run Tools > Bush > Create Placeholder Placeables.");

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(world.scene);
        Debug.Log("[Bush] Build mode set up:\n- " + (log.Count > 0 ? string.Join("\n- ", log) : "everything was already there."));
    }

    /// <summary>Chair, Chair2 -> placeable.chair; Plant -> placeable.potted_fern; otherwise by name.</summary>
    private static PlaceableDefinition MatchPlaceable(GameDatabase database, string objectName)
    {
        string name = new string(objectName.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        if (name == "plant") name = "pottedfern";
        return database.placeables.FirstOrDefault(p => p && p.id.Replace("placeable.", "").Replace("_", "") == name);
    }

    private static void SetField(Object target, string field, Object value)
    {
        if (!target || !value) return;
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null || property.objectReferenceValue == value) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static GameObject Find(string name) =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == name);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
