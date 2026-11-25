using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 경기 영상 엔진: 득점·슈팅·수비·선방 장면과 골 세리머니
    public partial class MatchReplayEngine
    {
        // ---------- 득점·슈팅 장면 ----------
        void BeginScript(ReplayEvent ev)
        {
            if (ev.type == ReplayEventType.TackleYou)
            {
                if (YouIdx < 0) return;   // 벤치라면 건너뜀
                // 내 선수와 가장 가까운 상대 선수가 공을 잡고 달려오고, 내 선수가 따라가 공을 빼앗습니다.
                // 상대가 공을 가지고 있을 때만 시작합니다 (시작 조건은 Step에서 확인). 그 선수를 쫓아가 빼앗습니다.
                if (carrier < 0 || flying || dots[carrier].team != 1) return;
                script = ev; phase = Phase.Press; phaseT = 0f; scriptTeam = 1; lastPoss = 1;
                return;
            }
            // 장면을 시작할 선수(도움 또는 득점)가 공을 잡을 때까지(Gather): 우리 공이면 그 선수에게 패스,
            // 상대 공이면 가장 가까운 우리 선수가 달려가 빼앗은 뒤 패스. 공은 순간이동하지 않습니다.
            script = ev; phase = Phase.Gather; phaseT = 0f;
            scriptTeam = ev.type == ReplayEventType.GoalTheirs || ev.type == ReplayEventType.SaveYou ? 1 : 0;
            gatherSlot = ev.assisterSlot >= 0 ? ev.assisterSlot : ev.scorerSlot;
            gatherPassed = false;
        }

        // 장면을 시작할 선수가 공을 잡으면: 어디로 몰고 가서 어떻게 마무리할지 정합니다.
        void StartBuild()
        {
            var ev = script;
            phase = Phase.Build; phaseT = 0f;
            carrier = Idx(scriptTeam, gatherSlot);
            var c = dots[carrier];
            lastPoss = scriptTeam;
            buildY = DepthY(scriptTeam, ev.assisterSlot >= 0 ? 0.68f : 0.80f);
            buildX = Mathf.Clamp(c.x + (34f - c.x) * 0.4f + Rand(-6f, 6f), 8f, 60f);
            if (gatherSlot == 0) { buildX = c.x; buildY = DepthY(scriptTeam, 0.08f); }   // 골키퍼의 도움: 공을 잡고 골문 앞에서 길게 차 줌
            // 측면 공격 팀: 측면 선수가 도움이면 터치라인 깊숙이 몰고 가서 크로스
            var adef = ev.assisterSlot >= 0 ? lay[scriptTeam][ev.assisterSlot] : default(SlotDef);
            bool wideAssist = ev.assisterSlot >= 0 && (adef.kind == SlotKind.W || adef.kind == SlotKind.WM || adef.kind == SlotKind.WB || adef.kind == SlotKind.FB);
            if (sp[scriptTeam].wing && wideAssist)
            {
                bool left = (scriptTeam == 0 ? adef.x : W - adef.x) < 34f;
                buildX = left ? 6f : W - 6f; buildY = DepthY(scriptTeam, 0.82f);
            }
            // 세트피스 골: 페널티킥은 상자 안에서 넘어짐, 프리킥은 상자 앞에서 반칙, 코너킥은 측면 깊이 들어가 수비가 걷어냄
            if (ev.setPiece == SetPiece.Penalty) { buildX = 34f + Rand(-8f, 8f); buildY = DepthY(scriptTeam, 0.9f); }
            else if (ev.setPiece == SetPiece.FreeKick) { buildX = 34f + Rand(-10f, 10f); buildY = DepthY(scriptTeam, 0.74f); }
            else if (ev.setPiece == SetPiece.Corner) { buildX = c.x < 34f ? 5f : W - 5f; buildY = DepthY(scriptTeam, 0.86f); }
            scorerOffset = Rand(-6f, 6f);
            // 중거리 슛: 세트피스·측면 크로스가 아니면 드물게 상자 바로 밖(약 27m)에서 때립니다. 미드필더·수비수일수록 조금 더 자주.
            ev.longShot = false;
            if (ev.setPiece == SetPiece.None && !(sp[scriptTeam].wing && wideAssist))
            {
                var sdef = lay[scriptTeam][ev.scorerSlot];
                double p = sdef.role == SquadRole.FW ? 0.05 : sdef.role == SquadRole.MF ? 0.13 : 0.18;   // 중거리 골은 드물게 (전체 골의 약 8%)
                if (rng.NextDouble() < p)
                {
                    ev.longShot = true;
                    if (ev.assisterSlot < 0) buildY = DepthY(scriptTeam, 0.74f);
                }
            }
        }

        void CallSetPiece()
        {
            var ev = script; var c = dots[carrier];
            phase = Phase.SetPiece; phaseT = 0f;
            if (SetPieceCalled != null) SetPieceCalled(ev);
            if (ev.setPiece == SetPiece.Corner)
            {
                lastPoss = 1 - scriptTeam; carrier = -1;
                StartOut(c.x < 34f ? Rand(3f, 10f) : W - Rand(3f, 10f), scriptTeam == 0 ? -1.5f : L + 1.5f, Restart.Corner, scriptTeam, 0.45f, 1.5f, -1, true);
            }
            else StartRestart(ev.setPiece == SetPiece.Penalty ? Restart.Penalty : Restart.FreeKick, scriptTeam, c.x, c.y, true);
        }

        void UpdateScript(float dt)
        {
            phaseT += dt;
            var ev = script;
            if (phase == Phase.Press)
            {
                if (carrier < 0 || dots[carrier].team != 1) { script = null; phase = Phase.None; return; }
                var opp = dots[carrier]; var you = dots[YouIdx];
                float dd = Mathf.Sqrt((opp.x - you.x) * (opp.x - you.x) + (opp.y - you.y) * (opp.y - you.y));
                if (dd >= 1.8f && phaseT > 14f) { script = null; phase = Phase.None; return; }   // 끝내 못 따라잡으면 태클 없이 끝
                if (dd < 1.8f)   // 가까워지면 태클 성공 (공은 내 발밑으로 굴러옴)
                {
                    carrier = YouIdx; lastPoss = 0; decideT = 0.8f;
                    if (TackleWon != null) TackleWon(ev);
                    script = null; phase = Phase.None;
                }
                return;
            }
            if (phase == Phase.Gather)
            {
                if (flying || restart != Restart.None) return;   // 날아가는 공(패스·굴러가는 공)이나 재개(스로인 등)가 끝날 때까지
                int target = Idx(scriptTeam, gatherSlot);
                if (carrier == target) { StartBuild(); return; }
                if (carrier >= 0 && dots[carrier].team == scriptTeam)
                {
                    if (!gatherPassed) { StartPass(carrier, target); gatherPassed = true; }   // 우리 공: 그 선수에게 패스
                    return;
                }
                int ch = GatherChaser();   // 상대 공이거나 멈춘 공: 가장 가까운 우리 선수가 달려가 빼앗음
                if (ch >= 0 && (DistTo(dots[ch], ballX, ballY) < 1.6f || phaseT > 8f))
                { carrier = ch; lastPoss = scriptTeam; gatherPassed = false; }
                return;
            }
            if (phase == Phase.Build && (carrier < 0 || dots[carrier].team != scriptTeam)) { phase = Phase.Gather; phaseT = 0f; gatherPassed = false; return; }   // 공을 놓쳤으면 다시 가져옴
            if (phase == Phase.Build)
            {
                var c = dots[carrier];
                float dist = Mathf.Sqrt((buildX - c.x) * (buildX - c.x) + (buildY - c.y) * (buildY - c.y));
                // 세트피스(코너킥·프리킥·페널티킥)는 그 자리까지 실제로 몰고 간 뒤에만 선언 (멀리서 갑자기 공이 나가지 않게)
                bool arrived = ev.setPiece != SetPiece.None ? (dist < 4f && phaseT > 1.5f) || phaseT > 20f : (dist < 2.5f && phaseT > 2.5f) || phaseT > 7f;
                if (arrived)
                {
                    if (ev.setPiece != SetPiece.None) { CallSetPiece(); return; }
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
                if (ev.setPiece == SetPiece.Corner || lastPassCross) { if (phaseT > 0.1f) ShootScript(0.7f, true); }   // 코너킥·크로스: 바로 헤딩
                else if (phaseT > 0.45f) ShootScript();
            }
            else if (phase == Phase.SetPiece)
            {
                if (phaseT > 15f && restart == Restart.None && !flying) ShootScript();   // 혹시 멈추면 바로 슈팅
            }
            else if (phase == Phase.After)
            {
                if (phaseT > CelebrateTime)
                {
                    Kickoff(1 - scriptTeam);
                    script = null; phase = Phase.None;
                }
            }
        }

        // 공에 가장 가까운 장면 팀 선수 (골키퍼 제외)
        int GatherChaser()
        {
            int best = -1; float bd = float.MaxValue;
            for (int s = 1; s <= 10; s++)
            {
                int i = Idx(scriptTeam, s);
                float d = DistTo(dots[i], ballX, ballY);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void ShootScript(float z = 1.6f, bool header = false)
        {
            var ev = script;
            carrier = Idx(scriptTeam, ev.scorerSlot);
            var c = dots[carrier];
            // 공은 이미 그 선수 발 앞에 있으므로 옮기지 않습니다 (페널티킥·프리킥은 공이 놓인 자리에서)
            ev.header = header;
            if (ShotTaken != null && (ev.youScore || ev.type == ReplayEventType.ShotYou)) ShotTaken(ev);
            if (ev.type == ReplayEventType.ShotYou) StartShot(c, ev.saved ? KindSaved : KindWide);
            else if (ev.type == ReplayEventType.SaveYou) StartShot(c, KindSaved, z);   // 내가(골키퍼) 막는 슈팅
            else StartShot(c, KindGoal, z);
            phase = Phase.Shot;
        }

        // 골 순간: 세리머니할 자리와 모여들 동료를 정합니다.
        void StartCelebration()
        {
            celebMates.Clear(); celebScorer = -1;
            if (script == null || script.scorerSlot < 0) return;
            celebScorer = Idx(scriptTeam, script.scorerSlot);
            var s = dots[celebScorer];
            // 가끔(약 1/3)은 득점자 혼자 가까운 쪽 터치라인 너머 관중석으로 달려가 세리머니
            CelebrateSolo = rng.NextDouble() < 0.35;
            if (CelebrateSolo)
            {
                float sx = s.x < W / 2f ? 0.6f : W - 0.6f;
                float ddx = sx - s.x, ddy = DepthY(scriptTeam, 0.78f) - s.y, dl = Mathf.Sqrt(ddx * ddx + ddy * ddy), go = Mathf.Min(dl, 20f);
                celebX = dl > 0.01f ? s.x + ddx / dl * go : s.x; celebY = dl > 0.01f ? s.y + ddy / dl * go : s.y;
                return;   // 동료는 모이지 않고 자기 자리로
            }
            // 득점자는 가까운 쪽 코너 깃발 방향으로 달려 나감 (상자 옆 측면)
            float cx = s.x < W / 2f ? Rand(6f, 11f) : W - Rand(6f, 11f), cy = DepthY(scriptTeam, Rand(0.84f, 0.9f));
            float dx = cx - s.x, dy = cy - s.y, len = Mathf.Sqrt(dx * dx + dy * dy), run = Mathf.Min(len, 9f);   // 너무 멀리 가지 않게 (동료가 따라올 수 있게)
            celebX = len > 0.01f ? s.x + dx / len * run : s.x; celebY = len > 0.01f ? s.y + dy / len * run : s.y;
            // 득점자에게 가까운 필드 동료 5명 (도움 선수는 꼭 포함)이 달려와 모임
            var near = new List<int>();
            for (int k = 1; k <= 10; k++) { int i = Idx(scriptTeam, k); if (i != celebScorer) near.Add(i); }
            near.Sort((a, b) => DistTo(dots[a], s.x, s.y).CompareTo(DistTo(dots[b], s.x, s.y)));
            if (script.assisterSlot > 0) { int a = Idx(scriptTeam, script.assisterSlot); near.Remove(a); near.Insert(0, a); }
            for (int k = 0; k < near.Count && celebMates.Count < 5; k++)
                if (celebMates.Count < 2 || DistTo(dots[near[k]], s.x, s.y) < 32f) celebMates.Add(near[k]);   // 너무 먼 동료는 제자리로
        }

        // 세리머니 동안의 움직임: 득점자는 코너 쪽으로 뛰어가 멈추고, 동료들은 그 둘레로 모여 듦.
        // 나머지 동료는 천천히 자기 진영으로, 실점한 팀은 고개를 숙이고 자기 진영으로 걸어 돌아감.
        void CelebrationTargets()
        {
            if (celebScorer < 0) return;
            var s = dots[celebScorer];
            s.tx = celebX; s.ty = celebY; s.maxSpeed = CelebrateSolo ? 8.5f : phaseT < 1.4f ? 7.5f : 1.5f;   // 혼자일 때는 관중석까지 전력 질주
            float hx = s.x, hy = s.y;   // 모이는 곳: 득점자 자리
            for (int k = 0; k < celebMates.Count; k++)
            {
                var m = dots[celebMates[k]];
                float ang = (k + 0.5f) / celebMates.Count * Mathf.PI * 2f + phaseT * 0.6f;   // 득점자 둘레를 천천히 돌며 껴안음
                float r = phaseT < 0.9f ? 4f : 1.6f;
                m.tx = Mathf.Clamp(hx + Mathf.Cos(ang) * r, 1f, W - 1f); m.ty = Mathf.Clamp(hy + Mathf.Sin(ang) * r, 1f, L - 1f);
                m.maxSpeed = 8.5f;
            }
            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 11; k++)
                {
                    int i = Idx(t, k);
                    if (i == celebScorer || celebMates.Contains(i)) continue;
                    var d = dots[i];
                    if (k == 0) { d.tx = W / 2f; d.ty = DepthY(t, 0.03f); d.maxSpeed = 1.5f; continue; }   // 골키퍼는 골문 앞
                    // 자기 진영 안쪽(킥오프 자리 쪽)으로 천천히
                    d.tx = d.x + (d.def.x * (t == 0 ? 1f : -1f) + (t == 0 ? 0f : W) - d.x) * 0.3f;
                    d.ty = DepthY(t, Mathf.Min(Dp(t, d.y), 0.45f));
                    d.maxSpeed = t == scriptTeam ? 3f : 2.2f;   // 실점한 팀은 더 느리게 걸음
                }
        }

        void ApplyScriptTargets()
        {
            var ev = script;
            if (phase == Phase.SetPiece) return;   // 세트피스 자리는 재개 쪽에서 정함
            if (phase == Phase.After) { CelebrationTargets(); return; }
            if (phase == Phase.Gather)
            {
                if (carrier >= 0 && dots[carrier].team == scriptTeam) return;   // 우리 공: 평소 움직임 (패스를 받으러)
                int ch = GatherChaser();
                if (ch >= 0) { var cd = dots[ch]; cd.tx = ballX; cd.ty = ballY; cd.maxSpeed = SprintSpeed; }
                return;
            }
            if (phase == Phase.Press)
            {
                var opp = dots[carrier]; var you = dots[YouIdx];
                opp.tx = opp.x + (34f - opp.x) * 0.2f; opp.ty = Mathf.Min(L - 2f, opp.y + 10f); opp.maxSpeed = 5.5f;   // 공을 몰고 가서 조금 느림
                you.tx = opp.x; you.ty = opp.y; you.maxSpeed = 9f;
                return;
            }
            bool cornerKick = ev.setPiece == SetPiece.Corner && phase != Phase.Build;   // 코너킥 뒤에는 상자 안 자리 그대로
            for (int s = 1; s <= 10 && !cornerKick; s++)
            {
                var d = dots[Idx(scriptTeam, s)];
                if ((phase == Phase.Build || phase == Phase.Pass || phase == Phase.Receive) && d.def.role != SquadRole.DF)
                { d.ty = DepthY(scriptTeam, 0.70f + 0.03f * (s % 3)); d.maxSpeed = 8f; }
            }
            var scorer = dots[Idx(scriptTeam, ev.scorerSlot)];
            if (cornerKick && phase == Phase.Pass) { }
            else if (phase == Phase.Build || phase == Phase.Pass)
            { scorer.tx = 34f + scorerOffset; scorer.ty = DepthY(scriptTeam, ev.longShot ? 0.74f : 0.84f); scorer.maxSpeed = 9f; }
            else if (phase == Phase.Receive || phase == Phase.Shot)
            { scorer.tx = scorer.x; scorer.ty = scorer.y; }
            if (phase == Phase.Build && carrier >= 0)
            { var c = dots[carrier]; c.tx = buildX; c.ty = buildY; c.maxSpeed = 8.5f; }
            var gk = dots[Idx(1 - scriptTeam, 0)];
            if (phase == Phase.Shot && flying)
            {
                if (ev.type != ReplayEventType.ShotYou && ev.type != ReplayEventType.SaveYou) gk.tx = 34f - shotSide * 3f;   // 골: 골키퍼는 반대 방향으로 몸을 던짐
                else if (ev.saved) { gk.tx = shotGx; gk.ty = fTy; }                         // 선방: 공 오는 곳으로 몸을 날림
                gk.maxSpeed = KeeperDive;
            }
        }
    }
}
