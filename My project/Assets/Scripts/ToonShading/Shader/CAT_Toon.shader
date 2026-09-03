Shader "CAT/Toon/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0.0, 2.0)) = 1.0

        // --- 2톤 셰이딩 ---------------------------------------------------
        _ShadeColor ("Shade Color", Color) = (0.55, 0.6, 0.78, 1)
        _ShadeThreshold ("Shade Threshold", Range(0.0, 1.0)) = 0.55
        _ShadeSmooth ("Shade Smoothness", Range(0.0, 0.5)) = 0.03
        _ShadeIntensity ("Shade Intensity", Range(0.0, 1.0)) = 0.55
        _HalfLambert ("Half Lambert", Range(0.0, 1.0)) = 1.0
        _ReceiveShadowStrength ("Receive Shadow Strength", Range(0.0, 1.0)) = 0.8
        _OcclusionStrength ("SSAO Strength", Range(0.0, 1.0)) = 0.5
        _AmbientStrength ("Ambient Strength", Range(0.0, 2.0)) = 1.0

        [Toggle(_MIDTONE_ON)] _MidToneEnabled ("Enable Mid Tone (3톤)", Float) = 0
        _MidColor ("Mid Tone Color", Color) = (0.78, 0.8, 0.9, 1)
        _MidThreshold ("Mid Tone Threshold", Range(0.0, 1.0)) = 0.32
        _MidSmooth ("Mid Tone Smoothness", Range(0.0, 0.5)) = 0.03

        // --- 툰 스페큘러 ---------------------------------------------------
        [Toggle(_TOONSPECULAR_ON)] _SpecularEnabled ("Enable Toon Specular", Float) = 1
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 0.6)
        _SpecularSize ("Specular Size", Range(0.0, 1.0)) = 0.35
        _SpecularSmooth ("Specular Smoothness", Range(0.0, 0.5)) = 0.04

        // --- 림 라이트 -----------------------------------------------------
        [Toggle(_RIM_ON)] _RimEnabled ("Enable Rim Light", Float) = 1
        [HDR] _RimColor ("Rim Color", Color) = (1, 0.95, 0.85, 0.8)
        _RimWidth ("Rim Width", Range(0.0, 1.0)) = 0.35
        _RimSmooth ("Rim Smoothness", Range(0.0, 0.5)) = 0.08
        _RimLightAlign ("Align To Light", Range(0.0, 1.0)) = 0.5

        // --- 스케치 해칭 ---------------------------------------------------
        [Toggle(_SKETCH_ON)] _SketchEnabled ("Enable Sketch Hatching", Float) = 0
        _SketchColor ("Sketch Line Color", Color) = (0.35, 0.35, 0.45, 1)
        _SketchScale ("Line Spacing (px)", Range(1.0, 40.0)) = 7.0
        _SketchAngle ("Line Angle", Range(0.0, 180.0)) = 45.0
        _SketchWidth ("Line Width", Range(0.0, 1.0)) = 0.45
        _SketchStrength ("Sketch Strength", Range(0.0, 1.0)) = 0.6
        [Toggle] _SketchUseTexture ("Use Sketch Texture", Float) = 0
        _SketchMap ("Sketch Texture (R)", 2D) = "white" {}

        // --- 이미시브 -------------------------------------------------------
        [Toggle(_EMISSION)] _EmissionEnabled ("Enable Emission", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // --- 렌더 상태 ------------------------------------------------------
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        [HideInInspector] _QueueOffset ("Queue Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue"          = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ===================================================================
        // Forward — 툰 라이팅 본 패스
        // ===================================================================
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ToonPassVertex
            #pragma fragment ToonPassFragment

            // 머티리얼 기능 토글
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _MIDTONE_ON
            #pragma shader_feature_local_fragment _TOONSPECULAR_ON
            #pragma shader_feature_local_fragment _RIM_ON
            #pragma shader_feature_local_fragment _SKETCH_ON

            // URP 라이팅 키워드
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "CAT_ToonInput.hlsl"
            #include "CAT_ToonForwardPass.hlsl"
            ENDHLSL
        }

        // ===================================================================
        // ShadowCaster — 다른 오브젝트에 그림자를 드리우기 위한 패스
        // ===================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "CAT_ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ===================================================================
        // DepthOnly — 카메라 Depth Texture 생성용
        // ===================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "CAT_ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ===================================================================
        // DepthNormals — 아웃라인 엣지 검출에 쓰는 _CameraNormalsTexture 생성용
        // ===================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "CAT_ToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
    CustomEditor "CAT.Toon.Editor.CATToonShaderGUI"
}
