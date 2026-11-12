using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyFootballCareer
{
    // 강화 결과 연출. 결과 창 대신 짧게 보여 주고 사라지므로 강화 버튼을 연달아 누를 수 있습니다.
    //  +1   : 능력치 숫자가 톡 튀고, 초록 "+1"이 떠오르고, 위쪽 배너가 잠깐 내려옵니다.
    //  +2   : 같은 연출을 더 크게 + 숫자가 금색으로 번쩍입니다.
    //  실패 : 회색 배너만 잠깐 보여 줍니다.
    // CareerPrototypeUI가 만들고 매 프레임 Tick을 부릅니다. 오브젝트에 붙이지 않습니다.
    public class UpgradeEffects
    {
        static readonly Color Green = new Color(.45f, .95f, .55f, 1);
        static readonly Color Gold = new Color(1f, .84f, .30f, 1);
        static readonly Color FlashWhite = new Color(1f, .98f, .85f, 1);

        readonly Font font;
        readonly RectTransform root;
        readonly List<Floater> floaters = new List<Floater>();
        readonly Dictionary<Text, LabelPunch> punches = new Dictionary<Text, LabelPunch>();
        Banner banner;

        public UpgradeEffects(Font font, RectTransform root) { this.font = font; this.root = root; }

        // 강화 결과 하나를 보여 줍니다. label은 그 능력치 줄의 글자 ("Shooting  32"), 이미 새 값으로 바뀐 상태여야 합니다.
        public void Play(UpgradeResult r, Text label, float doubleShare)
        {
            if (r == null || root == null) return;
            bool great = r.Outcome == UpgradeOutcome.GreatSuccess;
            bool success = great || r.Outcome == UpgradeOutcome.Success;
            SoundFx.Play(great ? Sfx.UpgradeGreat : success ? Sfx.UpgradeSuccess : Sfx.UpgradeFail);
            string name = Ko.Attr(r.Attribute);
            string chance = CareerPrototypeUI.ChanceText(r.Chance, r.FailStack > 0);
            string sub = success
                ? r.Before.ToString("0") + " -> " + r.After.ToString("0") + "   확률 " + chance
                  + CareerPrototypeUI.BonusText(r.Chance - r.BaseChance) + "   -" + SeasonSystem.Money(r.Cost) + " 포인트"
                : "다음 확률 " + CareerPrototypeUI.ChanceText(r.NextChance, true) + CareerPrototypeUI.BonusText(r.NextChance - r.BaseChance)
                  + "   -" + SeasonSystem.Money(r.Cost) + " 포인트";
            if (great) ShowBanner("대성공!  " + name + " +2", sub, PixelTheme.AccentFill, PixelTheme.Accent, Gold, 1.4f, true);
            else if (success) ShowBanner(name + " +1", sub, PixelTheme.ButtonFill, PixelTheme.ButtonEdge, PixelTheme.Text, 0.9f, false);
            else ShowBanner(name + "  강화 실패", sub, PixelTheme.QuietFill, PixelTheme.QuietEdge, PixelTheme.TextDim, 0.8f, false);

            if (!success || label == null) return;
            if (punches.TryGetValue(label, out var old)) old.End();   // 같은 줄을 연달아 누르면 이전 연출을 끝내고 새로 시작
            punches[label] = new LabelPunch(label, great);
            floaters.Add(new Floater(this, label, great));
        }

        public void Tick(float dt)
        {
            if (banner != null && !banner.Step(dt)) banner = null;
            for (int i = floaters.Count - 1; i >= 0; i--) if (!floaters[i].Step(dt)) floaters.RemoveAt(i);
            if (punches.Count == 0) return;
            List<Text> done = null;
            foreach (var kv in punches)
                if (!kv.Value.Step(dt)) { kv.Value.End(); (done ??= new List<Text>()).Add(kv.Key); }
            if (done != null) foreach (var k in done) punches.Remove(k);
        }

        // ---------- 공통 ----------
        Text NewText(Transform parent, string value, int size, Color color, TextAnchor align)
        {
            var go = new GameObject("FxText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = color; t.alignment = align;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Image NewImage(Transform parent, Color color)
        {
            var go = new GameObject("FxImage", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        static float Bump(float t, float duration) { return t >= duration ? 0f : Mathf.Sin(t / duration * Mathf.PI); }

        // ---------- 위쪽 배너 ----------
        void ShowBanner(string title, string sub, Color fill, Color edge, Color titleColor, float hold, bool great)
        {
            if (banner != null) banner.Destroy();
            banner = new Banner(this, title, sub, fill, edge, titleColor, hold, great);
        }

        class Banner
        {
            const float In = 0.18f, Out = 0.22f, Height = 96f, Margin = 16f;
            readonly RectTransform rect;
            readonly Text title;
            readonly Color titleColor;
            readonly float hold;
            readonly bool great;
            float t;

            public Banner(UpgradeEffects fx, string titleText, string subText, Color fill, Color edge, Color titleColor, float hold, bool great)
            {
                this.hold = hold; this.great = great; this.titleColor = titleColor;
                var outer = NewImage(fx.root, edge);
                rect = outer.rectTransform;
                rect.anchorMin = new Vector2(.04f, 1f); rect.anchorMax = new Vector2(.96f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.sizeDelta = new Vector2(0f, Height);
                rect.SetAsLastSibling();
                var inner = NewImage(rect, fill); Stretch(inner.rectTransform, 4f);
                title = fx.NewText(inner.rectTransform, titleText, great ? 32 : 28, titleColor, TextAnchor.MiddleCenter);
                var tr = title.rectTransform; tr.anchorMin = new Vector2(0, .45f); tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                var s = fx.NewText(inner.rectTransform, subText, 20, PixelTheme.Text, TextAnchor.MiddleCenter);
                var sr = s.rectTransform; sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(1, .45f); sr.offsetMin = sr.offsetMax = Vector2.zero;
                if (great) { var o = title.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.35f, .2f, 0f, 1f); o.effectDistance = new Vector2(2, -2); }
                Place();
            }

            void Place()
            {
                float shown;   // 0 = 화면 위로 숨음, 1 = 다 내려옴
                if (t < In) shown = 1f - (1f - t / In) * (1f - t / In);
                else if (t < In + hold) shown = 1f;
                else shown = 1f - Mathf.Clamp01((t - In - hold) / Out);
                float shake = great && t > In && t < In + 0.3f ? Mathf.Sin(t * 90f) * 6f * (1f - (t - In) / 0.3f) : 0f;
                rect.anchoredPosition = new Vector2(shake, Mathf.Lerp(Height + 8f, -Margin, shown));
                if (great && title != null)   // 대성공: 제목이 잠깐 하얗게 깜빡임
                    title.color = t < In + 0.5f && Mathf.FloorToInt(t / 0.08f) % 2 == 0 ? FlashWhite : titleColor;
            }

            public bool Step(float dt)
            {
                if (rect == null) return false;
                t += dt;
                if (t >= In + hold + Out) { Destroy(); return false; }
                Place();
                return true;
            }

            public void Destroy() { if (rect != null) Object.Destroy(rect.gameObject); }
        }

        // ---------- 떠오르는 +1 / +2 ----------
        class Floater
        {
            readonly RectTransform rect;
            readonly Text text;
            readonly Vector2 start;
            readonly float duration, rise;
            readonly bool great;
            float t;

            public Floater(UpgradeEffects fx, Text label, bool great)
            {
                this.great = great;
                duration = great ? 1.1f : 0.8f; rise = great ? 90f : 60f;
                text = fx.NewText(fx.root, great ? "+2" : "+1", great ? 42 : 30, great ? Gold : Green, TextAnchor.MiddleLeft);
                if (great) { var o = text.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.35f, .2f, 0f, 1f); o.effectDistance = new Vector2(2, -2); }
                rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(0f, .5f); rect.sizeDelta = new Vector2(120f, 50f);
                rect.SetAsLastSibling();
                // 능력치 글자 끝(숫자 바로 오른쪽)에서 시작
                var lr = label.rectTransform;
                Vector3 world = lr.TransformPoint(new Vector3(lr.rect.xMin + label.preferredWidth + 14f, lr.rect.center.y, 0f));
                Vector3 local = fx.root.InverseTransformPoint(world);
                start = new Vector2(local.x, local.y) - fx.root.rect.center;
                Place();
            }

            void Place()
            {
                float k = Mathf.Clamp01(t / duration);
                float ease = 1f - (1f - k) * (1f - k);
                rect.anchoredPosition = start + new Vector2(0f, rise * ease);
                float scale = great ? 1f + 0.7f * Mathf.Max(0f, 1f - t / 0.18f) : 1f + 0.3f * Mathf.Max(0f, 1f - t / 0.15f);
                rect.localScale = new Vector3(scale, scale, 1f);
                var c = text.color; c.a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f; text.color = c;
            }

            public bool Step(float dt)
            {
                if (rect == null) return false;
                t += dt;
                if (t >= duration) { Object.Destroy(rect.gameObject); return false; }
                Place();
                return true;
            }
        }

        // ---------- 능력치 숫자 튀기기 (+2는 번쩍임 추가) ----------
        class LabelPunch
        {
            readonly Text label;
            readonly bool great;
            readonly Color baseColor;
            readonly Image flash;       // +2: 글자 위를 덮는 금빛 섬광
            readonly Outline glow;      // +2: 글자 테두리 빛
            readonly float duration;
            float t;

            public LabelPunch(Text label, bool great)
            {
                this.label = label; this.great = great;
                baseColor = PixelTheme.Text;
                duration = great ? 0.9f : 0.5f;
                if (great)
                {
                    flash = NewImage(label.rectTransform, new Color(1f, .9f, .45f, 0f));
                    var fr = flash.rectTransform;
                    fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, .5f);
                    fr.anchoredPosition = new Vector2(-6f, 0f);
                    fr.sizeDelta = new Vector2(label.preferredWidth + 12f, 10f);
                    glow = label.gameObject.AddComponent<Outline>();
                    glow.effectColor = new Color(1f, .75f, .2f, 0f); glow.effectDistance = new Vector2(2, -2);
                }
                Step(0f);
            }

            public bool Step(float dt)
            {
                if (label == null) return false;
                t += dt;
                // 크기: 톡 튀었다가 제자리 (+2는 더 크게, 0.2초 뒤 한 번 더)
                float s = great ? 1f + 0.55f * Bump(t, 0.3f) + 0.25f * Bump(t - 0.3f, 0.25f) : 1f + 0.35f * Bump(t, 0.28f);
                label.rectTransform.localScale = new Vector3(s, s, 1f);
                if (!great)
                {
                    label.color = Color.Lerp(Green, baseColor, Mathf.Clamp01((t - 0.15f) / 0.35f));
                }
                else
                {
                    // 금색·흰색으로 번갈아 번쩍이다가 원래 색으로
                    bool blink = t < 0.6f && Mathf.FloorToInt(t / 0.07f) % 2 == 0;
                    Color c = blink ? FlashWhite : Gold;
                    label.color = t < 0.6f ? c : Color.Lerp(Gold, baseColor, (t - 0.6f) / 0.3f);
                    float a = Mathf.Max(0f, 0.85f - t * 3f) + 0.55f * Mathf.Max(0f, Bump(t - 0.3f, 0.25f));
                    if (flash != null) flash.color = new Color(1f, .9f, .45f, a);
                    if (glow != null) glow.effectColor = new Color(1f, .75f, .2f, Mathf.Clamp01(1f - t / duration));
                }
                return t < duration;
            }

            public void End()
            {
                if (flash != null) Object.Destroy(flash.gameObject);
                if (glow != null) Object.Destroy(glow);
                if (label == null) return;
                label.rectTransform.localScale = Vector3.one;
                label.color = baseColor;
            }
        }
    }
}
