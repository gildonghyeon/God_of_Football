using System;
namespace MyFootballCareer
{
    public enum Position { Forward, Midfielder, Defender }
    public enum Trait { Finisher, Playmaker, BallWinner }
    [Serializable] public class PlayerData
    {
        public string playerName;
        public Position position;
        public Trait trait;
        public float shooting, passing, dribbling, defending, stamina;
    }
    [Serializable] public class SaveData
    {
        public int version = 1;
        public PlayerData player;
    }
}
