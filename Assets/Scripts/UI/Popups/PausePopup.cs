using UnityEngine;

namespace UI.Popups
{
    /// <summary>
    /// Pause menu. Buttons: Resume -> Close, Settings -> OpenSettings, Restart -> RestartScene,
    /// Main Menu -> LoadMainMenu, Quit -> QuitGame. Opened by <see cref="PauseInput"/>.
    /// </summary>
    public class PausePopup : Popup
    {
        [SerializeField] private Popup settingsPopup;

        public void OpenSettings()
        {
            if (settingsPopup) settingsPopup.Open();
        }
    }
}
