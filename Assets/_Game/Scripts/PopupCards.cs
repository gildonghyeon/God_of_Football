using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 시상 한 줄 (level 0 미수상, 1 수상, 2 올해의 축구선수 수상)
    public class AwardRow { public int level; public string label, text; }

    // 시즌 종료 팝업에 보여 줄 데이터
    public class SeasonEndData
    {
        public int season; public string division = "";
        public int position, teams, points; public string champion = "";
        public int move = 9;            // 1 승격, -1 강등, 0 잔류, 9 해당 없음
        public string moveText = "";
        public int wins, draws, losses, goalsFor, goalsAgainst;
        public int goals, assists, apps, played; public float rating;
        public List<AwardRow> awards = new List<AwardRow>();
        public bool noTrophy;
        public bool offersReady; public int score = -1; public string grade = ""; public int offers;
    }

    // 새 시즌 팝업에 보여 줄 데이터
    public class NewSeasonData
    {
        public int season; public string division = "";
        public string fromTeam, toTeam;   // 이적했으면 채워짐
        public int oldWage, newWage;      // 주급이 바뀌었으면 newWage > 0
        public int move;                  // 1 승격, -1 강등, 0 그대로
        public string note;               // 부가 처음 생길 때의 안내
    }

    // 환생 확인 팝업에 보여 줄 데이터
    public class RebirthPreviewData
    {
        public float gain, total, speedBefore, speedAfter;
        public int keepPercent; public bool capped;
        public string[] reset, keep;
    }

    // 글자만 늘어놓던 팝업을 카드 모양으로 보여 줍니다: 큰 숫자 머리, 배지, 숫자 타일, 줄 목록.
    public static class PopupCards
    {
        static readonly Color Tile = new Color(.13f, .18f, .28f, 1f);
        static readonly Color RowColor = new Color(.12f, .16f, .25f, 1f);
        static readonly Color GoldFill = new Color(.42f, .33f, .10f, 1f);
        static readonly Color GoldRow = new Color(.24f, .20f, .09f, 1f);
        static readonly Color UpFill = new Color(.16f, .40f, .24f, 1f);
        static readonly Color UpText = new Color(.55f, .95f, .65f, 1f);
        static readonly Color DownFill = new Color(.45f, .16f, .16f, 1f);
        static readonly Color DownText = new Color(1f, .62f, .62f, 1f);
        static readonly Color Dark = new Color(.12f, .09f, .02f, 1f);

        // ---------- 화면별 내용 ----------
        public static void SeasonEnd(Transform p, Font f, SeasonEndData d)
        {
            bool champ = d.position == 1;
            Hero(p, f, "시즌 " + d.season + (d.division != "" ? " · " + d.division : "") + " 최종 순위",
                 d.position > 0 ? d.position + "위" : "-", champ ? PixelTheme.Accent : PixelTheme.Text,
                 "/ " + d.teams + "팀   ·   승점 " + d.points + (champ || string.IsNullOrEmpty(d.champion) ? "" : "   ·   우승 " + d.champion));
            var pills = new List<(string, Color, Color)>();
            if (champ) pills.Add(("★ 리그 우승", GoldFill, PixelTheme.Accent));
            if (d.move == 1) pills.Add(("▲ " + d.moveText, UpFill, UpText));
            else if (d.move == -1) pills.Add(("▼ " + d.moveText, DownFill, DownText));
            else if (d.move == 0) pills.Add((d.moveText, Tile, PixelTheme.TextDim));
            if (pills.Count > 0) Pills(p, f, pills);

            Caption(p, f, "팀 성적");
            Tiles(p, f, new[] { ("승", d.wins.ToString(), 0), ("무", d.draws.ToString(), 0), ("패", d.losses.ToString(), 0),
                                ("득점 - 실점", d.goalsFor + " - " + d.goalsAgainst, d.goalsFor > d.goalsAgainst ? 2 : d.goalsFor < d.goalsAgainst ? 3 : 0) });
            Caption(p, f, "내 기록");
            Tiles(p, f, new[] { ("골", d.goals.ToString(), d.goals > 0 ? 1 : 0), ("도움", d.assists.ToString(), d.assists > 0 ? 1 : 0),
                                ("평균 평점", d.apps > 0 ? d.rating.ToString("0.0") : "-", d.rating >= 7f ? 1 : 0), ("출전", d.apps + "/" + d.played, 0) });

            if (d.awards.Count > 0 || d.noTrophy)
            {
                Caption(p, f, "시상");
                if (d.noTrophy) Note(p, f, "이번 시즌 트로피 없음", PixelTheme.TextDim);
                foreach (var a in d.awards) Award(p, f, a);
            }

            if (d.offersReady)
            {
                Gap(p, 4);
                var box = Box(p, Tile, 76);
                if (d.score >= 0)
                {
                    MatchResultDialog.Label(box, f, "시즌 등급", 15, PixelTheme.TextDim, .02f, .66f, .28f, .96f, TextAnchor.MiddleCenter);
                    MatchResultDialog.Label(box, f, d.grade, 40, PixelTheme.Accent, .02f, .02f, .28f, .70f, TextAnchor.MiddleCenter);
                    MatchResultDialog.Label(box, f, "시즌 점수 " + d.score + " / 100", 17, PixelTheme.TextDim, .31f, .52f, .98f, .94f, TextAnchor.MiddleLeft);
                }
                bool any = d.offers > 0;
                MatchResultDialog.Fit(MatchResultDialog.Label(box, f, any ? "이적 제안 " + d.offers + "건 · 커리어 탭에서 확인" : "이적 제안 없음 · 다음 시즌에 더 활약해 보세요",
                    18, any ? PixelTheme.Accent : PixelTheme.TextDim, d.score >= 0 ? .31f : .03f, .06f, .98f, d.score >= 0 ? .52f : .94f,
                    d.score >= 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter), 12);
            }
        }

        public static void NewSeason(Transform p, Font f, NewSeasonData d)
        {
            Hero(p, f, "새 시즌 시작", "시즌 " + d.season, PixelTheme.Text, d.division);
            if (d.move == 1) Pills(p, f, new List<(string, Color, Color)> { ("▲ 승격", UpFill, UpText) });
            else if (d.move == -1) Pills(p, f, new List<(string, Color, Color)> { ("▼ 강등", DownFill, DownText) });
            if (!string.IsNullOrEmpty(d.toTeam))
            {
                Caption(p, f, "이적");
                var row = Box(p, Tile, 64);
                MatchResultDialog.Fit(MatchResultDialog.Label(row, f, d.fromTeam, 20, PixelTheme.TextDim, .03f, 0f, .42f, 1f, TextAnchor.MiddleCenter), 12);
                MatchResultDialog.Label(row, f, "→", 28, PixelTheme.Accent, .42f, 0f, .58f, 1f, TextAnchor.MiddleCenter);
                MatchResultDialog.Fit(MatchResultDialog.Label(row, f, d.toTeam, 22, PixelTheme.Accent, .58f, 0f, .97f, 1f, TextAnchor.MiddleCenter), 12);
            }
            if (d.newWage > 0)
            {
                Caption(p, f, "계약");
                Change(p, f, "주급", d.oldWage > 0 ? SeasonSystem.Money(d.oldWage) : "-", SeasonSystem.Money(d.newWage),
                       d.newWage >= d.oldWage ? UpText : DownText);
            }
            if (!string.IsNullOrEmpty(d.note)) Note(p, f, d.note, PixelTheme.TextDim);
        }

        public static void RebirthPreview(Transform p, Font f, RebirthPreviewData d)
        {
            Hero(p, f, "이번 환생으로 얻는 보너스", "+" + d.gain.ToString("0.#") + "%", PixelTheme.RebirthEdge,
                 d.capped ? "보너스가 상한에 도달했습니다" : "누적 보너스 +" + d.total.ToString("0.#") + "%");
            Change(p, f, "노력 포인트 속도", "x" + d.speedBefore.ToString("0.00"), "x" + d.speedAfter.ToString("0.00"), UpText);
            Chips(p, f, "초기화", d.reset, DownFill, DownText);
            if (d.keepPercent > 0) Note(p, f, "능력치는 오른 만큼의 " + d.keepPercent + "%를 남깁니다", PixelTheme.TextDim, 16, 24);
            Chips(p, f, "유지", d.keep, UpFill, UpText);
        }

        public static void RebirthDone(Transform p, Font f, int count, float gain, float total, float speed)
        {
            Hero(p, f, "새 커리어가 시작됩니다", "환생 " + count + "회", PixelTheme.RebirthEdge, "");
            Tiles(p, f, new[] { ("이번 보너스", "+" + gain.ToString("0.#") + "%", 1), ("누적 보너스", "+" + total.ToString("0.#") + "%", 0),
                                ("노력 속도", "x" + speed.ToString("0.00"), 2) });
        }

        public static void TransferConfirm(Transform p, Font f, string team, string division, int startPercent, int wage, int currentWage)
        {
            Hero(p, f, "다음 시즌에 뛸 팀", team, PixelTheme.Accent, division);
            var items = new List<(string, string, int)> { ("선발 확률", startPercent + "%", startPercent >= 80 ? 2 : 0) };
            if (wage > 0) items.Add(("주급", SeasonSystem.Money(wage), wage > currentWage ? 2 : wage < currentWage ? 3 : 0));
            if (currentWage > 0) items.Add(("지금 주급", SeasonSystem.Money(currentWage), 0));
            Tiles(p, f, items.ToArray());
            Warning(p, f, "확정하면 바꿀 수 없습니다");
        }

        public static void StayConfirm(Transform p, Font f, string team)
        {
            Hero(p, f, "다음 시즌에도 뛸 팀", team, PixelTheme.Text, "모든 이적 제안을 거절합니다");
            Warning(p, f, "확정하면 바꿀 수 없습니다");
        }

        // 저장된 시상 문장을 배지와 내용으로 나눕니다. 예: "* 득점왕: 이름, 12골" -> 수상 / 득점왕 / 이름, 12골
        public static AwardRow ParseAward(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string s = line.Trim();
            int level = 0;
            if (s.StartsWith("***")) { level = 2; s = s.Trim('*', ' '); }
            else if (s.StartsWith("*")) { level = 1; s = s.TrimStart('*', ' '); }
            int colon = s.IndexOf(": "), bang = s.IndexOf("! ");
            string label = "", text = s;
            if (colon > 0 && (bang < 0 || colon < bang)) { label = s.Substring(0, colon); text = s.Substring(colon + 2); }
            else if (bang > 0) { label = s.Substring(0, bang); text = s.Substring(bang + 2); }
            return new AwardRow { level = level, label = label, text = text };
        }

        // ---------- 부품 ----------
        // 큰 머리: 위 작은 글, 가운데 큰 글, 아래 작은 글
        static void Hero(Transform p, Font f, string top, string big, Color bigColor, string bottom)
        {
            bool hasBottom = !string.IsNullOrEmpty(bottom);
            var b = Box(p, Tile, hasBottom ? 140 : 112);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, top, 18, PixelTheme.TextDim, .03f, hasBottom ? .76f : .70f, .97f, .97f, TextAnchor.MiddleCenter), 12);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, big, 56, bigColor, .03f, hasBottom ? .26f : .04f, .97f, hasBottom ? .78f : .72f, TextAnchor.MiddleCenter), 24);
            if (hasBottom) MatchResultDialog.Fit(MatchResultDialog.Label(b, f, bottom, 18, PixelTheme.TextDim, .03f, .03f, .97f, .27f, TextAnchor.MiddleCenter), 12);
        }

        // 가운데 정렬된 배지들 (우승, 승격 등)
        static void Pills(Transform p, Font f, List<(string text, Color fill, Color color)> items)
        {
            var row = MatchResultDialog.Block(p, 40);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            foreach (var it in items)
            {
                var pill = MatchResultDialog.NewRect("Pill", row);
                pill.gameObject.AddComponent<Image>().color = it.fill;
                pill.gameObject.AddComponent<LayoutElement>().preferredWidth = 250;
                MatchResultDialog.Fit(MatchResultDialog.Label(pill, f, it.text, 20, it.color, .04f, 0f, .96f, 1f, TextAnchor.MiddleCenter), 12);
            }
        }

        // 구역 제목: 작은 글자와 밑줄
        static void Caption(Transform p, Font f, string text)
        {
            var b = MatchResultDialog.Block(p, 30);
            MatchResultDialog.Label(b, f, text, 17, PixelTheme.TextDim, 0f, .12f, 1f, 1f, TextAnchor.MiddleLeft);
            var line = MatchResultDialog.Img(b, PixelTheme.PanelEdge, 0f, 0f, 1f, .06f);
            line.color = new Color(line.color.r, line.color.g, line.color.b, .6f);
        }

        // 숫자 타일 한 줄. tone 0 보통, 1 금색(강조), 2 초록(좋음), 3 빨강(나쁨)
        static void Tiles(Transform p, Font f, (string label, string value, int tone)[] items)
        {
            var row = MatchResultDialog.Block(p, 70);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            foreach (var it in items)
            {
                var tile = MatchResultDialog.NewRect("Tile", row);
                tile.gameObject.AddComponent<Image>().color = it.tone == 1 ? GoldFill : Tile;
                Color c = it.tone == 1 ? PixelTheme.Accent : it.tone == 2 ? UpText : it.tone == 3 ? DownText : PixelTheme.Text;
                MatchResultDialog.Fit(MatchResultDialog.Label(tile, f, it.value, 26, c, .04f, .36f, .96f, 1f, TextAnchor.MiddleCenter), 14);
                MatchResultDialog.Fit(MatchResultDialog.Label(tile, f, it.label, 15, PixelTheme.TextDim, .04f, 0f, .96f, .38f, TextAnchor.MiddleCenter), 11);
            }
        }

        // 시상 한 줄: 왼쪽 배지(상 이름), 오른쪽 내용. 받은 상은 금색.
        static void Award(Transform p, Font f, AwardRow a)
        {
            bool won = a.level > 0;
            var row = Box(p, won ? GoldRow : RowColor, a.level == 2 ? 44 : 36);
            bool hasLabel = !string.IsNullOrEmpty(a.label);
            if (hasLabel)
            {
                var badge = MatchResultDialog.Img(row, won ? PixelTheme.Accent : Tile, .015f, .12f, .32f, .88f);
                MatchResultDialog.Fit(MatchResultDialog.Label(badge.rectTransform, f, a.label, 16, won ? Dark : PixelTheme.TextDim, .04f, 0f, .96f, 1f, TextAnchor.MiddleCenter), 10);
            }
            MatchResultDialog.Fit(MatchResultDialog.Label(row, f, (won ? "★ " : "") + a.text, a.level == 2 ? 20 : 17, won ? PixelTheme.Accent : PixelTheme.TextDim,
                hasLabel ? .35f : .03f, 0f, .98f, 1f, hasLabel ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter), 11);
        }

        // 바뀌는 값: 왼쪽 이름, 오른쪽 "전 → 후"
        static void Change(Transform p, Font f, string label, string from, string to, Color toColor)
        {
            var b = Box(p, Tile, 58);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, label, 18, PixelTheme.TextDim, .03f, 0f, .36f, 1f, TextAnchor.MiddleLeft), 12);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, from, 22, PixelTheme.TextDim, .36f, 0f, .62f, 1f, TextAnchor.MiddleRight), 12);
            MatchResultDialog.Label(b, f, "→", 24, PixelTheme.Accent, .62f, 0f, .72f, 1f, TextAnchor.MiddleCenter);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, to, 26, toColor, .72f, 0f, .98f, 1f, TextAnchor.MiddleLeft), 12);
        }

        // 제목 + 작은 칩들 (한 줄에 4개)
        static void Chips(Transform p, Font f, string title, string[] items, Color fill, Color color)
        {
            var head = MatchResultDialog.Block(p, 28);
            MatchResultDialog.Label(head, f, title, 18, color, 0f, 0f, 1f, 1f, TextAnchor.MiddleLeft);
            const int PerRow = 4;
            for (int i = 0; i < items.Length; i += PerRow)
            {
                var row = MatchResultDialog.Block(p, 34);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                for (int k = 0; k < PerRow; k++)
                {
                    var chip = MatchResultDialog.NewRect("Chip", row);
                    if (i + k >= items.Length) continue;   // 빈 칸으로 폭을 맞춤
                    chip.gameObject.AddComponent<Image>().color = new Color(fill.r, fill.g, fill.b, .75f);
                    MatchResultDialog.Fit(MatchResultDialog.Label(chip, f, items[i + k], 16, color, .04f, 0f, .96f, 1f, TextAnchor.MiddleCenter), 10);
                }
            }
        }

        static void Warning(Transform p, Font f, string text)
        {
            Gap(p, 2);
            var b = Box(p, new Color(DownFill.r, DownFill.g, DownFill.b, .55f), 36);
            MatchResultDialog.Label(b, f, "! " + text, 18, DownText, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
        }

        static void Note(Transform p, Font f, string text, Color color, int size = 17, float height = 30)
        {
            var b = MatchResultDialog.Block(p, height);
            MatchResultDialog.Fit(MatchResultDialog.Label(b, f, text, size, color, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter), 11);
        }

        static RectTransform Box(Transform p, Color c, float height)
        {
            var b = MatchResultDialog.Block(p, height);
            b.gameObject.AddComponent<Image>().color = c;
            return b;
        }

        static void Gap(Transform p, float height) { MatchResultDialog.Block(p, height); }
    }
}
