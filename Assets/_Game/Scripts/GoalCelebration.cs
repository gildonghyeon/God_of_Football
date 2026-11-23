using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 내 골 장면에 보여 줄 내용
    public class GoalInfo
    {
        public string minute, scorer, how, assist, ownName, oppName;
        public int goalsFor, goalsAgainst;
        public int matchGoals;   // 이번 경기에서 내가 넣은 골 수 (이 골 포함)
        public int seasonGoal;   // 이 골이 시즌 몇 호 골인지 (0이면 표시 안 함, 친선 경기 등)
        public TrainingStageDrills.GoalScene scene;   // 골 장면 종류 (드리블, 패스, 크로스 헤딩, 코너킥 헤딩, 프리킥, 페널티킥, 중거리)
        public bool isAssist;    // 내 도움으로 동료가 넣은 골 (ASSIST! 화면). 이때 matchGoals·seasonGoal은 도움 수
        public string partner;   // 도움일 때 골을 넣은 동료 이름
        public bool isDefense;   // 내 수비 장면 (TACKLE! 등). 이때 matchGoals는 이번 경기 수비 횟수
        public TrainingStageDrills.DefenseScene defense;
    }

    // 내가 골을 넣었을 때의 화면.
    // 1) 골 장면: 내 캐릭터(내 외형)가 공을 몰고 들어와 태클을 피하고 슈팅, 골키퍼가 몸을 날리고 공이 골망으로 (페널티킥이면 PK 장면)
    // 2) 공이 들어가는 순간: 번쩍임 → GOAL! 글자가 쾅 → 빛줄기·꽃가루, 멀티골·해트트릭 배지, 득점 정보 카드. 장면 속 내 캐릭터는 달려 나가 점프하고 환호
    // 경기 영상은 이 동안 멈춥니다. 골 장면 중에 누르면 골 순간으로 건너뛰고, 그 뒤에 누르면 닫힙니다.
    // 선수 그림이 없으면 예전처럼 골망 그림으로 대신합니다.
    public class GoalCelebration
    {
        const float AfterGoal = 3.0f;   // 골 순간 뒤 그냥 두면 닫히는 시간
        const float MinShow = 0.6f;     // 골 순간 뒤 누르기로 닫을 수 있는 최소 시간
        const int ConfettiCount = 56;
        const float PanelY = 40f;       // 장면 판의 가운데 높이 (화면 가운데 기준)

        static readonly Color Gold = PixelTheme.Accent;
        static readonly Color GoldDark = new Color(.35f, .22f, 0f, 1f);
        static readonly Color OurBlue = new Color(.30f, .50f, .90f, 1f);
        static readonly Color ComboColor = new Color(.88f, .36f, .16f, 1f);
        static readonly Color[] ConfettiColors =
        {
            new Color(.98f, .80f, .28f, 1f), new Color(.40f, .62f, 1f, 1f), Color.white,
            new Color(.45f, .90f, .55f, 1f), new Color(1f, .50f, .40f, 1f)
        };

        readonly Font font;
        readonly Sprite[] frames;
        readonly TrainingStageView scene;   // 내 캐릭터 골 장면 (null이면 골망 그림)
        readonly GameObject root;
        readonly RectTransform rootRect, rays, panel, goalText, comboPill, infoCard;
        readonly Image dim, flash, net;
        readonly CanvasGroup raysGroup, comboGroup, infoGroup, goalGroup;
        readonly Text comboLabel, minuteText, nameText, detailText, scoreText, hintText, captionText, goalLabel;
        readonly Outline goalShade;
        static readonly Color AssistColor = new Color(.45f, .92f, .78f, 1f);
        static readonly Color AssistDark = new Color(.04f, .28f, .22f, 1f);
        static readonly Color DefenseColor = new Color(.62f, .95f, .48f, 1f);
        static readonly Color DefenseDark = new Color(.10f, .30f, .06f, 1f);
        readonly System.Collections.Generic.List<Image> rayImages = new System.Collections.Generic.List<Image>();
        bool defenseMode;   // 수비 장면: 꽃가루 없이, 번쩍임과 빛줄기를 약하게
        readonly RectTransform[] confetti = new RectTransform[ConfettiCount];
        readonly Vector2[] cPos = new Vector2[ConfettiCount], cVel = new Vector2[ConfettiCount];
        readonly float[] cRot = new float[ConfettiCount], cSpin = new float[ConfettiCount];
        readonly System.Random rng = new System.Random();
        readonly float panelH, goalY, comboY, infoY;

        float t, goalAt;   // goalAt: 공이 골망에 닿는 시각
        bool comboShown, goalShown;
        int combo;
        public bool Active { get; private set; }

        public GoalCelebration(Font font, RectTransform parent, Sprite[] frames, TrainingStageView scene)
        {
            this.font = font; this.frames = frames; this.scene = scene;
            rootRect = NewRect("GoalCelebration", parent); Stretch(rootRect);
            root = rootRect.gameObject;
            dim = root.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, .78f);
            var tap = root.AddComponent<Button>(); tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(OnTap);

            // 빛줄기: 가운데에서 퍼지는 금색 띠들이 천천히 돕니다.
            rays = NewRect("Rays", rootRect); Center(rays, 0f, PanelY + 60f, 0f, 0f);
            raysGroup = rays.gameObject.AddComponent<CanvasGroup>(); raysGroup.blocksRaycasts = false;
            for (int i = 0; i < 12; i++)
            {
                var bar = NewRect("Ray", rays); Center(bar, 0f, 0f, 40f, 1900f);
                bar.localRotation = Quaternion.Euler(0f, 0f, i * 15f);
                rayImages.Add(Img(bar, new Color(Gold.r, Gold.g, Gold.b, .09f)));
            }

            // 장면 판 (금색 테두리): 내 캐릭터 골 장면, 또는 골망 그림
            float panelW;
            if (scene != null)
            {
                panelW = 700f;
                float viewW = panelW - 16f - 8f;   // 금색 테두리 8 x2, 무대 틀 4 x2
                panelH = (scene.ViewBottom - scene.ViewTop) * viewW / 752f + 8f + 16f;
            }
            else { panelW = 576f; panelH = 560f * 65f / 97f + 16f; }
            panel = NewRect("ScenePanel", rootRect); Center(panel, 0f, PanelY, panelW, panelH);
            Img(panel, Gold);
            var inner = NewRect("Inner", panel); Stretch(inner); inner.offsetMin = new Vector2(8, 8); inner.offsetMax = new Vector2(-8, -8);
            if (scene != null)
            {
                Img(inner, Color.black);
                var frame = scene.Create(inner);
                Stretch(frame);
            }
            else { net = Img(inner, Color.white); net.sprite = frames.Length > 0 ? frames[0] : null; }

            goalY = PanelY + panelH / 2f + 105f;
            comboY = PanelY - panelH / 2f - 40f;
            infoY = PanelY - panelH / 2f - 176f;

            // 장면 위 안내 (골 순간 전까지): 몇 분, 누구의 찬스인지
            captionText = Label(Center(NewRect("Caption", rootRect), 0f, PanelY + panelH / 2f + 40f, 700f, 50f), "", 28, PixelTheme.Text, TextAnchor.MiddleCenter);
            captionText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .8f);

            // GOAL! 글자
            goalText = NewRect("GoalText", rootRect); Center(goalText, 0f, goalY, 720f, 170f);
            goalGroup = goalText.gameObject.AddComponent<CanvasGroup>(); goalGroup.blocksRaycasts = false;
            var g = Label(goalText, "GOAL!", 124, Gold, TextAnchor.MiddleCenter);
            goalLabel = g;
            var o1 = g.gameObject.AddComponent<Outline>(); o1.effectColor = GoldDark; o1.effectDistance = new Vector2(6, -6);
            goalShade = o1;
            var o2 = g.gameObject.AddComponent<Outline>(); o2.effectColor = new Color(0f, 0f, 0f, .9f); o2.effectDistance = new Vector2(-4, 4);

            // 멀티골·해트트릭 배지 (장면 판 바로 아래)
            comboPill = NewRect("Combo", rootRect); Center(comboPill, 0f, comboY, 300f, 58f);
            comboGroup = comboPill.gameObject.AddComponent<CanvasGroup>(); comboGroup.blocksRaycasts = false;
            Img(comboPill, Color.white);
            var comboFill = NewRect("Fill", comboPill); Stretch(comboFill); comboFill.offsetMin = new Vector2(4, 4); comboFill.offsetMax = new Vector2(-4, -4);
            Img(comboFill, ComboColor);
            comboLabel = Label(comboFill, "", 30, Color.white, TextAnchor.MiddleCenter);
            comboLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, .6f);

            // 득점 정보 카드: 분, 내 이름, 어떻게 넣었는지, 점수
            infoCard = NewRect("Info", rootRect); Center(infoCard, 0f, infoY, 640f, 196f);
            infoGroup = infoCard.gameObject.AddComponent<CanvasGroup>(); infoGroup.blocksRaycasts = false;
            Img(infoCard, Gold);
            var card = NewRect("Fill", infoCard); Stretch(card); card.offsetMin = new Vector2(4, 4); card.offsetMax = new Vector2(-4, -4);
            Img(card, new Color(.06f, .08f, .14f, .97f));
            var badge = NewRect("Minute", card); Anchor(badge, .03f, .62f, .17f, .92f);
            Img(badge, OurBlue);
            minuteText = Label(badge, "", 24, Color.white, TextAnchor.MiddleCenter);
            nameText = Fit(Label(Anchor(NewRect("Name", card), .20f, .60f, .97f, .94f), "", 34, Gold, TextAnchor.MiddleLeft), 18);
            detailText = Fit(Label(Anchor(NewRect("Detail", card), .03f, .36f, .97f, .60f), "", 20, PixelTheme.Text, TextAnchor.MiddleLeft), 12);
            var scoreBand = NewRect("Score", card); Anchor(scoreBand, .03f, .06f, .97f, .34f);
            Img(scoreBand, new Color(.12f, .16f, .25f, 1f));
            scoreText = Fit(Label(scoreBand, "", 24, PixelTheme.Text, TextAnchor.MiddleCenter), 14);
            scoreText.supportRichText = true;

            hintText = Label(Anchor(NewRect("Hint", rootRect), 0f, 0f, 1f, 0f), "", 18, PixelTheme.TextDim, TextAnchor.MiddleCenter);
            hintText.rectTransform.pivot = new Vector2(.5f, 0f); hintText.rectTransform.offsetMin = new Vector2(0, 40); hintText.rectTransform.offsetMax = new Vector2(0, 76);

            // 꽃가루
            for (int i = 0; i < ConfettiCount; i++)
            {
                var c = NewRect("Confetti", rootRect); Center(c, 0f, 0f, 10f, 16f);
                Img(c, ConfettiColors[i % ConfettiColors.Length]);
                confetti[i] = c;
            }

            // 번쩍임 (맨 위)
            var flashRect = NewRect("Flash", rootRect); Stretch(flashRect);
            flash = Img(flashRect, Color.white);

            root.SetActive(false);
        }

        public void Play(GoalInfo info)
        {
            t = 0f; comboShown = false; goalShown = false; combo = info.matchGoals;
            minuteText.text = info.minute;
            nameText.text = info.scorer;
            var parts = new System.Collections.Generic.List<string>();
            if (info.isAssist)
            {
                // 도움: 넣은 동료, 어떤 도움이었는지, 시즌 몇 번째 도움인지
                if (!string.IsNullOrEmpty(info.partner)) parts.Add("골 " + info.partner);
                parts.Add(AssistKind(info.scene));
                if (info.seasonGoal > 0) parts.Add("시즌 " + info.seasonGoal + "호 도움");
            }
            else
            {
                if (!string.IsNullOrEmpty(info.how)) parts.Add(info.how + " 골");
                if (!string.IsNullOrEmpty(info.assist)) parts.Add("도움 " + info.assist);
                if (info.seasonGoal > 0) parts.Add("시즌 " + info.seasonGoal + "호 골");
            }
            defenseMode = info.isDefense;
            if (info.isDefense)
            {
                parts.Clear();
                parts.Add(DefenseKind(info.defense));
                if (info.matchGoals > 0) parts.Add("이번 경기 수비 " + info.matchGoals + "회");
            }
            detailText.text = parts.Count > 0 ? string.Join("   ·   ", parts) : "멋진 골!";
            goalLabel.text = info.isDefense ? DefenseTitle(info.defense) : info.isAssist ? "ASSIST!" : "GOAL!";
            goalLabel.color = info.isDefense ? DefenseColor : info.isAssist ? AssistColor : Gold;
            goalShade.effectColor = info.isDefense ? DefenseDark : info.isAssist ? AssistDark : GoldDark;
            Color ray = info.isDefense ? DefenseColor : Gold;
            foreach (var r in rayImages) r.color = new Color(ray.r, ray.g, ray.b, info.isDefense ? .06f : .09f);
            scoreText.text = info.ownName + "   <color=#FBCC47>" + info.goalsFor + "</color> - " + info.goalsAgainst + "   " + info.oppName;
            if (info.isDefense) { comboLabel.text = ""; combo = 0; }
            else if (info.isAssist) comboLabel.text = combo >= 3 ? "도움 " + combo + "개!" : combo == 2 ? "멀티 도움!" : "";
            else comboLabel.text = combo >= 4 ? combo + "골 폭발!" : combo == 3 ? "해트트릭!" : combo == 2 ? "멀티골!" : "";
            captionText.text = info.minute + "  " + info.scorer + "  " + (info.isDefense ? DefenseCaption(info.defense) : info.isAssist ? AssistCaption(info.scene) : SceneCaption(info.scene));

            root.SetActive(true);
            root.transform.SetAsLastSibling();
            goalAt = 0f;
            if (scene != null)
            {
                if (info.isDefense) scene.PlayScene(TrainingStageDrills.MatchDefense(info.defense, out goalAt));
                else scene.PlayScene(TrainingStageDrills.MatchGoal(info.scene, out goalAt, info.isAssist || !string.IsNullOrEmpty(info.assist), info.isAssist));
            }

            // 꽃가루는 화면 위쪽에서 흩어져 떨어집니다 (골 순간부터).
            Rect area = rootRect.rect;
            float w = Mathf.Max(400f, area.width), h = Mathf.Max(800f, area.height);
            for (int i = 0; i < ConfettiCount; i++)
            {
                cPos[i] = new Vector2(Rand(-w / 2f, w / 2f), h / 2f + Rand(0f, h * .35f));
                cVel[i] = new Vector2(Rand(-120f, 120f), Rand(-260f, -60f));
                cRot[i] = Rand(0f, 360f); cSpin[i] = Rand(-540f, 540f);
                confetti[i].anchoredPosition = cPos[i];
                confetti[i].gameObject.SetActive(false);
            }
            Active = true;
            Tick(0f);
        }

        // 골 장면 중에 누르면 골 순간으로, 그 뒤에는 닫기
        void OnTap()
        {
            if (!Active) return;
            if (t < goalAt)
            {
                float skip = goalAt - t;
                while (skip > 0.0001f) { float step = Mathf.Min(skip, 0.05f); if (scene != null) scene.Tick(step); skip -= step; }
                t = goalAt;
                Tick(0f);
            }
            else if (t - goalAt >= MinShow) Finish();
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            t += dt;
            if (scene != null && dt > 0f) scene.Tick(dt);
            float tg = t - goalAt;   // 골 순간부터 흐른 시간 (음수면 아직 골 장면 진행 중)

            SetA(dim, .78f * Mathf.Clamp01(t / .12f));
            float pk = Mathf.Clamp01(t / .3f);
            float ps = Mathf.Lerp(.7f, 1f, BackOut(pk));
            panel.localScale = new Vector3(ps, ps, 1f);
            if (net != null && frames.Length > 0) net.sprite = frames[Mathf.Min(frames.Length - 1, (int)(Mathf.Max(0f, tg) * 8f))];

            if (tg < 0f)   // 골 장면: 안내 글자만
            {
                SetA(captionText, Mathf.Clamp01(t / .25f));
                SetA(flash, 0f); raysGroup.alpha = 0f; goalGroup.alpha = 0f; comboGroup.alpha = 0f; infoGroup.alpha = 0f;
                hintText.text = "화면을 누르면 건너뛰기";
                SetA(hintText, t < .8f ? 0f : .45f);
                return;
            }

            if (!goalShown)
            {
                goalShown = true;
                SoundFx.Play(defenseMode ? Sfx.Tackle : Sfx.Goal);
                if (!defenseMode) foreach (var c in confetti) c.gameObject.SetActive(true);
            }
            SetA(captionText, Mathf.Clamp01(1f - tg / .2f));
            SetA(flash, (defenseMode ? .45f : .85f) * Mathf.Clamp01(1f - tg / .25f));
            raysGroup.alpha = Mathf.Clamp01(tg / .4f);
            rays.localRotation = Quaternion.Euler(0f, 0f, -tg * 22f);

            // GOAL!: 크게 떨어져 쾅 하고 자리 잡은 뒤 살짝 흔들리며 숨쉬기
            goalGroup.alpha = 1f;
            float gs; Vector2 shake = Vector2.zero;
            if (tg < .28f) gs = Mathf.Lerp(2.6f, 1f, 1f - Mathf.Pow(1f - tg / .28f, 3f));
            else
            {
                gs = 1f + .06f * Mathf.Sin((tg - .28f) * 12f);
                float amp = 10f * Mathf.Clamp01(1f - (tg - .28f) / .35f);
                shake = new Vector2(Rand(-amp, amp), Rand(-amp, amp));
            }
            goalText.localScale = new Vector3(gs, gs, 1f);
            goalText.anchoredPosition = new Vector2(0f, goalY) + shake;

            // 멀티골 배지: 0.45초에 튀어나옴
            if (combo >= 2)
            {
                float ck = Mathf.Clamp01((tg - .45f) / .25f);
                comboGroup.alpha = ck > 0f ? 1f : 0f;
                float cs = ck <= 0f ? 0f : BackOut(ck) * (1f + .05f * Mathf.Sin(tg * 10f));
                comboPill.localScale = new Vector3(cs, cs, 1f);
                if (!comboShown && ck > 0f) { comboShown = true; SoundFx.Play(combo >= 3 ? Sfx.Trophy : Sfx.UpgradeGreat, .8f); }
            }
            else comboGroup.alpha = 0f;

            // 정보 카드: 0.5초부터 아래에서 올라옴
            float ik = Mathf.Clamp01((tg - .5f) / .3f);
            infoGroup.alpha = ik;
            infoCard.anchoredPosition = new Vector2(0f, infoY - 70f * Mathf.Pow(1f - ik, 3f));

            hintText.text = "화면을 누르면 계속";
            SetA(hintText, tg < 1.2f ? 0f : .55f + .35f * Mathf.Sin(tg * 4f));

            for (int i = 0; i < ConfettiCount; i++)
            {
                cVel[i].y -= 520f * dt;
                cVel[i].x *= 1f - .6f * dt;
                cPos[i] += cVel[i] * dt;
                cRot[i] += cSpin[i] * dt;
                confetti[i].anchoredPosition = cPos[i] + new Vector2(Mathf.Sin(t * 5f + i) * 14f, 0f);
                confetti[i].localRotation = Quaternion.Euler(0f, 0f, cRot[i]);
                // 뒤집히는 느낌: 세로 크기가 출렁임
                confetti[i].localScale = new Vector3(1f, .35f + .65f * Mathf.Abs(Mathf.Sin(t * 6f + i * 1.7f)), 1f);
            }

            if (tg >= AfterGoal) Finish();
        }

        static string SceneCaption(TrainingStageDrills.GoalScene s)
        {
            switch (s)
            {
                case TrainingStageDrills.GoalScene.Penalty: return "페널티킥!";
                case TrainingStageDrills.GoalScene.FreeKick: return "프리킥!";
                case TrainingStageDrills.GoalScene.Corner: return "코너킥!";
                case TrainingStageDrills.GoalScene.Cross: return "크로스!";
                case TrainingStageDrills.GoalScene.Pass: return "패스를 받아 슈팅!";
                case TrainingStageDrills.GoalScene.LongShot: return "중거리 슛!";
                default: return "드리블 돌파!";
            }
        }

        static string DefenseTitle(TrainingStageDrills.DefenseScene d)
        {
            switch (d)
            {
                case TrainingStageDrills.DefenseScene.Block: return "BLOCKED!";
                case TrainingStageDrills.DefenseScene.Clearance: return "CLEARED!";
                case TrainingStageDrills.DefenseScene.Interception: return "INTERCEPT!";
                default: return "TACKLE!";
            }
        }

        static string DefenseCaption(TrainingStageDrills.DefenseScene d)
        {
            switch (d)
            {
                case TrainingStageDrills.DefenseScene.Block: return "슈팅을 몸으로 막는다!";
                case TrainingStageDrills.DefenseScene.Clearance: return "골문 앞 위기!";
                case TrainingStageDrills.DefenseScene.Interception: return "패스 길을 읽었다!";
                default: return "상대 돌파를 막는다!";
            }
        }

        static string DefenseKind(TrainingStageDrills.DefenseScene d)
        {
            switch (d)
            {
                case TrainingStageDrills.DefenseScene.Block: return "슈팅 블록";
                case TrainingStageDrills.DefenseScene.Clearance: return "골라인 걷어내기";
                case TrainingStageDrills.DefenseScene.Interception: return "가로채기 후 역습";
                default: return "슬라이딩 태클";
            }
        }

        static string AssistCaption(TrainingStageDrills.GoalScene s)
        {
            switch (s)
            {
                case TrainingStageDrills.GoalScene.Cross: return "크로스!";
                case TrainingStageDrills.GoalScene.Corner: return "코너킥!";
                case TrainingStageDrills.GoalScene.LongShot: return "슈팅 자리로 내준 패스!";
                default: return "스루 패스!";
            }
        }

        static string AssistKind(TrainingStageDrills.GoalScene s)
        {
            switch (s)
            {
                case TrainingStageDrills.GoalScene.Cross: return "크로스 도움";
                case TrainingStageDrills.GoalScene.Corner: return "코너킥 도움";
                case TrainingStageDrills.GoalScene.LongShot: return "내준 패스 도움";
                default: return "패스 도움";
            }
        }

        // 경기 영상을 닫을 때: 장면이 만든 그림을 지움
        public void Dispose() { if (scene != null) scene.Dispose(); }

        // 화면까지 통째로 지움 (설정 탭의 미리보기용)
        public void DestroyAll() { Dispose(); if (root != null) Object.Destroy(root); }

        void Finish()
        {
            Active = false;
            root.SetActive(false);
        }

        // ---------- 부품 ----------
        static float BackOut(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float k = x - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static void SetA(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        static RectTransform Anchor(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        // 화면 가운데 기준 위치(x, y)에 고정 크기로 놓기
        static RectTransform Center(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(x, y);
            return r;
        }

        static Image Img(RectTransform r, Color c)
        {
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        Text Label(RectTransform r, string value, int size, Color color, TextAnchor align)
        {
            var go = r.GetComponent<Image>() != null ? NewRect("Text", r) : r;
            if (go != r) Stretch(go);
            var t = go.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Text Fit(Text t, int min)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
    }
}
