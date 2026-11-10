using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // 시즌 시상. 시즌이 끝나면 한 번만 계산하고, 받은 트로피는 환생 뒤에도 남습니다 (보상은 트로피뿐).
    //  리그 우승 / 승격 / 득점왕 / 도움왕 / 수비왕 / 베스트 11 : 내가 뛴 리그 기준
    //  올해의 축구선수 : 모든 리그 통틀어 한 명 (높은 리그일수록 유리)
    // 다른 팀 선수는 저장하지 않으므로, 각 팀의 포지션별 주전 '경쟁자'를 시즌 번호로 고정된 난수로 만들어
    // 실제 경기 계산(MatchSimulation)으로 한 시즌을 치르게 한 뒤 내 기록과 비교합니다. 같은 시즌이면 언제 계산해도 같습니다.
    public static class AwardSystem
    {
        static readonly Position[] Positions = { Position.Forward, Position.Midfielder, Position.Defender };
        static readonly float[] DivisionWeight = { 1f, 0.85f, 0.72f, 0.6f, 0.5f };   // 올해의 축구선수: 1부부터의 점수 배율
        const float BestElevenMinApps = 0.5f;   // 베스트 11 후보가 되려면 이 비율 이상 출전
        const float StatAwardBonus = 4f;        // 올해의 축구선수 점수: 득점·도움·수비왕 하나당
        const float TitleBonus = 5f;            // 올해의 축구선수 점수: 리그 우승

        static readonly string Initials = "ABCDEFGHJKLMNOPRSTVW";
        static readonly string[] Surnames =
        {
            "Moreno", "Hart", "Silva", "Novak", "Okafor", "Brandt", "Costa", "Lindqvist", "Ruiz", "Becker",
            "Duarte", "Kovac", "Mensah", "Fischer", "Rossi", "Haaland", "Petrov", "Tanaka", "Walsh", "Dubois",
            "Santos", "Muller", "Varga", "Ibrahim", "Larsen", "Romero", "Kane", "Bianchi", "Nilsen", "Ortega",
            "Sato", "Keller", "Mbeki", "Andersen", "Quinn", "Ferreira", "Hughes", "Vidal", "Kim", "Zielinski",
        };

        // 경쟁자 한 경기의 기록 (시즌 도중 순위표용)
        struct MatchLine { public bool played; public int goals, assists, defense; public float rating; }

        class Rival
        {
            public string name, teamId;
            public Position position;
            public int division, played, apps, goals, assists, defense;
            public float ratingSum;
            public float Avg => apps > 0 ? ratingSum / apps : 0f;
            public readonly List<MatchLine> log = new List<MatchLine>();

            // 앞의 games경기까지만 친 기록 (시즌 도중 순위표용)
            public Rival Upto(int games)
            {
                var r = new Rival { name = name, teamId = teamId, position = position, division = division };
                for (int i = 0; i < log.Count && i < games; i++)
                {
                    var l = log[i]; r.played++;
                    if (!l.played) continue;
                    r.apps++; r.ratingSum += l.rating; r.goals += l.goals; r.assists += l.assists; r.defense += l.defense;
                }
                return r;
            }
        }

        // 같은 시즌 경쟁자 계산을 다시 하지 않도록 마지막 결과를 기억합니다 (시즌 도중 화면 갱신마다 부르므로)
        static string cacheKey;
        static List<Rival> cache;

        public static string KindName(TrophyKind kind)
        {
            switch (kind)
            {
                case TrophyKind.LeagueTitle: return "리그 우승";
                case TrophyKind.Promotion: return "승격";
                case TrophyKind.TopScorer: return "득점왕";
                case TrophyKind.TopAssists: return "도움왕";
                case TrophyKind.TopDefender: return "수비왕";
                case TrophyKind.BestEleven: return "베스트 11";
                default: return "올해의 축구선수";
            }
        }

        static int BestElevenSlots(Position p) { return p == Position.Defender ? 4 : 3; }   // 4-3-3

        // 이 팀들의 포지션별 경쟁자가 한 시즌(서로 홈·원정)을 치른 기록. skipTeam의 skipPosition 자리는 내 선수라 비웁니다.
        static List<Rival> SimulateRivals(SaveData save, MatchSettings ms, CareerSettings career, List<TeamDefinition> teams, int division,
                                          string skipTeam, Position skipPosition)
        {
            var key = save.season.seed + "|" + save.season.seasonNumber + "|" + division + "|" + skipTeam + "|" + skipPosition + "|" + career.attributeCap;
            foreach (var t in teams) key += "|" + t.id + ":" + t.requiredRating + ":" + t.strength;
            if (key == cacheKey && cache != null) return cache;
            var rng = new System.Random(unchecked(save.season.seed ^ (division * 104729) ^ 0x2545F491));
            var list = new List<Rival>();
            var used = new HashSet<string>();
            foreach (var team in teams)
                foreach (var pos in Positions)
                {
                    float rating = Mathf.Clamp(team.requiredRating + rng.Next(0, 13), 1f, career.attributeCap);   // 그 팀 요구 수준보다 조금 높은 주전
                    string name;
                    do name = Initials[rng.Next(Initials.Length)] + ". " + Surnames[rng.Next(Surnames.Length)]; while (!used.Add(name) && used.Count < 400);
                    int seed = rng.Next();
                    if (team.id == skipTeam && pos == skipPosition) continue;
                    var r = new Rival { name = name, teamId = team.id, position = pos, division = division };
                    var p = new PlayerData { playerName = name, position = pos, shooting = rating, passing = rating, dribbling = rating, defending = rating, stamina = rating };
                    var mr = new System.Random(seed);
                    foreach (var opp in teams)
                    {
                        if (opp.id == team.id) continue;
                        for (int leg = 0; leg < 2; leg++)
                        {
                            var m = new MatchRecord { home = leg == 0, seed = mr.Next() };
                            MatchSimulation.Play(m, p, team, opp, ms, career.attributeCap);
                            r.played++;
                            r.log.Add(new MatchLine { played = m.playerPlayed, goals = m.playerGoals, assists = m.playerAssists, defense = m.defensiveActions, rating = m.rating });
                            if (!m.playerPlayed) continue;
                            r.apps++; r.ratingSum += m.rating;
                            r.goals += m.playerGoals; r.assists += m.playerAssists; r.defense += m.defensiveActions;
                        }
                    }
                    list.Add(r);
                }
            cacheKey = key; cache = list;
            return list;
        }

        // ---------- 시즌 도중 개인 순위표 ----------
        // 지금까지 치른 라운드 수만큼 경쟁자 기록을 잘라 득점·도움·수비 순위와 베스트 11 기준을 보여 줍니다.
        // 시즌이 끝나면 시상 결과와 같은 숫자가 됩니다.
        public static string DescribeLeaders(SaveData save, MatchSettings ms, CareerSettings career)
        {
            var season = save.season;
            if (season == null || season.teamIds == null || season.teamIds.Count < 2) return "리그 정보가 없습니다.";
            int games = Mathf.Min(season.nextMatchIndex, season.matches.Count);
            if (games <= 0) return "첫 경기를 치르면 순위가 나옵니다.";
            var teams = new List<TeamDefinition>();
            foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) teams.Add(t); }
            var pos = save.player.position;
            var now = new List<Rival>();
            foreach (var r in SimulateRivals(save, ms, career, teams, season.division, save.teamId, pos)) now.Add(r.Upto(games));

            var me = new Rival { name = save.player.playerName, teamId = save.teamId, position = pos };
            for (int i = 0; i < games; i++)
            {
                var m = season.matches[i]; me.played++;
                if (!m.played || !m.playerPlayed) continue;
                me.apps++; me.ratingSum += m.rating; me.goals += m.playerGoals; me.assists += m.playerAssists; me.defense += m.defensiveActions;
            }

            var sb = new StringBuilder();
            sb.Append(season.matches.Count).Append("라운드 중 ").Append(games).Append("라운드 진행");
            Race(sb, ms, "득점 순위", now, me, r => r.goals);
            Race(sb, ms, "도움 순위", now, me, r => r.assists);
            Race(sb, ms, "수비 순위 (수비 횟수)", now, me, r => r.defense);

            int slots = BestElevenSlots(pos);
            var same = new List<Rival>();
            foreach (var r in now) if (r.position == pos && r.played > 0 && r.apps >= BestElevenMinApps * r.played) same.Add(r);
            same.Sort((a, b) => b.Avg.CompareTo(a.Avg));
            float cut = same.Count >= slots ? same[slots - 1].Avg : 0f;
            bool enough = me.apps >= BestElevenMinApps * me.played;
            sb.Append("\n\n베스트 11 (").Append(Ko.Pos(pos)).Append("): 기준 평점 ").Append(cut.ToString("0.0"))
              .Append(", " + me.name + " ").Append(me.apps > 0 ? me.Avg.ToString("0.0") : "-")
              .Append(!enough ? "  (출전 부족)" : me.Avg >= cut ? "  선정권" : "  탈락권");
            return sb.ToString();
        }

        // 순위 상위 3명 + 3위 밖이면 내 순위. 같은 기록이면 내가 앞 (시상도 동률이면 수상)
        static void Race(StringBuilder sb, MatchSettings ms, string title, List<Rival> rivals, Rival me, System.Func<Rival, int> value)
        {
            var all = new List<Rival>(rivals) { me };
            all.Sort((a, b) =>
            {
                int c = value(b).CompareTo(value(a));
                if (c != 0) return c;
                if (a == me) return -1;
                if (b == me) return 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            sb.Append("\n\n").Append(title);
            int myRank = all.IndexOf(me) + 1;
            for (int i = 0; i < all.Count && i < 3; i++) sb.Append('\n').Append(Line(ms, i + 1, all[i], me, value));
            if (myRank > 3) sb.Append("\n  ...\n").Append(Line(ms, myRank, me, me, value));
        }

        static string Line(MatchSettings ms, int rank, Rival r, Rival me, System.Func<Rival, int> value)
        {
            var t = ms.FindTeam(r.teamId);
            return rank + ". " + r.name + " (" + (t != null ? t.displayName : r.teamId) + ")" + (r == me ? " *" : "") + "  " + value(r);   // 내 선수는 * 표시
        }

        static Rival Best(List<Rival> list, System.Func<Rival, int> value)
        {
            Rival best = null;
            foreach (var r in list) if (best == null || value(r) > value(best)) best = r;
            return best;
        }

        // 순위표에서 몇 위인지 (1부터, 없으면 0)
        static int PositionIn(List<string> order, string teamId)
        {
            int i = order.IndexOf(teamId);
            return i >= 0 ? i + 1 : 0;
        }

        // 시즌이 끝났으면 시상을 한 번만 계산해 트로피와 안내 문장을 남깁니다. 새로 계산했으면 true.
        public static bool EnsureAwards(SaveData save, MatchSettings ms, CareerSettings career)
        {
            if (!SeasonSystem.IsComplete(save) || save.awardsSeason == save.season.seasonNumber) return false;
            SeasonSystem.EnsureSeasonEnd(save, ms);
            var season = save.season;
            save.awardsSeason = season.seasonNumber;
            save.awardLines = new List<string>();
            if (save.trophies == null) save.trophies = new List<TrophyRecord>();

            // 내 시즌 기록
            int goals = 0, assists = 0, defense = 0;
            foreach (var m in season.matches)
                if (m.played && m.playerPlayed) { goals += m.playerGoals; assists += m.playerAssists; defense += m.defensiveActions; }
            var mine = SeasonSystem.Summarize(save, ms);
            var table = SeasonSystem.Standings(save, ms);
            var pos = save.player.position;
            string team = SeasonSystem.TeamName(save, ms);
            int div = season.division;
            var lines = save.awardLines;
            var won = new List<TrophyKind>();
            void Give(TrophyKind kind, string detail)
            {
                won.Add(kind);
                save.trophies.Add(new TrophyRecord { kind = kind, career = save.rebirthCount + 1, season = season.seasonNumber, division = div, teamName = team, detail = detail });
            }

            if (mine.finalPosition == 1) { Give(TrophyKind.LeagueTitle, "승점 " + mine.points); lines.Add("* 리그 우승! " + ms.DivisionName(div) + " 챔피언!"); }
            if (div > 0 && season.nextDivision > 0 && season.nextDivision < div) { Give(TrophyKind.Promotion, season.nextDivision + "부로 승격"); lines.Add("* 승격! " + ms.DivisionName(season.nextDivision) + "로 올라갑니다."); }

            // 같은 리그 경쟁자
            var leagueTeams = new List<TeamDefinition>();
            foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) leagueTeams.Add(t); }
            var rivals = SimulateRivals(save, ms, career, leagueTeams, div, save.teamId, pos);

            int statWins = 0;
            void StatAward(TrophyKind kind, string label, string unit, int myValue, System.Func<Rival, int> value)
            {
                var best = Best(rivals, value);
                int bestValue = best != null ? value(best) : 0;
                if (myValue > 0 && myValue >= bestValue)
                {
                    statWins++;
                    Give(kind, myValue + unit);
                    lines.Add("* " + label + ": " + save.player.playerName + ", " + myValue + unit + (best != null ? "  (2위 " + best.name + " " + bestValue + ")" : ""));
                }
                else if (best != null) lines.Add(label + ": " + best.name + " " + bestValue + "  (" + save.player.playerName + " " + myValue + ")");
            }
            StatAward(TrophyKind.TopScorer, "득점왕", "골", goals, r => r.goals);
            StatAward(TrophyKind.TopAssists, "도움왕", "도움", assists, r => r.assists);
            StatAward(TrophyKind.TopDefender, "수비왕", "회 수비", defense, r => r.defense);

            // 베스트 11: 내 포지션 평균 평점 상위 (출전 절반 이상)
            int slots = BestElevenSlots(pos);
            var same = rivals.FindAll(r => r.position == pos && r.played > 0 && r.apps >= BestElevenMinApps * r.played);
            same.Sort((a, b) => b.Avg.CompareTo(a.Avg));
            float cut = same.Count >= slots ? same[slots - 1].Avg : 0f;
            bool enough = mine.played > 0 && mine.appearances >= BestElevenMinApps * mine.played;
            if (enough && mine.averageRating >= cut) { Give(TrophyKind.BestEleven, Ko.Pos(pos) + ", 평점 " + mine.averageRating.ToString("0.0")); lines.Add("* 베스트 11 (" + Ko.Pos(pos) + "): 선정! 평점 " + mine.averageRating.ToString("0.0")); }
            else lines.Add("베스트 11 (" + Ko.Pos(pos) + "): " + (enough ? "탈락, 기준 " + cut.ToString("0.0") + " (" + save.player.playerName + " " + mine.averageRating.ToString("0.0") + ")" : "출전 부족"));

            // 올해의 축구선수: 1부 경쟁자 중 최고와 비교 (내 점수는 리그가 낮을수록 깎임)
            float weight = div >= 1 && div <= DivisionWeight.Length ? DivisionWeight[div - 1] : DivisionWeight[DivisionWeight.Length - 1];
            float myScore = (SeasonSystem.PerformanceScore(mine, table.Count) + StatAwardBonus * statWins + (mine.finalPosition == 1 ? TitleBonus : 0f)) * weight;
            var topTeams = new List<TeamDefinition>();
            if (season.topTeamIds != null) foreach (var id in season.topTeamIds) { var t = ms.FindTeam(id); if (t != null) topTeams.Add(t); }
            if (topTeams.Count < 2) topTeams = div == 1 ? leagueTeams : SeasonSystem.TeamsInDivision(save, ms, 1);   // 예전 시즌: 지금 1부로 대신
            List<Rival> topRivals;
            List<string> topOrder;
            if (div == 1) { topRivals = rivals; topOrder = new List<string>(); foreach (var row in table) topOrder.Add(row.id); }
            else { topRivals = SimulateRivals(save, ms, career, topTeams, 1, null, pos); topOrder = SeasonSystem.FinalOrder(save, ms, topTeams, 1); }
            var topGoals = Best(topRivals, r => r.goals); var topAssists = Best(topRivals, r => r.assists); var topDefense = Best(topRivals, r => r.defense);
            Rival poty = null; float potyScore = 0f;
            foreach (var r in topRivals)
            {
                var sum = new SeasonSummary { played = r.played, appearances = r.apps, averageRating = r.Avg, finalPosition = PositionIn(topOrder, r.teamId) };
                float score = SeasonSystem.PerformanceScore(sum, topOrder.Count)
                            + StatAwardBonus * ((r == topGoals ? 1 : 0) + (r == topAssists ? 1 : 0) + (r == topDefense ? 1 : 0))
                            + (sum.finalPosition == 1 ? TitleBonus : 0f);
                if (poty == null || score > potyScore) { poty = r; potyScore = score; }
            }
            if (myScore >= potyScore && mine.appearances > 0)
            {
                Give(TrophyKind.PlayerOfTheYear, "점수 " + Mathf.RoundToInt(myScore));
                lines.Add("*** 올해의 축구선수: " + save.player.playerName + "! ***");
            }
            else if (poty != null)
            {
                var pt = ms.FindTeam(poty.teamId);
                lines.Add("올해의 축구선수: " + poty.name + " (" + (pt != null ? pt.displayName : poty.teamId) + ")");
            }
            if (won.Count == 0) lines.Insert(0, "이번 시즌 트로피 없음.");
            return true;
        }

        // 이번 시즌(지금 커리어)에 받은 트로피 (수여 연출용)
        public static List<TrophyRecord> SeasonTrophies(SaveData save)
        {
            var list = new List<TrophyRecord>();
            if (save.trophies == null || save.season == null) return list;
            foreach (var t in save.trophies)
                if (t.career == save.rebirthCount + 1 && t.season == save.season.seasonNumber) list.Add(t);
            return list;
        }

        // 시즌 종료 팝업에 붙일 시상 결과 (이번 시즌 계산 전이면 null)
        public static string DescribeAwards(SaveData save)
        {
            if (save.awardsSeason != save.season.seasonNumber || save.awardLines == null || save.awardLines.Count == 0) return null;
            return "시상\n" + string.Join("\n", save.awardLines);
        }

        // 트로피 진열장: 종류별 개수와 최근 트로피
        public static string DescribeTrophyRoom(SaveData save)
        {
            var list = save.trophies;
            if (list == null || list.Count == 0) return "아직 트로피가 없습니다. 우승과 시즌 시상으로 채워 보세요.";
            var counts = new int[System.Enum.GetValues(typeof(TrophyKind)).Length];
            foreach (var t in list) counts[(int)t.kind]++;
            var sb = new StringBuilder();
            sb.Append("총 ").Append(list.Count).Append("개\n");
            int shown = 0;
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0) continue;
                sb.Append(KindName((TrophyKind)k)).Append(" x").Append(counts[k]);
                sb.Append(++shown % 2 == 0 ? "\n" : "   ");
            }
            if (shown % 2 == 1) sb.Append('\n');
            sb.Append("최근:");
            for (int i = list.Count - 1, n = 0; i >= 0 && n < 6; i--, n++)
            {
                var t = list[i];
                sb.Append("\n 커리어").Append(t.career).Append(" 시즌").Append(t.season).Append(t.division > 0 ? " " + t.division + "부" : "")
                  .Append("  ").Append(KindName(t.kind)).Append(" (").Append(t.detail).Append(")");
            }
            return sb.ToString();
        }
    }
}
