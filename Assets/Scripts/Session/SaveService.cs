using System;
using System.IO;
using Bush.Core;
using UnityEngine;
using Utils;

/// <summary>Where the save is kept. Swappable so the web build can use whatever survives a refresh.</summary>
public interface ISaveStorage
{
    string Read();
    void Write(string text);
    void Delete();
}

/// <summary>PlayerPrefs: on WebGL these are written to the browser's IndexedDB and survive a refresh.</summary>
public class PlayerPrefsSaveStorage : ISaveStorage
{
    private readonly string _key;

    public PlayerPrefsSaveStorage(string key)
    {
        _key = key;
    }

    public string Read() => PlayerPrefs.GetString(_key, null);

    public void Write(string text)
    {
        PlayerPrefs.SetString(_key, text);
        PlayerPrefs.Save(); // on WebGL this is what flushes it to the browser's storage
    }

    public void Delete()
    {
        PlayerPrefs.DeleteKey(_key);
        PlayerPrefs.Save();
    }
}

/// <summary>A JSON file in persistentDataPath (desktop builds and the editor).</summary>
public class FileSaveStorage : ISaveStorage
{
    private readonly string _fileName;

    public FileSaveStorage(string fileName)
    {
        _fileName = fileName;
    }

    public string Read() => FileUtils.ReadFromFile(_fileName, out string text) == PathStatus.ContainsData ? text : null;

    public void Write(string text) => FileUtils.WriteToFile(_fileName, text);

    public void Delete()
    {
        string path = FileUtils.GetFilePath(_fileName);
        if (File.Exists(path)) File.Delete(path);
    }
}

/// <summary>
/// Turns the <see cref="GameState"/> into JSON and back. A missing, broken or newer save never
/// crashes the game: it starts fresh and logs why (Trello: handle save version changes safely).
/// </summary>
public class SaveService
{
    private readonly ISaveStorage _storage;
    private readonly ISaveStorage _backup;

    /// <param name="backup">Where a save that can't be used is copied before it's replaced. Optional.</param>
    public SaveService(ISaveStorage storage, ISaveStorage backup = null)
    {
        _storage = storage;
        _backup = backup;
    }

    /// <summary>PlayerPrefs on the web (reliable after a refresh), a file everywhere else.</summary>
    public static ISaveStorage CreateDefaultStorage() =>
        Application.platform == RuntimePlatform.WebGLPlayer
            ? new PlayerPrefsSaveStorage("bush.save.v1")
            : new FileSaveStorage("bush-save.json");

    public static ISaveStorage CreateDefaultBackupStorage() =>
        Application.platform == RuntimePlatform.WebGLPlayer
            ? new PlayerPrefsSaveStorage("bush.save.v1.backup")
            : new FileSaveStorage("bush-save.backup.json");

    public GameState Load(DesignData design)
    {
        string json;
        try
        {
            json = _storage.Read();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Couldn't read the save, starting fresh: {e.Message}");
            return GameState.CreateNew(design);
        }

        if (string.IsNullOrEmpty(json)) return GameState.CreateNew(design);

        GameState state;
        try
        {
            state = JsonUtility.FromJson<GameState>(json);
        }
        catch (Exception e)
        {
            return StartFresh(design, json, $"the save is unreadable ({e.Message})");
        }

        if (state == null) return StartFresh(design, json, "the save was empty");
        if (state.saveVersion > GameState.CurrentSaveVersion)
            return StartFresh(design, json, $"the save is from a newer version ({state.saveVersion})");

        // Older saves would be upgraded here, one version at a time. There's only version 1 so far.
        state.saveVersion = GameState.CurrentSaveVersion;
        state.Normalise(design);
        return state;
    }

    /// <summary>Keeps a copy of the unusable save (it would be overwritten by the next save), then starts again.</summary>
    private GameState StartFresh(DesignData design, string unusableJson, string why)
    {
        try
        {
            _backup?.Write(unusableJson);
            Debug.LogWarning($"[Save] Starting fresh because {why}. The old save was copied to the backup slot.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Starting fresh because {why}. Couldn't back it up: {e.Message}");
        }
        return GameState.CreateNew(design);
    }

    public void Save(GameState state)
    {
        try
        {
            _storage.Write(JsonUtility.ToJson(state));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Couldn't save: {e.Message}");
        }
    }

    public void Delete() => _storage.Delete();
}
