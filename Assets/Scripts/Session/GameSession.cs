using System;
using Bush.Core;
using Debugging;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The running game: the design data, the save, and the services that change it (economy, cafe,
/// quests, building). Created once by a <see cref="GameSessionBootstrap"/> in whichever area scene
/// starts first, then kept across scene loads. Saves after anything changes, and when the tab is
/// hidden or the app closes.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameSession : MonoBehaviour
{
    // A new key, so everyone starts with it off now that furniture comes from quests.
    private const string UnlimitedPrefKey = "bush.build.unlimited.v2";

    private SaveService _save;
    private bool _saveRequested;

    public static GameSession Current { get; private set; }
    /// <summary>Raised when a session has been created, or rebuilt after a save reset.</summary>
    public static event Action Ready;

    public GameDatabase Database { get; private set; }
    public DesignData Design { get; private set; }
    public GameState State { get; private set; }
    public GameEvents Events { get; private set; }
    public Economy Economy { get; private set; }
    public CafeService Cafe { get; private set; }
    public QuestService Quests { get; private set; }
    public BuildService Build { get; private set; }

    public static GameSession Create(GameDatabase database)
    {
        if (Current) return Current;

        var root = new GameObject("[GameSession]");
        DontDestroyOnLoad(root);
        var session = root.AddComponent<GameSession>();
        session.Database = database;
        session.Design = database.BuildDesign();
        session._save = new SaveService(SaveService.CreateDefaultStorage(), SaveService.CreateDefaultBackupStorage());
        Current = session;
        session.StartGame(session._save.Load(session.Design));
        session.RegisterCheats();

#if UNITY_EDITOR
        var problems = DesignValidator.Validate(session.Design);
        if (problems.Count > 0) Debug.LogWarning($"[GameSession] Design data has {problems.Count} problem(s). Run Tools > Bush > Validate Design Data.");
#endif
        return session;
    }

    /// <summary>Saves at the end of this frame (several changes in one frame save once).</summary>
    public void RequestSave() => _saveRequested = true;

    public void SaveNow()
    {
        _saveRequested = false;
        if (State != null) _save.Save(State);
    }

    /// <summary>Deletes the save and starts again from scratch, reloading the current scene.</summary>
    public void ResetSave()
    {
        _save.Delete();
        StartGame(GameState.CreateNew(Design));
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Prototype mode for building: place anything without owning it. Remembered on this device.</summary>
    public bool UnlimitedBuildItems
    {
        get => Build.UnlimitedItems;
        set
        {
            Build.UnlimitedItems = value;
            PlayerPrefs.SetInt(UnlimitedPrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Events.RaiseInventoryChanged(); // refresh trays and counts
        }
    }

    private void StartGame(GameState state)
    {
        Quests?.Dispose();
        State = state;
        if (State.luckSeed == 0) State.luckSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
        Events = new GameEvents();
        Economy = new Economy(State, Design, Events);
        Cafe = new CafeService(Design, State, Economy, Events);
        Quests = new QuestService(Design, State, Economy, Events);
        Build = new BuildService(Design, State, Economy, Events)
        {
            UnlimitedItems = PlayerPrefs.GetInt(UnlimitedPrefKey, 0) == 1 // off: you place what you own (a cheat turns it on)
        };

        Events.InventoryChanged += RequestSave;
        Events.CurrencyChanged += _ => RequestSave();
        Events.BuildChanged += _ => RequestSave();
        Events.QuestStarted += _ => RequestSave();
        Events.QuestCompleted += _ => RequestSave();
        Events.ExpeditionCompleted += _ => RequestSave();
        Events.RecipeLearned += _ => RequestSave();
        Events.PerkLearned += _ => RequestSave();
        Events.MenuChanged += RequestSave;
        Events.UpgradeBought += _ => RequestSave();
        Events.NightStarted += RequestSave;
        Events.MorningStarted += RequestSave;
        Events.DayEnded += _ => RequestSave();

        Ready?.Invoke();
    }

    private void LateUpdate()
    {
        if (_saveRequested) SaveNow();
    }

    // The tab or app going to the background is the last reliable moment to save on the web and on phones.
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveNow();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SaveNow();
    }

    private void OnApplicationQuit() => SaveNow();

    private void OnDestroy()
    {
        if (Current != this) return;
        Quests?.Dispose();
        Current = null;
    }

    private void RegisterCheats()
    {
        CheatMenu.AddToggle("Build: unlimited items", () => UnlimitedBuildItems, v => UnlimitedBuildItems = v, this);
        CheatMenu.AddButton("Build: give 3 of every placeable", () =>
        {
            foreach (PlaceableData placeable in Design.placeables) Economy.Add(placeable.itemId, 3);
        }, this);
        CheatMenu.AddButton("Bag: give 5 of every item", () =>
        {
            foreach (ItemData item in Design.items) Economy.Add(item.id, item.unique ? 1 : 5);
        }, this);
        CheatMenu.AddButton("Bag: give 100 coins", () => Economy.AddCurrency(100), this);
        CheatMenu.AddButton("Cafe: learn every recipe", () =>
        {
            foreach (RecipeData recipe in Design.recipes)
            {
                if (Unlocks.IsRecipeUnlocked(Design, State, recipe.id)) continue;
                State.unlockedRecipes.Add(recipe.id);
                Events.RaiseRecipeLearned(recipe.id);
            }
        }, this);
        CheatMenu.AddButton("Cafe: close for the day", () =>
        {
            DaySummary summary = Cafe.CloseDay();
            Debug.Log($"[Cheats] Day {summary.day} closed: sold {summary.sold} for {summary.coins} coins.");
        }, this);
        CheatMenu.AddButton("Guests: everyone drops by today", () =>
        {
            State.visitorsDay = State.day;
            State.visitorsToday = Design.npcs.FindAll(n => n.visits == VisitType.Random).ConvertAll(n => n.id);
            RequestSave();
        }, this);
        CheatMenu.AddButton("Guests: reroll today's visitors", () =>
        {
            Visitors.RollToday(Design, State);
            Debug.Log($"[Cheats] Today's visitors: {string.Join(", ", State.visitorsToday)}");
            RequestSave();
        }, this);
        CheatMenu.AddButton("Forage: befriend every companion and learn every perk", () =>
        {
            foreach (CompanionData companion in Design.companions)
                if (!State.unlockedCompanions.Contains(companion.id)) State.unlockedCompanions.Add(companion.id);
            foreach (PerkData perk in Design.perks)
                if (!State.unlockedPerks.Contains(perk.id)) State.unlockedPerks.Add(perk.id);
            State.companionsUsedToday.Clear();
            RequestSave();
        }, this);
        CheatMenu.AddButton("Quests: finish the current story step", () =>
        {
            QuestData step = Quests.CurrentMainQuest();
            if (step == null)
            {
                QuestData waiting = Quests.WaitingMainQuest();
                Debug.Log(waiting != null ? $"[Cheats] {waiting.title} opens tomorrow: close the day (or go to bed) first." : "[Cheats] No story step to finish.");
                return;
            }
            string error = StoryShortcuts.Finish(Design, State, Economy, Cafe, Quests, Events, step.id);
            Debug.Log(error == null ? $"[Cheats] Finished {step.title}." : $"[Cheats] Couldn't finish {step.title}: {error}");
            RequestSave();
        }, this);
        CheatMenu.AddButton("Day event: next one (cycles)", () =>
        {
            var events = Design.rules.dayEvents;
            if (events.Count == 0) { Debug.Log("[Cheats] No day events. Tools > Bush > Create Default Day Events."); return; }
            int index = events.FindIndex(e => e.id == State.todayEvent);
            DayEventData next = events[(index + 1) % events.Count];
            DayEvents.Force(Design, State, Economy, next.id);
            Debug.Log($"[Cheats] Today: {next.name}. {next.effect}" + (State.giftItem != "" ? $" Gift: {State.giftItem}{(State.giftExclusive ? " (exclusive)" : "")}" : ""));
            RequestSave();
        }, this);
        CheatMenu.AddButton("Night: unlock night time", () =>
        {
            State.nightUnlocked = true;
            RequestSave();
        }, this);
        CheatMenu.AddButton("Night: go to bed (next morning)", () => Cafe.GoToBed(), this);
        CheatMenu.AddButton("Forage: reset today's trips", () =>
        {
            State.forageTripsToday = 0;
            State.nightTripsToday = 0;
            State.companionsUsedToday.Clear();
            RequestSave();
        }, this);
        CheatMenu.AddButton("Save: save now", SaveNow, this);
        CheatMenu.AddButton("Save: reset everything (reloads the scene)", ResetSave, this);
    }
}
