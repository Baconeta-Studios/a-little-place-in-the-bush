using System;
using System.Collections.Generic;
using HighScores;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Object = UnityEngine.Object;

namespace Debugging
{
    /// <summary>
    /// Developer cheat overlay, toggled with ` (backquote) or F1, or on touch screens by tapping the
    /// top-left corner three times quickly. Created automatically - no prefab needed.
    ///
    /// Available in the editor and development builds. To ship it in a release build (e.g. for
    /// playtesters), add ENABLE_CHEATS to Player Settings > Scripting Define Symbols.
    ///
    /// Register cheats from game code, usually in Start:
    ///   CheatMenu.AddButton("Add 100 score", () => score += 100, this);
    ///   CheatMenu.AddToggle("God mode", () => godMode, v => godMode = v, this);
    /// Passing an owner removes the cheat automatically when that object is destroyed.
    /// Re-registering the same label replaces the old entry.
    /// </summary>
    public static class CheatMenu
    {
#if UNITY_EDITOR || DEBUG || ENABLE_CHEATS
        public const bool IsAvailable = true;
#else
        public const bool IsAvailable = false;
#endif

        internal class Entry
        {
            public string Label;
            public Action Action;
            public Func<bool> Getter;
            public Action<bool> Setter;
            public Object Owner;
            public bool HasOwner;

            public bool IsOrphaned => HasOwner && !Owner;
        }

        internal static readonly List<Entry> Entries = new();

        public static bool IsOpen { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            IsOpen = false;
        }

#pragma warning disable CS0162 // Unreachable code detected (IsAvailable is a compile-time constant)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOverlay()
        {
            if (!IsAvailable) return;

            GameObject go = new("[CheatMenu]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CheatMenuOverlay>();
            RegisterBuiltIns();
        }

        public static void AddButton(string label, Action action, Object owner = null)
        {
            if (!IsAvailable) return;
            Add(new Entry { Label = label, Action = action, Owner = owner, HasOwner = owner != null });
        }

        public static void AddToggle(string label, Func<bool> getter, Action<bool> setter, Object owner = null)
        {
            if (!IsAvailable) return;
            Add(new Entry { Label = label, Getter = getter, Setter = setter, Owner = owner, HasOwner = owner != null });
        }
#pragma warning restore CS0162

        public static void Remove(string label)
        {
            Entries.RemoveAll(e => e.Label == label);
        }

        private static void Add(Entry entry)
        {
            Remove(entry.Label);
            Entries.Add(entry);
        }

        private static void RegisterBuiltIns()
        {
            AddButton("Time scale: cycle 1x / 2x / 4x / 0.25x", () =>
            {
                Time.timeScale = Time.timeScale switch
                {
                    1f => 2f,
                    2f => 4f,
                    4f => 0.25f,
                    _ => 1f
                };
                Debug.Log($"[CheatMenu] Time scale = {Time.timeScale}");
            });
            AddButton("Reload scene", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            AddButton("Next scene in build", () =>
            {
                int next = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
                SceneManager.LoadScene(next);
            });
            AddButton("Clear high scores", HighScoreManager.ClearAll);
            AddButton("Clear ALL PlayerPrefs (settings, achievements...)", () =>
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            });
        }
    }

    internal class CheatMenuOverlay : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private const int WindowId = 0x43484554;
        private const float RowHeight = 36f;
        private const float CornerTapSize = 0.12f;
        private const float CornerTapWindow = 0.8f;
        private const int CornerTapsRequired = 3;

        private Rect _windowRect = new(10, 10, 380, 500);
        private int _cornerTaps;
        private float _lastCornerTapTime;
        private Vector2 _scroll;
        private bool _showFps;
        private float _smoothedDeltaTime;
        private CheatMenu.Entry[] _drawnEntries = System.Array.Empty<CheatMenu.Entry>();

        private void Update()
        {
            if (TogglePressedThisFrame() || CornerTappedEnough()) CheatMenu.IsOpen = !CheatMenu.IsOpen;
            CheatMenu.Entries.RemoveAll(e => e.IsOrphaned);

            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
        }

        private static bool TogglePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1);
#endif
        }

        // Pointer covers mouse and touch, so this also works on mobile web.
        private bool CornerTappedEnough()
        {
#if ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            Vector2 pos = pointer.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0)) return false;
            Vector2 pos = Input.mousePosition;
#endif
            // Screen space origin is bottom-left, so "top-left" means high y.
            bool inCorner = pos.x < Screen.width * CornerTapSize && pos.y > Screen.height * (1f - CornerTapSize);
            if (!inCorner)
            {
                _cornerTaps = 0;
                return false;
            }

            if (Time.unscaledTime - _lastCornerTapTime > CornerTapWindow) _cornerTaps = 0;
            _lastCornerTapTime = Time.unscaledTime;
            _cornerTaps++;

            if (_cornerTaps < CornerTapsRequired) return false;
            _cornerTaps = 0;
            return true;
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / ReferenceHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (_showFps && _smoothedDeltaTime > 0f)
            {
                GUI.Label(new Rect(Screen.width / scale - 90, 5, 85, 22), $"{1f / _smoothedDeltaTime:0} FPS");
            }

            if (!CheatMenu.IsOpen) return;

            _windowRect = GUILayout.Window(WindowId, _windowRect, DrawWindow, "Cheats");
        }

        private void DrawWindow(int id)
        {
            // IMGUI needs the same controls in Layout and Repaint, so only pick up list changes on Layout.
            if (Event.current.type == EventType.Layout) _drawnEntries = CheatMenu.Entries.ToArray();

            GUILayoutOption row = GUILayout.Height(RowHeight);

            if (GUILayout.Button("Close  ( ` / F1 / triple-tap corner )", row)) CheatMenu.IsOpen = false;

            _scroll = GUILayout.BeginScrollView(_scroll);

            _showFps = GUILayout.Toggle(_showFps, "Show FPS", row);

            foreach (CheatMenu.Entry entry in _drawnEntries)
            {
                if (entry.Getter != null)
                {
                    bool current = entry.Getter();
                    bool next = GUILayout.Toggle(current, entry.Label, row);
                    if (next != current) SafeInvoke(() => entry.Setter?.Invoke(next), entry.Label);
                }
                else if (GUILayout.Button(entry.Label, row))
                {
                    SafeInvoke(entry.Action, entry.Label);
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private static void SafeInvoke(Action action, string label)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(new Exception($"[CheatMenu] '{label}' threw", e));
            }
        }
    }
}
