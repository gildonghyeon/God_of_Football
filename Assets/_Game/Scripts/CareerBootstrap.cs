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
            saves = new CareerSaveSystem();
            ui = gameObject.AddComponent<CareerPrototypeUI>();
            ui.Initialize(uiFont, settings, trainingSettings, Create, Save, UpgradeAttribute, PlayMatch, ChooseOffer, StayWithTeam, Rebirth);
            ui.OnRetry = RetryLoad;
            ui.OnRestoreBackup = RestoreBackup;
            ui.OnStartNew = StartNewGame;
            ui.OnResetSave = ResetSave;
            ui.OnApplyLook = ChangeLook;
            ui.OnBuyEffortRate = BuyEffortRate;
            ui.OnPlayFriendly = PlayFriendly;
            ui.OnBuyFacility = BuyFacility;
            ui.OnClaimAttendance = ClaimAttendance; ui.OnClaimMission = ClaimMission; ui.OnClaimBonus = ClaimDailyBonus;
            ui.OnOpenSquads = OpenSquads; ui.OnShowSquad = ShowSquad;
            Debug.Log("Save folder: " + Application.persistentDataPath);
            LoadAndShow(null);
        }

        // 저장을 읽어 알맞은 화면(선수 생성, 플레이어, 오류)을 보여줍니다.
        void LoadAndShow(string extraMessage)
        {
            training = null;
            blocked = !saves.TryLoad(out current, out string message);
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
            blocked = false; current = null; training = null;
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
            current = null; training = null;
            ui.ShowCreation();
            ui.SetStatus(message + " 새 선수를 만드세요.");
        }

        void StartPlayer(string message, bool applyOffline)
        {
            training = new TrainingSystem(current, settings, trainingSettings);
            SeasonSystem.Prepare(current, matchSettings);
            SeasonSystem.EnsureContract(current, matchSettings, settings);
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
            if (blocked || current == null || training == null) return;
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

        void ChangeLook(int hair, int skin, int kit)
        {
            if (blocked || current == null) return;
            current.player.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            current.player.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            current.player.kitColor = Mathf.Clamp(kit, 0, PlayerLook.KitCount - 1);
            saves.TrySave(current, out string message);
            ui.ApplyLook(current.player);
            ui.SetStatus("외형을 바꿨습니다. " + message);
        }

        void Create(string rawName, Position position, Trait trait, int hair, int skin, int kit)
        {
            if (blocked || current != null) return;
            string name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > settings.maxNameLength)
            { ui.SetStatus("이름을 입력하세요 (1~" + settings.maxNameLength + "자)."); return; }
            foreach (char c in name) if (char.IsControl(c)) { ui.SetStatus("이름에 쓸 수 없는 문자가 있습니다."); return; }
            var candidate = new SaveData { player = settings.CreatePlayer(name, position, trait) };
            candidate.player.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            candidate.player.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            candidate.player.kitColor = Mathf.Clamp(kit, 0, PlayerLook.KitCount - 1);
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
            string text = SeasonSystem.DescribeSeasonEnd(current, matchSettings);
            ui.ShowPopup("시즌 종료", text, Mathf.Clamp(text.Split('\n').Length * 29f + 16f, 260f, 760f));
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
        }

        void Rebirth()
        {
            if (blocked || current == null) return;
            if (!RebirthSystem.CanRebirth(current, rebirthSettings)) { ui.SetStatus("아직 환생 조건을 채우지 못했습니다."); return; }
            ui.ShowConfirm("환생할까요?", RebirthSystem.DescribePreview(current, settings, rebirthSettings), 360f, "환생", DoRebirth);
        }

        void DoRebirth()
        {
            if (blocked || current == null || !RebirthSystem.CanRebirth(current, rebirthSettings)) return;
            string message = RebirthSystem.Perform(current, settings, rebirthSettings, matchSettings);
            saves.TrySave(current, out _);
            StartPlayer("환생 완료.", false);   // 새 커리어 화면으로 다시 만듭니다.
            ui.ShowPopup("환생 완료", message, 170f);
        }

        // 이적 제안을 고르면 한 번 더 묻고, 확정하면 저장합니다. 확정한 뒤에는 바꿀 수 없습니다.
        void ChooseOffer(string teamId)
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("이번 시즌 선택은 이미 확정되었습니다."); return; }
            var offer = SeasonSystem.FindOffer(current, teamId);
            if (offer == null) return;
            string body = offer.teamName + (offer.division > 0 ? " (" + offer.division + "부)" : "") + "\n다음 시즌에 이 팀으로 이적할까요?"
                + "\n선발 확률 " + Mathf.RoundToInt(offer.startChance * 100f) + "%"
                + (offer.weeklyWage > 0 ? "   주급 " + SeasonSystem.Money(offer.weeklyWage) : "")
                + "\n\n확정하면 바꿀 수 없습니다.";
            ui.ShowConfirm("이적 확정", body, 170f, "이적하기", () =>
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
            ui.ShowConfirm("잔류 확정", "모든 제안을 거절하고 다음 시즌에도 " + name + "에서 뛸까요?\n\n확정하면 바꿀 수 없습니다.", 150f,
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
                string changeMessage = SeasonSystem.StartNextSeason(current, matchSettings, settings);   // 이적·승강 안내 (바뀐 게 없으면 null)
                saves.TrySave(current, out string newSeasonMessage);
                RefreshSeasonUI();
                ui.SetStatus("새 시즌이 시작되었습니다. " + newSeasonMessage);
                if (changeMessage != null)
                    ui.ShowPopup("새 시즌", changeMessage + "\n시즌 " + current.season.seasonNumber + " 시작: "
                        + matchSettings.DivisionName(current.season.division), 170f);
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
            if (blocked || training == null || current == null) return;

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
            if (temporaryMatch && matchSettings != null) Destroy(matchSettings);
            if (temporaryRebirth && rebirthSettings != null) Destroy(rebirthSettings);
        }
    }
}
