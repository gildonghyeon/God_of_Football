using UnityEngine;
namespace MyFootballCareer
{
    // 화면과 무관한 훈련 규칙입니다. 오브젝트에 붙이지 않습니다.
    public class TrainingSystem
    {
        readonly SaveData save;
        readonly CareerSettings career;
        readonly TrainingSettings training;

        public TrainingSystem(SaveData save, CareerSettings career, TrainingSettings training)
        {
            this.save = save; this.career = career; this.training = training;
        }

        public float Interval => Mathf.Max(1f, training.rewardIntervalSeconds);
        public float CarrySeconds => save.trainingCarrySeconds;
        public TrainingDefinition Current => training.Find(save.trainingId);

        // 저장된 훈련 ID가 설정에 없으면 훈련을 중지 상태로 되돌립니다.
        public void ValidateSelection()
        {
            if (!string.IsNullOrEmpty(save.trainingId) && training.Find(save.trainingId) == null)
            {
                save.trainingId = "";
                save.trainingCarrySeconds = 0f;
            }
        }

        // 매 프레임 호출. 한 번에 최대 1초까지만 인정해 과도한 성장을 막습니다.
        public void Tick(float deltaSeconds)
        {
            var def = Current;
            if (def == null || save.player == null) return;
            save.trainingCarrySeconds += Mathf.Clamp(deltaSeconds, 0f, 1f);
            while (save.trainingCarrySeconds >= Interval)
            {
                save.trainingCarrySeconds -= Interval;
                Grow(def, Interval);
            }
        }

        // 훈련 변경: 이전 훈련의 남은 시간을 먼저 정산한 뒤 새 훈련을 시작합니다.
        public void Select(string id)
        {
            if (training.Find(id) == null) return;
            Settle();
            save.trainingId = id;
        }

        public void Stop()
        {
            Settle();
            save.trainingId = "";
        }

        void Settle()
        {
            var def = Current;
            if (def != null && save.player != null && save.trainingCarrySeconds > 0f)
                Grow(def, save.trainingCarrySeconds);
            save.trainingCarrySeconds = 0f;
        }

        void Grow(TrainingDefinition def, float seconds)
        {
            float value = save.player.Get(def.targetAttribute) + def.growthPerHour * seconds / 3600f * RebirthSystem.TrainingMultiplier(save);
            save.player.Set(def.targetAttribute, Mathf.Min(career.attributeCap, value));
        }
    }
}
