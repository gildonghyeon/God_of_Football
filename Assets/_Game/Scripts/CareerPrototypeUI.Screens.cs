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
            // 등번호 (1~99): 누르면 번호판이 뜸. 직접 고르기 전에는 포지션의 대표 번호
            NewNumber = SquadSystem.DefaultNumber(position); numberPicked = false;
            Button numberButton = null;
            numberButton = PixelButton("등번호: " + NewNumber, () => ShowNumberPicker(n => {
                NewNumber = n; numberPicked = true;
                numberButton.GetComponentInChildren<Text>().text = "등번호: " + n;
            }), content, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            onPositionNumber = () => { if (!numberPicked) { NewNumber = SquadSystem.DefaultNumber(position); numberButton.GetComponentInChildren<Text>().text = "등번호: " + NewNumber; } };
            AddLookButtons(content, createPreview);
            traitHelp = Label("", 20, 76); traitHelp.color = PixelTheme.TextDim;
            refreshTrait();
            PixelButton("선수 만들기", () => onCreate(nameInput.text, position, trait, newHair, newSkin, newKit, newBoots), content, 88f);
            if (retireMode) PixelButton("◀ 모드 선택으로", () => { if (OnCancelCreation != null) OnCancelCreation(); }, content, 68f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            status = Label("버튼을 눌러 포지션과 특성을 고르세요.", 22, 100);
            status.color = PixelTheme.TextDim;
        }

        public int NewNumber = 9;          // 선수 만들기에서 고른 등번호
        bool numberPicked; System.Action onPositionNumber;

        // 등번호 번호판: 1~99 (한 줄에 7개)
        void ShowNumberPicker(System.Action<int> picked)
        {
            var inner = BuildDialog("등번호", "1~99 중에서 고르세요", 40f, 20);
            for (int start = 1; start <= 99; start += 7)
            {
                var row = NewRect("Numbers", inner); Height(row.gameObject, 58);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
                for (int n = start; n < start + 7; n++)
                {
                    if (n > 99) { NewRect("Empty", row); continue; }
                    int pick = n; bool on = n == NewNumber;
                    PixelButton(n.ToString(), () => { CloseDialog(); picked(pick); }, row, 58f, on ? PixelTheme.AccentFill : PixelTheme.QuietFill, on ? PixelTheme.Accent : PixelTheme.QuietEdge)
                        .GetComponentInChildren<Text>().fontSize = 22;
                }
            }
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
            currentParent = Section(pages[1], "시즌", true);
            matchPage.BuildNext(Parent);
            var play = PixelButton("다음 경기", () => onPlayMatch(), Parent, 80f);
            playLabel = play.GetComponentInChildren<Text>();
            matchPage.BuildResult(Parent);
            currentParent = Section(pages[1], "친선 경기", true);
            friendlyText = Label("", 22, 64);
            friendlyButton = PixelButton("친선 경기 하기", () => { if (OnPlayFriendly != null) OnPlayFriendly(); }, Parent, 68f, PixelTheme.AccentFill, PixelTheme.Accent);
            currentParent = Section(pages[1], "순위표", true);
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
            PixelButton("외형 바꾸기", () => ShowLookDialog(), Parent, 56f, PixelTheme.QuietFill, PixelTheme.QuietEdge)
                .GetComponentInChildren<Text>().fontSize = 22;
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
