using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace MyFootballCareer
{
    // 화면 갱신: 훈련·능력치·친선 경기·커리어 카드
    public partial class CareerPrototypeUI
    {
        // ---------- 갱신 ----------
        void UpdateStats(PlayerData p)
        {
            float cap = Mathf.Max(1f, settings.attributeCap);
            for (int i = 0; i < statTexts.Length; i++)
            {
                if (statTexts[i] == null) continue;
                var kind = (AttributeKind)i;
                float value = p.Get(kind);
                statTexts[i].text = Ko.Attr(kind) + "  " + Mathf.Floor(value).ToString("0") + (value >= settings.attributeCap ? "  최대" : "");
                SetFill(statBars[i], value / cap);
            }
        }

        // 플레이어 화면이 열려 있을 때만 능력치와 훈련 진행도를 갱신합니다.
        public void RefreshTraining(PlayerData p, TrainingSystem training)
        {
            if (trainingText == null || p == null) return;
            UpdateStats(p);
            // 훈련장: 마지막으로 강화한 능력치의 훈련 장면 (아직 없으면 대기)
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var b = upgradeButtons[i];
                if (b == null) continue;
                var kind = (AttributeKind)i;
                bool max = training.IsMax(kind);
                b.GetComponentInChildren<Text>().text = max ? "최대"
                    : SeasonSystem.Money(training.Cost(kind)) + "  " + ChanceText(training.Chance(kind), training.FailStack(kind) > 0);   // 칸이 좁아 비용과 확률만
                ((Image)b.targetGraphic).color = training.CanAfford(kind) ? PixelTheme.ButtonFill : PixelTheme.QuietFill;
            }
            trainingText.text = "노력 포인트 " + SeasonSystem.Money((long)training.Points)
                + "   +" + training.PointsPerSecond.ToString("0.00") + "/초  (리그 x" + training.DivisionMultiplier.ToString("0.##") + ")"
                + "\n능력치 버튼을 누르면 강화에 도전합니다 (비용 / 성공 확률).";
            if (rateButton != null)
            {
                long rc = training.RateCost;
                rateButton.GetComponentInChildren<Text>().text = "노력 증가량 Lv " + training.RateLevel
                    + (rc < 0 ? "  (최대)" : "   강화 " + SeasonSystem.Money(rc));
                ((Image)rateButton.targetGraphic).color = rc >= 0 && training.Points >= rc ? PixelTheme.AccentFill : PixelTheme.QuietFill;
            }
        }

        // 친선 경기 안내와 버튼 색 (할 수 있을 때만 강조)
        public void SetFriendly(string info, bool ready)
        {
            if (friendlyText == null) return;
            friendlyText.text = info;
            if (friendlyButton != null) ((Image)friendlyButton.targetGraphic).color = ready ? PixelTheme.AccentFill : PixelTheme.QuietFill;
        }

        // 확률 글자. 천장 보너스가 붙었거나 10% 미만이면 소수 한 자리까지
        public static string ChanceText(float chance, bool precise)
        {
            float pct = chance * 100f;
            return (precise || pct < 10f ? pct.ToString("0.0") : Mathf.RoundToInt(pct).ToString()) + "%";
        }

        // 강화 천장으로 오른 확률(%p). 오르지 않았으면 빈 글자. 아주 작으면 소수 둘째 자리까지
        public static string BonusText(float bonus)
        {
            float pct = bonus * 100f;
            if (pct < 0.005f) return "";
            return " (+" + (pct < 0.1f ? pct.ToString("0.00") : pct.ToString("0.0")) + "%)";
        }

        public void SetTrophies(string text) { if (trophyText != null) SetLines(trophyText, text, 2); }

        // 커리어 탭 카드 (프로필, 트로피, 시즌 기록, 환생 조건)
        public void SetCareer(CareerPageData data)
        {
            if (careerPage != null) careerPage.Set(data);
            if (data == null) return;
            if (retireFill != null) retireFill.color = data.canRetire ? PixelTheme.RebirthFill : PixelTheme.QuietFill;
            if (clubSquadButton != null && clubSquadButton.activeSelf != data.clubActive) clubSquadButton.SetActive(data.clubActive);
        }
    }
}
