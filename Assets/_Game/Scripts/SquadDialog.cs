using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 선수단 창의 내용: 팀 요약 카드, 주전(4-4-2)과 교체 표.
    // 한 줄 = 포지션 배지(색) · 이름 · 나이 · 능력(숫자+막대) · 이번 시즌 기록. 내 줄은 금색.
    public static class SquadDialog
    {
        static readonly Color GkColor = new Color(.85f, .65f, .15f, 1f);
        static readonly Color DfColor = new Color(.30f, .50f, .90f, 1f);
        static readonly Color MfColor = new Color(.25f, .65f, .40f, 1f);
        static readonly Color FwColor = new Color(.85f, .35f, .35f, 1f);
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f);
        static readonly Color RowB = new Color(.10f, .14f, .22f, 1f);
        static readonly Color MeRow = new Color(.42f, .33f, .10f, 1f);
        static readonly Color Card = new Color(.13f, .18f, .28f, 1f);

        static Color RoleColor(SquadRole r) { return r == SquadRole.GK ? GkColor : r == SquadRole.DF ? DfColor : r == SquadRole.MF ? MfColor : FwColor; }

        public static void Build(Transform parent, Font font, SquadView v)
        {
            // 팀 요약 카드: 리그 + 숫자 4개
            var card = Block(parent, 74);
            card.gameObject.AddComponent<Image>().color = Card;
            var h = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 8, 8); h.spacing = 6;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            Stat(card, font, string.IsNullOrEmpty(v.division) ? "-" : v.division, "리그", true);
            Stat(card, font, v.strength.ToString("0"), "팀 전력", false);
            Stat(card, font, v.required.ToString("0"), "요구 평점", false);
            Stat(card, font, v.starterAvg.ToString("0"), "주전 평균", false);
            Stat(card, font, v.avgAge.ToString("0.0"), "평균 나이", false);

            Header(parent, font, "주전  4-4-2", true);
            for (int i = 0; i < v.starters.Count; i++) Row(parent, font, v.starters[i], i, true);
            Header(parent, font, "교체", false);
            for (int i = 0; i < v.subs.Count; i++) Row(parent, font, v.subs[i], i, false);
        }

        static void Stat(Transform parent, Font font, string value, string label, bool small)
        {
            var box = NewRect("Stat", parent);
            box.gameObject.AddComponent<Image>().color = RowB;
            Fit(Label(box, font, value, small ? 18 : 24, small ? PixelTheme.Accent : PixelTheme.Text, 0f, .4f, 1f, 1f, TextAnchor.MiddleCenter), 12);
            Label(box, font, label, 14, PixelTheme.TextDim, 0f, 0f, 1f, .42f, TextAnchor.MiddleCenter);
        }

        static void Header(Transform parent, Font font, string title, bool withColumns)
        {
            var row = Block(parent, 30);
            Label(row, font, title, 20, PixelTheme.Accent, .01f, 0f, .5f, 1f, TextAnchor.MiddleLeft);
            Label(row, font, "나이", 14, PixelTheme.TextDim, .52f, 0f, .62f, 1f, TextAnchor.MiddleCenter);
            Label(row, font, "능력", 14, PixelTheme.TextDim, .62f, 0f, .78f, 1f, TextAnchor.MiddleCenter);
            if (withColumns) Label(row, font, "골 · 도움 · 평점", 14, PixelTheme.TextDim, .78f, 0f, .99f, 1f, TextAnchor.MiddleRight);
        }

        static void Row(Transform parent, Font font, SquadRowView r, int index, bool starter)
        {
            var row = Block(parent, 38);
            row.gameObject.AddComponent<Image>().color = r.me ? MeRow : (index % 2 == 0 ? RowA : RowB);
            // 포지션 배지
            var badge = Img(row, RoleColor(r.role), .01f, .14f, .09f, .86f);
            Label(badge.rectTransform, font, r.role.ToString(), 15, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
            // 이름 (+ 표시)
            string name = r.name + (string.IsNullOrEmpty(r.tag) ? "" : r.tag == "나" ? "  ★" : "  · " + r.tag);
            Fit(Label(row, font, name, 19, r.me ? Color.white : (starter ? PixelTheme.Text : PixelTheme.TextDim), .11f, 0f, .52f, 1f, TextAnchor.MiddleLeft), 13);
            // 나이
            Label(row, font, r.age > 0 ? r.age + "세" : "-", 17, PixelTheme.TextDim, .52f, 0f, .62f, 1f, TextAnchor.MiddleCenter);
            // 능력: 숫자 + 막대
            Label(row, font, r.rating.ToString("0"), 19, r.me ? PixelTheme.Accent : PixelTheme.Text, .62f, 0f, .69f, 1f, TextAnchor.MiddleRight);
            Img(row, new Color(.04f, .06f, .10f, 1f), .70f, .38f, .77f, .62f);
            Img(row, RoleColor(r.role), .70f, .38f, .70f + .07f * Mathf.Clamp01(r.rating / 100f), .62f);
            // 이번 시즌 기록 (주전만)
            if (starter)
            {
                string line = r.line == null ? "-" : r.line.goals + " · " + r.line.assists + " · " + (r.line.apps > 0 ? r.line.avg.ToString("0.0") : "-");
                Label(row, font, line, 17, r.me ? PixelTheme.Accent : PixelTheme.Text, .78f, 0f, .99f, 1f, TextAnchor.MiddleRight);
            }
        }

        // ---------- 공통 ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static RectTransform Block(Transform parent, float height)
        {
            var r = NewRect("Block", parent);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height;
            return r;
        }

        static Image Img(Transform parent, Color c, float x0, float y0, float x1, float y1)
        {
            var r = NewRect("Image", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        static Text Label(Transform parent, Font font, string value, int size, Color color, float x0, float y0, float x1, float y1, TextAnchor align)
        {
            var r = NewRect("Text", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value ?? ""; t.color = color; t.alignment = align; t.raycastTarget = false;
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
