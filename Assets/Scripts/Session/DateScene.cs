using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bush.Core;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The date at the end of the story. Talking to the character for that quest plays this instead of the usual
/// conversation: a fade into night, the two of you at the table with everyone else gone and the lights on, the
/// lines from <see cref="SceneScript"/> (Resources/Scenes/DateScene), a quiet moment, and a fade back out.
/// What you get shows on the beat that gives the rewards, and the quest is handed in behind the last fade, so the
/// character is still there for the quiet moment (finishing her story would send her home).
/// </summary>
public class DateScene : MonoBehaviour
{
    public const string ResourcePath = "Scenes/DateScene";
    private const float SideGap = 0.45f;

    private SceneScript _script;
    private NpcDefinition _npcDefinition;
    private Npc _npc;
    private GameObject _focus;
    private readonly List<Renderer> _hidden = new();
    private CanvasGroup _hud;
    private UI.Popups.PauseInput _pause;
    private bool _waiting;
    private bool _cut;
    private bool _handedIn;

    /// <summary>Whether a date is playing now.</summary>
    public static bool Playing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Playing = false;

    /// <summary>Plays the scene if talking to this character now should, and says whether it did.</summary>
    public static bool TryStart(NpcDefinition npc)
    {
        GameSession session = GameSession.Current;
        if (!npc || session == null) return false;
        if (Playing) return true;
        var script = Resources.Load<SceneScript>(ResourcePath);
        if (!script || npc.id != script.npcId || script.beats.Count == 0) return false;
        NpcTopic topic = session.Quests.GetTopic(npc.id);
        if (topic.questId != script.questId || topic.kind is not (TopicKind.Offer or TopicKind.HandIn)) return false;

        var scene = new GameObject("[DateScene]").AddComponent<DateScene>();
        scene._script = script;
        scene._npcDefinition = npc;
        scene.StartCoroutine(scene.Play());
        return true;
    }

    private static GameSession Session => GameSession.Current;

    private IEnumerator Play()
    {
        Playing = true;
        InputLock.Lock(this);
        if (Interactor.Player) Interactor.Player.Cancel();
        _npc = FindObjectsByType<Npc>().FirstOrDefault(n => n.Definition == _npcDefinition);

        yield return Fade(_script.introCard, SetUp);

        DialogueBox box = DialogueBox.ShowScripted(_npcDefinition);
        // The box covers the bottom of the screen, so the view can go a little past the art to keep you above it.
        CameraFollow follow = Camera.main ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow)
        {
            follow.ExtraBelow = Camera.main.orthographicSize * 0.7f;
            follow.SnapToTarget();
        }
        foreach (SceneScript.Beat beat in _script.beats)
        {
            string detail = beat.givesRewards ? RewardsText() : null;
            SceneScript.Reply picked = null;
            var replies = beat.replies.Where(r => !string.IsNullOrEmpty(r.label))
                .Select(r => (r.label, (UnityAction)(() => picked = r))).ToList();
            if (replies.Count == 0) replies.Add((_script.continueLabel, () => { }));
            yield return Say(box, beat.line, detail, replies.ToArray());
            if (_cut) break;

            if (picked != null && !string.IsNullOrEmpty(picked.answer))
            {
                yield return Say(box, picked.answer, null, (_script.continueLabel, () => { }));
                if (_cut) break;
            }
        }
        if (box && box.IsOpen) box.Close();
        if (follow) follow.ExtraBelow = 0f; // eases back up for the quiet moment

        if (!_cut) yield return new WaitForSecondsRealtime(_script.quietSeconds);
        yield return Fade(_script.endCard, Restore);

        InputLock.Unlock(this);
        Playing = false;
        Destroy(gameObject);
    }

    /// <summary>Shows a line and waits for a reply. Closing the box (Escape) cuts the scene short.</summary>
    private IEnumerator Say(DialogueBox box, string line, string detail, params (string label, UnityAction onClick)[] replies)
    {
        _waiting = true;
        var wrapped = replies.Select(r => (r.label, (UnityAction)(() =>
        {
            r.onClick();
            _waiting = false;
        }))).ToArray();
        box.Script(line, detail, wrapped);
        while (_waiting && box && box.IsOpen) yield return null;
        if (_waiting) _cut = true;
    }

    private IEnumerator Fade(string card, System.Action atBlack)
    {
        AreaTravel travel = AreaTravel.Instance;
        for (int i = 0; i < 600 && travel.IsTravelling; i++) yield return null; // let anything already fading finish
        if (!travel.PassTime(card, atBlack))
        {
            atBlack();
            yield break;
        }
        while (travel.IsTravelling) yield return null;
    }

    private void HandIn()
    {
        if (_handedIn) return;
        _handedIn = true;
        QuestService quests = Session.Quests;
        string questId = _script.questId;
        if (quests.GetStatus(questId) == QuestStatus.NotStarted) quests.Accept(questId);
        if (!quests.Claim(questId, out string error)) Debug.LogWarning($"[DateScene] Couldn't hand in {questId}: {error}");
        Session.SaveNow();
    }

    /// <summary>What you get, for under the line that gives it.</summary>
    private string RewardsText()
    {
        QuestData quest = Session.Design.GetQuest(_script.questId);
        if (quest == null) return null;
        var text = new StringBuilder("<b>You got</b>\n");
        foreach (QuestReward reward in quest.rewards)
            text.Append($"<color=#{ColorUtility.ToHtmlStringRGB(GameUi.Moss)}>{QuestText.Reward(Session.Design, reward)}</color>\n");
        return quest.rewards.Count > 0 ? text.ToString().TrimEnd() : null;
    }

    // ----- Setting the scene, behind the fade -----

    private void SetUp()
    {
        DayNightLighting.Forced = DayNightLighting.Phase.Night;
        DayNightLighting.SnapNow();
        InteractionPrompt.Hidden = true;

        // Just the two of you: everyone else, and the HUD, out of the way.
        foreach (Npc other in FindObjectsByType<Npc>())
        {
            if (other == _npc) continue;
            foreach (Renderer r in other.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    _hidden.Add(r);
                }
        }
        if (_npc)
        {
            Transform marker = _npc.transform.Find("QuestMarker");
            if (marker && marker.TryGetComponent(out Renderer markerRenderer) && markerRenderer.enabled)
            {
                markerRenderer.enabled = false;
                _hidden.Add(markerRenderer);
            }
        }
        _pause = FindAnyObjectByType<UI.Popups.PauseInput>();
        if (_pause) _pause.AllowPause = false; // no cog, and Escape skips the scene rather than pausing it
        var hud = FindAnyObjectByType<HudView>();
        if (hud)
        {
            if (!hud.TryGetComponent(out _hud)) _hud = hud.gameObject.AddComponent<CanvasGroup>();
            _hud.alpha = 0f;
            _hud.blocksRaycasts = false;
        }

        // Opposite each other, either side of the table.
        Vector2 middle = _npc ? (Vector2)_npc.transform.position : Interactor.Player ? (Vector2)Interactor.Player.transform.position : Vector2.zero;
        float halfWidth = 0.6f;
        BuildArea area = BuildArea.Current;
        PlacedObject table = area ? area.Spawned.FirstOrDefault(p => p && p.Definition && p.Definition.id == _script.tableId) : null;
        if (table)
        {
            middle = table.transform.position;
            halfWidth = table.Definition.footprintSize.x * 0.5f;
        }
        Vector2 left = middle + new Vector2(-(halfWidth + SideGap), -0.05f);
        Vector2 right = middle + new Vector2(halfWidth + SideGap, -0.05f);
        if (Interactor.Player)
        {
            Interactor.Player.Mover.Teleport(left);
            Interactor.Player.Mover.SetFacing(true);
        }
        if (_npc) _npc.PlaceAt(right, right, false);

        // The camera comes in on the table.
        _focus = new GameObject("DateFocus");
        _focus.transform.position = middle + new Vector2(0f, -0.3f); // your feet just above the middle, so the box along the bottom leaves you in view
        Camera camera = Camera.main;
        if (camera)
        {
            if (camera.TryGetComponent(out CameraFit fit))
            {
                fit.Zoom = _script.zoom;
                fit.ApplyNow();
            }
            if (camera.TryGetComponent(out CameraFollow follow))
            {
                follow.Target = _focus.transform;
                follow.Centre = true;
                follow.SnapToTarget();
            }
        }
    }

    private void Restore()
    {
        HandIn();
        DayNightLighting.Forced = null;
        DayNightLighting.SnapNow();
        InteractionPrompt.Hidden = false;
        foreach (Renderer r in _hidden) if (r) r.enabled = true;
        _hidden.Clear();
        if (_pause) _pause.AllowPause = true;
        if (_hud)
        {
            _hud.alpha = 1f;
            _hud.blocksRaycasts = true;
        }
        Camera camera = Camera.main;
        if (camera)
        {
            if (camera.TryGetComponent(out CameraFit fit))
            {
                fit.Zoom = 1f;
                fit.ApplyNow();
            }
            if (camera.TryGetComponent(out CameraFollow follow))
            {
                follow.Centre = false;
                follow.ExtraBelow = 0f;
                follow.Target = Interactor.Player ? Interactor.Player.transform : null;
                follow.SnapToTarget();
            }
        }
        if (_focus) Destroy(_focus);
        GuestSeating.Refresh(); // she goes back to a seat of her own
    }

    private void OnDestroy()
    {
        if (!Playing) return;
        // Left mid-scene (the scene unloading): don't leave the world stuck at night or locked.
        DayNightLighting.Forced = null;
        InteractionPrompt.Hidden = false;
        InputLock.Unlock(this);
        Playing = false;
    }
}
