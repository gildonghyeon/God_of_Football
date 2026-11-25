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
    // 연출: 경기 영상, 시상식, 오프라인 보상, 강화 결과
    public partial class CareerPrototypeUI
    {
        // 경기 영상이 재생되는 동안에는 true입니다. 이 동안 훈련은 멈춥니다.
        public bool IsMatchPlaying { get { return matchView != null && matchView.Active; } }

        // 경기 영상을 보여 주고, 끝나면(또는 건너뛰면) onFinished를 부릅니다. 그림이 없으면 바로 onFinished를 부릅니다.
        public TeamTactic ReplayOwnTactic, ReplayOppTactic;   // 다음 경기 영상의 두 팀 전술 (골 기록과 같은 장면이 나오게)
        public int ReplaySeasonGoals = -1;                    // 다음 경기 영상 전까지의 시즌 골 수 (-1이면 친선 경기 등)
        public int ReplaySeasonAssists = -1;                  // 다음 경기 영상 전까지의 시즌 도움 수
        public int ReplayOwnKit, ReplayOppKit;                // 다음 경기 영상의 두 팀 유니폼 (PlayerLook.Kits 번호)
        public int ReplayYouKit;                              // 내 선수 유니폼 (골키퍼 유니폼일 수 있음)
        public int CreationKit;                               // 새 선수 만들기 미리보기의 유니폼 (시작 팀)
        public void ShowMatchReplay(MatchRecord record, PlayerData player, string ownTeamName, Action onFinished, TeamTactic ownTactic = null, TeamTactic oppTactic = null)
        {
            CloseDialog();
            if (matchView != null)
            {
                matchView.SeasonGoalsBefore = ReplaySeasonGoals; ReplaySeasonGoals = -1;
                matchView.SeasonAssistsBefore = ReplaySeasonAssists; ReplaySeasonAssists = -1;
                matchView.OwnKit = ReplayOwnKit; matchView.OppKit = ReplayOppKit; matchView.YouKit = ReplayYouKit; matchView.TeamMode = ReplayTeamMode;
            }
            if (matchView == null || !matchView.Show(safeRoot, record, player, ownTeamName, settings.attributeCap, onFinished, ownTactic ?? ReplayOwnTactic, oppTactic ?? ReplayOppTactic))
            {
                if (onFinished != null) onFinished();
            }
        }

        public void SetStatus(string message) { if (status != null) status.text = message; }

        // 시즌 끝 트로피 수여 연출. 다 보면 done을 부릅니다.
        public void ShowTrophyCeremony(List<TrophyRecord> trophies, string heading, Action done)
        {
            CloseDialog();
            if (ceremony == null) { done?.Invoke(); return; }
            ceremony.Show(trophies, heading, done);
        }

        // 돌아왔을 때의 보상 창: 받은 노력 포인트가 0부터 올라가는 숫자로 보이고, COLLECT로 닫습니다.
        public void ShowOfflineReward(OfflineResult r)
        {
            if (r == null || !r.Granted) return;
            float rate = r.CreditedSeconds > 0f ? r.Points / r.CreditedSeconds : 0f;
            var inner = BuildDialog("다시 오셨군요!", "자리를 비운 시간 " + IdleRewardSystem.FormatDuration(r.AwaySeconds) + "\n그동안 선수는 계속 훈련했습니다.", 70f);
            var caption = TextNode("얻은 노력 포인트", inner, 22); caption.alignment = TextAnchor.MiddleCenter; caption.color = PixelTheme.TextDim; Height(caption.gameObject, 30);
            offlineNumber = TextNode("+0", inner, 60); offlineNumber.alignment = TextAnchor.MiddleCenter; offlineNumber.color = PixelTheme.Accent; Height(offlineNumber.gameObject, 84);
            var glow = offlineNumber.gameObject.AddComponent<Outline>(); glow.effectColor = new Color(.35f, .22f, 0f, 1f); glow.effectDistance = new Vector2(3, -3);
            string detail = "인정 시간 " + IdleRewardSystem.FormatDuration(r.CreditedSeconds)
                + (r.Capped ? "  (최대 " + IdleRewardSystem.FormatDuration(r.CapSeconds) + ")" : "")
                + "   초당 " + rate.ToString("0.00") + "\n보유 " + SeasonSystem.Money((long)r.Total) + " 포인트. 홈에서 강화해 보세요.";
            var info = TextNode(detail, inner, 20); info.alignment = TextAnchor.MiddleCenter; Height(info.gameObject, 64);
            if (r.Capped)
            {
                var cap = TextNode("오프라인 시간이 최대치에 걸렸어요. 더 자주 들러 주세요!", inner, 18); cap.alignment = TextAnchor.MiddleCenter; cap.color = PixelTheme.DangerEdge; Height(cap.gameObject, 28);
            }
            offlineTarget = r.Points; offlineT = 0f;
            PixelButton("받기", () => { SoundFx.Play(Sfx.Collect); offlineNumber = null; CloseDialog(); }, inner, 76f, PixelTheme.AccentFill, PixelTheme.Accent);
        }

        void TickOfflineCount(float dt)
        {
            if (offlineNumber == null) return;
            if (overlay == null) { offlineNumber = null; return; }   // 창이 닫혔으면 그만
            if (offlineT >= OfflineCountTime) return;
            offlineT += dt;
            float k = Mathf.Clamp01(offlineT / OfflineCountTime);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            offlineNumber.text = "+" + SeasonSystem.Money((long)Mathf.Round(offlineTarget * ease));
            float s = k < 1f ? 1f : 1f;
            if (k < 1f) SoundFx.Play(Sfx.Tick, 0.6f);
            else { SoundFx.Play(Sfx.Collect, 0.8f); s = 1.15f; }
            offlineNumber.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        // 강화 결과 연출 (결과 창 대신). 능력치 글자가 새 값으로 바뀐 뒤에 부릅니다.
        public void ShowUpgradeResult(UpgradeResult r)
        {
            if (upgradeFx == null || r == null) return;
            int i = (int)r.Attribute;
            if (stage != null && stage.Available) stage.PlayUpgrade(r.Attribute, r.Outcome);   // 훈련장 선수가 그 능력치 동작과 결과 반응
            upgradeFx.Play(r, i >= 0 && i < statTexts.Length ? statTexts[i] : null, trainingSettings != null ? trainingSettings.doubleShare : 0.5f);
        }
    }
}
