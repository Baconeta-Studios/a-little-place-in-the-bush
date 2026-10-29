using Bush.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// A character you can talk to. Click them, the gecko walks over, and a conversation opens at the
/// bottom of the screen (<see cref="DialogueBox"/>): they offer quests, ask how it's going, and take
/// what you bring. A "!" floats over them when they have a quest to offer, and a "?" when you can
/// hand one in. They're only there while they're visiting (see <see cref="Visitors"/>): story
/// characters while the story is theirs, random visitors on the days they drop by. Place every
/// character in the scene at their own spot; the ones who aren't in today are hidden (in build mode
/// too) and block nothing. Visiting guests sit on the seats of furniture you've placed
/// (<see cref="GuestSeating"/>), and use their own spot when there's no free seat. Set it up like any interactable: a Collider2D on the Interactable layer and a
/// StandPoint child, then pick the character's NPC asset.
/// </summary>
public class Npc : Interactable
{
    [SerializeField] private NpcDefinition npc;
    [Tooltip("How high (world units) the ! or ? floats above the character's feet.")]
    [SerializeField] private float markerHeight = 2.4f;
    [Tooltip("Sits on free seats of placed furniture when visiting. Untick for someone who stays at their own spot (e.g. behind a counter).")]
    [SerializeField] private bool takesSeats = true;
    [Tooltip("Whether the art (in a child called Visual) faces right when its X scale is positive.")]
    [SerializeField] private bool artFacesRight = true;

    private TextMeshPro _marker;
    private float _nextMarkerCheck;
    private Renderer[] _renderers;
    private Collider2D[] _colliders;
    private bool _present = true;
    private Transform _visual;
    private StandPoint _stand;

    public bool TakesSeats => takesSeats;
    /// <summary>Their own spot, where they were placed in the scene.</summary>
    public Vector2 HomePosition { get; private set; }
    /// <summary>Where the gecko stands to talk to them at their own spot.</summary>
    public Vector2 HomeStand { get; private set; }
    public bool HomeFacesRight { get; private set; } = true;

    public NpcDefinition Definition => npc;
    public override string Prompt => npc ? $"Talk to {npc.displayName}" : base.Prompt;

    private void Start()
    {
        if (!npc) Debug.LogWarning($"[Npc] {name} has no NPC asset set.", this);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        _visual = transform.Find("Visual");
        _stand = GetComponentInChildren<StandPoint>(true);
        HomePosition = transform.position;
        HomeStand = _stand ? (Vector2)_stand.transform.position : HomePosition;
        HomeFacesRight = !_visual || (_visual.localScale.x >= 0f) == artFacesRight;
        GuestSeating.Ensure();
        // Hide straight away if they're not in today, rather than flashing up for a moment.
        GameSession session = GameSession.Current;
        if (session != null && npc) SetPresent(Visitors.IsPresent(session.Design, session.State, session.Quests, npc.id));

        var markerObject = new GameObject("QuestMarker");
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = new Vector3(0f, markerHeight, 0f);
        _marker = markerObject.AddComponent<TextMeshPro>();
        _marker.alignment = TextAlignmentOptions.Center;
        _marker.fontSize = 8f;
        _marker.fontStyle = FontStyles.Bold;
        _marker.color = GameUi.Berry;
        _marker.text = "";
        _marker.sortingLayerID = SortingLayer.NameToID("Foreground");
    }

    private void Update()
    {
        // A few times a second is plenty, and simpler than listening to every event that could change it.
        if (Time.unscaledTime < _nextMarkerCheck || !_marker) return;
        _nextMarkerCheck = Time.unscaledTime + 0.3f;
        GameSession session = GameSession.Current;
        if (session == null || !npc) return;
        SetPresent(Visitors.IsPresent(session.Design, session.State, session.Quests, npc.id));
        _marker.text = !_present ? "" : session.Quests.GetTopic(npc.id).kind switch
        {
            TopicKind.Offer => "!",
            TopicKind.HandIn => "?",
            _ => ""
        };
    }

    public override bool CanInteract(in InteractionContext context) => base.CanInteract(context) && npc && _present;

    public bool IsPresent => _present;

    /// <summary>Moves them (to a seat, or home), with the gecko's spot beside them, facing the given way.</summary>
    public void PlaceAt(Vector2 position, Vector2 stand, bool faceRight)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (_stand) _stand.transform.position = new Vector3(stand.x, stand.y, _stand.transform.position.z);
        if (_visual)
        {
            Vector3 scale = _visual.localScale;
            float size = Mathf.Abs(scale.x);
            scale.x = faceRight == artFacesRight ? size : -size;
            _visual.localScale = scale;
        }
    }

    /// <summary>Shows or hides the character (art and click areas) without disabling this component.</summary>
    private void SetPresent(bool present)
    {
        if (present == _present) return;
        _present = present;
        foreach (Renderer r in _renderers) if (r) r.enabled = present;
        foreach (Collider2D c in _colliders) if (c) c.enabled = present;
    }

    public override void Interact(in InteractionContext context)
    {
        base.Interact(context);
        DialogueBox.Show(npc);
    }
}
