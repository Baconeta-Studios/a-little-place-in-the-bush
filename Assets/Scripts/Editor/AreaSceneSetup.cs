using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Tools > Bush > Set Up Area Scene: adds the movement, interaction and area components to the
/// open scene, following the layout of the Cafe greybox (World grid, Floor/WalkableArea,
/// Characters/Gecko, Static props, Markers, Systems). Safe to run again: it only adds what's
/// missing. Everything is undoable.
/// </summary>
public static class AreaSceneSetup
{
    private const string InputActionsPath = "Assets/Input/BushInput.inputactions";
    private const string PopupCanvasPath = "Assets/Prefabs/UI/Popups/PopupCanvas.prefab";
    private const string PlaceholderSpritePath = "Assets/Images/WalkTestSquareBottom.png";

    [MenuItem("Tools/Bush/Set Up Area Scene", priority = 40)]
    public static void SetUp()
    {
        var log = new List<string>();
        Undo.SetCurrentGroupName("Set Up Area Scene");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject world = Find("World");
        GameObject walkableArea = Find("World/Floor/WalkableArea");
        GameObject gecko = Find("World/Characters/Gecko");
        GameObject markers = Find("World/Markers") ?? Create("Markers", world ? world.transform : null, Vector3.zero, log);
        GameObject statics = Find("World/Static") ?? Create("Static", world ? world.transform : null, Vector3.zero, log);
        GameObject systems = Find("Systems") ?? Create("Systems", null, Vector3.zero, log);
        Camera camera = Camera.main;

        if (!world || !walkableArea || !gecko)
        {
            EditorUtility.DisplayDialog("Set Up Area Scene",
                "This needs World (with a Grid), World/Floor/WalkableArea and World/Characters/Gecko, like the Cafe scene.", "OK");
            return;
        }

        // Walkable grid and pathfinding on the World grid.
        var grid = Ensure<WalkableGrid>(world, log);
        var gridObject = new SerializedObject(grid);
        if (!gridObject.FindProperty("walkableArea").objectReferenceValue)
            gridObject.FindProperty("walkableArea").objectReferenceValue = walkableArea.GetComponent<Collider2D>();
        if (gridObject.FindProperty("obstacleMask").intValue == 0)
            gridObject.FindProperty("obstacleMask").intValue = LayerMask.GetMask("Obstacle");
        gridObject.ApplyModifiedProperties();
        Ensure<GridPathfinder>(world, log);

        // The gecko.
        SetLayer(gecko, "Player");
        var mover = Ensure<CharacterMover>(gecko, log);
        Transform visual = gecko.transform.Find("Visual");
        if (visual) SetReference(mover, "visual", visual);
        Ensure<Interactor>(gecko, log);
        var input = Ensure<PlayerPointerInput>(gecko, log);
        SetReference(input, "actions", AssetDatabase.LoadAssetAtPath<Object>(InputActionsPath));
        if (camera) SetReference(input, "worldCamera", camera);
        var inputObject = new SerializedObject(input);
        if (inputObject.FindProperty("interactableMask").intValue == 0)
        {
            inputObject.FindProperty("interactableMask").intValue = LayerMask.GetMask("Interactable");
            inputObject.ApplyModifiedProperties();
        }

        // Systems: area info and cheats.
        var area = Ensure<AreaInfo>(systems, log);
        var areaObject = new SerializedObject(area);
        string sceneName = gecko.scene.name;
        if (areaObject.FindProperty("displayName").stringValue == "The Cafe" && sceneName != "Cafe")
            areaObject.FindProperty("displayName").stringValue = $"The {ObjectNames.NicifyVariableName(sceneName)}";
        if (sceneName == "Cafe") areaObject.FindProperty("isHub").boolValue = true;
        areaObject.ApplyModifiedProperties();
        Ensure<GameplayCheats>(systems, log);

        // A default spawn point where the gecko stands now.
        if (!Object.FindAnyObjectByType<SpawnPoint>())
        {
            GameObject spawn = Create("Spawn_Default", markers.transform, gecko.transform.position, log);
            var spawnPoint = Undo.AddComponent<SpawnPoint>(spawn);
            var spawnObject = new SerializedObject(spawnPoint);
            spawnObject.FindProperty("isDefault").boolValue = true;
            spawnObject.ApplyModifiedProperties();
        }

        // A placeholder counter to test click-to-interact, if the scene has nothing interactive yet.
        if (!Object.FindAnyObjectByType<Interactable>())
            CreateInteractable<Interactable>("Counter", statics.transform, new Vector2(2.5f, -1.4f), new Vector2(2.4f, 1.1f), "Look at the counter", log);

        // A placeholder exit on the left edge, per the GDD (garden to the left of the cafe).
        if (!Object.FindAnyObjectByType<AreaExit>())
        {
            AreaExit exit = CreateInteractable<AreaExit>("Exit_Garden", markers.transform, new Vector2(-8.4f, -2.6f), new Vector2(1.2f, 1.6f), null, log);
            exit.transform.Find("StandPoint").localPosition = new Vector3(0.9f, -0.4f, 0f);
            log.Add("Exit_Garden needs its Target Scene once the Garden scene exists.");
        }

        // The template pause menu (Escape, the pause button, auto-pause on tab switch).
        if (!Object.FindAnyObjectByType<UI.Popups.PauseInput>())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupCanvasPath);
            if (prefab)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add PopupCanvas");
                log.Add("Added PopupCanvas (pause menu).");
            }
        }

        if (camera && camera.GetComponent("Physics2DRaycaster"))
            log.Add("Note: the camera's Physics2DRaycaster isn't needed by PlayerPointerInput (it can stay or go).");

        grid.Rebuild();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(gecko.scene);
        Selection.activeGameObject = world;

        Debug.Log("[Bush] Area scene set up:\n- " + (log.Count > 0 ? string.Join("\n- ", log) : "nothing was missing."));
    }

    private static T CreateInteractable<T>(string name, Transform parent, Vector2 position, Vector2 size, string prompt, List<string> log)
        where T : Interactable
    {
        // Parent at the floor contact point with an Obstacle footprint, child Visual holds the scale.
        GameObject root = Create(name, parent, position, log);
        SetLayer(root, "Obstacle");
        var footprint = Undo.AddComponent<BoxCollider2D>(root);
        footprint.size = new Vector2(size.x, 0.4f);
        footprint.offset = new Vector2(0f, 0.2f);

        GameObject visual = Create("Visual", root.transform, Vector3.zero, null);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = Undo.AddComponent<SpriteRenderer>(visual);
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        renderer.color = new Color(0.62f, 0.45f, 0.3f);
        renderer.sortingLayerName = "World";
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;

        // What the player clicks: covers the whole visual, on the Interactable layer.
        GameObject click = Create("Click", root.transform, Vector3.zero, null);
        click.transform.localPosition = Vector3.zero;
        SetLayer(click, "Interactable");
        var clickCollider = Undo.AddComponent<BoxCollider2D>(click);
        clickCollider.isTrigger = true;
        clickCollider.size = size;
        clickCollider.offset = new Vector2(0f, size.y * 0.5f);

        GameObject stand = Create("StandPoint", root.transform, Vector3.zero, null);
        stand.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        Undo.AddComponent<StandPoint>(stand);

        var interactable = Undo.AddComponent<T>(root);
        if (prompt != null)
        {
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("prompt").stringValue = prompt;
            serialized.ApplyModifiedProperties();
        }
        Undo.AddComponent<InteractionPrompt>(root);
        log.Add($"Created placeholder {name} (move it, and its StandPoint, where you like).");
        return interactable;
    }

    private static T Ensure<T>(GameObject target, List<string> log) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component) return component;
        log.Add($"Added {typeof(T).Name} to {target.name}.");
        return Undo.AddComponent<T>(target);
    }

    private static void SetReference(Component component, string property, Object value)
    {
        if (!value) return;
        var serialized = new SerializedObject(component);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null || field.objectReferenceValue) return;
        field.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void SetLayer(GameObject target, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || target.layer == layer) return;
        Undo.RecordObject(target, "Set layer");
        target.layer = layer;
    }

    private static GameObject Create(string name, Transform parent, Vector3 position, List<string> log)
    {
        var created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        if (parent) created.transform.SetParent(parent, false);
        created.transform.position = position;
        return created;
    }

    /// <summary>Finds a root-relative path like "World/Floor/WalkableArea" in the active scene, including inactive objects.</summary>
    private static GameObject Find(string path)
    {
        string[] parts = path.Split('/');
        GameObject root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == parts[0]);
        if (!root || parts.Length == 1) return root;
        Transform child = root.transform.Find(string.Join("/", parts.Skip(1)));
        return child ? child.gameObject : null;
    }
}
