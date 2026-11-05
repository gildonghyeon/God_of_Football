using UnityEngine;
namespace MyFootballCareer
{
    public enum UpgradeOutcome { Success, GreatSuccess, Fail, NotEnough, Max }   // GreatSuccess: +2

    // 강화 한 번의 결과 (결과 창에 보여줄 내용)
    public class UpgradeResult
    {
        public UpgradeOutcome Outcome;
        public AttributeKind Attribute;
        public float Before, After, Chance;   // Chance: 천장 보너스를 더한 이번 확률
        public long Cost;
        public int FailStack;                  // 이번 도전 전의 연속 실패 횟수
        public float NextChance;               // 다음 도전 확률 (실패면 보너스가 한 단계 오른 값)
    }

    // 화면과 무관한 노력 포인트 규칙입니다. 오브젝트에 붙이지 않습니다.
    // 노력 포인트는 매 프레임 흐른 시간만큼 늘어나고(오프라인은 IdleRewardSystem), 강화와 증가량 업그레이드에 씁니다.
    public class TrainingSystem
    {
        readonly SaveData save;
        readonly CareerSettings career;
        readonly TrainingSettings training;
        readonly System.Random rng;

        public TrainingSystem(SaveData save, CareerSettings career, TrainingSettings training, System.Random rng = null)
        {
            this.save = save; this.career = career; this.training = training;
            this.rng = rng ?? new System.Random(unchecked((int)System.DateTime.UtcNow.Ticks));
        }

        public float Points => save.effortPoints;
        public float PointsPerSecond => PerSecond(save, training);
        public int LastUpgraded => save.lastUpgraded;
        public int RateLevel => save.effortRateLevel;
        public float FriendlyWait(long nowTicks) { return FriendlyWait(save, training, nowTicks); }
        public long NextFriendlyReward => FriendlyReward(save, training);

        public float DivisionMultiplier => training.DivisionMultiplier(save.season != null ? save.season.division : 0);

        // 초당 노력 포인트: 기본 × 증가량 업그레이드 × 리그 보너스(뛰는 부) × 훈련장 × 환생 보너스
        public static float PerSecond(SaveData save, TrainingSettings training)
        {
            float division = training.DivisionMultiplier(save.season != null ? save.season.division : 0);
            return training.effortPerSecond * training.RateMultiplier(save.effortRateLevel) * division
                 * FacilitySystem.EffortMultiplier(save, training) * RebirthSystem.TrainingMultiplier(save);
        }

        // ---------- 친선 경기 보상 ----------
        // 다섯 능력치 레벨의 평균 (정수로 버림)
        public static float AverageLevel(PlayerData p)
        {
            if (p == null) return 0f;
            return Mathf.Floor((Mathf.Floor(p.shooting) + Mathf.Floor(p.passing) + Mathf.Floor(p.dribbling)
                              + Mathf.Floor(p.defending) + Mathf.Floor(p.stamina)) / 5f);
        }

        // 친선 경기 보상량: 평균 능력치 레벨에서 강화를 friendlyRewardUpgrades번 할 수 있는 노력 포인트
        public static long FriendlyReward(SaveData save, TrainingSettings training)
        {
            return (long)Mathf.Round(training.friendlyRewardUpgrades * training.UpgradeCost(AverageLevel(save.player)));
        }

        // 다음 친선 경기까지 남은 시간(초). 0이면 바로 할 수 있음
        public static float FriendlyWait(SaveData save, TrainingSettings training, long nowTicks)
        {
            if (save.lastFriendlyTicks <= 0 || training.friendlyCooldownMinutes <= 0f) return 0f;
            double since = (nowTicks - save.lastFriendlyTicks) / (double)System.TimeSpan.TicksPerSecond;
            if (since < 0) return training.friendlyCooldownMinutes * 60f;   // 시계를 되돌렸으면 다시 기다리게 (시계를 돌려 모으지 못하게)
            return Mathf.Max(0f, training.friendlyCooldownMinutes * 60f - (float)since);
        }

        // 친선 경기를 한 번 치렀을 때 부름. friendlyRewardEvery번째 경기면 보상을 주고 그 양을, 아니면 0을 돌려줍니다.
        public static long CountFriendly(SaveData save, TrainingSettings training, long nowTicks)
        {
            save.lastFriendlyTicks = nowTicks;
            save.friendliesPlayed++;
            save.friendlyCount++;
            if (save.friendlyCount < Mathf.Max(1, training.friendlyRewardEvery)) return 0;
            save.friendlyCount = 0;
            long reward = FriendlyReward(save, training);
            save.effortPoints += reward;
            return reward;
        }

        public static void Earn(SaveData save, TrainingSettings training, float seconds)
        {
            save.effortPoints += PerSecond(save, training) * Mathf.Max(0f, seconds);
        }

        // 매 프레임 호출. 한 프레임에 최대 1초까지만 인정해 과도한 적립을 막습니다.
        public void Tick(float deltaSeconds)
        {
            if (save.player == null) return;
            Earn(save, training, Mathf.Clamp(deltaSeconds, 0f, 1f));
        }

        // ---------- 능력치 강화 ----------
        // 강화 비용 (전담 코치 할인 반영, 최소 1)
        public long Cost(AttributeKind kind)
        {
            long baseCost = training.UpgradeCost(save.player.Get(kind));
            return System.Math.Max(1L, (long)Mathf.Round(baseCost * (1f - FacilitySystem.CoachDiscount(save, training))));
        }
        // 원래 성공 확률 (천장 보너스 없이)
        public float BaseChance(AttributeKind kind) { return training.SuccessChance(save.player.Get(kind), career.attributeCap); }

        // 이번 성공 확률 = 원래 확률 × (1 + 연속 실패 × pityStepRatio)
        public float Chance(AttributeKind kind)
        {
            return Mathf.Min(1f, BaseChance(kind) * (1f + training.pityStepRatio * FailStack(kind)));
        }

        public int FailStack(AttributeKind kind)
        {
            int i = (int)kind;
            return save.failStacks != null && i < save.failStacks.Count ? Mathf.Max(0, save.failStacks[i]) : 0;
        }

        void SetFailStack(AttributeKind kind, int value)
        {
            if (save.failStacks == null) save.failStacks = new System.Collections.Generic.List<int>();
            while (save.failStacks.Count <= (int)kind) save.failStacks.Add(0);
            save.failStacks[(int)kind] = value;
        }
        public bool IsMax(AttributeKind kind) { return save.player.Get(kind) >= career.attributeCap; }
        public bool CanAfford(AttributeKind kind) { return !IsMax(kind) && save.effortPoints >= Cost(kind); }

        // 강화에 도전합니다. 비용은 성공·실패와 상관없이 씁니다.
        // 성공 확률 중 doubleShare 몫(기본 절반)은 +2, 나머지는 +1입니다. (예전의 소수점 능력치는 정수로 버린 뒤 더함, 최대치는 넘지 않음)
        public UpgradeResult TryUpgrade(AttributeKind kind)
        {
            var r = new UpgradeResult { Attribute = kind, Before = save.player.Get(kind), Cost = Cost(kind), Chance = Chance(kind), FailStack = FailStack(kind) };
            r.After = r.Before;
            if (IsMax(kind)) { r.Outcome = UpgradeOutcome.Max; return r; }
            if (save.effortPoints < r.Cost) { r.Outcome = UpgradeOutcome.NotEnough; return r; }
            save.effortPoints -= r.Cost;
            save.lastUpgraded = (int)kind;
            double roll = rng.NextDouble();
            if (roll < r.Chance)
            {
                bool great = roll < r.Chance * training.doubleShare;
                r.After = Mathf.Min(career.attributeCap, Mathf.Floor(r.Before) + (great ? 2f : 1f));
                save.player.Set(kind, r.After);
                r.Outcome = great ? UpgradeOutcome.GreatSuccess : UpgradeOutcome.Success;
                SetFailStack(kind, 0);   // 성공하면 천장 초기화
            }
            else
            {
                r.Outcome = UpgradeOutcome.Fail;
                SetFailStack(kind, r.FailStack + 1);
            }
            r.NextChance = IsMax(kind) ? 0f : Chance(kind);
            return r;
        }

        // ---------- 노력 포인트 증가량 ----------
        public long RateCost => training.RateCost(save.effortRateLevel);

        public bool BuyRate()
        {
            long cost = RateCost;
            if (cost < 0 || save.effortPoints < cost) return false;
            save.effortPoints -= cost;
            save.effortRateLevel++;
            return true;
        }
    }
}
