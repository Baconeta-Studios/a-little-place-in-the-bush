using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

/// <summary>
/// The one place for the game's audio files: Resources/Audio/SoundLibrary.asset. Every sound has a row saying where it
/// plays. Drop clips in, and if a row has several it picks one at random. A row with no clips stays silent, so nothing
/// needs to be ready at once. Tools > Bush > Audio > Fill Sound Library From Folders can fill it by file name: see docs/AUDIO.md.
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Bush/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    public const string ResourcePath = "Audio/SoundLibrary";

    [Serializable]
    public class Row
    {
        [HideInInspector] public string name; // the title Unity shows for the row
        public GameSound sound;
        [Tooltip("Where this plays.")]
        public string plays;
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random change in pitch each time, so repeats don't sound the same. 0.05 is about right. Music ignores it.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.05f;
    }

    [Serializable]
    public class Voice
    {
        [HideInInspector] public string name;
        public NpcDefinition npc;
        [Tooltip("Their gibberish. One plays each time they say something, never the same one twice running.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Below 1 is deeper and slower, above 1 is higher and quicker: one recording can make several creatures.")]
        [Range(0.5f, 2f)] public float pitch = 1f;
        [Range(0f, 0.3f)] public float pitchVariation = 0.04f;
    }

    [Header("Mixer groups (the volume sliders in Settings)")]
    public AudioMixerGroup music;
    [Tooltip("Creature voices. The Dialogue slider in Settings.")]
    [FormerlySerializedAs("ambient")] public AudioMixerGroup dialogue;
    public AudioMixerGroup sfx;
    [Tooltip("The AudioManager prefab, made when something first plays if the scene has none.")]
    public GameObject audioManagerPrefab;

    [Header("How it behaves")]
    [Tooltip("Scenes that play the main menu music.")]
    public string[] menuScenes = { "MenuTemplate", "Title" };
    [Min(0f)] public float musicFadeSeconds = 1.5f;
    [Tooltip("Time between footsteps while the gecko walks.")]
    [Min(0.05f)] public float footstepSeconds = 0.32f;

    [Header("Sounds")]
    public List<Row> sounds = new();

    [Header("Creature voices")]
    public List<Voice> voices = new();
    [Tooltip("Gibberish for anyone without their own clips.")]
    public AudioClip[] defaultVoice = new AudioClip[0];

    public Row Get(GameSound sound) => sounds.Find(r => r.sound == sound);

    public Voice VoiceOf(NpcDefinition npc) => npc ? voices.Find(v => v.npc == npc) : null;

    private void OnValidate() => AddMissingRows();

    /// <summary>A row for every sound there is, in order, with what's already in them kept.</summary>
    public void AddMissingRows()
    {
        foreach (GameSound sound in Enum.GetValues(typeof(GameSound)))
            if (Get(sound) == null) sounds.Add(new Row { sound = sound });
        sounds.Sort((a, b) => a.sound.CompareTo(b.sound));
        foreach (Row row in sounds)
        {
            row.name = row.sound.ToString();
            row.plays = WhereItPlays(row.sound);
        }
        foreach (Voice voice in voices) voice.name = voice.npc ? voice.npc.displayName : "(pick someone)";
    }

    public static string WhereItPlays(GameSound sound) => sound switch
    {
        GameSound.MusicMainMenu => "The main menu.",
        GameSound.MusicCafeDay => "At the cafe, morning and afternoon.",
        GameSound.MusicCafeEvening => "At the cafe once the day's foraging is done.",
        GameSound.MusicCafeNight => "At the cafe at night.",
        GameSound.MusicForaging => "While the forage screen is open.",
        GameSound.AmbientBushDay => "Under the music all day, on the Music slider: birds, wind in the trees.",
        GameSound.AmbientBushNight => "Under the music at night, on the Music slider: crickets, moreporks.",
        GameSound.UiTap => "Tapping any button.",
        GameSound.UiBack => "Tapping Back, Bye, Not now or an x.",
        GameSound.UiNope => "Tapping a button that's greyed out.",
        GameSound.PaperOpen => "A screen opening: a soft paper rustle.",
        GameSound.PaperClose => "A screen closing.",
        GameSound.Footstep => "Each step the gecko takes. Keep it very quiet.",
        GameSound.ForageSetOff => "Pressing Go on a forage trip.",
        GameSound.ForageFind => "Each find dropping into the basket.",
        GameSound.ForageRareFind => "A rare find dropping into the basket.",
        GameSound.ForageHome => "Getting back from a forage trip.",
        GameSound.Coins => "Coins going up.",
        GameSound.QuestStarted => "Taking on a quest.",
        GameSound.QuestDone => "Finishing a quest.",
        GameSound.RecipeLearned => "Learning a recipe.",
        GameSound.PerkLearned => "Learning a companion's perk.",
        GameSound.UpgradeBought => "Buying a cafe upgrade.",
        GameSound.MenuChanged => "Putting a dish on the menu or taking it down.",
        GameSound.DayEnded => "Closing the cafe for the day.",
        GameSound.NightFalls => "Night starting.",
        GameSound.Morning => "A new morning.",
        GameSound.PlaceObject => "Putting something down in build mode.",
        GameSound.PickUpObject => "Picking something up in build mode.",
        GameSound.PutAwayObject => "Putting something back in the bag in build mode.",
        _ => ""
    };
}
