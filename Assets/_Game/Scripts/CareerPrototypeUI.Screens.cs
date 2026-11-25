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
    // 화면: 선수 만들기, 선수 키우기(탭 5개), 오류
    public partial class CareerPrototypeUI
    {
        // ---------- 화면 ----------
        // retireMode: 은퇴한 뒤 다음 선수를 만드는 화면 (돌아가기를 누르면 맨 처음 화면으로)
        public void ShowCreation(bool retireMode = false)
        {
            Clear();
            Label(retireMode ? "새 커리어" : "나의 축구 커리어", 33, 64).color = PixelTheme.Accent;
            Label(retireMode ? "새로 키울 선수를 만드세요" : "첫 선수를 만들어 보세요", 22, 44);
            newHair = 0; newSkin = 0; newKit = CreationKit; newBoots = 0; previewGloves = position == Position.Goalkeeper;
            var createPreview = AddPreview(content, 190f);
            ApplyPreview(createPreview);
            Label("선수 이름", 22, 40).color = PixelTheme.TextDim;

            var inputOuter = NewRect("NameInput", content);
            inputOuter.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            Height(inputOuter.gameObject, 84);
            var inputFill = NewRect("Fill", inputOuter);
            Stretch(inputFill); inputFill.offsetMin = new Vector2(4, 4); inputFill.offsetMax = new Vector2(-4, -4);
            var inputImage = inputFill.gameObject.AddComponent<Image>(); inputImage.color = PixelTheme.BarBack;
            nameInput = inputOuter.gameObject.AddComponent<InputField>();
            nameInput.targetGraphic = inputImage;
            var text = TextNode("", inputFill, 28); Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 6); text.rectTransform.offsetMax = new Vector2(-16, -6);
            nameInput.textComponent = text; nameInput.characterLimit = settings.maxNameLength;
            nameInput.lineType = InputField.LineType.SingleLine;
            var placeholder = TextNode("이름 입력", inputFill, 26); Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(16, 6); placeholder.rectTransform.offsetMax = new Vector2(-16, -6);
            placeholder.color = PixelTheme.TextDim; nameInput.placeholder = placeholder;

            if (System.Array.IndexOf(Ko.TraitsFor(position), trait) < 0) trait = Ko.TraitsFor(position)[0];
            Button positionButton = null, traitButton = null;
            Text traitHelp = null;
            System.Action refreshTrait = () => {
                traitButton.GetComponentInChildren<Text>().text = "특성: " + Ko.TraitName(trait);
                var names = new System.Collections.Generic.List<string>();
                foreach (var t in Ko.TraitsFor(position)) names.Add(Ko.TraitName(t) + ": " + Ko.Attr(Ko.TraitAttribute(t)));
                traitHelp.text = string.Join(" / ", names) + "\n(보너스 +" + settings.traitBonus + ")";
            };
            positionButton = PixelButton("포지션: " + Ko.Pos(position), () => {
                position = (Position)(((int)position + 1) % 4);
                positionButton.GetComponentInChildren<Text>().text = "포지션: " + Ko.Pos(position);
                trait = Ko.TraitsFor(position)[0];   // 포지션이 바뀌면 그 포지션의 첫 특성으로
                refreshTrait();
                previewGloves = position == Position.Goalkeeper; ApplyPreview(createPreview);   // 골키퍼면 장갑
                if (onPositionNumber != null) onPositionNumber();   // 번호를 직접 고르지 않았으면 포지션의 대표 번호로
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            traitButton = PixelButton("특성: " + Ko.TraitName(trait), () => {
                var list = Ko.TraitsFor(position);
                trait = list[(System.Array.IndexOf(list, trait) + 1) % list.Length];
                refreshTrait();
            }, content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            // 등번호 (1~99): 누르면 등번호 설정 창이 뜸. 직접 고르기 전에는 포지션의 대표 번호
            NewNumber = SquadSystem.DefaultNumber(position); numberPicked = false;
            Button numberButton = null;
            numberButton = PixelButton("등번호: " + NewNumber, () => ShowNumberPicker(NewNumber, null, 0, "이 번호로 선택", n => {
                NewNumber = n; numberPicked = true;
                numberButton.GetComponentInChildren<Text>().text = "등번호: " + n;
            }), content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            onPositionNumber = () => { if (!numberPicked) { NewNumber = SquadSystem.DefaultNumber(position); numberButton.GetComponentInChildren<Text>().text = "등번호: " + NewNumber; } };
            AddLookButtons(content, createPreview);
            traitHelp = Label("", 20, 76); traitHelp.color = PixelTheme.TextDim;
            refreshTrait();
            PixelButton("선수 만들기", () => onCreate(nameInput.text, position, trait, newHair, newSkin, newKit, newBoots), content, 88f);
            if (retireMode) PixelButton("◀ 뒤로", () => { if (OnCancelCreation != null) OnCancelCreation(); }, content, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            status = Label("버튼을 눌러 포지션과 특성을 고르세요.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public int NewNumber = 9;          // 선수 만들기에서 고른 등번호
        bool numberPicked; System.Action onPositionNumber;

        // 커리어 탭: 등번호 변경
        public System.Action<int> OnChangeNumber;   // 커리어 탭: 등번호 바꾸기
        int shownNumber; Button numberChangeButton;

        // 등번호를 바꾼 뒤: 설정 창이 처음 보여 줄 지금 번호
        public void SetNumber(int n)
        {
            shownNumber = n;
        }

        // 커리어 탭의 등번호 변경에 쓰는 소속팀 정보: 동료들이 쓰는 번호(번호 → 성)와 홈 유니폼
        public System.Func<System.Collections.Generic.Dictionary<int, string>> TeammateNumbers;
        public System.Func<int> TeamKitIndex;

        // 등번호 설정: 10의 자리(0~9)와 1의 자리(0~9)를 따로 골라 1~99. 위에 고른 번호를 크게 미리 보여 주고, 확정 버튼을 눌러야 바뀜.
        // taken: 동료가 쓰는 번호 (번호 → 성). 그 번호를 고르면 설정 칸 아래에 '~의 번호를 가져옵니다' 안내
        // takenText: 고른 번호를 쓰는 선수가 있을 때 그 이름 뒤에 붙는 안내 (기본: '~의 번호를 가져옵니다')
        void ShowNumberPicker(int current, System.Collections.Generic.Dictionary<int, string> taken, int kit, string confirmLabel, System.Action<int> picked,
                              string takenText = "의 번호를 가져옵니다")
        {
            int start = current >= 1 && current <= 99 ? current : 1;
            int tens = start / 10, ones = start % 10;
            var inner = BuildDialog("등번호", "", 0f);

            // 미리 보기: 유니폼(소속팀이 있으면)과 고른 번호
            var prev = NewRect("Preview", inner); Height(prev.gameObject, 150);
            prev.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            bool hasKit = kit > 0 && TeamKits.PreviewImage(prev, kit, .04f, .06f, .30f, .94f) != null;
            var big = TextNode("", prev, 84); big.alignment = TextAnchor.MiddleCenter; big.color = PixelTheme.Accent;
            big.rectTransform.anchorMin = new Vector2(hasKit ? .32f : .02f, 0f); big.rectTransform.anchorMax = new Vector2(.98f, 1f); big.rectTransform.offsetMin = big.rectTransform.offsetMax = Vector2.zero;

            var tensFills = new Image[10]; var onesFills = new Image[10];
            Text info = null;
            System.Action refresh = null;
            refresh = () =>
            {
                int n = tens * 10 + ones;
                big.text = n >= 1 ? n.ToString() : "-";
                for (int d = 0; d < 10; d++)
                {
                    tensFills[d].color = d == tens ? PixelTheme.AccentFill : PixelTheme.QuietFill;
                    onesFills[d].color = d == ones ? PixelTheme.AccentFill : PixelTheme.QuietFill;
                }
                string who = n != current && taken != null && taken.TryGetValue(n, out var w) ? w : null;
                info.text = n < 1 ? "0번은 쓸 수 없습니다 (1~99)" : who != null ? who + takenText : "";
                info.color = n < 1 ? PixelTheme.TextDim : PixelTheme.Accent;
            };

            // 자리 하나: 이름 + 0~9 버튼 한 줄
            void DigitRow(string title, Image[] fills, System.Action<int> set)
            {
                var cap = TextNode(title, inner, 18); cap.color = PixelTheme.TextDim; Height(cap.gameObject, 28);
                var row = NewRect("Digits", inner); Height(row.gameObject, 60);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 4; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                for (int d = 0; d < 10; d++)
                {
                    var cell = NewRect("Digit", row);
                    var img = cell.gameObject.AddComponent<Image>();
                    var b = cell.gameObject.AddComponent<Button>(); b.targetGraphic = img;
                    var c = b.colors; c.pressedColor = new Color(.7f, .7f, .7f, 1f); c.fadeDuration = .05f; b.colors = c;
                    int digit = d;
                    b.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); set(digit); refresh(); });
                    var t = TextNode(d.ToString(), cell, 24); t.alignment = TextAnchor.MiddleCenter; t.color = Color.white;
                    t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                    fills[d] = img;
                }
            }
            DigitRow("10의 자리", tensFills, d => tens = d);
            DigitRow("1의 자리", onesFills, d => ones = d);
            info = TextNode("", inner, 18); info.alignment = TextAnchor.MiddleCenter; Height(info.gameObject, 34);   // 설정 칸 아래 안내 (동료 번호를 가져올 때만)
            refresh();
            PixelButton(confirmLabel, () => { int n = tens * 10 + ones; if (n < 1) return; CloseDialog(); if (n != current) picked(n); }, inner, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            PixelButton("닫기", () => CloseDialog(), inner, 60f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        public void ShowPlayer(PlayerData p, string message)
        {
            Clear();
            SetChrome(true);
            status = stripText;

            pages[0] = NewPage("HomePage"); pages[1] = NewPage("MatchPage"); pages[2] = NewPage("MissionPage"); pages[3] = NewPage("CareerPage"); pages[4] = NewPage("SettingsPage");
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor; lookBoots = p.bootColor; lookGloves = p.position == Position.Goalkeeper;
            stage.SetLook(lookHair, lookSkin, lookKit, lookBoots, lookGloves);               // 내 선수 그림을 외형에 맞게 준비
            if (stage.Available) stage.Create(pages[0]);   // HOME 맨 위에 훈련장 장면

            // HOME: 스크롤 없이 한 화면에 들어오도록 촘촘하게 배치합니다.
            currentParent = Section(pages[0], "", true);
            var nameRow = NewRect("NameRow", Parent);
            var nameLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            nameLayout.childControlWidth = true; nameLayout.childControlHeight = true;
            nameLayout.childForceExpandWidth = false; nameLayout.childForceExpandHeight = true;
            Height(nameRow.gameObject, 36);
            var nameText = TextNode(p.playerName, nameRow, 26);
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var infoText = TextNode(Ko.Pos(p.position) + " / " + Ko.TraitName(p.trait), nameRow, 22);
            infoText.alignment = TextAnchor.MiddleRight; infoText.color = PixelTheme.TextDim;
            infoText.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
            // 노력 포인트: 방치하면 쌓이고, 아래 능력치의 +1 버튼으로 씁니다.
            trainingText = Label("", 22, 64);
            var rate = PixelButton("노력 증가량", () => { if (OnBuyEffortRate != null) OnBuyEffortRate(); }, Parent, 56f);
            rateButton = rate; rate.GetComponentInChildren<Text>().fontSize = 20;
            for (int i = 0; i < Ko.AttributeCount(p.position); i++)   // GK 능력 칸까지 (모든 포지션)
                statBars[i] = MakeStatRow((AttributeKind)i, out statTexts[i], out upgradeButtons[i]);
            UpdateStats(p);

            // 시설: 돈으로 올리는 영구 효과 (홈에서 바로 올릴 수 있게)
            currentParent = Section(pages[0], "시설", true);
            facilityMoneyText = Label("", 22, 30);
            facilityMoneyText.color = PixelTheme.Accent;
            for (int i = 0; i < facilityButtons.Length; i++)
            {
                int index = i;
                facilityButtons[i] = PixelButton("", () => { if (OnBuyFacility != null) OnBuyFacility(index); }, Parent, 112f);
                facilityButtons[i].GetComponentInChildren<Text>().fontSize = 20;
            }
            Label("환생하면 시설은 초기화됩니다.", 20, 30).color = PixelTheme.TextDim;

            // MATCH: 시즌, 경기, 순위표
            matchPage = new MatchPageView(font);
            matchPage.OnBestEleven = ShowBestEleven;
            currentParent = Section(pages[1], "시즌", true);
            matchPage.BuildNext(Parent);
            var play = PixelButton("다음 경기", () => onPlayMatch(), Parent, 80f);
            playLabel = play.GetComponentInChildren<Text>();
            // 시즌이 끝나고 이적 제안이 들어왔으면: 알림과 '이적 제안 확인하기' (커리어 탭의 이적 제안으로)
            offerNotice = Label("", 21, 40); offerNotice.color = PixelTheme.Accent; offerNotice.alignment = TextAnchor.MiddleCenter;
            offerButton = PixelButton("이적 제안 확인하기", () => OpenTransferOffers(), Parent, 68f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge).gameObject;
            offerNotice.gameObject.SetActive(false); offerButton.SetActive(false);
            matchPage.BuildResult(Parent);
            currentParent = Section(pages[1], "친선 경기", true);
            friendlyText = Label("", 22, 64);
            friendlyButton = PixelButton("친선 경기 하기", () => { if (OnPlayFriendly != null) OnPlayFriendly(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[1], "", true);
            tableTitle = Label("순위표", 26, 36); tableTitle.color = PixelTheme.Accent;   // "순위표 · 5부 리그" (SetTableTitle)
            matchPage.BuildTable(Parent);
            PixelButton("팀 선수단 보기", () => { if (OnOpenSquads != null) OnOpenSquads(); }, Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                .GetComponentInChildren<Text>().fontSize = 22;
            currentParent = Section(pages[1], "개인 순위", true);
            matchPage.BuildLeaders(Parent);

            // CAREER: 선수 프로필, 이적 제안, 트로피 룸, 시즌 기록, 환생 (카드와 표로 보여 줌)
            careerPage = new CareerPageView(font);
            careerPage.OnLegend = n => { if (OnShowLegend != null) OnShowLegend(n); };
            currentParent = Section(pages[3], "", true);
            careerPage.BuildProfile(Parent);
            // 외형 바꾸기 · 등번호 바꾸기 (한 줄에 나란히)
            var lookRow = NewRect("LookRow", Parent); Height(lookRow.gameObject, 56);
            var lh = lookRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            lh.spacing = 8; lh.childControlWidth = true; lh.childControlHeight = true; lh.childForceExpandWidth = true; lh.childForceExpandHeight = true;
            PixelButton("외형 바꾸기", () => ShowLookDialog(), lookRow, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                .GetComponentInChildren<Text>().fontSize = 22;
            shownNumber = p.number;
            numberChangeButton = PixelButton("등번호 변경", () => ShowNumberPicker(shownNumber, TeammateNumbers != null ? TeammateNumbers() : null, TeamKitIndex != null ? TeamKitIndex() : 0, "이 번호로 변경",
                                                                                                n => { if (OnChangeNumber != null) OnChangeNumber(n); }),
                                             lookRow, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            numberChangeButton.GetComponentInChildren<Text>().fontSize = 22;
            transferRoot = NewRect("TransferRoot", pages[3]);
            ConfigureStack(transferRoot.gameObject, 0, 0, 0, 0, 18);
            currentParent = Section(pages[3], "트로피 룸", true);
            careerPage.BuildTrophies(Parent);
            currentParent = Section(pages[3], "시즌 기록", true);
            careerPage.BuildHistory(Parent);
            currentParent = Section(pages[3], "환생", true);
            careerPage.BuildRebirth(Parent);
            var rebirth = PixelButton("환생하기", () => onRebirth(), Parent, 72f, PixelTheme.RebirthFill, PixelTheme.RebirthEdge);
            rebirthFill = (Image)rebirth.targetGraphic;
            currentParent = Section(pages[3], "은퇴", true);
            careerPage.BuildRetire(Parent);
            var retire = PixelButton("은퇴하기", () => { if (OnRetire != null) OnRetire(); }, Parent, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            retireFill = (Image)retire.targetGraphic;

            // 미션: 출석 보상, 일일 미션
            currentParent = Section(pages[2], "출석 보상", true);
            attendText = Label("", 22, 34); attendText.alignment = TextAnchor.MiddleCenter;
            attendCards = new AttendanceCards(font);
            attendCards.Build(Parent);
            attendNote = Label("", 18, 76); attendNote.alignment = TextAnchor.MiddleCenter; attendNote.color = PixelTheme.TextDim;   // 카드의 '강화 n회 / 수당 n주' 뜻
            attendDetail = Label("", 20, 30); attendDetail.alignment = TextAnchor.MiddleCenter; attendDetail.color = PixelTheme.Text;
            attendButton = PixelButton("출석 보상 받기", () => { if (OnClaimAttendance != null) OnClaimAttendance(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[2], "일일 미션", true);
            resetText = Label("", 20, 30); resetText.color = PixelTheme.TextDim;
            for (int i = 0; i < missionTexts.Length; i++)
            {
                int index = i;
                missionTexts[i] = Label("", 22, 60);
                missionButtons[i] = PixelButton("받기", () => { if (OnClaimMission != null) OnClaimMission(index); }, Parent, 56f);
                missionButtons[i].GetComponentInChildren<Text>().fontSize = 20;
            }
            bonusText = Label("", 22, 34);
            bonusButton = PixelButton("보너스 받기", () => { if (OnClaimBonus != null) OnClaimBonus(); }, Parent, 64f, PixelTheme.AccentFill, PixelTheme.Accent);

            // 설정: 모드, 소리, 저장 안내, 초기화 (나만의 팀 모드의 설정 탭과 같은 내용)
            BuildSettings(pages[4], false);

            // 개발용: 골 장면 미리보기 (에디터·개발용 빌드에서만 보임, 출시 빌드에는 없음)
            if (Application.isEditor || Debug.isDebugBuild)
            {
                currentParent = Section(pages[4], "골 장면 미리보기 (개발용)", true);
                Label("경기 중 내가 골을 넣을 때의 장면을 바로 봅니다. 누를 때마다 1골 → 멀티골 → 해트트릭 순으로 배지가 바뀝니다.", 18, 52).color = PixelTheme.TextDim;
                string previewName = p.playerName;
                // 골 장면 (골키퍼 도움 장면은 도움 줄에)
                var kinds = System.Array.FindAll((TrainingStageDrills.GoalScene[])Enum.GetValues(typeof(TrainingStageDrills.GoalScene)),
                    g => g != TrainingStageDrills.GoalScene.KeeperKick && g != TrainingStageDrills.GoalScene.KeeperRoll);
                string[] names = { "드리블", "패스", "크로스 헤딩", "코너킥 헤딩", "프리킥", "페널티킥", "중거리 슛" };
                // 수비 장면
                var defKinds = (TrainingStageDrills.DefenseScene[])Enum.GetValues(typeof(TrainingStageDrills.DefenseScene));
                string[] defNames = { "수비: 태클", "수비: 슈팅 블록", "수비: 골라인", "수비: 가로채기",
                                      "골키퍼: 다이빙 선방", "골키퍼: PK 선방", "골키퍼: 코너킥 캐칭", "골키퍼: 1대1 선방" };
                for (int i = 0; i < defKinds.Length; i += 2)
                {
                    var drow = NewRect("DefensePreviewRow", Parent); Height(drow.gameObject, 60);
                    var dh = drow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    dh.spacing = 8; dh.childControlWidth = true; dh.childControlHeight = true; dh.childForceExpandWidth = true; dh.childForceExpandHeight = true;
                    for (int k = i; k < i + 2 && k < defKinds.Length; k++)
                    {
                        var dk = defKinds[k];
                        PixelButton(k < defNames.Length ? defNames[k] : dk.ToString(), () => PlayDefensePreview(dk, previewName), drow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 20;
                    }
                }
                // 도움 장면: 내가 공을 보내고 동료가 넣음
                var assistKinds = new[] { TrainingStageDrills.GoalScene.Pass, TrainingStageDrills.GoalScene.Cross, TrainingStageDrills.GoalScene.Corner, TrainingStageDrills.GoalScene.LongShot,
                                          TrainingStageDrills.GoalScene.KeeperKick, TrainingStageDrills.GoalScene.KeeperRoll };
                string[] assistNames = { "도움: 스루 패스", "도움: 크로스", "도움: 코너킥", "도움: 내준 패스", "골키퍼 도움: 롱킥", "골키퍼 도움: 빠른 배급" };
                for (int i = 0; i < assistKinds.Length; i += 2)
                {
                    var arow = NewRect("AssistPreviewRow", Parent); Height(arow.gameObject, 60);
                    var ah = arow.gameObject.AddComponent<HorizontalLayoutGroup>();
                    ah.spacing = 8; ah.childControlWidth = true; ah.childControlHeight = true; ah.childForceExpandWidth = true; ah.childForceExpandHeight = true;
                    for (int k = i; k < i + 2 && k < assistKinds.Length; k++)
                    {
                        var kind = assistKinds[k];
                        PixelButton(assistNames[k], () => PlayGoalPreview(kind, previewName, true), arow, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 20;
                    }
                }
                for (int i = 0; i < kinds.Length; i += 2)
                {
                    var row = NewRect("PreviewRow", Parent); Height(row.gameObject, 60);
                    var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                    h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                    for (int k = i; k < i + 2 && k < kinds.Length; k++)
                    {
                        var kind = kinds[k];
                        PixelButton(k < names.Length ? names[k] : kind.ToString(), () => PlayGoalPreview(kind, previewName), row, 0f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                            .GetComponentInChildren<Text>().fontSize = 22;
                    }
                }
            }

            currentParent = null;
            SetPage(0);
            status.text = message;
        }

        public void ShowError(string message, bool canRestore)
        {
            Clear();
            Label("저장을 불러올 수 없습니다", 30, 90).color = PixelTheme.Accent;
            status = Label(message + "\n저장 파일은 바뀌지 않았습니다.", 22, 180);
            PixelButton("다시 시도", () => { if (OnRetry != null) OnRetry(); }, content, 80f);
            if (canRestore)
                PixelButton("백업 복구", () => { if (OnRestoreBackup != null) OnRestoreBackup(); }, content, 80f, PixelTheme.AccentFill, PixelTheme.Accent);
            PixelButton("새 게임 시작", () => { if (OnStartNew != null) OnStartNew(); }, content, 80f, PixelTheme.DangerFill, PixelTheme.DangerEdge);
        }
    }
}
