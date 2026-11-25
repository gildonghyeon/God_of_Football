using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 게임 흐름: 매 프레임 갱신, 저장, 앱 일시정지·종료
    public partial class CareerBootstrap
    {
        void Update()
        {
            if (blocked || training == null || current == null || retiring || inClub || inMenu) return;   // 은퇴 후 새 선수 만들기·나만의 팀 화면에서는 멈춤

            if (Application.isFocused && !ui.IsMatchPlaying) training.Tick(Time.unscaledDeltaTime);   // 경기 영상 중에는 훈련 중지

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= 30f) { autoSaveTimer = 0f; saves.TrySave(current, out _); }

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= 0.2f) { refreshTimer = 0f; ui.RefreshTraining(current.player, training); RefreshRebirthUI(); RefreshFriendlyUI(); RefreshDailyUI(); }
        }

        void Save()
        {
            if (blocked || current == null || saves == null) return;
            saves.TrySave(current, out string message);
            if (ui != null) ui.SetStatus(message);
        }

        void QuietSave()
        {
            if (blocked || current == null || saves == null) return;
            if (inMenu || inClub) return;   // 선수 키우기 밖에서는 저장하지 않음 (저장하면 오프라인 보상 기준 시각이 바뀜. 나갈 때 이미 저장함)
            saves.TrySave(current, out _);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) QuietSave();
            else ApplyOfflineOnResume();
        }
        void OnApplicationFocus(bool focus) { if (!focus) QuietSave(); }
        void OnApplicationQuit() { QuietSave(); }
        void OnDestroy()
        {
            if (temporarySettings && settings != null) Destroy(settings);
            if (temporaryTraining && trainingSettings != null) Destroy(trainingSettings);
            if ((temporaryMatch || clonedMatch) && matchSettings != null) Destroy(matchSettings);
            if (temporaryRebirth && rebirthSettings != null) Destroy(rebirthSettings);
        }
    }
}
