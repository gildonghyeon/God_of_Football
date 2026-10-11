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
        public const float Scale = 7f;   // 이미지 한 픽셀을 화면에서 몇 배로 키울지

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

        // 테두리 칸 안에 배경과 선수 이미지를 겹쳐 만듭니다.
        public RectTransform Create(Transform parent)
        {
            var frameGo = new GameObject("StageFrame", typeof(RectTransform));
            frameGo.transform.SetParent(parent, false);
            frameGo.AddComponent<Image>().color = PixelTheme.PanelEdge;
            float height = background.rect.height * Scale + 8f;
            var layout = frameGo.AddComponent<LayoutElement>();
            layout.minHeight = height; layout.preferredHeight = height;

            var bg = AddLayer(frameGo.transform, "Background");
            bg.sprite = background;
            actor = AddLayer(frameGo.transform, "Actor");
            currentKey = null;
            Play("idle");
            return frameGo.GetComponent<RectTransform>();
        }

        static Image AddLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4, 4); rect.offsetMax = new Vector2(-4, -4);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
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
