using UnityEngine;
namespace MyFootballCareer
{
    // 환생 조건과 보상, 초기화 정도를 한곳에서 조절합니다.
    [CreateAssetMenu(menuName = "Football Career/Rebirth Settings", fileName = "RebirthSettings")]
    public class RebirthSettings : ScriptableObject
    {
        [Header("환생 조건 (모두 만족해야 함, 0이면 그 조건 없음)")]
        [Min(0)] public int requiredDivision = 1;             // 이번 커리어에서 이 부(또는 더 높은 부)에 올라가야 함. 1 = 1부
        [Min(0)] public int requiredSeasons = 0;              // 이 커리어에서 끝낸 시즌 수
        [Min(0)] public float requiredBestAttribute = 0f;     // 가장 높은 능력치

        [Header("영구 보너스: 훈련 성장 속도 증가율(%)")]
        public float baseBonusPercent = 10f;                  // 환생할 때마다 기본으로 얻는 보너스
        public float bonusPerSeasonPercent = 5f;              // 끝낸 시즌 1개당
        [Min(0)] public int maxBonusSeasons = 10;             // 시즌 보너스로 세는 최대 시즌 수 (경기는 바로 진행되므로 시즌만 넘겨 쌓지 못하게)
        public float bonusPerAttributePointPercent = 0.1f;    // 시작 때보다 오른 능력치 1점당
        public float maxBonusPercent = 300f;                  // 누적 보너스 상한

        [Header("초기화 정도")]
        [Range(0, 1)] public float keepProgressRatio = 0f;    // 오른 능력치 중 남기는 비율 (0 = 전부 초기화)
    }
}
