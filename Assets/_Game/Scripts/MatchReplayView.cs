using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 경기 영상 화면. 90분을 약 15초에 보여 주며, 위에서 본 전체 코트에 점으로 선수와 공이 움직입니다.
    // 내 선수가 골을 넣으면 골망을 가르는 장면과 GOAL!, 도움을 하면 ASSIST!가 나옵니다.
    //   그림: Resources/Art/Match/ (pitch, dot_blue, dot_red, dot_you, ball, goal_0 ~ goal_7)
    public class MatchReplayView
    {
        const float MinutesPerSecond = 6f;    // 90분 / 15초
        const float SimSpeed = 8f;            // 실제 1초 동안 진행하는 경기 속 시간(초)
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

        GameObject root, closeupRoot;
        RectTransform pitchRect, closeupPanel, youLabel, clockFill;
        readonly RectTransform[] dotRects = new RectTransform[22];
        RectTransform ballRect;
        Image closeupImage;
        Text scoreText, clockText, closeupText, benchText;
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

        float clock, bannerT, bannerDur, closeupT, closeupElapsed, finishT;
        int goalsFor, goalsAgainst, youSlot;
        int ourNote, theirNote;   // 다음에 보여 줄 골 기록 (GoalNote) 순서
        int yourGoals, yourAssists, yourShots, yourTackles;
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
        public bool Show(RectTransform parent, MatchRecord m, PlayerData player, string ownTeamName, float attributeCap, Action finished)
        {
            if (Active) return true;
            if (!Available) return false;
            record = m; onFinished = finished; ownName = ownTeamName; oppName = m.opponentName;
            youSlot = m.playerPlayed ? MatchReplayPlan.YouSlot(player.position) : -1;
            var rng = new System.Random(m.seed ^ 0x2f6e2b1);
            // 내 선수의 능력치가 영상 속 움직임에 반영됩니다. (속도, 후반 피로, 패스 정확도, 슛 속도와 유효 슈팅, 공 지키기)
            var stats = ReplayStats.From(player, m, attributeCap);
            var plan = MatchReplayPlan.Build(m, m.playerPlayed, MatchReplayPlan.YouSlot(player.position), rng, stats.shootN, m.defensiveActions);
            engine = new MatchReplayEngine(plan, youSlot, rng);
            engine.stats = stats;
            engine.GoalScored = OnGoal; engine.AssistMade = OnAssist; engine.ShotResolved = OnShot;
            engine.TackleWon = OnTackle; engine.ShotTaken = OnShotTaken;
            clock = 0f; goalsFor = 0; goalsAgainst = 0; bannerT = 0f; closeupT = 0f; finishing = false; ourNote = 0; theirNote = 0;
            yourGoals = 0; yourAssists = 0; yourShots = 0; yourTackles = 0; waitT = 0f; lastStatsLine = "";
            feedItems.Clear(); scorePulse = 0f;
            BuildUi(parent);
            Feed("0'", m.playerPlayed ? "킥오프" : "킥오프 (벤치 대기)", NeutralColor);
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
            Anchored(barBack, .22f, .12f, .97f, .32f);
            barBack.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            clockFill = NewRect("ClockFill", barBack);
            Anchored(clockFill, 0, 0, 0, 1);
            clockFill.gameObject.AddComponent<Image>().color = PixelTheme.Bar;

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

            for (int i = 0; i < 22; i++)
            {
                var r = NewRect(i < 11 ? "Ours" + i : "Theirs" + (i - 11), pitchRect);
                var img = r.gameObject.AddComponent<Image>();
                img.sprite = i == youSlot ? youSprite : ((i % 11) == 0 ? greenSprite : (i < 11 ? blueSprite : redSprite));
                img.raycastTarget = false;
                dotRects[i] = r;
            }
            ballRect = NewRect("Ball", pitchRect);
            var ballImage = ballRect.gameObject.AddComponent<Image>();
            ballImage.sprite = ballSprite; ballImage.raycastTarget = false;
            youLabel = NewRect("YouLabel", pitchRect);
            var youText = TextNode("나", youLabel, 18, TextAnchor.MiddleCenter, PixelTheme.Accent);
            Anchored(youText.rectTransform, 0, 0, 1, 1);
            youLabel.gameObject.SetActive(youSlot >= 0);

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
            string[] chipNames = { "골", "도움", "슈팅", "패스", "태클" };
            for (int i = 0; i < chipNames.Length; i++)
            {
                var chip = NewRect("Chip", chipRow);
                chip.gameObject.AddComponent<Image>().color = new Color(.12f, .16f, .25f, .95f);
                var label = TextNode(chipNames[i], chip, 16, TextAnchor.MiddleLeft, PixelTheme.TextDim);
                Anchored(label.rectTransform, .07f, 0, .45f, 1);
                chipValues[i] = Fit(TextNode("0", chip, 22, TextAnchor.MiddleRight, PixelTheme.Accent), 14);
                Anchored(chipValues[i].rectTransform, .45f, .05f, .94f, .95f);
            }
            benchText = TextNode("벤치 대기 중", rootRect, 20, TextAnchor.MiddleCenter, PixelTheme.TextDim);
            benchText.rectTransform.anchorMin = new Vector2(0, 0); benchText.rectTransform.anchorMax = new Vector2(1, 0); benchText.rectTransform.pivot = new Vector2(0, 0);
            benchText.rectTransform.offsetMin = new Vector2(16, 102); benchText.rectTransform.offsetMax = new Vector2(-16, 140);
            chipRow.gameObject.SetActive(youSlot >= 0); benchText.gameObject.SetActive(youSlot < 0);
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

            // 골망 장면 (평소에는 숨김)
            var closeup = NewRect("Closeup", rootRect);
            Anchored(closeup, 0, 0, 1, 1);
            closeup.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .65f);
            closeupRoot = closeup.gameObject;
            closeupPanel = Box(closeup, PixelTheme.Accent, PixelTheme.Background);
            closeupPanel.anchorMin = closeupPanel.anchorMax = new Vector2(.5f, .5f);
            closeupPanel.pivot = new Vector2(.5f, .5f);
            closeupPanel.sizeDelta = new Vector2(640, 640f * 65f / 97f + 16f);
            var goalLayer = NewRect("GoalImage", closeupPanel.GetChild(0));
            Anchored(goalLayer, 0, 0, 1, 1);
            closeupImage = goalLayer.gameObject.AddComponent<Image>();
            closeupImage.sprite = goalFrames[0]; closeupImage.raycastTarget = false;
            closeupText = TextNode("골!", closeup, 96, TextAnchor.MiddleCenter, PixelTheme.Accent);
            closeupText.rectTransform.anchorMin = closeupText.rectTransform.anchorMax = new Vector2(.5f, .5f);
            closeupText.rectTransform.sizeDelta = new Vector2(700, 140);
            closeupText.rectTransform.anchoredPosition = new Vector2(0, 330);
            var closeupOutline = closeupText.gameObject.AddComponent<Outline>();
            closeupOutline.effectColor = new Color(0, 0, 0, .9f); closeupOutline.effectDistance = new Vector2(5, -5);
            closeupRoot.SetActive(false);
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

            if (closeupT > 0f)   // 골 장면 동안 경기는 멈춤
            {
                closeupT -= dt; closeupElapsed += dt;
                int frame = Mathf.Min(goalFrames.Length - 1, (int)(closeupElapsed * 8f));
                closeupImage.sprite = goalFrames[frame];
                float pulse = 1f + 0.08f * Mathf.Sin(closeupElapsed * 14f);
                closeupText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                if (closeupT <= 0f) closeupRoot.SetActive(false);
                return;
            }
            if (finishing)
            {
                finishT -= dt;
                if (finishT <= 0f) Close();
                return;
            }

            // 시계: 아직 끝나지 않은 장면이 있으면 90분에서 기다립니다.
            if (clock < 90f || !engine.Busy) clock = Mathf.Min(90f, clock + dt * MinutesPerSecond);
            if (clock >= 90f && engine.Busy)   // 혹시 장면이 끝나지 않아도 오래 기다리지 않음
            {
                waitT += dt;
                if (waitT > 10f) { Skip(); return; }
            }
            float sim = dt * SimSpeed;
            while (sim > 0.0001f)
            {
                float step = Mathf.Min(sim, 0.05f);
                engine.Step(step, clock);
                sim -= step;
            }
            Render();

            if (clock >= 90f && !engine.Busy)
            {
                finishing = true; finishT = 1.3f;
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
            PlaceMeters(ballRect, engine.ballX, engine.ballY, engine.ballZ, 20f);
            if (youSlot >= 0)
            {
                var d = engine.dots[youSlot];
                PlacePx(youLabel, MeterX(d.x), MeterY(d.y) - 29f, 52f, 20f);
            }
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
        static float MeterX(float mx) { return FieldLeft + mx / 68f * (FieldRight - FieldLeft); }
        static float MeterY(float my)
        {
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
            if (youSlot < 0 || chipValues[0] == null) return;
            int attempted = Mathf.RoundToInt(record.passesAttempted * progress);
            int completed = Mathf.Min(attempted, Mathf.RoundToInt(record.passesCompleted * progress));
            string line = yourGoals + "|" + yourAssists + "|" + yourShots + "|" + completed + "/" + attempted + "|" + yourTackles;
            if (line == lastStatsLine) return;
            lastStatsLine = line;
            var parts = line.Split('|');
            for (int i = 0; i < chipValues.Length; i++) chipValues[i].text = parts[i];
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
            SoundFx.Play(ev.type == ReplayEventType.GoalOurs ? Sfx.Goal : Sfx.Conceded);
            scoreText.text = goalsFor + " - " + goalsAgainst;
            if (ev.type == ReplayEventType.GoalOurs)
            {
                scorePulse = 0.8f; scoreFlash = new Color(.55f, .75f, 1f);
                string who = note != null ? note.scorer + (string.IsNullOrEmpty(note.assist) ? "" : "  (도움 " + note.assist + ")") : ownName;
                if (ev.youScore)
                {
                    yourGoals++;
                    Feed(Minute(), "골! 내가 넣었다!" + (note != null && !string.IsNullOrEmpty(note.assist) ? "  (도움 " + note.assist + ")" : ""), OurGoalColor);
                    StartCloseup();
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
                string who = note != null ? note.scorer : oppName;
                Feed(Minute(), "실점  " + oppName + " " + who, TheirGoalColor);
                ShowEvent("실점", Minute() + "  " + oppName + "  " + who, TheirGoalColor, 1.2f);
            }
        }

        void OnAssist(ReplayEvent ev)
        {
            yourAssists++;
            SoundFx.Play(Sfx.Assist, 0.8f);
            ShowEvent("도움!", Minute() + "  내 패스가 골로 이어졌다", AssistColor, 1.4f);
        }

        void OnShot(ReplayEvent ev)
        {
            Feed(Minute(), ev.saved ? "내 슈팅, 골키퍼 선방" : "내 슈팅, 골문을 벗어남", NeutralColor);
        }

        void OnShotTaken(ReplayEvent ev) { yourShots++; }

        void OnTackle(ReplayEvent ev)
        {
            yourTackles++;
            Feed(Minute(), "태클! 공을 빼앗았다", TackleColor);
            SoundFx.Play(Sfx.Tackle, 0.8f);
            ShowEvent("태클!", Minute() + "  공을 빼앗았다", TackleColor, 0.9f);
        }

        void StartCloseup()
        {
            closeupT = 2.0f; closeupElapsed = 0f;
            closeupImage.sprite = goalFrames[0];
            closeupRoot.SetActive(true);
            bannerT = 0f; eventCard.gameObject.SetActive(false);
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

        // ---------- 끝내기 ----------
        void Skip()
        {
            if (!Active) return;
            goalsFor = record.goalsFor; goalsAgainst = record.goalsAgainst;
            SnapStats();
            Close();
        }

        void Close()
        {
            Active = false;
            if (root != null) UnityEngine.Object.Destroy(root);
            var callback = onFinished;
            onFinished = null;
            if (callback != null) callback();
        }
    }
}
