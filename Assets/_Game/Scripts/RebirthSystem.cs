using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace MyFootballCareer
{
    // 환생 조건 확인, 보상 계산, 초기화. 화면과 무관한 규칙입니다.
    public static class RebirthSystem
    {
        // 훈련 성장에 곱하는 배율. 환생 보너스 +37%라면 1.37
        public static float TrainingMultiplier(SaveData save)
        {
            return 1f + Mathf.Max(0f, save.rebirthBonusPercent) / 100f;
        }

        public static int CompletedSeasons(SaveData save)
        {
            return save.seasonHistory.Count + (SeasonSystem.IsComplete(save) ? 1 : 0);
        }

        public static float BestAttribute(PlayerData p)
        {
            return Mathf.Max(Mathf.Max(p.shooting, p.passing), Mathf.Max(p.dribbling, Mathf.Max(p.defending, p.stamina)));
        }

        // 선수를 처음 만들었을 때보다 오른 능력치 점수의 합
        public static float ProgressPoints(PlayerData p, CareerSettings career)
        {
            var start = career.CreatePlayer(p.playerName, p.position, p.trait);
            float points = 0f;
            for (int i = 0; i < 5; i++)
            {
                var kind = (AttributeKind)i;
                points += Mathf.Max(0f, p.Get(kind) - start.Get(kind));
            }
            return points;
        }

        public static bool CanRebirth(SaveData save, RebirthSettings rs)
        {
            return CompletedSeasons(save) >= rs.requiredSeasons && BestAttribute(save.player) >= rs.requiredBestAttribute;
        }

        // 이번 환생으로 새로 얻을 보너스(%). 누적 상한을 넘지 않습니다.
        public static float BonusGain(SaveData save, CareerSettings career, RebirthSettings rs)
        {
            float gain = rs.baseBonusPercent
                       + rs.bonusPerSeasonPercent * Mathf.Min(CompletedSeasons(save), rs.maxBonusSeasons)
                       + rs.bonusPerAttributePointPercent * ProgressPoints(save.player, career);
            float room = Mathf.Max(0f, rs.maxBonusPercent - save.rebirthBonusPercent);
            return Mathf.Clamp(gain, 0f, room);
        }

        public static string Perform(SaveData save, CareerSettings career, RebirthSettings rs, MatchSettings ms)
        {
            // 1) 이번 커리어의 대표 기록을 남깁니다.
            var done = new List<SeasonSummary>(save.seasonHistory);
            if (SeasonSystem.IsComplete(save)) done.Add(SeasonSystem.Summarize(save, ms));
            float gain = BonusGain(save, career, rs);
            var team = ms.FindTeam(save.teamId);
            var summary = new CareerSummary
            {
                careerNumber = save.rebirthCount + 1,
                seasons = done.Count,
                teamName = team != null ? team.displayName : save.teamId,
                bonusGainedPercent = gain
            };
            foreach (var s in done)
            {
                summary.goals += s.playerGoals;
                summary.assists += s.playerAssists;
                if (s.finalPosition <= 0) continue;
                // 최고 성적: 더 높은 부가 먼저, 같은 부면 더 높은 순위 (부가 생기기 전 시즌은 가장 낮게 봅니다)
                int div = s.division > 0 ? s.division : int.MaxValue;
                int bestDiv = summary.bestDivision > 0 ? summary.bestDivision : int.MaxValue;
                if (summary.bestPosition == 0 || div < bestDiv || (div == bestDiv && s.finalPosition < summary.bestPosition))
                {
                    summary.bestDivision = s.division;
                    summary.bestPosition = s.finalPosition;
                }
            }
            save.careers.Add(summary);

            // 2) 영구 보너스
            save.rebirthBonusPercent += gain;
            save.rebirthCount++;

            // 3) 능력치 초기화 (설정에 따라 일부는 남깁니다)
            var start = career.CreatePlayer(save.player.playerName, save.player.position, save.player.trait);
            for (int i = 0; i < 5; i++)
            {
                var kind = (AttributeKind)i;
                float baseValue = start.Get(kind);
                float kept = baseValue + Mathf.Max(0f, save.player.Get(kind) - baseValue) * rs.keepProgressRatio;
                save.player.Set(kind, Mathf.Min(career.attributeCap, kept));
            }

            // 4) 커리어 초기화
            save.trainingId = "";
            save.trainingCarrySeconds = 0f;
            save.teamId = "";
            save.season = new SeasonData();
            save.seasonHistory.Clear();
            save.offers.Clear();
            save.offersSeason = 0;
            save.pendingTeamId = "";
            save.transferDecided = false;
            save.transfers.Clear();
            save.league.Clear();              // 리그 소속도 처음 상태로
            save.leagueSeason = 0;
            save.money = 0;                   // 돈과 노력 포인트도 처음부터
            save.effortPoints = 0f;
            save.effortRateLevel = 0;
            save.facilityLevels = new System.Collections.Generic.List<int>();   // 시설도 처음부터         // 증가량 업그레이드도 처음부터 (환생 보너스는 남음)
            save.lastUpgraded = -1;
            save.weeklyWage = 0;
            SeasonSystem.Prepare(save, ms);   // 시작 팀과 첫 시즌을 다시 만듭니다.
            SeasonSystem.EnsureContract(save, ms, career);

            return "Rebirth #" + save.rebirthCount + " complete.\nEffort point bonus +" + gain.ToString("0.#")
                 + "% (total +" + save.rebirthBonusPercent.ToString("0.#") + "%).\nA new career begins.";
        }

        // ---------- 화면에 보여줄 글 ----------
        public static string DescribeStatus(SaveData save, RebirthSettings rs)
        {
            int seasons = CompletedSeasons(save);
            float best = BestAttribute(save.player);
            var sb = new StringBuilder();
            sb.Append("Rebirths ").Append(save.rebirthCount).Append("   Effort bonus +")
              .Append(save.rebirthBonusPercent.ToString("0.#")).Append("%\n");
            sb.Append("Seasons completed ").Append(seasons).Append('/').Append(rs.requiredSeasons)
              .Append(seasons >= rs.requiredSeasons ? "  OK" : "").Append('\n');
            sb.Append("Best attribute ").Append(best.ToString("0.0")).Append('/').Append(rs.requiredBestAttribute.ToString("0.#"))
              .Append(best >= rs.requiredBestAttribute ? "  OK" : "");
            int shown = 0;
            for (int i = save.careers.Count - 1; i >= 0 && shown < 1; i--, shown++)
            {
                var c = save.careers[i];
                sb.Append("\nCareer ").Append(c.careerNumber).Append(": ").Append(c.seasons).Append(" seasons");
                if (c.bestPosition > 0)
                {
                    sb.Append(", best ");
                    if (c.bestDivision > 0) sb.Append("Div ").Append(c.bestDivision).Append(' ');
                    sb.Append(SeasonSystem.Ordinal(c.bestPosition));
                }
                sb.Append(", G").Append(c.goals).Append(" A").Append(c.assists).Append(", +").Append(c.bonusGainedPercent.ToString("0.#")).Append("%");
            }
            return sb.ToString();
        }

        // 환생 확인창에 보여줄 글: 초기화되는 것, 유지되는 것, 받을 보너스
        public static string DescribePreview(SaveData save, CareerSettings career, RebirthSettings rs)
        {
            float gain = BonusGain(save, career, rs);
            float total = save.rebirthBonusPercent + gain;
            var sb = new StringBuilder();
            sb.Append("Resets: attributes");
            if (rs.keepProgressRatio > 0f) sb.Append(" (keep ").Append(Mathf.RoundToInt(rs.keepProgressRatio * 100f)).Append("% of progress)");
            sb.Append(", training, season, standings, team, transfers, money, effort points, effort rate.\n");
            sb.Append("Keeps: name, position, trait, rebirth count, bonus, career records.\n\n");
            sb.Append("Bonus gained: +").Append(gain.ToString("0.#")).Append("%\n");
            sb.Append("Effort point speed: x").Append(TrainingMultiplier(save).ToString("0.00"))
              .Append(" -> x").Append((1f + total / 100f).ToString("0.00"));
            if (gain <= 0f) sb.Append("\n(Bonus is at its limit.)");
            return sb.ToString();
        }
    }
}
