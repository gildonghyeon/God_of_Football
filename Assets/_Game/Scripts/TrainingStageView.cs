using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 훈련장 장면. Resources/Art/Training 폴더의 이미지를 불러와 프레임을 차례로 보여줍니다.
    //   bg.png                 : 배경 (한 장)
    //   <동작>_0.png, _1.png... : 선수·공이 그려진 투명 배경 프레임 (배경과 같은 크기)
    // 동작 이름: idle, shoot, pass, dribble, defend, stamina
    // 같은 이름으로 이미지를 바꿔 넣으면 코드를 고치지 않아도 새 그림이 적용됩니다.
    public class TrainingStageView
    {
        public const float Scale = 7f;      // 이미지 한 픽셀을 화면에서 대략 몇 배로 키울지
        public const int CropTopRows = 8;   // 위쪽 하늘 몇 줄을 잘라 공간을 아낄지

        readonly Dictionary<string, Sprite[]> animations = new Dictionary<string, Sprite[]>();
        Sprite background;
        Image actor;
        Sprite[] current;
        string currentKey;
        float timer, fps = 2f;
        int frame;

        // 배경과 기본 동작(idle)이 있어야 장면을 보여줍니다. 없으면 화면에서 조용히 빠집니다.
        public bool Available { get { return background != null && animations.ContainsKey("idle"); } }

        public TrainingStageView()
        {
            background = LoadSprite("bg");
            foreach (var key in new[] { "idle", "shoot", "pass", "dribble", "defend", "stamina" })
            {
                var frames = LoadFrames(key);
                if (frames.Length > 0) animations[key] = frames;
            }
        }

        static Sprite LoadSprite(string name)
        {
            var tex = Resources.Load<Texture2D>("Art/Training/" + name);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;   // 도트가 번지지 않게
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
        }

        static Sprite[] LoadFrames(string name)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 32; i++)
            {
                var sprite = LoadSprite(name + "_" + i);
                if (sprite == null) break;
                list.Add(sprite);
            }
            return list.ToArray();
        }

        public static string AnimationFor(AttributeKind kind)
        {
            switch (kind)
            {
                case AttributeKind.Shooting: return "shoot";
                case AttributeKind.Passing: return "pass";
                case AttributeKind.Dribbling: return "dribble";
                case AttributeKind.Defending: return "defend";
                default: return "stamina";
            }
        }

        static float FpsFor(string key)
        {
            switch (key)
            {
                case "shoot": return 5f;
                case "pass": return 6f;
                case "dribble": return 8f;
                case "defend": return 4f;
                case "stamina": return 8f;
                default: return 2f;
            }
        }

        // 테두리 칸 안에 배경과 선수 이미지를 겹쳐 만듭니다. 위쪽 하늘을 조금 잘라 높이를 줄입니다.
        public RectTransform Create(Transform parent)
        {
            var frameGo = new GameObject("StageFrame", typeof(RectTransform));
            frameGo.transform.SetParent(parent, false);
            frameGo.AddComponent<Image>().color = PixelTheme.PanelEdge;
            float height = (background.rect.height - CropTopRows) * Scale + 8f;
            var layout = frameGo.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(frameGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4, 4); viewportRect.offsetMax = new Vector2(-4, -4);
            viewport.AddComponent<RectMask2D>();

            float aspect = background.rect.width / background.rect.height;
            var bg = AddLayer(viewport.transform, "Background", aspect);
            bg.sprite = background;
            actor = AddLayer(viewport.transform, "Actor", aspect);
            currentKey = null;
            Play("idle");
            return frameGo.GetComponent<RectTransform>();
        }

        // 아래쪽에 맞춰 가로를 가득 채우고, 높이는 그림 비율대로 정합니다.
        static Image AddLayer(Transform parent, string name, float aspect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0, 100);
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = aspect;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void Play(string key)
        {
            if (key == currentKey) return;
            Sprite[] frames;
            if (!animations.TryGetValue(key, out frames) || frames.Length == 0) frames = animations["idle"];
            currentKey = key;
            current = frames;
            frame = 0; timer = 0f;
            fps = FpsFor(key);
            if (actor != null) actor.sprite = current[0];
        }

        public void Tick(float deltaSeconds)
        {
            if (actor == null || current == null || current.Length == 0) return;
            timer += deltaSeconds;
            float interval = 1f / Mathf.Max(1f, fps);
            int guard = 0;
            while (timer >= interval && guard++ < 8)
            {
                timer -= interval;
                frame = (frame + 1) % current.Length;
                actor.sprite = current[frame];
            }
            if (guard >= 8) timer = 0f;
        }
    }
}
