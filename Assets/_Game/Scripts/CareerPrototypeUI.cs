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
        Action<string, Position, Trait, int, int, int, int> onCreate;   // 이름, 포지션, 특성, 머리색, 피부색, 유니폼, 축구화
        Action onSave, onPlayMatch, onStay, onRebirth;
        Action<string> onChooseOffer;
        Action<AttributeKind> onUpgrade;
        public Action<int, int, int, int> OnApplyLook;   // 외형(머리색, 피부색, 유니폼, 축구화)을 바꾸면 부르는 동작
        public Action OnBuyEffortRate;              // 노력 포인트 증가량을 올리는 동작
        public Action OnPlayFriendly;               // 친선 경기를 하는 동작
        public Action<int> OnBuyFacility;           // 시설 레벨을 올리는 동작 (FacilityKind 번호)
        public Action OnClaimAttendance, OnClaimBonus;   // 출석 보상, 일일 미션 보너스 받기
        public Action<int> OnClaimMission;          // 일일 미션 보상 받기 (0~2)
        public Action OnOpenSquads;                 // 순위표의 '팀 선수단 보기'
        public Action<string> OnShowSquad;          // 이 팀의 선수단 보기 (팀 ID)
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
        RectTransform[] pages = new RectTransform[5];   // 홈 / 경기 / 미션 / 커리어 / 설정
        Image[] tabFills = new Image[5];
        Text[] tabLabels = new Text[5];
        Text attendText, attendDetail, attendNote, resetText, bonusText;
        AttendanceCards attendCards;   // 출석 7일 카드
        Button attendButton, bonusButton;
        readonly Text[] missionTexts = new Text[3];
        readonly Button[] missionButtons = new Button[3];
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
        MatchPageView matchPage;   // 경기 탭의 카드와 표
        CareerPageView careerPage; // 커리어 탭의 카드와 표
        TrophyCeremony ceremony;   // 시즌 끝 트로피 수여 연출
        Text offlineNumber;        // 오프라인 보상 창의 올라가는 숫자
        float offlineTarget, offlineT;
        const float OfflineCountTime = 1.4f;

        Transform Parent { get { return currentParent != null ? currentParent : content; } }

        public void Initialize(Font customFont, CareerSettings config, TrainingSettings training,
            Action<string, Position, Trait, int, int, int, int> create, Action save,
            Action<AttributeKind> upgrade, Action playMatch,
            Action<string> chooseOffer, Action stay, Action rebirth)
        {
            font = customFont != null ? customFont : Resources.Load<Font>("Fonts/Galmuri11");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settings = config; trainingSettings = training;
            stage = new TrainingStageView();
            matchView = new MatchReplayView(font);
            matchView.SceneArt = stage;   // 내 골 장면은 훈련장 그림(내 외형 포함)을 같이 씁니다.
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
            string[] tabNames = { "홈", "경기", "미션", "커리어", "설정" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var tab = PixelButton(tabNames[i], () => SetPage(index), tabBar, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                tabFills[i] = (Image)tab.targetGraphic;
                tabLabels[i] = tab.GetComponentInChildren<Text>();
            }
            tabBar.gameObject.SetActive(false);
        }

        void Update()
        {
            if (stage != null) stage.Tick(Time.unscaledDeltaTime);
            if (matchView != null) matchView.Tick(Time.unscaledDeltaTime);
            TickGoalPreview(Time.unscaledDeltaTime);
            if (upgradeFx != null) upgradeFx.Tick(Time.unscaledDeltaTime);
            if (ceremony != null) ceremony.Tick(Time.unscaledDeltaTime);
            if (attendCards != null) attendCards.Tick(Time.unscaledDeltaTime);
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
            rateButton = null; matchPage = null; careerPage = null;
            trainingText = seasonText = resultText = playLabel = standingsText = historyText = rebirthText = friendlyText = trophyText = leadersText = null;
            friendlyButton = null;
            Array.Clear(facilityButtons, 0, facilityButtons.Length);
            Array.Clear(missionTexts, 0, missionTexts.Length); Array.Clear(missionButtons, 0, missionButtons.Length);
            attendText = attendDetail = attendNote = resetText = bonusText = null; attendButton = bonusButton = null; attendCards = null;
            moneyText = facilityMoneyText = null;
            transferRoot = null; rebirthFill = null; tCurrentKey = null;
            SetChrome(false);
            DestroyPreview();
        }

        // ---------- 화면 ----------
        public void ShowCreation()
        {
            Clear();
            Label("나의 축구 커리어", 33, 64).color = PixelTheme.Accent;
            Label("첫 선수를 만들어 보세요", 22, 44);
            newHair = 0; newSkin = 0; newKit = CreationKit; newBoots = 0;
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
            PixelButton("선수 만들기", () => onCreate(nameInput.text, position, trait, newHair, newSkin, newKit, newBoots), content, 88f);
            status = Label("버튼을 눌러 포지션과 특성을 고르세요.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public void ShowPlayer(PlayerData p, string message)
        {
            Clear();
            SetChrome(true);
            status = stripText;

            pages[0] = NewPage("HomePage"); pages[1] = NewPage("MatchPage"); pages[2] = NewPage("MissionPage"); pages[3] = NewPage("CareerPage"); pages[4] = NewPage("SettingsPage");
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor; lookBoots = p.bootColor;
            stage.SetLook(lookHair, lookSkin, lookKit, lookBoots);               // 내 선수 그림을 외형에 맞게 준비
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
            matchPage = new MatchPageView(font);
            currentParent = Section(pages[1], "시즌", true);
            matchPage.BuildNext(Parent);
            var play = PixelButton("다음 경기", () => onPlayMatch(), Parent, 80f);
            playLabel = play.GetComponentInChildren<Text>();
            matchPage.BuildResult(Parent);
            currentParent = Section(pages[1], "친선 경기", true);
            friendlyText = Label("", 22, 64);
            friendlyButton = PixelButton("친선 경기 하기", () => { if (OnPlayFriendly != null) OnPlayFriendly(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[1], "순위표", true);
            matchPage.BuildTable(Parent);
            PixelButton("팀 선수단 보기", () => { if (OnOpenSquads != null) OnOpenSquads(); }, Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                .GetComponentInChildren<Text>().fontSize = 22;
            currentParent = Section(pages[1], "개인 순위", true);
            matchPage.BuildLeaders(Parent);

            // CAREER: 선수 프로필, 이적 제안, 트로피 룸, 시즌 기록, 환생 (카드와 표로 보여 줌)
            careerPage = new CareerPageView(font);
            currentParent = Section(pages[3], "", true);
            careerPage.BuildProfile(Parent);
            PixelButton("외형 바꾸기", () => ShowLookDialog(), Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                .GetComponentInChildren<Text>().fontSize = 22;
            transferRoot = NewRect("TransferRoot", pages[3]);
            ConfigureStack(transferRoot.gameObject, 0, 0, 0, 0, 18);
            currentParent = Section(pages[3], "트로피 룸", true);
            careerPage.BuildTrophies(Parent);
            currentParent = Section(pages[3], "시즌 기록", true);
            careerPage.BuildHistory(Parent);
            currentParent = Section(pages[3], "환생", true);
            careerPage.BuildRebirth(Parent);
            var rebirth = PixelButton("환생하기", () => onRebirth(), Parent, 72f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            rebirthFill = (Image)rebirth.targetGraphic;

            // 미션: 출석 보상, 일일 미션
            currentParent = Section(pages[2], "출석 보상", true);
            attendText = Label("", 22, 34); attendText.alignment = TextAnchor.MiddleCenter;
            attendCards = new AttendanceCards(font);
            attendCards.Build(Parent);
            attendNote = Label("", 18, 76); attendNote.alignment = TextAnchor.MiddleCenter; attendNote.color = PixelTheme.TextDim;   // 카드의 '강화 n회 / 수당 n주' 뜻
            attendDetail = Label("", 20, 30); attendDetail.alignment = TextAnchor.MiddleCenter; attendDetail.color = PixelTheme.Text;
            attendButton = PixelButton("출석 보상 받기", () => { if (OnClaimAttendance != null) OnClaimAttendance(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[2], "일일 미션", true);
            resetText = Label("", 20, 30); resetText.color = PixelTheme.TextDim;
            for (int i = 0; i < missionTexts.Length; i++)
            {
                int index = i;
                missionTexts[i] = Label("", 22, 60);
                missionButtons[i] = PixelButton("받기", () => { if (OnClaimMission != null) OnClaimMission(index); }, Parent, 56f);
                missionButtons[i].GetComponentInChildren<Text>().fontSize = 20;
            }
            bonusText = Label("", 22, 34);
            bonusButton = PixelButton("보너스 받기", () => { if (OnClaimBonus != null) OnClaimBonus(); }, Parent, 64f, PixelTheme.AccentFill, PixelTheme.Accent);

            // 설정: 소리, 저장 안내, 초기화
            currentParent = Section(pages[4], "소리", true);
            Button soundButton = null;
            soundButton = PixelButton(SoundFx.Enabled ? "효과음 켬" : "효과음 끔", () =>
            {
                SoundFx.Enabled = !SoundFx.Enabled;
                soundButton.GetComponentInChildren<Text>().text = SoundFx.Enabled ? "효과음 켬" : "효과음 끔";
                SoundFx.Play(Sfx.Click);
            }, Parent, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            currentParent = Section(pages[4], "저장", true);
            Label("진행 상황은 자동으로 저장됩니다.\n(행동할 때마다, 30초마다, 앱을 닫거나 내릴 때)", 20, 64).color = PixelTheme.TextDim;
            Label("오프라인 보상은 최대 " + trainingSettings.offlineCapHours.ToString("0.#") + "시간까지 인정됩니다.", 20, 30).color = PixelTheme.TextDim;
            currentParent = Section(pages[4], "초기화", true);
            Label("지금 선수와 진행 상황을 지우고 처음부터 시작합니다.", 20, 30).color = PixelTheme.TextDim;
            PixelButton("저장 초기화", () => { if (OnResetSave != null) OnResetSave(); }, Parent, 68f, PixelTheme.DangerFill, PixelTheme.DangerEdge);

            // 개발용: 골 장면 미리보기 (에디터·개발용 빌드에서만 보임, 출시 빌드에는 없음)
            if (Application.isEditor || Debug.isDebugBuild)
            {
                currentParent = Section(pages[4], "골 장면 미리보기 (개발용)", true);
                Label("경기 중 내가 골을 넣을 때의 장면을 바로 봅니다. 누를 때마다 1골 → 멀티골 → 해트트릭 순으로 배지가 바뀝니다.", 18, 52).color = PixelTheme.TextDim;
                string previewName = p.playerName;
                var kinds = (TrainingStageDrills.GoalScene[])Enum.GetValues(typeof(TrainingStageDrills.GoalScene));
                string[] names = { "드리블", "패스", "크로스 헤딩", "코너킥 헤딩", "프리킥", "페널티킥", "중거리 슛" };
                // 수비 장면
                var defKinds = (TrainingStageDrills.DefenseScene[])Enum.GetValues(typeof(TrainingStageDrills.DefenseScene));
                string[] defNames = { "수비: 태클", "수비: 슈팅 블록", "수비: 골라인", "수비: 가로채기" };
                for (int i = 0; i < defKinds.Length; i += 2)
                {
                    var drow = NewRect("DefensePreviewRow", Parent); Height(drow.gameObject, 60);
                    var dh = drow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    dh.spacing = 8; dh.childControlWidth = true; dh.childControlHeight = true; dh.childForceExpandWidth = true; dh.childForceExpandHeight = true;
                    for (int k = i; k < i + 2 && k < defKinds.Length; k++)
                    {
                        var dk = defKinds[k];
                        PixelButton(defNames[k], () => PlayDefensePreview(dk, previewName), drow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 20;
                    }
                }
                // 도움 장면: 내가 공을 보내고 동료가 넣음
                var assistKinds = new[] { TrainingStageDrills.GoalScene.Pass, TrainingStageDrills.GoalScene.Cross, TrainingStageDrills.GoalScene.Corner, TrainingStageDrills.GoalScene.LongShot };
                string[] assistNames = { "도움: 스루 패스", "도움: 크로스", "도움: 코너킥", "도움: 내준 패스" };
                for (int i = 0; i < assistKinds.Length; i += 2)
                {
                    var arow = NewRect("AssistPreviewRow", Parent); Height(arow.gameObject, 60);
                    var ah = arow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    ah.spacing = 8; ah.childControlWidth = true; ah.childControlHeight = true; ah.childForceExpandWidth = true; ah.childForceExpandHeight = true;
                    for (int k = i; k < i + 2; k++)
                    {
                        var kind = assistKinds[k];
                        PixelButton(assistNames[k], () => PlayGoalPreview(kind, previewName, true), arow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 20;
                    }
                }
                for (int i = 0; i < kinds.Length; i += 2)
                {
                    var row = NewRect("PreviewRow", Parent); Height(row.gameObject, 60);
                    var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                    h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                    for (int k = i; k < i + 2 && k < kinds.Length; k++)
                    {
                        var kind = kinds[k];
                        PixelButton(k < names.Length ? names[k] : kind.ToString(), () => PlayGoalPreview(kind, previewName), row, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 22;
                    }
                }
            }

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

        // 커리어 탭 카드 (프로필, 트로피, 시즌 기록, 환생 조건)
        public void SetCareer(CareerPageData data) { if (careerPage != null) careerPage.Set(data); }

        // ---------- 미션 탭 ----------
        static void SetButton(Button b, string label, bool ready, Color fill, Color quiet)
        {
            if (b == null) return;
            b.GetComponentInChildren<Text>().text = label;
            ((Image)b.targetGraphic).color = ready ? fill : quiet;
        }

        // header: 위 안내, claimed: 이번 7일 중 받은 날 수, today: 오늘 받을 날(1~7, 없으면 0), rewards: 카드 7장의 보상 글, detail: 아래 보상 설명
        // note: 카드의 보상 단위 안내 (강화 1회·수당 1주가 지금 얼마인지)
        public void SetAttendance(string header, int claimed, int today, string[] rewards, string detail, string note, bool ready)
        {
            if (attendText == null) return;
            attendText.text = header;
            attendText.color = ready ? PixelTheme.Accent : PixelTheme.Text;
            if (attendCards != null) attendCards.Set(claimed, today, rewards);
            attendDetail.text = detail;
            attendNote.text = note;
            SetButton(attendButton, ready ? "출석 보상 받기" : "오늘은 받았어요", ready, PixelTheme.AccentFill, PixelTheme.QuietFill);
        }

        // 미션 한 줄: 설명·진행도·보상, 버튼 (받을 수 있음 / 진행 중 / 받음)
        public void SetMission(int index, string info, string button, bool ready)
        {
            if (index < 0 || index >= missionTexts.Length || missionTexts[index] == null) return;
            missionTexts[index].text = info;
            SetButton(missionButtons[index], button, ready, PixelTheme.ButtonFill, PixelTheme.QuietFill);
        }

        public void SetDailyBonus(string reset, string info, string button, bool ready)
        {
            if (bonusText == null) return;
            resetText.text = reset;
            bonusText.text = info;
            SetButton(bonusButton, button, ready, PixelTheme.AccentFill, PixelTheme.QuietFill);
        }

        // 받을 보상이 있으면 미션 탭 이름에 ! 표시
        public void SetMissionBadge(bool on)
        {
            if (tabLabels[2] == null) return;
            tabLabels[2].text = on ? "미션 !" : "미션";
            tabLabels[2].color = on ? PixelTheme.Accent : PixelTheme.Text;
        }
        public void SetLeaders(string text) { if (leadersText != null) SetLines(leadersText, text, 2); }

        static void SetLines(Text t, string text, int minLines)
        {
            t.text = text;
            int lines = Mathf.Max(minLines, text.Split('\n').Length);
            t.GetComponent<LayoutElement>().preferredHeight = lines * 30f + 8f;
        }

        public void SetSeason(bool seasonComplete, string history)
        {
            if (playLabel == null) return;
            if (historyText != null) SetLines(historyText, history, 2);
            playLabel.text = seasonComplete ? "다음 시즌 시작" : "다음 경기";
        }

        // 경기 탭: 다음 경기 카드, 지난 경기 카드, 순위표
        public void SetMatchPage(NextMatchView next, ResultView last, List<TableRowView> table)
        {
            if (matchPage == null) return;
            matchPage.SetNext(next); matchPage.SetResult(last); matchPage.SetTable(table);
        }

        public void SetLeaderBoard(string header, List<LeaderBlockView> blocks, string footer)
        {
            if (matchPage != null) matchPage.SetLeaders(header, blocks, footer);
        }

        // 커리어 탭의 '소속 팀' 카드와 '이적 제안' 카드들. 제안 카드의 숫자 아래에는 지금 팀과의 차이(▲▼)를 보여 줍니다.
        // summary: 시즌 성적 등급과 제안 수 안내 (null이면 아직 시즌 중)
        // decided: 선택을 확정했으면 true. 이때는 버튼을 누를 수 없고 결과만 보여 줍니다.
        List<TransferOffer> tOffers;
        string tPending, tTeamName, tSummary, tCurrentKey;
        bool tDecided;
        TeamInfoView tCurrent;

        public void SetTransfer(List<TransferOffer> offers, string pendingTeamId, string currentTeamName, string summary, bool decided)
        {
            tOffers = offers; tPending = pendingTeamId; tTeamName = currentTeamName; tSummary = summary; tDecided = decided;
            RenderTransfer();
        }

        // 지금 팀 정보 (능력치가 오르면 선발 확률이 바뀌므로 자주 불림). 바뀌었을 때만 다시 그립니다.
        public void SetCurrentTeam(TeamInfoView team)
        {
            string key = team != null ? JsonUtility.ToJson(team) : "";
            if (key == tCurrentKey) return;
            tCurrentKey = key; tCurrent = team;
            RenderTransfer();
        }

        void RenderTransfer()
        {
            if (transferRoot == null) return;
            foreach (Transform child in transferRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var cur = tCurrent;
            Color good = new Color(.55f, .95f, .65f, 1f);
            if (cur != null)
            {
                var section = Section(transferRoot, "소속 팀", true);
                var card = NewRect("Current", section);
                card.gameObject.AddComponent<Image>().color = new Color(.12f, .20f, .34f, 1f);
                ConfigureStack(card.gameObject, 12, 10, 12, 12, 8);
                var head = NewRect("Head", card); Height(head.gameObject, 84);
                TeamKits.PreviewImage(head, TeamKits.Home(cur.teamId), 0f, 0f, .13f, 1f);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, cur.name, 24, PixelTheme.Text, .15f, .45f, .78f, .95f, TextAnchor.MiddleLeft), 14);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, TeamKits.Name(cur.teamId), 16, PixelTheme.TextDim, .15f, .05f, .78f, .45f, TextAnchor.MiddleLeft), 11);
                MatchResultDialog.Label(head, font, cur.division > 0 ? cur.division + "부" : "", 18, PixelTheme.TextDim, .78f, 0f, 1f, 1f, TextAnchor.MiddleRight);
                var tiles = TileRow(card);
                MiniTile(tiles, "전력", cur.strength.ToString("0"), null, PixelTheme.TextDim);
                MiniTile(tiles, "선발 확률", Mathf.RoundToInt(cur.startChance * 100f) + "%", null, PixelTheme.TextDim);
                MiniTile(tiles, "주급", cur.wage > 0 ? SeasonSystem.Money(cur.wage) : "-", null, PixelTheme.TextDim);
                MiniTile(tiles, "중시 능력", Ko.Attr(cur.focus), null, PixelTheme.TextDim);
                MatchResultDialog.Label(NewRectH(card, 24), font, "내 평점 " + cur.myRating + "  ·  팀 요구 평점 " + cur.required, 16,
                    cur.myRating >= cur.required ? good : PixelTheme.DangerEdge, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                string curId = cur.teamId;
                PixelButton("선수단 보기", () => { if (OnShowSquad != null) OnShowSquad(curId); }, card, 52f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                    .GetComponentInChildren<Text>().fontSize = 21;
            }

            var offers = tOffers; string pendingTeamId = tPending, currentTeamName = tTeamName; bool decided = tDecided;
            bool none = offers == null || offers.Count == 0;
            if (none && tSummary == null) return;

            var body = Section(transferRoot, "이적 제안", true);
            if (tSummary != null)
            {
                var s = TextNode(tSummary, body, 20); s.color = PixelTheme.Accent; s.alignment = TextAnchor.MiddleCenter;
                s.horizontalOverflow = HorizontalWrapMode.Wrap; Height(s.gameObject, 56);
            }
            if (none) return;   // 제안이 없으면 지금 팀에 남습니다
            string noteText = !decided ? "다음 시즌에 갈 팀을 고르거나 잔류하세요. 선택은 한 번뿐입니다."
                : string.IsNullOrEmpty(pendingTeamId) ? "결정 완료: " + currentTeamName + "에 잔류합니다."
                : "결정 완료: 표시된 팀으로 다음 시즌에 이적합니다.";
            var info = TextNode(noteText, body, 18); info.color = PixelTheme.TextDim; info.alignment = TextAnchor.MiddleCenter;
            info.horizontalOverflow = HorizontalWrapMode.Wrap; Height(info.gameObject, 30);
            if (cur != null)
            {
                var hint = TextNode("▲▼ 는 지금 팀(" + cur.name + ")과 비교한 차이입니다.", body, 16); hint.color = PixelTheme.TextDim;
                hint.alignment = TextAnchor.MiddleCenter; hint.horizontalOverflow = HorizontalWrapMode.Wrap; Height(hint.gameObject, 26);
            }
            foreach (var offer in offers)
            {
                string id = offer.teamId;
                bool agreed = id == pendingTeamId;
                // 제안 카드: 팀 이름 + 숫자 타일(지금 팀과의 차이) + 버튼 두 개
                var card = NewRect("Offer", body);
                card.gameObject.AddComponent<Image>().color = agreed ? new Color(.30f, .24f, .08f, 1f) : new Color(.13f, .18f, .28f, 1f);
                ConfigureStack(card.gameObject, 12, 10, 12, 12, 8);
                var head = NewRect("Head", card); Height(head.gameObject, 84);
                TeamKits.PreviewImage(head, TeamKits.Home(offer.teamId), 0f, 0f, .13f, 1f);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, offer.teamName, 24, PixelTheme.Accent, .15f, .45f, .7f, .95f, TextAnchor.MiddleLeft), 14);
                MatchResultDialog.Fit(MatchResultDialog.Label(head, font, TeamKits.Name(offer.teamId), 16, PixelTheme.TextDim, .15f, .05f, .7f, .45f, TextAnchor.MiddleLeft), 11);
                // 부: 지금보다 높은 부면 ▲ 초록, 낮은 부면 ▼ 빨강
                string divText = offer.division > 0 ? offer.division + "부" : "";
                Color divColor = PixelTheme.TextDim;
                if (cur != null && offer.division > 0 && cur.division > 0 && offer.division != cur.division)
                {
                    bool up = offer.division < cur.division;
                    divText = (up ? "▲ " : "▼ ") + divText; divColor = up ? good : PixelTheme.DangerEdge;
                }
                MatchResultDialog.Label(head, font, agreed ? "확정" : divText, 18, agreed ? PixelTheme.Accent : divColor, .7f, 0f, 1f, 1f, TextAnchor.MiddleRight);
                var tiles = TileRow(card);
                Color dc = PixelTheme.TextDim;
                string d1 = cur != null ? Delta(offer.teamStrength - cur.strength, "0", out dc) : null;
                MiniTile(tiles, "전력", offer.teamStrength.ToString("0"), d1, dc);
                string d2 = cur != null ? Delta(Mathf.RoundToInt(offer.startChance * 100f) - Mathf.RoundToInt(cur.startChance * 100f), "0", out dc) : null;
                MiniTile(tiles, "선발 확률", Mathf.RoundToInt(offer.startChance * 100f) + "%", d2 != null && d2 != "=" ? d2 + "%" : d2, dc);
                string d3 = cur != null && offer.weeklyWage > 0 && cur.wage > 0 ? Delta(offer.weeklyWage - cur.wage, "#,0", out dc) : null;
                MiniTile(tiles, "주급", offer.weeklyWage > 0 ? SeasonSystem.Money(offer.weeklyWage) : "-", d3, dc);
                MiniTile(tiles, "중시 능력", Ko.Attr(offer.focus), cur != null ? (offer.focus == cur.focus ? "같음" : "다름") : null, PixelTheme.TextDim);
                var buttons = NewRect("Buttons", card); Height(buttons.gameObject, 56);
                var bh = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
                bh.spacing = 8; bh.childControlWidth = true; bh.childControlHeight = true; bh.childForceExpandWidth = true; bh.childForceExpandHeight = true;
                var button = PixelButton(agreed ? "이적 확정됨" : "이 팀으로 이적", () => onChooseOffer(id), buttons, 0f,
                            agreed ? PixelTheme.AccentFill : decided ? PixelTheme.QuietFill : PixelTheme.ButtonFill,
                            agreed ? PixelTheme.Accent : decided ? PixelTheme.QuietEdge : PixelTheme.ButtonEdge);
                button.GetComponentInChildren<Text>().fontSize = 21;
                if (decided) button.interactable = false;
                PixelButton("선수단 보기", () => { if (OnShowSquad != null) OnShowSquad(id); }, buttons, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                    .GetComponentInChildren<Text>().fontSize = 21;
            }
            var stay = PixelButton((decided && string.IsNullOrEmpty(pendingTeamId) ? "[확정] " : "") + currentTeamName + " 잔류", () => onStay(), body, 68f,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.AccentFill : PixelTheme.QuietFill,
                decided && string.IsNullOrEmpty(pendingTeamId) ? PixelTheme.Accent : PixelTheme.QuietEdge);
            if (decided) stay.interactable = false;
        }

        // 지금 팀과의 차이 글자: 양수 ▲ 초록, 음수 ▼ 빨강, 같으면 "="
        static string Delta(float diff, string format, out Color color)
        {
            if (Mathf.Abs(diff) < 0.5f) { color = PixelTheme.TextDim; return "="; }
            color = diff > 0f ? new Color(.55f, .95f, .65f, 1f) : PixelTheme.DangerEdge;
            return (diff > 0f ? "▲ " : "▼ ") + Mathf.Abs(diff).ToString(format);
        }

        RectTransform TileRow(Transform card)
        {
            var tiles = NewRect("Tiles", card); Height(tiles.gameObject, 72);
            var th = tiles.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 6; th.childControlWidth = true; th.childControlHeight = true; th.childForceExpandWidth = true; th.childForceExpandHeight = true;
            return tiles;
        }

        RectTransform NewRectH(Transform parent, float height) { var r = NewRect("Line", parent); Height(r.gameObject, height); return r; }

        // 카드의 작은 숫자 칸: 값, 차이(없으면 비움), 이름
        void MiniTile(Transform row, string label, string value, string delta, Color deltaColor)
        {
            var tile = NewRect("Tile", row);
            tile.gameObject.AddComponent<Image>().color = new Color(.09f, .12f, .19f, 1f);
            MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, value, 21, PixelTheme.Text, .04f, .50f, .96f, .98f, TextAnchor.MiddleCenter), 12);
            if (!string.IsNullOrEmpty(delta))
                MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, delta, 14, deltaColor, .04f, .27f, .96f, .52f, TextAnchor.MiddleCenter), 10);
            MatchResultDialog.Fit(MatchResultDialog.Label(tile, font, label, 13, PixelTheme.TextDim, .04f, .02f, .96f, .29f, TextAnchor.MiddleCenter), 9);
        }

        // 돈, 주급, 동시 훈련 레벨과 다음 레벨 가격 (price < 0이면 최고 레벨)
        // pay: 경기 한 번에 실제로 받는 돈, agentPercent: 에이전트 보너스(%)
        public void SetMoney(long money, int pay, int agentPercent)
        {
            if (facilityMoneyText != null) facilityMoneyText.text = "보유 돈 " + SeasonSystem.Money(money);
            if (moneyText == null) return;
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
            if (rebirthText != null) SetLines(rebirthText, text, 4);
            if (rebirthFill != null) rebirthFill.color = available ? PixelTheme.RebirthFill : PixelTheme.QuietFill;
        }

        // 경기 영상이 재생되는 동안에는 true입니다. 이 동안 훈련은 멈춥니다.
        public bool IsMatchPlaying { get { return matchView != null && matchView.Active; } }

        // 경기 영상을 보여 주고, 끝나면(또는 건너뛰면) onFinished를 부릅니다. 그림이 없으면 바로 onFinished를 부릅니다.
        public TeamTactic ReplayOwnTactic, ReplayOppTactic;   // 다음 경기 영상의 두 팀 전술 (골 기록과 같은 장면이 나오게)
        public int ReplaySeasonGoals = -1;                    // 다음 경기 영상 전까지의 시즌 골 수 (-1이면 친선 경기 등)
        public int ReplaySeasonAssists = -1;                  // 다음 경기 영상 전까지의 시즌 도움 수
        public int ReplayOwnKit, ReplayOppKit;                // 다음 경기 영상의 두 팀 유니폼 (PlayerLook.Kits 번호)
        public int CreationKit;                               // 새 선수 만들기 미리보기의 유니폼 (시작 팀)
        public void ShowMatchReplay(MatchRecord record, PlayerData player, string ownTeamName, Action onFinished, TeamTactic ownTactic = null, TeamTactic oppTactic = null)
        {
            CloseDialog();
            if (matchView != null)
            {
                matchView.SeasonGoalsBefore = ReplaySeasonGoals; ReplaySeasonGoals = -1;
                matchView.SeasonAssistsBefore = ReplaySeasonAssists; ReplaySeasonAssists = -1;
                matchView.OwnKit = ReplayOwnKit; matchView.OppKit = ReplayOppKit;
            }
            if (matchView == null || !matchView.Show(safeRoot, record, player, ownTeamName, settings.attributeCap, onFinished, ownTactic ?? ReplayOwnTactic, oppTactic ?? ReplayOppTactic))
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
            if (stage != null && stage.Available) stage.PlayUpgrade(r.Attribute, r.Outcome);   // 훈련장 선수가 그 능력치 동작과 결과 반응
            upgradeFx.Play(r, i >= 0 && i < statTexts.Length ? statTexts[i] : null, trainingSettings != null ? trainingSettings.doubleShare : 0.5f);
        }

        // ---------- 골 장면 미리보기 (개발용) ----------
        GoalCelebration goalPreview;
        int previewCount;

        void PlayGoalPreview(TrainingStageDrills.GoalScene kind, string scorer, bool isAssist = false)
        {
            if (goalPreview != null) { goalPreview.DestroyAll(); goalPreview = null; }
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                var tex = Resources.Load<Texture2D>("Art/Match/goal_" + i);
                if (tex == null) { frames = new Sprite[0]; break; }
                tex.filterMode = FilterMode.Point;
                frames[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
            }
            var scene = stage != null && stage.Available ? new TrainingStageView(stage) : null;
            goalPreview = new GoalCelebration(font, safeRoot, frames, scene);
            int goals = previewCount++ % 3 + 1;
            bool header = kind == TrainingStageDrills.GoalScene.Cross || kind == TrainingStageDrills.GoalScene.Corner;
            string how = kind == TrainingStageDrills.GoalScene.LongShot ? "중거리" : kind == TrainingStageDrills.GoalScene.Penalty ? "PK" : kind == TrainingStageDrills.GoalScene.FreeKick ? "프리킥"
                : kind == TrainingStageDrills.GoalScene.Corner ? "코너킥" : "";
            if (header) how = (how + " 헤딩").Trim();
            bool assisted = kind == TrainingStageDrills.GoalScene.Pass || header || (kind == TrainingStageDrills.GoalScene.LongShot && previewCount % 2 == 0);   // 중거리는 혼자/패스 받아 번갈아
            goalPreview.Play(new GoalInfo
            {
                oppKit = PreviewOppKit(),
                minute = "67'", scorer = scorer, how = how, assist = assisted ? "J. Teammate" : "",
                ownName = "My Team", oppName = "Rival FC", goalsFor = goals, goalsAgainst = 1,
                matchGoals = goals, seasonGoal = 6 + goals, scene = kind,
                isAssist = isAssist, partner = isAssist ? "J. Teammate" : ""
            });
        }

        void PlayDefensePreview(TrainingStageDrills.DefenseScene kind, string name)
        {
            if (goalPreview != null) { goalPreview.DestroyAll(); goalPreview = null; }
            var scene = stage != null && stage.Available ? new TrainingStageView(stage) : null;
            goalPreview = new GoalCelebration(font, safeRoot, new Sprite[0], scene);
            goalPreview.Play(new GoalInfo
            {
                oppKit = PreviewOppKit(),
                minute = "71'", scorer = name, isDefense = true, defense = kind, how = "", assist = "",
                ownName = "My Team", oppName = "Rival FC", goalsFor = 1, goalsAgainst = 0, matchGoals = previewCount++ % 5 + 1
            });
        }

        // 미리보기의 상대 유니폼: 누를 때마다 다른 팀 홈 유니폼 (내 유니폼과 겹치면 다음 팀)
        int PreviewOppKit()
        {
            int teams = (PlayerLook.KitCount - 1) / 2;
            for (int k = 0; k < teams; k++)
            {
                int home = 1 + ((previewCount * 7 + k) % teams) * 2;
                if (home != lookKit) return home;
            }
            return 0;
        }

        void TickGoalPreview(float dt)
        {
            if (goalPreview == null) return;
            goalPreview.Tick(dt);
            if (!goalPreview.Active) { goalPreview.DestroyAll(); goalPreview = null; }
        }

        // ---------- 팝업 ----------
        // ---------- 외형(머리색, 피부색, 유니폼) ----------
        int lookHair, lookSkin, lookKit, lookBoots;   // 지금 선수의 외형
        int newHair, newSkin, newKit, newBoots;       // 선수 생성 화면과 변경 창에서 고르는 외형
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
            previewTexture = PlayerLook.LoadRecolored(asset.bytes, newHair, newSkin, newKit, newBoots);
            if (previewTexture == null) return;
            previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height), new Vector2(.5f, .5f), 100f);
            target.sprite = previewSprite; target.enabled = true;
        }

        // 머리색, 피부색, 축구화를 순서대로 바꾸는 버튼 세 개 (newHair, newSkin, newBoots 값을 바꾸고 미리보기를 갱신).
        // 유니폼은 고를 수 없고 소속 팀 유니폼을 입습니다.
        void AddLookButtons(Transform parent, Image preview)
        {
            Button hairButton = null, skinButton = null, bootButton = null;
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
            bootButton = PixelButton("축구화: " + PlayerLook.BootNames[newBoots], () => {
                newBoots = (newBoots + 1) % PlayerLook.BootCount;
                bootButton.GetComponentInChildren<Text>().text = "축구화: " + PlayerLook.BootNames[newBoots];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        void ShowLookDialog()
        {
            newHair = lookHair; newSkin = lookSkin; newKit = lookKit; newBoots = lookBoots;
            var inner = BuildDialog("외형", "머리색, 피부색, 축구화를 고르세요. 유니폼은 소속 팀 유니폼입니다.", 36f, 20);
            var preview = AddPreview(inner, 220f);
            ApplyPreview(preview);
            AddLookButtons(inner, preview);
            PixelButton("적용", () => { CloseDialog(); DestroyPreview(); if (OnApplyLook != null) OnApplyLook(newHair, newSkin, newKit, newBoots); }, inner, 76f);
            PixelButton("취소", () => { CloseDialog(); DestroyPreview(); }, inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 선수의 외형이 바뀌었을 때 훈련장 그림을 갱신합니다.
        public void ApplyLook(PlayerData p)
        {
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor; lookBoots = p.bootColor;
            if (stage != null) stage.SetLook(lookHair, lookSkin, lookKit, lookBoots);
        }

        Transform BuildDialog(string title, string body, float bodyHeight, int bodyFont = 24)
        {
            if (overlay != null) Destroy(overlay);
            var root = NewRect("Dialog", safeRoot); Stretch(root); overlay = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            // 내용이 화면보다 길면 위아래로 스크롤하고, 짧으면 가운데에 둡니다.
            var view = NewRect("View", root); Stretch(view);
            view.offsetMin = new Vector2(0, 24); view.offsetMax = new Vector2(0, -24);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            var holder = NewRect("Holder", view);
            holder.anchorMin = new Vector2(.06f, 1f); holder.anchorMax = new Vector2(.94f, 1f);
            holder.pivot = new Vector2(.5f, 1f); holder.offsetMin = holder.offsetMax = Vector2.zero;
            ConfigureStack(holder.gameObject, 0, 0, 0, 0, 0);
            holder.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = holder; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            view.gameObject.AddComponent<DialogCenter>().Init(scroll, view, holder);
            var inner = Section(holder, title);
            var b = TextNode(body, inner, bodyFont); Height(b.gameObject, bodyHeight);
            return inner;
        }

        void CloseDialog() { if (overlay != null) Destroy(overlay); overlay = null; }

        public void ShowPopup(string title, string body, float bodyHeight = 190f, Action onClose = null, int bodyFont = 24)
        {
            var inner = BuildDialog(title, body, bodyHeight, bodyFont);
            PixelButton("확인", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 카드형 팝업: build가 내용 칸(세로로 쌓임)을 채웁니다. 글자만 늘어놓는 대신 숫자 타일·배지로 보여 줍니다.
        public void ShowCards(string title, Action<Transform, Font> build, Action onClose = null, string okLabel = "확인")
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("CardBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 8);
            build(body, font);
            PixelButton(okLabel, () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 카드형 확인 창: 내용 + 실행 버튼 + 취소
        public void ShowCardsConfirm(string title, Action<Transform, Font> build, string yesLabel, Action onYes)
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("CardBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 8);
            build(body, font);
            PixelButton(yesLabel, () => { CloseDialog(); if (onYes != null) onYes(); }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("취소", () => CloseDialog(), inner, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 경기 결과 창: 결과 배지·큰 점수, 골 타임라인, 내 기록 타일, 보상 줄
        public void ShowMatchResult(string title, MatchResultData data, Action onClose)
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("ResultBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 4);
            MatchResultDialog.Build(body, font, data);
            PixelButton("확인", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 72f);
        }

        // 선수단 창: 팀 요약 카드, 주전(4-4-2)과 교체 선수 표
        public void ShowSquad(string title, SquadView view)
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("SquadBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 3);
            SquadDialog.Build(body, font, view);
            PixelButton("닫기", () => CloseDialog(), inner, 64f);
        }

        // 팀 고르기 창: 팀 이름 버튼들 (누르면 그 팀 선수단)
        public void ShowTeamPicker(List<KeyValuePair<string, string>> teams, string title)
        {
            var inner = BuildDialog(title, "선수단을 볼 팀을 고르세요.", 34f, 22);
            foreach (var t in teams)
            {
                string id = t.Key;
                var teamButton = PixelButton(t.Value, () => { CloseDialog(); if (OnShowSquad != null) OnShowSquad(id); }, inner, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                teamButton.GetComponentInChildren<Text>().fontSize = 22;
                TeamKits.PreviewImage(teamButton.transform.GetChild(0), TeamKits.Home(id), .01f, .04f, .11f, .96f);   // 왼쪽에 팀 유니폼
            }
            PixelButton("닫기", () => CloseDialog(), inner, 60f);
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
