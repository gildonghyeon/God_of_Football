using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 나만의 팀 모드 화면에 보여 줄 데이터
    public class ClubModeView
    {
        public string name, tactic, division;
        public int kit, season, legends;
        public int squadCount;              // 선수단 인원 (최대 19)
        public float strength;
        public bool complete;               // 이번 시즌 경기를 모두 마침
        public string nextText = "";        // 다음 경기 안내 (예: 5라운드 · 홈 · 상대)
        public string nextOpponentId;       // 다음 경기 상대 (시즌이 끝났으면 null)
        public int round, rounds;
        public int w, d, l, gf, ga;
        public List<TableRowView> table = new List<TableRowView>();
        public List<string> recent = new List<string>();          // 최근 경기 (예: "승 2-1 상대")
        public List<RetiredPlayer> free = new List<RetiredPlayer>();   // 무료 영입 후보
        public List<SeasonSummary> history = new List<SeasonSummary>();
        public string leadersHeader;                                // 개인 순위 (우리 팀 선수끼리)
        public List<LeaderBlockView> leaders;
        public BestElevenSquadView best;                            // 시즌이 끝나면 베스트 11 스쿼드
    }

    // 게임을 켰을 때의 모드 선택 화면에 보여 줄 내용
    public class ModeSelectView
    {
        public string playerName, playerLine;     // 선수 키우기: 이름, "공격수 · 팀 · 2부 · 시즌 3"
        public int hair, skin, kit, boots;
        public bool gloves;                        // 키우는 선수가 골키퍼
        public bool noPlayer;                      // 육성 중인 선수 없음 (은퇴 뒤): 그림은 그림자처럼
        public bool hasClub;                       // 나만의 팀이 있으면 선택 가능
        public string clubName = "", clubLine = "";
        public int clubKit, legends, legendNeed;
    }

    // 나만의 팀 모드 화면의 카드들 (버튼은 CareerPrototypeUI가 붙임)
    public static class ClubModeScreen
    {
        static readonly Color Tile = new Color(.13f, .18f, .28f, 1f);
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f), RowB = new Color(.10f, .14f, .22f, 1f);
        static readonly Color MeRow = new Color(.42f, .33f, .10f, 1f);
        static readonly Color UpZone = new Color(.35f, .85f, .5f, 1f), DownZone = new Color(.95f, .4f, .4f, 1f);

        static RectTransform Box(Transform p, Color c, float h)
        {
            var b = MatchResultDialog.Block(p, h);
            var img = b.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return b;
        }
        static Text L(Transform p, Font f, string s, int size, Color c, float x0, float y0, float x1, float y1, TextAnchor a, int min = 10)
        { return MatchResultDialog.Fit(MatchResultDialog.Label(p, f, s, size, c, x0, y0, x1, y1, a), min); }

        // 팀 카드: 유니폼, 이름, 부·시즌, 전술, 전력, 이번 시즌 성적
        public static void Header(Transform p, Font f, ClubModeView v)
        {
            var b = Box(p, Tile, 150);
            TeamKits.PreviewImage(b, v.kit, .02f, .06f, .20f, .94f);
            L(b, f, v.name, 34, PixelTheme.Accent, .23f, .62f, .97f, .96f, TextAnchor.MiddleLeft, 18);
            L(b, f, v.division + " · 시즌 " + v.season, 20, PixelTheme.Text, .23f, .38f, .97f, .62f, TextAnchor.MiddleLeft, 12);
            L(b, f, v.tactic + " · 전력 " + v.strength.ToString("0") + " · 내가 키운 선수 " + v.legends + "명", 16, PixelTheme.TextDim, .23f, .06f, .97f, .36f, TextAnchor.MiddleLeft, 10);
            var row = MatchResultDialog.Block(p, 64);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            foreach (var (label, value) in new[] { ("승 · 무 · 패", v.w + " · " + v.d + " · " + v.l), ("득점 · 실점", v.gf + " · " + v.ga), ("진행", v.round + " / " + v.rounds) })
            {
                var t = MatchResultDialog.NewRect("Tile", row);
                t.gameObject.AddComponent<Image>().color = Tile;
                L(t, f, value, 24, PixelTheme.Text, .04f, .38f, .96f, 1f, TextAnchor.MiddleCenter, 12);
                L(t, f, label, 14, PixelTheme.TextDim, .04f, 0f, .96f, .40f, TextAnchor.MiddleCenter, 10);
            }
        }

        // 다음 경기 카드. 상대가 있으면 카드를 누를 수 있음 (onOpponent)
        public static void Next(Transform p, Font f, ClubModeView v, System.Action onOpponent = null)
        {
            var b = Box(p, Tile, 62);
            if (onOpponent != null && !v.complete)
            {
                var img = b.GetComponent<Image>(); img.raycastTarget = true;
                var button = b.gameObject.AddComponent<Button>(); button.targetGraphic = img;
                var c = button.colors; c.pressedColor = new Color(.65f, .65f, .65f, 1f); c.fadeDuration = .06f; button.colors = c;
                button.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); onOpponent(); });
                TeamKits.PreviewImage(b, TeamKits.Opponent(ClubSystem.ClubId, v.nextOpponentId), .02f, .06f, .12f, .94f);
                L(b, f, "누르면 상대 팀 선수단 ›", 14, PixelTheme.TextDim, .55f, .02f, .98f, .36f, TextAnchor.MiddleRight, 10);
            }
            L(b, f, v.complete ? "시즌 종료! 다음 시즌을 시작하세요." : v.nextText, 20, v.complete ? PixelTheme.Accent : PixelTheme.Text, .03f, 0f, .97f, 1f, TextAnchor.MiddleCenter, 12);
            if (v.recent.Count > 0)
            {
                var r = MatchResultDialog.Block(p, 28);
                L(r, f, "최근: " + string.Join("   ", v.recent), 15, PixelTheme.TextDim, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter, 10);
            }
        }

        public static void Table(Transform p, Font f, ClubModeView v)
        {
            var head = MatchResultDialog.Block(p, 24);
            L(head, f, "순위", 14, PixelTheme.TextDim, .01f, 0f, .10f, 1f, TextAnchor.MiddleCenter);
            L(head, f, "팀", 14, PixelTheme.TextDim, .12f, 0f, .60f, 1f, TextAnchor.MiddleLeft);
            L(head, f, "승 · 무 · 패", 14, PixelTheme.TextDim, .60f, 0f, .80f, 1f, TextAnchor.MiddleCenter);
            L(head, f, "득실", 14, PixelTheme.TextDim, .80f, 0f, .89f, 1f, TextAnchor.MiddleCenter);
            L(head, f, "승점", 14, PixelTheme.TextDim, .89f, 0f, .99f, 1f, TextAnchor.MiddleCenter);
            for (int i = 0; i < v.table.Count; i++)
            {
                var t = v.table[i];
                var r = Box(p, t.me ? MeRow : i % 2 == 0 ? RowA : RowB, 32);
                if (t.zone != 0) MatchResultDialog.Img(r, t.zone > 0 ? UpZone : DownZone, 0f, 0f, .008f, 1f);
                L(r, f, t.pos.ToString(), 17, PixelTheme.Text, .01f, 0f, .10f, 1f, TextAnchor.MiddleCenter);
                L(r, f, t.name, 16, t.me ? PixelTheme.Accent : PixelTheme.Text, .12f, 0f, .60f, 1f, TextAnchor.MiddleLeft);
                L(r, f, t.w + " · " + t.d + " · " + t.l, 16, PixelTheme.Text, .60f, 0f, .80f, 1f, TextAnchor.MiddleCenter);
                L(r, f, (t.gd > 0 ? "+" : "") + t.gd, 16, PixelTheme.TextDim, .80f, 0f, .89f, 1f, TextAnchor.MiddleCenter);
                L(r, f, t.pts.ToString(), 17, PixelTheme.Accent, .89f, 0f, .99f, 1f, TextAnchor.MiddleCenter);
            }
        }

        // 무료 영입 후보 한 줄: 모습, 이름·포지션·평점, 영입 버튼 자리(오른쪽 28%는 비워 둠). 줄 자체를 돌려줍니다.
        public static RectTransform FreeRow(Transform p, Font f, RetiredPlayer lg, int index)
        {
            var r = Box(p, index % 2 == 0 ? RowA : RowB, 76);
            var sprite = PopupCards.LookSprite(lg.hairColor, lg.skinTone, lg.kitColor, lg.bootColor, r.gameObject, lg.position == Position.Goalkeeper);
            if (sprite != null) { var img = MatchResultDialog.Img(r, Color.white, .01f, .04f, .12f, .96f); img.sprite = sprite; img.preserveAspect = true; }
            L(r, f, lg.name, 20, PixelTheme.Text, .14f, .50f, .70f, .96f, TextAnchor.MiddleLeft, 12);
            L(r, f, Ko.Pos(lg.position) + " · 평점 " + Mathf.RoundToInt(lg.rating) + " · " + ClubSystem.LegendLine(lg), 14, PixelTheme.TextDim, .14f, .06f, .70f, .50f, TextAnchor.MiddleLeft, 10);
            return r;
        }

        // 모드 카드: 왼쪽 그림(선수 모습 또는 팀 유니폼), 오른쪽 모드 이름·설명. 비활성이면 흐리게
        // shadow: 선수 그림을 검은 그림자로 (빈 자리)
        public static RectTransform ModeCard(Transform p, Font f, string mode, string title, string line, Sprite art, int kit, bool enabled, bool shadow = false)
        {
            var b = Box(p, enabled ? Tile : new Color(.09f, .11f, .16f, 1f), 200);
            Color dim = shadow ? new Color(0f, 0f, 0f, .55f) : enabled ? Color.white : new Color(1f, 1f, 1f, .35f);
            if (art != null) { var img = MatchResultDialog.Img(b, dim, .05f, .15f, .28f, .85f); img.sprite = art; img.preserveAspect = true; }   // 나만의 팀 카드의 선수 그림과 같은 크기
            else if (kit > 0) { var k = TeamKits.PreviewImage(b, kit, .05f, .15f, .28f, .85f); if (k != null) k.color = dim; }
            else L(b, f, "?", 64, PixelTheme.TextDim, .03f, .05f, .30f, .95f, TextAnchor.MiddleCenter, 30);
            L(b, f, mode, 18, enabled ? PixelTheme.TextDim : new Color(.5f, .5f, .55f, 1f), .34f, .74f, .97f, .94f, TextAnchor.MiddleLeft, 12);
            L(b, f, title, shadow ? 26 : 34, shadow ? PixelTheme.TextDim : enabled ? PixelTheme.Accent : new Color(.45f, .45f, .5f, 1f), .34f, .40f, .97f, .74f, TextAnchor.MiddleLeft, 16);
            L(b, f, line, 17, enabled ? PixelTheme.Text : new Color(.45f, .45f, .5f, 1f), .34f, .08f, .97f, .40f, TextAnchor.MiddleLeft, 11);
            return b;
        }

        // 나만의 팀 시즌 결산
        public static void SeasonEnd(Transform p, Font f, string club, int position, int teams, string division, int move, int w, int d, int l, int gf, int ga)
        {
            var b = Box(p, Tile, 130);
            L(b, f, club + " · " + division, 18, PixelTheme.TextDim, .03f, .74f, .97f, .97f, TextAnchor.MiddleCenter, 12);
            L(b, f, position + "위", 56, position == 1 ? PixelTheme.Accent : PixelTheme.Text, .03f, .22f, .97f, .76f, TextAnchor.MiddleCenter, 24);
            L(b, f, teams + "팀 중", 16, PixelTheme.TextDim, .03f, .03f, .97f, .24f, TextAnchor.MiddleCenter, 10);
            if (move != 0)
            {
                var m = Box(p, move > 0 ? new Color(.16f, .40f, .24f, 1f) : new Color(.45f, .16f, .16f, 1f), 44);
                L(m, f, move > 0 ? "▲ 승격!" : "▼ 강등", 22, Color.white, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
            }
            var r = Box(p, RowA, 40);
            L(r, f, w + "승 " + d + "무 " + l + "패 · 득점 " + gf + " 실점 " + ga, 18, PixelTheme.Text, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
        }

        public static void Signed(Transform p, Font f, RetiredPlayer lg, string club, float before, float after)
        {
            var b = Box(p, Tile, 170);
            var sprite = PopupCards.LookSprite(lg.hairColor, lg.skinTone, lg.kitColor, lg.bootColor, b.gameObject, lg.position == Position.Goalkeeper);
            if (sprite != null) { var img = MatchResultDialog.Img(b, Color.white, .02f, .03f, .32f, .97f); img.sprite = sprite; img.preserveAspect = true; }
            L(b, f, club + " 무료 영입", 16, PixelTheme.TextDim, .35f, .72f, .97f, .94f, TextAnchor.MiddleLeft, 11);
            L(b, f, lg.name, 36, PixelTheme.Accent, .35f, .38f, .97f, .72f, TextAnchor.MiddleLeft, 18);
            L(b, f, Ko.Pos(lg.position) + " · 평점 " + Mathf.RoundToInt(lg.rating), 18, PixelTheme.Text, .35f, .10f, .97f, .38f, TextAnchor.MiddleLeft, 12);
            var r = Box(p, RowA, 40);
            L(r, f, "팀 전력 " + before.ToString("0") + " → " + after.ToString("0"), 20, after > before ? new Color(.55f, .95f, .65f, 1f) : PixelTheme.Text, 0f, 0f, 1f, 1f, TextAnchor.MiddleCenter);
        }
    }
}
