using UnityEngine;
namespace MyFootballCareer
{
    // 노력 포인트 규칙. 노력 포인트는 1초마다 계속 늘어나고(오프라인 포함), 그 포인트로 능력치 강화에 도전합니다.
    // 강화는 확률로 성공·실패하며, 능력치가 높을수록 비용은 늘고 성공 확률은 줄어듭니다.
    // 노력 포인트로 '노력 포인트 증가량'도 올릴 수 있습니다.
    // 기본값은 처음 커리어에서 약 1주 만에 1부 권장 평점(85)에 닿도록 시뮬레이션으로 맞춘 값입니다.
    [CreateAssetMenu(menuName = "Football Career/Training Settings", fileName = "TrainingSettings")]
    public class TrainingSettings : ScriptableObject
    {
        [Min(0)] public float offlineCapHours = 10f;         // 오프라인 보상으로 인정하는 최대 시간 (잠자는 동안을 덮도록)
        [Min(0)] public float minOfflineSeconds = 30f;       // 이보다 짧은 부재는 보상 없음

        [Header("노력 포인트")]
        [Min(0)] public float effortPerSecond = 1f;          // 기본 초당 노력 포인트

        [Header("능력치 강화 (레벨 10 → 100)")]
        [Min(0)] public float upgradeBaseCost = 110f;        // 레벨 10에서 한 번 도전하는 비용 (기본 속도로 110초)
        // 비용 = 기본 비용 × (1 + 오른 레벨 / upgradeCostSpan)^upgradeCostPower
        // 레벨이 오를수록 비용은 계속 늘지만, 레벨당 오르는 비율은 점점 줄어듭니다 (Lv10 약 12% → Lv99 약 3%)
        // Lv30 약 700, Lv50 약 2,400, Lv70 약 6,080, Lv90 약 12,850, Lv99 약 17,240
        [Min(1)] public float upgradeCostSpan = 32f;
        [Min(0)] public float upgradeCostPower = 3.8f;
        [Range(0, 1)] public float minSuccessChance = 0.01f; // 99 → 100 강화의 성공 확률 (10 → 11은 100%, 그 사이는 고르게 줄어듦)
        [Range(0, 1)] public float pityStepRatio = 0.01f;   // 강화 천장: 실패할 때마다 원래 확률의 이 비율만큼 확률이 오름 (성공하면 초기화, 능력치별)
        [Range(0, 1)] public float doubleShare = 0.5f;      // 성공 확률 중 +2가 되는 몫 (0.5면 성공 확률의 절반 확률로 +2)
        [Min(0)] public float baseLevel = 10f;               // 비용·확률 계산의 시작 레벨

        [Header("노력 포인트 증가량 업그레이드")]
        [Min(0)] public float rateStepPercent = 15f;         // 레벨마다 늘어나는 증가량 (%)
        [Min(0)] public float rateBaseCost = 900f;           // Lv0 → Lv1 비용
        [Min(1)] public float rateCostGrowth = 1.32f;        // 레벨이 오를 때마다 비용 배율
        [Min(0)] public int rateMaxLevel = 30;

        [Header("리그 보너스: 뛰는 부가 높을수록 노력 포인트 배율 (1부부터)")]
        public float[] divisionMultipliers = { 6f, 3.5f, 2.2f, 1.5f, 1f };   // 레벨이 높을수록 비싸지는 강화 비용에 맞춰 벌어짐 (강화 한 번에 걸리는 시간이 완만하게 늘도록)

        [Header("친선 경기: 노력 포인트 보상 (승패와 상관없음, 하루 횟수 제한 없음)")]
        [Min(1)] public int friendlyRewardEvery = 1;             // 이 경기 수마다 보상 (1이면 매 경기)
        [Min(0)] public float friendlyRewardUpgrades = 2f;       // 보상 = 평균 능력치 레벨에서 강화 몇 번 할 수 있는 양인지
        [Min(0)] public float friendlyCooldownMinutes = 5f;      // 친선 경기 사이 대기 시간 (경기는 바로 끝나므로 연달아 모으지 못하게). 0이면 제한 없음

        [Header("시설: 돈으로 레벨업 (환생하면 초기화). 에이전트 효과는 MatchSettings")]
        [Min(0)] public int facilityMaxLevel = 10;
        [Min(0)] public float facilityBaseCost = 3000f;        // Lv0 → Lv1 가격
        [Min(1)] public float facilityCostGrowth = 2.2f;       // 레벨이 오를 때마다 가격 배율 (3,000 → Lv9→10 약 362만)
        [Min(0)] public float trainingGroundPercent = 10f;     // 훈련장: 레벨마다 노력 포인트 +%
        [Min(0)] public float coachDiscountPercent = 3f;       // 전담 코치: 레벨마다 강화 비용 -%

        // 이 부에서 뛸 때의 노력 포인트 배율 (부가 없던 예전 시즌은 1)
        public float DivisionMultiplier(int division)
        {
            if (division <= 0 || divisionMultipliers == null || divisionMultipliers.Length == 0) return 1f;
            return divisionMultipliers[Mathf.Clamp(division, 1, divisionMultipliers.Length) - 1];
        }

        // 지금 능력치에서 강화에 한 번 도전하는 비용 (정수)
        public long UpgradeCost(float value)
        {
            float level = Mathf.Max(baseLevel, Mathf.Floor(value));
            return (long)Mathf.Round(upgradeBaseCost * Mathf.Pow(1f + (level - baseLevel) / Mathf.Max(1f, upgradeCostSpan), upgradeCostPower));
        }

        // 지금 능력치에서 강화(+1)에 성공할 확률 (0~1). baseLevel → +1이 100%, (최대치 − 1) → 최대치가 minSuccessChance
        public float SuccessChance(float value, float cap)
        {
            float span = Mathf.Max(1f, cap - 1f - baseLevel);
            float t = Mathf.Clamp01((Mathf.Floor(value) - baseLevel) / span);
            return 1f - (1f - minSuccessChance) * t;
        }

        public float RateMultiplier(int level) { return 1f + rateStepPercent / 100f * Mathf.Clamp(level, 0, rateMaxLevel); }

        // 다음 증가량 레벨 비용. 이미 최고 레벨이면 -1
        public long RateCost(int level)
        {
            if (level >= rateMaxLevel) return -1;
            return (long)Mathf.Round(rateBaseCost * Mathf.Pow(rateCostGrowth, Mathf.Max(0, level)));
        }
    }
}
