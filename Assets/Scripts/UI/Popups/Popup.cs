using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

namespace UI.Popups
{
    /// <summary>
    /// Base class for any popup panel. Opening = activating the GameObject, closing = deactivating it.
    /// Wire UI buttons straight to Open/Close/Restart/LoadMainMenu/QuitGame.
    /// </summary>
    public abstract class Popup : MonoBehaviour
    {
        [SerializeField] private bool pausesGame = true;
        [Tooltip("Whether Escape / gamepad Start closes this popup when it is the top-most one.")]
        [SerializeField] private bool closesOnBack = true;
        [Tooltip("Leave empty to load the first scene in the build list.")]
        [SerializeField] private SceneReference mainMenuScene;
        [Tooltip("Hidden on WebGL, mobile and in the editor, where quitting does nothing useful.")]
        [SerializeField] private GameObject quitButton;

        public bool PausesGame => pausesGame;
        public bool ClosesOnBack => closesOnBack;
        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (quitButton) quitButton.SetActive(!UnityHelper.IsWebGL && !UnityHelper.IsMobile && !UnityHelper.IsInUnityEditor);
        }

        protected virtual void OnEnable()
        {
            PopupManager.Register(this);
        }

        protected virtual void OnDisable()
        {
            PopupManager.Unregister(this);
        }

        public virtual void Open()
        {
            gameObject.SetActive(true);
        }

        public virtual void Close()
        {
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void RestartScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void LoadMainMenu()
        {
            if (mainMenuScene != null && mainMenuScene.State == SceneReferenceState.Regular)
            {
                SceneManager.LoadScene(mainMenuScene.BuildIndex);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
