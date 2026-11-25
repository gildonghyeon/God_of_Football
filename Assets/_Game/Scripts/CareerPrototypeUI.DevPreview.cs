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
    // 개발용 장면 미리보기
    public partial class CareerPrototypeUI
    {
        // ---------- 골 장면 미리보기 (개발용) ----------
        GoalCelebration goalPreview;
        int previewCount;

        void PlayGoalPreview(TrainingStageDrills.GoalScene kind, string scorer, bool isAssist = false)
        {
            if (goalPreview != null) { goalPreview.DestroyAll(); goalPreview = null; }
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                var tex = Resources.Load<Texture2D>("Art/Match/goal_" + i);
                if (tex == null) { frames = new Sprite[0]; break; }
                tex.filterMode = FilterMode.Point;
                frames[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
            }
            var scene = stage != null && stage.Available ? new TrainingStageView(stage) : null;
            goalPreview = new GoalCelebration(font, safeRoot, frames, scene);
            int goals = previewCount++ % 3 + 1;
            bool header = kind == TrainingStageDrills.GoalScene.Cross || kind == TrainingStageDrills.GoalScene.Corner;
            string how = kind == TrainingStageDrills.GoalScene.LongShot ? "중거리" : kind == TrainingStageDrills.GoalScene.Penalty ? "PK" : kind == TrainingStageDrills.GoalScene.FreeKick ? "프리킥"
                : kind == TrainingStageDrills.GoalScene.Corner ? "코너킥" : "";
            if (header) how = (how + " 헤딩").Trim();
            bool assisted = kind == TrainingStageDrills.GoalScene.Pass || header || (kind == TrainingStageDrills.GoalScene.LongShot && previewCount % 2 == 0);   // 중거리는 혼자/패스 받아 번갈아
            goalPreview.Play(new GoalInfo
            {
                oppKit = PreviewOppKit(),
                minute = "67'", scorer = scorer, how = how, assist = assisted ? "J. Teammate" : "",
                ownName = "My Team", oppName = "Rival FC", goalsFor = goals, goalsAgainst = 1,
                matchGoals = goals, seasonGoal = 6 + goals, scene = kind,
                isAssist = isAssist, partner = isAssist ? "J. Teammate" : ""
            });
        }

        void PlayDefensePreview(TrainingStageDrills.DefenseScene kind, string name)
        {
            if (goalPreview != null) { goalPreview.DestroyAll(); goalPreview = null; }
            var scene = stage != null && stage.Available ? new TrainingStageView(stage) : null;
            goalPreview = new GoalCelebration(font, safeRoot, new Sprite[0], scene);
            goalPreview.Play(new GoalInfo
            {
                oppKit = PreviewOppKit(),
                minute = "71'", scorer = name, isDefense = true, defense = kind, how = "", assist = "", keeper = kind >= TrainingStageDrills.DefenseScene.Save,
                ownName = "My Team", oppName = "Rival FC", goalsFor = 1, goalsAgainst = 0, matchGoals = previewCount++ % 5 + 1
            });
        }

        // 미리보기의 상대 유니폼: 누를 때마다 다른 팀 홈 유니폼 (내 유니폼과 겹치면 다음 팀)
        int PreviewOppKit()
        {
            int teams = (PlayerLook.KitCount - 1) / 2;
            for (int k = 0; k < teams; k++)
            {
                int home = 1 + ((previewCount * 7 + k) % teams) * 2;
                if (home != lookKit) return home;
            }
            return 0;
        }

        void TickGoalPreview(float dt)
        {
            if (goalPreview == null) return;
            goalPreview.Tick(dt);
            if (!goalPreview.Active) { goalPreview.DestroyAll(); goalPreview = null; }
        }
    }
}
