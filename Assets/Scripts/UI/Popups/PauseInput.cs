using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UI.Popups
{
    /// <summary>
    /// Escape / gamepad Start closes the top popup, or opens the pause popup if nothing is open.
    /// Also drives the on-screen pause button (needed on touch devices) and auto-pauses when the
    /// player switches app or browser tab. Put this on an object that stays active (e.g. the popup
    /// canvas), not on the popup itself.
    /// </summary>
    public class PauseInput : MonoBehaviour
    {
        [SerializeField] private PausePopup pausePopup;
        [Tooltip("Turn off in scenes that shouldn't be pausable, e.g. the main menu.")]
        [SerializeField] private bool allowPause = true;
        [Tooltip("Optional on-screen button that opens the pause popup. Hidden while any popup is open.")]
        [SerializeField] private GameObject pauseButton;
        [Tooltip("Open the pause popup when the app/tab loses focus (phone call, app switch, tab change).")]
        [SerializeField] private bool pauseOnFocusLoss = true;

        public bool AllowPause
        {
            get => allowPause;
            set => allowPause = value;
        }

        private void Update()
        {
            if (pauseButton) pauseButton.SetActive(allowPause && !PopupManager.AnyOpen);

            if (!BackPressedThisFrame()) return;

            if (PopupManager.CloseTop()) return;

            Pause();
        }

        /// <summary>Opens the pause popup if pausing is allowed and nothing else is open. Wire the pause button here.</summary>
        public void Pause()
        {
            if (allowPause && !PopupManager.AnyOpen && pausePopup)
            {
                pausePopup.Open();
            }
        }

        // Skipped in the editor, where clicking any other window would pause the game.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && pauseOnFocusLoss && !Application.isEditor) Pause();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused && pauseOnFocusLoss && !Application.isEditor) Pause();
        }

        private static bool BackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
