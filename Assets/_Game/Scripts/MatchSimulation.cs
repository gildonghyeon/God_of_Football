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

        // kind: 포메이션 속 내 자리 역할 (없으면 팀 포메이션과 포지션으로 정함)
        public static void Play(MatchRecord m, PlayerData p, TeamDefinition own, TeamDefinition opp,
            MatchSettings ms, float attributeCap, SlotKind? kind = null)
        {
            var ownT = TacticSystem.Of(own); var oppT = TacticSystem.Of(opp);
            var rng = new System.Random(m.seed);
            float cap = Mathf.Max(1f, attributeCap);
            var profile = ms.Profile(p.position);

            // 0) 출전 여부: 팀이 요구하는 수준보다 낮으면 벤치에 앉을 수 있습니다.
            float posRating = PositionRating(profile, p, cap);
            bool started = rng.NextDouble() < StartChance(own, posRating);
            m.playerPlayed = started;

            // 능력치는 상대 수준과 비교해 얼마나 통하는지(0~1, 0.5가 리그 평균)로 씁니다. 높은 리그일수록 숫자도 조금 커지게 절대값도 섞습니다.
            float level = Mathf.Max(5f, opp.requiredRating);
            float shootingN = Eff(p.shooting, level, cap), passingN = Eff(p.passing, level, cap), dribblingN = Eff(p.dribbling, level, cap);
            float defendingN = Eff(p.defending, level, cap), staminaN = Eff(p.stamina, level, cap);
            float activity = 0.7f + 0.6f * staminaN;   // 체력이 낮으면 활동량이 줄어듭니다. (평균 1배)
            // 포메이션 속 역할과 팀 스타일 (예: 공격형 미드필더는 슈팅·도움 기회가 많고, 수비형은 수비가 많음)
            var role = kind ?? TacticSystem.KindAt(ownT.formation, TacticSystem.YouSlot(ownT.formation, p.position));
            TacticSystem.RoleMultipliers(role, ownT.style, out float shotMul, out float passMul, out float assistMul, out float defMul);

            // 1) 팀 전력: 내 평점이 팀이 요구하는 수준보다 높을수록 팀이 강해집니다.
            float ownPower = own.strength * (started ? 1.13f + ms.playerImpact * 2f * (posRating - own.requiredRating) / 100f : 1f);
            float oppPower = opp.strength;
            if (m.home) ownPower *= ms.homeAdvantage; else oppPower *= ms.homeAdvantage;
            ownPower = Mathf.Max(1f, ownPower);
            oppPower = Mathf.Max(1f, oppPower);
            float ratio = ownPower / oppPower;
            // 전술: 스타일 상성(±6%)과 경기가 얼마나 열리는지(압박·측면은 골이 많고, 내려선 수비는 적음)
            float open = Mathf.Sqrt(TacticSystem.Openness(ownT.style) * TacticSystem.Openness(oppT.style));
            float forL = Mathf.Clamp(ms.baseGoals * ratio * TacticSystem.Matchup(ownT.style, oppT.style) * open, 0.2f, 4.5f);
            float againstL = Mathf.Clamp(ms.baseGoals / ratio * TacticSystem.Matchup(oppT.style, ownT.style) * open, 0.2f, 4.5f);

            if (!started)   // 벤치: 팀 결과만 남기고 개인 기록은 없습니다.
            {
                m.goalsFor = Poisson(rng, forL); m.goalsAgainst = Poisson(rng, againstL);
                m.playerShots = 0; m.playerGoals = 0; m.playerAssists = 0;
                m.passesAttempted = 0; m.passesCompleted = 0; m.defensiveActions = 0;
                m.rating = 0f;
                m.played = true;
                return;
            }

            // 2) 내 슈팅과 골: 슈팅이 좋을수록 많이 차고 잘 넣고, 드리블이 좋을수록 기회를 더 만듭니다.
            float shotBase = profile.shotsPerMatch * shotMul;
            m.playerShots = Poisson(rng, shotBase * activity * (0.45f + 0.75f * shootingN + 0.35f * dribblingN));
            float goalChance = 0.03f + 0.18f * shootingN + 0.06f * dribblingN;
            int goals = 0;
            for (int i = 0; i < m.playerShots; i++)
                if (rng.NextDouble() < goalChance) goals++;
            m.playerGoals = goals;

            // 3) 팀 골 = 내 골 + 나머지 선수들의 골. 리그 평균 선수가 넣을 만큼을 빼고, 내 패스가 좋을수록 동료의 기회가 늘어납니다.
            float averageMine = shotBase * 1f * 0.15f;
            float restL = Mathf.Max(0.15f, forL - averageMine) * (1f + 0.25f * (passingN - 0.5f) * profile.passing / 0.35f);
            m.goalsFor = goals + Poisson(rng, restL);
            // 실점: 수비가 좋을수록(수비수일수록 크게), 체력이 좋을수록(끝까지 버팀) 줄어듭니다.
            againstL *= 1f - 0.3f * (defendingN - 0.5f) * profile.defending / 0.5f;
            againstL *= 1f - 0.08f * (staminaN - 0.5f);
            m.goalsAgainst = Poisson(rng, Mathf.Clamp(againstL, 0.15f, 4.5f));

            // 4) 패스·도움·수비
            m.passesAttempted = Mathf.RoundToInt(profile.passesPerMatch * activity * (0.8f + 0.4f * (float)rng.NextDouble()) * passMul);
            float passRate = 0.60f + 0.35f * passingN;
            int completed = 0;
            for (int i = 0; i < m.passesAttempted; i++)
                if (rng.NextDouble() < passRate) completed++;
            m.passesCompleted = completed;

            float assistChance = Mathf.Clamp01(profile.assistChance * (0.3f + 1.4f * passingN) * assistMul);
            int assists = 0;
            for (int i = 0; i < m.goalsFor - m.playerGoals; i++)
                if (rng.NextDouble() < assistChance) assists++;
            m.playerAssists = assists;

            m.defensiveActions = Poisson(rng, profile.defensiveActionsPerMatch * activity * (0.4f + 1.2f * defendingN) * defMul);

            // 5) 평점
            float rating = 6.0f;
            rating += m.goalsFor > m.goalsAgainst ? 0.5f : (m.goalsFor < m.goalsAgainst ? -0.4f : 0f);
            rating += m.playerGoals * 0.9f + m.playerAssists * 0.6f;
            float actualPassRate = m.passesAttempted > 0 ? m.passesCompleted / (float)m.passesAttempted : passRate;
            rating += (actualPassRate - 0.78f) * 2f;
            rating += m.defensiveActions * 0.08f;
            if (p.position == Position.Defender && m.goalsAgainst == 0) rating += 0.5f;   // 무실점
            else if (p.position == Position.Defender) rating -= 0.15f * Mathf.Max(0, m.goalsAgainst - 1);
            m.rating = Mathf.Round(Mathf.Clamp(rating, 3f, 10f) * 10f) / 10f;

            m.played = true;
        }

        // 능력치가 상대에게 얼마나 통하는지 (0~1). 상대 수준과 같으면 0.5, 25 높으면 1. 절대값을 1/4 섞습니다.
        static float Eff(float stat, float level, float cap)
        {
            float rel = Mathf.Clamp01(0.5f + (stat - level) / 50f);
            return 0.75f * rel + 0.25f * Mathf.Clamp01(stat / cap);
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
            var ht = TacticSystem.Of(home); var at = TacticSystem.Of(away);
            float open = Mathf.Sqrt(TacticSystem.Openness(ht.style) * TacticSystem.Openness(at.style));
            g.homeGoals = Poisson(rng, Mathf.Clamp(ms.baseGoals * ratio * TacticSystem.Matchup(ht.style, at.style) * open, 0.2f, 4.5f));
            g.awayGoals = Poisson(rng, Mathf.Clamp(ms.baseGoals / ratio * TacticSystem.Matchup(at.style, ht.style) * open, 0.2f, 4.5f));
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
