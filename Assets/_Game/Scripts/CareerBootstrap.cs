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
            ui.ShowConfirm("START NEW GAME?", "The broken save file is moved aside, not deleted. A new player will be created.", 150f, "START NEW", DoStartNew);
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
            ui.ShowConfirm("RESET SAVE?", "Your current player and progress are moved aside (not deleted) and you start over with a new player. Rebirth bonuses are not kept.", 200f, "RESET", DoResetSave);
        }

        void DoResetSave()
        {
            if (blocked || current == null) return;
            saves.TrySave(current, out _);
            if (!saves.ArchiveSave(out string message)) { ui.SetStatus(message); return; }
            current = null; training = null;
            ui.ShowCreation();
            ui.SetStatus(message + " Create a new player.");
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
                else if (offline.ClockRewound) message = "System clock moved backwards. No offline reward.";
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
            ui.ShowPopup("WELCOME BACK", IdleRewardSystem.Describe(offline));
        }

        // 모바일에서 앱이 백그라운드에서 돌아왔을 때도 같은 규칙으로 보상합니다.
        void ApplyOfflineOnResume()
        {
            if (blocked || current == null || training == null) return;
            var offline = IdleRewardSystem.Apply(current, settings, trainingSettings, System.DateTime.UtcNow.Ticks);
            saves.TrySave(current, out _);
            if (offline.ClockRewound) ui.SetStatus("System clock moved backwards. No offline reward.");
            ui.RefreshTraining(current.player, training);
            ShowOffline(offline);
        }

        void RefreshSeasonUI()
        {
            bool awarded = AwardSystem.EnsureAwards(current, matchSettings, settings);   // 시상을 이적 제안보다 먼저
            if (SeasonSystem.EnsureOffers(current, matchSettings, settings) || awarded) saves.TrySave(current, out _);
            ui.SetTrophies(AwardSystem.DescribeTrophyRoom(current));
            ui.SetSeason(SeasonSystem.DescribeNext(current, matchSettings, settings),
                         SeasonSystem.DescribeLast(current, matchSettings),
                         SeasonSystem.IsComplete(current),
                         SeasonSystem.DescribeStandings(current, matchSettings),
                         SeasonSystem.DescribeHistory(current));
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
            ui.SetStatus("Look changed. " + message);
        }

        void Create(string rawName, Position position, Trait trait, int hair, int skin, int kit)
        {
            if (blocked || current != null) return;
            string name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > settings.maxNameLength)
            { ui.SetStatus("Enter a name (1-" + settings.maxNameLength + " characters)."); return; }
            foreach (char c in name) if (char.IsControl(c)) { ui.SetStatus("Name contains invalid characters."); return; }
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
            if (r.Outcome == UpgradeOutcome.Max) { ui.SetStatus(kind + " is already at the maximum."); return; }
            if (r.Outcome == UpgradeOutcome.NotEnough)
            {
                ui.SetStatus("Not enough effort points: " + kind + " needs " + SeasonSystem.Money(r.Cost) + ".");
                return;
            }
            saves.TrySave(current, out _);
            ui.SetStatus(r.Outcome == UpgradeOutcome.Fail ? kind + " upgrade failed." : kind + " +" + (r.After - r.Before).ToString("0") + ".");
            ui.RefreshTraining(current.player, training);   // 새 값을 먼저 그린 뒤 연출
            ui.ShowUpgradeResult(r);
            RefreshRebirthUI();
        }

        // 노력 포인트로 노력 포인트 증가량을 올립니다.
        void BuyEffortRate()
        {
            if (blocked || current == null || training == null) return;
            long cost = training.RateCost;
            if (cost < 0) { ui.SetStatus("Effort rate is at the max level."); return; }
            if (!training.BuyRate()) { ui.SetStatus("Not enough effort points: " + SeasonSystem.Money(cost) + " needed."); return; }
            saves.TrySave(current, out _);
            ui.SetStatus("Effort rate Lv " + current.effortRateLevel + ": " + training.PointsPerSecond.ToString("0.00") + " per second.");
            ui.RefreshTraining(current.player, training);
        }

        // 시즌 결산 팝업 (시상 결과가 붙어 길이가 달라지므로 줄 수에 맞춰 높이를 정함)
        void ShowSeasonEnd()
        {
            string text = SeasonSystem.DescribeSeasonEnd(current, matchSettings);
            ui.ShowPopup("SEASON COMPLETE", text, Mathf.Clamp(text.Split('\n').Length * 29f + 16f, 260f, 760f));
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
                    + "\n" + (cost < 0 ? "MAX LEVEL" : "UPGRADE  " + SeasonSystem.Money(cost)),
                    cost >= 0 && current.money >= cost);
            }
        }

        // 돈으로 시설 레벨을 올립니다.
        void BuyFacility(int index)
        {
            if (blocked || current == null || index < 0 || index >= FacilitySystem.Count) return;
            var kind = (FacilityKind)index;
            long cost = FacilitySystem.Cost(current, kind, trainingSettings);
            if (cost < 0) { ui.SetStatus(FacilitySystem.Name(kind) + " is at the max level."); return; }
            if (!FacilitySystem.Buy(current, kind, trainingSettings)) { ui.SetStatus("Not enough money: " + SeasonSystem.Money(cost) + " needed."); return; }
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
            if (!RebirthSystem.CanRebirth(current, rebirthSettings)) { ui.SetStatus("Rebirth conditions are not met yet."); return; }
            ui.ShowConfirm("REBIRTH?", RebirthSystem.DescribePreview(current, settings, rebirthSettings), 360f, "REBIRTH", DoRebirth);
        }

        void DoRebirth()
        {
            if (blocked || current == null || !RebirthSystem.CanRebirth(current, rebirthSettings)) return;
            string message = RebirthSystem.Perform(current, settings, rebirthSettings, matchSettings);
            saves.TrySave(current, out _);
            StartPlayer("Rebirth complete.", false);   // 새 커리어 화면으로 다시 만듭니다.
            ui.ShowPopup("REBIRTH COMPLETE", message, 170f);
        }

        // 이적 제안을 고르면 한 번 더 묻고, 확정하면 저장합니다. 확정한 뒤에는 바꿀 수 없습니다.
        void ChooseOffer(string teamId)
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("Your decision for this season is final."); return; }
            var offer = SeasonSystem.FindOffer(current, teamId);
            if (offer == null) return;
            string body = "Join " + offer.teamName + (offer.division > 0 ? " (Div " + offer.division + ")" : "") + " next season?"
                + "\nStart chance " + Mathf.RoundToInt(offer.startChance * 100f) + "%"
                + (offer.weeklyWage > 0 ? "   Wage " + SeasonSystem.Money(offer.weeklyWage) + " per week" : "")
                + "\n\nThis cannot be changed later.";
            ui.ShowConfirm("CONFIRM TRANSFER", body, 170f, "JOIN " + offer.teamName.ToUpperInvariant(), () =>
            {
                if (SeasonSystem.SetPending(current, teamId) == null) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus("Transfer to " + offer.teamName + " confirmed. It starts next season. " + message);
            });
        }

        void StayWithTeam()
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("Your decision for this season is final."); return; }
            var team = matchSettings.FindTeam(current.teamId);
            string name = team != null ? team.displayName : current.teamId;
            ui.ShowConfirm("CONFIRM STAY", "Turn down every offer and stay with " + name + " next season?\n\nThis cannot be changed later.", 150f,
                "STAY WITH " + name.ToUpperInvariant(), () =>
            {
                if (!SeasonSystem.ConfirmStay(current)) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus("You will stay with " + name + ". " + message);
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
                ui.SetStatus("New season started. " + newSeasonMessage);
                if (changeMessage != null)
                    ui.ShowPopup("NEW SEASON", changeMessage + "\nSeason " + current.season.seasonNumber + " starts in "
                        + matchSettings.DivisionName(current.season.division) + ".", 170f);
                return;
            }

            var record = SeasonSystem.PlayNext(current, matchSettings, settings);
            if (record == null) { ui.SetStatus("Cannot play: team data is missing."); return; }
            saves.TrySave(current, out string message);   // 결과를 바로 저장해 다시 불러와도 바뀌지 않게 합니다.
            ui.SetStatus(message);
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            // 경기 영상을 보여 준 뒤(건너뛰기 포함) 순위와 요약을 갱신하고 결과를 알려 줍니다.
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshSeasonUI();
                System.Action afterResult = null;
                if (SeasonSystem.IsComplete(current))   // 마지막 경기라면 결과 팝업을 닫은 뒤 시즌 결산을 보여줍니다.
                    afterResult = () => ShowSeasonEnd();
                ui.ShowPopup("MATCH RESULT", SeasonSystem.DescribeResult(record, current, matchSettings), 260f, afterResult);
            });
        }

        // 친선 경기: 순위와 상관없는 한 경기. 주급을 받고, friendlyRewardEvery경기마다 노력 포인트 보상
        void PlayFriendly()
        {
            if (blocked || current == null || training == null || ui.IsMatchPlaying) return;
            long now = System.DateTime.UtcNow.Ticks;
            float wait = training.FriendlyWait(now);
            if (wait > 0f) { ui.SetStatus("Next friendly in " + IdleRewardSystem.FormatDuration(wait) + "."); return; }

            var record = SeasonSystem.PlayFriendly(current, matchSettings, settings, new System.Random(unchecked((int)now)));
            if (record == null) { ui.SetStatus("Cannot play a friendly: no opponent."); return; }
            long reward = TrainingSystem.CountFriendly(current, trainingSettings, now);
            saves.TrySave(current, out string message);
            ui.SetStatus(message);
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            string rewardLine = reward > 0
                ? "\nFriendly reward: +" + SeasonSystem.Money(reward) + " effort points!"
                : "\nFriendlies " + current.friendlyCount + "/" + every + "  (reward after " + every + ")";
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshMoneyUI();
                RefreshFriendlyUI();
                ui.RefreshTraining(current.player, training);
                ui.ShowPopup("FRIENDLY RESULT", SeasonSystem.DescribeResult(record, current, matchSettings) + rewardLine, 290f);
            });
        }

        void RefreshFriendlyUI()
        {
            if (current == null || training == null) return;
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            float wait = training.FriendlyWait(System.DateTime.UtcNow.Ticks);
            string reward = "+" + SeasonSystem.Money(training.NextFriendlyReward) + " effort points";
            ui.SetFriendly((every <= 1 ? "Reward " + reward + " per friendly"
                                       : "Friendlies " + current.friendlyCount + "/" + every + "   Reward " + reward)
                + "\n" + (wait > 0f ? "Next friendly in " + IdleRewardSystem.FormatDuration(wait) : "Ready to play.  Wage paid as usual."),
                wait <= 0f);
        }

        void Update()
        {
            if (blocked || training == null || current == null) return;

            if (Application.isFocused && !ui.IsMatchPlaying) training.Tick(Time.unscaledDeltaTime);   // 경기 영상 중에는 훈련 중지

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= 30f) { autoSaveTimer = 0f; saves.TrySave(current, out _); }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.2f) { refreshTimer = 0f; ui.RefreshTraining(current.player, training); RefreshRebirthUI(); RefreshFriendlyUI(); }
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
