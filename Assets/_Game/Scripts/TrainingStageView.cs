using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 동선 데이터 한 칸: 시간 t에 (x, y)에 서서 pose 동작을 하고 face 방향을 봅니다. z는 공이 뜬 높이입니다.
    public struct StageKey
    {
        public float t, x, y, z;
        public string pose;   // auto(걷기/서기 자동), stand, kick, dive, hide(공 숨김)
        public char face;     // u(위) d(아래) l(왼쪽) r(오른쪽) *(이동 방향)
        public StageKey(float t, float x, float y, string pose, char face, float z)
        { this.t = t; this.x = x; this.y = y; this.pose = pose; this.face = face; this.z = z; }
    }

    // 한 훈련의 장면: 선수·골키퍼·상대·동료·공이 시간에 따라 어떻게 움직이는지
    public class StageDrill
    {
        public float duration = 1f;
        public bool ballFollows;   // 공이 선수 발 앞을 따라다니는지 (드리블)
        public StageKey[] player, gk, attacker, mate, ball;
    }

    // 반코트를 위에서 비스듬히 내려다본 훈련장. 선택한 훈련에 따라 선수가 해당 구역으로 걸어가 훈련합니다.
    //   Resources/Art/Stage/ 폴더: bg.png(배경), <팀>_<방향>_<동작>.png(선수), ball.png, shadow.png, ball_shadow.png
    //   동선 데이터는 TrainingStageDrills.cs에 있습니다. 같은 이름과 크기로 이미지를 바꾸면 새 그림이 적용됩니다.
    public class TrainingStageView
    {
        public const float Scale = 5f;     // 이미지 한 픽셀을 화면에서 대략 몇 배로 키울지
        const float WalkSpeed = 40f;       // 훈련 구역으로 이동하는 속도 (배경 픽셀/초)
        const string ArtPath = "Art/Stage/";

        class Actor
        {
            public string team;                       // blue / red / gold (공은 null)
            public RectTransform root, body, shadow;
            public Image bodyImage, shadowImage;
            public float x, y, z, sortKey, walkDist;
            public float homeX, homeY;
            public char homeFace = 'd';
            public char face = 'd';
            public string pose = "stand";
            public bool shown = true;
        }

        static readonly Comparison<Actor> BySortKey = (p, q) => p.sortKey.CompareTo(q.sortKey);

        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, StageDrill> drills;
        readonly List<Actor> drawOrder = new List<Actor>();
        Sprite background, ballSprite, shadowSprite, ballShadowSprite;
        float bgW = 131f, bgH = 56f;
        RectTransform world;
        Actor player, gk, attacker, mate, ball;
        string currentKey = "idle";
        bool walking;
        float drillTime, clock;

        public bool Available
        {
            get
            {
                return background != null && ballSprite != null && shadowSprite != null
                    && sprites.ContainsKey("blue_down_stand") && drills.ContainsKey("idle");
            }
        }

        public TrainingStageView()
        {
            drills = TrainingStageDrills.Build();
            background = LoadSprite("bg");
            ballSprite = LoadSprite("ball");
            shadowSprite = LoadSprite("shadow");
            ballShadowSprite = LoadSprite("ball_shadow");
            if (ballShadowSprite == null) ballShadowSprite = shadowSprite;
            if (background != null) { bgW = background.rect.width; bgH = background.rect.height; }
            foreach (string team in new[] { "blue", "red", "gold" })
            {
                foreach (string face in new[] { "down", "up", "side" })
                    foreach (string pose in new[] { "stand", "walk0", "walk1", "kick" })
                        Register(team + "_" + face + "_" + pose);
                Register(team + "_side_dive");
            }
        }

        void Register(string name)
        {
            var sprite = LoadSprite(name);
            if (sprite != null) sprites[name] = sprite;
        }

        static Sprite LoadSprite(string name)
        {
            var tex = Resources.Load<Texture2D>(ArtPath + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;   // 도트가 번지지 않게
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }

        public static string AnimationFor(AttributeKind kind)
        {
            switch (kind)
            {
                case AttributeKind.Shooting: return "shoot";
                case AttributeKind.Passing: return "pass";
                case AttributeKind.Dribbling: return "dribble";
                case AttributeKind.Defending: return "defend";
                default: return "stamina";
            }
        }

        // ---------- 화면 만들기 ----------
        public RectTransform Create(Transform parent)
        {
            var frameGo = new GameObject("StageFrame", typeof(RectTransform));
            frameGo.transform.SetParent(parent, false);
            frameGo.AddComponent<Image>().color = PixelTheme.PanelEdge;
            float height = bgH * Scale + 8f;
            var layout = frameGo.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(frameGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4, 4); viewportRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<RectMask2D>();

            // 배경: 가로를 가득 채우고 높이는 그림 비율대로
            var worldGo = new GameObject("World", typeof(RectTransform));
            worldGo.transform.SetParent(viewport.transform, false);
            world = worldGo.GetComponent<RectTransform>();
            world.anchorMin = new Vector2(0, 0); world.anchorMax = new Vector2(1, 0);
            world.pivot = new Vector2(.5f, 0);
            world.anchoredPosition = Vector2.zero; world.sizeDelta = new Vector2(0, 100);
            var fitter = worldGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = bgW / bgH;
            var bgImage = worldGo.AddComponent<Image>();
            bgImage.sprite = background; bgImage.raycastTarget = false;

            gk = MakeActor("Goalkeeper", "gold");
            attacker = MakeActor("Attacker", "red");
            mate = MakeActor("Teammate", "red");
            player = MakeActor("Player", "blue");
            ball = MakeActor("Ball", null);
            ball.bodyImage.sprite = ballSprite;
            ball.shadowImage.sprite = ballShadowSprite;

            gk.homeX = TrainingStageDrills.HomeGkX; gk.homeY = TrainingStageDrills.HomeGkY; gk.homeFace = TrainingStageDrills.HomeGkFace;
            attacker.homeX = TrainingStageDrills.HomeAttackerX; attacker.homeY = TrainingStageDrills.HomeAttackerY; attacker.homeFace = TrainingStageDrills.HomeAttackerFace;
            mate.homeX = TrainingStageDrills.HomeMateX; mate.homeY = TrainingStageDrills.HomeMateY; mate.homeFace = TrainingStageDrills.HomeMateFace;
            foreach (var npc in new[] { gk, attacker, mate }) { npc.x = npc.homeX; npc.y = npc.homeY; npc.face = npc.homeFace; }

            var start = drills["idle"].player[0];
            player.x = start.x; player.y = start.y; player.face = start.face == '*' ? 'd' : start.face;

            drawOrder.Clear();
            drawOrder.Add(gk); drawOrder.Add(attacker); drawOrder.Add(mate); drawOrder.Add(player); drawOrder.Add(ball);

            currentKey = "idle"; walking = false; drillTime = 0f; clock = 0f;
            Tick(0f);
            return frameGo.GetComponent<RectTransform>();
        }

        Actor MakeActor(string name, string team)
        {
            var actor = new Actor { team = team };
            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(world, false);
            actor.root = rootGo.GetComponent<RectTransform>();
            actor.root.anchorMin = Vector2.zero; actor.root.anchorMax = Vector2.one;
            actor.root.offsetMin = Vector2.zero; actor.root.offsetMax = Vector2.zero;
            actor.shadowImage = AddSprite(actor.root, "Shadow", shadowSprite);
            actor.shadow = actor.shadowImage.rectTransform;
            actor.bodyImage = AddSprite(actor.root, "Body", null);
            actor.body = actor.bodyImage.rectTransform;
            return actor;
        }

        static Image AddSprite(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            return image;
        }

        // ---------- 동작 바꾸기 ----------
        // 훈련이 바뀌면 선수가 새 훈련 구역으로 걸어간 뒤 그 훈련을 반복합니다.
        public void Play(string key)
        {
            if (!drills.ContainsKey(key)) key = "idle";
            if (key == currentKey) return;
            currentKey = key;
            walking = true;
            drillTime = 0f;
        }

        // ---------- 매 프레임 ----------
        public void Tick(float deltaSeconds)
        {
            if (world == null || player == null || player.root == null) return;
            float dt = Mathf.Min(deltaSeconds, 0.1f);
            clock += dt;
            StageDrill drill = drills[currentKey];

            // 선수
            float prevX = player.x, prevY = player.y;
            string pose = "auto";
            char face = player.face;
            if (walking)
            {
                StageKey first = drill.player[0];
                float dx = first.x - player.x, dy = first.y - player.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float step = WalkSpeed * dt;
                if (dist <= step)
                {
                    player.x = first.x; player.y = first.y;
                    walking = false; drillTime = 0f;
                    pose = first.pose;
                    if (first.face != '*') face = first.face;
                }
                else
                {
                    player.x += dx / dist * step; player.y += dy / dist * step;
                    face = DirectionFace(dx, dy, player.face);
                }
            }
            else
            {
                drillTime += dt;
                if (drillTime >= drill.duration) drillTime -= drill.duration * Mathf.Floor(drillTime / drill.duration);
                float sx, sy, sz;
                Sample(drill.player, drillTime, player.face, out sx, out sy, out sz, out pose, out face);
                player.x = sx; player.y = sy;
            }
            Resolve(player, pose, face, Distance(prevX, prevY, player.x, player.y));

            // 다른 인물: 훈련에 참여하지 않거나 선수가 이동 중이면 제자리에 서 있습니다.
            UpdateNpc(gk, drill.gk);
            UpdateNpc(attacker, drill.attacker);
            UpdateNpc(mate, drill.mate);

            // 공
            bool ballVisible = true;
            ball.z = 0f;
            if (!walking && drill.ball != null && drill.ball.Length > 0)
            {
                float bx, by, bz; string bp; char bf;
                Sample(drill.ball, drillTime, 'd', out bx, out by, out bz, out bp, out bf);
                ball.x = bx; ball.y = by; ball.z = bz;
                ballVisible = bp != "hide";
            }
            else if (walking || drill.ballFollows)
            {
                float ox, oy;
                FollowOffset(player.face, out ox, out oy);
                ball.x = player.x + ox; ball.y = player.y + oy;
                bool moving = Distance(prevX, prevY, player.x, player.y) > 0.02f;
                ball.z = moving ? Mathf.Abs(Mathf.Sin(clock * 9f)) * 1.5f : 0f;
            }
            else ballVisible = false;

            // 그리기
            RenderCharacter(gk); RenderCharacter(attacker); RenderCharacter(mate); RenderCharacter(player);
            RenderBall(ballVisible);
            drawOrder.Sort(BySortKey);
            for (int i = 0; i < drawOrder.Count; i++) drawOrder[i].root.SetSiblingIndex(i);
        }

        void UpdateNpc(Actor a, StageKey[] keys)
        {
            float prevX = a.x, prevY = a.y;
            float x, y, z;
            string pose;
            char face;
            if (keys != null && keys.Length > 0 && !walking)
                Sample(keys, drillTime, a.face, out x, out y, out z, out pose, out face);
            else
            { x = a.homeX; y = a.homeY; pose = "stand"; face = a.homeFace; }
            a.x = x; a.y = y;
            Resolve(a, pose, face, Distance(prevX, prevY, x, y));
        }

        // auto 동작은 움직이면 걷기 두 장을 번갈아, 멈추면 서 있는 모습으로 정합니다.
        static void Resolve(Actor a, string pose, char face, float moved)
        {
            if (pose == "auto")
            {
                if (moved > 0.02f)
                {
                    a.walkDist += moved;
                    pose = ((int)(a.walkDist / 3f)) % 2 == 0 ? "walk0" : "walk1";
                }
                else pose = "stand";
            }
            a.pose = pose; a.face = face;
        }

        static float Distance(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static char DirectionFace(float dx, float dy, char fallback)
        {
            if (Mathf.Abs(dx) + Mathf.Abs(dy) < 0.01f) return fallback;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy)) return dx > 0f ? 'r' : 'l';
            return dy > 0f ? 'd' : 'u';
        }

        static void FollowOffset(char face, out float ox, out float oy)
        {
            switch (face)
            {
                case 'u': ox = 4f; oy = -2f; break;
                case 'l': ox = -6f; oy = -1f; break;
                case 'r': ox = 6f; oy = -1f; break;
                default: ox = 4f; oy = 1f; break;
            }
        }

        // 시간 time에서 키 사이를 직선으로 이어 위치를 구합니다. 동작과 방향은 지나온 키의 값을 씁니다.
        static void Sample(StageKey[] keys, float time, char fallbackFace,
            out float x, out float y, out float z, out string pose, out char face)
        {
            int i = 0;
            while (i + 1 < keys.Length && keys[i + 1].t <= time) i++;
            StageKey a = keys[i];
            if (i + 1 >= keys.Length)
            {
                x = a.x; y = a.y; z = a.z; pose = a.pose;
                face = a.face == '*' ? fallbackFace : a.face;
                return;
            }
            StageKey b = keys[i + 1];
            float u = Mathf.Clamp01((time - a.t) / Mathf.Max(0.0001f, b.t - a.t));
            x = Mathf.Lerp(a.x, b.x, u);
            y = Mathf.Lerp(a.y, b.y, u);
            z = Mathf.Lerp(a.z, b.z, u);
            pose = a.pose;
            face = a.face == '*' ? DirectionFace(b.x - a.x, b.y - a.y, fallbackFace) : a.face;
        }

        // ---------- 그리기 ----------
        Sprite GetSprite(string team, char face, string pose, out bool flip)
        {
            flip = face == 'l';
            string f = face == 'd' ? "down" : (face == 'u' ? "up" : "side");
            if (pose == "dive" && f != "side") pose = "stand";
            Sprite sprite;
            if (sprites.TryGetValue(team + "_" + f + "_" + pose, out sprite)) return sprite;
            if (sprites.TryGetValue(team + "_" + f + "_stand", out sprite)) return sprite;
            return null;
        }

        void SetShown(Actor a, bool shown)
        {
            if (a.shown == shown) return;
            a.shown = shown;
            a.root.gameObject.SetActive(shown);
        }

        // 배경 픽셀 좌표(왼쪽 위가 0,0)로 이미지 위치를 정합니다. 위치는 정수로 맞춰 도트가 어긋나지 않게 합니다.
        void Place(RectTransform rect, float left, float top, float w, float h)
        {
            rect.anchorMin = new Vector2(left / bgW, 1f - (top + h) / bgH);
            rect.anchorMax = new Vector2((left + w) / bgW, 1f - top / bgH);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        static float Snap(float v) { return Mathf.Floor(v + 0.5f); }

        void RenderCharacter(Actor a)
        {
            bool flip;
            Sprite sprite = GetSprite(a.team, a.face, a.pose, out flip);
            if (sprite == null) { SetShown(a, false); return; }
            SetShown(a, true);
            float w = sprite.rect.width, h = sprite.rect.height;
            float feet = Snap(a.y);
            a.bodyImage.sprite = sprite;
            a.body.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            Place(a.body, Snap(a.x) - Mathf.Floor((w - 1f) / 2f), feet - h + 1f, w, h);
            float sw = shadowSprite.rect.width, sh = shadowSprite.rect.height;
            Place(a.shadow, Snap(a.x) - Mathf.Floor((sw - 1f) / 2f), feet, sw, sh);
            a.sortKey = a.y;
        }

        void RenderBall(bool visible)
        {
            SetShown(ball, visible);
            if (!visible) return;
            float w = ballSprite.rect.width, h = ballSprite.rect.height;
            float sw = ballShadowSprite.rect.width, sh = ballShadowSprite.rect.height;
            float bx = Snap(ball.x);
            Place(ball.body, bx - Mathf.Floor((w - 1f) / 2f), Snap(ball.y - ball.z) - Mathf.Floor((h - 1f) / 2f), w, h);
            Place(ball.shadow, bx - Mathf.Floor((sw - 1f) / 2f), Snap(ball.y) + 2f, sw, sh);
            ball.sortKey = ball.y + 0.01f;
        }
    }
}
