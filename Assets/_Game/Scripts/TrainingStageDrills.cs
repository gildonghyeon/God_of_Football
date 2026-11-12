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
