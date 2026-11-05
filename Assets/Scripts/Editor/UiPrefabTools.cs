using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Bush > Create UI Prefabs: saves the HUD, forage screen and menu board's default layouts as prefabs in
/// Assets/Resources/UI, to restyle in the Editor. The game uses the prefab when there is one, and builds
/// the default layout in code when there isn't. Asks before replacing a prefab, since that loses its edits.
/// The rounded panel and circle are drawn in code, which a prefab can't keep, so they're saved as images first.
/// Text gets its font by job: titles Caveat Brush, buttons and numbers Grandstander, notes from characters
/// Mynerve, everything else Pangolin. Tools > Bush > Rebuild Forage Screen Prefab and Rebuild Menu Board Prefab
/// replace only that one.
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

        Dictionary<Sprite, Sprite> sprites = SavedSprites();
        var made = new List<string>();
        Save(HudView.ResourcePath, () => HudView.Build(null).gameObject, sprites, made);
        Save(ForagePopup.ResourcePath, BuildForageScreen, sprites, made);
        Save(CafePopup.ResourcePath, BuildMenuBoard, sprites, made);
        Finish(made);
    }

    [MenuItem("Tools/Bush/Rebuild Menu Board Prefab", priority = 45)]
    public static void RebuildMenuBoard()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(SpriteFolder);
        AssetDatabase.Refresh();
        var made = new List<string>();
        Save(CafePopup.ResourcePath, BuildMenuBoard, SavedSprites(), made);
        Finish(made);
    }

    [MenuItem("Tools/Bush/Rebuild Forage Screen Prefab", priority = 43)]
    public static void RebuildForageScreen()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(SpriteFolder);
        AssetDatabase.Refresh();
        var made = new List<string>();
        Save(ForagePopup.ResourcePath, BuildForageScreen, SavedSprites(), made);
        Finish(made);
    }

    /// <summary>Replaces only the trip's scene in the forage prefab, keeping any edits to the other two steps.</summary>
    [MenuItem("Tools/Bush/Rebuild Forage Trip Scene", priority = 44)]
    public static void RebuildForageTrip()
    {
        string path = $"Assets/Resources/{ForagePopup.ResourcePath}.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var popup = root.GetComponent<ForagePopup>();
            popup.RebuildOutStep();
            popup.UseArt(LoadSprite("Assets/Images/Backgrounds/Background.png"), LoadSprite("Assets/Images/Characters/Gecko.png"),
                LoadSprite("Assets/Images/Placeables/Bush.png"));
            UseFonts(popup.OutStep);
            UseSavedSprites(popup.OutStep, SavedSprites());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Finish(new List<string> { path });
    }

    private static Dictionary<Sprite, Sprite> SavedSprites() => new()
    {
        [GeneratedSprites.RoundedPanel] = SaveSprite(GeneratedSprites.RoundedPanel, "RoundedPanel"),
        [GeneratedSprites.Circle] = SaveSprite(GeneratedSprites.Circle, "Circle")
    };

    private static void Finish(List<string> made)
    {
        AssetDatabase.SaveAssets();
        Debug.Log(made.Count > 0 ? $"[UI] Saved: {string.Join(", ", made)}. Edit them in Assets/Resources/UI." : "[UI] Nothing changed.");
    }

    private static GameObject BuildForageScreen()
    {
        ForagePopup popup = ForagePopup.BuildDefault(null);
        popup.UseArt(LoadSprite("Assets/Images/Backgrounds/Background.png"), LoadSprite("Assets/Images/Characters/Gecko.png"),
            LoadSprite("Assets/Images/Placeables/Bush.png"));
        return popup.gameObject;
    }

    private static GameObject BuildMenuBoard()
    {
        CafePopup popup = CafePopup.BuildDefault(null);
        popup.UseArt(LoadSprite("Assets/Images/Backgrounds/Background (1).png"), LoadSprite("Assets/Images/UI/Panel.png"),
            LoadSprite("Assets/Images/Placeables/Cafe sign.png"), LoadSprite("Assets/Images/UI/Icons/Koru-icon.png"));
        return popup.gameObject;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    /// <summary>Gives each piece of text the font for its job, going by what it's called and what it's on.</summary>
    private static void UseFonts(GameObject root)
    {
        TMP_FontAsset title = LoadFont("Assets/Fonts/CaveatBrush-Regular SDF.asset");
        TMP_FontAsset body = LoadFont("Assets/Fonts/Pangolin-Regular SDF.asset");
        TMP_FontAsset bold = LoadFont("Assets/Fonts/Grandstander/Grandstander-Bold SDF.asset");
        TMP_FontAsset note = LoadFont("Assets/Fonts/Mynerve-Regular SDF.asset");
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string name = text.gameObject.name;
            bool onButton = text.transform.parent && text.transform.parent.GetComponent<Button>();
            TMP_FontAsset font = name is "Title" or "OutText" or "SignTitle" ? title
                : name == "Nudge" ? note
                : name is "Letter" or "BadgeText" or "DishName" or "Heading" || onButton ? bold
                : body;
            if (!font) continue;
            text.font = font;
            text.fontStyle = FontStyles.Normal;
        }
    }

    private static TMP_FontAsset LoadFont(string path) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

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
            UseFonts(root);
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
