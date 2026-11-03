using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    public class StandingRow
    {
        public string id, name;
        public int played, won, drawn, lost, goalsFor, goalsAgainst;
        public int Points => won * 3 + drawn;
        public int GoalDiff => goalsFor - goalsAgainst;
    }

    // 시즌 일정, 경기 진행, 순위표, 승강제, 결과 설명. 화면과 무관한 규칙입니다.
    // 리그는 여러 부(1부가 가장 높음)로 나뉘고, 내 팀은 자기 부의 팀들과 홈·원정으로 한 번씩 경기합니다.
    // 시즌이 끝나면 각 부의 상위 팀은 올라가고 하위 팀은 내려갑니다. 다른 부의 결과는 시즌 끝에 한 번에 계산합니다.
    public static class SeasonSystem
    {
        // 소속 팀, 리그 소속, 시즌이 없으면 만들고, 빠진 다른 팀 경기 결과를 채워 둡니다.
        public static void Prepare(SaveData save, MatchSettings ms)
        {
            if (ms.FindTeam(save.teamId) == null)
            {
                var start = ms.StartTeam();
                if (start != null) save.teamId = start.id;
            }
            EnsureLeague(save, ms);
            if (save.season.matches.Count == 0)
                StartSeason(save, ms, Mathf.Max(1, save.season.seasonNumber));
            BackfillOthers(save, ms);
        }

        // ---------- 리그 소속 ----------
        // 설정에 있는 모든 팀이 소속 부를 갖게 합니다. 새로 생긴 팀은 설정의 처음 부로, 없어진 팀은 지웁니다.
        static void EnsureLeague(SaveData save, MatchSettings ms)
        {
            if (save.league == null) save.league = new List<TeamDivision>();
            var known = new HashSet<string>();
            if (ms.teams != null)
                foreach (var t in ms.teams)
                    if (t != null && !string.IsNullOrEmpty(t.id)) known.Add(t.id);
            save.league.RemoveAll(e => e == null || !known.Contains(e.teamId));
            var has = new HashSet<string>();
            foreach (var e in save.league) { has.Add(e.teamId); e.division = ms.ClampDivision(e.division); }
            if (ms.teams == null) return;
            foreach (var t in ms.teams)
                if (t != null && !string.IsNullOrEmpty(t.id) && has.Add(t.id))
                    save.league.Add(new TeamDivision { teamId = t.id, division = ms.ClampDivision(t.division) });
        }

        public static int DivisionOf(SaveData save, MatchSettings ms, string teamId)
        {
            if (save.league != null)
                foreach (var e in save.league)
                    if (e.teamId == teamId) return e.division;
            var team = ms.FindTeam(teamId);
            return team != null ? ms.ClampDivision(team.division) : ms.DivisionCount;
        }

        // 지금 그 부에 속한 팀들 (설정의 순서대로)
        static List<TeamDefinition> TeamsIn(SaveData save, MatchSettings ms, int division)
        {
            var list = new List<TeamDefinition>();
            if (ms.teams == null) return list;
            foreach (var t in ms.teams)
                if (t != null && DivisionOf(save, ms, t.id) == division) list.Add(t);
            return list;
        }

        // ---------- 일정 ----------
        static void StartSeason(SaveData save, MatchSettings ms, int seasonNumber)
        {
            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            int division = DivisionOf(save, ms, save.teamId);
            var ids = new List<string>();
            foreach (var t in TeamsIn(save, ms, division)) ids.Add(t.id);
            if (!ids.Contains(save.teamId)) ids.Add(save.teamId);

            var season = new SeasonData { seasonNumber = seasonNumber, division = division, teamIds = ids, seed = rng.Next() };
            season.fixtures = DoubleRoundRobin(ids, rng);
            foreach (var f in season.fixtures)
            {
                if (f.homeId != save.teamId && f.awayId != save.teamId) continue;
                bool home = f.homeId == save.teamId;
                string opp = home ? f.awayId : f.homeId;
                var team = ms.FindTeam(opp);
                season.matches.Add(new MatchRecord { round = f.round, opponentId = opp, opponentName = team != null ? team.displayName : opp, home = home, seed = rng.Next() });
            }
            season.matches.Sort((a, b) => a.round.CompareTo(b.round));
            save.season = season;
            if (season.matches.Count == 0) Debug.LogError("Match Settings needs at least 2 teams in a division.");
        }

        // 원형 배치로 만든 홈·원정 리그 일정. 모든 팀이 서로 두 번(홈 한 번, 원정 한 번) 만나고, 라운드마다 한 경기씩 치릅니다.
        // 팀 수가 홀수면 라운드마다 한 팀이 쉽니다.
        static List<LeagueGame> DoubleRoundRobin(List<string> ids, System.Random rng)
        {
            var order = new List<string>(ids);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            if (order.Count % 2 == 1) order.Add(null);   // 쉬는 칸
            int n = order.Count, half = n - 1;
            var games = new List<LeagueGame>();
            for (int r = 0; r < half; r++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    string a = order[i], b = order[n - 1 - i];
                    if (a == null || b == null) continue;
                    bool aHome = (r + i) % 2 == 0;
                    games.Add(new LeagueGame { round = r + 1, homeId = aHome ? a : b, awayId = aHome ? b : a });
                    games.Add(new LeagueGame { round = r + 1 + half, homeId = aHome ? b : a, awayId = aHome ? a : b });
                }
                // 첫 칸은 고정하고 나머지를 한 칸씩 돌립니다.
                string last = order[n - 1];
                order.RemoveAt(n - 1);
                order.Insert(1, last);
            }
            games.Sort((x, y) => x.round.CompareTo(y.round));
            return games;
        }

        public static bool IsComplete(SaveData save)
        {
            return save.season.matches.Count > 0 && save.season.nextMatchIndex >= save.season.matches.Count;
        }

        // 다음 경기를 진행하고 결과를 저장 데이터에 기록합니다. 진행할 수 없으면 null.
        public static MatchRecord PlayNext(SaveData save, MatchSettings ms, CareerSettings career)
        {
            var season = save.season;
            if (season.nextMatchIndex >= season.matches.Count) return null;
            var match = season.matches[season.nextMatchIndex];
            var own = ms.FindTeam(save.teamId);
            var opp = ms.FindTeam(match.opponentId);
            if (own == null || opp == null) return null;
            MatchSimulation.Play(match, save.player, own, opp, ms, career.attributeCap);
            GenerateOthers(match, save, ms);
            EnsureContract(save, ms, career);
            match.wage = save.weeklyWage;            // 경기 한 번 = 1주
            save.money += save.weeklyWage;
            season.nextMatchIndex++;
            return match;
        }

        // 같은 라운드에 열린 다른 팀끼리의 경기 결과를 만듭니다.
        static void GenerateOthers(MatchRecord match, SaveData save, MatchSettings ms)
        {
            match.others = new List<LeagueGame>();
            var rng = new System.Random(match.seed ^ 0x5bd1e995);
            var season = save.season;
            if (season.fixtures != null && season.fixtures.Count > 0)
            {
                foreach (var f in season.fixtures)
                {
                    if (f.round != match.round || f.homeId == save.teamId || f.awayId == save.teamId) continue;
                    var home = ms.FindTeam(f.homeId); var away = ms.FindTeam(f.awayId);
                    if (home == null || away == null) continue;
                    var game = new LeagueGame { round = f.round, homeId = f.homeId, awayId = f.awayId };
                    MatchSimulation.PlayOther(game, home, away, ms, rng);
                    match.others.Add(game);
                }
                return;
            }

            // 부가 생기기 전의 시즌: 내 경기에 나오지 않은 팀들끼리 무작위로 짝을 짓습니다.
            var pool = new List<TeamDefinition>();
            foreach (var t in SeasonTeams(save, ms))
                if (t.id != save.teamId && t.id != match.opponentId) pool.Add(t);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            for (int i = 0; i + 1 < pool.Count; i += 2)
            {
                bool firstHome = rng.Next(2) == 0;
                var home = firstHome ? pool[i] : pool[i + 1];
                var away = firstHome ? pool[i + 1] : pool[i];
                var game = new LeagueGame { homeId = home.id, awayId = away.id };
                MatchSimulation.PlayOther(game, home, away, ms, rng);
                match.others.Add(game);
            }
        }

        // 이전 버전에서 저장된 경기처럼 다른 팀 결과가 없는 경우 한 번 채워 넣습니다.
        static void BackfillOthers(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            for (int i = 0; i < season.nextMatchIndex && i < season.matches.Count; i++)
            {
                var m = season.matches[i];
                if (m.played && (m.others == null || m.others.Count == 0)) GenerateOthers(m, save, ms);
            }
        }

        // 이번 시즌에 같은 부에서 경기한 팀들
        static List<TeamDefinition> SeasonTeams(SaveData save, MatchSettings ms)
        {
            var list = new List<TeamDefinition>();
            var ids = save.season.teamIds;
            if (ids != null && ids.Count > 0)
            {
                foreach (var id in ids) { var t = ms.FindTeam(id); if (t != null) list.Add(t); }
                return list;
            }
            if (ms.teams != null) foreach (var t in ms.teams) if (t != null) list.Add(t);
            return list;
        }

        // ---------- 시즌 종료: 승강 ----------
        // 시즌이 끝났으면 모든 부의 최종 순위로 승강을 한 번만 반영합니다. 이적 제안은 반영된 다음 시즌의 부를 기준으로 합니다.
        public static void EnsureSeasonEnd(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            if (!IsComplete(save) || save.leagueSeason == season.seasonNumber) return;
            save.leagueSeason = season.seasonNumber;
            EnsureLeague(save, ms);
            if (season.division > 0)
            {
                int count = ms.DivisionCount;
                var moves = new Dictionary<string, int>();
                for (int d = 1; d <= count; d++)
                {
                    var order = new List<string>();
                    if (d == season.division) foreach (var row in Standings(save, ms)) order.Add(row.id);
                    else order = SimulateDivision(save, ms, d);
                    int k = Mathf.Min(ms.promotionSpots, order.Count / 2);
                    if (d > 1) for (int i = 0; i < k; i++) moves[order[i]] = d - 1;
                    if (d < count) for (int i = 0; i < k; i++) moves[order[order.Count - 1 - i]] = d + 1;
                }
                foreach (var e in save.league)
                    if (moves.TryGetValue(e.teamId, out int to)) e.division = to;
            }
            season.nextDivision = DivisionOf(save, ms, save.teamId);
        }

        // 내 팀이 없는 부의 한 시즌을 전력만으로 계산해 최종 순위(팀 ID)를 돌려줍니다. 같은 시즌이면 언제 계산해도 같습니다.
        static List<string> SimulateDivision(SaveData save, MatchSettings ms, int division)
        {
            var teams = TeamsIn(save, ms, division);
            var ids = new List<string>();
            foreach (var t in teams) ids.Add(t.id);
            var rng = new System.Random(save.season.seed ^ (division * 7919));
            var rows = NewRows(teams, out var list);
            foreach (var f in DoubleRoundRobin(ids, rng))
            {
                var game = new LeagueGame { homeId = f.homeId, awayId = f.awayId };
                MatchSimulation.PlayOther(game, ms.FindTeam(f.homeId), ms.FindTeam(f.awayId), ms, rng);
                AddResult(rows, game.homeId, game.homeGoals, game.awayGoals);
                AddResult(rows, game.awayId, game.awayGoals, game.homeGoals);
            }
            SortRows(list);
            var order = new List<string>();
            foreach (var r in list) order.Add(r.id);
            return order;
        }

        // 다음 시즌을 시작합니다. 끝난 시즌 요약을 이력에 남기고, 이적이 합의되어 있으면 새 팀으로 옮깁니다.
        // 이적하거나 부가 바뀌었으면 안내 문장을, 아니면 null을 돌려줍니다.
        public static string StartNextSeason(SaveData save, MatchSettings ms, CareerSettings career)
        {
            EnsureSeasonEnd(save, ms);
            save.seasonHistory.Add(Summarize(save, ms));
            int before = save.season.division;
            int oldWage = save.weeklyWage;
            var lines = new List<string>();
            int newWage = -1;
            if (!string.IsNullOrEmpty(save.pendingTeamId))
            {
                var target = ms.FindTeam(save.pendingTeamId);
                if (target != null)
                {
                    string from = TeamName(save, ms);
                    save.transfers.Add(new TransferRecord { afterSeason = save.season.seasonNumber, fromTeamName = from, toTeamName = target.displayName });
                    lines.Add("Transferred: " + from + " -> " + target.displayName);
                    save.teamId = target.id;
                    foreach (var o in save.offers) if (o.teamId == target.id && o.weeklyWage > 0) newWage = o.weeklyWage;
                }
            }
            if (newWage <= 0) newWage = WageFor(save, ms, career, save.teamId);   // 남으면 지금 능력치로 재계약
            save.weeklyWage = newWage;
            if (newWage != oldWage) lines.Add("New contract: " + Money(newWage) + " per week.");
            save.pendingTeamId = "";
            save.offers.Clear();
            StartSeason(save, ms, save.season.seasonNumber + 1);
            int after = save.season.division;
            if (before > 0 && after != before)
                lines.Add((after < before ? "Up to " : "Down to ") + ms.DivisionName(after) + ".");
            else if (before == 0)
                lines.Add("The league now has " + ms.DivisionCount + " divisions. You play in " + ms.DivisionName(after) + ".");
            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        // ---------- 계약 (주급) ----------
        // 이 팀이 지금 내 선수에게 주는 주급
        public static int WageFor(SaveData save, MatchSettings ms, CareerSettings career, string teamId)
        {
            return ms.WeeklyWage(ms.FindTeam(teamId), DivisionOf(save, ms, teamId), PlayerRating(save, ms, career));
        }

        // 계약이 없으면(새 선수, 예전 저장, 환생 직후) 지금 팀과 능력치로 주급을 정합니다.
        public static void EnsureContract(SaveData save, MatchSettings ms, CareerSettings career)
        {
            if (save.weeklyWage <= 0) save.weeklyWage = WageFor(save, ms, career, save.teamId);
        }

        // ---------- 이적 ----------
        const float OfferMinStartChance = 0.5f;   // 선발 가능성이 이보다 낮은 팀은 제안하지 않습니다.

        // 시즌이 끝나면 최대 3곳에서 제안을 받습니다. 실력이 좋으면 한두 단계 위 부의 팀이 먼저 제안합니다. 새 제안이 생기면 true.
        public static bool EnsureOffers(SaveData save, MatchSettings ms, CareerSettings career)
        {
            if (!IsComplete(save)) return false;
            EnsureSeasonEnd(save, ms);
            if (save.offersSeason == save.season.seasonNumber) return false;
            save.offers = new List<TransferOffer>();
            save.pendingTeamId = "";
            save.offersSeason = save.season.seasonNumber;

            float rating = PlayerRating(save, ms, career);
            int myDivision = DivisionOf(save, ms, save.teamId);
            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks) ^ save.season.seasonNumber);
            var picks = new List<TeamDefinition>();
            var sameDivision = new List<TeamDefinition>();
            if (ms.teams != null)
                foreach (var t in ms.teams)
                {
                    if (t == null || t.id == save.teamId) continue;
                    int d = DivisionOf(save, ms, t.id);
                    if (d == myDivision) sameDivision.Add(t);
                    if (d < myDivision - 2 || d > myDivision + 1) continue;
                    if (MatchSimulation.StartChance(t, rating) >= OfferMinStartChance) picks.Add(t);
                }
            if (picks.Count == 0) picks = sameDivision;   // 갈 만한 팀이 없으면 같은 부에서라도
            Shuffle(picks, rng);
            picks.Sort((a, b) => DivisionOf(save, ms, a.id).CompareTo(DivisionOf(save, ms, b.id)));   // 높은 부부터
            for (int i = 0; i < picks.Count && save.offers.Count < 3; i++)
            {
                var t = picks[i];
                save.offers.Add(new TransferOffer
                {
                    teamId = t.id, teamName = t.displayName, teamStrength = t.strength, division = DivisionOf(save, ms, t.id),
                    weeklyWage = ms.WeeklyWage(t, DivisionOf(save, ms, t.id), rating),
                    startChance = MatchSimulation.StartChance(t, rating), focus = t.focus
                });
            }
            return true;
        }

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        public static TransferOffer SetPending(SaveData save, string teamId)
        {
            foreach (var offer in save.offers)
                if (offer.teamId == teamId) { save.pendingTeamId = teamId; return offer; }
            return null;
        }

        // 내 선수의 포지션 평점 (0~100)
        public static float PlayerRating(SaveData save, MatchSettings ms, CareerSettings career)
        {
            return MatchSimulation.PositionRating(ms.Profile(save.player.position), save.player, career.attributeCap);
        }

        // ---------- 순위표 ----------
        public static List<StandingRow> Standings(SaveData save, MatchSettings ms)
        {
            var rows = NewRows(SeasonTeams(save, ms), out var list);
            foreach (var m in save.season.matches)
            {
                if (!m.played) continue;
                AddResult(rows, save.teamId, m.goalsFor, m.goalsAgainst);
                AddResult(rows, m.opponentId, m.goalsAgainst, m.goalsFor);
                if (m.others == null) continue;
                foreach (var g in m.others)
                {
                    AddResult(rows, g.homeId, g.homeGoals, g.awayGoals);
                    AddResult(rows, g.awayId, g.awayGoals, g.homeGoals);
                }
            }
            SortRows(list);
            return list;
        }

        static Dictionary<string, StandingRow> NewRows(List<TeamDefinition> teams, out List<StandingRow> list)
        {
            var rows = new Dictionary<string, StandingRow>();
            list = new List<StandingRow>();
            foreach (var t in teams)
            {
                if (t == null || rows.ContainsKey(t.id)) continue;
                var row = new StandingRow { id = t.id, name = t.displayName };
                rows[t.id] = row; list.Add(row);
            }
            return rows;
        }

        static void SortRows(List<StandingRow> list)
        {
            list.Sort((a, b) =>
            {
                int c = b.Points.CompareTo(a.Points); if (c != 0) return c;
                c = b.GoalDiff.CompareTo(a.GoalDiff); if (c != 0) return c;
                c = b.goalsFor.CompareTo(a.goalsFor); if (c != 0) return c;
                return string.Compare(a.name, b.name, StringComparison.Ordinal);
            });
        }

        static void AddResult(Dictionary<string, StandingRow> rows, string id, int goalsFor, int goalsAgainst)
        {
            if (string.IsNullOrEmpty(id) || !rows.TryGetValue(id, out var r)) return;
            r.played++;
            r.goalsFor += goalsFor; r.goalsAgainst += goalsAgainst;
            if (goalsFor > goalsAgainst) r.won++;
            else if (goalsFor < goalsAgainst) r.lost++;
            else r.drawn++;
        }

        // ---------- 요약 ----------
        public static SeasonSummary Summarize(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            var r = new SeasonSummary { seasonNumber = season.seasonNumber, division = season.division };
            float ratingSum = 0f;
            foreach (var m in season.matches)
            {
                if (!m.played) continue;
                r.played++;
                r.goalsFor += m.goalsFor; r.goalsAgainst += m.goalsAgainst;
                r.playerGoals += m.playerGoals; r.playerAssists += m.playerAssists;
                if (m.playerPlayed) { r.appearances++; ratingSum += m.rating; }
                if (m.goalsFor > m.goalsAgainst) r.wins++;
                else if (m.goalsFor < m.goalsAgainst) r.losses++;
                else r.draws++;
            }
            r.averageRating = r.appearances > 0 ? ratingSum / r.appearances : 0f;
            r.teamName = TeamName(save, ms);

            var table = Standings(save, ms);
            for (int i = 0; i < table.Count; i++)
                if (table[i].id == save.teamId) { r.finalPosition = i + 1; r.points = table[i].Points; }
            if (table.Count > 0) r.champion = table[0].name;
            return r;
        }

        public static string Ordinal(int n)
        {
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        static string TeamName(SaveData save, MatchSettings ms)
        {
            var team = ms.FindTeam(save.teamId);
            return team != null ? team.displayName : save.teamId;
        }

        static string ShortDivision(int division) { return "Div " + division; }

        public static string Money(long amount) { return amount.ToString("#,0"); }

        // 끝난 시즌의 승강 결과 (내 팀 기준)
        static string MovementText(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            if (season.division <= 0 || season.nextDivision <= 0) return null;
            if (season.nextDivision < season.division) return "Promoted to " + ms.DivisionName(season.nextDivision) + "!";
            if (season.nextDivision > season.division) return "Relegated to " + ms.DivisionName(season.nextDivision) + ".";
            return "Staying in " + ms.DivisionName(season.division) + ".";
        }

        // ---------- 화면에 보여줄 글 ----------
        public static string DescribeNext(SaveData save, MatchSettings ms, CareerSettings career)
        {
            var season = save.season;
            if (season.matches.Count == 0) return "No season available.";
            string where = season.division > 0 ? ms.DivisionName(season.division) : "League";
            if (IsComplete(save))
            {
                var t = Summarize(save, ms);
                var sb = new StringBuilder();
                sb.Append("Season ").Append(season.seasonNumber).Append(" complete - ").Append(where).Append('\n')
                  .Append(Ordinal(t.finalPosition)).Append(" of ").Append(Standings(save, ms).Count)
                  .Append("   W").Append(t.wins).Append(" D").Append(t.draws).Append(" L").Append(t.losses)
                  .Append("   Avg rating ").Append(t.averageRating.ToString("0.0"));
                string move = MovementText(save, ms);
                if (move != null) sb.Append('\n').Append(move);
                return sb.ToString();
            }
            var next = season.matches[season.nextMatchIndex];
            var text = new StringBuilder();
            text.Append("Season ").Append(season.seasonNumber).Append("   ").Append(where)
                .Append("   Round ").Append(next.round).Append('/').Append(season.matches.Count)
                .Append("\nNext: vs ").Append(next.opponentName).Append(next.home ? " (Home)" : " (Away)");
            if (season.division > 0)
                text.Append("\nYour rating ").Append(PlayerRating(save, ms, career).ToString("0"))
                    .Append("  /  recommended ").Append(ms.RecommendedRating(season.division).ToString("0"));
            return text.ToString();
        }

        public static string DescribeLast(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            if (season.nextMatchIndex <= 0 || season.nextMatchIndex > season.matches.Count) return "No matches played yet.";
            return DescribeResult(season.matches[season.nextMatchIndex - 1], save, ms);
        }

        public static string DescribeResult(MatchRecord m, SaveData save, MatchSettings ms)
        {
            string outcome = m.goalsFor > m.goalsAgainst ? "WIN" : (m.goalsFor < m.goalsAgainst ? "LOSS" : "DRAW");
            int passPercent = m.passesAttempted > 0 ? Mathf.RoundToInt(100f * m.passesCompleted / m.passesAttempted) : 0;
            var sb = new StringBuilder();
            sb.Append("Round ").Append(m.round).Append(": ").Append(TeamName(save, ms)).Append(' ')
              .Append(m.goalsFor).Append(" - ").Append(m.goalsAgainst).Append(' ').Append(m.opponentName)
              .Append("  (").Append(outcome).Append(")\n");
            if (m.wage > 0) sb.Append("Wage +").Append(Money(m.wage)).Append('\n');
            if (!m.playerPlayed) return sb.Append("You did not play (bench).").ToString();
            sb.Append("Rating ").Append(m.rating.ToString("0.0")).Append('\n');
            sb.Append("Goals ").Append(m.playerGoals).Append("  Assists ").Append(m.playerAssists)
              .Append("  Shots ").Append(m.playerShots).Append('\n');
            sb.Append("Passes ").Append(m.passesCompleted).Append('/').Append(m.passesAttempted)
              .Append(" (").Append(passPercent).Append("%)  Defensive actions ").Append(m.defensiveActions);
            return sb.ToString();
        }

        // 순위표. 승격권 아래와 강등권 위에 구분선을 넣습니다.
        public static string DescribeStandings(SaveData save, MatchSettings ms)
        {
            var table = Standings(save, ms);
            if (table.Count == 0) return "No teams.";
            var season = save.season;
            int k = season.division > 0 ? Mathf.Min(ms.promotionSpots, table.Count / 2) : 0;
            bool canGoUp = season.division > 1, canGoDown = season.division > 0 && season.division < ms.DivisionCount;
            var sb = new StringBuilder();
            if (season.division > 0) sb.Append(ms.DivisionName(season.division)).Append('\n');
            for (int i = 0; i < table.Count; i++)
            {
                var r = table[i];
                if (canGoDown && k > 0 && i == table.Count - k) sb.Append("---- relegation ----\n");
                sb.Append(i + 1).Append(". ").Append(r.name).Append(r.id == save.teamId ? " *" : "")
                  .Append("   W").Append(r.won).Append(" D").Append(r.drawn).Append(" L").Append(r.lost)
                  .Append("  ").Append(r.GoalDiff >= 0 ? "+" : "").Append(r.GoalDiff)
                  .Append("  ").Append(r.Points).Append("pts");
                if (i < table.Count - 1) sb.Append('\n');
                if (canGoUp && k > 0 && i == k - 1) sb.Append("---- promotion ----\n");
            }
            return sb.ToString();
        }

        public static string DescribeSeasonEnd(SaveData save, MatchSettings ms)
        {
            EnsureSeasonEnd(save, ms);
            var t = Summarize(save, ms);
            int teams = Standings(save, ms).Count;
            var sb = new StringBuilder();
            sb.Append("Season ").Append(t.seasonNumber).Append(" final: ").Append(Ordinal(t.finalPosition))
              .Append(" of ").Append(teams).Append("  (").Append(t.points).Append(" pts)\n");
            sb.Append(t.finalPosition == 1 ? "You are the champion!\n" : "Champion: " + t.champion + "\n");
            string move = MovementText(save, ms);
            if (move != null) sb.Append(move).Append('\n');
            sb.Append("W").Append(t.wins).Append(" D").Append(t.draws).Append(" L").Append(t.losses)
              .Append("   Goals ").Append(t.goalsFor).Append(" - ").Append(t.goalsAgainst).Append('\n');
            sb.Append("Your goals ").Append(t.playerGoals).Append("  assists ").Append(t.playerAssists)
              .Append("  avg rating ").Append(t.averageRating.ToString("0.0"))
              .Append("  apps ").Append(t.appearances).Append('/').Append(t.played);
            return sb.ToString();
        }

        public static string DescribeHistory(SaveData save)
        {
            var history = save.seasonHistory;
            if (history.Count == 0) return "No completed seasons yet.";
            var sb = new StringBuilder();
            int shown = 0;
            for (int i = history.Count - 1; i >= 0 && shown < 3; i--, shown++)
            {
                var h = history[i];
                if (shown > 0) sb.Append('\n');
                sb.Append("Season ").Append(h.seasonNumber).Append(": ");
                if (h.division > 0) sb.Append(ShortDivision(h.division)).Append(' ');
                if (h.finalPosition > 0) sb.Append(Ordinal(h.finalPosition)).Append(", ");
                if (!string.IsNullOrEmpty(h.teamName)) sb.Append(h.teamName).Append(", ");
                sb.Append("W").Append(h.wins).Append(" D").Append(h.draws).Append(" L").Append(h.losses)
                  .Append(", G").Append(h.playerGoals).Append(" A").Append(h.playerAssists)
                  .Append(", avg ").Append(h.averageRating.ToString("0.0"));
            }
            return sb.ToString();
        }
    }
}
