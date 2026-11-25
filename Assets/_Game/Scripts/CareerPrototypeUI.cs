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

    // 임시 화면입니다. 게임 규칙과 저장 규칙은 이 클래스 밖에 있습니다.
    // 화면은 HOME / MATCH / CAREER 세 탭으로 나뉩니다.
    public partial class CareerPrototypeUI : MonoBehaviour
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
        public Action<int> OnPickLeague;            // 리그 팀 목록에서 다른 부 고르기
        public Action OnRetire;                     // 은퇴하기
        public Action<int> OnShowLegend;            // 내가 키운 선수의 커리어 보기 (커리어 번호)
        public Action OnCancelCreation;             // 은퇴 후 새 선수 만들기에서 취소
        public Action<string, int, Formation, TacticStyle> OnFoundClub;   // 나만의 팀 창단 (이름, 유니폼 디자인, 포메이션, 스타일)
        public Action OnOpenClub, OnClubPlay, OnClubSquad, OnClubBack;   // 나만의 팀 모드: 열기, 경기(또는 다음 시즌), 선수단, 선수 키우기로 돌아가기
        public Action<int> OnClubSign;              // 나만의 팀 모드: 은퇴 선수 무료 영입 (커리어 번호)
        public Action OnPickCareer, OnPickClub;     // 시작 화면에서 모드 고르기
        public Action OnExitToMenu;                 // 선수 키우기 설정 탭: 모드 선택으로 나가기
        public bool ReplayTeamMode;                 // 다음 경기 영상이 나만의 팀 모드 경기인지
        Image retireFill; GameObject foundButton, clubSquadButton;
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
        Text[] statTexts = new Text[6];          // 능력치 6개 (GK 능력은 골키퍼만 칸이 생김)
        Button[] upgradeButtons = new Button[6];
        Button rateButton;
        RectTransform[] statBars = new RectTransform[6];
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
    }
}
