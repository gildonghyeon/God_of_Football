using System;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    public class OfflineResult
    {
        public bool Granted;          // 보상이 실제로 지급되었는가
        public bool ClockRewound;     // 기기 시계가 마지막 저장보다 과거인가
        public bool Capped;           // 시간 상한에 걸렸는가
        public float AwaySeconds;     // 실제로 자리를 비운 시간
        public float CreditedSeconds; // 보상으로 인정한 시간
        public float CapSeconds;      // 인정 시간 상한
        public string TrainingName;
        public AttributeKind Attribute;
        public float Before, After;
        public float Gain => After - Before;
    }

    // 앱을 꺼둔 시간(또는 백그라운드에 있던 시간)만큼 현재 훈련 성장을 반영합니다.
    public static class IdleRewardSystem
    {
        public static OfflineResult Apply(SaveData save, CareerSettings career, TrainingSettings training, long nowTicks)
        {
            var result = new OfflineResult();
            if (save == null || save.player == null) return result;

            long last = save.lastSeenUtcTicks;
            save.lastSeenUtcTicks = nowTicks;   // 기준 시각을 먼저 갱신해 같은 시간이 두 번 지급되지 않게 합니다.
            if (last <= 0) return result;

            double away = (nowTicks - last) / (double)TimeSpan.TicksPerSecond;
            if (away < 0) { result.ClockRewound = true; return result; }
            result.AwaySeconds = (float)away;

            var def = training.Find(save.trainingId);
            if (def == null) return result;
            if (away < Mathf.Max(0f, training.minOfflineSeconds)) return result;

            float cap = Mathf.Max(0f, training.offlineCapHours) * 3600f;
            float credited = Mathf.Min((float)away, cap);
            if (credited <= 0f) return result;

            float before = save.player.Get(def.targetAttribute);
            float after = Mathf.Min(career.attributeCap, before + def.growthPerHour * credited / 3600f * RebirthSystem.TrainingMultiplier(save));
            save.player.Set(def.targetAttribute, after);

            result.Granted = true;
            result.Capped = away > cap;
            result.CreditedSeconds = credited;
            result.CapSeconds = cap;
            result.TrainingName = def.displayName;
            result.Attribute = def.targetAttribute;
            result.Before = before;
            result.After = after;
            return result;
        }

        public static string Describe(OfflineResult r)
        {
            var sb = new StringBuilder();
            sb.Append("Away: ").Append(FormatDuration(r.AwaySeconds)).Append('\n');
            sb.Append("Counted: ").Append(FormatDuration(r.CreditedSeconds));
            if (r.Capped) sb.Append(" (cap ").Append(FormatDuration(r.CapSeconds)).Append(')');
            sb.Append('\n').Append("Training: ").Append(r.TrainingName).Append('\n');
            if (r.Gain > 0.0001f)
                sb.Append(r.Attribute).Append(": ").Append(r.Before.ToString("0.00")).Append(" -> ")
                  .Append(r.After.ToString("0.00")).Append("  (+").Append(r.Gain.ToString("0.00")).Append(')');
            else
                sb.Append(r.Attribute).Append(" is already at the maximum.");
            return sb.ToString();
        }

        public static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m " + s + "s";
            return s + "s";
        }
    }
}
