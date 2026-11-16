using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public enum MissionKind { Upgrade, UpgradeSuccess, Friendly, LeagueMatch, LeagueWin, AttackPoint, Purchase }

    // 오늘의 미션 하나
    [Serializable] public class DailyMission
    {
        public MissionKind kind;
        public int target, progress;
        public bool claimed;
        public bool Done => progress >= target;
    }

    // 일일 미션과 출석 보상. 날짜는 기기의 현지 날짜(자정에 바뀜)를 씁니다.
    //  일일 미션: 매일 3개 (강화 / 경기 / 기타 묶음에서 하나씩). 하나 끝낼 때마다 보상, 셋 다 받으면 보너스.
    //  출석: 하루 한 번 받을 수 있고 7일마다 처음으로 돌아갑니다. 빠진 날이 있어도 이어서 셉니다(초기화하지 않음).
    //  보상은 지금 수준에 맞춰 커집니다: 노력 포인트 = 평균 능력치 레벨의 강화 n회분, 돈 = 경기 수당 n주치.
    //  기기 시계를 되돌리면 하루가 바뀌지 않고, 출석도 받을 수 없습니다.
    public static class DailySystem
    {
        public const int MissionUpgrades = 3, MissionWeeks = 2;     // 미션 하나 보상
        public const int BonusUpgrades = 5, BonusWeeks = 5;         // 셋 다 받았을 때 보너스
        static readonly int[] AttendUpgrades = { 2, 3, 3, 4, 4, 5, 10 };
        static readonly int[] AttendWeeks = { 2, 2, 3, 3, 4, 4, 10 };

        // 미션 묶음: 날마다 묶음마다 하나씩 뽑습니다. (종류, 목표 후보)
        static readonly (MissionKind kind, int[] targets)[][] Groups =
        {
            new[] { (MissionKind.Upgrade, new[] { 10, 15, 20 }), (MissionKind.UpgradeSuccess, new[] { 5, 8 }) },
            new[] { (MissionKind.Friendly, new[] { 3, 5 }), (MissionKind.LeagueMatch, new[] { 3, 6 }), (MissionKind.LeagueWin, new[] { 2, 3 }) },
            new[] { (MissionKind.AttackPoint, new[] { 2, 4 }), (MissionKind.Purchase, new[] { 1, 2 }) },
        };

        // ---------- 날짜 ----------
        public static string Today(long utcTicks) { return new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyyMMdd"); }

        // 다음 자정까지 남은 시간(초)
        public static float SecondsToReset(long utcTicks)
        {
            var local = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
            return (float)(local.Date.AddDays(1) - local).TotalSeconds;
        }

        // 오늘 미션이 없으면 새로 뽑습니다. 시계를 되돌려 날짜가 과거면 그대로 둡니다. 새로 뽑았으면 true.
        public static bool Ensure(SaveData save, long utcTicks)
        {
            string today = Today(utcTicks);
            if (save.dailyMissions == null) save.dailyMissions = new List<DailyMission>();
            if (save.dailyDate == today && save.dailyMissions.Count > 0) return false;
            if (!string.IsNullOrEmpty(save.dailyDate) && string.CompareOrdinal(today, save.dailyDate) < 0) return false;
            save.dailyDate = today;
            save.dailyBonusClaimed = false;
            save.dailyMissions.Clear();
            var rng = new System.Random(int.Parse(today) * 31 + save.rebirthCount);
            foreach (var group in Groups)
            {
                var pick = group[rng.Next(group.Length)];
                save.dailyMissions.Add(new DailyMission { kind = pick.kind, target = pick.targets[rng.Next(pick.targets.Length)] });
            }
            return true;
        }

        // 행동을 했을 때 부름 (오늘 미션 중 같은 종류의 진행도를 올림)
        public static void Count(SaveData save, MissionKind kind, int amount, long utcTicks)
        {
            if (amount <= 0) return;
            Ensure(save, utcTicks);
            foreach (var m in save.dailyMissions)
                if (m.kind == kind && !m.claimed) m.progress = Mathf.Min(m.target, m.progress + amount);
        }

        public static string Describe(DailyMission m)
        {
            switch (m.kind)
            {
                case MissionKind.Upgrade: return "강화 " + m.target + "회 도전";
                case MissionKind.UpgradeSuccess: return "강화 " + m.target + "회 성공";
                case MissionKind.Friendly: return "친선 경기 " + m.target + "판";
                case MissionKind.LeagueMatch: return "리그 경기 " + m.target + "경기";
                case MissionKind.LeagueWin: return "리그 경기 " + m.target + "승";
                case MissionKind.AttackPoint: return "골·도움 " + m.target + "개 (리그·친선)";
                default: return "시설·노력 증가량 업그레이드 " + m.target + "회";
            }
        }

        // ---------- 보상 ----------
        public static long Points(SaveData save, TrainingSettings training, int upgrades)
        {
            return (long)Mathf.Round(upgrades * training.UpgradeCost(TrainingSystem.AverageLevel(save.player)));
        }

        public static long Money(SaveData save, MatchSettings ms, int weeks)
        {
            return (long)weeks * Mathf.Max(0, FacilitySystem.MatchPay(save, ms));
        }

        public static string RewardText(SaveData save, TrainingSettings training, MatchSettings ms, int upgrades, int weeks)
        {
            return "노력 포인트 +" + SeasonSystem.Money(Points(save, training, upgrades)) + "  돈 +" + SeasonSystem.Money(Money(save, ms, weeks));
        }

        static string Give(SaveData save, TrainingSettings training, MatchSettings ms, int upgrades, int weeks)
        {
            string text = RewardText(save, training, ms, upgrades, weeks);
            save.effortPoints += Points(save, training, upgrades);
            save.money += Money(save, ms, weeks);
            return text;
        }

        // 미션 보상 받기. 받을 수 없으면 null
        public static string ClaimMission(SaveData save, int index, TrainingSettings training, MatchSettings ms, long utcTicks)
        {
            Ensure(save, utcTicks);
            if (index < 0 || index >= save.dailyMissions.Count) return null;
            var m = save.dailyMissions[index];
            if (!m.Done || m.claimed) return null;
            m.claimed = true;
            return Give(save, training, ms, MissionUpgrades, MissionWeeks);
        }

        public static bool BonusReady(SaveData save)
        {
            if (save.dailyBonusClaimed || save.dailyMissions == null || save.dailyMissions.Count == 0) return false;
            foreach (var m in save.dailyMissions) if (!m.claimed) return false;
            return true;
        }

        public static string ClaimBonus(SaveData save, TrainingSettings training, MatchSettings ms, long utcTicks)
        {
            Ensure(save, utcTicks);
            if (!BonusReady(save)) return null;
            save.dailyBonusClaimed = true;
            return Give(save, training, ms, BonusUpgrades, BonusWeeks);
        }

        // ---------- 출석 ----------
        public static int AttendDay(SaveData save) { return save.attendanceCount % 7 + 1; }   // 다음에 받을 날 (1~7)

        // 출석 카드에 쓸 짧은 보상 글 (예: "강화 2회\n수당 2주")
        public static string AttendShort(int day) { return "강화 " + AttendUpgrades[day - 1] + "회\n수당 " + AttendWeeks[day - 1] + "주"; }

        public static bool CanAttend(SaveData save, long utcTicks)
        {
            string today = Today(utcTicks);
            return string.IsNullOrEmpty(save.attendanceLastDate) || string.CompareOrdinal(today, save.attendanceLastDate) > 0;
        }

        public static string AttendReward(SaveData save, TrainingSettings training, MatchSettings ms, int day)
        {
            return RewardText(save, training, ms, AttendUpgrades[day - 1], AttendWeeks[day - 1]);
        }

        public static string ClaimAttendance(SaveData save, TrainingSettings training, MatchSettings ms, long utcTicks)
        {
            if (!CanAttend(save, utcTicks)) return null;
            int day = AttendDay(save);
            string text = Give(save, training, ms, AttendUpgrades[day - 1], AttendWeeks[day - 1]);
            save.attendanceCount++;
            save.attendanceLastDate = Today(utcTicks);
            return day + "일차 출석 보상: " + text;
        }

        // 받을 수 있는 보상이 있는지 (탭에 ! 표시)
        public static bool AnyClaimable(SaveData save, long utcTicks)
        {
            if (CanAttend(save, utcTicks) || BonusReady(save)) return true;
            if (save.dailyMissions != null) foreach (var m in save.dailyMissions) if (m.Done && !m.claimed) return true;
            return false;
        }
    }
}
