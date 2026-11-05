using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Bush > Set Up Story Characters In This Scene: brings the scene's characters in line with the NPC assets. Characters
/// retired from the story hand their spot on to a new one (Kererū to Kunekune, Kea to Kōwhai, Wētā to Gus), anyone still
/// missing is copied from Snail, and everyone with a portrait wears it. Safe to run again. Save the scene afterwards.
/// </summary>
public static class StoryCharacterTools
{
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";

    private static readonly Dictionary<string, string> ReplacedBy = new()
    {
        ["Kereru"] = "npc.kunekune",
        ["Kea"] = "npc.kowhai",
        ["Weta"] = "npc.gus"
    };

    [MenuItem("Tools/Bush/Set Up Story Characters In This Scene", priority = 41)]
    public static void SetUp()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database)
        {
            Debug.LogError("[Bush] Run Tools > Bush > Import Design Data first.");
            return;
        }

        var log = new List<string>();
        List<Npc> inScene = Object.FindObjectsByType<Npc>(FindObjectsInactive.Include).ToList();

        // Retired characters hand over their spot.
        foreach (Npc npc in inScene.Where(n => !n.Definition).ToList())
        {
            NpcDefinition next = ReplacedBy.TryGetValue(npc.name, out string id) ? database.npcs.Find(d => d && d.id == id) : null;
            if (next && !inScene.Exists(n => n.Definition == next))
            {
                Undo.RecordObject(npc.gameObject, "Set up story characters");
                log.Add($"{npc.name} is now {next.displayName}");
                npc.name = next.displayName;
                Assign(npc, next);
            }
            else
            {
                log.Add($"Removed {npc.name}, who's no longer in the story");
                inScene.Remove(npc);
                Undo.DestroyObjectImmediate(npc.gameObject);
            }
        }

        // Anyone missing, copied from someone who's already set up.
        Npc template = inScene.Find(n => n.Definition && n.Definition.id == "npc.snail") ?? inScene.Find(n => n.Definition);
        float nudge = 1.5f;
        foreach (NpcDefinition definition in database.npcs.Where(d => d && !inScene.Exists(n => n.Definition == d)))
        {
            if (!template)
            {
                log.Add($"Couldn't add {definition.displayName}: there's nobody to copy");
                continue;
            }
            Npc copy = Object.Instantiate(template, template.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy.gameObject, "Set up story characters");
            copy.name = definition.displayName;
            copy.transform.position = template.transform.position + new Vector3(nudge, 0f, 0f);
            nudge += 1.5f;
            Assign(copy, definition);
            inScene.Add(copy);
            log.Add($"Added {definition.displayName}, next to {template.name}: move them where they should stand");
        }

        foreach (Npc npc in inScene.Where(n => n.Definition)) WearPortrait(npc);

        if (!Application.isBatchMode) EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Bush] Story characters: {(log.Count == 0 ? "nothing to change." : string.Join(". ", log) + ".")} Save the scene to keep this.");
    }

    private static void Assign(Npc npc, NpcDefinition definition)
    {
        var serialized = new SerializedObject(npc);
        serialized.FindProperty("npc").objectReferenceValue = definition;
        serialized.ApplyModifiedProperties();
    }

    /// <summary>Puts their portrait on their art, at the same height as before. Anyone with no portrait keeps what they have.</summary>
    private static void WearPortrait(Npc npc)
    {
        Sprite portrait = npc.Definition.portrait;
        Transform visual = npc.transform.Find("Visual");
        var renderer = visual ? visual.GetComponent<SpriteRenderer>() : null;
        if (!portrait || !renderer || renderer.sprite == portrait) return;

        Undo.RecordObject(renderer, "Set up story characters");
        Undo.RecordObject(visual, "Set up story characters");
        float before = renderer.sprite ? renderer.sprite.bounds.size.y * Mathf.Abs(visual.localScale.y) : 1.6f;
        renderer.sprite = portrait;
        float factor = before / Mathf.Max(0.001f, portrait.bounds.size.y * Mathf.Abs(visual.localScale.y));
        visual.localScale = new Vector3(visual.localScale.x * factor, visual.localScale.y * factor, visual.localScale.z);
    }
}
