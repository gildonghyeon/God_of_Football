using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 경기 영상 엔진: 공 (패스·슈팅·날아가는 공·드리블 판단)
    public partial class MatchReplayEngine
    {
        // ---------- 공 ----------
        void StartFlight(float tx, float ty, float dur, int kind, int recv, float zPeak)
        {
            youPassFlight = pendingYouPass; pendingYouPass = false;
            passFlightTeam = pendingPassTeam; pendingPassTeam = -1; passFlightFrom = pendingPasser; pendingPasser = -1;
            flying = true;
            fFx = ballX; fFy = ballY; fTx = tx; fTy = ty; fT = 0f; fDur = dur; fKind = kind; fRecv = recv; fZ = zPeak; fLong = false;
            if (carrier >= 0) lastPoss = dots[carrier].team;
            carrier = -1;
        }

        // speed: 공이 나는 속도(거리 ÷ 시간, m/s). 주지 않으면 보통 패스 17, 높이 뜬 크로스 14
        void StartPass(int from, int to, float z = -1f, float speed = -1f)
        {
            pendingYouPass = from == YouIdx && YouIdx >= 0;   // 내 선수의 패스 (경기 화면의 개인 패스 기록)
            pendingPassTeam = dots[from].team; pendingPasser = from;   // 팀·선수 패스 기록
            var r = dots[to];
            float tx = Mathf.Clamp(r.x + r.vx * 0.5f, 1f, W - 1f);
            float ty = Mathf.Clamp(r.y + r.vy * 0.5f, 1f, L - 1f);
            float dist = Mathf.Sqrt((tx - ballX) * (tx - ballX) + (ty - ballY) * (ty - ballY));
            // 측면 깊은 곳에서 상자 안으로 보내면 높이 뜬 크로스
            float targetDepth = r.team == 0 ? 1f - ty / L : ty / L;
            bool cross = Mathf.Abs(ballX - 34f) > 18f && targetDepth > 0.76f && Mathf.Abs(tx - 34f) < 14f;
            if (script == null && restart == Restart.None && !executing && dots[from].slot != 0)
            {
                // 빗나간 패스: 터치라인 밖이면 상대 스로인, 골라인 밖이면 상대 골킥
                float outP = ((dist > 28f ? 0.09f : 0.05f) + (Mathf.Abs(tx - 34f) > 22f ? 0.16f : 0f)) * (1.6f - PassN(from));   // 패스가 좋을수록 덜 나감
                if (rng.NextDouble() < outP)
                {
                    int passer = dots[from].team;
                    float dur = Mathf.Max(0.5f, dist / 16f), zz = Mathf.Min(2.5f, dist * 0.05f);
                    if (Mathf.Abs(tx - 34f) > 14f)
                    { StartOut(tx < 34f ? -1.5f : W + 1.5f, Mathf.Clamp(ty + (ty - ballY) * 0.2f, 2f, L - 2f), Restart.ThrowIn, 1 - passer, dur, zz, to); return; }
                    if (targetDepth > 0.8f)
                    { StartOut(tx, passer == 0 ? -1.5f : L + 1.5f, Restart.GoalKick, 1 - passer, dur, zz, to); return; }
                }
            }
            lastPassCross = cross && z < 0f && script != null && rng.NextDouble() < 0.55;   // 득점 장면의 크로스는 절반 남짓 헤딩으로
            MarkOffside(to);
            StartFlight(tx, ty, Mathf.Max(0.45f, dist / (speed > 0f ? speed : cross ? 14f : 17f)), KindPass, to, z >= 0f ? z : cross ? 3.5f : Mathf.Min(2.5f, dist * 0.05f));
        }

        // 밖으로 나가는 공. 끝나면 next 재개를 team이 합니다.
        void StartOut(float tx, float ty, Restart next, int team, float dur, float z, int recv = -1, bool scripted = false)
        {
            float od = Mathf.Sqrt((tx - ballX) * (tx - ballX) + (ty - ballY) * (ty - ballY));
            StartFlight(tx, ty, Mathf.Max(dur, od / 16f), KindOut, recv, z);   // 밖으로 굴러가는 공도 보통 패스 정도의 속도 (짧은 시간에 휙 날아가지 않게)
            outKind = next; outTeam = team; outScripted = scripted;
        }

        // 골키퍼·수비의 긴 공: 앞쪽 동료에게 높이 차 올림
        bool LongBall(int from)
        {
            var c = dots[from]; int best = -1; float bestScore = -9f;
            for (int s = 1; s <= 10; s++)
            {
                var m = dots[Idx(c.team, s)];
                if (m.def.role == SquadRole.DF) continue;
                float md = m.team == 0 ? 1f - m.y / L : m.y / L;
                if (md < 0.4f) continue;
                if (md > SafeDepth(c.team) + 0.005f && rng.NextDouble() < (m.runT > 0f && m.earlyRun ? 0.35 : 0.75)) continue;   // 오프사이드 자리는 대개 피함
                float score = md + (float)rng.NextDouble() * 0.3f;
                if (score > bestScore) { bestScore = score; best = Idx(c.team, s); }
            }
            if (best < 0) return false;
            var r = dots[best];
            float dist = DistTo(c, r.x, r.y);
            MarkOffside(best);
            pendingPassTeam = c.team; pendingPasser = from;   // 긴 공도 패스 기록
            StartFlight(Mathf.Clamp(r.x + Rand(-3f, 3f), 2f, W - 2f), Mathf.Clamp(r.y + Rand(-3f, 3f), 2f, L - 2f), Mathf.Max(1f, dist / 20f), KindPass, best, 7f);
            fLong = true;
            return true;
        }

        // 길게 차는 팀 (역습·내려선 수비)
        bool LongStyle(int team) { return sp[team].distPenalty < 0.01f; }

        // outcome: KindGoal / KindSaved / KindWide
        void StartShot(ReplayDot shooter, int outcome, float z = 1.6f)
        {
            int si = Idx(shooter.team, shooter.slot);
            if (outcome == KindWide) PShotsOff[si]++; else PShotsOn[si]++;
            if (outcome == KindSaved) PSaves[Idx(1 - shooter.team, 0)]++;
            if (TeamShot != null) TeamShot(shooter.team, outcome != KindWide);
            if (outcome == KindSaved && KeeperSaved != null) KeeperSaved(1 - shooter.team);
            float goalY = shooter.team == 0 ? 0f : L;
            shotSide = rng.NextDouble() < 0.5 ? 1f : -1f;
            if (outcome == KindGoal) shotGx = 34f + shotSide * 3f;
            else if (outcome == KindSaved) shotGx = 34f + shotSide * Rand(1.5f, 3.5f);
            else shotGx = 34f + shotSide * Rand(4.8f, 9f);
            float ty = outcome == KindSaved ? (shooter.team == 0 ? 1.6f : L - 1.6f) : goalY;   // 막히는 슛: 골키퍼가 몸을 날린 자리에서 막힘
            // 슛 속도: 22~34 m/s (슈팅이 좋을수록 빠름). 패스(17)·크로스(14)보다 늘 빠름
            float sd = Mathf.Sqrt((shotGx - ballX) * (shotGx - ballX) + (ty - ballY) * (ty - ballY));
            StartFlight(shotGx, ty, Mathf.Clamp(sd / (22f + 12f * ShootN(Idx(shooter.team, shooter.slot))), 0.3f, 1.3f), outcome, -1, z);
        }

        // 패스 받을 동료 고르기. 스타일마다 다름:
        //  점유: 짧고 안전한 패스 / 내려선 수비·역습: 길게 앞으로 / 측면 공격: 측면으로 벌린 뒤 상자 안으로 크로스
        int ChoosePass(ReplayDot c)
        {
            var st = sp[c.team];
            bool countering = counterT[c.team] > 0f;
            float cDepth = c.team == 0 ? 1f - c.y / L : c.y / L;
            bool carrierWide = Mathf.Abs(c.x - 34f) > 18f;
            float safe = SafeDepth(c.team);
            int best = -1; float bestScore = -9f;
            for (int s = 0; s < 11; s++)
            {
                var m = dots[Idx(c.team, s)];
                if (m == c) continue;
                // 오프사이드 자리는 대개 피함. 너무 일찍 뛴 동료는 잘못 보고 패스할 때가 많음 → 오프사이드
                if (Dp(c.team, m.y) > safe + 0.005f && rng.NextDouble() < (m.runT > 0f && m.earlyRun ? 0.35 : 0.75)) continue;
                if (c.slot == 0 && m.def.role != SquadRole.DF) continue;
                if (c.slot != 0 && s == 0) continue;
                float forward = c.team == 0 ? c.y - m.y : m.y - c.y;
                float dist = Mathf.Sqrt((m.x - c.x) * (m.x - c.x) + (m.y - c.y) * (m.y - c.y));
                float maxDist = countering ? 52f : st.maxPass;
                if (dist < 5f || dist > maxDist) continue;
                float score = forward * (countering ? 0.12f : st.fwdWeight) - dist * st.distPenalty + (float)rng.NextDouble() * 0.5f;
                float mDepth = c.team == 0 ? 1f - m.y / L : m.y / L;
                if (cDepth > 0.66f)
                {
                    // 골문 앞: 뒤로 빼는 패스는 가끔만(다시 만들기), 옆 패스는 더 좋은 자리일 때만
                    // 단, 뒤따라 올라온 미드필더·풀백에게 짧게 내주는 패스(컷백·레이오프)는 슈팅 기회가 되므로 허용
                    bool support = m.def.role == SquadRole.MF || m.def.kind == SlotKind.FB || m.def.kind == SlotKind.WB;
                    bool layoff = support && finalPasses < 2 && mDepth > 0.62f && mDepth > cDepth - 0.14f && dist < 22f;
                    if (!layoff && mDepth < cDepth - 0.08f && (finalPasses > 0 || rng.NextDouble() > 0.15)) continue;
                    bool better = layoff || ((m.def.kind == SlotKind.FB || m.def.kind == SlotKind.WB) && m.runT > 0f) || mDepth > cDepth + 0.03f || Mathf.Abs(m.x - 34f) < Mathf.Abs(c.x - 34f) - 6f;
                    if (layoff) score += 0.25f;
                    bool overlapping = (m.def.kind == SlotKind.FB || m.def.kind == SlotKind.WB) && m.runT > 0f && mDepth > 0.62f;
                    if (overlapping) score += 0.5f;    // 측면으로 오버래핑해 올라온 풀백에게
                    if (m.def.role == SquadRole.MF && mDepth > 0.68f && Mathf.Abs(m.x - 34f) < 16f) score += 0.3f;   // 상자 앞까지 올라온 미드필더에게
                    if (mDepth >= cDepth - 0.08f && !better && !(st.wing && carrierWide)) continue;
                    if (mDepth > st.shotDepth && Mathf.Abs(m.x - 34f) < 14f) score += 0.4f;     // 슈팅 자리
                }
                if (st.wing)
                {
                    if (!carrierWide && cDepth > 0.35f && cDepth < 0.72f && Mathf.Abs(m.x - 34f) > 18f) score += 0.35f;          // 측면으로 벌림
                    if (carrierWide && cDepth > 0.68f && mDepth > 0.76f && Mathf.Abs(m.x - 34f) < 14f) score += 0.6f;            // 크로스
                }
                if (countering && (m.def.role == SquadRole.FW || m.def.kind == SlotKind.W || m.def.kind == SlotKind.AM)) score += 0.4f;
                if (score > bestScore) { best = Idx(c.team, s); bestScore = score; }
            }
            return best;
        }

        // (x, y)에 가장 가까운 필드 선수 (두 팀 모두, 골키퍼 제외)
        int NearestOutfield(float x, float y)
        {
            int best = 1; float bd = float.MaxValue;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i].slot == 0) continue;
                float d = DistTo(dots[i], x, y);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void UpdateBall(float dt)
        {
            if (gkHoldT > 0f) gkHoldT -= dt;
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
                // 발 앞의 공 자리로 굴러감: 공 주인이 바뀌어도(태클·가로채기) 공이 순간이동하지 않습니다.
                float bx = d.x, by = d.y + dir * 0.9f;
                float gx = bx - ballX, gy = by - ballY, gd = Mathf.Sqrt(gx * gx + gy * gy), step = BallRoll * dt;
                if (gd <= step || gd > 70f) { ballX = bx; ballY = by; }
                else { ballX += gx / gd * step; ballY += gy / gd * step; }
                ballZ = 0f;
                if (script == null)
                {
                    decideT -= dt;
                    if (decideT <= 0f) NaturalDecision();
                    for (int s = 1; s <= 10 && carrier >= 0; s++)   // 태클
                    {
                        var o = dots[Idx(1 - d.team, s)];
                        float dd = Mathf.Sqrt((o.x - d.x) * (o.x - d.x) + (o.y - d.y) * (o.y - d.y));
                        if (stagedRunner >= 0) break;   // 오프사이드 장면: 침투 패스를 기다리는 잠깐 동안은 태클하지 않음
                        if (dd < 1.5f && rng.NextDouble() < 0.5 * dt * (1.4f - DribbleN(carrier)) * (0.5f + SkillDefend[Idx(1 - d.team, s)]))   // 드리블이 좋으면 덜 뺏기고, 수비가 좋으면 더 뺏음
                        {
                            if (rng.NextDouble() < FoulChance) { Tackled(Idx(1 - d.team, s), false); Foul(d); break; }   // 거친 태클은 파울 → 프리킥
                            if (Mathf.Abs(d.x - 34f) > 22f && rng.NextDouble() < 0.5)   // 터치라인 근처: 태클한 공이 밖으로 → 스로인
                            {
                                Tackled(Idx(1 - d.team, s), false);   // 공은 밖으로 → 상대 스로인 (성공 아님)
                                lastPoss = o.team; carrier = -1;
                                StartOut(d.x < 34f ? -1.5f : W + 1.5f, d.y + Rand(-3f, 3f), Restart.ThrowIn, d.team, 0.4f, 0.5f);
                                break;
                            }
                            Tackled(Idx(1 - d.team, s), true);
                            carrier = Idx(1 - d.team, s); decideT = Rand(0.7f, 1.4f); break;
                        }
                    }
                }
            }
        }

        void NaturalDecision()
        {
            var c = dots[carrier];
            var st = sp[c.team];
            // 오프사이드 장면: 뛰어든 동료가 라인을 넘으면 침투 패스 (그 전에는 잠깐 공을 지킴)
            if (stagedRunner >= 0)
            {
                var r = dots[stagedRunner];
                if (r.team != c.team || r.runT <= 0f || stagedRunner == carrier) stagedRunner = -1;
                else if (Dp(r.team, r.y) > SafeDepth(r.team) + 0.012f)
                {
                    int target = stagedRunner; stagedRunner = -1;
                    StartPass(carrier, target, -1f, 19f);
                    return;
                }
                else { decideT = 0.15f; return; }
            }
            float depth = c.team == 0 ? 1f - c.y / L : c.y / L;
            bool central = Mathf.Abs(c.x - 34f) < 18f;
            if (depth < 0.6f) finalPasses = 0;
            if (carrier != YouIdx && c.slot != 0 && depth > st.shotDepth - 0.04f && central)
            {
                // 골문에 가까울수록, 앞에서 패스를 많이 돌렸을수록 슈팅
                float shootP = Mathf.Min(0.92f, 0.5f + (depth - st.shotDepth) * 3f + 0.2f * finalPasses);
                if (rng.NextDouble() < shootP)
                {
                    StartShot(c, OnTarget(c) ? KindSaved : KindWide);   // 슈팅 능력이 높을수록 골문으로
                    return;
                }
            }
            if (c.slot == 0 && rng.NextDouble() < (LongStyle(c.team) ? 0.6 : 0.2) && LongBall(carrier)) return;
            // 우리 진영 깊은 곳에서 압박받는 수비수: 걷어냄 (터치라인 밖이나 앞으로 길게)
            if (carrier != YouIdx && c.def.role == SquadRole.DF && depth < 0.25f && presser >= 0 && DistTo(dots[presser], c.x, c.y) < 4f && rng.NextDouble() < 0.5)
            {
                if (rng.NextDouble() < 0.45)
                { StartOut(c.x < 34f ? -1.5f : W + 1.5f, Mathf.Clamp(c.y + (c.team == 0 ? -14f : 14f), 2f, L - 2f), Restart.ThrowIn, 1 - c.team, 0.8f, 4f); return; }
                if (LongBall(carrier)) return;
            }
            // 침투 패스: 앞으로 뛰어드는 동료가 있으면 그 앞 공간으로 찔러 줌 (역습 팀이 가장 자주).
            // 타이밍 좋게 뛴 동료면 기회, 너무 일찍 뛴 동료면 오프사이드
            if (c.slot != 0 && depth > 0.35f && depth < 0.8f)
            {
                int runner = -1; float bestFw = 0.06f;
                for (int s = 1; s <= 10; s++)
                {
                    var m = dots[Idx(c.team, s)];
                    if (m == c || m.runT <= 0f || m.runAge < 0.6f) continue;   // 뛰기 시작하고 조금 뒤에 찔러 줌
                    float fw = Dp(c.team, m.y) - depth;
                    if (fw > bestFw && DistTo(m, c.x, c.y) < 45f) { bestFw = fw; runner = Idx(c.team, s); }
                }
                float throughP = st.counter ? 0.45f : st.wing ? 0.25f : 0.3f;
                if (runner >= 0 && rng.NextDouble() < throughP)
                {
                    StartPass(carrier, runner, -1f, 19f);
                    return;
                }
            }
            float passP = st.passProb;
            if (depth > 0.66f) passP *= finalPasses >= 2 ? 0.25f : 0.65f;   // 골문 앞에서는 직접 치고 들어감
            if (c.slot == 0 || rng.NextDouble() < passP)
            {
                int r = ChoosePass(c);
                if (r >= 0)
                {
                    badPass = carrier == YouIdx ? rng.NextDouble() >= stats.passAccuracy   // 패스 능력만큼만 정확함
                                                : rng.NextDouble() < 0.22f * (1f - PassN(carrier));   // 다른 선수도 패스가 나쁠수록 자주 빗나감
                    if (depth > 0.66f) finalPasses++;
                    StartPass(carrier, r);
                    return;
                }
            }
            decideT = counterT[c.team] > 0f ? Rand(0.25f, 0.5f) : Rand(st.decideMin, st.decideMax);
        }

        // 공이 도착했을 때: 내 선수가 보낸 패스였으면 동료가 받았는지 알려 줌
        void EndFlight()
        {
            bool mine = youPassFlight && (fKind == KindPass || fKind == KindOut);
            int passTeam = (fKind == KindPass || fKind == KindOut) ? passFlightTeam : -1;
            int passer = passTeam >= 0 ? passFlightFrom : -1;
            youPassFlight = false; passFlightTeam = -1; passFlightFrom = -1;
            EndFlightCore();
            if (mine && YouPassed != null) YouPassed(!flying && carrier >= 0 && carrier != YouIdx && dots[carrier].team == 0);
            bool passOk = !flying && carrier >= 0 && dots[carrier].team == passTeam;
            if (passer >= 0) { PPassAtt[passer]++; if (passOk) PPassComp[passer]++; }
            if (passTeam >= 0 && TeamPassed != null) TeamPassed(passTeam, passOk);
        }

        void EndFlightCore()
        {
            flying = false; focusFlight = false;
            if (fKind == KindOut)
            {
                float fd = Mathf.Max(0.2f, fDur);
                rollVX = (fTx - fFx) / fd * 0.35f; rollVY = (fTy - fFy) / fd * 0.35f;
                float rv = Mathf.Sqrt(rollVX * rollVX + rollVY * rollVY);
                if (rv > 7f) { rollVX *= 7f / rv; rollVY *= 7f / rv; }   // 너무 빨리 미끄러지지 않게
                bool sc = outScripted; outScripted = false;
                StartRestart(outKind, outTeam, fTx, fTy, sc);
                return;
            }
            if (fKind == KindLoose)   // 굴러간 공을 잡음
            {
                float far = DistTo(dots[fRecv], ballX, ballY);
                if (far > 3f)   // 아직 공에 닿지 못했으면 공이 그 선수 쪽으로 계속 굴러감 (멀리 있는 선수에게 순간이동하지 않게)
                {
                    var rr = dots[fRecv];
                    StartFlight(ballX + (rr.x - ballX) * 0.6f, ballY + (rr.y - ballY) * 0.6f, Mathf.Max(0.3f, far * 0.6f / 10f), KindLoose, fRecv, 0.2f);
                    return;
                }
                carrier = fRecv; lastPoss = dots[carrier].team;
                var ls = sp[lastPoss];
                decideT = Rand(ls.decideMin, ls.decideMax);
                return;
            }
            if (fKind == KindPass && offsidePass)
            {
                offsidePass = false;
                if (script == null)   // 오프사이드: 받는 선수가 있던 자리에서 상대 프리킥
                {
                    OffsideCount++; offsideCall = true;
                    OffsideLineY = DepthY(offTeam, offLine); OffsideX = offX; OffsideY = offY; OffsideShown++;   // 화면: 패스 순간의 오프사이드 라인
                    StartRestart(Restart.FreeKick, 1 - dots[fRecv].team, offX, offY, false);
                    return;
                }
            }
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
                if (script == null && !badPass && fZ >= 3f && Cross(fRecv, near, nearDist)) return;
                float reach = fLong ? 5f : 3.5f, steal = fLong ? 0.5f : 0.25f;   // 긴 공은 경합
                if (script == null && fLong && nearDist < reach && rng.NextDouble() < 0.2)   // 공중볼 경합 중 파울
                { carrier = -1; ballX = fTx; ballY = fTy; Foul(dots[fRecv]); return; }
                bool lost = script == null && (badPass || (nearDist < reach && rng.NextDouble() < steal));   // 가로채기
                if (script == null && nearDist < reach) Tackled(near, lost);   // 패스를 끊으러 붙음: 가로채면 성공
                if (lost && nearDist > 2.5f)
                {
                    // 가로챌 상대가 떨어져 있으면: 빗나간 공이 그 선수 쪽으로 굴러가고, 그 선수가 달려와 잡음
                    badPass = false; lastPoss = dots[near].team;
                    var o = dots[near];
                    StartFlight(o.x + (fTx - o.x) * 0.3f, o.y + (fTy - o.y) * 0.3f, Mathf.Max(0.35f, nearDist / 12f), KindLoose, near, 0.3f);
                    return;
                }
                carrier = lost ? near : fRecv;
                badPass = false;
                var rs = sp[dots[carrier].team];
                decideT = counterT[dots[carrier].team] > 0f ? Rand(0.25f, 0.5f) : Rand(rs.decideMin + 0.1f, rs.decideMax + 0.2f);
                if (script != null && phase == Phase.Pass) { phase = Phase.Receive; phaseT = 0f; }
                return;
            }
            int shooterTeam = lastPoss;
            if (fKind == KindGoal)
            {
                ballX = fTx; ballY = shooterTeam == 0 ? -1.2f : L + 1.2f; ballZ = 0f;   // 골망 안
                phase = Phase.After; phaseT = 0f;
                if (script != null && script.scorerSlot >= 0)
                {
                    PGoals[Idx(scriptTeam, script.scorerSlot)]++;
                    if (script.assisterSlot >= 0) PAssists[Idx(scriptTeam, script.assisterSlot)]++;
                }
                StartCelebration();
                if (GoalScored != null && script != null) GoalScored(script);
            }
            else
            {
                if (script != null && phase == Phase.Shot)   // 장면의 슈팅일 때만 장면을 끝냄 (장면이 공을 기다리는 중에 끝난 다른 슈팅은 아님)
                {
                    if (ShotResolved != null) ShotResolved(script);
                    script = null; phase = Phase.None;
                }
                int def = 1 - shooterTeam;
                if (fKind == KindWide) { StartRestart(Restart.GoalKick, def, 34f, 0f, false); return; }   // 골문 밖 → 골킥
                double saveRoll = rng.NextDouble();
                if (saveRoll < 0.3)   // 골키퍼가 쳐내 골라인 밖으로 → 코너킥
                {
                    ballX = fTx; ballY = fTy; carrier = -1; lastPoss = def;
                    StartOut(Mathf.Clamp(fTx + shotSide * Rand(6f, 14f), 2f, W - 2f), shooterTeam == 0 ? -1.5f : L + 1.5f, Restart.Corner, shooterTeam, 0.45f, 2f);
                    return;
                }
                int gk = Idx(def, 0);
                if (saveRoll < 0.62)
                {
                    // 앞으로 쳐냄: 공이 옆 앞쪽으로 튀고, 가장 가까운 선수(어느 팀이든)가 달려가 잡음. 공격수가 잡으면 다시 슈팅할 수도 있음
                    float side = fTx < 34f ? -1f : 1f;
                    float rx = Mathf.Clamp(fTx + side * Rand(4f, 9f), 8f, W - 8f);
                    float ry = def == 1 ? Rand(5f, 11f) : L - Rand(5f, 11f);
                    int recv = NearestOutfield(rx, ry);
                    lastPoss = dots[recv].team;
                    StartFlight(rx, ry, Rand(0.5f, 0.7f), KindLoose, recv, 1.2f);
                    Call("선방 · 쳐냄");
                    return;
                }
                // 잡음: 공은 골키퍼 품으로 들어가고(순간이동하지 않음), 골키퍼는 공을 안고 몇 걸음 나온 뒤 동료에게
                carrier = gk; lastPoss = def; decideT = 1.8f; gkHoldT = 1.8f;
                Call("선방!", def);
            }
        }

        // 상자 안으로 올라온 크로스(코너킥 포함): 수비가 걷어내거나 공격수가 헤더로 슈팅
        bool Cross(int recv, int near, float nearDist)
        {
            var a = dots[recv];
            if (!InBox(a.team, fTx, fTy) || recv == YouIdx) return false;
            ballX = fTx; ballY = fTy;
            double r = rng.NextDouble();
            if (nearDist < 5f && r < 0.45)
            {
                if (rng.NextDouble() < 0.4)   // 골라인 밖으로 걷어냄 → 다시 코너킥
                {
                    carrier = -1; lastPoss = 1 - a.team;
                    StartOut(Mathf.Clamp(fTx + Rand(-12f, 12f), 16f, 52f), a.team == 0 ? -1.5f : L + 1.5f, Restart.Corner, a.team, 0.5f, 2.5f);
                    return true;
                }
                carrier = near; decideT = 0.15f;   // 걷어낸 공을 잡음
                return true;
            }
            if (r < 0.75)   // 헤더
            {
                carrier = recv;
                StartShot(a, OnTarget(a, true) ? KindSaved : KindWide, 0.7f);
                return true;
            }
            return false;
        }
    }
}
