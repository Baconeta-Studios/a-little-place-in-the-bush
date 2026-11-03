using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Bush > Create UI Prefabs: saves the HUD and the forage screen's default layouts as prefabs in
/// Assets/Resources/UI, to restyle in the Editor. The game uses the prefab when there is one, and builds
/// the default layout in code when there isn't. Asks before replacing a prefab, since that loses its edits.
/// The rounded panel and circle are drawn in code, which a prefab can't keep, so they're saved as images first.
/// </summary>
public static class UiPrefabTools
{
    private const string PrefabFolder = "Assets/Resources/UI";
    private const string SpriteFolder = "Assets/Images/UI/Generated";

    [MenuItem("Tools/Bush/Create UI Prefabs", priority = 42)]
    public static void CreateUiPrefabs()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(SpriteFolder);
        AssetDatabase.Refresh();

        var sprites = new Dictionary<Sprite, Sprite>
        {
            [GeneratedSprites.RoundedPanel] = SaveSprite(GeneratedSprites.RoundedPanel, "RoundedPanel"),
            [GeneratedSprites.Circle] = SaveSprite(GeneratedSprites.Circle, "Circle")
        };

        var made = new List<string>();
        Save(HudView.ResourcePath, () => HudView.Build(null).gameObject, sprites, made);
        Save(ForagePopup.ResourcePath, () => ForagePopup.BuildDefault(null).gameObject, sprites, made);
        AssetDatabase.SaveAssets();

        Debug.Log(made.Count > 0 ? $"[UI] Saved: {string.Join(", ", made)}. Edit them in Assets/Resources/UI." : "[UI] Nothing changed.");
    }

    private static void Save(string resourcePath, System.Func<GameObject> build, Dictionary<Sprite, Sprite> sprites, List<string> made)
    {
        string path = $"Assets/Resources/{resourcePath}.prefab";
        if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace UI prefab?",
                $"{path} already exists. Replacing it loses any edits you've made to it.", "Replace", "Keep mine"))
            return;

        GameObject root = build();
        try
        {
            root.SetActive(true); // saved turned on, so it shows when you open the prefab
            UseSavedSprites(root, sprites);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            made.Add(path);
        }
        finally
        {
            Object.DestroyImmediate(root); // never leave a half-built copy in the open scene
        }
    }

    /// <summary>Swaps every link to a sprite drawn in code for its saved image, on every component.</summary>
    private static void UseSavedSprites(GameObject root, Dictionary<Sprite, Sprite> sprites)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue is Sprite sprite && sprites.TryGetValue(sprite, out Sprite saved))
                    property.objectReferenceValue = saved;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Saves a sprite drawn in code as an image, keeping its slicing. Leaves one that's already there alone, so it can be painted over.</summary>
    private static Sprite SaveSprite(Sprite sprite, string name)
    {
        string path = $"{SpriteFolder}/{name}.png";
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, sprite.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
            importer.spriteBorder = sprite.border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
