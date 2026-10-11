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

    // 시즌 일정, 경기 진행, 순위표, 결과 설명. 화면과 무관한 규칙입니다.
    public static class SeasonSystem
    {
        // 소속 팀과 시즌이 없으면 만들고, 빠진 다른 팀 경기 결과를 채워 둡니다.
        public static void Prepare(SaveData save, MatchSettings ms)
        {
            if (ms.FindTeam(save.teamId) == null && ms.teams != null && ms.teams.Length > 0 && ms.teams[0] != null)
                save.teamId = ms.teams[0].id;
            if (save.season.matches.Count == 0)
                StartSeason(save, ms, Mathf.Max(1, save.season.seasonNumber));
            BackfillOthers(save, ms);
        }

        static void StartSeason(SaveData save, MatchSettings ms, int seasonNumber)
        {
            var season = new SeasonData { seasonNumber = seasonNumber };
            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks));
            var slots = new List<MatchRecord>();
            if (ms.teams != null)
            {
                foreach (var team in ms.teams)
                {
                    if (team == null || team.id == save.teamId) continue;
                    slots.Add(new MatchRecord { opponentId = team.id, opponentName = team.displayName, home = true });
                    slots.Add(new MatchRecord { opponentId = team.id, opponentName = team.displayName, home = false });
                }
            }
            for (int i = slots.Count - 1; i > 0; i--)   // 일정 순서 섞기
            {
                int j = rng.Next(i + 1);
                var tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp;
            }
            for (int i = 0; i < slots.Count; i++) { slots[i].round = i + 1; slots[i].seed = rng.Next(); }
            season.matches = slots;
            save.season = season;
            if (slots.Count == 0) Debug.LogError("Match Settings needs at least 2 teams.");
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
            season.nextMatchIndex++;
            return match;
        }

        // 같은 라운드에 내 경기에 나오지 않은 팀들끼리 짝을 지어 경기를 치릅니다.
        // 그래서 모든 팀이 라운드마다 정확히 한 경기씩 치르게 됩니다.
        static void GenerateOthers(MatchRecord match, SaveData save, MatchSettings ms)
        {
            match.others = new List<LeagueGame>();
            var pool = new List<TeamDefinition>();
            if (ms.teams != null)
                foreach (var t in ms.teams)
                    if (t != null && t.id != save.teamId && t.id != match.opponentId) pool.Add(t);

            var rng = new System.Random(match.seed ^ 0x5bd1e995);
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

        // 끝난 시즌 요약을 이력에 남기고 새 시즌을 시작합니다.
        // 이적이 합의되어 있으면 새 시즌부터 새 팀으로 뜁니다. 이적했다면 안내 문장을, 아니면 null을 돌려줍니다.
        public static string StartNextSeason(SaveData save, MatchSettings ms)
        {
            save.seasonHistory.Add(Summarize(save, ms));
            string message = null;
            if (!string.IsNullOrEmpty(save.pendingTeamId))
            {
                var target = ms.FindTeam(save.pendingTeamId);
                if (target != null)
                {
                    string from = TeamName(save, ms);
                    save.transfers.Add(new TransferRecord { afterSeason = save.season.seasonNumber, fromTeamName = from, toTeamName = target.displayName });
                    message = "Transferred: " + from + " -> " + target.displayName;
                    save.teamId = target.id;
                }
            }
            save.pendingTeamId = "";
            save.offers.Clear();
            StartSeason(save, ms, save.season.seasonNumber + 1);
            return message;
        }

        // ---------- 이적 ----------
        // 시즌이 끝나면 다른 팀 최대 3곳에서 제안을 받습니다. 새 제안이 생기면 true.
        public static bool EnsureOffers(SaveData save, MatchSettings ms, CareerSettings career)
        {
            if (!IsComplete(save) || save.offersSeason == save.season.seasonNumber) return false;
            save.offers = new List<TransferOffer>();
            save.pendingTeamId = "";
            save.offersSeason = save.season.seasonNumber;

            float rating = MatchSimulation.PositionRating(ms.Profile(save.player.position), save.player, career.attributeCap);
            var pool = new List<TeamDefinition>();
            if (ms.teams != null)
                foreach (var t in ms.teams)
                    if (t != null && t.id != save.teamId) pool.Add(t);

            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks) ^ save.season.seasonNumber);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            int count = Mathf.Min(3, pool.Count);
            for (int i = 0; i < count; i++)
            {
                var t = pool[i];
                save.offers.Add(new TransferOffer
                {
                    teamId = t.id, teamName = t.displayName, teamStrength = t.strength,
                    startChance = MatchSimulation.StartChance(t, rating), focus = t.focus
                });
            }
            return true;
        }

        public static TransferOffer SetPending(SaveData save, string teamId)
        {
            foreach (var offer in save.offers)
                if (offer.teamId == teamId) { save.pendingTeamId = teamId; return offer; }
            return null;
        }

        // ---------- 순위표 ----------
        public static List<StandingRow> Standings(SaveData save, MatchSettings ms)
        {
            var rows = new Dictionary<string, StandingRow>();
            var list = new List<StandingRow>();
            if (ms.teams != null)
            {
                foreach (var t in ms.teams)
                {
                    if (t == null || rows.ContainsKey(t.id)) continue;
                    var row = new StandingRow { id = t.id, name = t.displayName };
                    rows[t.id] = row; list.Add(row);
                }
            }
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
            list.Sort((a, b) =>
            {
                int c = b.Points.CompareTo(a.Points); if (c != 0) return c;
                c = b.GoalDiff.CompareTo(a.GoalDiff); if (c != 0) return c;
                c = b.goalsFor.CompareTo(a.goalsFor); if (c != 0) return c;
                return string.Compare(a.name, b.name, StringComparison.Ordinal);
            });
            return list;
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
            var r = new SeasonSummary { seasonNumber = season.seasonNumber };
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

        static string Ordinal(int n)
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

        // ---------- 화면에 보여줄 글 ----------
        public static string DescribeNext(SaveData save, MatchSettings ms)
        {
            var season = save.season;
            if (season.matches.Count == 0) return "No season available.";
            if (IsComplete(save))
            {
                var t = Summarize(save, ms);
                return "Season " + season.seasonNumber + " complete\n" + Ordinal(t.finalPosition) + " of " + Standings(save, ms).Count
                    + "   W" + t.wins + " D" + t.draws + " L" + t.losses + "   Avg rating " + t.averageRating.ToString("0.0");
            }
            var next = season.matches[season.nextMatchIndex];
            return "Season " + season.seasonNumber + "   Round " + next.round + "/" + season.matches.Count
                + "\nNext: vs " + next.opponentName + (next.home ? " (Home)" : " (Away)");
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
            if (!m.playerPlayed) return sb.Append("You did not play (bench).").ToString();
            sb.Append("Rating ").Append(m.rating.ToString("0.0")).Append('\n');
            sb.Append("Goals ").Append(m.playerGoals).Append("  Assists ").Append(m.playerAssists)
              .Append("  Shots ").Append(m.playerShots).Append('\n');
            sb.Append("Passes ").Append(m.passesCompleted).Append('/').Append(m.passesAttempted)
              .Append(" (").Append(passPercent).Append("%)  Defensive actions ").Append(m.defensiveActions);
            return sb.ToString();
        }

        public static string DescribeStandings(SaveData save, MatchSettings ms)
        {
            var table = Standings(save, ms);
            if (table.Count == 0) return "No teams.";
            var sb = new StringBuilder();
            for (int i = 0; i < table.Count; i++)
            {
                var r = table[i];
                if (i > 0) sb.Append('\n');
                sb.Append(i + 1).Append(". ").Append(r.name).Append(r.id == save.teamId ? " *" : "")
                  .Append("   P").Append(r.played).Append(" W").Append(r.won).Append(" D").Append(r.drawn).Append(" L").Append(r.lost)
                  .Append("  GD").Append(r.GoalDiff >= 0 ? "+" : "").Append(r.GoalDiff)
                  .Append("  ").Append(r.Points).Append("pts");
            }
            return sb.ToString();
        }

        public static string DescribeSeasonEnd(SaveData save, MatchSettings ms)
        {
            var t = Summarize(save, ms);
            int teams = Standings(save, ms).Count;
            var sb = new StringBuilder();
            sb.Append("Season ").Append(t.seasonNumber).Append(" final: ").Append(Ordinal(t.finalPosition))
              .Append(" of ").Append(teams).Append("  (").Append(t.points).Append(" pts)\n");
            sb.Append(t.finalPosition == 1 ? "You are the champion!\n" : "Champion: " + t.champion + "\n");
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
                if (!string.IsNullOrEmpty(h.teamName)) sb.Append(h.teamName).Append(", ");
                if (h.finalPosition > 0) sb.Append(Ordinal(h.finalPosition)).Append(", ");
                sb.Append("W").Append(h.wins).Append(" D").Append(h.draws).Append(" L").Append(h.losses)
                  .Append(", G").Append(h.playerGoals).Append(" A").Append(h.playerAssists)
                  .Append(", avg ").Append(h.averageRating.ToString("0.0"));
            }
            return sb.ToString();
        }
    }
}
