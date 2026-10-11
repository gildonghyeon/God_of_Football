using System;
using UnityEngine;
namespace MyFootballCareer
{
    // 통계 기반 경기 계산. 같은 seed와 같은 능력치라면 같은 결과가 나옵니다.
    public static class MatchSimulation
    {
        // 포지션 가중치로 계산한 선수 평점 (0~100)
        public static float PositionRating(PositionProfile profile, PlayerData p, float cap)
        {
            float total = profile.shooting + profile.passing + profile.dribbling + profile.defending + profile.stamina;
            if (total <= 0f || cap <= 0f) return 0f;
            float sum = profile.shooting * p.shooting + profile.passing * p.passing + profile.dribbling * p.dribbling
                      + profile.defending * p.defending + profile.stamina * p.stamina;
            return Mathf.Clamp(sum / total / cap * 100f, 0f, 100f);
        }

        public static void Play(MatchRecord m, PlayerData p, TeamDefinition own, TeamDefinition opp,
            MatchSettings ms, float attributeCap)
        {
            var rng = new System.Random(m.seed);
            float cap = Mathf.Max(1f, attributeCap);
            var profile = ms.Profile(p.position);

            // 0) 출전 여부: 팀이 요구하는 수준보다 낮으면 벤치에 앉을 수 있습니다.
            float posRating = PositionRating(profile, p, cap);
            bool started = rng.NextDouble() < StartChance(own, posRating);
            m.playerPlayed = started;

            // 1) 팀 득점·실점
            float ownPower = own.strength + (started ? ms.playerImpact * posRating : 0f);
            float oppPower = opp.strength;
            if (m.home) ownPower *= ms.homeAdvantage; else oppPower *= ms.homeAdvantage;
            ownPower = Mathf.Max(1f, ownPower);
            oppPower = Mathf.Max(1f, oppPower);
            float ratio = ownPower / oppPower;
            m.goalsFor = Poisson(rng, Mathf.Clamp(ms.baseGoals * ratio, 0.2f, 4.5f));
            m.goalsAgainst = Poisson(rng, Mathf.Clamp(ms.baseGoals / ratio, 0.2f, 4.5f));

            if (!started)   // 벤치: 팀 결과만 남기고 개인 기록은 없습니다.
            {
                m.playerShots = 0; m.playerGoals = 0; m.playerAssists = 0;
                m.passesAttempted = 0; m.passesCompleted = 0; m.defensiveActions = 0;
                m.rating = 0f;
                m.played = true;
                return;
            }

            // 2) 선수 개인 기록
            float shootingN = Mathf.Clamp01(p.shooting / cap);
            float passingN = Mathf.Clamp01(p.passing / cap);
            float dribblingN = Mathf.Clamp01(p.dribbling / cap);
            float defendingN = Mathf.Clamp01(p.defending / cap);
            float staminaN = Mathf.Clamp01(p.stamina / cap);
            float activity = 0.7f + 0.3f * staminaN;   // 체력이 낮으면 활동량이 줄어듭니다.

            m.playerShots = Poisson(rng, profile.shotsPerMatch * activity * (0.5f + shootingN));
            float goalChance = 0.06f + 0.40f * (0.7f * shootingN + 0.3f * dribblingN);
            int goals = 0;
            for (int i = 0; i < m.playerShots; i++)
                if (rng.NextDouble() < goalChance) goals++;
            m.playerGoals = Mathf.Min(goals, m.goalsFor);   // 선수 득점은 팀 득점을 넘을 수 없습니다.

            m.passesAttempted = Mathf.RoundToInt(profile.passesPerMatch * activity * (0.8f + 0.4f * (float)rng.NextDouble()));
            float passRate = 0.55f + 0.40f * passingN;
            int completed = 0;
            for (int i = 0; i < m.passesAttempted; i++)
                if (rng.NextDouble() < passRate) completed++;
            m.passesCompleted = completed;

            float assistChance = Mathf.Clamp01(profile.assistChance * (0.3f + 1.2f * passingN));
            int assists = 0;
            for (int i = 0; i < m.goalsFor - m.playerGoals; i++)
                if (rng.NextDouble() < assistChance) assists++;
            m.playerAssists = assists;

            m.defensiveActions = Poisson(rng, profile.defensiveActionsPerMatch * activity * (0.4f + 1.2f * defendingN));

            // 3) 평점
            float rating = 6.0f;
            rating += m.goalsFor > m.goalsAgainst ? 0.5f : (m.goalsFor < m.goalsAgainst ? -0.4f : 0f);
            rating += m.playerGoals * 0.9f + m.playerAssists * 0.6f;
            float actualPassRate = m.passesAttempted > 0 ? m.passesCompleted / (float)m.passesAttempted : passRate;
            rating += (actualPassRate - 0.75f) * 2f;
            rating += m.defensiveActions * 0.08f;
            if (p.position == Position.Defender && m.goalsAgainst == 0) rating += 0.4f;
            m.rating = Mathf.Round(Mathf.Clamp(rating, 3f, 10f) * 10f) / 10f;

            m.played = true;
        }

        // 선발로 뛸 가능성. 팀이 요구하는 평점보다 높을수록 커집니다.
        public static float StartChance(TeamDefinition team, float positionRating)
        {
            return Mathf.Clamp(0.75f + (positionRating - team.requiredRating) / 30f, 0.1f, 1f);
        }

        // 내 팀이 아닌 두 팀의 경기를 전력만으로 계산합니다.
        public static void PlayOther(LeagueGame g, TeamDefinition home, TeamDefinition away, MatchSettings ms, System.Random rng)
        {
            float homePower = Mathf.Max(1f, home.strength) * ms.homeAdvantage;
            float awayPower = Mathf.Max(1f, away.strength);
            float ratio = homePower / awayPower;
            g.homeGoals = Poisson(rng, Mathf.Clamp(ms.baseGoals * ratio, 0.2f, 4.5f));
            g.awayGoals = Poisson(rng, Mathf.Clamp(ms.baseGoals / ratio, 0.2f, 4.5f));
        }

        static int Poisson(System.Random rng, float lambda)
        {
            double limit = Math.Exp(-Math.Max(0f, lambda)), product = 1.0;
            int k = 0;
            do { k++; product *= rng.NextDouble(); } while (product > limit && k < 30);
            return k - 1;
        }
    }
}
