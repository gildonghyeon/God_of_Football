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
        public static readonly Color DangerFill = new Color(.45f, .16f, .16f, 1);
        public static readonly Color DangerEdge = new Color(.92f, .45f, .45f, 1);
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
        Action<string, Position, Trait, int, int, int> onCreate;
        Action onSave, onPlayMatch, onStay, onRebirth;
        Action<string> onChooseOffer;
        Action<AttributeKind> onUpgrade;
        public Action<int, int, int> OnApplyLook;   // 외형(머리색, 피부색, 유니폼)을 바꾸면 부르는 동작
        public Action OnBuyEffortRate;              // 노력 포인트 증가량을 올리는 동작
        public Action OnPlayFriendly;               // 친선 경기를 하는 동작
        public Action<int> OnBuyFacility;           // 시설 레벨을 올리는 동작 (FacilityKind 번호)
        public Action OnRetry, OnRestoreBackup, OnStartNew, OnResetSave;   // 저장 복구·초기화 버튼이 부르는 동작
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
        Button[] upgradeButtons = new Button[5];
        Button rateButton;
        RectTransform[] statBars = new RectTransform[5];
        Text trainingText, seasonText, resultText, playLabel, standingsText, historyText, rebirthText, friendlyText;
        Button friendlyButton;
        Button[] facilityButtons = new Button[FacilitySystem.Count];
        Text moneyText;
        RectTransform transferRoot;
        Image rebirthFill;
        TrainingStageView stage;   // 훈련장 장면
        MatchReplayView matchView; // 경기 영상 화면
        UpgradeEffects upgradeFx;  // 강화 결과 연출

        Transform Parent { get { return currentParent != null ? currentParent : content; } }

        public void Initialize(Font customFont, CareerSettings config, TrainingSettings training,
            Action<string, Position, Trait, int, int, int> create, Action save,
            Action<AttributeKind> upgrade, Action playMatch,
            Action<string> chooseOffer, Action stay, Action rebirth)
        {
            font = customFont != null ? customFont : Resources.Load<Font>("Fonts/Galmuri11");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settings = config; trainingSettings = training;
            stage = new TrainingStageView();
            matchView = new MatchReplayView(font);
            onCreate = create; onSave = save;
            onUpgrade = upgrade; onPlayMatch = playMatch;
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
            upgradeFx = new UpgradeEffects(font, safeRoot);
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
            if (matchView != null) matchView.Tick(Time.unscaledDeltaTime);
            if (upgradeFx != null) upgradeFx.Tick(Time.unscaledDeltaTime);
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

        // 테두리 + 안쪽 칸. 안쪽 내용을 넣을 Transform을 돌려줍니다. compact면 여백을 줄입니다.
        Transform Section(Transform parent, string title, bool compact = false)
        {
            var outer = NewRect("Section", parent);
            outer.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            ConfigureStack(outer.gameObject, 4, 4, 4, 4, 0);
            var inner = NewRect("Body", outer);
            inner.gameObject.AddComponent<Image>().color = PixelTheme.Panel;
            if (compact) ConfigureStack(inner.gameObject, 16, 10, 16, 12, 6);
            else ConfigureStack(inner.gameObject, 20, 16, 20, 20, 12);
            if (!string.IsNullOrEmpty(title))
            {
                var t = TextNode(title, inner, compact ? 26 : 30); t.color = PixelTheme.Accent; Height(t.gameObject, compact ? 36 : 44);
            }
            return inner;
        }

        // 능력치·진행도에 쓰는 가로 막대. 채워지는 부분의 RectTransform을 돌려줍니다.
        RectTransform MakeBar(Transform parent, float height, bool flexibleWidth)
        {
            var bar = NewRect("Bar", parent);
            bar.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            var layout = bar.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            if (flexibleWidth) { layout.flexibleWidth = 1; layout.preferredWidth = 100; }
            var back = NewRect("Back", bar);
            Stretch(back); back.offsetMin = new Vector2(2, 2); back.offsetMax = new Vector2(-2, -2);
            back.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            var fill = NewRect("Fill", back);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = PixelTheme.Bar;
            SetFill(fill, 0f);
            return fill;
        }

        // 능력치 한 줄: 왼쪽에 글자, 오른쪽에 막대
        // 능력치 한 줄: 왼쪽에 글자, 가운데 막대, 오른쪽에 노력 포인트로 +1 하는 버튼
        RectTransform MakeStatRow(AttributeKind kind, out Text label, out Button upgrade)
        {
            var row = NewRect("StatRow", Parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Height(row.gameObject, 52);
            label = TextNode("", row, 22);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 190; le.preferredHeight = 30;
            label.rectTransform.pivot = new Vector2(0f, .5f);   // 강화 연출에서 글자가 왼쪽 끝을 기준으로 커지도록
            var bar = MakeBar(row, 20, true);
            upgrade = PixelButton("+1", () => onUpgrade(kind), row, 0f);
            upgrade.GetComponentInChildren<Text>().fontSize = 20;
            var ble = upgrade.gameObject.GetComponent<LayoutElement>() ?? upgrade.gameObject.AddComponent<LayoutElement>();
            ble.preferredWidth = 220; ble.minWidth = 220; ble.preferredHeight = 48; ble.flexibleWidth = 0;
            return bar;
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
            Array.Clear(upgradeButtons, 0, upgradeButtons.Length);
            rateButton = null;
            trainingText = seasonText = resultText = playLabel = standingsText = historyText = rebirthText = friendlyText = null;
            friendlyButton = null;
            Array.Clear(facilityButtons, 0, facilityButtons.Length);
            moneyText = null;
            transferRoot = null; rebirthFill = null;
            SetChrome(false);
            DestroyPreview();
        }

        // ---------- 화면 ----------
        public void ShowCreation()
        {
            Clear();
            Label("MY FOOTBALL CAREER", 33, 64).color = PixelTheme.Accent;
            Label("Create your first player", 22, 44);
            newHair = 0; newSkin = 0; newKit = 0;
            var createPreview = AddPreview(content, 190f);
            ApplyPreview(createPreview);
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
            AddLookButtons(content, createPreview);
            Label("Finisher: Shooting / Playmaker: Passing\nBallWinner: Defending (bonus +" + settings.traitBonus + ")", 22, 76).color = PixelTheme.TextDim;
            PixelButton("CREATE PLAYER", () => onCreate(nameInput.text, position, trait, newHair, newSkin, newKit), content, 88f);
            status = Label("Choose position and trait by tapping each button.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public void ShowPlayer(PlayerData p, string message)
        {
            Clear();
            SetChrome(true);
            status = stripText;

            pages[0] = NewPage("HomePage"); pages[1] = NewPage("MatchPage"); pages[2] = NewPage("CareerPage");
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor;
            stage.SetLook(lookHair, lookSkin, lookKit);               // 내 선수 그림을 외형에 맞게 준비
            if (stage.Available) stage.Create(pages[0]);   // HOME 맨 위에 훈련장 장면

            // HOME: 스크롤 없이 한 화면에 들어오도록 촘촘하게 배치합니다.
            currentParent = Section(pages[0], "", true);
            var nameRow = NewRect("NameRow", Parent);
            var nameLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            nameLayout.childControlWidth = true; nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false; nameLayout.childForceExpandHeight = true;
            Height(nameRow.gameObject, 36);
            var nameText = TextNode(p.playerName, nameRow, 26);
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var infoText = TextNode(p.position + " / " + p.trait, nameRow, 22);
            infoText.alignment = TextAnchor.MiddleRight; infoText.color = PixelTheme.TextDim;
            infoText.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
            // 노력 포인트: 방치하면 쌓이고, 아래 능력치의 +1 버튼으로 씁니다.
            trainingText = Label("", 22, 64);
            var rate = PixelButton("EFFORT RATE", () => { if (OnBuyEffortRate != null) OnBuyEffortRate(); }, Parent, 56f);
            rateButton = rate; rate.GetComponentInChildren<Text>().fontSize = 20;
            for (int i = 0; i < statTexts.Length; i++)
                statBars[i] = MakeStatRow((AttributeKind)i, out statTexts[i], out upgradeButtons[i]);
            UpdateStats(p);

            currentParent = Section(pages[0], "", true);
            var systemRow = NewRect("SystemRow", Parent);
            var systemLayout = systemRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            systemLayout.spacing = 8;
            systemLayout.childControlWidth = true; systemLayout.childControlHeight = true;
            systemLayout.childForceExpandWidth = true; systemLayout.childForceExpandHeight = true;
            Height(systemRow.gameObject, 68);
            PixelButton("SAVE", () => onSave(), systemRow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            PixelButton("LOOK", () => ShowLookDialog(), systemRow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            PixelButton("RESET SAVE", () => { if (OnResetSave != null) OnResetSave(); }, systemRow, 0f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
            Label("Offline reward cap: " + trainingSettings.offlineCapHours.ToString("0.#") + "h", 22, 30).color = PixelTheme.TextDim;

            // MATCH: 시즌, 경기, 순위표
            currentParent = Section(pages[1], "SEASON", true);
            seasonText = Label("", 22, 64);
            var play = PixelButton("PLAY NEXT MATCH", () => onPlayMatch(), Parent, 80f);
            playLabel = play.GetComponentInChildren<Text>();
            resultText = Label("", 22, 130);
            currentParent = Section(pages[1], "FRIENDLY", true);
            friendlyText = Label("", 22, 64);
            friendlyButton = PixelButton("PLAY FRIENDLY", () => { if (OnPlayFriendly != null) OnPlayFriendly(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[1], "STANDINGS", true);
            standingsText = Label("", 22, 200);

            // CAREER: 이적 제안, 이력, 환생
            currentParent = Section(pages[2], "MONEY", true);
            moneyText = Label("", 22, 64);
            transferRoot = NewRect("TransferRoot", pages[2]);
            ConfigureStack(transferRoot.gameObject, 0, 0, 0, 0, 18);
            currentParent = Section(pages[2], "FACILITIES", true);
            Label("Spend money on permanent boosts. Reset on rebirth.", 20, 30).color = PixelTheme.TextDim;
            for (int i = 0; i < facilityButtons.Length; i++)
            {
                int index = i;
                facilityButtons[i] = PixelButton("", () => { if (OnBuyFacility != null) OnBuyFacility(index); }, Parent, 112f);
                facilityButtons[i].GetComponentInChildren<Text>().fontSize = 20;
            }
            currentParent = Section(pages[2], "HISTORY", true);
            historyText = Label("", 22, 60);
            currentParent = Section(pages[2], "REBIRTH", true);
            rebirthText = Label("", 22, 120);
            var rebirth = PixelButton("REBIRTH", () => onRebirth(), Parent, 72f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            rebirthFill = (Image)rebirth.targetGraphic;

            currentParent = null;
            SetPage(0);
            status.text = message;
        }

        public void ShowError(string message, bool canRestore)
        {
            Clear();
            Label("SAVE COULD NOT BE LOADED", 30, 90).color = PixelTheme.Accent;
            status = Label(message + "\nYour save file was not changed.", 22, 180);
            PixelButton("RETRY", () => { if (OnRetry != null) OnRetry(); }, content, 80f);
            if (canRestore)
                PixelButton("RESTORE BACKUP", () => { if (OnRestoreBackup != null) OnRestoreBackup(); }, content, 80f, PixelTheme.AccentFill, PixelTheme.Accent);
            PixelButton("START NEW GAME", () => { if (OnStartNew != null) OnStartNew(); }, content, 80f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
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
                statTexts[i].text = kind + "  " + Mathf.Floor(value).ToString("0") + (value >= settings.attributeCap ? "  MAX" : "");
                SetFill(statBars[i], value / cap);
            }
        }

        // 플레이어 화면이 열려 있을 때만 능력치와 훈련 진행도를 갱신합니다.
        public void RefreshTraining(PlayerData p, TrainingSystem training)
        {
            if (trainingText == null || p == null) return;
            UpdateStats(p);
            // 훈련장: 마지막으로 강화한 능력치의 훈련 장면 (아직 없으면 대기)
            if (stage != null && stage.Available)
                stage.Play(training.LastUpgraded < 0 ? "idle" : TrainingStageView.AnimationFor((AttributeKind)training.LastUpgraded));
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var b = upgradeButtons[i];
                if (b == null) continue;
                var kind = (AttributeKind)i;
                bool max = training.IsMax(kind);
                b.GetComponentInChildren<Text>().text = max ? "MAX"
                    : SeasonSystem.Money(training.Cost(kind)) + "  " + Mathf.RoundToInt(training.Chance(kind) * 100f) + "%";
                ((Image)b.targetGraphic).color = training.CanAfford(kind) ? PixelTheme.ButtonFill : PixelTheme.QuietFill;
            }
            trainingText.text = "Effort points " + SeasonSystem.Money((long)training.Points)
                + "   +" + training.PointsPerSecond.ToString("0.00") + "/s  (league x" + training.DivisionMultiplier.ToString("0.##") + ")"
                + "\nTap an attribute button to try an upgrade (cost / success chance).";
            if (rateButton != null)
            {
                long rc = training.RateCost;
                rateButton.GetComponentInChildren<Text>().text = "EFFORT RATE Lv " + training.RateLevel
                    + (rc < 0 ? "  (MAX)" : "   UPGRADE " + SeasonSystem.Money(rc));
                ((Image)rateButton.targetGraphic).color = rc >= 0 && training.Points >= rc ? PixelTheme.AccentFill : PixelTheme.QuietFill;
            }
        }

        // 친선 경기 안내와 버튼 색 (할 수 있을 때만 강조)
        public void SetFriendly(string info, bool ready)
        {
            if (friendlyText == null) return;
            friendlyText.text = info;
            if (friendlyButton != null) ((Image)friendlyButton.targetGraphic).color = ready ? PixelTheme.AccentFill : PixelTheme.QuietFill;
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
            SetLines(seasonText, info, 2);
            SetLines(resultText, lastResult, 5);
            SetLines(standingsText, standings, 6);
            SetLines(historyText, history, 2);
            playLabel.text = seasonComplete ? "START NEXT SEASON" : "PLAY NEXT MATCH";
        }

        // 시즌이 끝나 이적 제안이 있을 때만 제안 목록을 보여줍니다.
        // summary: 시즌 성적 등급과 제안 수 안내 (null이면 아직 시즌 중)
        // decided: 선택을 확정했으면 true. 이때는 버튼을 누를 수 없고 결과만 보여 줍니다.
        public void SetTransfer(List<TransferOffer> offers, string pendingTeamId, string currentTeamName, string summary, bool decided)
        {
            if (transferRoot == null) return;
            foreach (Transform child in transferRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            bool none = offers == null || offers.Count == 0;
            if (none && summary == null) return;

            var body = Section(transferRoot, "TRANSFER OFFERS", true);
            if (summary != null) { var s = TextNode(summary, body, 22); s.horizontalOverflow = HorizontalWrapMode.Wrap; Height(s.gameObject, 64); }
            if (none) return;   // 제안이 없으면 지금 팀에 남습니다
            string note = !decided ? "Pick a team to join next season, or stay. You decide only once."
                : string.IsNullOrEmpty(pendingTeamId) ? "Decision final: you stay with " + currentTeamName + "."
                : "Decision final: you join the marked team next season.";
            var info = TextNode(note, body, 22); info.color = PixelTheme.TextDim; Height(info.gameObject, 34);
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                bool agreed = id == pendingTeamId;
                var button = PixelButton((agreed ? "[CONFIRMED] " : "") + offer.teamName + (offer.division > 0 ? "  (Div " + offer.division + ")" : "")
                            + "\nStrength " + offer.teamStrength.ToString("0")
                            + "   Start " + Mathf.RoundToInt(offer.startChance * 100f) + "%   Wants " + offer.focus
                            + (offer.weeklyWage > 0 ? "\nWage " + SeasonSystem.Money(offer.weeklyWage) + " per week" : ""),
                            () => onChooseOffer(id), body, offer.weeklyWage > 0 ? 120f : 92f,
                            agreed ? PixelTheme.AccentFill : decided ? PixelTheme.QuietFill : PixelTheme.ButtonFill,
                            agreed ? PixelTheme.Accent : decided ? PixelTheme.QuietEdge : PixelTheme.ButtonEdge);
                if (decided) button.interactable = false;
            }
            var stay = PixelButton((decided && string.IsNullOrEmpty(pendingTeamId) ? "[CONFIRMED] " : "") + "STAY WITH " + currentTeamName, () => onStay(), body, 68f,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.AccentFill : PixelTheme.QuietFill,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.Accent : PixelTheme.QuietEdge);
            if (decided) stay.interactable = false;
        }

        // 돈, 주급, 동시 훈련 레벨과 다음 레벨 가격 (price < 0이면 최고 레벨)
        // pay: 경기 한 번에 실제로 받는 돈, agentPercent: 에이전트 보너스(%)
        public void SetMoney(long money, int pay, int agentPercent)
        {
            if (moneyText == null) return;
            moneyText.text = "Money " + SeasonSystem.Money(money) + "\nWage " + SeasonSystem.Money(pay) + " per match (1 week)"
                + (agentPercent > 0 ? "  incl. agent +" + agentPercent + "%" : "");
        }

        // 시설 버튼: 글자와 살 수 있는지 (살 수 있으면 강조)
        public void SetFacility(int index, string text, bool affordable)
        {
            if (index < 0 || index >= facilityButtons.Length || facilityButtons[index] == null) return;
            facilityButtons[index].GetComponentInChildren<Text>().text = text;
            ((Image)facilityButtons[index].targetGraphic).color = affordable ? PixelTheme.AccentFill : PixelTheme.QuietFill;
        }

        public void SetRebirth(string text, bool available)
        {
            if (rebirthText == null) return;
            SetLines(rebirthText, text, 4);
            rebirthFill.color = available ? PixelTheme.RebirthFill : PixelTheme.QuietFill;
        }

        // 경기 영상이 재생되는 동안에는 true입니다. 이 동안 훈련은 멈춥니다.
        public bool IsMatchPlaying { get { return matchView != null && matchView.Active; } }

        // 경기 영상을 보여 주고, 끝나면(또는 건너뛰면) onFinished를 부릅니다. 그림이 없으면 바로 onFinished를 부릅니다.
        public void ShowMatchReplay(MatchRecord record, PlayerData player, string ownTeamName, Action onFinished)
        {
            CloseDialog();
            if (matchView == null || !matchView.Show(safeRoot, record, player, ownTeamName, settings.attributeCap, onFinished))
            {
                if (onFinished != null) onFinished();
            }
        }

        public void SetStatus(string message) { if (status != null) status.text = message; }

        // 강화 결과 연출 (결과 창 대신). 능력치 글자가 새 값으로 바뀐 뒤에 부릅니다.
        public void ShowUpgradeResult(UpgradeResult r)
        {
            if (upgradeFx == null || r == null) return;
            int i = (int)r.Attribute;
            upgradeFx.Play(r, i >= 0 && i < statTexts.Length ? statTexts[i] : null, trainingSettings != null ? trainingSettings.doubleShare : 0.5f);
        }

        // ---------- 팝업 ----------
        // ---------- 외형(머리색, 피부색, 유니폼) ----------
        int lookHair, lookSkin, lookKit;   // 지금 선수의 외형
        int newHair, newSkin, newKit;      // 선수 생성 화면과 변경 창에서 고르는 외형
        Texture2D previewTexture;
        Sprite previewSprite;

        void DestroyPreview()
        {
            if (previewSprite != null) Destroy(previewSprite);
            if (previewTexture != null) Destroy(previewTexture);
            previewSprite = null; previewTexture = null;
        }

        Image AddPreview(Transform parent, float height)
        {
            var rect = NewRect("LookPreview", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false; image.enabled = false;
            Height(rect.gameObject, height);
            return image;
        }

        // 정면 대기 그림에 고르는 중인 머리색·피부색·유니폼을 적용해 미리보기로 보여 줍니다.
        void ApplyPreview(Image target)
        {
            var asset = Resources.Load<TextAsset>("Art/Chars/blue_idle_front_0");
            if (asset == null || target == null) return;
            DestroyPreview();
            previewTexture = PlayerLook.LoadRecolored(asset.bytes, newHair, newSkin, newKit);
            if (previewTexture == null) return;
            previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height), new Vector2(.5f, .5f), 100f);
            target.sprite = previewSprite; target.enabled = true;
        }

        // 머리색, 피부색, 유니폼을 순서대로 바꾸는 버튼 세 개 (newHair, newSkin, newKit 값을 바꾸고 미리보기를 갱신)
        void AddLookButtons(Transform parent, Image preview)
        {
            Button hairButton = null, skinButton = null, kitButton = null;
            hairButton = PixelButton("Hair: " + PlayerLook.HairNames[newHair], () => {
                newHair = (newHair + 1) % PlayerLook.HairCount;
                hairButton.GetComponentInChildren<Text>().text = "Hair: " + PlayerLook.HairNames[newHair];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            skinButton = PixelButton("Skin: " + PlayerLook.SkinNames[newSkin], () => {
                newSkin = (newSkin + 1) % PlayerLook.SkinCount;
                skinButton.GetComponentInChildren<Text>().text = "Skin: " + PlayerLook.SkinNames[newSkin];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            kitButton = PixelButton("Kit: " + PlayerLook.KitNames[newKit], () => {
                newKit = (newKit + 1) % PlayerLook.KitCount;
                kitButton.GetComponentInChildren<Text>().text = "Kit: " + PlayerLook.KitNames[newKit];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        void ShowLookDialog()
        {
            newHair = lookHair; newSkin = lookSkin; newKit = lookKit;
            var inner = BuildDialog("LOOK", "Choose hair, skin and kit color.", 36f);
            var preview = AddPreview(inner, 220f);
            ApplyPreview(preview);
            AddLookButtons(inner, preview);
            PixelButton("APPLY", () => { CloseDialog(); DestroyPreview(); if (OnApplyLook != null) OnApplyLook(newHair, newSkin, newKit); }, inner, 76f);
            PixelButton("CANCEL", () => { CloseDialog(); DestroyPreview(); }, inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 선수의 외형이 바뀌었을 때 훈련장 그림을 갱신합니다.
        public void ApplyLook(PlayerData p)
        {
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor;
            if (stage != null) stage.SetLook(lookHair, lookSkin, lookKit);
        }

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
