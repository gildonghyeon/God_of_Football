using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 시작 화면(모드 고르기)
    public partial class CareerBootstrap
    {
        // ---------- 시작 화면 ----------
        void ShowModeSelect()
        {
            if (blocked || current == null) return;
            inMenu = true; inClub = false; retiring = false;
            var p = current.player;
            var team = matchSettings.FindTeam(current.teamId);
            var v = new ModeSelectView
            {
                gloves = p.position == Position.Goalkeeper,
                playerName = p.playerName, hair = p.hairColor, skin = p.skinTone, kit = p.kitColor, boots = p.bootColor,
                playerLine = Ko.Pos(p.position) + " · " + (team != null ? team.displayName : "")
                           + (current.season != null && current.season.division > 0 ? " · " + matchSettings.DivisionName(current.season.division) : "")
                           + (current.season != null ? " · 시즌 " + Mathf.Max(1, current.season.seasonNumber) : ""),
                hasClub = club != null, legends = ClubSystem.LegendCount(current), legendNeed = ClubSystem.FoundLegends
            };
            if (current.noActivePlayer) { v.noPlayer = true; v.playerName = "육성 중인 선수가 없습니다"; v.playerLine = "선수 키우기 시작을 누르면 새 선수를 만듭니다"; }
            if (club != null)
            {
                ClubSystem.ApplyRoster(club, clubMs, baseTeams);
                v.clubName = club.myClub.name; v.clubKit = TeamKits.Home(ClubSystem.ClubId);
                v.clubLine = clubMs.DivisionName(club.season.division) + " · 시즌 " + club.season.seasonNumber + " · 선수 " + club.legends.Count + "명";
            }
            ui.ShowModeSelect(v);
        }

        void PickCareer()
        {
            if (blocked || current == null) return;
            inMenu = false;
            if (current.noActivePlayer) { retiring = true; ui.ShowCreation(true); return; }   // 은퇴 뒤: 새 선수 만들기
            offlineApplied = true;
            string message = menuMessage; menuMessage = null;
            StartPlayer(message, true);   // 선수 키우기를 비운 동안(앱을 껐거나 다른 모드에 있었던 시간)의 오프라인 보상
        }

        // 선수 키우기 설정 탭에서 모드 선택으로: 지금 진행을 저장하고 맨 처음 화면으로 (비운 시간은 돌아올 때 오프라인 보상)
        void ExitToMenu()
        {
            if (blocked || current == null || ui.IsMatchPlaying) return;
            saves.TrySave(current, out _);
            ShowModeSelect();
        }

        void PickClub()
        {
            if (blocked || current == null || club == null) return;
            inMenu = false;
            OpenClubMode();
        }
    }
}
