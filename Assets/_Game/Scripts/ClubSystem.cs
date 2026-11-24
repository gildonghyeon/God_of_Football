using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 은퇴한 선수 (내가 키운 선수 목록에 남음)
    [Serializable] public class RetiredPlayer
    {
        public string name;
        public Position position;
        public Trait trait;
        public int hairColor, skinTone, bootColor, kitColor;
        public float shooting, passing, dribbling, defending, stamina;
        public float rating;            // 은퇴할 때 포지션 평점 (0~100)
        public int seasons, goals, assists, apps, trophies, age, careerNumber;
        public string lastTeam = "";    // 마지막 소속 팀
        public List<SeasonSummary> history = new List<SeasonSummary>();   // 시즌별 기록 (오래된 시즌부터)
    }

    // 한 선수의 커리어 기록 (은퇴 확인 창, 내가 키운 선수 상세 창)
    public class CareerRecordView
    {
        public string name, position, trait, team;
        public float rating;
        public int careerNumber, seasons, apps, goals, assists;
        public float avgRating;                 // 시즌 평균 평점의 출전 수 가중 평균 (0이면 없음)
        public string best = "";             // 최고 성적 (예: 2부 1위)
        public List<SeasonSummary> history = new List<SeasonSummary>();   // 오래된 시즌부터
        public List<KeyValuePair<TrophyKind, int>> trophies = new List<KeyValuePair<TrophyKind, int>>();
        public int trophyTotal;
        public List<string> teams = new List<string>();   // 거쳐 간 팀 (뛴 순서)
        public bool hasLook; public int hair, skin, kit, boots;   // 선수 모습 (은퇴할 때의 외형)
    }

    // 나만의 팀 (은퇴 선수를 모아 창단)
    [Serializable] public class MyClubData
    {
        public string name = "";
        public int kit;                                     // TeamKits.ClubDesigns 번호
        public Formation formation = Formation.F433;
        public TacticStyle style = TacticStyle.Possession;
        public bool active;                                 // 리그에 참가 중인지 (창단한 다음 시즌부터)
        public string replacedTeamId = "";                  // 이 팀 대신 리그에서 빠진 팀
    }

    // 은퇴와 나만의 팀 모드.
    //  은퇴(선수 키우기 모드): 시즌이 끝나면 지금 선수를 은퇴시킬 수 있습니다. 은퇴한 선수는 '내가 키운 선수' 목록에 남고, 새 선수로 커리어를 시작합니다.
    //  나만의 팀 모드: 선수 키우기와 따로 돌아가는 모드입니다. 은퇴 선수 FoundLegends명으로 팀을 창단하면(나머지 자리는 새 선수)
    //        자기만의 리그(따로 저장)에서 가장 아래 부의 가장 약한 팀 자리를 넘겨받아 시즌을 치릅니다. 내가 키운 선수들이 직접 뜁니다.
    //        그 뒤에 은퇴한 선수는 나만의 팀 모드에서 무료로 영입할 수 있습니다.
    //  나만의 팀 모드의 저장(club)은 SaveData를 그대로 쓰되 teamMode가 켜져 있고, legends는 영입한 선수, player는 이름만 있는 자리표시입니다.
    public static class ClubSystem
    {
        public const string ClubId = "my-club";
        public const int FoundLegends = 5;

        public static int LegendCount(SaveData s) { return s != null && s.legends != null ? s.legends.Count : 0; }
        public static bool CanFound(SaveData career) { return career != null && !career.hasClub && LegendCount(career) >= FoundLegends; }

        // 몇 번째 커리어인지 (환생과 은퇴 모두 커리어를 끝냄)
        public static int CareerNumber(SaveData s) { return s.rebirthCount + s.retireCount + 1; }

        // ---------- 은퇴 ----------
        public static bool CanRetire(SaveData s) { return s != null && SeasonSystem.IsComplete(s); }

        // 지금 선수를 은퇴 선수로 남기고, newPlayer로 새 커리어를 시작합니다. 은퇴한 선수를 돌려줍니다.
        public static RetiredPlayer Retire(SaveData save, MatchSettings ms, CareerSettings career, PlayerData newPlayer)
        {
            var done = new List<SeasonSummary>(save.seasonHistory);
            if (SeasonSystem.IsComplete(save)) done.Add(SeasonSystem.Summarize(save, ms));
            int careerNo = CareerNumber(save);
            var p = save.player;
            var lg = new RetiredPlayer
            {
                name = p.playerName, position = p.position, trait = p.trait,
                hairColor = p.hairColor, skinTone = p.skinTone, bootColor = p.bootColor, kitColor = p.kitColor,
                shooting = p.shooting, passing = p.passing, dribbling = p.dribbling, defending = p.defending, stamina = p.stamina,
                rating = SeasonSystem.PlayerRating(save, ms, career), careerNumber = careerNo,
                lastTeam = SeasonSystem.TeamName(save, ms), seasons = done.Count, age = 18 + done.Count,
                history = done
            };
            var summary = new CareerSummary { careerNumber = careerNo, seasons = done.Count, teamName = lg.lastTeam, bonusGainedPercent = 0f };
            foreach (var s in done)
            {
                lg.goals += s.playerGoals; lg.assists += s.playerAssists; lg.apps += s.appearances;
                summary.goals += s.playerGoals; summary.assists += s.playerAssists;
                if (s.finalPosition <= 0) continue;
                int div = s.division > 0 ? s.division : int.MaxValue;
                int bestDiv = summary.bestDivision > 0 ? summary.bestDivision : int.MaxValue;
                if (summary.bestPosition == 0 || div < bestDiv || (div == bestDiv && s.finalPosition < summary.bestPosition))
                { summary.bestDivision = s.division; summary.bestPosition = s.finalPosition; }
            }
            if (save.trophies != null) foreach (var t in save.trophies) if (t.career == careerNo) lg.trophies++;
            if (save.legends == null) save.legends = new List<RetiredPlayer>();
            save.legends.Add(lg);
            if (save.careers == null) save.careers = new List<CareerSummary>();
            save.careers.Add(summary);
            save.retireCount++;

            // 새 커리어: 선수·시즌·돈·시설은 처음부터, 리그 상황과 환생 보너스·트로피·내가 키운 선수는 그대로
            save.player = newPlayer;
            save.trainingId = ""; save.trainingCarrySeconds = 0f;
            save.teamId = "";
            save.season = new SeasonData();
            save.seasonHistory.Clear();
            save.offers.Clear(); save.offersSeason = 0; save.offersScore = -1;
            save.awardsSeason = 0; save.awardLines = new List<string>();
            save.pendingTeamId = ""; save.transferDecided = false;
            save.transfers.Clear();
            save.leagueSeason = 0;
            save.money = 0; save.effortPoints = 0f; save.effortRateLevel = 0;
            save.facilityLevels = new List<int>(); save.failStacks = new List<int>();
            save.lastUpgraded = -1; save.weeklyWage = 0;
            return lg;
        }

        // ---------- 커리어 기록 ----------
        // 지금 뛰는 선수의 기록 (끝난 시즌 + 이번 시즌이 끝났으면 이번 시즌)
        public static CareerRecordView CurrentRecord(SaveData save, MatchSettings ms, CareerSettings career)
        {
            var done = new List<SeasonSummary>(save.seasonHistory);
            if (SeasonSystem.IsComplete(save)) done.Add(SeasonSystem.Summarize(save, ms));
            var p = save.player;
            var v = Record(p.playerName, p.position, p.trait, SeasonSystem.PlayerRating(save, ms, career), CareerNumber(save), SeasonSystem.TeamName(save, ms), done, save);
            v.hasLook = true; v.hair = p.hairColor; v.skin = p.skinTone; v.kit = p.kitColor; v.boots = p.bootColor;
            return v;
        }

        public static CareerRecordView LegendRecord(SaveData save, RetiredPlayer lg)
        {
            var v = Record(lg.name, lg.position, lg.trait, lg.rating, lg.careerNumber, lg.lastTeam, lg.history ?? new List<SeasonSummary>(), save);
            if (v.history.Count == 0) { v.seasons = lg.seasons; v.goals = lg.goals; v.assists = lg.assists; v.apps = lg.apps; }   // 시즌 기록이 없는 예전 데이터
            v.hasLook = true; v.hair = lg.hairColor; v.skin = lg.skinTone; v.kit = lg.kitColor; v.boots = lg.bootColor;   // 은퇴할 때의 모습
            return v;
        }

        static CareerRecordView Record(string name, Position pos, Trait trait, float rating, int careerNo, string team, List<SeasonSummary> done, SaveData save)
        {
            var v = new CareerRecordView { name = name, position = Ko.Pos(pos), trait = Ko.TraitName(trait), rating = rating, careerNumber = careerNo, team = team, seasons = done.Count };
            v.history.AddRange(done);
            float ratingSum = 0f; int bestDiv = int.MaxValue, bestPos = 0;
            foreach (var s in done)
            {
                v.goals += s.playerGoals; v.assists += s.playerAssists; v.apps += s.appearances;
                ratingSum += s.averageRating * s.appearances;
                if (!string.IsNullOrEmpty(s.teamName) && (v.teams.Count == 0 || v.teams[v.teams.Count - 1] != s.teamName)) v.teams.Add(s.teamName);
                if (s.finalPosition <= 0) continue;
                int div = s.division > 0 ? s.division : int.MaxValue;
                if (bestPos == 0 || div < bestDiv || (div == bestDiv && s.finalPosition < bestPos)) { bestDiv = div; bestPos = s.finalPosition; }
            }
            if (v.teams.Count == 0 && !string.IsNullOrEmpty(team)) v.teams.Add(team);
            v.avgRating = v.apps > 0 ? ratingSum / v.apps : 0f;
            if (bestPos > 0) v.best = (bestDiv != int.MaxValue ? bestDiv + "부 " : "") + bestPos + "위";
            var counts = new Dictionary<TrophyKind, int>();
            if (save.trophies != null)
                foreach (var t in save.trophies)
                    if (t.career == careerNo) { counts.TryGetValue(t.kind, out int n); counts[t.kind] = n + 1; v.trophyTotal++; }
            foreach (TrophyKind k in Enum.GetValues(typeof(TrophyKind)))
                if (counts.TryGetValue(k, out int n)) v.trophies.Add(new KeyValuePair<TrophyKind, int>(k, n));
            return v;
        }

        // ---------- 예전 저장 정리 ----------
        // 이전 버전에서는 나만의 팀이 선수 키우기 리그에 들어갔습니다. 선수 키우기 리그를 원래대로 돌립니다 (빠졌던 팀 복귀, 내 팀이었다면 시작 팀으로).
        public static bool CleanCareer(SaveData career)
        {
            bool changed = false;
            if (career.droppedTeams != null && career.droppedTeams.Count > 0) { career.droppedTeams.Clear(); changed = true; }
            if (career.myClub != null && career.myClub.active && !career.teamMode) { career.myClub.active = false; changed = true; }
            if (career.teamId == ClubId) { career.teamId = ""; changed = true; }
            if (career.pendingTeamId == ClubId) { career.pendingTeamId = ""; career.transferDecided = false; changed = true; }
            if (career.offers != null && career.offers.RemoveAll(o => o.teamId == ClubId) > 0) changed = true;
            if (career.squads != null && career.squads.RemoveAll(s => s.teamId == ClubId) > 0) changed = true;
            if (career.league != null && career.league.RemoveAll(e => e.teamId == ClubId) > 0) changed = true;
            return changed;
        }

        // ---------- 나만의 팀 모드 ----------
        // 나만의 팀 리그의 설정: 원래 팀 - 자리를 넘긴 팀 + 나만의 팀. baseTeams는 선수 키우기 설정의 팀 목록 (바뀌지 않음)
        public static void ApplyRoster(SaveData club, MatchSettings clubMs, TeamDefinition[] baseTeams)
        {
            var list = new List<TeamDefinition>();
            foreach (var t in baseTeams)
                if (t != null && t.id != ClubId && (club.droppedTeams == null || !club.droppedTeams.Contains(t.id))) list.Add(CopyTeam(t));
            list.Add(ClubTeam(club, clubMs));
            clubMs.teams = list.ToArray();
            TeamKits.SetClubKit(club.myClub.kit);
            TacticSystem.Forget(ClubId);
            TacticSystem.SetClubTactic(new TeamTactic { formation = club.myClub.formation, style = club.myClub.style });
        }

        static TeamDefinition CopyTeam(TeamDefinition t)
        {
            return new TeamDefinition { id = t.id, displayName = t.displayName, division = t.division, strength = t.strength, requiredRating = t.requiredRating, focus = t.focus };
        }

        // 창단: 은퇴 선수 중 처음 FoundLegends명 + 새 선수로 팀을 만들고, 나만의 팀 리그를 시작합니다.
        public static SaveData Found(SaveData career, MatchSettings clubMs, TeamDefinition[] baseTeams, string name, int kit, Formation formation, TacticStyle style)
        {
            var club = new SaveData
            {
                teamMode = true, hasClub = true, teamId = ClubId,
                player = new PlayerData { playerName = name, position = Position.Midfielder, trait = Trait.Playmaker, shooting = 1, passing = 1, dribbling = 1, defending = 1, stamina = 1 },
                myClub = new MyClubData { name = name, kit = Mathf.Clamp(kit, 0, TeamKits.ClubDesignCount - 1), formation = formation, style = style, active = true }
            };
            for (int i = 0; i < FoundLegends && i < career.legends.Count; i++) club.legends.Add(Copy(career.legends[i]));
            // 가장 아래 부에서 가장 약한 팀이 빠지고 그 자리를 넘겨받음
            int bottom = 1; foreach (var t in baseTeams) if (t != null) bottom = Mathf.Max(bottom, t.division);
            TeamDefinition weakest = null;
            foreach (var t in baseTeams)
                if (t != null && t.division == bottom && (weakest == null || t.strength < weakest.strength)) weakest = t;
            if (weakest != null) { club.droppedTeams.Add(weakest.id); club.myClub.replacedTeamId = weakest.id; }
            SyncSquad(club);
            ApplyRoster(club, clubMs, baseTeams);
            SeasonSystem.Prepare(club, clubMs);
            career.hasClub = true;
            career.myClub = club.myClub;
            return club;
        }

        // 아직 나만의 팀에 오지 않은 은퇴 선수 (무료 영입 후보)
        public static List<RetiredPlayer> FreeAgents(SaveData career, SaveData club)
        {
            var list = new List<RetiredPlayer>();
            if (career == null || career.legends == null || club == null) return list;
            foreach (var lg in career.legends)
                if (!club.legends.Exists(x => x.careerNumber == lg.careerNumber)) list.Add(lg);
            return list;
        }

        // 무료 영입: 선수단에 들어가고 팀 전력이 다시 계산됩니다.
        public static bool Sign(SaveData career, SaveData club, MatchSettings clubMs, TeamDefinition[] baseTeams, int careerNumber)
        {
            var lg = career.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null || club.legends.Exists(x => x.careerNumber == careerNumber)) return false;
            club.legends.Add(Copy(lg));
            SyncSquad(club);
            ApplyRoster(club, clubMs, baseTeams);
            return true;
        }

        // 나만의 팀으로 옮길 때 복사 (선수 키우기 쪽 기록은 그대로)
        static RetiredPlayer Copy(RetiredPlayer lg)
        {
            return new RetiredPlayer
            {
                name = lg.name, position = lg.position, trait = lg.trait, hairColor = lg.hairColor, skinTone = lg.skinTone, bootColor = lg.bootColor, kitColor = lg.kitColor,
                shooting = lg.shooting, passing = lg.passing, dribbling = lg.dribbling, defending = lg.defending, stamina = lg.stamina, rating = lg.rating,
                seasons = lg.seasons, goals = lg.goals, assists = lg.assists, apps = lg.apps, trophies = lg.trophies, age = lg.age, careerNumber = lg.careerNumber,
                lastTeam = lg.lastTeam, history = new List<SeasonSummary>(lg.history ?? new List<SeasonSummary>())
            };
        }

        // 나만의 팀의 팀 정보: 선수단 주전(4-4-2) 평균으로 전력과 요구 평점을 정합니다.
        public static TeamDefinition ClubTeam(SaveData club, MatchSettings ms)
        {
            float level = TeamLevel(club);
            return new TeamDefinition
            {
                id = ClubId, displayName = club.myClub != null ? club.myClub.name : "My Club", division = ms != null ? ms.DivisionCount : 5,
                strength = Mathf.Clamp(25f + level * 0.7f, 25f, 95f), requiredRating = Mathf.Clamp(level * 0.9f, 2f, 96f),
                focus = AttributeKind.Passing
            };
        }

        // 주전(GK 1, DF 4, MF 4, FW 2)의 평균 평점
        public static float TeamLevel(SaveData club)
        {
            var squad = SquadSystem.Find(club, ClubId);
            if (squad == null) { SyncSquad(club); squad = SquadSystem.Find(club, ClubId); }
            if (squad == null) return 20f;
            int[] need = { 1, 4, 4, 2 };
            float sum = 0f; int n = 0;
            for (int r = 0; r < 4; r++)
            {
                var list = squad.players.FindAll(p => (int)p.role == r);
                list.Sort((a, b) => b.rating.CompareTo(a.rating));
                for (int k = 0; k < need[r] && k < list.Count; k++) { sum += list[k].rating; n++; }
            }
            return n > 0 ? sum / n : 20f;
        }

        public static SquadRole RoleOf(Position p)
        {
            return p == Position.Forward ? SquadRole.FW : p == Position.Midfielder ? SquadRole.MF : p == Position.Goalkeeper ? SquadRole.GK : SquadRole.DF;
        }

        // 나만의 팀 선수단: 영입한 은퇴 선수(포지션 그대로)로 자리를 채우고, 남은 자리는 새 선수. 은퇴 선수는 나이를 먹지 않습니다.
        // 새 선수는 처음 만든 그대로 두고(이름이 바뀌지 않게), 은퇴 선수가 들어오면 같은 포지션의 새 선수 자리를 넘겨받습니다.
        public static void SyncSquad(SaveData club)
        {
            if (club == null || !club.hasClub) return;
            var squad = SquadSystem.Find(club, ClubId);
            if (squad == null) { squad = new TeamSquad { teamId = ClubId }; club.squads.Add(squad); }
            var prospects = squad.players.FindAll(p => p.id <= -2000);
            if (prospects.Count == 0)
            {
                var used = SquadSystem.UsedNamesExcept(club, ClubId);
                var rng = new System.Random(0x6c75b ^ (club.myClub != null && club.myClub.name != null ? club.myClub.name.Length * 977 : 0));
                int n = 0;
                foreach (var role in SquadSystem.SquadShape)
                {
                    var p = SquadSystem.MakePlayer(club, rng, used, role, rng.Next(18, 22), 22f + rng.Next(0, 9));
                    p.id = -2000 - n++;
                    prospects.Add(p);
                }
            }
            squad.players.Clear();
            var byRole = new List<RetiredPlayer>[4];
            for (int r = 0; r < 4; r++) byRole[r] = new List<RetiredPlayer>();
            foreach (var lg in club.legends) byRole[(int)RoleOf(lg.position)].Add(lg);
            for (int r = 0; r < 4; r++) byRole[r].Sort((a, b) => b.rating.CompareTo(a.rating));
            int[] taken = new int[4];
            foreach (var role in SquadSystem.SquadShape)
            {
                int r = (int)role;
                if (taken[r] < byRole[r].Count) { squad.players.Add(LegendPlayer(byRole[r][taken[r]], role)); taken[r]++; }
            }
            // 남은 자리는 새 선수 (포지션 묶음마다 정원 SquadShape만큼)
            int[] room = new int[4];
            foreach (var role in SquadSystem.SquadShape) room[(int)role]++;
            foreach (var p in prospects)
            {
                int r = (int)p.role;
                if (taken[r] < room[r]) { squad.players.Add(p); taken[r]++; }
                else squad.players.Add(p);   // 은퇴 선수에게 자리를 내준 새 선수도 교체 명단에 남음
            }
            for (int r = 0; r < 4; r++)   // 정원보다 많은 은퇴 선수
                for (int k = 0; k < byRole[r].Count; k++)
                {
                    var lg = byRole[r][k];
                    if (!squad.players.Exists(x => x.id == -1000 - lg.careerNumber)) squad.players.Add(LegendPlayer(lg, (SquadRole)r));
                }
        }

        static SquadPlayer LegendPlayer(RetiredPlayer lg, SquadRole role)
        {
            return new SquadPlayer { id = -1000 - lg.careerNumber, name = lg.name, role = role, age = lg.age, rating = Mathf.Clamp(lg.rating, 1f, 99f) };
        }
    }
}
