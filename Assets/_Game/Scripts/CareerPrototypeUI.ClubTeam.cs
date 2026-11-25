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
        public int selected2 = int.MinValue;   // 두 번째로 고른 선수 (두 명이면 비교·교체)
    }

    // 나만의 팀 관리 화면과 방출 선수 고르기 창
    public partial class CareerPrototypeUI
    {
        public Action OnClubTeam, OnClubTeamBack;          // 팀 관리 열기, 나만의 팀 화면으로 돌아가기
        public Action<int> OnClubTap;                      // 선수 누르기 (선수 번호): 하나를 고르고 다른 선수를 누르면 자리를 바꿈
        public Action<int> OnClubPlayerInfo;               // 선택 막대: 세부 능력치 (한 명 골랐을 때)
        public Action OnClubCompare, OnClubSwap, OnClubClearSelection;   // 선택 막대: 두 명 골랐을 때 비교·교체, 취소
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

            currentParent = Section(content, "1군  " + TacticSystem.FormationName(v.formation), true);
            HintBar(v.selected == int.MinValue ? 0 : v.selected2 == int.MinValue ? 1 : 2);
            // 포메이션 그림 (선수단 창과 같은 그림, 점을 눌러도 고를 수 있음)
            var pv = new SquadView { layout = TacticSystem.Layout(v.formation) };
            foreach (var p in v.starters) pv.starters.Add(new SquadRowView { id = p.id, name = p.name, role = p.slotRole, rating = p.effective });   // 그 자리에서 내는 실력
            SquadDialog.Pitch(Parent, font, pv, r => { if (OnClubTap != null) OnClubTap(r.id); }, r => Mark(v, r.id),
                              r => { var p = v.starters.Find(o => o.id == r.id); return p != null ? p.role : r.role; });   // 본래 포지션
            ListHeader();
            PlayerGrid(SortByRole(v.starters, true), v, true);

            currentParent = Section(content, "교체", true);
            ListHeader();
            PlayerGrid(SortByRole(v.bench), v, false);
            currentParent = null;
            PixelButton("◀ 나만의 팀으로", () => { if (OnClubTeamBack != null) OnClubTeamBack(); }, content, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            status = Label("", 18, 36); status.color = PixelTheme.TextDim;

            ShowSelectionBar(v);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            content.anchoredPosition = keep;
        }

        // 선수를 고르면 화면 아래에 고정되는 큰 막대: 고른 선수, '세부 능력치'와 '선택 취소' 버튼 (손가락으로 누르기 쉽게)
        RectTransform selectionBar;
        const float SelectionBarHeight = 132f;

        void ShowSelectionBar(ClubTeamView v)
        {
            HideSelectionBar();
            ClubPlayerView sel = Find(v, v.selected), sel2 = Find(v, v.selected2);
            if (sel == null) return;
            selectionBar = NewRect("SelectionBar", safeRoot);
            selectionBar.anchorMin = new Vector2(0, 0); selectionBar.anchorMax = new Vector2(1, 0); selectionBar.pivot = new Vector2(.5f, 0);
            selectionBar.offsetMin = Vector2.zero; selectionBar.offsetMax = new Vector2(0, SelectionBarHeight);
            selectionBar.gameObject.AddComponent<Image>().color = PixelTheme.Accent;
            var inner = NewRect("Fill", selectionBar); Stretch(inner); inner.offsetMin = new Vector2(0, 0); inner.offsetMax = new Vector2(0, -4);
            inner.gameObject.AddComponent<Image>().color = new Color(.09f, .11f, .17f, 1f);
            var title = TextNode(sel2 == null
                ? "고른 선수  " + Ko.RoleTag(sel.role) + "  " + sel.name + (sel.legend ? " ★" : "") + "  ·  능력 " + Mathf.RoundToInt(sel.rating)
                : sel.name + (sel.legend ? " ★" : "") + " (" + Mathf.RoundToInt(sel.rating) + ")   <color=#8FD0FF>↔</color>   " + sel2.name + (sel2.legend ? " ★" : "") + " (" + Mathf.RoundToInt(sel2.rating) + ")", inner, 20);
            title.rectTransform.anchorMin = new Vector2(0, .58f); title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.offsetMin = new Vector2(16, 0); title.rectTransform.offsetMax = new Vector2(-16, 0);
            title.color = PixelTheme.Accent;
            var row = NewRect("Buttons", inner);
            row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, .58f); row.offsetMin = new Vector2(12, 10); row.offsetMax = new Vector2(-12, 0);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            int id = sel.id;
            if (sel2 == null)   // 한 명: 세부 능력치
                PixelButton("세부 능력치", () => { if (OnClubPlayerInfo != null) OnClubPlayerInfo(id); }, row, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge).GetComponentInChildren<Text>().fontSize = 22;
            else                // 두 명: 비교·교체
            {
                PixelButton("비교", () => { if (OnClubCompare != null) OnClubCompare(); }, row, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge).GetComponentInChildren<Text>().fontSize = 22;
                PixelButton("교체", () => { if (OnClubSwap != null) OnClubSwap(); }, row, 0f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge).GetComponentInChildren<Text>().fontSize = 22;
            }
            PixelButton("취소", () => { if (OnClubClearSelection != null) OnClubClearSelection(); }, row, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge).GetComponentInChildren<Text>().fontSize = 22;
            viewport.offsetMin = new Vector2(0, SelectionBarHeight);   // 내용이 막대에 가리지 않게
        }

        void HideSelectionBar()
        {
            if (selectionBar != null) Destroy(selectionBar.gameObject);
            selectionBar = null;
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
            for (int s = 0; s < 8; s += 2)                               // 잔디 줄무늬
            {
                var band = NewRect("Grass", pitch);
                band.anchorMin = new Vector2(0f, s / 8f); band.anchorMax = new Vector2(1f, (s + 1) / 8f); band.offsetMin = band.offsetMax = Vector2.zero;
                var bi = band.gameObject.AddComponent<Image>(); bi.color = new Color(.16f, .46f, .25f, 1f); bi.raycastTarget = false;
            }
            Line(0f, .985f, 1f, .99f);                                   // 하프라인 (위 끝)
            Line(.22f, .0f, .225f, .2f); Line(.775f, .0f, .78f, .2f); Line(.22f, .195f, .78f, .2f);   // 페널티 박스
            Line(.38f, .0f, .385f, .07f); Line(.615f, .0f, .62f, .07f); Line(.38f, .065f, .62f, .07f); // 골 에어리어
        }

        static readonly Color CardFill = new Color(.10f, .13f, .20f, .96f), OffEdge = new Color(.95f, .38f, .38f, 1f);

        static Color RoleColor(SquadRole r)
        {
            switch (r) { case SquadRole.GK: return new Color(.85f, .65f, .15f, 1f); case SquadRole.DF: return new Color(.30f, .50f, .90f, 1f); case SquadRole.MF: return new Color(.25f, .65f, .40f, 1f); default: return new Color(.85f, .35f, .35f, 1f); }
        }

        // 안내 막대: 고른 선수 수(0·1·2)에 따라 다음에 할 일을 안내
        void HintBar(int picked)
        {
            var bar = NewRect("Hint", Parent); Height(bar.gameObject, 40);
            bar.gameObject.AddComponent<Image>().color = picked > 0 ? SelectFill : new Color(.12f, .16f, .25f, 1f);
            string text = picked == 0 ? "선수를 누르면 고를 수 있습니다 · 두 명을 고르면 비교·교체"
                        : picked == 1 ? "한 명 더 고르면 비교하거나 자리를 바꿀 수 있습니다"
                        : "두 선수를 비교하거나 교체할 수 있습니다";
            var t = TextNode(text, bar, 17);
            Stretch(t.rectTransform); t.alignment = TextAnchor.MiddleCenter; t.color = picked > 0 ? PixelTheme.Accent : PixelTheme.TextDim;
        }

        // 카드 표시: 0 보통, 1 첫 번째로 고름(금색), 2 두 번째로 고름(하늘색)
        static int Mark(ClubTeamView v, int id) { return id == v.selected ? 1 : id == v.selected2 ? 2 : 0; }

        static ClubPlayerView Find(ClubTeamView v, int id)
        {
            if (id == int.MinValue) return null;
            foreach (var p in v.starters) if (p.id == id) return p;
            foreach (var p in v.bench) if (p.id == id) return p;
            return null;
        }

        // ---------- 선수 목록 (선수단 창과 같은 모양: 두 명씩 한 줄, 포지션 배지 · 이름 · 능력) ----------
        static readonly Color RowA = new Color(.12f, .16f, .25f, 1f), RowB = new Color(.10f, .14f, .22f, 1f);

        // 위에서부터 공격수 → 미드필더 → 수비수 → 골키퍼 (같은 포지션은 능력 순)
        static List<ClubPlayerView> SortByRole(List<ClubPlayerView> src, bool bySlot = false)
        {
            var list = new List<ClubPlayerView>(src);
            System.Func<ClubPlayerView, SquadRole> r = p => bySlot ? p.slotRole : p.role;   // 1군은 서 있는 자리 기준
            list.Sort((a, b) => r(a) != r(b) ? r(b).CompareTo(r(a)) : b.rating.CompareTo(a.rating));
            return list;
        }

        void ListHeader()
        {
            var row = NewRect("Head", Parent); Height(row.gameObject, 22);
            var t = TextNode("이름 · 능력", row, 14); Stretch(t.rectTransform); t.alignment = TextAnchor.MiddleRight; t.color = PixelTheme.TextDim;
        }

        void PlayerGrid(List<ClubPlayerView> list, ClubTeamView v, bool starters)
        {
            for (int i = 0; i < list.Count; i += 2)
            {
                var line = NewRect("Line", Parent); Height(line.gameObject, 44);
                var h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                for (int k = 0; k < 2; k++)
                {
                    if (i + k < list.Count) PlayerRow(line, list[i + k], Mark(v, list[i + k].id), (i / 2 + k) % 2 == 0, starters);
                    else NewRect("Empty", line);
                }
            }
        }

        // 한 칸: 포지션 배지(1군은 서 있는 자리) · 이름(★) · 능력. 고르면 금색/하늘색, 자기 포지션이 아닌 자리면 이름 옆에 본래 포지션(빨강)과 빨간 능력
        void PlayerRow(Transform parent, ClubPlayerView p, int mark, bool even, bool starter)
        {
            var cell = NewRect("Player", parent);
            var bg = cell.gameObject.AddComponent<Image>();
            bg.color = mark == 1 ? SelectFill : mark == 2 ? SecondFill : even ? RowA : RowB;
            var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
            var c = b.colors; c.pressedColor = new Color(.65f, .65f, .65f, 1f); c.fadeDuration = .06f; b.colors = c;
            b.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); if (OnClubTap != null) OnClubTap(p.id); });
            if (mark > 0)   // 고른 칸 테두리
            {
                var edge = mark == 1 ? PixelTheme.Accent : SecondEdge;
                foreach (var (x0, y0, x1, y1) in new[] { (0f, 0f, 1f, .06f), (0f, .94f, 1f, 1f), (0f, 0f, .012f, 1f), (.988f, 0f, 1f, 1f) })
                {
                    var e = NewRect("Edge", cell); e.anchorMin = new Vector2(x0, y0); e.anchorMax = new Vector2(x1, y1); e.offsetMin = e.offsetMax = Vector2.zero;
                    var ei = e.gameObject.AddComponent<Image>(); ei.color = edge; ei.raycastTarget = false;
                }
            }
            var badge = NewRect("Badge", cell); badge.anchorMin = new Vector2(.02f, .16f); badge.anchorMax = new Vector2(.2f, .84f); badge.offsetMin = badge.offsetMax = Vector2.zero;
            var bi = badge.gameObject.AddComponent<Image>(); var shown = starter ? p.slotRole : p.role;   // 1군은 지금 서 있는 자리의 포지션
            bi.color = RoleColor(shown); bi.raycastTarget = false;
            var bt = TextNode(shown.ToString(), badge, 14); Stretch(bt.rectTransform); bt.alignment = TextAnchor.MiddleCenter; bt.color = Color.white;
            bool off = starter && p.role != p.slotRole;
            var name = TextNode(p.name + (p.legend ? " ★" : "") + (off ? "  <color=#F26060>" + p.role + "</color>" : ""), cell, 17);
            name.rectTransform.anchorMin = new Vector2(.23f, 0f); name.rectTransform.anchorMax = new Vector2(.80f, 1f); name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
            name.color = mark == 1 ? PixelTheme.Accent : mark == 2 ? SecondEdge : PixelTheme.Text;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 11; name.resizeTextMaxSize = 17;
            var val = TextNode(Mathf.RoundToInt(starter ? p.effective : p.rating).ToString(), cell, 19);
            val.rectTransform.anchorMin = new Vector2(.78f, 0f); val.rectTransform.anchorMax = new Vector2(.97f, 1f); val.rectTransform.offsetMin = val.rectTransform.offsetMax = Vector2.zero;
            val.alignment = TextAnchor.MiddleRight; val.color = off ? OffEdge : PixelTheme.Text;
        }

        // 선수 카드 한 장: 위쪽 포지션 색 띠(자리 포지션), 이름, 능력. 고르면 금색, 맞지 않는 자리면 빨간 테두리
        static readonly Color SecondFill = new Color(.12f, .30f, .45f, 1f), SecondEdge = new Color(.56f, .82f, 1f, 1f);

        Button PlayerCard(Transform parent, ClubPlayerView p, int mark, SquadRole shown, float value, bool off, float height)
        {
            bool selected = mark > 0;
            var b = PixelButton("", () => { if (OnClubTap != null) OnClubTap(p.id); }, parent, height,
                mark == 1 ? SelectFill : mark == 2 ? SecondFill : CardFill, mark == 1 ? PixelTheme.Accent : mark == 2 ? SecondEdge : off ? OffEdge : new Color(.25f, .30f, .42f, 1f));
            b.GetComponentInChildren<Text>().gameObject.SetActive(false);
            var fill = (RectTransform)b.transform.GetChild(0);
            var strip = NewRect("Role", fill); strip.anchorMin = new Vector2(0f, .68f); strip.anchorMax = new Vector2(1f, 1f); strip.offsetMin = strip.offsetMax = Vector2.zero;
            var si = strip.gameObject.AddComponent<Image>(); si.color = RoleColor(shown); si.raycastTarget = false;
            var role = TextNode(shown.ToString() + (shown != p.role ? "  (" + p.role + ")" : ""), strip, 14); Stretch(role.rectTransform);
            role.alignment = TextAnchor.MiddleCenter; role.color = Color.white;
            var name = TextNode(Short(p.name) + (p.legend ? " ★" : ""), fill, 16);
            name.rectTransform.anchorMin = new Vector2(0f, 0f); name.rectTransform.anchorMax = new Vector2(.72f, .68f);
            name.rectTransform.offsetMin = new Vector2(8, 0); name.rectTransform.offsetMax = Vector2.zero;
            name.color = mark == 1 ? PixelTheme.Accent : mark == 2 ? SecondEdge : PixelTheme.Text;
            var val = TextNode(Mathf.RoundToInt(value).ToString(), fill, 22);
            val.rectTransform.anchorMin = new Vector2(.68f, 0f); val.rectTransform.anchorMax = new Vector2(1f, .68f);
            val.rectTransform.offsetMin = Vector2.zero; val.rectTransform.offsetMax = new Vector2(-8, 0);
            val.alignment = TextAnchor.MiddleRight; val.color = off ? OffEdge : PixelTheme.Accent;
            return b;
        }

        void PitchPlayer(RectTransform pitch, ClubPlayerView p, int mark)
        {
            bool off = p.role != p.slotRole;
            var b = PlayerCard(pitch, p, mark, p.slotRole, p.effective, off, 0f);
            var r = (RectTransform)b.transform;
            float ax = Mathf.Clamp(p.x / 68f, .12f, .88f), ay = Mathf.Clamp(.07f + p.depth / .78f * .86f, .07f, .92f);
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.sizeDelta = new Vector2(150f, 80f);
        }

        void BenchPlayer(Transform parent, ClubPlayerView p, int mark)
        {
            PlayerCard(parent, p, mark, p.role, p.rating, false, 0f);
        }

        static string Short(string name) { return name != null && name.Length > 9 ? name.Substring(0, 9) : name; }

        // 선수 고르기 창 (비교·교체 상대): 1군/교체 표시, 포지션 색, 이름, 능력
        public void ShowPlayerPicker(string title, string body, List<ClubPlayerView> rows, HashSet<int> starters, Action<int> pick)
        {
            var inner = BuildDialog(title, body, 40f, 20);
            foreach (var p in rows)
            {
                bool st = starters.Contains(p.id);
                var b = PixelButton((st ? "1군  " : "교체  ") + Ko.RoleTag(p.role) + "   " + p.name + (p.legend ? " ★" : "") + "   능력 " + Mathf.RoundToInt(p.rating),
                    () => { CloseDialog(); pick(p.id); }, inner, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                var t = b.GetComponentInChildren<Text>(); t.fontSize = 20; t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(16, 0);
            }
            PixelButton("취소", () => CloseDialog(), inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

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
