using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace MyFootballCareer
{
    // 환생 조건 한 줄
    [Serializable] public class RebirthCondition { public string label, value; public float progress; public bool ok; }

    // 지금 뛰는 팀 정보 (이적 제안 카드와 같은 숫자로 비교)
    [Serializable] public class TeamInfoView
    {
        public string teamId, name;
        public int division, wage, myRating, required;
        public float strength, startChance;
        public AttributeKind focus;
    }

    // 커리어 탭에 보여 줄 데이터 (규칙 쪽에서 만들어 넘김)
    [Serializable] public class CareerPageData
    {
        public string name, position, trait, team, division;
        public int season, career;
        public long money; public int pay, agentPercent, wage;
        public int[] trophyCounts = new int[0]; public int trophyTotal;
        public List<TrophyRecord> recent = new List<TrophyRecord>();        // 최근 트로피부터
        public List<SeasonSummary> history = new List<SeasonSummary>();     // 최근 시즌부터
        public int rebirthCount; public float bonus, speed; public bool canRebirth;
        public List<RebirthCondition> conditions = new List<RebirthCondition>();
        public bool hasLastCareer; public CareerSummary lastCareer;
        // 은퇴 · 내가 키운 선수 · 나만의 팀
        public bool canRetire;
        public List<RetiredPlayer> legends = new List<RetiredPlayer>();   // 평점 높은 순
        public int legendNeed;
        public bool hasClub, clubActive;
        public string clubName, clubTactic, clubDivision;
        public int clubKit; public float clubStrength;
    }

    // 커리어 탭의 카드들: 선수 프로필, 트로피 룸, 시즌 기록 표, 환생 조건.
    // CareerPrototypeUI가 칸(Section) 안에 자리를 만들고, 데이터가 바뀔 때만 다시 그립니다.
    public class CareerPageView
    {
        static readonly Color Tile = new Color(.13f, .18f, .28f, 1f);
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f);
        static readonly Color RowB = new Color(.10f, .14f, .22f, 1f);
        static readonly Color GoldFill = new Color(.42f, .33f, .10f, 1f);
        static readonly Color GoodText = new Color(.55f, .95f, .65f, 1f);
        static readonly Color BarBack = new Color(.04f, .06f, .10f, 1f);

        readonly Font font;
        RectTransform profileRoot, trophyRoot, historyRoot, rebirthRoot, retireRoot, legendRoot;
        string lastKey;

        public Action<int> OnLegend;   // 내가 키운 선수 줄을 누르면 (커리어 번호)

        public CareerPageView(Font font) { this.font = font; }

        public void BuildProfile(Transform parent) { profileRoot = Stack(parent, "Profile", 8); }
        public void BuildTrophies(Transform parent) { trophyRoot = Stack(parent, "Trophies", 6); }
        public void BuildHistory(Transform parent) { historyRoot = Stack(parent, "History", 2); }
        public void BuildRebirth(Transform parent) { rebirthRoot = Stack(parent, "Rebirth", 6); }
        public void BuildRetire(Transform parent) { retireRoot = Stack(parent, "Retire", 6); }
        public void BuildLegends(Transform parent) { legendRoot = Stack(parent, "Legends", 4); }

        // 0.2초마다 불려도 값이 바뀌었을 때만 다시 만듭니다.
        public void Set(CareerPageData d)
        {
            if (d == null) return;
            string key = JsonUtility.ToJson(d);
            if (key == lastKey) return;
            lastKey = key;
            SetProfile(d); SetTrophies(d); SetHistory(d); SetRebirth(d); SetRetire(d); SetLegends(d);
        }

        // ---------- 프로필 ----------
        void SetProfile(CareerPageData d)
        {
            if (profileRoot == null) return;
            Clear(profileRoot);
            var head = Box(profileRoot, Tile, 92);
            Fit(Label(head, d.name, 30, PixelTheme.Text, .04f, .48f, .56f, .94f, TextAnchor.MiddleLeft), 16);
            Fit(Label(head, d.position + " · " + d.trait, 18, PixelTheme.TextDim, .04f, .08f, .56f, .46f, TextAnchor.MiddleLeft), 12);
            Fit(Label(head, d.team, 22, PixelTheme.Accent, .52f, .48f, .96f, .94f, TextAnchor.MiddleRight), 12);
            Fit(Label(head, "시즌 " + d.season + (string.IsNullOrEmpty(d.division) ? "" : " · " + d.division), 17, PixelTheme.TextDim, .52f, .08f, .96f, .46f, TextAnchor.MiddleRight), 11);
            Tiles(profileRoot, new[] {
                ("보유 돈", SeasonSystem.Money(d.money), 1),
                ("경기당 수입" + (d.agentPercent > 0 ? " (+" + d.agentPercent + "%)" : ""), SeasonSystem.Money(d.pay), 0),
                ("커리어", d.career + "번째", 0) });
        }

        // ---------- 트로피 룸 ----------
        void SetTrophies(CareerPageData d)
        {
            if (trophyRoot == null) return;
            Clear(trophyRoot);
            int kinds = d.trophyCounts.Length;
            const int PerRow = 4;
            for (int i = 0; i < kinds; i += PerRow)
            {
                var row = Row(trophyRoot, 74);
                for (int k = 0; k < PerRow; k++)
                {
                    var cell = NewRect("Trophy", row);
                    if (i + k >= kinds) continue;   // 빈 칸으로 폭을 맞춤
                    int n = d.trophyCounts[i + k];
                    cell.gameObject.AddComponent<Image>().color = n > 0 ? GoldFill : Tile;
                    Label(cell, n > 0 ? "★ " + n : "-", 24, n > 0 ? PixelTheme.Accent : PixelTheme.TextDim, .04f, .40f, .96f, .98f, TextAnchor.MiddleCenter);
                    Fit(Label(cell, AwardSystem.KindName((TrophyKind)(i + k)), 14, n > 0 ? PixelTheme.Text : PixelTheme.TextDim, .04f, .02f, .96f, .42f, TextAnchor.MiddleCenter), 10);
                }
            }
            if (d.trophyTotal == 0)
            {
                Note(trophyRoot, "아직 트로피가 없습니다. 우승과 시즌 시상으로 채워 보세요.", PixelTheme.TextDim);
                return;
            }
            var cap = Block(trophyRoot, 26);
            Label(cap, "최근 트로피", 16, PixelTheme.TextDim, 0f, 0f, .6f, 1f, TextAnchor.MiddleLeft);
            Label(cap, "총 " + d.trophyTotal + "개", 16, PixelTheme.Accent, .6f, 0f, 1f, 1f, TextAnchor.MiddleRight);
            for (int i = 0; i < d.recent.Count; i++)
            {
                var t = d.recent[i];
                var r = Box(trophyRoot, i % 2 == 0 ? RowA : RowB, 32);
                Fit(Label(r, "커리어" + t.career + " · 시즌" + t.season + (t.division > 0 ? " · " + t.division + "부" : ""), 15, PixelTheme.TextDim, .02f, 0f, .40f, 1f, TextAnchor.MiddleLeft), 10);
                Fit(Label(r, "★ " + AwardSystem.KindName(t.kind), 17, PixelTheme.Accent, .40f, 0f, .70f, 1f, TextAnchor.MiddleLeft), 11);
                Fit(Label(r, t.detail, 15, PixelTheme.Text, .70f, 0f, .98f, 1f, TextAnchor.MiddleRight), 10);
            }
        }

        // ---------- 시즌 기록 ----------
        void SetHistory(CareerPageData d)
        {
            if (historyRoot == null) return;
            Clear(historyRoot);
            if (d.history.Count == 0) { Note(historyRoot, "아직 끝낸 시즌이 없습니다.", PixelTheme.TextDim); return; }
            var head = Block(historyRoot, 26);
            Label(head, "시즌", 14, PixelTheme.TextDim, .01f, 0f, .11f, 1f, TextAnchor.MiddleCenter);
            Label(head, "순위", 14, PixelTheme.TextDim, .11f, 0f, .27f, 1f, TextAnchor.MiddleCenter);
            Label(head, "팀", 14, PixelTheme.TextDim, .28f, 0f, .52f, 1f, TextAnchor.MiddleLeft);
            Label(head, "승 · 무 · 패", 14, PixelTheme.TextDim, .52f, 0f, .73f, 1f, TextAnchor.MiddleCenter);
            Label(head, "골 · 도움", 14, PixelTheme.TextDim, .73f, 0f, .88f, 1f, TextAnchor.MiddleCenter);
            Label(head, "평점", 14, PixelTheme.TextDim, .88f, 0f, .99f, 1f, TextAnchor.MiddleCenter);
            for (int i = 0; i < d.history.Count; i++)
            {
                var h = d.history[i];
                bool champ = h.finalPosition == 1;
                var r = Box(historyRoot, champ ? GoldFill : i % 2 == 0 ? RowA : RowB, 36);
                Label(r, h.seasonNumber.ToString(), 18, PixelTheme.Text, .01f, 0f, .11f, 1f, TextAnchor.MiddleCenter);
                string pos = (h.division > 0 ? h.division + "부 " : "") + (h.finalPosition > 0 ? h.finalPosition + "위" : "-");
                Fit(Label(r, pos, 17, champ ? PixelTheme.Accent : PixelTheme.Text, .11f, 0f, .27f, 1f, TextAnchor.MiddleCenter), 11);
                Fit(Label(r, h.teamName, 16, PixelTheme.TextDim, .28f, 0f, .52f, 1f, TextAnchor.MiddleLeft), 10);
                Fit(Label(r, h.wins + " · " + h.draws + " · " + h.losses, 17, PixelTheme.Text, .52f, 0f, .73f, 1f, TextAnchor.MiddleCenter), 11);
                Label(r, h.playerGoals + " · " + h.playerAssists, 17, h.playerGoals + h.playerAssists > 0 ? PixelTheme.Accent : PixelTheme.Text, .73f, 0f, .88f, 1f, TextAnchor.MiddleCenter);
                Label(r, h.appearances > 0 ? h.averageRating.ToString("0.0") : "-", 17, h.averageRating >= 7f ? GoodText : PixelTheme.Text, .88f, 0f, .99f, 1f, TextAnchor.MiddleCenter);
            }
        }

        // ---------- 환생 ----------
        void SetRebirth(CareerPageData d)
        {
            if (rebirthRoot == null) return;
            Clear(rebirthRoot);
            Tiles(rebirthRoot, new[] {
                ("환생", d.rebirthCount + "회", 0),
                ("노력 보너스", "+" + d.bonus.ToString("0.#") + "%", d.bonus > 0f ? 1 : 0),
                ("노력 속도", "x" + d.speed.ToString("0.00"), 0) });
            var cap = Block(rebirthRoot, 26);
            Label(cap, "환생 조건", 16, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleLeft);
            if (d.conditions.Count == 0) Note(rebirthRoot, "조건 없음", PixelTheme.TextDim);
            foreach (var c in d.conditions)
            {
                var r = Box(rebirthRoot, RowA, 40);
                Fit(Label(r, c.label, 17, PixelTheme.Text, .03f, 0f, .36f, 1f, TextAnchor.MiddleLeft), 11);
                Img(r, BarBack, .38f, .34f, .74f, .66f);
                Img(r, c.ok ? PixelTheme.Bar : PixelTheme.RebirthEdge, .38f, .34f, .38f + .36f * Mathf.Clamp01(c.progress), .66f);
                Fit(Label(r, c.ok ? "달성" : c.value, 16, c.ok ? GoodText : PixelTheme.TextDim, .76f, 0f, .98f, 1f, TextAnchor.MiddleRight), 10);
            }
            Note(rebirthRoot, d.canRebirth ? "환생할 수 있습니다! 보너스를 받고 새 커리어를 시작하세요." : "조건을 모두 채우면 환생할 수 있습니다.",
                 d.canRebirth ? PixelTheme.Accent : PixelTheme.TextDim);
            if (d.hasLastCareer)
            {
                var c = d.lastCareer;
                string best = c.bestPosition > 0 ? "최고 " + (c.bestDivision > 0 ? c.bestDivision + "부 " : "") + c.bestPosition + "위 · " : "";
                Note(rebirthRoot, "지난 커리어 " + c.careerNumber + ": " + c.seasons + "시즌 · " + best + "골 " + c.goals + " 도움 " + c.assists
                     + " · +" + c.bonusGainedPercent.ToString("0.#") + "%", PixelTheme.TextDim, 15);
            }
        }

        // ---------- 은퇴 ----------
        void SetRetire(CareerPageData d)
        {
            if (retireRoot == null) return;
            Clear(retireRoot);
            Note(retireRoot, d.canRetire ? "시즌이 끝났습니다. 원하면 지금 은퇴할 수 있습니다." : "시즌이 끝나면 은퇴할 수 있습니다.",
                 d.canRetire ? PixelTheme.Accent : PixelTheme.TextDim);
            Note(retireRoot, "은퇴한 선수는 '내가 키운 선수'에 남고, 새 선수로 다시 시작합니다.", PixelTheme.TextDim, 15);
        }

        // ---------- 내가 키운 선수 · 나만의 팀 ----------
        void SetLegends(CareerPageData d)
        {
            if (legendRoot == null) return;
            Clear(legendRoot);
            if (d.hasClub)
            {
                var card = Box(legendRoot, GoldFill, 104);
                TeamKits.PreviewImage(card, d.clubKit, .02f, .06f, .16f, .94f);
                Fit(Label(card, d.clubName, 26, PixelTheme.Accent, .19f, .52f, .74f, .96f, TextAnchor.MiddleLeft), 14);
                Fit(Label(card, d.clubTactic, 16, PixelTheme.TextDim, .19f, .06f, .74f, .48f, TextAnchor.MiddleLeft), 11);
                Fit(Label(card, d.clubActive ? d.clubDivision : "다음 시즌 참가", 18, PixelTheme.Text, .70f, .52f, .97f, .96f, TextAnchor.MiddleRight), 11);
                Fit(Label(card, "전력 " + d.clubStrength.ToString("0"), 18, PixelTheme.Text, .70f, .06f, .97f, .48f, TextAnchor.MiddleRight), 11);
            }
            else
            {
                int n = d.legends.Count;
                var cap = Block(legendRoot, 30);
                Label(cap, "나만의 팀 창단", 16, PixelTheme.TextDim, 0f, 0f, .5f, 1f, TextAnchor.MiddleLeft);
                Label(cap, Mathf.Min(n, d.legendNeed) + " / " + d.legendNeed, 16, n >= d.legendNeed ? GoodText : PixelTheme.Accent, .5f, 0f, 1f, 1f, TextAnchor.MiddleRight);
                var bar = Box(legendRoot, BarBack, 12);
                Img(bar, n >= d.legendNeed ? PixelTheme.Bar : PixelTheme.Accent, 0f, 0f, Mathf.Clamp01(d.legendNeed > 0 ? (float)n / d.legendNeed : 1f), 1f);
                Note(legendRoot, n >= d.legendNeed ? "창단할 수 있습니다! 은퇴 선수로 팀을 만들어 리그에 참가하세요."
                                                    : "은퇴 선수 " + d.legendNeed + "명을 모으면 나만의 팀을 창단할 수 있습니다.",
                     n >= d.legendNeed ? PixelTheme.Accent : PixelTheme.TextDim, 15);
            }
            if (d.legends.Count == 0) { Note(legendRoot, "아직 은퇴한 선수가 없습니다.", PixelTheme.TextDim); return; }
            Note(legendRoot, "선수를 누르면 커리어 기록을 볼 수 있습니다.", PixelTheme.TextDim, 14);
            for (int i = 0; i < d.legends.Count; i++)
            {
                var lg = d.legends[i];
                var r = Box(legendRoot, i % 2 == 0 ? RowA : RowB, 56);
                var img = r.GetComponent<Image>(); img.raycastTarget = true;
                var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = img;
                int careerNo = lg.careerNumber;
                button.onClick.AddListener(() => { if (OnLegend != null) OnLegend(careerNo); });
                Label(r, Mathf.RoundToInt(lg.rating).ToString(), 24, PixelTheme.Accent, .01f, 0f, .12f, 1f, TextAnchor.MiddleCenter);
                Fit(Label(r, lg.name, 19, PixelTheme.Text, .13f, .48f, .58f, 1f, TextAnchor.MiddleLeft), 11);
                Fit(Label(r, Ko.Pos(lg.position) + " · " + Ko.TraitName(lg.trait) + " · 커리어" + lg.careerNumber, 14, PixelTheme.TextDim, .13f, 0f, .58f, .50f, TextAnchor.MiddleLeft), 10);
                Fit(Label(r, lg.seasons + "시즌 · 골 " + lg.goals + " · 도움 " + lg.assists, 15, PixelTheme.Text, .58f, .48f, .99f, 1f, TextAnchor.MiddleRight), 10);
                Fit(Label(r, (lg.trophies > 0 ? "★ " + lg.trophies + " · " : "") + lg.lastTeam + "  ›", 14, lg.trophies > 0 ? PixelTheme.Accent : PixelTheme.TextDim, .58f, 0f, .99f, .50f, TextAnchor.MiddleRight), 10);
            }
        }

        // ---------- 부품 ----------
        // 숫자 타일 한 줄. tone 1이면 금색 강조
        void Tiles(Transform p, (string label, string value, int tone)[] items)
        {
            var row = Row(p, 70);
            foreach (var it in items)
            {
                var tile = NewRect("Tile", row);
                tile.gameObject.AddComponent<Image>().color = it.tone == 1 ? GoldFill : Tile;
                Fit(Label(tile, it.value, 26, it.tone == 1 ? PixelTheme.Accent : PixelTheme.Text, .04f, .36f, .96f, 1f, TextAnchor.MiddleCenter), 14);
                Fit(Label(tile, it.label, 15, PixelTheme.TextDim, .04f, 0f, .96f, .38f, TextAnchor.MiddleCenter), 10);
            }
        }

        // 같은 폭으로 나눠 담는 가로 줄
        static RectTransform Row(Transform p, float height)
        {
            var row = Block(p, height);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            return row;
        }

        void Note(Transform p, string text, Color color, int size = 16)
        {
            var b = Block(p, 30);
            Fit(Label(b, text, size, color, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter), 10);
        }

        static RectTransform Stack(Transform parent, string name, float spacing)
        {
            var r = MatchResultDialog.NewRect(name, parent);
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return r;
        }

        static void Clear(RectTransform r)
        {
            foreach (Transform child in r) { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
        }

        static RectTransform Box(Transform p, Color c, float height)
        {
            var b = Block(p, height);
            var img = b.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return b;
        }

        static RectTransform NewRect(string name, Transform p) { return MatchResultDialog.NewRect(name, p); }
        static RectTransform Block(Transform p, float h) { return MatchResultDialog.Block(p, h); }
        static Image Img(Transform p, Color c, float x0, float y0, float x1, float y1) { return MatchResultDialog.Img(p, c, x0, y0, x1, y1); }
        static Text Fit(Text t, int min) { return MatchResultDialog.Fit(t, min); }
        Text Label(Transform p, string value, int size, Color color, float x0, float y0, float x1, float y1, TextAnchor align)
        { return MatchResultDialog.Label(p, font, value, size, color, x0, y0, x1, y1, align); }
    }
}
