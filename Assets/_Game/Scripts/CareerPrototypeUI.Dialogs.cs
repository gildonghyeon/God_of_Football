using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace MyFootballCareer
{
    // 팝업 창 (안내·카드형·확인·경기 결과·선수단·팀 고르기)
    public partial class CareerPrototypeUI
    {
        Transform BuildDialog(string title, string body, float bodyHeight, int bodyFont = 24)
        {
            if (overlay != null) Destroy(overlay);
            var root = NewRect("Dialog", safeRoot); Stretch(root); overlay = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            // 내용이 화면보다 길면 위아래로 스크롤하고, 짧으면 가운데에 둡니다.
            var view = NewRect("View", root); Stretch(view);
            view.offsetMin = new Vector2(0, 24); view.offsetMax = new Vector2(0, -24);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            var holder = NewRect("Holder", view);
            holder.anchorMin = new Vector2(.06f, 1f); holder.anchorMax = new Vector2(.94f, 1f);
            holder.pivot = new Vector2(.5f, 1f); holder.offsetMin = holder.offsetMax = Vector2.zero;
            ConfigureStack(holder.gameObject, 0, 0, 0, 0, 0);
            holder.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = holder; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;
            view.gameObject.AddComponent<DialogCenter>().Init(scroll, view, holder);
            var inner = Section(holder, title);
            var b = TextNode(body, inner, bodyFont); Height(b.gameObject, bodyHeight);
            return inner;
        }

        // 베스트 11 스쿼드: 4-3-3 포메이션 그림과 명단 (내 선수·우리 팀 선수는 금색)
        public Action<BestElevenPick, Action> OnBestElevenPlayer;   // 베스트 11에서 선수를 누르면 세부 능력치 (닫으면 다시 베스트 11)

        public void ShowBestEleven(BestElevenSquadView b)
        {
            if (b == null) return;
            var inner = BuildDialog("베스트 11", b.title, 30f, 18);
            var layout = TacticSystem.Layout(Formation.F433);
            var queues = new Dictionary<SquadRole, Queue<BestElevenPick>>();
            foreach (SquadRole r in Enum.GetValues(typeof(SquadRole))) queues[r] = new Queue<BestElevenPick>();
            foreach (var p in b.picks) queues[p.role].Enqueue(p);
            var sv = new SquadView { layout = layout };
            foreach (var slot in layout)
            {
                var q = queues[slot.role];
                var p = q.Count > 0 ? q.Dequeue() : null;
                sv.starters.Add(new SquadRowView { role = slot.role, name = p != null ? p.name : "-", rating = p != null ? p.avg : 0f, id = p != null ? b.picks.IndexOf(p) : -1 });
            }
            Action<BestElevenPick> open = p => { if (p != null && OnBestElevenPlayer != null) OnBestElevenPlayer(p, () => ShowBestEleven(b)); };
            SquadDialog.Pitch(inner, font, sv, r => { if (r.id >= 0) open(b.picks[r.id]); }, null, null, true);   // 점을 누르면 세부 능력치
            foreach (var p in b.picks)   // 명단: 포지션 · 이름 · 팀 · 평균 평점 (출전)
            {
                var row = NewRect("Pick", inner); Height(row.gameObject, 40);
                var bg = row.gameObject.AddComponent<Image>(); bg.color = p.mine ? new Color(.42f, .33f, .10f, 1f) : new Color(.12f, .16f, .25f, 1f);
                var rb = row.gameObject.AddComponent<Button>(); rb.targetGraphic = bg;   // 줄을 눌러도 세부 능력치
                var rc = rb.colors; rc.pressedColor = new Color(.65f, .65f, .65f, 1f); rc.fadeDuration = .06f; rb.colors = rc;
                var pick = p; rb.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); open(pick); });
                var t = TextNode("<color=#" + ColorUtility.ToHtmlStringRGB(SquadDialog.RoleColor(p.role)) + ">" + p.role + "</color>  " + p.name + (p.mine ? " ★" : "") + "  <color=#8A93A6>" + p.team + "</color>", row, 17);
                t.rectTransform.anchorMin = new Vector2(.02f, 0f); t.rectTransform.anchorMax = new Vector2(.76f, 1f); t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.alignment = TextAnchor.MiddleLeft; t.resizeTextForBestFit = true; t.resizeTextMinSize = 11; t.resizeTextMaxSize = 17; t.color = p.mine ? Color.white : PixelTheme.Text;
                var a = TextNode(p.avg.ToString("0.00") + "  (" + p.apps + "경기)", row, 16);
                a.rectTransform.anchorMin = new Vector2(.74f, 0f); a.rectTransform.anchorMax = new Vector2(.98f, 1f); a.rectTransform.offsetMin = a.rectTransform.offsetMax = Vector2.zero;
                a.alignment = TextAnchor.MiddleRight; a.color = p.mine ? PixelTheme.Accent : PixelTheme.Text;
            }
            if (b.picks.Count > 0) { var hint = TextNode("포메이션 그림이나 명단에서 선수를 누르면 세부 능력치를 볼 수 있습니다", inner, 15); hint.color = PixelTheme.TextDim; hint.alignment = TextAnchor.MiddleCenter; Height(hint.gameObject, 30); }
            if (b.picks.Count == 0) { var none = TextNode("출전 수가 기준(경기 수의 절반)을 채운 선수가 없습니다.", inner, 17); Height(none.gameObject, 40); none.color = PixelTheme.TextDim; }
            PixelButton("닫기", () => CloseDialog(), inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        void CloseDialog() { if (overlay != null) Destroy(overlay); overlay = null; }

        public void ShowPopup(string title, string body, float bodyHeight = 190f, Action onClose = null, int bodyFont = 24)
        {
            var inner = BuildDialog(title, body, bodyHeight, bodyFont);
            PixelButton("확인", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 카드형 팝업: build가 내용 칸(세로로 쌓임)을 채웁니다. 글자만 늘어놓는 대신 숫자 타일·배지로 보여 줍니다.
        public void ShowCards(string title, Action<Transform, Font> build, Action onClose = null, string okLabel = "확인")
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("CardBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 8);
            build(body, font);
            PixelButton(okLabel, () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 76f);
        }

        // 카드형 확인 창: 내용 + 실행 버튼 + 취소
        public void ShowCardsConfirm(string title, Action<Transform, Font> build, string yesLabel, Action onYes)
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("CardBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 8);
            build(body, font);
            PixelButton(yesLabel, () => { CloseDialog(); if (onYes != null) onYes(); }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("취소", () => CloseDialog(), inner, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 경기 결과 창: 결과 배지·큰 점수, 골 타임라인, 내 기록 타일, 보상 줄
        public void ShowMatchResult(string title, MatchResultData data, Action onClose)
        {
            if (matchView != null && matchView.LastBox != null && data != null) { data.box = matchView.LastBox; matchView.LastBox = null; }   // 방금 본 경기 영상의 팀 기록·MVP
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("ResultBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 4);
            MatchResultDialog.Build(body, font, data);
            PixelButton("확인", () => { CloseDialog(); if (onClose != null) onClose(); }, inner, 72f);
        }

        // 선수단 창: 팀 요약 카드, 주전(4-4-2)과 교체 선수 표
        // back: 팀 목록에서 열었으면 그 목록으로 돌아가는 버튼
        public void ShowSquad(string title, SquadView view, Action back = null)
        {
            var inner = BuildDialog(title, "", 0f);
            var body = NewRect("SquadBody", inner);
            ConfigureStack(body.gameObject, 0, 0, 0, 0, 3);
            SquadDialog.Build(body, font, view, r =>   // 선수를 누르면 세부 능력치, 닫으면 선수단으로 돌아옴
                ShowCards(r.name, (b, f) => PopupCards.PlayerDetail(b, f, r, view.division), () => ShowSquad(title, view, back), "◀ 뒤로"));
            if (back != null) PixelButton("◀ 뒤로", () => back(), inner, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            PixelButton("닫기", () => CloseDialog(), inner, 64f);
        }

        // 리그 팀 목록: 부를 고르는 버튼, 그 부의 순위 순서대로 팀 (순위·유니폼·이름·경기·승점). 팀을 누르면 선수단
        public void ShowLeagueTeams(LeagueTeamsView v)
        {
            var inner = BuildDialog(v.title, "부를 고르고, 팀을 누르면 포메이션과 선수 구성을 볼 수 있습니다.", 60f, 20);
            var row = NewRect("Divisions", inner); Height(row.gameObject, 56);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            for (int d = 1; d <= v.divisionCount; d++)
            {
                int div = d; bool on = d == v.division;
                var b = PixelButton(d + "부" + (d == v.myDivision ? " ★" : ""), () => { if (!on && OnPickLeague != null) OnPickLeague(div); }, row, 0f,
                    on ? PixelTheme.TabActive : PixelTheme.QuietFill, on ? PixelTheme.Accent : PixelTheme.QuietEdge);
                var t = b.GetComponentInChildren<Text>(); t.fontSize = 19; t.color = on ? PixelTheme.Accent : PixelTheme.Text;
            }
            var head = NewRect("Head", inner); Height(head.gameObject, 26);
            var hl = TextNode("순위   팀", head, 15); Stretch(hl.rectTransform); hl.color = PixelTheme.TextDim; hl.rectTransform.offsetMin = new Vector2(12, 0);
            var hr = TextNode(v.played > 0 ? v.played + "라운드 진행 · 승점" : "시즌 시작 전 · 승점", head, 15); Stretch(hr.rectTransform); hr.color = PixelTheme.TextDim; hr.alignment = TextAnchor.MiddleRight; hr.rectTransform.offsetMax = new Vector2(-12, 0);
            foreach (var r in v.rows)
            {
                string id = r.id;
                var b = PixelButton("", () => { CloseDialog(); if (OnShowSquad != null) OnShowSquad(id); }, inner, 60f,
                    r.me ? PixelTheme.AccentFill : PixelTheme.QuietFill, r.me ? PixelTheme.Accent : PixelTheme.QuietEdge);
                var fill = b.transform.GetChild(0);
                var label = b.GetComponentInChildren<Text>(); label.gameObject.SetActive(false);
                var rank = TextNode(r.pos.ToString(), fill, 22); rank.alignment = TextAnchor.MiddleCenter;
                rank.color = r.zone > 0 ? new Color(.45f, .9f, .55f, 1f) : r.zone < 0 ? new Color(1f, .5f, .5f, 1f) : PixelTheme.Text;
                Place(rank.rectTransform, 0f, .09f);
                TeamKits.PreviewImage(fill, TeamKits.Home(id), .10f, .06f, .19f, .94f);
                var name = TextNode(r.name + (r.me ? "  (내 팀)" : ""), fill, 21); name.color = r.me ? PixelTheme.Accent : PixelTheme.Text;
                Place(name.rectTransform, .21f, .66f);
                var rec = TextNode(r.w + "승 " + r.d + "무 " + r.l + "패", fill, 16); rec.color = PixelTheme.TextDim; rec.alignment = TextAnchor.MiddleRight;
                Place(rec.rectTransform, .62f, .84f);
                var pts = TextNode(r.pts.ToString(), fill, 24); pts.color = PixelTheme.Accent; pts.alignment = TextAnchor.MiddleRight;
                Place(pts.rectTransform, .84f, .97f);
            }
            PixelButton("닫기", () => CloseDialog(), inner, 60f);
        }

        static void Place(RectTransform r, float x0, float x1)
        {
            r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 1f); r.offsetMin = r.offsetMax = Vector2.zero;
        }

        // 팀 고르기 창: 팀 이름 버튼들 (누르면 그 팀 선수단)
        public void ShowTeamPicker(List<KeyValuePair<string, string>> teams, string title)
        {
            var inner = BuildDialog(title, "선수단을 볼 팀을 고르세요.", 34f, 22);
            foreach (var t in teams)
            {
                string id = t.Key;
                var teamButton = PixelButton(t.Value, () => { CloseDialog(); if (OnShowSquad != null) OnShowSquad(id); }, inner, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                teamButton.GetComponentInChildren<Text>().fontSize = 22;
                TeamKits.PreviewImage(teamButton.transform.GetChild(0), TeamKits.Home(id), .01f, .04f, .11f, .96f);   // 왼쪽에 팀 유니폼
            }
            PixelButton("닫기", () => CloseDialog(), inner, 60f);
        }

        // 되돌릴 수 없는 선택 앞에서 한 번 더 확인합니다.
        public void ShowConfirm(string title, string body, float bodyHeight, string yesLabel, Action onYes)
        {
            var inner = BuildDialog(title, body, bodyHeight);
            PixelButton(yesLabel, () => { CloseDialog(); if (onYes != null) onYes(); }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("취소", () => CloseDialog(), inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }
    }
}
