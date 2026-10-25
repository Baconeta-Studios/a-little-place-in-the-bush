using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI.Popups
{
    /// <summary>
    /// Tracks open popups and pauses the game (Time.timeScale = 0) while any popup with
    /// pausesGame is open. Static so popups can register/unregister safely during scene
    /// unloads and application quit without spawning a singleton object.
    /// </summary>
    public static class PopupManager
    {
        private static readonly List<Popup> OpenPopups = new();
        private static float _timeScaleBeforePause = 1f;
        private static bool _isPaused;

        public static bool AnyOpen => OpenPopups.Count > 0;
        public static bool IsPaused => _isPaused;

        // Static state survives play mode restarts when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenPopups.Clear();
            _timeScaleBeforePause = 1f;
            _isPaused = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            // Popups from the old scene are gone; never carry a pause into a fresh scene.
            OpenPopups.Clear();
            if (_isPaused)
            {
                Time.timeScale = _timeScaleBeforePause;
                _isPaused = false;
            }
        }

        internal static void Register(Popup popup)
        {
            if (!OpenPopups.Contains(popup)) OpenPopups.Add(popup);
            RefreshPause();
        }

        internal static void Unregister(Popup popup)
        {
            OpenPopups.Remove(popup);
            RefreshPause();
        }

        /// <summary>
        /// Closes the most recently opened popup if it allows closing via back/escape.
        /// Returns false if nothing was closed.
        /// </summary>
        public static bool CloseTop()
        {
            if (OpenPopups.Count == 0) return false;

            Popup top = OpenPopups[^1];
            if (!top.ClosesOnBack) return false;

            top.Close();
            return true;
        }

        public static void CloseAll()
        {
            foreach (Popup popup in OpenPopups.ToArray())
            {
                popup.Close();
            }
        }

        private static void RefreshPause()
        {
            bool shouldPause = OpenPopups.Exists(p => p.PausesGame);
            if (shouldPause == _isPaused) return;

            if (shouldPause)
            {
                _timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _timeScaleBeforePause;
            }

            _isPaused = shouldPause;
        }
    }
}
