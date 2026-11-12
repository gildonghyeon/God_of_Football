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
        RectTransform[] pages = new RectTransform[4];   // 홈 / 경기 / 커리어 / 설정
        Image[] tabFills = new Image[4];
        Text[] statTexts = new Text[5];
        Button[] upgradeButtons = new Button[5];
        Button rateButton;
        RectTransform[] statBars = new RectTransform[5];
        Text trainingText, seasonText, resultText, playLabel, standingsText, historyText, rebirthText, friendlyText, trophyText, leadersText;
        Button friendlyButton;
        Button[] facilityButtons = new Button[FacilitySystem.Count];
        Text moneyText, facilityMoneyText;
        RectTransform transferRoot;
        Image rebirthFill;
        TrainingStageView stage;   // 훈련장 장면
        MatchReplayView matchView; // 경기 영상 화면
        UpgradeEffects upgradeFx;  // 강화 결과 연출
        TrophyCeremony ceremony;   // 시즌 끝 트로피 수여 연출
        Text offlineNumber;        // 오프라인 보상 창의 올라가는 숫자
        float offlineTarget, offlineT;
        const float OfflineCountTime = 1.4f;

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
            ceremony = new TrophyCeremony(font, safeRoot);
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
            string[] tabNames = { "홈", "경기", "커리어", "설정" };
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
            if (ceremony != null) ceremony.Tick(Time.unscaledDeltaTime);
            TickOfflineCount(Time.unscaledDeltaTime);
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
            button.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); action(); });
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
            trainingText = seasonText = resultText = playLabel = standingsText = historyText = rebirthText = friendlyText = trophyText = leadersText = null;
            friendlyButton = null;
            Array.Clear(facilityButtons, 0, facilityButtons.Length);
            moneyText = facilityMoneyText = null;
            transferRoot = null; rebirthFill = null;
            SetChrome(false);
            DestroyPreview();
        }

        // ---------- 화면 ----------
        public void ShowCreation()
        {
            Clear();
            Label("나의 축구 커리어", 33, 64).color = PixelTheme.Accent;
            Label("첫 선수를 만들어 보세요", 22, 44);
            newHair = 0; newSkin = 0; newKit = 0;
            var createPreview = AddPreview(content, 190f);
            ApplyPreview(createPreview);
            Label("선수 이름", 22, 40).color = PixelTheme.TextDim;

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
            var placeholder = TextNode("이름 입력", inputFill, 26); Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16, 6); placeholder.rectTransform.offsetMax = new Vector2(-16, -6);
            placeholder.color = PixelTheme.TextDim; nameInput.placeholder = placeholder;

            Button positionButton = null;
            positionButton = PixelButton("포지션: " + Ko.Pos(position), () => {
                position = (Position)(((int)position + 1) % 3);
                positionButton.GetComponentInChildren<Text>().text = "포지션: " + Ko.Pos(position);
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            Button traitButton = null;
            traitButton = PixelButton("특성: " + Ko.TraitName(trait), () => {
                trait = (Trait)(((int)trait + 1) % 3);
                traitButton.GetComponentInChildren<Text>().text = "특성: " + Ko.TraitName(trait);
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            AddLookButtons(content, createPreview);
            Label("피니셔: 슈팅 / 플레이메이커: 패스\n볼 위너: 수비 (보너스 +" + settings.traitBonus + ")", 22, 76).color = PixelTheme.TextDim;
            PixelButton("선수 만들기", () => onCreate(nameInput.text, position, trait, newHair, newSkin, newKit), content, 88f);
            status = Label("버튼을 눌러 포지션과 특성을 고르세요.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public void ShowPlayer(PlayerData p, string message)
        {
            Clear();
            SetChrome(true);
            status = stripText;

            pages[0] = NewPage("HomePage"); pages[1] = NewPage("MatchPage"); pages[2] = NewPage("CareerPage"); pages[3] = NewPage("SettingsPage");
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
            var infoText = TextNode(Ko.Pos(p.position) + " / " + Ko.TraitName(p.trait), nameRow, 22);
            infoText.alignment = TextAnchor.MiddleRight; infoText.color = PixelTheme.TextDim;
            infoText.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
            // 노력 포인트: 방치하면 쌓이고, 아래 능력치의 +1 버튼으로 씁니다.
            trainingText = Label("", 22, 64);
            var rate = PixelButton("노력 증가량", () => { if (OnBuyEffortRate != null) OnBuyEffortRate(); }, Parent, 56f);
            rateButton = rate; rate.GetComponentInChildren<Text>().fontSize = 20;
            for (int i = 0; i < statTexts.Length; i++)
                statBars[i] = MakeStatRow((AttributeKind)i, out statTexts[i], out upgradeButtons[i]);
            UpdateStats(p);

            // 시설: 돈으로 올리는 영구 효과 (홈에서 바로 올릴 수 있게)
            currentParent = Section(pages[0], "시설", true);
            facilityMoneyText = Label("", 22, 30);
            facilityMoneyText.color = PixelTheme.Accent;
            for (int i = 0; i < facilityButtons.Length; i++)
            {
                int index = i;
                facilityButtons[i] = PixelButton("", () => { if (OnBuyFacility != null) OnBuyFacility(index); }, Parent, 112f);
                facilityButtons[i].GetComponentInChildren<Text>().fontSize = 20;
            }
            Label("환생하면 시설은 초기화됩니다.", 20, 30).color = PixelTheme.TextDim;

            // MATCH: 시즌, 경기, 순위표
            currentParent = Section(pages[1], "시즌", true);
            seasonText = Label("", 22, 64);
            var play = PixelButton("다음 경기", () => onPlayMatch(), Parent, 80f);
            playLabel = play.GetComponentInChildren<Text>();
            resultText = Label("", 22, 130);
            currentParent = Section(pages[1], "친선 경기", true);
            friendlyText = Label("", 22, 64);
            friendlyButton = PixelButton("친선 경기 하기", () => { if (OnPlayFriendly != null) OnPlayFriendly(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[1], "순위표", true);
            standingsText = Label("", 22, 200);
            currentParent = Section(pages[1], "개인 순위", true);
            leadersText = Label("", 22, 60);

            // CAREER: 이적 제안, 이력, 환생
            currentParent = Section(pages[2], "돈", true);
            moneyText = Label("", 22, 64);
            transferRoot = NewRect("TransferRoot", pages[2]);
            ConfigureStack(transferRoot.gameObject, 0, 0, 0, 0, 18);
            currentParent = Section(pages[2], "외형", true);
            Label("머리색, 피부색, 유니폼 색을 바꿉니다.", 20, 30).color = PixelTheme.TextDim;
            PixelButton("외형 바꾸기", () => ShowLookDialog(), Parent, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            currentParent = Section(pages[2], "트로피 룸", true);
            trophyText = Label("", 22, 60);
            currentParent = Section(pages[2], "시즌 기록", true);
            historyText = Label("", 22, 60);
            currentParent = Section(pages[2], "환생", true);
            rebirthText = Label("", 22, 120);
            var rebirth = PixelButton("환생하기", () => onRebirth(), Parent, 72f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            rebirthFill = (Image)rebirth.targetGraphic;

            // 설정: 소리, 저장 안내, 초기화
            currentParent = Section(pages[3], "소리", true);
            Button soundButton = null;
            soundButton = PixelButton(SoundFx.Enabled ? "효과음 켬" : "효과음 끔", () =>
            {
                SoundFx.Enabled = !SoundFx.Enabled;
                soundButton.GetComponentInChildren<Text>().text = SoundFx.Enabled ? "효과음 켬" : "효과음 끔";
                SoundFx.Play(Sfx.Click);
            }, Parent, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            currentParent = Section(pages[3], "저장", true);
            Label("진행 상황은 자동으로 저장됩니다.\n(행동할 때마다, 30초마다, 앱을 닫거나 내릴 때)", 20, 64).color = PixelTheme.TextDim;
            Label("오프라인 보상은 최대 " + trainingSettings.offlineCapHours.ToString("0.#") + "시간까지 인정됩니다.", 20, 30).color = PixelTheme.TextDim;
            currentParent = Section(pages[3], "초기화", true);
            Label("지금 선수와 진행 상황을 지우고 처음부터 시작합니다.", 20, 30).color = PixelTheme.TextDim;
            PixelButton("저장 초기화", () => { if (OnResetSave != null) OnResetSave(); }, Parent, 68f, PixelTheme.DangerFill, PixelTheme.DangerEdge);

            currentParent = null;
            SetPage(0);
            status.text = message;
        }

        public void ShowError(string message, bool canRestore)
        {
            Clear();
            Label("저장을 불러올 수 없습니다", 30, 90).color = PixelTheme.Accent;
            status = Label(message + "\n저장 파일은 바뀌지 않았습니다.", 22, 180);
            PixelButton("다시 시도", () => { if (OnRetry != null) OnRetry(); }, content, 80f);
            if (canRestore)
                PixelButton("백업 복구", () => { if (OnRestoreBackup != null) OnRestoreBackup(); }, content, 80f, PixelTheme.AccentFill, PixelTheme.Accent);
            PixelButton("새 게임 시작", () => { if (OnStartNew != null) OnStartNew(); }, content, 80f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
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
                statTexts[i].text = Ko.Attr(kind) + "  " + Mathf.Floor(value).ToString("0") + (value >= settings.attributeCap ? "  최대" : "");
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
                b.GetComponentInChildren<Text>().text = max ? "최대"
                    : SeasonSystem.Money(training.Cost(kind)) + "  " + ChanceText(training.Chance(kind), training.FailStack(kind) > 0);   // 칸이 좁아 비용과 확률만
                ((Image)b.targetGraphic).color = training.CanAfford(kind) ? PixelTheme.ButtonFill : PixelTheme.QuietFill;
            }
            trainingText.text = "노력 포인트 " + SeasonSystem.Money((long)training.Points)
                + "   +" + training.PointsPerSecond.ToString("0.00") + "/초  (리그 x" + training.DivisionMultiplier.ToString("0.##") + ")"
                + "\n능력치 버튼을 누르면 강화에 도전합니다 (비용 / 성공 확률).";
            if (rateButton != null)
            {
                long rc = training.RateCost;
                rateButton.GetComponentInChildren<Text>().text = "노력 증가량 Lv " + training.RateLevel
                    + (rc < 0 ? "  (최대)" : "   강화 " + SeasonSystem.Money(rc));
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

        // 확률 글자. 천장 보너스가 붙었거나 10% 미만이면 소수 한 자리까지
        public static string ChanceText(float chance, bool precise)
        {
            float pct = chance * 100f;
            return (precise || pct < 10f ? pct.ToString("0.0") : Mathf.RoundToInt(pct).ToString()) + "%";
        }

        // 강화 천장으로 오른 확률(%p). 오르지 않았으면 빈 글자. 아주 작으면 소수 둘째 자리까지
        public static string BonusText(float bonus)
        {
            float pct = bonus * 100f;
            if (pct < 0.005f) return "";
            return " (+" + (pct < 0.1f ? pct.ToString("0.00") : pct.ToString("0.0")) + "%)";
        }

        public void SetTrophies(string text) { if (trophyText != null) SetLines(trophyText, text, 2); }
        public void SetLeaders(string text) { if (leadersText != null) SetLines(leadersText, text, 2); }

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
            playLabel.text = seasonComplete ? "다음 시즌 시작" : "다음 경기";
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

            var body = Section(transferRoot, "이적 제안", true);
            if (summary != null) { var s = TextNode(summary, body, 22); s.horizontalOverflow = HorizontalWrapMode.Wrap; Height(s.gameObject, 64); }
            if (none) return;   // 제안이 없으면 지금 팀에 남습니다
            string note = !decided ? "다음 시즌에 갈 팀을 고르거나 잔류하세요. 선택은 한 번뿐입니다."
                : string.IsNullOrEmpty(pendingTeamId) ? "결정 완료: " + currentTeamName + "에 잔류합니다."
                : "결정 완료: 표시된 팀으로 다음 시즌에 이적합니다.";
            var info = TextNode(note, body, 22); info.color = PixelTheme.TextDim; Height(info.gameObject, 34);
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                bool agreed = id == pendingTeamId;
                var button = PixelButton((agreed ? "[확정] " : "") + offer.teamName + (offer.division > 0 ? "  (" + offer.division + "부)" : "")
                            + "\n전력 " + offer.teamStrength.ToString("0")
                            + "   선발 확률 " + Mathf.RoundToInt(offer.startChance * 100f) + "%   중시 " + Ko.Attr(offer.focus)
                            + (offer.weeklyWage > 0 ? "\n주급 " + SeasonSystem.Money(offer.weeklyWage) : ""),
                            () => onChooseOffer(id), body, offer.weeklyWage > 0 ? 120f : 92f,
                            agreed ? PixelTheme.AccentFill : decided ? PixelTheme.QuietFill : PixelTheme.ButtonFill,
                            agreed ? PixelTheme.Accent : decided ? PixelTheme.QuietEdge : PixelTheme.ButtonEdge);
                if (decided) button.interactable = false;
            }
            var stay = PixelButton((decided && string.IsNullOrEmpty(pendingTeamId) ? "[확정] " : "") + currentTeamName + " 잔류", () => onStay(), body, 68f,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.AccentFill : PixelTheme.QuietFill,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.Accent : PixelTheme.QuietEdge);
            if (decided) stay.interactable = false;
        }

        // 돈, 주급, 동시 훈련 레벨과 다음 레벨 가격 (price < 0이면 최고 레벨)
        // pay: 경기 한 번에 실제로 받는 돈, agentPercent: 에이전트 보너스(%)
        public void SetMoney(long money, int pay, int agentPercent)
        {
            if (moneyText == null) return;
            if (facilityMoneyText != null) facilityMoneyText.text = "보유 돈 " + SeasonSystem.Money(money);
            moneyText.text = "돈 " + SeasonSystem.Money(money) + "\n경기당 " + SeasonSystem.Money(pay) + " (주급 1주치)"
                + (agentPercent > 0 ? "  에이전트 +" + agentPercent + "% 포함" : "");
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

        // 시즌 끝 트로피 수여 연출. 다 보면 done을 부릅니다.
        public void ShowTrophyCeremony(List<TrophyRecord> trophies, string heading, Action done)
        {
            CloseDialog();
            if (ceremony == null) { done?.Invoke(); return; }
            ceremony.Show(trophies, heading, done);
        }

        // 돌아왔을 때의 보상 창: 받은 노력 포인트가 0부터 올라가는 숫자로 보이고, COLLECT로 닫습니다.
        public void ShowOfflineReward(OfflineResult r)
        {
            if (r == null || !r.Granted) return;
            float rate = r.CreditedSeconds > 0f ? r.Points / r.CreditedSeconds : 0f;
            var inner = BuildDialog("다시 오셨군요!", "자리를 비운 시간 " + IdleRewardSystem.FormatDuration(r.AwaySeconds) + "\n그동안 선수는 계속 훈련했습니다.", 70f);
            var caption = TextNode("얻은 노력 포인트", inner, 22); caption.alignment = TextAnchor.MiddleCenter; caption.color = PixelTheme.TextDim; Height(caption.gameObject, 30);
            offlineNumber = TextNode("+0", inner, 60); offlineNumber.alignment = TextAnchor.MiddleCenter; offlineNumber.color = PixelTheme.Accent; Height(offlineNumber.gameObject, 84);
            var glow = offlineNumber.gameObject.AddComponent<Outline>(); glow.effectColor = new Color(.35f, .22f, 0f, 1f); glow.effectDistance = new Vector2(3, -3);
            string detail = "인정 시간 " + IdleRewardSystem.FormatDuration(r.CreditedSeconds)
                + (r.Capped ? "  (최대 " + IdleRewardSystem.FormatDuration(r.CapSeconds) + ")" : "")
                + "   초당 " + rate.ToString("0.00") + "\n보유 " + SeasonSystem.Money((long)r.Total) + " 포인트. 홈에서 강화해 보세요.";
            var info = TextNode(detail, inner, 20); info.alignment = TextAnchor.MiddleCenter; Height(info.gameObject, 64);
            if (r.Capped)
            {
                var cap = TextNode("오프라인 시간이 최대치에 걸렸어요. 더 자주 들러 주세요!", inner, 18); cap.alignment = TextAnchor.MiddleCenter; cap.color = PixelTheme.DangerEdge; Height(cap.gameObject, 28);
            }
            offlineTarget = r.Points; offlineT = 0f;
            PixelButton("받기", () => { SoundFx.Play(Sfx.Collect); offlineNumber = null; CloseDialog(); }, inner, 76f, PixelTheme.AccentFill, PixelTheme.Accent);
        }

        void TickOfflineCount(float dt)
        {
            if (offlineNumber == null) return;
            if (overlay == null) { offlineNumber = null; return; }   // 창이 닫혔으면 그만
            if (offlineT >= OfflineCountTime) return;
            offlineT += dt;
            float k = Mathf.Clamp01(offlineT / OfflineCountTime);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            offlineNumber.text = "+" + SeasonSystem.Money((long)Mathf.Round(offlineTarget * ease));
            float s = k < 1f ? 1f : 1f;
            if (k < 1f) SoundFx.Play(Sfx.Tick, 0.6f);
            else { SoundFx.Play(Sfx.Collect, 0.8f); s = 1.15f; }
            offlineNumber.rectTransform.localScale = new Vector3(s, s, 1f);
        }

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
            hairButton = PixelButton("머리: " + PlayerLook.HairNames[newHair], () => {
                newHair = (newHair + 1) % PlayerLook.HairCount;
                hairButton.GetComponentInChildren<Text>().text = "머리: " + PlayerLook.HairNames[newHair];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            skinButton = PixelButton("피부: " + PlayerLook.SkinNames[newSkin], () => {
                newSkin = (newSkin + 1) % PlayerLook.SkinCount;
                skinButton.GetComponentInChildren<Text>().text = "피부: " + PlayerLook.SkinNames[newSkin];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            kitButton = PixelButton("유니폼: " + PlayerLook.KitNames[newKit], () => {
                newKit = (newKit + 1) % PlayerLook.KitCount;
                kitButton.GetComponentInChildren<Text>().text = "유니폼: " + PlayerLook.KitNames[newKit];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        void ShowLookDialog()
        {
            newHair = lookHair; newSkin = lookSkin; newKit = lookKit;
            var inner = BuildDialog("외형", "머리색, 피부색, 유니폼 색을 고르세요.", 36f);
            var preview = AddPreview(inner, 220f);
            ApplyPreview(preview);
            AddLookButtons(inner, preview);
            PixelButton("적용", () => { CloseDialog(); DestroyPreview(); if (OnApplyLook != null) OnApplyLook(newHair, newSkin, newKit); }, inner, 76f);
            PixelButton("취소", () => { CloseDialog(); DestroyPreview(); }, inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
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
            PixelButton("확인", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 되돌릴 수 없는 선택 앞에서 한 번 더 확인합니다.
        public void ShowConfirm(string title, string body, float bodyHeight, string yesLabel, Action onYes)
        {
            var inner = BuildDialog(title, body, bodyHeight);
            PixelButton(yesLabel, () => { CloseDialog(); if (onYes != null) onYes(); }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("취소", () => CloseDialog(), inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }
    }
}
