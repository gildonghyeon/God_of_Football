using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    public enum SquadRole { GK, DF, MF, FW }

    // 팀 선수 한 명 (저장됨). 능력은 내 선수의 포지션 평점과 같은 0~100 단위입니다.
    [Serializable] public class SquadPlayer
    {
        public int id;
        public string name;
        public SquadRole role;
        public int age;
        public float rating;
    }

    // 한 선수의 이번 시즌 기록 (화면용)
    public class SeasonLine { public int goals, assists, apps; public float avg; }

    // 선수단 화면용 한 줄
    public class SquadRowView
    {
        public SquadRole role;
        public SlotKind kind;        // 포메이션 속 역할 (주전만 의미 있음)
        public string name, tag;     // tag: "나", "경쟁" 등
        public int age;              // 0이면 표시하지 않음
        public float rating;
        public SeasonLine line;      // 이번 시즌 기록 (없으면 null)
        public bool me;
    }

    public class SquadView
    {
        public string division;      // 예: "3부 리그"
        public float strength, required, avgAge, starterAvg;
        public TeamTactic tactic;
        public SlotDef[] layout;     // 포메이션 그림용 (주전 순서와 같음)
        public List<SquadRowView> starters = new List<SquadRowView>(), subs = new List<SquadRowView>();
    }

    [Serializable] public class TeamSquad
    {
        public string teamId;
        public List<SquadPlayer> players = new List<SquadPlayer>();
    }

    // 모든 팀의 선수단. 경기 영상과 같은 4-4-2로 주전 11명(GK 1, DF 4, MF 4, FW 2)과 교체 선수를 둡니다.
    //  처음 만들 때: 팀 요구 평점을 기준으로 주전은 조금 높게, 교체는 조금 낮게
    //  시즌마다: 한 살씩 먹고, 어리면 크고 나이 들면 떨어지며, 팀 수준 쪽으로 조금씩 끌려갑니다. 35세 전후로 은퇴하면 유망주가 들어옵니다.
    // 내 팀에서는 내 포지션 자리(영상의 내 자리)를 내가 차지합니다.
    public static class SquadSystem
    {
        static readonly SquadRole[] Shape = {
            SquadRole.GK, SquadRole.GK,
            SquadRole.DF, SquadRole.DF, SquadRole.DF, SquadRole.DF, SquadRole.DF,
            SquadRole.MF, SquadRole.MF, SquadRole.MF, SquadRole.MF, SquadRole.MF,
            SquadRole.FW, SquadRole.FW, SquadRole.FW, SquadRole.FW,
        };   // 16명: 주전 11 + 교체 5
        static readonly int[] StarterCount = { 1, 4, 4, 2 };   // GK, DF, MF, FW (4-4-2)

        static readonly string Initials = "ABCDEFGHIJKLMNOPRSTVWY";
        static readonly string[] Surnames =
        {
            "Moreno", "Hart", "Silva", "Novak", "Okafor", "Brandt", "Costa", "Lindqvist", "Ruiz", "Becker",
            "Duarte", "Kovac", "Mensah", "Fischer", "Rossi", "Haaland", "Petrov", "Tanaka", "Walsh", "Dubois",
            "Santos", "Muller", "Varga", "Ibrahim", "Larsen", "Romero", "Kane", "Bianchi", "Nilsen", "Ortega",
            "Sato", "Keller", "Mbeki", "Andersen", "Quinn", "Ferreira", "Hughes", "Vidal", "Kim", "Zielinski",
            "Alves", "Barros", "Carver", "Dalton", "Eriksen", "Fontaine", "Garcia", "Hoffman", "Ivanov", "Jensen",
            "Kowalski", "Laurent", "Mertens", "Nakamura", "Olsen", "Pereira", "Rahman", "Schmidt", "Torres", "Ueda",
            "Vargas", "Weber", "Yilmaz", "Zanetti", "Adeyemi", "Bauer", "Cruz", "Diallo", "Evans", "Foster",
            "Gomez", "Hansen", "Ito", "Jovanovic", "Klein", "Lopez", "Martins", "Nunez", "Owusu", "Park",
        };

        public static string RoleName(SquadRole role) { return role.ToString(); }

        public static Position ToPosition(SquadRole role)
        {
            return role == SquadRole.FW ? Position.Forward : role == SquadRole.MF ? Position.Midfielder : Position.Defender;
        }

        // ---------- 만들기 ----------
        // 선수단이 없는 팀이 있으면 만듭니다. 새로 만든 팀이 있으면 true.
        public static bool Ensure(SaveData save, MatchSettings ms)
        {
            if (save.squads == null) save.squads = new List<TeamSquad>();
            if (ms.teams == null) return false;
            bool made = false;
            HashSet<string> used = null;
            foreach (var team in ms.teams)
            {
                if (team == null || string.IsNullOrEmpty(team.id) || Find(save, team.id) != null) continue;
                if (used == null) used = UsedNames(save);
                var rng = new System.Random(StableHash(team.id) ^ 0x51ed27);
                var squad = new TeamSquad { teamId = team.id };
                int[] roleIndex = new int[4];
                foreach (var role in Shape)
                {
                    bool starter = roleIndex[(int)role]++ < StarterCount[(int)role];
                    float rating = team.requiredRating + (starter ? rng.Next(-2, 13) : rng.Next(-10, 3));
                    squad.players.Add(NewPlayer(save, rng, used, role, rng.Next(18, 34), rating));
                }
                save.squads.Add(squad);
                made = true;
            }
            return made;
        }

        static SquadPlayer NewPlayer(SaveData save, System.Random rng, HashSet<string> used, SquadRole role, int age, float rating)
        {
            string name;
            int guard = 0;
            do name = Initials[rng.Next(Initials.Length)] + ". " + Surnames[rng.Next(Surnames.Length)];
            while (!used.Add(name) && ++guard < 50);
            return new SquadPlayer { id = ++save.nextSquadPlayerId, name = name, role = role, age = age, rating = Mathf.Clamp(rating, 1f, 99f) };
        }

        static HashSet<string> UsedNames(SaveData save)
        {
            var set = new HashSet<string>();
            if (save.squads != null) foreach (var s in save.squads) foreach (var p in s.players) set.Add(p.name);
            return set;
        }

        static int StableHash(string s)
        {
            unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; }
        }

        public static TeamSquad Find(SaveData save, string teamId)
        {
            if (save.squads != null) foreach (var s in save.squads) if (s.teamId == teamId) return s;
            return null;
        }

        // ---------- 주전 ----------
        // 팀 포메이션의 자리 순서(TacticSystem.Layout)로 주전 11명. 같은 포지션 묶음 안에서는 능력이 높은 순으로 채웁니다.
        public static SquadPlayer[] Starters(SaveData save, MatchSettings ms, string teamId)
        {
            Ensure(save, ms);
            var squad = Find(save, teamId);
            var layout = TacticSystem.Layout(TacticSystem.Of(ms.FindTeam(teamId)).formation);
            var xi = new SquadPlayer[layout.Length];
            if (squad == null) return xi;
            for (int r = 0; r < 4; r++)
            {
                var list = squad.players.FindAll(p => (int)p.role == r);
                list.Sort((a, b) => b.rating.CompareTo(a.rating));
                int next = 0;
                for (int slot = 0; slot < layout.Length; slot++)
                    if ((int)layout[slot].role == r) xi[slot] = next < list.Count ? list[next++] : null;
            }
            return xi;
        }

        // 내 팀에서 내가 서는 자리 (팀 포메이션과 내 포지션으로 정함)
        public static int MySlot(SaveData save, MatchSettings ms)
        {
            return TacticSystem.YouSlot(TacticSystem.Of(ms.FindTeam(save.teamId)).formation, save.player.position);
        }

        static string NameAt(SquadPlayer[] xi, int slot, int youSlot, string youName)
        {
            if (slot == youSlot && youSlot >= 0) return youName;
            return slot >= 0 && slot < xi.Length && xi[slot] != null ? xi[slot].name : "?";
        }

        // ---------- 득점 기록 ----------
        // 경기 영상과 같은 계산으로 골 장면(시간·득점자·도움 자리)을 만든 뒤, 자리를 선수 이름으로 바꿔 경기 기록에 남깁니다.
        public static void FillGoalNotes(MatchRecord m, SaveData save, MatchSettings ms, CareerSettings career, string ownId, string oppId)
        {
            m.goalNotes = new List<GoalNote>();
            var player = save.player;
            var ownTactic = TacticSystem.Of(ms.FindTeam(ownId)); var oppTactic = TacticSystem.Of(ms.FindTeam(oppId));
            int mySlot = TacticSystem.YouSlot(ownTactic.formation, player.position);
            int youSlot = m.playerPlayed ? mySlot : -1;
            var rng = new System.Random(m.seed ^ 0x2f6e2b1);   // MatchReplayView와 같은 시드
            var stats = ReplayStats.From(player, m, career.attributeCap);
            var plan = MatchReplayPlan.Build(m, m.playerPlayed, mySlot, rng, stats.shootN, m.defensiveActions, ownTactic.formation, oppTactic.formation);
            var ours = Starters(save, ms, ownId);
            var theirs = Starters(save, ms, oppId);
            foreach (var e in plan)
            {
                if (e.type == ReplayEventType.GoalOurs)
                    m.goalNotes.Add(new GoalNote { minute = (int)e.minute, ours = true,
                        scorer = e.youScore ? player.playerName : NameAt(ours, e.scorerSlot, youSlot, player.playerName),
                        assist = e.youAssist ? player.playerName : e.assisterSlot >= 0 ? NameAt(ours, e.assisterSlot, youSlot, player.playerName) : "",
                        how = MatchReplayPlan.SetPieceName(e.setPiece) });
                else if (e.type == ReplayEventType.GoalTheirs)
                    m.goalNotes.Add(new GoalNote { minute = (int)e.minute, ours = false,
                        scorer = NameAt(theirs, e.scorerSlot, -1, null),
                        assist = e.assisterSlot >= 0 ? NameAt(theirs, e.assisterSlot, -1, null) : "",
                        how = MatchReplayPlan.SetPieceName(e.setPiece) });
            }
        }

        public static string DescribeGoals(MatchRecord m, string ownName, string oppName)
        {
            if (m.goalNotes == null || m.goalNotes.Count == 0) return null;
            var sb = new StringBuilder();
            foreach (bool ours in new[] { true, false })
            {
                var parts = new List<string>();
                foreach (var g in m.goalNotes)
                    if (g.ours == ours) parts.Add(g.minute + "' " + g.scorer + (string.IsNullOrEmpty(g.how) ? "" : " [" + g.how + "]") + (string.IsNullOrEmpty(g.assist) ? "" : "(도움 " + g.assist + ")"));
                if (parts.Count == 0) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ours ? ownName : oppName).Append(": ").Append(string.Join(", ", parts));
            }
            return sb.ToString();
        }

        // ---------- 시즌마다 ----------
        // 한 살씩 먹고 능력이 변하며, 은퇴한 자리에 유망주가 들어옵니다. 바뀐 내용 몇 줄을 돌려줍니다.
        public static void AdvanceSeason(SaveData save, MatchSettings ms)
        {
            Ensure(save, ms);
            save.squadSeason++;
            var used = UsedNames(save);
            foreach (var squad in save.squads)
            {
                var team = ms.FindTeam(squad.teamId);
                if (team == null) continue;
                var rng = new System.Random(StableHash(squad.teamId) ^ (save.squadSeason * 7919) ^ save.rebirthCount);
                float target = team.requiredRating + 5f;   // 팀 수준 (주전 평균 정도)
                for (int i = 0; i < squad.players.Count; i++)
                {
                    var p = squad.players[i];
                    p.age++;
                    bool retire = p.age >= 36 || (p.age >= 33 && rng.NextDouble() < 0.3);
                    if (retire)
                    {
                        used.Remove(p.name);
                        squad.players[i] = NewPlayer(save, rng, used, p.role, rng.Next(18, 21), team.requiredRating - 8f + rng.Next(0, 7));
                        continue;
                    }
                    float grow = p.age <= 21 ? rng.Next(2, 6) : p.age <= 24 ? rng.Next(0, 4) : p.age <= 29 ? rng.Next(-1, 2) : p.age <= 32 ? rng.Next(-3, 1) : rng.Next(-5, -1);
                    p.rating = Mathf.Clamp(p.rating + grow + (target - p.rating) * 0.15f, 1f, 99f);
                }
            }
        }

        // ---------- 팀 정보 (화면용 데이터) ----------
        public static SquadView View(SaveData save, MatchSettings ms, CareerSettings career, string teamId, int division, Dictionary<string, SeasonLine> lines)
        {
            var team = ms.FindTeam(teamId);
            var squad = Find(save, teamId);
            var v = new SquadView();
            if (team == null || squad == null) return v;
            v.division = division > 0 ? ms.DivisionName(division) : "";
            v.strength = team.strength; v.required = team.requiredRating;
            var xi = Starters(save, ms, teamId);
            var tactic = TacticSystem.Of(team);
            var layout = TacticSystem.Layout(tactic.formation);
            v.tactic = tactic; v.layout = layout;
            bool mine = teamId == save.teamId;
            int youSlot = mine ? MySlot(save, ms) : -1;
            float ageSum = 0f; foreach (var p in squad.players) ageSum += p.age;
            v.avgAge = squad.players.Count > 0 ? ageSum / squad.players.Count : 0f;
            float ratingSum = 0f; int count = 0;
            for (int slot = 0; slot < xi.Length; slot++)
            {
                if (slot == youSlot)
                {
                    float myRating = SeasonSystem.PlayerRating(save, ms, career);
                    v.starters.Add(new SquadRowView { role = layout[slot].role, kind = layout[slot].kind, name = save.player.playerName, tag = "나", rating = myRating, line = MyLine(save), me = true });
                    ratingSum += myRating; count++;
                    continue;
                }
                var p = xi[slot];
                if (p == null) continue;
                var row = Row(p, lines, null); row.kind = layout[slot].kind;
                v.starters.Add(row);
                ratingSum += p.rating; count++;
            }
            v.starterAvg = count > 0 ? ratingSum / count : 0f;
            var starters = new HashSet<SquadPlayer>(xi);
            var subs = squad.players.FindAll(p => !starters.Contains(p));
            subs.Sort((a, b) => a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            if (mine && youSlot >= 0 && xi[youSlot] != null) v.subs.Add(Row(xi[youSlot], null, "경쟁")   /* 내 자리를 두고 경쟁하는 선수 */);
            foreach (var p in subs) v.subs.Add(Row(p, null, null));
            return v;
        }


        static SquadRowView Row(SquadPlayer p, Dictionary<string, SeasonLine> lines, string tag)
        {
            SeasonLine line = null;
            if (lines != null) lines.TryGetValue(p.name, out line);
            return new SquadRowView { role = p.role, kind = DefaultKind(p.role), name = p.name, age = p.age, rating = p.rating, line = line, tag = tag };
        }

        static SlotKind DefaultKind(SquadRole r) { return r == SquadRole.GK ? SlotKind.GK : r == SquadRole.DF ? SlotKind.CB : r == SquadRole.MF ? SlotKind.CM : SlotKind.ST; }

        // 내 선수의 이번 시즌 리그 기록
        static SeasonLine MyLine(SaveData save)
        {
            var l = new SeasonLine();
            float sum = 0f;
            var matches = save.season.matches;
            for (int i = 0; i < save.season.nextMatchIndex && i < matches.Count; i++)
            {
                var m = matches[i];
                if (!m.played || !m.playerPlayed) continue;
                l.apps++; sum += m.rating; l.goals += m.playerGoals; l.assists += m.playerAssists;
            }
            l.avg = l.apps > 0 ? sum / l.apps : 0f;
            return l;
        }

        // ---------- 팀 정보 (예전 글 형식) ----------
        public static string Describe(SaveData save, MatchSettings ms, string teamId, int division, Dictionary<string, SeasonLine> seasonStats)
        {
            var team = ms.FindTeam(teamId);
            var squad = Find(save, teamId);
            if (team == null || squad == null) return "선수단 정보가 없습니다.";
            var xi = Starters(save, ms, teamId);
            var starters = new HashSet<SquadPlayer>(xi);
            bool mine = teamId == save.teamId;
            int youSlot = mine ? MySlot(save, ms) : -1;
            var sb = new StringBuilder();
            sb.Append(division > 0 ? division + "부  " : "").Append("전력 ").Append(team.strength.ToString("0"))
              .Append("  요구 평점 ").Append(team.requiredRating.ToString("0")).Append('\n');
            sb.Append("\n[주전]");
            for (int slot = 0; slot < xi.Length; slot++)
            {
                if (slot == youSlot) { sb.Append('\n').Append(RoleOf(slot)).Append("  ").Append(save.player.playerName).Append(" (나)"); continue; }
                Line(sb, xi[slot], seasonStats);
            }
            sb.Append("\n\n[교체]");
            var subs = squad.players.FindAll(p => !starters.Contains(p));
            subs.Sort((a, b) => a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            foreach (var p in subs) Line(sb, p, null);
            if (mine && youSlot >= 0 && xi[youSlot] != null) Line(sb, xi[youSlot], null, " (내 자리 경쟁)");
            return sb.ToString();
        }

        static string RoleOf(int slot) { return slot == 0 ? "GK" : slot <= 4 ? "DF" : slot <= 8 ? "MF" : "FW"; }

        static void Line(StringBuilder sb, SquadPlayer p, Dictionary<string, SeasonLine> stats, string tag = "")
        {
            if (p == null) return;
            sb.Append('\n').Append(RoleName(p.role)).Append("  ").Append(p.name).Append(tag)
              .Append("  ").Append(p.age).Append("세  능력 ").Append(p.rating.ToString("0"));
            if (stats != null && stats.TryGetValue(p.name, out var s)) sb.Append("  골 ").Append(s.goals).Append(" 도움 ").Append(s.assists);
        }
    }
}
