using UnityEngine;

/// <summary>
/// Simple sprites drawn in code (a square, a soft circle), so prototype UI and markers need no art.
/// Made once and shared.
/// </summary>
public static class GeneratedSprites
{
    private static Sprite _square;
    private static Sprite _circle;
    private static Sprite _rounded;

    /// <summary>A plain white 4x4 square, 1 unit across at 4 pixels per unit. Tint it with the renderer's colour.</summary>
    public static Sprite Square => _square ? _square : _square = Make(4, 4f, (x, y) => 1f, new Vector2(0.5f, 0.5f));

    /// <summary>A soft-edged white circle, 1 unit across. Good for shadows (squash it flat) and markers.</summary>
    public static Sprite Circle => _circle ? _circle : _circle = Make(64, 64f, (x, y) =>
    {
        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
        return Mathf.Clamp01((32f - distance) / 6f);
    }, new Vector2(0.5f, 0.5f));

    /// <summary>A white rounded rectangle for UI panels and buttons, sliced so it stretches cleanly.</summary>
    public static Sprite RoundedPanel
    {
        get
        {
            if (_rounded) return _rounded;
            const int size = 32, radius = 10;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f)));
                }
            }
            texture.Apply();
            _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _rounded;
        }
    }

    private static Sprite Make(int size, float pixelsPerUnit, System.Func<int, int, float> alpha, Vector2 pivot)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), pivot, pixelsPerUnit);
    }
}
