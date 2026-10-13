using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public enum ReplayEventType { GoalOurs, GoalTheirs, ShotYou, TackleYou }

    // 경기 영상에서 보여줄 한 장면. 실제 경기 결과(득점·도움·슈팅 수)와 어긋나지 않게 미리 짜 둡니다.
    public class ReplayEvent
    {
        public float minute;
        public ReplayEventType type;
        public int scorerSlot = -1, assisterSlot = -1;   // 0=골키퍼, 1~4 수비, 5~8 미드필더, 9~10 공격수
        public bool youScore, youAssist;
        public bool saved;                                // ShotYou: 골키퍼가 막았는지(true) 빗나갔는지(false)
    }

    // 저장된 경기 결과로부터 장면 목록을 만듭니다.
    public static class MatchReplayPlan
    {
        public static int YouSlot(Position position)
        {
            switch (position)
            {
                case Position.Forward: return 9;
                case Position.Midfielder: return 6;
                default: return 2;
            }
        }

        static int RandomMate(System.Random rng, int excludeA, int excludeB)
        {
            float total = 0f;
            for (int s = 1; s <= 10; s++) if (s != excludeA && s != excludeB) total += Weight(s);
            float r = (float)rng.NextDouble() * total;
            int last = 1;
            for (int s = 1; s <= 10; s++)
            {
                if (s == excludeA || s == excludeB) continue;
                last = s; r -= Weight(s);
                if (r <= 0f) return s;
            }
            return last;
        }
        static float Weight(int slot) { return slot <= 4 ? 0.05f : (slot <= 8 ? 0.12f : 0.25f); }

        // shootN: 슈팅 능력(0~1), tackles: 경기 기록의 수비 기여 횟수
        public static List<ReplayEvent> Build(MatchRecord m, bool youPlayed, int youSlot, System.Random rng, float shootN, int tackles)
        {
            var list = new List<ReplayEvent>();
            int ourGoals = Mathf.Max(0, m.goalsFor);
            int youGoals = youPlayed ? Mathf.Min(m.playerGoals, ourGoals) : 0;
            int youAssists = youPlayed ? Mathf.Min(m.playerAssists, ourGoals - youGoals) : 0;
            int you = youPlayed ? youSlot : -1;
            for (int i = 0; i < ourGoals; i++)
            {
                var e = new ReplayEvent { type = ReplayEventType.GoalOurs };
                if (i < youGoals)
                {
                    e.scorerSlot = youSlot; e.youScore = true;
                    e.assisterSlot = rng.NextDouble() < 0.5 ? RandomMate(rng, youSlot, -1) : -1;
                }
                else if (i < youGoals + youAssists)
                {
                    e.scorerSlot = RandomMate(rng, you, -1); e.assisterSlot = youSlot; e.youAssist = true;
                }
                else
                {
                    e.scorerSlot = RandomMate(rng, you, -1);
                    e.assisterSlot = rng.NextDouble() < 0.7 ? RandomMate(rng, you, e.scorerSlot) : -1;
                }
                list.Add(e);
            }
            for (int i = 0; i < Mathf.Max(0, m.goalsAgainst); i++)
            {
                var e = new ReplayEvent { type = ReplayEventType.GoalTheirs };
                e.scorerSlot = RandomMate(rng, -1, -1);
                e.assisterSlot = rng.NextDouble() < 0.7 ? RandomMate(rng, e.scorerSlot, -1) : -1;
                list.Add(e);
            }
            int extraShots = youPlayed ? Mathf.Min(4, Mathf.Max(0, m.playerShots - youGoals)) : 0;
            for (int i = 0; i < extraShots; i++)
                list.Add(new ReplayEvent { type = ReplayEventType.ShotYou, scorerSlot = youSlot, saved = rng.NextDouble() < 0.3 + 0.6 * shootN });   // 슈팅이 좋을수록 유효 슈팅(선방)이 많음

            int tackleEvents = youPlayed ? Mathf.Min(Mathf.Max(0, tackles), Mathf.Max(0, 10 - list.Count)) : 0;
            for (int i = 0; i < tackleEvents; i++)
                list.Add(new ReplayEvent { type = ReplayEventType.TackleYou, scorerSlot = -1 });

            for (int i = list.Count - 1; i > 0; i--)   // 순서 섞기
            {
                int j = rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
            if (list.Count > 0)
            {
                float step = 80f / list.Count;
                for (int i = 0; i < list.Count; i++)
                    list[i].minute = Mathf.Round(8f + step * (i + 0.5f) + ((float)rng.NextDouble() - 0.5f) * step * 0.4f);
            }
            return list;
        }
    }

    // 내 선수의 능력치가 영상 속 움직임에 미치는 영향
    public class ReplayStats
    {
        public float speedFactor = 1f;     // 이동 속도 배율: 체력과 드리블이 높을수록 빠름
        public float staminaN = 0.5f;      // 체력(0~1): 낮으면 경기 후반에 느려짐
        public float passAccuracy = 0.7f;  // 패스 성공률: 경기 기록의 성공률을 그대로 씀
        public float dribbleN = 0.2f;      // 드리블(0~1): 높을수록 공을 잘 빼앗기지 않음
        public float shootN = 0.2f;        // 슈팅(0~1): 높을수록 슛이 빠르고 유효 슈팅이 많음

        public static ReplayStats From(PlayerData p, MatchRecord m, float cap)
        {
            cap = Mathf.Max(1f, cap);
            var s = new ReplayStats();
            s.staminaN = Mathf.Clamp01(p.stamina / cap);
            s.dribbleN = Mathf.Clamp01(p.dribbling / cap);
            s.shootN = Mathf.Clamp01(p.shooting / cap);
            float passN = Mathf.Clamp01(p.passing / cap);
            s.speedFactor = 0.8f + 0.5f * (s.staminaN * 0.6f + s.dribbleN * 0.4f);
            s.passAccuracy = m.passesAttempted > 0
                ? Mathf.Clamp(m.passesCompleted / (float)m.passesAttempted, 0.3f, 0.98f)
                : 0.55f + 0.40f * passN;
            return s;
        }
    }

    public class ReplayDot
    {
        public int team, slot;                 // team 0=내 팀(위쪽 골대로 공격), 1=상대
        public float x, y, tx, ty, vx, vy, maxSpeed = 7f;
    }

    // 점 22개와 공이 실제 축구처럼 움직이는 간단한 경기 엔진입니다. 화면과는 무관하게 위치만 계산합니다.
    // 좌표는 미터이고 가로 0~68, 세로 0~105 (세로 0이 위쪽 골대)입니다.
    public class MatchReplayEngine
    {
        public const float W = 68f, L = 105f;
        public const float LeadMinutes = 6f;   // 장면 시각보다 이만큼 먼저 공격을 시작합니다.

        static readonly float[] RoleDepth = { 0.03f, .20f, .20f, .20f, .20f, .42f, .42f, .42f, .42f, .63f, .63f };
        static readonly float[] RoleX = { 34f, 9f, 25f, 43f, 59f, 9f, 25f, 43f, 59f, 24f, 44f };
        static readonly float[] RoleShift = { 0.06f, .40f, .40f, .40f, .40f, .55f, .55f, .55f, .55f, .50f, .50f };

        enum Phase { None, Build, Pass, Receive, Shot, After, Press }
        const int KindPass = 0, KindGoal = 1, KindSaved = 2, KindWide = 3;

        public readonly ReplayDot[] dots = new ReplayDot[22];
        public float ballX = W / 2f, ballY = L / 2f, ballZ;
        public int youSlot = -1;
        public Action<ReplayEvent> GoalScored, AssistMade, ShotResolved, TackleWon, ShotTaken;
        public ReplayStats stats = new ReplayStats();

        readonly System.Random rng;
        readonly List<ReplayEvent> pending;
        ReplayEvent script;
        Phase phase = Phase.None;
        float phaseT, decideT = 1f, time, boost, clockNow;
        bool badPass;   // 내 선수의 패스가 빗나가는 중
        int carrier = -1, lastPoss, scriptTeam;
        float buildX, buildY, scorerOffset, shotSide, shotGx;

        // 공이 날아가는 중일 때
        bool flying;
        float fFx, fFy, fTx, fTy, fT, fDur, fZ;
        int fKind, fRecv;

        int YouIdx { get { return youSlot >= 0 ? Idx(0, youSlot) : -1; } }

        public bool Busy { get { return script != null || pending.Count > 0; } }

        public MatchReplayEngine(List<ReplayEvent> events, int youSlot, System.Random rng)
        {
            this.rng = rng; this.youSlot = youSlot;
            pending = new List<ReplayEvent>(events);
            for (int t = 0; t < 2; t++)
                for (int s = 0; s < 11; s++) dots[t * 11 + s] = new ReplayDot { team = t, slot = s };
            Kickoff(1);
            foreach (var d in dots) { FormationTarget(d); d.x = d.tx; d.y = d.ty; }
            carrier = Idx(0, 9);
            dots[carrier].x = W / 2f; dots[carrier].y = L / 2f + 1f;
        }

        static int Idx(int team, int slot) { return team * 11 + slot; }
        static float DepthY(int team, float depth) { return team == 0 ? L * (1f - depth) : L * depth; }
        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        float BallDepth(int team) { return team == 0 ? 1f - ballY / L : ballY / L; }

        // ---------- 한 걸음 ----------
        // dt: 경기 속의 초, clockMinute: 화면에 보이는 시계(분)
        public void Step(float dt, float clockMinute)
        {
            time += dt;
            clockNow = clockMinute;
            boost = Mathf.Max(0f, boost - dt);
            if (script == null && pending.Count > 0 && clockMinute >= pending[0].minute - LeadMinutes)
            {
                var next = pending[0]; pending.RemoveAt(0);
                BeginScript(next);
            }
            UpdateBall(dt);
            ComputeTargets();
            for (int i = 0; i < dots.Length; i++) MoveDot(dots[i], dt);
            if (script != null) UpdateScript(dt);
        }

        // ---------- 선수 위치 ----------
        void FormationTarget(ReplayDot d)
        {
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            float bd = Mathf.Clamp01(BallDepth(d.team));
            float depth, x;
            if (d.slot == 0)
            {
                depth = 0.03f + 0.08f * bd;
                x = 34f + (ballX - 34f) * 0.15f;
            }
            else
            {
                depth = RoleDepth[d.slot] + (bd - 0.5f) * RoleShift[d.slot] + (poss == d.team ? 0.04f : -0.03f);
                float rx = d.team == 0 ? RoleX[d.slot] : W - RoleX[d.slot];
                x = 34f + (rx - 34f) * 0.9f + (ballX - 34f) * 0.22f;
            }
            depth = Mathf.Clamp(depth, 0.03f, d.slot == 0 ? 0.14f : 0.95f);
            d.tx = x + Mathf.Sin(time * 0.9f + d.team * 3f + d.slot) * 0.8f;
            d.ty = DepthY(d.team, depth) + Mathf.Cos(time * 0.7f + d.slot) * 0.8f;
            d.maxSpeed = d.slot == 0 ? 4.5f : 7f;
        }

        void ComputeTargets()
        {
            for (int i = 0; i < dots.Length; i++) FormationTarget(dots[i]);
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            int press = 1 - poss;

            // 공을 갖지 않은 팀: 공에 가장 가까운 두 명이 압박
            int first = -1, second = -1;
            float firstD = float.MaxValue, secondD = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                var d = dots[Idx(press, s)];
                float dist = Mathf.Sqrt((d.x - ballX) * (d.x - ballX) + (d.y - ballY) * (d.y - ballY));
                if (dist < firstD) { second = first; secondD = firstD; first = s; firstD = dist; }
                else if (dist < secondD) { second = s; secondD = dist; }
            }
            if (first > 0) PressBall(dots[Idx(press, first)]);
            if (second > 0) PressBall(dots[Idx(press, second)]);

            if (carrier >= 0 && script == null)
            {
                var c = dots[carrier];
                if (c.slot != 0)
                {
                    float dir = c.team == 0 ? -1f : 1f;
                    c.ty = Mathf.Clamp(c.y + dir * 12f, 2f, L - 2f);
                    c.tx = c.x + (34f - c.x) * 0.15f + Mathf.Sin(time * 0.7f + carrier) * 3f;
                    c.maxSpeed = 7.8f;
                }
                else { c.tx = c.x; c.ty = c.y; }
            }
            if (flying && fKind == KindPass && fRecv >= 0)
            {
                var r = dots[fRecv];
                r.tx = fTx; r.ty = fTy; r.maxSpeed = 8f;
            }
            if (script != null) ApplyScriptTargets();
        }

        void PressBall(ReplayDot d) { d.tx = ballX; d.ty = ballY; d.maxSpeed = 8f; }

        void MoveDot(ReplayDot d, float dt)
        {
            float dx = d.tx - d.x, dy = d.ty - d.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float mult = 1f;
            if (YouIdx >= 0 && d == dots[YouIdx])   // 내 선수: 체력·드리블이 속도를, 체력이 후반 피로를 정함
                mult = stats.speedFactor * (1f - (1f - stats.staminaN) * 0.3f * clockNow / 90f);
            float speed = Mathf.Min(d.maxSpeed * mult * (boost > 0f ? 1.6f : 1f), dist * 2.5f);
            float vx = dist > 0.01f ? dx / dist * speed : 0f;
            float vy = dist > 0.01f ? dy / dist * speed : 0f;
            float k = 1f - Mathf.Exp(-6f * dt);
            d.vx += (vx - d.vx) * k; d.vy += (vy - d.vy) * k;
            d.x = Mathf.Clamp(d.x + d.vx * dt, 0f, W);
            d.y = Mathf.Clamp(d.y + d.vy * dt, 0f, L);
        }

        void Kickoff(int conceding)
        {
            ballX = W / 2f; ballY = L / 2f; ballZ = 0f;
            flying = false; lastPoss = conceding; boost = 2f;
            carrier = Idx(conceding, 9); decideT = 1f;
            var c = dots[carrier];
            if (c != null) { c.x = W / 2f; c.y = L / 2f + (conceding == 0 ? 1f : -1f); c.vx = 0f; c.vy = 0f; }
        }

        // ---------- 공 ----------
        void StartFlight(float tx, float ty, float dur, int kind, int recv, float zPeak)
        {
            flying = true;
            fFx = ballX; fFy = ballY; fTx = tx; fTy = ty; fT = 0f; fDur = dur; fKind = kind; fRecv = recv; fZ = zPeak;
            if (carrier >= 0) lastPoss = dots[carrier].team;
            carrier = -1;
        }

        void StartPass(int from, int to)
        {
            var r = dots[to];
            float tx = Mathf.Clamp(r.x + r.vx * 0.5f, 1f, W - 1f);
            float ty = Mathf.Clamp(r.y + r.vy * 0.5f, 1f, L - 1f);
            float dist = Mathf.Sqrt((tx - ballX) * (tx - ballX) + (ty - ballY) * (ty - ballY));
            StartFlight(tx, ty, Mathf.Max(0.45f, dist / 17f), KindPass, to, Mathf.Min(2.5f, dist * 0.05f));
        }

        // outcome: KindGoal / KindSaved / KindWide
        void StartShot(ReplayDot shooter, int outcome)
        {
            float goalY = shooter.team == 0 ? 0f : L;
            shotSide = rng.NextDouble() < 0.5 ? 1f : -1f;
            if (outcome == KindGoal) shotGx = 34f + shotSide * 3f;
            else if (outcome == KindSaved) shotGx = 34f + shotSide * Rand(1.5f, 3.5f);
            else shotGx = 34f + shotSide * Rand(4.8f, 9f);
            bool byYou = YouIdx >= 0 && shooter == dots[YouIdx];
            StartFlight(shotGx, goalY, byYou ? 0.5f - 0.15f * stats.shootN : 0.5f, outcome, -1, 1.6f);   // 슈팅이 좋을수록 빠른 슛
        }

        int ChoosePass(ReplayDot c)
        {
            int best = -1; float bestScore = -9f;
            for (int s = 0; s < 11; s++)
            {
                var m = dots[Idx(c.team, s)];
                if (m == c) continue;
                if (c.slot == 0 && !(s >= 1 && s <= 4)) continue;
                if (c.slot != 0 && s == 0) continue;
                float forward = c.team == 0 ? c.y - m.y : m.y - c.y;
                float dist = Mathf.Sqrt((m.x - c.x) * (m.x - c.x) + (m.y - c.y) * (m.y - c.y));
                if (dist < 5f || dist > 38f) continue;
                float score = forward * 0.04f - dist * 0.012f + (float)rng.NextDouble() * 0.5f;
                if (score > bestScore) { best = Idx(c.team, s); bestScore = score; }
            }
            return best;
        }

        void UpdateBall(float dt)
        {
            if (flying)
            {
                fT += dt;
                float u = Mathf.Min(1f, fT / fDur);
                ballX = Mathf.Lerp(fFx, fTx, u); ballY = Mathf.Lerp(fFy, fTy, u);
                ballZ = Mathf.Sin(Mathf.PI * u) * fZ;
                if (fT >= fDur) EndFlight();
            }
            else if (carrier >= 0)
            {
                var d = dots[carrier];
                float dir = d.team == 0 ? -1f : 1f;
                ballX = d.x; ballY = d.y + dir * 0.9f; ballZ = 0f;
                if (script == null)
                {
                    decideT -= dt;
                    if (decideT <= 0f) NaturalDecision();
                    for (int s = 1; s <= 10 && carrier >= 0; s++)   // 태클
                    {
                        var o = dots[Idx(1 - d.team, s)];
                        float dd = Mathf.Sqrt((o.x - d.x) * (o.x - d.x) + (o.y - d.y) * (o.y - d.y));
                        if (dd < 1.5f && rng.NextDouble() < 0.5 * dt * (carrier == YouIdx ? 1.4f - stats.dribbleN : 1f))
                        { carrier = Idx(1 - d.team, s); decideT = Rand(0.7f, 1.4f); break; }
                    }
                }
            }
        }

        void NaturalDecision()
        {
            var c = dots[carrier];
            float depth = c.team == 0 ? 1f - c.y / L : c.y / L;
            if (carrier != YouIdx && c.slot != 0 && depth > 0.78f && Mathf.Abs(c.x - 34f) < 18f && rng.NextDouble() < 0.4)
            {
                StartShot(c, rng.NextDouble() < 0.5 ? KindSaved : KindWide);
                return;
            }
            if (c.slot == 0 || rng.NextDouble() < 0.78)
            {
                int r = ChoosePass(c);
                if (r >= 0)
                {
                    badPass = carrier == YouIdx && rng.NextDouble() >= stats.passAccuracy;   // 패스 능력만큼만 정확함
                    StartPass(carrier, r);
                    return;
                }
            }
            decideT = Rand(0.6f, 1.4f);
        }

        void EndFlight()
        {
            flying = false;
            if (fKind == KindPass)
            {
                int team = dots[fRecv].team;
                int near = -1; float nearDist = float.MaxValue;
                for (int s = 0; s < 11; s++)
                {
                    var o = dots[Idx(1 - team, s)];
                    float dd = Mathf.Sqrt((o.x - fTx) * (o.x - fTx) + (o.y - fTy) * (o.y - fTy));
                    if (dd < nearDist) { near = Idx(1 - team, s); nearDist = dd; }
                }
                if (script == null && (badPass || (nearDist < 3.5f && rng.NextDouble() < 0.25))) carrier = near;   // 가로채기
                else carrier = fRecv;
                badPass = false;
                decideT = Rand(0.7f, 1.5f);
                if (script != null && phase == Phase.Pass) { phase = Phase.Receive; phaseT = 0f; }
                return;
            }
            int shooterTeam = lastPoss;
            if (fKind == KindGoal)
            {
                ballX = fTx; ballY = shooterTeam == 0 ? -1.2f : L + 1.2f; ballZ = 0f;   // 골망 안
                phase = Phase.After; phaseT = 0f;
                if (GoalScored != null && script != null) GoalScored(script);
            }
            else
            {
                int gk = Idx(1 - shooterTeam, 0);
                carrier = gk; ballX = dots[gk].x; ballY = dots[gk].y; decideT = 1.4f;
                if (script != null)
                {
                    if (ShotResolved != null) ShotResolved(script);
                    script = null; phase = Phase.None;
                }
            }
        }

        // ---------- 득점·슈팅 장면 ----------
        void BeginScript(ReplayEvent ev)
        {
            if (ev.type == ReplayEventType.TackleYou)
            {
                if (YouIdx < 0) return;   // 벤치라면 건너뜀
                // 내 선수와 가장 가까운 상대 선수가 공을 잡고 달려오고, 내 선수가 따라가 공을 빼앗습니다.
                script = ev; phase = Phase.Press; phaseT = 0f; scriptTeam = 1;
                var you = dots[YouIdx];
                int best = 1; float bestDist = float.MaxValue;
                for (int s = 1; s <= 10; s++)
                {
                    var o = dots[Idx(1, s)];
                    float dd = Mathf.Sqrt((o.x - you.x) * (o.x - you.x) + (o.y - you.y) * (o.y - you.y));
                    if (dd < bestDist) { best = s; bestDist = dd; }
                }
                flying = false; carrier = Idx(1, best);
                var opp = dots[carrier];
                ballX = opp.x; ballY = opp.y; lastPoss = 1;
                return;
            }
            script = ev; phase = Phase.Build; phaseT = 0f;
            scriptTeam = ev.type == ReplayEventType.GoalTheirs ? 1 : 0;
            int carrierSlot = ev.assisterSlot >= 0 ? ev.assisterSlot : ev.scorerSlot;
            flying = false;
            carrier = Idx(scriptTeam, carrierSlot);
            var c = dots[carrier];
            ballX = c.x; ballY = c.y; lastPoss = scriptTeam;
            buildY = DepthY(scriptTeam, ev.assisterSlot >= 0 ? 0.68f : 0.80f);
            buildX = Mathf.Clamp(c.x + (34f - c.x) * 0.4f + Rand(-6f, 6f), 8f, 60f);
            scorerOffset = Rand(-6f, 6f);
        }

        void UpdateScript(float dt)
        {
            phaseT += dt;
            var ev = script;
            if (phase == Phase.Press)
            {
                var opp = dots[carrier]; var you = dots[YouIdx];
                float dd = Mathf.Sqrt((opp.x - you.x) * (opp.x - you.x) + (opp.y - you.y) * (opp.y - you.y));
                if (dd < 1.8f || phaseT > 6f)   // 가까워지면 태클 성공
                {
                    carrier = YouIdx; lastPoss = 0; decideT = 0.8f;
                    ballX = you.x; ballY = you.y;
                    if (TackleWon != null) TackleWon(ev);
                    script = null; phase = Phase.None;
                }
                return;
            }
            if (phase == Phase.Build)
            {
                var c = dots[carrier];
                float dist = Mathf.Sqrt((buildX - c.x) * (buildX - c.x) + (buildY - c.y) * (buildY - c.y));
                if ((dist < 2.5f && phaseT > 2.5f) || phaseT > 7f)
                {
                    if (ev.assisterSlot >= 0)
                    {
                        StartPass(carrier, Idx(scriptTeam, ev.scorerSlot));
                        phase = Phase.Pass;
                        if (ev.youAssist && AssistMade != null) AssistMade(ev);
                    }
                    else ShootScript();
                }
            }
            else if (phase == Phase.Receive)
            {
                if (phaseT > 0.45f) ShootScript();
            }
            else if (phase == Phase.After)
            {
                if (phaseT > 1.4f)
                {
                    Kickoff(1 - scriptTeam);
                    script = null; phase = Phase.None;
                }
            }
        }

        void ShootScript()
        {
            var ev = script;
            carrier = Idx(scriptTeam, ev.scorerSlot);
            var c = dots[carrier];
            ballX = c.x; ballY = c.y;
            if (ShotTaken != null && (ev.youScore || ev.type == ReplayEventType.ShotYou)) ShotTaken(ev);
            if (ev.type == ReplayEventType.ShotYou) StartShot(c, ev.saved ? KindSaved : KindWide);
            else StartShot(c, KindGoal);
            phase = Phase.Shot;
        }

        void ApplyScriptTargets()
        {
            var ev = script;
            if (phase == Phase.Press)
            {
                var opp = dots[carrier]; var you = dots[YouIdx];
                opp.tx = opp.x + (34f - opp.x) * 0.2f; opp.ty = Mathf.Min(L - 2f, opp.y + 10f); opp.maxSpeed = 7.5f;
                you.tx = opp.x; you.ty = opp.y; you.maxSpeed = 9f;
                return;
            }
            for (int s = 1; s <= 10; s++)
            {
                var d = dots[Idx(scriptTeam, s)];
                if ((phase == Phase.Build || phase == Phase.Pass || phase == Phase.Receive) && s >= 5)
                { d.ty = DepthY(scriptTeam, 0.70f + 0.03f * (s % 3)); d.maxSpeed = 8f; }
            }
            var scorer = dots[Idx(scriptTeam, ev.scorerSlot)];
            if (phase == Phase.Build || phase == Phase.Pass)
            { scorer.tx = 34f + scorerOffset; scorer.ty = DepthY(scriptTeam, 0.84f); scorer.maxSpeed = 9f; }
            else if (phase == Phase.Receive || phase == Phase.Shot)
            { scorer.tx = scorer.x; scorer.ty = scorer.y; }
            if (phase == Phase.Build && carrier >= 0)
            { var c = dots[carrier]; c.tx = buildX; c.ty = buildY; c.maxSpeed = 8.5f; }
            var gk = dots[Idx(1 - scriptTeam, 0)];
            if (phase == Phase.Shot && flying)
            {
                if (ev.type != ReplayEventType.ShotYou) gk.tx = 34f - shotSide * 3f;          // 골: 골키퍼는 반대 방향으로 몸을 던짐
                else if (ev.saved) gk.tx = shotGx;                                            // 선방
                gk.maxSpeed = 7f;
            }
        }
    }
}
