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
    // 시작 화면(모드 고르기)과 나만의 팀 모드 화면, 창단 창
    public partial class CareerPrototypeUI
    {
        // ---------- 시작 화면: 모드 고르기 ----------
        // 위는 선수 키우기(지금 키우는 선수), 아래는 나만의 팀. 나만의 팀은 창단 전이면 누를 수 없음
        public void ShowModeSelect(ModeSelectView v)
        {
            CloseDialog();
            Clear();
            Label("나의 축구 커리어", 34, 70).color = PixelTheme.Accent;
            Label("플레이할 모드를 고르세요", 22, 44).color = PixelTheme.TextDim;
            currentParent = Section(content, "", true);
            var art = PopupCards.LookSprite(v.hair, v.skin, v.kit, v.boots, ((Component)Parent).gameObject, v.gloves && !v.noPlayer);
            ClubModeScreen.ModeCard(Parent, font, "선수 키우기", v.playerName, v.playerLine, art, 0, true, v.noPlayer);
            PixelButton("선수 키우기 시작", () => { if (OnPickCareer != null) OnPickCareer(); }, Parent, 84f);
            currentParent = Section(content, "", true);
            ClubModeScreen.ModeCard(Parent, font, "나만의 팀", v.hasClub ? v.clubName : "창단 전", v.hasClub ? v.clubLine
                : "은퇴 선수 " + Mathf.Min(v.legends, v.legendNeed) + " / " + v.legendNeed + "명" + (v.legends >= v.legendNeed ? " · 창단할 수 있습니다!" : " · 모이면 창단"), null, v.hasClub ? v.clubKit : 0, v.hasClub);
            if (v.hasClub) PixelButton("나만의 팀 시작", () => { if (OnPickClub != null) OnPickClub(); }, Parent, 84f);
            else if (v.legends >= v.legendNeed) PixelButton("나만의 팀 만들기", () => ShowFoundDialog(), Parent, 84f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);   // 나만의 팀이 없을 때 여기서 창단
            else
            {
                var locked = PixelButton("나만의 팀 만들기 · 은퇴 선수 " + v.legends + "/" + v.legendNeed, () => SetStatus("은퇴 선수 " + v.legendNeed + "명이 모이면 나만의 팀을 만들 수 있습니다."), Parent, 84f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                locked.GetComponentInChildren<Text>().color = PixelTheme.TextDim;
                locked.GetComponentInChildren<Text>().fontSize = 22;
            }
            currentParent = null;
            status = Label("", 20, 50); status.color = PixelTheme.TextDim;
        }

        // ---------- 나만의 팀 모드 ----------
        // 선수 키우기와 다른 모드: 내가 키운 선수들로 꾸린 팀으로 리그를 치릅니다. 위에 '팀'과 '설정' 탭.
        int clubTab;                // 0 팀, 1 설정
        public void ResetClubTab() { clubTab = 0; }   // 나만의 팀 모드에 새로 들어올 때는 팀 탭부터
        ClubModeView lastClubView;

        public void ShowClubMode(ClubModeView v)
        {
            lastClubView = v;
            CloseDialog();
            Clear();
            Label("나만의 팀 모드", 30, 54).color = PixelTheme.Accent;
            ShowClubBar();   // 아래쪽 탭: 팀 / 설정 (선수 키우기 탭과 같은 자리·모양)
            if (clubTab == 1)
            {
                BuildSettings(content, true);
                currentParent = null;
                status = Label("", 18, 36); status.color = PixelTheme.TextDim;
                return;
            }
            currentParent = Section(content, "", true);
            ClubModeScreen.Header(Parent, font, v);
            currentParent = Section(content, "다음 경기", true);
            ClubModeScreen.Next(Parent, font, v, v.nextOpponentId != null ? (Action)(() => { if (OnShowSquad != null) OnShowSquad(v.nextOpponentId); }) : null);
            PixelButton(v.complete ? "다음 시즌 시작" : "경기 시작", () => { if (OnClubPlay != null) OnClubPlay(); }, Parent, 84f);
            PixelButton("팀관리/전술", () => { if (OnClubTeam != null) OnClubTeam(); }, Parent, 72f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);   // 선수단 기록과 1군·전술 관리를 한 화면에서
            currentParent = Section(content, "순위표 · " + v.division, true);
            ClubModeScreen.Table(Parent, font, v);
            PixelButton("팀 선수단 보기", () => { if (OnOpenSquads != null) OnOpenSquads(); }, Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge).GetComponentInChildren<Text>().fontSize = 22;
            if (v.leaders != null)   // 우리 팀 선수들끼리의 개인 순위 (선수 키우기 개인 순위와 같은 모양)
            {
                currentParent = Section(content, "개인 순위", true);
                var leaders = new MatchPageView(font) { LeaderKey = "club", OnBestEleven = ShowBestEleven };
                leaders.BuildLeaders(Parent);
                leaders.SetLeaders(v.leadersHeader, v.leaders, v.best);
            }
            currentParent = Section(content, "트로피 룸", true);
            ClubModeScreen.Trophies(Parent, font, v.trophies);
            var trophyList = v.trophies;
            if (ClubModeScreen.TrophySeasons(trophyList).Count > 1)   // 지난 시즌 트로피는 창에서 시즌을 넘겨 가며
                PixelButton("시즌별 트로피 보기", () => ShowTrophySeasons(trophyList, 0), Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge).GetComponentInChildren<Text>().fontSize = 22;
            currentParent = Section(content, "무료 영입 · 내가 키운 선수", true);
            Label("선수단 " + v.squadCount + " / " + ClubSystem.SquadSize + "명 (1군 11 + 교체 8)" + (v.squadCount >= ClubSystem.SquadSize ? " · 영입하려면 한 명을 방출해야 합니다" : ""), 17, 32).color = PixelTheme.TextDim;
            if (v.free.Count == 0)
                Label("영입할 수 있는 은퇴 선수가 없습니다.\n선수 키우기 모드에서 은퇴한 선수는 여기서 무료로 영입할 수 있습니다.", 18, 60).color = PixelTheme.TextDim;
            for (int i = 0; i < v.free.Count; i++)
            {
                var lg = v.free[i];
                var row = ClubModeScreen.FreeRow(Parent, font, lg, i);
                int careerNo = lg.careerNumber;
                var b = PixelButton("영입", () => { if (OnClubSign != null) OnClubSign(careerNo); }, row, 0f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
                var br = (RectTransform)b.transform;
                var le = b.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = true;
                br.anchorMin = new Vector2(.72f, .14f); br.anchorMax = new Vector2(.98f, .86f); br.offsetMin = br.offsetMax = Vector2.zero;
                b.GetComponentInChildren<Text>().fontSize = 22;
            }
            currentParent = null;
            status = Label("", 18, 36); status.color = PixelTheme.TextDim;
        }

        // 나만의 팀 모드의 아래쪽 탭 막대 (처음 한 번 만들고, 이 화면에서만 보임)
        RectTransform clubBar;
        Image[] clubTabFills;

        void ShowClubBar()
        {
            if (clubBar == null)
            {
                clubBar = NewRect("ClubTabBar", safeRoot);
                clubBar.anchorMin = new Vector2(0, 0); clubBar.anchorMax = new Vector2(1, 0);
                clubBar.pivot = new Vector2(.5f, 0);
                clubBar.offsetMin = Vector2.zero; clubBar.offsetMax = new Vector2(0, TabHeight);
                clubBar.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
                var layout = clubBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 12, 10); layout.spacing = 10;
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
                string[] names = { "팀", "설정" };
                clubTabFills = new Image[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    int index = i;
                    var tab = PixelButton(names[i], () => { if (clubTab == index) return; clubTab = index; ShowClubMode(lastClubView); }, clubBar, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
                    clubTabFills[i] = (Image)tab.targetGraphic;
                }
            }
            clubBar.gameObject.SetActive(true);
            for (int i = 0; i < clubTabFills.Length; i++) clubTabFills[i].color = i == clubTab ? PixelTheme.TabActive : PixelTheme.QuietFill;
            viewport.offsetMin = new Vector2(0, TabHeight);   // 내용은 탭 막대 위까지
            viewport.offsetMax = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
        }

        // 시즌별 트로피 창: 한 번에 한 시즌, ◀ ▶로 앞뒤 시즌 (index 0이 가장 최근 시즌)
        void ShowTrophySeasons(List<TrophyRecord> list, int index)
        {
            var seasons = ClubModeScreen.TrophySeasons(list);
            if (seasons.Count == 0) return;
            index = Mathf.Clamp(index, 0, seasons.Count - 1);
            var inner = BuildDialog("시즌별 트로피", (index + 1) + " / " + seasons.Count + "  (최근 시즌부터)", 30f, 18);
            var nav = NewRect("Nav", inner); Height(nav.gameObject, 60);
            var h = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            var newer = PixelButton("◀ 최근", () => ShowTrophySeasons(list, index - 1), nav, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            newer.GetComponentInChildren<Text>().fontSize = 20; newer.interactable = index > 0;
            var older = PixelButton("이전 ▶", () => ShowTrophySeasons(list, index + 1), nav, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            older.GetComponentInChildren<Text>().fontSize = 20; older.interactable = index < seasons.Count - 1;
            ClubModeScreen.TrophySeason(inner, font, list, seasons[index]);
            PixelButton("닫기", () => CloseDialog(), inner, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 설정 칸 (선수 키우기 설정 탭과 나만의 팀 설정 탭이 함께 씀): 모드 선택으로 나가기, 효과음, 저장 안내, 저장 초기화
        void BuildSettings(Transform page, bool clubMode)
        {
            currentParent = Section(page, "모드", true);
            Label(clubMode ? "맨 처음 화면으로 나가 선수 키우기 모드로 바꿀 수 있습니다. 진행 상황은 저장됩니다."
                           : "맨 처음 화면으로 나가 나만의 팀 모드로 바꿀 수 있습니다. 진행 상황은 저장됩니다.", 20, 56).color = PixelTheme.TextDim;
            PixelButton("◀ 모드 선택으로 나가기", () =>
            {
                if (clubMode) { clubTab = 0; if (OnClubBack != null) OnClubBack(); }
                else if (OnExitToMenu != null) OnExitToMenu();
            }, Parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            currentParent = Section(page, "소리", true);
            Button soundButton = null;
            soundButton = PixelButton(SoundFx.Enabled ? "효과음 켬" : "효과음 끔", () =>
            {
                SoundFx.Enabled = !SoundFx.Enabled;
                soundButton.GetComponentInChildren<Text>().text = SoundFx.Enabled ? "효과음 켬" : "효과음 끔";
                SoundFx.Play(Sfx.Click);
            }, Parent, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            currentParent = Section(page, "저장", true);
            Label("진행 상황은 자동으로 저장됩니다.\n(행동할 때마다, 30초마다, 앱을 닫거나 내릴 때)", 20, 64).color = PixelTheme.TextDim;
            Label("오프라인 보상은 최대 " + trainingSettings.offlineCapHours.ToString("0.#") + "시간까지 인정됩니다.", 20, 30).color = PixelTheme.TextDim;
            currentParent = Section(page, "초기화", true);
            if (clubMode)   // 나만의 팀만 초기화 (선수 키우기는 그대로)
            {
                Label("나만의 팀(선수단·시즌·기록)을 지웁니다. 선수 키우기는 그대로입니다.", 20, 56).color = PixelTheme.TextDim;
                PixelButton("나만의 팀 초기화", () => { if (OnResetClub != null) OnResetClub(); }, Parent, 68f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
            }
            else            // 선수 키우기만 초기화 (나만의 팀은 그대로)
            {
                Label("지금 선수와 진행 상황을 지우고 새 선수로 시작합니다. 나만의 팀은 그대로입니다.", 20, 56).color = PixelTheme.TextDim;
                PixelButton("선수 키우기 초기화", () => { if (OnResetSave != null) OnResetSave(); }, Parent, 68f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
            }
        }

        // 나만의 팀 창단 창: 팀 이름, 유니폼, 포메이션, 스타일
        public void ShowFoundDialog()
        {
            int kit = 0; var formation = Formation.F433; var style = TacticStyle.Possession;
            var inner = BuildDialog("나만의 팀 창단", "은퇴 선수 5명과 새 선수들로 팀을 만듭니다.\n나만의 팀 모드에서 가장 아래 부부터 시작합니다.", 70f, 20);
            var input = NameField(inner, "팀 이름 (예: Legends FC)", 20);
            var kitRow = NewRect("KitRow", inner); Height(kitRow.gameObject, 130);
            kitRow.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            var kitImage = TeamKits.PreviewImage(kitRow, TeamKits.ClubPreview(kit), .38f, .04f, .62f, .96f);
            Button kitButton = null, formationButton = null, styleButton = null;
            kitButton = PixelButton("유니폼: " + TeamKits.ClubDesignName(kit), () => {
                kit = (kit + 1) % TeamKits.ClubDesignCount;
                kitButton.GetComponentInChildren<Text>().text = "유니폼: " + TeamKits.ClubDesignName(kit);
                if (kitImage != null) kitImage.sprite = TeamKits.Preview(TeamKits.ClubPreview(kit));
            }, inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            formationButton = PixelButton("포메이션: " + TacticSystem.FormationName(formation), () => {
                formation = (Formation)(((int)formation + 1) % System.Enum.GetValues(typeof(Formation)).Length);
                formationButton.GetComponentInChildren<Text>().text = "포메이션: " + TacticSystem.FormationName(formation);
            }, inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            styleButton = PixelButton("스타일: " + TacticSystem.StyleName(style), () => {
                style = (TacticStyle)(((int)style + 1) % System.Enum.GetValues(typeof(TacticStyle)).Length);
                styleButton.GetComponentInChildren<Text>().text = "스타일: " + TacticSystem.StyleName(style);
            }, inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            PixelButton("창단하기", () => {
                string name = input.text.Trim();
                if (name.Length == 0) name = "Legends FC";
                CloseDialog();
                if (OnFoundClub != null) OnFoundClub(name, kit, formation, style);
            }, inner, 76f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            PixelButton("취소", () => CloseDialog(), inner, 64f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 한 줄 글자 입력 칸
        InputField NameField(Transform parent, string hint, int limit)
        {
            var outer = NewRect("NameInput", parent);
            outer.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            Height(outer.gameObject, 72);
            var fill = NewRect("Fill", outer);
            Stretch(fill); fill.offsetMin = new Vector2(4, 4); fill.offsetMax = new Vector2(-4, -4);
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = PixelTheme.BarBack;
            var field = outer.gameObject.AddComponent<InputField>();
            field.targetGraphic = fillImage;
            var text = TextNode("", fill, 26); Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 6); text.rectTransform.offsetMax = new Vector2(-16, -6);
            field.textComponent = text; field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            var placeholder = TextNode(hint, fill, 22); Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16, 6); placeholder.rectTransform.offsetMax = new Vector2(-16, -6);
            placeholder.color = PixelTheme.TextDim; field.placeholder = placeholder;
            return field;
        }
    }
}
