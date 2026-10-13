using System;
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
        const float PxW = 114f, PxH = 170f, PxOff = 6f, PxPerMeter = 1.5f;   // pitch.png 기준

        readonly Font font;
        Sprite pitchSprite, blueSprite, redSprite, youSprite, ballSprite;
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
        Text scoreText, clockText, bannerText, feedText, closeupText, statsText;
        readonly string[] feed = new string[2];

        float clock, bannerT, bannerDur, closeupT, closeupElapsed, finishT;
        int goalsFor, goalsAgainst, youSlot;
        int yourGoals, yourAssists, yourShots, yourTackles;
        float waitT;
        string lastStatsLine = "";
        bool finishing;

        public MatchReplayView(Font font)
        {
            this.font = font;
            pitchSprite = Load("pitch"); blueSprite = Load("dot_blue"); redSprite = Load("dot_red");
            youSprite = Load("dot_you"); ballSprite = Load("ball");
            for (int i = 0; i < goalFrames.Length; i++) goalFrames[i] = Load("goal_" + i);
        }

        public bool Available
        {
            get
            {
                if (pitchSprite == null || blueSprite == null || redSprite == null || youSprite == null || ballSprite == null) return false;
                foreach (var f in goalFrames) if (f == null) return false;
                return true;
            }
        }

        static Sprite Load(string name)
        {
            var tex = Resources.Load<Texture2D>("Art/Match/" + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
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
            clock = 0f; goalsFor = 0; goalsAgainst = 0; bannerT = 0f; closeupT = 0f; finishing = false;
            yourGoals = 0; yourAssists = 0; yourShots = 0; yourTackles = 0; waitT = 0f; lastStatsLine = "";
            feed[0] = ""; feed[1] = "";
            BuildUi(parent);
            Feed(m.playerPlayed ? "0'  Kick-off" : "0'  Kick-off (you are on the bench)");
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
            var hud = Box(rootRect, PixelTheme.PanelEdge, PixelTheme.Panel);
            hud.anchorMin = new Vector2(0, 1); hud.anchorMax = new Vector2(1, 1); hud.pivot = new Vector2(.5f, 1);
            hud.offsetMin = new Vector2(16, -136); hud.offsetMax = new Vector2(-16, -12);
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

            // 가운데: 코트
            var area = NewRect("PitchArea", rootRect);
            Anchored(area, 0, 0, 1, 1);
            area.offsetMin = new Vector2(12, 146); area.offsetMax = new Vector2(-12, -148);
            pitchRect = NewRect("Pitch", area);
            pitchRect.anchorMin = pitchRect.anchorMax = new Vector2(.5f, .5f);
            pitchRect.pivot = new Vector2(.5f, .5f);
            pitchRect.sizeDelta = new Vector2(100, 100);
            var fitter = pitchRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = PxW / PxH;
            var pitchImage = pitchRect.gameObject.AddComponent<Image>();
            pitchImage.sprite = pitchSprite; pitchImage.raycastTarget = false;

            for (int i = 0; i < 22; i++)
            {
                var r = NewRect(i < 11 ? "Ours" + i : "Theirs" + (i - 11), pitchRect);
                var img = r.gameObject.AddComponent<Image>();
                img.sprite = i == youSlot ? youSprite : (i < 11 ? blueSprite : redSprite);
                img.raycastTarget = false;
                dotRects[i] = r;
            }
            ballRect = NewRect("Ball", pitchRect);
            var ballImage = ballRect.gameObject.AddComponent<Image>();
            ballImage.sprite = ballSprite; ballImage.raycastTarget = false;
            youLabel = NewRect("YouLabel", pitchRect);
            var youText = TextNode("YOU", youLabel, 18, TextAnchor.MiddleCenter, PixelTheme.Accent);
            Anchored(youText.rectTransform, 0, 0, 1, 1);
            youLabel.gameObject.SetActive(youSlot >= 0);

            // 큰 글자 알림 (ASSIST!, GOAL! 등)
            bannerText = TextNode("", area, 64, TextAnchor.MiddleCenter, Color.white);
            Anchored(bannerText.rectTransform, 0, .60f, 1, .80f);
            var outline = bannerText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .9f); outline.effectDistance = new Vector2(4, -4);
            bannerText.gameObject.SetActive(false);

            // 아래쪽: 내 선수 기록, 진행 기록, 건너뛰기
            statsText = TextNode("", rootRect, 20, TextAnchor.MiddleLeft, PixelTheme.Accent);
            statsText.rectTransform.anchorMin = new Vector2(0, 0); statsText.rectTransform.anchorMax = new Vector2(1, 0);
            statsText.rectTransform.pivot = new Vector2(0, 0);
            statsText.rectTransform.offsetMin = new Vector2(24, 104); statsText.rectTransform.offsetMax = new Vector2(-24, 138);
            feedText = TextNode("", rootRect, 22, TextAnchor.MiddleLeft, PixelTheme.Text);
            feedText.rectTransform.anchorMin = new Vector2(0, 0); feedText.rectTransform.anchorMax = new Vector2(1, 0);
            feedText.rectTransform.pivot = new Vector2(0, 0);
            feedText.rectTransform.offsetMin = new Vector2(24, 16); feedText.rectTransform.offsetMax = new Vector2(-190, 96);
            var skip = Box(rootRect, PixelTheme.QuietEdge, PixelTheme.QuietFill);
            skip.anchorMin = skip.anchorMax = new Vector2(1, 0); skip.pivot = new Vector2(1, 0);
            skip.anchoredPosition = new Vector2(-16, 16); skip.sizeDelta = new Vector2(160, 76);
            var skipButton = skip.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skip.GetChild(0).GetComponent<Image>();
            skipButton.onClick.AddListener(Skip);
            var skipText = TextNode("SKIP", skip.GetChild(0), 26, TextAnchor.MiddleCenter, PixelTheme.Text);
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
            closeupText = TextNode("GOAL!", closeup, 96, TextAnchor.MiddleCenter, PixelTheme.Accent);
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
                ShowBanner("FULL TIME", PixelTheme.Text, 1.3f, 56);
                Feed("90'  Full time");
            }
        }

        void Render()
        {
            for (int i = 0; i < 22; i++)
            {
                var d = engine.dots[i];
                PlaceMeters(dotRects[i], d.x, d.y, 0f, i == youSlot ? 9f : 5f);
            }
            PlaceMeters(ballRect, engine.ballX, engine.ballY, engine.ballZ, 3f);
            if (youSlot >= 0)
            {
                var d = engine.dots[youSlot];
                float cx = PxOff + d.x * PxPerMeter, cy = PxOff + d.y * PxPerMeter - 9f;
                PlacePx(youLabel, cx, cy, 22f, 8f);
            }
            UpdateStatsLine(Mathf.Clamp01(clock / 90f));
            clockText.text = Mathf.FloorToInt(Mathf.Min(clock, 90f)) + "'";
            clockFill.anchorMax = new Vector2(Mathf.Clamp01(clock / 90f), 1f);
        }

        // 미터 좌표를 코트 그림 위의 위치로 바꿉니다. z는 공이 뜬 높이입니다.
        void PlaceMeters(RectTransform rect, float mx, float my, float z, float sizePx)
        {
            PlacePx(rect, PxOff + mx * PxPerMeter, PxOff + my * PxPerMeter - z * 1.2f, sizePx, sizePx);
        }

        void PlacePx(RectTransform rect, float cx, float cy, float w, float h)
        {
            rect.anchorMin = new Vector2((cx - w / 2f) / PxW, 1f - (cy + h / 2f) / PxH);
            rect.anchorMax = new Vector2((cx + w / 2f) / PxW, 1f - (cy - h / 2f) / PxH);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        // 내 선수의 기록. 패스는 최종 기록을 향해 경기 진행에 맞춰 늘어납니다.
        void UpdateStatsLine(float progress)
        {
            string line;
            if (youSlot < 0) line = "You are on the bench";
            else
            {
                int attempted = Mathf.RoundToInt(record.passesAttempted * progress);
                int completed = Mathf.Min(attempted, Mathf.RoundToInt(record.passesCompleted * progress));
                line = "GOAL " + yourGoals + "   ASSIST " + yourAssists + "   SHOT " + yourShots
                     + "   PASS " + completed + "/" + attempted + "   TACKLE " + yourTackles;
            }
            if (line == lastStatsLine) return;
            lastStatsLine = line;
            statsText.text = line;
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

        void OnGoal(ReplayEvent ev)
        {
            if (ev.type == ReplayEventType.GoalOurs) goalsFor++; else goalsAgainst++;
            scoreText.text = goalsFor + " - " + goalsAgainst;
            if (ev.type == ReplayEventType.GoalOurs)
            {
                if (ev.youScore)
                {
                    yourGoals++;
                    Feed(Minute() + "  GOAL! You scored!");
                    StartCloseup();
                }
                else
                {
                    Feed(Minute() + (ev.youAssist ? "  Goal! Your assist paid off." : "  Goal for " + ownName + "."));
                    ShowBanner("GOAL!", new Color(.55f, .75f, 1f), 1.0f, 56);
                }
            }
            else
            {
                Feed(Minute() + "  " + oppName + " scores.");
                ShowBanner("CONCEDED", new Color(1f, .5f, .5f), 1.0f, 52);
            }
        }

        void OnAssist(ReplayEvent ev)
        {
            yourAssists++;
            Feed(Minute() + "  ASSIST! You set up the goal.");
            ShowBanner("ASSIST!", PixelTheme.Accent, 1.3f, 72);
        }

        void OnShot(ReplayEvent ev)
        {
            Feed(Minute() + (ev.saved ? "  Your shot is saved." : "  Your shot goes wide."));
        }

        void OnShotTaken(ReplayEvent ev) { yourShots++; }

        void OnTackle(ReplayEvent ev)
        {
            yourTackles++;
            Feed(Minute() + "  Tackle! You won the ball.");
            ShowBanner("TACKLE!", new Color(.6f, 1f, .7f), 0.8f, 44);
        }

        void StartCloseup()
        {
            closeupT = 2.0f; closeupElapsed = 0f;
            closeupImage.sprite = goalFrames[0];
            closeupRoot.SetActive(true);
            bannerT = 0f; bannerText.gameObject.SetActive(false);
        }

        void ShowBanner(string text, Color color, float seconds, int size)
        {
            bannerText.text = text; bannerText.color = color; bannerText.fontSize = size;
            bannerDur = seconds; bannerT = seconds;
            bannerText.gameObject.SetActive(true);
        }

        void UpdateBanner(float dt)
        {
            if (bannerT <= 0f) return;
            bannerT -= dt;
            if (bannerT <= 0f) { bannerText.gameObject.SetActive(false); return; }
            float elapsed = bannerDur - bannerT;
            float punch = 1f + 0.5f * Mathf.Clamp01(1f - elapsed / 0.25f);   // 처음에 크게 나타났다가 줄어듦
            bannerText.rectTransform.localScale = new Vector3(punch, punch, 1f);
            var c = bannerText.color; c.a = Mathf.Clamp01(bannerT / 0.3f);
            bannerText.color = c;
        }

        void Feed(string line)
        {
            feed[0] = feed[1]; feed[1] = line;
            feedText.text = (string.IsNullOrEmpty(feed[0]) ? "" : feed[0] + "\n") + feed[1];
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
