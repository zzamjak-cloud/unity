// ─────────────────────────────────────────────────────────
// CAT SoftMask Core — 파티클 셰이더용 공용 마스크 샘플링
// ─────────────────────────────────────────────────────────
// 사용법:
// 1. Properties에 SoftMask 프로퍼티 추가 ([HideInInspector])
// 2. #pragma multi_compile_local _ _CAT_SOFTMASK
//    #pragma multi_compile_local _ _SOFTMASK_NESTED
// 3. 해당 셰이더와 같은 폴더의 CAT_SoftMask.cginc 사용: #include "CAT_SoftMask.cginc"
// 4. v2f에 CAT_SOFTMASK_COORDS(idx1, idx2) 추가
// 5. 버텍스: float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
//           CAT_SOFTMASK_VERT(worldPos, o)
// 6. 프래그먼트: half mask = CAT_SOFTMASK_FRAG(i);
//              color.a *= mask; (또는 premultiplied additive: color *= mask;)
// ─────────────────────────────────────────────────────────

#ifndef CAT_SOFTMASK_CORE_INCLUDED
#define CAT_SOFTMASK_CORE_INCLUDED

#if defined(_CAT_SOFTMASK)

    // ── 유니폼 (SoftMask.cs 프로퍼티 이름과 동일) ──

    sampler2D _MaskTex;
    half _Softness;
    half _InvertMask;
    float4x4 _MaskWorldToUV;
    float4 _MaskUVRect;

    #if defined(_SOFTMASK_NESTED)
    sampler2D _MaskTex2;
    half _Softness2;
    half _InvertMask2;
    float4x4 _MaskWorldToUV2;
    float4 _MaskUVRect2;
    #endif

    // ── 마스크 샘플링 함수 (half precision, 분기 없음) ──

    inline half _CAT_SampleMask1(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

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

#endif // CAT_SOFTMASK_CORE_INCLUDED
