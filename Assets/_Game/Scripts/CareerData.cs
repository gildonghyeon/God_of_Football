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
        public int hairColor, skinTone, kitColor;   // 외형: PlayerLook의 머리색·피부색·유니폼 번호 (0이 원래 모습)

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
        public int version = 17;
        public PlayerData player;
        public string trainingId = "";      // v11 이전의 훈련 선택 (지금은 쓰지 않음)
        public float trainingCarrySeconds;  // v12의 지급 대기 시간 (지금은 쓰지 않음: 포인트는 1초마다 계속 늘어남)
        public float effortPoints;          // 가진 노력 포인트
        public int effortRateLevel;         // 노력 포인트 증가량 업그레이드 레벨
        public long lastWinBonusTicks;      // v14의 승리 보너스 시각 (지금은 쓰지 않음)
        public int friendlyCount;           // 다음 친선 경기 보상까지 치른 친선 경기 수
        public int friendliesPlayed;        // 지금까지 치른 친선 경기 수
        public long lastFriendlyTicks;      // 마지막 친선 경기의 UTC 시각
        public int lastUpgraded = -1;       // 마지막으로 강화한 능력치 (훈련장 장면용, -1이면 없음)
        public long lastSeenUtcTicks;   // 마지막으로 저장한 UTC 시각 (오프라인 보상 계산용)
        public string teamId = "";      // 소속 팀 ID
        public SeasonData season = new SeasonData();
        public List<SeasonSummary> seasonHistory = new List<SeasonSummary>();
        public List<TransferOffer> offers = new List<TransferOffer>();   // 시즌 종료 후 받은 이적 제안
        public int offersSeason;                                         // 제안을 받은 시즌 번호
        public int offersScore = -1;                                     // 그 시즌의 성적 점수 (0~100, -1이면 없음)
        public string pendingTeamId = "";                                // 다음 시즌에 합류하기로 한 팀
        public bool transferDecided;                                     // 이번 제안에 대한 선택(이적 또는 잔류)을 확정했는지. 확정하면 바꿀 수 없음
        public List<TransferRecord> transfers = new List<TransferRecord>();
        public int rebirthCount;
        public float rebirthBonusPercent;                                   // 영구 보너스: 훈련 성장 속도 증가율(%)
        public List<CareerSummary> careers = new List<CareerSummary>();    // 환생 전에 마친 커리어 기록
        public List<TeamDivision> league = new List<TeamDivision>();       // 지금 각 팀이 속한 부 (승강제로 바뀜)
        public int leagueSeason;                                           // 승강을 마지막으로 반영한 시즌 번호
        public long money;                                                 // 가진 돈
        public int weeklyWage;                                             // 지금 계약의 주급 (경기 한 번에 1주치)
        public List<int> facilityLevels = new List<int>();                 // 시설 레벨 (FacilityKind 순서)
        public List<int> failStacks = new List<int>();                     // 능력치별 연속 실패 횟수 (강화 천장, AttributeKind 순서)
        public int multiTrainingLevel;                                     // v11의 동시 훈련 레벨 (변환할 때 돈으로 돌려줌)
        public List<string> extraTrainingIds = new List<string>();         // v11의 함께 하던 훈련 (지금은 쓰지 않음)
    }

    // 한 팀의 소속 부 (1부가 가장 높음)
    [Serializable] public class TeamDivision
    {
        public string teamId;
        public int division;
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
        public int wage;                   // 이 경기로 받은 주급 (벤치여도 계약대로 받음)
        public List<LeagueGame> others = new List<LeagueGame>();   // 같은 라운드에 열린 다른 팀끼리의 경기
    }

    [Serializable] public class LeagueGame
    {
        public int round;                  // 시즌 일정(fixtures)에서만 씀
        public string homeId, awayId;
        public int homeGoals, awayGoals;
    }

    [Serializable] public class SeasonData
    {
        public int seasonNumber = 1;
        public int nextMatchIndex;
        public List<MatchRecord> matches = new List<MatchRecord>();
        public int division;                                            // 이번 시즌 내 팀의 부 (0이면 부가 생기기 전의 시즌)
        public List<string> teamIds = new List<string>();               // 이번 시즌 같은 부의 팀들
        public List<LeagueGame> fixtures = new List<LeagueGame>();      // 같은 부 전체 일정 (다른 팀끼리 경기의 대진)
        public int seed;                                                // 다른 부 결과를 계산할 때 쓰는 값
        public int nextDivision;                                        // 승강 반영 뒤 내 팀의 부 (0이면 아직)
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
        public int division;        // 그 시즌의 부 (0이면 부가 생기기 전)
    }

    // 다른 팀이 보낸 이적 제안. 계약·연봉 같은 조건은 이후 이 클래스에 추가합니다.
    [Serializable] public class TransferOffer
    {
        public string teamId, teamName;
        public float teamStrength;
        public float startChance;        // 이 팀에서 선발로 뛸 가능성 (0~1)
        public AttributeKind focus;      // 팀이 중요하게 보는 능력치
        public int division;             // 다음 시즌 이 팀의 부
        public int weeklyWage;           // 이 팀이 주는 주급
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
        public int bestPosition;            // 최고 순위 (0이면 기록 없음). bestDivision에서의 순위
        public int bestDivision;            // 가장 높이 올라간 부 (0이면 기록 없음)
        public float bonusGainedPercent;    // 이 환생으로 얻은 영구 보너스(%)
        public string teamName;             // 환생 직전 소속 팀
    }
}
