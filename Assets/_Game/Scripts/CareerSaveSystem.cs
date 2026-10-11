using System;
using System.IO;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // Unknown versions and corrupt saves are never silently overwritten.
    public class CareerSaveSystem
    {
        const int CurrentVersion = 3;
        public string FilePath => Path.Combine(Application.persistentDataPath, "football-career-v1.json");

        static bool Valid(SaveData data)
        {
            if (data == null || data.version != CurrentVersion || data.player == null) return false;
            var p = data.player;
            if (string.IsNullOrWhiteSpace(p.playerName) ||
                !Enum.IsDefined(typeof(Position), p.position) || !Enum.IsDefined(typeof(Trait), p.trait)) return false;
            foreach (float value in new[] {p.shooting,p.passing,p.dribbling,p.defending,p.stamina})
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) return false;
            if (data.trainingId == null) return false;
            if (float.IsNaN(data.trainingCarrySeconds) || float.IsInfinity(data.trainingCarrySeconds) || data.trainingCarrySeconds < 0) return false;
            if (data.lastSeenUtcTicks < 0) return false;
            return true;
        }

        // Returns true when an older save was upgraded to the current version.
        static bool Migrate(SaveData data)
        {
            if (data == null) return false;
            bool upgraded = false;
            if (data.version == 1)
            {
                data.version = 2;
                data.trainingId = "";
                data.trainingCarrySeconds = 0f;
                upgraded = true;
            }
            if (data.version == 2)
            {
                data.version = 3;
                data.lastSeenUtcTicks = 0;   // 처음 업그레이드할 때는 오프라인 보상 없음
                upgraded = true;
            }
            if (data.trainingId == null) data.trainingId = "";
            return upgraded;
        }

        static SaveData Parse(string json, out bool upgraded)
        {
            upgraded = false;
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data != null && data.version > CurrentVersion)
                throw new InvalidDataException("Unsupported save version. Original file preserved.");
            upgraded = Migrate(data);
            return data;
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
                        data = Parse(File.ReadAllText(FilePath + ".bak"), out _);
                        if (!Valid(data)) throw new InvalidDataException("Backup is invalid.");
                        message = "Backup loaded. Press Save to restore the main file.";
                    }
                    return true;
                }
                data = Parse(File.ReadAllText(FilePath), out bool upgraded);
                if (!Valid(data)) throw new InvalidDataException("Invalid save. Original file preserved.");
                if (upgraded) message = "Save upgraded to version 3.";
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
                data.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
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
