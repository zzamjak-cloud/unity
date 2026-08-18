// ─────────────────────────────────────────────────────────
// SoftMaskLight Core — Hidden 변형 셰이더용 공용 마스크 샘플링
// ─────────────────────────────────────────────────────────
// Hidden 변형 셰이더에서 사용:
// 1. #define _CAT_SOFTMASK 1
// 2. #include "SoftMaskLight_Core.cginc"
// 3. Properties에 SoftMask 프로퍼티 추가 ([HideInInspector])
// 4. #pragma multi_compile_local _ _SOFTMASK_NESTED
//    #pragma multi_compile_local _ _SOFTMASK_SLICE
// 5. v2f에 CAT_SOFTMASK_COORDS(idx1, idx2) 추가
// 6. 버텍스: CAT_SOFTMASK_VERT(v.vertex.xyz, o)
// 7. 프래그먼트: half mask = CAT_SOFTMASK_FRAG(i);
//
// 키워드 정책 (변형 수 최소화):
// - _SOFTMASK_SLICE 하나가 마스크 1·2의 "형태 대응"(9-slice / Tiled / Filled)을 모두 담당한다.
//   (해당 없는 쪽은 C#이 항등 파라미터를 넣어 각 단계가 no-op이 된다)
//   * Sliced: 축당 3구간 조각 선형 리매핑
//   * Tiled:  중앙 구간을 frac() 반복으로 일반화 (기울기 벡터 w = 타일 수 - ε)
//   * Filled: 반평면 2개 테스트 (H/V/Radial 90·180·360 — C#이 fillAmount로 사전 계산, atan2 없음)
// - _CAT_SOFTMASK_FORCE_SLICE를 include 이전에 define하면 형태 대응이 키워드 없이
//   항상 컴파일된다. 변형 수가 이펙트 조합과 곱해지는 셰이더(UIEffect)용.
// ─────────────────────────────────────────────────────────

#ifndef SOFTMASKLIGHT_CORE_INCLUDED
#define SOFTMASKLIGHT_CORE_INCLUDED

#if defined(_CAT_SOFTMASK)

    // 슬라이스 코드 컴파일 여부 (키워드 또는 강제 플래그)
    #if defined(_SOFTMASK_SLICE) || defined(_CAT_SOFTMASK_FORCE_SLICE)
        #define CAT_SOFTMASK_USE_SLICE 1
    #endif

    // ── 유니폼 (SoftMaskLight.cs 프로퍼티 이름과 동일) ──

    sampler2D _MaskTex;
    half _SoftnessRcp;   // = 1 / max(softness, 0.001) — C# 사전 계산 (픽셀당 나눗셈 제거)
    half _InvertMask;
    float4x4 _MaskWorldToUV;
    float4 _MaskUVRect;

    #if defined(CAT_SOFTMASK_USE_SLICE)
    float4 _MaskSliceBorder;   // (leftBreak, bottomBreak, rightBreak, topBreak) rect 정규화 [0,1]
    float4 _MaskSliceInnerUV;  // (innerLeft, innerBottom, innerRight, innerTop) 스프라이트 UV [0,1]
    float4 _MaskSliceSlopeX;   // (k1, k2n, k3, 타일수-ε) X축 — C# 사전 계산 (픽셀당 나눗셈 제거)
    float4 _MaskSliceSlopeY;   // (k1, k2n, k3, 타일수-ε) Y축. 비타일은 타일수=1
    float4 _MaskFillLineA;     // Filled 커버리지 반평면 A: (a, b, c, 결합모드 0=AND/1=OR). 항등=(0,0,1,0)
    float4 _MaskFillLineB;     // Filled 커버리지 반평면 B: (a, b, c, 0). 항등=(0,0,1,0)
    #endif

    #if defined(_SOFTMASK_NESTED)
    sampler2D _MaskTex2;
    half _SoftnessRcp2;
    half _InvertMask2;
    float4x4 _MaskWorldToUV2;
    float4 _MaskUVRect2;

    #if defined(CAT_SOFTMASK_USE_SLICE)
    float4 _MaskSliceBorder2;
    float4 _MaskSliceInnerUV2;
    float4 _MaskSliceSlopeX2;
    float4 _MaskSliceSlopeY2;
    float4 _MaskFillLineA2;
    float4 _MaskFillLineB2;
    #endif
    #endif

    // ── 9-슬라이스/타일 1D 리매핑 (브랜치·나눗셈 없음, 모바일 최적화) ──
    // u:  입력 [0,1] (rect 정규화 좌표)
    // uA: 왼쪽/아래쪽 break point (rect 공간)
    // uB: 오른쪽/위쪽 break point (rect 공간, = 1 - 오른쪽/위쪽 테두리 비율)
    // pA: 왼쪽/아래쪽 inner UV break point (스프라이트 UV 공간)
    // pB: 오른쪽/위쪽 inner UV break point (스프라이트 UV 공간)
    // k:  (k1, k2n, k3, nMax) — C#이 역수 나눗셈/타일 수를 사전 계산해 전달
    //     중앙 구간은 frac((u-uA)*k2n)로 타일 반복. Sliced는 타일 수 1 → 기존 선형 스트레치와 동일
    //
    // (uA,uB,pA,pB,k) = (0,1,0,1,(0,1,0,1-ε))이면 항등 함수가 되므로,
    // 해당 없는 마스크는 C#이 이 값을 넣어 리매핑을 무효화한다.
    #if defined(CAT_SOFTMASK_USE_SLICE)
    inline float _CAT_SliceRemap1D(float u, float uA, float uB, float pA, float pB, float4 k)
    {
        // step으로 각 구간 가중치 계산 (브랜치 없음)
        float s1 = step(u, uA);       // u <= uA: 왼쪽/아래쪽 코너 구간
        float s3 = step(uB, u);       // u >= uB: 오른쪽/위쪽 코너 구간
        // saturate: uA == uB (테두리가 rect 전체)인 경계점에서 음수 가중치 방지
        float s2 = saturate(1.0 - s1 - s3); // 가운데 구간 (스트레치 또는 타일 반복)

        // 각 구간의 리매핑 값 (기울기는 사전 계산된 역수 — 픽셀당 나눗셈 0회)
        float r1 = u * k.x;
        // 중앙: frac로 타일 반복. min(…, nMax)로 끝 경계에서 frac이 0으로 감기는 것 방지
        float r2 = pA + frac(min((u - uA) * k.y, k.w)) * (pB - pA);
        float r3 = pB + (u - uB) * k.z;

        return s1 * r1 + s2 * r2 + s3 * r3;
    }

    // ── Filled 커버리지 (반평면 2개 테스트, atan2 없음) ──
    // C#이 fillAmount/fillMethod/fillOrigin/fillClockwise로 두 반평면과 결합 모드를 사전 계산.
    // lineA.w: 0 = 교집합(부채꼴 ≤180°), 1 = 합집합(Radial360에서 fill > 0.5)
    // lineB.w: AA 스케일 — (a,b)가 단위 벡터라 dot = uv 부호 거리, saturate(d*aa+0.5)로 ~1px 소프트 경계
    inline half _CAT_FillCoverage(float2 uv, float4 lineA, float4 lineB)
    {
        half aa = (half)lineB.w;
        half a = saturate((half)dot(float3(uv, 1.0), lineA.xyz) * aa + 0.5h);
        half b = saturate((half)dot(float3(uv, 1.0), lineB.xyz) * aa + 0.5h);
        return lerp(a * b, max(a, b), (half)lineA.w);
    }
    #endif

    // ── 마스크 샘플링 함수 (half precision, 분기 없음) ──

    inline half _CAT_SampleMask1(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 형태 대응: Filled 커버리지 (rect 정규화 좌표 기준 — 리매핑 이전에 적용)
        #if defined(CAT_SOFTMASK_USE_SLICE)
        inBounds *= _CAT_FillCoverage(maskUV, _MaskFillLineA, _MaskFillLineB);

        // 형태 대응: 9-slice / Tiled UV 리매핑
        maskUV = float2(
            _CAT_SliceRemap1D(maskUV.x, _MaskSliceBorder.x, _MaskSliceBorder.z, _MaskSliceInnerUV.x, _MaskSliceInnerUV.z, _MaskSliceSlopeX),
            _CAT_SliceRemap1D(maskUV.y, _MaskSliceBorder.y, _MaskSliceBorder.w, _MaskSliceInnerUV.y, _MaskSliceInnerUV.w, _MaskSliceSlopeY)
        );
        #endif

        float2 atlasUV = _MaskUVRect.xy + maskUV * _MaskUVRect.zw;

        // rect 밖은 마스크 알파 0으로 취급 (샘플링 후 곱하여 이웃 아틀라스 픽셀 유입 차단)
        // 인버트 시 rect 밖은 "알파 0의 반전 = 표시"가 되도록 반전 이전에 적용
        half maskAlpha = tex2D(_MaskTex, atlasUV).a * inBounds;
        // smoothstep(0, softness, a)와 동일하되 역수 사전 계산으로 픽셀당 나눗셈 제거
        half t = saturate(maskAlpha * _SoftnessRcp);
        half softEdge = t * t * (3.0h - 2.0h * t);
        return lerp(softEdge, 1.0h - softEdge, _InvertMask);
    }

    #if defined(_SOFTMASK_NESTED)
    inline half _CAT_SampleMask2(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 중첩 마스크 형태 대응: 마스크 1과 동일 키워드로 제어
        #if defined(CAT_SOFTMASK_USE_SLICE)
        inBounds *= _CAT_FillCoverage(maskUV, _MaskFillLineA2, _MaskFillLineB2);

        maskUV = float2(
            _CAT_SliceRemap1D(maskUV.x, _MaskSliceBorder2.x, _MaskSliceBorder2.z, _MaskSliceInnerUV2.x, _MaskSliceInnerUV2.z, _MaskSliceSlopeX2),
            _CAT_SliceRemap1D(maskUV.y, _MaskSliceBorder2.y, _MaskSliceBorder2.w, _MaskSliceInnerUV2.y, _MaskSliceInnerUV2.w, _MaskSliceSlopeY2)
        );
        #endif

        float2 atlasUV = _MaskUVRect2.xy + maskUV * _MaskUVRect2.zw;

        half maskAlpha = tex2D(_MaskTex2, atlasUV).a * inBounds;
        half t = saturate(maskAlpha * _SoftnessRcp2);
        half softEdge = t * t * (3.0h - 2.0h * t);
        return lerp(softEdge, 1.0h - softEdge, _InvertMask2);
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
