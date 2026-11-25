using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public partial class MatchReplayEngine
    {
        public const float W = 68f, L = 105f;
        public const float LeadMinutes = 6f;   // 장면 시각보다 이만큼 먼저 공격을 시작합니다.

        // 두 팀의 전술: 포메이션 자리, 스타일 값, 킥오프하는 선수 자리
        readonly SlotDef[][] lay = new SlotDef[2][];
        readonly StyleParams[] sp = new StyleParams[2];
        readonly int[] kickSlot = new int[2];
        readonly float[] counterT = new float[2];   // 공을 뺏은 뒤 빠른 역습 중이면 남은 시간
        int lastOwner = -1;                         // 마지막으로 공을 가졌던 팀 (역습 시작 판정)
        int finalPasses;                            // 공격 진영(상대 골문 앞)에서 이어진 패스 수. 많을수록 슈팅·돌파를 고름

        // 경기가 멈췄다가 다시 시작하는 장면 (킥오프, 스로인, 골킥, 코너킥, 프리킥, 페널티킥)
        public enum Restart { None, Kickoff, ThrowIn, GoalKick, Corner, FreeKick, Penalty }
        Restart restart;                            // 지금 준비 중인 재개
        int rTeam, rTaker;                          // 재개하는 팀과 차는(던지는) 선수
        float rX, rY, rT;                           // 공을 놓는 자리, 남은 준비 시간
        // 재개 단계: 0 멈춤(나간 공이 굴러가다 멈추고 선수들도 멈춤) → 1 공을 재개 자리로 가져옴 → 2 준비(자리 잡기) 뒤 재개
        int rStage = 2;
        float rDeadT, rollVX, rollVY;               // 멈춤 단계 남은 시간, 나간 공이 굴러가는 속도
        const float FetchSpeed = 11f;               // 공을 재개 자리로 가져오는 속도 (m/s)
        const float CornerSpeed = 21f;              // 코너킥 크로스가 나는 속도 (m/s)
        bool rScripted, rDanger, executing;         // 득점 장면의 일부인가 / 골문을 직접 노릴 수 있는 프리킥인가 / 재개 킥 중
        readonly bool[] rUse = new bool[22];        // 재개 때 정해진 자리로 가는 선수
        readonly float[] rSx = new float[22], rSy = new float[22];
        Restart outKind; int outTeam; bool outScripted;   // 공이 밖으로 나가는 중이면 이어질 재개
        bool fLong;                                 // 높이 차 올린 긴 공 (경합이 많아 자주 뺏김)
        bool halfDone;                              // 후반 킥오프를 했는가
        public string CallText = ""; public int CallId;     // 화면에 잠깐 띄울 판정 (스로인, 코너킥…). 새 판정마다 CallId가 늘어남
        public int CallTeam = -1;                           // 그 판정으로 공을 가진 팀 (0 우리, 1 상대, -1 없음)
        public bool CallFoul;                               // 파울 판정인지 (휘슬)
        // 공이 밖으로 나가거나 재개(스로인·골킥·코너킥·프리킥·페널티킥)를 준비하는 동안: 화면에서 느리게 보여 줍니다. 킥오프는 제외
        public bool RestartFocus { get { return (restart != Restart.None && restart != Restart.Kickoff) || (flying && fKind == KindOut); } }
        bool focusFlight;                                   // 코너킥 크로스·프리킥 슈팅이 날아가는 중
        // 코너킥·페널티킥·골문 앞 프리킥은 화면에서 천천히 보여 줍니다.
        public bool SetPieceFocus { get { return restart == Restart.Corner || restart == Restart.Penalty || (restart == Restart.FreeKick && rDanger) || (flying && focusFlight); } }
        public Action<ReplayEvent> SetPieceCalled;          // 득점 장면에서 페널티킥이 선언됨 등
        // 후반전: 두 팀이 진영을 바꿉니다. 엔진 좌표는 그대로 두고 화면이 경기장을 180도 돌려 그립니다.
        public bool SecondHalf { get; private set; }
        public Action HalfTime;                             // 전반이 끝나고 후반 킥오프 준비를 시작함
        public readonly int[] RestartCount = new int[7];    // 재개 종류별 횟수 (확인용)
        public int OffsideCount;                            // 오프사이드 판정 수 (확인용)
        // 오프사이드: 패스 순간 받는 선수가 오프사이드 자리였으면, 공이 도착할 때 판정 → 상대 프리킥
        bool offsidePass, offsideCall;
        float offX, offY;
        // 공을 잃은 팀의 전환 시간: 압박 팀은 바로 되찾으러(게겐프레싱), 나머지는 빠르게 복귀
        readonly float[] lossT = new float[2];
        int prevPoss = -1;
        // 경기마다 2~3번 오프사이드 장면(조건이 맞을 때만 일어나 실제로는 경기당 1번 안팎): 라인 위의 공격수가 너무 일찍 뛰고, 라인을 넘은 뒤 침투 패스가 나옴 → 오프사이드
        // (오프사이드는 경기 기록에 남지 않으므로 저장된 결과와 상관없이 영상에서만 정함)
        readonly List<float> offsideMoments = new List<float>();
        int stagedRunner = -1;

        enum Phase { None, Gather, Build, Pass, Receive, Shot, After, Press, SetPiece }
        const int KindPass = 0, KindGoal = 1, KindSaved = 2, KindWide = 3, KindOut = 4;
        const int KindLoose = 5;        // 빼앗긴 공이 가로챈 선수 쪽으로 굴러감 (끝나면 그 선수의 공)
        const float BallRoll = 16f;     // 공 주인이 바뀌면 공이 새 주인 발밑으로 이 속도(m/s)로 굴러감 (순간이동하지 않게)
        const float KeeperDive = 8.5f;  // 슈팅이 날아올 때 골키퍼가 몸을 날리는 속도
        float gkHoldT;                  // 공을 잡은 골키퍼가 안고 앞으로 나오는 남은 시간
        int gatherSlot;                 // 장면을 시작할 선수 (도움 또는 득점 선수)
        // 골 세리머니: 득점자가 코너 쪽으로 달려 나가고, 가까운 동료들이 모여 함께 기뻐한 뒤 킥오프
        public const float CelebrateTime = 4f;
        int celebScorer = -1;
        public bool CelebrateSolo;      // 이번 세리머니는 득점자 혼자 관중석 쪽으로 (확인용으로 공개)
        float celebX, celebY;
        readonly List<int> celebMates = new List<int>();
        public bool Celebrating { get { return phase == Phase.After && script != null; } }
        bool gatherPassed;              // 그 선수에게 패스를 보냈는지

        public readonly ReplayDot[] dots = new ReplayDot[22];
        public float ballX = W / 2f, ballY = L / 2f, ballZ;
        public int youSlot = -1;
        public Action<ReplayEvent> GoalScored, AssistMade, ShotResolved, TackleWon, ShotTaken;
        public Action<bool> YouPassed;   // 내 선수가 패스함 (true면 동료가 받음)
        bool pendingYouPass, youPassFlight;
        // 팀 기록 (나만의 팀 경기 화면): 슈팅(팀, 결과 KindGoal/KindSaved/KindWide), 패스(팀, 성공), 태클(팀, 성공)
        public Action<int, bool> TeamPassed, TeamTackled;
        public Action<int, bool> TeamShot;   // (팀, 골문으로 간 슛인지)
        public Action<int> KeeperSaved;      // 이 팀 골키퍼가 막음
        int pendingPassTeam = -1, passFlightTeam = -1;
        public ReplayStats stats = new ReplayStats();

        readonly System.Random rng;
        readonly List<ReplayEvent> pending;
        ReplayEvent script;
        Phase phase = Phase.None;
        float phaseT, decideT = 1f, time, boost, clockNow;
        bool badPass;   // 내 선수의 패스가 빗나가는 중
        int carrier = -1, lastPoss, scriptTeam;
        float buildX, buildY, scorerOffset, shotSide, shotGx;
        bool lastPassCross;   // 마지막 패스가 측면에서 상자 안으로 띄운 크로스였는지 (득점 장면이면 헤딩으로 마무리)

        // 공이 날아가는 중일 때
        bool flying;
        float fFx, fFy, fTx, fTy, fT, fDur, fZ;
        int fKind, fRecv;

        int YouIdx { get { return youSlot >= 0 ? Idx(0, youSlot) : -1; } }

        public bool Busy { get { return script != null || pending.Count > 0; } }

        public MatchReplayEngine(List<ReplayEvent> events, int youSlot, System.Random rng, TeamTactic ours, TeamTactic theirs)
        {
            this.rng = rng; this.youSlot = youSlot;
            pending = new List<ReplayEvent>(events);
            int offsides = rng.NextDouble() < 0.5 ? 2 : 3;
            for (int k = 0; k < offsides; k++) offsideMoments.Add(Rand(5f, 80f));
            offsideMoments.Sort();
            var tactics = new[] { ours ?? new TeamTactic(), theirs ?? new TeamTactic() };
            for (int t = 0; t < 2; t++)
            {
                lay[t] = TacticSystem.Layout(tactics[t].formation);
                sp[t] = StyleParams.For(tactics[t].style);
                kickSlot[t] = 10;
                for (int s = 10; s >= 1; s--) if (lay[t][s].kind == SlotKind.ST) kickSlot[t] = s;
            }
            for (int t = 0; t < 2; t++)
                for (int s = 0; s < 11; s++)
                    dots[t * 11 + s] = new ReplayDot
                    {
                        team = t, slot = s, def = lay[t][s],
                        ox = s == 0 ? 0f : Rand(-3f, 3f), oy = s == 0 ? 0f : Rand(-0.025f, 0.025f), noise = Rand(0f, 100f)
                    };
            lastPoss = 0; boost = 2f;
            foreach (var d in dots) { FormationTarget(d, 0f); d.x = d.tx; d.y = d.ty; }
            // 경기 시작: 두 팀이 중앙선 기준 자기 진영의 킥오프 자리에 선 채로 시작합니다.
            StartRestart(Restart.Kickoff, 0, W / 2f, L / 2f, false);
            rStage = 2; ballX = rX; ballY = rY;
            RestartTargets();
            foreach (var d in dots) { d.x = d.tx; d.y = d.ty; d.ax = d.tx; d.ay = d.ty; }
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
            counterT[0] = Mathf.Max(0f, counterT[0] - dt); counterT[1] = Mathf.Max(0f, counterT[1] - dt);
            // 공을 가진 팀이 바뀌면(태클·가로채기·선방) 역습 팀은 빠르게 앞으로
            int owner = carrier >= 0 ? dots[carrier].team : -1;
            if (owner >= 0 && owner != lastOwner)
            {
                if (lastOwner >= 0 && sp[owner].counter && script == null) { counterT[owner] = 4f; decideT = Mathf.Min(decideT, 0.35f); }
                lastOwner = owner; finalPasses = 0;
            }
            // 후반 시작: 전반에 킥오프하지 않은 팀이 킥오프
            if (!halfDone && clockMinute >= 45f && script == null && restart == Restart.None && !flying)
            {
                halfDone = true; SecondHalf = true;
                Kickoff(1);
                rT = 6f;   // 하프타임 안내를 볼 시간
                // 쉬는 시간 뒤 선수들은 바뀐 진영의 킥오프 자리에 서서 나옵니다.
                rStage = 2; ballX = rX; ballY = rY; ballZ = 0f;
                RestartTargets();
                foreach (var d in dots) { d.x = d.tx; d.y = d.ty; d.ax = d.tx; d.ay = d.ty; d.vx = 0f; d.vy = 0f; }
                Call("후반 시작");
                if (HalfTime != null) HalfTime();
            }
            // 오프사이드 장면 시작: 우리 진영을 넘어 공을 가진 팀의, 라인 위 공격수가 너무 일찍 뛰어듦
            if (offsideMoments.Count > 0 && clockMinute >= offsideMoments[0] && stagedRunner < 0 && script == null && restart == Restart.None
                && !flying && carrier >= 0 && dots[carrier].slot != 0 && BallDepth(dots[carrier].team) > 0.25f && BallDepth(dots[carrier].team) < 0.8f)
            {
                int team = dots[carrier].team, best = -1; float bd = -1f;
                for (int s = 1; s <= 10; s++)
                {
                    int i = Idx(team, s); var d = dots[i];
                    if (i == carrier || d.def.role == SquadRole.DF) continue;
                    float dd = Dp(team, d.y);
                    if (dd > bd) { bd = dd; best = i; }
                }
                if (best >= 0 && SafeDepth(team) - bd < 0.14f)   // 라인에서 15m 안에 있는 공격수가 있을 때만
                {
                    var r = dots[best];
                    r.runT = 5f; r.runAge = 0f; r.earlyRun = true; r.runDx = Rand(-5f, 5f);
                    stagedRunner = best;
                    offsideMoments.RemoveAt(0);
                }
            }
            if (script == null && restart == Restart.None && pending.Count > 0 && clockMinute >= pending[0].minute - LeadMinutes
                && (halfDone || pending[0].minute <= 45f))   // 후반 장면은 진영을 바꾼 뒤에
            {
                var next = pending[0];
                // 내 태클 장면: 상대가 공을 가지고 있고 내가 따라잡을 만한 거리(22m 안)일 때 시작
                bool waitTackle = next.type == ReplayEventType.TackleYou && YouIdx >= 0
                    && (flying || carrier < 0 || dots[carrier].team != 1 || DistTo(dots[YouIdx], dots[carrier].x, dots[carrier].y) > 30f);
                if (!waitTackle) { pending.RemoveAt(0); BeginScript(next); }
                else if (clockMinute > next.minute + 8f || clockMinute >= 90f) pending.RemoveAt(0);   // 상대가 공을 잡지 않으면 이 태클 장면은 건너뜀
            }
            if (restart != Restart.None) UpdateRestart(dt);
            UpdateBall(dt);
            UpdateRuns(dt);
            ComputeTargets(dt);
            for (int i = 0; i < dots.Length; i++) MoveDot(dots[i], dt);
            if (script != null) UpdateScript(dt);
        }
    }
}
