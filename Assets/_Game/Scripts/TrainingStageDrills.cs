using System.Collections.Generic;
namespace MyFootballCareer
{
    // 훈련장 동선 데이터입니다. (art/gen_stage.py로 생성) 좌표는 배경 이미지의 픽셀 기준이고, y는 아래로 갈수록 커집니다.
    // StageKey(시간, x, y, 동작, 방향, 공 높이): 동작 auto/stand/kick/dive/hide, 방향 u/d/l/r/* (*는 이동 방향)
    public static class TrainingStageDrills
    {
        public const float HomeGkX = 66f, HomeGkY = 17f;
        public const char HomeGkFace = 'd';
        public const float HomeAttackerX = 101f, HomeAttackerY = 30f;
        public const char HomeAttackerFace = 'd';
        public const float HomeMateX = 82f, HomeMateY = 46f;
        public const char HomeMateFace = 'l';

        public static Dictionary<string, StageDrill> Build()
        {
            var d = new Dictionary<string, StageDrill>();
            d["idle"] = new StageDrill
            {
                duration = 2f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 66f, 46f, "stand", 'd', 0f),
                    new StageKey(2f, 66f, 46f, "stand", 'd', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 73f, 47f, "auto", '*', 0f),
                    new StageKey(2f, 73f, 47f, "auto", '*', 0f)
                },
            };
            d["shoot"] = new StageDrill
            {
                duration = 7.2f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(0.7f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(0.8f, 66f, 31f, "kick", 'u', 0f),
                    new StageKey(1.3f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(3.5f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(3.6f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(4.3f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(4.4f, 66f, 31f, "kick", 'u', 0f),
                    new StageKey(4.9f, 66f, 33f, "stand", 'u', 0f),
                    new StageKey(7.1f, 66f, 33f, "stand", 'u', 0f)
                },
                gk = new[] {
                    new StageKey(0f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(1f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(1.3f, 72f, 17f, "dive", 'r', 0f),
                    new StageKey(2.2f, 72f, 17f, "dive", 'r', 0f),
                    new StageKey(2.5f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(3.5f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(3.6f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(4.6f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(4.9f, 60f, 17f, "dive", 'l', 0f),
                    new StageKey(5.8f, 60f, 17f, "dive", 'l', 0f),
                    new StageKey(6.1f, 66f, 17f, "stand", 'd', 0f),
                    new StageKey(7.1f, 66f, 17f, "stand", 'd', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 66f, 27f, "auto", '*', 0f),
                    new StageKey(0.8f, 66f, 27f, "auto", '*', 0f),
                    new StageKey(1.05f, 63f, 19f, "auto", '*', 7f),
                    new StageKey(1.3f, 60f, 9f, "auto", '*', 1f),
                    new StageKey(2.4f, 60f, 9f, "auto", '*', 0f),
                    new StageKey(2.5f, 60f, 9f, "hide", '*', 0f),
                    new StageKey(3.4f, 66f, 27f, "hide", '*', 0f),
                    new StageKey(3.5f, 66f, 27f, "auto", '*', 0f),
                    new StageKey(3.6f, 66f, 27f, "auto", '*', 0f),
                    new StageKey(4.4f, 66f, 27f, "auto", '*', 0f),
                    new StageKey(4.65f, 69f, 19f, "auto", '*', 7f),
                    new StageKey(4.9f, 72f, 9f, "auto", '*', 1f),
                    new StageKey(6f, 72f, 9f, "auto", '*', 0f),
                    new StageKey(6.1f, 72f, 9f, "hide", '*', 0f),
                    new StageKey(7f, 66f, 27f, "hide", '*', 0f),
                    new StageKey(7.1f, 66f, 27f, "auto", '*', 0f)
                },
            };
            d["dribble"] = new StageDrill
            {
                duration = 7.4f, ballFollows = true,
                player = new[] {
                    new StageKey(0f, 14f, 50f, "auto", '*', 0f),
                    new StageKey(1f, 26f, 42f, "auto", '*', 0f),
                    new StageKey(2f, 14f, 34f, "auto", '*', 0f),
                    new StageKey(3f, 26f, 26f, "auto", '*', 0f),
                    new StageKey(4f, 14f, 18f, "auto", '*', 0f),
                    new StageKey(4.6f, 34f, 22f, "auto", '*', 0f),
                    new StageKey(6.2f, 34f, 50f, "auto", '*', 0f),
                    new StageKey(7.4f, 14f, 50f, "auto", '*', 0f)
                },
            };
            d["defend"] = new StageDrill
            {
                duration = 4.2f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 101f, 40f, "stand", 'u', 0f),
                    new StageKey(0.4f, 101f, 40f, "stand", 'u', 0f),
                    new StageKey(2f, 117f, 40f, "auto", '*', 0f),
                    new StageKey(2.4f, 117f, 40f, "stand", 'u', 0f),
                    new StageKey(2.7f, 117f, 36f, "kick", 'u', 0f),
                    new StageKey(3f, 117f, 36f, "stand", 'u', 0f),
                    new StageKey(4.2f, 101f, 40f, "auto", '*', 0f)
                },
                attacker = new[] {
                    new StageKey(0f, 101f, 30f, "stand", 'd', 0f),
                    new StageKey(0.1f, 101f, 30f, "auto", '*', 0f),
                    new StageKey(1.6f, 117f, 30f, "auto", '*', 0f),
                    new StageKey(2.4f, 117f, 30f, "stand", 'd', 0f),
                    new StageKey(3f, 117f, 30f, "stand", 'd', 0f),
                    new StageKey(4.2f, 101f, 30f, "auto", '*', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 104f, 31f, "auto", '*', 0f),
                    new StageKey(1.6f, 120f, 31f, "auto", '*', 0f),
                    new StageKey(2.4f, 120f, 31f, "auto", '*', 0f),
                    new StageKey(2.7f, 122f, 35f, "auto", '*', 3f),
                    new StageKey(3f, 125f, 40f, "auto", '*', 0f),
                    new StageKey(3.2f, 125f, 40f, "hide", '*', 0f),
                    new StageKey(4f, 104f, 31f, "hide", '*', 0f),
                    new StageKey(4.2f, 104f, 31f, "auto", '*', 0f)
                },
            };
            d["pass"] = new StageDrill
            {
                duration = 4f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 52f, 46f, "stand", 'r', 0f),
                    new StageKey(0.3f, 52f, 46f, "stand", 'r', 0f),
                    new StageKey(0.4f, 52f, 46f, "kick", 'r', 0f),
                    new StageKey(0.7f, 52f, 46f, "stand", 'r', 0f),
                    new StageKey(4f, 52f, 46f, "stand", 'r', 0f)
                },
                mate = new[] {
                    new StageKey(0f, 82f, 46f, "stand", 'l', 0f),
                    new StageKey(1.6f, 82f, 46f, "stand", 'l', 0f),
                    new StageKey(1.8f, 82f, 46f, "kick", 'l', 0f),
                    new StageKey(2.1f, 82f, 46f, "stand", 'l', 0f),
                    new StageKey(4f, 82f, 46f, "stand", 'l', 0f)
                },
                ball = new[] {
                    new StageKey(0f, 58f, 46f, "auto", '*', 0f),
                    new StageKey(0.4f, 58f, 46f, "auto", '*', 0f),
                    new StageKey(0.9f, 68f, 46f, "auto", '*', 3f),
                    new StageKey(1.4f, 78f, 46f, "auto", '*', 0f),
                    new StageKey(1.8f, 78f, 46f, "auto", '*', 0f),
                    new StageKey(2.4f, 68f, 46f, "auto", '*', 3f),
                    new StageKey(3.1f, 58f, 46f, "auto", '*', 0f),
                    new StageKey(4f, 58f, 46f, "auto", '*', 0f)
                },
            };
            d["stamina"] = new StageDrill
            {
                duration = 6.6f, ballFollows = false,
                player = new[] {
                    new StageKey(0f, 14f, 53f, "auto", '*', 0f),
                    new StageKey(3f, 114f, 53f, "stand", 'l', 0f),
                    new StageKey(3.3f, 114f, 53f, "auto", '*', 0f),
                    new StageKey(6.3f, 14f, 53f, "stand", 'r', 0f),
                    new StageKey(6.6f, 14f, 53f, "auto", '*', 0f)
                },
            };
            return d;
        }
    }
}
