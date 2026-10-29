using System.Text;
using Bush.Core;
using TMPro;
using UI.Popups;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Cafe upgrades, opened from the menu board: each bought once with coins, in ladders (the second
/// menu slot before the third), pricier further down. A template popup, so it pauses the game and
/// Escape closes it. Built in code.
/// </summary>
public class UpgradesPopup : Popup
{
    private static UpgradesPopup _instance;

    private TextMeshProUGUI _coins;
    private RectTransform _list;

    public static void Show()
    {
        if (GameSession.Current == null) return;
        if (!_instance) _instance = Create();
        _instance.Open();
        _instance.Refresh();
    }

    private static UpgradesPopup Create()
    {
        RectTransform root = GameUi.NewRect("Upgrades", GameUi.Current.SafeRoot);
        GameUi.Stretch(root);
        root.gameObject.SetActive(false); // a popup is "open" while active
        var popup = root.gameObject.AddComponent<UpgradesPopup>();
        popup.Build(root);
        return popup;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Current;
        if (session == null) return;
        _coins.text = $"{session.Economy.Currency} coins";

        for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
        if (session.Design.rules.upgrades.Count == 0)
        {
            GameUi.AddText(_list, "No upgrades yet (add some to the design data).", 30f, GameUi.InkSoft, TextAlignmentOptions.Center);
            return;
        }

        foreach (UpgradeData upgrade in session.Design.rules.upgrades)
        {
            bool owned = session.Cafe.HasUpgrade(upgrade.id);
            bool canBuy = session.Cafe.CanBuyUpgrade(upgrade.id, out string reason);
            // Hide the far end of a ladder until the step before it is bought, so the list stays short.
            if (!owned && !string.IsNullOrEmpty(upgrade.requires) && !session.Cafe.HasUpgrade(upgrade.requires)) continue;
            string id = upgrade.id;
            AddRow(upgrade, owned, canBuy, reason, () =>
            {
                if (session.Cafe.BuyUpgrade(id)) session.SaveNow();
                Refresh();
            });
        }
    }

    private void AddRow(UpgradeData upgrade, bool owned, bool canBuy, string reason, UnityAction buy)
    {
        RectTransform row = GameUi.NewRect(upgrade.id, _list);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 120f;
        GameUi.AddPanel(row, owned ? GameUi.MossLight : GameUi.Cream);

        var text = new StringBuilder($"<b>{upgrade.name}</b>");
        if (!string.IsNullOrEmpty(upgrade.description))
            text.Append($"\n<size=85%><color=#{ColorUtility.ToHtmlStringRGB(GameUi.InkSoft)}>{upgrade.description}</color></size>");
        TextMeshProUGUI label = GameUi.AddText(row, text.ToString(), 32f, GameUi.Ink, TextAlignmentOptions.MidlineLeft, "Text");
        label.rectTransform.offsetMin = new Vector2(24f, 6f);
        label.rectTransform.offsetMax = new Vector2(-300f, -6f);

        string buttonLabel = owned ? "Done" : $"{upgrade.price} coins";
        Button button = GameUi.AddButton(row, buttonLabel, buy, new Vector2(260f, 90f), owned ? GameUi.MossLight : GameUi.Moss,
            owned ? GameUi.Ink : GameUi.Cream, 30f);
        GameUi.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(260f, 90f));
        button.interactable = !owned && canBuy;
    }

    private void Build(RectTransform root)
    {
        GameUi.AddPanel(root, new Color(0.08f, 0.07f, 0.05f, 0.55f), false);

        RectTransform panel = GameUi.NewRect("Panel", root);
        panel.anchorMin = new Vector2(0.15f, 0.06f);
        panel.anchorMax = new Vector2(0.85f, 0.94f);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        GameUi.AddPanel(panel, GameUi.Panel);

        TextMeshProUGUI title = GameUi.AddText(panel, "Cafe upgrades", 50f, GameUi.Ink, TextAlignmentOptions.TopLeft, "Title");
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.offsetMin = new Vector2(40f, 0f);
        title.rectTransform.offsetMax = new Vector2(-480f, -28f);
        _coins = GameUi.AddText(panel, "", 34f, GameUi.Bark, TextAlignmentOptions.TopRight, "Coins");
        _coins.rectTransform.offsetMax = new Vector2(-250f, -38f);

        Button close = GameUi.AddButton(panel, "Close", Close, new Vector2(190f, 84f), GameUi.Bark, GameUi.Cream);
        GameUi.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 84f));

        // A scrolling column of rows.
        RectTransform area = GameUi.NewRect("Scroll", panel);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(32f, 32f);
        area.offsetMax = new Vector2(-32f, -130f);
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        RectTransform viewport = GameUi.NewRect("Viewport", area);
        GameUi.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear; // lets drags on empty space scroll
        scroll.viewport = viewport;

        _list = GameUi.NewRect("Content", viewport);
        _list.anchorMin = new Vector2(0f, 1f);
        _list.anchorMax = new Vector2(1f, 1f);
        _list.pivot = new Vector2(0.5f, 1f);
        _list.offsetMin = _list.offsetMax = Vector2.zero;
        _list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        VerticalLayoutGroup column = GameUi.AddLayout<VerticalLayoutGroup>(_list, 12f);
        column.childForceExpandWidth = true;
        scroll.content = _list;
    }
}
