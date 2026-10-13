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
            ui.Initialize(uiFont, settings, trainingSettings, Create, Save, SelectTraining, StopTraining, PlayMatch, ChooseOffer, StayWithTeam, Rebirth);
            ui.OnRetry = RetryLoad;
            ui.OnRestoreBackup = RestoreBackup;
            ui.OnStartNew = StartNewGame;
            ui.OnResetSave = ResetSave;
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
            training.ValidateSelection();
            SeasonSystem.Prepare(current, matchSettings);
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
            if (SeasonSystem.EnsureOffers(current, matchSettings, settings)) saves.TrySave(current, out _);
            ui.SetSeason(SeasonSystem.DescribeNext(current, matchSettings),
                         SeasonSystem.DescribeLast(current, matchSettings),
                         SeasonSystem.IsComplete(current),
                         SeasonSystem.DescribeStandings(current, matchSettings),
                         SeasonSystem.DescribeHistory(current));
            var team = matchSettings.FindTeam(current.teamId);
            ui.SetTransfer(current.offers, current.pendingTeamId, team != null ? team.displayName : current.teamId);
            RefreshRebirthUI();
        }

        void Create(string rawName, Position position, Trait trait)
        {
            if (blocked || current != null) return;
            string name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > settings.maxNameLength)
            { ui.SetStatus("Enter a name (1-" + settings.maxNameLength + " characters)."); return; }
            foreach (char c in name) if (char.IsControl(c)) { ui.SetStatus("Name contains invalid characters."); return; }
            var candidate = new SaveData { player = settings.CreatePlayer(name, position, trait) };
            SeasonSystem.Prepare(candidate, matchSettings);
            if (!saves.TrySave(candidate, out string message)) { ui.SetStatus(message); return; }
            current = candidate;
            StartPlayer(message, false);
        }

        void SelectTraining(string id)
        {
            if (blocked || training == null) return;
            training.Select(id);
            saves.TrySave(current, out string message);
            var def = training.Current;
            ui.SetStatus(def == null ? message : "Training: " + def.displayName + " (+" + (def.growthPerHour * RebirthSystem.TrainingMultiplier(current)).ToString("0.#") + "/h). " + message);
            ui.RefreshTraining(current.player, training);
        }

        void StopTraining()
        {
            if (blocked || training == null) return;
            training.Stop();
            saves.TrySave(current, out string message);
            ui.SetStatus("Training stopped. " + message);
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

        void ChooseOffer(string teamId)
        {
            if (blocked || current == null) return;
            var offer = SeasonSystem.SetPending(current, teamId);
            if (offer == null) return;
            saves.TrySave(current, out string message);
            RefreshSeasonUI();
            ui.SetStatus("Transfer agreed with " + offer.teamName + ". It starts next season. " + message);
        }

        void StayWithTeam()
        {
            if (blocked || current == null) return;
            current.pendingTeamId = "";
            saves.TrySave(current, out string message);
            RefreshSeasonUI();
            ui.SetStatus("You will stay with your current team. " + message);
        }

        void PlayMatch()
        {
            if (blocked || current == null || ui.IsMatchPlaying) return;

            if (SeasonSystem.IsComplete(current))
            {
                string transferMessage = SeasonSystem.StartNextSeason(current, matchSettings);
                saves.TrySave(current, out string newSeasonMessage);
                RefreshSeasonUI();
                ui.SetStatus("New season started. " + newSeasonMessage);
                if (transferMessage != null)
                    ui.ShowPopup("TRANSFER COMPLETE", transferMessage + "\nSeason " + current.season.seasonNumber + " starts with your new team.", 150f);
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
                    afterResult = () => ui.ShowPopup("SEASON COMPLETE", SeasonSystem.DescribeSeasonEnd(current, matchSettings), 260f);
                ui.ShowPopup("MATCH RESULT", SeasonSystem.DescribeResult(record, current, matchSettings), 260f, afterResult);
            });
        }

        void Update()
        {
            if (blocked || training == null || current == null) return;

            if (Application.isFocused && !ui.IsMatchPlaying) training.Tick(Time.unscaledDeltaTime);   // 경기 영상 중에는 훈련 중지

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= 30f) { autoSaveTimer = 0f; saves.TrySave(current, out _); }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.2f) { refreshTimer = 0f; ui.RefreshTraining(current.player, training); RefreshRebirthUI(); }
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
