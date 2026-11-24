using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public class CareerBootstrap : MonoBehaviour
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
            ui.OnOpenSquads = OpenSquads; ui.OnShowSquad = ShowSquad;
            ui.OnShowLegend = ShowLegend;
            ui.OnRetire = Retire; ui.OnCancelCreation = CancelRetire; ui.OnFoundClub = FoundClub;
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
            if (current != null) StartPlayer(message, true);
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
            retiring = false; inClub = false;
            if (ClubSystem.CleanCareer(current)) saves.TrySave(current, out _);   // 예전 버전에서 선수 키우기 리그에 들어갔던 나만의 팀 정리
            current.hasClub = club != null;
            if (club != null) current.myClub = club.myClub;
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
            if (blocked || current == null || training == null || retiring || inClub) return;
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
            if (retiring) { DoRetire(newPlayer, hair, skin, boots); return; }
            var candidate = new SaveData { player = newPlayer };
            candidate.player.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            candidate.player.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            candidate.player.kitColor = Mathf.Clamp(kit, 0, PlayerLook.KitCount - 1);
            candidate.player.bootColor = Mathf.Clamp(boots, 0, PlayerLook.BootCount - 1);
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

        // ---------- 팀 선수단 ----------
        void OpenSquads()
        {
            if (blocked || current == null) return;
            var list = new List<KeyValuePair<string, string>>();
            foreach (var id in current.season.teamIds)
            {
                var t = matchSettings.FindTeam(id);
                if (t != null) list.Add(new KeyValuePair<string, string>(id, t.displayName + (id == current.teamId ? "  (내 팀)" : "")));
            }
            ui.ShowTeamPicker(list, matchSettings.DivisionName(current.season.division) + " 팀");
        }

        void ShowSquad(string teamId)
        {
            if (blocked || current == null) return;
            var team = matchSettings.FindTeam(teamId);
            if (team == null) return;
            var stats = AwardSystem.TeamSeasonStats(current, matchSettings, settings, teamId, out int division);
            ui.ShowSquad(team.displayName + (teamId == current.teamId ? "  (내 팀)" : ""),
                         SquadSystem.View(current, matchSettings, settings, teamId, division, stats));
        }

        // ---------- 일일 미션·출석 ----------
        void CountMatchMissions(MatchRecord record, bool league)
        {
            long now = System.DateTime.UtcNow.Ticks;
            if (league)
            {
                DailySystem.Count(current, MissionKind.LeagueMatch, 1, now);
                if (record.goalsFor > record.goalsAgainst) DailySystem.Count(current, MissionKind.LeagueWin, 1, now);
            }
            else DailySystem.Count(current, MissionKind.Friendly, 1, now);
            if (record.playerPlayed) DailySystem.Count(current, MissionKind.AttackPoint, record.playerGoals + record.playerAssists, now);
        }

        void RefreshDailyUI()
        {
            if (current == null || current.player == null) return;
            long now = System.DateTime.UtcNow.Ticks;
            if (DailySystem.Ensure(current, now)) saves.TrySave(current, out _);   // 날짜가 바뀌어 새 미션
            int day = DailySystem.AttendDay(current);                 // 다음에 받을 날 (1~7)
            bool canAttend = DailySystem.CanAttend(current, now);
            int claimed = canAttend ? day - 1 : (day == 1 ? 7 : day - 1);   // 이번 7일 중 이미 받은 날 수
            var cardRewards = new string[7];
            for (int d = 1; d <= 7; d++) cardRewards[d - 1] = DailySystem.AttendShort(d);
            ui.SetAttendance(canAttend ? day + "일차 출석 보상을 받을 수 있어요!" : "오늘 출석 완료! 내일 또 만나요",
                claimed, canAttend ? day : 0, cardRewards,
                (canAttend ? "오늘 보상: " : "내일(" + day + "일차) 보상: ") + DailySystem.AttendReward(current, trainingSettings, matchSettings, day),
                "강화 1회 = 평균 능력치 Lv " + TrainingSystem.AverageLevel(current.player).ToString("0") + "에서 강화 1번 할 노력 포인트 ("
                    + SeasonSystem.Money(DailySystem.Points(current, trainingSettings, 1)) + ")"
                    + "\n수당 1주 = 지금 경기 수당 1주치 (" + SeasonSystem.Money(DailySystem.Money(current, matchSettings, 1)) + ")"
                    + "\n레벨과 주급이 오를수록 보상도 커집니다.",
                canAttend);
            string missionReward = DailySystem.RewardText(current, trainingSettings, matchSettings, DailySystem.MissionUpgrades, DailySystem.MissionWeeks);
            for (int i = 0; i < 3; i++)
            {
                if (i >= current.dailyMissions.Count) { ui.SetMission(i, "", "", false); continue; }
                var m = current.dailyMissions[i];
                string state = m.claimed ? "받음" : m.Done ? "받기" : m.progress + " / " + m.target;
                ui.SetMission(i, DailySystem.Describe(m) + "   " + m.progress + "/" + m.target + "\n" + missionReward, state, m.Done && !m.claimed);
            }
            bool bonus = DailySystem.BonusReady(current);
            ui.SetDailyBonus("새 미션까지 " + IdleRewardSystem.FormatDuration(DailySystem.SecondsToReset(now)),
                "모두 완료 보너스: " + DailySystem.RewardText(current, trainingSettings, matchSettings, DailySystem.BonusUpgrades, DailySystem.BonusWeeks),
                current.dailyBonusClaimed ? "보너스 받음" : bonus ? "보너스 받기" : "미션 3개를 모두 받으면 열림", bonus);
            ui.SetMissionBadge(DailySystem.AnyClaimable(current, now));
        }

        void AfterDailyClaim(string text, string title)
        {
            if (text == null) return;
            SoundFx.Play(Sfx.Collect);
            saves.TrySave(current, out _);
            ui.SetStatus(title + ": " + text);
            RefreshDailyUI();
            RefreshMoneyUI();
            ui.RefreshTraining(current.player, training);
        }

        void ClaimAttendance()
        {
            if (blocked || current == null) return;
            string text = DailySystem.ClaimAttendance(current, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks);
            if (text == null) { ui.SetStatus("오늘 출석 보상은 이미 받았습니다."); return; }
            AfterDailyClaim(text, "출석");
        }

        void ClaimMission(int index)
        {
            if (blocked || current == null) return;
            AfterDailyClaim(DailySystem.ClaimMission(current, index, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks), "미션 보상");
        }

        void ClaimDailyBonus()
        {
            if (blocked || current == null) return;
            AfterDailyClaim(DailySystem.ClaimBonus(current, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks), "미션 보너스");
        }

        // 돈으로 시설 레벨을 올립니다.
        void BuyFacility(int index)
        {
            if (blocked || current == null || index < 0 || index >= FacilitySystem.Count) return;
            var kind = (FacilityKind)index;
            long cost = FacilitySystem.Cost(current, kind, trainingSettings);
            if (cost < 0) { ui.SetStatus(FacilitySystem.Name(kind) + " 최대 레벨입니다."); return; }
            if (!FacilitySystem.Buy(current, kind, trainingSettings)) { ui.SetStatus("돈이 부족합니다: " + SeasonSystem.Money(cost) + " 필요"); return; }
            SoundFx.Play(Sfx.Buy);
            DailySystem.Count(current, MissionKind.Purchase, 1, System.DateTime.UtcNow.Ticks);
            saves.TrySave(current, out _);
            ui.SetStatus(FacilitySystem.Name(kind) + " Lv " + FacilitySystem.Level(current, kind) + ": " + FacilitySystem.Effect(current, kind, trainingSettings, matchSettings));
            RefreshMoneyUI();
            ui.RefreshTraining(current.player, training);
        }

        void RefreshRebirthUI()
        {
            ui.SetRebirth(RebirthSystem.DescribeStatus(current, rebirthSettings), RebirthSystem.CanRebirth(current, rebirthSettings));
            RefreshCareerUI();
        }

        // 커리어 탭 카드: 프로필, 트로피 룸, 시즌 기록, 환생 조건 (바뀐 게 없으면 화면은 그대로)
        void RefreshCareerUI()
        {
            if (current == null || current.player == null || current.season == null) return;
            var team = matchSettings.FindTeam(current.teamId);
            var d = new CareerPageData
            {
                name = current.player.playerName, position = Ko.Pos(current.player.position), trait = Ko.TraitName(current.player.trait),
                team = team != null ? team.displayName : current.teamId,
                division = current.season.division > 0 ? matchSettings.DivisionName(current.season.division) : "",
                season = current.season.seasonNumber, career = ClubSystem.CareerNumber(current),
                money = current.money, pay = FacilitySystem.MatchPay(current, matchSettings),
                agentPercent = Mathf.RoundToInt((FacilitySystem.WageMultiplier(current, matchSettings) - 1f) * 100f), wage = current.weeklyWage,
                rebirthCount = current.rebirthCount, bonus = current.rebirthBonusPercent, speed = RebirthSystem.TrainingMultiplier(current),
                canRebirth = RebirthSystem.CanRebirth(current, rebirthSettings),
                conditions = RebirthSystem.Conditions(current, rebirthSettings),
                canRetire = ClubSystem.CanRetire(current), legendNeed = ClubSystem.FoundLegends,
                hasClub = club != null, clubActive = club != null
            };
            if (current.legends != null) { d.legends.AddRange(current.legends); d.legends.Sort((a, b) => b.rating.CompareTo(a.rating)); }
            if (club != null)
            {
                var clubTeam = clubMs.FindTeam(ClubSystem.ClubId) ?? ClubSystem.ClubTeam(club, clubMs);
                d.clubName = club.myClub.name; d.clubKit = TeamKits.Home(ClubSystem.ClubId); d.clubStrength = clubTeam.strength;
                int free = ClubSystem.FreeAgents(current, club).Count;
                d.clubTactic = TacticSystem.FormationName(club.myClub.formation) + " · " + TacticSystem.StyleName(club.myClub.style) + " · 선수 " + club.legends.Count + "명"
                             + (free > 0 ? " · 영입 가능 " + free + "명" : "");
                d.clubDivision = clubMs.DivisionName(SeasonSystem.DivisionOf(club, clubMs, ClubSystem.ClubId)) + " · 시즌 " + club.season.seasonNumber;
            }
            d.trophyCounts = new int[System.Enum.GetValues(typeof(TrophyKind)).Length];
            if (current.trophies != null)
            {
                // 트로피 룸에는 지금 선수(이번 커리어)의 트로피만. 지난 선수의 트로피는 내가 키운 선수 기록에서 봄
                int careerNo = ClubSystem.CareerNumber(current);
                foreach (var t in current.trophies) if (t.career == careerNo) { d.trophyCounts[(int)t.kind]++; d.trophyTotal++; }
                for (int i = current.trophies.Count - 1; i >= 0 && d.recent.Count < 4; i--) if (current.trophies[i].career == careerNo) d.recent.Add(current.trophies[i]);
            }
            for (int i = current.seasonHistory.Count - 1; i >= 0 && d.history.Count < 5; i--) d.history.Add(current.seasonHistory[i]);
            if (current.careers != null && current.careers.Count > 0) { d.hasLastCareer = true; d.lastCareer = current.careers[current.careers.Count - 1]; }
            ui.SetCareer(d);
            ui.SetCurrentTeam(SeasonSystem.CurrentTeamInfo(current, matchSettings, settings));
        }

        // ---------- 은퇴 ----------
        void Retire()
        {
            if (blocked || current == null) return;
            if (!ClubSystem.CanRetire(current)) { ui.SetStatus("시즌이 끝나야 은퇴할 수 있습니다."); return; }
            var record = ClubSystem.CurrentRecord(current, matchSettings, settings);
            int legends = ClubSystem.LegendCount(current), need = current.hasClub ? 0 : ClubSystem.FoundLegends;
            ui.ShowCardsConfirm("은퇴할까요?", (body, font) => PopupCards.RetireConfirm(body, font, record, legends, need),
                "은퇴하고 새 선수 만들기", () =>
            {
                if (blocked || current == null || !ClubSystem.CanRetire(current)) return;
                saves.TrySave(current, out _);
                retiring = true;
                ui.ShowCreation(true);   // 새 선수를 만들면 그때 은퇴합니다 (취소하면 그대로)
            });
        }

        // 내가 키운 선수 한 명의 커리어 기록
        void ShowLegend(int careerNumber)
        {
            if (blocked || current == null || current.legends == null) return;
            var lg = current.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null) return;
            var record = ClubSystem.LegendRecord(current, lg);
            ui.ShowCards("내가 키운 선수", (body, font) => PopupCards.CareerRecord(body, font, record, "은퇴 · 마지막 팀 " + lg.lastTeam), null, "닫기");
        }

        void CancelRetire()
        {
            if (!retiring || current == null) return;
            StartPlayer(null, false);
        }

        void DoRetire(PlayerData newPlayer, int hair, int skin, int boots)
        {
            if (current == null || !ClubSystem.CanRetire(current)) { retiring = false; return; }
            newPlayer.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            newPlayer.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            newPlayer.bootColor = Mathf.Clamp(boots, 0, PlayerLook.BootCount - 1);
            var legend = ClubSystem.Retire(current, matchSettings, settings, newPlayer);
            saves.TrySave(current, out _);
            StartPlayer(legend.name + " 은퇴. 새 커리어 시작.", false);
            int count = ClubSystem.LegendCount(current);
            bool canFound = ClubSystem.CanFound(current);
            string newName = newPlayer.playerName;
            ui.ShowCards("은퇴 완료", (body, font) => PopupCards.RetireDone(body, font, legend.name, newName, count, ClubSystem.FoundLegends, canFound || current.hasClub));
        }

        // ---------- 나만의 팀 ----------
        void FoundClub(string name, int kit, Formation formation, TacticStyle style)
        {
            if (blocked || current == null) return;
            if (club != null || !ClubSystem.CanFound(current)) { ui.SetStatus("은퇴 선수 " + ClubSystem.FoundLegends + "명이 있어야 창단할 수 있습니다."); return; }
            if (name.Length > 24) name = name.Substring(0, 24);
            club = ClubSystem.Found(current, clubMs, baseTeams, name, kit, formation, style);
            if (!clubSaves.TrySave(club, out string message)) { club = null; current.hasClub = false; ui.SetStatus(message); return; }
            saves.TrySave(current, out _);
            var team = clubMs.FindTeam(ClubSystem.ClubId);
            int kitIndex = TeamKits.Home(ClubSystem.ClubId), legends = club.legends.Count;
            string tactic = TacticSystem.FormationName(formation) + " · " + TacticSystem.StyleName(style);
            string division = clubMs.DivisionName(club.season.division);
            SoundFx.Play(Sfx.Buy);
            OpenClubMode();
            ui.ShowCards("창단 완료", (body, font) => PopupCards.ClubFounded(body, font, name, kitIndex, tactic, legends, team != null ? team.strength : 0f, division, false));
        }

        void LoadClub()
        {
            club = null;
            if (!clubSaves.TryLoad(out var loaded, out _) || loaded == null || !loaded.teamMode) return;
            club = loaded;
            ClubSystem.ApplyRoster(club, clubMs, baseTeams);
            SeasonSystem.Prepare(club, clubMs);
        }

        // 저장 초기화: 나만의 팀 저장도 옆으로 옮겨 둠
        void ArchiveClub()
        {
            if (club != null) clubSaves.TrySave(club, out _);
            clubSaves.ArchiveSave(out _);
            club = null; inClub = false;
        }

        // ---------- 나만의 팀 모드 ----------
        void OpenClubMode()
        {
            if (blocked || current == null || club == null) return;
            inClub = true;
            ClubSystem.ApplyRoster(club, clubMs, baseTeams);
            SeasonSystem.Prepare(club, clubMs);
            ShowClubScreen();
        }

        void ShowClubScreen()
        {
            var team = clubMs.FindTeam(ClubSystem.ClubId);
            var s = club.season;
            var v = new ClubModeView
            {
                name = club.myClub.name, kit = TeamKits.Home(ClubSystem.ClubId), season = s.seasonNumber, legends = club.legends.Count,
                tactic = TacticSystem.FormationName(club.myClub.formation) + " · " + TacticSystem.StyleName(club.myClub.style),
                division = clubMs.DivisionName(s.division), strength = team != null ? team.strength : 0f,
                complete = SeasonSystem.IsComplete(club), round = s.nextMatchIndex, rounds = s.matches.Count,
                table = SeasonSystem.TableView(club, clubMs)
            };
            foreach (var m in s.matches)
            {
                if (!m.played) continue;
                v.gf += m.goalsFor; v.ga += m.goalsAgainst;
                if (m.goalsFor > m.goalsAgainst) v.w++; else if (m.goalsFor == m.goalsAgainst) v.d++; else v.l++;
            }
            for (int i = s.nextMatchIndex - 1; i >= 0 && v.recent.Count < 3; i--)
            {
                var m = s.matches[i];
                v.recent.Add((m.goalsFor > m.goalsAgainst ? "승 " : m.goalsFor == m.goalsAgainst ? "무 " : "패 ") + m.goalsFor + "-" + m.goalsAgainst + " " + m.opponentName);
            }
            if (!v.complete)
            {
                var next = s.matches[s.nextMatchIndex];
                v.nextText = next.round + "라운드 · " + (next.home ? "홈" : "원정") + " · " + next.opponentName;
            }
            v.free = ClubSystem.FreeAgents(current, club);
            ui.ShowClubMode(v);
        }

        void ClubPlay()
        {
            if (club == null || ui.IsMatchPlaying) return;
            if (SeasonSystem.IsComplete(club))
            {
                SeasonSystem.StartNextSeason(club, clubMs, settings, out var info);
                clubSaves.TrySave(club, out _);
                ShowClubScreen();
                ui.ShowCards("새 시즌", (body, font) => PopupCards.NewSeason(body, font, info));
                return;
            }
            var record = SeasonSystem.PlayNext(club, clubMs, settings);
            if (record == null) { ui.SetStatus("경기를 할 수 없습니다."); return; }
            clubSaves.TrySave(club, out _);
            var own = clubMs.FindTeam(ClubSystem.ClubId);
            ui.ReplayOwnTactic = TacticSystem.Of(own); ui.ReplayOppTactic = TacticSystem.Of(clubMs.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(ClubSystem.ClubId); ui.ReplayOppKit = TeamKits.Opponent(ClubSystem.ClubId, record.opponentId);
            ui.ReplayYouKit = 0; ui.ReplayTeamMode = true;
            ui.ShowMatchReplay(record, club.player, club.myClub.name, () =>
            {
                ShowClubScreen();
                System.Action after = null;
                if (SeasonSystem.IsComplete(club)) after = ShowClubSeasonEnd;
                ui.ShowMatchResult("경기 결과", SeasonSystem.ResultData(record, club, clubMs, null), after);
            });
        }

        void ShowClubSeasonEnd()
        {
            SeasonSystem.EnsureSeasonEnd(club, clubMs);
            clubSaves.TrySave(club, out _);
            var sum = SeasonSystem.Summarize(club, clubMs);
            int teams = club.season.teamIds.Count, div = club.season.division, next = club.season.nextDivision;
            int move = next > 0 && div > 0 ? (next < div ? 1 : next > div ? -1 : 0) : 0;
            string name = club.myClub.name, division = clubMs.DivisionName(div);
            ShowClubScreen();
            ui.ShowCards("시즌 종료", (body, font) => ClubModeScreen.SeasonEnd(body, font, name, sum.finalPosition, teams, division, move, sum.wins, sum.draws, sum.losses, sum.goalsFor, sum.goalsAgainst));
        }

        void ClubSquad()
        {
            if (club == null) return;
            int div = SeasonSystem.DivisionOf(club, clubMs, ClubSystem.ClubId);
            ui.ShowSquad(club.myClub.name, SquadSystem.View(club, clubMs, settings, ClubSystem.ClubId, div, null));
        }

        void ClubSign(int careerNumber)
        {
            if (club == null || current == null) return;
            var lg = current.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null) return;
            float before = clubMs.FindTeam(ClubSystem.ClubId).strength;
            if (!ClubSystem.Sign(current, club, clubMs, baseTeams, careerNumber)) return;
            clubSaves.TrySave(club, out _);
            float after = clubMs.FindTeam(ClubSystem.ClubId).strength;
            string name = club.myClub.name;
            SoundFx.Play(Sfx.Buy);
            ShowClubScreen();
            ui.ShowCards("무료 영입", (body, font) => ClubModeScreen.Signed(body, font, lg, name, before, after));
        }

        void ClubBack()
        {
            inClub = false;
            ui.ReplayTeamMode = false;
            StartPlayer(null, false);
        }

        void Rebirth()
        {
            if (blocked || current == null) return;
            if (!RebirthSystem.CanRebirth(current, rebirthSettings)) { ui.SetStatus("아직 환생 조건을 채우지 못했습니다."); return; }
            var preview = RebirthSystem.PreviewView(current, settings, rebirthSettings);
            ui.ShowCardsConfirm("환생할까요?", (body, font) => PopupCards.RebirthPreview(body, font, preview), "환생", DoRebirth);
        }

        void DoRebirth()
        {
            if (blocked || current == null || !RebirthSystem.CanRebirth(current, rebirthSettings)) return;
            RebirthSystem.Perform(current, settings, rebirthSettings, matchSettings);
            saves.TrySave(current, out _);
            StartPlayer("환생 완료.", false);   // 새 커리어 화면으로 다시 만듭니다.
            int count = current.rebirthCount;
            float total = current.rebirthBonusPercent, speed = RebirthSystem.TrainingMultiplier(current);
            float gain = current.careers.Count > 0 ? current.careers[current.careers.Count - 1].bonusGainedPercent : 0f;
            ui.ShowCards("환생 완료", (body, font) => PopupCards.RebirthDone(body, font, count, gain, total, speed));
        }

        // 이적 제안을 고르면 한 번 더 묻고, 확정하면 저장합니다. 확정한 뒤에는 바꿀 수 없습니다.
        void ChooseOffer(string teamId)
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("이번 시즌 선택은 이미 확정되었습니다."); return; }
            var offer = SeasonSystem.FindOffer(current, teamId);
            if (offer == null) return;
            string division = offer.division > 0 ? matchSettings.DivisionName(offer.division) : "";
            int startPercent = Mathf.RoundToInt(offer.startChance * 100f), currentWage = current.weeklyWage;
            ui.ShowCardsConfirm("이적할까요?", (body, font) => PopupCards.TransferConfirm(body, font, offer.teamName, division, startPercent, offer.weeklyWage, currentWage),
                "이적하기", () =>
            {
                if (SeasonSystem.SetPending(current, teamId) == null) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus(offer.teamName + " 이적 확정. 다음 시즌부터 뜁니다. " + message);
            });
        }

        void StayWithTeam()
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("이번 시즌 선택은 이미 확정되었습니다."); return; }
            var team = matchSettings.FindTeam(current.teamId);
            string name = team != null ? team.displayName : current.teamId;
            ui.ShowCardsConfirm("잔류할까요?", (body, font) => PopupCards.StayConfirm(body, font, name),
                "잔류하기", () =>
            {
                if (!SeasonSystem.ConfirmStay(current)) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus(name + " 잔류 확정. " + message);
            });
        }

        void PlayMatch()
        {
            if (blocked || current == null || ui.IsMatchPlaying) return;

            if (SeasonSystem.IsComplete(current))
            {
                string changeMessage = SeasonSystem.StartNextSeason(current, matchSettings, settings, out var newSeason);   // 이적·승강 안내 (바뀐 게 없으면 null)
                int teamKit = TeamKits.PlayerKit(current.teamId, current.player.position);
                if (current.player.kitColor != teamKit) { current.player.kitColor = teamKit; ui.ApplyLook(current.player); }   // 이적하면 새 팀 유니폼으로
                saves.TrySave(current, out string newSeasonMessage);
                RefreshSeasonUI();
                ui.SetStatus("새 시즌이 시작되었습니다. " + newSeasonMessage);
                if (changeMessage != null)
                    ui.ShowCards("새 시즌", (body, font) => PopupCards.NewSeason(body, font, newSeason));
                return;
            }

            var record = SeasonSystem.PlayNext(current, matchSettings, settings);
            if (record == null) { ui.SetStatus("경기를 할 수 없습니다: 팀 정보가 없습니다."); return; }
            CountMatchMissions(record, true);
            saves.TrySave(current, out string message);   // 결과를 바로 저장해 다시 불러와도 바뀌지 않게 합니다.
            ui.SetStatus(message);
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            // 경기 영상을 보여 준 뒤(건너뛰기 포함) 순위와 요약을 갱신하고 결과를 알려 줍니다.
            ui.ReplayOwnTactic = TacticSystem.Of(ownTeam); ui.ReplayOppTactic = TacticSystem.Of(matchSettings.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(current.teamId); ui.ReplayOppKit = TeamKits.Opponent(current.teamId, record.opponentId);
            ui.ReplayYouKit = current.player.kitColor; ui.ReplayTeamMode = false;
            // 세리머니의 '시즌 N호 골': 이번 경기를 뺀 시즌 골 수
            int seasonGoals = 0, seasonAssists = 0;
            foreach (var sm in current.season.matches) if (sm.played && sm.playerPlayed) { seasonGoals += sm.playerGoals; seasonAssists += sm.playerAssists; }
            ui.ReplaySeasonGoals = Mathf.Max(0, seasonGoals - record.playerGoals);
            ui.ReplaySeasonAssists = Mathf.Max(0, seasonAssists - record.playerAssists);
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshSeasonUI();
                System.Action afterResult = null;
                if (SeasonSystem.IsComplete(current))   // 마지막 경기라면 결과 팝업을 닫은 뒤 시즌 결산을 보여줍니다.
                    afterResult = ShowSeasonEndWithTrophies;
                ui.ShowMatchResult("경기 결과", SeasonSystem.ResultData(record, current, matchSettings, null), afterResult);
            });
        }

        // 친선 경기: 순위와 상관없는 한 경기. 주급을 받고, friendlyRewardEvery경기마다 노력 포인트 보상
        void PlayFriendly()
        {
            if (blocked || current == null || training == null || ui.IsMatchPlaying) return;
            long now = System.DateTime.UtcNow.Ticks;
            float wait = training.FriendlyWait(now);
            if (wait > 0f) { ui.SetStatus("다음 친선 경기까지 " + IdleRewardSystem.FormatDuration(wait) + "."); return; }

            var record = SeasonSystem.PlayFriendly(current, matchSettings, settings, new System.Random(unchecked((int)now)));
            if (record == null) { ui.SetStatus("친선 경기를 할 수 없습니다: 상대 팀이 없습니다."); return; }
            long reward = TrainingSystem.CountFriendly(current, trainingSettings, now);
            CountMatchMissions(record, false);
            saves.TrySave(current, out string message);
            ui.SetStatus(message);
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            string rewardLine = reward > 0
                ? "\n친선 경기 보상: 노력 포인트 +" + SeasonSystem.Money(reward) + "!"
                : "\n친선 경기 " + current.friendlyCount + "/" + every + "  (" + every + "경기마다 보상)";
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            ui.ReplayOwnTactic = TacticSystem.Of(ownTeam); ui.ReplayOppTactic = TacticSystem.Of(matchSettings.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(current.teamId); ui.ReplayOppKit = TeamKits.Opponent(current.teamId, record.opponentId);
            ui.ReplayYouKit = current.player.kitColor; ui.ReplayTeamMode = false;
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshMoneyUI();
                RefreshFriendlyUI();
                ui.RefreshTraining(current.player, training);
                ui.ShowMatchResult("친선 경기 결과", SeasonSystem.ResultData(record, current, matchSettings, rewardLine.Trim()), null);
            });
        }

        void RefreshFriendlyUI()
        {
            if (current == null || training == null) return;
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            float wait = training.FriendlyWait(System.DateTime.UtcNow.Ticks);
            string reward = "노력 포인트 +" + SeasonSystem.Money(training.NextFriendlyReward);
            ui.SetFriendly((every <= 1 ? "친선 경기마다 " + reward
                                       : "친선 경기 " + current.friendlyCount + "/" + every + "   보상 " + reward)
                + "\n" + (wait > 0f ? "다음 친선 경기까지 " + IdleRewardSystem.FormatDuration(wait) : "지금 할 수 있습니다. 주급도 받습니다."),
                wait <= 0f);
        }

        void Update()
        {
            if (blocked || training == null || current == null || retiring || inClub) return;   // 은퇴 후 새 선수 만들기·나만의 팀 화면에서는 멈춤

            if (Application.isFocused && !ui.IsMatchPlaying) training.Tick(Time.unscaledDeltaTime);   // 경기 영상 중에는 훈련 중지

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= 30f) { autoSaveTimer = 0f; saves.TrySave(current, out _); }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.2f) { refreshTimer = 0f; ui.RefreshTraining(current.player, training); RefreshRebirthUI(); RefreshFriendlyUI(); RefreshDailyUI(); }
        }

        void Save()
        {
            if (blocked || current == null) return;
            saves.TrySave(current, out string message);
            if (ui != null) ui.SetStatus(message);
        }

        void QuietSave()
        {
            if (blocked || current == null) return;
            saves.TrySave(current, out _);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) QuietSave();
            else ApplyOfflineOnResume();
        }
        void OnApplicationFocus(bool focus) { if (!focus) QuietSave(); }
        void OnApplicationQuit() { QuietSave(); }
        void OnDestroy()
        {
            if (temporarySettings && settings != null) Destroy(settings);
            if (temporaryTraining && trainingSettings != null) Destroy(trainingSettings);
            if ((temporaryMatch || clonedMatch) && matchSettings != null) Destroy(matchSettings);
            if (temporaryRebirth && rebirthSettings != null) Destroy(rebirthSettings);
        }
    }
}
