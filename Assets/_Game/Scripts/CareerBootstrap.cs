using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public partial class CareerBootstrap : MonoBehaviour
    {
        [SerializeField] CareerSettings settings;
        [SerializeField] TrainingSettings trainingSettings;
        [SerializeField] MatchSettings matchSettings;
        [SerializeField] RebirthSettings rebirthSettings;
        [Tooltip("Optional font with Korean glyphs. Blank uses the built-in prototype font.")]
        [SerializeField] Font uiFont;
        CareerSaveSystem saves;
        SaveData current;
        CareerPrototypeUI ui;
        TrainingSystem training;
        bool blocked;
        bool temporarySettings;
        bool temporaryTraining;
        bool temporaryMatch;
        bool temporaryRebirth;
        bool clonedMatch;      // 실행 중 팀 목록(나만의 팀)을 바꾸므로 설정의 복사본을 씁니다
        bool retiring;         // 은퇴 후 새 선수 만들기 화면
        // 나만의 팀 모드: 따로 저장되는 팀·리그 (선수 키우기와 다른 모드)
        CareerSaveSystem clubSaves;
        SaveData club;                 // 없으면 아직 창단 전
        MatchSettings clubMs;          // 나만의 팀 리그의 팀 목록 (선수 키우기 설정의 복사본)
        TeamDefinition[] baseTeams;    // 선수 키우기 리그의 원래 팀 목록
        bool inClub;                   // 나만의 팀 모드 화면을 보는 중
        bool inMenu;                   // 시작 화면(모드 고르기)을 보는 중
        bool offlineApplied;           // 이번 실행에서 오프라인 보상을 이미 줬는지 (선수 키우기에 처음 들어갈 때)
        string menuMessage;            // 저장을 읽을 때 나온 안내 (선수 키우기에 들어가면 보여 줌)
        float refreshTimer;
        float autoSaveTimer;

        void Start()
        {
            if (settings == null) { settings = ScriptableObject.CreateInstance<CareerSettings>(); temporarySettings = true; }
            if (trainingSettings == null)
            {
                trainingSettings = ScriptableObject.CreateInstance<TrainingSettings>();
                temporaryTraining = true;
                Debug.LogWarning("Training Settings is not assigned on GameRoot. Using temporary default values.");
            }
            if (matchSettings == null)
            {
                matchSettings = ScriptableObject.CreateInstance<MatchSettings>();
                temporaryMatch = true;
                Debug.LogWarning("Match Settings is not assigned on GameRoot. Using temporary default values.");
            }
            if (rebirthSettings == null)
            {
                rebirthSettings = ScriptableObject.CreateInstance<RebirthSettings>();
                temporaryRebirth = true;
                Debug.LogWarning("Rebirth Settings is not assigned on GameRoot. Using temporary default values.");
            }
            if (!temporaryMatch) { matchSettings = Instantiate(matchSettings); clonedMatch = true; }   // 원본 설정 파일은 바꾸지 않음
            saves = new CareerSaveSystem();
            ui = gameObject.AddComponent<CareerPrototypeUI>();
            ui.Initialize(uiFont, settings, trainingSettings, Create, Save, UpgradeAttribute, PlayMatch, ChooseOffer, StayWithTeam, Rebirth);
            ui.OnRetry = RetryLoad;
            ui.OnRestoreBackup = RestoreBackup;
            ui.OnStartNew = StartNewGame;
            ui.OnResetSave = ResetSave;
            ui.OnApplyLook = ChangeLook;
            TeamIdentity.ApplyTo(matchSettings);                         // 팀 이름은 팀 표의 이름으로
            ui.CreationKit = TeamKits.Home(matchSettings.startTeamId);   // 새 선수 미리보기는 시작 팀 유니폼
            ui.OnBuyEffortRate = BuyEffortRate;
            ui.OnPlayFriendly = PlayFriendly;
            ui.OnBuyFacility = BuyFacility;
            ui.OnClaimAttendance = ClaimAttendance; ui.OnClaimMission = ClaimMission; ui.OnClaimBonus = ClaimDailyBonus;
            ui.OnOpenSquads = OpenSquads; ui.OnShowSquad = ShowSquad; ui.OnPickLeague = OpenLeague;
            ui.OnShowLegend = ShowLegend;
            ui.OnRetire = Retire; ui.OnCancelCreation = CancelRetire; ui.OnFoundClub = FoundClub;
            ui.OnPickCareer = PickCareer; ui.OnPickClub = PickClub; ui.OnExitToMenu = ExitToMenu;
            ui.OnClubTeam = OpenClubTeam; ui.OnClubTeamBack = ShowClubScreen; ui.OnClubTap = ClubTap; ui.OnClubFormation = ClubFormation; ui.OnClubStyle = ClubStyle;
            ui.OnOpenClub = OpenClubMode; ui.OnClubPlay = ClubPlay; ui.OnClubSquad = ClubSquad; ui.OnClubSign = ClubSign; ui.OnClubBack = ClubBack;
            baseTeams = matchSettings.teams != null ? (TeamDefinition[])matchSettings.teams.Clone() : new TeamDefinition[0];
            clubMs = Instantiate(matchSettings);
            clubSaves = new CareerSaveSystem("football-club-v1.json");
            LoadClub();
            Debug.Log("Save folder: " + Application.persistentDataPath);
            LoadAndShow(null);
        }

        // 저장을 읽어 알맞은 화면(선수 생성, 플레이어, 오류)을 보여줍니다.
        void LoadAndShow(string extraMessage)
        {
            training = null;
            blocked = !saves.TryLoad(out current, out string message);
            retiring = false;
            if (blocked) { ui.ShowError(message, saves.HasValidBackup()); return; }
            if (!string.IsNullOrEmpty(extraMessage))
                message = string.IsNullOrEmpty(message) ? extraMessage : extraMessage + " " + message;
            if (current != null) { menuMessage = message; ShowModeSelect(); }   // 시작 화면에서 모드를 고름
            else ui.ShowCreation();
        }

        void RetryLoad() { LoadAndShow(null); }

        void RestoreBackup()
        {
            if (!saves.TryRestoreBackup(out string message)) { ui.ShowError(message, saves.HasValidBackup()); return; }
            LoadAndShow(message);
        }

        // 오류 화면에서 새로 시작: 망가진 파일은 지우지 않고 옆으로 옮깁니다.
        void StartNewGame()
        {
            ui.ShowConfirm("새 게임을 시작할까요?", "손상된 저장 파일은 지우지 않고 옆으로 옮겨 둡니다. 새 선수를 만듭니다.", 150f, "새로 시작", DoStartNew);
        }

        void DoStartNew()
        {
            if (!saves.ArchiveSave(out string message)) { ui.ShowError(message, saves.HasValidBackup()); return; }
            blocked = false; current = null; training = null; retiring = false;
            ArchiveClub();
            ui.ShowCreation();
        }

        // 플레이 중 저장 초기화: 현재 저장을 먼저 저장한 뒤 옆으로 옮기고 처음부터 시작합니다.
        void ResetSave()
        {
            if (blocked || current == null) return;
            ui.ShowConfirm("저장을 초기화할까요?", "지금 선수와 진행 상황은 지우지 않고 옆으로 옮겨 두고, 새 선수로 처음부터 시작합니다. 환생 보너스는 남지 않습니다.", 200f, "초기화", DoResetSave);
        }

        void DoResetSave()
        {
            if (blocked || current == null) return;
            saves.TrySave(current, out _);
            if (!saves.ArchiveSave(out string message)) { ui.SetStatus(message); return; }
            current = null; training = null; retiring = false;
            ArchiveClub();
            ui.ShowCreation();
            ui.SetStatus(message + " 새 선수를 만드세요.");
        }

        void StartPlayer(string message, bool applyOffline)
        {
            retiring = false; inClub = false; inMenu = false;
            if (ClubSystem.CleanCareer(current)) saves.TrySave(current, out _);   // 예전 버전에서 선수 키우기 리그에 들어갔던 나만의 팀 정리
            current.hasClub = club != null;
            if (club != null) current.myClub = club.myClub;
            // GK 능력이 생기기 전 저장: 처음 값으로 (골키퍼는 그동안 선방을 맡던 수비 능력치를 이어받음)
            if (current.player.goalkeeping <= 0f)
                current.player.goalkeeping = current.player.position == Position.Goalkeeper ? Mathf.Max(current.player.defending, settings.initialAttribute) : settings.initialAttribute;
            training = new TrainingSystem(current, settings, trainingSettings);
            SeasonSystem.Prepare(current, matchSettings);
            SeasonSystem.EnsureContract(current, matchSettings, settings);
            TeamIdentity.MigrateSave(current, matchSettings);           // 저장에 남은 예전 팀 이름을 새 이름으로
            current.player.kitColor = TeamKits.PlayerKit(current.teamId, current.player.position);   // 유니폼은 소속 팀 유니폼 (골키퍼는 골키퍼 유니폼)
            OfflineResult offline = null;
            if (applyOffline)
            {
                offline = IdleRewardSystem.Apply(current, settings, trainingSettings, System.DateTime.UtcNow.Ticks);
                if (!saves.TrySave(current, out string saveMessage)) message = saveMessage;   // 보상과 새 기준 시각을 바로 저장
                else if (offline.ClockRewound) message = "기기 시계가 뒤로 돌아가 오프라인 보상이 없습니다.";
            }
            ui.TrainingMultiplier = RebirthSystem.TrainingMultiplier(current);
            ui.ShowPlayer(current.player, message);
            ui.RefreshTraining(current.player, training);
            RefreshSeasonUI();
            ShowOffline(offline);
        }

        void ShowOffline(OfflineResult offline)
        {
            if (offline == null || !offline.Granted) return;
            ui.ShowOfflineReward(offline);
        }

        // 모바일에서 앱이 백그라운드에서 돌아왔을 때도 같은 규칙으로 보상합니다.
        void ApplyOfflineOnResume()
        {
            if (blocked || current == null || training == null || retiring || inClub || inMenu) return;
            var offline = IdleRewardSystem.Apply(current, settings, trainingSettings, System.DateTime.UtcNow.Ticks);
            saves.TrySave(current, out _);
            if (offline.ClockRewound) ui.SetStatus("기기 시계가 뒤로 돌아가 오프라인 보상이 없습니다.");
            ui.RefreshTraining(current.player, training);
            ShowOffline(offline);
        }

        void RefreshSeasonUI()
        {
            bool awarded = AwardSystem.EnsureAwards(current, matchSettings, settings);   // 시상을 이적 제안보다 먼저
            if (SeasonSystem.EnsureOffers(current, matchSettings, settings) || awarded) saves.TrySave(current, out _);
            ui.SetTrophies(AwardSystem.DescribeTrophyRoom(current));
            var leaders = AwardSystem.LeadersView(current, matchSettings, settings, out string leadersHeader, out string leadersFooter);
            ui.SetLeaderBoard(leadersHeader, leaders, leadersFooter);
            ui.SetMatchPage(SeasonSystem.NextView(current, matchSettings, settings), SeasonSystem.LastView(current, matchSettings),
                            SeasonSystem.TableView(current, matchSettings));
            ui.SetSeason(SeasonSystem.IsComplete(current), SeasonSystem.DescribeHistory(current));
            var team = matchSettings.FindTeam(current.teamId);
            ui.SetTransfer(current.offers, current.pendingTeamId, team != null ? team.displayName : current.teamId, SeasonSystem.DescribeOffers(current),
                           SeasonSystem.TransferDecided(current));
            RefreshMoneyUI();
            RefreshRebirthUI();
            RefreshFriendlyUI();
        }

        void ChangeLook(int hair, int skin, int kit, int boots)
        {
            if (current != null) current.player.bootColor = Mathf.Clamp(boots, 0, PlayerLook.BootCount - 1);
            if (blocked || current == null) return;
            current.player.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            current.player.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            current.player.kitColor = TeamKits.PlayerKit(current.teamId, current.player.position);   // 유니폼은 소속 팀 것 그대로
            saves.TrySave(current, out string message);
            ui.ApplyLook(current.player);
            ui.SetStatus("외형을 바꿨습니다. " + message);
        }

        void Create(string rawName, Position position, Trait trait, int hair, int skin, int kit, int boots)
        {
            if (blocked || (current != null && !retiring)) return;
            string name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > settings.maxNameLength)
            { ui.SetStatus("이름을 입력하세요 (1~" + settings.maxNameLength + "자)."); return; }
            foreach (char c in name) if (char.IsControl(c)) { ui.SetStatus("이름에 쓸 수 없는 문자가 있습니다."); return; }
            var newPlayer = settings.CreatePlayer(name, position, trait);
            if (retiring) { StartNextCareer(newPlayer, hair, skin, boots); return; }   // 은퇴 뒤 새 선수
            var candidate = new SaveData { player = newPlayer };
            candidate.player.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            candidate.player.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            candidate.player.kitColor = Mathf.Clamp(kit, 0, PlayerLook.KitCount - 1);
            candidate.player.bootColor = Mathf.Clamp(boots, 0, PlayerLook.BootCount - 1);
            var imported = saves.TakeLegendImport();   // 새 게임에 가져올 '내가 키운 선수'가 준비되어 있으면 넣음
            if (imported != null)
            {
                candidate.legends.AddRange(imported);
                foreach (var lg in imported) candidate.retireCount = Mathf.Max(candidate.retireCount, lg.careerNumber);   // 지금 선수는 그다음 커리어
            }
            SeasonSystem.Prepare(candidate, matchSettings);
            if (!saves.TrySave(candidate, out string message)) { ui.SetStatus(message); return; }
            current = candidate;
            StartPlayer(message, false);
        }

        // 노력 포인트로 능력치 강화에 도전합니다. 확률로 성공·실패하고 결과 창을 보여줍니다.
        void UpgradeAttribute(AttributeKind kind)
        {
            if (blocked || current == null || training == null) return;
            var r = training.TryUpgrade(kind);
            if (r.Outcome == UpgradeOutcome.Max) { ui.SetStatus(Ko.Attr(kind) + " 능력치가 이미 최대입니다."); return; }
            if (r.Outcome == UpgradeOutcome.NotEnough)
            {
                ui.SetStatus("노력 포인트가 부족합니다: " + Ko.Attr(kind) + " 강화에 " + SeasonSystem.Money(r.Cost) + " 필요");
                return;
            }
            long nowTicks = System.DateTime.UtcNow.Ticks;
            DailySystem.Count(current, MissionKind.Upgrade, 1, nowTicks);
            if (r.Outcome != UpgradeOutcome.Fail) DailySystem.Count(current, MissionKind.UpgradeSuccess, 1, nowTicks);
            saves.TrySave(current, out _);
            ui.SetStatus(r.Outcome == UpgradeOutcome.Fail ? Ko.Attr(kind) + " 강화 실패." : Ko.Attr(kind) + " +" + (r.After - r.Before).ToString("0") + "!");
            ui.RefreshTraining(current.player, training);   // 새 값을 먼저 그린 뒤 연출
            ui.ShowUpgradeResult(r);
            RefreshRebirthUI();
        }

        // 노력 포인트로 노력 포인트 증가량을 올립니다.
        void BuyEffortRate()
        {
            if (blocked || current == null || training == null) return;
            long cost = training.RateCost;
            if (cost < 0) { ui.SetStatus("노력 증가량이 최대 레벨입니다."); return; }
            if (!training.BuyRate()) { ui.SetStatus("노력 포인트가 부족합니다: " + SeasonSystem.Money(cost) + " 필요"); return; }
            SoundFx.Play(Sfx.Buy);
            DailySystem.Count(current, MissionKind.Purchase, 1, System.DateTime.UtcNow.Ticks);
            saves.TrySave(current, out _);
            ui.SetStatus("노력 증가량 Lv " + current.effortRateLevel + ": 초당 " + training.PointsPerSecond.ToString("0.00"));
            ui.RefreshTraining(current.player, training);
        }

        // 받은 트로피가 있으면 수여 연출을 먼저 보여 주고, 그다음 시즌 결산 팝업
        void ShowSeasonEndWithTrophies()
        {
            var won = AwardSystem.SeasonTrophies(current);
            if (won.Count == 0) { ShowSeasonEnd(); return; }
            ui.ShowTrophyCeremony(won, "시즌 " + current.season.seasonNumber + " 시상식", ShowSeasonEnd);
        }

        // 시즌 결산 팝업 (시상 결과가 붙어 길이가 달라지므로 줄 수에 맞춰 높이를 정함)
        void ShowSeasonEnd()
        {
            var view = SeasonSystem.SeasonEndView(current, matchSettings);
            ui.ShowCards("시즌 종료", (body, font) => PopupCards.SeasonEnd(body, font, view));
        }

        void RefreshMoneyUI()
        {
            ui.SetMoney(current.money, FacilitySystem.MatchPay(current, matchSettings),
                        Mathf.RoundToInt((FacilitySystem.WageMultiplier(current, matchSettings) - 1f) * 100f));
            for (int i = 0; i < FacilitySystem.Count; i++)
            {
                var kind = (FacilityKind)i;
                long cost = FacilitySystem.Cost(current, kind, trainingSettings);
                ui.SetFacility(i, FacilitySystem.Name(kind) + "  Lv " + FacilitySystem.Level(current, kind) + "/" + trainingSettings.facilityMaxLevel
                    + "\n" + FacilitySystem.Effect(current, kind, trainingSettings, matchSettings)
                    + "\n" + (cost < 0 ? "최대 레벨" : "업그레이드  " + SeasonSystem.Money(cost)),
                    cost >= 0 && current.money >= cost);
            }
        }
    }
}
