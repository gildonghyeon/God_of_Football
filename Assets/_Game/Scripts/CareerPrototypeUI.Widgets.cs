using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
namespace MyFootballCareer
{
    // 화면 만들기 도우미 (버튼·칸·막대·탭 전환)
    public partial class CareerPrototypeUI
    {
        // ---------- 만들기 도우미 ----------
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Height(GameObject go, float height) { go.AddComponent<LayoutElement>().preferredHeight = height; }

        static void ConfigureStack(GameObject go, int left, int top, int right, int bottom, float spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(left, right, top, bottom); v.spacing = spacing;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        }

        Text TextNode(string value, Transform parent, int size)
        {
            var r = NewRect("Text", parent); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.text = value; t.color = PixelTheme.Text;
            t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            return t;
        }

        Text Label(string value, int size = 22, float height = 40)
        { var t = TextNode(value, Parent, size); Height(t.gameObject, height); return t; }

        // 두꺼운 테두리가 있는 픽셀 느낌 버튼
        Button PixelButton(string label, Action action, Transform parent, float height, Color fill, Color edge)
        {
            var outer = NewRect("Button", parent);
            outer.gameObject.AddComponent<Image>().color = edge;
            if (height > 0f) { var le = outer.gameObject.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height; }
            var inner = NewRect("Fill", outer);
            Stretch(inner); inner.offsetMin = new Vector2(4, 4); inner.offsetMax = new Vector2(-4, -4);
            var fillImage = inner.gameObject.AddComponent<Image>(); fillImage.color = fill;
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImage;
            var colors = button.colors;
            colors.highlightedColor = Color.white; colors.pressedColor = new Color(.7f, .7f, .7f, 1);
            button.colors = colors;
            button.onClick.AddListener(() => { SoundFx.Play(Sfx.Click); action(); });
            var text = TextNode(label, inner, 26); Stretch(text.rectTransform); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        Button PixelButton(string label, Action action, Transform parent, float height = 76f)
        { return PixelButton(label, action, parent, height, PixelTheme.ButtonFill, PixelTheme.ButtonEdge); }

        // 테두리 + 안쪽 칸. 안쪽 내용을 넣을 Transform을 돌려줍니다. compact면 여백을 줄입니다.
        Transform Section(Transform parent, string title, bool compact = false)
        {
            var outer = NewRect("Section", parent);
            outer.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            ConfigureStack(outer.gameObject, 4, 4, 4, 4, 0);
            var inner = NewRect("Body", outer);
            inner.gameObject.AddComponent<Image>().color = PixelTheme.Panel;
            if (compact) ConfigureStack(inner.gameObject, 16, 10, 16, 12, 6);
            else ConfigureStack(inner.gameObject, 20, 16, 20, 20, 12);
            if (!string.IsNullOrEmpty(title))
            {
                var t = TextNode(title, inner, compact ? 26 : 30); t.color = PixelTheme.Accent; Height(t.gameObject, compact ? 36 : 44);
            }
            return inner;
        }

        // 능력치·진행도에 쓰는 가로 막대. 채워지는 부분의 RectTransform을 돌려줍니다.
        RectTransform MakeBar(Transform parent, float height, bool flexibleWidth)
        {
            var bar = NewRect("Bar", parent);
            bar.gameObject.AddComponent<Image>().color = PixelTheme.PanelEdge;
            var layout = bar.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            if (flexibleWidth) { layout.flexibleWidth = 1; layout.preferredWidth = 100; }
            var back = NewRect("Back", bar);
            Stretch(back); back.offsetMin = new Vector2(2, 2); back.offsetMax = new Vector2(-2, -2);
            back.gameObject.AddComponent<Image>().color = PixelTheme.BarBack;
            var fill = NewRect("Fill", back);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = PixelTheme.Bar;
            SetFill(fill, 0f);
            return fill;
        }

        // 능력치 한 줄: 왼쪽에 글자, 오른쪽에 막대
        // 능력치 한 줄: 왼쪽에 글자, 가운데 막대, 오른쪽에 노력 포인트로 +1 하는 버튼
        RectTransform MakeStatRow(AttributeKind kind, out Text label, out Button upgrade)
        {
            var row = NewRect("StatRow", Parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Height(row.gameObject, 52);
            label = TextNode("", row, 22);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 190; le.preferredHeight = 30;
            label.rectTransform.pivot = new Vector2(0f, .5f);   // 강화 연출에서 글자가 왼쪽 끝을 기준으로 커지도록
            var bar = MakeBar(row, 20, true);
            upgrade = PixelButton("+1", () => onUpgrade(kind), row, 0f);
            upgrade.GetComponentInChildren<Text>().fontSize = 20;
            var ble = upgrade.gameObject.GetComponent<LayoutElement>() ?? upgrade.gameObject.AddComponent<LayoutElement>();
            ble.preferredWidth = 220; ble.minWidth = 220; ble.preferredHeight = 48; ble.flexibleWidth = 0;
            return bar;
        }

        static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null) return;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        RectTransform NewPage(string name)
        {
            var page = NewRect(name, content);
            ConfigureStack(page.gameObject, 0, 0, 0, 0, 18);
            return page;
        }

        void SetChrome(bool visible)
        {
            tabBar.gameObject.SetActive(visible);
            if (clubBar != null) clubBar.gameObject.SetActive(false);   // 나만의 팀 탭은 ShowClubMode에서만 켬
            statusStrip.gameObject.SetActive(visible);
            viewport.offsetMin = new Vector2(0, visible ? TabHeight + StatusHeight : 0);
            viewport.offsetMax = Vector2.zero;
        }

        void SetPage(int index)
        {
            for (int k = 0; k < pages.Length; k++)
            {
                if (pages[k] != null) pages[k].gameObject.SetActive(k == index);
                if (tabFills[k] != null) tabFills[k].color = k == index ? PixelTheme.TabActive : PixelTheme.QuietFill;
            }
            content.anchoredPosition = Vector2.zero;
        }

        void Clear()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            content.anchoredPosition = Vector2.zero;
            status = null; currentParent = null;
            Array.Clear(pages, 0, pages.Length);
            Array.Clear(statTexts, 0, statTexts.Length);
            Array.Clear(statBars, 0, statBars.Length);
            Array.Clear(upgradeButtons, 0, upgradeButtons.Length);
            rateButton = null; matchPage = null; careerPage = null;
            trainingText = seasonText = resultText = playLabel = standingsText = historyText = rebirthText = friendlyText = trophyText = leadersText = null;
            friendlyButton = null;
            Array.Clear(facilityButtons, 0, facilityButtons.Length);
            Array.Clear(missionTexts, 0, missionTexts.Length); Array.Clear(missionButtons, 0, missionButtons.Length);
            attendText = attendDetail = attendNote = resetText = bonusText = null; attendButton = bonusButton = null; attendCards = null;
            moneyText = facilityMoneyText = null;
            transferRoot = null; rebirthFill = null; tCurrentKey = null;
            SetChrome(false);
            DestroyPreview();
        }
    }
}
