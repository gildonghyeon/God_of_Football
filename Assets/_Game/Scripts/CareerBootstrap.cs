using UnityEngine;
namespace MyFootballCareer
{
    public class CareerBootstrap : MonoBehaviour
    {
        [SerializeField] CareerSettings settings;
        [SerializeField] TrainingSettings trainingSettings;
        [Tooltip("Optional font with Korean glyphs. Blank uses the built-in prototype font.")]
        [SerializeField] Font uiFont;
        CareerSaveSystem saves;
        SaveData current;
        CareerPrototypeUI ui;
        TrainingSystem training;
        bool blocked;
        bool temporarySettings;
        bool temporaryTraining;
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
            saves = new CareerSaveSystem();
            ui = gameObject.AddComponent<CareerPrototypeUI>();
            ui.Initialize(uiFont, settings, trainingSettings, Create, Save, SelectTraining, StopTraining);
            blocked = !saves.TryLoad(out current, out string message);
            if (blocked) ui.ShowError(message);
            else if (current != null) StartPlayer(message, true);
            else ui.ShowCreation();
        }

        void StartPlayer(string message, bool applyOffline)
        {
            training = new TrainingSystem(current, settings, trainingSettings);
            training.ValidateSelection();
            OfflineResult offline = null;
            if (applyOffline)
            {
                offline = IdleRewardSystem.Apply(current, settings, trainingSettings, System.DateTime.UtcNow.Ticks);
                if (!saves.TrySave(current, out string saveMessage)) message = saveMessage;   // 보상과 새 기준 시각을 바로 저장
                else if (offline.ClockRewound) message = "System clock moved backwards. No offline reward.";
            }
            ui.ShowPlayer(current.player, message);
            ui.RefreshTraining(current.player, training);
            ShowOffline(offline);
        }

        void ShowOffline(OfflineResult offline)
        {
            if (offline == null || !offline.Granted) return;
            ui.ShowOfflineSummary("WELCOME BACK", IdleRewardSystem.Describe(offline));
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

        void Create(string rawName, Position position, Trait trait)
        {
            if (blocked || current != null) return;
            string name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > settings.maxNameLength)
            { ui.SetStatus("Enter a name (1-" + settings.maxNameLength + " characters)."); return; }
            foreach (char c in name) if (char.IsControl(c)) { ui.SetStatus("Name contains invalid characters."); return; }
            var candidate = new SaveData { player = settings.CreatePlayer(name, position, trait) };
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
            ui.SetStatus(def == null ? message : "Training: " + def.displayName + " (+" + def.growthPerHour.ToString("0.#") + "/h). " + message);
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

        void Update()
        {
            if (blocked || training == null || current == null) return;

            if (Application.isFocused) training.Tick(Time.unscaledDeltaTime);

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= 30f) { autoSaveTimer = 0f; saves.TrySave(current, out _); }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.2f) { refreshTimer = 0f; ui.RefreshTraining(current.player, training); }
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
        }
    }
}
