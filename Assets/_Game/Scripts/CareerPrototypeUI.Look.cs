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
    // 외형(머리색·피부색·축구화) 고르기
    public partial class CareerPrototypeUI
    {
        // ---------- 팝업 ----------
        // ---------- 외형(머리색, 피부색, 유니폼) ----------
        int lookHair, lookSkin, lookKit, lookBoots;   // 지금 선수의 외형
        bool lookGloves;                              // 골키퍼면 장갑
        bool previewGloves;                           // 미리보기 그림에 장갑
        int newHair, newSkin, newKit, newBoots;       // 선수 생성 화면과 변경 창에서 고르는 외형
        Texture2D previewTexture;
        Sprite previewSprite;

        void DestroyPreview()
        {
            if (previewSprite != null) Destroy(previewSprite);
            if (previewTexture != null) Destroy(previewTexture);
            previewSprite = null; previewTexture = null;
        }

        Image AddPreview(Transform parent, float height)
        {
            var rect = NewRect("LookPreview", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false; image.enabled = false;
            Height(rect.gameObject, height);
            return image;
        }

        // 정면 대기 그림에 고르는 중인 머리색·피부색·유니폼을 적용해 미리보기로 보여 줍니다.
        void ApplyPreview(Image target)
        {
            var asset = Resources.Load<TextAsset>("Art/Chars/blue_idle_front_0");
            if (asset == null || target == null) return;
            DestroyPreview();
            previewTexture = PlayerLook.LoadRecolored(asset.bytes, newHair, newSkin, newKit, newBoots, previewGloves);
            if (previewTexture == null) return;
            previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, previewTexture.width, previewTexture.height), new Vector2(.5f, .5f), 100f);
            target.sprite = previewSprite; target.enabled = true;
        }

        // 머리색, 피부색, 축구화를 순서대로 바꾸는 버튼 세 개 (newHair, newSkin, newBoots 값을 바꾸고 미리보기를 갱신).
        // 유니폼은 고를 수 없고 소속 팀 유니폼을 입습니다.
        void AddLookButtons(Transform parent, Image preview)
        {
            Button hairButton = null, skinButton = null, bootButton = null;
            hairButton = PixelButton("머리: " + PlayerLook.HairNames[newHair], () => {
                newHair = (newHair + 1) % PlayerLook.HairCount;
                hairButton.GetComponentInChildren<Text>().text = "머리: " + PlayerLook.HairNames[newHair];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            skinButton = PixelButton("피부: " + PlayerLook.SkinNames[newSkin], () => {
                newSkin = (newSkin + 1) % PlayerLook.SkinCount;
                skinButton.GetComponentInChildren<Text>().text = "피부: " + PlayerLook.SkinNames[newSkin];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
            bootButton = PixelButton("축구화: " + PlayerLook.BootNames[newBoots], () => {
                newBoots = (newBoots + 1) % PlayerLook.BootCount;
                bootButton.GetComponentInChildren<Text>().text = "축구화: " + PlayerLook.BootNames[newBoots];
                ApplyPreview(preview);
            }, parent, 72f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        void ShowLookDialog()
        {
            newHair = lookHair; newSkin = lookSkin; newKit = lookKit; newBoots = lookBoots; previewGloves = lookGloves;
            var inner = BuildDialog("외형", "머리색, 피부색, 축구화를 고르세요. 유니폼은 소속 팀 유니폼입니다.", 36f, 20);
            var preview = AddPreview(inner, 220f);
            ApplyPreview(preview);
            AddLookButtons(inner, preview);
            PixelButton("적용", () => { CloseDialog(); DestroyPreview(); if (OnApplyLook != null) OnApplyLook(newHair, newSkin, newKit, newBoots); }, inner, 76f);
            PixelButton("취소", () => { CloseDialog(); DestroyPreview(); }, inner, 76f, PixelTheme.QuietFill, PixelTheme.QuietEdge);
        }

        // 선수의 외형이 바뀌었을 때 훈련장 그림을 갱신합니다.
        public void ApplyLook(PlayerData p)
        {
            lookHair = p.hairColor; lookSkin = p.skinTone; lookKit = p.kitColor; lookBoots = p.bootColor; lookGloves = p.position == Position.Goalkeeper;
            if (stage != null) stage.SetLook(lookHair, lookSkin, lookKit, lookBoots, lookGloves);
        }
    }
}
