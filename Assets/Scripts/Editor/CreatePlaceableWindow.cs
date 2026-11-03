using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bush.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Right-click a sprite in the Project window: Bush > Create Placeable. Makes everything a new placeable
/// needs and links it up: the item (with the sprite as its icon), the placeable, its prefab, the database,
/// and entries in Design/design-data.json so importing keeps it. Pick a base to copy its settings and
/// its prefab (seat points and all), or leave it empty for plain defaults to fill in later.
/// </summary>
public class CreatePlaceableWindow : EditorWindow
{
    private const string ItemFolder = "Assets/Data/Items";
    private const string PlaceableFolder = "Assets/Data/Placeables";
    private const string PrefabFolder = "Assets/Prefabs/Placeables";
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";
    private const float DefaultWidth = 1.5f;

    private Sprite _sprite;
    private string _name = "";
    private string _description = "";
    private PlaceableDefinition _base;
    private float _width = DefaultWidth;
    private List<PlaceableDefinition> _bases;

    [MenuItem("Assets/Bush/Create Placeable...", true)]
    private static bool CanOpen() => Selection.activeObject is Sprite or Texture2D;

    [MenuItem("Assets/Bush/Create Placeable...", false, 20)]
    private static void Open()
    {
        Sprite sprite = SpriteFrom(Selection.activeObject);
        if (!sprite)
        {
            EditorUtility.DisplayDialog("Create Placeable", "Couldn't get a sprite from that. Check its Texture Type in the Inspector.", "OK");
            return;
        }

        var window = GetWindow<CreatePlaceableWindow>(true, "Create Placeable");
        window.minSize = new Vector2(400f, 270f);
        window._sprite = sprite;
        window._name = ObjectNames.NicifyVariableName(sprite.name);
        window._description = "";
        window._base = null;
        window._width = DefaultWidth;
        window._bases = AssetDatabase.FindAssets("t:PlaceableDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<PlaceableDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(p => p).OrderBy(p => p.displayName).ToList();
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Sprite", _sprite, typeof(Sprite), false);
        _name = EditorGUILayout.TextField("Name", _name);

        var labels = new List<string> { "None: start fresh" };
        labels.AddRange(_bases.Select(p => p.displayName));
        int current = _base ? _bases.IndexOf(_base) + 1 : 0;
        int picked = EditorGUILayout.Popup(new GUIContent("Base on", "Copies its settings and its prefab, including seat points."), current, labels.ToArray());
        if (picked != current)
        {
            _base = picked == 0 ? null : _bases[picked - 1];
            _width = _base ? WidthOf(_base) : DefaultWidth;
        }
        _width = EditorGUILayout.FloatField(new GUIContent("Width on screen", "In world units. The art is scaled to fit, standing on the ground line."), _width);

        EditorGUILayout.LabelField("Description (optional)");
        _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(40f));

        string slug = Slug(_name);
        string problem = _sprite ? Problem(slug) : "Choose a sprite.";
        EditorGUILayout.Space();
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
        else
            EditorGUILayout.HelpBox(_base
                ? $"Makes item.{slug} and placeable.{slug}, with {_base.displayName}'s settings and prefab."
                : $"Makes item.{slug} and placeable.{slug}, with plain defaults.", MessageType.Info);

        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Create", GUILayout.Height(30f)))
            {
                PlaceableDefinition made = Create(_sprite, _name, _base, _width, _description);
                Selection.activeObject = made;
                EditorGUIUtility.PingObject(made);
                Close();
            }
        }
    }

    private static string Problem(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return "Give it a name.";
        if (File.Exists($"{ItemFolder}/item.{slug}.asset") || File.Exists($"{PlaceableFolder}/placeable.{slug}.asset"))
            return $"There's already a placeable called {slug}.";
        return null;
    }

    /// <summary>Makes and links everything. Public so it can be run without the window.</summary>
    public static PlaceableDefinition Create(Sprite sprite, string displayName, PlaceableDefinition basedOn, float width, string description)
    {
        string slug = Slug(displayName);
        Directory.CreateDirectory(ItemFolder);
        Directory.CreateDirectory(PlaceableFolder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        ItemDefinition baseItem = basedOn ? basedOn.item : null;
        var item = CreateInstance<ItemDefinition>();
        item.id = $"item.{slug}";
        item.displayName = displayName;
        item.description = description ?? "";
        item.icon = sprite;
        item.category = ItemCategory.Placeable;
        item.rarity = baseItem ? baseItem.rarity : Rarity.Common;
        item.tags = baseItem ? new List<string>(baseItem.tags) : new List<string>();
        item.sellValue = baseItem ? baseItem.sellValue : 0;
        AssetDatabase.CreateAsset(item, $"{ItemFolder}/{item.id}.asset");

        var placeable = CreateInstance<PlaceableDefinition>();
        placeable.id = $"placeable.{slug}";
        placeable.displayName = displayName;
        placeable.description = description ?? "";
        placeable.item = item;
        placeable.placement = basedOn ? basedOn.placement : PlacementType.Floor;
        placeable.footprintSize = basedOn ? basedOn.footprintSize : new Vector2(Mathf.Max(0.2f, width * 0.8f), 0.3f);
        placeable.chainSpan = basedOn ? basedOn.chainSpan : 0f;
        placeable.canFlip = !basedOn || basedOn.canFlip;
        placeable.category = basedOn ? basedOn.category : "Decor";
        placeable.seats = basedOn ? basedOn.seats : 0;
        placeable.cosiness = basedOn ? basedOn.cosiness : 1;
        placeable.style = basedOn ? basedOn.style : "";
        placeable.prefab = BuildPrefab(sprite, placeable.id, basedOn, width);
        AssetDatabase.CreateAsset(placeable, $"{PlaceableFolder}/{placeable.id}.asset");
        AssetDatabase.SaveAssets();

        DesignDataTools.Collect();
        AddToDesignFile(item, placeable);
        Debug.Log($"[Bush] Created {placeable.id}. To get it in the game: a quest reward, or the cheat Build: give 3 of every placeable.", placeable);
        return placeable;
    }

    private static GameObject BuildPrefab(Sprite sprite, string id, PlaceableDefinition basedOn, float width)
    {
        string path = $"{PrefabFolder}/{id}.prefab";
        if (basedOn && basedOn.prefab)
        {
            GameObject copy = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(basedOn.prefab));
            try
            {
                FitArt(copy, sprite, width);
                copy.name = id;
                return PrefabUtility.SaveAsPrefabAsset(copy, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(copy);
            }
        }

        var root = new GameObject(id) { layer = LayerMask.NameToLayer("Obstacle") };
        root.AddComponent<PlacedObject>();
        FitArt(root, sprite, width);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        return saved;
    }

    /// <summary>Puts the sprite on the Visual child, scaled to the width, with its bottom on the ground line.</summary>
    private static void FitArt(GameObject root, Sprite sprite, float width)
    {
        Transform visual = root.transform.Find("Visual");
        if (!visual)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
        }
        var renderer = visual.GetComponent<SpriteRenderer>();
        if (!renderer)
        {
            renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "World";
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.drawMode = SpriteDrawMode.Simple;

        float scale = Mathf.Max(0.01f, width) / Mathf.Max(0.0001f, sprite.bounds.size.x);
        visual.localScale = new Vector3(scale, scale, 1f);
        visual.localPosition = new Vector3(-sprite.bounds.center.x * scale, -sprite.bounds.min.y * scale, 0f);
    }

    private static float WidthOf(PlaceableDefinition placeable)
    {
        Transform visual = placeable.prefab ? placeable.prefab.transform.Find("Visual") : null;
        var renderer = visual ? visual.GetComponent<SpriteRenderer>() : null;
        if (!renderer || !renderer.sprite) return DefaultWidth;
        return renderer.sprite.bounds.size.x * Mathf.Abs(visual.localScale.x);
    }

    private static Sprite SpriteFrom(Object selected)
    {
        if (selected is Sprite sprite) return sprite;
        string path = AssetDatabase.GetAssetPath(selected);
        sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite || AssetImporter.GetAtPath(path) is not TextureImporter importer) return sprite;

        // A plain texture: import it as a sprite.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>"Mossy Stone (big)" becomes "mossy_stone_big". Macrons go too: IDs stay plain.</summary>
    public static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (char c in (name ?? "").Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') slug.Append(c);
            else if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            else if (slug.Length > 0 && slug[^1] != '_') slug.Append('_');
        }
        return slug.ToString().Trim('_');
    }

    // ----- The design file -----

    /// <summary>Adds the new entries to the JSON by hand, so the rest of the file isn't reformatted.</summary>
    private static void AddToDesignFile(ItemDefinition item, PlaceableDefinition placeable)
    {
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Design", "design-data.json"));
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);

        var itemEntry = new DesignFile.Item
        {
            id = item.id, name = item.displayName, category = item.category.ToString(), rarity = item.rarity.ToString(),
            tags = new List<string>(item.tags), sellValue = item.sellValue, unique = false, description = item.description ?? "", teachesRecipe = ""
        };
        var placeableEntry = new DesignFile.Placeable
        {
            id = placeable.id, item = item.id, placement = placeable.placement.ToString(), footprintWidth = placeable.footprintSize.x,
            footprintDepth = placeable.footprintSize.y, chainSpan = placeable.chainSpan, canFlip = placeable.canFlip,
            category = placeable.category, seats = placeable.seats, cosiness = placeable.cosiness, style = placeable.style ?? ""
        };
        text = AppendToArray(text, "items", JsonUtility.ToJson(itemEntry, true));
        text = AppendToArray(text, "placeables", JsonUtility.ToJson(placeableEntry, true));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>Adds an object at the end of a top-level array, indented like the rest of the file.</summary>
    public static string AppendToArray(string json, string arrayName, string entry)
    {
        string newline = json.Contains("\r\n") ? "\r\n" : "\n";
        int start = json.IndexOf($"\"{arrayName}\": [", System.StringComparison.Ordinal);
        if (start < 0) throw new System.InvalidOperationException($"No {arrayName} in the design file.");
        int open = json.IndexOf('[', start);
        int close = MatchingBracket(json, open);

        // JsonUtility indents by four; this file by two, with entries two levels in.
        var lines = entry.Replace("\r\n", "\n").Split('\n').Select(line =>
        {
            int spaces = line.Length - line.TrimStart(' ').Length;
            return new string(' ', spaces / 2 + 4) + line.TrimStart(' ');
        });
        string block = string.Join(newline, lines);

        int end = close;
        while (end > open && char.IsWhiteSpace(json[end - 1])) end--;
        bool empty = end == open + 1;
        return json.Substring(0, end) + (empty ? "" : ",") + newline + block + newline + "  " + json.Substring(close);
    }

    private static int MatchingBracket(string json, int open)
    {
        int depth = 0;
        bool inString = false;
        for (int i = open; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == '[') depth++;
            else if (c == ']' && --depth == 0) return i;
        }
        throw new System.InvalidOperationException("The design file's brackets don't match.");
    }
}
