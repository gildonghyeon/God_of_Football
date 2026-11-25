using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 경기 영상 엔진: 파울과 재개 (킥오프·스로인·골킥·코너킥·프리킥·페널티킥·오프사이드)
    public partial class MatchReplayEngine
    {
        // ---------- 파울과 재개 ----------
        const double FoulChance = 0.45;

        void Foul(ReplayDot victim)
        {
            int team = victim.team;
            float x = ballX, y = ballY;
            if (InBox(team, x, y)) y = DepthY(team, 0.83f);   // 경기 중 파울은 상자 바로 밖 프리킥 (페널티킥은 득점 장면에서만)
            StartRestart(Restart.FreeKick, team, x, y, false);
        }

        // team이 공격하는 골문 앞 페널티 박스 안인가
        static bool InBox(int team, float x, float y)
        {
            float depth = team == 0 ? 1f - y / L : y / L;
            return depth > 1f - 16.5f / L && Mathf.Abs(x - 34f) < 20.15f;
        }

        // team이 공격하는 골라인의 y, 그리고 그 골라인에서 경기장 안쪽으로 가는 방향
        static float GoalY(int team) { return team == 0 ? 0f : L; }
        static float Inward(int team) { return team == 0 ? 1f : -1f; }

        void Call(string text, int team = -1, bool foul = false) { CallText = text; CallTeam = team; CallFoul = foul; CallId++; }

        void StartRestart(Restart kind, int team, float x, float y, bool scripted)
        {
            restart = kind; rTeam = team; rScripted = scripted; rDanger = false;
            RestartCount[(int)kind]++;
            flying = false; carrier = -1; lastPoss = team; finalPasses = 0; badPass = false;
            counterT[0] = 0f; counterT[1] = 0f;
            for (int i = 0; i < 22; i++) { rUse[i] = false; dots[i].runT = 0f; }
            float gY = GoalY(team), s = Inward(team);
            switch (kind)
            {
                case Restart.Kickoff: rX = W / 2f; rY = L / 2f; break;
                case Restart.ThrowIn: rX = x < 34f ? 0f : W; rY = Mathf.Clamp(y, 2f, L - 2f); break;
                case Restart.GoalKick: rX = 34f + (rng.NextDouble() < 0.5 ? -1f : 1f) * Rand(3f, 9f); rY = GoalY(1 - team) + Inward(1 - team) * 5.5f; break;
                case Restart.Corner: rX = x < 34f ? 0.5f : W - 0.5f; rY = gY + s * 0.5f; break;
                case Restart.Penalty: rX = 34f; rY = gY + s * 11f; break;
                default: rX = Mathf.Clamp(x, 1f, W - 1f); rY = Mathf.Clamp(y, 1f, L - 1f); break;   // 프리킥: 반칙한 자리
            }
            // 공은 바로 옮기지 않습니다: 잠깐 멈춘 뒤(멈춤 단계) 재개 자리로 가져와 놓습니다.
            rStage = 0; ballZ = 0f;
            rDeadT = kind == Restart.Kickoff ? 0.3f : (kind == Restart.FreeKick || kind == Restart.Penalty) ? 0.6f : 0.9f;
            float depth = team == 0 ? 1f - rY / L : rY / L;
            if (kind == Restart.FreeKick) rDanger = scripted || (depth > 0.7f && Mathf.Abs(rX - 34f) < 22f);
            rTaker = PickTaker(kind);
            switch (kind)
            {
                case Restart.Kickoff: rT = 1.4f; break;
                case Restart.ThrowIn: rT = 2.6f; Call("스로인", team); break;
                case Restart.GoalKick: rT = 3f; Call("골킥", team); break;
                case Restart.Corner: rT = 3.8f; Call("코너킥", team); break;
                case Restart.Penalty: rT = 4.6f; Call("파울! 페널티킥", team, true); break;
                default:
                    rT = rDanger ? 3.6f : 2.6f;
                    if (offsideCall) { offsideCall = false; Call("오프사이드", team, true); }
                    else Call(rDanger ? "파울! 골문 앞 프리킥" : "파울 · 프리킥", team, true);
                    break;
            }
            PlanRestartSpots(kind);
        }

        // 세트피스 키커: 그 능력치가 가장 좋은 필드 선수 (같으면 가까운 선수). skip: 뺄 선수 (예: 득점자)
        int BestKicker(float[] skill, int skip)
        {
            int best = -1; float bs = float.MinValue;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(rTeam, s);
                if (i == YouIdx || i == skip) continue;   // 내 선수는 기록에 없는 슈팅·도움이 생기지 않게 빼고
                float score = skill[i] * 100f - DistTo(dots[i], rX, rY) * 0.05f;
                if (score > bs) { bs = score; best = i; }
            }
            return best;
        }

        int PickTaker(Restart kind)
        {
            if (rScripted && script != null)   // 득점 장면: 경기 계획에서 이미 능력치로 키커를 정함 (득점 기록의 이름과 같음)
                return Idx(rTeam, kind == Restart.Corner && script.assisterSlot >= 0 ? script.assisterSlot : script.scorerSlot);
            if (kind == Restart.Kickoff) return Idx(rTeam, kickSlot[rTeam]);
            if (kind == Restart.GoalKick) return Idx(rTeam, 0);
            if (kind == Restart.Corner) { int k = BestKicker(SkillPass, -1); if (k >= 0) return k; }   // 코너킥: 패스가 가장 좋은 선수
            if (kind == Restart.Penalty || (kind == Restart.FreeKick && (rDanger || Dp(rTeam, rY) > 0.5f)))   // 페널티킥·상대 진영 프리킥: 슈팅이 가장 좋은 선수
            { int k = BestKicker(SkillShoot, -1); if (k >= 0) return k; }
            bool setKick = kind == Restart.Corner || (kind == Restart.FreeKick && rDanger);
            int best = -1; float bestScore = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(rTeam, s); var d = dots[i];
                if (setKick && i == YouIdx) continue;   // 기록에 없는 슈팅·도움이 생기지 않게
                float score = DistTo(d, rX, rY);
                if (setKick)
                {
                    var k = d.def.kind;
                    if (d.def.role == SquadRole.DF) score += 40f;                                      // 키커는 앞쪽·측면 선수
                    else if (k == SlotKind.W || k == SlotKind.WM || k == SlotKind.AM) score -= 15f;
                }
                if (kind == Restart.ThrowIn && (d.def.kind == SlotKind.FB || d.def.kind == SlotKind.WB)) score -= 8f;   // 스로인은 풀백이 주로
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        void Spot(int i, float x, float y) { rUse[i] = true; rSx[i] = Mathf.Clamp(x, 0f, W); rSy[i] = Mathf.Clamp(y, 0f, L); }

        // team의 필드 선수 중 (x, y)에 가까운 순서로 count명 (이미 자리를 받은 선수 제외)
        List<int> Nearest(int team, float x, float y, int count)
        {
            var list = new List<int>();
            for (int s = 1; s <= 10; s++) { int i = Idx(team, s); if (!rUse[i]) list.Add(i); }
            list.Sort((a, b) => DistTo(dots[a], x, y).CompareTo(DistTo(dots[b], x, y)));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        // 재개 때 선수들이 설 자리를 미리 정합니다. (준비하는 동안 같은 자리를 유지)
        void PlanRestartSpots(Restart kind)
        {
            int atk = rTeam, def = 1 - rTeam;
            float gY = GoalY(atk), s = Inward(atk);
            float back = kind == Restart.Penalty ? 2.2f : kind == Restart.Kickoff ? 0.6f : kind == Restart.ThrowIn || kind == Restart.Corner ? 0f : 0.9f;
            Spot(rTaker, rX, rY + s * back);   // 차는 선수는 공 뒤 (자기 골문 쪽)
            switch (kind)
            {
                case Restart.ThrowIn:
                {
                    float inward = rX < 34f ? 1f : -1f; int n = 0;
                    foreach (int i in Nearest(atk, rX, rY, 2)) { Spot(i, rX + inward * (6f + 5f * n), rY + (n == 0 ? -7f : 6f) * s); n++; }   // 가까운 동료가 받으러 옴
                    break;
                }
                case Restart.GoalKick:
                    if (!LongStyle(atk))   // 짧게 시작하는 팀: 센터백이 상자 양옆으로 벌림
                    {
                        int n = 0;
                        for (int k = 1; k <= 10; k++)
                        {
                            int i = Idx(atk, k);
                            if (dots[i].def.kind != SlotKind.CB || n >= 2) continue;
                            Spot(i, 34f + (n == 0 ? -17f : 17f), GoalY(def) + Inward(def) * 10f); n++;
                        }
                    }
                    break;
                case Restart.Kickoff: KickoffSetup(); break;
                case Restart.Corner: BoxSetup(true); break;
                case Restart.FreeKick: if (rDanger) BoxSetup(false); break;
                case Restart.Penalty: PenaltySetup(); break;
            }
        }

        // 킥오프: 두 팀 모두 중앙선 기준 자기 진영에 포메이션 모양으로 섭니다.
        // 차는 팀은 공격수 둘이 공 옆에 서고, 상대 팀은 센터 서클(9.15m) 밖에 섭니다.
        void KickoffSetup()
        {
            int partner = -1, best = 99;
            for (int k = 1; k <= 10; k++)
            {
                int i = Idx(rTeam, k); if (i == rTaker) continue;
                var kind = dots[i].def.kind;
                int pr = kind == SlotKind.ST ? 0 : kind == SlotKind.AM ? 1 : kind == SlotKind.W ? 2 : kind == SlotKind.CM ? 3 : 9;
                if (pr < best) { best = pr; partner = i; }
            }
            if (partner >= 0) Spot(partner, W / 2f + 3f, L / 2f + (rTeam == 0 ? 1.2f : -1.2f));
            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 11; k++)
                {
                    int i = Idx(t, k); if (rUse[i]) continue;
                    var d = dots[i];
                    // 자기 골문에서의 깊이: 수비 0.07, 미드필더 0.25, 공격수 0.46 (중앙선은 0.5)
                    float depth = k == 0 ? 0.03f : Mathf.Clamp(0.06f + (d.def.depth - 0.18f) / 0.5f * 0.40f, 0.05f, 0.46f);
                    float x = t == 0 ? d.def.x : W - d.def.x;
                    Spot(i, 34f + (x - 34f) * 0.9f, DepthY(t, depth));
                }
        }

        static int BoxPriority(SlotKind k)
        {
            switch (k)
            {
                case SlotKind.ST: return 0; case SlotKind.CB: return 1; case SlotKind.AM: return 2; case SlotKind.W: return 3;
                case SlotKind.CM: return 4; case SlotKind.WM: return 5; default: return 9;
            }
        }

        // 코너킥·골문 앞 프리킥: 공격수와 키 큰 수비수가 상자 안으로, 수비는 한 명씩 붙고 (프리킥은 수비벽)
        void BoxSetup(bool corner)
        {
            int atk = rTeam, def = 1 - rTeam;
            float gY = GoalY(atk), s = Inward(atk);
            var cand = new List<int>();
            for (int k = 1; k <= 10; k++) { int i = Idx(atk, k); if (!rUse[i]) cand.Add(i); }
            cand.Sort((a, b) => BoxPriority(dots[a].def.kind).CompareTo(BoxPriority(dots[b].def.kind)));
            float[] bx = { 0f, -6f, 6f, -2.5f, 3.5f, -9f };
            float[] by = { 7f, 6f, 8.5f, 11f, 12f, 10f };
            float extra = corner ? 0f : -1f, spread = corner ? 1f : 1.8f;   // 프리킥: 수비벽을 피해 양옆으로 벌려 섬
            int maxN = corner ? 6 : 5;
            var att = new List<int>();
            int scorer = rScripted && script != null ? Idx(atk, script.scorerSlot) : -1;
            if (scorer >= 0 && !rUse[scorer]) { Spot(scorer, 34f + scorerOffset * 0.6f, gY + s * (corner ? 8f : 12f)); att.Add(scorer); }
            foreach (int i in cand)
            {
                if (att.Count >= maxN) break;
                if (rUse[i] || BoxPriority(dots[i].def.kind) > 5) continue;
                int n = att.Count;
                Spot(i, 34f + bx[n] * spread, gY + s * (by[n] + extra));
                att.Add(i);
            }
            int rest = 0;   // 남은 선수는 역습에 대비해 뒤에
            for (int k = 1; k <= 10; k++)
            {
                int i = Idx(atk, k); if (rUse[i]) continue;
                Spot(i, 34f + ((rest % 3) - 1) * 16f, gY + s * (26f + 12f * (rest / 3))); rest++;
            }
            // 수비
            Spot(Idx(def, 0), 34f + (corner ? (rX < 34f ? -1.2f : 1.2f) : 0f), gY + s * 0.6f);
            if (!corner)   // 수비벽: 공에서 골문 쪽으로 9.15m
            {
                float dx = 34f - rX, dy = gY - rY, dl = Mathf.Max(0.1f, Mathf.Sqrt(dx * dx + dy * dy));
                dx /= dl; dy /= dl;
                float cx = rX + dx * 9.15f, cy = rY + dy * 9.15f;
                int wn = Mathf.Abs(rX - 34f) < 12f ? 4 : 3, k = 0;
                foreach (int i in Nearest(def, cx, cy, wn)) { float o = (k - (wn - 1) / 2f) * 0.9f; Spot(i, cx - dy * o, cy + dx * o); k++; }
            }
            foreach (int a in att)   // 공격수마다 한 명씩 골문 쪽에 붙어 막음
            {
                var near = Nearest(def, rSx[a], rSy[a], 1);
                if (near.Count == 0) break;
                Spot(near[0], rSx[a] + 0.8f, rSy[a] - s * 1.2f);
            }
            if (corner)   // 골대 옆
            {
                var posts = Nearest(def, 34f, gY, 2);
                if (posts.Count > 0) Spot(posts[0], rX < 34f ? 30.5f : 37.5f, gY + s * 1.2f);
                if (posts.Count > 1) Spot(posts[1], 34f, gY + s * 5f);
            }
            int fw = 0, zn = 0;
            for (int k = 1; k <= 10; k++)
            {
                int i = Idx(def, k); if (rUse[i]) continue;
                if (dots[i].def.role == SquadRole.FW) { Spot(i, 34f + (fw++ % 2 == 0 ? -8f : 8f), gY + s * 40f); continue; }   // 앞에 남아 역습 대기
                Spot(i, 34f + (zn - 1.5f) * 8f, gY + s * 15f); zn++;   // 상자 가장자리
            }
        }

        // 페널티킥: 키커와 골키퍼만 상자 안, 나머지는 상자 밖 반원 뒤
        void PenaltySetup()
        {
            int atk = rTeam, def = 1 - rTeam;
            float gY = GoalY(atk), s = Inward(atk);
            Spot(Idx(def, 0), 34f, gY + s * 0.3f);
            int n = 0;
            for (int k = 1; k <= 10; k++)
                for (int t = 0; t < 2; t++)
                {
                    int i = Idx(t == 0 ? atk : def, k);
                    if (rUse[i]) continue;
                    Spot(i, 34f + ((n % 10) - 4.5f) * 4f, gY + s * (21.5f + (n / 10) * 3.5f)); n++;
                }
        }

        void RestartTargets()
        {
            if (rStage == 0)
            {
                foreach (var d in dots) { d.tx = d.x + d.vx * 0.3f; d.ty = d.y + d.vy * 0.3f; d.maxSpeed = 2.5f; }
                return;
            }
            for (int i = 0; i < 22; i++)
            {
                var d = dots[i];
                if (rUse[i]) { d.tx = rSx[i]; d.ty = rSy[i]; d.maxSpeed = d.slot == 0 ? 6f : SprintSpeed; continue; }
                if (d.team == rTeam) continue;
                // 상대는 공에서 떨어져 섬 (스로인 3m, 그 밖 9.15m)
                PushOff(d);
            }
            for (int i = 0; i < 22; i++)   // 정해진 자리를 받은 상대 선수도 9.15m 밖 (수비벽은 이미 그 거리)
                if (rUse[i] && dots[i].team != rTeam && dots[i].slot != 0 && restart != Restart.Penalty) PushOff(dots[i]);
        }

        void PushOff(ReplayDot d)
        {
            {
                float min = restart == Restart.ThrowIn ? 3f : 9.15f;
                float dx = d.tx - rX, dy = d.ty - rY, dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < min) { if (dist < 0.01f) { dx = 1f; dy = 0f; dist = 1f; } d.tx = rX + dx / dist * min; d.ty = rY + dy / dist * min; }
                if (restart == Restart.GoalKick && InBox(d.team, d.tx, d.ty)) d.ty = GoalY(d.team) + Inward(d.team) * 18f;   // 골킥: 상대는 상자 밖
            }
        }

        void UpdateRestart(float dt)
        {
            if (rStage == 0)   // 멈춤: 나간 공은 조금 굴러가다 멈추고, 선수들도 속도를 늦춤
            {
                rDeadT -= dt;
                ballX = Mathf.Clamp(ballX + rollVX * dt, -4f, W + 4f);
                ballY = Mathf.Clamp(ballY + rollVY * dt, -4f, L + 4f);
                float k = Mathf.Exp(-3.5f * dt); rollVX *= k; rollVY *= k;
                if (rDeadT <= 0f) { rStage = 1; rollVX = 0f; rollVY = 0f; }
                return;
            }
            if (rStage == 1)   // 공을 재개 자리로 가져옴 (살짝 띄워 건네주듯)
            {
                float dx = rX - ballX, dy = rY - ballY, dd = Mathf.Sqrt(dx * dx + dy * dy), step = FetchSpeed * dt;
                if (dd <= step) { ballX = rX; ballY = rY; ballZ = 0f; rStage = 2; }
                else { ballX += dx / dd * step; ballY += dy / dd * step; ballZ = Mathf.Min(1.2f, dd * 0.08f); }
                return;
            }
            rT -= dt;
            ballX = rX; ballY = rY; ballZ = 0f;
            // 페널티킥·프리킥: 마지막에 키커가 공으로 달려듦
            if ((restart == Restart.Penalty || (restart == Restart.FreeKick && rDanger)) && rT < 0.5f)
            { rSx[rTaker] = rX; rSy[rTaker] = rY + Inward(rTeam) * 0.4f; }
            bool ready = DistTo(dots[rTaker], rSx[rTaker], rSy[rTaker]) < 1.5f;
            float giveUp = -2.5f;
            if (restart == Restart.Kickoff)   // 킥오프: 모두 중앙선 기준 자기 진영에 들어오고, 상대는 센터 서클 밖으로 나갈 때까지
            {
                giveUp = -15f;
                for (int i = 0; i < 22 && ready; i++)
                {
                    if (i == rTaker) continue;
                    var d = dots[i];
                    if (d.team == 0 ? d.y < L / 2f + 0.3f : d.y > L / 2f - 0.3f) ready = false;
                    else if (d.team != rTeam && DistTo(d, rX, rY) < 8.5f) ready = false;
                    else if (rT > -8f && rUse[i] && DistTo(d, rSx[i], rSy[i]) > 6f) ready = false;   // 대형 자리 가까이까지 (늦으면 진영만 확인)
                }
            }
            if (rT > 0f || (!ready && rT > giveUp)) return;
            ExecuteRestart();
        }

        void ExecuteRestart()
        {
            var kind = restart; restart = Restart.None; execKind = kind;
            for (int i = 0; i < 22; i++) rUse[i] = false;
            int team = rTeam;
            carrier = rTaker; var c = dots[carrier];
            ballX = rX; ballY = rY;
            decideT = Rand(0.4f, 0.8f);
            executing = true;
            focusFlight = kind == Restart.Penalty || (kind == Restart.FreeKick && rDanger);   // 코너킥 크로스는 평소 속도로 (준비만 느리게)
            switch (kind)
            {
                case Restart.Kickoff:   // 뒤쪽 동료에게 짧게
                {
                    int best = -1; float bd = float.MaxValue;
                    for (int s = 1; s <= 10; s++)
                    {
                        int i = Idx(team, s); if (i == carrier) continue;
                        var m = dots[i]; float behind = team == 0 ? m.y - c.y : c.y - m.y;
                        if (behind < 2f) continue;
                        float dd = DistTo(m, c.x, c.y);
                        if (dd < bd) { bd = dd; best = i; }
                    }
                    if (best >= 0) StartPass(carrier, best);
                    break;
                }
                case Restart.ThrowIn:
                {
                    int best = -1; float bs = -99f;
                    for (int s = 1; s <= 10; s++)
                    {
                        int i = Idx(team, s); if (i == carrier) continue;
                        var m = dots[i]; float dd = DistTo(m, rX, rY);
                        if (dd > 25f) continue;
                        float fwd = team == 0 ? rY - m.y : m.y - rY;
                        float score = -dd * 0.06f + fwd * 0.02f + (float)rng.NextDouble() * 0.4f;
                        if (score > bs) { bs = score; best = i; }
                    }
                    if (best >= 0)
                    {
                        var r = dots[best]; float dd = DistTo(r, rX, rY);
                        StartFlight(Mathf.Clamp(r.x, 1f, W - 1f), Mathf.Clamp(r.y, 1f, L - 1f), Mathf.Max(0.5f, dd / 12f), KindPass, best, 1.8f);
                    }
                    break;
                }
                case Restart.GoalKick:
                    if ((LongStyle(team) || rng.NextDouble() < 0.3) && LongBall(carrier)) break;
                    { int r = ChoosePass(c); if (r >= 0) StartPass(carrier, r); }
                    break;
                case Restart.Corner:
                    // 코너킥 크로스는 강하게 감아 올려 보통 패스보다 빠르게 날아감
                    if (rScripted && script != null) { StartPass(carrier, Idx(team, script.scorerSlot), 3.5f, CornerSpeed); phase = Phase.Pass; break; }
                    { var box = BoxMates(team); if (box.Count > 0) StartPass(carrier, box[rng.Next(box.Count)], 3.5f, CornerSpeed); }
                    break;
                case Restart.Penalty:
                    if (rScripted && script != null) ShootScript(0.8f);
                    break;
                default:   // 프리킥
                    if (rScripted && script != null) { ShootScript(2.4f); break; }   // 수비벽을 넘겨 감아 차기
                    if (rDanger && rng.NextDouble() < 0.55)
                    {
                        double r = rng.NextDouble();
                        var wall = Nearest(1 - team, rX, rY, 1);
                        if (r < 0.25 && wall.Count > 0)   // 수비벽에 맞음
                        { var w = dots[wall[0]]; StartFlight(w.x, w.y, 0.25f, KindPass, wall[0], 0.8f); }
                        else StartShot(c, OnTarget(c) ? KindSaved : KindWide, 2.4f);
                        break;
                    }
                    if (rDanger) { var box = BoxMates(team); if (box.Count > 0) { StartPass(carrier, box[rng.Next(box.Count)], 3.2f); break; } }
                    { int r = ChoosePass(c); if (r >= 0) StartPass(carrier, r); }
                    break;
            }
            executing = false;
        }

        List<int> BoxMates(int team)
        {
            var box = new List<int>();
            for (int s = 1; s <= 10; s++) { int i = Idx(team, s); if (i != carrier && i != YouIdx && InBox(team, dots[i].x, dots[i].y)) box.Add(i); }
            return box;
        }
    }
}
