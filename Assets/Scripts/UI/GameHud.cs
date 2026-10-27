using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's HUD buttons, top left: coins, Bag (the read-only inventory), and Build (only in areas
/// with a <see cref="BuildArea"/>, hidden while building). Put it on Systems in each area scene.
/// </summary>
public class GameHud : MonoBehaviour
{
    private RectTransform _bar;
    private Button _buildButton;
    private TextMeshProUGUI _coins;
    private InventoryPopup _bag;

    private void Start()
    {
        GameUi ui = GameUi.Current;
        _bar = GameUi.NewRect("HudBar", ui.SafeRoot);
        GameUi.Place(_bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(700f, 110f));
        HorizontalLayoutGroup row = GameUi.AddLayout<HorizontalLayoutGroup>(_bar, 14f);
        row.childAlignment = TextAnchor.MiddleLeft;

        RectTransform coins = GameUi.NewRect("Coins", _bar);
        var coinsLayout = coins.gameObject.AddComponent<LayoutElement>();
        coinsLayout.preferredWidth = 200f;
        coinsLayout.preferredHeight = 90f;
        GameUi.AddPanel(coins, GameUi.PanelDark).raycastTarget = false;
        _coins = GameUi.AddText(coins, "", 32f, GameUi.Cream);

        GameUi.AddButton(_bar, "Bag", OpenBag, new Vector2(180f, 110f), GameUi.Cream, GameUi.Ink);
        _buildButton = GameUi.AddButton(_bar, "Build", EnterBuild, new Vector2(200f, 110f), GameUi.Moss, GameUi.Cream);
        _buildButton.gameObject.SetActive(false);

        BuildMode.ActiveChanged += OnBuildModeChanged;
        GameSession.Ready += OnSessionReady;
        OnSessionReady();
    }

    private void OnDestroy()
    {
        BuildMode.ActiveChanged -= OnBuildModeChanged;
        GameSession.Ready -= OnSessionReady;
        if (GameSession.Current != null) GameSession.Current.Events.CurrencyChanged -= OnCurrencyChanged;
        if (_bar) Destroy(_bar.gameObject);
        if (_bag) Destroy(_bag.gameObject);
    }

    private void Update()
    {
        // Show Build once the area has loaded its placed things.
        BuildArea area = BuildArea.Current;
        bool canBuild = area && area.IsLoaded && !IsBuilding(area);
        if (_buildButton && _buildButton.gameObject.activeSelf != canBuild) _buildButton.gameObject.SetActive(canBuild);
    }

    private void OnSessionReady()
    {
        GameSession session = GameSession.Current;
        if (session == null || !_coins) return;
        session.Events.CurrencyChanged -= OnCurrencyChanged;
        session.Events.CurrencyChanged += OnCurrencyChanged;
        OnCurrencyChanged(session.Economy.Currency);
    }

    private void OnCurrencyChanged(int coins) => _coins.text = $"{coins} coins";

    private void OnBuildModeChanged(bool building) => _bar.gameObject.SetActive(!building);

    private void OpenBag()
    {
        if (!_bag) _bag = InventoryPopup.Create();
        _bag.Open();
    }

    private static void EnterBuild()
    {
        BuildArea area = BuildArea.Current;
        if (area) area.GetComponent<BuildMode>().Enter();
    }

    private static bool IsBuilding(BuildArea area)
    {
        var mode = area.GetComponent<BuildMode>();
        return mode && mode.IsActive;
    }
}
