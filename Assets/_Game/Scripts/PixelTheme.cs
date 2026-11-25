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
    // 화면 색 모음
    // 픽셀 아트 느낌의 색 모음. 나중에 이미지로 바꿀 때도 여기부터 손보면 됩니다.
    public static class PixelTheme
    {
        public static readonly Color Background = new Color(.05f, .07f, .12f, 1);
        public static readonly Color Panel = new Color(.10f, .14f, .22f, 1);
        public static readonly Color PanelEdge = new Color(.30f, .40f, .60f, 1);
        public static readonly Color ButtonFill = new Color(.16f, .40f, .32f, 1);
        public static readonly Color ButtonEdge = new Color(.45f, .85f, .62f, 1);
        public static readonly Color QuietFill = new Color(.16f, .21f, .31f, 1);
        public static readonly Color QuietEdge = new Color(.33f, .40f, .55f, 1);
        public static readonly Color AccentFill = new Color(.48f, .38f, .10f, 1);
        public static readonly Color Accent = new Color(.98f, .80f, .28f, 1);
        public static readonly Color RebirthFill = new Color(.42f, .24f, .52f, 1);
        public static readonly Color RebirthEdge = new Color(.78f, .55f, .90f, 1);
        public static readonly Color DangerFill = new Color(.45f, .16f, .16f, 1);
        public static readonly Color DangerEdge = new Color(.92f, .45f, .45f, 1);
        public static readonly Color TabActive = new Color(.20f, .45f, .75f, 1);
        public static readonly Color Text = new Color(.93f, .95f, .98f, 1);
        public static readonly Color TextDim = new Color(.62f, .68f, .80f, 1);
        public static readonly Color Bar = new Color(.30f, .80f, .45f, 1);
        public static readonly Color BarBack = new Color(.04f, .06f, .10f, 1);
    }
}
