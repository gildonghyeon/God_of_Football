using System;
using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    // 내 선수(blue)의 달리기 그림을 보강한 '달리기 동작' 20장을 만듭니다.
    // 원래 달리기 그림은 팔다리가 거의 움직이지 않아서, 다른 그림을 잘라 붙여 팔다리가 움직이게 합니다.
    //   옆모습: 드리블 그림의 상체(팔 흔들기, 숙인 자세를 살짝 세움) + 다리 벌림(달리기 그림)/모음(서 있는 그림) 교대
    //           + 먼 쪽 팔(앞으로 내민 주먹)과 가까운 쪽 팔(몸 뒤 팔뚝)을 서로 반대로 앞뒤로 움직임
    //   정면·뒷모습: 달리기 그림 + 한쪽 다리와 반대쪽 팔을 번갈아 들어 올림
    // 어느 픽셀을 어디서 가져올지는 원래 색의 그림으로 계산하고(Build), 같은 배치를 머리색·피부색·유니폼을 바꾼 그림에 적용합니다(Apply).
    // 좌표는 그림 위쪽이 y=0입니다. (Unity의 픽셀 배열은 아래 줄부터 저장되므로 읽고 쓸 때 뒤집습니다)
    public static class StrideFrames
    {
        public const int FrameCount = 20;
        public static readonly string[] Views = { "side", "front", "back" };

        const int W = 100, H = 130;
        const int Hip = 95;            // 다리가 시작되는 줄 (바지 아래)
        const int Cut = 89;            // 옆모습 이음새: 이 줄부터 아래(바지 아랫단·허벅지)는 다리 그림에서 가져옴
        const int LegLift = 3;         // 정면·뒷모습: 들어 올린 다리
        const int ArmLift = 3;         // 정면·뒷모습: 들어 올린 팔 (다리와 반대쪽)
        const int Squash = 1;          // 땅을 디딜 때 상체가 내려앉는 정도
        const int ArmTop = 64, ArmBot = 86;   // 팔이 있는 줄
        const int NearRaise = 2;       // 옆모습: 가까운 쪽 팔이 앞으로 흔들릴 때 올라가는 정도
        const float Lean = -0.02f;     // 옆모습: 기울이는 정도 (이음새에서 한 줄 올라갈 때마다). 드리블 상체가 원래 앞으로 숙여서 살짝 세웁니다.
        static readonly int[] FarSwing = { 5, -2, -9, -2 };   // 옆모습: 다리 단계별 먼 쪽 팔 위치 (음수는 몸 뒤로 숨음)

        // 필요한 원본 그림 (blue_ 뒤의 이름). Build와 Apply에서 같은 순서로 씁니다.
        public static readonly string[] Sources = MakeSources();
        static string[] MakeSources()
        {
            var list = new List<string>();
            for (int i = 0; i < 5; i++) list.Add("dribble_side_" + i);
            for (int i = 0; i < 5; i++) list.Add("run_side_" + i);
            list.Add("idle_side_0");
            for (int i = 0; i < 4; i++) list.Add("run_front_" + i);
            for (int i = 0; i < 4; i++) list.Add("run_back_" + i);
            return list.ToArray();
        }

        class Img
        {
            public Color32[] px; public int slot;
            public bool A(int x, int y) { return px[(H - 1 - y) * W + x].a > 0; }
            public Color32 P(int x, int y) { return px[(H - 1 - y) * W + x]; }
            public int Code(int x, int y) { return slot * W * H + y * W + x; }
        }

        // 원래 색 그림(이름 → 픽셀)으로 보기별 20장의 배치를 만듭니다. 그림이 없거나 크기가 다르면 null.
        public static Dictionary<string, int[][]> Build(Func<string, Color32[]> original)
        {
            var img = new Img[Sources.Length];
            for (int s = 0; s < Sources.Length; s++)
            {
                var px = original(Sources[s]);
                if (px == null || px.Length != W * H) return null;
                img[s] = new Img { px = px, slot = s };
            }
            Func<string, Img> get = name => img[Array.IndexOf(Sources, name)];
            var maps = new Dictionary<string, int[][]>();

            var side = new int[FrameCount][];
            for (int f = 0; f < FrameCount; f++)
            {
                int ph = f % 4;
                var legs = ph % 2 == 0 ? get("run_side_" + (f / 2) % 5) : get("idle_side_0");
                side[f] = Side(get("dribble_side_" + f % 5), legs, ph % 2 == 0, FarSwing[ph]);
            }
            maps["side"] = side;
            foreach (string view in new[] { "front", "back" })
            {
                var frames = new int[FrameCount][];
                for (int f = 0; f < FrameCount; f++) frames[f] = Front(get("run_" + view + "_" + f % 4), f % 4);   // 달리기 그림 4번은 다리를 옆으로 뻗어서 뺌
                maps[view] = frames;
            }
            return maps;
        }

        // 배치에 따라 색을 바꾼 그림(Sources 순서)에서 픽셀을 가져와 한 장을 만듭니다. 결과는 Unity 순서(아래 줄부터)입니다.
        public static Color32[] Apply(int[] map, Color32[][] colored)
        {
            var result = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int code = map[y * W + x];
                    if (code < 0) continue;
                    int slot = code / (W * H), rest = code % (W * H);
                    int sy = rest / W, sx = rest % W;
                    result[(H - 1 - y) * W + x] = colored[slot][(H - 1 - sy) * W + sx];
                }
            return result;
        }

        static int[] NewMap()
        {
            var m = new int[W * H];
            for (int i = 0; i < m.Length; i++) m[i] = -1;
            return m;
        }

        // ---------- 옆모습 ----------
        static int[] Side(Img up, Img legs, bool contact, int armDx)
        {
            var o = NewMap();
            int dx = ShortsCenter(up) - ShortsCenter(legs);
            int lo = 0, hi = W - 1, mn = W, mx = -1;
            for (int x = 0; x < W; x++) if (up.A(x, Cut - 1)) { mn = Mathf.Min(mn, x); mx = Mathf.Max(mx, x); }
            if (mx >= 0) { lo = mn - 1; hi = mx + 1; }
            for (int y = Cut; y < H; y++)                       // 다리
                for (int x = 0; x < W; x++)
                {
                    int sx = x - dx;
                    if (sx < 0 || sx >= W || !legs.A(sx, y)) continue;
                    if (y < Cut + 3 && (x < lo || x > hi)) continue;   // 바지 폭 밖으로 삐져나온 윤곽선
                    o[y * W + x] = legs.Code(sx, y);
                }
            int drop = contact ? Squash : 0;
            var far = FarArm(up); var stub = ThighStub(up, far); var near = NearArm(up, far);
            int nearDx = armDx == 5 ? -3 : (armDx == -9 ? 6 : 0);   // 가까운 쪽 팔은 먼 쪽 팔과 반대로
            Paint(o, up, i => far[i], armDx, armDx > 0, 0, drop, stub);                              // 먼 쪽 팔: 몸 뒤 (왼쪽이 몸에 붙음)
            Paint(o, up, i => !far[i] && !near[i], 0, false, 0, drop, stub);                         // 몸
            Paint(o, up, i => near[i], nearDx, nearDx < 0, nearDx > 0 ? NearRaise : 0, drop, stub);  // 가까운 쪽 팔: 몸 앞 (오른쪽이 몸에 붙음)
            return o;
        }

        // 상체를 앞으로 기울여 그립니다. stretch: 팔이 몸에서 멀어질 때 0부터 shift까지 모두 그려 어깨 쪽 틈을 메웁니다.
        static void Paint(int[] o, Img up, Func<int, bool> mask, int shift, bool stretch, int rise, int drop, bool[] stub)
        {
            int step = shift >= 0 ? 1 : -1;
            int first = stretch ? 0 : shift;
            for (int off = first; ; off += step)
            {
                for (int y = 0; y < Cut; y++)
                {
                    int lean = Mathf.RoundToInt((Cut - y) * Lean);   // 원본 그림은 오른쪽을 봅니다
                    for (int x = 0; x < W; x++)
                    {
                        int i = y * W + x;
                        if (!up.A(x, y) || stub[i] || !mask(i)) continue;
                        int nx = x + lean + off, ny = y + drop - rise;
                        if (nx >= 0 && nx < W && ny >= 0 && ny < H) o[ny * W + nx] = up.Code(x, y);
                    }
                }
                if (off == shift) break;
            }
        }

        // 몸통 오른쪽 윤곽선 바깥의 팔 줄: 앞으로 내민 먼 쪽 팔(주먹)
        static bool[] FarArm(Img im)
        {
            var m = new bool[W * H];
            int edge = ShirtEdge(im);
            if (edge < 0) return m;
            int top = -1, bottom = -1;
            for (int y = ArmTop; y < ArmBot; y++)
                for (int x = edge + 3; x < W; x++)
                    if (IsSkin(im, x, y)) { if (top < 0) top = y; bottom = y; break; }
            if (top < 0) return m;
            for (int y = top - 2; y < bottom + 3; y++)          // 주먹과 그 윤곽선
                for (int x = edge + 3; x < W; x++)              // 몸통 윤곽선 2px는 남김
                    if (im.A(x, y)) m[y * W + x] = true;
            return m;
        }

        // 드리블 그림은 공을 차려고 앞쪽 허벅지를 들어서, 그 윤곽선이 이음새 위 몸 오른쪽에 삐져나옵니다.
        static bool[] ThighStub(Img im, bool[] far)
        {
            var m = new bool[W * H];
            int edge = ShirtEdge(im);
            if (edge < 0) return m;
            for (int y = ArmBot - 6; y < Cut; y++)
                for (int x = edge + 3; x < W; x++)
                    if (im.A(x, y) && !far[y * W + x]) m[y * W + x] = true;
            return m;
        }

        // 몸 뒤쪽(원본 그림의 왼쪽) 팔뚝과 손, 그 윤곽선
        static bool[] NearArm(Img im, bool[] far)
        {
            var m = new bool[W * H];
            int c = ShortsCenter(im);
            int x0 = W, x1 = -1, y0 = H, y1 = -1;
            for (int y = ArmTop; y < ArmBot; y++)
                for (int x = 0; x < c - 6; x++)
                    if (IsSkin(im, x, y)) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            if (x1 < 0) return m;
            x0 -= 2; x1 += 1; y0 -= 1; y1 += 2;
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(Cut, y1 + 1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(W, x1 + 1); x++)
                {
                    int i = y * W + x;
                    if (im.A(x, y) && !far[i] && !IsShirt(im, x, y)) m[i] = true;
                }
            return m;
        }

        // ---------- 정면·뒷모습 ----------
        // phase 0: 왼쪽 다리와 오른쪽 팔을 들어 올림, 2: 그 반대, 1·3: 다리가 모이는 순간
        static int[] Front(Img im, int phase)
        {
            var o = NewMap();
            int c = ShortsCenter(im), tl, tr;
            TorsoSpan(im, 76, out tl, out tr);
            bool lifting = phase == 0 || phase == 2;
            bool legLeft = phase == 0, armLeft = !legLeft;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!im.A(x, y)) continue;
                    int ny = y;
                    if (y >= Hip && lifting && (x < c) == legLeft) ny = y - LegLift;
                    else if (y >= 66 && y < Hip && lifting && (armLeft ? x < tl - 1 : x > tr + 1)) ny = y - ArmLift;
                    else if (y < Hip && !lifting) ny = y;
                    else if (y < Hip) ny = y + Squash;
                    if (ny >= 0 && ny < H) o[ny * W + x] = im.Code(x, y);
                }
            return o;
        }

        // ---------- 그림 분석 ----------
        // 바지 아랫부분 흰 픽셀들의 가운데 x (엉덩이 위치)
        static int ShortsCenter(Img im)
        {
            var xs = new List<int>();
            for (int x = 0; x < W; x++)
                for (int y = Hip - 6; y < Hip; y++)
                {
                    var p = im.P(x, y);
                    if (p.a > 0 && Mathf.Min(p.r, Mathf.Min(p.g, p.b)) > 170) xs.Add(x);
                }
            if (xs.Count == 0) return 50;
            xs.Sort();
            return xs[xs.Count / 2];
        }

        static bool IsShirt(Img im, int x, int y)
        {
            var p = im.P(x, y);
            if (p.a == 0) return false;
            float h, s, v;
            Color.RGBToHSV((Color)p, out h, out s, out v);
            float d = h * 360f;
            return d >= 195f && d <= 250f && s > 0.35f && v > 0.2f;
        }

        static bool IsSkin(Img im, int x, int y)
        {
            var p = im.P(x, y);
            if (p.a == 0) return false;
            float h, s, v;
            Color.RGBToHSV((Color)p, out h, out s, out v);
            float d = h * 360f;
            return (d <= 40f || d >= 335f) && s > 0.1f && v > 0.5f;
        }

        // 팔 줄에서 상의가 가장 오른쪽까지 간 x (몸통 오른쪽 끝). 없으면 -1
        static int ShirtEdge(Img im)
        {
            int edge = -1;
            for (int y = ArmTop + 2; y < ArmBot - 4; y++)
                for (int x = 0; x < W; x++)
                    if (IsShirt(im, x, y)) edge = Mathf.Max(edge, x);
            return edge;
        }

        // 한 줄에서 몸통(상의)이 차지하는 좌우 범위
        static void TorsoSpan(Img im, int row, out int left, out int right)
        {
            int c = 50;
            if (!IsShirt(im, c, row))
            {
                int best = -1;
                for (int x = 35; x < 66; x++)
                    if (IsShirt(im, x, row) && (best < 0 || Mathf.Abs(x - 50) < Mathf.Abs(best - 50))) best = x;
                if (best < 0) { left = 40; right = 60; return; }
                c = best;
            }
            left = c; right = c;
            while (left - 1 >= 0 && IsShirt(im, left - 1, row)) left--;
            while (right + 1 < W && IsShirt(im, right + 1, row)) right++;
        }
    }
}
