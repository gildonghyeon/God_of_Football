using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public enum Formation { F442, F433, F4231, F352, F532 }
    public enum TacticStyle { Possession, Counter, HighPress, LowBlock, Wing }
    // 포메이션 속 자리의 역할
    public enum SlotKind { GK, CB, FB, WB, DM, CM, WM, AM, W, ST }

    // 포메이션의 한 자리: 역할, 선수단 포지션 묶음, 기본 위치(자기 골대에서의 깊이 0~1, 공격 방향 기준 왼쪽에서의 가로 위치 0~68m), 공을 따라 움직이는 정도
    public struct SlotDef
    {
        public SlotKind kind;
        public SquadRole role;
        public float depth, x, shift;
        public SlotDef(SlotKind kind, SquadRole role, float depth, float x, float shift) { this.kind = kind; this.role = role; this.depth = depth; this.x = x; this.shift = shift; }
    }

    public class TeamTactic
    {
        public Formation formation;
        public TacticStyle style;
    }

    // 팀 전술. 팀마다 포메이션과 스타일이 정해져 있고(팀 ID로 고정), 경기 영상의 움직임과 경기 기록에 반영됩니다.
    //  스타일끼리는 서로 물고 물립니다: 전방 압박 > 점유 > 내려선 수비 > 측면 공격 > 역습 > 전방 압박
    public static class TacticSystem
    {
        static SlotDef S(SlotKind k, SquadRole r, float depth, float x, float shift) { return new SlotDef(k, r, depth, x, shift); }
        const SquadRole G = SquadRole.GK, D = SquadRole.DF, M = SquadRole.MF, F = SquadRole.FW;

        // 자리 순서는 GK → DF → MF → FW (선수단에서 능력 순으로 채움). 각 묶음 안은 왼쪽부터.
        static readonly Dictionary<Formation, SlotDef[]> Layouts = new Dictionary<Formation, SlotDef[]>
        {
            [Formation.F442] = new[] {
                S(SlotKind.GK, G, .03f, 34f, .06f),
                S(SlotKind.FB, D, .20f, 9f, .40f), S(SlotKind.CB, D, .20f, 25f, .40f), S(SlotKind.CB, D, .20f, 43f, .40f), S(SlotKind.FB, D, .20f, 59f, .40f),
                S(SlotKind.WM, M, .42f, 9f, .55f), S(SlotKind.CM, M, .42f, 25f, .55f), S(SlotKind.CM, M, .42f, 43f, .55f), S(SlotKind.WM, M, .42f, 59f, .55f),
                S(SlotKind.ST, F, .63f, 24f, .50f), S(SlotKind.ST, F, .63f, 44f, .50f) },
            [Formation.F433] = new[] {
                S(SlotKind.GK, G, .03f, 34f, .06f),
                S(SlotKind.FB, D, .20f, 9f, .42f), S(SlotKind.CB, D, .19f, 25f, .40f), S(SlotKind.CB, D, .19f, 43f, .40f), S(SlotKind.FB, D, .20f, 59f, .42f),
                S(SlotKind.CM, M, .45f, 20f, .55f), S(SlotKind.DM, M, .35f, 34f, .50f), S(SlotKind.CM, M, .45f, 48f, .55f),
                S(SlotKind.W, F, .64f, 9f, .50f), S(SlotKind.ST, F, .68f, 34f, .50f), S(SlotKind.W, F, .64f, 59f, .50f) },
            [Formation.F4231] = new[] {
                S(SlotKind.GK, G, .03f, 34f, .06f),
                S(SlotKind.FB, D, .20f, 9f, .42f), S(SlotKind.CB, D, .19f, 25f, .40f), S(SlotKind.CB, D, .19f, 43f, .40f), S(SlotKind.FB, D, .20f, 59f, .42f),
                S(SlotKind.DM, M, .36f, 25f, .50f), S(SlotKind.DM, M, .36f, 43f, .50f),
                S(SlotKind.W, M, .55f, 10f, .55f), S(SlotKind.AM, M, .57f, 34f, .55f), S(SlotKind.W, M, .55f, 58f, .55f),
                S(SlotKind.ST, F, .68f, 34f, .50f) },
            [Formation.F352] = new[] {
                S(SlotKind.GK, G, .03f, 34f, .06f),
                S(SlotKind.CB, D, .19f, 18f, .38f), S(SlotKind.CB, D, .18f, 34f, .36f), S(SlotKind.CB, D, .19f, 50f, .38f),
                S(SlotKind.WB, M, .40f, 6f, .58f), S(SlotKind.CM, M, .42f, 23f, .55f), S(SlotKind.DM, M, .34f, 34f, .50f), S(SlotKind.CM, M, .42f, 45f, .55f), S(SlotKind.WB, M, .40f, 62f, .58f),
                S(SlotKind.ST, F, .64f, 26f, .50f), S(SlotKind.ST, F, .64f, 42f, .50f) },
            [Formation.F532] = new[] {
                S(SlotKind.GK, G, .03f, 34f, .06f),
                S(SlotKind.WB, D, .24f, 6f, .50f), S(SlotKind.CB, D, .19f, 20f, .36f), S(SlotKind.CB, D, .18f, 34f, .34f), S(SlotKind.CB, D, .19f, 48f, .36f), S(SlotKind.WB, D, .24f, 62f, .50f),
                S(SlotKind.CM, M, .42f, 20f, .55f), S(SlotKind.DM, M, .37f, 34f, .50f), S(SlotKind.CM, M, .42f, 48f, .55f),
                S(SlotKind.ST, F, .62f, 26f, .50f), S(SlotKind.ST, F, .62f, 42f, .50f) },
        };

        public static SlotDef[] Layout(Formation f) { return Layouts[f]; }

        // GK, DF, MF, FW 인원
        public static int[] RoleCounts(Formation f)
        {
            var c = new int[4];
            foreach (var s in Layouts[f]) c[(int)s.role]++;
            return c;
        }

        // ---------- 팀마다 고정된 전술 ----------
        static readonly Dictionary<string, TeamTactic> cache = new Dictionary<string, TeamTactic>();

        public static TeamTactic Of(TeamDefinition team)
        {
            if (team == null) return new TeamTactic { formation = Formation.F442, style = TacticStyle.Possession };
            if (cache.TryGetValue(team.id, out var t)) return t;
            // 팀마다 정한 전술 (그 팀 이름이 떠올리게 하는 클럽의 경기 방식)
            if (TeamIdentity.TryGet(team.id, out var id))
            {
                t = new TeamTactic { formation = id.formation, style = id.style };
                cache[team.id] = t;
                return t;
            }
            int h = 17; unchecked { foreach (char ch in team.id) h = h * 31 + ch; }
            var rng = new System.Random(h ^ 0x7ac71c);
            t = new TeamTactic { formation = (Formation)rng.Next(5) };
            // 팀 수준에 따라 스타일 경향이 다름: 요구 수준보다 전력이 많이 높은 팀은 공을 쥐고, 낮은 팀은 내려서거나 역습
            float edge = team.strength - team.requiredRating;   // 대략 15~30
            float[] w = { 1f + Mathf.Max(0f, edge - 20f) * 0.08f, 1.2f, 1f, 1f + Mathf.Max(0f, 22f - edge) * 0.08f, 1f };
            float total = 0f; foreach (var x in w) total += x;
            float r = (float)rng.NextDouble() * total;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0f) { t.style = (TacticStyle)i; break; } }
            cache[team.id] = t;
            return t;
        }

        // ---------- 이름 ----------
        public static string FormationName(Formation f)
        {
            switch (f) { case Formation.F433: return "4-3-3"; case Formation.F4231: return "4-2-3-1"; case Formation.F352: return "3-5-2"; case Formation.F532: return "5-3-2"; default: return "4-4-2"; }
        }

        public static string StyleName(TacticStyle s)
        {
            switch (s) { case TacticStyle.Counter: return "역습"; case TacticStyle.HighPress: return "전방 압박"; case TacticStyle.LowBlock: return "내려선 수비"; case TacticStyle.Wing: return "측면 공격"; default: return "점유"; }
        }

        public static string Describe(TeamTactic t) { return FormationName(t.formation) + " · " + StyleName(t.style); }

        public static string KindShort(SlotKind k) { return k.ToString(); }

        public static string KindName(SlotKind k)
        {
            switch (k)
            {
                case SlotKind.GK: return "골키퍼"; case SlotKind.CB: return "센터백"; case SlotKind.FB: return "풀백"; case SlotKind.WB: return "윙백";
                case SlotKind.DM: return "수비형 미드필더"; case SlotKind.CM: return "중앙 미드필더"; case SlotKind.WM: return "측면 미드필더";
                case SlotKind.AM: return "공격형 미드필더"; case SlotKind.W: return "윙어"; default: return "스트라이커";
            }
        }

        // 내 포지션이 이 포메이션에서 맡는 자리: 공격수는 스트라이커, 미드필더는 공격형 → 중앙, 수비수는 센터백
        public static int YouSlot(Formation f, Position p)
        {
            var l = Layouts[f];
            SlotKind[] prefer = p == Position.Forward ? new[] { SlotKind.ST, SlotKind.W }
                              : p == Position.Midfielder ? new[] { SlotKind.AM, SlotKind.CM, SlotKind.DM, SlotKind.WM }
                              : new[] { SlotKind.CB, SlotKind.FB, SlotKind.WB };
            foreach (var k in prefer)
            {
                int best = -1; float bestCenter = float.MaxValue;
                for (int i = 1; i < l.Length; i++)
                    if (l[i].kind == k && Mathf.Abs(l[i].x - 34f) < bestCenter) { best = i; bestCenter = Mathf.Abs(l[i].x - 34f); }
                if (best >= 0) return best;
            }
            return 1;
        }

        public static SlotKind KindAt(Formation f, int slot)
        {
            var l = Layouts[f];
            return slot >= 0 && slot < l.Length ? l[slot].kind : SlotKind.CM;
        }

        // ---------- 경기 계산에 주는 영향 ----------
        // a 스타일이 b 스타일을 상대할 때 공격력 배율 (물고 물림 ±6%)
        public static float Matchup(TacticStyle a, TacticStyle b)
        {
            if (Beats(a, b)) return 1.06f;
            if (Beats(b, a)) return 0.94f;
            return 1f;
        }

        static bool Beats(TacticStyle a, TacticStyle b)
        {
            return (a == TacticStyle.HighPress && b == TacticStyle.Possession) || (a == TacticStyle.Possession && b == TacticStyle.LowBlock)
                || (a == TacticStyle.LowBlock && b == TacticStyle.Wing) || (a == TacticStyle.Wing && b == TacticStyle.Counter)
                || (a == TacticStyle.Counter && b == TacticStyle.HighPress);
        }

        // 경기가 얼마나 열리는지 (양 팀 골 배율): 압박·측면은 골이 많고, 내려선 수비는 적음
        public static float Openness(TacticStyle s)
        {
            switch (s) { case TacticStyle.HighPress: return 1.06f; case TacticStyle.Wing: return 1.04f; case TacticStyle.LowBlock: return 0.9f; default: return 1f; }
        }

        // 자리 역할과 팀 스타일에 따른 개인 기록 배율 (슈팅, 패스, 도움 기회, 수비)
        public static void RoleMultipliers(SlotKind k, TacticStyle s, out float shots, out float passes, out float assists, out float defense)
        {
            switch (k)
            {
                case SlotKind.ST: shots = 1.15f; passes = 0.8f; assists = 0.9f; defense = 0.6f; break;
                case SlotKind.W: shots = 0.95f; passes = 0.9f; assists = 1.25f; defense = 0.7f; break;
                case SlotKind.AM: shots = 1.15f; passes = 1.0f; assists = 1.25f; defense = 0.75f; break;
                case SlotKind.WM: shots = 0.8f; passes = 0.95f; assists = 1.15f; defense = 0.9f; break;
                case SlotKind.DM: shots = 0.55f; passes = 1.15f; assists = 0.75f; defense = 1.35f; break;
                case SlotKind.CB: shots = 0.7f; passes = 1.0f; assists = 0.6f; defense = 1.1f; break;
                case SlotKind.FB: shots = 0.9f; passes = 1.0f; assists = 1.3f; defense = 0.95f; break;
                case SlotKind.WB: shots = 1.0f; passes = 1.0f; assists = 1.45f; defense = 0.85f; break;
                default: shots = 1f; passes = 1f; assists = 1f; defense = 1f; break;
            }
            switch (s)
            {
                case TacticStyle.Possession: passes *= 1.25f; shots *= 0.95f; break;
                case TacticStyle.Counter: passes *= 0.8f; if (k == SlotKind.ST || k == SlotKind.W) shots *= 1.1f; break;
                case TacticStyle.HighPress: defense *= 1.25f; break;
                case TacticStyle.LowBlock: defense *= 1.2f; shots *= 0.85f; passes *= 0.9f; break;
                case TacticStyle.Wing: if (k == SlotKind.W || k == SlotKind.WB || k == SlotKind.FB || k == SlotKind.WM) assists *= 1.2f; break;
            }
        }
    }
}
