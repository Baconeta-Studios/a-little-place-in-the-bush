using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Bush > Placeable Shadows and Bases: every placeable in one list, with its art, the base that blocks walking
/// (green) and its shadow, so they can be tuned by eye. Fit Base To Art measures the bottom of the picture. A base is
/// saved to the asset and to Design/design-data.json, or the next import would put the old number back.
/// </summary>
public class PlaceableBasesWindow : EditorWindow
{
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";
    private const string DesignPath = "Design/design-data.json";

    private readonly List<PlaceableDefinition> _placeables = new();
    // How far each one's art sits off the prefab's centre. Measuring reads the art back off the GPU, which can't happen
    // while the window is drawing (it blanks the window), so it's done just after and remembered.
    private readonly Dictionary<PlaceableDefinition, float> _offCentre = new();
    private PlaceableDefinition _selected;
    private Vector2 _listScroll;

    [MenuItem("Tools/Bush/Placeable Shadows and Bases", priority = 32)]
    private static void Open() => GetWindow<PlaceableBasesWindow>("Shadows and Bases").minSize = new Vector2(760f, 460f);

    private void OnEnable() => Reload();

    private void Reload()
    {
        _placeables.Clear();
        _offCentre.Clear();
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (database) _placeables.AddRange(database.placeables.Where(p => p && p.placement == Bush.Core.PlacementType.Floor).OrderBy(p => p.id));
        if (!_selected && _placeables.Count > 0) _selected = _placeables[0];
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selected) DrawSelected(_selected);
                else EditorGUILayout.HelpBox("Nothing to show. Run Tools > Bush > Import Design Data first.", MessageType.Info);
            }
        }
    }

    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (PlaceableDefinition placeable in _placeables)
            {
                GUI.backgroundColor = placeable == _selected ? new Color(0.6f, 0.85f, 0.6f) : Color.white;
                if (GUILayout.Button(placeable.id.Replace("placeable.", ""), GUILayout.Height(26f)))
                {
                    _selected = placeable;
                    GUI.FocusControl(null);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Fit every base to its art", GUILayout.Height(28f)) &&
                EditorUtility.DisplayDialog("Fit every base", "This replaces the base size of every floor placeable with one measured from its art.", "Fit them", "Cancel"))
                EditorApplication.delayCall += () =>
                {
                    FitAll();
                    _offCentre.Clear();
                    Repaint();
                };
        }
    }

    private void DrawSelected(PlaceableDefinition placeable)
    {
        Rect area = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawPreview(area, placeable);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Base (blocks walking)", EditorStyles.boldLabel);
        Vector2 footprint = EditorGUILayout.Vector2Field("Width, depth", placeable.footprintSize);
        if (EditorGUI.EndChangeCheck()) SetFootprint(placeable, footprint);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fit Base To Art"))
                EditorApplication.delayCall += () =>
                {
                    if (TryMeasureBase(placeable, out Vector2 fitted, out _)) SetFootprint(placeable, fitted);
                    Repaint();
                };
            if (GUILayout.Button("Select Prefab") && placeable.prefab) Selection.activeObject = placeable.prefab;
        }
        if (!_offCentre.TryGetValue(placeable, out float offCentre))
        {
            _offCentre[placeable] = 0f; // asked for once
            EditorApplication.delayCall += () =>
            {
                if (this && TryMeasureBase(placeable, out _, out float measured)) _offCentre[placeable] = measured;
                if (this) Repaint();
            };
        }
        else if (Mathf.Abs(offCentre) > 0.1f)
            EditorGUILayout.HelpBox($"The art sits {offCentre:+0.00;-0.00} from the prefab's centre. Bases are centred, so move the Visual child by that much to line them up.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Shadow", EditorStyles.boldLabel);
        bool on = EditorGUILayout.Toggle("Has a shadow", placeable.groundShadow);
        Vector2 size = EditorGUILayout.Vector2Field("Size (0 follows the base)", placeable.shadowSize);
        Vector2 offset = EditorGUILayout.Vector2Field("Move by", placeable.shadowOffset);
        float strength = EditorGUILayout.Slider("Strength", placeable.shadowStrength, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(placeable, "Change shadow");
            placeable.groundShadow = on;
            placeable.shadowSize = size;
            placeable.shadowOffset = offset;
            placeable.shadowStrength = strength;
            EditorUtility.SetDirty(placeable);
            AssetDatabase.SaveAssetIfDirty(placeable);
        }
    }

    // ----- The picture -----

    private static void DrawPreview(Rect area, PlaceableDefinition placeable)
    {
        EditorGUI.DrawRect(area, new Color(0.42f, 0.36f, 0.27f));
        var parts = ArtParts(placeable).ToList();

        Rect footprint = PlacedObject.FootprintAt(placeable, Vector2.zero);
        Vector2 shadowSize = placeable.ShadowSize, shadowCentre = placeable.ShadowCentre;
        Rect shadow = new(shadowCentre - shadowSize * 0.5f, shadowSize);
        Rect bounds = footprint;
        foreach ((Sprite _, Rect rect, bool _) in parts) bounds = Union(bounds, rect);
        bounds = Union(bounds, shadow);

        float zoom = Mathf.Min((area.width - 40f) / bounds.width, (area.height - 40f) / bounds.height);
        Vector2 middle = area.center;
        Rect ToScreen(Rect world) => new(
            middle.x + (world.xMin - bounds.center.x) * zoom,
            middle.y - (world.yMax - bounds.center.y) * zoom,
            world.width * zoom, world.height * zoom);

        if (placeable.groundShadow)
        {
            Color before = GUI.color;
            GUI.color = new Color(0.1f, 0.08f, 0.05f, placeable.shadowStrength);
            GUI.DrawTexture(ToScreen(shadow), GeneratedSprites.SoftShadow.texture, ScaleMode.StretchToFill, true);
            GUI.color = before;
        }

        foreach ((Sprite sprite, Rect rect, bool flip) in parts)
        {
            Rect uv = sprite.textureRect;
            var coords = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height);
            if (flip) coords = new Rect(coords.xMax, coords.y, -coords.width, coords.height);
            GUI.DrawTextureWithTexCoords(ToScreen(rect), sprite.texture, coords, true);
        }

        Rect baseOnScreen = ToScreen(footprint);
        EditorGUI.DrawRect(baseOnScreen, new Color(0.3f, 0.9f, 0.35f, 0.4f));
        GUI.Label(new Rect(area.x + 8f, area.y + 6f, area.width, 20f), $"{placeable.displayName}: base {placeable.footprintSize.x:0.##} x {placeable.footprintSize.y:0.##}", EditorStyles.whiteLabel);
    }

    private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    /// <summary>Each sprite in the prefab, with where it sits in world units from the prefab's root.</summary>
    private static IEnumerable<(Sprite sprite, Rect rect, bool flip)> ArtParts(PlaceableDefinition placeable)
    {
        if (!placeable.prefab) yield break;
        Matrix4x4 toRoot = placeable.prefab.transform.worldToLocalMatrix;
        foreach (SpriteRenderer renderer in placeable.prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!renderer.sprite || renderer.name == "Shadow" || renderer.name == GroundShadow.ChildName) continue;
            Matrix4x4 matrix = toRoot * renderer.transform.localToWorldMatrix;
            Bounds local = renderer.sprite.bounds;
            Vector2 a = matrix.MultiplyPoint3x4(local.min), b = matrix.MultiplyPoint3x4(local.max);
            yield return (renderer.sprite, Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)), (b.x < a.x) != renderer.flipX);
        }
    }

    // ----- Measuring -----

    /// <summary>Measures the visible art: as wide as its bottom part, and deep enough to look solid.</summary>
    public static bool TryMeasureBase(PlaceableDefinition placeable, out Vector2 size, out float offCentre)
    {
        size = placeable.footprintSize;
        offCentre = 0f;
        var solid = new List<Vector2>();
        foreach ((Sprite sprite, Rect rect, bool flip) in ArtParts(placeable))
        {
            Color32[] pixels = ReadSprite(sprite, out int width, out int height);
            int step = Mathf.Max(1, Mathf.Min(width, height) / 256);
            for (int y = 0; y < height; y += step)
            for (int x = 0; x < width; x += step)
            {
                if (pixels[y * width + x].a < 128) continue;
                float u = (x + 0.5f) / width;
                solid.Add(new Vector2(rect.x + (flip ? 1f - u : u) * rect.width, rect.y + (y + 0.5f) / height * rect.height));
            }
        }
        if (solid.Count == 0) return false;

        float bottom = solid.Min(p => p.y), top = solid.Max(p => p.y);
        float band = bottom + Mathf.Max(0.1f, (top - bottom) * 0.15f);
        var feet = solid.Where(p => p.y <= band).ToList();
        float left = feet.Min(p => p.x), right = feet.Max(p => p.x);
        // A little narrower than the art, so the gecko can get close to its rounded edges.
        float widthAtFeet = Mathf.Max(0.2f, Mathf.Round((right - left) * 0.9f * 20f) / 20f);
        float depth = Mathf.Clamp(Mathf.Round(widthAtFeet * 0.3f * 20f) / 20f, 0.25f, 0.6f);
        size = new Vector2(widthAtFeet, depth);
        offCentre = (left + right) * 0.5f;
        return true;
    }

    private static Color32[] ReadSprite(Sprite sprite, out int width, out int height)
    {
        Rect rect = sprite.textureRect;
        width = Mathf.RoundToInt(rect.width);
        height = Mathf.RoundToInt(rect.height);
        Texture2D texture = sprite.texture;
        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        Color32[] pixels = copy.GetPixels32();
        DestroyImmediate(copy);
        return pixels;
    }

    // ----- Saving -----

    public static void FitAll()
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database) return;
        foreach (PlaceableDefinition placeable in database.placeables)
        {
            if (!placeable || placeable.placement != Bush.Core.PlacementType.Floor || placeable.chainSpan > 0f) continue;
            if (TryMeasureBase(placeable, out Vector2 size, out float offCentre))
            {
                Debug.Log($"[Bases] {placeable.id}: was {placeable.footprintSize.x:0.##} x {placeable.footprintSize.y:0.##}, now {size.x:0.##} x {size.y:0.##}, art off centre by {offCentre:0.00}");
                SetFootprint(placeable, size);
            }
        }
    }

    private static void SetFootprint(PlaceableDefinition placeable, Vector2 size)
    {
        size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        Undo.RecordObject(placeable, "Change base");
        placeable.footprintSize = size;
        EditorUtility.SetDirty(placeable);
        AssetDatabase.SaveAssetIfDirty(placeable);
        WriteToDesignFile(placeable.id, size);
    }

    /// <summary>Changes just those two numbers, so the rest of the file stays exactly as it is.</summary>
    private static void WriteToDesignFile(string id, Vector2 size)
    {
        if (!File.Exists(DesignPath)) return;
        string text = File.ReadAllText(DesignPath);
        var pattern = new Regex("(\"id\":\\s*\"" + Regex.Escape(id) + "\"[^{}]*?\"footprintWidth\":\\s*)[-0-9.]+(,\\s*\"footprintDepth\":\\s*)[-0-9.]+");
        string updated = pattern.Replace(text, m =>
            m.Groups[1].Value + size.x.ToString("0.##", CultureInfo.InvariantCulture) + m.Groups[2].Value + size.y.ToString("0.##", CultureInfo.InvariantCulture), 1);
        if (updated == text) return;
        File.WriteAllText(DesignPath, updated);
    }
}
