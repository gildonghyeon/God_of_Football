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
        [Min(0)] public float growthPerHour = 60f; // 시간당 성장량 (테스트용으로 크게 설정)
    }

    [CreateAssetMenu(menuName = "Football Career/Training Settings", fileName = "TrainingSettings")]
    public class TrainingSettings : ScriptableObject
    {
        [Min(1)] public float rewardIntervalSeconds = 5f;
        [Min(0)] public float offlineCapHours = 8f;      // 오프라인 보상으로 인정하는 최대 시간
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
            return new TrainingDefinition { id = id, displayName = name, targetAttribute = kind, growthPerHour = 60f };
        }

        public TrainingDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id) || trainings == null) return null;
            foreach (var t in trainings)
                if (t != null && t.id == id) return t;
            return null;
        }
    }
}
