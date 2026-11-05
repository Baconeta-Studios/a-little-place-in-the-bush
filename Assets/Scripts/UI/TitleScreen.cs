using UI.Popups;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The title screen: New game, Continue, Settings and Credits. It's all plain objects in the Title
/// scene, so move and restyle them freely. Buttons are wired up here when the game runs.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [Tooltip("Only shows if there's a save to go back to.")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;

    [Header("Starting again (only asked if there's a save)")]
    [SerializeField] private Popup confirmNew;
    [SerializeField] private Button startAgainButton;
    [SerializeField] private Button confirmBackButton;

    [Header("Other screens")]
    [SerializeField] private Popup creditsPopup;
    [SerializeField] private Button creditsBackButton;
    [Tooltip("The settings from the PopupCanvas in this scene.")]
    [SerializeField] private Popup settingsPopup;

    [Header("Layout")]
    [Tooltip("Kept clear of phone notches and rounded corners.")]
    [SerializeField] private RectTransform safeArea;
    [Tooltip("The scene the game starts in.")]
    [SerializeField] private string gameScene = "Cafe";

    private Rect _appliedSafeArea;
    private bool _leaving;

    private void Awake()
    {
        // Back here through the pause menu: keep the game, and let the next scene load it again.
        GameSession session = GameSession.Current;
        if (!session) return;
        session.SaveNow();
        Destroy(session.gameObject);
    }

    private void Start()
    {
        continueButton.gameObject.SetActive(SaveService.HasSave());

        newGameButton.onClick.AddListener(NewGame);
        continueButton.onClick.AddListener(StartPlaying);
        settingsButton.onClick.AddListener(settingsPopup.Open);
        creditsButton.onClick.AddListener(creditsPopup.Open);
        startAgainButton.onClick.AddListener(StartAgain);
        confirmBackButton.onClick.AddListener(confirmNew.Close);
        creditsBackButton.onClick.AddListener(creditsPopup.Close);

        ApplySafeArea();
    }

    private void Update() => ApplySafeArea();

    private void NewGame()
    {
        if (SaveService.HasSave()) confirmNew.Open();
        else StartAgain();
    }

    private void StartAgain()
    {
        if (_leaving) return;
        SaveService.DeleteSave();
        StartPlaying();
    }

    private void StartPlaying()
    {
        if (_leaving) return;
        _leaving = true;
        SceneManager.LoadScene(gameScene);
    }

    private void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;
        if (!safeArea || safe == _appliedSafeArea || Screen.width == 0 || Screen.height == 0) return;
        _appliedSafeArea = safe;
        safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }
}
