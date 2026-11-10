using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 시즌 끝 트로피 수여 연출. 받은 트로피를 한 개씩 보여 줍니다.
    // 트로피가 커지며 나타나고(팡파레), 뒤에서 빛이 돌고, 별 조각이 퍼집니다. 화면이나 버튼을 누르면 다음 트로피로.
    // CareerPrototypeUI가 만들고 매 프레임 Tick을 부릅니다.
    public class TrophyCeremony
    {
        const float PopTime = 0.45f;
        readonly Font font;
        readonly RectTransform root;
        readonly Dictionary<TrophyKind, Sprite> icons = new Dictionary<TrophyKind, Sprite>();

        GameObject overlay;
        RectTransform trophyRect, glowRect;
        Image glow;
        Text title, kindText, detailText, counterText, buttonText;
        readonly List<RectTransform> sparks = new List<RectTransform>();
        readonly List<Vector2> sparkVel = new List<Vector2>();
        List<TrophyRecord> list;
        Action onDone;
        int index;
        float t;

        public bool Active => overlay != null;

        public TrophyCeremony(Font font, RectTransform root) { this.font = font; this.root = root; }

        public void Show(List<TrophyRecord> trophies, string heading, Action done)
        {
            if (trophies == null || trophies.Count == 0) { done?.Invoke(); return; }
            Close();
            list = trophies; onDone = done; index = 0;
            Build(heading);
            Reveal();
        }

        public void Tick(float dt)
        {
            if (overlay == null) return;
            t += dt;
            // 트로피: 0 → 1.25 → 1 로 튀어나오고, 그 뒤 살짝 둥실
            float s = t < PopTime ? Pop(t / PopTime) : 1f + 0.03f * Mathf.Sin((t - PopTime) * 3f);
            trophyRect.localScale = new Vector3(s, s, 1f);
            trophyRect.anchoredPosition = new Vector2(0f, 80f + (t > PopTime ? 6f * Mathf.Sin((t - PopTime) * 2f) : 0f));
            // 뒤의 빛: 돌면서 숨쉬기
            glowRect.localEulerAngles = new Vector3(0, 0, t * 25f);
            float g = Mathf.Clamp01(t / PopTime) * (0.55f + 0.2f * Mathf.Sin(t * 4f));
            var c = glow.color; c.a = g; glow.color = c;
            // 글자는 트로피가 나온 뒤 차례로
            SetAlpha(kindText, Mathf.Clamp01((t - 0.25f) / 0.25f));
            SetAlpha(detailText, Mathf.Clamp01((t - 0.4f) / 0.25f));
            // 별 조각
            for (int i = 0; i < sparks.Count; i++)
            {
                var r = sparks[i];
                if (r == null) continue;
                sparkVel[i] += new Vector2(0f, -260f) * dt;
                r.anchoredPosition += sparkVel[i] * dt;
                var img = r.GetComponent<Image>();
                var sc = img.color; sc.a = Mathf.Clamp01(1.2f - t / 1.2f); img.color = sc;
            }
        }

        static float Pop(float k)   // 0→1.25→1
        {
            return k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
        }

        static void SetAlpha(Text text, float a) { var c = text.color; c.a = a; text.color = c; }

        void Next()
        {
            if (t < 0.3f) return;   // 너무 빨리 넘기지 않게
            SoundFx.Play(Sfx.Click);
            index++;
            if (index >= list.Count) { var done = onDone; Close(); done?.Invoke(); return; }
            Reveal();
        }

        void Reveal()
        {
            var tr = list[index];
            t = 0f;
            trophyRect.GetComponent<Image>().sprite = Icon(tr.kind);
            kindText.text = AwardSystem.KindName(tr.kind).ToUpperInvariant();
            kindText.color = KindColor(tr.kind);
            detailText.text = tr.detail + (tr.division > 0 ? "   Division " + tr.division : "") + "\n" + tr.teamName;
            counterText.text = (index + 1) + " / " + list.Count;
            buttonText.text = index + 1 < list.Count ? "NEXT" : "CONTINUE";
            foreach (var r in sparks) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            sparks.Clear(); sparkVel.Clear();
            var rng = new System.Random(index * 31 + 5);
            for (int i = 0; i < 18; i++)
            {
                var img = NewImage(overlay.transform, i % 3 == 0 ? Color.white : KindColor(tr.kind));
                var r = img.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
                float size = 8f + rng.Next(0, 3) * 4f;
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = new Vector2(0f, 80f);
                float ang = (float)(rng.NextDouble() * Math.PI * 2), spd = 220f + (float)rng.NextDouble() * 260f;
                sparks.Add(r); sparkVel.Add(new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd + 120f));
            }
            SoundFx.Play(Sfx.Trophy);
            Tick(0f);
        }

        void Close()
        {
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            overlay = null; sparks.Clear(); sparkVel.Clear();
        }

        // ---------- 화면 ----------
        void Build(string heading)
        {
            var bg = NewImage(root, new Color(0.02f, 0.03f, 0.06f, 0.92f));
            overlay = bg.gameObject;
            var r = bg.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            r.SetAsLastSibling();
            bg.raycastTarget = true;
            var tap = overlay.AddComponent<Button>(); tap.transition = Selectable.Transition.None; tap.onClick.AddListener(Next);

            title = NewText(overlay.transform, heading, 34, PixelTheme.Accent, new Vector2(0f, 420f), new Vector2(680f, 60f));

            glow = NewImage(overlay.transform, new Color(1f, .85f, .35f, 0f));
            glow.sprite = GlowSprite();
            glowRect = glow.rectTransform; glowRect.anchorMin = glowRect.anchorMax = new Vector2(.5f, .5f);
            glowRect.sizeDelta = new Vector2(460f, 460f); glowRect.anchoredPosition = new Vector2(0f, 80f);

            var trophy = NewImage(overlay.transform, Color.white);
            trophyRect = trophy.rectTransform; trophyRect.anchorMin = trophyRect.anchorMax = new Vector2(.5f, .5f);
            trophyRect.sizeDelta = new Vector2(240f, 280f);
            trophy.preserveAspect = true;

            kindText = NewText(overlay.transform, "", 40, PixelTheme.Accent, new Vector2(0f, -150f), new Vector2(680f, 60f));
            var o = kindText.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.25f, .15f, 0f, 1f); o.effectDistance = new Vector2(3, -3);
            detailText = NewText(overlay.transform, "", 24, PixelTheme.Text, new Vector2(0f, -225f), new Vector2(680f, 70f));
            counterText = NewText(overlay.transform, "", 22, PixelTheme.TextDim, new Vector2(0f, -300f), new Vector2(680f, 34f));

            // 버튼 (화면 아무 곳이나 눌러도 넘어감)
            var edge = NewImage(overlay.transform, PixelTheme.Accent);
            var er = edge.rectTransform; er.anchorMin = er.anchorMax = new Vector2(.5f, .5f);
            er.sizeDelta = new Vector2(320f, 76f); er.anchoredPosition = new Vector2(0f, -390f);
            var fill = NewImage(er, PixelTheme.AccentFill);
            var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(-4, -4);
            var b = edge.gameObject.AddComponent<Button>(); b.targetGraphic = fill; b.onClick.AddListener(Next);
            buttonText = NewText(fr, "NEXT", 28, PixelTheme.Text, Vector2.zero, Vector2.zero);
            var btr = buttonText.rectTransform; btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one; btr.offsetMin = btr.offsetMax = Vector2.zero;
        }

        Text NewText(Transform parent, string value, int size, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = box; r.anchoredPosition = pos;
            return t;
        }

        static Image NewImage(Transform parent, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        // ---------- 그림 (코드로 그린 픽셀 트로피) ----------
        static Color KindColor(TrophyKind kind)
        {
            switch (kind)
            {
                case TrophyKind.LeagueTitle: return new Color(1f, .82f, .25f);       // 금
                case TrophyKind.PlayerOfTheYear: return new Color(1f, .95f, .6f);    // 백금빛 금
                case TrophyKind.Promotion: return new Color(.82f, .86f, .92f);       // 은
                case TrophyKind.BestEleven: return new Color(.55f, .8f, 1f);         // 파랑
                default: return new Color(.86f, .56f, .3f);                         // 동 (득점·도움·수비왕)
            }
        }

        Sprite Icon(TrophyKind kind)
        {
            if (icons.TryGetValue(kind, out var s) && s != null) return s;
            const int W = 24, H = 28;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color baseC = KindColor(kind);
            Color32 mid = baseC, dark = baseC * 0.62f, light = Color.Lerp(baseC, Color.white, 0.55f), outline = new Color(.12f, .08f, .04f, 1f);
            dark.a = 255;
            // 아래부터 위로 (y=0이 아래). 받침 → 기둥 → 잔 → 손잡이
            void Fill(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }
            Fill(5, 1, 18, 3, outline); Fill(6, 2, 17, 2, dark);                   // 받침
            Fill(7, 4, 16, 5, outline); Fill(8, 4, 15, 4, mid);                    // 받침 윗단
            Fill(10, 6, 13, 10, outline); Fill(11, 6, 12, 10, mid);                // 기둥
            Fill(4, 11, 19, 25, outline);                                           // 잔 외곽
            Fill(5, 12, 18, 24, mid);
            Fill(5, 12, 6, 24, dark); Fill(17, 12, 18, 24, dark);                   // 그늘
            Fill(8, 15, 9, 23, light);                                              // 반사광
            Fill(5, 11, 18, 11, outline); Fill(6, 12, 17, 12, dark);               // 잔 아래 둥글게
            Fill(3, 25, 20, 26, outline); Fill(4, 25, 19, 25, light);              // 테두리
            Fill(0, 15, 3, 23, outline); Fill(1, 16, 3, 22, new Color32(0, 0, 0, 0)); // 왼쪽 손잡이
            Fill(20, 15, 23, 23, outline); Fill(20, 16, 22, 22, new Color32(0, 0, 0, 0));
            Fill(1, 16, 1, 22, mid); Fill(22, 16, 22, 22, mid);
            if (kind == TrophyKind.PlayerOfTheYear) { Fill(11, 17, 12, 21, Color.white); Fill(9, 19, 14, 19, Color.white); }   // 별 모양
            else if (kind == TrophyKind.LeagueTitle) { Fill(12, 16, 13, 22, light); Fill(11, 18, 14, 20, light); }   // 가운데 문양
            tex.SetPixels32(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(.5f, .5f), 10f);
            icons[kind] = s;
            return s;
        }

        static Sprite glowSprite;
        static Sprite GlowSprite()   // 가운데가 밝은 둥근 빛 + 빛줄기
        {
            if (glowSprite != null) return glowSprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float rays = 0.5f + 0.5f * Mathf.Cos(ang * 8f);
                    float a = Mathf.Clamp01(1f - d) * (0.45f + 0.55f * rays * rays);
                    px[y * N + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels32(px); tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 64f);
            return glowSprite;
        }
    }
}
