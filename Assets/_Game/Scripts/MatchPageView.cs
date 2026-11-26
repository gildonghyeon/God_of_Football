using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // ---------- 경기 탭에 보여 줄 데이터 (규칙 쪽에서 만들어 넘김) ----------
    public class NextMatchView
    {
        public bool done;                 // 시즌이 끝났으면 true (doneTitle/doneBody를 보여 줌)
        public string header;             // 예: "시즌 2 · 3부 리그 · 5 / 18 라운드"
        public string own, opp;
        public bool home;
        public string note;               // 예: "내 평점 52 / 권장 39"
        public string ownTactic, oppTactic; // 예: "4-3-3 · 역습"
        public string doneTitle, doneBody;
    }

    public class ResultView
    {
        public bool any;                  // 치른 경기가 있는지
        public string label;              // 예: "지난 경기 · 5라운드"
        public string own, opp, outcome;  // outcome: 승리 / 패배 / 무승부
        public int gf, ga;
        public string ownGoals, oppGoals; // 득점자 목록 (없으면 빈 글자)
        public string myLine;             // 예: "평점 7.1   골 1   도움 0   슈팅 3   패스 76%"
    }

    // 리그 팀 목록 창 (순위표의 '팀 선수단 보기')
    public class LeagueTeamsView
    {
        public string title;
        public int division, divisionCount, myDivision, played;
        public List<TableRowView> rows = new List<TableRowView>();
    }

    public class TableRowView
    {
        public string id;
        public int pos, w, d, l, gd, pts, zone;   // zone: 1 승격권, -1 강등권, 0 그 외
        public string name;
        public bool me;
    }

    public class LeaderRowView
    {
        public int rank, value;           // rank 0: 순위 밖(출전 부족)
        public string name, team;
        public SquadRole role;            // 선수 포지션 (이름 옆에 포지션 색 글자)
        public string valueText, sub;     // 보여 줄 값(평점·선방률 등 소수·퍼센트), 아래 작은 글(출전 수·경기당)
        public bool me, gap;              // gap: 이 줄 앞에 "…" 표시 (내 순위가 3위 밖)
    }

    // 시즌이 끝난 뒤 개인 순위 아래 '베스트 11 스쿼드': 공격수 3 · 미드필더 3 · 수비수 4 · 골키퍼 1 (평균 평점 순)
    public class BestElevenSquadView
    {
        public string title;              // 예: "5부 리그 · 시즌 2"
        public List<BestElevenPick> picks = new List<BestElevenPick>();
    }

    public class BestElevenPick
    {
        public SquadRole role;
        public string name, team, teamId;
        public float avg;
        public int apps;
        public bool mine;                 // 내 선수(선수 키우기) 또는 우리 팀 선수(나만의 팀)
    }

    public class LeaderBlockView
    {
        public string title;
        public List<LeaderRowView> rows = new List<LeaderRowView>();
    }

    // 경기 탭의 카드와 표. 글 대신 큰 점수, 팀 대 팀 카드, 줄 맞춘 순위표로 보여 줍니다.
    // CareerPrototypeUI가 칸(Section) 안에 만들고, 데이터가 바뀔 때 Set으로 다시 그립니다.
    public class MatchPageView
    {
        static readonly Color CardFill = new Color(.13f, .18f, .28f, 1f);
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f);
        static readonly Color RowB = new Color(.10f, .14f, .22f, 1f);
        static readonly Color MeRow = new Color(.42f, .33f, .10f, 1f);
        static readonly Color PromoColor = new Color(.35f, .85f, .45f, 1f);
        static readonly Color RelegColor = new Color(.92f, .40f, .40f, 1f);
        static readonly Color WinColor = new Color(.30f, .75f, .40f, 1f);
        static readonly Color LossColor = new Color(.85f, .35f, .35f, 1f);
        static readonly Color DrawColor = new Color(.55f, .60f, .70f, 1f);

        readonly Font font;
        RectTransform nextRoot, resultRoot, tableRoot, leadersRoot;

        public MatchPageView(Font font) { this.font = font; }

        public void BuildNext(Transform parent) { nextRoot = Box(parent, "NextMatch"); }
        public void BuildResult(Transform parent) { resultRoot = Box(parent, "LastResult"); }
        public void BuildTable(Transform parent) { tableRoot = Stack(parent, "Table", 2); }
        public void BuildLeaders(Transform parent) { leadersRoot = Stack(parent, "Leaders", 4); }

        // ---------- 다음 경기 ----------
        public void SetNext(NextMatchView v)
        {
            if (nextRoot == null || v == null) return;
            Clear(nextRoot);
            if (v.done)
            {
                Height(nextRoot, 128);
                Text(nextRoot, v.header, 16, PixelTheme.TextDim, 0f, .78f, 1f, .97f, TextAnchor.MiddleCenter);
                Text(nextRoot, v.doneTitle, 28, PixelTheme.Accent, 0f, .5f, 1f, .78f, TextAnchor.MiddleCenter);
                Fit(Text(nextRoot, v.doneBody, 17, PixelTheme.Text, .03f, .04f, .97f, .5f, TextAnchor.MiddleCenter), 11);
                return;
            }
            Height(nextRoot, 132);
            Text(nextRoot, "다음 경기", 16, PixelTheme.Accent, .03f, .8f, .5f, .97f, TextAnchor.MiddleLeft);
            Text(nextRoot, v.header, 16, PixelTheme.TextDim, .3f, .8f, .97f, .97f, TextAnchor.MiddleRight);
            Fit(Text(nextRoot, v.own, 22, PixelTheme.Text, .02f, .4f, .43f, .78f, TextAnchor.MiddleRight), 13);
            Text(nextRoot, "VS", 24, PixelTheme.Accent, .43f, .4f, .57f, .78f, TextAnchor.MiddleCenter);
            Fit(Text(nextRoot, v.opp, 22, PixelTheme.Text, .57f, .4f, .98f, .78f, TextAnchor.MiddleLeft), 13);
            // 두 팀 전술 (팀 이름 아래)
            Fit(Text(nextRoot, v.ownTactic, 14, new Color(.65f, .80f, 1f), .02f, .25f, .34f, .41f, TextAnchor.MiddleRight), 10);
            Fit(Text(nextRoot, v.oppTactic, 14, new Color(1f, .70f, .70f), .66f, .25f, .98f, .41f, TextAnchor.MiddleLeft), 10);
            Pill(nextRoot, v.home ? "홈 경기" : "원정 경기", v.home ? PixelTheme.ButtonFill : PixelTheme.QuietFill, .35f, .2f, .65f, .38f);
            Fit(Text(nextRoot, v.note, 15, PixelTheme.TextDim, 0f, .02f, 1f, .2f, TextAnchor.MiddleCenter), 10);
        }

        // ---------- 지난 경기 ----------
        public void SetResult(ResultView v)
        {
            if (resultRoot == null || v == null) return;
            Clear(resultRoot);
            if (!v.any)
            {
                Height(resultRoot, 70);
                Text(resultRoot, "아직 치른 경기가 없습니다.", 20, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                return;
            }
            // 아래부터 쌓습니다: 내 기록 띠(0~52) → 득점자(있으면 56~124) → 점수 줄 → 라벨 줄
            bool goals = !string.IsNullOrEmpty(v.ownGoals) || !string.IsNullOrEmpty(v.oppGoals);
            float scoreBottom = goals ? 128f : 58f, H = scoreBottom + 58f + 30f;
            Height(resultRoot, H);
            float F(float px) => Mathf.Clamp01(px / H);
            Color oc = v.outcome == "승리" ? WinColor : v.outcome == "패배" ? LossColor : DrawColor;
            // 라벨 + 결과 표시
            Text(resultRoot, v.label, 16, PixelTheme.TextDim, .03f, F(H - 30f), .6f, F(H - 4f), TextAnchor.MiddleLeft);
            Pill(resultRoot, v.outcome, oc, .76f, F(H - 30f), .97f, F(H - 6f));
            // 팀 이름과 큰 점수
            Fit(Text(resultRoot, v.own, 20, PixelTheme.Text, .02f, F(scoreBottom), .33f, F(scoreBottom + 56f), TextAnchor.MiddleRight), 13);
            Text(resultRoot, v.gf + " - " + v.ga, 40, oc == DrawColor ? PixelTheme.Text : oc, .34f, F(scoreBottom), .66f, F(scoreBottom + 58f), TextAnchor.MiddleCenter);
            Fit(Text(resultRoot, v.opp, 20, PixelTheme.Text, .67f, F(scoreBottom), .98f, F(scoreBottom + 56f), TextAnchor.MiddleLeft), 13);
            // 득점자 (우리 왼쪽, 상대 오른쪽)
            if (goals)
            {
                var a = Text(resultRoot, v.ownGoals, 15, PixelTheme.Text, .03f, F(56f), .49f, F(124f), TextAnchor.UpperLeft);
                a.horizontalOverflow = HorizontalWrapMode.Wrap;
                var b = Text(resultRoot, v.oppGoals, 15, PixelTheme.TextDim, .51f, F(56f), .97f, F(124f), TextAnchor.UpperRight);
                b.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            // 내 기록 띠
            var band = Image(resultRoot, RowB, 0f, 0f, 1f, F(52f));
            band.rectTransform.offsetMin = new Vector2(6, 6); band.rectTransform.offsetMax = new Vector2(-6, 0);
            Fit(Text(band.rectTransform, v.myLine, 17, PixelTheme.Accent, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter), 11);
        }

        // ---------- 순위표 ----------
        public void SetTable(List<TableRowView> rows)
        {
            if (tableRoot == null || rows == null) return;
            Clear(tableRoot);
            Row(tableRoot, null, RowB, true);
            for (int i = 0; i < rows.Count; i++) Row(tableRoot, rows[i], rows[i].me ? MeRow : (i % 2 == 0 ? RowA : RowB), false);
            var legend = Text(NewRect("Legend", tableRoot), "", 14, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleLeft);
            Height(legend.rectTransform.parent as RectTransform, 24);
            legend.supportRichText = true;
            legend.text = "<color=#59D973>■</color> 승격권    <color=#EB6666>■</color> 강등권    <color=#FAD047>■</color> 내 팀";
        }

        static readonly float[] Cols = { 44f, -1f, 104f, 50f, 54f };   // 순위, 팀(남은 폭), 승·무·패, 득실, 승점 (나만의 팀 순위표와 같은 칸)

        void Row(Transform parent, TableRowView r, Color fill, bool header)
        {
            var row = NewRect(header ? "Header" : "Row", parent);
            row.gameObject.AddComponent<Image>().color = fill;
            Height(row, header ? 24 : 32);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 8, 0, 0); h.spacing = 2;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            if (!header && r.zone != 0)   // 왼쪽 색 띠
            {
                var stripe = NewRect("Zone", row);
                stripe.gameObject.AddComponent<Image>().color = r.zone > 0 ? PromoColor : RelegColor;
                var ie = stripe.gameObject.AddComponent<LayoutElement>(); ie.ignoreLayout = true;
                stripe.anchorMin = new Vector2(0, 0); stripe.anchorMax = new Vector2(0, 1); stripe.pivot = new Vector2(0, .5f);
                stripe.sizeDelta = new Vector2(6, 0); stripe.anchoredPosition = Vector2.zero;
            }
            string[] cells = header
                ? new[] { "순위", "팀", "승 · 무 · 패", "득실", "승점" }
                : new[] { r.pos.ToString(), r.name + (r.me ? "  ★" : ""), r.w + " · " + r.d + " · " + r.l, (r.gd > 0 ? "+" : "") + r.gd, r.pts.ToString() };
            for (int c = 0; c < cells.Length; c++)
            {
                var cell = NewRect("Cell", row);
                var le = cell.gameObject.AddComponent<LayoutElement>();
                if (Cols[c] < 0) le.flexibleWidth = 1; else { le.preferredWidth = Cols[c]; le.minWidth = Cols[c]; }
                var t = Text(cell, cells[c], header ? 14 : (c == 0 || c == 4 ? 17 : 16), header ? PixelTheme.TextDim : (r.me && c == 1 ? PixelTheme.Accent : PixelTheme.Text),
                    0f, 0f, 1f, 1f, c == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                if (!header && c == 3) t.color = PixelTheme.TextDim;   // 득실은 흐리게
                if (!header && c == 4) t.color = PixelTheme.Accent;    // 승점은 강조색
                if (c == 1) Fit(t, 11);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        // ---------- 개인 순위 ----------
        // 항목 버튼(득점·도움·공격포인트·평점·수비 / 골키퍼는 선방·클린시트·선방률·평점)으로 한 항목씩, 아래에 베스트 11 카드
        string lHeader; List<LeaderBlockView> lBlocks; BestElevenSquadView lBest;
        public System.Action<BestElevenSquadView> OnBestEleven;   // '베스트 11 스쿼드' 버튼
        static readonly Dictionary<string, int> leaderTabs = new Dictionary<string, int>();   // 고른 항목 (화면을 다시 그려도 유지, 모드마다 따로)
        public string LeaderKey = "career";
        int leaderTab { get { return leaderTabs.TryGetValue(LeaderKey, out var t) ? t : 0; } set { leaderTabs[LeaderKey] = value; } }
        static readonly Color TabOn = new Color(.55f, .42f, .10f, 1f), TabOff = new Color(.14f, .18f, .27f, 1f);

        public void SetLeaders(string header, List<LeaderBlockView> blocks, BestElevenSquadView best)
        {
            lHeader = header; lBlocks = blocks; lBest = best;
            RenderLeaders();
        }

        void RenderLeaders()
        {
            if (leadersRoot == null) return;
            Clear(leadersRoot);
            var head = Text(NewRect("Head", leadersRoot), lHeader, 18, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleLeft);
            Height(head.rectTransform.parent as RectTransform, 28);
            if (lBlocks == null || lBlocks.Count == 0) return;
            leaderTab = Mathf.Clamp(leaderTab, 0, lBlocks.Count - 1);

            // 항목 버튼
            var tabs = NewRect("Tabs", leadersRoot); Height(tabs, 44);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            for (int i = 0; i < lBlocks.Count; i++)
            {
                int index = i; bool on = i == leaderTab;
                var tab = NewRect("Tab", tabs);
                var img = tab.gameObject.AddComponent<Image>(); img.color = on ? TabOn : TabOff;
                var button = tab.gameObject.AddComponent<Button>(); button.targetGraphic = img;
                button.onClick.AddListener(() => { if (leaderTab == index) return; SoundFx.Play(Sfx.Click); leaderTab = index; RenderLeaders(); });
                Fit(Text(tab, lBlocks[i].title, 17, on ? Color.white : PixelTheme.TextDim, .02f, 0f, .98f, 1f, TextAnchor.MiddleCenter), 11);
            }

            // 고른 항목의 순위 (상위 5명 + 내 순위)
            var b = lBlocks[leaderTab];
            if (b.rows.Count == 0)
            {
                var none = Text(NewRect("None", leadersRoot), "아직 기록이 없습니다.", 17, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                Height(none.rectTransform.parent as RectTransform, 40);
            }
            for (int i = 0; i < b.rows.Count; i++)
            {
                var r = b.rows[i];
                if (r.gap)
                {
                    var dots = Text(NewRect("Gap", leadersRoot), "...", 16, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                    Height(dots.rectTransform.parent as RectTransform, 16);
                }
                var row = NewRect("Leader", leadersRoot);
                row.gameObject.AddComponent<Image>().color = r.me ? MeRow : (i % 2 == 0 ? RowA : RowB);
                Height(row, 50);
                Text(row, r.rank > 0 ? r.rank.ToString() : "-", 20, r.rank == 1 ? PixelTheme.Accent : PixelTheme.Text, .01f, 0f, .09f, 1f, TextAnchor.MiddleCenter);
                Fit(Text(row, "<color=#" + ColorUtility.ToHtmlStringRGB(SquadDialog.RoleColor(r.role)) + ">" + r.role + "</color>  " + r.name + (r.me ? "  ★" : ""), 19, r.me ? Color.white : PixelTheme.Text, .1f, .46f, .62f, 1f, TextAnchor.MiddleLeft), 12);   // 이름 앞 포지션 (포지션 색)
                Fit(Text(row, r.team, 14, PixelTheme.TextDim, .1f, 0f, .62f, .5f, TextAnchor.MiddleLeft), 10);
                Text(row, string.IsNullOrEmpty(r.valueText) ? r.value.ToString() : r.valueText, 22, r.me ? PixelTheme.Accent : PixelTheme.Text, .6f, .42f, .98f, 1f, TextAnchor.MiddleRight);
                Fit(Text(row, r.sub, 13, PixelTheme.TextDim, .5f, 0f, .98f, .46f, TextAnchor.MiddleRight), 10);
            }

            // 시즌이 끝나면: 베스트 11 스쿼드 버튼 (포메이션 그림 창)
            if (lBest == null) return;
            Height(NewRect("Space", leadersRoot), 8);
            var edge = NewRect("BestElevenButton", leadersRoot);
            edge.gameObject.AddComponent<Image>().color = PixelTheme.RebirthEdge;
            Height(edge, 64);
            var fill = NewRect("Fill", edge); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = new Vector2(3, 3); fill.offsetMax = new Vector2(-3, -3);
            var fi = fill.gameObject.AddComponent<Image>(); fi.color = PixelTheme.RebirthFill;
            var bb = edge.gameObject.AddComponent<Button>(); bb.targetGraphic = fi;
            var best = lBest;
            bb.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); if (OnBestEleven != null) OnBestEleven(best); });
            Text(fill, "베스트 11 스쿼드", 24, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
        }

        // ---------- 공통 ----------
        // 칸보다 긴 글자는 칸에 맞게 작아지게 (팀 이름 등)
        static Text Fit(Text t, int min)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        RectTransform Box(Transform parent, string name)
        {
            var edge = NewRect(name, parent);
            edge.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            var inner = NewRect("Inner", edge);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one; inner.offsetMin = new Vector2(3, 3); inner.offsetMax = new Vector2(-3, -3);
            inner.gameObject.AddComponent<Image>().color = CardFill;
            Height(edge, 100);
            return inner;
        }

        static RectTransform Stack(Transform parent, string name, float spacing)
        {
            var r = NewRect(name, parent);
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return r;
        }

        // 카드(Box 안쪽)면 바깥 틀의 높이를, 아니면 자기 높이를 정합니다.
        static void Height(RectTransform r, float h)
        {
            if (r == null) return;
            var target = r.name == "Inner" && r.parent != null ? (RectTransform)r.parent : r;
            var le = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
        }

        static void Clear(RectTransform r)
        {
            foreach (Transform child in r) { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image Image(Transform parent, Color color, float x0, float y0, float x1, float y1)
        {
            var r = NewRect("Image", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var img = r.gameObject.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        Text Text(Transform parent, string value, int size, Color color, float x0, float y0, float x1, float y1, TextAnchor align)
        {
            var r = NewRect("Text", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value ?? ""; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        void Pill(Transform parent, string label, Color fill, float x0, float y0, float x1, float y1)
        {
            var img = Image(parent, fill, x0, y0, x1, y1);
            Text(img.rectTransform, label, 17, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
        }
    }
}
