using System;
using System.Collections.Generic;
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
        public int version = 7;
        public PlayerData player;
        public string trainingId = "";
        public float trainingCarrySeconds;
        public long lastSeenUtcTicks;   // 마지막으로 저장한 UTC 시각 (오프라인 보상 계산용)
        public string teamId = "";      // 소속 팀 ID
        public SeasonData season = new SeasonData();
        public List<SeasonSummary> seasonHistory = new List<SeasonSummary>();
        public List<TransferOffer> offers = new List<TransferOffer>();   // 시즌 종료 후 받은 이적 제안
        public int offersSeason;                                         // 제안을 받은 시즌 번호
        public string pendingTeamId = "";                                // 다음 시즌에 합류하기로 한 팀
        public List<TransferRecord> transfers = new List<TransferRecord>();
        public int rebirthCount;
        public float rebirthBonusPercent;                                   // 영구 보너스: 훈련 성장 속도 증가율(%)
        public List<CareerSummary> careers = new List<CareerSummary>();    // 환생 전에 마친 커리어 기록
    }

    // 경기 한 번의 일정과 결과. 결과는 한 번 저장하면 다시 계산하지 않습니다.
    [Serializable] public class MatchRecord
    {
        public int round;
        public string opponentId, opponentName;
        public bool home;
        public int seed;
        public bool played;
        public int goalsFor, goalsAgainst;
        public int playerGoals, playerAssists, playerShots;
        public int passesAttempted, passesCompleted, defensiveActions;
        public float rating;
        public bool playerPlayed = true;   // false면 벤치에서 경기를 지켜봄
        public List<LeagueGame> others = new List<LeagueGame>();   // 같은 라운드에 열린 다른 팀끼리의 경기
    }

    [Serializable] public class LeagueGame
    {
        public string homeId, awayId;
        public int homeGoals, awayGoals;
    }

    [Serializable] public class SeasonData
    {
        public int seasonNumber = 1;
        public int nextMatchIndex;
        public List<MatchRecord> matches = new List<MatchRecord>();
    }

    [Serializable] public class SeasonSummary
    {
        public int seasonNumber, played, wins, draws, losses;
        public int goalsFor, goalsAgainst, playerGoals, playerAssists;
        public float averageRating;
        public int finalPosition, points;   // 최종 순위와 승점
        public string champion;
        public string teamName;     // 그 시즌에 뛴 팀
        public int appearances;     // 출전 경기 수
    }

    // 다른 팀이 보낸 이적 제안. 계약·연봉 같은 조건은 이후 이 클래스에 추가합니다.
    [Serializable] public class TransferOffer
    {
        public string teamId, teamName;
        public float teamStrength;
        public float startChance;        // 이 팀에서 선발로 뛸 가능성 (0~1)
        public AttributeKind focus;      // 팀이 중요하게 보는 능력치
    }

    [Serializable] public class TransferRecord
    {
        public int afterSeason;
        public string fromTeamName, toTeamName;
    }

    // 환생해도 유지되는 한 커리어의 대표 기록
    [Serializable] public class CareerSummary
    {
        public int careerNumber, seasons, goals, assists;
        public int bestPosition;            // 최고 순위 (0이면 기록 없음)
        public float bonusGainedPercent;    // 이 환생으로 얻은 영구 보너스(%)
        public string teamName;             // 환생 직전 소속 팀
    }
}
