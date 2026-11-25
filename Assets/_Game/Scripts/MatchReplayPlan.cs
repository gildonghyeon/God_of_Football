using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 경기 영상의 장면 목록(득점·슈팅·태클·선방)과 그 재료가 되는 값들
    public enum ReplayEventType { GoalOurs, GoalTheirs, ShotYou, TackleYou, SaveYou }   // SaveYou: 골키퍼인 내가 막는 상대 슈팅
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
            if (youPlayed && youSlot == 0)
            {
                // 골키퍼: 수비 기록은 선방 수. 상대가 공격해 와 슈팅하고 내가 막는 장면 (경기 중 슈팅, 페널티킥, 코너킥 헤딩)
                // 득점 기록과 순서가 어긋나지 않도록 따로 만든 난수를 씁니다.
                var kr = new System.Random(m.seed ^ 0x6b33f);
                for (int i = 0; i < Mathf.Min(tackleEvents, 5); i++)
                {
                    var e = new ReplayEvent { type = ReplayEventType.SaveYou, saved = true, scorerSlot = RandomMate(kr, -1, -1, lt) };
                    double r = kr.NextDouble();
                    if (r < 0.16) e.setPiece = SetPiece.Penalty;
                    else if (r < 0.40) { e.setPiece = SetPiece.Corner; e.assisterSlot = RandomMate(kr, e.scorerSlot, -1, lt); }
                    else if (kr.NextDouble() < 0.5) e.assisterSlot = RandomMate(kr, e.scorerSlot, -1, lt);
                    list.Add(e);
                }
            }
            else
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
}
