// ─────────────────────────────────────────────────────────
// SoftMaskLight UIClip — RectMask2D (UNITY_UI_CLIP_RECT) 공용 클립
// Unity 6 UI/Default · TMP와 동일한 버텍스 마스크 방식.
// UnityGet2DClipping + 기본 _ClipRect=(0,0,0,0)이면 알파가 전부 0이 된다.
// UnityCG.cginc 이후에 include 할 것.
// ─────────────────────────────────────────────────────────

#ifndef SOFTMASKLIGHT_UICLIP_INCLUDED
#define SOFTMASKLIGHT_UICLIP_INCLUDED

float4 _ClipRect;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;

#define CAT_UI_CLIP_COORDS(idx) float4 uiClipMask : TEXCOORD##idx;

// clipPos: UnityObjectToClipPos 결과, localPos: UI 메시 로컬(v.vertex.xy)
inline float4 CAT_UI_ComputeClipMask(float4 clipPos, float2 localPos)
{
    float2 pixelSize = clipPos.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    return float4(
        localPos * 2 - clampedRect.xy - clampedRect.zw,
        0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
}

// 클립 팩터 (0~1). 프리멀티플라이/Additive처럼 RGB에도 곱해야 하는 경우 사용.
inline half CAT_UI_ClipFactor(float4 uiClipMask)
{
#ifdef UNITY_UI_CLIP_RECT
    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(uiClipMask.xy)) * uiClipMask.zw);
    return m.x * m.y;
#else
    return 1.0h;
#endif
}

// straight alpha 블렌딩용: 알파에만 적용
inline void CAT_UI_ApplyClipRect(inout half4 color, float4 uiClipMask)
{
#ifdef UNITY_UI_CLIP_RECT
    color.a *= CAT_UI_ClipFactor(uiClipMask);
#endif
}

#endif
