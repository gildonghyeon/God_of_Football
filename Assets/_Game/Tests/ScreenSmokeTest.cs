using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MyFootballCareer.Tests
{
    // 화면 자동 점검: 게임의 모든 화면을 차례로 열어 보고, 그동안 오류(예외·LogError)가 하나도 없는지 확인합니다.
    // (Unity 테스트는 오류 로그가 나오면 자동으로 실패합니다)
    // 진짜 저장 파일을 건드리지 않도록 임시 폴더에 저장합니다.
    //   Unity 메뉴 Window > General > Test Runner > PlayMode 에서 실행하거나,
    //   Unity를 닫은 상태에서 명령줄: Unity.exe -batchmode -projectPath <프로젝트> -runTests -testPlatform PlayMode
    public class ScreenSmokeTest
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Application.temporaryCachePath, "smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            CareerSaveSystem.TestMode = true;   // 이 실행 동안 진짜 저장 폴더는 쓰지 않음 (테스트가 끝난 뒤에도)
            CareerSaveSystem.RootOverride = root;
        }

        // 테스트 장면의 오브젝트를 먼저 모두 지운 뒤(앱 종료 때 저장하지 않게) 임시 폴더를 정리합니다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.Destroy(go);
            foreach (var b in UnityEngine.Object.FindObjectsByType<CareerBootstrap>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(b.gameObject);
            yield return null;
            CareerSaveSystem.RootOverride = null;   // TestMode는 켜 둔 채로
            try { Directory.Delete(root, true); } catch { }
        }

        // ---------- 리플렉션 도우미 (화면 흐름 메서드는 private이라) ----------
        static object Call(object target, string name, params object[] args)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (m.Name == name && m.GetParameters().Length >= args.Length)
                    {
                        var ps = m.GetParameters();
                        var full = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++) full[i] = i < args.Length ? args[i] : ps[i].DefaultValue;
                        return m.Invoke(target, full);
                    }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        static T Get<T>(object target, string name)
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "필드 없음: " + name);
            return (T)f.GetValue(target);
        }

        static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // 경기 영상이 나오면 몇 프레임 돌려 본 뒤 건너뛰고, 결과 팝업을 닫음
        static IEnumerator FinishReplay(CareerPrototypeUI ui)
        {
            yield return Frames(30);
            var view = Get<MatchReplayView>(ui, "matchView");
            if (view != null && view.Active) Call(view, "Skip");
            yield return Frames(3);
            Call(ui, "CloseDialog");
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator AllScreensOpenWithoutErrors()
        {
            SceneManager.LoadScene("Main");
            yield return Frames(3);
            var boot = UnityEngine.Object.FindAnyObjectByType<CareerBootstrap>();
            Assert.IsNotNull(boot, "CareerBootstrap 없음");
            var ui = boot.GetComponent<CareerPrototypeUI>();
            Assert.IsNotNull(ui, "CareerPrototypeUI 없음");

            // 1) 포지션마다 선수 만들기 → 탭 5개 모두 열기 → 저장 초기화
            foreach (Position pos in Enum.GetValues(typeof(Position)))
            {
                var trait = Ko.TraitsFor(pos)[0];
                Call(boot, "Create", "Smoke" + (int)pos, pos, trait, 1, 1, 0, 1);
                yield return Frames(3);
                Assert.IsNotNull(Get<SaveData>(boot, "current"), pos + " 선수를 만들지 못함");
                for (int page = 0; page < 5; page++) { Call(ui, "SetPage", page); yield return Frames(2); }
                Call(boot, "DoResetSave");
                yield return Frames(2);
            }

            // 2) 골키퍼로 다시 만들고 경기·팝업·창들
            Call(boot, "Create", "Keeper", Position.Goalkeeper, Trait.ShotStopper, 2, 2, 0, 2);
            yield return Frames(3);
            Call(boot, "PlayMatch");
            yield return FinishReplay(ui);
            Call(ui, "ShowLookDialog"); yield return Frames(2); Call(ui, "CloseDialog");
            Call(boot, "OpenSquads"); yield return Frames(2); Call(ui, "CloseDialog");
            var save = Get<SaveData>(boot, "current");
            Call(boot, "ShowSquad", save.teamId); yield return Frames(2); Call(ui, "CloseDialog");
            Call(ui, "ShowFoundDialog"); yield return Frames(2); Call(ui, "CloseDialog");

            // 3) 개발용 장면 미리보기 (골·도움·수비·선방 장면 전부)
            foreach (TrainingStageDrills.GoalScene g in Enum.GetValues(typeof(TrainingStageDrills.GoalScene)))
            {
                bool keeperAssist = g == TrainingStageDrills.GoalScene.KeeperKick || g == TrainingStageDrills.GoalScene.KeeperRoll;
                Call(ui, "PlayGoalPreview", g, "Keeper", keeperAssist);
                yield return Frames(20);
            }
            foreach (TrainingStageDrills.DefenseScene d in Enum.GetValues(typeof(TrainingStageDrills.DefenseScene)))
            {
                Call(ui, "PlayDefensePreview", d, "Keeper");
                yield return Frames(20);
            }

            // 4) 은퇴 5번 → 나만의 팀 창단 → 나만의 팀 모드 화면·경기·영입·선수단
            var ms = Get<MatchSettings>(boot, "matchSettings");
            var career = Get<CareerSettings>(boot, "settings");
            for (int k = 0; k < 6; k++)
            {
                save = Get<SaveData>(boot, "current");
                while (!SeasonSystem.IsComplete(save)) SeasonSystem.PlayNext(save, ms, career);
                Call(boot, "RefreshSeasonUI");
                yield return Frames(2);
                Call(boot, "Retire"); yield return Frames(2); Call(ui, "CloseDialog");   // 은퇴 확인 창
                Call(boot, "DoRetire"); yield return Frames(3);                          // 은퇴 → 맨 처음 화면 + 커리어 카드
                Assert.IsTrue(Get<SaveData>(boot, "current").noActivePlayer, "은퇴 뒤 선수 없음 상태가 아님");
                Call(ui, "CloseDialog");
                Call(boot, "PickCareer"); yield return Frames(2);                        // 선수 키우기 시작 → 새 선수 만들기
                var pos = (Position)(k % 4);
                Call(boot, "Create", "Legend" + k, pos, Ko.TraitsFor(pos)[0], k % 3, k % 2, 0, 0);
                yield return Frames(3);
                Assert.IsFalse(Get<SaveData>(boot, "current").noActivePlayer, "새 선수로 시작하지 않음");
                if (k == 4)
                {
                    Call(boot, "FoundClub", "Smoke FC", 1, Formation.F433, TacticStyle.Possession);
                    yield return Frames(3);
                    Call(ui, "CloseDialog");
                    Call(boot, "ClubPlay");
                    yield return FinishReplay(ui);
                    Call(boot, "ClubSquad"); yield return Frames(2); Call(ui, "CloseDialog");
                    Call(boot, "ClubBack"); yield return Frames(2);   // 시작 화면
                    Call(boot, "PickCareer"); yield return Frames(3);
                }
            }
            // 창단 뒤 은퇴한 선수 무료 영입
            Call(boot, "ShowModeSelect"); yield return Frames(2);
            Call(boot, "PickClub"); yield return Frames(3);
            var legends = Get<SaveData>(boot, "current").legends;
            int newcomer = legends[legends.Count - 1].careerNumber;
            Call(boot, "ClubSign", newcomer); yield return Frames(3);            // 방출할 선수 고르기 창
            Call(ui, "CloseDialog");
            var clubSave = Get<SaveData>(boot, "club");
            var squad = ClubSystem.Squad(clubSave);
            Assert.AreEqual(ClubSystem.SquadSize, squad.players.Count, "선수단이 19명이 아님");
            Assert.AreEqual(ClubSystem.Starters, clubSave.myClub.lineup.Count, "주전이 11명이 아님");
            int release = squad.players.Find(p => p.id <= -2000 && clubSave.myClub.lineup.Contains(p.id)).id;   // 주전 새 선수를 방출
            Call(boot, "DoClubSign", newcomer, release); yield return Frames(3);
            Call(ui, "CloseDialog");
            Assert.AreEqual(ClubSystem.SquadSize, squad.players.Count, "영입 뒤 선수단이 19명이 아님");
            Assert.IsTrue(clubSave.myClub.lineup.Contains(-1000 - newcomer), "영입한 선수가 방출한 주전 자리에 들어가지 않음");
            // 팀 관리: 포메이션·스타일 바꾸기, 주전↔교체 바꾸기
            Call(boot, "OpenClubTeam"); yield return Frames(2);
            foreach (Formation f in Enum.GetValues(typeof(Formation))) { Call(boot, "ClubFormation", f); yield return Frames(1); }
            foreach (TacticStyle st in Enum.GetValues(typeof(TacticStyle))) { Call(boot, "ClubStyle", st); yield return Frames(1); }
            int benchId = squad.players.Find(p => !clubSave.myClub.lineup.Contains(p.id)).id;
            Call(boot, "ClubTap", clubSave.myClub.lineup[5]); yield return Frames(1);
            Call(boot, "ClubTap", benchId); yield return Frames(2);
            Assert.IsTrue(clubSave.myClub.lineup.Contains(benchId), "교체 선수가 주전으로 들어가지 않음");
            Call(boot, "ShowClubScreen"); yield return Frames(2);
            Call(boot, "ShowLegend", legends[0].careerNumber); yield return Frames(2); Call(ui, "CloseDialog");
            Call(boot, "ClubBack"); yield return Frames(2);
            Call(boot, "PickCareer"); yield return Frames(3);
            for (int page = 0; page < 5; page++) { Call(ui, "SetPage", page); yield return Frames(2); }

            Assert.IsNotNull(Get<SaveData>(boot, "club"), "나만의 팀이 만들어지지 않음");
            Assert.AreEqual(6, Get<SaveData>(boot, "current").legends.Count);
        }
    }
}
