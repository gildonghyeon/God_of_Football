using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 은퇴와 내가 키운 선수
    public partial class CareerBootstrap
    {
        // ---------- 은퇴 ----------
        void Retire()
        {
            if (blocked || current == null) return;
            if (!ClubSystem.CanRetire(current)) { ui.SetStatus("시즌이 끝나야 은퇴할 수 있습니다."); return; }
            var record = ClubSystem.CurrentRecord(current, matchSettings, settings);
            int legends = ClubSystem.LegendCount(current), need = current.hasClub ? 0 : ClubSystem.FoundLegends;
            ui.ShowCardsConfirm("은퇴할까요?", (body, font) => PopupCards.RetireConfirm(body, font, record, legends, need),
                "은퇴하기", DoRetire);
        }

        // 내가 키운 선수 한 명의 커리어 기록
        void ShowLegend(int careerNumber)
        {
            if (blocked || current == null || current.legends == null) return;
            var lg = current.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null) return;
            var record = ClubSystem.LegendRecord(current, lg);
            ui.ShowCards("내가 키운 선수", (body, font) => PopupCards.CareerRecord(body, font, record, "은퇴 · 마지막 팀 " + lg.lastTeam), null, "닫기");
        }

        // 은퇴: 선수를 '내가 키운 선수'에 남기고 맨 처음 화면으로. 은퇴한 선수의 커리어 카드를 한 번 보여 줍니다.
        // 선수 키우기는 '육성 중인 선수 없음' 상태가 되고, 선수 키우기 시작을 누르면 새 선수를 만듭니다.
        void DoRetire()
        {
            if (blocked || current == null || !ClubSystem.CanRetire(current)) return;
            var legend = ClubSystem.Retire(current, matchSettings, settings);
            saves.TrySave(current, out _);
            training = null;
            var record = ClubSystem.LegendRecord(current, legend);
            int count = ClubSystem.LegendCount(current);
            bool canFound = ClubSystem.CanFound(current);
            ShowModeSelect();
            ui.ShowCards(legend.name + " 은퇴", (body, font) => PopupCards.RetiredCard(body, font, record, count, ClubSystem.FoundLegends, canFound, club != null));
        }

        // 새 선수 만들기 화면에서 돌아가기: 맨 처음 화면으로
        void CancelRetire()
        {
            if (!retiring || current == null) return;
            retiring = false;
            ShowModeSelect();
        }

        // 은퇴 뒤 새 선수로 다음 커리어 시작
        void StartNextCareer(PlayerData newPlayer, int hair, int skin, int boots)
        {
            retiring = false;
            if (current == null || !current.noActivePlayer) return;
            newPlayer.hairColor = Mathf.Clamp(hair, 0, PlayerLook.HairCount - 1);
            newPlayer.skinTone = Mathf.Clamp(skin, 0, PlayerLook.SkinCount - 1);
            newPlayer.bootColor = Mathf.Clamp(boots, 0, PlayerLook.BootCount - 1);
            ClubSystem.StartCareer(current, newPlayer);
            saves.TrySave(current, out _);
            offlineApplied = true;
            StartPlayer(newPlayer.playerName + " 커리어 시작.", false);
        }
    }
}
