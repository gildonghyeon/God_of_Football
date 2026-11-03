using System;
using UnityEngine;
namespace MyFootballCareer
{
    [Serializable]
    public class TrainingDefinition
    {
        public string id;                         // 저장에 쓰는 고유 ID. 바꾸지 마세요.
        public string displayName;                // 화면에 표시되는 이름
        public AttributeKind targetAttribute;     // 성장시킬 능력치
        [Min(0)] public float growthPerHour = 2f;   // 시간당 성장량. 처음 커리어에서 동시 훈련을 사 가며 약 1주 만에 1부 권장 평점(85)에 닿도록 맞춤
    }

    [CreateAssetMenu(menuName = "Football Career/Training Settings", fileName = "TrainingSettings")]
    public class TrainingSettings : ScriptableObject
    {
        [Min(1)] public float rewardIntervalSeconds = 60f;   // 이 간격마다 성장을 반영 (총 성장량은 같음)
        [Min(0)] public float offlineCapHours = 10f;     // 오프라인 보상으로 인정하는 최대 시간 (잠자는 동안을 덮도록)
        [Min(0)] public float minOfflineSeconds = 30f;   // 이보다 짧은 부재는 보상 없음

        public TrainingDefinition[] trainings = new TrainingDefinition[]
        {
            Make("shooting",  "Shooting",  AttributeKind.Shooting),
            Make("passing",   "Passing",   AttributeKind.Passing),
            Make("dribbling", "Dribbling", AttributeKind.Dribbling),
            Make("defending", "Defending", AttributeKind.Defending),
            Make("stamina",   "Stamina",   AttributeKind.Stamina),
        };

        static TrainingDefinition Make(string id, string name, AttributeKind kind)
        {
            return new TrainingDefinition { id = id, displayName = name, targetAttribute = kind, growthPerHour = 2f };
        }

        [Header("동시 훈련: 레벨마다 가격. Lv1 2번째 칸 50%, Lv2 100%, Lv3 3번째 칸 50%, Lv4 100%")]
        public long[] multiTrainingPrices = { 30000, 150000, 500000, 1500000 };   // 부마다 1~3시즌치 주급

        public TrainingDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id) || trainings == null) return null;
            foreach (var t in trainings)
                if (t != null && t.id == id) return t;
            return null;
        }

        public int MaxMultiLevel { get { return multiTrainingPrices != null ? multiTrainingPrices.Length : 0; } }

        // 다음 레벨 가격. 이미 최고 레벨이면 -1
        public long MultiPrice(int level)
        {
            return multiTrainingPrices != null && level >= 0 && level < multiTrainingPrices.Length ? multiTrainingPrices[level] : -1;
        }

        // 동시 훈련 레벨에서 2번째, 3번째 칸의 속도 배율 (칸이 없으면 목록에 없음)
        public static float[] ExtraSlotSpeeds(int level)
        {
            switch (Mathf.Max(0, level))
            {
                case 0: return new float[0];
                case 1: return new[] { 0.5f };
                case 2: return new[] { 1f };
                case 3: return new[] { 1f, 0.5f };
                default: return new[] { 1f, 1f };
            }
        }
    }
}
