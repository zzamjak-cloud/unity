#ifndef CAT_NEON_GLOW_CORE_INCLUDED
#define CAT_NEON_GLOW_CORE_INCLUDED

// ============================================================================
// NeonGlow SDF 코어
//
// _MainTex 는 NeonSdfBaker 로 구운 SDF 텍스처다. R 채널 인코딩 규약:
//   R = 0.5 : 마스크 경계
//   R > 0.5 : 마스크 내부 (1.0 = 경계에서 spread 픽셀만큼 안쪽)
//   R < 0.5 : 마스크 외부 (0.0 = 경계에서 spread 픽셀만큼 바깥)
// 따라서 아래 모든 폭/반경 파라미터는 spread 를 1.0 으로 보는 정규화 단위다.
//
// 블렌딩은 프리멀티플라이드(Blend One OneMinusSrcAlpha)를 전제로 한다.
// 글로우는 alpha=0 이라 가산 합성되고 튜브 몸통만 알파를 가져 배경을 덮는다.
// 이 구조 덕분에 글로우가 원본 실루엣 밖으로 자유롭게 퍼진다.
//
// 모바일 주의사항:
//  - 플리커는 _Time 에만 의존하므로 반드시 정점에서 계산해 넘긴다. (CatNeonFlicker)
//  - 패딩된 쿼드는 대부분 화면에 기여하지 않으므로 _GlowCutoff 로 조기 폐기한다.
//  - 모든 머티리얼 프로퍼티는 UnityPerMaterial 에 있어야 SRP Batcher 가 동작한다.
// ============================================================================

sampler2D _MainTex;

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;

    half4 _Color;

    half4 _TubeColor;
    half4 _CoreColor;
    half4 _InnerGlowColor;
    half4 _OuterGlowColor;

    half _TubeWidth;
    half _TubeShading;
    half _TubeIntensity;
    half _TubeOpacity;
    half _CoreWidth;
    half _CoreSoftness;

    half _InnerGlowRadius;
    half _InnerGlowFalloff;
    half _InnerGlowIntensity;

    half _OuterGlowRadius;
    half _OuterGlowFalloff;
    half _OuterGlowIntensity;

    half _Exposure;

    // 글로우 기여가 사실상 0 이 되는 거리. NeonGlow 컴포넌트가 CPU 에서 계산해 넣는다.
    half _GlowCutoff;

    // 깜빡임: 0 = 없음, 1 = Hum, 2 = Broken, 3 = Warmup
    half _FlickerMode;
    half _FlickerSpeed;
    half _FlickerAmount;
    half _FlickerGlowOnly;
    half _WarmupDuration;
    float _StartTime;

    // 에디터 씬 뷰 프리뷰용. 빌드에서는 건드리지 않는다.
    half _UsePreviewTime;
    float _PreviewTime;
CBUFFER_END

// 편집 모드에서는 _Time 이 자유롭게 흐르지 않아 깜빡임이 재현되지 않는다.
// 프리뷰 중에는 드라이버가 넣어주는 시간을 대신 쓴다.
float CatNeonTime()
{
    return (_UsePreviewTime > 0.5h) ? _PreviewTime : _Time.y;
}

// ---------------------------------------------------------------------------
// 절차적 노이즈 (노이즈 텍스처 불필요)
// ---------------------------------------------------------------------------
float CatNeonHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

// 계단이 아닌 부드러운 1D 값 노이즈
float CatNeonValueNoise(float x)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(CatNeonHash11(i), CatNeonHash11(i + 1.0), f);
}

// ---------------------------------------------------------------------------
// 깜빡임. _Time 유니폼에만 의존하므로 화면 전체에서 값이 같다.
// 반드시 정점 셰이더에서 호출하고 보간기로 넘길 것. 프래그먼트에서 부르면
// 같은 값을 픽셀마다 다시 계산하게 되고, 이 함수가 프래그먼트 코드의 절반을 차지한다.
// ---------------------------------------------------------------------------
half CatNeonFlicker(float t)
{
    if (_FlickerMode < 0.5h) return 1.0h;

    half amount = saturate(_FlickerAmount);

    // Hum: 가스관의 미세한 떨림. 저주파 흔들림 + 전원 주파수 진동을 섞는다.
    if (_FlickerMode < 1.5h)
    {
        float s = t * _FlickerSpeed;
        float w = 0.55 * CatNeonValueNoise(s * 2.7)
                + 0.30 * CatNeonValueNoise(s * 9.3)
                + 0.15 * (0.5 + 0.5 * sin(s * 40.0));
        return lerp(1.0h, (half)(0.70 + 0.30 * w), amount);
    }

    // Broken: 스타터 불량. 불규칙한 계단형 점멸과 완전 소등이 섞인다.
    if (_FlickerMode < 2.5h)
    {
        float seg = floor(t * _FlickerSpeed * 9.0);
        float on  = CatNeonHash11(seg * 1.37);
        float dim = CatNeonHash11(seg * 3.71);
        if (on < lerp(0.0, 0.20, amount)) return 0.0h;
        return lerp(1.0h, (half)(0.50 + 0.50 * dim), amount);
    }

    // Warmup: 점등 직후 더듬다가 서서히 안정된다.
    float e = max(t - _StartTime, 0.0);
    float warm = saturate(e / max(_WarmupDuration, 0.0001h));
    float seg2 = floor(e * _FlickerSpeed * 14.0);
    half unstable = (CatNeonHash11(seg2 * 2.13) < lerp(0.0, 0.55, amount)) ? 0.0h : 1.0h;
    half gate = lerp(unstable, 1.0h, warm * warm);
    return gate * lerp(0.30h, 1.0h, warm);
}

// ---------------------------------------------------------------------------
// 본 셰이딩. rgb 는 프리멀티플라이드 결과, a 는 튜브 몸통의 커버리지.
// flicker 는 정점에서 계산해 넘긴 값이다.
// ---------------------------------------------------------------------------
half4 CatNeonShade(float2 uv, half flicker, half4 vertexColor)
{
    // d: +1 내부 깊숙이 / 0 경계 / -1 외부 끝
    half d = (tex2D(_MainTex, uv).r - 0.5h) * 2.0h;

    // 화면 공간 기울기 기반 안티앨리어싱. 확대해도 계단이 생기지 않는다.
    // clip 보다 먼저 구해야 파생값이 안전하다.
    half aa = max(fwidth(d) * 0.5h, 0.0001h);

    half outDist = max(-d, 0.0h);

    // 글로우 기여가 1/255 미만인 바깥 영역은 블렌딩 전에 버린다.
    // SDF 패딩 때문에 쿼드의 상당 부분이 여기 해당해서 모바일 오버드로가 크게 준다.
    clip(_GlowCutoff - outDist);

    // --- 유리관 몸통 ---
    half tubeMask = smoothstep(-aa, aa, d);

    // 관 단면의 둥근 굴곡. 경계에서 0, 관 중심에서 1 인 원 프로파일.
    half r = saturate(d / max(_TubeWidth, 0.0001h));
    half roundness = sqrt(saturate(r * (2.0h - r)));
    half shade = lerp(1.0h, roundness, saturate(_TubeShading));

    // 관 한가운데의 뜨거운 심지. 네온이 "타는" 느낌을 만드는 부분.
    // 원 프로파일(roundness)은 관 중앙이 평평해 코어 폭 조절이 잘 안 먹는다.
    // 경계 0, 중심 1 인 선형 r 을 기준으로 삼아야 슬라이더가 직관적으로 동작한다.
    half core = smoothstep(_CoreWidth - _CoreSoftness, _CoreWidth + _CoreSoftness, r);

    // --- 외곽 글로우 ---
    half inner = pow(saturate(1.0h - outDist / max(_InnerGlowRadius, 0.0001h)), _InnerGlowFalloff);
    half outer = pow(saturate(1.0h - outDist / max(_OuterGlowRadius, 0.0001h)), _OuterGlowFalloff);

    half tubeFlicker = (_FlickerGlowOnly > 0.5h) ? 1.0h : flicker;

    half3 glass = lerp(_TubeColor.rgb, _CoreColor.rgb, core);

    // 글로우는 유리관 뒤에서 나오는 빛이다. 관이 덮는 만큼 가려야
    // 관 자체의 색이 흰색으로 날아가지 않는다.
    half tubeAlpha = tubeMask * saturate(_TubeOpacity);
    half glowOcclusion = 1.0h - tubeAlpha;

    half3 glow = _InnerGlowColor.rgb * (inner * _InnerGlowIntensity)
               + _OuterGlowColor.rgb * (outer * _OuterGlowIntensity);

    half3 col = glass * (tubeMask * shade * _TubeIntensity * tubeFlicker);
    col += glow * (flicker * glowOcclusion);

    // HDR 로 밀어올려 URP Bloom 이 원거리 번짐을 담당하게 한다.
    col *= _Exposure * _Color.rgb * vertexColor.rgb;

    half alpha = tubeAlpha * _Color.a * vertexColor.a;
    col *= _Color.a * vertexColor.a;

    return half4(col, alpha);
}

#endif // CAT_NEON_GLOW_CORE_INCLUDED
