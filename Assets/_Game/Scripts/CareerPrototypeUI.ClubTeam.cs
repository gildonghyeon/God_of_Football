using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 팀 관리 화면에 보여 줄 선수 한 명
    public class ClubPlayerView
    {
        public int id;
        public string name;
        public SquadRole role;          // 선수 포지션
        public SquadRole slotRole;      // 주전이면 서 있는 자리의 포지션
        public SlotKind kind;           // 주전이면 자리 역할 (센터백, 윙어…)
        public float x, depth;          // 주전이면 경기장 위 위치 (가로 0~68m, 우리 골대에서 깊이 0~1)
        public float rating, effective; // 실력, 그 자리에서 내는 실력
        public bool legend;             // 내가 키운 선수
    }

    // 나만의 팀 관리 화면 (포메이션·전술 스타일·주전 11명·교체 8명)
    public class ClubTeamView
    {
        public string name;
        public Formation formation;
        public TacticStyle style;
        public float strength, level;
        public List<ClubPlayerView> starters = new List<ClubPlayerView>(), bench = new List<ClubPlayerView>();
        public int selected = int.MinValue;    // 고른 선수 (없으면 MinValue)
    }

    // 나만의 팀 관리 화면과 방출 선수 고르기 창
    public partial class CareerPrototypeUI
    {
        public Action OnClubTeam, OnClubTeamBack;          // 팀 관리 열기, 나만의 팀 화면으로 돌아가기
        public Action<int> OnClubTap;                      // 선수 누르기 (선수 번호): 하나를 고르고 다른 선수를 누르면 자리를 바꿈
        public Action<Formation> OnClubFormation;
        public Action<TacticStyle> OnClubStyle;

        static readonly Color PitchGreen = new Color(.14f, .42f, .22f, 1f), PitchLine = new Color(1f, 1f, 1f, .35f);
        static readonly Color SelectFill = new Color(.55f, .42f, .10f, 1f), OffFill = new Color(.45f, .16f, .16f, 1f);

        public void ShowClubTeam(ClubTeamView v)
        {
            // 다시 그려도 보던 위치를 지키도록 스크롤 위치를 기억
            Vector2 keep = content != null ? content.anchoredPosition : Vector2.zero;
            CloseDialog();
            Clear();
            Label("팀관리/전술", 30, 54).color = PixelTheme.Accent;
            Label(v.name + " · 전력 " + v.strength.ToString("0") + " · 1군 평균 " + v.level.ToString("0"), 20, 34).color = PixelTheme.Text;

            currentParent = Section(content, "포메이션", true);
            ChoiceRow((Formation[])Enum.GetValues(typeof(Formation)), f => TacticSystem.FormationName(f), f => f == v.formation, f => { if (OnClubFormation != null) OnClubFormation(f); });
            currentParent = Section(content, "전술 스타일", true);
            ChoiceRow((TacticStyle[])Enum.GetValues(typeof(TacticStyle)), s => TacticSystem.StyleName(s), s => s == v.style, s => { if (OnClubStyle != null) OnClubStyle(s); });

            currentParent = Section(content, "1군 11명", true);
            Label(v.selected == int.MinValue ? "선수를 누른 뒤 바꿀 선수를 누르면 자리가 바뀝니다." : "바꿀 선수를 누르세요. (같은 선수를 다시 누르면 취소)", 17, 30).color = v.selected == int.MinValue ? PixelTheme.TextDim : PixelTheme.Accent;
            var pitch = NewRect("Pitch", Parent);
            pitch.gameObject.AddComponent<Image>().color = PitchGreen;
            Height(pitch.gameObject, 600);
            PitchLines(pitch);
            foreach (var p in v.starters) PitchPlayer(pitch, p, p.id == v.selected);

            currentParent = Section(content, "교체 " + v.bench.Count + "명", true);
            foreach (var p in v.bench) BenchPlayer(Parent, p, p.id == v.selected);
            Label("빨간 칸: 자기 포지션이 아닌 자리라 실력을 다 내지 못합니다.", 16, 30).color = PixelTheme.TextDim;
            currentParent = null;
            PixelButton("◀ 나만의 팀으로", () => { if (OnClubTeamBack != null) OnClubTeamBack(); }, content, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            status = Label("", 18, 36); status.color = PixelTheme.TextDim;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = keep;
        }

        // 한 줄에 고르기 버튼들 (고른 것은 금색)
        void ChoiceRow<T>(T[] items, Func<T, string> label, Func<T, bool> on, Action<T> pick)
        {
            var row = NewRect("Choices", Parent); Height(row.gameObject, 60);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            foreach (var it in items)
            {
                var item = it;
                bool sel = on(item);
                var b = PixelButton(label(item), () => pick(item), row, 0f, sel ? PixelTheme.RebirthFill : PixelTheme.QuietFill, sel ? PixelTheme.RebirthEdge : PixelTheme.QuietEdge);
                var t = b.GetComponentInChildren<Text>(); t.fontSize = 18; t.color = sel ? PixelTheme.Accent : PixelTheme.Text;
            }
        }

        // 경기장 선: 하프라인, 우리 쪽 페널티 박스 (아래가 우리 골대)
        void PitchLines(RectTransform pitch)
        {
            void Line(float x0, float y0, float x1, float y1)
            {
                var r = NewRect("Line", pitch);
                r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
                var img = r.gameObject.AddComponent<Image>(); img.color = PitchLine; img.raycastTarget = false;
            }
            Line(0f, .985f, 1f, .99f);                                   // 하프라인 (위 끝)
            Line(.22f, .0f, .225f, .2f); Line(.775f, .0f, .78f, .2f); Line(.22f, .195f, .78f, .2f);   // 페널티 박스
            Line(.38f, .0f, .385f, .07f); Line(.615f, .0f, .62f, .07f); Line(.38f, .065f, .62f, .07f); // 골 에어리어
        }

        void PitchPlayer(RectTransform pitch, ClubPlayerView p, bool selected)
        {
            bool off = p.role != p.slotRole;
            var b = PixelButton(Short(p.name) + (p.legend ? " ★" : "") + "\n" + p.slotRole.ToString() + " · " + Mathf.RoundToInt(p.effective),
                () => { if (OnClubTap != null) OnClubTap(p.id); }, pitch, 0f,
                selected ? SelectFill : off ? OffFill : PixelTheme.QuietFill, selected ? PixelTheme.Accent : off ? new Color(.9f, .4f, .4f, 1f) : PixelTheme.QuietEdge);
            var r = (RectTransform)b.transform;
            float ax = Mathf.Clamp(p.x / 68f, .1f, .9f), ay = Mathf.Clamp(.07f + p.depth / .78f * .86f, .06f, .93f);
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.sizeDelta = new Vector2(158f, 72f);
            var t = b.GetComponentInChildren<Text>(); t.fontSize = 16; t.color = selected ? PixelTheme.Accent : PixelTheme.Text;
        }

        void BenchPlayer(Transform parent, ClubPlayerView p, bool selected)
        {
            var b = PixelButton(Ko.RoleTag(p.role) + "   " + p.name + (p.legend ? " ★" : "") + "   평점 " + Mathf.RoundToInt(p.rating),
                () => { if (OnClubTap != null) OnClubTap(p.id); }, parent, 58f, selected ? SelectFill : PixelTheme.QuietFill, selected ? PixelTheme.Accent : PixelTheme.QuietEdge);
            var t = b.GetComponentInChildren<Text>(); t.fontSize = 20; t.alignment = TextAnchor.MiddleLeft;
            t.rectTransform.offsetMin = new Vector2(18, 0);
            t.color = selected ? PixelTheme.Accent : PixelTheme.Text;
        }

        static string Short(string name) { return name != null && name.Length > 9 ? name.Substring(0, 9) : name; }

        // 영입할 때 내보낼 선수 고르기: 선수단 19명 중 한 명을 누르면 pick(선수 번호)
        public void ShowReleasePicker(string newcomer, List<ClubPlayerView> squad, HashSet<int> starters, Action<int> pick)
        {
            var inner = BuildDialog("누구를 방출할까요?", newcomer + "을(를) 영입하려면 선수단(19명)에서 한 명을 내보내야 합니다.\n1군 선수를 내보내면 새 선수가 그 자리에 들어갑니다.", 96f, 20);
            foreach (var p in squad)
            {
                bool st = starters.Contains(p.id);
                var b = PixelButton((st ? "1군  " : "교체  ") + Ko.RoleTag(p.role) + "   " + p.name + (p.legend ? " ★" : "") + "   평점 " + Mathf.RoundToInt(p.rating),
                    () => { CloseDialog(); pick(p.id); }, inner, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                var t = b.GetComponentInChildren<Text>(); t.fontSize = 20; t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(16, 0);
            }
            PixelButton("취소", () => CloseDialog(), inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }
    }
}
