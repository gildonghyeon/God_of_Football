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
        public float Points;          // 받은 노력 포인트
        public float Total;           // 받은 뒤 가진 노력 포인트
    }

    // 앱을 꺼둔 시간(또는 백그라운드에 있던 시간)만큼 노력 포인트를 줍니다.
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
            if (away < Mathf.Max(0f, training.minOfflineSeconds)) return result;

            float cap = Mathf.Max(0f, training.offlineCapHours) * 3600f;
            float credited = Mathf.Min((float)away, cap);
            if (credited <= 0f) return result;

            float before = save.effortPoints;
            TrainingSystem.Earn(save, training, credited);
            result.Granted = true;
            result.Capped = away > cap;
            result.CreditedSeconds = credited;
            result.CapSeconds = cap;
            result.Points = save.effortPoints - before;
            result.Total = save.effortPoints;
            return result;
        }

        public static string Describe(OfflineResult r)
        {
            var sb = new StringBuilder();
            sb.Append("비운 시간: ").Append(FormatDuration(r.AwaySeconds)).Append('\n');
            sb.Append("인정 시간: ").Append(FormatDuration(r.CreditedSeconds));
            if (r.Capped) sb.Append(" (최대 ").Append(FormatDuration(r.CapSeconds)).Append(')');
            sb.Append("\n노력 포인트 +").Append(SeasonSystem.Money((long)r.Points)).Append("  (보유 ").Append(SeasonSystem.Money((long)r.Total)).Append(')');
            sb.Append("\n홈 탭에서 강화해 보세요.");
            return sb.ToString();
        }

        public static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
            if (h > 0) return h + "시간 " + m + "분";
            if (m > 0) return m + "분 " + s + "초";
            return s + "초";
        }
    }
}
