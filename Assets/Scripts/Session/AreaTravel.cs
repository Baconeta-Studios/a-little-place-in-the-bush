using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utils;

/// <summary>
/// Moves the player between areas: fade out, load the area scene, put the gecko on the right
/// spawn point, fade in and show the area's name. Input is locked the whole time and a second
/// request is ignored, so the transition state is always predictable. Created on first use and
/// kept across scenes, so no setup is needed.
/// </summary>
public class AreaTravel : EverlastingSingleton<AreaTravel>
{
    [SerializeField, Min(0f)] private float fadeOutTime = 0.35f;
    [SerializeField, Min(0f)] private float fadeInTime = 0.45f;
    [SerializeField, Min(0f)] private float nameShowTime = 1.6f;

    private CanvasGroup _fade;
    private CanvasGroup _banner;
    private TMP_Text _bannerText;
    private Coroutine _nameRoutine;

    /// <summary>Scene to return to when the player is stuck. Set by the hub's <see cref="AreaInfo"/>.</summary>
    public static string HubSceneName { get; set; } = "Cafe";

    public bool IsTravelling { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        name = "[AreaTravel]";
        BuildOverlay();
    }

    /// <summary>Go to another area scene (must be in the build list) and arrive at a spawn point.</summary>
    public bool Travel(string sceneName, string spawnId)
    {
        if (IsTravelling) return false;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[AreaTravel] Scene '{sceneName}' isn't in the build list (File > Build Profiles).");
            return false;
        }

        StartCoroutine(TravelRoutine(sceneName, spawnId));
        return true;
    }

    /// <summary>Back to the hub's default spawn: the safe way out if the player gets stuck.</summary>
    public bool ReturnToHub() => Travel(HubSceneName, null);

    /// <summary>Put the gecko on a spawn point in the current scene without loading anything.</summary>
    public static void PlacePlayer(string spawnId)
    {
        Interactor player = Interactor.Player;
        SpawnPoint spawn = SpawnPoint.Find(spawnId);
        if (!player || !spawn) return;

        player.Cancel();
        player.Mover.Teleport(spawn.transform.position);
        player.Mover.SetFacing(spawn.FaceRight);
        foreach (CameraFollow follow in FindObjectsByType<CameraFollow>()) follow.SnapToTarget();
    }

    private IEnumerator TravelRoutine(string sceneName, string spawnId)
    {
        IsTravelling = true;
        InputLock.Lock(this);
        if (Interactor.Player) Interactor.Player.Cancel();

        yield return Fade(_fade, 1f, fadeOutTime);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone) yield return null;
        yield return null; // let the new scene's Awake/Start run

        // Single-scene loads reset the popup pause, but make sure time runs in the new area.
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        PlacePlayer(spawnId);

        yield return Fade(_fade, 0f, fadeInTime);
        InputLock.Unlock(this);
        IsTravelling = false;

        ShowAreaName();
    }

    /// <summary>Shows the current area's name for a moment (also used when the game starts).</summary>
    public void ShowAreaName()
    {
        AreaInfo area = AreaInfo.Current;
        if (!area || string.IsNullOrEmpty(area.DisplayName)) return;
        if (_nameRoutine != null) StopCoroutine(_nameRoutine);
        _nameRoutine = StartCoroutine(NameRoutine(area.DisplayName));
    }

    private IEnumerator NameRoutine(string areaName)
    {
        _bannerText.text = areaName;
        yield return Fade(_banner, 1f, 0.3f);
        yield return new WaitForSecondsRealtime(nameShowTime);
        yield return Fade(_banner, 0f, 0.5f);
    }

    private static IEnumerator Fade(CanvasGroup group, float to, float duration)
    {
        float from = group.alpha;
        group.blocksRaycasts = true;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
        group.blocksRaycasts = to > 0.01f && group.interactable;
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("Overlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // Full-screen fade. Blocks clicks while visible so nothing happens mid-transition.
        var fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        Stretch(fadeObject.GetComponent<RectTransform>());
        fadeObject.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.05f);
        _fade = fadeObject.GetComponent<CanvasGroup>();
        _fade.alpha = 0f;
        _fade.interactable = true;
        _fade.blocksRaycasts = false;

        // Area name banner near the top, inside the safe area.
        var bannerObject = new GameObject("AreaName", typeof(RectTransform), typeof(CanvasGroup));
        bannerObject.transform.SetParent(canvasObject.transform, false);
        var bannerRect = bannerObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -140f);
        bannerRect.sizeDelta = new Vector2(1200f, 120f);
        _banner = bannerObject.GetComponent<CanvasGroup>();
        _banner.alpha = 0f;
        _banner.interactable = false;
        _banner.blocksRaycasts = false;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(bannerObject.transform, false);
        Stretch(textObject.GetComponent<RectTransform>());
        _bannerText = textObject.GetComponent<TextMeshProUGUI>();
        _bannerText.alignment = TextAlignmentOptions.Center;
        _bannerText.fontSize = 72f;
        _bannerText.color = new Color(1f, 0.96f, 0.86f);
        _bannerText.outlineWidth = 0.2f;
        _bannerText.outlineColor = new Color32(40, 30, 20, 255);
        _bannerText.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
