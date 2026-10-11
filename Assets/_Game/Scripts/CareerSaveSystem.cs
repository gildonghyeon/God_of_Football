using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // Unknown versions and corrupt saves are never silently overwritten.
    public class CareerSaveSystem
    {
        const int CurrentVersion = 7;
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
            if (data.teamId == null || data.season == null || data.season.matches == null || data.seasonHistory == null) return false;
            if (data.season.nextMatchIndex < 0 || data.season.nextMatchIndex > data.season.matches.Count) return false;
            if (data.offers == null || data.transfers == null || data.pendingTeamId == null || data.careers == null) return false;
            if (data.rebirthCount < 0 || float.IsNaN(data.rebirthBonusPercent) || float.IsInfinity(data.rebirthBonusPercent) || data.rebirthBonusPercent < 0) return false;
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
            if (data.version == 3)
            {
                data.version = 4;
                data.teamId = "";
                data.season = new SeasonData();
                data.seasonHistory = new List<SeasonSummary>();
                upgraded = true;
            }
            if (data.version == 4)
            {
                data.version = 5;   // 다른 팀 경기 결과(others) 추가. 없는 값은 비어 있는 목록으로 시작
                upgraded = true;
            }
            if (data.version == 5)
            {
                data.version = 6;   // 이적 제안·이적 기록 추가
                upgraded = true;
            }
            if (data.version == 6)
            {
                data.version = 7;   // 환생 횟수·영구 보너스·커리어 기록 추가
                upgraded = true;
            }
            if (data.trainingId == null) data.trainingId = "";
            if (data.teamId == null) data.teamId = "";
            if (data.season == null) data.season = new SeasonData();
            if (data.season.matches == null) data.season.matches = new List<MatchRecord>();
            if (data.seasonHistory == null) data.seasonHistory = new List<SeasonSummary>();
            if (data.offers == null) data.offers = new List<TransferOffer>();
            if (data.transfers == null) data.transfers = new List<TransferRecord>();
            if (data.pendingTeamId == null) data.pendingTeamId = "";
            if (data.careers == null) data.careers = new List<CareerSummary>();
            foreach (var match in data.season.matches)
                if (match != null && match.others == null) match.others = new List<LeagueGame>();
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
                if (upgraded) message = "Save upgraded to version 7.";
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
