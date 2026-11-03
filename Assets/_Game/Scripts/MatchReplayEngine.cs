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
        public float ax, ay;                   // 대형 위치를 천천히 따라가는 기준점 (목표가 툭툭 바뀌지 않게)
        public float ox, oy;                   // 선수마다 다른 위치 버릇 (줄이 반듯하게 서지 않게)
        public float noise;                    // 제자리에서 어슬렁거리는 움직임의 씨앗
        public float runT, runDx;              // 공 없이 앞으로 뛰어드는 중이면 남은 시간과 옆 방향
        public bool started;                   // 기준점을 처음 위치로 잡았는가
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
                for (int s = 0; s < 11; s++)
                    dots[t * 11 + s] = new ReplayDot
                    {
                        team = t, slot = s,
                        ox = s == 0 ? 0f : Rand(-3f, 3f), oy = s == 0 ? 0f : Rand(-0.025f, 0.025f), noise = Rand(0f, 100f)
                    };
            Kickoff(1);
            foreach (var d in dots) { FormationTarget(d, 0f); d.x = d.tx; d.y = d.ty; }
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
            UpdateRuns(dt);
            ComputeTargets(dt);
            for (int i = 0; i < dots.Length; i++) MoveDot(dots[i], dt);
            if (script != null) UpdateScript(dt);
        }

        // ---------- 선수 위치 ----------
        // 속도(m/s): 평소에는 걷거나 조깅하고, 압박·침투·공 받기처럼 급할 때만 전력으로 뜁니다.
        const float JogSpeed = 4.2f, CarrySpeed = 5.6f, SprintSpeed = 7.8f, KeeperSpeed = 3.6f;
        const float Accel = 4.5f, Decel = 7f;     // 가속·감속 한계 (m/s²). 출발과 멈춤, 방향 전환에 시간이 걸립니다.

        // 대형 안의 자리. 실제 축구처럼 수비할 때는 공 쪽으로 좁게 모이고, 공격할 때는 넓게 벌립니다.
        // 결과는 기준점(ax, ay)이 천천히 따라가서, 공이 빨리 움직여도 선수 위치가 툭툭 바뀌지 않습니다.
        void FormationTarget(ReplayDot d, float dt)
        {
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            bool attacking = poss == d.team;
            float bd = Mathf.Clamp01(BallDepth(d.team));
            float depth, x;
            if (d.slot == 0)
            {
                depth = 0.03f + 0.08f * bd;
                x = 34f + (ballX - 34f) * 0.2f;
            }
            else
            {
                depth = RoleDepth[d.slot] + (bd - 0.5f) * RoleShift[d.slot] + (attacking ? 0.05f : -0.04f) + d.oy;
                float rx = d.team == 0 ? RoleX[d.slot] : W - RoleX[d.slot];
                float width = attacking ? 0.95f : 0.68f;   // 수비 때는 가운데로 좁힘
                float slide = attacking ? 0.22f : 0.38f;   // 수비 때는 공 쪽으로 더 쏠림
                x = 34f + (rx - 34f) * width + (ballX - 34f) * slide + d.ox;
                bool fullBack = d.slot == 1 || d.slot == 4;
                bool ballSide = (rx < 34f) == (ballX < 34f);
                if (attacking && fullBack && ballSide && bd > 0.45f) depth += 0.12f;   // 공 쪽 풀백은 오버래핑
                if (d.runT > 0f) { depth += 0.16f; x += d.runDx; }                   // 공 없이 침투
            }
            depth = Mathf.Clamp(depth, 0.03f, d.slot == 0 ? 0.14f : 0.95f);
            float fx = Mathf.Clamp(x, 2f, W - 2f), fy = DepthY(d.team, depth);
            if (!d.started) { d.ax = fx; d.ay = fy; d.started = true; }
            float follow = 1f - Mathf.Exp(-(d.runT > 0f ? 2.5f : 1.3f) * dt);
            d.ax += (fx - d.ax) * follow; d.ay += (fy - d.ay) * follow;
            // 제자리에서도 조금씩 자리를 고치는 움직임 (선수마다 다른 느린 흔들림)
            float wx = (Noise(d.noise, time * 0.18f) - 0.5f) * 4f;
            float wy = (Noise(d.noise + 37f, time * 0.15f) - 0.5f) * 4f;
            d.tx = d.ax + (d.slot == 0 ? wx * 0.25f : wx);
            d.ty = d.ay + (d.slot == 0 ? wy * 0.15f : wy);
            d.maxSpeed = d.slot == 0 ? KeeperSpeed : (d.runT > 0f ? SprintSpeed : JogSpeed);
        }

        // 공을 가진 팀의 미드필더·공격수가 가끔 수비 뒷공간으로 뛰어듭니다.
        void UpdateRuns(float dt)
        {
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            for (int i = 0; i < dots.Length; i++)
            {
                var d = dots[i];
                if (d.runT > 0f) { d.runT -= dt; if (d.team != poss) d.runT = 0f; continue; }
                if (d.team != poss || d.slot < 5 || i == carrier || script != null) continue;
                if (rng.NextDouble() < (d.slot >= 9 ? 0.09 : 0.04) * dt)
                {
                    d.runT = Rand(2.5f, 4.5f);
                    d.runDx = Rand(-7f, 7f);
                }
            }
        }

        int presser = -1, coverer = -1;   // 공을 막는 선수, 패스 길을 막는 선수 (자주 바뀌지 않게 기억)

        void ComputeTargets(float dt)
        {
            for (int i = 0; i < dots.Length; i++) FormationTarget(dots[i], dt);
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            int press = 1 - poss;

            // 공을 갖지 않은 팀: 가장 가까운 선수가 공을 막고, 다음 선수는 가까운 패스 길을 막습니다.
            // 원래 압박하던 선수가 3m 넘게 더 멀어져야 다른 선수로 바뀝니다.
            int first = -1; float firstD = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                float dist = DistTo(dots[Idx(press, s)], ballX, ballY);
                if (dist < firstD) { first = Idx(press, s); firstD = dist; }
            }
            if (presser < 0 || dots[presser].team != press || DistTo(dots[presser], ballX, ballY) > firstD + 3f) presser = first;
            int second = -1; float secondD = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(press, s);
                if (i == presser) continue;
                float dist = DistTo(dots[i], ballX, ballY);
                if (dist < secondD) { second = i; secondD = dist; }
            }
            if (coverer < 0 || coverer == presser || dots[coverer].team != press || DistTo(dots[coverer], ballX, ballY) > secondD + 3f) coverer = second;
            if (presser >= 0) PressBall(dots[presser], poss);
            if (coverer >= 0) CoverLane(dots[coverer], poss);

            if (carrier >= 0 && script == null)
            {
                var c = dots[carrier];
                if (c.slot != 0)
                {
                    // 공을 몰고 앞으로: 곧게 달리지 않고 빈 곳을 찾아 천천히 방향을 틉니다. 수비가 가까우면 느려집니다.
                    float dir = c.team == 0 ? -1f : 1f;
                    float weave = (Noise(c.noise + 11f, time * 0.35f) - 0.5f) * 16f;
                    c.ty = Mathf.Clamp(c.y + dir * 10f, 2f, L - 2f);
                    c.tx = Mathf.Clamp(c.x + (34f - c.x) * 0.12f + weave, 3f, W - 3f);
                    float near = presser >= 0 ? DistTo(dots[presser], c.x, c.y) : 99f;
                    c.maxSpeed = near < 4f ? JogSpeed : CarrySpeed;
                }
                else { c.tx = c.x; c.ty = c.y; }
            }
            if (flying && fKind == KindPass && fRecv >= 0)
            {
                var r = dots[fRecv];
                r.tx = fTx; r.ty = fTy; r.maxSpeed = SprintSpeed;
            }
            Separate();
            if (script != null) ApplyScriptTargets();
        }

        static float DistTo(ReplayDot d, float x, float y) { return Mathf.Sqrt((d.x - x) * (d.x - x) + (d.y - y) * (d.y - y)); }

        // 부드럽게 변하는 0~1 값 (값 노이즈). seed가 다르면 다른 흐름이 나옵니다.
        static float Noise(float seed, float t)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i, u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(seed, i), Hash(seed, i + 1), u);
        }

        static float Hash(float seed, int i)
        {
            float h = Mathf.Sin(i * 127.1f + seed * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // 공 앞 1.5m(자기 골대 쪽)에 서서 막습니다.
        void PressBall(ReplayDot d, int possTeam)
        {
            float toOwnGoal = d.team == 0 ? 1f : -1f;
            d.tx = ballX; d.ty = ballY + toOwnGoal * 1.5f; d.maxSpeed = SprintSpeed;
        }

        // 공을 가진 선수와 가장 가까운 동료 사이에 서서 패스 길을 막습니다.
        void CoverLane(ReplayDot d, int possTeam)
        {
            int mate = -1; float best = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(possTeam, s);
                if (i == carrier) continue;
                float dist = DistTo(dots[i], ballX, ballY);
                if (dist < best) { best = dist; mate = i; }
            }
            if (mate < 0) return;
            d.tx = Mathf.Lerp(ballX, dots[mate].x, 0.55f);
            d.ty = Mathf.Lerp(ballY, dots[mate].y, 0.55f);
            d.maxSpeed = (SprintSpeed + JogSpeed) * 0.5f;
        }

        // 같은 팀끼리 4m 안으로 겹치면 목표를 서로 밀어냅니다. (공을 가진 선수와 골키퍼는 그대로)
        void Separate()
        {
            const float Min = 4f;
            for (int t = 0; t < 2; t++)
                for (int a = 1; a <= 10; a++)
                    for (int b = a + 1; b <= 10; b++)
                    {
                        var p = dots[Idx(t, a)]; var q = dots[Idx(t, b)];
                        float dx = q.tx - p.tx, dy = q.ty - p.ty;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist >= Min) continue;
                        if (dist < 0.01f) { dx = 1f; dy = 0f; dist = 1f; }
                        float push = (Min - dist) * 0.5f;
                        bool pFixed = Idx(t, a) == carrier, qFixed = Idx(t, b) == carrier;
                        if (!pFixed) { p.tx -= dx / dist * push; p.ty -= dy / dist * push; }
                        if (!qFixed) { q.tx += dx / dist * push; q.ty += dy / dist * push; }
                    }
        }

        // 목표 쪽으로 속도를 바꾸되, 가속·감속 한계 안에서만 바꿉니다. 목표에 가까워지면 미리 속도를 줄입니다.
        void MoveDot(ReplayDot d, float dt)
        {
            float dx = d.tx - d.x, dy = d.ty - d.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float mult = 1f;
            if (YouIdx >= 0 && d == dots[YouIdx])   // 내 선수: 체력·드리블이 속도를, 체력이 후반 피로를 정함
                mult = stats.speedFactor * (1f - (1f - stats.staminaN) * 0.3f * clockNow / 90f);
            float top = d.maxSpeed * mult * (boost > 0f ? 1.3f : 1f);
            float speed = Mathf.Min(top, Mathf.Sqrt(2f * Decel * 0.6f * dist));   // 멈출 거리를 생각해 미리 감속
            if (dist < 0.4f) speed = 0f;
            float wantX = dist > 0.01f ? dx / dist * speed : 0f;
            float wantY = dist > 0.01f ? dy / dist * speed : 0f;
            float cx = wantX - d.vx, cy = wantY - d.vy;
            float change = Mathf.Sqrt(cx * cx + cy * cy);
            bool slowing = wantX * wantX + wantY * wantY < d.vx * d.vx + d.vy * d.vy;
            float limit = (slowing ? Decel : Accel) * dt;
            if (change > limit) { cx *= limit / change; cy *= limit / change; }
            d.vx += cx; d.vy += cy;
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
