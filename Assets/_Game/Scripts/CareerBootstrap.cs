using UnityEngine;
namespace MyFootballCareer
{
    public class CareerBootstrap : MonoBehaviour
    {
        [SerializeField] CareerSettings settings;
        [Tooltip("Optional font with Korean glyphs. Blank uses the built-in prototype font.")]
        [SerializeField] Font uiFont;
        CareerSaveSystem saves;
        SaveData current;
        CareerPrototypeUI ui;
        bool blocked;
        bool temporarySettings;
        void Start()
        {
            if (settings == null) { settings = ScriptableObject.CreateInstance<CareerSettings>(); temporarySettings = true; }
            saves = new CareerSaveSystem();
            ui = gameObject.AddComponent<CareerPrototypeUI>();
            ui.Initialize(uiFont, settings, Create, Save);
            blocked = !saves.TryLoad(out current, out string message);
            if (blocked) ui.ShowError(message);
            else if (current != null) ui.ShowPlayer(current.player, message);
            else ui.ShowCreation();
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
            ui.ShowPlayer(current.player, message);
        }
        void Save()
        {
            if (blocked || current == null) return;
            saves.TrySave(current, out string message);
            if (ui != null) ui.SetStatus(message);
        }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
        void OnDestroy() { if (temporarySettings && settings != null) Destroy(settings); }
    }
}
