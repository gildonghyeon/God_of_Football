using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 경기 결과 창에 보여 줄 데이터
    public class MatchResultData
    {
        public string label;                 // 예: "4라운드 · 3부 리그", "친선 경기"
        public string own, opp, outcome;     // outcome: 승리 / 패배 / 무승부
        public int gf, ga;
        public List<GoalNote> goals = new List<GoalNote>();
        public bool bench;
        public float rating;
        public int myGoals, myAssists, shots, defense;
        public bool keeper;                  // 골키퍼: 수비 칸은 선방 수
        public bool teamMode;                // 나만의 팀 모드: 개인 기록 없음
        public string passText;              // 예: "16/21"
        public List<string> extras = new List<string>();   // 예: "주급 +2,640", "친선 경기 보상: 노력 포인트 +816"
    }

    // 경기 결과 창의 내용: 결과 배지와 큰 점수, 골 타임라인(분 표시), 내 기록 타일, 보상 줄.
    public static class MatchResultDialog
    {
        static readonly Color WinColor = new Color(.30f, .75f, .40f, 1f);
        static readonly Color LossColor = new Color(.85f, .35f, .35f, 1f);
        static readonly Color DrawColor = new Color(.55f, .60f, .70f, 1f);
        static readonly Color OurColor = new Color(.30f, .50f, .90f, 1f);
        static readonly Color TheirColor = new Color(.80f, .30f, .30f, 1f);
        static readonly Color Tile = new Color(.13f, .18f, .28f, 1f);
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f);

        public static Color OutcomeColor(string outcome) { return outcome == "승리" ? WinColor : outcome == "패배" ? LossColor : DrawColor; }

        // parent(세로로 쌓는 칸) 안에 결과 내용을 만듭니다.
        public static void Build(Transform parent, Font font, MatchResultData v)
        {
            Color oc = OutcomeColor(v.outcome);
            // 1) 점수 머리
            var head = Block(parent, 128);
            Label(head, font, v.label, 18, PixelTheme.TextDim, 0f, .78f, 1f, 1f, TextAnchor.MiddleCenter);
            var pill = Img(head, oc, .38f, .56f, .62f, .78f);
            Label(pill.rectTransform, font, v.outcome, 18, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
            Fit(Label(head, font, v.own, 22, new Color(.65f, .80f, 1f), 0f, .02f, .32f, .56f, TextAnchor.MiddleRight), 13);
            Label(head, font, v.gf + " - " + v.ga, 50, oc == DrawColor ? PixelTheme.Text : oc, .33f, 0f, .67f, .58f, TextAnchor.MiddleCenter);
            Fit(Label(head, font, v.opp, 22, new Color(1f, .70f, .70f), .68f, .02f, 1f, .56f, TextAnchor.MiddleLeft), 13);

            // 2) 골 타임라인
            if (v.goals != null && v.goals.Count > 0)
            {
                var sorted = new List<GoalNote>(v.goals);
                sorted.Sort((a, b) => a.minute.CompareTo(b.minute));
                foreach (var g in sorted)
                {
                    var row = Block(parent, 30);
                    row.gameObject.AddComponent<Image>().color = RowA;
                    var badge = Img(row, g.ours ? OurColor : TheirColor, .02f, .1f, .14f, .9f);
                    Label(badge.rectTransform, font, g.minute + "'", 17, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                    Fit(Label(row, font, g.scorer + (string.IsNullOrEmpty(g.how) ? "" : " [" + g.how + "]") + (string.IsNullOrEmpty(g.assist) ? "" : "  (도움 " + g.assist + ")"), 18,
                        g.ours ? PixelTheme.Text : new Color(1f, .78f, .78f), .17f, 0f, .7f, 1f, TextAnchor.MiddleLeft), 13);
                    Fit(Label(row, font, g.ours ? v.own : v.opp, 15, PixelTheme.TextDim, .7f, 0f, .98f, 1f, TextAnchor.MiddleRight), 11);
                }
            }
            else
            {
                var none = Block(parent, 28);
                Label(none, font, "골 없음", 18, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
            }

            // 3) 내 기록 타일
            Block(parent, 6);   // 간격
            if (v.teamMode) { }   // 나만의 팀 모드: 개인 기록 없음
            else if (v.bench)
            {
                var b = Block(parent, 56);
                b.gameObject.AddComponent<Image>().color = Tile;
                Label(b, font, "벤치 대기 (출전하지 못함)", 20, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
            }
            else
            {
                if (v.keeper)   // 골키퍼: 골·도움 대신 선방률·클린시트
                    Tiles(parent, font, new[] { ("평점", v.rating.ToString("0.0"), true), ("선방률", SeasonSystem.SaveRateText(v.defense, v.ga), false), ("클린시트", v.ga == 0 ? "O" : "-", v.ga == 0) });
                else Tiles(parent, font, new[] { ("평점", v.rating.ToString("0.0"), true), ("골", v.myGoals.ToString(), v.myGoals > 0), ("도움", v.myAssists.ToString(), v.myAssists > 0) });
                if (v.keeper) Tiles(parent, font, new[] { ("선방", v.defense.ToString(), v.defense > 0), ("패스", v.passText, false), ("실점", v.ga.ToString(), false) });
                else Tiles(parent, font, new[] { ("슈팅", v.shots.ToString(), false), ("패스", v.passText, false), ("수비", v.defense.ToString(), false) });
            }

            // 4) 보상 줄
            if (v.extras != null)
                foreach (var e in v.extras)
                {
                    var line = Block(parent, 28);
                    Label(line, font, e, 19, PixelTheme.Accent, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
                }
        }

        static void Tiles(Transform parent, Font font, (string label, string value, bool hot)[] items)
        {
            var row = Block(parent, 66);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            foreach (var it in items)
            {
                var tile = NewRect("Tile", row);
                tile.gameObject.AddComponent<Image>().color = it.hot ? new Color(.42f, .33f, .10f, 1f) : Tile;
                Label(tile, font, it.value, 26, it.hot ? PixelTheme.Accent : PixelTheme.Text, 0f, .36f, 1f, 1f, TextAnchor.MiddleCenter);
                Label(tile, font, it.label, 15, PixelTheme.TextDim, 0f, 0f, 1f, .38f, TextAnchor.MiddleCenter);
            }
        }

        // ---------- 공통 ----------
        // 칸보다 긴 글자는 칸에 맞게 작아지게 (팀 이름 등)
        internal static Text Fit(Text t, int min)
        {
            t.resizeTextForBestFit = true; t.resizeTextMinSize = min; t.resizeTextMaxSize = t.fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static RectTransform Block(Transform parent, float height)
        {
            var r = NewRect("Block", parent);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height;
            return r;
        }

        internal static Image Img(Transform parent, Color c, float x0, float y0, float x1, float y1)
        {
            var r = NewRect("Image", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var img = r.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        internal static Text Label(Transform parent, Font font, string value, int size, Color color, float x0, float y0, float x1, float y1, TextAnchor align)
        {
            var r = NewRect("Text", parent);
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value ?? ""; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
