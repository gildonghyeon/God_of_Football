using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 선수 외형(머리색, 피부색, 유니폼). 원본 캐릭터 그림에서 머리카락·피부·유니폼 영역을 찾아 색만 바꿉니다.
    // 머리 모양은 그림 자체이므로 바뀌지 않습니다. 머리색·피부색은 아래 표에, 유니폼은 Kits 목록(기본 몇 벌 + TeamKits의 팀 유니폼)에 있습니다.
    public static class PlayerLook
    {
        public static readonly string[] HairNames = { "갈색", "검정", "밤색", "금발", "빨강", "은색", "파랑" };
        public static readonly string[] SkinNames = { "밝은 톤", "황갈색", "갈색", "어두운 톤" };
        // 축구화 색. 첫 번째(원본 검정)는 바꾸지 않습니다.
        public static readonly string[] BootNames = { "검정", "흰색", "빨강", "파랑", "노랑", "형광", "주황", "금색", "분홍", "하늘" };
        static readonly Color32[] BootRgb =
        {
            new Color32(0, 0, 0, 255), new Color32(246, 246, 246, 255), new Color32(222, 40, 44, 255), new Color32(40, 104, 232, 255),
            new Color32(250, 214, 40, 255), new Color32(168, 255, 56, 255), new Color32(255, 132, 30, 255), new Color32(226, 182, 64, 255),
            new Color32(255, 112, 182, 255), new Color32(70, 214, 240, 255)
        };
        public static int BootCount { get { return BootNames.Length; } }

        // 머리색: 색상(도), 채도, 기준 밝기. 첫 번째(원본)는 바꾸지 않습니다.
        static readonly float[] HairHue = { 0f, 20f, 22f, 44f, 13f, 220f, 214f };
        static readonly float[] HairSat = { 0f, .18f, .62f, .55f, .80f, .06f, .78f };
        static readonly float[] HairVal = { 0f, .10f, .36f, .95f, .66f, .88f, .62f };
        // 피부색(기준 색). 첫 번째(원본)는 바꾸지 않습니다.
        static readonly Color32[] SkinRgb =
        {
            new Color32(0, 0, 0, 255), new Color32(222, 168, 122, 255), new Color32(170, 116, 78, 255), new Color32(116, 74, 50, 255)
        };

        // 유니폼 목록. 0번(null)은 원래 그림 그대로, 그다음은 팀마다 홈·원정 유니폼 (TeamKits)
        static readonly Color32 None = new Color32(0, 0, 0, 0);
        public static readonly List<KitDesign> Kits = BuildKits();
        static List<KitDesign> BuildKits()
        {
            var list = new List<KitDesign> { null };
            TeamKits.AddTo(list);
            return list;
        }
        public static void EnsureKits() { if (Kits == null) throw new System.InvalidOperationException(); }   // 목록을 먼저 만들게 하는 용도
        public static KitDesign Kit(int i) { return i > 0 && i < Kits.Count ? Kits[i] : null; }

        const float BaseHairValue = 0.22f;                    // 원본 머리카락의 보통 밝기
        const float HairEdgeShade = 0.6f;                     // 피부와 맞닿은 머리 가장자리의 밝기 배율
        const float BaseSkinSat = 0.28f, BaseSkinVal = 0.98f; // 원본 피부의 채도와 밝기
        const float BaseShirtSat = 0.80f, BaseShirtVal = 0.60f; // 원본 파란 상의의 채도와 밝기
        const float BaseWhiteVal = 0.97f;                     // 원본 흰 하의의 밝기

        // 유니폼 부위
        const byte PartNone = 0, PartShirt = 1, PartSocks = 2, PartShorts = 3, PartTrim = 4, PartNeutral = 5;

        public static int HairCount { get { return HairNames.Length; } }
        public static int SkinCount { get { return SkinNames.Length; } }
        public static int KitCount { get { return Kits.Count; } }

        // 그림 데이터(png 파일 내용)를 읽어서 색을 바꾼 질감을 만듭니다. 쓰고 나면 Destroy로 지워 주세요.
        public static Texture2D LoadRecolored(byte[] pngBytes, int hair, int skin, int kit, int boots = 0)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(pngBytes)) { Object.Destroy(tex); return null; }
            if (hair > 0 || skin > 0 || kit > 0 || boots > 0)
            {
                var pixels = tex.GetPixels32();
                Recolor(pixels, tex.width, tex.height, hair, skin, kit, boots);
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static void Recolor(Color32[] px, int w, int h, int hair, int skin, int kit, int boots = 0)
        {
            hair = Mathf.Clamp(hair, 0, HairCount - 1);
            skin = Mathf.Clamp(skin, 0, SkinCount - 1);
            kit = Mathf.Clamp(kit, 0, KitCount - 1);
            boots = Mathf.Clamp(boots, 0, BootCount - 1);
            if (hair == 0 && skin == 0 && kit == 0 && boots == 0) return;
            int n = w * h;
            var hue = new float[n]; var sat = new float[n]; var val = new float[n];
            var opaque = new bool[n]; var hairMask = new bool[n]; var skinMask = new bool[n];

            // 1) 머리카락과 피부 색을 가진 픽셀 찾기
            for (int i = 0; i < n; i++)
            {
                if (px[i].a < 128) continue;
                opaque[i] = true;
                float hh, s, v;
                Color.RGBToHSV((Color)px[i], out hh, out s, out v);
                float deg = hh * 360f;
                hue[i] = deg; sat[i] = s; val[i] = v;
                bool warm = deg >= 335f || deg <= 40f;
                // 달리기 뒷모습 등의 머리 음영은 보랏빛 갈색(300° 근처)이라 범위를 넓힙니다. 채도가 아주 낮은 것은 윤곽선이 아닐 만큼 밝을 때만.
                bool hairTone = warm || deg >= 300f;
                hairMask[i] = hairTone && (s >= 0.12f || (s >= 0.08f && v >= 0.14f)) && s <= 0.65f && v >= 0.045f && v <= 0.48f;
                skinMask[i] = (deg >= 335f || (deg >= 6f && deg <= 40f)) && s >= 0.10f && s <= 0.72f && v >= 0.55f;
            }

            // 머리카락은 머리 위의 큰 덩어리만 남깁니다. 목·팔·가랑이의 작은 조각은 피부 그림자입니다.
            var hairParts = Components(hairMask, w, h, out _);
            int biggestHair = 0;
            foreach (var c in hairParts) biggestHair = Mathf.Max(biggestHair, c.Count);
            foreach (var c in hairParts)
                if (c.Count < biggestHair * 0.1f)
                    foreach (int j in c) hairMask[j] = false;

            if (hair > 0)
            {
                // 2) 머리카락 사이에 섞인 어두운 그림자 점도 머리로 포함 (바깥 윤곽선은 제외)
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
                            if (CountAround(hairMask, w, h, x, y) >= 9) add.Add(i);
                        }
                    if (add.Count == 0) break;
                    for (int k = 0; k < add.Count; k++) hairMask[add[k]] = true;
                }

                // 3) 피부에 둘러싸인 눈썹·속눈썹은 머리색을 바꾸지 않고 피부 음영으로 칠합니다. (은발일 때 눈이 하얗게 되는 것 방지)
                add.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (hairMask[y * w + x] && CountAround(skinMask, w, h, x, y) >= 8) add.Add(y * w + x);
                foreach (int i in add) MoveToSkin(i, hue, hairMask, skinMask);

                // 4) 헤어라인: 얼굴 피부가 시작되는 높이보다 아래로 내려온 앞머리 끝·속눈썹은 머리색을 바꾸지 않습니다.
                //    (그림 데이터는 아래 줄부터 저장되므로 y가 클수록 위쪽입니다.)
                RemoveBelowHairline(w, h, hue, hairMask, skinMask);

                // 5) 머리와 피부 경계의 주황빛 픽셀은 피부 음영입니다.
                add.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (hairMask[i] && sat[i] >= 0.45f && val[i] >= 0.33f && HasNear(skinMask, w, h, x, y, 2)) add.Add(i);
                    }
                foreach (int i in add) { hairMask[i] = false; skinMask[i] = true; }
            }

            // 6) 피부 그림자와 밝은 반사광도 피부로 포함 (어두운 피부에서 밝은 점이 남지 않게)
            if (skin > 0)
            {
                // 흰 덩어리: 주변 절반 이상이 피부면 얼굴의 반사광(눈 옆·코의 흰 칸)이라 피부로 칠하고,
                // 그렇지 않은 큰 덩어리(하의·양말·공)는 피부 반사광이 아닙니다.
                var whiteish = new bool[n];
                for (int i = 0; i < n; i++) whiteish[i] = opaque[i] && sat[i] <= 0.15f && val[i] >= 0.6f;
                var bigWhite = new bool[n];
                var whiteParts = Components(whiteish, w, h, out int[] whiteLabel);
                for (int ci = 0; ci < whiteParts.Count; ci++)
                {
                    var c = whiteParts[ci];
                    int around = 0, skinAround = 0;
                    foreach (int j in c)
                    {
                        int x = j % w, y = j / w;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                                int k = ny * w + nx;
                                if (whiteLabel[k] == ci || !opaque[k]) continue;
                                around++;
                                if (skinMask[k]) skinAround++;
                            }
                    }
                    if (around > 0 && skinAround >= around * 0.5f)
                    {
                        foreach (int j in c) if (!hairMask[j]) skinMask[j] = true;
                    }
                    else if (c.Count >= 20) foreach (int j in c) bigWhite[j] = true;
                }

                var add = new List<int>();
                for (int pass = 0; pass < 2; pass++)
                {
                    add.Clear();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * w + x;
                            if (!opaque[i] || skinMask[i] || hairMask[i]) continue;
                            bool shadow = (hue[i] >= 335f || hue[i] <= 40f) && val[i] >= 0.25f && sat[i] >= 0.1f && sat[i] <= 0.85f;
                            // 옆모습 코·볼의 반사광은 큰 덩어리여도 노란빛이 돌아서 하의의 흰색과 구별됩니다.
                            bool warmWhite = (hue[i] <= 60f || hue[i] >= 335f) && sat[i] >= 0.05f;
                            bool light = sat[i] < 0.25f && val[i] >= 0.75f && (!bigWhite[i] || warmWhite);
                            if ((shadow || light) && CountAround(skinMask, w, h, x, y) >= 5) add.Add(i);
                        }
                    if (add.Count == 0) break;
                    for (int k = 0; k < add.Count; k++) skinMask[add[k]] = true;
                }
            }

            // 7) 유니폼 부위 나누기
            var design = Kit(kit);
            byte[] part = design != null || boots > 0 ? FindKitParts(w, h, hue, sat, val, opaque, hairMask, skinMask) : null;
            bool[] bootMask = boots > 0 ? FindBoots(w, h, sat, val, opaque, hairMask, skinMask, part) : null;
            // 축구화 부위: 1 윗부분(밝게), 2 밑창(맨 아래 줄, 어둡게), 3 발목(양말 맨 아래 줄을 축구화 색으로 덮어 발목까지 올라오게)
            byte[] bootPart = bootMask != null ? BootParts(w, h, bootMask, opaque, val, sat, skinMask) : null;
            float bootHue = 0f, bootS = 0f, bootV = 0f;
            if (boots > 0) Color.RGBToHSV((Color)BootRgb[boots], out bootHue, out bootS, out bootV);
            if (design == null) part = null;   // 유니폼은 그대로 (축구화를 찾는 데만 씀)
            // 상의가 차지하는 범위 (어깨띠·반반 무늬의 기준)
            var shirtBox = new RectInt(0, 0, w, h);
            if (part != null)
            {
                int x0 = w, y0 = h, x1 = -1, y1 = -1;
                for (int i = 0; i < n; i++)
                    if (part[i] == PartShirt) { int x = i % w, y = i / w; x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                if (x1 >= x0) shirtBox = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }

            // 8) 색 바꾸기: 밝고 어두운 정도(음영)는 그대로 두고 색만 바꿉니다.
            //    피부와 맞닿은 머리 가장자리는 조금 어둡게 해서 밝은 머리색이 경계선처럼 튀지 않게 합니다.
            float skinHue = 0f, skinS = 0f, skinV = 0f;
            if (skin > 0) Color.RGBToHSV((Color)SkinRgb[skin], out skinHue, out skinS, out skinV);
            float hairHue = HairHue[hair] / 360f;
            for (int i = 0; i < n; i++)
            {
                if (!opaque[i]) continue;
                Color c;
                if (hair > 0 && hairMask[i])
                {
                    float edgeShade = HasNear(skinMask, w, h, i % w, i / w, 1) ? HairEdgeShade : 1f;
                    c = Color.HSVToRGB(hairHue, HairSat[hair], Mathf.Clamp01(HairVal[hair] * val[i] / BaseHairValue) * edgeShade);
                }
                else if (skin > 0 && skinMask[i])
                    c = Color.HSVToRGB(skinHue, Mathf.Clamp01(skinS * sat[i] / BaseSkinSat), Mathf.Clamp01(skinV * val[i] / BaseSkinVal));
                else if (bootPart != null && bootPart[i] != 0)
                {
                    // 윗부분은 고르게 밝게 (원래 음영은 살짝만), 밑창은 어둡게, 발목은 윗부분보다 조금 밝게
                    float shade = bootPart[i] == 2 ? 0.38f
                                : bootPart[i] == 3 ? 1f
                                : Mathf.Lerp(0.84f, 1f, Mathf.Clamp01(val[i] / 0.18f));
                    float s2 = bootPart[i] == 2 ? Mathf.Min(bootS, 0.5f) : bootS;
                    c = Color.HSVToRGB(bootHue, s2, Mathf.Clamp01(bootV * shade));
                }
                else if (part != null && part[i] != PartNone)
                {
                    Color32 target;
                    switch (part[i])
                    {
                        case PartShirt:
                            target = design.ShirtAt(i % w, i / w, shirtBox);
                            c = Tint(target, sat[i], val[i], BaseShirtSat, BaseShirtVal); break;
                        case PartSocks: c = Tint(design.socks, sat[i], val[i], BaseShirtSat, BaseShirtVal); break;
                        case PartShorts: c = Tint(design.shorts, 0f, val[i], 0f, BaseWhiteVal); break;
                        case PartNeutral: c = new Color(val[i], val[i], val[i]); break;
                        default:
                            if (design.trim.a == 0) continue;
                            c = Tint(design.trim, 0f, val[i], 0f, BaseWhiteVal); break;
                    }
                }
                else continue;
                px[i] = new Color32((byte)(c.r * 255f + 0.5f), (byte)(c.g * 255f + 0.5f), (byte)(c.b * 255f + 0.5f), px[i].a);
            }
        }

        // 원본의 파란 부분은 상의(큰 덩어리와 그 근처)와 양말 띠(멀리 떨어진 작은 덩어리)로,
        // 흰 부분은 등번호·장식(상의에 둘러싸임)과 하의(상의에 붙은 큰 덩어리)로 나눕니다. 공처럼 상의에 닿지 않는 흰 덩어리는 그대로 둡니다.
        static byte[] FindKitParts(int w, int h, float[] hue, float[] sat, float[] val, bool[] opaque, bool[] hairMask, bool[] skinMask)
        {
            int n = w * h;
            var part = new byte[n];

            var blue = new bool[n];
            for (int i = 0; i < n; i++)
                blue[i] = opaque[i] && hue[i] >= 195f && hue[i] <= 250f && sat[i] >= 0.35f && val[i] >= 0.2f;   // 더 어두운 것은 윤곽선
            var blueParts = Components(blue, w, h, out _);
            int biggest = 0;
            foreach (var c in blueParts) biggest = Mathf.Max(biggest, c.Count);
            var nearMain = new bool[n];   // 큰 덩어리에서 4픽셀 안쪽
            foreach (var c in blueParts)
            {
                if (c.Count < biggest * 0.25f) continue;
                foreach (int j in c)
                {
                    int x = j % w, y = j / w;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h) nearMain[ny * w + nx] = true;
                        }
                }
            }
            foreach (var c in blueParts)
            {
                bool shirt = c.Count >= biggest * 0.25f;
                if (!shirt) foreach (int j in c) if (nearMain[j]) { shirt = true; break; }
                if (!shirt)
                {
                    // 윤곽선에 섞인 작은 점이나 어두운 줄은 양말이 아닙니다. (진짜 양말 띠는 더 밝습니다)
                    float brightest = 0f;
                    foreach (int j in c) brightest = Mathf.Max(brightest, val[j]);
                    if (c.Count < 4 || brightest < 0.35f) continue;
                }
                foreach (int j in c) part[j] = shirt ? PartShirt : PartSocks;
            }

            // 상의에 둘러싸인 옅은 파란 반사광도 상의입니다.
            var add = new List<int>();
            for (int pass = 0; pass < 2; pass++)
            {
                add.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!opaque[i] || part[i] != PartNone || hue[i] < 195f || hue[i] > 260f || sat[i] < 0.15f || val[i] < 0.14f) continue;
                        if (CountNear(part, PartShirt, w, h, x, y) >= 4) add.Add(i);
                    }
                if (add.Count == 0) break;
                foreach (int i in add) part[i] = PartShirt;
            }

            // 흰 부분: 하의의 옅은 회청색 음영까지 포함합니다.
            var white = new bool[n];
            for (int i = 0; i < n; i++)
                white[i] = opaque[i] && part[i] == PartNone && val[i] >= 0.6f && !skinMask[i] && !hairMask[i]
                        && (sat[i] <= 0.15f || (hue[i] >= 195f && hue[i] <= 260f && sat[i] <= 0.3f));
            var whiteParts = Components(white, w, h, out int[] label);
            var shirtShare = new float[whiteParts.Count];
            int biggestLoose = 0;   // 상의에 둘러싸이지 않은 흰 덩어리 중 가장 큰 것
            for (int ci = 0; ci < whiteParts.Count; ci++)
            {
                int around = 0, shirtAround = 0;
                foreach (int j in whiteParts[ci])
                {
                    int x = j % w, y = j / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int k = ny * w + nx;
                            if (label[k] == ci) continue;
                            around++;
                            if (part[k] == PartShirt) shirtAround++;
                        }
                }
                shirtShare[ci] = shirtAround / (float)Mathf.Max(1, around);
                if (shirtShare[ci] < 0.5f) biggestLoose = Mathf.Max(biggestLoose, whiteParts[ci].Count);
            }
            for (int ci = 0; ci < whiteParts.Count; ci++)
            {
                byte p;
                if (shirtShare[ci] >= 0.5f) p = whiteParts[ci].Count >= 6 ? PartTrim : PartShirt;   // 아주 작은 것은 상의의 반사광
                else if (biggestLoose >= 40 && whiteParts[ci].Count >= biggestLoose * 0.3f && shirtShare[ci] > 0f) p = PartShorts;
                else continue;
                foreach (int j in whiteParts[ci]) part[j] = p;
            }

            // 주름선 때문에 떨어진 하의 조각: 하의에서 2픽셀 안쪽의 흰 덩어리도 하의입니다.
            for (int pass = 0; pass < 2; pass++)
            {
                bool changed = false;
                foreach (var c in whiteParts)
                {
                    if (part[c[0]] != PartNone) continue;
                    bool near = false;
                    foreach (int j in c)
                        if (HasNear(part, PartShorts, w, h, j % w, j / w, 2)) { near = true; break; }
                    if (!near) continue;
                    foreach (int j in c) part[j] = PartShorts;
                    changed = true;
                }
                if (!changed) break;
            }

            // 하의에 붙은 작은 파란 조각은 양말이 아니라 하의의 음영입니다.
            foreach (var c in blueParts)
            {
                if (part[c[0]] != PartSocks) continue;
                bool touch = false;
                foreach (int j in c)
                    if (HasNear(part, PartShorts, w, h, j % w, j / w, 1)) { touch = true; break; }
                if (touch) foreach (int j in c) part[j] = PartShorts;
            }

            // 하의 가장자리의 회색 음영도 하의로 칠합니다.
            for (int pass = 0; pass < 4; pass++)
            {
                add.Clear();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (!opaque[i] || part[i] != PartNone || skinMask[i] || hairMask[i] || sat[i] > 0.5f || val[i] < 0.25f) continue;
                        if (CountNear(part, PartShorts, w, h, x, y) >= 3) add.Add(i);
                    }
                if (add.Count == 0) break;
                foreach (int i in add) part[i] = PartShorts;
            }

            // 유니폼 근처에 남은 회청색 음영(양말 흰 부분 등)은 파란빛만 빼서 무채색으로 둡니다.
            add.Clear();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!opaque[i] || part[i] != PartNone || skinMask[i] || hairMask[i]) continue;
                    if (hue[i] < 195f || hue[i] > 260f || sat[i] <= 0.15f || sat[i] > 0.5f || val[i] < 0.25f) continue;
                    if (HasNear(part, PartShirt, w, h, x, y, 3) || HasNear(part, PartSocks, w, h, x, y, 3) || HasNear(part, PartShorts, w, h, x, y, 3))
                        add.Add(i);
                }
            foreach (int i in add) part[i] = PartNeutral;
            return part;
        }

        // 주변 3x3 칸 중 부위가 p인 칸 수
        static int CountNear(byte[] part, byte p, int w, int h, int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && part[ny * w + nx] == p) count++;
                }
            return count;
        }

        // 반경 r 안에 부위가 p인 칸이 있는가
        static bool HasNear(byte[] part, byte p, int w, int h, int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && part[ny * w + nx] == p) return true;
                }
            return false;
        }

        // 머리카락에서 빼낸 픽셀: 따뜻한 색이면 피부 음영으로 칠합니다.
        static void MoveToSkin(int i, float[] hue, bool[] hairMask, bool[] skinMask)
        {
            hairMask[i] = false;
            if (hue[i] >= 335f || hue[i] <= 40f) skinMask[i] = true;
        }

        // 머리에 닿은 피부(얼굴·귀)의 열마다 가장 위쪽 높이를 헤어라인으로 보고, 그보다 아래에서 피부 옆에 있는 머리 픽셀을 뺍니다.
        // 뒷모습처럼 보이는 피부가 머리에 비해 적으면 하지 않습니다.
        static void RemoveBelowHairline(int w, int h, float[] hue, bool[] hairMask, bool[] skinMask)
        {
            int n = w * h, hairCount = 0;
            for (int i = 0; i < n; i++) if (hairMask[i]) hairCount++;
            var top = new int[w];
            for (int x = 0; x < w; x++) top[x] = -1;
            int faceCount = 0;
            foreach (var c in Components(skinMask, w, h, out _))
            {
                bool touchesHair = false;
                foreach (int j in c)
                    if (HasNear(hairMask, w, h, j % w, j / w, 1)) { touchesHair = true; break; }
                if (!touchesHair) continue;
                faceCount += c.Count;
                foreach (int j in c) top[j % w] = Mathf.Max(top[j % w], j / w);
            }
            if (faceCount == 0 || faceCount < hairCount * 0.3f) return;

            const int Window = 4;   // 눈 때문에 끊긴 얼굴 윗선을 옆 열과 이어 줍니다.
            var line = new int[w];
            for (int x = 0; x < w; x++)
            {
                line[x] = -1;
                for (int k = Mathf.Max(0, x - Window); k <= Mathf.Min(w - 1, x + Window); k++) line[x] = Mathf.Max(line[x], top[k]);
            }
            var remove = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (hairMask[i] && y < line[x] && HasNear(skinMask, w, h, x, y, 2)) remove.Add(i);
                }
            foreach (int i in remove) MoveToSkin(i, hue, hairMask, skinMask);
        }

        // 반경 r 안에 mask에 속한 칸이 있는가
        static bool HasNear(bool[] mask, int w, int h, int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && mask[ny * w + nx]) return true;
                }
            return false;
        }

        // 8방향으로 이어진 덩어리 목록. label에는 픽셀마다 덩어리 번호(없으면 -1)를 넣습니다.
        // 축구화: 양말 바로 아래의 어두운 칸. 바깥 윤곽선(투명과 맞닿은 칸)은 검정 그대로 두고 안쪽만 칠합니다.
        //  1) 양말 = 양말 띠 + 다리(피부)·양말 띠·하의에 붙은 흰 칸 (공의 흰색은 다리에 닿지 않아 빠짐)
        //  2) 어두운 칸에서 같은 열을 위로 따라가 6칸 안에 처음 만나는 밝은 칸이 양말이고,
        //     아래로 따라가면 6칸 안에 그림 바깥(발바닥 밑)에 닿아야 축구화
        //     (다리 사이 그림자는 위로 가면 하의를, 공의 무늬는 공의 흰색을, 양말 띠의 그늘은 아래로 흰 양말을 만나서 빠짐)
        //  3) 옆모습의 앞코처럼 양말 바로 아래가 아닌 부분은 축구화 칸 옆으로 몇 칸 넓힘
        static bool[] FindBoots(int w, int h, float[] sat, float[] val, bool[] opaque, bool[] hairMask, bool[] skinMask, byte[] part)
        {
            int n = w * h;
            var boot = new bool[n];
            if (part == null) return boot;
            int minY = h, maxY = -1;
            for (int i = 0; i < n; i++) if (opaque[i]) { int y = i / w; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            if (maxY < 0) return boot;
            float lowLimit = minY + (maxY - minY) * 0.45f;   // 그림 데이터는 아래 줄부터: y가 작을수록 아래

            // 1) 양말
            var light = new bool[n];
            for (int i = 0; i < n; i++)
                light[i] = opaque[i] && !skinMask[i] && (part[i] == PartNone || part[i] == PartNeutral) && val[i] > 0.4f && sat[i] < 0.38f;   // 그늘진 먼 쪽 다리의 회색 양말 포함
            var sock = new bool[n];
            for (int i = 0; i < n; i++) sock[i] = part[i] == PartSocks;
            var lightParts = Components(light, w, h, out _);
            foreach (var c in lightParts)
            {
                bool touches = false;
                foreach (int j in c)
                {
                    int x = j % w, y = j / w;
                    for (int dy = -1; dy <= 1 && !touches; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int k = ny * w + nx;
                            if (part[k] == PartSocks || part[k] == PartShorts || skinMask[k]) { touches = true; break; }
                        }
                    if (touches) break;
                }
                if (touches) foreach (int j in c) sock[j] = true;
            }

            // 안쪽의 어두운 칸
            var dark = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!opaque[i] || hairMask[i] || skinMask[i] || val[i] >= 0.24f || y > lowLimit || part[i] == PartShirt) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                             || !opaque[i - 1] || !opaque[i + 1] || !opaque[i - w] || !opaque[i + w];
                    if (!edge) dark[i] = true;
                }

            // 축구화는 다리 아래에만: 같은 열(또는 바로 옆 열) 위쪽 10칸 안에 양말이 있어야 함
            // (다리 사이의 어두운 틈이나 다리 바깥의 두꺼운 윤곽선은 위에 양말이 없어서 빠짐)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!dark[i]) continue;
                    bool under = false;
                    for (int dx = -1; dx <= 1 && !under; dx++)
                    {
                        int cx = x + dx;
                        if (cx < 0 || cx >= w) continue;
                        for (int k = 1; k <= 10 && y + k < h; k++)
                            if (sock[(y + k) * w + cx]) { under = true; break; }
                    }
                    if (!under) dark[i] = false;
                }

            // 2) 시작: 같은 열 바로 위(어두운 칸만 지나 4칸 안)가 양말이고, 아래로 어두운 칸만 지나 8칸 안에 발바닥 밑(그림 바깥)이 있는 칸
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (dark[i] && TallOk(x, y, w, h, opaque, val, sock) && Sole(x, y, w, h, opaque, val, 8)) boot[i] = true;
                }

            // 3) 넓히기: 옆(앞코·뒤꿈치)과 아래(발바닥 쪽)로만. 위로는 넓히지 않아 다리 가장자리를 타고 올라가지 않음
            //    (위로 번지면 신발 앞쪽이 들린 구두처럼 보임)
            for (int pass = 0; pass < 12; pass++)
            {
                var add = new List<int>();
                for (int y = 0; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        int i = y * w + x;
                        if (!dark[i] || boot[i]) continue;
                        if (!(boot[i - 1] || boot[i + 1] || boot[i + w])) continue;   // 왼쪽·오른쪽·바로 위 칸이 축구화 (그림 데이터는 y+1이 위)
                        if (Sole(x, y, w, h, opaque, val, 8)) add.Add(i);
                    }
                if (add.Count == 0) break;
                foreach (int i in add) boot[i] = true;
            }

            // 4) 축구화 모양이 아닌 조각은 지움.
            //    땅에 닿은 발(인물 맨 아래에서 6줄 안)은 그대로 두고, 들린 발은 다리 아래쪽 30% 안에서
            //    옆으로 넓고 꽉 찬 덩어리만 축구화로 봄 (들린 다리를 따라 생긴 가늘거나 비스듬한 획은 지움)
            float liftLimit = minY + (maxY - minY) * 0.3f;
            foreach (var c in Components(boot, w, h, out _))
            {
                int y0 = h, y1 = -1, x0 = w, x1 = -1;
                foreach (int j in c) { int y = j / w, x = j % w; y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); }
                int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
                bool keep;
                if (bh < 3 || bw < 3) keep = false;                                         // 한두 줄짜리 윤곽선
                else if (y0 - minY <= 6) keep = true;                                       // 땅에 닿은 발
                else keep = y0 <= liftLimit && bw >= 4 && bw >= bh && c.Count >= 0.55f * bw * bh;   // 들린 발
                if (!keep) foreach (int j in c) boot[j] = false;
            }
            return boot;
        }

        // 축구화를 윗부분·밑창·발목으로 나눕니다.
        //  밑창: 축구화 칸 중 바로 아래가 그림 바깥이거나 윤곽선인 칸 (발바닥 줄)
        //  발목: 축구화 맨 위 칸 바로 위(또는 윤곽선 한 줄 위)의 양말 칸
        static byte[] BootParts(int w, int h, bool[] boot, bool[] opaque, float[] val, float[] sat, bool[] skinMask)
        {
            int n = w * h;
            var part = new byte[n];
            for (int i = 0; i < n; i++) if (boot[i]) part[i] = 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!boot[i]) continue;
                    // 바로 아래(그림 데이터는 y-1이 아래)가 축구화가 아니면 밑창
                    if (y == 0 || !boot[i - w]) part[i] = 2;
                    // 바로 위가 축구화가 아니면, 그 위의 양말 한 줄을 발목으로
                    if (y + 1 < h && !boot[i + w])
                    {
                        // 2칸 안에 흰 양말이 바로 위에 있을 때만: 사이 윤곽선은 축구화 윗부분, 양말 맨 아래 줄은 발목
                        int found = 0;
                        for (int k = 1; k <= 2 && y + k < h; k++)
                        {
                            int j = (y + k) * w + x;
                            if (!opaque[j] || skinMask[j]) break;
                            if (val[j] > 0.4f && sat[j] < 0.38f) { found = k; break; }
                            if (val[j] >= 0.24f) break;
                        }
                        if (found > 0)
                        {
                            for (int k = 1; k < found; k++) part[(y + k) * w + x] = 1;
                            part[(y + found) * w + x] = 3;
                        }
                    }
                }
            // 밑창이 축구화의 거의 전부인 얇은 칸(한 줄짜리)은 윗부분으로 (밑창만 보이면 슬리퍼처럼 보임)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (part[i] == 2 && (y + 1 >= h || part[i + w] == 0 || part[i + w] == 3)) part[i] = 1;
                }
            return part;
        }

        // 아래로 어두운 칸만 지나 6칸 안에 그림 바깥에 닿는지 (발바닥 쪽인지)
        // 같은 열 바로 위(어두운 칸만 지나 4칸 안)가 양말인지: 양말 바로 밑의 축구화 몸통 열
        static bool TallOk(int x, int y, int w, int h, bool[] opaque, float[] val, bool[] sock)
        {
            for (int k = 1; k <= 4 && y + k < h; k++)
            {
                int j = (y + k) * w + x;
                if (!opaque[j]) return false;
                if (val[j] < 0.24f) continue;
                return sock[j];
            }
            return false;
        }

        static bool Sole(int x, int y, int w, int h, bool[] opaque, float[] val, int limit = 5)
        {
            for (int k = 1; k <= limit; k++)
            {
                if (y - k < 0) return true;
                int j = (y - k) * w + x;
                if (!opaque[j]) return true;
                if (val[j] >= 0.24f) return false;
            }
            return false;
        }

        static List<List<int>> Components(bool[] mask, int w, int h, out int[] label)
        {
            int n = w * h;
            label = new int[n];
            for (int i = 0; i < n; i++) label[i] = -1;
            var result = new List<List<int>>();
            var stack = new Stack<int>();
            for (int i = 0; i < n; i++)
            {
                if (!mask[i] || label[i] >= 0) continue;
                var comp = new List<int>();
                label[i] = result.Count; stack.Push(i);
                while (stack.Count > 0)
                {
                    int j = stack.Pop(); comp.Add(j);
                    int x = j % w, y = j / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int k = ny * w + nx;
                            if (mask[k] && label[k] < 0) { label[k] = result.Count; stack.Push(k); }
                        }
                }
                result.Add(comp);
            }
            return result;
        }

        // 주변 5x5 칸 중 mask에 속한 칸 수
        static int CountAround(bool[] mask, int w, int h, int x, int y)
        {
            int count = 0;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && mask[ny * w + nx]) count++;
                }
            return count;
        }

        // 원래 밝기(v)가 기준보다 어두운 만큼 목표 색도 어둡게 합니다. 밝은 색은 더 밝아지지 않게 둡니다.
        static Color Tint(Color32 target, float s, float v, float baseSat, float baseVal)
        {
            float th, ts, tv;
            Color.RGBToHSV((Color)target, out th, out ts, out tv);
            float r = v / baseVal;
            float nv = r < 1f ? tv * (1f - 0.85f * (1f - r)) : (tv < 0.9f ? tv * Mathf.Min(r, 1.25f) : tv);
            float ns = baseSat > 0f ? ts * s / baseSat : ts;
            return Color.HSVToRGB(th, Mathf.Clamp01(ns), Mathf.Clamp01(nv));
        }
    }
}
