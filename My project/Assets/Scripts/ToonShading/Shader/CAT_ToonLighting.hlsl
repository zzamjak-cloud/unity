#ifndef CAT_TOON_LIGHTING_INCLUDED
#define CAT_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "CAT_ToonInput.hlsl"

// 툰 셰이딩에 필요한 표면 정보
struct CATToonSurface
{
    half3  albedo;
    half   alpha;
    half3  emission;
    float2 screenPixel;   // 스케치 해칭용 픽셀 좌표 (positionCS.xy)
};

// ---------------------------------------------------------------------------
// 톤 분할 유틸
// ---------------------------------------------------------------------------

// 하프 램버트와 일반 램버트를 _HalfLambert 로 블렌딩한 뒤 그림자 감쇠를 곱한다.
// 하프 램버트를 쓰면 명암 경계가 뒤쪽으로 밀려나 그림자 영역이 과도하게 어두워지지 않는다.
half CAT_LightValue(half3 normalWS, half3 lightDir, half attenuation)
{
    half ndl     = dot(normalWS, lightDir);
    half lambert = lerp(saturate(ndl), ndl * 0.5h + 0.5h, _HalfLambert);
    return saturate(lambert * lerp(1.0h, attenuation, _ReceiveShadowStrength));
}

// 부드러운 계단 함수. smoothness 가 0 이면 완전한 하드 엣지 2톤이 된다.
half CAT_Tone(half value, half threshold, half smoothness)
{
    half s = max(smoothness, HALF_MIN);
    return smoothstep(threshold - s, threshold + s, value);
}

// 밝은 톤 / (선택) 중간 톤 / 그림자 톤을 합성한다.
// _ShadeIntensity 가 그림자의 "깊이"를 제어하므로 0 에 가까울수록 그림자가 밝게 남는다.
half3 CAT_ApplyToneRamp(half3 albedo, half toneLit, half toneMid)
{
    half3 shadeAlbedo = albedo * lerp(half3(1.0h, 1.0h, 1.0h), _ShadeColor.rgb, _ShadeIntensity);

#ifdef _MIDTONE_ON
    half3 midAlbedo = albedo * lerp(half3(1.0h, 1.0h, 1.0h), _MidColor.rgb, _ShadeIntensity);
    half3 result    = lerp(shadeAlbedo, midAlbedo, toneMid);
    return lerp(result, albedo, toneLit);
#else
    return lerp(shadeAlbedo, albedo, toneLit);
#endif
}

// ---------------------------------------------------------------------------
// 툰 스페큘러 / 림 라이트
// ---------------------------------------------------------------------------

half CAT_ToonSpecular(half3 normalWS, half3 lightDir, half3 viewDirWS)
{
    half3 halfVec = SafeNormalize(lightDir + viewDirWS);
    half  ndh     = saturate(dot(normalWS, halfVec));
    // _SpecularSize 가 클수록 하이라이트가 넓어진다.
    half  power   = exp2(lerp(11.0h, 1.0h, saturate(_SpecularSize)));
    half  raw     = pow(ndh, power);
    half  s       = max(_SpecularSmooth, HALF_MIN);
    return smoothstep(0.5h - s, 0.5h + s, raw);
}

// 뷰 기준 프레넬 림. _RimLightAlign 으로 광원 쪽 림만 남기도록 가중할 수 있다.
half CAT_ViewRim(half3 normalWS, half3 viewDirWS, half3 lightDir)
{
    half fresnel   = 1.0h - saturate(dot(normalWS, viewDirWS));
    half threshold = 1.0h - saturate(_RimWidth);
    half s         = max(_RimSmooth, HALF_MIN);
    half rim       = smoothstep(threshold - s, threshold + s, fresnel);

    half align = lerp(1.0h, saturate(dot(normalWS, lightDir) * 0.5h + 0.5h), saturate(_RimLightAlign));
    return rim * align;
}

// ---------------------------------------------------------------------------
// 스케치(해칭) 오버레이 — 스크린 스페이스 라인
// ---------------------------------------------------------------------------

// 한 방향의 평행선 패턴을 만든다. 0 = 선 위, 1 = 빈 공간.
half CAT_HatchLine(float2 pixelPos, half angleDeg, half scale, half width)
{
    half   rad = radians(angleDeg);
    float2 dir = float2(cos(rad), sin(rad));
    float  v   = dot(pixelPos, dir) / max(scale, 1.0);
    half   tri = abs(frac(v) - 0.5) * 2.0h;       // 0..1 삼각파
    return smoothstep(width - 0.2h, width + 0.2h, tri);
}

// 그림자 영역에 해칭을 얹는다. 깊은 그림자에는 교차 해칭이 추가된다.
half3 CAT_ApplySketch(half3 color, float2 pixelPos, half toneLit, half toneMid)
{
#ifdef _SKETCH_ON
    half strength = saturate(_SketchStrength);
    if (strength <= 0.0h)
        return color;

    half lines;
    if (_SketchUseTexture > 0.5h)
    {
        half2 uv = pixelPos / max(_SketchScale * 8.0h, 1.0h);
        lines = SAMPLE_TEXTURE2D(_SketchMap, sampler_SketchMap, uv).r;
    }
    else
    {
        lines = CAT_HatchLine(pixelPos, _SketchAngle, _SketchScale, _SketchWidth);
    }

    // 밝은 영역은 그대로 두고 그림자로 갈수록 선이 진해진다.
    half shadowMask = (1.0h - toneLit) * strength;
    half result     = lerp(1.0h, lines, shadowMask);

    // 가장 어두운 구간에는 직교 방향 해칭을 한 겹 더 얹어 밀도를 올린다.
    half deepMask   = (1.0h - toneMid) * strength;
    half crossLines = CAT_HatchLine(pixelPos, _SketchAngle + 90.0h, _SketchScale, _SketchWidth);
    result *= lerp(1.0h, crossLines, deepMask);

    return lerp(color * _SketchColor.rgb, color, saturate(result));
#else
    return color;
#endif
}

// ---------------------------------------------------------------------------
// 메인 셰이딩
// ---------------------------------------------------------------------------

// 파라미터 이름이 반드시 inputData 여야 한다. Forward+ 의 LIGHT_LOOP_BEGIN 매크로가
// inputData.positionWS / inputData.normalizedScreenSpaceUV 를 직접 참조한다.
half4 CAT_ToonShade(InputData inputData, CATToonSurface surf)
{
    half ssao = 1.0h;
#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    ssao = lerp(1.0h, aoFactor.directAmbientOcclusion, saturate(_OcclusionStrength));
#endif

    half4 shadowMask = CalculateShadowMask(inputData);
    Light mainLight  = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;

    half mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation * ssao;
    half lightVal  = CAT_LightValue(N, mainLight.direction, mainAtten);

    half toneLit = CAT_Tone(lightVal, _ShadeThreshold, _ShadeSmooth);
    half toneMid = CAT_Tone(lightVal, _MidThreshold,  _MidSmooth);

    half3 color = CAT_ApplyToneRamp(surf.albedo, toneLit, toneMid) * mainLight.color;

    // 환경광 — 그림자가 새까맣게 죽지 않도록 항상 더해준다.
    color += inputData.bakedGI * surf.albedo * _AmbientStrength;

#ifdef _TOONSPECULAR_ON
    half spec = CAT_ToonSpecular(N, mainLight.direction, V) * toneLit;
    color += spec * _SpecularColor.rgb * _SpecularColor.a * mainLight.color;
#endif

    // 추가 광원 — 메인 라이트와 같은 톤 분할을 적용해 밴딩이 일관되게 유지된다.
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        half  v     = CAT_LightValue(N, light.direction, light.shadowAttenuation);
        half  t     = CAT_Tone(v, _ShadeThreshold, _ShadeSmooth);
        color += surf.albedo * light.color * t * light.distanceAttenuation * ssao;
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        half  v     = CAT_LightValue(N, light.direction, light.shadowAttenuation);
        half  t     = CAT_Tone(v, _ShadeThreshold, _ShadeSmooth);
        color += surf.albedo * light.color * t * light.distanceAttenuation * ssao;
    LIGHT_LOOP_END
#endif

#ifdef _RIM_ON
    half rim = CAT_ViewRim(N, V, mainLight.direction);
    color += rim * _RimColor.rgb * _RimColor.a;
#endif

    color = CAT_ApplySketch(color, surf.screenPixel, toneLit, toneMid);
    color += surf.emission;

    return half4(color, surf.alpha);
}

#endif // CAT_TOON_LIGHTING_INCLUDED
