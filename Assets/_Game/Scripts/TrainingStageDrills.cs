using System.Collections.Generic;
namespace MyFootballCareer
{
    // 훈련장 동선 데이터입니다. 좌표는 훈련장 배경 그림(752x360)의 픽셀 기준이고 y는 아래로 갈수록 커집니다. 선수 좌표는 발바닥 위치입니다.
    // StageKey(시간, x, y, 동작, 방향, 공 높이): 동작 auto/carry/stand/kick/dive/cheer/hide, 방향 u/d/l/r/* (*는 이동 방향)
    public static class TrainingStageDrills
    {
        public const float HomeGkX = 375f, HomeGkY = 126f;
        public const char HomeGkFace = 'd';
        public const float HomeAttackerX = 560f, HomeAttackerY = 290f;
        public const char HomeAttackerFace = 'd';
        public const float HomeMateX = 460f, HomeMateY = 335f;
        public const char HomeMateFace = 'l';

        public static Dictionary<string, StageDrill> Build()
        {
            var d = new Dictionary<string, StageDrill>();
            d["idle"] = new StageDrill
            {
                duration = 2f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 375f, 318f, "stand", 'd', 0f),
                    new StageKey(2f, 375f, 318f, "stand", 'd', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 395f, 320f, "auto", '*', 0f),
                    new StageKey(2f, 395f, 320f, "auto", '*', 0f)
                },
            };
            d["shoot"] = new StageDrill
            {
                duration = 7.2f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(0.7f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(0.8f, 360f, 218f, "kick", 'u', 0f),
                    new StageKey(1.2f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(1.7f, 360f, 218f, "cheer", 'd', 0f),
                    new StageKey(2.7f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(3.5f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(3.6f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(4.3f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(4.4f, 360f, 218f, "kick", 'u', 0f),
                    new StageKey(4.8f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(5.3f, 360f, 218f, "cheer", 'd', 0f),
                    new StageKey(6.3f, 360f, 218f, "stand", 'u', 0f),
                    new StageKey(7.1f, 360f, 218f, "stand", 'u', 0f)
                },
                gk = new[] {
                    new StageKey(0f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(1.3f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(1.7f, 409f, 132f, "dive", 'r', 0f),
                    new StageKey(2.7f, 409f, 132f, "dive", 'r', 0f),
                    new StageKey(3f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(3.5f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(3.6f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(4.9f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(5.3f, 341f, 132f, "dive", 'l', 0f),
                    new StageKey(6.3f, 341f, 132f, "dive", 'l', 0f),
                    new StageKey(6.6f, 375f, 126f, "stand", 'd', 0f),
                    new StageKey(7.1f, 375f, 126f, "stand", 'd', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(0.75f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(0.8f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(1.15f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(1.2f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(1.45f, 356.5f, 150f, "auto", '*', 24f),
                    new StageKey(1.7f, 337f, 82f, "auto", '*', 4f),
                    new StageKey(2.8f, 337f, 82f, "auto", '*', 0f),
                    new StageKey(2.9f, 337f, 82f, "hide", '*', 0f),
                    new StageKey(3.4f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(3.5f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(3.6f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(4.35f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(4.4f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(4.75f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(4.8f, 374.5f, 203f, "auto", '*', 0f),
                    new StageKey(5.05f, 392.5f, 150f, "auto", '*', 24f),
                    new StageKey(5.3f, 413f, 82f, "auto", '*', 4f),
                    new StageKey(6.4f, 413f, 82f, "auto", '*', 0f),
                    new StageKey(6.5f, 413f, 82f, "hide", '*', 0f),
                    new StageKey(7f, 374.5f, 203f, "hide", '*', 0f),
                    new StageKey(7.1f, 374.5f, 203f, "auto", '*', 0f)
                },
            };
            d["dribble"] = new StageDrill
            {
                duration = 8.1f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 62f, 336f, "carry", '*', 0f),
                    new StageKey(1.2f, 138f, 308f, "carry", '*', 0f),
                    new StageKey(2.4f, 62f, 280f, "carry", '*', 0f),
                    new StageKey(3.6f, 138f, 252f, "carry", '*', 0f),
                    new StageKey(4.8f, 62f, 224f, "carry", '*', 0f),
                    new StageKey(5.5f, 160f, 230f, "carry", '*', 0f),
                    new StageKey(7.1f, 160f, 336f, "carry", '*', 0f),
                    new StageKey(8.1f, 62f, 336f, "carry", '*', 0f)
                },
            };
            d["defend"] = new StageDrill
            {
                duration = 4.6f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 560f, 340f, "stand", 'u', 0f),
                    new StageKey(0.4f, 560f, 340f, "stand", 'u', 0f),
                    new StageKey(2f, 680f, 340f, "auto", '*', 0f),
                    new StageKey(2.4f, 680f, 340f, "stand", 'u', 0f),
                    new StageKey(2.6f, 680f, 340f, "kick", 'u', 0f),
                    new StageKey(3f, 680f, 340f, "stand", 'u', 0f),
                    new StageKey(4.6f, 560f, 340f, "auto", '*', 0f)
                },
                attacker = new[] {
                    new StageKey(0f, 560f, 290f, "carry", 'd', 0f),
                    new StageKey(0.1f, 560f, 290f, "carry", '*', 0f),
                    new StageKey(1.6f, 680f, 290f, "carry", '*', 0f),
                    new StageKey(2.4f, 680f, 290f, "carry", 'd', 0f),
                    new StageKey(2.7f, 680f, 290f, "dive", 'r', 0f),
                    new StageKey(3.8f, 680f, 290f, "dive", 'r', 0f),
                    new StageKey(4f, 680f, 290f, "stand", 'd', 0f),
                    new StageKey(4.6f, 560f, 290f, "auto", '*', 0f)
                },
            };
            d["pass"] = new StageDrill
            {
                duration = 4f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 300f, 335f, "stand", 'r', 0f),
                    new StageKey(0.3f, 300f, 335f, "kick", 'r', 0f),
                    new StageKey(0.7f, 300f, 335f, "stand", 'r', 0f),
                    new StageKey(4f, 300f, 335f, "stand", 'r', 0f)
                },
                mate = new[] {
                    new StageKey(0f, 460f, 335f, "stand", 'l', 0f),
                    new StageKey(2f, 460f, 335f, "kick", 'l', 0f),
                    new StageKey(2.4f, 460f, 335f, "stand", 'l', 0f),
                    new StageKey(4f, 460f, 335f, "stand", 'l', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 319.5f, 320f, "auto", '*', 0f),
                    new StageKey(0.25f, 319.5f, 320f, "auto", '*', 0f),
                    new StageKey(0.3f, 319.5f, 320f, "hide", '*', 0f),
                    new StageKey(0.65f, 319.5f, 320f, "hide", '*', 0f),
                    new StageKey(0.7f, 319.5f, 320f, "auto", '*', 0f),
                    new StageKey(1.1f, 380f, 332f, "auto", '*', 12f),
                    new StageKey(1.5f, 440.5f, 320f, "auto", '*', 0f),
                    new StageKey(1.95f, 440.5f, 320f, "auto", '*', 0f),
                    new StageKey(2f, 440.5f, 320f, "hide", '*', 0f),
                    new StageKey(2.35f, 440.5f, 320f, "hide", '*', 0f),
                    new StageKey(2.4f, 440.5f, 320f, "auto", '*', 0f),
                    new StageKey(2.8f, 380f, 332f, "auto", '*', 12f),
                    new StageKey(3.2f, 319.5f, 320f, "auto", '*', 0f),
                    new StageKey(4f, 319.5f, 320f, "auto", '*', 0f)
                },
            };
            d["stamina"] = new StageDrill
            {
                duration = 10.6f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 60f, 346f, "auto", '*', 0f),
                    new StageKey(5f, 690f, 346f, "stand", 'l', 0f),
                    new StageKey(5.3f, 690f, 346f, "auto", '*', 0f),
                    new StageKey(10.3f, 60f, 346f, "stand", 'r', 0f),
                    new StageKey(10.6f, 60f, 346f, "auto", '*', 0f)
                },
            };
            return d;
        }
    }
}
