#ifndef CAT_TOON_INPUT_INCLUDED
#define CAT_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// SRP Batcher 호환을 위해 모든 머티리얼 프로퍼티는 이 CBUFFER 안에만 선언한다.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;
    half   _NormalScale;

    // 2톤 셰이딩
    half4  _ShadeColor;
    half4  _MidColor;
    half   _ShadeThreshold;
    half   _ShadeSmooth;
    half   _MidThreshold;
    half   _MidSmooth;
    half   _HalfLambert;
    half   _ShadeIntensity;
    half   _ReceiveShadowStrength;
    half   _OcclusionStrength;
    half   _AmbientStrength;

    // 툰 스페큘러
    half4  _SpecularColor;
    half   _SpecularSize;
    half   _SpecularSmooth;

    // 림 라이트
    half4  _RimColor;
    half   _RimWidth;
    half   _RimSmooth;
    half   _RimLightAlign;

    // 스케치 해칭
    half4  _SketchColor;
    half   _SketchScale;
    half   _SketchAngle;
    half   _SketchWidth;
    half   _SketchStrength;
    half   _SketchUseTexture;

    half4  _EmissionColor;
CBUFFER_END

// _BaseMap / _BumpMap / _EmissionMap 및 샘플러는 SurfaceInput.hlsl 이 이미 선언한다.
TEXTURE2D(_SketchMap); SAMPLER(sampler_SketchMap);

#endif // CAT_TOON_INPUT_INCLUDED
