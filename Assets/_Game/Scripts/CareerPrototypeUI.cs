using System;
using System.Collections.Generic;
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
        Action onPlayMatch;
        Text seasonText, resultText, playLabel, standingsText, historyText;
        RectTransform transferRoot;
        Action<string> onChooseOffer;
        Action onStay;
        Action onRebirth;
        Text rebirthText;
        Button rebirthButton;
        public float TrainingMultiplier = 1f;   // 환생 보너스가 반영된 훈련 속도 배율
        Position position;
        Trait trait;
        Rect lastSafe;
        int lastWidth, lastHeight;

        public void Initialize(Font customFont, CareerSettings config, TrainingSettings training,
            Action<string, Position, Trait> create, Action save,
            Action<string> selectTraining, Action stopTraining, Action playMatch,
            Action<string> chooseOffer, Action stay, Action rebirth)
        {
            font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settings = config; trainingSettings = training;
            onCreate = create; onSave = save;
            onSelectTraining = selectTraining; onStopTraining = stopTraining; onPlayMatch = playMatch; onChooseOffer = chooseOffer; onStay = stay; onRebirth = rebirth;
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
        Button Button(string value, Action action, Transform parent = null, float height = 72f)
        {
            var r = Rect("Button", parent != null ? parent : content); Height(r.gameObject, height);
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
            seasonText = null; resultText = null; playLabel = null; standingsText = null; historyText = null; transferRoot = null; rebirthText = null; rebirthButton = null;
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
                Button(def.displayName + "   +" + (def.growthPerHour * TrainingMultiplier).ToString("0.#") + "/h", () => onSelectTraining(id));
            }
            Button("STOP TRAINING", () => onStopTraining());

            Label("SEASON", 28, 44);
            seasonText = Label("", 24, 80);
            var playButton = Button("PLAY NEXT MATCH", () => onPlayMatch());
            playLabel = playButton.GetComponentInChildren<Text>();
            resultText = Label("", 22, 130);
            transferRoot = Rect("TransferRoot", content);
            var transferLayout = transferRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            transferLayout.spacing = 10;
            transferLayout.childControlWidth = true; transferLayout.childControlHeight = true;
            transferLayout.childForceExpandWidth = true; transferLayout.childForceExpandHeight = false;
            Label("STANDINGS", 28, 44);
            standingsText = Label("", 22, 200);
            Label("HISTORY", 28, 44);
            historyText = Label("", 22, 60);
            Label("REBIRTH", 28, 44);
            rebirthText = Label("", 22, 120);
            rebirthButton = Button("REBIRTH", () => onRebirth());
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
            trainingText.text = "Now: " + def.displayName + "\n+" + (def.growthPerHour * TrainingMultiplier).ToString("0.#") + "/h  ->  "
                + def.targetAttribute + "\nNext reward in " + remain.ToString("0.0") + "s";
            SetFill(training.CarrySeconds / training.Interval);
        }
        public void ShowPopup(string title, string body, float bodyHeight = 190f, Action onClose = null)
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
            var b = TextNode(body, panel, 26); Height(b.gameObject, bodyHeight);
            Button("OK", () => { if (overlay != null) Destroy(overlay); overlay = null; if (onClose != null) onClose(); }, panel);
        }
        static void SetLines(Text t, string text, int minLines)
        {
            t.text = text;
            int lines = Mathf.Max(minLines, text.Split('\n').Length);
            t.GetComponent<LayoutElement>().preferredHeight = lines * 30f + 8f;
        }
        public void SetRebirth(string text, bool available)
        {
            if (rebirthText == null) return;
            SetLines(rebirthText, text, 4);
            rebirthButton.GetComponent<Image>().color = available ? new Color(.45f, .25f, .5f, 1) : new Color(.13f, .3f, .34f, .5f);
        }
        // 되돌릴 수 없는 선택 앞에서 한 번 더 확인합니다.
        public void ShowConfirm(string title, string body, float bodyHeight, string yesLabel, Action onYes)
        {
            if (overlay != null) Destroy(overlay);
            var root = Rect("ConfirmDialog", safeRoot); Stretch(root); overlay = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            var panel = Rect("Panel", root);
            panel.anchorMin = new Vector2(.06f, .5f); panel.anchorMax = new Vector2(.94f, .5f);
            panel.pivot = new Vector2(.5f, .5f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(.1f, .14f, .2f, 1);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 28, 28); v.spacing = 16;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = TextNode(title, panel, 32); t.alignment = TextAnchor.MiddleCenter; Height(t.gameObject, 56);
            var b = TextNode(body, panel, 23); Height(b.gameObject, bodyHeight);
            Button(yesLabel, () => { if (overlay != null) Destroy(overlay); overlay = null; if (onYes != null) onYes(); }, panel);
            Button("CANCEL", () => { if (overlay != null) Destroy(overlay); overlay = null; }, panel);
        }
        // 시즌이 끝나 이적 제안이 있을 때만 제안 목록을 보여줍니다.
        public void SetTransfer(List<TransferOffer> offers, string pendingTeamId, string currentTeamName)
        {
            if (transferRoot == null) return;
            foreach (Transform child in transferRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (offers == null || offers.Count == 0) return;

            var title = TextNode("TRANSFER OFFERS", transferRoot, 28); Height(title.gameObject, 44);
            string note = string.IsNullOrEmpty(pendingTeamId)
                ? "Pick a team to join next season, or stay. Offers end when the next season starts."
                : "Transfer agreed. You join the marked team when the next season starts.";
            var info = TextNode(note, transferRoot, 22); Height(info.gameObject, 70);
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                string mark = id == pendingTeamId ? "[AGREED] " : "";
                Button(mark + offer.teamName + "\nStrength " + offer.teamStrength.ToString("0")
                       + "   Start " + Mathf.RoundToInt(offer.startChance * 100f) + "%   Wants " + offer.focus,
                       () => onChooseOffer(id), transferRoot, 96f);
            }
            Button("STAY WITH " + currentTeamName, () => onStay(), transferRoot, 72f);
        }
        public void SetSeason(string info, string lastResult, bool seasonComplete, string standings, string history)
        {
            if (seasonText == null) return;
            seasonText.text = info;
            SetLines(resultText, lastResult, 5);
            SetLines(standingsText, standings, 6);
            SetLines(historyText, history, 2);
            playLabel.text = seasonComplete ? "START NEXT SEASON" : "PLAY NEXT MATCH";
        }
        public void SetStatus(string message) { if (status != null) status.text = message; }
        public void ShowError(string message)
        { Clear(); Label("SAVE COULD NOT BE LOADED", 30, 90); status = Label(message + "\nExisting save was not overwritten.", 23, 240); }
    }
}
