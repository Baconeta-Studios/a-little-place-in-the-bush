using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighScores
{
    [Serializable]
    public class HighScoreEntry
    {
        public string playerName;
        public int score;
    }

    // JsonUtility cannot serialize a root List<T> directly - wrap it.
    [Serializable]
    internal class HighScoreList
    {
        public List<HighScoreEntry> entries = new();
    }

    /// <summary>
    /// Local high scores stored in PlayerPrefs as JSON.
    ///
    /// Usage:
    ///   if (HighScoreManager.IsHighScore(score)) { int rank = HighScoreManager.SaveScore(name, score); }
    ///   List&lt;HighScoreEntry&gt; top = HighScoreManager.GetTopScores();
    ///
    /// For time-trial style games set LowerIsBetter = true before saving or reading.
    /// </summary>
    public static class HighScoreManager
    {
        private const string PrefKey = "HighScores";
        private const string LastNameKey = "HighScores_LastName";
        public const int MaxNameLength = 12;

        public static int MaxEntries { get; set; } = 10;
        public static bool LowerIsBetter { get; set; }

        public static string LastPlayerName
        {
            get => PlayerPrefs.GetString(LastNameKey, string.Empty);
            private set => PlayerPrefs.SetString(LastNameKey, value);
        }

        public static bool IsHighScore(int score)
        {
            List<HighScoreEntry> entries = Load().entries;
            if (entries.Count < MaxEntries) return true;

            int worst = entries[^1].score;
            return LowerIsBetter ? score < worst : score > worst;
        }

        /// <summary>
        /// Adds a score, keeps the top <see cref="MaxEntries"/> and persists.
        /// Returns the 1-based rank of the new entry, or 0 if it didn't make the table.
        /// </summary>
        public static int SaveScore(string playerName, int score)
        {
            playerName = SanitiseName(playerName);
            HighScoreList list = Load();
            HighScoreEntry newEntry = new() { playerName = playerName, score = score };
            list.entries.Add(newEntry);
            Sort(list.entries);

            if (list.entries.Count > MaxEntries)
            {
                list.entries.RemoveRange(MaxEntries, list.entries.Count - MaxEntries);
            }

            LastPlayerName = playerName;
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(list));
            PlayerPrefs.Save();

            return list.entries.IndexOf(newEntry) + 1;
        }

        public static List<HighScoreEntry> GetTopScores() => Load().entries;

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        public static string SanitiseName(string playerName)
        {
            // Strip '<' so names can't inject TMP rich text tags into the table.
            playerName = (playerName ?? string.Empty).Replace("<", string.Empty).Trim();
            if (playerName.Length > MaxNameLength) playerName = playerName[..MaxNameLength];
            return playerName.Length == 0 ? "Anonymous" : playerName;
        }

        private static void Sort(List<HighScoreEntry> entries)
        {
            // List.Sort is unstable; tie-break on insertion order so earlier scores keep their rank.
            List<HighScoreEntry> original = new(entries);
            entries.Sort((a, b) =>
            {
                int cmp = LowerIsBetter ? a.score.CompareTo(b.score) : b.score.CompareTo(a.score);
                return cmp != 0 ? cmp : original.IndexOf(a).CompareTo(original.IndexOf(b));
            });
        }

        private static HighScoreList Load()
        {
            string json = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new HighScoreList();

            try
            {
                HighScoreList list = JsonUtility.FromJson<HighScoreList>(json) ?? new HighScoreList();
                list.entries ??= new List<HighScoreEntry>();
                Sort(list.entries);
                return list;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HighScoreManager] Could not parse saved scores ({e.Message}). Starting fresh.");
                return new HighScoreList();
            }
        }
    }
}
