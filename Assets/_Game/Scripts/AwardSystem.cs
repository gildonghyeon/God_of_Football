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

        class Rival
        {
            public string name, teamId;
            public Position position;
            public int division, played, apps, goals, assists, defense;
            public float ratingSum;
            public float Avg => apps > 0 ? ratingSum / apps : 0f;
        }

        public static string KindName(TrophyKind kind)
        {
            switch (kind)
            {
                case TrophyKind.LeagueTitle: return "League Title";
                case TrophyKind.Promotion: return "Promotion";
                case TrophyKind.TopScorer: return "Top Scorer";
                case TrophyKind.TopAssists: return "Top Assists";
                case TrophyKind.TopDefender: return "Top Defender";
                case TrophyKind.BestEleven: return "Best XI";
                default: return "Player of the Year";
            }
        }

        static int BestElevenSlots(Position p) { return p == Position.Defender ? 4 : 3; }   // 4-3-3

        // 이 팀들의 포지션별 경쟁자가 한 시즌(서로 홈·원정)을 치른 기록. skipTeam의 skipPosition 자리는 내 선수라 비웁니다.
        static List<Rival> SimulateRivals(SaveData save, MatchSettings ms, CareerSettings career, List<TeamDefinition> teams, int division,
                                          string skipTeam, Position skipPosition)
        {
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
                            if (!m.playerPlayed) continue;
                            r.apps++; r.ratingSum += m.rating;
                            r.goals += m.playerGoals; r.assists += m.playerAssists; r.defense += m.defensiveActions;
                        }
                    }
                    list.Add(r);
                }
            return list;
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

            if (mine.finalPosition == 1) { Give(TrophyKind.LeagueTitle, mine.points + " pts"); lines.Add("* LEAGUE TITLE! Champions of " + ms.DivisionName(div) + "."); }
            if (div > 0 && season.nextDivision > 0 && season.nextDivision < div) { Give(TrophyKind.Promotion, "to Div " + season.nextDivision); lines.Add("* PROMOTION to " + ms.DivisionName(season.nextDivision) + "!"); }

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
                    Give(kind, myValue + " " + unit);
                    lines.Add("* " + label.ToUpperInvariant() + ": you, " + myValue + " " + unit + (best != null ? "  (2nd " + best.name + " " + bestValue + ")" : ""));
                }
                else if (best != null) lines.Add(label + ": " + best.name + " " + bestValue + "  (you " + myValue + ")");
            }
            StatAward(TrophyKind.TopScorer, "Top scorer", "goals", goals, r => r.goals);
            StatAward(TrophyKind.TopAssists, "Top assists", "assists", assists, r => r.assists);
            StatAward(TrophyKind.TopDefender, "Top defender", "def. actions", defense, r => r.defense);

            // 베스트 11: 내 포지션 평균 평점 상위 (출전 절반 이상)
            int slots = BestElevenSlots(pos);
            var same = rivals.FindAll(r => r.position == pos && r.played > 0 && r.apps >= BestElevenMinApps * r.played);
            same.Sort((a, b) => b.Avg.CompareTo(a.Avg));
            float cut = same.Count >= slots ? same[slots - 1].Avg : 0f;
            bool enough = mine.played > 0 && mine.appearances >= BestElevenMinApps * mine.played;
            if (enough && mine.averageRating >= cut) { Give(TrophyKind.BestEleven, pos + ", avg " + mine.averageRating.ToString("0.0")); lines.Add("* BEST XI (" + pos + "): selected, avg " + mine.averageRating.ToString("0.0")); }
            else lines.Add("Best XI (" + pos + "): " + (enough ? "missed, cut " + cut.ToString("0.0") + " (you " + mine.averageRating.ToString("0.0") + ")" : "not enough appearances"));

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
                Give(TrophyKind.PlayerOfTheYear, "score " + Mathf.RoundToInt(myScore));
                lines.Add("*** PLAYER OF THE YEAR: YOU! ***");
            }
            else if (poty != null)
            {
                var pt = ms.FindTeam(poty.teamId);
                lines.Add("Player of the Year: " + poty.name + " (" + (pt != null ? pt.displayName : poty.teamId) + ")");
            }
            if (won.Count == 0) lines.Insert(0, "No trophies this season.");
            return true;
        }

        // 시즌 종료 팝업에 붙일 시상 결과 (이번 시즌 계산 전이면 null)
        public static string DescribeAwards(SaveData save)
        {
            if (save.awardsSeason != save.season.seasonNumber || save.awardLines == null || save.awardLines.Count == 0) return null;
            return "AWARDS\n" + string.Join("\n", save.awardLines);
        }

        // 트로피 진열장: 종류별 개수와 최근 트로피
        public static string DescribeTrophyRoom(SaveData save)
        {
            var list = save.trophies;
            if (list == null || list.Count == 0) return "No trophies yet. Win titles and season awards to fill this room.";
            var counts = new int[System.Enum.GetValues(typeof(TrophyKind)).Length];
            foreach (var t in list) counts[(int)t.kind]++;
            var sb = new StringBuilder();
            sb.Append("Total ").Append(list.Count).Append(" trophies\n");
            int shown = 0;
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0) continue;
                sb.Append(KindName((TrophyKind)k)).Append(" x").Append(counts[k]);
                sb.Append(++shown % 2 == 0 ? "\n" : "   ");
            }
            if (shown % 2 == 1) sb.Append('\n');
            sb.Append("Recent:");
            for (int i = list.Count - 1, n = 0; i >= 0 && n < 6; i--, n++)
            {
                var t = list[i];
                sb.Append("\n C").Append(t.career).Append(" S").Append(t.season).Append(t.division > 0 ? " Div " + t.division : "")
                  .Append("  ").Append(KindName(t.kind)).Append(" (").Append(t.detail).Append(")");
            }
            return sb.ToString();
        }
    }
}
