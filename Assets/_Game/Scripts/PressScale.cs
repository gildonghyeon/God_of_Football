using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MyFootballCareer
{
    // 누르는 동안 대상들이 살짝 작아졌다가, 손을 떼면 부드럽게 원래 크기로 돌아옵니다 (포메이션 그림의 선수 점·이름표).
    // 색이 번쩍이지 않고 눌린 선수만 움직입니다.
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public readonly List<RectTransform> targets = new List<RectTransform>();
        const float Pressed = 0.86f, Rate = 1.8f;   // 약 0.08초에 눌리고 돌아옴
        bool down;
        float scale = 1f;

        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }
        public void OnPointerExit(PointerEventData e) { down = false; }

        void Update()
        {
            float goal = down ? Pressed : 1f;
            if (Mathf.Approximately(scale, goal)) return;
            scale = Mathf.MoveTowards(scale, goal, Rate * Time.unscaledDeltaTime);
            foreach (var t in targets) if (t != null) t.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
