using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 선수단 보기, 일일 미션·출석, 시설, 커리어 탭
    public partial class CareerBootstrap
    {
        // ---------- 팀 선수단 ----------
        // 지금 보는 모드의 저장과 리그 (나만의 팀 모드면 나만의 팀 리그)
        SaveData LeagueSave { get { return inClub && club != null ? club : current; } }
        MatchSettings LeagueSettings { get { return inClub && club != null ? clubMs : matchSettings; } }
        int leagueShown;   // 팀 목록에서 보고 있는 부 (0이면 목록에서 연 선수단이 아님)

        // 순위표의 '팀 선수단 보기': 내 부부터, 순위 순서로
        void OpenSquads()
        {
            if (blocked || current == null) return;
            var save = LeagueSave;
            OpenLeague(save.season.division > 0 ? save.season.division : 1);
        }

        void OpenLeague(int division)
        {
            var save = LeagueSave; var ms = LeagueSettings;
            if (save == null) return;
            leagueShown = division;
            var table = SeasonSystem.DivisionStandings(save, ms, division);
            int k = Mathf.Min(ms.promotionSpots, table.Count / 2);
            var v = new LeagueTeamsView { title = ms.DivisionName(division) + " 팀", division = division, divisionCount = ms.DivisionCount, myDivision = save.season.division };
            foreach (var m in save.season.matches) if (m.played) v.played = Mathf.Max(v.played, m.round);
            for (int i = 0; i < table.Count; i++)
            {
                var r = table[i];
                v.rows.Add(new TableRowView { id = r.id, pos = i + 1, name = r.name, w = r.won, d = r.drawn, l = r.lost, gd = r.GoalDiff, pts = r.Points, me = r.id == save.teamId,
                    zone = division > 1 && i < k ? 1 : division < ms.DivisionCount && i >= table.Count - k ? -1 : 0 });
            }
            ui.ShowLeagueTeams(v);
        }

        // 선수단 창. 팀 목록에서 열었으면 '팀 목록으로' 버튼
        void ShowSquad(string teamId)
        {
            if (blocked || current == null) return;
            var save = LeagueSave; var ms = LeagueSettings;
            var team = ms.FindTeam(teamId);
            if (team == null) return;
            int division = SeasonSystem.DivisionOf(save, ms, teamId);
            var stats = save.teamMode && teamId == ClubSystem.ClubId ? ClubSystem.SeasonLines(save, ms, settings)   // 나만의 팀: 경기마다 쌓은 기록
                      : AwardSystem.TeamSeasonStats(save, ms, settings, teamId, out division);
            int back = leagueShown; leagueShown = 0;
            ui.ShowSquad(team.displayName + (teamId == save.teamId ? "  (내 팀)" : ""),
                         SquadSystem.View(save, ms, settings, teamId, division, stats), back > 0 ? (System.Action)(() => OpenLeague(back)) : null);
        }

        // 베스트 11에서 누른 선수의 세부 능력치: 그 팀 선수단 창과 같은 정보 (능력치·등번호·이번 시즌 기록). 닫으면 베스트 11로
        void BestElevenPlayer(BestElevenPick pick, System.Action back)
        {
            if (blocked || current == null || pick == null) return;
            var save = LeagueSave; var ms = LeagueSettings;
            if (ms.FindTeam(pick.teamId) == null) return;
            int division = SeasonSystem.DivisionOf(save, ms, pick.teamId);
            var stats = save.teamMode && pick.teamId == ClubSystem.ClubId ? ClubSystem.SeasonLines(save, ms, settings)
                      : AwardSystem.TeamSeasonStats(save, ms, settings, pick.teamId, out division);
            var view = SquadSystem.View(save, ms, settings, pick.teamId, division, stats);
            var row = view.starters.Find(r => r.name == pick.name) ?? view.subs.Find(r => r.name == pick.name);
            if (row == null) return;
            ui.ShowCards(row.name, (body, font) => PopupCards.PlayerDetail(body, font, row, view.division), back, "◀ 뒤로");
        }

        // ---------- 일일 미션·출석 ----------
        void CountMatchMissions(MatchRecord record, bool league)
        {
            long now = System.DateTime.UtcNow.Ticks;
            if (league)
            {
                DailySystem.Count(current, MissionKind.LeagueMatch, 1, now);
                if (record.goalsFor > record.goalsAgainst) DailySystem.Count(current, MissionKind.LeagueWin, 1, now);
            }
            else DailySystem.Count(current, MissionKind.Friendly, 1, now);
            if (record.playerPlayed) DailySystem.Count(current, MissionKind.AttackPoint, record.playerGoals + record.playerAssists, now);
        }

        void RefreshDailyUI()
        {
            if (current == null || current.player == null) return;
            long now = System.DateTime.UtcNow.Ticks;
            if (DailySystem.Ensure(current, now)) saves.TrySave(current, out _);   // 날짜가 바뀌어 새 미션
            int day = DailySystem.AttendDay(current);                 // 다음에 받을 날 (1~7)
            bool canAttend = DailySystem.CanAttend(current, now);
            int claimed = canAttend ? day - 1 : (day == 1 ? 7 : day - 1);   // 이번 7일 중 이미 받은 날 수
            var cardRewards = new string[7];
            for (int d = 1; d <= 7; d++) cardRewards[d - 1] = DailySystem.AttendShort(d);
            ui.SetAttendance(canAttend ? day + "일차 출석 보상을 받을 수 있어요!" : "오늘 출석 완료! 내일 또 만나요",
                claimed, canAttend ? day : 0, cardRewards,
                (canAttend ? "오늘 보상: " : "내일(" + day + "일차) 보상: ") + DailySystem.AttendReward(current, trainingSettings, matchSettings, day),
                "강화 1회 = 평균 능력치 Lv " + TrainingSystem.AverageLevel(current.player).ToString("0") + "에서 강화 1번 할 노력 포인트 ("
                    + SeasonSystem.Money(DailySystem.Points(current, trainingSettings, 1)) + ")"
                    + "\n수당 1주 = 지금 경기 수당 1주치 (" + SeasonSystem.Money(DailySystem.Money(current, matchSettings, 1)) + ")"
                    + "\n레벨과 주급이 오를수록 보상도 커집니다.",
                canAttend);
            string missionReward = DailySystem.RewardText(current, trainingSettings, matchSettings, DailySystem.MissionUpgrades, DailySystem.MissionWeeks);
            for (int i = 0; i < 3; i++)
            {
                if (i >= current.dailyMissions.Count) { ui.SetMission(i, "", "", false); continue; }
                var m = current.dailyMissions[i];
                string state = m.claimed ? "받음" : m.Done ? "받기" : m.progress + " / " + m.target;
                ui.SetMission(i, DailySystem.Describe(m) + "   " + m.progress + "/" + m.target + "\n" + missionReward, state, m.Done && !m.claimed);
            }
            bool bonus = DailySystem.BonusReady(current);
            ui.SetDailyBonus("새 미션까지 " + IdleRewardSystem.FormatDuration(DailySystem.SecondsToReset(now)),
                "모두 완료 보너스: " + DailySystem.RewardText(current, trainingSettings, matchSettings, DailySystem.BonusUpgrades, DailySystem.BonusWeeks),
                current.dailyBonusClaimed ? "보너스 받음" : bonus ? "보너스 받기" : "미션 3개를 모두 받으면 열림", bonus);
            ui.SetMissionBadge(DailySystem.AnyClaimable(current, now));
        }

        void AfterDailyClaim(string text, string title)
        {
            if (text == null) return;
            SoundFx.Play(Sfx.Collect);
            saves.TrySave(current, out _);
            ui.SetStatus(title + ": " + text);
            RefreshDailyUI();
            RefreshMoneyUI();
            ui.RefreshTraining(current.player, training);
        }

        void ClaimAttendance()
        {
            if (blocked || current == null) return;
            string text = DailySystem.ClaimAttendance(current, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks);
            if (text == null) { ui.SetStatus("오늘 출석 보상은 이미 받았습니다."); return; }
            AfterDailyClaim(text, "출석");
        }

        void ClaimMission(int index)
        {
            if (blocked || current == null) return;
            AfterDailyClaim(DailySystem.ClaimMission(current, index, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks), "미션 보상");
        }

        void ClaimDailyBonus()
        {
            if (blocked || current == null) return;
            AfterDailyClaim(DailySystem.ClaimBonus(current, trainingSettings, matchSettings, System.DateTime.UtcNow.Ticks), "미션 보너스");
        }

        // 돈으로 시설 레벨을 올립니다.
        void BuyFacility(int index)
        {
            if (blocked || current == null || index < 0 || index >= FacilitySystem.Count) return;
            var kind = (FacilityKind)index;
            long cost = FacilitySystem.Cost(current, kind, trainingSettings);
            if (cost < 0) { ui.SetStatus(FacilitySystem.Name(kind) + " 최대 레벨입니다."); return; }
            if (!FacilitySystem.Buy(current, kind, trainingSettings)) { ui.SetStatus("돈이 부족합니다: " + SeasonSystem.Money(cost) + " 필요"); return; }
            SoundFx.Play(Sfx.Buy);
            DailySystem.Count(current, MissionKind.Purchase, 1, System.DateTime.UtcNow.Ticks);
            saves.TrySave(current, out _);
            ui.SetStatus(FacilitySystem.Name(kind) + " Lv " + FacilitySystem.Level(current, kind) + ": " + FacilitySystem.Effect(current, kind, trainingSettings, matchSettings));
            RefreshMoneyUI();
            ui.RefreshTraining(current.player, training);
        }

        void RefreshRebirthUI()
        {
            ui.SetRebirth(RebirthSystem.DescribeStatus(current, rebirthSettings), RebirthSystem.CanRebirth(current, rebirthSettings));
            RefreshCareerUI();
        }

        // 커리어 탭 카드: 프로필, 트로피 룸, 시즌 기록, 환생 조건 (바뀐 게 없으면 화면은 그대로)
        void RefreshCareerUI()
        {
            if (current == null || current.player == null || current.season == null) return;
            var team = matchSettings.FindTeam(current.teamId);
            var d = new CareerPageData
            {
                name = current.player.playerName, position = Ko.Pos(current.player.position), trait = Ko.TraitName(current.player.trait),
                team = team != null ? team.displayName : current.teamId,
                division = current.season.division > 0 ? matchSettings.DivisionName(current.season.division) : "",
                season = current.season.seasonNumber, career = ClubSystem.CareerNumber(current),
                money = current.money, pay = FacilitySystem.MatchPay(current, matchSettings),
                agentPercent = Mathf.RoundToInt((FacilitySystem.WageMultiplier(current, matchSettings) - 1f) * 100f), wage = current.weeklyWage,
                rebirthCount = current.rebirthCount, bonus = current.rebirthBonusPercent, speed = RebirthSystem.TrainingMultiplier(current),
                canRebirth = RebirthSystem.CanRebirth(current, rebirthSettings),
                conditions = RebirthSystem.Conditions(current, rebirthSettings),
                canRetire = ClubSystem.CanRetire(current), legendNeed = ClubSystem.FoundLegends,
                hasClub = club != null, clubActive = club != null
            };
            if (current.legends != null) { d.legends.AddRange(current.legends); d.legends.Sort((a, b) => b.rating.CompareTo(a.rating)); }
            if (club != null)
            {
                var clubTeam = clubMs.FindTeam(ClubSystem.ClubId) ?? ClubSystem.ClubTeam(club, clubMs);
                d.clubName = club.myClub.name; d.clubKit = TeamKits.Home(ClubSystem.ClubId); d.clubStrength = clubTeam.strength;
                int free = ClubSystem.FreeAgents(current, club).Count;
                d.clubTactic = TacticSystem.FormationName(club.myClub.formation) + " · " + TacticSystem.StyleName(club.myClub.style) + " · 선수 " + club.legends.Count + "명"
                             + (free > 0 ? " · 영입 가능 " + free + "명" : "");
                d.clubDivision = clubMs.DivisionName(SeasonSystem.DivisionOf(club, clubMs, ClubSystem.ClubId)) + " · 시즌 " + club.season.seasonNumber;
            }
            d.trophyCounts = new int[System.Enum.GetValues(typeof(TrophyKind)).Length];
            if (current.trophies != null)
            {
                // 트로피 룸에는 지금 선수(이번 커리어)의 트로피만. 지난 선수의 트로피는 내가 키운 선수 기록에서 봄
                int careerNo = ClubSystem.CareerNumber(current);
                foreach (var t in current.trophies) if (t.career == careerNo) { d.trophyCounts[(int)t.kind]++; d.trophyTotal++; }
                for (int i = current.trophies.Count - 1; i >= 0 && d.recent.Count < 4; i--) if (current.trophies[i].career == careerNo) d.recent.Add(current.trophies[i]);
            }
            for (int i = current.seasonHistory.Count - 1; i >= 0 && d.history.Count < 5; i--) d.history.Add(current.seasonHistory[i]);
            if (current.careers != null && current.careers.Count > 0) { d.hasLastCareer = true; d.lastCareer = current.careers[current.careers.Count - 1]; }
            ui.SetCareer(d);
            ui.SetCurrentTeam(SeasonSystem.CurrentTeamInfo(current, matchSettings, settings));
        }
    }
}
