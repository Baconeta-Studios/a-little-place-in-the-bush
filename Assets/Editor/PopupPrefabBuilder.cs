using HighScores;
using TMPro;
using UI.Popups;
using UI.Settings;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Editor
{
    /// <summary>
    /// Generates Assets/Prefabs/UI/Popups/PopupCanvas.prefab: pause, settings, end game and high score
    /// popups wired up and ready to drop into a scene. Re-run after changing popup scripts' fields,
    /// or restyle the generated prefab by hand and stop using this.
    /// </summary>
    public static class PopupPrefabBuilder
    {
        private const string Folder = "Assets/Prefabs/UI/Popups";
        private const string PrefabPath = Folder + "/PopupCanvas.prefab";
        private const string MixerPath = "Assets/Audio/AudioMixer.mixer";

        // ~48pt on a landscape phone; Apple/Google recommend at least 44-48pt touch targets.
        private const float ButtonHeight = 110f;
        private const float ButtonFontSize = 44f;

        private static readonly Color DimColour = new(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColour = new(0.14f, 0.14f, 0.18f, 0.97f);

        private static TMP_DefaultControls.Resources _tmpResources;
        private static DefaultControls.Resources _uiResources;

        [MenuItem("Tools/Jam Template/Rebuild Popup Canvas Prefab")]
        public static void Build()
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogError("[PopupPrefabBuilder] No default TMP font. Import TMP Essential Resources first (Window > TextMeshPro).");
                return;
            }

            LoadResources();

            GameObject root = new("PopupCanvas", typeof(RectTransform));
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();
                PauseInput pauseInput = root.AddComponent<PauseInput>();

                GameObject pauseButton = BuildPauseButton(root.transform, pauseInput);

                SimplePopup settings = BuildSettingsPopup(root.transform);
                PausePopup pause = BuildPausePopup(root.transform, settings);
                BuildEndGamePopup(root.transform);
                BuildHighScoresPopup(root.transform);

                // Settings must render above pause, so move it last.
                settings.transform.SetAsLastSibling();

                SetField(pauseInput, "pausePopup", pause);
                SetField(pauseInput, "pauseButton", pauseButton);

                if (!AssetDatabase.IsValidFolder(Folder))
                {
                    AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Popups");
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                Debug.Log(success
                    ? $"[PopupPrefabBuilder] Saved {PrefabPath}"
                    : $"[PopupPrefabBuilder] Failed to save {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Touch devices have no Escape key, so gameplay needs a visible pause button.
        private static GameObject BuildPauseButton(Transform parent, PauseInput pauseInput)
        {
            GameObject safeArea = new("HudSafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(parent, false);
            Stretch(safeArea.GetComponent<RectTransform>());
            // Unity's built-in Safe Area (uGUI 2.6+). Edges default to none, so inset all four.
            safeArea.AddComponent<SafeArea>().Edges =
                SafeArea.SafeAreaMode.Top | SafeArea.SafeAreaMode.Right | SafeArea.SafeAreaMode.Bottom | SafeArea.SafeAreaMode.Left;

            Button button = AddButton(safeArea.transform, "II", pauseInput.Pause);
            button.name = "PauseButton";
            button.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(ButtonHeight, ButtonHeight);
            return button.gameObject;
        }

        private static PausePopup BuildPausePopup(Transform parent, Popup settings)
        {
            PausePopup popup = CreatePopup<PausePopup>(parent, "PausePopup", "Paused", out Transform panel);
            AddButton(panel, "Resume", popup.Close);
            AddButton(panel, "Settings", popup.OpenSettings);
            AddButton(panel, "Restart", popup.RestartScene);
            AddButton(panel, "Main Menu", popup.LoadMainMenu);
            Button quit = AddButton(panel, "Quit", popup.QuitGame);

            SetField(popup, "settingsPopup", settings);
            SetField(popup, "quitButton", quit.gameObject);
            return popup;
        }

        private static SimplePopup BuildSettingsPopup(Transform parent)
        {
            SimplePopup popup = CreatePopup<SimplePopup>(parent, "SettingsPopup", "Settings", out Transform panel);
            // Opened from pause (already paused) or a menu; it shouldn't pause on its own.
            SetField(popup, "pausesGame", false);

            Slider music = AddSlider(panel, "Music");
            Slider sfx = AddSlider(panel, "Sound Effects");
            Slider ambient = AddSlider(panel, "Ambience");
            AddButton(panel, "Back", popup.Close);

            VolumeSettings volume = popup.gameObject.AddComponent<VolumeSettings>();
            SetField(volume, "mixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
            SetField(volume, "musicSlider", music);
            SetField(volume, "sfxSlider", sfx);
            SetField(volume, "ambientSlider", ambient);
            return popup;
        }

        private static void BuildEndGamePopup(Transform parent)
        {
            EndGamePopup popup = CreatePopup<EndGamePopup>(parent, "EndGamePopup", "Game Over", out Transform panel);
            SetField(popup, "closesOnBack", false);
            SetField(popup, "titleText", panel.Find("Title").GetComponent<TMP_Text>());
            SetField(popup, "scoreText", AddText(panel, "Score: 0", 44, "Score"));

            GameObject nameRow = AddRow(panel, "NameEntry").gameObject;

            GameObject inputGo = TMP_DefaultControls.CreateInputField(_tmpResources);
            inputGo.name = "NameInput";
            inputGo.transform.SetParent(nameRow.transform, false);
            TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
            input.pointSize = 40;
            input.placeholder.GetComponent<TMP_Text>().text = "Enter your name...";
            input.placeholder.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            inputGo.AddComponent<LayoutElement>().flexibleWidth = 1;

            Button submit = AddButton(nameRow.transform, "Submit", popup.SubmitScore);
            submit.GetComponent<LayoutElement>().preferredWidth = 240;

            HighScoreTable table = AddHighScoreTable(panel);

            // Side by side to keep the popup short enough for landscape phones.
            Transform buttonRow = AddRow(panel, "Buttons");
            AddButton(buttonRow, "Retry", popup.RestartScene).GetComponent<LayoutElement>().flexibleWidth = 1;
            AddButton(buttonRow, "Main Menu", popup.LoadMainMenu).GetComponent<LayoutElement>().flexibleWidth = 1;

            SetField(popup, "nameEntryRoot", nameRow);
            SetField(popup, "nameInput", input);
            SetField(popup, "submitButton", submit);
            SetField(popup, "highScoreTable", table);
        }

        private static void BuildHighScoresPopup(Transform parent)
        {
            SimplePopup popup = CreatePopup<SimplePopup>(parent, "HighScoresPopup", "High Scores", out Transform panel);
            SetField(popup, "pausesGame", false);
            AddHighScoreTable(panel);
            AddButton(panel, "Back", popup.Close);
        }

        private static T CreatePopup<T>(Transform parent, string name, string title, out Transform panel) where T : Popup
        {
            GameObject popupGo = new(name, typeof(RectTransform));
            popupGo.transform.SetParent(parent, false);
            Stretch(popupGo.GetComponent<RectTransform>());
            // Full-screen dimmer also blocks clicks reaching the game underneath.
            popupGo.AddComponent<Image>().color = DimColour;
            T popup = popupGo.AddComponent<T>();

            GameObject panelGo = new("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(popupGo.transform, false);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820, 0);

            Image panelImage = panelGo.AddComponent<Image>();
            panelImage.sprite = _uiResources.background;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelColour;

            VerticalLayoutGroup layout = panelGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 32, 40);
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panelGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panelGo.transform, title, 64, "Title").fontStyle = FontStyles.Bold;

            popupGo.SetActive(false);
            panel = panelGo.transform;
            return popup;
        }

        private static Transform AddRow(Transform parent, string name)
        {
            GameObject row = new(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            SetHeight(row, ButtonHeight);
            return row.transform;
        }

        private static TMP_Text AddText(Transform parent, string text, float size, string name)
        {
            GameObject go = TMP_DefaultControls.CreateText(_tmpResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button AddButton(Transform parent, string label, UnityAction onClick)
        {
            GameObject go = TMP_DefaultControls.CreateButton(_tmpResources);
            go.name = label.Replace(" ", string.Empty) + "Button";
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = ButtonFontSize;
            text.color = new Color(0.12f, 0.12f, 0.12f);
            SetHeight(go, ButtonHeight);

            Button button = go.GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(button.onClick, onClick);
            return button;
        }

        private static Slider AddSlider(Transform parent, string label)
        {
            AddText(parent, label, 32, label + "Label").alignment = TextAlignmentOptions.Left;

            GameObject go = DefaultControls.CreateSlider(_uiResources);
            go.name = label.Replace(" ", string.Empty) + "Slider";
            go.transform.SetParent(parent, false);
            SetHeight(go, 64);
            // Default handle is tiny; widen it so it can be grabbed with a thumb.
            go.transform.Find("Handle Slide Area/Handle").GetComponent<RectTransform>().sizeDelta = new Vector2(56, 0);

            Slider slider = go.GetComponent<Slider>();
            // Log10(0) is -Infinity, which silences the mixer group permanently.
            slider.minValue = 0.0001f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            return slider;
        }

        private static HighScoreTable AddHighScoreTable(Transform parent)
        {
            TMP_Text text = AddText(parent, "1.<pos=12%>Name<pos=75%>0", 30, "HighScoreTable");
            text.alignment = TextAlignmentOptions.TopLeft;
            // Tall enough for HighScoreManager.MaxEntries (10) rows at this font size.
            text.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 390);
            SetHeight(text.gameObject, 390);

            HighScoreTable table = text.gameObject.AddComponent<HighScoreTable>();
            SetField(table, "tableText", text);
            return table;
        }

        private static void SetHeight(GameObject go, float height)
        {
            if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            SerializedObject so = new(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[PopupPrefabBuilder] {target.GetType().Name} has no serialized field '{fieldName}'.");
                return;
            }

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case Object o: prop.objectReferenceValue = o; break;
                default:
                    Debug.LogError($"[PopupPrefabBuilder] Unsupported value type for '{fieldName}'.");
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LoadResources()
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            Sprite inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            Sprite dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            Sprite mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");

            _tmpResources = new TMP_DefaultControls.Resources
            {
                standard = uiSprite, background = background, inputField = inputField,
                knob = knob, checkmark = checkmark, dropdown = dropdown, mask = mask
            };
            _uiResources = new DefaultControls.Resources
            {
                standard = uiSprite, background = background, inputField = inputField,
                knob = knob, checkmark = checkmark, dropdown = dropdown, mask = mask
            };
        }
    }
}
