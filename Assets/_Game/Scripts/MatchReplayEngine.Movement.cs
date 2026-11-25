using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 경기 영상 엔진: 선수 위치·대형·압박·이동
    public partial class MatchReplayEngine
    {
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
                depth = 0.02f + 0.09f * bd;   // 공이 가까우면 골라인 가까이(2m), 멀면 조금 앞으로 나옴(최대 약 12m)
                x = 34f + (ballX - 34f) * 0.2f;
            }
            else
            {
                // 포메이션 자리 + 공 위치에 따른 이동 + 스타일 (공격 때 올라서고, 수비 때 내려서거나 높게 섬)
                var st = sp[d.team];
                depth = d.def.depth + (bd - 0.5f) * d.def.shift + (attacking ? st.attackPush : st.defendDrop) + d.oy;
                if (attacking && counterT[d.team] > 0f && d.def.role != SquadRole.DF) depth += 0.08f;   // 역습: 앞으로 달려 나감
                float adv = attacking ? Mathf.Clamp01((bd - 0.35f) / 0.35f) : 0f;   // 공이 중앙선을 넘어 들어간 정도
                if (attacking) depth += SupportPush(d.def.kind) * st.support * adv;   // 팀 전체가 같이 올라섬
                float rx = d.team == 0 ? d.def.x : W - d.def.x;
                float width = attacking ? st.attackWidth : st.defendWidth;   // 수비 때는 가운데로 좁힘
                float slide = attacking ? 0.22f : st.slide;                 // 수비 때는 공 쪽으로 쏠림
                x = 34f + (rx - 34f) * width + (ballX - 34f) * slide + d.ox;
                bool wideBack = d.def.kind == SlotKind.FB || d.def.kind == SlotKind.WB;
                bool ballSide = (rx < 34f) == (ballX < 34f);
                if (attacking && wideBack && ballSide && bd > 0.45f) depth += 0.12f * st.overlap;   // 공 쪽 풀백·윙백 오버래핑
                if (d.runT > 0f) { depth += 0.16f; x += d.runDx; }                                  // 공 없이 침투
            }
            depth = d.slot == 0 ? Mathf.Clamp(depth, 0.02f, 0.12f) : Mathf.Clamp(depth, MinOutfield, 0.95f);   // 필드 선수는 골키퍼보다 뒤로 가지 않음
            float fx = Mathf.Clamp(x, 2f, W - 2f), fy = DepthY(d.team, depth);
            if (!d.started) { d.ax = fx; d.ay = fy; d.started = true; }
            // 공격 때 뒤쪽 줄(수비·미드필더)은 빨리 따라 올라와 간격을 좁힙니다.
            bool backLine = d.slot != 0 && d.def.role != SquadRole.FW && d.def.kind != SlotKind.W && d.def.kind != SlotKind.ST;
            bool pushing = backLine && (carrier >= 0 ? dots[carrier].team : lastPoss) == d.team && fy != d.ay && (d.team == 0 ? fy < d.ay : fy > d.ay);
            float follow = 1f - Mathf.Exp(-(d.runT > 0f ? 2.5f : pushing ? 2.2f : 1.3f) * dt);
            d.ax += (fx - d.ax) * follow; d.ay += (fy - d.ay) * follow;
            // 제자리에서도 조금씩 자리를 고치는 움직임 (선수마다 다른 느린 흔들림)
            float wx = (Noise(d.noise, time * 0.18f) - 0.5f) * 4f;
            float wy = (Noise(d.noise + 37f, time * 0.15f) - 0.5f) * 4f;
            d.tx = d.ax + (d.slot == 0 ? wx * 0.25f : wx);
            d.ty = d.ay + (d.slot == 0 ? wy * 0.15f : wy);
            d.maxSpeed = d.slot == 0 ? KeeperSpeed : (d.runT > 0f ? SprintSpeed : pushing ? (JogSpeed + SprintSpeed) / 2f : JogSpeed);
        }

        // 공이 상대 진영 깊이 들어갔을 때 자리별로 더 올라서는 깊이. 뒤쪽 줄일수록 많이 따라 올라와
        // 공격수만 따로 노는 대신 미드필더는 상자 앞까지, 풀백은 측면 높이까지, 센터백은 중앙선 근처까지 올라섭니다.
        static float SupportPush(SlotKind k)
        {
            switch (k)
            {
                case SlotKind.CB: return 0.16f;
                case SlotKind.FB: case SlotKind.WB: return 0.30f;
                case SlotKind.DM: return 0.14f;
                case SlotKind.CM: return 0.18f;
                case SlotKind.WM: return 0.14f;
                case SlotKind.AM: return 0.08f;
                case SlotKind.W: return 0.03f;
                default: return 0f;
            }
        }

        // 공을 가진 팀의 미드필더·공격수가 가끔 수비 뒷공간으로 뛰어듭니다.
        // 공이 상대 진영에 있으면 풀백·윙백도 측면으로 오버래핑해 올라갑니다.
        void UpdateRuns(float dt)
        {
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            for (int i = 0; i < dots.Length; i++)
            {
                var d = dots[i];
                if (d.runT > 0f) { d.runT -= dt; d.runAge += dt; if (d.team != poss) d.runT = 0f; continue; }
                bool wideBack = d.def.kind == SlotKind.FB || d.def.kind == SlotKind.WB;
                if (d.team != poss || d.slot == 0 || (d.def.role == SquadRole.DF && !wideBack) || i == carrier || script != null) continue;
                if (wideBack && BallDepth(d.team) < 0.5f) continue;   // 풀백은 공이 상대 진영에 있을 때만
                var st = sp[d.team];
                bool front = d.def.kind == SlotKind.ST || d.def.kind == SlotKind.W || d.def.kind == SlotKind.AM;
                float rate = front ? st.runForward : wideBack ? st.runMid * st.overlap * 4f : st.runMid * 1.4f;
                if (counterT[d.team] > 0f && front) rate *= 6f;   // 역습: 앞쪽 선수들이 한꺼번에 뛰어듦
                bool onLine = Dp(d.team, d.y) > SafeDepth(d.team) - 0.06f;   // 마지막 수비수 어깨(6m 안)에 있는 선수
                if (front && onLine && BallDepth(d.team) > 0.35f) rate *= 3f;   // 라인 위의 공격수는 뒷공간을 자주 노림
                if (rng.NextDouble() < rate * dt)
                {
                    d.runT = Rand(2.5f, 4.5f); d.runAge = 0f;
                    // 라인 가까이(6m 안)에 있던 선수는 가끔 너무 일찍 뛰어 라인을 넘음 → 그때 패스가 오면 오프사이드
                    d.earlyRun = onLine && rng.NextDouble() < 0.55;
                    d.runDx = d.def.kind == SlotKind.W ? (d.def.x < 34f ? Rand(4f, 10f) : Rand(-10f, -4f))   // 윙어는 안쪽으로 파고듦
                            : wideBack ? 0f : Rand(-7f, 7f);                                                   // 풀백은 측면을 따라 올라감
                }
            }
        }

        int presser = -1, coverer = -1;   // 공을 막는 선수, 패스 길을 막는 선수 (자주 바뀌지 않게 기억)

        void ComputeTargets(float dt)
        {
            for (int i = 0; i < dots.Length; i++) FormationTarget(dots[i], dt);
            if (restart != Restart.None) { RestartTargets(); Separate(); return; }
            int poss = carrier >= 0 ? dots[carrier].team : lastPoss;
            int press = 1 - poss;
            // 공을 잃은 순간: 그 팀의 전환 시간 시작
            if (prevPoss >= 0 && poss != prevPoss && !flying) lossT[prevPoss] = 3f;
            if (!flying) prevPoss = poss;
            lossT[0] -= dt; lossT[1] -= dt;
            TeamShape(poss);

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
            // 스타일에 따라 압박 위치가 다름: 전방 압박은 어디서든 둘이서, 내려선 수비·역습은 우리 진영에 들어와야 압박
            var ps = sp[press];
            bool pressNow = BallDepth(press) <= ps.pressFrom;
            if (presser >= 0) { if (pressNow) PressBall(dots[presser], poss); else HoldOff(dots[presser]); }
            if (coverer >= 0) { if (pressNow && ps.doublePress) PressBall(dots[coverer], poss, 2.5f); else CoverLane(dots[coverer], poss); }
            // 공을 막 잃은 팀 (3초): 점유·전방 압박 팀은 세 번째 선수까지 바로 되찾으러, 나머지 팀은 자기 진영으로 빠르게 복귀
            if (lossT[press] > 0f && script == null)
            {
                bool gegen = ps.pressFrom >= 0.75f;
                if (gegen)
                {
                    int third = -1; float td = float.MaxValue;
                    for (int s = 1; s <= 10; s++)
                    {
                        int i = Idx(press, s); if (i == presser || i == coverer) continue;
                        float dd = DistTo(dots[i], ballX, ballY);
                        if (dd < td) { td = dd; third = i; }
                    }
                    if (third >= 0 && td < 18f) PressBall(dots[third], poss, -2.5f);
                }
                else
                {
                    for (int s = 1; s <= 10; s++)
                    {
                        int i = Idx(press, s); if (i == presser || i == coverer) continue;
                        var d = dots[i];
                        d.ty = DepthY(press, Mathf.Max(0.04f, Dp(press, d.ty) - 0.07f)); d.maxSpeed = SprintSpeed * 0.9f;
                    }
                }
            }

            if (carrier >= 0 && script == null)
            {
                var c = dots[carrier];
                if (c.slot != 0 && stagedRunner >= 0)   // 오프사이드 장면: 동료가 뛰어드는 동안 멈추지 않고 옆으로 천천히 몰며 기다림
                {
                    float dir = c.team == 0 ? -1f : 1f;
                    float weave = (Noise(c.noise + 11f, time * 0.5f) - 0.5f) * 14f;
                    c.ty = Mathf.Clamp(c.y + dir * 3f, 2f, L - 2f);
                    c.tx = Mathf.Clamp(c.x + weave, 3f, W - 3f);
                    c.maxSpeed = JogSpeed * 0.7f;
                }
                else if (c.slot != 0)
                {
                    // 공을 몰고 앞으로: 곧게 달리지 않고 빈 곳을 찾아 천천히 방향을 틉니다. 수비가 가까우면 느려집니다.
                    float dir = c.team == 0 ? -1f : 1f;
                    float weave = (Noise(c.noise + 11f, time * 0.35f) - 0.5f) * 16f;
                    c.ty = Mathf.Clamp(c.y + dir * 10f, 2f, L - 2f);
                    c.tx = Mathf.Clamp(c.x + (34f - c.x) * 0.12f + weave, 3f, W - 3f);
                    float near = presser >= 0 ? DistTo(dots[presser], c.x, c.y) : 99f;
                    c.maxSpeed = near < 4f ? JogSpeed : CarrySpeed;
                }
                else if (gkHoldT > 0f)   // 공을 잡은 골키퍼: 공을 안고 몇 걸음 앞으로 나온 뒤 동료에게
                { c.tx = c.x + (34f - c.x) * 0.3f; c.ty = DepthY(c.team, 0.09f); c.maxSpeed = 2.4f; }
                else { c.tx = c.x; c.ty = c.y; }
            }
            if (flying && (fKind == KindPass || fKind == KindOut || fKind == KindLoose) && fRecv >= 0)
            {
                var r = dots[fRecv];
                r.tx = fTx; r.ty = fTy; r.maxSpeed = SprintSpeed;
            }
            // 슈팅이 날아오면 골키퍼가 공 쪽으로 몸을 날림: 막는 슛은 공이 오는 곳까지, 빗나가는 슛은 반쯤 뻗다 맒
            if (flying && (fKind == KindSaved || fKind == KindWide))
            {
                var gk = dots[Idx(1 - lastPoss, 0)];
                float reach = fKind == KindSaved ? 1f : 0.5f;
                gk.tx = gk.x + (Mathf.Clamp(fTx, 30f, 38f) - gk.x) * reach;
                if (fKind == KindSaved) gk.ty = fTy;
                gk.maxSpeed = KeeperDive;
            }
            Separate();
            if (script != null) ApplyScriptTargets();
        }

        static float DistTo(ReplayDot d, float x, float y) { return Mathf.Sqrt((d.x - x) * (d.x - x) + (d.y - y) * (d.y - y)); }

        // 그 팀 골대에서 잰 깊이 (0 자기 골라인, 1 상대 골라인)
        float Dp(int team, float y) { return team == 0 ? 1f - y / L : y / L; }

        // 공격 팀이 오프사이드가 아닌 가장 깊은 곳: 상대에서 두 번째로 뒤에 있는 선수(골키퍼 포함), 공, 중앙선 중 가장 앞
        float SafeDepth(int att)
        {
            float a = 0f, b = 0f;   // 상대 선수 중 가장 깊은 둘 (공격 팀 기준 깊이)
            for (int s = 0; s < 11; s++)
            {
                float dd = Dp(att, dots[Idx(1 - att, s)].y);
                if (dd > a) { b = a; a = dd; } else if (dd > b) b = dd;
            }
            return Mathf.Max(b, Mathf.Max(Dp(att, ballY), 0.5f));
        }

        // 패스 순간 받는 선수가 오프사이드 자리인지 기억 (경기 중 일반 패스만. 재개 킥·득점 장면은 제외)
        // 규칙대로: 스로인·골킥·코너킥에서 바로 받으면 오프사이드가 아님. 프리킥은 오프사이드가 있음
        void MarkOffside(int recv)
        {
            offsidePass = false;
            if (script != null || recv < 0) return;
            if (executing && (execKind == Restart.ThrowIn || execKind == Restart.GoalKick || execKind == Restart.Corner || execKind == Restart.Kickoff)) return;
            if (!executing && restart != Restart.None) return;
            var r = dots[recv];
            float line = SafeDepth(r.team);
            if (Dp(r.team, r.y) > line + 0.005f) { offsidePass = true; offX = r.x; offY = r.y; offLine = line; offTeam = r.team; }
        }

        // 득점 장면: 공격 팀에서 가장 깊이 들어간 선수보다 상대 수비 한 명이 늘 조금 더 뒤에 있게 (마지막 수비수가 따라 내려옴)
        const float MinOutfield = 0.06f;   // 필드 선수가 자기 골라인에서 떨어져 서는 최소 거리 (약 6m, 골키퍼보다 앞)

        void KeepSceneOnside()
        {
            int att = scriptTeam, def = 1 - att;
            float deepest = 0f;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(att, s); if (i == carrier) continue;
                var d = dots[i];
                deepest = Mathf.Max(deepest, Mathf.Max(Dp(att, d.y), Dp(att, d.ty)));
            }
            float need = Mathf.Min(1f - MinOutfield, deepest + 0.015f);   // 수비수도 골키퍼보다 뒤로는 가지 않음
            if (need <= Mathf.Max(Dp(att, ballY), 0.5f)) return;   // 공보다 뒤나 자기 진영이면 오프사이드가 아님
            int best = -1; float bd = -1f;
            for (int s = 1; s <= 10; s++)
            {
                float dd = Dp(att, dots[Idx(def, s)].y);
                if (dd > bd) { bd = dd; best = Idx(def, s); }
            }
            if (best < 0 || bd >= need) return;
            var m = dots[best];
            m.ty = DepthY(att, need); m.maxSpeed = SprintSpeed * 1.15f;
        }

        // 팀 대형 다듬기 (실제 전술 참고)
        //  공격 팀: 공 없는 선수는 오프사이드 라인 안쪽에서 기다렸다가 패스가 나오면 뒷공간으로 (침투 타이밍).
        //          최전방 공격수는 공이 중앙선을 넘으면 마지막 수비수 어깨에 붙어 섬.
        //  수비 팀: 수비수는 한 줄로 서서 같이 올라가고 내려섬 (전방 압박 팀은 높게, 내려선 수비 팀은 깊게).
        //          미드필더는 수비 라인 앞 7~15m 안, 공격수는 라인 앞 38m 안으로 간격을 좁힘.
        void TeamShape(int poss)
        {
            if (script != null) return;   // 득점 장면은 장면대로
            float safe = SafeDepth(poss), lim = safe - 0.008f;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(poss, s);
                if (i == carrier || (flying && fRecv == i)) continue;
                var d = dots[i];
                if (d.runT > 0f && d.earlyRun)   // 너무 일찍 뛴 선수는 마지막 수비수를 2~3m 지나 있음
                {
                    if (BallDepth(poss) > 0.35f || i == stagedRunner) d.ty = DepthY(poss, Mathf.Min(0.97f, safe + 0.025f));
                    continue;
                }
                float td = Dp(poss, d.ty);
                if (td > lim) { td = lim; d.ty = DepthY(poss, td); }
                if (d.def.kind == SlotKind.ST && BallDepth(poss) > 0.4f && td < lim - 0.04f)
                    d.ty = DepthY(poss, Mathf.Lerp(td, lim - 0.015f, 0.6f));
            }
            // 수비 팀 (실제 축구처럼):
            //  · 수비 라인은 공과 일정한 간격을 두고 같이 올라서고 내려섬 (전방 압박은 높고 좁게, 내려선 수비는 깊게). 골키퍼보다 뒤로는 내려가지 않음
            //  · 공이 우리 진영 깊이 오면 센터백은 상자 안의 공격수를 골문 쪽에서 따라 붙음 (마킹)
            //  · 공 쪽 풀백은 측면의 공을 막으러 나가고, 반대쪽 풀백은 가운데로 좁힘
            int def = 1 - poss;
            var st = sp[def];
            float ballD = Dp(def, ballY);   // 수비 팀 골에서 잰 공의 깊이
            float gap = st.pressFrom >= 1f ? 0.16f : st.pressFrom <= 0.45f ? 0.27f : 0.21f;
            float maxLine = st.pressFrom >= 1f ? 0.46f : st.pressFrom <= 0.45f ? 0.27f : 0.38f;
            float line = Mathf.Clamp(ballD - gap, 0.07f, maxLine);
            bool danger = ballD < 0.32f;
            var marked = new List<int>();
            for (int s = 1; s <= 10; s++)
            {
                var d = dots[Idx(def, s)];
                float td = Dp(def, d.ty);
                bool wideBack = d.def.kind == SlotKind.FB || d.def.kind == SlotKind.WB;
                if (d.def.role == SquadRole.DF)
                {
                    td = line + (wideBack ? 0.01f : 0f);
                    if (danger && !wideBack)   // 센터백 마킹: 가장 가까운, 아직 아무도 안 붙은 상자 근처 공격수
                    {
                        int mk = -1; float md = 18f;
                        for (int k = 1; k <= 10; k++)
                        {
                            int ai = Idx(poss, k); if (ai == carrier || marked.Contains(ai)) continue;
                            var a = dots[ai];
                            if (Dp(def, a.y) > 0.24f || Mathf.Abs(a.x - 34f) > 22f) continue;
                            float dd = DistTo(d, a.x, a.y);
                            if (dd < md) { md = dd; mk = ai; }
                        }
                        if (mk >= 0)
                        {
                            marked.Add(mk); var a = dots[mk];
                            td = Mathf.Min(td, Dp(def, a.y) - 0.02f);   // 공격수보다 골문 쪽 2m
                            d.tx = Mathf.Lerp(d.tx, a.x, 0.7f); d.maxSpeed = Mathf.Max(d.maxSpeed, SprintSpeed * 0.85f);
                        }
                    }
                    else if (wideBack)
                    {
                        bool ballSide = (d.tx < 34f) == (ballX < 34f);
                        if (ballD < 0.5f && Mathf.Abs(ballX - 34f) > 16f && ballSide)
                        { d.tx = Mathf.Lerp(d.tx, ballX, 0.45f); td = Mathf.Max(MinOutfield, Mathf.Min(td + 0.03f, ballD - 0.02f)); }   // 측면 공을 막으러
                        else if (!ballSide) d.tx = Mathf.Lerp(d.tx, 34f, 0.3f);                                                   // 반대쪽은 가운데로
                    }
                }
                else if (d.def.role == SquadRole.MF) td = Mathf.Clamp(td, line + 0.07f, line + 0.15f);
                else td = Mathf.Min(td, line + 0.38f);
                d.ty = DepthY(def, Mathf.Max(td, MinOutfield));
            }
        }

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

        // 공 앞 1.5m(자기 골대 쪽)에 서서 막습니다. side: 둘이서 압박할 때 옆으로 비켜 섬
        void PressBall(ReplayDot d, int possTeam, float side = 0f)
        {
            float toOwnGoal = d.team == 0 ? 1f : -1f;
            d.tx = ballX + side; d.ty = ballY + toOwnGoal * 1.5f; d.maxSpeed = SprintSpeed;
        }

        // 압박하지 않을 때: 공 앞 7m에서 길만 막고 기다림
        void HoldOff(ReplayDot d)
        {
            float toOwnGoal = d.team == 0 ? 1f : -1f;
            d.tx = ballX; d.ty = ballY + toOwnGoal * 7f; d.maxSpeed = JogSpeed;
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
                        bool on = restart != Restart.None;   // 재개 때 정해진 자리(수비벽 등)는 밀지 않음
                        bool pFixed = Idx(t, a) == carrier || (on && rUse[Idx(t, a)]), qFixed = Idx(t, b) == carrier || (on && rUse[Idx(t, b)]);
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
            // 슈팅이 날아오는 동안 막는 팀 골키퍼는 몸을 날림: 미리 감속하지 않고 순간적으로 빨라짐
            bool dive = flying && (fKind == KindSaved || fKind == KindWide || fKind == KindGoal) && d.slot == 0 && d.team != lastPoss;
            float speed = dive ? top : Mathf.Min(top, Mathf.Sqrt(2f * Decel * 0.6f * dist));   // 멈출 거리를 생각해 미리 감속
            if (dist < (dive ? 0.15f : 0.4f)) speed = 0f;
            float wantX = dist > 0.01f ? dx / dist * speed : 0f;
            float wantY = dist > 0.01f ? dy / dist * speed : 0f;
            float cx = wantX - d.vx, cy = wantY - d.vy;
            float change = Mathf.Sqrt(cx * cx + cy * cy);
            bool slowing = wantX * wantX + wantY * wantY < d.vx * d.vx + d.vy * d.vy;
            float limit = (slowing ? Decel : Accel) * (dive ? 3f : 1f) * dt;
            if (change > limit) { cx *= limit / change; cy *= limit / change; }
            d.vx += cx; d.vy += cy;
            d.x = Mathf.Clamp(d.x + d.vx * dt, 0f, W);
            d.y = Mathf.Clamp(d.y + d.vy * dt, 0f, L);
        }

        void Kickoff(int team)
        {
            boost = 2f;
            StartRestart(Restart.Kickoff, team, W / 2f, L / 2f, false);
        }
    }
}
