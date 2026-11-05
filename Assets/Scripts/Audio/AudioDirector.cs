using System.Collections.Generic;
using Bush.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays what nothing has to ask for: the right music and background sound for the moment, a sound for every button
/// (including new ones), footsteps, and the game's events such as coins and finished quests. It makes itself when the
/// game starts. Sounds tied to one screen (forage finds, build mode, creature voices) are asked for where they happen.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    private const float CheckSeconds = 0.5f;

    private readonly List<RaycastResult> _hits = new();
    private GameSession _boundTo;
    private int _lastCoins;
    private float _nextCheck;
    private float _nextStep;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!GameAudio.Library) return; // no library, no sound
        var director = new GameObject("[AudioDirector]");
        DontDestroyOnLoad(director);
        director.AddComponent<AudioDirector>();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + CheckSeconds;
            ChooseMusic();
            BindToSession();
        }
        ButtonSounds();
        Footsteps();
    }

    private void OnDestroy() => BindTo(null);

    // ----- Music -----

    private void ChooseMusic()
    {
        if (System.Array.IndexOf(GameAudio.Library.menuScenes, SceneManager.GetActiveScene().name) >= 0)
        {
            GameAudio.PlayMusic(GameSound.MusicMainMenu);
            GameAudio.StopAmbient();
            return;
        }

        GameSession session = GameSession.Current;
        if (session == null) return;
        GameAudio.PlayMusic(SoundChoice.MusicFor(session.Design, session.State, ForagePopup.IsShowing));
        GameAudio.PlayAmbient(SoundChoice.AmbientFor(session.State));
    }

    // ----- Buttons -----

    private void ButtonSounds()
    {
        Pointer pointer = Pointer.current;
        EventSystem events = EventSystem.current;
        if (pointer == null || !events || !pointer.press.wasPressedThisFrame) return;

        var data = new PointerEventData(events) { position = pointer.position.ReadValue() };
        _hits.Clear();
        events.RaycastAll(data, _hits);
        if (_hits.Count == 0) return;
        Selectable pressed = _hits[0].gameObject.GetComponentInParent<Selectable>();
        if (!pressed) return;
        GameAudio.Play(!pressed.IsInteractable() ? GameSound.UiNope : IsWayOut(pressed) ? GameSound.UiBack : GameSound.UiTap);
    }

    private static bool IsWayOut(Selectable pressed)
    {
        string label = pressed.GetComponentInChildren<TextMeshProUGUI>()?.text;
        return label is "Back" or "Bye" or "Not now" or "x" or "Resume";
    }

    // ----- Footsteps -----

    private void Footsteps()
    {
        Interactor player = Interactor.Player;
        if (!player || !player.Mover || !player.Mover.IsMoving || Time.timeScale <= 0f || Time.unscaledTime < _nextStep) return;
        _nextStep = Time.unscaledTime + GameAudio.Library.footstepSeconds;
        GameAudio.Play(GameSound.Footstep);
    }

    // ----- Things that happen -----

    private void BindToSession()
    {
        if (_boundTo != GameSession.Current) BindTo(GameSession.Current);
    }

    private void BindTo(GameSession session)
    {
        if (_boundTo != null)
        {
            GameEvents events = _boundTo.Events;
            events.CurrencyChanged -= OnCoins;
            events.QuestStarted -= OnQuestStarted;
            events.QuestCompleted -= OnQuestDone;
            events.RecipeLearned -= OnRecipe;
            events.PerkLearned -= OnPerk;
            events.UpgradeBought -= OnUpgrade;
            events.MenuChanged -= OnMenu;
            events.DayEnded -= OnDayEnded;
            events.NightStarted -= OnNight;
            events.MorningStarted -= OnMorning;
            events.ExpeditionCompleted -= OnHome;
        }
        _boundTo = session;
        if (session == null) return;
        _lastCoins = session.Economy.Currency;
        GameEvents now = session.Events;
        now.CurrencyChanged += OnCoins;
        now.QuestStarted += OnQuestStarted;
        now.QuestCompleted += OnQuestDone;
        now.RecipeLearned += OnRecipe;
        now.PerkLearned += OnPerk;
        now.UpgradeBought += OnUpgrade;
        now.MenuChanged += OnMenu;
        now.DayEnded += OnDayEnded;
        now.NightStarted += OnNight;
        now.MorningStarted += OnMorning;
        now.ExpeditionCompleted += OnHome;
    }

    private void OnCoins(int coins)
    {
        if (coins > _lastCoins) GameAudio.Play(GameSound.Coins);
        _lastCoins = coins;
    }

    private static void OnQuestStarted(string _) => GameAudio.Play(GameSound.QuestStarted);
    private static void OnQuestDone(string _) => GameAudio.Play(GameSound.QuestDone);
    private static void OnRecipe(string _) => GameAudio.Play(GameSound.RecipeLearned);
    private static void OnPerk(string _) => GameAudio.Play(GameSound.PerkLearned);
    private static void OnUpgrade(string _) => GameAudio.Play(GameSound.UpgradeBought);
    private static void OnMenu() => GameAudio.Play(GameSound.MenuChanged);
    private static void OnDayEnded(DaySummary _) => GameAudio.Play(GameSound.DayEnded);
    private static void OnNight() => GameAudio.Play(GameSound.NightFalls);
    private static void OnMorning() => GameAudio.Play(GameSound.Morning);
    private static void OnHome(ForageResult _) => GameAudio.Play(GameSound.ForageHome);
}
