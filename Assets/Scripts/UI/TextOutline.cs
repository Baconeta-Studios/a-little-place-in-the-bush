using TMPro;
using UnityEngine;

/// <summary>
/// An outline for text that sits straight on the scenery. It's set when the text first shows, because
/// text that hasn't been switched on yet can't take one.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextOutline : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _width = 0.18f;
    [SerializeField] private Color32 _color = new(40, 30, 20, 255);

    private void Start()
    {
        var text = GetComponent<TextMeshProUGUI>();
        text.outlineWidth = _width;
        text.outlineColor = _color;
    }
}
