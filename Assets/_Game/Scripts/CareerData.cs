using System;
namespace MyFootballCareer
{
    public enum Position { Forward, Midfielder, Defender }
    public enum Trait { Finisher, Playmaker, BallWinner }
    public enum AttributeKind { Shooting, Passing, Dribbling, Defending, Stamina }

    [Serializable] public class PlayerData
    {
        public string playerName;
        public Position position;
        public Trait trait;
        public float shooting, passing, dribbling, defending, stamina;

        public float Get(AttributeKind kind)
        {
            switch (kind)
            {
                case AttributeKind.Shooting: return shooting;
                case AttributeKind.Passing: return passing;
                case AttributeKind.Dribbling: return dribbling;
                case AttributeKind.Defending: return defending;
                default: return stamina;
            }
        }

        public void Set(AttributeKind kind, float value)
        {
            switch (kind)
            {
                case AttributeKind.Shooting: shooting = value; break;
                case AttributeKind.Passing: passing = value; break;
                case AttributeKind.Dribbling: dribbling = value; break;
                case AttributeKind.Defending: defending = value; break;
                default: stamina = value; break;
            }
        }
    }

    [Serializable] public class SaveData
    {
        public int version = 3;
        public PlayerData player;
        public string trainingId = "";
        public float trainingCarrySeconds;
        public long lastSeenUtcTicks;   // 마지막으로 저장한 UTC 시각 (오프라인 보상 계산용)
    }
}
