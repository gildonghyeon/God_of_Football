using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 환생, 이적, 리그·친선 경기
    public partial class CareerBootstrap
    {
        void Rebirth()
        {
            if (blocked || current == null) return;
            if (!RebirthSystem.CanRebirth(current, rebirthSettings)) { ui.SetStatus("아직 환생 조건을 채우지 못했습니다."); return; }
            var preview = RebirthSystem.PreviewView(current, settings, rebirthSettings);
            ui.ShowCardsConfirm("환생할까요?", (body, font) => PopupCards.RebirthPreview(body, font, preview), "환생", DoRebirth);
        }

        void DoRebirth()
        {
            if (blocked || current == null || !RebirthSystem.CanRebirth(current, rebirthSettings)) return;
            RebirthSystem.Perform(current, settings, rebirthSettings, matchSettings);
            saves.TrySave(current, out _);
            StartPlayer("환생 완료.", false);   // 새 커리어 화면으로 다시 만듭니다.
            int count = current.rebirthCount;
            float total = current.rebirthBonusPercent, speed = RebirthSystem.TrainingMultiplier(current);
            float gain = current.careers.Count > 0 ? current.careers[current.careers.Count - 1].bonusGainedPercent : 0f;
            ui.ShowCards("환생 완료", (body, font) => PopupCards.RebirthDone(body, font, count, gain, total, speed));
        }

        // 이적 제안을 고르면 한 번 더 묻고, 확정하면 저장합니다. 확정한 뒤에는 바꿀 수 없습니다.
        void ChooseOffer(string teamId)
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("이번 시즌 선택은 이미 확정되었습니다."); return; }
            var offer = SeasonSystem.FindOffer(current, teamId);
            if (offer == null) return;
            string division = offer.division > 0 ? matchSettings.DivisionName(offer.division) : "";
            int startPercent = Mathf.RoundToInt(offer.startChance * 100f), currentWage = current.weeklyWage;
            ui.ShowCardsConfirm("이적할까요?", (body, font) => PopupCards.TransferConfirm(body, font, offer.teamName, division, startPercent, offer.weeklyWage, currentWage),
                "이적하기", () =>
            {
                if (SeasonSystem.SetPending(current, teamId) == null) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus(offer.teamName + " 이적 확정. 다음 시즌부터 뜁니다. " + message);
            });
        }

        void StayWithTeam()
        {
            if (blocked || current == null) return;
            if (SeasonSystem.TransferDecided(current)) { ui.SetStatus("이번 시즌 선택은 이미 확정되었습니다."); return; }
            var team = matchSettings.FindTeam(current.teamId);
            string name = team != null ? team.displayName : current.teamId;
            ui.ShowCardsConfirm("잔류할까요?", (body, font) => PopupCards.StayConfirm(body, font, name),
                "잔류하기", () =>
            {
                if (!SeasonSystem.ConfirmStay(current)) return;
                saves.TrySave(current, out string message);
                RefreshSeasonUI();
                ui.SetStatus(name + " 잔류 확정. " + message);
            });
        }

        void PlayMatch()
        {
            if (blocked || current == null || ui.IsMatchPlaying) return;

            if (SeasonSystem.IsComplete(current))
            {
                string changeMessage = SeasonSystem.StartNextSeason(current, matchSettings, settings, out var newSeason);   // 이적·승강 안내 (바뀐 게 없으면 null)
                int teamKit = TeamKits.PlayerKit(current.teamId, current.player.position);
                if (current.player.kitColor != teamKit) { current.player.kitColor = teamKit; ui.ApplyLook(current.player); }   // 이적하면 새 팀 유니폼으로
                saves.TrySave(current, out string newSeasonMessage);
                RefreshSeasonUI();
                ui.SetStatus("새 시즌이 시작되었습니다. " + newSeasonMessage);
                if (changeMessage != null)
                    ui.ShowCards("새 시즌", (body, font) => PopupCards.NewSeason(body, font, newSeason));
                return;
            }

            var record = SeasonSystem.PlayNext(current, matchSettings, settings);
            if (record == null) { ui.SetStatus("경기를 할 수 없습니다: 팀 정보가 없습니다."); return; }
            CountMatchMissions(record, true);
            saves.TrySave(current, out string message);   // 결과를 바로 저장해 다시 불러와도 바뀌지 않게 합니다.
            ui.SetStatus(message);
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            // 경기 영상을 보여 준 뒤(건너뛰기 포함) 순위와 요약을 갱신하고 결과를 알려 줍니다.
            ui.ReplayOwnTactic = TacticSystem.Of(ownTeam); ui.ReplayOppTactic = TacticSystem.Of(matchSettings.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(current.teamId); ui.ReplayOppKit = TeamKits.Opponent(current.teamId, record.opponentId);
            ui.ReplayYouKit = current.player.kitColor; ui.ReplayTeamMode = false;
            // 세리머니의 '시즌 N호 골': 이번 경기를 뺀 시즌 골 수
            int seasonGoals = 0, seasonAssists = 0;
            foreach (var sm in current.season.matches) if (sm.played && sm.playerPlayed) { seasonGoals += sm.playerGoals; seasonAssists += sm.playerAssists; }
            ui.ReplaySeasonGoals = Mathf.Max(0, seasonGoals - record.playerGoals);
            ui.ReplaySeasonAssists = Mathf.Max(0, seasonAssists - record.playerAssists);
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshSeasonUI();
                System.Action afterResult = null;
                if (SeasonSystem.IsComplete(current))   // 마지막 경기라면 결과 팝업을 닫은 뒤 시즌 결산을 보여줍니다.
                    afterResult = ShowSeasonEndWithTrophies;
                ui.ShowMatchResult("경기 결과", SeasonSystem.ResultData(record, current, matchSettings, null), afterResult);
            });
        }

        // 친선 경기: 순위와 상관없는 한 경기. 주급을 받고, friendlyRewardEvery경기마다 노력 포인트 보상
        void PlayFriendly()
        {
            if (blocked || current == null || training == null || ui.IsMatchPlaying) return;
            long now = System.DateTime.UtcNow.Ticks;
            float wait = training.FriendlyWait(now);
            if (wait > 0f) { ui.SetStatus("다음 친선 경기까지 " + IdleRewardSystem.FormatDuration(wait) + "."); return; }

            var record = SeasonSystem.PlayFriendly(current, matchSettings, settings, new System.Random(unchecked((int)now)));
            if (record == null) { ui.SetStatus("친선 경기를 할 수 없습니다: 상대 팀이 없습니다."); return; }
            long reward = TrainingSystem.CountFriendly(current, trainingSettings, now);
            CountMatchMissions(record, false);
            saves.TrySave(current, out string message);
            ui.SetStatus(message);
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            string rewardLine = reward > 0
                ? "\n친선 경기 보상: 노력 포인트 +" + SeasonSystem.Money(reward) + "!"
                : "\n친선 경기 " + current.friendlyCount + "/" + every + "  (" + every + "경기마다 보상)";
            var ownTeam = matchSettings.FindTeam(current.teamId);
            string ownName = ownTeam != null ? ownTeam.displayName : current.teamId;
            ui.ReplayOwnTactic = TacticSystem.Of(ownTeam); ui.ReplayOppTactic = TacticSystem.Of(matchSettings.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(current.teamId); ui.ReplayOppKit = TeamKits.Opponent(current.teamId, record.opponentId);
            ui.ReplayYouKit = current.player.kitColor; ui.ReplayTeamMode = false;
            ui.ShowMatchReplay(record, current.player, ownName, () =>
            {
                RefreshMoneyUI();
                RefreshFriendlyUI();
                ui.RefreshTraining(current.player, training);
                ui.ShowMatchResult("친선 경기 결과", SeasonSystem.ResultData(record, current, matchSettings, rewardLine.Trim()), null);
            });
        }

        void RefreshFriendlyUI()
        {
            if (current == null || training == null) return;
            int every = Mathf.Max(1, trainingSettings.friendlyRewardEvery);
            float wait = training.FriendlyWait(System.DateTime.UtcNow.Ticks);
            string reward = "노력 포인트 +" + SeasonSystem.Money(training.NextFriendlyReward);
            ui.SetFriendly((every <= 1 ? "친선 경기마다 " + reward
                                       : "친선 경기 " + current.friendlyCount + "/" + every + "   보상 " + reward)
                + "\n" + (wait > 0f ? "다음 친선 경기까지 " + IdleRewardSystem.FormatDuration(wait) : "지금 할 수 있습니다. 주급도 받습니다."),
                wait <= 0f);
        }
    }
}
