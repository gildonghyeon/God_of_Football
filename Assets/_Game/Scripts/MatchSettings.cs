using System;
using UnityEngine;
namespace MyFootballCareer
{
    [Serializable]
    public class TeamDefinition
    {
        public string id;
        public string displayName;
        [Range(1, 10)] public int division = 1;             // 처음 소속된 부 (1이 가장 높음). 이후에는 승강제로 바뀌어 저장에 기록됩니다.
        [Range(1, 100)] public float strength = 50f;        // 팀 전력
        [Header("이적 시장")]
        [Range(0, 100)] public float requiredRating = 10f;  // 선발로 뛰기 위해 필요한 포지션 평점(0~100)
        public AttributeKind focus = AttributeKind.Shooting; // 팀이 중요하게 보는 능력치
    }

    // 리그의 한 부(1부가 가장 높음). 배열 순서가 곧 부 번호입니다.
    [Serializable]
    public class DivisionDefinition
    {
        public string name;
        [Range(0, 100)] public float recommendedRating = 10f;   // 이 부에서 주전으로 뛰기 알맞은 포지션 평점 (화면 안내용)
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

        [Header("리그")]
        public string startTeamId = "harbor-town";   // 새 선수(그리고 환생한 선수)가 시작하는 팀
        [Min(0)] public int promotionSpots = 2;      // 시즌이 끝나면 각 부의 상위 몇 팀이 올라가고, 하위 몇 팀이 내려가는지
        [Header("주급 (1부부터). 평점이 팀이 요구하는 수준보다 높을수록 더 받습니다 (0.5~2배)")]
        public float[] weeklyWageByDivision = { 25000f, 10000f, 4000f, 1500f, 500f };
        [Header("에이전트 시설: 레벨마다 받는 주급 +%, 시즌 성적 점수 +")]
        [Min(0)] public float agentWagePercent = 5f;
        [Min(0)] public float agentScorePerLevel = 1f;
        public DivisionDefinition[] divisions = new DivisionDefinition[]
        {
            new DivisionDefinition { name = "Division 1", recommendedRating = 85f },
            new DivisionDefinition { name = "Division 2", recommendedRating = 60f },
            new DivisionDefinition { name = "Division 3", recommendedRating = 39f },
            new DivisionDefinition { name = "Division 4", recommendedRating = 23f },
            new DivisionDefinition { name = "Division 5", recommendedRating = 10f },
        };

        [Header("가상 팀 (부마다 강한 팀부터)")]
        public TeamDefinition[] teams = new TeamDefinition[]
        {
            // Division 1
            MakeTeam("crownhaven", "Crownhaven FC", 1, 90f, 94f, AttributeKind.Passing),
            MakeTeam("kingsport-athletic", "Kingsport Athletic", 1, 88f, 92f, AttributeKind.Defending),
            MakeTeam("silverkeep-united", "Silverkeep United", 1, 87f, 90f, AttributeKind.Shooting),
            MakeTeam("goldmoor-city", "Goldmoor City", 1, 85f, 88f, AttributeKind.Dribbling),
            MakeTeam("highspire", "Highspire FC", 1, 84f, 86f, AttributeKind.Stamina),
            MakeTeam("stormwatch-rovers", "Stormwatch Rovers", 1, 82f, 84f, AttributeKind.Passing),
            MakeTeam("starfall-united", "Starfall United", 1, 81f, 82f, AttributeKind.Defending),
            MakeTeam("valecrest-city", "Valecrest City", 1, 79f, 80f, AttributeKind.Shooting),
            MakeTeam("thunderpeak", "Thunderpeak FC", 1, 78f, 78f, AttributeKind.Dribbling),
            MakeTeam("aldercrown-albion", "Aldercrown Albion", 1, 76f, 76f, AttributeKind.Stamina),
            // Division 2
            MakeTeam("northbrook", "Northbrook FC", 2, 76f, 69f, AttributeKind.Dribbling),
            MakeTeam("eastmere-united", "Eastmere United", 2, 75f, 67f, AttributeKind.Stamina),
            MakeTeam("westhollow-city", "Westhollow City", 2, 73f, 65f, AttributeKind.Passing),
            MakeTeam("southvale-rovers", "Southvale Rovers", 2, 72f, 63f, AttributeKind.Defending),
            MakeTeam("oakridge-athletic", "Oakridge Athletic", 2, 70f, 61f, AttributeKind.Shooting),
            MakeTeam("pinecrest", "Pinecrest FC", 2, 69f, 59f, AttributeKind.Dribbling),
            MakeTeam("rivermouth-town", "Rivermouth Town", 2, 67f, 57f, AttributeKind.Stamina),
            MakeTeam("stonegate-united", "Stonegate United", 2, 66f, 55f, AttributeKind.Passing),
            MakeTeam("copperton", "Copperton FC", 2, 64f, 53f, AttributeKind.Defending),
            MakeTeam("brightwater", "Brightwater SC", 2, 63f, 51f, AttributeKind.Shooting),
            // Division 3
            MakeTeam("ashgrove", "Ashgrove FC", 3, 63f, 47f, AttributeKind.Defending),
            MakeTeam("elmstead-town", "Elmstead Town", 3, 62f, 45f, AttributeKind.Shooting),
            MakeTeam("foxden-united", "Foxden United", 3, 60f, 43f, AttributeKind.Dribbling),
            MakeTeam("glenhollow-rovers", "Glenhollow Rovers", 3, 59f, 42f, AttributeKind.Stamina),
            MakeTeam("hollowmere", "Hollowmere FC", 3, 58f, 40f, AttributeKind.Passing),
            MakeTeam("lakeshore-athletic", "Lakeshore Athletic", 3, 56f, 38f, AttributeKind.Defending),
            MakeTeam("millbrook-city", "Millbrook City", 3, 55f, 36f, AttributeKind.Shooting),
            MakeTeam("nettlefield", "Nettlefield FC", 3, 54f, 35f, AttributeKind.Dribbling),
            MakeTeam("quarry-hill-united", "Quarry Hill United", 3, 52f, 33f, AttributeKind.Stamina),
            MakeTeam("rosewood-town", "Rosewood Town", 3, 51f, 31f, AttributeKind.Passing),
            // Division 4
            MakeTeam("saltmarsh", "Saltmarsh FC", 4, 51f, 30f, AttributeKind.Stamina),
            MakeTeam("thistledown-united", "Thistledown United", 4, 50f, 28f, AttributeKind.Passing),
            MakeTeam("underhill-rovers", "Underhill Rovers", 4, 49f, 27f, AttributeKind.Defending),
            MakeTeam("wheatley-green", "Wheatley Green FC", 4, 47f, 25f, AttributeKind.Shooting),
            MakeTeam("yarrow-town", "Yarrow Town", 4, 46f, 24f, AttributeKind.Dribbling),
            MakeTeam("brambleton", "Brambleton FC", 4, 45f, 22f, AttributeKind.Stamina),
            MakeTeam("cinderwick-athletic", "Cinderwick Athletic", 4, 44f, 21f, AttributeKind.Passing),
            MakeTeam("dunmore-rangers", "Dunmore Rangers", 4, 42f, 19f, AttributeKind.Defending),
            MakeTeam("fernvale", "Fernvale FC", 4, 41f, 18f, AttributeKind.Shooting),
            MakeTeam("graniteford-united", "Graniteford United", 4, 40f, 16f, AttributeKind.Dribbling),
            // Division 5
            MakeTeam("red-cedar", "Red Cedar United", 5, 40f, 16f, AttributeKind.Shooting),
            MakeTeam("iron-bay", "Iron Bay SC", 5, 39f, 15f, AttributeKind.Dribbling),
            MakeTeam("blue-ridge", "Blue Ridge FC", 5, 38f, 13f, AttributeKind.Stamina),
            MakeTeam("mossbank-united", "Mossbank United", 5, 37f, 12f, AttributeKind.Passing),
            MakeTeam("maple-hill", "Maple Hill FC", 5, 36f, 11f, AttributeKind.Defending),
            MakeTeam("lantern-bay-rovers", "Lantern Bay Rovers", 5, 34f, 9f, AttributeKind.Shooting),
            MakeTeam("kettle-cove", "Kettle Cove FC", 5, 33f, 8f, AttributeKind.Dribbling),
            MakeTeam("hazelbrook", "Hazelbrook FC", 5, 32f, 7f, AttributeKind.Stamina),
            MakeTeam("sunfield", "Sunfield Rovers", 5, 31f, 5f, AttributeKind.Passing),
            MakeTeam("harbor-town", "Harbor Town FC", 5, 30f, 4f, AttributeKind.Defending),
        };

        public PositionProfile[] profiles = new PositionProfile[]
        {
            MakeProfile(Position.Forward,    0.40f, 0.15f, 0.30f, 0.05f, 0.10f, 3.0f, 25f, 1.0f, 0.20f),
            MakeProfile(Position.Midfielder, 0.15f, 0.35f, 0.20f, 0.15f, 0.15f, 1.5f, 50f, 3.0f, 0.35f),
            MakeProfile(Position.Defender,   0.05f, 0.15f, 0.10f, 0.50f, 0.20f, 0.5f, 40f, 6.0f, 0.12f),
        };

        static TeamDefinition MakeTeam(string id, string name, int division, float strength, float requiredRating, AttributeKind focus)
        {
            return new TeamDefinition { id = id, displayName = name, division = division, strength = strength, requiredRating = requiredRating, focus = focus };
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

        // ---------- 리그 ----------
        public int DivisionCount { get { return divisions != null && divisions.Length > 0 ? divisions.Length : 1; } }

        public int ClampDivision(int division) { return Mathf.Clamp(division, 1, DivisionCount); }

        public string DivisionName(int division)
        {
            int d = ClampDivision(division);
            var def = divisions != null && divisions.Length >= d ? divisions[d - 1] : null;
            return def != null && !string.IsNullOrEmpty(def.name) ? def.name : "Division " + d;
        }

        public float RecommendedRating(int division)
        {
            int d = ClampDivision(division);
            var def = divisions != null && divisions.Length >= d ? divisions[d - 1] : null;
            return def != null ? def.recommendedRating : 0f;
        }

        // 이 팀이 이 평점의 선수에게 주는 주급 (10 단위)
        public int WeeklyWage(TeamDefinition team, int division, float rating)
        {
            int d = ClampDivision(division);
            float baseWage = weeklyWageByDivision != null && weeklyWageByDivision.Length >= d ? weeklyWageByDivision[d - 1] : 500f;
            float factor = Mathf.Clamp(0.6f + (rating - (team != null ? team.requiredRating : 0f)) / 50f, 0.5f, 2f);
            return Mathf.Max(10, Mathf.RoundToInt(baseWage * factor / 10f) * 10);
        }

        // 시작 팀: startTeamId가 없으면 가장 낮은 부의 첫 팀
        public TeamDefinition StartTeam()
        {
            var team = FindTeam(startTeamId);
            if (team != null || teams == null) return team;
            foreach (var t in teams)
                if (t != null && (team == null || ClampDivision(t.division) > ClampDivision(team.division))) team = t;
            return team;
        }
    }
}
