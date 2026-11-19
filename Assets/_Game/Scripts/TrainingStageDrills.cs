using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 훈련장 동선 데이터입니다. 좌표는 훈련장 배경 그림(752x360)의 픽셀 기준이고 y는 아래로 갈수록 커집니다. 선수 좌표는 발바닥 위치입니다.
    // StageKey(시간, x, y, 동작, 방향, 공 높이): 동작 auto/carry/stand/kick/dive/cheer/jump/hide, 방향 u/d/l/r/* (*는 이동 방향)
    // 화면에는 골대와 페널티 박스가 있는 위쪽 띠(BandTop~BandBottom)만 보입니다.
    // 내 선수는 페널티 박스 가운데(Spot)에서 리프팅을 하고, 강화를 누르면 그 능력치의 짧은 동작 → 결과 반응을 한 뒤 다시 리프팅으로 돌아옵니다.
    public static class TrainingStageDrills
    {
        public const float BandTop = 20f, BandBottom = 256f;   // 보여 줄 배경 범위 (세로)
        public const float SpotX = 375f, SpotY = 240f;          // 내 선수 자리
        public const float HomeGkX = 375f, HomeGkY = 126f;
        public const char HomeGkFace = 'd';
        public const float HomeAttackerX = 215f, HomeAttackerY = 240f;   // 수비 훈련 상대 (왼쪽)
        public const char HomeAttackerFace = 'r';
        public const float HomeMateX = 540f, HomeMateY = 240f;           // 패스 훈련 동료 (오른쪽)
        public const char HomeMateFace = 'l';
        public const float ReactTime = 1.0f;                    // 결과 반응 시간

        static StageKey K(float t, float x, float y, string pose, char face, float z = 0f) { return new StageKey(t, x, y, pose, face, z); }

        // 계속 반복하는 장면 (지금은 리프팅 하나)
        public static Dictionary<string, StageDrill> Build()
        {
            var d = new Dictionary<string, StageDrill>();
            // 리프팅: 선수는 제자리, 공이 발 앞에서 포물선으로 튀어 오르내립니다.
            // 공은 선수보다 앞(y가 큼)에 있어야 몸에 가려지지 않습니다. 오른발·왼발을 번갈아 차는 것처럼 좌우로 조금씩 옮깁니다.
            d["idle"] = new StageDrill
            {
                duration = 1.4f, loop = true,
                // 평소에는 두 발로 서 있고, 공이 발에 닿을 때(0초, 0.7초)만 오른발·왼발을 번갈아 잠깐 듭니다.
                player = new[] {
                    K(0f, SpotX, SpotY, "juggleR", 'd'), K(0.12f, SpotX, SpotY, "stand", 'd'),
                    K(0.64f, SpotX, SpotY, "juggleL", 'd'), K(0.76f, SpotX, SpotY, "stand", 'd'),
                    K(1.34f, SpotX, SpotY, "juggleR", 'd'), K(1.4f, SpotX, SpotY, "juggleR", 'd'),
                },
                ball = JuggleBall(),
            };
            return d;
        }

        // 리프팅 공 궤적: 0.7초에 한 번 든 발에 닿고(높이 14) 가장 높을 때 38(가슴 높이)까지 (그림 픽셀)
        static StageKey[] JuggleBall()
        {
            const float period = 0.7f, low = 14f, high = 38f, step = 0.05f;   // low: 든 발 위에 공이 닿는 높이
            var keys = new List<StageKey>();
            for (int bounce = 0; bounce < 2; bounce++)
            {
                float footX = SpotX + (bounce == 0 ? 9f : -9f);        // 오른발 → 왼발
                float nextX = SpotX + (bounce == 0 ? -9f : 9f);
                for (float u = 0f; u < period - 0.001f; u += step)
                {
                    float k = u / period;
                    float z = low + (high - low) * (1f - (2f * k - 1f) * (2f * k - 1f));   // 포물선
                    keys.Add(K(bounce * period + u, Mathf.Lerp(footX, nextX, k), SpotY + 3f, "auto", '*', z));
                }
            }
            keys.Add(K(2f * period, SpotX + 9f, SpotY + 3f, "auto", '*', low));
            return keys.ToArray();
        }

        // 강화 한 번의 장면: 능력치 동작(action: shoot/pass/dribble/defend/stamina) → 결과 반응(react: cheer/jump/dive)
        // success: 성공했는지 (슈팅은 골인/선방이 갈림)
        public static StageDrill Action(string action, string react, bool success)
        {
            switch (action)
            {
                case "shoot": return Shoot(react, success);
                case "pass": return Pass(react);
                case "dribble": return Dribble(react);
                case "defend": return Defend(react);
                default: return Stamina(react);
            }
        }

        // 반응: 제자리에서 앞을 보고 react 동작
        static List<StageKey> WithReaction(List<StageKey> keys, float start, string react)
        {
            keys.Add(K(start, SpotX, SpotY, react, 'd'));
            keys.Add(K(start + ReactTime, SpotX, SpotY, react, 'd'));
            keys.Add(K(start + ReactTime + 0.05f, SpotX, SpotY, "stand", 'd'));
            return keys;
        }

        static StageDrill Shoot(string react, bool goal)
        {
            const float bx = SpotX + 14f, by = SpotY - 15f;   // 위를 볼 때 발 앞의 공
            var player = WithReaction(new List<StageKey> {
                K(0f, SpotX, SpotY, "stand", 'u'), K(0.2f, SpotX, SpotY, "kick", 'u'), K(0.6f, SpotX, SpotY, "stand", 'u'),
            }, 1.15f, react);
            float end = 1.15f + ReactTime + 0.1f;
            var ball = new List<StageKey> {
                K(0f, bx, by, "auto", '*'), K(0.2f, bx, by, "hide", '*'), K(0.55f, bx, by, "hide", '*'),   // 차는 그림에는 공이 그려져 있음
                K(0.6f, bx, by, "auto", '*'),
            };
            StageKey[] gk;
            if (goal)
            {
                ball.Add(K(0.85f, 368f, 150f, "auto", '*', 24f));
                ball.Add(K(1.1f, 345f, 85f, "auto", '*', 6f));          // 골망 왼쪽
                ball.Add(K(1.9f, 345f, 85f, "auto", '*', 0f));
                ball.Add(K(1.95f, 345f, 85f, "hide", '*'));
                ball.Add(K(end, 345f, 85f, "hide", '*'));
                gk = new[] {                                              // 골키퍼는 반대로 몸을 날림
                    K(0f, HomeGkX, HomeGkY, "stand", 'd'), K(0.8f, HomeGkX, HomeGkY, "stand", 'd'),
                    K(1.1f, HomeGkX + 34f, HomeGkY + 6f, "dive", 'r'), K(1.8f, HomeGkX + 34f, HomeGkY + 6f, "dive", 'r'),
                    K(2.1f, HomeGkX, HomeGkY, "auto", '*'), K(end, HomeGkX, HomeGkY, "stand", 'd'),
                };
            }
            else
            {
                ball.Add(K(0.85f, 380f, 152f, "auto", '*', 22f));
                ball.Add(K(1.05f, HomeGkX, HomeGkY + 2f, "auto", '*', 28f));   // 골키퍼 품으로
                ball.Add(K(1.08f, HomeGkX, HomeGkY + 2f, "hide", '*'));
                ball.Add(K(end, HomeGkX, HomeGkY, "hide", '*'));
                gk = new[] {
                    K(0f, HomeGkX, HomeGkY, "stand", 'd'), K(1.05f, HomeGkX, HomeGkY, "stand", 'd'),
                    K(1.1f, HomeGkX, HomeGkY, "cheer", 'd'), K(1.9f, HomeGkX, HomeGkY, "cheer", 'd'), K(end, HomeGkX, HomeGkY, "stand", 'd'),
                };
            }
            return new StageDrill { duration = end, loop = false, player = player.ToArray(), ball = ball.ToArray(), gk = gk };
        }

        // ---------- 경기 중 내 골 장면 ----------
        // 골 종류마다 다른 장면 (좌표는 훈련장 배경 픽셀, 위쪽이 상대 골문). 공이 골망에 닿는 시각을 goalTime으로 돌려줍니다.
        // 인물: player(나), gk(상대 골키퍼), extras(ally 우리 팀 동료, red 상대 수비). 기본 상대·동료(attacker, mate)는 화면 밖에 둡니다.
        // 골 뒤에는 내가 오른쪽 앞으로 달려 나가 점프하고 환호하고, 동료가 달려와 같이 환호합니다.
        public enum GoalScene { Dribble, Pass, Cross, Corner, FreeKick, Penalty, LongShot }
        public const float MatchViewBottom = 336f;   // 경기 골 장면은 상자 밖 반원(D) 아래까지 보여 줌

        const float SceneEnd = 8f;
        static readonly StageKey[] OffScreen = { new StageKey(0f, -200f, 330f, "stand", 'r', 0f) };

        // iAssist: 내가 도움을 한 장면. 같은 장면에서 패스·크로스·코너킥을 하는 동료(첫 번째 ally)와 나의 동선을 바꿔,
        // 내가 공을 보내고 동료가 넣습니다. 골 뒤에는 넣은 동료가 달려 나가 환호하고 내가 뒤따라가 같이 환호합니다.
        public static StageDrill MatchGoal(GoalScene kind, out float goalTime, bool assisted = false, bool iAssist = false)
        {
            var player = new List<StageKey>();
            var ball = new List<StageKey>();
            var gk = new List<StageKey>();
            var extras = new List<StageExtra>();
            switch (kind)
            {
                case GoalScene.Penalty: goalTime = PenaltyScene(player, ball, gk, extras); break;
                case GoalScene.FreeKick: goalTime = FreeKickScene(player, ball, gk, extras); break;
                case GoalScene.Corner: goalTime = CornerScene(player, ball, gk, extras); break;
                case GoalScene.Cross: goalTime = CrossScene(player, ball, gk, extras); break;
                case GoalScene.Pass: goalTime = PassScene(player, ball, gk, extras); break;
                case GoalScene.LongShot: goalTime = LongShotScene(player, ball, gk, extras, assisted); break;
                default: goalTime = DribbleScene(player, ball, gk, extras); break;
            }
            var me = player.ToArray();
            if (iAssist)
                for (int i = 0; i < extras.Count; i++)
                    if (extras[i].team == "ally") { var passer = extras[i].keys; extras[i] = new StageExtra("ally", me); me = passer; break; }
            return new StageDrill
            {
                duration = SceneEnd, loop = false, player = me, ball = ball.ToArray(), gk = gk.ToArray(),
                attacker = OffScreen, mate = OffScreen, extras = extras.ToArray(),
            };
        }

        // 혼자 드리블: 왼쪽에서 몰고 들어와 앞을 막은 수비를 옆으로 제치고, 미끄러져 들어오는 태클 전에 슈팅
        static float DribbleScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            player.Add(K(0f, 150f, 250f, "carry", '*')); player.Add(K(0.6f, 236f, 226f, "carry", '*'));
            player.Add(K(0.85f, 262f, 216f, "carry", '*')); player.Add(K(1.1f, 302f, 230f, "carry", '*'));
            player.Add(K(1.45f, 350f, 206f, "carry", '*')); player.Add(K(1.5f, 350f, 206f, "carry", 'u'));
            ball.Add(K(0f, 150f, 250f, "hide", '*'));                                   // 드리블 그림 안에 공이 그려져 있음
            float goal = Shoot(player, ball, 1.5f, 350f, 206f, 336f, 78f, 24f);
            Celebrate(player, goal + 0.15f, 350f, 206f);
            extras.Add(new StageExtra("red", new[] {                                   // 앞을 막다가 제쳐지고 뒤쫓음
                K(0f, 330f, 196f, "auto", '*'), K(0.8f, 284f, 212f, "stand", 'l'), K(1.05f, 284f, 212f, "stand", 'r'),
                K(1.2f, 296f, 222f, "auto", '*'), K(1.7f, 328f, 214f, "auto", '*'), K(goal + 0.3f, 328f, 214f, "stand", 'u'), K(SceneEnd, 328f, 214f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] {                                   // 오른쪽에서 미끄러지며 태클, 한발 늦음
                K(0f, 470f, 186f, "auto", '*'), K(1.3f, 394f, 200f, "auto", '*'), K(1.35f, 394f, 200f, "dive", 'l'),
                K(2.3f, 382f, 202f, "dive", 'l'), K(2.9f, 382f, 202f, "stand", 'u'), K(SceneEnd, 382f, 202f, "stand", 'u') }));
            var mate = new List<StageKey> { K(0f, 190f, 236f, "auto", '*'), K(1.6f, 236f, 168f, "auto", '*'), K(goal, 236f, 168f, "stand", 'u') };
            Join(mate, goal + 0.3f, 236f, 168f);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            Keeper(gk, 1.3f, 362f, 1.8f, 'r', 398f);
            return goal;
        }

        // 동료 패스: 오른쪽 동료가 몰고 와 땅볼 패스, 달려 들어간 내가 한 번에 슈팅
        static float PassScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            var mate = new List<StageKey> {
                K(0f, 610f, 252f, "carry", '*'), K(0.7f, 552f, 214f, "carry", '*'), K(0.75f, 552f, 214f, "kick", 'l'), K(1.15f, 552f, 214f, "stand", 'l') };
            ball.Add(K(0f, 610f, 252f, "hide", '*')); ball.Add(K(1.13f, 533f, 202f, "hide", '*'));
            ball.Add(K(1.15f, 533f, 202f, "auto", '*', 1f)); ball.Add(K(1.6f, 366f, 196f, "auto", '*', 0f));   // 내 발 앞으로
            player.Add(K(0f, 262f, 252f, "auto", '*')); player.Add(K(1.5f, 352f, 211f, "auto", '*')); player.Add(K(1.55f, 352f, 211f, "stand", 'u'));
            float goal = Shoot(player, ball, 1.6f, 352f, 211f, 334f, 78f, 22f);
            Celebrate(player, goal + 0.15f, 352f, 211f);
            Join(mate, goal + 0.3f, 552f, 214f);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            extras.Add(new StageExtra("red", new[] {                                   // 패스 길을 막으러 오다 미끄러짐
                K(0f, 300f, 168f, "auto", '*'), K(1.45f, 394f, 204f, "auto", '*'), K(1.5f, 394f, 204f, "dive", 'l'),
                K(2.4f, 382f, 206f, "dive", 'l'), K(3.0f, 382f, 206f, "stand", 'u'), K(SceneEnd, 382f, 206f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] {                                   // 동료를 막는 수비
                K(0f, 640f, 200f, "auto", '*'), K(0.75f, 585f, 212f, "auto", '*'), K(1.2f, 575f, 214f, "stand", 'l'), K(SceneEnd, 575f, 214f, "stand", 'u') }));
            Keeper(gk, 1.6f, 388f, 2.0f, 'r', 410f);
            return goal;
        }

        // 측면 크로스 헤딩: 오른쪽 측면 동료가 골라인 근처까지 몰고 가 띄우고, 달려 들어간 내가 뛰어올라 헤딩
        static float CrossScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            var mate = new List<StageKey> {
                K(0f, 700f, 252f, "carry", '*'), K(0.8f, 676f, 178f, "carry", '*'), K(0.85f, 676f, 178f, "kick", 'l'), K(1.25f, 676f, 178f, "stand", 'l') };
            ball.Add(K(0f, 700f, 252f, "hide", '*')); ball.Add(K(1.23f, 657f, 166f, "hide", '*'));
            ball.Add(K(1.25f, 657f, 166f, "auto", '*', 2f)); ball.Add(K(1.65f, 520f, 152f, "auto", '*', 72f));
            ball.Add(K(2.05f, 380f, 156f, "auto", '*', 108f));                            // 머리 높이
            player.Add(K(0f, 318f, 252f, "auto", '*')); player.Add(K(1.7f, 378f, 154f, "auto", '*'));
            player.Add(K(1.78f, 378f, 154f, "leap", 'u')); player.Add(K(2.35f, 378f, 154f, "stand", 'u'));
            float goal = 2.4f;
            IntoNet(ball, goal, 342f, 80f, 12f);
            Celebrate(player, goal + 0.15f, 378f, 154f);
            Join(mate, goal + 0.3f, 676f, 178f);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            extras.Add(new StageExtra("red", new[] {                                   // 나를 따라오다 같이 뛰지만 늦음
                K(0f, 350f, 236f, "auto", '*'), K(1.7f, 356f, 162f, "auto", '*'), K(1.85f, 356f, 162f, "jump", 'd'),
                K(2.45f, 356f, 162f, "stand", 'u'), K(SceneEnd, 356f, 162f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] {
                K(0f, 432f, 140f, "stand", 'r'), K(1.2f, 442f, 150f, "auto", '*'), K(1.8f, 442f, 150f, "stand", 'r'), K(SceneEnd, 442f, 150f, "stand", 'u') }));
            Keeper(gk, 1.3f, 392f, 2.1f, 'l', 362f);
            return goal;
        }

        // 코너킥 헤딩: 오른쪽 코너에서 동료가 올리고, 상자 안에서 달려 들어간 내가 헤딩
        static float CornerScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            var taker = new List<StageKey> {
                K(0f, 762f, 126f, "stand", 'l'), K(0.5f, 762f, 126f, "auto", '*'), K(0.8f, 746f, 106f, "auto", '*'),
                K(0.82f, 746f, 106f, "kick", 'l'), K(1.22f, 746f, 106f, "stand", 'l') };
            ball.Add(K(0f, 728f, 94f, "auto", '*')); ball.Add(K(0.82f, 728f, 94f, "hide", '*')); ball.Add(K(1.2f, 728f, 94f, "hide", '*'));
            ball.Add(K(1.22f, 727f, 94f, "auto", '*', 4f)); ball.Add(K(1.6f, 560f, 112f, "auto", '*', 96f));
            ball.Add(K(2.0f, 384f, 154f, "auto", '*', 108f));                             // 머리 높이
            player.Add(K(0f, 420f, 212f, "stand", 'u')); player.Add(K(0.95f, 420f, 212f, "auto", '*'));
            player.Add(K(1.72f, 384f, 152f, "auto", '*')); player.Add(K(1.73f, 384f, 152f, "leap", 'u')); player.Add(K(2.3f, 384f, 152f, "stand", 'u'));
            float goal = 2.35f;
            IntoNet(ball, goal, 336f, 80f, 12f);
            Celebrate(player, goal + 0.15f, 384f, 152f);
            Join(taker, goal + 0.3f, 746f, 106f);
            extras.Add(new StageExtra("ally", taker.ToArray()));
            extras.Add(new StageExtra("ally", new[] {                                  // 상자 안의 다른 동료
                K(0f, 330f, 214f, "stand", 'u'), K(1.0f, 330f, 214f, "auto", '*'), K(1.8f, 318f, 172f, "auto", '*'),
                K(goal + 0.3f, 318f, 172f, "cheer", 'd'), K(SceneEnd, 318f, 172f, "cheer", 'd') }));
            extras.Add(new StageExtra("red", new[] {                                   // 나를 막던 수비: 같이 뛰지만 늦음
                K(0f, 400f, 176f, "stand", 'r'), K(1.6f, 394f, 168f, "auto", '*'), K(1.75f, 394f, 168f, "jump", 'd'),
                K(2.4f, 394f, 168f, "stand", 'u'), K(SceneEnd, 394f, 168f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] { K(0f, 344f, 168f, "stand", 'r'), K(SceneEnd, 344f, 168f, "stand", 'r') }));
            extras.Add(new StageExtra("red", new[] { K(0f, 448f, 112f, "stand", 'r'), K(SceneEnd, 448f, 112f, "stand", 'r') }));
            Keeper(gk, 1.5f, 392f, 2.05f, 'l', 360f);
            return goal;
        }

        // 프리킥: 상자 앞에서 수비벽 위로 감아 차 골문 오른쪽 위 구석으로
        static float FreeKickScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            player.Add(K(0f, 322f, 256f, "stand", 'u')); player.Add(K(0.5f, 322f, 256f, "stand", 'u'));
            player.Add(K(0.55f, 322f, 256f, "auto", '*')); player.Add(K(0.95f, 338f, 245f, "auto", '*'));
            player.Add(K(0.95f, 338f, 245f, "kick", 'u')); player.Add(K(1.35f, 338f, 245f, "stand", 'u'));
            ball.Add(K(0f, 352f, 230f, "auto", '*')); ball.Add(K(0.95f, 352f, 230f, "hide", '*')); ball.Add(K(1.33f, 352f, 230f, "hide", '*'));
            ball.Add(K(1.35f, 352f, 230f, "auto", '*', 4f)); ball.Add(K(1.6f, 372f, 180f, "auto", '*', 96f));   // 벽 머리 위로
            float goal = 1.95f;
            IntoNet(ball, goal, 432f, 82f, 30f);
            Celebrate(player, goal + 0.15f, 338f, 245f);
            foreach (float wx in new[] { 350f, 368f, 386f })                            // 수비벽 셋: 공이 올 때 뛰어오름
                extras.Add(new StageExtra("red", new[] {
                    K(0f, wx, 196f, "stand", 'd'), K(1.38f, wx, 196f, "stand", 'd'), K(1.4f, wx, 196f, "jump", 'd'),
                    K(2.0f, wx, 196f, "stand", 'd'), K(2.6f, wx, 196f, "stand", 'u'), K(SceneEnd, wx, 196f, "stand", 'u') }));
            var mate = new List<StageKey> { K(0f, 290f, 230f, "stand", 'u'), K(goal, 290f, 230f, "stand", 'u') };
            Join(mate, goal + 0.3f, 290f, 230f);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            Keeper(gk, 0.2f, 356f, 1.6f, 'r', 392f);
            return goal;
        }

        // 페널티킥: 페널티 지점 뒤에서 달려와 차고, 골키퍼는 반대로 몸을 날림. 상자 밖에는 다른 선수들이 지켜봄
        static float PenaltyScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras)
        {
            const float kickX = 366f, kickY = 214f, kick = 1.05f;
            player.Add(K(0f, 378f, 252f, "stand", 'u')); player.Add(K(0.45f, 378f, 252f, "stand", 'u'));
            player.Add(K(0.5f, 378f, 252f, "auto", '*')); player.Add(K(kick - 0.05f, kickX, kickY, "auto", '*'));
            ball.Add(K(0f, kickX + 14f, kickY - 15f, "auto", '*'));                    // 페널티 지점
            float goal = Shoot(player, ball, kick, kickX, kickY, 414f, 80f, 22f);
            Celebrate(player, goal + 0.15f, kickX, kickY);
            gk.Add(K(0f, HomeGkX, HomeGkY - 4f, "stand", 'd')); gk.Add(K(kick + 0.3f, HomeGkX, HomeGkY - 4f, "stand", 'd'));
            gk.Add(K(kick + 0.35f, 336f, HomeGkY, "dive", 'l')); gk.Add(K(kick + 1.4f, 336f, HomeGkY, "dive", 'l'));
            gk.Add(K(kick + 1.9f, 350f, HomeGkY, "stand", 'd')); gk.Add(K(SceneEnd, 350f, HomeGkY, "stand", 'd'));
            foreach (float x in new[] { 250f, 500f })                                   // 상자 밖에서 지켜보는 상대
                extras.Add(new StageExtra("red", new[] { K(0f, x, 262f, "stand", 'u'), K(SceneEnd, x, 262f, "stand", 'u') }));
            var mate = new List<StageKey> { K(0f, 290f, 262f, "stand", 'u'), K(goal, 290f, 262f, "stand", 'u') };
            Join(mate, goal + 0.3f, 290f, 262f);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            return goal;
        }

        // 중거리 슛: 상자 밖 반원 앞(약 28m)에서 골문 오른쪽 위 구석으로 강하게. 도움이 있으면 오른쪽 동료가 옆으로 내준 공을 바로 때림.
        static float LongShotScene(List<StageKey> player, List<StageKey> ball, List<StageKey> gk, List<StageExtra> extras, bool assisted)
        {
            const float sx = 346f, sy = 324f, kick = 1.2f;                             // 슈팅 자리 (반원 바로 아래)
            List<StageKey> mate = null;
            if (assisted)
            {
                mate = new List<StageKey> {
                    K(0f, 520f, 330f, "carry", '*'), K(0.55f, 470f, 312f, "carry", '*'), K(0.6f, 470f, 312f, "kick", 'l'), K(1.0f, 470f, 312f, "stand", 'l') };
                ball.Add(K(0f, 520f, 330f, "hide", '*')); ball.Add(K(0.98f, 451f, 300f, "hide", '*'));
                ball.Add(K(1.0f, 451f, 300f, "auto", '*', 1f)); ball.Add(K(kick, sx + 14f, sy - 15f, "auto", '*', 0f));   // 옆으로 내줌
                player.Add(K(0f, 300f, 340f, "auto", '*')); player.Add(K(kick - 0.05f, sx, sy, "auto", '*'));
            }
            else
            {
                player.Add(K(0f, 236f, 336f, "carry", '*')); player.Add(K(0.6f, 300f, 330f, "carry", '*'));
                player.Add(K(kick - 0.05f, sx, sy, "carry", '*')); player.Add(K(kick - 0.02f, sx, sy, "carry", 'u'));
                ball.Add(K(0f, 236f, 336f, "hide", '*'));
                mate = new List<StageKey> { K(0f, 470f, 300f, "auto", '*'), K(1.2f, 500f, 250f, "auto", '*'), K(1.6f, 500f, 250f, "stand", 'u') };
            }
            // 차기: 차는 그림 0.4초, 그다음 공이 높게 날아가 크로스바 바로 아래 구석으로 (먼 거리라 조금 오래 날아감)
            float fly = kick + 0.4f;
            player.Add(K(kick, sx, sy, "kick", 'u')); player.Add(K(fly, sx, sy, "stand", 'u'));
            ball.Add(K(kick, sx + 14f, sy - 15f, "hide", '*')); ball.Add(K(fly - 0.02f, sx + 14f, sy - 15f, "hide", '*'));
            ball.Add(K(fly, sx + 14f, sy - 15f, "auto", '*', 4f));
            ball.Add(K(fly + 0.28f, 398f, 196f, "auto", '*', 62f));
            float goal = fly + 0.56f;
            IntoNet(ball, goal, 430f, 84f, 30f);
            Celebrate(player, goal + 0.15f, sx, sy);
            Join(mate, goal + 0.3f, mate[mate.Count - 1].x, mate[mate.Count - 1].y);
            extras.Add(new StageExtra("ally", mate.ToArray()));
            extras.Add(new StageExtra("red", new[] {                                   // 달려 나와 막으려 하지만 늦음
                K(0f, 330f, 250f, "auto", '*'), K(kick - 0.1f, 344f, 292f, "auto", '*'), K(kick, 344f, 292f, "dive", 'r'),
                K(kick + 0.9f, 350f, 294f, "dive", 'r'), K(kick + 1.5f, 350f, 294f, "stand", 'u'), K(SceneEnd, 350f, 294f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] { K(0f, 420f, 200f, "stand", 'd'), K(fly, 420f, 200f, "stand", 'd'), K(fly + 0.3f, 420f, 200f, "stand", 'u'), K(SceneEnd, 420f, 200f, "stand", 'u') }));
            extras.Add(new StageExtra("red", new[] { K(0f, 300f, 196f, "stand", 'd'), K(SceneEnd, 300f, 196f, "stand", 'u') }));
            extras.Add(new StageExtra("ally", new[] { K(0f, 380f, 230f, "stand", 'u'), K(goal + 0.3f, 380f, 230f, "cheer", 'd'), K(SceneEnd, 380f, 230f, "cheer", 'd') }));
            Keeper(gk, 0.9f, 368f, fly + 0.2f, 'r', 404f);
            return goal;
        }

        // 발로 차기: kick 시각부터 차는 그림 0.4초(공이 그림 안에 있음), 그다음 공이 날아가 골망으로. 골 시각을 돌려줍니다.
        static float Shoot(List<StageKey> player, List<StageKey> ball, float kick, float x, float y, float netX, float netY, float peakZ)
        {
            float fly = kick + 0.4f;
            player.Add(K(kick, x, y, "kick", 'u')); player.Add(K(fly, x, y, "stand", 'u'));
            ball.Add(K(kick, x + 14f, y - 15f, "hide", '*'));
            ball.Add(K(fly - 0.02f, x + 14f, y - 15f, "hide", '*'));
            ball.Add(K(fly, x + 14f, y - 15f, "auto", '*', 2f));
            ball.Add(K(fly + 0.18f, (x + netX) / 2f, (y + netY) / 2f, "auto", '*', peakZ));
            float goal = fly + 0.36f;
            IntoNet(ball, goal, netX, netY, 9f);
            return goal;
        }

        // 공이 골망에 닿고(goal 시각) 걸려 떨어짐
        static void IntoNet(List<StageKey> ball, float goal, float netX, float netY, float z)
        {
            ball.Add(K(goal, netX, netY, "auto", '*', z));
            ball.Add(K(goal + 0.5f, netX - 3f, netY + 4f, "auto", '*', 0f));
            ball.Add(K(SceneEnd, netX - 3f, netY + 4f, "auto", '*', 0f));
        }

        // 골 뒤 내 선수: 오른쪽 앞으로 달려 나가 점프, 그리고 환호
        static void Celebrate(List<StageKey> player, float at, float x, float y)
        {
            player.Add(K(at, x, y, "auto", '*'));
            player.Add(K(at + 0.7f, 492f, 236f, "auto", '*'));
            player.Add(K(at + 0.75f, 492f, 236f, "jump", 'd'));
            player.Add(K(at + 1.45f, 492f, 236f, "cheer", 'd'));
            player.Add(K(SceneEnd, 492f, 236f, "cheer", 'd'));
        }

        // 골 뒤 동료: 내 옆으로 달려와 같이 환호
        static void Join(List<StageKey> mate, float at, float x, float y)
        {
            mate.Add(K(at, x, y, "auto", '*'));
            mate.Add(K(at + 0.9f, 532f, 242f, "auto", '*'));
            mate.Add(K(at + 0.95f, 532f, 242f, "cheer", 'd'));
            mate.Add(K(SceneEnd, 532f, 242f, "cheer", 'd'));
        }

        // 골키퍼: move 시각부터 x로 옮겨 서 있다가, dive 시각에 face 쪽으로 몸을 날려 diveX까지 (공은 반대쪽이라 못 막음)
        static void Keeper(List<StageKey> gk, float move, float x, float dive, char face, float diveX)
        {
            gk.Add(K(0f, HomeGkX, HomeGkY, "stand", 'd'));
            gk.Add(K(move, HomeGkX, HomeGkY, "auto", '*'));
            gk.Add(K(Mathf.Min(dive - 0.05f, move + 0.4f), x, HomeGkY, "stand", 'd'));
            gk.Add(K(dive, x, HomeGkY, "dive", face));
            gk.Add(K(dive + 0.8f, diveX, HomeGkY + 4f, "dive", face));
            gk.Add(K(dive + 1.4f, (x + diveX) / 2f, HomeGkY, "stand", 'd'));
            gk.Add(K(SceneEnd, (x + diveX) / 2f, HomeGkY, "stand", 'd'));
        }

        static StageDrill Pass(string react)
        {
            const float px = SpotX + 19f, py = SpotY - 12f;              // 오른쪽을 볼 때 내 발 앞
            const float mx = HomeMateX - 19f, my = HomeMateY - 12f;      // 왼쪽을 보는 동료 발 앞
            var player = WithReaction(new List<StageKey> {
                K(0f, SpotX, SpotY, "stand", 'r'), K(0.2f, SpotX, SpotY, "kick", 'r'), K(0.6f, SpotX, SpotY, "stand", 'r'),
                K(1.95f, SpotX, SpotY, "stand", 'r'),
            }, 2.0f, react);
            float end = 2.0f + ReactTime + 0.1f;
            var ball = new[] {
                K(0f, px, py, "auto", '*'), K(0.2f, px, py, "hide", '*'), K(0.55f, px, py, "hide", '*'),
                K(0.6f, px, py, "auto", '*'), K(0.95f, (px + mx) / 2f, py + 6f, "auto", '*', 10f), K(1.3f, mx, my, "auto", '*'),
                K(1.35f, mx, my, "hide", '*'), K(1.7f, mx, my, "hide", '*'),
                K(1.72f, mx, my, "auto", '*'), K(2.0f, px, py, "auto", '*'), K(2.05f, px, py, "hide", '*'), K(end, px, py, "hide", '*'),
            };
            var mate = new[] {
                K(0f, HomeMateX, HomeMateY, "stand", 'l'), K(1.35f, HomeMateX, HomeMateY, "kick", 'l'),
                K(1.75f, HomeMateX, HomeMateY, "stand", 'l'), K(end, HomeMateX, HomeMateY, "stand", 'l'),
            };
            return new StageDrill { duration = end, loop = false, player = player.ToArray(), ball = ball, mate = mate };
        }

        static StageDrill Dribble(string react)
        {
            // 공을 몰고 왼쪽으로 지그재그 후 제자리로 (드리블 그림에 공이 그려져 있어 따로 그리지 않음)
            var player = WithReaction(new List<StageKey> {
                K(0f, SpotX, SpotY, "carry", '*'), K(0.4f, 335f, SpotY - 18f, "carry", '*'), K(0.8f, 300f, SpotY + 12f, "carry", '*'),
                K(1.2f, 270f, SpotY - 15f, "carry", '*'), K(1.6f, 320f, SpotY + 10f, "carry", '*'), K(2.0f, SpotX, SpotY, "carry", '*'),
            }, 2.05f, react);
            return new StageDrill { duration = 2.05f + ReactTime + 0.1f, loop = false, player = player.ToArray() };
        }

        static StageDrill Defend(string react)
        {
            // 왼쪽에서 상대가 공을 몰고 오면 다가가 태클로 빼앗고, 상대는 넘어졌다가 제자리로
            var player = WithReaction(new List<StageKey> {
                K(0f, SpotX, SpotY, "stand", 'l'), K(0.55f, SpotX, SpotY, "stand", 'l'), K(0.8f, 345f, SpotY, "auto", '*'),
                K(0.85f, 345f, SpotY, "kick", 'l'), K(1.25f, 345f, SpotY, "stand", 'l'), K(1.55f, SpotX, SpotY, "auto", '*'),
            }, 1.6f, react);
            float end = 1.6f + ReactTime + 0.1f;
            var attacker = new[] {
                K(0f, HomeAttackerX, HomeAttackerY, "carry", '*'), K(0.85f, 310f, HomeAttackerY, "carry", '*'),
                K(0.9f, 310f, HomeAttackerY, "dive", 'r'), K(1.7f, 310f, HomeAttackerY, "dive", 'r'),
                K(2.4f, HomeAttackerX, HomeAttackerY, "auto", '*'), K(end, HomeAttackerX, HomeAttackerY, "stand", 'r'),
            };
            return new StageDrill { duration = end, loop = false, player = player.ToArray(), attacker = attacker };
        }

        static StageDrill Stamina(string react)
        {
            // 좌우 왕복 달리기
            var player = WithReaction(new List<StageKey> {
                K(0f, SpotX, SpotY, "auto", '*'), K(0.55f, 470f, SpotY, "stand", 'l'), K(0.65f, 470f, SpotY, "auto", '*'),
                K(1.4f, 280f, SpotY, "stand", 'r'), K(1.5f, 280f, SpotY, "auto", '*'), K(1.95f, SpotX, SpotY, "auto", '*'),
            }, 2.0f, react);
            return new StageDrill { duration = 2.0f + ReactTime + 0.1f, loop = false, player = player.ToArray() };
        }
    }
}
