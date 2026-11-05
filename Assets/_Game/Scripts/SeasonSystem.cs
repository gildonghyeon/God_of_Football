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

        public static List<TeamDefinition> TeamsInDivision(SaveData save, MatchSettings ms, int division) { return TeamsIn(save, ms, division); }

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
            foreach (var t in TeamsIn(save, ms, 1)) season.topTeamIds.Add(t.id);   // 시상(올해의 축구선수)용
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
            match.wage = FacilitySystem.MatchPay(save, ms);   // 경기 한 번 = 1주 (에이전트 보너스 포함)
            save.money += match.wage;
            season.nextMatchIndex++;
            return match;
        }

        // 친선 경기: 같은 부의 다른 팀과 한 경기. 시즌 일정·순위에는 들어가지 않고 주급은 똑같이 받습니다.
        // round가 0인 기록이 친선 경기입니다. 상대 팀이 없으면 null.
        public static MatchRecord PlayFriendly(SaveData save, MatchSettings ms, CareerSettings career, System.Random rng)
        {
            var own = ms.FindTeam(save.teamId);
            if (own == null) return null;
            var pool = new List<TeamDefinition>();
            foreach (var t in SeasonTeams(save, ms)) if (t.id != save.teamId) pool.Add(t);
            if (pool.Count == 0) return null;
            var opp = pool[rng.Next(pool.Count)];
            var match = new MatchRecord { round = 0, opponentId = opp.id, opponentName = opp.displayName, home = rng.Next(2) == 0, seed = rng.Next() };
            MatchSimulation.Play(match, save.player, own, opp, ms, career.attributeCap);
            EnsureContract(save, ms, career);
            match.wage = FacilitySystem.MatchPay(save, ms);   // 친선 경기도 1주치
            save.money += match.wage;
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
            return FinalOrder(save, ms, TeamsIn(save, ms, division), division);
        }

        // 이 팀들로 이 부의 시즌을 전력만으로 계산한 최종 순위 (승강 계산과 같은 결과)
        public static List<string> FinalOrder(SaveData save, MatchSettings ms, List<TeamDefinition> teams, int division)
        {
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
            AwardSystem.EnsureAwards(save, ms, career);   // 시상을 건너뛰지 않도록
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
            save.transferDecided = false;
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

        // ---------- 시즌 성적 점수 ----------
        // 이적 제안은 능력치(선발로 뛸 수 있는 팀인지)와 이번 시즌 성적 점수로 정해집니다.
        // 점수 0~100 = 평균 평점 60점 + 출전 비율 25점 + 팀 순위 15점
        public static float PerformanceScore(SeasonSummary s, int teamCount)
        {
            if (s == null || s.played <= 0) return 0f;
            float ratingPart = s.appearances > 0 ? Mathf.Clamp01((s.averageRating - 5.8f) / 2.0f) * 60f : 0f;   // 평균 5.8 이하 0점, 7.8 이상 만점
            float appsPart = 25f * s.appearances / s.played;
            float teamPart = teamCount > 1 && s.finalPosition > 0 ? 15f * (1f - (s.finalPosition - 1f) / (teamCount - 1f)) : 0f;
            return Mathf.Round(ratingPart + appsPart + teamPart);
        }

        public static string Grade(float score)
        {
            if (score >= 85) return "S";
            if (score >= 70) return "A";
            if (score >= 55) return "B";
            if (score >= 40) return "C";
            if (score >= 25) return "D";
            return "F";
        }

        // 부 차이에 따라 제안에 필요한 성적 점수 (위로 2단계, 1단계, 같은 부, 아래 1단계)
        // 이 점수에서 제안 확률 50%, 10점 높으면 100%, 10점 낮으면 0%
        static float OfferNeed(int divisionGap)
        {
            if (divisionGap <= -2) return 80f;
            if (divisionGap == -1) return 62f;
            if (divisionGap == 0) return 42f;
            return 22f;
        }

        // 성적 등급별로 받을 수 있는 제안 수: S·A 3곳, B 2곳, C 1곳, D 절반 확률로 1곳, F 없음
        static int MaxOffers(float score, System.Random rng)
        {
            if (score >= 70) return 3;
            if (score >= 55) return 2;
            if (score >= 40) return 1;
            if (score >= 25) return rng.NextDouble() < 0.5 ? 1 : 0;
            return 0;
        }

        public static float OfferChance(float score, int divisionGap)
        {
            return Mathf.Clamp01(0.5f + (score - OfferNeed(divisionGap)) / 20f);
        }

        // 시즌이 끝나면 최대 3곳에서 제안을 받습니다. 성적이 좋을수록 많은 팀이, 높은 부의 팀이 제안합니다.
        // 성적이 나쁘면 제안이 하나도 없을 수 있습니다 (그러면 지금 팀에 남음). 새로 정했으면 true.
        public static bool EnsureOffers(SaveData save, MatchSettings ms, CareerSettings career)
        {
            if (!IsComplete(save)) return false;
            EnsureSeasonEnd(save, ms);
            if (save.offersSeason == save.season.seasonNumber) return false;
            save.offers = new List<TransferOffer>();
            save.pendingTeamId = "";
            save.transferDecided = false;
            save.offersSeason = save.season.seasonNumber;

            float rating = PlayerRating(save, ms, career);
            float score = Mathf.Min(100f, PerformanceScore(Summarize(save, ms), Standings(save, ms).Count) + FacilitySystem.ScoreBonus(save, ms));   // 에이전트 보너스 포함
            save.offersScore = Mathf.RoundToInt(score);
            int myDivision = DivisionOf(save, ms, save.teamId);
            var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks) ^ save.season.seasonNumber);
            var picks = new List<TeamDefinition>();
            if (ms.teams != null)
                foreach (var t in ms.teams)
                {
                    if (t == null || t.id == save.teamId) continue;
                    int d = DivisionOf(save, ms, t.id);
                    if (d < myDivision - 2 || d > myDivision + 1) continue;
                    if (MatchSimulation.StartChance(t, rating) < OfferMinStartChance) continue;   // 선발로 못 뛸 팀은 제안하지 않음
                    if (rng.NextDouble() < OfferChance(score, d - myDivision)) picks.Add(t);      // 성적을 보고 관심을 보인 팀만
                }
            Shuffle(picks, rng);
            picks.Sort((a, b) => DivisionOf(save, ms, a.id).CompareTo(DivisionOf(save, ms, b.id)));   // 높은 부부터
            int maxOffers = MaxOffers(score, rng);
            for (int i = 0; i < picks.Count && save.offers.Count < maxOffers; i++)
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

        // 이적 제안 안내 한 줄 (이번 시즌 제안을 정하기 전이면 null)
        public static string DescribeOffers(SaveData save)
        {
            if (!IsComplete(save) || save.offersSeason != save.season.seasonNumber) return null;
            string grade = save.offersScore >= 0 ? "Season grade " + Grade(save.offersScore) + " (" + save.offersScore + "/100). " : "";
            int n = save.offers != null ? save.offers.Count : 0;
            if (n == 0) return grade + "No clubs made an offer. Play better next season.";
            return grade + n + (n == 1 ? " club made an offer." : " clubs made offers.");
        }

        // 이번 제안에 대한 선택을 이미 확정했는지 (예전 저장에서 이적이 합의되어 있던 것도 확정으로 봄)
        public static bool TransferDecided(SaveData save)
        {
            return save.transferDecided || !string.IsNullOrEmpty(save.pendingTeamId);
        }

        public static TransferOffer FindOffer(SaveData save, string teamId)
        {
            if (save.offers != null) foreach (var offer in save.offers) if (offer.teamId == teamId) return offer;
            return null;
        }

        // 이 팀으로의 이적을 확정합니다. 이미 확정했거나 제안이 없으면 null (다시 고를 수 없음)
        public static TransferOffer SetPending(SaveData save, string teamId)
        {
            if (TransferDecided(save)) return null;
            var offer = FindOffer(save, teamId);
            if (offer == null) return null;
            save.pendingTeamId = teamId;
            save.transferDecided = true;
            return offer;
        }

        // 지금 팀에 남기로 확정합니다. 이미 확정했으면 false
        public static bool ConfirmStay(SaveData save)
        {
            if (TransferDecided(save)) return false;
            save.pendingTeamId = "";
            save.transferDecided = true;
            return true;
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

        public static string TeamName(SaveData save, MatchSettings ms)
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
            sb.Append(m.round > 0 ? "Round " + m.round : "Friendly").Append(": ").Append(TeamName(save, ms)).Append(' ')
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
            string awards = AwardSystem.DescribeAwards(save);
            if (awards != null) sb.Append("\n\n").Append(awards);
            string offers = DescribeOffers(save);
            if (offers != null) sb.Append(awards != null ? "\n\n" : "\n").Append(offers).Append(save.offers.Count > 0 ? " See CAREER." : "");
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
