using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // 시즌 시상. 시즌이 끝나면 한 번만 계산하고, 받은 트로피는 환생 뒤에도 남습니다 (보상은 트로피뿐).
    //  리그 우승 / 승격 / 득점왕 / 도움왕 / 수비왕 / 베스트 11 : 내가 뛴 리그 기준
    //  올해의 축구선수 : 모든 리그 통틀어 한 명 (높은 리그일수록 유리)
    // 각 팀 선수단(SquadSystem)의 주전 필드 선수 10명이 실제 경기 계산(MatchSimulation)으로 한 시즌을 치른 기록을
    // 시즌 시드로 고정해 만들고 내 기록과 비교합니다. 같은 시즌이면 언제 계산해도 같습니다.
    // 내 팀 동료의 골·도움은 내 경기 기록에 남은 득점자(GoalNote)로 셉니다. 내 자리의 주전은 내가 대신합니다.
    public static class AwardSystem
    {
        static readonly Position[] Positions = { Position.Forward, Position.Midfielder, Position.Defender, Position.Goalkeeper };
        static readonly float[] DivisionWeight = { 1f, 0.85f, 0.72f, 0.6f, 0.5f };   // 올해의 축구선수: 1부부터의 점수 배율
        const float BestElevenMinApps = 0.5f;   // 베스트 11 후보가 되려면 이 비율 이상 출전
        const float StatAwardBonus = 4f;        // 올해의 축구선수 점수: 득점·도움·수비왕 하나당
        const float TitleBonus = 5f;            // 올해의 축구선수 점수: 리그 우승


        // 경쟁자 한 경기의 기록 (시즌 도중 순위표용)
        struct MatchLine { public bool played; public int goals, assists, defense, saves, conceded; public float rating; }   // conceded: 그 경기 팀 실점

        class Rival
        {
            public string name, teamId;
            public Position position;
            public int division, played, apps, goals, assists, defense, saves;   // saves: 골키퍼 선방
            public int conceded, cleanSheets;                                   // 뛴 경기의 팀 실점, 무실점 경기 (골키퍼 순위)
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
                    r.apps++; r.ratingSum += l.rating; r.goals += l.goals; r.assists += l.assists; r.defense += l.defense; r.saves += l.saves;
                    r.conceded += l.conceded; if (l.conceded == 0) r.cleanSheets++;
                }
                return r;
            }
        }

        // 같은 시즌 경쟁자 계산을 다시 하지 않도록 결과를 기억합니다 (시즌 도중 화면 갱신마다 부르므로)
        static readonly Dictionary<string, List<Rival>> cache = new Dictionary<string, List<Rival>>();

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

        static int BestElevenSlots(Position p) { return p == Position.Goalkeeper ? 1 : p == Position.Defender ? 4 : 3; }   // 4-3-3 + 골키퍼

        // 이 팀들의 주전 필드 선수(영상 자리 1~10)가 한 시즌(서로 홈·원정)을 치른 기록. skipTeam의 skipSlot 자리는 내 선수라 비웁니다.
        static List<Rival> SimulateRivals(SaveData save, MatchSettings ms, CareerSettings career, List<TeamDefinition> teams, int division,
                                          string skipTeam, int skipSlot)
        {
            var key = save.season.seed + "|" + save.season.seasonNumber + "|" + division + "|" + skipTeam + "|" + skipSlot + "|" + career.attributeCap + "|sq" + save.squadSeason;
            foreach (var t in teams) key += "|" + t.id + ":" + t.requiredRating + ":" + t.strength;
            if (cache.TryGetValue(key, out var hit)) return hit;
            var list = new List<Rival>();
            foreach (var team in teams)
            {
                // 나만의 팀은 내가 정한 1군이 늘 뛰므로 요구 평점을 0으로 (선발에서 빠지는 일 없이)
                var simTeam = save.teamMode && team.id == ClubSystem.ClubId
                    ? new TeamDefinition { id = team.id, displayName = team.displayName, division = team.division, strength = team.strength, requiredRating = 0f, focus = team.focus } : team;
                var xi = SquadSystem.Starters(save, ms, team.id);
                for (int slot = 0; slot < xi.Length; slot++)   // 0번은 골키퍼 (베스트 11 골키퍼 자리)
                {
                    var sp = xi[slot];
                    if (sp == null || (team.id == skipTeam && slot == skipSlot)) continue;
                    var pos = SquadSystem.ToPosition(sp.role); SlotKind kind = TacticSystem.KindAt(TacticSystem.Of(team).formation, slot);   // 포메이션 속 역할
                    float rating = Mathf.Clamp(sp.rating, 1f, career.attributeCap);
                    int seed = unchecked(save.season.seed ^ (division * 104729) ^ (sp.id * 7919) ^ 0x2545F491);
                    string name = sp.name;
                    var r = new Rival { name = name, teamId = team.id, position = pos, division = division };
                    var p = new PlayerData { playerName = name, position = pos, shooting = rating, passing = rating, dribbling = rating, defending = rating, stamina = rating, goalkeeping = rating };
                    var mr = new System.Random(seed);
                    foreach (var opp in teams)
                    {
                        if (opp.id == team.id) continue;
                        for (int leg = 0; leg < 2; leg++)
                        {
                            var m = new MatchRecord { home = leg == 0, seed = mr.Next() };
                            MatchSimulation.Play(m, p, simTeam, opp, ms, career.attributeCap, kind);
                            r.played++;
                            r.log.Add(new MatchLine { played = m.playerPlayed, goals = m.playerGoals, assists = m.playerAssists, defense = pos == Position.Goalkeeper ? 0 : m.defensiveActions, saves = pos == Position.Goalkeeper ? m.defensiveActions : 0, conceded = m.goalsAgainst, rating = m.rating });   // 골키퍼의 선방은 수비 순위에서 뺌
                            if (!m.playerPlayed) continue;
                            r.apps++; r.ratingSum += m.rating;
                            r.goals += m.playerGoals; r.assists += m.playerAssists; if (pos != Position.Goalkeeper) r.defense += m.defensiveActions; else r.saves += m.defensiveActions;
                            r.conceded += m.goalsAgainst; if (m.goalsAgainst == 0) r.cleanSheets++;
                        }
                    }
                    list.Add(r);
                }
            }
            if (cache.Count > 8) cache.Clear();
            cache[key] = list;
            return list;
        }

        // 앞의 games경기까지 자른 복사본. 내 팀 동료의 골·도움은 내 경기 기록의 득점자로 셉니다.
        static List<Rival> Snapshot(List<Rival> src, int games, SaveData save)
        {
            var list = new List<Rival>(src.Count);
            Dictionary<string, int> goals = null, assists = null;
            foreach (var r in src)
            {
                var c = r.Upto(games);
                if (r.teamId == save.teamId)
                {
                    if (goals == null) TallyOwnTeam(save, games, out goals, out assists);
                    c.goals = goals.TryGetValue(c.name, out int g) ? g : 0;
                    c.assists = assists.TryGetValue(c.name, out int a) ? a : 0;
                }
                list.Add(c);
            }
            return list;
        }

        static void TallyOwnTeam(SaveData save, int games, out Dictionary<string, int> goals, out Dictionary<string, int> assists)
        {
            goals = new Dictionary<string, int>(); assists = new Dictionary<string, int>();
            var matches = save.season.matches;
            for (int i = 0; i < games && i < matches.Count; i++)
            {
                if (matches[i].goalNotes == null) continue;
                foreach (var n in matches[i].goalNotes)
                {
                    if (!n.ours) continue;
                    if (!string.IsNullOrEmpty(n.scorer)) goals[n.scorer] = (goals.TryGetValue(n.scorer, out int g) ? g : 0) + 1;
                    if (!string.IsNullOrEmpty(n.assist)) assists[n.assist] = (assists.TryGetValue(n.assist, out int a) ? a : 0) + 1;
                }
            }
        }

        // 팀 정보 화면용: 이 팀 주전들의 이번 시즌 기록 (이름 → 기록)
        public static Dictionary<string, SeasonLine> TeamSeasonStats(SaveData save, MatchSettings ms, CareerSettings career, string teamId, out int division)
        {
            var result = new Dictionary<string, SeasonLine>();
            var season = save.season;
            division = SeasonSystem.DivisionOf(save, ms, teamId);
            if (season == null) return result;
            int games = Mathf.Min(season.nextMatchIndex, season.matches.Count);
            List<TeamDefinition> teams;
            string skipTeam = null;
            int div;
            if (season.teamIds != null && season.teamIds.Contains(teamId))
            {
                teams = new List<TeamDefinition>();
                foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) teams.Add(t); }
                div = season.division; skipTeam = save.teamMode ? null : save.teamId; division = season.division;   // 나만의 팀 모드에는 내 선수 자리가 없음
            }
            else { div = division; teams = SeasonSystem.TeamsInDivision(save, ms, div); }
            if (games <= 0 || teams.Count < 2) return result;
            var rivals = Snapshot(SimulateRivals(save, ms, career, teams, div, skipTeam, SquadSystem.MySlot(save, ms)), games, save);
            foreach (var r in rivals)
                if (r.teamId == teamId)
                    result[r.name] = new SeasonLine { goals = r.goals, assists = r.assists, apps = r.apps, avg = r.Avg, defense = r.defense, saves = r.saves };
            return result;
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
            now.AddRange(Snapshot(SimulateRivals(save, ms, career, teams, season.division, save.teamId, SquadSystem.MySlot(save, ms)), games, save));

            var me = new Rival { name = save.player.playerName, teamId = save.teamId, position = pos };
            for (int i = 0; i < games; i++)
            {
                var m = season.matches[i]; me.played++;
                if (!m.played || !m.playerPlayed) continue;
                me.apps++; me.ratingSum += m.rating; me.goals += m.playerGoals; me.assists += m.playerAssists; if (pos != Position.Goalkeeper) me.defense += m.defensiveActions;
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

        // 경기 탭의 개인 순위: 항목별 순위(상위 5명 + 내 순위)와 베스트 11 카드
        //  득점 · 도움 · 공격포인트 · 평점 · 수비 · 선방, 포지션과 상관없이 리그 전체 선수와 비교
        public static List<LeaderBlockView> LeadersView(SaveData save, MatchSettings ms, CareerSettings career, out string header, out BestElevenSquadView best)
        {
            best = null;
            var season = save.season;
            if (season == null || season.teamIds == null || season.teamIds.Count < 2) { header = "리그 정보가 없습니다."; return null; }
            int games = Mathf.Min(season.nextMatchIndex, season.matches.Count);
            if (games <= 0) { header = "첫 경기를 치르면 순위가 나옵니다."; return null; }
            var teams = new List<TeamDefinition>();
            foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) teams.Add(t); }
            var pos = save.player.position;
            bool keeper = pos == Position.Goalkeeper;
            var now = Snapshot(SimulateRivals(save, ms, career, teams, season.division, save.teamId, SquadSystem.MySlot(save, ms)), games, save);
            var me = new Rival { name = save.player.playerName, teamId = save.teamId, position = pos };
            for (int i = 0; i < games; i++)
            {
                var m = season.matches[i]; me.played++;
                if (!m.played || !m.playerPlayed) continue;
                me.apps++; me.ratingSum += m.rating; me.goals += m.playerGoals; me.assists += m.playerAssists;
                if (keeper) me.saves += m.defensiveActions; else me.defense += m.defensiveActions;
                me.conceded += m.goalsAgainst; if (m.goalsAgainst == 0) me.cleanSheets++;
            }
            header = season.matches.Count + "라운드 중 " + games + "라운드 진행";
            System.Func<Rival, bool> enoughApps = r => r.played > 0 && r.apps >= BestElevenMinApps * r.played;
            System.Func<Rival, string> perGame(System.Func<Rival, int> v) => r => "출전 " + r.apps + (r.apps > 0 ? " · 경기당 " + (v(r) / (float)r.apps).ToString("0.0#") : "");
            var blocks = new List<LeaderBlockView>();
            // 포지션과 상관없이 같은 항목, 모두 리그 전체 선수와 비교 (골키퍼도 수비수로 뛰면 수비를, 필드 선수도 골문을 지키면 선방을 쌓음)
            blocks.Add(Ranking(ms, "득점", now, me, r => r.goals, r => r.goals.ToString(), perGame(r => r.goals), null));
            blocks.Add(Ranking(ms, "도움", now, me, r => r.assists, r => r.assists.ToString(), perGame(r => r.assists), null));
            blocks.Add(Ranking(ms, "공격포인트", now, me, r => r.goals + r.assists, r => (r.goals + r.assists).ToString(), r => r.goals + "골 " + r.assists + "도움", null));
            blocks.Add(Ranking(ms, "평점", now, me, r => r.Avg, r => r.apps > 0 ? r.Avg.ToString("0.00") : "-", r => "출전 " + r.apps, enoughApps));
            blocks.Add(Ranking(ms, "수비", now, me, r => r.defense, r => r.defense.ToString(), perGame(r => r.defense), null));
            blocks.Add(Ranking(ms, "선방", now, me, r => r.saves, r => r.saves.ToString(), perGame(r => r.saves), null));

            // 시즌이 끝나면 베스트 11 스쿼드 (나도 후보)
            if (SeasonSystem.IsComplete(save))
            {
                var pool = new List<Rival>(now) { me };
                best = PickBestEleven(ms, pool, new HashSet<Rival> { me }, ms.DivisionName(season.division) + " · 시즌 " + season.seasonNumber);
            }
            return blocks;
        }

        // 나만의 팀 개인 순위: 리그 전체 선수와 비교. 우리 팀 선수(경기마다 쌓은 실제 기록)는 금색 줄로 보이고, 5위 밖이어도 순위와 함께 붙음
        public static List<LeaderBlockView> ClubLeadersView(SaveData club, MatchSettings ms, CareerSettings career, Dictionary<string, SeasonLine> lines,
                                                            List<SquadPlayer> squad, out string header, out BestElevenSquadView best)
        {
            best = null;
            var season = club.season;
            if (season == null || season.teamIds == null || season.teamIds.Count < 2) { header = "리그 정보가 없습니다."; return null; }
            int games = Mathf.Min(season.nextMatchIndex, season.matches.Count);
            if (games <= 0) { header = "첫 경기를 치르면 순위가 나옵니다."; return null; }
            var teams = new List<TeamDefinition>();
            foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) teams.Add(t); }
            // 다른 팀 선수들: 시상과 같은 계산 (우리 팀은 실제 기록을 쓰므로 계산에서 뺌)
            var now = Snapshot(SimulateRivals(club, ms, career, teams, season.division, null, -1), games, club).FindAll(r => r.teamId != ClubSystem.ClubId);
            var ours = new List<Rival>();
            foreach (var p in squad)
            {
                if (!lines.TryGetValue(p.name, out var l) || l.apps <= 0) continue;
                ours.Add(new Rival { name = p.name, teamId = ClubSystem.ClubId, position = SquadSystem.ToPosition(p.role), played = games, apps = l.apps,
                                     ratingSum = l.avg * l.apps, goals = l.goals, assists = l.assists, defense = l.defense, saves = l.saves });
            }
            header = season.matches.Count + "라운드 중 " + games + "라운드 진행 · 금색 줄이 우리 팀 선수";
            System.Func<Rival, bool> enoughApps = r => r.played > 0 && r.apps >= BestElevenMinApps * r.played;
            System.Func<Rival, string> perGame(System.Func<Rival, int> v) => r => "출전 " + r.apps + (r.apps > 0 ? " · 경기당 " + (v(r) / (float)r.apps).ToString("0.0#") : "");
            if (SeasonSystem.IsComplete(club))   // 시즌이 끝나면 베스트 11 스쿼드 (우리 팀 선수도 후보)
            {
                var pool = new List<Rival>(now); pool.AddRange(ours);
                best = PickBestEleven(ms, pool, new HashSet<Rival>(ours), ms.DivisionName(season.division) + " · 시즌 " + season.seasonNumber);
            }
            return new List<LeaderBlockView>
            {
                RankingOurs(ms, "득점", now, ours, r => r.goals, r => r.goals.ToString(), perGame(r => r.goals), null),
                RankingOurs(ms, "도움", now, ours, r => r.assists, r => r.assists.ToString(), perGame(r => r.assists), null),
                RankingOurs(ms, "공격포인트", now, ours, r => r.goals + r.assists, r => (r.goals + r.assists).ToString(), r => r.goals + "골 " + r.assists + "도움", null),
                RankingOurs(ms, "평점", now, ours, r => r.Avg, r => r.apps > 0 ? r.Avg.ToString("0.00") : "-", r => "출전 " + r.apps, enoughApps),
                RankingOurs(ms, "수비", now, ours, r => r.defense, r => r.defense.ToString(), perGame(r => r.defense), null),
                RankingOurs(ms, "선방", now, ours, r => r.saves, r => r.saves.ToString(), perGame(r => r.saves), null),
            };
        }

        // 리그 전체 순위에서 상위 5명 + 5위 밖의 우리 팀 선수(최대 2명, 기록이 있는 선수만). 같은 기록이면 우리 팀 선수가 앞
        static LeaderBlockView RankingOurs(MatchSettings ms, string title, List<Rival> rivals, List<Rival> ours, System.Func<Rival, float> value,
                                           System.Func<Rival, string> text, System.Func<Rival, string> sub, System.Func<Rival, bool> qualified)
        {
            var all = new List<Rival>();
            foreach (var r in rivals) if (qualified == null || qualified(r)) all.Add(r);
            var mine = new HashSet<Rival>();
            foreach (var r in ours) if (qualified == null || qualified(r)) { all.Add(r); mine.Add(r); }
            all.Sort((a, b) =>
            {
                int c = value(b).CompareTo(value(a));
                if (c != 0) return c;
                if (mine.Contains(a) != mine.Contains(b)) return mine.Contains(a) ? -1 : 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            var block = new LeaderBlockView { title = title };
            void Add(int rank, Rival r, bool gap)
            {
                var t = ms.FindTeam(r.teamId);
                block.rows.Add(new LeaderRowView { rank = rank, name = r.name, team = t != null ? t.displayName : r.teamId, value = Mathf.RoundToInt(value(r)), role = ClubSystem.RoleOf(r.position),
                                                   valueText = text(r), sub = sub != null ? sub(r) : "", me = mine.Contains(r), gap = gap });
            }
            for (int i = 0; i < all.Count && i < 5; i++) Add(i + 1, all[i], false);
            int extra = 0; bool first = true;
            for (int i = 5; i < all.Count && extra < 2; i++)
            {
                if (!mine.Contains(all[i]) || value(all[i]) <= 0f) continue;
                Add(i + 1, all[i], first); first = false; extra++;
            }
            return block;
        }

        // 베스트 11: 포지션마다 평균 평점이 높은 순 (출전이 경기 수의 절반 이상). 공격수 3 · 미드필더 3 · 수비수 4 · 골키퍼 1 (4-3-3)
        static BestElevenSquadView PickBestEleven(MatchSettings ms, List<Rival> pool, HashSet<Rival> mine, string title)
        {
            var v = new BestElevenSquadView { title = title };
            foreach (var (role, count) in new[] { (SquadRole.FW, 3), (SquadRole.MF, 3), (SquadRole.DF, 4), (SquadRole.GK, 1) })
            {
                var list = pool.FindAll(r => ClubSystem.RoleOf(r.position) == role && r.played > 0 && r.apps >= BestElevenMinApps * r.played);
                list.Sort((a, b) => { int c = b.Avg.CompareTo(a.Avg); if (c != 0) return c; if (mine.Contains(a) != mine.Contains(b)) return mine.Contains(a) ? -1 : 1; return string.CompareOrdinal(a.name, b.name); });
                for (int i = 0; i < list.Count && i < count; i++)
                {
                    var r = list[i]; var t = ms.FindTeam(r.teamId);
                    v.picks.Add(new BestElevenPick { role = role, name = r.name, teamId = r.teamId, team = t != null ? t.displayName : r.teamId, avg = r.Avg, apps = r.apps, mine = mine.Contains(r) });
                }
            }
            return v;
        }

        static float SaveRate(Rival r) { return r.saves + r.conceded > 0 ? r.saves / (float)(r.saves + r.conceded) : 0f; }

        // 한 항목의 순위: 상위 5명 + 5위 밖이면 내 줄. qualified가 있으면 조건(출전 수)을 채운 선수만 순위에 들고, 나는 못 채우면 '-'로 맨 아래
        static LeaderBlockView Ranking(MatchSettings ms, string title, List<Rival> rivals, Rival me, System.Func<Rival, float> value,
                                       System.Func<Rival, string> text, System.Func<Rival, string> sub, System.Func<Rival, bool> qualified, bool showMe = true)
        {
            var all = new List<Rival>();
            foreach (var r in rivals) if (qualified == null || qualified(r)) all.Add(r);
            bool meIn = showMe && (qualified == null || qualified(me));
            if (meIn) all.Add(me);
            all.Sort((a, b) =>
            {
                int c = value(b).CompareTo(value(a));
                if (c != 0) return c;
                if (a == me) return -1;
                if (b == me) return 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            var block = new LeaderBlockView { title = title };
            void Add(int rank, Rival r, bool gap)
            {
                var t = ms.FindTeam(r.teamId);
                block.rows.Add(new LeaderRowView { rank = rank, name = r.name, team = t != null ? t.displayName : r.teamId, value = Mathf.RoundToInt(value(r)), role = ClubSystem.RoleOf(r.position),
                                                   valueText = text(r), sub = sub != null ? sub(r) : "", me = r == me, gap = gap });
            }
            for (int i = 0; i < all.Count && i < 5; i++) Add(i + 1, all[i], false);
            int myRank = all.IndexOf(me) + 1;
            if (!showMe) return block;   // 내 포지션이 아닌 항목 (필드 선수의 선방, 골키퍼의 수비)
            if (!meIn) { Add(0, me, true); block.rows[block.rows.Count - 1].sub = "출전 부족 (" + me.apps + "경기)"; }
            else if (myRank > 5) Add(myRank, me, true);
            return block;
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
            if (save.teamMode) { save.awardsSeason = save.season.seasonNumber; save.awardLines = new List<string>(); return true; }   // 나만의 팀 모드: 개인 시상 없음
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
                save.trophies.Add(new TrophyRecord { kind = kind, career = ClubSystem.CareerNumber(save), season = season.seasonNumber, division = div, teamName = team, detail = detail });
            }

            if (mine.finalPosition == 1) { Give(TrophyKind.LeagueTitle, "승점 " + mine.points); lines.Add("* 리그 우승! " + ms.DivisionName(div) + " 챔피언!"); }
            if (div > 0 && season.nextDivision > 0 && season.nextDivision < div) { Give(TrophyKind.Promotion, season.nextDivision + "부로 승격"); lines.Add("* 승격! " + ms.DivisionName(season.nextDivision) + "로 올라갑니다."); }

            // 같은 리그 경쟁자
            var leagueTeams = new List<TeamDefinition>();
            foreach (var id in season.teamIds) { var t = ms.FindTeam(id); if (t != null) leagueTeams.Add(t); }
            var rivals = Snapshot(SimulateRivals(save, ms, career, leagueTeams, div, save.teamId, SquadSystem.MySlot(save, ms)), int.MaxValue, save);

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
            if (pos != Position.Goalkeeper)   // 골키퍼의 기록은 선방이라 수비왕 경쟁에서 뺌
                StatAward(TrophyKind.TopDefender, "수비왕", "회 수비", defense, r => r.position == Position.Goalkeeper ? 0 : r.defense);

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
            else { topRivals = Snapshot(SimulateRivals(save, ms, career, topTeams, 1, null, -1), int.MaxValue, save); topOrder = SeasonSystem.FinalOrder(save, ms, topTeams, 1); }
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
                if (t.career == ClubSystem.CareerNumber(save) && t.season == save.season.seasonNumber) list.Add(t);
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
            int careerNo = ClubSystem.CareerNumber(save);   // 지금 선수의 트로피만
            var list = save.trophies == null ? null : save.trophies.FindAll(t => t.career == careerNo);
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
