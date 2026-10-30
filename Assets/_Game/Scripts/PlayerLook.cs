using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 선수 외형(머리색, 피부색). 원본 캐릭터 그림에서 머리카락과 피부 색 영역을 찾아 색만 바꿉니다.
    // 머리 모양은 그림 자체이므로 바뀌지 않습니다. 새 머리색·피부색을 늘리려면 아래 표에 한 줄씩 추가하면 됩니다.
    public static class PlayerLook
    {
        public static readonly string[] HairNames = { "Brown", "Black", "Chestnut", "Blond", "Red", "Silver", "Blue" };
        public static readonly string[] SkinNames = { "Light", "Tan", "Brown", "Dark" };

        // 머리색: 색상(도), 채도, 기준 밝기. 첫 번째(원본)는 바꾸지 않습니다.
        static readonly float[] HairHue = { 0f, 20f, 22f, 44f, 13f, 220f, 214f };
        static readonly float[] HairSat = { 0f, .18f, .62f, .55f, .80f, .06f, .78f };
        static readonly float[] HairVal = { 0f, .10f, .36f, .95f, .66f, .88f, .62f };
        // 피부색(기준 색). 첫 번째(원본)는 바꾸지 않습니다.
        static readonly Color32[] SkinRgb =
        {
            new Color32(0, 0, 0, 255), new Color32(222, 168, 122, 255), new Color32(170, 116, 78, 255), new Color32(116, 74, 50, 255)
        };

        const float BaseHairValue = 0.22f;                    // 원본 머리카락의 보통 밝기
        const float BaseSkinSat = 0.28f, BaseSkinVal = 0.98f; // 원본 피부의 채도와 밝기

        public static int HairCount { get { return HairNames.Length; } }
        public static int SkinCount { get { return SkinNames.Length; } }

        // 그림 데이터(png 파일 내용)를 읽어서 색을 바꾼 질감을 만듭니다. 쓰고 나면 Destroy로 지워 주세요.
        public static Texture2D LoadRecolored(byte[] pngBytes, int hair, int skin)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(pngBytes)) { Object.Destroy(tex); return null; }
            if (hair > 0 || skin > 0)
            {
                var pixels = tex.GetPixels32();
                Recolor(pixels, tex.width, tex.height, hair, skin);
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static void Recolor(Color32[] px, int w, int h, int hair, int skin)
        {
            hair = Mathf.Clamp(hair, 0, HairCount - 1);
            skin = Mathf.Clamp(skin, 0, SkinCount - 1);
            if (hair == 0 && skin == 0) return;
            int n = w * h;
            var sat = new float[n]; var val = new float[n];
            var opaque = new bool[n]; var hairMask = new bool[n]; var skinMask = new bool[n];

            // 1) 머리카락과 피부 색을 가진 픽셀 찾기
            for (int i = 0; i < n; i++)
            {
                if (px[i].a < 128) continue;
                opaque[i] = true;
                float hue, s, v;
                Color.RGBToHSV((Color)px[i], out hue, out s, out v);
                sat[i] = s; val[i] = v;
                float deg = hue * 360f;
                bool warm = deg >= 335f || deg <= 40f;
                hairMask[i] = warm && s >= 0.12f && s <= 0.65f && v >= 0.045f && v <= 0.48f;
                skinMask[i] = (deg >= 335f || (deg >= 6f && deg <= 40f)) && s >= 0.10f && s <= 0.52f && v >= 0.66f;
            }

            // 2) 머리카락 사이에 섞인 어두운 그림자 점도 머리로 포함 (바깥 윤곽선은 제외)
            if (hair > 0)
            {
                var edge = new bool[n];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!opaque[i]) continue;
                        for (int dy = -1; dy <= 1 && !edge[i]; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h || !opaque[ny * w + nx]) { edge[i] = true; break; }
                            }
                    }
                var add = new List<int>();
                for (int pass = 0; pass < 3; pass++)
                {
                    add.Clear();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * w + x;
                            if (!opaque[i] || hairMask[i] || skinMask[i] || edge[i] || val[i] >= 0.32f) continue;
                            int count = 0;
                            for (int dy = -2; dy <= 2; dy++)
                                for (int dx = -2; dx <= 2; dx++)
                                {
                                    int nx = x + dx, ny = y + dy;
                                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && hairMask[ny * w + nx]) count++;
                                }
                            if (count >= 9) add.Add(i);
                        }
                    if (add.Count == 0) break;
                    for (int k = 0; k < add.Count; k++) hairMask[add[k]] = true;
                }
            }

            // 3) 색 바꾸기: 밝고 어두운 정도(음영)는 그대로 두고 색만 바꿉니다.
            float skinHue = 0f, skinS = 0f, skinV = 0f;
            if (skin > 0) Color.RGBToHSV((Color)SkinRgb[skin], out skinHue, out skinS, out skinV);
            float hairHue = HairHue[hair] / 360f;
            for (int i = 0; i < n; i++)
            {
                if (!opaque[i]) continue;
                Color c;
                if (hair > 0 && hairMask[i])
                    c = Color.HSVToRGB(hairHue, HairSat[hair], Mathf.Clamp01(HairVal[hair] * val[i] / BaseHairValue));
                else if (skin > 0 && skinMask[i])
                    c = Color.HSVToRGB(skinHue, Mathf.Clamp01(skinS * sat[i] / BaseSkinSat), Mathf.Clamp01(skinV * val[i] / BaseSkinVal));
                else continue;
                px[i] = new Color32((byte)(c.r * 255f + 0.5f), (byte)(c.g * 255f + 0.5f), (byte)(c.b * 255f + 0.5f), px[i].a);
            }
        }
    }
}
