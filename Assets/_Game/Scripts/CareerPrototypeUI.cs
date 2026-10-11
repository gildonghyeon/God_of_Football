using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace MyFootballCareer
{
    // Temporary runtime UI. Gameplay and save rules are kept outside this class.
    public class CareerPrototypeUI : MonoBehaviour
    {
        Font font;
        CareerSettings settings;
        TrainingSettings trainingSettings;
        Action<string, Position, Trait> onCreate;
        Action onSave;
        Action<string> onSelectTraining;
        Action onStopTraining;
        RectTransform safeRoot, content, barFill;
        Text status, trainingText;
        Text[] statTexts = new Text[5];
        InputField nameInput;
        GameObject overlay;
        Position position;
        Trait trait;
        Rect lastSafe;
        int lastWidth, lastHeight;

        public void Initialize(Font customFont, CareerSettings config, TrainingSettings training,
            Action<string, Position, Trait> create, Action save,
            Action<string> selectTraining, Action stopTraining)
        {
            font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settings = config; trainingSettings = training;
            onCreate = create; onSave = save;
            onSelectTraining = selectTraining; onStopTraining = stopTraining;
            var canvasGO = new GameObject("CareerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0;
            safeRoot = Rect("SafeArea", canvasGO.transform);
            FitSafeArea();
            if (EventSystem.current == null)
            {
                var events = new GameObject("CareerEventSystem", typeof(EventSystem));
                events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
            var viewport = Rect("ScrollView", safeRoot);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(.055f,.075f,.11f,1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1);
            content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32,32,24,32); layout.spacing = 10;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
        }
        void Update()
        {
            if (safeRoot != null && (Screen.safeArea != lastSafe || Screen.width != lastWidth || Screen.height != lastHeight)) FitSafeArea();
        }
        void FitSafeArea()
        {
            lastSafe = Screen.safeArea; lastWidth = Screen.width; lastHeight = Screen.height;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safeRoot.anchorMin = new Vector2(lastSafe.xMin / lastWidth, lastSafe.yMin / lastHeight);
            safeRoot.anchorMax = new Vector2(lastSafe.xMax / lastWidth, lastSafe.yMax / lastHeight);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Height(GameObject go, float height) { go.AddComponent<LayoutElement>().preferredHeight = height; }
        Text TextNode(string value, Transform parent, int size)
        {
            var r = Rect("Text", parent); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            return t;
        }
        Text Label(string value, int size = 26, float height = 48)
        { var t = TextNode(value, content, size); Height(t.gameObject, height); return t; }
        Button Button(string value, Action action, Transform parent = null)
        {
            var r = Rect("Button", parent != null ? parent : content); Height(r.gameObject, 72);
            r.gameObject.AddComponent<Image>().color = new Color(.13f,.3f,.34f,1);
            var button = r.gameObject.AddComponent<Button>();
            var text = TextNode(value, r, 26); Stretch(text.rectTransform); text.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => action()); return button;
        }
        void Clear()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            content.anchoredPosition = Vector2.zero; status = null;
            Array.Clear(statTexts, 0, statTexts.Length);
            trainingText = null; barFill = null;
        }
        public void ShowCreation()
        {
            Clear(); Label("MY FOOTBALL CAREER", 36, 64); Label("Create your first player");
            Label("Player name");
            var inputRect = Rect("NameInput", content); Height(inputRect.gameObject, 80);
            inputRect.gameObject.AddComponent<Image>().color = new Color(.14f,.17f,.22f,1);
            nameInput = inputRect.gameObject.AddComponent<InputField>();
            var text = TextNode("", inputRect, 28); Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(18,8); text.rectTransform.offsetMax = new Vector2(-18,-8);
            nameInput.textComponent = text; nameInput.characterLimit = settings.maxNameLength;
            nameInput.lineType = InputField.LineType.SingleLine;
            var placeholder = TextNode("Enter a name", inputRect, 26); Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(18,8); placeholder.rectTransform.offsetMax = new Vector2(-18,-8);
            placeholder.color = Color.gray; nameInput.placeholder = placeholder;
            Button positionButton = null;
            positionButton = Button("Position: " + position, () => {
                position = (Position)(((int)position + 1) % 3);
                positionButton.GetComponentInChildren<Text>().text = "Position: " + position;
            });
            Button traitButton = null;
            traitButton = Button("Trait: " + trait, () => {
                trait = (Trait)(((int)trait + 1) % 3);
                traitButton.GetComponentInChildren<Text>().text = "Trait: " + trait;
            });
            Label("Finisher: Shooting / Playmaker: Passing\nBallWinner: Defending (bonus +" + settings.traitBonus + ")", 23, 90);
            Button("CREATE PLAYER", () => onCreate(nameInput.text, position, trait));
            status = Label("Choose position and trait by tapping each button.", 23, 110);
        }
        public void ShowPlayer(PlayerData p, string message)
        {
            Clear(); Label("PLAYER PROFILE", 32, 48);
            Label(p.playerName, 30, 48); Label("Position: " + p.position + "   Trait: " + p.trait, 24, 40);
            for (int i = 0; i < statTexts.Length; i++) statTexts[i] = Label("", 24, 38);
            UpdateStats(p);

            Label("TRAINING", 28, 44);
            trainingText = Label("", 24, 110);
            var bar = Rect("ProgressBar", content); Height(bar.gameObject, 24);
            bar.gameObject.AddComponent<Image>().color = new Color(.14f,.17f,.22f,1);
            barFill = Rect("Fill", bar);
            barFill.gameObject.AddComponent<Image>().color = new Color(.3f,.8f,.4f,1);
            SetFill(0f);

            foreach (var def in trainingSettings.trainings)
            {
                if (def == null) continue;
                string id = def.id;
                Button(def.displayName + "   +" + def.growthPerHour.ToString("0.#") + "/h", () => onSelectTraining(id));
            }
            Button("STOP TRAINING", () => onStopTraining());
            Button("SAVE", onSave);
            Label("Offline reward cap: " + trainingSettings.offlineCapHours.ToString("0.#") + "h\nMatches: not implemented yet.", 23, 80);
            status = Label(message, 23, 110);
        }
        void UpdateStats(PlayerData p)
        {
            for (int i = 0; i < statTexts.Length; i++)
            {
                if (statTexts[i] == null) continue;
                var kind = (AttributeKind)i;
                float value = p.Get(kind);
                statTexts[i].text = kind + "   " + value.ToString("0.00") + " / " + settings.attributeCap.ToString("0.#")
                    + (value >= settings.attributeCap ? "  MAX" : "");
            }
        }
        void SetFill(float fraction)
        {
            if (barFill == null) return;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        }
        // 플레이어 화면이 열려 있을 때만 능력치와 훈련 진행도를 갱신합니다.
        public void RefreshTraining(PlayerData p, TrainingSystem training)
        {
            if (trainingText == null || p == null) return;
            UpdateStats(p);
            var def = training.Current;
            if (def == null)
            {
                trainingText.text = "Now: none\nPick a training below.";
                SetFill(0f);
                return;
            }
            float remain = Mathf.Max(0f, training.Interval - training.CarrySeconds);
            trainingText.text = "Now: " + def.displayName + "\n+" + def.growthPerHour.ToString("0.#") + "/h  ->  "
                + def.targetAttribute + "\nNext reward in " + remain.ToString("0.0") + "s";
            SetFill(training.CarrySeconds / training.Interval);
        }
        public void ShowOfflineSummary(string title, string body)
        {
            if (overlay != null) Destroy(overlay);
            var root = Rect("OfflineSummary", safeRoot); Stretch(root); overlay = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            var panel = Rect("Panel", root);
            panel.anchorMin = new Vector2(.08f, .5f); panel.anchorMax = new Vector2(.92f, .5f);
            panel.pivot = new Vector2(.5f, .5f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(.1f, .14f, .2f, 1);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(32, 32, 32, 32); v.spacing = 20;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = TextNode(title, panel, 32); t.alignment = TextAnchor.MiddleCenter; Height(t.gameObject, 56);
            var b = TextNode(body, panel, 26); Height(b.gameObject, 190);
            Button("OK", () => { if (overlay != null) Destroy(overlay); overlay = null; }, panel);
        }
        public void SetStatus(string message) { if (status != null) status.text = message; }
        public void ShowError(string message)
        { Clear(); Label("SAVE COULD NOT BE LOADED", 30, 90); status = Label(message + "\nExisting save was not overwritten.", 23, 240); }
    }
}
