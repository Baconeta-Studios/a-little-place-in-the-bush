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

    public SaveService(ISaveStorage storage)
    {
        _storage = storage;
    }

    /// <summary>PlayerPrefs on the web (reliable after a refresh), a file everywhere else.</summary>
    public static ISaveStorage CreateDefaultStorage() =>
        Application.platform == RuntimePlatform.WebGLPlayer
            ? new PlayerPrefsSaveStorage("bush.save.v1")
            : new FileSaveStorage("bush-save.json");

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
            Debug.LogWarning($"[Save] The save is unreadable, starting fresh: {e.Message}");
            return GameState.CreateNew(design);
        }

        if (state == null)
        {
            Debug.LogWarning("[Save] The save was empty, starting fresh.");
            return GameState.CreateNew(design);
        }

        if (state.saveVersion > GameState.CurrentSaveVersion)
        {
            Debug.LogWarning($"[Save] The save is from a newer version ({state.saveVersion}), starting fresh.");
            return GameState.CreateNew(design);
        }

        // Older saves would be upgraded here, one version at a time. There's only version 1 so far.
        state.saveVersion = GameState.CurrentSaveVersion;
        state.Normalise();
        return state;
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
