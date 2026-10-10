using System;
using System.IO;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // Unknown versions and corrupt saves are never silently overwritten.
    public class CareerSaveSystem
    {
        public string FilePath => Path.Combine(Application.persistentDataPath, "football-career-v1.json");
        static bool Valid(SaveData data)
        {
            if (data == null || data.version != 1 || data.player == null) return false;
            var p = data.player;
            if (string.IsNullOrWhiteSpace(p.playerName) ||
                !Enum.IsDefined(typeof(Position), p.position) || !Enum.IsDefined(typeof(Trait), p.trait)) return false;
            foreach (float value in new[] {p.shooting,p.passing,p.dribbling,p.defending,p.stamina})
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) return false;
            return true;
        }
        public bool TryLoad(out SaveData data, out string message)
        {
            data = null; message = "";
            try
            {
                if (!File.Exists(FilePath))
                {
                    if (File.Exists(FilePath + ".bak"))
                    {
                        data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath + ".bak"));
                        if (!Valid(data)) throw new InvalidDataException("Backup is invalid.");
                        message = "Backup loaded. Press Save to restore the main file.";
                    }
                    return true;
                }
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data != null && data.version != 1)
                    throw new InvalidDataException("Unsupported save version. Original file preserved.");
                if (!Valid(data)) throw new InvalidDataException("Invalid save. Original file preserved.");
                return true;
            }
            catch (Exception e) { data = null; message = "Cannot load: " + e.Message; return false; }
        }
        public bool TrySave(SaveData data, out string message)
        {
            try
            {
                if (!Valid(data)) throw new InvalidDataException("Invalid player data.");
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
                message = "Saved locally.";
                return true;
            }
            catch (Exception e) { message = "Save failed: " + e.Message; return false; }
        }
    }
}
