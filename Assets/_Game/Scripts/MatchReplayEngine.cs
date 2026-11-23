using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public enum ReplayEventType { GoalOurs, GoalTheirs, ShotYou, TackleYou }
    public enum SetPiece { None, Corner, FreeKick, Penalty }   // 세트피스로 나온 골

    // 경기 영상에서 보여줄 한 장면. 실제 경기 결과(득점·도움·슈팅 수)와 어긋나지 않게 미리 짜 둡니다.
    public class ReplayEvent
    {
        public float minute;
        public ReplayEventType type;
        public int scorerSlot = -1, assisterSlot = -1;   // 0=골키퍼, 1~4 수비, 5~8 미드필더, 9~10 공격수
        public bool youScore, youAssist;
        public bool saved;                                // ShotYou: 골키퍼가 막았는지(true) 빗나갔는지(false)
        public SetPiece setPiece;                         // 골이 나온 세트피스 (None이면 경기 중 골)
        public bool header;                               // 헤딩 슈팅이었는지 (영상이 정함: 코너킥·측면 크로스)
        public bool longShot;                             // 상자 밖에서 때린 중거리 슛인지 (영상이 정함)
    }

    // 저장된 경기 결과로부터 장면 목록을 만듭니다.
    public static class MatchReplayPlan
    {
        static int RandomMate(System.Random rng, int excludeA, int excludeB, SlotDef[] layout)
        {
            float total = 0f;
            for (int s = 1; s <= 10; s++) if (s != excludeA && s != excludeB) total += Weight(layout[s].kind);
            float r = (float)rng.NextDouble() * total;
            int last = 1;
            for (int s = 1; s <= 10; s++)
            {
                if (s == excludeA || s == excludeB) continue;
                last = s; r -= Weight(layout[s].kind);
                if (r <= 0f) return s;
            }
            return last;
        }

        // 골에 관여할 가능성: 앞쪽·측면 공격 자리일수록 높음
        static float Weight(SlotKind k)
        {
            switch (k)
            {
                case SlotKind.ST: return .27f; case SlotKind.W: return .22f; case SlotKind.AM: return .20f; case SlotKind.WM: return .14f;
                case SlotKind.CM: return .12f; case SlotKind.WB: return .09f; case SlotKind.FB: return .07f; case SlotKind.DM: return .07f;
                case SlotKind.CB: return .04f; default: return 0f;
            }
        }

        // shootN: 슈팅 능력(0~1), tackles: 경기 기록의 수비 기여 횟수
        // ours/theirs: 두 팀의 포메이션 (득점자·도움 자리를 고를 때 씀)
        public static List<ReplayEvent> Build(MatchRecord m, bool youPlayed, int youSlot, System.Random rng, float shootN, int tackles, Formation ours, Formation theirs)
        {
            var lo = TacticSystem.Layout(ours); var lt = TacticSystem.Layout(theirs);
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
                    e.assisterSlot = rng.NextDouble() < 0.5 ? RandomMate(rng, youSlot, -1, lo) : -1;
                }
                else if (i < youGoals + youAssists)
                {
                    e.scorerSlot = RandomMate(rng, you, -1, lo); e.assisterSlot = youSlot; e.youAssist = true;
                }
                else
                {
                    e.scorerSlot = RandomMate(rng, you, -1, lo);
                    e.assisterSlot = rng.NextDouble() < 0.7 ? RandomMate(rng, you, e.scorerSlot, lo) : -1;
                }
                list.Add(e);
            }
            for (int i = 0; i < Mathf.Max(0, m.goalsAgainst); i++)
            {
                var e = new ReplayEvent { type = ReplayEventType.GoalTheirs };
                e.scorerSlot = RandomMate(rng, -1, -1, lt);
                e.assisterSlot = rng.NextDouble() < 0.7 ? RandomMate(rng, e.scorerSlot, -1, lt) : -1;
                list.Add(e);
            }
            int extraShots = youPlayed ? Mathf.Min(4, Mathf.Max(0, m.playerShots - youGoals)) : 0;
            for (int i = 0; i < extraShots; i++)
                list.Add(new ReplayEvent { type = ReplayEventType.ShotYou, scorerSlot = youSlot, saved = rng.NextDouble() < 0.3 + 0.6 * shootN });   // 슈팅이 좋을수록 유효 슈팅(선방)이 많음

            int tackleEvents = youPlayed ? Mathf.Min(Mathf.Max(0, tackles), Mathf.Max(0, 10 - list.Count)) : 0;
            for (int i = 0; i < tackleEvents; i++)
                list.Add(new ReplayEvent { type = ReplayEventType.TackleYou, scorerSlot = -1 });

            // 세트피스 골: 실제 축구처럼 골의 약 1/4. 도움이 있으면 코너킥, 없으면 페널티킥이나 직접 프리킥.
            // 저장된 득점 기록과 순서가 어긋나지 않도록 따로 만든 난수를 씁니다.
            var sr = new System.Random(m.seed ^ 0x51ec7);
            foreach (var e in list)
            {
                if (e.type != ReplayEventType.GoalOurs && e.type != ReplayEventType.GoalTheirs) continue;
                double r = sr.NextDouble();
                if (e.assisterSlot >= 0) { if (r < 0.15) e.setPiece = SetPiece.Corner; }
                else if (r < 0.28) e.setPiece = SetPiece.Penalty;
                else if (r < 0.4) e.setPiece = SetPiece.FreeKick;
            }

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

        public static string SetPieceName(SetPiece s)
        {
            return s == SetPiece.Penalty ? "PK" : s == SetPiece.FreeKick ? "프리킥" : s == SetPiece.Corner ? "코너킥" : "";
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
        public bool earlyRun;                  // 침투 타이밍을 놓쳐 너무 일찍 라인을 넘음 (오프사이드가 될 수 있음)
        public float runAge;                   // 뛰어들기 시작한 뒤 흐른 시간
        public bool started;                   // 기준점을 처음 위치로 잡았는가
        public SlotDef def;                    // 포메이션 속 자리 (기본 위치와 역할)
    }

    // 팀 스타일이 경기 영상 움직임에 주는 값
    public class StyleParams
    {
        public float attackPush, defendDrop;          // 공격·수비할 때 대형 높이 조정 (깊이)
        public float attackWidth, defendWidth, slide; // 대형 폭, 수비 때 공 쪽으로 쏠리는 정도
        public float overlap;                         // 풀백·윙백 오버래핑 정도
        public float support = 1f;                    // 공이 앞으로 갈 때 수비·미드필더가 따라 올라서는 정도 (팀 전체가 같이 공격)
        public float pressFrom;                       // 공이 우리 골에서 이 깊이 안쪽이면 압박 (1이면 어디서든)
        public bool doublePress;                      // 둘이서 공을 압박
        public float passProb, decideMin, decideMax;  // 공을 잡으면 패스할 확률, 다음 결정까지 시간
        public float fwdWeight, distPenalty, maxPass; // 패스 고르기: 앞으로 보내는 정도, 짧은 패스 선호, 최대 거리
        public float shotDepth;                       // 이 깊이보다 앞이면 슈팅을 노림
        public float runForward, runMid;              // 공 없이 뒷공간으로 뛰어드는 빈도
        public bool wing, counter;                    // 측면으로 벌려 크로스 / 공을 뺏으면 빠른 역습

        public static StyleParams For(TacticStyle s)
        {
            switch (s)
            {
                case TacticStyle.Counter: return new StyleParams { attackPush = .03f, defendDrop = -.08f, attackWidth = .9f, defendWidth = .66f, slide = .38f, overlap = .6f, support = .75f,
                    pressFrom = .5f, passProb = .8f, decideMin = .5f, decideMax = 1.1f, fwdWeight = .07f, distPenalty = .008f, maxPass = 48f, shotDepth = .76f, runForward = .12f, runMid = .05f, counter = true };
                case TacticStyle.HighPress: return new StyleParams { attackPush = .06f, defendDrop = .06f, attackWidth = .95f, defendWidth = .72f, slide = .32f, overlap = 1f, support = 1.1f,
                    pressFrom = 1f, doublePress = true, passProb = .82f, decideMin = .5f, decideMax = 1.1f, fwdWeight = .045f, distPenalty = .012f, maxPass = 38f, shotDepth = .78f, runForward = .09f, runMid = .04f };
                case TacticStyle.LowBlock: return new StyleParams { attackPush = 0f, defendDrop = -.12f, attackWidth = .9f, defendWidth = .58f, slide = .42f, overlap = .5f, support = .65f,
                    pressFrom = .42f, passProb = .72f, decideMin = .7f, decideMax = 1.5f, fwdWeight = .06f, distPenalty = .006f, maxPass = 46f, shotDepth = .78f, runForward = .07f, runMid = .02f };
                case TacticStyle.Wing: return new StyleParams { attackPush = .05f, defendDrop = -.04f, attackWidth = 1.12f, defendWidth = .68f, slide = .36f, overlap = 1.4f, support = 1f,
                    pressFrom = .7f, passProb = .85f, decideMin = .6f, decideMax = 1.2f, fwdWeight = .035f, distPenalty = .012f, maxPass = 40f, shotDepth = .80f, runForward = .09f, runMid = .04f, wing = true };
                default: return new StyleParams { attackPush = .06f, defendDrop = -.02f, attackWidth = 1f, defendWidth = .7f, slide = .38f, overlap = 1f, support = 1.1f,
                    pressFrom = .75f, passProb = .92f, decideMin = .5f, decideMax = 1f, fwdWeight = .02f, distPenalty = .02f, maxPass = 30f, shotDepth = .82f, runForward = .06f, runMid = .05f };
            }
        }
    }

    // 점 22개와 공이 실제 축구처럼 움직이는 간단한 경기 엔진입니다. 화면과는 무관하게 위치만 계산합니다.
    // 좌표는 미터이고 가로 0~68, 세로 0~105 (세로 0이 위쪽 골대)입니다.
    public class MatchReplayEngine
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
        bool gatherPassed;              // 그 선수에게 패스를 보냈는지

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
            depth = Mathf.Clamp(depth, 0.03f, d.slot == 0 ? 0.14f : 0.95f);
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
                if (c.slot != 0 && stagedRunner >= 0)   // 오프사이드 장면: 동료가 뛰어드는 동안 공을 지키며 기다림
                { c.tx = c.x; c.ty = c.y; c.maxSpeed = JogSpeed; }
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
        void MarkOffside(int recv)
        {
            offsidePass = false;
            if (script != null || restart != Restart.None || executing || recv < 0) return;
            var r = dots[recv];
            if (Dp(r.team, r.y) > SafeDepth(r.team) + 0.005f) { offsidePass = true; offX = r.x; offY = r.y; }
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
            int def = 1 - poss;
            var st = sp[def];
            float sum = 0f; int n = 0;
            for (int s = 1; s <= 10; s++) { var d = dots[Idx(def, s)]; if (d.def.role == SquadRole.DF) { sum += Dp(def, d.ty); n++; } }
            if (n == 0) return;
            float line = sum / n;
            if (st.pressFrom >= 1f) line = Mathf.Max(line, BallDepth(def) > 0.5f ? 0.3f : line);   // 전방 압박: 높은 라인
            if (st.pressFrom <= 0.45f) line = Mathf.Min(line, 0.24f);                            // 내려선 수비: 깊은 라인
            for (int s = 1; s <= 10; s++)
            {
                var d = dots[Idx(def, s)];
                float td = Dp(def, d.ty);
                if (d.def.role == SquadRole.DF) td = line + (d.def.kind == SlotKind.FB || d.def.kind == SlotKind.WB ? 0.01f : 0f);
                else if (d.def.role == SquadRole.MF) td = Mathf.Clamp(td, line + 0.07f, line + 0.14f);
                else td = Mathf.Min(td, line + 0.38f);
                d.ty = DepthY(def, td);
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

        // ---------- 공 ----------
        void StartFlight(float tx, float ty, float dur, int kind, int recv, float zPeak)
        {
            flying = true;
            fFx = ballX; fFy = ballY; fTx = tx; fTy = ty; fT = 0f; fDur = dur; fKind = kind; fRecv = recv; fZ = zPeak; fLong = false;
            if (carrier >= 0) lastPoss = dots[carrier].team;
            carrier = -1;
        }

        // speed: 공이 나는 속도(거리 ÷ 시간, m/s). 주지 않으면 보통 패스 17, 높이 뜬 크로스 14
        void StartPass(int from, int to, float z = -1f, float speed = -1f)
        {
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
                float outP = (dist > 28f ? 0.09f : 0.05f) + (Mathf.Abs(tx - 34f) > 22f ? 0.16f : 0f);
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
            StartFlight(tx, ty, dur, KindOut, recv, z);
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
            StartFlight(Mathf.Clamp(r.x + Rand(-3f, 3f), 2f, W - 2f), Mathf.Clamp(r.y + Rand(-3f, 3f), 2f, L - 2f), Mathf.Max(1f, dist / 20f), KindPass, best, 7f);
            fLong = true;
            return true;
        }

        // 길게 차는 팀 (역습·내려선 수비)
        bool LongStyle(int team) { return sp[team].distPenalty < 0.01f; }

        // outcome: KindGoal / KindSaved / KindWide
        void StartShot(ReplayDot shooter, int outcome, float z = 1.6f)
        {
            float goalY = shooter.team == 0 ? 0f : L;
            shotSide = rng.NextDouble() < 0.5 ? 1f : -1f;
            if (outcome == KindGoal) shotGx = 34f + shotSide * 3f;
            else if (outcome == KindSaved) shotGx = 34f + shotSide * Rand(1.5f, 3.5f);
            else shotGx = 34f + shotSide * Rand(4.8f, 9f);
            bool byYou = YouIdx >= 0 && shooter == dots[YouIdx];
            float ty = outcome == KindSaved ? (shooter.team == 0 ? 1.6f : L - 1.6f) : goalY;   // 막히는 슛: 골키퍼가 몸을 날린 자리에서 막힘
            StartFlight(shotGx, ty, byYou ? 0.5f - 0.15f * stats.shootN : 0.5f, outcome, -1, z);   // 슈팅이 좋을수록 빠른 슛
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
                        if (dd < 1.5f && rng.NextDouble() < 0.5 * dt * (carrier == YouIdx ? 1.4f - stats.dribbleN : 1f))
                        {
                            if (rng.NextDouble() < FoulChance) { Foul(d); break; }   // 거친 태클은 파울 → 프리킥
                            if (Mathf.Abs(d.x - 34f) > 22f && rng.NextDouble() < 0.5)   // 터치라인 근처: 태클한 공이 밖으로 → 스로인
                            {
                                lastPoss = o.team; carrier = -1;
                                StartOut(d.x < 34f ? -1.5f : W + 1.5f, d.y + Rand(-3f, 3f), Restart.ThrowIn, d.team, 0.4f, 0.5f);
                                break;
                            }
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
                    StartShot(c, rng.NextDouble() < 0.5 ? KindSaved : KindWide);
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
                    badPass = carrier == YouIdx && rng.NextDouble() >= stats.passAccuracy;   // 패스 능력만큼만 정확함
                    if (depth > 0.66f) finalPasses++;
                    StartPass(carrier, r);
                    return;
                }
            }
            decideT = counterT[c.team] > 0f ? Rand(0.25f, 0.5f) : Rand(st.decideMin, st.decideMax);
        }

        void EndFlight()
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
                StartShot(a, rng.NextDouble() < 0.45 ? KindSaved : KindWide, 0.7f);
                return true;
            }
            return false;
        }

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

        int PickTaker(Restart kind)
        {
            if (rScripted && script != null)
                return Idx(rTeam, kind == Restart.Corner && script.assisterSlot >= 0 ? script.assisterSlot : script.scorerSlot);
            if (kind == Restart.Kickoff) return Idx(rTeam, kickSlot[rTeam]);
            if (kind == Restart.GoalKick) return Idx(rTeam, 0);
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
            var kind = restart; restart = Restart.None;
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
                        else StartShot(c, r < 0.6 ? KindSaved : KindWide, 2.4f);
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
            scriptTeam = ev.type == ReplayEventType.GoalTheirs ? 1 : 0;
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
                if ((dist < 2.5f && phaseT > 2.5f) || phaseT > 7f)
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
                if (phaseT > 1.4f)
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
            else StartShot(c, KindGoal, z);
            phase = Phase.Shot;
        }

        void ApplyScriptTargets()
        {
            var ev = script;
            if (phase == Phase.SetPiece) return;   // 세트피스 자리는 재개 쪽에서 정함
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
                if (ev.type != ReplayEventType.ShotYou) gk.tx = 34f - shotSide * 3f;          // 골: 골키퍼는 반대 방향으로 몸을 던짐
                else if (ev.saved) { gk.tx = shotGx; gk.ty = fTy; }                         // 선방: 공 오는 곳으로 몸을 날림
                gk.maxSpeed = KeeperDive;
            }
        }
    }
}
