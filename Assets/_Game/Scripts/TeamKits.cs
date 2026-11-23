using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 상의 무늬: 단색, 세로 줄무늬, 가로 줄무늬(후프), 어깨띠(사선), 좌우 반반
    public enum KitPattern { Solid, Stripes, Hoops, Sash, Halves }

    // 유니폼 한 벌. second는 무늬의 두 번째 색, width는 줄무늬 폭(그림 픽셀), trim은 등번호·장식(투명이면 원래 흰색)
    public class KitDesign
    {
        public string name;
        public Color32 shirt, second, shorts, socks, trim;
        public KitPattern pattern;
        public int width = 4;

        // 상의의 (x, y) 픽셀 색. box는 그 그림에서 상의가 차지하는 범위 (어깨띠·반반 무늬에 씀). y는 아래에서 위로 커짐
        public Color32 ShirtAt(int x, int y, RectInt box)
        {
            if (pattern == KitPattern.Solid || second.a == 0) return shirt;
            int w = Mathf.Max(1, width);
            switch (pattern)
            {
                case KitPattern.Stripes: return (x - box.xMin) / w % 2 == 1 ? second : shirt;
                case KitPattern.Hoops: return (box.yMax - y) / w % 2 == 1 ? second : shirt;
                case KitPattern.Halves: return x >= box.xMin + box.width / 2 ? second : shirt;
                case KitPattern.Sash:
                {
                    // 왼쪽 어깨(위 왼쪽)에서 오른쪽 허리(아래 오른쪽)로 내려가는 띠
                    float u = box.width > 0 ? (x - box.xMin) / (float)box.width : 0f;
                    float lineY = box.yMax - u * box.height;
                    return Mathf.Abs(y - lineY) <= w * 0.9f ? second : shirt;
                }
            }
            return shirt;
        }

        // 멀리서 보이는 대표 색 (색이 겹치는지 볼 때)
        public Color Average()
        {
            if (pattern == KitPattern.Solid || second.a == 0) return shirt;
            float k = pattern == KitPattern.Sash ? 0.25f : 0.5f;
            return Color.Lerp(shirt, second, k);
        }
    }

    // 팀마다 정해진 유니폼. 내 선수는 소속 팀의 홈 유니폼을 입습니다 (직접 고를 수 없음).
    // 디자인은 팀 이름이 떠올리게 하는 세리에A·프리미어리그·라리가 클럽의 색 조합과 무늬(줄무늬, 후프, 어깨띠, 반반)를 참고했습니다. 엠블럼은 쓰지 않습니다.
    // 원정 유니폼은 홈 유니폼에서 자동으로 만듭니다 (밝은 홈이면 짙은 남색, 아니면 흰색).
    public static class TeamKits
    {
        static readonly Color32 None = new Color32(0, 0, 0, 0);
        static readonly Color32 White = new Color32(244, 244, 244, 255), Black = new Color32(34, 34, 38, 255);
        static readonly Color32 Navy = new Color32(24, 36, 88, 255), Royal = new Color32(30, 74, 192, 255), Sky = new Color32(118, 188, 236, 255);
        static readonly Color32 Red = new Color32(206, 30, 42, 255), Claret = new Color32(124, 26, 52, 255), Maroon = new Color32(112, 22, 34, 255);
        static readonly Color32 Gold = new Color32(214, 170, 60, 255), Yellow = new Color32(250, 214, 32, 255), Orange = new Color32(240, 122, 30, 255);
        static readonly Color32 Green = new Color32(22, 140, 72, 255), Purple = new Color32(104, 52, 156, 255), Pink = new Color32(240, 150, 190, 255);
        static readonly Color32 Grey = new Color32(74, 80, 92, 255), Teal = new Color32(0, 128, 140, 255);
        static readonly Color32 BlauBlue = new Color32(22, 58, 150, 255), BlauRed = new Color32(150, 22, 52, 255);

        static KitDesign Kit(string name, KitPattern p, Color32 shirt, Color32 second, int width, Color32 shorts, Color32 socks, Color32 trim)
        { return new KitDesign { name = name, pattern = p, shirt = shirt, second = second, width = width, shorts = shorts, socks = socks, trim = trim }; }
        static KitDesign Solid(string name, Color32 shirt, Color32 shorts, Color32 socks, Color32 trim)
        { return Kit(name, KitPattern.Solid, shirt, None, 4, shorts, socks, trim); }

        // 팀 ID → 홈 유니폼
        static readonly (string id, KitDesign kit)[] Homes =
        {
            // 1부
            ("crownhaven",          Solid("올 화이트·금색 장식", White, White, White, Gold)),                                     // 마드리드 (흰색)
            ("kingsport-athletic",  Kit("파랑·자주 줄무늬", KitPattern.Stripes, BlauBlue, BlauRed, 5, Navy, Navy, Gold)),         // 카탈루냐 (블라우그라나)
            ("silverkeep-united",   Solid("하늘색", Sky, White, Sky, Navy)),                                                      // 맨체스터 (하늘색)
            ("goldmoor-city",       Solid("올 레드", Red, Red, Red, None)),                                                       // 머지사이드 (빨강)
            ("highspire",           Solid("빨강·흰색 하의", Red, White, White, Gold)),                                             // 북런던 (빨강·흰색)
            ("stormwatch-rovers",   Kit("파랑·검정 줄무늬", KitPattern.Stripes, Royal, Black, 3, Black, Black, Gold)),             // 밀라노 (네라주리)
            ("starfall-united",     Kit("흑백 줄무늬", KitPattern.Stripes, Black, White, 4, White, White, None)),                  // 토리노 (비앙코네리)
            ("valecrest-city",      Kit("빨강·흰색 줄무늬", KitPattern.Stripes, Red, White, 4, Navy, Red, None)),                 // 마드리드 (로히블랑코스)
            ("thunderpeak",         Kit("빨강·검정 줄무늬", KitPattern.Stripes, Red, Black, 4, White, Black, None)),               // 밀라노 (로쏘네리)
            ("aldercrown-albion",   Solid("로열 블루", Royal, Royal, White, None)),                                               // 런던 (파랑)
            // 2부
            ("northbrook",          Solid("나폴리 블루", new Color32(84, 166, 228, 255), White, new Color32(84, 166, 228, 255), None)),   // 나폴리 (아주리)
            ("eastmere-united",     Solid("빨강·검정 양말", Red, White, Black, None)),                                             // 맨체스터 (붉은 악마)
            ("westhollow-city",     Solid("흰색·남색 하의", White, Navy, White, Navy)),                                            // 북런던 (흰색)
            ("southvale-rovers",    Solid("버건디·주황 장식", new Color32(140, 26, 44, 255), White, new Color32(140, 26, 44, 255), Orange)),   // 로마 (잘로로시)
            ("oakridge-athletic",   Kit("파랑·검정 넓은 줄무늬", KitPattern.Stripes, new Color32(20, 62, 172, 255), Black, 6, Black, Black, None)),   // 베르가모
            ("pinecrest",           Solid("흰색·빨간 장식", White, White, Black, Red)),                                            // 세비야 (흰색)
            ("rivermouth-town",     Solid("셀레스테·흰색 하의", new Color32(150, 206, 242, 255), White, White, Navy)),              // 로마 (독수리)
            ("stonegate-united",    Kit("흑백 줄무늬·검정 하의", KitPattern.Stripes, Black, White, 4, Black, Black, None)),         // 타인사이드 (까치)
            ("copperton",           Solid("올 옐로", Yellow, Yellow, Yellow, Royal)),                                             // 노란 잠수함
            ("brightwater",         Solid("클라렛·하늘 양말", Claret, White, Sky, Sky)),                                           // 미들랜즈 (클라렛)
            // 3부
            ("ashgrove",            Solid("보라", Purple, White, Purple, None)),                                                   // 피렌체 (비올라)
            ("elmstead-town",       Kit("빨강·흰색 넓은 줄무늬", KitPattern.Stripes, Red, White, 5, Black, Black, None)),           // 빌바오 (사자)
            ("foxden-united",       Kit("파랑·흰색 줄무늬", KitPattern.Stripes, Royal, White, 4, White, White, None)),             // 도노스티아
            ("glenhollow-rovers",   Kit("초록·흰색 줄무늬", KitPattern.Stripes, Green, White, 4, White, Green, None)),              // 세비야 (초록·흰색)
            ("hollowmere",          Solid("그라나타", Maroon, White, Maroon, None)),                                                // 토리노 (그라나타)
            ("lakeshore-athletic",  Kit("파랑·흰색 줄무늬·남색 하의", KitPattern.Stripes, new Color32(0, 86, 180, 255), White, 5, Navy, White, None)),   // 서식스 (갈매기)
            ("millbrook-city",      Solid("클라렛·하늘 장식", new Color32(124, 26, 52, 255), White, new Color32(124, 26, 52, 255), Sky)),   // 런던 (해머스)
            ("nettlefield",         Solid("흰색·검정 하의", White, Black, Black, Orange)),                                          // 발렌시아 (박쥐)
            ("quarry-hill-united",  Kit("흰색·빨간 어깨띠", KitPattern.Sash, White, Red, 6, White, White, Red)),                    // 바예카스
            ("rosewood-town",       Kit("빨강·남색 넓은 줄무늬", KitPattern.Stripes, Red, Navy, 6, Navy, Navy, None)),              // 볼로냐 (로쏘블루)
            // 4부
            ("saltmarsh",           Solid("올드 골드", new Color32(240, 168, 30, 255), Black, new Color32(240, 168, 30, 255), Black)),   // 블랙 컨트리 (늑대)
            ("thistledown-united",  Kit("빨강·남색 반반", KitPattern.Halves, Red, Navy, 4, Navy, Red, None)),                        // 제노바 (그리포네)
            ("underhill-rovers",    Kit("노랑·남색 줄무늬", KitPattern.Stripes, Yellow, Navy, 5, Navy, Yellow, None)),              // 베로나 (스칼리제리)
            ("wheatley-green",      Kit("초록·검정 줄무늬", KitPattern.Stripes, Green, Black, 4, Black, Green, None)),              // 에밀리아 (네로베르디)
            ("yarrow-town",         Kit("빨강·남색 가는 줄무늬", KitPattern.Stripes, Red, Navy, 3, Navy, Navy, None)),             // 사르데냐
            ("brambleton",          Kit("흑백 가는 줄무늬", KitPattern.Stripes, White, Black, 3, Black, Black, None)),              // 우디네 (작은 얼룩말)
            ("cinderwick-athletic", Kit("빨강·흰색 가는 줄무늬", KitPattern.Stripes, Red, White, 3, Black, White, None)),           // 햄프셔 (세인츠)
            ("dunmore-rangers",     Kit("파랑·흰색 후프", KitPattern.Hoops, Royal, White, 5, White, White, None)),                  // 서런던 (후프스)
            ("fernvale",            Solid("셀레스테·빨간 장식", new Color32(130, 200, 240, 255), White, new Color32(130, 200, 240, 255), Red)),   // 비고
            ("graniteford-united",  Solid("올 블루", new Color32(0, 70, 172, 255), new Color32(0, 70, 172, 255), new Color32(0, 70, 172, 255), None)),   // 마드리드 남부 (아술로네스)
            // 5부
            ("red-cedar",           Solid("빨강·흰색 하의", new Color32(220, 24, 40, 255), White, new Color32(220, 24, 40, 255), None)),   // 트렌트 (숲)
            ("iron-bay",            Solid("클라렛", new Color32(110, 30, 50, 255), White, new Color32(110, 30, 50, 255), Sky)),      // 랭커셔 (클라렛)
            ("blue-ridge",          Solid("로열 블루·흰색 하의", Royal, White, White, None)),                                       // 머지사이드 (토피스)
            ("mossbank-united",     Solid("빨강·남색 하의", new Color32(190, 20, 40, 255), Navy, Navy, None)),                      // 팜플로나 (로히요스)
            ("maple-hill",          Solid("분홍·검정 하의", Pink, Black, Pink, Black)),                                             // 팔레르모풍 (로자네로)
            ("lantern-bay-rovers",  Solid("노랑·검정 하의", Yellow, Black, Yellow, Red)),                                           // 하트퍼드 (말벌)
            ("kettle-cove",         Solid("빨강·검정 하의", new Color32(200, 20, 30, 255), Black, Black, None)),                    // 발레아레스
            ("hazelbrook",          Kit("노랑·빨강 줄무늬", KitPattern.Stripes, Yellow, Red, 5, Navy, Red, None)),                 // 살렌토 (잘로로시)
            ("sunfield",            Solid("올 화이트·파란 장식", White, White, White, Royal)),                                     // 요크셔 (흰색)
            ("harbor-town",         Solid("파랑·흰색 하의", Royal, White, Royal, None)),                                           // 라리아니 (파랑)
        };

        static readonly Dictionary<string, int> homeIndex = new Dictionary<string, int>();
        static readonly Dictionary<string, int> awayIndex = new Dictionary<string, int>();

        // PlayerLook이 유니폼 목록을 만들 때 부릅니다: 팀마다 홈·원정 유니폼을 차례로 넣습니다.
        public static void AddTo(List<KitDesign> kits)
        {
            foreach (var (id, kit) in Homes)
            {
                homeIndex[id] = kits.Count; kits.Add(kit);
                awayIndex[id] = kits.Count; kits.Add(AwayOf(kit));
            }
        }

        static bool IsLight(Color c) { return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 0.72f; }

        static KitDesign AwayOf(KitDesign home)
        {
            Color main = home.Average();
            var accent = home.pattern != KitPattern.Solid && home.second.a > 0 ? (IsLight(home.shirt) ? home.second : home.shirt) : home.shirt;
            if (IsLight(main) || IsLight(home.shirt))
                return Solid(home.name + " (원정)", Navy, Navy, Navy, home.trim.a > 0 ? home.trim : (IsLight(accent) ? Gold : accent));
            return Solid(home.name + " (원정)", White, IsLight(accent) ? Navy : accent, White, accent);
        }

        // 그 팀의 홈 유니폼 번호 (PlayerLook.Kits 안). 모르는 팀이면 0 (원래 그림)
        public static int Home(string teamId)
        {
            PlayerLook.EnsureKits();
            return teamId != null && homeIndex.TryGetValue(teamId, out int i) ? i : 0;
        }

        public static int Away(string teamId)
        {
            PlayerLook.EnsureKits();
            return teamId != null && awayIndex.TryGetValue(teamId, out int i) ? i : 0;
        }

        // 상대 팀이 입을 유니폼: 우리 홈과 색이 겹치면 원정 유니폼
        public static int Opponent(string ownId, string oppId)
        {
            int own = Home(ownId), opp = Home(oppId);
            if (own == 0 || opp == 0) return opp;
            return Clash(PlayerLook.Kit(own), PlayerLook.Kit(opp)) ? Away(oppId) : opp;
        }

        static bool Clash(KitDesign a, KitDesign b)
        {
            if (a == null || b == null) return false;
            Color x = a.Average(), y = b.Average();
            if (IsLight(x) && IsLight(y)) return true;
            float d = Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b);
            return d < 0.42f;
        }

        public static string Name(string teamId)
        {
            foreach (var (id, kit) in Homes) if (id == teamId) return kit.name;
            return "";
        }

        // ---------- 유니폼 그림 (팀 정보 화면) ----------
        static readonly Dictionary<int, Sprite> previewCache = new Dictionary<int, Sprite>();

        // 정면으로 서 있는 선수에게 이 유니폼을 입힌 그림 (유니폼 번호마다 한 번만 만들어 계속 씀)
        public static Sprite Preview(int kitIndex)
        {
            if (previewCache.TryGetValue(kitIndex, out var cached) && cached != null) return cached;
            var asset = Resources.Load<TextAsset>("Art/Chars/blue_idle_front_0");
            if (asset == null) return null;
            var tex = PlayerLook.LoadRecolored(asset.bytes, 0, 0, kitIndex);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Point;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
            previewCache[kitIndex] = sprite;
            return sprite;
        }

        // parent 안의 (x0,y0)~(x1,y1) 비율 자리에 유니폼 그림을 놓습니다. 그림이 없으면 아무것도 하지 않음
        public static UnityEngine.UI.Image PreviewImage(Transform parent, int kitIndex, float x0, float y0, float x1, float y1)
        {
            var sprite = Preview(kitIndex);
            if (sprite == null) return null;
            var go = new GameObject("Kit", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return img;
        }

        // ---------- 경기 영상의 점 ----------
        // 팀 유니폼 무늬로 칠한 둥근 점 그림을 만듭니다. rim: 테두리 색(보통 흰색, 내 선수는 금색). 쓰고 나면 텍스처를 지워 주세요.
        public static Sprite Dot(int kitIndex, int size, Color32 rim, int rimWidth, List<Object> owned)
        {
            var kit = PlayerLook.Kit(kitIndex);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) / 2f, r = size / 2f - 0.5f;
            int inner = Mathf.RoundToInt(size * 0.2f), span = size - inner * 2;
            var box = new RectInt(inner, inner, span, span);
            // 점이 작아서 무늬 폭은 점 크기에 맞춰 키웁니다.
            var scaled = kit == null ? null : new KitDesign { pattern = kit.pattern, shirt = kit.shirt, second = kit.second, width = Mathf.Max(2, Mathf.RoundToInt(size / 6f)) };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r + 0.5f - d);
                    if (a <= 0f) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    Color col;
                    if (d > r - rimWidth) col = rim;
                    else
                    {
                        Color32 fill = scaled != null ? scaled.ShirtAt(x, y, box) : new Color32(46, 116, 232, 255);
                        float shade = 1.08f - 0.18f * ((x - c) - (y - c)) / Mathf.Max(1f, r);   // 왼쪽 위가 밝게
                        col = (Color)fill * Mathf.Clamp(shade, 0.8f, 1.15f);
                        // 아주 밝은 유니폼(흰색)은 테두리와 구별되게 조금 어둡게
                        if (IsLight(fill)) col *= 0.9f;
                    }
                    col.a = a * (d > r - rimWidth ? rim.a / 255f : 1f);
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            if (owned != null) { owned.Add(tex); owned.Add(sprite); }
            return sprite;
        }
    }
}
