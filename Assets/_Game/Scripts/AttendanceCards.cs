using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 출석 보상 7일 카드 한 줄. 카드마다 날짜·아이콘·보상을 보여 주고 상태에 따라 모양이 바뀝니다.
    //  받은 날: 어둡게 + 초록 "받음" 도장
    //  오늘 받을 날: 금색 테두리, 숨쉬듯 커졌다 작아짐
    //  남은 날: 기본 색 (7일차는 보라 테두리 + 선물 상자)
    // CareerPrototypeUI가 만들고 매 프레임 Tick을 부릅니다.
    public class AttendanceCards
    {
        class Card
        {
            public RectTransform root;
            public Image edge, fill, icon;
            public Text day, reward, stamp;
        }

        static readonly Color ClaimedFill = new Color(.08f, .11f, .17f, 1f);
        static readonly Color StampColor = new Color(.45f, .95f, .55f, 1f);
        static readonly Color Day7Edge = new Color(.78f, .55f, .90f, 1f);
        static readonly Color Day7Fill = new Color(.30f, .18f, .40f, 1f);

        readonly Card[] cards = new Card[7];
        readonly Font font;
        static Sprite coinSprite, giftSprite;
        int pulseIndex = -1;
        float t;

        public AttendanceCards(Font font) { this.font = font; }

        public RectTransform Build(Transform parent)
        {
            var row = NewRect("AttendanceRow", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            var le = row.gameObject.AddComponent<LayoutElement>(); le.minHeight = 132; le.preferredHeight = 132;
            for (int i = 0; i < 7; i++) cards[i] = MakeCard(row, i);
            return row;
        }

        Card MakeCard(Transform parent, int i)
        {
            var c = new Card();
            // 카드 바깥(테두리) → 안쪽(채움) → 날짜 / 아이콘 / 보상, 맨 위에 도장
            var holder = NewRect("Day" + (i + 1), parent);
            c.edge = holder.gameObject.AddComponent<Image>();
            c.root = NewRect("Card", holder); Stretch(c.root, 0f);   // 숨쉬기 크기 조절용
            var edgeInner = NewRect("Edge", c.root); Stretch(edgeInner, 0f);
            c.edge.color = Color.clear;
            var edgeImg = edgeInner.gameObject.AddComponent<Image>();
            c.edge = edgeImg;
            var fillRect = NewRect("Fill", c.root); Stretch(fillRect, 4f);
            c.fill = fillRect.gameObject.AddComponent<Image>();

            c.day = NewText(c.root, (i + 1) + "일", 20, new Vector2(0f, .76f), new Vector2(1f, .98f));
            var iconRect = NewRect("Icon", c.root);
            iconRect.anchorMin = new Vector2(.5f, .5f); iconRect.anchorMax = new Vector2(.5f, .5f);
            iconRect.sizeDelta = i == 6 ? new Vector2(44f, 44f) : new Vector2(34f, 34f);
            iconRect.anchoredPosition = new Vector2(0f, 8f);
            c.icon = iconRect.gameObject.AddComponent<Image>(); c.icon.raycastTarget = false;
            c.icon.sprite = i == 6 ? GiftSprite() : CoinSprite();
            c.reward = NewText(c.root, "", 15, new Vector2(0f, .03f), new Vector2(1f, .32f));

            c.stamp = NewText(c.root, "받음", 22, new Vector2(0f, .25f), new Vector2(1f, .75f));
            c.stamp.color = StampColor;
            var o = c.stamp.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, .2f, .05f, 1f); o.effectDistance = new Vector2(2, -2);
            c.stamp.rectTransform.localEulerAngles = new Vector3(0, 0, 14f);
            return c;
        }

        // claimed: 이번 7일 중 받은 날 수, today: 오늘 받을 날(1~7, 없으면 0), rewards: 카드에 쓸 보상 (7개)
        public void Set(int claimed, int today, string[] rewards)
        {
            pulseIndex = today - 1;
            for (int i = 0; i < 7; i++)
            {
                var c = cards[i];
                if (c == null) return;
                bool got = i < claimed, isToday = i == today - 1, day7 = i == 6;
                c.reward.text = rewards != null && i < rewards.Length ? rewards[i] : "";
                c.stamp.gameObject.SetActive(got);
                c.edge.color = isToday ? PixelTheme.Accent : got ? PixelTheme.QuietEdge : day7 ? Day7Edge : PixelTheme.PanelEdge;
                c.fill.color = got ? ClaimedFill : isToday ? PixelTheme.AccentFill : day7 ? Day7Fill : PixelTheme.QuietFill;
                float dim = got ? 0.35f : 1f;
                c.icon.color = new Color(1f, 1f, 1f, dim);
                c.day.color = isToday ? Color.white : got ? PixelTheme.TextDim : day7 ? Day7Edge : PixelTheme.Text;
                c.reward.color = got ? new Color(.5f, .55f, .65f, 1f) : PixelTheme.Text;
                if (!isToday) c.root.localScale = Vector3.one;
            }
        }

        public void Tick(float dt)
        {
            t += dt;
            if (pulseIndex < 0 || pulseIndex >= 7 || cards[pulseIndex] == null || cards[pulseIndex].root == null) return;
            float s = 1f + 0.05f * (0.5f + 0.5f * Mathf.Sin(t * 5f));
            cards[pulseIndex].root.localScale = new Vector3(s, s, 1f);
            var e = cards[pulseIndex].edge; var col = PixelTheme.Accent;
            e.color = Color.Lerp(col, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 5f)));
        }

        // ---------- 공통 ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        Text NewText(Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var r = NewRect("Text", parent);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.alignment = TextAnchor.MiddleCenter; t.color = PixelTheme.Text;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ---------- 아이콘 (코드로 그린 픽셀 그림) ----------
        static Sprite Make(string[] rows, System.Func<char, Color32> palette)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[(h - 1 - y) * w + x] = palette(rows[y][x]);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 10f);
        }

        static Sprite CoinSprite()   // 금화
        {
            if (coinSprite != null) return coinSprite;
            coinSprite = Make(new[] {
                "....kkkkkk....",
                "..kkyyyyyykk..",
                ".kyywwyyyyyyk.",
                ".kywyyooooyyk.",
                "kywyyoyyyyoyyk",
                "kyyyoyyyyyyoyk",
                "kyyyoyyyyyyoyk",
                "kyyyoyyyyyyoyk",
                "kyyyoyyyyyyoyk",
                "kyyyyoyyyyoyyk",
                ".kyyyyooooyyk.",
                ".kyyyyyyyyyok.",
                "..kkoyyyyookk.",
                "....kkkkkk....",
            }, ch => ch == 'k' ? new Color32(70, 40, 10, 255) : ch == 'y' ? new Color32(250, 205, 70, 255)
                   : ch == 'o' ? new Color32(205, 140, 30, 255) : ch == 'w' ? new Color32(255, 250, 210, 255) : new Color32(0, 0, 0, 0));
            return coinSprite;
        }

        static Sprite GiftSprite()   // 선물 상자 (7일차)
        {
            if (giftSprite != null) return giftSprite;
            giftSprite = Make(new[] {
                "...yy....yy...",
                "..y..y..y..y..",
                "..y...yy...y..",
                "...yyyyyyyy...",
                "kkkkkkyykkkkkk",
                "krrrrryyrrrrrk",
                "kRRRRRyyRRRRRk",
                "kkkkkkyykkkkkk",
                ".krrrryyrrrrk.",
                ".krrrryyrrrrk.",
                ".kRrrryyrrrrk.",
                ".kRrrryyrrrrk.",
                ".kRRRRyyRRRRk.",
                ".kkkkkkkkkkkk.",
            }, ch => ch == 'k' ? new Color32(60, 15, 20, 255) : ch == 'r' ? new Color32(220, 60, 70, 255)
                   : ch == 'R' ? new Color32(160, 35, 50, 255) : ch == 'y' ? new Color32(250, 205, 70, 255) : new Color32(0, 0, 0, 0));
            return giftSprite;
        }
    }
}
