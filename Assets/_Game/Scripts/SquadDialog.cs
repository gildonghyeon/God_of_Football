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

        public static Color RoleColor(SquadRole r) { return r == SquadRole.GK ? GkColor : r == SquadRole.DF ? DfColor : r == SquadRole.MF ? MfColor : FwColor; }

        // onTap: 선수 줄을 누르면 (세부 능력치 보기)
        public static void Build(Transform parent, Font font, SquadView v, System.Action<SquadRowView> onTap = null)
        {
            Summary(parent, font, v);
            // 전술: 포메이션 · 스타일 + 설명, 그리고 포메이션 그림
            if (v.tactic != null)
            {
                var t = Block(parent, 30);
                Label(t, font, TacticSystem.Describe(v.tactic), 20, PixelTheme.Accent, .01f, 0f, .99f, 1f, TextAnchor.MiddleLeft);
                if (v.layout != null) Pitch(parent, font, v, onTap);
            }
            Header(parent, font, "1군  " + (v.tactic != null ? TacticSystem.FormationName(v.tactic.formation) : ""), true);
            Grid(parent, font, v.starters, true, onTap);
            Header(parent, font, "교체", false);
            Grid(parent, font, v.subs, false, onTap);
            if (onTap != null) { var hint = Block(parent, 28); Label(hint, font, "포메이션 그림이나 목록에서 선수를 누르면 세부 능력치를 볼 수 있습니다", 15, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter); }
        }

        // 팀 요약 카드(리그·팀 전력·요구 평점·1군 평균·평균 나이)와 유니폼 (선수단 창, 나만의 팀 팀관리/전술)
        public static void Summary(Transform parent, Font font, SquadView v)
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
            Stat(card, font, v.starterAvg.ToString("0"), "1군 평균", false);
            Stat(card, font, v.avgAge.ToString("0.0"), "평균 나이", false);

            // 유니폼: 홈·원정 두 벌
            if (!string.IsNullOrEmpty(v.teamId) && TeamKits.Home(v.teamId) > 0)
            {
                var kit = Block(parent, 128);
                kit.gameObject.AddComponent<Image>().color = Card;
                TeamKits.PreviewImage(kit, TeamKits.Home(v.teamId), .02f, .16f, .20f, .98f);
                TeamKits.PreviewImage(kit, TeamKits.Away(v.teamId), .22f, .16f, .40f, .98f);
                Label(kit, font, "홈", 15, PixelTheme.TextDim, .02f, 0f, .20f, .17f, TextAnchor.MiddleCenter);
                Label(kit, font, "원정", 15, PixelTheme.TextDim, .22f, 0f, .40f, .17f, TextAnchor.MiddleCenter);
                Label(kit, font, "유니폼", 15, PixelTheme.TextDim, .44f, .56f, .98f, .86f, TextAnchor.MiddleLeft);
                Fit(Label(kit, font, TeamKits.Name(v.teamId), 22, PixelTheme.Accent, .44f, .18f, .98f, .58f, TextAnchor.MiddleLeft), 13);
            }
        }

        // 포메이션 그림: 잔디 위에 11자리 점과 이름 (아래가 우리 골대, 위로 공격)
        // 실제 경기장 비율에 가깝게, 가운데에 세로로 긴 판으로 그립니다. (우리 골대부터 상대 진영 일부까지)
        const float PitchW = 420f, PitchH = 440f;
        static float PitchY(float depth) { return .035f + depth * 1.25f; }   // 깊이(0=우리 골라인) -> 판 높이 비율
        // mark: 0 없음, 1 처음 고른 선수(금색), 2 두 번째 고른 선수(하늘색). home: 선수 본래 포지션 (자리와 다르면 빨간 테두리 + 이름 왼쪽에 본래 포지션)
        static readonly Color PickGold = new Color(.98f, .80f, .28f, 1f), PickBlue = new Color(.56f, .82f, 1f, 1f), OffRed = new Color(.95f, .38f, .38f, 1f);
        public static void Pitch(Transform parent, Font font, SquadView v, System.Action<SquadRowView> onTap,
                                 System.Func<SquadRowView, int> mark = null, System.Func<SquadRowView, SquadRole> home = null, bool decimals = false)
        {
            var block = Block(parent, PitchH + 8f);
            var box = NewRect("Pitch", block);
            box.anchorMin = box.anchorMax = new Vector2(.5f, .5f); box.pivot = new Vector2(.5f, .5f);
            box.sizeDelta = new Vector2(PitchW, PitchH);
            box.gameObject.AddComponent<Image>().color = new Color(.16f, .38f, .22f, 1f);
            for (int b = 0; b < 8; b += 2) Img(box, new Color(.18f, .42f, .24f, 1f), 0f, b / 8f, 1f, (b + 1) / 8f);   // 잔디 줄무늬
            var line = new Color(1f, 1f, 1f, .28f);
            float half = PitchY(.5f);
            Img(box, line, 0f, half - .003f, 1f, half + .003f);                                   // 하프라인
            Img(box, line, .48f, half - .008f, .52f, half + .008f);                               // 센터 스팟
            Frame(box, line, .204f, PitchY(0f), .796f, PitchY(16.5f / 105f));                     // 페널티 박스
            Frame(box, line, .365f, PitchY(0f), .635f, PitchY(5.5f / 105f));                      // 골 에어리어
            Img(box, new Color(1f, 1f, 1f, .5f), .446f, 0f, .554f, PitchY(0f));                   // 골대
            for (int i = 0; i < v.layout.Length && i < v.starters.Count; i++)
            {
                var s = v.layout[i]; var r = v.starters[i];
                float x = s.x / 68f, y = Mathf.Clamp(PitchY(s.depth), .08f, .93f);
                int m = mark != null ? mark(r) : 0;
                SquadRole own = home != null ? home(r) : s.role;
                bool wrong = own != s.role;
                Color pick = m == 1 ? PickGold : PickBlue;
                if (m > 0)   // 고른 선수: 점 둘레에 테두리
                {
                    Point(box, pick, x, y, 44f, 44f);
                }
                else if (wrong) Point(box, OffRed, x, y, 40f, 40f);   // 맞지 않는 자리: 빨간 테두리
                float size = m > 0 ? 38f : 34f;
                var dotColor = RoleColor(s.role);   // 내 선수도 다른 선수처럼 포지션 색
                var dot = Point(box, dotColor, x, y, size, size);
                Label(dot.rectTransform, font, r.number > 0 ? r.number.ToString() : s.role.ToString(), r.number > 0 ? 16 : 13, Color.white, -.5f, 0f, 1.5f, 1f, TextAnchor.MiddleCenter);   // 등번호 (모르면 포지션)
                string surname = r.name; int dotIdx = surname.IndexOf(". "); if (dotIdx >= 0) surname = surname.Substring(dotIdx + 2);
                var name = Point(box, m > 0 ? pick : new Color(0f, 0f, 0f, .45f), x, y - 36f / PitchH, 136f, 22f);   // 본래 포지션 + 이름 + 능력 (고르면 이름표도 같은 색)
                // 이름 왼쪽: 선수 본래 포지션 (포지션 색 글자). 고른 이름표(밝은 바탕) 위에서는 어두운 글자
                string homeText = "<color=#" + (m > 0 ? "10141E" : ColorUtility.ToHtmlStringRGB(RoleColor(own))) + ">" + own + "</color> ";
                string rv = decimals ? r.rating.ToString("0.0") : Mathf.RoundToInt(r.rating).ToString();   // 베스트 11은 평균 평점 (소수 한 자리)
                string ability = m > 0 ? "<color=#10141E>" + rv + "</color>"
                               : wrong ? "<color=#F26060>" + rv + "</color>"
                               : "<color=#FBCC47>" + rv + "</color>";
                var nameColor = m > 0 ? new Color(.06f, .08f, .12f, 1f) : Color.white;
                Fit(Label(name.rectTransform, font, homeText + surname + " " + ability, 15, nameColor, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter), 10);
                if (m > 0)   // 고른 순서 (1, 2) 표시
                {
                    var tag = Point(box, new Color(.06f, .08f, .12f, 1f), x + 22f / PitchW, y + 20f / PitchH, 20f, 20f);
                    Label(tag.rectTransform, font, m.ToString(), 13, pick, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                }
                if (onTap != null)   // 점과 이름을 누르면 그 선수의 세부 능력치
                {
                    // 완전히 투명한 터치 칸 (색 전환 없음: 화면이 다시 그려지거나 잠시 비활성이 돼도 흰 칸이 비치지 않음)
                    var hit = Point(box, new Color(0f, 0f, 0f, 0f), x, y - 14f / PitchH, 112f, 74f);   // 손가락으로 누르기 쉽게 넉넉한 칸
                    hit.raycastTarget = true;
                    var button = hit.gameObject.AddComponent<Button>();
                    button.targetGraphic = hit;
                    button.transition = Selectable.Transition.None;
                    var row = r; button.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); onTap(row); });
                }
            }
        }

        // 판 위 한 점(x, y 비율)에 고정 크기(픽셀)로 놓는 칸
        static Image Point(Transform parent, Color c, float x, float y, float w, float h)
        {
            var r = NewRect("Point", parent);
            r.anchorMin = r.anchorMax = new Vector2(x, y); r.pivot = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(w, h);
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        // 테두리만 있는 네모 (선 두께 3픽셀)
        static void Frame(Transform parent, Color c, float x0, float y0, float x1, float y1)
        {
            const float T = 3f;
            Edge(parent, c, x0, y0, x1, y0, new Vector2(0f, T)); Edge(parent, c, x0, y1, x1, y1, new Vector2(0f, T));
            Edge(parent, c, x0, y0, x0, y1, new Vector2(T, 0f)); Edge(parent, c, x1, y0, x1, y1, new Vector2(T, 0f));
        }

        static void Edge(Transform parent, Color c, float x0, float y0, float x1, float y1, Vector2 size)
        {
            var r = NewRect("Line", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size; r.anchoredPosition = Vector2.zero;
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
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
            Label(row, font, title, 20, PixelTheme.Accent, .01f, 0f, .7f, 1f, TextAnchor.MiddleLeft);
            Label(row, font, "이름 · 능력", 14, PixelTheme.TextDim, .7f, 0f, .99f, 1f, TextAnchor.MiddleRight);   // 나이·시즌 기록은 선수를 누르면
        }

        // 선수 목록을 두 줄(두 칸씩)로: 한 줄에 선수 둘
        // 위에서부터 공격수 → 미드필더 → 수비수 → 골키퍼 (같은 포지션 안에서는 원래 순서)
        static void Grid(Transform parent, Font font, System.Collections.Generic.List<SquadRowView> source, bool starter, System.Action<SquadRowView> onTap)
        {
            var list = new System.Collections.Generic.List<SquadRowView>();
            for (int role = (int)SquadRole.FW; role >= (int)SquadRole.GK; role--)
                foreach (var r in source) if ((int)r.role == role) list.Add(r);
            for (int i = 0; i < list.Count; i += 2)
            {
                var line = Block(parent, 40);
                for (int k = 0; k < 2 && i + k < list.Count; k++)
                {
                    var cell = NewRect("Cell", line);
                    cell.anchorMin = new Vector2(k == 0 ? 0f : .505f, 0f); cell.anchorMax = new Vector2(k == 0 ? .495f : 1f, 1f);
                    cell.offsetMin = cell.offsetMax = Vector2.zero;
                    Row(cell, font, list[i + k], (i / 2 + k) % 2, starter, onTap);
                }
            }
        }

        // 선수 한 칸: 포지션 배지 · 이름 · 능력(숫자+막대). 나이·시즌 기록은 누르면 나오는 세부 창에서
        static void Row(RectTransform row, Font font, SquadRowView r, int index, bool starter, System.Action<SquadRowView> onTap)
        {
            var bg = row.gameObject.AddComponent<Image>(); bg.color = r.me ? MeRow : (index % 2 == 0 ? RowA : RowB);
            if (onTap != null)
            {
                var b = row.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
                var c = b.colors; c.pressedColor = new Color(.65f, .65f, .65f, 1f); c.fadeDuration = .06f; b.colors = c;   // 누르면 어두워짐
                b.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); onTap(r); });
            }
            // 포지션 배지
            var badge = Img(row, RoleColor(r.role), .02f, .16f, .2f, .84f);
            Label(badge.rectTransform, font, r.role.ToString(), 14, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);   // 포지션은 FW·MF·DF·GK로 통일
            // 이름 (+ 표시)
            string name = r.name + (r.legend ? " ★" : "") + (string.IsNullOrEmpty(r.tag) ? "" : r.tag == "나" ? " ★" : " · " + r.tag);
            Fit(Label(row, font, name, 17, r.me ? Color.white : (starter ? PixelTheme.Text : PixelTheme.TextDim), .23f, 0f, .74f, 1f, TextAnchor.MiddleLeft), 11);
            // 능력: 숫자 + 막대
            Label(row, font, r.rating.ToString("0"), 19, r.me ? PixelTheme.Accent : PixelTheme.Text, .74f, 0f, .97f, 1f, TextAnchor.MiddleRight);
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
