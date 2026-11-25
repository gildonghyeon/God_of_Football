using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 경기 영상 화면. 90분을 약 15초에 보여 주며, 위에서 본 전체 코트에 점으로 선수와 공이 움직입니다.
    // 내 선수가 골을 넣으면 세리머니 화면(GoalCelebration: GOAL!, 골망, 꽃가루, 득점 정보), 도움을 하면 도움! 카드가 나옵니다.
    //   그림: Resources/Art/Match/ (pitch, dot_blue, dot_red, dot_you, ball, goal_0 ~ goal_7)
    public class MatchReplayView
    {
        // 아래 두 값에 속도 배율을 곱해 씁니다. 1배속 = 0.32배 (90분이 약 47초), 2배속 = 정확히 그 두 배 (약 24초).
        const float MinutesPerSecond = 6f;    // 90분 / 15초
        const float SimSpeed = 8f;            // 실제 1초 동안 진행하는 경기 속 시간(초)
        const string SpeedPref = "ReplaySpeed";
        int speed = 2;                        // 1배속 또는 2배속 (기본 2배속, 마지막 선택을 기억)
        readonly Image[] speedEdges = new Image[2], speedFills = new Image[2];
        readonly Text[] speedTexts = new Text[2];
        // stadium.png(912x1490) 안에서 경기장 흰 선의 위치입니다. 그림을 바꾸면 이 숫자만 맞춰 주세요.
        const float ImgW = 912f, ImgH = 1490f;
        const float FieldLeft = 88f, FieldRight = 823.5f;        // 왼쪽·오른쪽 터치라인
        const float FieldTop = 145f, FieldMid = 737.5f, FieldBottom = 1343.5f;   // 위쪽 골라인, 중앙선, 아래쪽 골라인

        readonly Font font;
        Sprite pitchSprite, blueSprite, redSprite, greenSprite, youSprite, ballSprite;
        readonly Sprite[] goalFrames = new Sprite[8];

        public bool Active { get; private set; }

        MatchReplayEngine engine;
        MatchRecord record;
        Action onFinished;
        string ownName, oppName;

        GameObject root;
        GoalCelebration celebration;   // 내 골 세리머니
        string playerName = "";
        public int OwnKit, OppKit;           // 두 팀 유니폼 (PlayerLook.Kits 번호, 0이면 기본 파랑·빨강 점)
        public int YouKit;                   // 내 선수 점의 유니폼 (골키퍼면 골키퍼 유니폼, 0이면 OwnKit)
        public bool TeamMode;                // 나만의 팀 모드: 내 선수 없이 팀 경기 (벤치 안내 없음)
        public float[][] OwnSkills, OppSkills;   // 두 팀 선수 11명의 세부 능력치 (자리 순서). 없으면 보통 실력
        float paceNow = 1f;                  // 지금 화면 속도 (부드럽게 바뀜)
        public string[] OwnNames, OppNames;   // 두 팀 주전 11명의 이름 (자리 순서). 경기 MVP 이름에 씀
        public MatchBox LastBox;              // 끝난 경기의 우리 팀 기록과 MVP (경기 결과 창에서 가져감)
        RectTransform offLineRect, offMarkRect; int offShown; float offStart = -99f;   // 오프사이드 판정 때 잠깐 보여 주는 선
        public string[] OwnLabels;           // 우리 팀 선수 11명의 이름표 (포메이션 자리 순서, 포지션 색 글자 포함). 없으면 표시 안 함
        readonly RectTransform[] ownLabelRects = new RectTransform[11];
        readonly List<UnityEngine.Object> kitObjects = new List<UnityEngine.Object>();   // 유니폼 점 그림 (영상을 닫을 때 지움)
        public TrainingStageView SceneArt;   // 내 골 장면에 쓸 선수 그림 (훈련장과 같은 그림, 없으면 골망 장면)
        public int SeasonGoalsBefore = -1;
        public int SeasonAssistsBefore = -1;   // 이번 경기 전까지 시즌 도움 수 (-1이면 '시즌 N호 도움'을 보여 주지 않음)
        ReplayEvent countedAssist;             // 도움을 이미 센 장면 (패스할 때 세고, 코너킥처럼 패스 신호가 없으면 골 때 셈)
        Position youPosition;                  // 내 포지션 (수비 장면 종류를 고를 때)
        int defenseShown;                      // 이번 경기에 보여 준 수비 장면 수 (전반 첫 수비, 60분 뒤 첫 수비 - 최대 2번)   // 이번 경기 전까지 시즌 골 수 (-1이면 '시즌 N호 골'을 보여 주지 않음, 친선 경기 등)
        RectTransform pitchRect, youLabel, clockFill;
        readonly RectTransform[] dotRects = new RectTransform[22];
        RectTransform ballRect;
        Text callLabel;   // 공 옆에 뜨는 판정 (스로인, 코너킥, 프리킥…): 어두운 상자에 큰 글자 + 어느 팀 공인지
        Text callTeamLabel;
        RectTransform callBox;
        CanvasGroup callGroup;
        Image callStripe;   // 상자 왼쪽 띠: 공을 가진 팀 색
        int lastCallId; float callTimer;   // 판정 글자는 실제 시간으로 0.9초 보여 줌
        Text scoreText, clockText, benchText;
        // 사건 카드 (골, 실점, 도움, 태클, 경기 종료): 경기장 위쪽에 잠깐 내려왔다 사라짐
        RectTransform eventCard;
        Image eventFill, eventEdge;
        Text eventTitle, eventSub;
        // 진행 기록: 최근 3개 (분 배지 + 내용), 아래가 최신
        const int FeedRows = 3;
        readonly Image[] feedBadges = new Image[FeedRows];
        readonly Text[] feedMinutes = new Text[FeedRows], feedLines = new Text[FeedRows];
        readonly List<(string minute, string text, Color color)> feedItems = new List<(string, string, Color)>();
        // 내 기록 칩
        readonly Text[] chipValues = new Text[5];
        RectTransform chipRow;
        float scorePulse;
        Color scoreFlash = Color.white;

        static readonly Color OurGoalColor = new Color(.30f, .50f, .90f, 1f);
        static readonly Color TheirGoalColor = new Color(.80f, .30f, .30f, 1f);
        static readonly Color AssistColor = new Color(.85f, .65f, .15f, 1f);
        static readonly Color TackleColor = new Color(.25f, .65f, .40f, 1f);
        static readonly Color NeutralColor = new Color(.35f, .40f, .52f, 1f);

        float clock, bannerT, bannerDur, finishT;
        int goalsFor, goalsAgainst, youSlot;
        int ourNote, theirNote;   // 다음에 보여 줄 골 기록 (GoalNote) 순서
        int yourGoals, yourAssists, yourShots, yourTackles;
        int livePassAtt, livePassComp;       // 영상 속 내 패스 (시도, 성공)
        int teamShots, teamOnTarget, teamPassAtt, teamPassComp, teamTackleAtt, teamTackleWon, teamSaves;   // 나만의 팀 경기: 우리 팀 기록
        float waitT;
        string lastStatsLine = "";
        bool finishing;

        public MatchReplayView(Font font)
        {
            this.font = font;
            pitchSprite = Load("stadium", true); blueSprite = Load("dot_blue", true); redSprite = Load("dot_red", true);
            greenSprite = Load("dot_green", true); youSprite = Load("dot_you", true); ballSprite = Load("ball", true);
            for (int i = 0; i < goalFrames.Length; i++) goalFrames[i] = Load("goal_" + i, false);
        }

        public bool Available
        {
            get
            {
                if (pitchSprite == null || blueSprite == null || redSprite == null || greenSprite == null || youSprite == null || ballSprite == null) return false;
                foreach (var f in goalFrames) if (f == null) return false;
                return true;
            }
        }

        // smooth: 사진처럼 부드러운 그림은 true, 도트 그림은 false
        static Sprite Load(string name, bool smooth)
        {
            var tex = Resources.Load<Texture2D>("Art/Match/" + name);
            if (tex == null) return null;
            tex.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }

        // ---------- 시작 ----------
        public bool Show(RectTransform parent, MatchRecord m, PlayerData player, string ownTeamName, float attributeCap, Action finished, TeamTactic ownTactic, TeamTactic oppTactic)
        {
            if (Active) return true;
            LastBox = null;
            if (!Available) return false;
            record = m; onFinished = finished; ownName = ownTeamName; oppName = m.opponentName; playerName = player.playerName;
            ownTactic = ownTactic ?? new TeamTactic(); oppTactic = oppTactic ?? new TeamTactic();
            int mySlot = TacticSystem.YouSlot(ownTactic.formation, player.position);
            youPosition = player.position; defenseShown = 0;
            youSlot = m.playerPlayed ? mySlot : -1;
            var rng = new System.Random(m.seed ^ 0x2f6e2b1);
            // 내 선수의 능력치가 영상 속 움직임에 반영됩니다. (속도, 후반 피로, 패스 정확도, 슛 속도와 유효 슈팅, 공 지키기)
            var stats = ReplayStats.From(player, m, attributeCap);
            var plan = MatchReplayPlan.Build(m, m.playerPlayed, mySlot, rng, stats.shootN, m.defensiveActions, ownTactic.formation, oppTactic.formation, OwnSkills, OppSkills);
            engine = new MatchReplayEngine(plan, youSlot, rng, ownTactic, oppTactic);
            engine.stats = stats;
            engine.SetSkills(0, OwnSkills, attributeCap); engine.SetSkills(1, OppSkills, attributeCap);   // 선수마다 능력치가 경기 영상에 반영됨
            paceNow = 1f;
            engine.GoalScored = OnGoal; engine.AssistMade = OnAssist; engine.ShotResolved = OnShot;
            engine.YouPassed = ok => { livePassAtt++; if (ok) livePassComp++; };   // 영상 속 내 패스를 그대로 셈
            teamShots = teamOnTarget = teamPassAtt = teamPassComp = teamTackleAtt = teamTackleWon = teamSaves = 0;
            engine.TeamShot = (team, onTarget) => { if (team == 0) { teamShots++; if (onTarget) teamOnTarget++; } };
            engine.TeamPassed = (team, ok) => { if (team == 0) { teamPassAtt++; if (ok) teamPassComp++; } };
            engine.TeamTackled = (team, ok) => { if (team == 0) { teamTackleAtt++; if (ok) teamTackleWon++; } };
            engine.KeeperSaved = team => { if (team == 0) teamSaves++; };
            engine.TackleWon = OnTackle; engine.ShotTaken = OnShotTaken; engine.SetPieceCalled = OnSetPiece; engine.HalfTime = OnHalfTime;
            clock = 0f; goalsFor = 0; goalsAgainst = 0; bannerT = 0f; finishing = false; countedAssist = null; ourNote = 0; theirNote = 0;
            yourGoals = 0; yourAssists = 0; yourShots = 0; yourTackles = 0; livePassAtt = 0; livePassComp = 0; waitT = 0f; lastStatsLine = "";
            feedItems.Clear(); scorePulse = 0f; lastCallId = 0; callTimer = 0f;
            try { speed = Mathf.Clamp(PlayerPrefs.GetInt(SpeedPref, 2), 1, 2); } catch { speed = 2; }
            BuildUi(parent);
            Feed("0'", m.playerPlayed || TeamMode ? "킥오프" : "킥오프 (벤치 대기)", NeutralColor);
            SoundFx.Play(Sfx.Whistle);
            Active = true;
            Tick(0f);
            return true;
        }

        // ---------- 화면 만들기 ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Anchored(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        Text TextNode(string value, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var r = NewRect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = color;
            t.alignment = anchor; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // 칸보다 긴 글자는 칸에 맞게 작아지게 (팀 이름 등)
        static Text Fit(Text t, int min)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // 테두리가 있는 상자. 안쪽 칸을 돌려줍니다.
        static RectTransform Box(Transform parent, Color edge, Color fill)
        {
            var outer = NewRect("Box", parent);
            outer.gameObject.AddComponent<Image>().color = edge;
            var inner = NewRect("Fill", outer);
            Anchored(inner, 0, 0, 1, 1);
            inner.offsetMin = new Vector2(4, 4); inner.offsetMax = new Vector2(-4, -4);
            inner.gameObject.AddComponent<Image>().color = fill;
            return outer;
        }

        void BuildUi(RectTransform parent)
        {
            var rootRect = NewRect("MatchReplay", parent);
            Anchored(rootRect, 0, 0, 1, 1);
            rootRect.gameObject.AddComponent<Image>().color = new Color(.04f, .06f, .10f, 1);   // 뒤쪽 클릭을 막음
            root = rootRect.gameObject;

            // 위쪽: 점수와 시계
            const float HudBottom = 136f;   // 화면 위쪽에서 점수판 아래 끝까지의 거리
            var hud = Box(rootRect, PixelTheme.PanelEdge, new Color(PixelTheme.Panel.r, PixelTheme.Panel.g, PixelTheme.Panel.b, .86f));
            hud.anchorMin = new Vector2(0, 1); hud.anchorMax = new Vector2(1, 1); hud.pivot = new Vector2(.5f, 1);
            hud.offsetMin = new Vector2(16, -HudBottom); hud.offsetMax = new Vector2(-16, -12);
            var hudInner = hud.GetChild(0) as RectTransform;
            var left = TextNode(ownName, hudInner, 22, TextAnchor.MiddleLeft, new Color(.55f, .75f, 1f));
            Anchored(left.rectTransform, .03f, .45f, .38f, .95f);
            scoreText = TextNode("0 - 0", hudInner, 44, TextAnchor.MiddleCenter, PixelTheme.Text);
            Anchored(scoreText.rectTransform, .38f, .42f, .62f, .98f);
            var right = TextNode(oppName, hudInner, 22, TextAnchor.MiddleRight, new Color(1f, .6f, .6f));
            Anchored(right.rectTransform, .62f, .45f, .97f, .95f);
            clockText = TextNode("0'", hudInner, 26, TextAnchor.MiddleLeft, PixelTheme.Accent);
            Anchored(clockText.rectTransform, .03f, .04f, .2f, .42f);
            var barBack = NewRect("ClockBack", hudInner);
            Anchored(barBack, .22f, .12f, .69f, .32f);
            barBack.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            clockFill = NewRect("ClockFill", barBack);
            Anchored(clockFill, 0, 0, 0, 1);
            clockFill.gameObject.AddComponent<Image>().color = PixelTheme.Bar;
            // 배속 버튼: 시계 막대 오른쪽
            for (int i = 0; i < 2; i++)
            {
                int value = i + 1;
                var b = Box(hudInner, PixelTheme.QuietEdge, PixelTheme.QuietFill);
                Anchored(b, i == 0 ? .71f : .845f, .03f, i == 0 ? .835f : .97f, .42f);
                speedEdges[i] = b.GetComponent<Image>();
                speedFills[i] = b.GetChild(0).GetComponent<Image>();
                speedTexts[i] = Fit(TextNode(value + "배속", b.GetChild(0), 18, TextAnchor.MiddleCenter, PixelTheme.Text), 12);
                Anchored(speedTexts[i].rectTransform, 0, 0, 1, 1);
                var button = b.gameObject.AddComponent<Button>();
                button.targetGraphic = speedFills[i];
                button.onClick.AddListener(() => SetSpeed(value));
            }
            RefreshSpeed();

            // 가운데: 코트. 점수판 아래에 두고, 아래쪽 골라인이 기록 띠에 가리지 않도록 아래도 조금 띄웁니다.
            // 점수판은 코트 그림보다 앞에 그립니다.
            var area = NewRect("PitchArea", rootRect);
            Anchored(area, 0, 0, 1, 1);
            area.offsetMin = new Vector2(0, 66f); area.offsetMax = new Vector2(0, -(HudBottom + 8f));
            hud.SetAsLastSibling();
            pitchRect = NewRect("Pitch", area);
            pitchRect.anchorMin = pitchRect.anchorMax = new Vector2(.5f, .5f);
            pitchRect.pivot = new Vector2(.5f, .5f);
            pitchRect.sizeDelta = new Vector2(100, 100);
            var fitter = pitchRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = ImgW / ImgH;
            var pitchImage = pitchRect.gameObject.AddComponent<Image>();
            pitchImage.sprite = pitchSprite; pitchImage.raycastTarget = false;

            // 선수 점은 두 팀 유니폼 무늬로 칠합니다 (골키퍼는 초록, 내 선수는 금색 테두리)
            var ownDot = OwnKit > 0 ? TeamKits.Dot(OwnKit, 25, new Color32(255, 255, 255, 238), 2, kitObjects) : blueSprite;
            var oppDot = OppKit > 0 ? TeamKits.Dot(OppKit, 25, new Color32(255, 255, 255, 238), 2, kitObjects) : redSprite;
            int meKit = YouKit > 0 ? YouKit : OwnKit;
            var meDot = meKit > 0 ? TeamKits.Dot(meKit, 35, new Color32(255, 205, 61, 252), 4, kitObjects) : youSprite;
            for (int i = 0; i < 22; i++)
            {
                var r = NewRect(i < 11 ? "Ours" + i : "Theirs" + (i - 11), pitchRect);
                var img = r.gameObject.AddComponent<Image>();
                img.sprite = i == youSlot ? meDot : ((i % 11) == 0 ? greenSprite : (i < 11 ? ownDot : oppDot));
                img.raycastTarget = false;
                dotRects[i] = r;
            }
            ballRect = NewRect("Ball", pitchRect);
            var ballImage = ballRect.gameObject.AddComponent<Image>();
            ballImage.sprite = ballSprite; ballImage.raycastTarget = false;
            for (int i = 0; i < 11; i++)   // 우리 팀 선수 이름표 (포지션 + 이름)
            {
                ownLabelRects[i] = null;
                if (OwnLabels == null || i >= OwnLabels.Length || string.IsNullOrEmpty(OwnLabels[i])) continue;
                var lr = NewRect("OwnName" + i, pitchRect);
                var lt = TextNode(OwnLabels[i], lr, 14, TextAnchor.MiddleCenter, Color.white);
                Anchored(lt.rectTransform, 0, 0, 1, 1);
                lt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .85f);
                ownLabelRects[i] = lr;
            }
            // 오프사이드 선: 패스 순간 마지막 수비수 라인(노란 선)과 그때 받는 선수가 있던 자리(노란 점)
            offLineRect = NewRect("OffsideLine", pitchRect);
            var oli = offLineRect.gameObject.AddComponent<Image>(); oli.color = new Color(1f, .85f, .2f, .9f); oli.raycastTarget = false;
            offMarkRect = NewRect("OffsideMark", pitchRect);
            var omi = offMarkRect.gameObject.AddComponent<Image>(); omi.color = new Color(1f, .85f, .2f, .75f); omi.raycastTarget = false;
            offLineRect.gameObject.SetActive(false); offMarkRect.gameObject.SetActive(false);
            offShown = 0; offStart = -99f;
            youLabel = NewRect("YouLabel", pitchRect);
            var youText = TextNode("나", youLabel, 18, TextAnchor.MiddleCenter, PixelTheme.Accent);
            Anchored(youText.rectTransform, 0, 0, 1, 1);
            youLabel.gameObject.SetActive(youSlot >= 0);
            callBox = NewRect("Call", pitchRect);
            callBox.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.86f);
            callBox.GetComponent<Image>().raycastTarget = false;
            callGroup = callBox.gameObject.AddComponent<CanvasGroup>(); callGroup.blocksRaycasts = false;
            var stripe = NewRect("Stripe", callBox); Anchored(stripe, 0f, 0f, 0.03f, 1f);
            callStripe = stripe.gameObject.AddComponent<Image>(); callStripe.raycastTarget = false;
            callLabel = TextNode("", callBox, 30, TextAnchor.MiddleCenter, Color.white);
            Anchored(callLabel.rectTransform, 0.05f, 0.42f, 0.97f, 0.98f);
            callLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            callLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .9f);
            callTeamLabel = TextNode("", callBox, 18, TextAnchor.MiddleCenter, PixelTheme.TextDim);
            Anchored(callTeamLabel.rectTransform, 0.05f, 0.04f, 0.97f, 0.44f);
            callTeamLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            callBox.gameObject.SetActive(false);

            // 사건 카드 (골!, 실점, 도움!, 태클!, 경기 종료): 테두리 있는 띠에 큰 제목과 설명
            eventCard = NewRect("EventCard", area);
            eventCard.anchorMin = new Vector2(.06f, .66f); eventCard.anchorMax = new Vector2(.94f, .86f);
            eventCard.offsetMin = eventCard.offsetMax = Vector2.zero;
            eventEdge = eventCard.gameObject.AddComponent<Image>(); eventEdge.raycastTarget = false;
            var fillRect = NewRect("Fill", eventCard); Anchored(fillRect, 0, 0, 1, 1);
            fillRect.offsetMin = new Vector2(4, 4); fillRect.offsetMax = new Vector2(-4, -4);
            eventFill = fillRect.gameObject.AddComponent<Image>(); eventFill.raycastTarget = false;
            eventTitle = TextNode("", fillRect, 44, TextAnchor.MiddleCenter, Color.white);
            Anchored(eventTitle.rectTransform, 0, .4f, 1, 1);
            var titleOutline = eventTitle.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, .8f); titleOutline.effectDistance = new Vector2(3, -3);
            eventSub = TextNode("", fillRect, 20, TextAnchor.MiddleCenter, Color.white);
            Anchored(eventSub.rectTransform, .02f, .02f, .98f, .42f);
            eventCard.gameObject.SetActive(false);

            // 아래쪽: 내 선수 기록, 진행 기록, 건너뛰기 (경기장 가장자리 위에 반투명 띠를 깔아 글자가 잘 보이게 함)
            var bottomBand = NewRect("BottomBand", rootRect);
            bottomBand.anchorMin = new Vector2(0, 0); bottomBand.anchorMax = new Vector2(1, 0);
            bottomBand.pivot = new Vector2(.5f, 0);
            bottomBand.offsetMin = Vector2.zero; bottomBand.offsetMax = new Vector2(0, 146);
            bottomBand.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, .78f);
            bottomBand.gameObject.GetComponent<Image>().raycastTarget = false;
            // 내 기록 칩: 골 / 도움 / 슈팅 / 패스 / 태클
            chipRow = NewRect("Chips", rootRect);
            chipRow.anchorMin = new Vector2(0, 0); chipRow.anchorMax = new Vector2(1, 0); chipRow.pivot = new Vector2(0, 0);
            chipRow.offsetMin = new Vector2(16, 102); chipRow.offsetMax = new Vector2(-16, 140);
            var chipLayout = chipRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 6; chipLayout.childControlWidth = true; chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = true; chipLayout.childForceExpandHeight = true;
            System.Array.Clear(chipValues, 0, chipValues.Length);   // 지난 경기 칸 지우기 (골키퍼는 4칸)
            string[] chipNames = TeamMode ? new[] { "슈팅", "유효슈팅", "패스", "태클", "선방" }   // 나만의 팀: 우리 팀 기록 (패스·태클은 성공/시도, 선방은 횟수)
                : youPosition == Position.Goalkeeper
                ? new[] { "선방", "실점", "선방률", "패스" }   // 골키퍼: 골·도움 대신 (4칸)
                : new[] { "골", "도움", "슈팅", "패스", "태클" };
            for (int i = 0; i < chipNames.Length; i++)
            {
                var chip = NewRect("Chip", chipRow);
                chip.gameObject.AddComponent<Image>().color = new Color(.12f, .16f, .25f, .95f);
                var label = Fit(TextNode(chipNames[i], chip, 16, TextAnchor.MiddleLeft, PixelTheme.TextDim), 10);
                Anchored(label.rectTransform, .07f, 0, TeamMode ? .52f : .45f, 1);
                chipValues[i] = Fit(TextNode("0", chip, 22, TextAnchor.MiddleRight, PixelTheme.Accent), 14);
                Anchored(chipValues[i].rectTransform, .45f, .05f, .94f, .95f);
            }
            benchText = TextNode("벤치 대기 중", rootRect, 20, TextAnchor.MiddleCenter, PixelTheme.TextDim);
            benchText.rectTransform.anchorMin = new Vector2(0, 0); benchText.rectTransform.anchorMax = new Vector2(1, 0); benchText.rectTransform.pivot = new Vector2(0, 0);
            benchText.rectTransform.offsetMin = new Vector2(16, 102); benchText.rectTransform.offsetMax = new Vector2(-16, 140);
            chipRow.gameObject.SetActive(youSlot >= 0 || TeamMode); benchText.gameObject.SetActive(youSlot < 0 && !TeamMode);
            // 진행 기록: 분 배지 + 내용, 3줄
            for (int i = 0; i < FeedRows; i++)
            {
                var row = NewRect("FeedRow", rootRect);
                row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0); row.pivot = new Vector2(0, 0);
                float y = 12f + (FeedRows - 1 - i) * 29f;   // 0번이 맨 위(오래됨), 마지막이 맨 아래(최신)
                row.offsetMin = new Vector2(16, y); row.offsetMax = new Vector2(-188, y + 26f);
                var badge = NewRect("Minute", row);
                badge.anchorMin = new Vector2(0, 0); badge.anchorMax = new Vector2(0, 1); badge.pivot = new Vector2(0, .5f);
                badge.sizeDelta = new Vector2(52, 0); badge.anchoredPosition = Vector2.zero;
                feedBadges[i] = badge.gameObject.AddComponent<Image>(); feedBadges[i].raycastTarget = false;
                feedMinutes[i] = TextNode("", badge, 17, TextAnchor.MiddleCenter, Color.white);
                Anchored(feedMinutes[i].rectTransform, 0, 0, 1, 1);
                feedLines[i] = TextNode("", row, 19, TextAnchor.MiddleLeft, PixelTheme.Text);
                Anchored(feedLines[i].rectTransform, 0, 0, 1, 1);
                feedLines[i].rectTransform.offsetMin = new Vector2(60, 0);
                Fit(feedLines[i], 13);
                row.gameObject.SetActive(false);
            }
            var skip = Box(rootRect, PixelTheme.QuietEdge, PixelTheme.QuietFill);
            skip.anchorMin = skip.anchorMax = new Vector2(1, 0); skip.pivot = new Vector2(1, 0);
            skip.anchoredPosition = new Vector2(-16, 16); skip.sizeDelta = new Vector2(160, 76);
            var skipButton = skip.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skip.GetChild(0).GetComponent<Image>();
            skipButton.onClick.AddListener(Skip);
            var skipText = TextNode("건너뛰기", skip.GetChild(0), 26, TextAnchor.MiddleCenter, PixelTheme.Text);
            Anchored(skipText.rectTransform, 0, 0, 1, 1);

            // 내 골 세리머니 (평소에는 숨김)
            var sceneStage = SceneArt != null && SceneArt.Available ? new TrainingStageView(SceneArt) : null;
            if (sceneStage != null) sceneStage.AllyKit = OwnKit;   // 장면 속 동료는 팀 유니폼 (내가 골키퍼여도)
            celebration = new GoalCelebration(font, rootRect, goalFrames, sceneStage);
        }

        // ---------- 매 프레임 ----------
        public void Tick(float deltaSeconds)
        {
            if (!Active) return;
            float dt = Mathf.Min(deltaSeconds, 0.1f);
            UpdateBanner(dt);
            if (scorePulse > 0f)   // 골이 나면 점수가 커지며 팀 색으로 번쩍
            {
                scorePulse -= dt;
                float k = Mathf.Clamp01(scorePulse / 0.8f);
                float s = 1f + 0.35f * k;
                scoreText.rectTransform.localScale = new Vector3(s, s, 1f);
                scoreText.color = Color.Lerp(PixelTheme.Text, scoreFlash, k);
            }

            if (celebration != null && celebration.Active)   // 골 세리머니 동안 경기는 멈춤
            {
                celebration.Tick(dt);
                return;
            }
            if (finishing)
            {
                finishT -= dt;
                if (finishT <= 0f) Close();
                return;
            }

            // 시계: 아직 끝나지 않은 장면이 있으면 90분에서 기다립니다.
            // 코너킥·페널티킥·골문 앞 프리킥은 절반 속도 (시계도 같이 느려져 장면 순서는 그대로)
            // 세트피스·재개·세리머니는 조금 느리게. 갑자기 바뀌지 않고 부드럽게 느려지고 빨라짐
            float paceTarget = engine.SetPieceFocus ? 0.55f : engine.RestartFocus ? 0.65f : engine.Celebrating ? 0.5f : 1f;
            paceNow = Mathf.MoveTowards(paceNow, paceTarget, dt * 1.6f);
            float pace = paceNow * (speed == 2 ? 0.64f : 0.32f);   // 2배속은 1배속의 정확히 두 배
            if (clock < 90f || !engine.Busy) clock = Mathf.Min(90f, clock + dt * MinutesPerSecond * pace);
            if (!engine.SecondHalf) clock = Mathf.Min(clock, 45f);   // 전반 추가 시간: 진영을 바꿀 때까지 45분에서 기다림
            if (clock >= 90f && engine.Busy)   // 혹시 장면이 끝나지 않아도 오래 기다리지 않음
            {
                waitT += dt;
                if (waitT > 10f) { Skip(); return; }
            }
            float sim = dt * SimSpeed * pace;
            while (sim > 0.0001f)
            {
                float step = Mathf.Min(sim, 0.05f);
                engine.Step(step, clock);
                sim -= step;
                if (celebration != null && celebration.Active) break;   // 골·도움·선방 장면이 시작되면 경기를 바로 멈춤
            }
            if (engine.CallId != lastCallId)
            {
                lastCallId = engine.CallId; callTimer = 1.6f;   // 판정은 실제 시간으로 1.6초
                if (engine.CallFoul) SoundFx.Play(Sfx.Whistle, 0.8f);   // 파울은 휘슬
            }
            callTimer = Mathf.Max(0f, callTimer - dt);
            Render();

            if (celebration != null && celebration.Active) return;   // 장면을 다 본 뒤에 경기 종료 (종료 휘슬 뒤에 선방 장면이 뜨지 않게)
            if (clock >= 90f && !engine.Busy)
            {
                finishing = true; finishT = 1.3f;
                // 끝까지 본 경기: 내 패스 기록은 영상에서 실제로 보낸 패스로 (결과 창·지난 경기와 같은 숫자)
                if (record.playerPlayed) { record.passesAttempted = livePassAtt; record.passesCompleted = livePassComp; }
                SnapStats();
                ShowEvent("경기 종료", ownName + "  " + goalsFor + " - " + goalsAgainst + "  " + oppName, NeutralColor, 1.3f);
                SoundFx.Play(Sfx.FullTime);
                Feed("90'", "경기 종료", NeutralColor);
            }
        }

        void Render()
        {
            for (int i = 0; i < 22; i++)
            {
                var d = engine.dots[i];
                PlaceMeters(dotRects[i], d.x, d.y, 0f, i == youSlot ? 42f : 30f);
            }
            for (int i = 0; i < 11; i++)   // 이름표는 선수 점 바로 아래
            {
                if (ownLabelRects[i] == null) continue;
                var d = engine.dots[i];
                PlacePx(ownLabelRects[i], MeterX(d.x), MeterY(d.y) + (i == youSlot ? 28f : 22f), 110f, 18f);   // 내 선수 점은 더 커서 조금 더 아래
            }
            PlaceMeters(ballRect, engine.ballX, engine.ballY, engine.ballZ, 20f);
            if (youSlot >= 0)
            {
                var d = engine.dots[youSlot];
                PlacePx(youLabel, MeterX(d.x), MeterY(d.y) - 29f, 52f, 20f);
            }
            if (offLineRect != null)
            {
                if (engine.OffsideShown != offShown) { offShown = engine.OffsideShown; offStart = Time.realtimeSinceStartup; }
                bool showOff = Time.realtimeSinceStartup - offStart < 2.4f;
                offLineRect.gameObject.SetActive(showOff); offMarkRect.gameObject.SetActive(showOff);
                if (showOff)
                {
                    float x0 = MeterX(0f), x1 = MeterX(68f);
                    PlacePx(offLineRect, (x0 + x1) / 2f, MeterY(engine.OffsideLineY), Mathf.Abs(x1 - x0), 3f);
                    PlacePx(offMarkRect, MeterX(engine.OffsideX), MeterY(engine.OffsideY), 14f, 14f);
                }
            }
            if (callTimer > 0f && !string.IsNullOrEmpty(engine.CallText))
            {
                callBox.gameObject.SetActive(true);
                callLabel.text = engine.CallText;
                int team = engine.CallTeam;
                callTeamLabel.text = team == 0 ? ownName + " 공" : team == 1 ? oppName + " 공" : "";
                callStripe.color = team == 0 ? OurGoalColor : team == 1 ? TheirGoalColor : NeutralColor;
                // 공 근처, 경기장 안쪽에 (가장자리 공이면 안쪽으로 당김)
                const float bw = 330f, bh = 78f;
                float cx = Mathf.Clamp(MeterX(engine.ballX), FieldLeft + bw / 2f + 6f, FieldRight - bw / 2f - 6f);
                float cy = Mathf.Clamp(MeterY(engine.ballY) - 80f, FieldTop + bh / 2f + 6f, FieldBottom - bh / 2f - 6f);
                PlacePx(callBox, cx, cy, bw, bh);
                float k = 1.6f - callTimer;                                   // 나타난 뒤 흐른 시간
                callGroup.alpha = Mathf.Clamp01(callTimer / 0.3f) * Mathf.Clamp01(k / 0.12f);
                float s = 1f + 0.18f * Mathf.Clamp01(1f - k / 0.15f);         // 처음에 살짝 크게
                callBox.localScale = new Vector3(s, s, 1f);
            }
            else callBox.gameObject.SetActive(false);
            UpdateStatsLine(Mathf.Clamp01(clock / 90f));
            clockText.text = Mathf.FloorToInt(Mathf.Min(clock, 90f)) + "'";
            clockFill.anchorMax = new Vector2(Mathf.Clamp01(clock / 90f), 1f);
        }

        // 미터 좌표를 코트 그림 위의 위치로 바꿉니다. z는 공이 뜬 높이입니다.
        void PlaceMeters(RectTransform rect, float mx, float my, float z, float sizePx)
        {
            PlacePx(rect, MeterX(mx), MeterY(my) - z * 10f, sizePx, sizePx);
        }

        // 경기장 좌표(가로 0~68m, 세로 0~105m)를 그림 픽셀로 바꿉니다. 중앙선을 기준으로 위아래를 따로 맞춥니다.
        // 후반에는 진영이 바뀌므로 경기장을 180도 돌려 그립니다.
        bool Flip { get { return engine != null && engine.SecondHalf; } }
        float MeterX(float mx) { if (Flip) mx = 68f - mx; return FieldLeft + mx / 68f * (FieldRight - FieldLeft); }
        float MeterY(float my)
        {
            if (Flip) my = 105f - my;
            return my <= 52.5f
                ? FieldTop + my / 52.5f * (FieldMid - FieldTop)
                : FieldMid + (my - 52.5f) / 52.5f * (FieldBottom - FieldMid);
        }

        void PlacePx(RectTransform rect, float cx, float cy, float w, float h)
        {
            rect.anchorMin = new Vector2((cx - w / 2f) / ImgW, 1f - (cy + h / 2f) / ImgH);
            rect.anchorMax = new Vector2((cx + w / 2f) / ImgW, 1f - (cy - h / 2f) / ImgH);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        // 내 선수의 기록. 패스는 최종 기록을 향해 경기 진행에 맞춰 늘어납니다.
        void UpdateStatsLine(float progress)
        {
            if (chipValues[0] == null) return;
            if (TeamMode)   // 나만의 팀: 슈팅 수, 유효 슈팅, 패스 성공/시도, 태클 성공/시도, 선방 횟수
            {
                string teamLine = teamShots + "|" + teamOnTarget + "|" + teamPassComp + "/" + teamPassAtt + "|" + teamTackleWon + "/" + teamTackleAtt + "|" + teamSaves;
                if (teamLine == lastStatsLine) return;
                lastStatsLine = teamLine;
                var tp = teamLine.Split('|');
                for (int i = 0; i < chipValues.Length && i < tp.Length; i++) if (chipValues[i] != null) chipValues[i].text = tp[i];
                return;
            }
            if (youSlot < 0) return;
            // 패스: 경기 영상에서 내 선수가 실제로 보낸 패스 (성공/시도). 시간에 따라 늘어나는 숫자가 아님
            int attempted = livePassAtt, completed = livePassComp;
            string line = youPosition == Position.Goalkeeper
                ? yourTackles + "|" + goalsAgainst + "|" + SeasonSystem.SaveRateText(yourTackles, goalsAgainst) + "|" + completed + "/" + attempted
                : yourGoals + "|" + yourAssists + "|" + yourShots + "|" + completed + "/" + attempted + "|" + yourTackles;
            if (line == lastStatsLine) return;
            lastStatsLine = line;
            var parts = line.Split('|');
            for (int i = 0; i < chipValues.Length && i < parts.Length; i++) if (chipValues[i] != null) chipValues[i].text = parts[i];
        }


        // 경기 끝에 내 선수의 기록을 저장된 결과와 똑같이 맞춥니다.
        void SnapStats()
        {
            if (!record.playerPlayed) return;
            yourGoals = record.playerGoals; yourAssists = record.playerAssists;
            yourShots = record.playerShots; yourTackles = record.defensiveActions;
            UpdateStatsLine(1f);
        }

        // ---------- 사건 ----------
        string Minute() { return Mathf.FloorToInt(Mathf.Min(clock, 90f)) + "'"; }

        // 경기 기록에 남은 득점자 이름 (없으면 null). 영상과 같은 계산으로 만든 기록이라 순서가 같습니다.
        GoalNote NextNote(bool ours)
        {
            if (record.goalNotes == null) return null;
            int want = ours ? ourNote++ : theirNote++, k = 0;
            foreach (var n in record.goalNotes)
                if (n.ours == ours) { if (k == want) return n; k++; }
            return null;
        }

        void OnGoal(ReplayEvent ev)
        {
            var note = NextNote(ev.type == ReplayEventType.GoalOurs);
            if (ev.type == ReplayEventType.GoalOurs) goalsFor++; else goalsAgainst++;
            bool assistScene = ev.type == ReplayEventType.GoalOurs && !ev.youScore && ev.youAssist && youSlot >= 0;
            if (!ev.youScore && !assistScene) SoundFx.Play(ev.type == ReplayEventType.GoalOurs ? Sfx.Goal : Sfx.Conceded);   // 내 골·도움은 장면 속 공이 들어갈 때
            scoreText.text = goalsFor + " - " + goalsAgainst;
            string how = ev.setPiece != SetPiece.None ? " [" + MatchReplayPlan.SetPieceName(ev.setPiece) + "]" : "";
            if (ev.type == ReplayEventType.GoalOurs)
            {
                scorePulse = 0.8f; scoreFlash = new Color(.55f, .75f, 1f);
                string who = note != null ? note.scorer + how + (string.IsNullOrEmpty(note.assist) ? "" : "  (도움 " + note.assist + ")") : ownName + how;
                if (ev.youScore)
                {
                    yourGoals++;
                    Feed(Minute(), "골! 내가 넣었다!" + how + (note != null && !string.IsNullOrEmpty(note.assist) ? "  (도움 " + note.assist + ")" : ""), OurGoalColor);
                    StartCelebration(ev, note);
                }
                else if (assistScene)
                {
                    if (countedAssist != ev) { countedAssist = ev; yourAssists++; }
                    Feed(Minute(), "골! " + who + "  ← 내 도움", OurGoalColor);
                    StartAssistCelebration(ev, note);
                }
                else
                {
                    Feed(Minute(), "골! " + who + (ev.youAssist ? "  ← 내 도움" : ""), OurGoalColor);
                    ShowEvent("골!", Minute() + "  " + who, OurGoalColor, 1.3f);
                }
            }
            else
            {
                scorePulse = 0.8f; scoreFlash = new Color(1f, .5f, .5f);
                string who = (note != null ? note.scorer : oppName) + how;
                Feed(Minute(), "실점  " + oppName + " " + who, TheirGoalColor);
                ShowEvent("실점", Minute() + "  " + oppName + "  " + who, TheirGoalColor, 1.2f);
            }
        }

        // 득점 장면의 페널티킥 선언 (코너킥·프리킥은 경기장 위 판정 글자만)
        void OnHalfTime()
        {
            SoundFx.Play(Sfx.Whistle);
            Feed("45'", "전반 종료 · 진영 교체", NeutralColor);
            ShowEvent("하프타임", "전반 " + goalsFor + " - " + goalsAgainst + "  ·  진영 교체", NeutralColor, 1.4f);
        }

        void OnSetPiece(ReplayEvent ev)
        {
            if (ev.setPiece != SetPiece.Penalty) return;
            bool ours = ev.type == ReplayEventType.GoalOurs;
            SoundFx.Play(Sfx.Whistle, 0.7f);
            ShowEvent("페널티킥!", Minute() + "  " + (ours ? ownName : oppName), ours ? OurGoalColor : TheirGoalColor, 1.0f);
        }

        // 내 패스가 나가는 순간: 도움을 세고 효과음만 (골이 들어가면 도움 장면이 나옴)
        void OnAssist(ReplayEvent ev)
        {
            if (countedAssist != ev) { countedAssist = ev; yourAssists++; }
            SoundFx.Play(Sfx.Assist, 0.8f);
        }

        void OnShot(ReplayEvent ev)
        {
            if (ev.type == ReplayEventType.SaveYou) { OnKeeperSave(ev); return; }
            Feed(Minute(), ev.saved ? "내 슈팅, 골키퍼 선방" : "내 슈팅, 골문을 벗어남", NeutralColor);
        }

        void OnShotTaken(ReplayEvent ev) { yourShots++; }

        void OnTackle(ReplayEvent ev)
        {
            yourTackles++;
            // 경기마다 두 번까지 내 수비 장면: 첫 수비, 그리고 60분 뒤 첫 수비. 나머지는 짧은 카드만
            bool scene = celebration != null && (defenseShown == 0 || (defenseShown == 1 && clock >= 60f));
            var kind = DefenseFor(youPosition, clock, yourTackles);
            Feed(Minute(), DefenseFeed(kind), TackleColor);
            if (scene)
            {
                defenseShown++;
                bannerT = 0f; eventCard.gameObject.SetActive(false);
                celebration.Play(new GoalInfo
                {
                    oppKit = OppKit > 0 ? OppKit : -1,
                minute = Minute(), scorer = playerName, isDefense = true, defense = kind, how = "", assist = "",
                    ownName = ownName, oppName = oppName, goalsFor = goalsFor, goalsAgainst = goalsAgainst, matchGoals = yourTackles
                });
                return;
            }
            SoundFx.Play(Sfx.Tackle, 0.8f);
            ShowEvent("태클!", Minute() + "  공을 빼앗았다", TackleColor, 0.9f);
        }

        // 골키퍼인 내 선방: 경기마다 두 번까지 선방 장면 (첫 선방, 그리고 페널티킥 선방은 언제나). 나머지는 짧은 카드만
        void OnKeeperSave(ReplayEvent ev)
        {
            yourTackles++;
            var kind = ev.setPiece == SetPiece.Penalty ? TrainingStageDrills.DefenseScene.PenaltySave
                     : ev.setPiece == SetPiece.Corner ? TrainingStageDrills.DefenseScene.CornerCatch
                     : (Mathf.Abs((int)(clock * 7.3f)) + yourTackles) % 3 == 0 ? TrainingStageDrills.DefenseScene.OneOnOne : TrainingStageDrills.DefenseScene.Save;
            string feed = kind == TrainingStageDrills.DefenseScene.PenaltySave ? "페널티킥 선방!" : kind == TrainingStageDrills.DefenseScene.CornerCatch ? "코너킥을 잡아냈다!"
                        : kind == TrainingStageDrills.DefenseScene.OneOnOne ? "1대1 선방!" : "선방! 몸을 날려 막았다";
            Feed(Minute(), feed, TackleColor);
            bool scene = celebration != null && (defenseShown == 0 || kind == TrainingStageDrills.DefenseScene.PenaltySave || (defenseShown == 1 && clock >= 60f));
            if (scene)
            {
                defenseShown++;
                bannerT = 0f; eventCard.gameObject.SetActive(false);
                celebration.Play(new GoalInfo
                {
                    oppKit = OppKit > 0 ? OppKit : -1, keeper = true,
                    minute = Minute(), scorer = playerName, isDefense = true, defense = kind, how = "", assist = "",
                    ownName = ownName, oppName = oppName, goalsFor = goalsFor, goalsAgainst = goalsAgainst, matchGoals = yourTackles
                });
                return;
            }
            SoundFx.Play(Sfx.Tackle, 0.8f);
            ShowEvent("선방!", Minute() + "  " + feed, TackleColor, 0.9f);
        }

        // 수비 장면 종류: 포지션마다 자주 나오는 수비가 다름 (수비수는 블록·걷어내기, 미드필더는 가로채기, 공격수는 전방 압박 태클)
        static TrainingStageDrills.DefenseScene DefenseFor(Position pos, float minute, int count)
        {
            int[] w = pos == Position.Defender ? new[] { 3, 3, 2, 2 } : pos == Position.Midfielder ? new[] { 4, 1, 0, 5 } : new[] { 6, 0, 0, 4 };
            int total = w[0] + w[1] + w[2] + w[3];
            int r = Mathf.Abs((int)(minute * 7.3f) * 31 + count * 17) % total;   // 같은 경기 영상이면 같은 장면
            for (int i = 0; i < 4; i++) { if (r < w[i]) return (TrainingStageDrills.DefenseScene)i; r -= w[i]; }
            return TrainingStageDrills.DefenseScene.Tackle;
        }

        static string DefenseFeed(TrainingStageDrills.DefenseScene k)
        {
            switch (k)
            {
                case TrainingStageDrills.DefenseScene.Block: return "슈팅 블록! 몸으로 막았다";
                case TrainingStageDrills.DefenseScene.Clearance: return "골라인 앞에서 걷어냈다!";
                case TrainingStageDrills.DefenseScene.Interception: return "패스를 가로챘다!";
                default: return "태클! 공을 빼앗았다";
            }
        }

        // 골 장면 종류: 세트피스면 그 장면, 헤딩이면 크로스 헤딩, 도움이 있으면 패스 받아 슈팅, 아니면 드리블 돌파
        static TrainingStageDrills.GoalScene SceneFor(ReplayEvent ev)
        {
            if (ev.setPiece == SetPiece.Penalty) return TrainingStageDrills.GoalScene.Penalty;
            if (ev.setPiece == SetPiece.FreeKick) return TrainingStageDrills.GoalScene.FreeKick;
            if (ev.setPiece == SetPiece.Corner) return TrainingStageDrills.GoalScene.Corner;
            if (ev.header) return TrainingStageDrills.GoalScene.Cross;
            if (ev.longShot) return TrainingStageDrills.GoalScene.LongShot;
            return ev.assisterSlot >= 0 ? TrainingStageDrills.GoalScene.Pass : TrainingStageDrills.GoalScene.Dribble;
        }

        // 내 도움 장면 종류: 코너킥, 크로스(동료 헤딩), 슈팅 자리로 내준 패스(동료 중거리), 그 밖에는 스루 패스
        static TrainingStageDrills.GoalScene AssistSceneFor(ReplayEvent ev)
        {
            if (ev.assisterSlot == 0)   // 골키퍼인 내 도움: 롱킥 또는 빠른 배급
                return ((int)ev.minute) % 2 == 0 ? TrainingStageDrills.GoalScene.KeeperKick : TrainingStageDrills.GoalScene.KeeperRoll;
            if (ev.setPiece == SetPiece.Corner) return TrainingStageDrills.GoalScene.Corner;
            if (ev.header) return TrainingStageDrills.GoalScene.Cross;
            if (ev.longShot) return TrainingStageDrills.GoalScene.LongShot;
            return TrainingStageDrills.GoalScene.Pass;
        }

        void StartAssistCelebration(ReplayEvent ev, GoalNote note)
        {
            bannerT = 0f; eventCard.gameObject.SetActive(false);
            celebration.Play(new GoalInfo
            {
                oppKit = OppKit > 0 ? OppKit : -1,
                minute = Minute(), scorer = playerName, isAssist = true, partner = note != null ? note.scorer : "",
                scene = AssistSceneFor(ev), how = "", assist = "",
                ownName = ownName, oppName = oppName, goalsFor = goalsFor, goalsAgainst = goalsAgainst,
                matchGoals = yourAssists, seasonGoal = SeasonAssistsBefore >= 0 ? SeasonAssistsBefore + yourAssists : 0
            });
        }

        void StartCelebration(ReplayEvent ev, GoalNote note)
        {
            bannerT = 0f; eventCard.gameObject.SetActive(false);
            celebration.Play(new GoalInfo
            {
                oppKit = OppKit > 0 ? OppKit : -1,
                minute = Minute(), scorer = playerName,
                how = ((ev.setPiece != SetPiece.None ? MatchReplayPlan.SetPieceName(ev.setPiece) : "") + (ev.header ? " 헤딩" : "") + (ev.longShot ? " 중거리" : "")).Trim(),
                scene = SceneFor(ev),
                assist = note != null ? note.assist : "",
                ownName = ownName, oppName = oppName, goalsFor = goalsFor, goalsAgainst = goalsAgainst,
                matchGoals = yourGoals, seasonGoal = SeasonGoalsBefore >= 0 ? SeasonGoalsBefore + yourGoals : 0
            });
        }

        // 사건 카드: 팀 색 띠에 큰 제목과 한 줄 설명. 위에서 톡 떨어지듯 나타났다가 흐려지며 사라집니다.
        void ShowEvent(string title, string sub, Color color, float seconds)
        {
            eventTitle.text = title; eventSub.text = sub;
            eventEdge.color = Color.Lerp(color, Color.white, 0.35f);
            eventFill.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.92f);
            bannerDur = seconds; bannerT = seconds;
            eventCard.gameObject.SetActive(true);
            UpdateBanner(0f);
        }

        void UpdateBanner(float dt)
        {
            if (bannerT <= 0f || eventCard == null) return;
            bannerT -= dt;
            if (bannerT <= 0f) { eventCard.gameObject.SetActive(false); return; }
            float elapsed = bannerDur - bannerT;
            float k = Mathf.Clamp01(elapsed / 0.2f);
            float s = 1f + 0.25f * (1f - k);                                  // 처음엔 크게
            eventCard.localScale = new Vector3(s, s, 1f);
            eventCard.anchoredPosition = new Vector2(0f, 30f * (1f - k));     // 위에서 내려옴
            float a = Mathf.Clamp01(bannerT / 0.3f);                          // 끝에 흐려짐
            SetAlpha(eventEdge, a); SetAlpha(eventFill, a * 0.92f); SetAlpha(eventTitle, a); SetAlpha(eventSub, a);
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        // 진행 기록에 한 줄 추가 (분 배지 색 = 사건 종류)
        void Feed(string minute, string text, Color color)
        {
            feedItems.Add((minute, text, color));
            while (feedItems.Count > FeedRows) feedItems.RemoveAt(0);
            for (int i = 0; i < FeedRows; i++)
            {
                int item = i - (FeedRows - feedItems.Count);   // 맨 아래 줄이 최신
                var row = feedLines[i].transform.parent.gameObject;
                if (item < 0) { row.SetActive(false); continue; }
                row.SetActive(true);
                var f = feedItems[item];
                float fade = i == FeedRows - 1 ? 1f : (i == FeedRows - 2 ? 0.75f : 0.5f);
                feedBadges[i].color = new Color(f.color.r, f.color.g, f.color.b, fade);
                feedMinutes[i].text = f.minute;
                feedLines[i].text = f.text;
                feedLines[i].color = new Color(PixelTheme.Text.r, PixelTheme.Text.g, PixelTheme.Text.b, fade);
            }
        }

        // ---------- 배속 ----------
        void SetSpeed(int value)
        {
            if (speed == value) return;
            speed = value;
            try { PlayerPrefs.SetInt(SpeedPref, value); PlayerPrefs.Save(); } catch { }   // 이 기기에만 기억
            SoundFx.Play(Sfx.Click);
            RefreshSpeed();
        }

        // 고른 배속은 금색으로 채움
        void RefreshSpeed()
        {
            for (int i = 0; i < 2; i++)
            {
                if (speedFills[i] == null) continue;
                bool on = speed == i + 1;
                speedEdges[i].color = on ? PixelTheme.Accent : PixelTheme.QuietEdge;
                speedFills[i].color = on ? new Color(PixelTheme.Accent.r * .45f, PixelTheme.Accent.g * .45f, PixelTheme.Accent.b * .45f, 1f) : PixelTheme.QuietFill;
                speedTexts[i].color = on ? PixelTheme.Accent : PixelTheme.TextDim;
            }
        }

        // ---------- 끝내기 ----------
        void Skip()
        {
            if (!Active) return;
            FastForward();   // 남은 경기를 화면 없이 끝까지 돌려 기록을 셈 (경기 결과의 팀 기록·MVP)
            goalsFor = record.goalsFor; goalsAgainst = record.goalsAgainst;
            SnapStats();
            Close();
        }

        // 건너뛴 경기: 남은 시간을 화면·소리 없이 빠르게 진행 (결과는 이미 정해져 있고 장면 순서도 같음)
        void FastForward()
        {
            if (engine == null) return;
            engine.GoalScored = engine.AssistMade = engine.ShotResolved = engine.TackleWon = engine.ShotTaken = null;
            engine.SetPieceCalled = null; engine.HalfTime = null; engine.YouPassed = null;
            engine.TeamPassed = engine.TeamTackled = engine.TeamShot = null; engine.KeeperSaved = null;
            float c = clock;
            for (int n = 0; n < 8000 && (c < 90f || engine.Busy); n++)
            {
                engine.Step(0.05f, c);
                c = Mathf.Min(90f, c + 0.05f * MinutesPerSecond / SimSpeed);
                if (!engine.SecondHalf) c = Mathf.Min(c, 45f);
            }
        }

        // 경기 영상에서 센 기록으로 우리 팀 기록과 MVP(두 팀에서 평점이 가장 높은 선수)를 만듭니다.
        MatchBox BuildBox()
        {
            if (engine == null || record == null) return null;
            var e = engine;
            int shots = 0, on = 0, pa = 0, pc = 0, ta = 0, tw = 0;
            for (int i = 0; i < 11; i++) { shots += e.PShotsOn[i] + e.PShotsOff[i]; on += e.PShotsOn[i]; pa += e.PPassAtt[i]; pc += e.PPassComp[i]; ta += e.PTackleAtt[i]; tw += e.PTackleWon[i]; }
            var box = new MatchBox
            {
                statNames = new[] { "슈팅", "유효슈팅", "패스", "태클", "선방" },
                statValues = new[] { shots.ToString(), on.ToString(), pc + "/" + pa, tw + "/" + ta, e.PSaves[0].ToString() }
            };
            int best = -1; float bestR = -1f;
            for (int i = 0; i < 22; i++)
            {
                int team = i < 11 ? 0 : 1, s = i % 11;
                var names = team == 0 ? OwnNames : OppNames;
                bool me = team == 0 && s == youSlot && youSlot >= 0;
                if (!me && (names == null || s >= names.Length || string.IsNullOrEmpty(names[s]))) continue;
                float r = me && record.rating > 0f ? record.rating : PlayerRating(i);
                if (r > bestR || (r == bestR && best >= 0 && e.PGoals[i] > e.PGoals[best])) { bestR = r; best = i; }
            }
            if (best >= 0)
            {
                int team = best < 11 ? 0 : 1, s = best % 11;
                bool me = team == 0 && s == youSlot && youSlot >= 0;
                box.mvpName = me ? playerName : (team == 0 ? OwnNames : OppNames)[s];
                box.mvpOurs = team == 0; box.mvpTeam = team == 0 ? ownName : oppName; box.mvpRating = bestR;
                var parts = new List<string>();
                if (e.PGoals[best] > 0) parts.Add(e.PGoals[best] + "골");
                if (e.PAssists[best] > 0) parts.Add(e.PAssists[best] + "도움");
                if (e.PSaves[best] > 0) parts.Add("선방 " + e.PSaves[best]);
                if (e.PTackleWon[best] > 0) parts.Add("태클 " + e.PTackleWon[best]);
                if (parts.Count == 0 && e.PPassAtt[best] > 0) parts.Add("패스 " + e.PPassComp[best] + "/" + e.PPassAtt[best]);
                box.mvpLine = string.Join(" · ", parts);
            }
            return box;
        }

        // 경기 영상 속 기록으로 매긴 평점 (6.0 기준, 3~10)
        float PlayerRating(int i)
        {
            var e = engine; var d = e.dots[i];
            int team = i < 11 ? 0 : 1;
            int gf = team == 0 ? record.goalsFor : record.goalsAgainst, ga = team == 0 ? record.goalsAgainst : record.goalsFor;
            float r = 6f + e.PGoals[i] * 1.0f + e.PAssists[i] * 0.7f + e.PShotsOn[i] * 0.2f - e.PShotsOff[i] * 0.05f
                    + e.PPassComp[i] * 0.04f - (e.PPassAtt[i] - e.PPassComp[i]) * 0.08f
                    + e.PTackleWon[i] * 0.2f - (e.PTackleAtt[i] - e.PTackleWon[i]) * 0.05f + e.PSaves[i] * 0.4f;
            r += gf > ga ? 0.4f : gf < ga ? -0.3f : 0f;
            bool back = d.slot == 0 || d.def.role == SquadRole.DF;
            if (back) r += ga == 0 ? 0.5f : -(d.slot == 0 ? 0.25f : 0.15f) * Mathf.Max(0, ga - 1);
            return Mathf.Round(Mathf.Clamp(r, 3f, 10f) * 10f) / 10f;
        }

        void Close()
        {
            LastBox = BuildBox();
            Active = false;
            if (celebration != null) { celebration.Dispose(); celebration = null; }
            foreach (var obj in kitObjects) if (obj != null) UnityEngine.Object.Destroy(obj);
            kitObjects.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
            var callback = onFinished;
            onFinished = null;
            if (callback != null) callback();
        }
    }
}
