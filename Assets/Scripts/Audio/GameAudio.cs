using Audio;
using Bush.Core;
using Managers;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// How the game asks for a sound: GameAudio.Play(GameSound.Coins). The clips come from the <see cref="SoundLibrary"/> and
/// play through the <see cref="AudioManager"/> and its mixer, so the Settings sliders work. Anything with no clips yet
/// simply stays silent.
/// </summary>
public static class GameAudio
{
    private static SoundLibrary _library;
    private static bool _looked;
    private static CustomAudioSource _music;
    private static GameSound? _musicNow;
    private static CustomAudioSource _ambient;
    private static GameSound? _ambientNow;
    private static CustomAudioSource _voice;
    private static AudioClip _lastVoiceClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _library = null;
        _looked = false;
        _music = _ambient = _voice = null;
        _musicNow = _ambientNow = null;
        _lastVoiceClip = null;
    }

    public static SoundLibrary Library
    {
        get
        {
            if (!_looked)
            {
                _looked = true;
                _library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
            }
            return _library;
        }
    }

    public static bool HasClips(GameSound sound)
    {
        SoundLibrary.Row row = Library ? Library.Get(sound) : null;
        return row != null && Pick(row.clips, null);
    }

    /// <summary>Plays a sound once.</summary>
    public static CustomAudioSource Play(GameSound sound)
    {
        SoundLibrary.Row row = Library ? Library.Get(sound) : null;
        AudioClip clip = row != null ? Pick(row.clips, null) : null;
        if (!clip) return null;
        float pitch = 1f + Random.Range(-row.pitchVariation, row.pitchVariation);
        return PlayOnce(clip, Library.sfx, row.volume, pitch);
    }

    /// <summary>Fades over to this music, unless it's what's playing already.</summary>
    public static void PlayMusic(GameSound sound) => Crossfade(sound, Library ? Library.music : null, ref _music, ref _musicNow);

    /// <summary>Fades over to this background sound, unless it's what's playing already.</summary>
    public static void PlayAmbient(GameSound sound) => Crossfade(sound, Library ? Library.ambient : null, ref _ambient, ref _ambientNow);

    public static void StopAmbient()
    {
        FadeOut(_ambient);
        _ambient = null;
        _ambientNow = null;
    }

    /// <summary>A creature says something: a line of their gibberish, cutting off the last one.</summary>
    public static void Say(NpcDefinition npc)
    {
        StopVoice();
        if (!Library) return;
        SoundLibrary.Voice voice = Library.VoiceOf(npc);
        bool own = voice != null && Pick(voice.clips, null);
        AudioClip clip = Pick(own ? voice.clips : Library.defaultVoice, _lastVoiceClip);
        if (!clip) return;
        _lastVoiceClip = clip;
        float pitch = voice != null ? voice.pitch + Random.Range(-voice.pitchVariation, voice.pitchVariation) : 1f;
        _voice = PlayOnce(clip, Library.sfx, voice?.volume ?? 1f, pitch);
    }

    public static void StopVoice()
    {
        if (_voice) _voice.StopAudio();
        _voice = null;
    }

    private static void Crossfade(GameSound sound, AudioMixerGroup group, ref CustomAudioSource source, ref GameSound? now)
    {
        if (now == sound) return;
        now = sound;
        FadeOut(source);
        source = null;

        SoundLibrary.Row row = Library ? Library.Get(sound) : null;
        AudioClip clip = row != null ? Pick(row.clips, null) : null;
        if (!clip || !EnsureManager()) return;
        source = AudioManager.Instance.Setup(group);
        if (!source) return;
        source.PlayLooping(clip, 0f);
        source.FadeTo(row.volume, Library.musicFadeSeconds);
    }

    private static void FadeOut(CustomAudioSource source)
    {
        if (source) source.FadeTo(0f, Library ? Library.musicFadeSeconds : 0f, true);
    }

    private static CustomAudioSource PlayOnce(AudioClip clip, AudioMixerGroup group, float volume, float pitch)
    {
        if (!EnsureManager()) return null;
        CustomAudioSource source = AudioManager.Instance.Setup(group);
        if (!source) return null;
        source.SetPitch(pitch);
        return AudioManager.Instance.Play(clip, group, false, volume, source);
    }

    /// <summary>Makes the audio manager if this scene didn't come with one, so its saved volumes are applied.</summary>
    private static bool EnsureManager()
    {
        if (Object.FindAnyObjectByType<AudioManager>()) return true;
        if (!Library || !Library.audioManagerPrefab) return false;
        Object.Instantiate(Library.audioManagerPrefab);
        return true;
    }

    /// <summary>A random clip, avoiding <paramref name="not"/> when there's a choice.</summary>
    private static AudioClip Pick(AudioClip[] clips, AudioClip not)
    {
        if (clips == null) return null;
        int usable = 0;
        AudioClip chosen = null;
        foreach (AudioClip clip in clips)
        {
            if (!clip || clip == not) continue;
            usable++;
            if (Random.Range(0, usable) == 0) chosen = clip;
        }
        if (chosen) return chosen;
        return not && System.Array.IndexOf(clips, not) >= 0 ? not : null;
    }
}
