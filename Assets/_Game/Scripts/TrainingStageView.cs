using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 동선 데이터 한 칸: 시간 t에 (x, y)에 서서 pose 동작을 하고 face 방향을 봅니다. z는 공이 뜬 높이(픽셀)입니다.
    public struct StageKey
    {
        public float t, x, y, z;
        public string pose;   // auto(걷기/서기 자동), carry(공을 몰고 가기), stand, kick, dive, cheer, hide(공 숨김)
        public char face;     // u(위) d(아래) l(왼쪽) r(오른쪽) *(이동 방향)
        public StageKey(float t, float x, float y, string pose, char face, float z)
        { this.t = t; this.x = x; this.y = y; this.pose = pose; this.face = face; this.z = z; }
    }

    // 한 훈련의 장면: 선수·골키퍼·상대·동료·공이 시간에 따라 어떻게 움직이는지
    public class StageDrill
    {
        public float duration = 1f;
        public bool loop = true;   // false면 한 번만 하고 리프팅(idle)으로 돌아갑니다
        public bool ballFollows;   // 공이 선수 발 앞을 따라다니는지
        public StageKey[] player, gk, attacker, mate, ball;
    }

    // 경기장 위쪽(골대와 페널티 박스) 띠를 훈련장으로 쓰는 화면. 내 선수는 제자리에서 리프팅을 하고,
    // 강화를 누르면 그 능력치의 짧은 동작과 결과 반응(+1 세리머니, +2 점프, 실패 넘어짐)을 보여 줍니다.
    //   Resources/Art/Chars/ 폴더: training_bg(배경), <팀>_<동작>_<방향>_<번호>(선수 프레임), ball, shadow
    //   팀: blue(내 선수) red(상대·동료) gold(골키퍼), 동작: idle run dribble shoot (방향 front side back), jump cheer fall (방향 x)
    //   동선 데이터는 TrainingStageDrills.cs에 있습니다.
    public class TrainingStageView
    {
        public const float Scale = 664f / 752f;   // 훈련장 그림 한 픽셀이 화면에서 차지하는 크기(기준 해상도)
        const float CharScale = 0.85f;            // 선수 그림을 훈련장 그림에 맞춰 줄이는 비율
        const float BallDiameter = 23f;           // 선수 그림 속 공의 지름(선수 그림 픽셀)
        const float BaseRow = 118f;               // 선수 그림에서 발바닥이 있는 줄
        const float StepPx = 8f;                  // 달리기 그림이 한 장 넘어가는 이동 거리
        const float WalkSpeed = 240f;             // 동작이 끊겼을 때 시작 자리로 돌아가는 속도 (그림 픽셀/초)
        const float StepBob = 3f;                 // 달릴 때 걸음마다 몸이 들썩이는 높이 (선수 그림 픽셀). 그림자는 땅에 그대로 둡니다.
        const float StrideStepPx = 9f;            // 보강한 달리기 그림이 한 장 넘어가는 이동 거리
        const float StrideBob = 2f;               // 보강한 달리기 그림에서 다리가 모일 때 몸이 떠오르는 높이
        const string ArtPath = "Art/Chars/";

        class Actor
        {
            public string team;                       // blue / red / gold (공은 null)
            public RectTransform root, body, shadow;
            public Image bodyImage, shadowImage;
            public float x, y, z, sortKey, walkDist, poseTime;
            public float homeX, homeY;
            public char homeFace = 'd';
            public char face = 'd';
            public string pose = "stand";
            public int phase;
            public bool shown = true;
        }

        static readonly Comparison<Actor> BySortKey = (p, q) => p.sortKey.CompareTo(q.sortKey);

        readonly Dictionary<string, Sprite[]> strips = new Dictionary<string, Sprite[]>();
        readonly Dictionary<string, StageDrill> drills;
        readonly List<Actor> drawOrder = new List<Actor>();
        Sprite background, ballSprite, shadowSprite;
        float bgW = 752f, bgH = 360f;
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
                    && strips.ContainsKey("blue_idle_front") && strips.ContainsKey("blue_run_side")
                    && drills.ContainsKey("idle");
            }
        }

        public TrainingStageView()
        {
            drills = TrainingStageDrills.Build();
            background = LoadSprite("training_bg");
            ballSprite = LoadSprite("ball");
            shadowSprite = LoadSprite("shadow");
            if (background != null) { bgW = background.rect.width; bgH = background.rect.height; }
            foreach (string team in new[] { "red", "gold" })
            {
                foreach (string anim in new[] { "idle", "run", "dribble", "shoot" })
                    foreach (string view in new[] { "front", "side", "back" })
                        LoadStrip(team + "_" + anim + "_" + view);
                foreach (string anim in new[] { "jump", "cheer", "fall" })
                    LoadStrip(team + "_" + anim + "_x");
            }
            SetLook(0, 0, 0);   // 내 선수(blue): 원래 모습으로 시작
        }

        // ---------- 내 선수 외형 ----------
        int lookHair = -1, lookSkin = -1, lookKit = -1;
        readonly List<UnityEngine.Object> lookObjects = new List<UnityEngine.Object>();

        // 내 선수(blue) 그림을 머리색·피부색·유니폼에 맞춰 다시 만듭니다. 같은 외형이면 아무것도 하지 않습니다.
        public void SetLook(int hair, int skin, int kit)
        {
            hair = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            skin = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            kit = Mathf.Clamp(kit, 0, PlayerLook.KitCount - 1);
            if (hair == lookHair && skin == lookSkin && kit == lookKit) return;
            foreach (var obj in lookObjects) if (obj != null) UnityEngine.Object.Destroy(obj);
            lookObjects.Clear();
            foreach (string anim in new[] { "idle", "run", "dribble", "shoot" })
                foreach (string view in new[] { "front", "side", "back" })
                    LoadLookStrip("blue_" + anim + "_" + view, hair, skin, kit);
            foreach (string anim in new[] { "jump", "cheer", "fall" })
                LoadLookStrip("blue_" + anim + "_x", hair, skin, kit);
            LoadStrideStrips();
            BuildJuggleStrip();
            lookHair = hair; lookSkin = skin; lookKit = kit;
        }

        // ---------- 리프팅 그림 ----------
        // 리프팅 중 공을 찰 때 보여 줄 '발 들기' 그림 2장(blue_juggle_x)을 만듭니다: 0 오른발, 1 왼발(0을 좌우로 뒤집음).
        // 정면 슈팅 그림에서 발 옆의 공을 지우고, 서 있는 그림과 키가 같도록 발을 기준으로 키웁니다(슈팅 그림이 약 10% 작음).
        // 평소에는 두 발로 선 기본 그림을 쓰고, 공이 발에 닿는 순간에만 이 그림을 잠깐 보여 줍니다. 그림을 읽을 수 없으면 만들지 않습니다.
        const float JuggleFootX = 50f, JuggleFootY = 119f, JuggleScale = 113f / 103f;
        void BuildJuggleStrip()
        {
            strips.Remove("blue_juggle_x");
            var src = Find("blue_shoot_front");
            if (src == null || src.Length < 2) return;
            var liftPx = WithoutBall(src[1], 75f, 108f, 63);
            if (liftPx == null) return;
            int w = src[1].texture.width, h = src[1].texture.height;
            liftPx = ScaleAboutFoot(liftPx, w, h);
            var mirrored = new Color32[liftPx.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) mirrored[y * w + x] = liftPx[y * w + (w - 1 - x)];
            strips["blue_juggle_x"] = new[] { MakeSprite(liftPx, w, h), MakeSprite(mirrored, w, h) };
        }

        // 발 위치(JuggleFoot)를 기준으로 그림을 JuggleScale배 키웁니다 (가장 가까운 픽셀)
        static Color32[] ScaleAboutFoot(Color32[] src, int w, int h)
        {
            var dst = new Color32[src.Length];
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;   // 그림 좌표 (위가 0)
                for (int x = 0; x < w; x++)
                {
                    int sx = (int)(JuggleFootX + (x + 0.5f - JuggleFootX) / JuggleScale);
                    int sy = (int)(JuggleFootY + (y + 0.5f - JuggleFootY) / JuggleScale);
                    if (sx < 0 || sx >= w || sy < 0 || sy >= h) continue;
                    dst[row * w + x] = src[(h - 1 - sy) * w + sx];
                }
            }
            return dst;
        }

        // 그림 좌표(왼쪽 위 0,0)의 원(반지름 14) 안 픽셀 중 minX보다 오른쪽만 지웁니다 (공만 지우고 다리는 남김)
        static Color32[] WithoutBall(Sprite sprite, float cx, float cy, int minX)
        {
            var tex = sprite.texture;
            if (!tex.isReadable) return null;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;   // 텍스처는 아래 줄부터 저장
                for (int x = minX; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= 14f * 14f) px[row * w + x] = new Color32(0, 0, 0, 0);
                }
            }
            return px;
        }

        Sprite MakeSprite(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
            lookObjects.Add(tex); lookObjects.Add(sprite);
            return sprite;
        }

        // ---------- 보강한 달리기 그림 (StrideFrames) ----------
        Dictionary<string, int[][]> strideMaps;   // 어느 픽셀을 어디서 가져올지: 원래 색 그림으로 한 번만 계산
        bool strideTried;

        // 방금 만든 외형 그림들로 보강한 달리기 그림(blue_stride_<방향>)을 만듭니다. 그림이 모자라면 원래 달리기 그림을 씁니다.
        void LoadStrideStrips()
        {
            foreach (string view in StrideFrames.Views) strips.Remove("blue_stride_" + view);
            if (!strideTried) { strideTried = true; strideMaps = StrideFrames.Build(OriginalPixels); }
            if (strideMaps == null) return;
            var colored = new Color32[StrideFrames.Sources.Length][];
            for (int s = 0; s < colored.Length; s++)
            {
                colored[s] = LookPixels("blue_" + StrideFrames.Sources[s]);
                if (colored[s] == null) return;
            }
            foreach (string view in StrideFrames.Views)
            {
                var frames = strideMaps[view];
                var list = new Sprite[frames.Length];
                for (int f = 0; f < frames.Length; f++)
                {
                    var tex = new Texture2D(100, 130, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
                    tex.SetPixels32(StrideFrames.Apply(frames[f], colored));
                    tex.Apply(false, true);
                    list[f] = Sprite.Create(tex, new Rect(0, 0, 100, 130), new Vector2(.5f, .5f), 100f);
                    lookObjects.Add(tex); lookObjects.Add(list[f]);
                }
                strips["blue_stride_" + view] = list;
            }
        }

        // 원래 색 그림의 픽셀 (blue_ 뒤 이름)
        static Color32[] OriginalPixels(string name)
        {
            var asset = Resources.Load<TextAsset>(ArtPath + "blue_" + name);
            if (asset == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color32[] px = tex.LoadImage(asset.bytes) ? tex.GetPixels32() : null;
            UnityEngine.Object.Destroy(tex);
            return px;
        }

        // 지금 외형으로 만든 그림의 픽셀 (예: blue_run_side_3)
        Color32[] LookPixels(string name)
        {
            int cut = name.LastIndexOf('_');
            int index;
            if (cut < 0 || !int.TryParse(name.Substring(cut + 1), out index)) return null;
            Sprite[] strip;
            if (!strips.TryGetValue(name.Substring(0, cut), out strip) || index >= strip.Length) return null;
            var tex = strip[index].texture;
            return tex.isReadable && tex.width == 100 && tex.height == 130 ? tex.GetPixels32() : null;
        }

        // 색을 바꿀 수 있는 그림(.bytes)을 읽어 색을 바꿉니다. .bytes가 없으면 일반 이미지를 그대로 씁니다.
        void LoadLookStrip(string prefix, int hair, int skin, int kit)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                Sprite sprite = null;
                var asset = Resources.Load<TextAsset>(ArtPath + prefix + "_" + i);
                if (asset != null)
                {
                    var tex = PlayerLook.LoadRecolored(asset.bytes, hair, skin, kit);
                    if (tex != null)
                    {
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
                        lookObjects.Add(tex); lookObjects.Add(sprite);
                    }
                }
                else sprite = LoadSprite(prefix + "_" + i);
                if (sprite == null) break;
                list.Add(sprite);
            }
            if (list.Count > 0) strips[prefix] = list.ToArray();
        }

        void LoadStrip(string prefix)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var sprite = LoadSprite(prefix + "_" + i);
                if (sprite == null) break;
                list.Add(sprite);
            }
            if (list.Count > 0) strips[prefix] = list.ToArray();
        }

        static Sprite LoadSprite(string name)
        {
            var tex = Resources.Load<Texture2D>(ArtPath + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Bilinear;
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
            float height = (TrainingStageDrills.BandBottom - TrainingStageDrills.BandTop) * Scale + 8f;   // 위쪽 띠만 보여 줌
            var layout = frameGo.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(frameGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4, 4); viewportRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<RectMask2D>();

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

            gk = MakeActor("Goalkeeper", "gold", 1);
            attacker = MakeActor("Attacker", "red", 2);
            mate = MakeActor("Teammate", "red", 3);
            player = MakeActor("Player", "blue", 0);
            ball = MakeActor("Ball", null, 0);
            ball.bodyImage.sprite = ballSprite;
            ball.shadowImage.sprite = shadowSprite;

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

        Actor MakeActor(string name, string team, int phase)
        {
            var actor = new Actor { team = team, phase = phase };
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
        // 반복 장면으로 바꿉니다 (지금은 리프팅 "idle" 하나). 시작 자리와 다르면 먼저 그곳으로 돌아갑니다.
        public void Play(string key)
        {
            if (!drills.ContainsKey(key)) key = "idle";
            if (key == currentKey) return;
            currentKey = key;
            walking = true;
            drillTime = 0f;
        }

        // 강화 한 번: 능력치 동작 → 결과 반응 → 리프팅. 동작 중에 또 누르면 새 동작으로 바로 바꿉니다.
        public void PlayUpgrade(AttributeKind kind, UpgradeOutcome outcome)
        {
            if (outcome != UpgradeOutcome.Success && outcome != UpgradeOutcome.GreatSuccess && outcome != UpgradeOutcome.Fail) return;
            string react = outcome == UpgradeOutcome.GreatSuccess ? "jump" : outcome == UpgradeOutcome.Success ? "cheer" : "dive";
            drills["action"] = TrainingStageDrills.Action(AnimationFor(kind), react, outcome != UpgradeOutcome.Fail);
            currentKey = "action";
            walking = true;   // 시작 자리에 이미 있으면 바로 시작
            drillTime = 0f;
        }

        // ---------- 매 프레임 ----------
        public void Tick(float deltaSeconds)
        {
            if (world == null || player == null || player.root == null) return;
            float dt = Mathf.Min(deltaSeconds, 0.1f);
            clock += dt;
            // 배경의 위쪽 띠(BandTop~BandBottom)만 보이도록 아래로 내립니다.
            float worldH = world.rect.height;
            if (worldH > 0f) world.anchoredPosition = new Vector2(0f, -(bgH - TrainingStageDrills.BandBottom) / bgH * worldH);
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
                float step = WalkSpeed * dt * Mathf.Clamp(dist / 24f, 0.4f, 1f);   // 도착할 때는 천천히 멈춥니다.
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
                    // 보강한 달리기 그림이 있으면 달려가고(공은 발 앞을 따라옴), 없으면 다리 움직임이 큰 드리블 그림을 씁니다.
                    pose = strips.ContainsKey(player.team + "_stride_side") ? "auto" : "carry";
                }
            }
            else
            {
                drillTime += dt;
                if (drillTime >= drill.duration)
                {
                    if (drill.loop) drillTime -= drill.duration * Mathf.Floor(drillTime / drill.duration);
                    else
                    {
                        // 한 번 하는 장면이 끝나면 리프팅으로
                        currentKey = "idle"; drill = drills[currentKey]; drillTime = 0f;
                    }
                }
                float sx, sy, sz;
                Sample(drill.player, drillTime, player.face, out sx, out sy, out sz, out pose, out face);
                player.x = sx; player.y = sy;
            }
            Resolve(player, pose, face, Distance(prevX, prevY, player.x, player.y), dt);

            // 다른 인물: 훈련에 참여하지 않거나 선수가 이동 중이면 제자리에 서 있습니다.
            UpdateNpc(gk, drill.gk, dt);
            UpdateNpc(attacker, drill.attacker, dt);
            UpdateNpc(mate, drill.mate, dt);

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
            else if (walking && player.pose == "dribble") ballVisible = false;   // 드리블 그림 안에 공이 그려져 있습니다.
            else if (walking || drill.ballFollows)
            {
                float ox, oy;
                FollowOffset(player.face, out ox, out oy);
                ball.x = player.x + ox; ball.y = player.y + oy;
                bool moving = Distance(prevX, prevY, player.x, player.y) > 0.05f;
                ball.z = moving ? Mathf.Abs(Mathf.Sin(clock * 9f)) * 5f : 0f;
            }
            else ballVisible = false;

            // 그리기
            RenderCharacter(gk); RenderCharacter(attacker); RenderCharacter(mate); RenderCharacter(player);
            RenderBall(ballVisible);
            drawOrder.Sort(BySortKey);
            for (int i = 0; i < drawOrder.Count; i++) drawOrder[i].root.SetSiblingIndex(i);
        }

        void UpdateNpc(Actor a, StageKey[] keys, float dt)
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
            Resolve(a, pose, face, Distance(prevX, prevY, x, y), dt);
        }

        // auto는 움직이면 달리기, 멈추면 서 있는 모습으로, carry는 움직이면 공을 몰고, 멈추면 공을 발 앞에 둔 모습으로 정합니다.
        static void Resolve(Actor a, string pose, char face, float moved, float dt)
        {
            bool moving = moved > 0.05f;
            if (pose == "auto") pose = moving ? "walk" : "stand";
            else if (pose == "carry") pose = moving ? "dribble" : "dribbleStill";
            if (moved < 40f) a.walkDist += moved;   // 순간이동한 거리는 달리기 걸음에 넣지 않음
            if (pose != a.pose) a.poseTime = 0f; else a.poseTime += dt;
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
                case 'u': ox = 14f; oy = -15f; break;
                case 'l': ox = -19f; oy = -12f; break;
                case 'r': ox = 19f; oy = -12f; break;
                default: ox = 14f; oy = -6f; break;
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

        // ---------- 그림 고르기 ----------
        // lift: 이 그림을 땅에서 띄워 그릴 높이(선수 그림 픽셀)
        Sprite PickFrame(Actor a, out bool flip, out float lift)
        {
            flip = a.face == 'l';
            lift = 0f;
            string view = a.face == 'd' ? "front" : (a.face == 'u' ? "back" : "side");
            string key;
            int index;
            Sprite[] strip;
            switch (a.pose)
            {
                case "walk":
                    // 보강한 달리기 그림(20장): 다리가 모이는 장(홀수)에서 몸이 떠오릅니다.
                    strip = Find(a.team + "_stride_" + view);
                    if (strip != null)
                    {
                        index = ((int)(a.walkDist / StrideStepPx)) % strip.Length;
                        lift = index % 2 == 1 ? StrideBob : 0f;
                        break;
                    }
                    // 달리기 그림은 다리 움직임이 작아서, 한 걸음(그림 2.5장)마다 몸을 살짝 띄워 발을 딛는 느낌을 줍니다.
                    lift = Mathf.Abs(Mathf.Sin(a.walkDist / (StepPx * 2.5f) * Mathf.PI)) * StepBob;
                    key = a.team + "_run_" + view;
                    strip = Find(key); index = strip == null ? 0 : ((int)(a.walkDist / StepPx)) % strip.Length; break;
                case "dribble":
                    lift = Mathf.Abs(Mathf.Sin(a.walkDist / (StepPx * 2.5f) * Mathf.PI)) * StepBob;
                    key = a.team + "_dribble_" + view;
                    strip = Find(key); index = strip == null ? 0 : ((int)(a.walkDist / StepPx)) % strip.Length; break;
                case "dribbleStill":
                    key = a.team + "_dribble_" + view;
                    strip = Find(key); index = 0; break;
                case "kick":
                    key = a.team + "_shoot_" + view;
                    strip = Find(key); index = strip == null ? 0 : Mathf.Min(strip.Length - 1, (int)(a.poseTime * 10f)); break;
                case "cheer":
                    key = a.team + "_cheer_x"; flip = false;
                    strip = Find(key); index = strip == null ? 0 : ((int)(a.poseTime * 5f)) % strip.Length; break;
                case "juggleR":
                case "juggleL":
                    // 리프팅 중 공을 차는 순간: 0 오른발, 1 왼발 (그림이 없으면 서 있는 그림)
                    strip = Find(a.team + "_juggle_x"); flip = false; key = "";
                    index = a.pose == "juggleL" ? 1 : 0;
                    if (strip == null) { strip = Find(a.team + "_idle_front"); index = ((int)(clock * 4f) + a.phase) % (strip != null ? strip.Length : 1); }
                    break;
                case "jump":
                    // 대성공: 점프 그림 + 몸을 띄움
                    key = a.team + "_jump_x"; flip = false;
                    strip = Find(key); index = strip == null ? 0 : Mathf.Min(strip.Length - 1, (int)(a.poseTime * 5f));
                    lift = Mathf.Sin(Mathf.Clamp01(a.poseTime / 0.6f) * Mathf.PI) * 22f;
                    break;
                case "dive":
                    key = a.team + "_fall_x";
                    strip = Find(key); index = strip == null ? 0 : Mathf.Min(strip.Length - 1, (int)(a.poseTime * 6f)); break;
                default:
                    key = a.team + "_idle_" + view;
                    strip = Find(key); index = strip == null ? 0 : ((int)(clock * 4f) + a.phase) % strip.Length; break;
            }
            if (strip == null) { strip = Find(a.team + "_idle_front"); index = 0; flip = false; }
            return strip == null ? null : strip[Mathf.Clamp(index, 0, strip.Length - 1)];
        }

        Sprite[] Find(string key)
        {
            Sprite[] strip;
            return strips.TryGetValue(key, out strip) ? strip : null;
        }

        void SetShown(Actor a, bool shown)
        {
            if (a.shown == shown) return;
            a.shown = shown;
            a.root.gameObject.SetActive(shown);
        }

        // 훈련장 그림 픽셀 좌표(왼쪽 위가 0,0)로 이미지 위치를 정합니다.
        void Place(RectTransform rect, float left, float top, float w, float h)
        {
            rect.anchorMin = new Vector2(left / bgW, 1f - (top + h) / bgH);
            rect.anchorMax = new Vector2((left + w) / bgW, 1f - top / bgH);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        void RenderCharacter(Actor a)
        {
            bool flip;
            float lift;
            Sprite sprite = PickFrame(a, out flip, out lift);
            if (sprite == null) { SetShown(a, false); return; }
            SetShown(a, true);
            float w = sprite.rect.width * CharScale, h = sprite.rect.height * CharScale;
            a.bodyImage.sprite = sprite;
            a.body.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            Place(a.body, a.x - w / 2f, a.y - (BaseRow + lift) * CharScale, w, h);
            float sw = shadowSprite.rect.width * CharScale, sh = shadowSprite.rect.height * CharScale;
            Place(a.shadow, a.x - sw / 2f, a.y - sh * 0.62f, sw, sh);
            a.sortKey = a.y;
        }

        void RenderBall(bool visible)
        {
            SetShown(ball, visible);
            if (!visible) return;
            // 공 그림 크기와 상관없이, 선수 그림(드리블 동작) 속 공과 같은 지름으로 그립니다.
            float aspect = ballSprite.rect.height / ballSprite.rect.width;
            float w = BallDiameter * CharScale, h = BallDiameter * aspect * CharScale;
            Place(ball.body, ball.x - w / 2f, ball.y - ball.z - h / 2f, w, h);
            float sw = shadowSprite.rect.width * CharScale * 0.5f, sh = shadowSprite.rect.height * CharScale * 0.5f;
            Place(ball.shadow, ball.x - sw / 2f, ball.y - sh * 0.3f, sw, sh);
            ball.sortKey = ball.y + 0.01f;
        }
    }
}
