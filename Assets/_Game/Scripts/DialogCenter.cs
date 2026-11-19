using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 팝업 내용이 화면보다 짧으면 가운데에 두고 스크롤을 끄고, 길면 맨 위부터 스크롤할 수 있게 합니다.
    public class DialogCenter : MonoBehaviour
    {
        ScrollRect scroll;
        RectTransform view, content;
        CanvasGroup group;
        bool wasFit = true;
        int frames;

        public void Init(ScrollRect s, RectTransform v, RectTransform c)
        {
            scroll = s; view = v; content = c;
            group = c.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;   // 첫 배치가 끝날 때까지 숨겨 위치가 튀어 보이지 않게
        }

        void LateUpdate()
        {
            if (scroll == null) return;
            float vh = view.rect.height, ch = content.rect.height;
            bool fits = ch <= vh;
            scroll.vertical = !fits;
            if (fits) content.anchoredPosition = new Vector2(0f, -(vh - ch) * 0.5f);
            else if (wasFit) { content.anchoredPosition = Vector2.zero; scroll.velocity = Vector2.zero; }
            wasFit = fits;
            if (frames < 2 && ++frames == 2) group.alpha = 1f;
        }
    }
}
