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
    // 픽셀 아트 느낌의 색 모음. 나중에 이미지로 바꿀 때도 여기부터 손보면 됩니다.
    public static class PixelTheme
    {
        public static readonly Color Background = new Color(.05f, .07f, .12f, 1);
        public static readonly Color Panel = new Color(.10f, .14f, .22f, 1);
        public static readonly Color PanelEdge = new Color(.30f, .40f, .60f, 1);
        public static readonly Color ButtonFill = new Color(.16f, .40f, .32f, 1);
        public static readonly Color ButtonEdge = new Color(.45f, .85f, .62f, 1);
        public static readonly Color QuietFill = new Color(.16f, .21f, .31f, 1);
        public static readonly Color QuietEdge = new Color(.33f, .40f, .55f, 1);
        public static readonly Color AccentFill = new Color(.48f, .38f, .10f, 1);
        public static readonly Color Accent = new Color(.98f, .80f, .28f, 1);
        public static readonly Color RebirthFill = new Color(.42f, .24f, .52f, 1);
        public static readonly Color RebirthEdge = new Color(.78f, .55f, .90f, 1);
        public static readonly Color TabActive = new Color(.20f, .45f, .75f, 1);
        public static readonly Color Text = new Color(.93f, .95f, .98f, 1);
        public static readonly Color TextDim = new Color(.62f, .68f, .80f, 1);
        public static readonly Color Bar = new Color(.30f, .80f, .45f, 1);
        public static readonly Color BarBack = new Color(.04f, .06f, .10f, 1);
    }

    // 임시 화면입니다. 게임 규칙과 저장 규칙은 이 클래스 밖에 있습니다.
    // 화면은 HOME / MATCH / CAREER 세 탭으로 나뉩니다.
    public class CareerPrototypeUI : MonoBehaviour
    {
        const float TabHeight = 100f, StatusHeight = 64f;

        Font font;
        CareerSettings settings;
        TrainingSettings trainingSettings;
        Action<string, Position, Trait> onCreate;
        Action onSave, onStopTraining, onPlayMatch, onStay, onRebirth;
        Action<string> onSelectTraining, onChooseOffer;
        public float TrainingMultiplier = 1f;   // 환생 보너스가 반영된 훈련 속도 배율

        RectTransform safeRoot, viewport, content, tabBar, statusStrip;
        Transform currentParent;
        GameObject overlay;
        Text status, stripText;
        Rect lastSafe;
        int lastWidth, lastHeight;

        // 생성 화면
        InputField nameInput;
        Position position;
        Trait trait;

        // 플레이어 화면
        RectTransform[] pages = new RectTransform[3];
        Image[] tabFills = new Image[3];
        Text[] statTexts = new Text[5];
        RectTransform[] statBars = new RectTransform[5];
        Text trainingText, seasonText, resultText, playLabel, standingsText, historyText, rebirthText;
        RectTransform barFill, transferRoot;
        Image rebirthFill;
        TrainingStageView stage;   // 훈련장 장면

        Transform Parent { get { return currentParent != null ? currentParent : content; } }

        public void Initialize(Font customFont, CareerSettings config, TrainingSettings training,
            Action<string, Position, Trait> create, Action save,
            Action<string> selectTraining, Action stopTraining, Action playMatch,
            Action<string> chooseOffer, Action stay, Action rebirth)
        {
            font = customFont != null ? customFont : Resources.Load<Font>("Fonts/Galmuri11");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settings = config; trainingSettings = training;
            stage = new TrainingStageView();
            onCreate = create; onSave = save;
            onSelectTraining = selectTraining; onStopTraining = stopTraining; onPlayMatch = playMatch;
            onChooseOffer = chooseOffer; onStay = stay; onRebirth = rebirth;

            var canvasGO = new GameObject("CareerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0;

            var bg = NewRect("Background", canvasGO.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = PixelTheme.Background;

            safeRoot = NewRect("SafeArea", canvasGO.transform);
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

            viewport = NewRect("ScrollView", safeRoot);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24); layout.spacing = 18;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            // 항상 보이는 상태 줄과 아래쪽 탭 막대
            statusStrip = NewRect("StatusStrip", safeRoot);
            statusStrip.anchorMin = new Vector2(0, 0); statusStrip.anchorMax = new Vector2(1, 0);
            statusStrip.pivot = new Vector2(.5f, 0);
            statusStrip.offsetMin = new Vector2(0, TabHeight); statusStrip.offsetMax = new Vector2(0, TabHeight + StatusHeight);
            statusStrip.gameObject.AddComponent<Image>().color = PixelTheme.Panel;
            stripText = TextNode("", statusStrip, 22);
            Stretch(stripText.rectTransform);
            stripText.rectTransform.offsetMin = new Vector2(20, 4); stripText.rectTransform.offsetMax = new Vector2(-20, -4);
            stripText.color = PixelTheme.TextDim;
            statusStrip.gameObject.SetActive(false);

            tabBar = NewRect("TabBar", safeRoot);
            tabBar.anchorMin = new Vector2(0, 0); tabBar.anchorMax = new Vector2(1, 0);
            tabBar.pivot = new Vector2(.5f, 0);
            tabBar.offsetMin = Vector2.zero; tabBar.offsetMax = new Vector2(0, TabHeight);
            tabBar.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.padding = new RectOffset(10, 10, 12, 10); tabLayout.spacing = 10;
            tabLayout.childControlWidth = true; tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true; tabLayout.childForceExpandHeight = true;
            string[] tabNames = { "HOME", "MATCH", "CAREER" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var tab = PixelButton(tabNames[i], () => SetPage(index), tabBar, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                tabFills[i] = (Image)tab.targetGraphic;
            }
            tabBar.gameObject.SetActive(false);
        }

        void Update()
        {
            if (stage != null) stage.Tick(Time.unscaledDeltaTime);
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

        // ---------- 만들기 도우미 ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Height(GameObject go, float height) { go.AddComponent<LayoutElement>().preferredHeight = height; }

        static void ConfigureStack(GameObject go, int left, int top, int right, int bottom, float spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(left, right, top, bottom); v.spacing = spacing;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        }

        Text TextNode(string value, Transform parent, int size)
        {
            var r = NewRect("Text", parent); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = PixelTheme.Text;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            return t;
        }

        Text Label(string value, int size = 22, float height = 40)
        { var t = TextNode(value, Parent, size); Height(t.gameObject, height); return t; }

        // 두꺼운 테두리가 있는 픽셀 느낌 버튼
        Button PixelButton(string label, Action action, Transform parent, float height, Color fill, Color edge)
        {
            var outer = NewRect("Button", parent);
            outer.gameObject.AddComponent<Image>().color = edge;
            if (height > 0f) { var le = outer.gameObject.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height; }
            var inner = NewRect("Fill", outer);
            Stretch(inner); inner.offsetMin = new Vector2(4, 4); inner.offsetMax = new Vector2(-4, -4);
            var fillImage = inner.gameObject.AddComponent<Image>(); fillImage.color = fill;
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImage;
            var colors = button.colors;
            colors.highlightedColor = Color.white; colors.pressedColor = new Color(.7f, .7f, .7f, 1);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            var text = TextNode(label, inner, 26); Stretch(text.rectTransform); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        Button PixelButton(string label, Action action, Transform parent, float height = 76f)
        { return PixelButton(label, action, parent, height, PixelTheme.ButtonFill, PixelTheme.ButtonEdge); }

        // 테두리 + 안쪽 칸. 안쪽 내용을 넣을 Transform을 돌려줍니다.
        Transform Section(Transform parent, string title)
        {
            var outer = NewRect("Section", parent);
            outer.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            ConfigureStack(outer.gameObject, 4, 4, 4, 4, 0);
            var inner = NewRect("Body", outer);
            inner.gameObject.AddComponent<Image>().color = PixelTheme.Panel;
            ConfigureStack(inner.gameObject, 20, 16, 20, 20, 12);
            if (!string.IsNullOrEmpty(title))
            {
                var t = TextNode(title, inner, 30); t.color = PixelTheme.Accent; Height(t.gameObject, 44);
            }
            return inner;
        }

        // 능력치·진행도에 쓰는 가로 막대. 채워지는 부분의 RectTransform을 돌려줍니다.
        RectTransform MakeBar()
        {
            var bar = NewRect("Bar", Parent);
            bar.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            Height(bar.gameObject, 18);
            var back = NewRect("Back", bar);
            Stretch(back); back.offsetMin = new Vector2(2, 2); back.offsetMax = new Vector2(-2, -2);
            back.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            var fill = NewRect("Fill", back);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = PixelTheme.Bar;
            SetFill(fill, 0f);
            return fill;
        }

        static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        RectTransform NewPage(string name)
        {
            var page = NewRect(name, content);
            ConfigureStack(page.gameObject, 0, 0, 0, 0, 18);
            return page;
        }

        void SetChrome(bool visible)
        {
            tabBar.gameObject.SetActive(visible);
            statusStrip.gameObject.SetActive(visible);
            viewport.offsetMin = new Vector2(0, visible ? TabHeight + StatusHeight : 0);
            viewport.offsetMax = Vector2.zero;
        }

        void SetPage(int index)
        {
            for (int k = 0; k < pages.Length; k++)
            {
                if (pages[k] != null) pages[k].gameObject.SetActive(k == index);
                if (tabFills[k] != null) tabFills[k].color = k == index ? PixelTheme.TabActive : PixelTheme.QuietFill;
            }
            content.anchoredPosition = Vector2.zero;
        }

        void Clear()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            content.anchoredPosition = Vector2.zero;
            status = null; currentParent = null;
            Array.Clear(pages, 0, pages.Length);
            Array.Clear(statTexts, 0, statTexts.Length);
            Array.Clear(statBars, 0, statBars.Length);
            trainingText = seasonText = resultText = playLabel = standingsText = historyText = rebirthText = null;
            barFill = null; transferRoot = null; rebirthFill = null;
            SetChrome(false);
        }

        // ---------- 화면 ----------
        public void ShowCreation()
        {
            Clear();
            Label("MY FOOTBALL CAREER", 33, 64).color = PixelTheme.Accent;
            Label("Create your first player", 22, 44);
            Label("Player name", 22, 40).color = PixelTheme.TextDim;

            var inputOuter = NewRect("NameInput", content);
            inputOuter.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            Height(inputOuter.gameObject, 84);
            var inputFill = NewRect("Fill", inputOuter);
            Stretch(inputFill); inputFill.offsetMin = new Vector2(4, 4); inputFill.offsetMax = new Vector2(-4, -4);
            var inputImage = inputFill.gameObject.AddComponent<Image>(); inputImage.color = PixelTheme.BarBack;
            nameInput = inputOuter.gameObject.AddComponent<InputField>();
            nameInput.targetGraphic = inputImage;
            var text = TextNode("", inputFill, 28); Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 6); text.rectTransform.offsetMax = new Vector2(-16, -6);
            nameInput.textComponent = text; nameInput.characterLimit = settings.maxNameLength;
            nameInput.lineType = InputField.LineType.SingleLine;
            var placeholder = TextNode("Enter a name", inputFill, 26); Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16, 6); placeholder.rectTransform.offsetMax = new Vector2(-16, -6);
            placeholder.color = PixelTheme.TextDim; nameInput.placeholder = placeholder;

            Button positionButton = null;
            positionButton = PixelButton("Position: " + position, () => {
                position = (Position)(((int)position + 1) % 3);
                positionButton.GetComponentInChildren<Text>().text = "Position: " + position;
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            Button traitButton = null;
            traitButton = PixelButton("Trait: " + trait, () => {
                trait = (Trait)(((int)trait + 1) % 3);
                traitButton.GetComponentInChildren<Text>().text = "Trait: " + trait;
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            Label("Finisher: Shooting / Playmaker: Passing\nBallWinner: Defending (bonus +" + settings.traitBonus + ")", 22, 76).color = PixelTheme.TextDim;
            PixelButton("CREATE PLAYER", () => onCreate(nameInput.text, position, trait), content, 88f);
            status = Label("Choose position and trait by tapping each button.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public void ShowPlayer(PlayerData p, string message)
        {
            Clear();
            SetChrome(true);
            status = stripText;

            pages[0] = NewPage("HomePage"); pages[1] = NewPage("MatchPage"); pages[2] = NewPage("CareerPage");
            if (stage.Available) stage.Create(pages[0]);   // HOME 맨 위에 훈련장 장면

            // HOME: 선수 정보, 훈련, 저장
            currentParent = Section(pages[0], "PLAYER");
            Label(p.playerName, 33, 48);
            Label("Position: " + p.position + "   Trait: " + p.trait, 22, 34).color = PixelTheme.TextDim;
            for (int i = 0; i < statTexts.Length; i++) { statTexts[i] = Label("", 22, 32); statBars[i] = MakeBar(); }
            UpdateStats(p);

            currentParent = Section(pages[0], "TRAINING");
            trainingText = Label("", 22, 100);
            barFill = MakeBar();
            foreach (var def in trainingSettings.trainings)
            {
                if (def == null) continue;
                string id = def.id;
                PixelButton(def.displayName + "   +" + (def.growthPerHour * TrainingMultiplier).ToString("0.#") + "/h",
                            () => onSelectTraining(id), Parent, 76f);
            }
            PixelButton("STOP TRAINING", () => onStopTraining(), Parent, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);

            currentParent = Section(pages[0], "SYSTEM");
            PixelButton("SAVE", () => onSave(), Parent, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            Label("Offline reward cap: " + trainingSettings.offlineCapHours.ToString("0.#") + "h", 22, 34).color = PixelTheme.TextDim;

            // MATCH: 시즌, 경기, 순위표
            currentParent = Section(pages[1], "SEASON");
            seasonText = Label("", 22, 70);
            var play = PixelButton("PLAY NEXT MATCH", () => onPlayMatch(), Parent, 88f);
            playLabel = play.GetComponentInChildren<Text>();
            resultText = Label("", 22, 130);
            currentParent = Section(pages[1], "STANDINGS");
            standingsText = Label("", 22, 200);

            // CAREER: 이적 제안, 이력, 환생
            transferRoot = NewRect("TransferRoot", pages[2]);
            ConfigureStack(transferRoot.gameObject, 0, 0, 0, 0, 18);
            currentParent = Section(pages[2], "HISTORY");
            historyText = Label("", 22, 60);
            currentParent = Section(pages[2], "REBIRTH");
            rebirthText = Label("", 22, 120);
            var rebirth = PixelButton("REBIRTH", () => onRebirth(), Parent, 80f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            rebirthFill = (Image)rebirth.targetGraphic;

            currentParent = null;
            SetPage(0);
            status.text = message;
        }

        public void ShowError(string message)
        {
            Clear();
            Label("SAVE COULD NOT BE LOADED", 30, 90).color = PixelTheme.Accent;
            status = Label(message + "\nExisting save was not overwritten.", 22, 240);
        }

        // ---------- 갱신 ----------
        void UpdateStats(PlayerData p)
        {
            float cap = Mathf.Max(1f, settings.attributeCap);
            for (int i = 0; i < statTexts.Length; i++)
            {
                if (statTexts[i] == null) continue;
                var kind = (AttributeKind)i;
                float value = p.Get(kind);
                statTexts[i].text = kind + "   " + value.ToString("0.00") + " / " + settings.attributeCap.ToString("0.#")
                    + (value >= settings.attributeCap ? "  MAX" : "");
                SetFill(statBars[i], value / cap);
            }
        }

        // 플레이어 화면이 열려 있을 때만 능력치와 훈련 진행도를 갱신합니다.
        public void RefreshTraining(PlayerData p, TrainingSystem training)
        {
            if (trainingText == null || p == null) return;
            UpdateStats(p);
            var def = training.Current;
            if (stage != null && stage.Available)
                stage.Play(def == null ? "idle" : TrainingStageView.AnimationFor(def.targetAttribute));
            if (def == null)
            {
                trainingText.text = "Now: none\nPick a training below.";
                SetFill(barFill, 0f);
                return;
            }
            float remain = Mathf.Max(0f, training.Interval - training.CarrySeconds);
            trainingText.text = "Now: " + def.displayName + "\n+" + (def.growthPerHour * TrainingMultiplier).ToString("0.#") + "/h  ->  "
                + def.targetAttribute + "\nNext reward in " + remain.ToString("0.0") + "s";
            SetFill(barFill, training.CarrySeconds / training.Interval);
        }

        static void SetLines(Text t, string text, int minLines)
        {
            t.text = text;
            int lines = Mathf.Max(minLines, text.Split('\n').Length);
            t.GetComponent<LayoutElement>().preferredHeight = lines * 30f + 8f;
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

        // 시즌이 끝나 이적 제안이 있을 때만 제안 목록을 보여줍니다.
        public void SetTransfer(List<TransferOffer> offers, string pendingTeamId, string currentTeamName)
        {
            if (transferRoot == null) return;
            foreach (Transform child in transferRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (offers == null || offers.Count == 0) return;

            var body = Section(transferRoot, "TRANSFER OFFERS");
            string note = string.IsNullOrEmpty(pendingTeamId)
                ? "Pick a team to join next season, or stay. Offers end when the next season starts."
                : "Transfer agreed. You join the marked team when the next season starts.";
            var info = TextNode(note, body, 22); info.color = PixelTheme.TextDim; Height(info.gameObject, 76);
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                bool agreed = id == pendingTeamId;
                PixelButton((agreed ? "[AGREED] " : "") + offer.teamName + "\nStrength " + offer.teamStrength.ToString("0")
                            + "   Start " + Mathf.RoundToInt(offer.startChance * 100f) + "%   Wants " + offer.focus,
                            () => onChooseOffer(id), body, 104f,
                            agreed ? PixelTheme.AccentFill : PixelTheme.ButtonFill, agreed ? PixelTheme.Accent : PixelTheme.ButtonEdge);
            }
            PixelButton("STAY WITH " + currentTeamName, () => onStay(), body, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        public void SetRebirth(string text, bool available)
        {
            if (rebirthText == null) return;
            SetLines(rebirthText, text, 4);
            rebirthFill.color = available ? PixelTheme.RebirthFill : PixelTheme.QuietFill;
        }

        public void SetStatus(string message) { if (status != null) status.text = message; }

        // ---------- 팝업 ----------
        Transform BuildDialog(string title, string body, float bodyHeight)
        {
            if (overlay != null) Destroy(overlay);
            var root = NewRect("Dialog", safeRoot); Stretch(root); overlay = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            var holder = NewRect("Holder", root);
            holder.anchorMin = new Vector2(.06f, .5f); holder.anchorMax = new Vector2(.94f, .5f);
            holder.pivot = new Vector2(.5f, .5f); holder.offsetMin = holder.offsetMax = Vector2.zero;
            ConfigureStack(holder.gameObject, 0, 0, 0, 0, 0);
            holder.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var inner = Section(holder, title);
            var b = TextNode(body, inner, 24); Height(b.gameObject, bodyHeight);
            return inner;
        }

        void CloseDialog() { if (overlay != null) Destroy(overlay); overlay = null; }

        public void ShowPopup(string title, string body, float bodyHeight = 190f, Action onClose = null)
        {
            var inner = BuildDialog(title, body, bodyHeight);
            PixelButton("OK", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 되돌릴 수 없는 선택 앞에서 한 번 더 확인합니다.
        public void ShowConfirm(string title, string body, float bodyHeight, string yesLabel, Action onYes)
        {
            var inner = BuildDialog(title, body, bodyHeight);
            PixelButton(yesLabel, () => { CloseDialog(); if (onYes != null) onYes(); }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("CANCEL", () => CloseDialog(), inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }
    }
}
