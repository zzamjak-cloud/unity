#ifndef CAT_TOON_FORWARD_PASS_INCLUDED
#define CAT_TOON_FORWARD_PASS_INCLUDED

#include "CAT_ToonLighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS       : POSITION;
    float3 normalOS         : NORMAL;
    float4 tangentOS        : TANGENT;
    float2 texcoord         : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV: TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                          : TEXCOORD0;
    float3 positionWS                  : TEXCOORD1;

    #ifdef _NORMALMAP
        half4 normalWS                 : TEXCOORD2;   // xyz: normal,    w: viewDir.x
        half4 tangentWS                : TEXCOORD3;   // xyz: tangent,   w: viewDir.y
        half4 bitangentWS              : TEXCOORD4;   // xyz: bitangent, w: viewDir.z
    #else
        half3 normalWS                 : TEXCOORD2;
    #endif

    half  fogFactor                    : TEXCOORD5;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord             : TEXCOORD6;
    #endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);

    #ifdef DYNAMICLIGHTMAP_ON
        float2 dynamicLightmapUV       : TEXCOORD8;
    #endif

    #ifdef USE_APV_PROBE_OCCLUSION
        float4 probeOcclusion          : TEXCOORD9;
    #endif

    float4 positionCS                  : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeToonInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    #ifdef _NORMALMAP
        half3 viewDirWS = half3(input.normalWS.w, input.tangentWS.w, input.bitangentWS.w);
        inputData.tangentToWorld = half3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(inputData.positionWS);
        inputData.normalWS = input.normalWS;
    #endif

    inputData.normalWS        = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = SafeNormalize(viewDirWS);

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = input.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    inputData.fogCoord               = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting         = half3(0, 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
        #if defined(DYNAMICLIGHTMAP_ON)
            inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
        #endif
        #if defined(LIGHTMAP_ON)
            inputData.staticLightmapUV = input.staticLightmapUV;
        #else
            inputData.vertexSH = input.vertexSH;
        #endif
        #if defined(USE_APV_PROBE_OCCLUSION)
            inputData.probeOcclusion = input.probeOcclusion;
        #endif
    #endif
}

void InitializeToonBakedGI(Varyings input, inout InputData inputData)
{
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI    = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI    = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

Varyings ToonPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv            = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS    = vertexInput.positionWS;
    output.positionCS    = vertexInput.positionCS;

#ifdef _NORMALMAP
    half3 viewDirWS      = GetWorldSpaceViewDir(vertexInput.positionWS);
    output.normalWS      = half4(normalInput.normalWS,    viewDirWS.x);
    output.tangentWS     = half4(normalInput.tangentWS,   viewDirWS.y);
    output.bitangentWS   = half4(normalInput.bitangentWS, viewDirWS.z);
#else
    output.normalWS      = NormalizeNormalPerVertex(normalInput.normalWS);
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

#if defined(_FOG_FRAGMENT)
    output.fogFactor = 0;
#else
    output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    return output;
}

void ToonPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    half4 baseSample = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
    half  alpha      = Alpha(baseSample.a, _BaseColor, _Cutoff);

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
#ifdef _NORMALMAP
    normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _NormalScale);
#endif

    InputData inputData;
    InitializeToonInputData(input, normalTS, inputData);
    InitializeToonBakedGI(input, inputData);

    CATToonSurface surf;
    surf.albedo      = baseSample.rgb;
    surf.alpha       = alpha;
    surf.emission    = SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    surf.screenPixel = input.positionCS.xy;

    half4 color = CAT_ToonShade(inputData, surf);
    color.rgb   = MixFog(color.rgb, inputData.fogCoord);

#ifdef _SURFACE_TYPE_TRANSPARENT
    outColor = half4(color.rgb, alpha);
#else
    outColor = half4(color.rgb, 1.0h);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers   = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

#endif // CAT_TOON_FORWARD_PASS_INCLUDED
