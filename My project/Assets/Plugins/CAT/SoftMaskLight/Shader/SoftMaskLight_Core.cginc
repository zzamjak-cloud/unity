// ─────────────────────────────────────────────────────────
// SoftMaskLight Core — Hidden 변형 셰이더용 공용 마스크 샘플링
// ─────────────────────────────────────────────────────────
// Hidden 변형 셰이더에서 사용:
// 1. #define _CAT_SOFTMASK 1
// 2. #include "SoftMaskLight_Core.cginc"
// 3. Properties에 SoftMask 프로퍼티 추가 ([HideInInspector])
// 4. #pragma multi_compile_local _ _SOFTMASK_NESTED
//    #pragma multi_compile_local _ _SOFTMASK_SLICE
//    #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE
// 5. v2f에 CAT_SOFTMASK_COORDS(idx1, idx2) 추가
// 6. 버텍스: CAT_SOFTMASK_VERT(v.vertex.xyz, o)
// 7. 프래그먼트: half mask = CAT_SOFTMASK_FRAG(i);
// ─────────────────────────────────────────────────────────

#ifndef SOFTMASKLIGHT_CORE_INCLUDED
#define SOFTMASKLIGHT_CORE_INCLUDED

#if defined(_CAT_SOFTMASK)

    // ── 유니폼 (SoftMask.cs 프로퍼티 이름과 동일) ──

    sampler2D _MaskTex;
    half _Softness;
    half _InvertMask;
    float4x4 _MaskWorldToUV;
    float4 _MaskUVRect;

    // 슬라이스 유니폼 (마스크 1)
    #if defined(_SOFTMASK_SLICE)
    float4 _MaskSliceBorder;   // (leftBreak, bottomBreak, rightBreak, topBreak) rect 정규화 [0,1]
    float4 _MaskSliceInnerUV;  // (innerLeft, innerBottom, innerRight, innerTop) 스프라이트 UV [0,1]
    #endif

    #if defined(_SOFTMASK_NESTED)
    sampler2D _MaskTex2;
    half _Softness2;
    half _InvertMask2;
    float4x4 _MaskWorldToUV2;
    float4 _MaskUVRect2;

    // 슬라이스 유니폼 (마스크 2, 중첩)
    #if defined(_SOFTMASK_NESTED_SLICE)
    float4 _MaskSliceBorder2;
    float4 _MaskSliceInnerUV2;
    #endif
    #endif

    // ── 9-슬라이스 1D 리매핑 (브랜치 없음, 모바일 최적화) ──
    // u:  입력 [0,1] (rect 정규화 좌표)
    // uA: 왼쪽/아래쪽 break point (rect 공간)
    // uB: 오른쪽/위쪽 break point (rect 공간, = 1 - 오른쪽/위쪽 테두리 비율)
    // pA: 왼쪽/아래쪽 inner UV break point (스프라이트 UV 공간)
    // pB: 오른쪽/위쪽 inner UV break point (스프라이트 UV 공간)
    #if defined(_SOFTMASK_SLICE) || defined(_SOFTMASK_NESTED_SLICE)
    inline float _CAT_SliceRemap1D(float u, float uA, float uB, float pA, float pB)
    {
        // step으로 각 구간 가중치 계산 (브랜치 없음)
        float s1 = step(u, uA);       // u <= uA: 왼쪽/아래쪽 코너 구간
        float s3 = step(uB, u);       // u >= uB: 오른쪽/위쪽 코너 구간
        float s2 = 1.0 - s1 - s3;    // 가운데 스트레치 구간

        // 각 구간의 리매핑 값
        float r1 = u * pA / max(uA, 0.00001);
        float r2 = pA + (u - uA) * (pB - pA) / max(uB - uA, 0.00001);
        float r3 = pB + (u - uB) * (1.0 - pB) / max(1.0 - uB, 0.00001);

        return s1 * r1 + s2 * r2 + s3 * r3;
    }
    #endif

    // ── 마스크 샘플링 함수 (half precision, 분기 없음) ──

    inline half _CAT_SampleMask1(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 슬라이스 타입: 9-slice UV 리매핑 적용
        #if defined(_SOFTMASK_SLICE)
        maskUV = float2(
            _CAT_SliceRemap1D(maskUV.x, _MaskSliceBorder.x, _MaskSliceBorder.z, _MaskSliceInnerUV.x, _MaskSliceInnerUV.z),
            _CAT_SliceRemap1D(maskUV.y, _MaskSliceBorder.y, _MaskSliceBorder.w, _MaskSliceInnerUV.y, _MaskSliceInnerUV.w)
        );
        #endif

        float2 atlasUV = _MaskUVRect.xy + maskUV * _MaskUVRect.zw;

        half maskAlpha = tex2D(_MaskTex, atlasUV).a;
        half softEdge = smoothstep(0.0h, max(_Softness, 0.001h), maskAlpha);
        half finalMask = lerp(softEdge, 1.0h - softEdge, _InvertMask);
        return finalMask * inBounds;
    }

    #if defined(_SOFTMASK_NESTED)
    inline half _CAT_SampleMask2(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 중첩 마스크 슬라이스 타입: 9-slice UV 리매핑 적용
        #if defined(_SOFTMASK_NESTED_SLICE)
        maskUV = float2(
            _CAT_SliceRemap1D(maskUV.x, _MaskSliceBorder2.x, _MaskSliceBorder2.z, _MaskSliceInnerUV2.x, _MaskSliceInnerUV2.z),
            _CAT_SliceRemap1D(maskUV.y, _MaskSliceBorder2.y, _MaskSliceBorder2.w, _MaskSliceInnerUV2.y, _MaskSliceInnerUV2.w)
        );
        #endif

        float2 atlasUV = _MaskUVRect2.xy + maskUV * _MaskUVRect2.zw;

        half maskAlpha = tex2D(_MaskTex2, atlasUV).a;
        half softEdge = smoothstep(0.0h, max(_Softness2, 0.001h), maskAlpha);
        half finalMask = lerp(softEdge, 1.0h - softEdge, _InvertMask2);
        return finalMask * inBounds;
    }
    #endif

    // ── 매크로: v2f 구조체용 (TEXCOORD 슬롯 할당) ──

    #if defined(_SOFTMASK_NESTED)
        #define CAT_SOFTMASK_COORDS(idx1, idx2) \
            float2 maskUV : TEXCOORD##idx1; \
            float2 maskUV2 : TEXCOORD##idx2;
    #else
        #define CAT_SOFTMASK_COORDS(idx1, idx2) \
            float2 maskUV : TEXCOORD##idx1;
    #endif

    // ── 매크로: 버텍스 셰이더용 (월드좌표 → 마스크 UV) ──

    #if defined(_SOFTMASK_NESTED)
        #define CAT_SOFTMASK_VERT(worldPos, o) \
            o.maskUV = mul(_MaskWorldToUV, float4(worldPos, 1)).xy; \
            o.maskUV2 = mul(_MaskWorldToUV2, float4(worldPos, 1)).xy;
    #else
        #define CAT_SOFTMASK_VERT(worldPos, o) \
            o.maskUV = mul(_MaskWorldToUV, float4(worldPos, 1)).xy;
    #endif

    // ── 매크로: 프래그먼트 셰이더용 (마스크 값 반환) ──

    #if defined(_SOFTMASK_NESTED)
        #define CAT_SOFTMASK_FRAG(i) (_CAT_SampleMask1(i.maskUV) * _CAT_SampleMask2(i.maskUV2))
    #else
        #define CAT_SOFTMASK_FRAG(i) _CAT_SampleMask1(i.maskUV)
    #endif

#else // _CAT_SOFTMASK 비활성 — 오버헤드 없음

    #define CAT_SOFTMASK_COORDS(idx1, idx2)
    #define CAT_SOFTMASK_VERT(worldPos, o)
    #define CAT_SOFTMASK_FRAG(i) 1.0h

#endif // _CAT_SOFTMASK

#endif // SOFTMASKLIGHT_CORE_INCLUDED
