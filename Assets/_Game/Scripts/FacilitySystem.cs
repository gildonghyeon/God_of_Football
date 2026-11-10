using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 돈으로 레벨을 올리는 시설. 환생하면 돈과 함께 처음으로 돌아갑니다.
    //  Training Ground : 노력 포인트 초당 획득량 증가
    //  Personal Coach  : 능력치 강화 비용 할인
    //  Agent           : 받는 주급 증가 + 시즌 성적 점수(이적 제안) 보너스
    public enum FacilityKind { TrainingGround, Coach, Agent }

    // 화면과 무관한 시설 규칙. 가격과 훈련장·코치 효과는 TrainingSettings, 에이전트 효과는 MatchSettings(주급·이적 쪽)에 있습니다.
    public static class FacilitySystem
    {
        public const int Count = 3;

        public static string Name(FacilityKind kind)
        {
            switch (kind)
            {
                case FacilityKind.TrainingGround: return "훈련장";
                case FacilityKind.Coach: return "전담 코치";
                default: return "에이전트";
            }
        }

        public static int Level(SaveData save, FacilityKind kind)
        {
            int i = (int)kind;
            return save != null && save.facilityLevels != null && i < save.facilityLevels.Count ? Mathf.Max(0, save.facilityLevels[i]) : 0;
        }

        static void SetLevel(SaveData save, FacilityKind kind, int level)
        {
            if (save.facilityLevels == null) save.facilityLevels = new List<int>();
            while (save.facilityLevels.Count < Count) save.facilityLevels.Add(0);
            save.facilityLevels[(int)kind] = level;
        }

        // 다음 레벨 가격. 이미 최고 레벨이면 -1
        public static long Cost(SaveData save, FacilityKind kind, TrainingSettings training)
        {
            int level = Level(save, kind);
            if (level >= training.facilityMaxLevel) return -1;
            return (long)Mathf.Round(training.facilityBaseCost * Mathf.Pow(training.facilityCostGrowth, level) / 100f) * 100;   // 100 단위
        }

        public static bool Buy(SaveData save, FacilityKind kind, TrainingSettings training)
        {
            long cost = Cost(save, kind, training);
            if (cost < 0 || save.money < cost) return false;
            save.money -= cost;
            SetLevel(save, kind, Level(save, kind) + 1);
            return true;
        }

        // ---------- 효과 ----------
        public static float EffortMultiplier(SaveData save, TrainingSettings training)
        {
            return 1f + training.trainingGroundPercent / 100f * Level(save, FacilityKind.TrainingGround);
        }

        // 강화 비용에서 빼는 비율 (0~0.9)
        public static float CoachDiscount(SaveData save, TrainingSettings training)
        {
            return Mathf.Clamp(training.coachDiscountPercent / 100f * Level(save, FacilityKind.Coach), 0f, 0.9f);
        }

        public static float WageMultiplier(SaveData save, MatchSettings ms)
        {
            return 1f + ms.agentWagePercent / 100f * Level(save, FacilityKind.Agent);
        }

        // 경기 한 번에 실제로 받는 돈 (계약 주급 × 에이전트 보너스)
        public static int MatchPay(SaveData save, MatchSettings ms)
        {
            return Mathf.RoundToInt(save.weeklyWage * WageMultiplier(save, ms));
        }

        public static float ScoreBonus(SaveData save, MatchSettings ms)
        {
            return ms.agentScorePerLevel * Level(save, FacilityKind.Agent);
        }

        // 화면에 보여 줄 효과 설명 (지금 레벨 기준)
        public static string Effect(SaveData save, FacilityKind kind, TrainingSettings training, MatchSettings ms)
        {
            switch (kind)
            {
                case FacilityKind.TrainingGround:
                    return "노력 포인트 +" + Mathf.RoundToInt((EffortMultiplier(save, training) - 1f) * 100f) + "%  (+" + training.trainingGroundPercent.ToString("0.#") + "%/레벨)";
                case FacilityKind.Coach:
                    return "강화 비용 -" + Mathf.RoundToInt(CoachDiscount(save, training) * 100f) + "%  (-" + training.coachDiscountPercent.ToString("0.#") + "%/레벨)";
                default:
                    return "주급 +" + Mathf.RoundToInt((WageMultiplier(save, ms) - 1f) * 100f) + "%, 시즌 점수 +" + ScoreBonus(save, ms).ToString("0")
                         + "  (+" + ms.agentWagePercent.ToString("0.#") + "% / +" + ms.agentScorePerLevel.ToString("0.#") + "/레벨)";
            }
        }
    }
}
