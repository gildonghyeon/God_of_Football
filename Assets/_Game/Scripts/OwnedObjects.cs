using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 화면 조각이 만든 그림(텍스처·스프라이트)을 그 조각이 사라질 때 같이 지웁니다.
    public class OwnedObjects : MonoBehaviour
    {
        public readonly List<Object> items = new List<Object>();

        void OnDestroy()
        {
            foreach (var o in items) if (o != null) Destroy(o);
            items.Clear();
        }
    }
}
