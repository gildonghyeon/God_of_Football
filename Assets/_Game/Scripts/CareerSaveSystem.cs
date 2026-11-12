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
        const int CurrentVersion = 18;
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
            if (data.version == 7)
            {
                data.version = 8;   // 선수 외형(머리색·피부색) 추가. 기존 선수는 원래 모습으로 시작
                upgraded = true;
            }
            if (data.version == 8)
            {
                data.version = 9;   // 유니폼 색 추가. 기존 선수는 원래 파란 유니폼으로 시작
                upgraded = true;
            }
            if (data.version == 9)
            {
                // 5부 리그·승강제 추가. 진행 중인 시즌은 원래 6팀으로 끝까지 치르고(division 0), 다음 시즌부터 부에 들어갑니다.
                data.version = 10;
                if (data.season != null && data.season.matches != null && data.season.matches.Count > 0)
                {
                    var ids = new List<string>();
                    if (!string.IsNullOrEmpty(data.teamId)) ids.Add(data.teamId);
                    foreach (var m in data.season.matches)
                        if (m != null && !string.IsNullOrEmpty(m.opponentId) && !ids.Contains(m.opponentId)) ids.Add(m.opponentId);
                    data.season.teamIds = ids;
                    data.season.division = 0;
                }
                upgraded = true;
            }
            if (data.version == 10)
            {
                data.version = 11;   // 돈·주급·동시 훈련 추가. 주급은 불러올 때 지금 팀과 능력치로 정해집니다.
                upgraded = true;
            }
            if (data.version == 11)
            {
                // 훈련 → 노력 포인트로 바뀜. 동시 훈련은 없어져서 산 레벨만큼 돈을 돌려줍니다.
                data.version = 12;
                long[] paid = { 30000, 150000, 500000, 1500000 };
                for (int i = 0; i < data.multiTrainingLevel && i < paid.Length; i++) data.money += paid[i];
                data.multiTrainingLevel = 0;
                if (data.extraTrainingIds != null) data.extraTrainingIds.Clear();
                data.trainingId = "";
                data.trainingCarrySeconds = 0f;
                data.effortPoints = 0f;
                data.lastUpgraded = -1;
                upgraded = true;
            }
            if (data.version == 12)
            {
                // 노력 포인트 단위가 커짐(시간당 7 → 초당 1, 강화 비용 약 230배). 모아 둔 포인트도 같은 비율로 바꿉니다.
                data.version = 13;
                data.effortPoints *= 230f;
                data.effortRateLevel = 0;
                data.trainingCarrySeconds = 0f;
                upgraded = true;
            }
            if (data.version == 13)
            {
                data.version = 14;   // 리그 보너스·승리 보너스 추가 (바로 받을 수 있는 상태로 시작)
                data.lastWinBonusTicks = 0;
                upgraded = true;
            }
            if (data.version == 14)
            {
                data.version = 15;   // 승리 보너스 → 친선 경기 보상 (바로 친선 경기를 할 수 있는 상태로 시작)
                data.friendlyCount = 0;
                data.friendliesPlayed = 0;
                data.lastFriendlyTicks = 0;
                upgraded = true;
            }
            if (data.version == 15)
            {
                data.version = 16;   // 시설 추가 (모두 Lv0에서 시작)
                data.facilityLevels = new List<int>();
                upgraded = true;
            }
            if (data.version == 16)
            {
                data.version = 17;   // 강화 천장 추가 (실패 스택 0에서 시작)
                data.failStacks = new List<int>();
                upgraded = true;
            }
            if (data.version == 17)
            {
                data.version = 18;   // 시즌 시상과 트로피 추가 (이미 끝난 시즌은 시상하지 않음)
                data.trophies = new List<TrophyRecord>();
                data.awardLines = new List<string>();
                if (data.season != null && data.season.matches != null && data.season.matches.Count > 0
                    && data.season.nextMatchIndex >= data.season.matches.Count) data.awardsSeason = data.season.seasonNumber;
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
            if (data.league == null) data.league = new List<TeamDivision>();
            if (data.extraTrainingIds == null) data.extraTrainingIds = new List<string>();
            if (data.facilityLevels == null) data.facilityLevels = new List<int>();
            if (data.failStacks == null) data.failStacks = new List<int>();
            if (data.trophies == null) data.trophies = new List<TrophyRecord>();
            if (data.awardLines == null) data.awardLines = new List<string>();
            if (data.season != null && data.season.topTeamIds == null) data.season.topTeamIds = new List<string>();
            if (data.money < 0) data.money = 0;
            if (data.multiTrainingLevel < 0) data.multiTrainingLevel = 0;
            if (float.IsNaN(data.effortPoints) || float.IsInfinity(data.effortPoints) || data.effortPoints < 0f) data.effortPoints = 0f;
            if (data.lastUpgraded < -1 || data.lastUpgraded > 4) data.lastUpgraded = -1;
            if (data.effortRateLevel < 0) data.effortRateLevel = 0;
            if (data.season.teamIds == null) data.season.teamIds = new List<string>();
            if (data.season.fixtures == null) data.season.fixtures = new List<LeagueGame>();
            if (data.player != null)
            {
                data.player.hairColor = Mathf.Clamp(data.player.hairColor, 0, PlayerLook.HairCount - 1);
                data.player.skinTone = Mathf.Clamp(data.player.skinTone, 0, PlayerLook.SkinCount - 1);
                data.player.kitColor = Mathf.Clamp(data.player.kitColor, 0, PlayerLook.KitCount - 1);
            }
            foreach (var match in data.season.matches)
                if (match != null && match.others == null) match.others = new List<LeagueGame>();
            return upgraded;
        }

        static SaveData Parse(string json, out bool upgraded)
        {
            upgraded = false;
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data != null && data.version > CurrentVersion)
                throw new InvalidDataException("지원하지 않는 저장 버전입니다. 원본 파일은 그대로 둡니다.");
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
                        if (!Valid(data)) throw new InvalidDataException("백업 파일이 올바르지 않습니다.");
                        message = "백업을 불러왔습니다. 다음 자동 저장 때 기본 파일이 복구됩니다.";
                    }
                    return true;
                }
                data = Parse(File.ReadAllText(FilePath), out bool upgraded);
                if (!Valid(data)) throw new InvalidDataException("저장 파일이 올바르지 않습니다. 원본 파일은 그대로 둡니다.");
                if (upgraded) message = "저장 파일을 최신 버전으로 바꿨습니다.";
                return true;
            }
            catch (Exception e) { data = null; message = "불러올 수 없습니다: " + e.Message; return false; }
        }

        // ---------- 복구 도구 ----------
        public bool HasValidBackup()
        {
            try
            {
                string backup = FilePath + ".bak";
                return File.Exists(backup) && Valid(Parse(File.ReadAllText(backup), out _));
            }
            catch { return false; }
        }

        // 망가진 본 파일은 지우지 않고 .broken- 이름으로 옆에 두고, 백업을 본 파일로 복사합니다.
        public bool TryRestoreBackup(out string message)
        {
            try
            {
                if (!HasValidBackup()) { message = "쓸 수 있는 백업이 없습니다."; return false; }
                string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                if (File.Exists(FilePath)) File.Move(FilePath, FilePath + ".broken-" + stamp);
                File.Copy(FilePath + ".bak", FilePath);
                message = "백업을 복구했습니다.";
                return true;
            }
            catch (Exception e) { message = "복구 실패: " + e.Message; return false; }
        }

        // 새로 시작하기 전에 기존 저장 파일을 지우지 않고 .archived- 이름으로 옆으로 옮깁니다.
        public bool ArchiveSave(out string message)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                int moved = 0;
                foreach (string path in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
                {
                    if (!File.Exists(path)) continue;
                    File.Move(path, path + ".archived-" + stamp);
                    moved++;
                }
                message = moved > 0 ? "이전 저장을 옆으로 옮겼습니다." : "옮길 저장 파일이 없습니다.";
                return true;
            }
            catch (Exception e) { message = "이전 저장을 옮기지 못했습니다: " + e.Message; return false; }
        }

        public bool TrySave(SaveData data, out string message)
        {
            try
            {
                if (!Valid(data)) throw new InvalidDataException("선수 정보가 올바르지 않습니다.");
                Directory.CreateDirectory(Application.persistentDataPath);
                data.lastSeenUtcTicks = DateTime.UtcNow.Ticks;
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
                message = "저장했습니다.";
                return true;
            }
            catch (Exception e) { message = "저장 실패: " + e.Message; return false; }
        }
    }
}
