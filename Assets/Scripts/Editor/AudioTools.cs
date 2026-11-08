using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bush.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Tools > Bush > Audio: finds or makes the sound library, and fills it from the Audio folders by file name, so a batch
/// of new sounds is one click. A file goes in a row if its name is that row's with a number on the end or not:
/// UiTap.wav, UiTap2.wav. A creature's clips go in Assets/Audio/Voices/ and a folder with their name.
/// </summary>
public static class AudioTools
{
    private const string LibraryPath = "Assets/Resources/Audio/SoundLibrary.asset";
    private const string AudioFolder = "Assets/Audio";
    private const string VoicesFolder = "Assets/Audio/Voices";
    private const string DefaultVoice = "Default";
    private const string DatabasePath = "Assets/Data/GameDatabase.asset";

    [MenuItem("Tools/Bush/Audio/Open Sound Library", priority = 50)]
    public static void OpenLibrary()
    {
        SoundLibrary library = FindOrCreateLibrary();
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);
    }

    public static SoundLibrary FindOrCreateLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (!library)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            AssetDatabase.Refresh();
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/AudioMixer.mixer");
        if (mixer)
        {
            if (!library.music) library.music = Group(mixer, "Music");
            if (!library.dialogue) library.dialogue = Group(mixer, "Dialogue");
            if (!library.sfx) library.sfx = Group(mixer, "SFX");
        }
        if (!library.audioManagerPrefab) library.audioManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Audio/AudioManager.prefab");

        AddEveryone(library);
        library.AddMissingRows();
        MakeVoiceFolders(library);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssetIfDirty(library);
        return library;
    }

    private static AudioMixerGroup Group(AudioMixer mixer, string name) => mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);

    /// <summary>A voice row for every character there is.</summary>
    private static void AddEveryone(SoundLibrary library)
    {
        var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        if (!database) return;
        library.voices.RemoveAll(v => !v.npc);
        foreach (NpcDefinition npc in database.npcs)
            if (npc && library.VoiceOf(npc) == null) library.voices.Add(new SoundLibrary.Voice { npc = npc });
    }

    private static string VoiceFolderName(NpcDefinition npc)
    {
        string plain = npc.id.Replace("npc.", "");
        return char.ToUpperInvariant(plain[0]) + plain.Substring(1);
    }

    private static void MakeVoiceFolders(SoundLibrary library)
    {
        if (!AssetDatabase.IsValidFolder(VoicesFolder)) AssetDatabase.CreateFolder(AudioFolder, "Voices");
        foreach (string name in library.voices.Where(v => v.npc).Select(v => VoiceFolderName(v.npc)).Append(DefaultVoice))
            if (!AssetDatabase.IsValidFolder($"{VoicesFolder}/{name}")) AssetDatabase.CreateFolder(VoicesFolder, name);
    }

    [MenuItem("Tools/Bush/Audio/Fill Sound Library From Folders", priority = 51)]
    public static void FillFromFolders()
    {
        SoundLibrary library = FindOrCreateLibrary();
        string report = Fill(library);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;
        Debug.Log($"[Audio] {report}");
        if (!Application.isBatchMode) EditorUtility.DisplayDialog("Sound library", report, "OK");
    }

    /// <summary>Adds what's in the folders and keeps what's already in the rows, so clips put in by hand stay.</summary>
    public static string Fill(SoundLibrary library) => Fill(library, out _);

    public static string Fill(SoundLibrary library, out int added)
    {
        var rows = library.sounds.ToDictionary(r => Plain(r.sound.ToString()));
        added = 0;
        var unmatched = new List<string>();

        foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (path.StartsWith($"{AudioFolder}/Test/")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (path.StartsWith($"{VoicesFolder}/"))
            {
                string inVoices = path.Substring(VoicesFolder.Length + 1);
                if (!inVoices.Contains('/')) continue; // loose files here are the raw recordings the lines are cut from
                string folder = inVoices.Split('/')[0];
                if (folder.Equals(DefaultVoice, StringComparison.OrdinalIgnoreCase))
                {
                    added += AddTo(ref library.defaultVoice, clip);
                    continue;
                }
                SoundLibrary.Voice voice = library.voices.Find(v => v.npc && VoiceFolderName(v.npc).Equals(folder, StringComparison.OrdinalIgnoreCase));
                if (voice != null) added += AddTo(ref voice.clips, clip);
                else unmatched.Add(path);
                continue;
            }

            if (rows.TryGetValue(Plain(Path.GetFileNameWithoutExtension(path)), out SoundLibrary.Row row)) added += AddTo(ref row.clips, clip);
            else unmatched.Add(path);
        }

        int silent = library.sounds.Count(r => r.clips.All(c => !c)) ;
        string report = $"Added {added} clip(s). {silent} of {library.sounds.Count} sounds still have none.";
        if (unmatched.Count > 0) report += $"\n\nThese didn't match a row, so check their names:\n{string.Join("\n", unmatched.Select(Path.GetFileName))}";
        return report;
    }

    private static int AddTo(ref AudioClip[] clips, AudioClip clip)
    {
        if (clips.Contains(clip)) return 0;
        clips = clips.Where(c => c).Append(clip).ToArray();
        return 1;
    }

    /// <summary>Lower case, letters only, so "Ui Tap 2", "ui_tap-2" and "UiTap" all match.</summary>
    private static string Plain(string name) => new(name.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
}

/// <summary>
/// A clip dropped in a creature's voice folder joins their voice by itself, so their lines play straight away without
/// anyone running Fill. Runs after the import has finished, and only says something when it added a clip.
/// </summary>
public class VoiceFolderWatcher : AssetPostprocessor
{
    private static bool _queued;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (_queued || Application.isBatchMode) return;
        if (!imported.Concat(moved).Any(IsVoiceClip)) return;
        _queued = true;
        EditorApplication.delayCall += () =>
        {
            _queued = false;
            SoundLibrary library = AudioTools.FindOrCreateLibrary();
            AudioTools.Fill(library, out int added);
            if (added == 0) return;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            Debug.Log($"[Audio] Added {added} voice clip(s) to the sound library.");
        };
    }

    private static bool IsVoiceClip(string path) =>
        path.StartsWith("Assets/Audio/Voices/") && path.Substring("Assets/Audio/Voices/".Length).Contains('/')
        && AssetImporter.GetAtPath(path) is AudioImporter;
}

/// <summary>Sensible settings for a new sound, going by its folder, so music doesn't fill a phone's memory. Change any afterwards.</summary>
public class AudioImportRules : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetImporter.importSettingsMissing || !assetPath.StartsWith("Assets/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;

        if (assetPath.StartsWith("Assets/Audio/Music/"))
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = 0.6f;
        }
        else if (assetPath.StartsWith("Assets/Audio/Ambient/"))
        {
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.quality = 0.5f;
        }
        else
        {
            // Short sounds: ready the moment they're needed, and one channel is plenty on a phone.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.quality = 0.6f;
            importer.forceToMono = true;
        }
        importer.defaultSampleSettings = settings;
    }
}
