using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 화면과 무관한 훈련 규칙입니다. 오브젝트에 붙이지 않습니다.
    // 첫 번째 칸(trainingId)은 항상 100% 속도이고, 동시 훈련 레벨에 따라 2·3번째 칸(extraTrainingIds)이 생깁니다.
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
        public int SlotCount => 1 + TrainingSettings.ExtraSlotSpeeds(save.multiTrainingLevel).Length;

        // 지금 하는 훈련들과 속도 배율 (첫 칸부터)
        public List<KeyValuePair<TrainingDefinition, float>> Active()
        {
            return ActiveTrainings(save, training);
        }

        public static List<KeyValuePair<TrainingDefinition, float>> ActiveTrainings(SaveData save, TrainingSettings training)
        {
            var list = new List<KeyValuePair<TrainingDefinition, float>>();
            var main = training.Find(save.trainingId);
            if (main == null) return list;
            list.Add(new KeyValuePair<TrainingDefinition, float>(main, 1f));
            var speeds = TrainingSettings.ExtraSlotSpeeds(save.multiTrainingLevel);
            for (int i = 0; i < speeds.Length && i < save.extraTrainingIds.Count; i++)
            {
                var def = training.Find(save.extraTrainingIds[i]);
                if (def != null) list.Add(new KeyValuePair<TrainingDefinition, float>(def, speeds[i]));
            }
            return list;
        }

        // 저장된 훈련 ID가 설정에 없거나 칸이 모자라면 정리합니다.
        public void ValidateSelection()
        {
            if (!string.IsNullOrEmpty(save.trainingId) && training.Find(save.trainingId) == null)
            {
                save.trainingId = "";
                save.trainingCarrySeconds = 0f;
            }
            save.extraTrainingIds.RemoveAll(id => training.Find(id) == null || id == save.trainingId);
            int extraSlots = SlotCount - 1;
            if (string.IsNullOrEmpty(save.trainingId) && save.extraTrainingIds.Count > 0)
            {
                save.trainingId = save.extraTrainingIds[0];
                save.extraTrainingIds.RemoveAt(0);
            }
            while (save.extraTrainingIds.Count > extraSlots) save.extraTrainingIds.RemoveAt(save.extraTrainingIds.Count - 1);
        }

        // 매 프레임 호출. 한 번에 최대 1초까지만 인정해 과도한 성장을 막습니다.
        public void Tick(float deltaSeconds)
        {
            if (Current == null || save.player == null) return;
            save.trainingCarrySeconds += Mathf.Clamp(deltaSeconds, 0f, 1f);
            while (save.trainingCarrySeconds >= Interval)
            {
                save.trainingCarrySeconds -= Interval;
                GrowAll(Interval);
            }
        }

        public bool IsActive(string id) { return id == save.trainingId || save.extraTrainingIds.Contains(id); }

        // 훈련 버튼: 하고 있는 훈련이면 빼고, 빈 칸이 있으면 넣고, 칸이 다 찼으면 마지막 칸을 바꿉니다.
        // 바꾸기 전에 지금까지 쌓인 시간을 먼저 정산합니다.
        public void Toggle(string id)
        {
            if (training.Find(id) == null) return;
            Settle();
            if (IsActive(id))
            {
                if (save.trainingId == id)
                {
                    save.trainingId = save.extraTrainingIds.Count > 0 ? save.extraTrainingIds[0] : "";
                    if (save.extraTrainingIds.Count > 0) save.extraTrainingIds.RemoveAt(0);
                }
                else save.extraTrainingIds.Remove(id);
                return;
            }
            int extraSlots = SlotCount - 1;
            if (string.IsNullOrEmpty(save.trainingId)) save.trainingId = id;
            else if (save.extraTrainingIds.Count < extraSlots) save.extraTrainingIds.Add(id);
            else if (extraSlots == 0) save.trainingId = id;
            else save.extraTrainingIds[save.extraTrainingIds.Count - 1] = id;
        }

        public void Stop()
        {
            Settle();
            save.trainingId = "";
            save.extraTrainingIds.Clear();
        }

        void Settle()
        {
            if (Current != null && save.player != null && save.trainingCarrySeconds > 0f)
                GrowAll(save.trainingCarrySeconds);
            save.trainingCarrySeconds = 0f;
        }

        void GrowAll(float seconds)
        {
            foreach (var slot in Active()) Grow(save, career, slot.Key, seconds * slot.Value);
        }

        public static void Grow(SaveData save, CareerSettings career, TrainingDefinition def, float seconds)
        {
            float value = save.player.Get(def.targetAttribute) + def.growthPerHour * seconds / 3600f * RebirthSystem.TrainingMultiplier(save);
            save.player.Set(def.targetAttribute, Mathf.Min(career.attributeCap, value));
        }
    }
}
