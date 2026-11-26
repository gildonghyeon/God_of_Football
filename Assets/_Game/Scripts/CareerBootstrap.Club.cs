using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 나만의 팀 창단과 나만의 팀 모드
    public partial class CareerBootstrap
    {
        // ---------- 나만의 팀 ----------
        void FoundClub(string name, int kit, Formation formation, TacticStyle style)
        {
            if (blocked || current == null) return;
            if (club != null || !ClubSystem.CanFound(current)) { ui.SetStatus("은퇴 선수 " + ClubSystem.FoundLegends + "명이 있어야 창단할 수 있습니다."); return; }
            if (name.Length > 24) name = name.Substring(0, 24);
            club = ClubSystem.Found(current, clubMs, baseTeams, name, kit, formation, style);
            if (!clubSaves.TrySave(club, out string message)) { club = null; current.hasClub = false; ui.SetStatus(message); return; }
            saves.TrySave(current, out _);
            var team = clubMs.FindTeam(ClubSystem.ClubId);
            int kitIndex = TeamKits.Home(ClubSystem.ClubId), legends = club.legends.Count;
            string tactic = TacticSystem.FormationName(formation) + " · " + TacticSystem.StyleName(style);
            string division = clubMs.DivisionName(club.season.division);
            SoundFx.Play(Sfx.Buy);
            OpenClubMode();
            ui.ShowCards("창단 완료", (body, font) => PopupCards.ClubFounded(body, font, name, kitIndex, tactic, legends, team != null ? team.strength : 0f, division, false));
        }

        void LoadClub()
        {
            club = null;
            if (!clubSaves.TryLoad(out var loaded, out _) || loaded == null || !loaded.teamMode) return;
            club = loaded;
            ClubSystem.ApplyRoster(club, clubMs, baseTeams);
            SeasonSystem.Prepare(club, clubMs);
        }

        // 저장 초기화: 나만의 팀 저장도 옆으로 옮겨 둠
        void ArchiveClub()
        {
            if (club != null) clubSaves.TrySave(club, out _);
            clubSaves.ArchiveSave(out _);
            club = null; inClub = false;
        }

        // ---------- 나만의 팀 모드 ----------
        void OpenClubMode()
        {
            if (blocked || current == null || club == null) return;
            inClub = true; inMenu = false;
            ui.ResetClubTab();
            ClubSystem.ApplyRoster(club, clubMs, baseTeams);
            SeasonSystem.Prepare(club, clubMs);
            ShowClubScreen();
        }

        void ShowClubScreen()
        {
            var team = clubMs.FindTeam(ClubSystem.ClubId);
            var s = club.season;
            var v = new ClubModeView
            {
                name = club.myClub.name, kit = TeamKits.Home(ClubSystem.ClubId), season = s.seasonNumber, legends = club.legends.Count,
                tactic = TacticSystem.FormationName(club.myClub.formation) + " · " + TacticSystem.StyleName(club.myClub.style),
                division = clubMs.DivisionName(s.division), strength = team != null ? team.strength : 0f,
                complete = SeasonSystem.IsComplete(club), round = s.nextMatchIndex, rounds = s.matches.Count,
                table = SeasonSystem.TableView(club, clubMs)
            };
            foreach (var m in s.matches)
            {
                if (!m.played) continue;
                v.gf += m.goalsFor; v.ga += m.goalsAgainst;
                if (m.goalsFor > m.goalsAgainst) v.w++; else if (m.goalsFor == m.goalsAgainst) v.d++; else v.l++;
            }
            for (int i = s.nextMatchIndex - 1; i >= 0 && v.recent.Count < 3; i--)
            {
                var m = s.matches[i];
                v.recent.Add((m.goalsFor > m.goalsAgainst ? "승 " : m.goalsFor == m.goalsAgainst ? "무 " : "패 ") + m.goalsFor + "-" + m.goalsAgainst + " " + m.opponentName);
            }
            if (!v.complete)
            {
                var next = s.matches[s.nextMatchIndex];
                v.nextText = next.round + "라운드 · " + (next.home ? "홈" : "원정") + " · " + next.opponentName;
                v.nextOpponentId = next.opponentId;
            }
            v.free = ClubSystem.FreeAgents(current, club);
            ClubSystem.EnsureClubSquad(club);
            v.squadCount = ClubSystem.Squad(club).players.Count;
            v.leaders = ClubLeaders(out v.leadersHeader, out v.best);
            bool newTrophies = AwardSystem.BackfillClubTrophies(club);                // 트로피 룸 전에 끝난 시즌의 우승·승격
            if (AwardSystem.EnsureAwards(club, clubMs, settings)) newTrophies = true;   // 시즌이 끝나면 트로피 (한 번만)
            if (newTrophies) clubSaves.TrySave(club, out _);
            v.trophies = club.trophies;
            ui.ShowClubMode(v);
        }

        // 나만의 팀 개인 순위: 리그 전체에서 비교 (우리 팀 선수는 금색 줄, 5위 밖이어도 순위와 함께)
        List<LeaderBlockView> ClubLeaders(out string header, out BestElevenSquadView best)
        {
            return AwardSystem.ClubLeadersView(club, clubMs, settings, ClubSystem.SeasonLines(club, clubMs, settings), ClubSystem.Squad(club).players, out header, out best);
        }

        void ClubPlay()
        {
            if (club == null || ui.IsMatchPlaying) return;
            if (SeasonSystem.IsComplete(club))
            {
                SeasonSystem.StartNextSeason(club, clubMs, settings, out var info);
                clubSaves.TrySave(club, out _);
                ShowClubScreen();
                ui.ShowCards("새 시즌", (body, font) => PopupCards.NewSeason(body, font, info));
                return;
            }
            var record = SeasonSystem.PlayNext(club, clubMs, settings);
            if (record == null) { ui.SetStatus("경기를 할 수 없습니다."); return; }
            clubSaves.TrySave(club, out _);
            var own = clubMs.FindTeam(ClubSystem.ClubId);
            ui.ReplayOwnTactic = TacticSystem.Of(own); ui.ReplayOppTactic = TacticSystem.Of(clubMs.FindTeam(record.opponentId));
            ui.ReplayOwnKit = TeamKits.Home(ClubSystem.ClubId); ui.ReplayOppKit = TeamKits.Opponent(ClubSystem.ClubId, record.opponentId);
            ui.ReplayYouKit = 0; ui.ReplayTeamMode = true;
            ui.ReplayOwnLabels = TeamLabels(club, clubMs, ClubSystem.ClubId, -1, null);   // 우리 팀 이름표: 지금 뛰는 자리의 포지션 + 이름
            ui.ReplayOwnSkills = TeamSkills(club, clubMs, ClubSystem.ClubId); ui.ReplayOppSkills = TeamSkills(club, clubMs, record.opponentId);
            ui.ReplayOwnNames = TeamNames(club, clubMs, ClubSystem.ClubId); ui.ReplayOppNames = TeamNames(club, clubMs, record.opponentId);
            ui.ShowMatchReplay(record, club.player, club.myClub.name, () =>
            {
                ShowClubScreen();
                System.Action after = null;
                if (SeasonSystem.IsComplete(club)) after = ShowClubSeasonEnd;
                ui.ShowMatchResult("경기 결과", SeasonSystem.ResultData(record, club, clubMs, null), after);
            });
        }

        void ShowClubSeasonEnd()
        {
            SeasonSystem.EnsureSeasonEnd(club, clubMs);
            clubSaves.TrySave(club, out _);
            var sum = SeasonSystem.Summarize(club, clubMs);
            int teams = club.season.teamIds.Count, div = club.season.division, next = club.season.nextDivision;
            int move = next > 0 && div > 0 ? (next < div ? 1 : next > div ? -1 : 0) : 0;
            string name = club.myClub.name, division = clubMs.DivisionName(div);
            ShowClubScreen();
            ui.ShowCards("시즌 종료", (body, font) => ClubModeScreen.SeasonEnd(body, font, name, sum.finalPosition, teams, division, move, sum.wins, sum.draws, sum.losses, sum.goalsFor, sum.goalsAgainst));
        }

        void ClubSquad()
        {
            if (club == null) return;
            int div = SeasonSystem.DivisionOf(club, clubMs, ClubSystem.ClubId);
            ui.ShowSquad(club.myClub.name, SquadSystem.View(club, clubMs, settings, ClubSystem.ClubId, div, null));
        }

        // 영입: 선수단(19명)이 꽉 찼으면 먼저 내보낼 선수를 고르는 창 → 확인 → 영입
        void ClubSign(int careerNumber)
        {
            if (club == null || current == null) return;
            var lg = current.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null) return;
            ClubSystem.EnsureClubSquad(club);
            var squad = ClubSystem.Squad(club);
            if (squad.players.Count < ClubSystem.SquadSize) { DoClubSign(careerNumber, int.MinValue); return; }
            var starters = new HashSet<int>(club.myClub.lineup);
            var rows = new List<ClubPlayerView>();
            foreach (var p in squad.players) rows.Add(PlayerView(p, null, 0));
            rows.Sort((a, b) => starters.Contains(a.id) != starters.Contains(b.id) ? (starters.Contains(a.id) ? -1 : 1)
                             : a.role != b.role ? a.role.CompareTo(b.role) : b.rating.CompareTo(a.rating));
            ui.ShowReleasePicker(lg.name, rows, starters, releaseId =>
            {
                var gone = squad.players.Find(p => p.id == releaseId);
                if (gone == null) return;
                string body = gone.name + "을(를) 방출하고 " + lg.name + "을(를) 영입합니다.\n"
                    + (ClubSystem.IsLegend(gone) ? "방출한 은퇴 선수는 다시 무료 영입 목록으로 돌아갑니다." : "방출한 선수는 팀을 떠나 돌아오지 않습니다.");
                ui.ShowConfirm("방출하고 영입할까요?", body, 130f, "영입하기", () => DoClubSign(careerNumber, releaseId));
            });
        }

        void DoClubSign(int careerNumber, int releaseId)
        {
            var lg = current.legends.Find(x => x.careerNumber == careerNumber);
            if (lg == null) return;
            float before = clubMs.FindTeam(ClubSystem.ClubId).strength;
            if (!ClubSystem.Sign(current, club, clubMs, baseTeams, careerNumber, releaseId)) return;
            clubSaves.TrySave(club, out _);
            float after = clubMs.FindTeam(ClubSystem.ClubId).strength;
            string name = club.myClub.name;
            SoundFx.Play(Sfx.Buy);
            ShowClubScreen();
            ui.ShowCards("무료 영입", (body, font) => ClubModeScreen.Signed(body, font, lg, name, before, after));
        }

        // ---------- 팀 관리 (포메이션·전술·주전/교체) ----------
        int clubSelected = int.MinValue, clubSelected2 = int.MinValue;   // 고른 선수 (두 명까지: 두 명이면 비교·교체)

        void OpenClubTeam()
        {
            if (club == null) return;
            clubSelected = clubSelected2 = int.MinValue;
            ShowClubTeam();
        }

        ClubPlayerView PlayerView(SquadPlayer p, SlotDef[] layout, int slot)
        {
            var v = new ClubPlayerView { id = p.id, name = p.name, role = p.role, slotRole = p.role, rating = p.rating, effective = p.rating, legend = ClubSystem.IsLegend(p), number = p.number };
            if (layout != null)
            {
                var s = layout[slot];
                v.slotRole = s.role; v.kind = s.kind; v.x = s.x; v.depth = s.depth;
                v.effective = ClubSystem.Effective(club, p, s.role);
            }
            return v;
        }

        void ShowClubTeam()
        {
            ClubSystem.EnsureClubSquad(club);
            SquadSystem.Ensure(club, clubMs);   // 등번호
            var layout = TacticSystem.Layout(club.myClub.formation);
            var xi = ClubSystem.Lineup(club);
            var team = clubMs.FindTeam(ClubSystem.ClubId);
            var v = new ClubTeamView
            {
                name = club.myClub.name, formation = club.myClub.formation, style = club.myClub.style, selected = clubSelected, selected2 = clubSelected2,
                strength = team != null ? team.strength : 0f, level = ClubSystem.TeamLevel(club),
                summary = SquadSystem.View(club, clubMs, settings, ClubSystem.ClubId, SeasonSystem.DivisionOf(club, clubMs, ClubSystem.ClubId), null)
            };
            for (int i = 0; i < xi.Length; i++) if (xi[i] != null) v.starters.Add(PlayerView(xi[i], layout, i));
            var starters = new HashSet<int>(club.myClub.lineup);
            foreach (var p in ClubSystem.Squad(club).players) if (!starters.Contains(p.id)) v.bench.Add(PlayerView(p, null, 0));
            v.bench.Sort((a, b) => a.role != b.role ? b.role.CompareTo(a.role) : b.rating.CompareTo(a.rating));   // 공격수 → 미드필더 → 수비수 → 골키퍼, 같은 포지션은 능력 순
            ui.ShowClubTeam(v);
        }

        // 카드의 'i': 선수 세부 능력치 (내가 키운 선수는 실제 능력치, 새 선수는 능력과 포지션으로 추정)
        void ClubPlayerInfo(int id)
        {
            var row = ClubRow(id);
            if (row == null) return;
            string division = clubMs.DivisionName(club.season.division);
            ui.ShowCards(row.name, (b, f) => PopupCards.PlayerDetail(b, f, row, division), null, "닫기");
        }

        // 나만의 팀 선수 한 명의 정보 (세부 창·비교용): 능력치(내가 키운 선수는 실제 값)와 이번 시즌 기록
        SquadRowView ClubRow(int id)
        {
            if (club == null) return null;
            var p = ClubSystem.Squad(club).players.Find(x => x.id == id);
            if (p == null) return null;
            var row = new SquadRowView { id = p.id, name = p.name, role = p.role, age = p.age, rating = p.rating, attrs = ClubSystem.Attrs(club, p), legend = ClubSystem.IsLegend(p), number = p.number };
            var stats = ClubSystem.SeasonLines(club, clubMs, settings);   // 경기마다 쌓은 기록 (교체로 빠져도 남음)
            if (stats.TryGetValue(p.name, out var line)) row.line = line;
            return row;
        }

        // 나만의 팀 등번호 변경: 같은 번호를 쓰던 선수와는 서로 맞바꿈
        void ClubNumber(int id, int number)
        {
            if (club == null) return;
            number = Mathf.Clamp(number, 1, 99);
            var players = ClubSystem.Squad(club).players;
            var p = players.Find(x => x.id == id);
            if (p == null || p.number == number) return;
            var other = players.Find(x => x.id != id && x.number == number);
            if (other != null) other.number = p.number;
            p.number = number;
            clubSaves.TrySave(club, out _);
            ShowClubTeam();
            ui.SetStatus(p.name + "의 등번호를 " + number + "번으로 바꿨습니다." + (other != null ? " (" + other.name + "는 " + other.number + "번)" : ""));
        }

        // 두 명을 고른 뒤 '비교': 두 선수 비교 창
        void ClubCompare()
        {
            var a = ClubRow(clubSelected); var b = ClubRow(clubSelected2);
            if (a == null || b == null) return;
            ui.ShowCards("선수 비교", (body, f) => PopupCards.ComparePlayers(body, f, a, b), null, "닫기");
        }

        // 두 명을 고른 뒤 '교체': 두 선수의 자리를 바꿈 (1군↔1군은 자리 맞바꾸기, 1군↔교체는 교체 선수가 그 자리에)
        void ClubSwap()
        {
            if (club == null || clubSelected == int.MinValue || clubSelected2 == int.MinValue) return;
            if (!ClubSystem.Swap(club, clubSelected, clubSelected2)) { ui.SetStatus("교체 선수끼리는 바꿀 자리가 없습니다."); return; }
            clubSelected = clubSelected2 = int.MinValue;
            ApplyClubChange();
            ShowClubTeam();
        }

        // 선수 누르기: 한 명 고르기 → 두 번째 선수 고르기 (두 명이면 비교·교체). 고른 선수를 다시 누르면 선택을 풂
        void ClubTap(int id)
        {
            if (club == null) return;
            if (id == clubSelected) { clubSelected = clubSelected2; clubSelected2 = int.MinValue; }
            else if (id == clubSelected2) clubSelected2 = int.MinValue;
            else if (clubSelected == int.MinValue) clubSelected = id;
            else clubSelected2 = id;   // 이미 두 명이면 두 번째 선수를 바꿈
            ShowClubTeam();
        }

        // 선택 막대의 '취소': 고른 선수 모두 풀기
        void ClubClearSelection()
        {
            clubSelected = clubSelected2 = int.MinValue;
            ShowClubTeam();
        }

        void ClubFormation(Formation f)
        {
            if (club == null) return;
            ClubSystem.SetFormation(club, f);
            clubSelected = clubSelected2 = int.MinValue;
            ApplyClubChange();
            ShowClubTeam();
        }

        void ClubStyle(TacticStyle s)
        {
            if (club == null) return;
            club.myClub.style = s;
            ApplyClubChange();
            ShowClubTeam();
        }

        // 바뀐 라인업·전술을 팀 전력과 경기 전술에 반영하고 저장
        void ApplyClubChange()
        {
            ClubSystem.ApplyRoster(club, clubMs, baseTeams);
            current.myClub = club.myClub;
            clubSaves.TrySave(club, out _);
        }

        void ClubBack()
        {
            inClub = false;
            ui.ReplayTeamMode = false;
            ShowModeSelect();
        }
    }
}
