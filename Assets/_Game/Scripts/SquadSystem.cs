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
        public int number;      // 등번호 1~99 (팀 안에서 겹치지 않음)
        public SquadRole role;
        public int age;
        public float rating;
    }

    // 한 선수의 이번 시즌 기록 (화면용)
    public class SeasonLine { public int goals, assists, apps, defense, saves; public float avg; }   // defense: 수비(태클·가로채기 등), saves: 골키퍼 선방

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
        public int id;               // 선수 번호 (내 선수는 0)
        public float[] attrs;        // 세부 능력치 6개 (AttributeKind 순서). 내 선수·은퇴 선수는 실제 값, 나머지는 능력과 포지션으로 정한 값
        public bool legend;          // 내가 키운 선수
        public int number;           // 등번호 (0이면 모름)
    }

    public class SquadView
    {
        public string teamId;        // 유니폼 그림용
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
        internal static readonly SquadRole[] SquadShape = {
            SquadRole.GK, SquadRole.GK,
            SquadRole.DF, SquadRole.DF, SquadRole.DF, SquadRole.DF, SquadRole.DF, SquadRole.DF,
            SquadRole.MF, SquadRole.MF, SquadRole.MF, SquadRole.MF, SquadRole.MF, SquadRole.MF,
            SquadRole.FW, SquadRole.FW, SquadRole.FW, SquadRole.FW, SquadRole.FW,
        };   // 19명: 1군 11 + 교체 8 (나만의 팀과 같은 인원). 선수 키우기의 내 팀은 내 포지션에서 한 명 적은 18명 + 나
        static readonly int[] ShapeCount = { 2, 6, 6, 5 };   // GK, DF, MF, FW
        static SquadRole[] Shape { get { return SquadShape; } }
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
            return role == SquadRole.FW ? Position.Forward : role == SquadRole.MF ? Position.Midfielder : role == SquadRole.GK ? Position.Goalkeeper : Position.Defender;
        }

        // ---------- 만들기 ----------
        // 선수단이 없는 팀이 있으면 만듭니다. 새로 만든 팀이 있으면 true.
        public const float MinStat = 10f;   // 모든 능력치·능력의 최소값
        public const float MaxStat = 100f;  // 최대값
        public static float[] AtLeastMin(float[] a) { for (int k = 0; k < a.Length; k++) a[k] = Mathf.Max(a[k], MinStat); return a; }

        public static bool Ensure(SaveData save, MatchSettings ms)
        {
            if (save.squads == null) save.squads = new List<TeamSquad>();
            foreach (var s in save.squads) if (s.players != null) foreach (var p in s.players) if (p.rating < MinStat) p.rating = MinStat;   // 예전 저장: 최소 능력 10
            if (ms.teams == null) return false;
            bool made = false;
            HashSet<string> used = null;
            foreach (var team in ms.teams)
            {
                if (team == null || string.IsNullOrEmpty(team.id) || Find(save, team.id) != null) continue;
                if (team.id == ClubSystem.ClubId) { ClubSystem.EnsureClubSquad(save); made = true; continue; }   // 나만의 팀은 은퇴 선수 + 새 선수 19명
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
            foreach (var s in save.squads) FitSquadSize(save, s, ms);   // 예전 저장(16명)이나 이적한 뒤에도 1군 11 + 교체 8명이 되게
            foreach (var s in save.squads)   // 등번호: 없거나 겹치면 정함 (선수 키우기의 내 팀은 내 번호를 비워 둠)
                AssignNumbers(s, s.teamId == save.teamId && !save.teamMode && save.player != null ? save.player.number : 0);
            return made;
        }

        // 선수단 인원 맞추기: 포지션별 GK 2 · DF 6 · MF 6 · FW 5 (19명). 선수 키우기의 내 팀은 내 포지션 자리 하나를 내가 채우므로 그 포지션이 한 명 적음.
        // 모자라면 교체 수준의 새 선수를 만들고, 넘치면(내가 이적해 온 팀) 그 포지션에서 능력이 가장 낮은 선수가 팀을 떠남
        static void FitSquadSize(SaveData save, TeamSquad squad, MatchSettings ms)
        {
            if (squad == null || squad.players == null || squad.teamId == ClubSystem.ClubId) return;   // 나만의 팀은 ClubSystem이 19명으로 관리
            var team = ms.FindTeam(squad.teamId);
            if (team == null) return;
            var want = (int[])ShapeCount.Clone();
            if (squad.teamId == save.teamId && !save.teamMode && save.player != null) want[(int)ClubSystem.RoleOf(save.player.position)]--;
            HashSet<string> used = null;
            System.Random rng = null;
            for (int r = 0; r < 4; r++)
            {
                var role = (SquadRole)r;
                var list = squad.players.FindAll(p => p.role == role);
                while (list.Count > want[r])
                {
                    SquadPlayer weakest = null;
                    foreach (var p in list) if (weakest == null || p.rating < weakest.rating) weakest = p;
                    squad.players.Remove(weakest); list.Remove(weakest);
                }
                while (list.Count < want[r])
                {
                    if (used == null) used = UsedNames(save);
                    if (rng == null) rng = new System.Random(StableHash(squad.teamId) ^ (save.squadSeason * 104729) ^ 0x2b7e15);
                    var p = NewPlayer(save, rng, used, role, rng.Next(18, 30), team.requiredRating + rng.Next(-10, 3));   // 교체 수준
                    squad.players.Add(p); list.Add(p);
                }
            }
        }

        // ---------- 등번호 ----------
        // 포지션마다 흔히 쓰는 번호부터 (골키퍼 1, 수비 2~5, 미드필더 6·8·10, 공격 9·11 …)
        static readonly int[][] NumberPrefs =
        {
            new[] { 1, 13, 12, 25, 31, 30, 99 },                                  // GK
            new[] { 2, 3, 4, 5, 15, 12, 14, 22, 23, 24, 26, 28, 32, 33 },        // DF
            new[] { 8, 6, 10, 7, 14, 16, 18, 20, 17, 21, 23, 34 },               // MF
            new[] { 9, 11, 7, 10, 19, 20, 17, 27, 29, 18, 21 },                  // FW
        };

        // 그 포지션의 대표 번호 (새 선수를 만들 때 처음 보이는 번호)
        public static int DefaultNumber(Position p) { return NumberPrefs[(int)ClubSystem.RoleOf(p)][0]; }

        // 팀 안에서 등번호가 겹치지 않게: 내가 키운 선수와 능력이 높은 선수부터 지금 번호를 지키고, 없거나 겹치면 포지션에 맞는 빈 번호
        static void AssignNumbers(TeamSquad squad, int reserved)
        {
            if (squad == null || squad.players == null) return;
            var used = new HashSet<int>();
            if (reserved >= 1 && reserved <= 99) used.Add(reserved);
            var order = new List<SquadPlayer>(squad.players);
            order.Sort((a, b) => ClubSystem.IsLegend(a) != ClubSystem.IsLegend(b) ? (ClubSystem.IsLegend(a) ? -1 : 1) : b.rating.CompareTo(a.rating));
            var need = new List<SquadPlayer>();
            foreach (var p in order)
            {
                if (p.number >= 1 && p.number <= 99 && used.Add(p.number)) continue;
                need.Add(p);
            }
            foreach (var p in need)
            {
                p.number = 0;
                foreach (int n in NumberPrefs[(int)p.role]) if (used.Add(n)) { p.number = n; break; }
                for (int n = 1; p.number == 0 && n <= 99; n++) if (used.Add(n)) p.number = n;
            }
        }

        internal static SquadPlayer MakePlayer(SaveData save, System.Random rng, HashSet<string> used, SquadRole role, int age, float rating) { return NewPlayer(save, rng, used, role, age, rating); }

        // 다른 팀 선수와 겹치지 않게 (이 팀 선수 이름은 빼고)
        internal static HashSet<string> UsedNamesExcept(SaveData save, string teamId)
        {
            var set = new HashSet<string>();
            if (save.squads != null) foreach (var s in save.squads) if (s.teamId != teamId) foreach (var p in s.players) set.Add(p.name);
            return set;
        }

        static SquadPlayer NewPlayer(SaveData save, System.Random rng, HashSet<string> used, SquadRole role, int age, float rating)
        {
            string name;
            int guard = 0;
            do name = Initials[rng.Next(Initials.Length)] + ". " + Surnames[rng.Next(Surnames.Length)];
            while (!used.Add(name) && ++guard < 50);
            return new SquadPlayer { id = ++save.nextSquadPlayerId, name = name, role = role, age = age, rating = Mathf.Clamp(rating, MinStat, MaxStat) };
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
            if (teamId == ClubSystem.ClubId && save.hasClub && save.teamMode) return ClubSystem.Lineup(save);   // 나만의 팀: 내가 정한 주전
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

        // 한 팀 주전 11명의 세부 능력치 (포메이션 자리 순서). 경기 영상과 득점 기록이 같은 값을 씀
        public static float[][] TeamAttrs(SaveData save, MatchSettings ms, string teamId)
        {
            if (save == null || ms == null || string.IsNullOrEmpty(teamId)) return null;
            var xi = Starters(save, ms, teamId);
            if (xi == null) return null;
            var a = new float[xi.Length][];
            for (int i = 0; i < xi.Length; i++) if (xi[i] != null) a[i] = ClubSystem.Attrs(save, xi[i]);
            return a;
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
            var plan = MatchReplayPlan.Build(m, m.playerPlayed, mySlot, rng, stats.shootN, m.defensiveActions, ownTactic.formation, oppTactic.formation,
                                             TeamAttrs(save, ms, ownId), TeamAttrs(save, ms, oppId));   // 경기 영상과 같은 능력치로 세트피스 키커
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
                if (team == null || squad.teamId == ClubSystem.ClubId) continue;   // 나만의 팀(은퇴 선수)은 나이를 먹지 않음
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
                    p.rating = Mathf.Clamp(p.rating + grow + (target - p.rating) * 0.15f, MinStat, MaxStat);
                }
            }
        }

        // ---------- 팀 정보 (화면용 데이터) ----------
        public static SquadView View(SaveData save, MatchSettings ms, CareerSettings career, string teamId, int division, Dictionary<string, SeasonLine> lines)
        {
            var team = ms.FindTeam(teamId);
            var squad = Find(save, teamId);
            var v = new SquadView { teamId = teamId };
            if (team == null || squad == null) return v;
            v.division = division > 0 ? ms.DivisionName(division) : "";
            v.strength = team.strength; v.required = team.requiredRating;
            var xi = Starters(save, ms, teamId);
            var tactic = TacticSystem.Of(team);
            var layout = TacticSystem.Layout(tactic.formation);
            v.tactic = tactic; v.layout = layout;
            bool mine = teamId == save.teamId && !save.teamMode;   // 나만의 팀 모드에는 내 선수가 없음
            int youSlot = mine ? MySlot(save, ms) : -1;
            float ageSum = 0f; foreach (var p in squad.players) ageSum += p.age;
            v.avgAge = squad.players.Count > 0 ? ageSum / squad.players.Count : 0f;
            float ratingSum = 0f; int count = 0;
            for (int slot = 0; slot < xi.Length; slot++)
            {
                if (slot == youSlot)
                {
                    float myRating = SeasonSystem.PlayerRating(save, ms, career);
                    var mp = save.player;
                    v.starters.Add(new SquadRowView { role = layout[slot].role, kind = layout[slot].kind, name = mp.playerName, tag = "나", rating = myRating, line = MyLine(save), me = true, number = mp.number,
                        age = 18 + save.seasonHistory.Count, attrs = AtLeastMin(new[] { mp.shooting, mp.passing, mp.dribbling, mp.defending, mp.stamina, mp.goalkeeping }) });
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
            if (mine && youSlot >= 0 && xi[youSlot] != null) starters.Remove(xi[youSlot]);   // 내 자리의 선수는 교체 명단으로 (따로 표시하지 않음)
            var subs = squad.players.FindAll(p => !starters.Contains(p));
            subs.Sort((a, b) => a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            foreach (var p in subs) v.subs.Add(Row(p, null, null));
            // 내가 키운 선수(나만의 팀)는 실제 능력치
            if (save.legends != null)
                foreach (var list in new[] { v.starters, v.subs })
                    foreach (var r in list)
                    {
                        var lg = save.legends.Find(l => -1000 - l.careerNumber == r.id && r.id != 0);
                        if (lg != null) { r.legend = true; r.attrs = AtLeastMin(new[] { lg.shooting, lg.passing, lg.dribbling, lg.defending, lg.stamina, lg.goalkeeping }); }
                    }
            return v;
        }


        // 다른 선수의 세부 능력치: 능력(평점)을 기준으로 포지션마다 강한 것·약한 것을 두고, 선수마다 조금씩 다르게 (같은 선수는 늘 같은 값)
        static readonly float[][] RoleBias =
        {
            new[] { -25f, 0f, -18f, -6f, -4f, 0f },    // GK: GK 능력은 능력 그대로 (아래에서 따로 정함)
            new[] { -14f, -2f, -8f, 8f, 3f, -40f },    // DF
            new[] { -3f, 8f, 2f, -4f, 4f, -40f },      // MF
            new[] { 8f, -4f, 5f, -18f, 0f, -40f },     // FW
        };

        public static float[] DeriveAttrs(SquadPlayer p)
        {
            var a = new float[6];
            for (int k = 0; k < 6; k++)
            {
                int h; unchecked { h = p.id * 73856093 ^ (k + 1) * 19349663; }
                float jitter = (Mathf.Abs(h) % 9) - 4f;
                a[k] = Mathf.Clamp(Mathf.Round(p.rating + RoleBias[(int)p.role][k] + jitter), MinStat, MaxStat);
            }
            if (p.role == SquadRole.GK) a[(int)AttributeKind.Goalkeeping] = Mathf.Clamp(Mathf.Round(p.rating), MinStat, MaxStat);   // 골키퍼의 GK 능력 = 그 선수의 능력 그대로 (경기 계산과 같은 값)
            return a;
        }

        static SquadRowView Row(SquadPlayer p, Dictionary<string, SeasonLine> lines, string tag)
        {
            SeasonLine line = null;
            if (lines != null) lines.TryGetValue(p.name, out line);
            return new SquadRowView { role = p.role, kind = DefaultKind(p.role), name = p.name, age = p.age, rating = p.rating, line = line, tag = tag, id = p.id, attrs = DeriveAttrs(p), number = p.number };
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
                if (save.player.position == Position.Goalkeeper) l.saves += m.defensiveActions; else l.defense += m.defensiveActions;
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
            bool mine = teamId == save.teamId && !save.teamMode;   // 나만의 팀 모드에는 내 선수가 없음
            int youSlot = mine ? MySlot(save, ms) : -1;
            var sb = new StringBuilder();
            sb.Append(division > 0 ? division + "부  " : "").Append("전력 ").Append(team.strength.ToString("0"))
              .Append("  요구 평점 ").Append(team.requiredRating.ToString("0")).Append('\n');
            sb.Append("\n[1군]");
            for (int slot = 0; slot < xi.Length; slot++)
            {
                if (slot == youSlot) { sb.Append('\n').Append(RoleOf(slot)).Append("  ").Append(save.player.playerName).Append(" (나)"); continue; }
                Line(sb, xi[slot], seasonStats);
            }
            sb.Append("\n\n[교체]");
            var subs = squad.players.FindAll(p => !starters.Contains(p));
            subs.Sort((a, b) => a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            if (mine && youSlot >= 0 && xi[youSlot] != null && !subs.Contains(xi[youSlot])) subs.Add(xi[youSlot]);   // 내 자리의 선수도 교체 명단에
            subs.Sort((a, b) => a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            foreach (var p in subs) Line(sb, p, null);
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
