using System;
using UnityEngine;
namespace MyFootballCareer
{
    [Serializable]
    public class TeamDefinition
    {
        public string id;
        public string displayName;
        [Range(1, 100)] public float strength = 50f;        // 팀 전력
        [Header("이적 시장")]
        [Range(0, 100)] public float requiredRating = 10f;  // 선발로 뛰기 위해 필요한 포지션 평점(0~100)
        public AttributeKind focus = AttributeKind.Shooting; // 팀이 중요하게 보는 능력치
    }

    // 포지션별로 어떤 능력치를 중요하게 보고, 경기에서 어떤 활동을 얼마나 하는지 정합니다.
    [Serializable]
    public class PositionProfile
    {
        public Position position;
        [Header("포지션 평점 가중치")]
        public float shooting = 0.2f, passing = 0.2f, dribbling = 0.2f, defending = 0.2f, stamina = 0.2f;
        [Header("경기당 기준 활동량")]
        public float shotsPerMatch = 1f;
        public float passesPerMatch = 30f;
        public float defensiveActionsPerMatch = 3f;
        [Range(0, 1)] public float assistChance = 0.2f;
    }

    [CreateAssetMenu(menuName = "Football Career/Match Settings", fileName = "MatchSettings")]
    public class MatchSettings : ScriptableObject
    {
        [Header("경기 계산")]
        public float baseGoals = 1.3f;        // 전력이 같을 때 팀의 평균 득점
        public float playerImpact = 0.3f;     // 선수 평점이 팀 전력에 더해지는 비율
        public float homeAdvantage = 1.1f;    // 홈 팀 전력 배율

        [Header("가상 팀 (첫 번째 팀이 내 시작 팀)")]
        public TeamDefinition[] teams = new TeamDefinition[]
        {
            MakeTeam("harbor-town", "Harbor Town FC",   48f,  8f, AttributeKind.Passing),
            MakeTeam("maple-hill",  "Maple Hill FC",    46f, 12f, AttributeKind.Dribbling),
            MakeTeam("iron-bay",    "Iron Bay SC",      52f, 25f, AttributeKind.Defending),
            MakeTeam("red-cedar",   "Red Cedar United", 55f, 35f, AttributeKind.Shooting),
            MakeTeam("blue-ridge",  "Blue Ridge FC",    50f, 18f, AttributeKind.Stamina),
            MakeTeam("sunfield",    "Sunfield Rovers",  44f, 10f, AttributeKind.Passing),
        };

        public PositionProfile[] profiles = new PositionProfile[]
        {
            MakeProfile(Position.Forward,    0.40f, 0.15f, 0.30f, 0.05f, 0.10f, 3.0f, 25f, 1.0f, 0.20f),
            MakeProfile(Position.Midfielder, 0.15f, 0.35f, 0.20f, 0.15f, 0.15f, 1.5f, 50f, 3.0f, 0.35f),
            MakeProfile(Position.Defender,   0.05f, 0.15f, 0.10f, 0.50f, 0.20f, 0.5f, 40f, 6.0f, 0.12f),
        };

        static TeamDefinition MakeTeam(string id, string name, float strength, float requiredRating, AttributeKind focus)
        {
            return new TeamDefinition { id = id, displayName = name, strength = strength, requiredRating = requiredRating, focus = focus };
        }

        static PositionProfile MakeProfile(Position pos, float sh, float pa, float dr, float de, float st,
            float shots, float passes, float defActions, float assist)
        {
            return new PositionProfile
            {
                position = pos, shooting = sh, passing = pa, dribbling = dr, defending = de, stamina = st,
                shotsPerMatch = shots, passesPerMatch = passes, defensiveActionsPerMatch = defActions, assistChance = assist
            };
        }

        public TeamDefinition FindTeam(string id)
        {
            if (string.IsNullOrEmpty(id) || teams == null) return null;
            foreach (var t in teams)
                if (t != null && t.id == id) return t;
            return null;
        }

        public PositionProfile Profile(Position position)
        {
            if (profiles != null)
                foreach (var p in profiles)
                    if (p != null && p.position == position) return p;
            return new PositionProfile { position = position };
        }
    }
}
