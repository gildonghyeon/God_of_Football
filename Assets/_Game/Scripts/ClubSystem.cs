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
        public float goalkeeping;       // GK 능력
        public int number;              // 등번호 (선수 시절 번호, 0이면 없음)
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
        public bool keeper;                                       // 골키퍼 (장갑, 기록은 선방률·클린시트)
        public int saves, cleanSheets, conceded;                  // 골키퍼 통산
    }

    // 나만의 팀 선수 한 명의 이번 시즌 기록 (1군으로 뛴 경기만)
    [Serializable] public class ClubPlayerStat
    {
        public int id;
        public int apps, goals, assists, defense, saves;
        public float ratingSum;
    }

    // 나만의 팀 (은퇴 선수를 모아 창단)
    [Serializable] public class MyClubData
    {
        public string name = "";
        public int kit;                                     // TeamKits.ClubDesigns 번호
        public Formation formation = Formation.F433;
        public TacticStyle style = TacticStyle.Possession;
        public List<int> lineup = new List<int>();          // 주전 11명 (선수 번호, 포메이션 자리 순서). 나머지는 교체
        public List<ClubPlayerStat> stats = new List<ClubPlayerStat>();   // 이번 시즌 선수별 기록 (경기마다 그때의 1군으로 쌓임)
        public int statsSeason;                             // stats가 몇 시즌 기록인지
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

        // 지금 선수를 은퇴 선수로 남기고 커리어를 정리합니다. 새 선수는 나중에 StartCareer로 (그동안은 '육성 중인 선수 없음'). 은퇴한 선수를 돌려줍니다.
        public static RetiredPlayer Retire(SaveData save, MatchSettings ms, CareerSettings career)
        {
            var done = new List<SeasonSummary>(save.seasonHistory);
            if (SeasonSystem.IsComplete(save)) done.Add(SeasonSystem.Summarize(save, ms));
            int careerNo = CareerNumber(save);
            var p = save.player;
            var lg = new RetiredPlayer
            {
                name = p.playerName, position = p.position, trait = p.trait,
                hairColor = p.hairColor, skinTone = p.skinTone, bootColor = p.bootColor, kitColor = p.kitColor,
                shooting = p.shooting, passing = p.passing, dribbling = p.dribbling, defending = p.defending, stamina = p.stamina, goalkeeping = p.goalkeeping, number = p.number,
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

            // 커리어 정리: 선수·시즌·돈·시설은 처음부터, 리그 상황과 환생 보너스·트로피·내가 키운 선수는 그대로
            save.noActivePlayer = true;
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

        // 은퇴 뒤 새 선수로 다음 커리어를 시작합니다.
        public static void StartCareer(SaveData save, PlayerData newPlayer)
        {
            save.player = newPlayer;
            save.noActivePlayer = false;
            save.teamId = "";
            save.season = new SeasonData();
        }

        // 내가 키운 선수 목록의 한 줄 기록: 필드 선수는 골·도움, 골키퍼는 선방률·클린시트
        public static string LegendLine(RetiredPlayer lg)
        {
            if (lg.position != Position.Goalkeeper || lg.history == null || lg.history.Count == 0)
                return lg.seasons + "시즌 · 골 " + lg.goals + " · 도움 " + lg.assists;
            int saves = 0, conceded = 0, cs = 0;
            foreach (var s in lg.history) { saves += s.saves; conceded += s.conceded; cs += s.cleanSheets; }
            return lg.seasons + "시즌 · 선방률 " + SeasonSystem.SaveRateText(saves, conceded) + " · 클린시트 " + cs;
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
            v.keeper = p.position == Position.Goalkeeper;
            return v;
        }

        public static CareerRecordView LegendRecord(SaveData save, RetiredPlayer lg)
        {
            var v = Record(lg.name, lg.position, lg.trait, lg.rating, lg.careerNumber, lg.lastTeam, lg.history ?? new List<SeasonSummary>(), save);
            if (v.history.Count == 0) { v.seasons = lg.seasons; v.goals = lg.goals; v.assists = lg.assists; v.apps = lg.apps; }   // 시즌 기록이 없는 예전 데이터
            v.hasLook = true; v.hair = lg.hairColor; v.skin = lg.skinTone; v.kit = lg.kitColor; v.boots = lg.bootColor;   // 은퇴할 때의 모습
            v.keeper = lg.position == Position.Goalkeeper;
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
                v.saves += s.saves; v.cleanSheets += s.cleanSheets; v.conceded += s.conceded;
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
            EnsureClubSquad(club);
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

        // 나만의 팀으로 옮길 때 복사 (선수 키우기 쪽 기록은 그대로)
        static RetiredPlayer Copy(RetiredPlayer lg)
        {
            return new RetiredPlayer
            {
                name = lg.name, position = lg.position, trait = lg.trait, hairColor = lg.hairColor, skinTone = lg.skinTone, bootColor = lg.bootColor, kitColor = lg.kitColor,
                shooting = lg.shooting, passing = lg.passing, dribbling = lg.dribbling, defending = lg.defending, stamina = lg.stamina, goalkeeping = lg.goalkeeping, rating = lg.rating, number = lg.number,
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

        // ---------- 나만의 팀 선수단 (주전 11명 + 교체 8명) ----------
        public const int SquadSize = 19, Starters = 11;
        static readonly int[] RoleQuota = { 2, 6, 6, 5 };   // 새로 꾸릴 때 포지션별 인원 (GK, DF, MF, FW)

        public static SquadRole RoleOf(Position p)
        {
            return p == Position.Forward ? SquadRole.FW : p == Position.Midfielder ? SquadRole.MF : p == Position.Goalkeeper ? SquadRole.GK : SquadRole.DF;
        }

        static SquadPlayer LegendPlayer(RetiredPlayer lg)
        {
            return new SquadPlayer { id = -1000 - lg.careerNumber, name = lg.name, role = RoleOf(lg.position), age = lg.age, rating = LegendRating(lg), number = lg.number };   // 선수 시절 등번호 (겹치면 SquadSystem이 다른 번호로)
        }

        // 내가 키운 선수의 능력: 실제 세부 능력치를 자기 포지션 비중으로 평균 (모두 100이면 100)
        static float LegendRating(RetiredPlayer lg)
        {
            var a = SquadSystem.AtLeastMin(new[] { lg.shooting, lg.passing, lg.dribbling, lg.defending, lg.stamina, lg.goalkeeping });
            return Mathf.Clamp(Mathf.Round(SlotRating(a, RoleOf(lg.position))), SquadSystem.MinStat, SquadSystem.MaxStat);
        }

        public static bool IsLegend(SquadPlayer p) { return p != null && p.id <= -1000 && p.id > -2000; }

        public static TeamSquad Squad(SaveData club)
        {
            var squad = SquadSystem.Find(club, ClubId);
            if (squad == null) { squad = new TeamSquad { teamId = ClubId }; club.squads.Add(squad); }
            return squad;
        }

        // 선수단을 19명으로 맞추고(은퇴 선수는 모두 포함, 모자라면 새 선수, 넘치면 가장 약한 새 선수부터 내보냄) 라인업을 확인합니다.
        // 처음 창단할 때와 예전 저장을 불러올 때 씁니다. 이미 19명이면 그대로 둡니다.
        public static void EnsureClubSquad(SaveData club)
        {
            if (club == null || !club.hasClub) return;
            var squad = Squad(club);
            foreach (var lg in club.legends)
            {
                var have = squad.players.Find(x => x.id == -1000 - lg.careerNumber);
                if (have == null) squad.players.Add(LegendPlayer(lg));
                else have.rating = LegendRating(lg);   // 이미 있는 선수도 능력을 지금 비중으로 다시 계산
            }
            squad.players.RemoveAll(p => IsLegend(p) && !club.legends.Exists(l => -1000 - l.careerNumber == p.id));
            while (squad.players.Count > SquadSize)
            {
                SquadPlayer weakest = null;
                foreach (var p in squad.players) if (!IsLegend(p) && (weakest == null || p.rating < weakest.rating)) weakest = p;
                if (weakest == null) break;
                squad.players.Remove(weakest);
            }
            if (squad.players.Count < SquadSize)
            {
                var used = SquadSystem.UsedNamesExcept(club, ClubId);
                foreach (var p in squad.players) used.Add(p.name);
                var rng = new System.Random(0x6c75b ^ squad.players.Count ^ (club.myClub != null && club.myClub.name != null ? club.myClub.name.Length * 977 : 0));
                int next = 0; foreach (var p in squad.players) if (p.id <= -2000) next = Mathf.Max(next, -2000 - p.id + 1);
                while (squad.players.Count < SquadSize)
                {
                    // 정원 대비 가장 모자란 포지션부터
                    int[] have = new int[4]; foreach (var p in squad.players) have[(int)p.role]++;
                    int need = 0; for (int r = 1; r < 4; r++) if (RoleQuota[r] - have[r] > RoleQuota[need] - have[need]) need = r;
                    var np = SquadSystem.MakePlayer(club, rng, used, (SquadRole)need, rng.Next(18, 22), 22f + rng.Next(0, 9));
                    np.id = -2000 - next++;
                    squad.players.Add(np);
                }
            }
            FixLineup(club);
        }

        // 라인업이 지금 포메이션의 11자리와 맞지 않으면(선수 없음·중복·개수 다름) 자동으로 다시 짭니다.
        static void FixLineup(SaveData club)
        {
            var squad = Squad(club);
            var lineup = club.myClub.lineup;
            bool ok = lineup != null && lineup.Count == Starters;
            if (ok)
            {
                var seen = new HashSet<int>();
                foreach (int id in lineup) if (!seen.Add(id) || !squad.players.Exists(p => p.id == id)) { ok = false; break; }
            }
            if (!ok) AutoLineup(club, null);
        }

        // 자리마다 그 포지션의 가장 강한 선수로 채웁니다. prefer에 있는 선수(지금 주전)를 먼저 씁니다.
        public static void AutoLineup(SaveData club, ICollection<int> prefer)
        {
            var squad = Squad(club);
            var layout = TacticSystem.Layout(club.myClub.formation);
            var lineup = new List<int>();
            var taken = new HashSet<int>();
            for (int s = 0; s < layout.Length; s++)
            {
                SquadPlayer best = null; int bestScore = int.MinValue;
                foreach (var p in squad.players)
                {
                    if (taken.Contains(p.id)) continue;
                    int score = (p.role == layout[s].role ? 2000 : 0) + (prefer != null && prefer.Contains(p.id) ? 1000 : 0) + Mathf.RoundToInt(p.rating * 5f);
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best == null) break;
                taken.Add(best.id); lineup.Add(best.id);
            }
            club.myClub.lineup = lineup;
        }

        // 주전 11명 (포메이션 자리 순서)
        public static SquadPlayer[] Lineup(SaveData club)
        {
            EnsureClubSquad(club);
            var squad = Squad(club);
            var xi = new SquadPlayer[Starters];
            for (int i = 0; i < Starters && i < club.myClub.lineup.Count; i++) xi[i] = squad.players.Find(p => p.id == club.myClub.lineup[i]);
            return xi;
        }

        // 자리별로 요구되는 능력치의 비중 (슈팅, 패스, 드리블, 수비, 체력, GK 능력)
        static readonly float[][] SlotWeights =
        {
            new[] { 0f, 0f, 0f, 0f, 0f, 1f },             // GK: GK 능력 100%
            new[] { 0f, .20f, .10f, .55f, .15f, 0f },       // DF: 패스 20, 드리블 10, 수비 55, 체력 15
            new[] { .20f, .30f, .15f, .15f, .20f, 0f },     // MF: 슈팅 20, 패스 30, 드리블 15, 수비 15, 체력 20
            new[] { .50f, .15f, .20f, 0f, .15f, 0f },       // FW: 슈팅 50, 패스 15, 드리블 20, 체력 15
        };

        // 선수의 세부 능력치 6개: 내가 키운 선수는 실제 값, 나머지는 능력과 포지션으로 정한 값
        public static float[] Attrs(SaveData club, SquadPlayer p)
        {
            var lg = club != null && club.legends != null ? club.legends.Find(l => -1000 - l.careerNumber == p.id) : null;
            if (lg != null) return SquadSystem.AtLeastMin(new[] { lg.shooting, lg.passing, lg.dribbling, lg.defending, lg.stamina, lg.goalkeeping });
            return SquadSystem.DeriveAttrs(p);
        }

        // 그 자리에서 요구되는 능력치의 가중 평균
        public static float SlotRating(float[] a, SquadRole slot)
        {
            var w = SlotWeights[(int)slot];
            float sum = 0f, total = 0f;
            for (int k = 0; k < 6 && k < a.Length; k++) { sum += w[k] * a[k]; total += w[k]; }
            return total > 0f ? sum / total : 0f;
        }

        // 그 자리에서의 능력: 자기 포지션이면 능력 그대로, 아니면 그 자리가 요구하는 능력치의 평균 (따로 깎지 않음)
        public static float Effective(SaveData club, SquadPlayer p, SquadRole slot)
        {
            if (p == null) return 0f;
            if (p.role == slot) return p.rating;
            return Mathf.Round(SlotRating(Attrs(club, p), slot));
        }

        // 주전 11명의 (자리 적합도를 반영한) 평균 평점
        public static float TeamLevel(SaveData club)
        {
            var xi = Lineup(club);
            var layout = TacticSystem.Layout(club.myClub.formation);
            float sum = 0f; int n = 0;
            for (int i = 0; i < xi.Length && i < layout.Length; i++) if (xi[i] != null) { sum += Effective(club, xi[i], layout[i].role); n++; }
            return n > 0 ? sum / n : 20f;
        }

        // 두 선수 자리 바꾸기: 주전끼리면 자리를 맞바꾸고, 주전과 교체면 교체 선수가 그 자리에 들어감
        public static bool Swap(SaveData club, int a, int b)
        {
            if (a == b) return false;
            var lineup = club.myClub.lineup;
            int ia = lineup.IndexOf(a), ib = lineup.IndexOf(b);
            if (ia < 0 && ib < 0) return false;
            if (ia >= 0) lineup[ia] = b;
            if (ib >= 0) lineup[ib] = a;
            return true;
        }

        public static void SetFormation(SaveData club, Formation f)
        {
            if (club.myClub.formation == f) return;
            var keep = new HashSet<int>(club.myClub.lineup);
            club.myClub.formation = f;
            AutoLineup(club, keep);   // 지금 주전을 그대로 쓰되 새 포메이션 자리에 맞춰 다시 배치
        }

        // ---------- 선수별 시즌 기록 ----------
        // 경기가 끝날 때마다 그 경기의 1군 11명 기록을 쌓습니다 (교체로 빠져도 1군으로 뛴 기록은 남음).
        // 골·도움은 경기 기록의 득점자, 출전·평점·수비·선방은 다른 팀 선수와 같은 경기 계산으로 만듭니다.
        public static void RecordMatch(SaveData club, MatchSettings ms, CareerSettings career, MatchRecord m)
        {
            if (club == null || !club.hasClub || m == null) return;
            var mc = club.myClub;
            if (mc.stats == null) mc.stats = new List<ClubPlayerStat>();
            if (mc.statsSeason != club.season.seasonNumber)   // 새 시즌(또는 기록을 쌓기 전 저장): 이번 시즌 치른 경기 전부로 다시
            {
                mc.stats.Clear(); mc.statsSeason = club.season.seasonNumber;
                foreach (var pm in club.season.matches) if (pm.played) AddMatch(club, ms, career, pm);
                return;
            }
            AddMatch(club, ms, career, m);
        }

        static void AddMatch(SaveData club, MatchSettings ms, CareerSettings career, MatchRecord m)
        {
            var team = ms.FindTeam(ClubId); var opp = ms.FindTeam(m.opponentId);
            if (team == null || opp == null) return;
            var simTeam = new TeamDefinition { id = team.id, displayName = team.displayName, division = team.division, strength = team.strength, requiredRating = 0f, focus = team.focus };
            var layout = TacticSystem.Layout(club.myClub.formation);
            var xi = Lineup(club);
            for (int i = 0; i < xi.Length && i < layout.Length; i++)
            {
                var p = xi[i];
                if (p == null) continue;
                // 세부 능력치 그대로 그 자리(포메이션의 포지션)에서 경기 (자기 포지션이 아니어도 따로 깎지 않음)
                var a = Attrs(club, p);
                float cap = career.attributeCap;
                var pd = new PlayerData { playerName = p.name, position = SquadSystem.ToPosition(layout[i].role),
                    shooting = Mathf.Clamp(a[0], SquadSystem.MinStat, cap), passing = Mathf.Clamp(a[1], SquadSystem.MinStat, cap), dribbling = Mathf.Clamp(a[2], SquadSystem.MinStat, cap),
                    defending = Mathf.Clamp(a[3], SquadSystem.MinStat, cap), stamina = Mathf.Clamp(a[4], SquadSystem.MinStat, cap), goalkeeping = Mathf.Clamp(a[5], SquadSystem.MinStat, cap) };
                var mr = new MatchRecord { home = m.home, seed = unchecked(m.seed ^ (p.id * 7919) ^ 0x3c6ef35f) };
                MatchSimulation.Play(mr, pd, simTeam, opp, ms, career.attributeCap, layout[i].kind);
                var s = club.myClub.stats.Find(x => x.id == p.id);
                if (s == null) { s = new ClubPlayerStat { id = p.id }; club.myClub.stats.Add(s); }
                s.apps++; s.ratingSum += mr.rating;
                if (layout[i].role == SquadRole.GK) s.saves += mr.defensiveActions; else s.defense += mr.defensiveActions;   // 골문을 지킨 경기는 선방
                if (m.goalNotes != null)
                    foreach (var g in m.goalNotes) if (g.ours) { if (g.scorer == p.name) s.goals++; if (g.assist == p.name) s.assists++; }
            }
        }

        // 이번 시즌 선수별 기록 (이름 → 기록). 기록을 쌓기 전에 치른 경기가 있으면(예전 저장) 지금 1군으로 한 번 채웁니다.
        public static Dictionary<string, SeasonLine> SeasonLines(SaveData club, MatchSettings ms, CareerSettings career)
        {
            var mc = club.myClub;
            if (mc.stats == null) mc.stats = new List<ClubPlayerStat>();
            if (mc.statsSeason != club.season.seasonNumber)
            {
                mc.stats.Clear(); mc.statsSeason = club.season.seasonNumber;
                foreach (var m in club.season.matches) if (m.played) AddMatch(club, ms, career, m);
            }
            var lines = new Dictionary<string, SeasonLine>();
            var squad = Squad(club);
            foreach (var s in mc.stats)
            {
                var p = squad.players.Find(x => x.id == s.id);
                if (p == null) continue;   // 방출한 선수
                lines[p.name] = new SeasonLine { apps = s.apps, goals = s.goals, assists = s.assists, defense = s.defense, saves = s.saves, avg = s.apps > 0 ? s.ratingSum / s.apps : 0f };
            }
            return lines;
        }

        // 무료 영입: 선수단이 꽉 찼으면 releaseId 선수를 내보내고 그 자리(주전이면 주전 자리)에 들어갑니다.
        // 내보낸 선수가 은퇴 선수면 다시 무료 영입 목록으로, 새 선수면 팀을 떠납니다.
        public static bool Sign(SaveData career, SaveData club, MatchSettings clubMs, TeamDefinition[] baseTeams, int careerNumber, int releaseId)
        {
            var lg = career.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null || club.legends.Exists(x => x.careerNumber == careerNumber)) return false;
            EnsureClubSquad(club);
            var squad = Squad(club);
            var newcomer = LegendPlayer(lg);
            var out_ = squad.players.Find(p => p.id == releaseId);
            if (squad.players.Count >= SquadSize && out_ == null) return false;   // 꽉 찼으면 반드시 한 명을 내보내야 함
            if (out_ != null)
            {
                squad.players.Remove(out_);
                if (IsLegend(out_)) club.legends.RemoveAll(l => -1000 - l.careerNumber == out_.id);
                int slot = club.myClub.lineup.IndexOf(out_.id);
                if (slot >= 0) club.myClub.lineup[slot] = newcomer.id;
            }
            club.legends.Add(Copy(lg));
            squad.players.Add(newcomer);
            FixLineup(club);
            ApplyRoster(club, clubMs, baseTeams);
            return true;
        }
    }
}
