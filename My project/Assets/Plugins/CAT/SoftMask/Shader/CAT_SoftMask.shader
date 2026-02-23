Shader "CAT/UI/SoftMask"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        // 마스크 파라미터
        _MaskTex("Mask Texture", 2D) = "white" {}
        _Softness("Softness", Range(0, 1)) = 0.1
        _InvertMask("Invert Mask", Float) = 0
        _MaskUVRect("Mask UV Rect", Vector) = (0, 0, 1, 1)

        // 슬라이스 마스크 파라미터 (Image.Type.Sliced 대응)
        [HideInInspector] _MaskSliceBorder("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)

        // 중첩 마스크 파라미터
        [HideInInspector] _MaskTex2("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _Softness2("Softness 2", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask2("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2("Mask UV Rect 2", Vector) = (0, 0, 1, 1)

        // 중첩 슬라이스 마스크 파라미터
        [HideInInspector] _MaskSliceBorder2("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)

        // UI 스텐실/마스크 설정
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    sampler2D _MaskTex;
    half _Softness;
    half _InvertMask;
    float4x4 _MaskWorldToUV;
    float4 _MaskUVRect; // (minU, minV, rangeU, rangeV) - Atlas UV 보정

    #if defined(_SOFTMASK_NESTED)
    sampler2D _MaskTex2;
    half _Softness2;
    half _InvertMask2;
    float4x4 _MaskWorldToUV2;
    float4 _MaskUVRect2;
    #endif

    // 슬라이스 유니폼 (마스크 1)
    #if defined(_SOFTMASK_SLICE)
    float4 _MaskSliceBorder;   // (leftBreak, bottomBreak, rightBreak, topBreak) rect 정규화 [0,1]
    float4 _MaskSliceInnerUV;  // (innerLeft, innerBottom, innerRight, innerTop) 스프라이트 UV [0,1]
    #endif

    // 슬라이스 유니폼 (마스크 2, 중첩)
    #if defined(_SOFTMASK_NESTED) && defined(_SOFTMASK_NESTED_SLICE)
    float4 _MaskSliceBorder2;
    float4 _MaskSliceInnerUV2;
    #endif

    // 9-슬라이스 1D 리매핑 (브랜치 없음, 모바일 최적화)
    // u:  입력 [0,1] (rect 정규화 좌표)
    // uA: 왼쪽/아래쪽 break point (rect 공간)
    // uB: 오른쪽/위쪽 break point (rect 공간, = 1 - 오른쪽/위쪽 테두리 비율)
    // pA: 왼쪽/아래쪽 inner UV break point (스프라이트 UV 공간)
    // pB: 오른쪽/위쪽 inner UV break point (스프라이트 UV 공간)
    inline float SliceRemap1D(float u, float uA, float uB, float pA, float pB)
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

    // 마스크 1 샘플링 (half precision, 분기 없음)
    inline half SampleMask1(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 슬라이스 타입: 9-slice UV 리매핑 적용
        #if defined(_SOFTMASK_SLICE)
        maskUV = float2(
            SliceRemap1D(maskUV.x, _MaskSliceBorder.x, _MaskSliceBorder.z, _MaskSliceInnerUV.x, _MaskSliceInnerUV.z),
            SliceRemap1D(maskUV.y, _MaskSliceBorder.y, _MaskSliceBorder.w, _MaskSliceInnerUV.y, _MaskSliceInnerUV.w)
        );
        #endif

        // Atlas 스프라이트 UV 보정
        float2 atlasUV = _MaskUVRect.xy + maskUV * _MaskUVRect.zw;

        half maskAlpha = tex2D(_MaskTex, atlasUV).a;
        half softEdge = smoothstep(0.0h, max(_Softness, 0.001h), maskAlpha);
        half finalMask = lerp(softEdge, 1.0h - softEdge, _InvertMask);
        return finalMask * inBounds;
    }

    // 중첩 마스크 2 샘플링
    #if defined(_SOFTMASK_NESTED)
    inline half SampleMask2(float2 maskUV)
    {
        half inBounds = step(0.0h, maskUV.x) * step(maskUV.x, 1.0h)
                      * step(0.0h, maskUV.y) * step(maskUV.y, 1.0h);

        // 중첩 마스크 슬라이스 타입: 9-slice UV 리매핑 적용
        #if defined(_SOFTMASK_NESTED_SLICE)
        maskUV = float2(
            SliceRemap1D(maskUV.x, _MaskSliceBorder2.x, _MaskSliceBorder2.z, _MaskSliceInnerUV2.x, _MaskSliceInnerUV2.z),
            SliceRemap1D(maskUV.y, _MaskSliceBorder2.y, _MaskSliceBorder2.w, _MaskSliceInnerUV2.y, _MaskSliceInnerUV2.w)
        );
        #endif

        float2 atlasUV = _MaskUVRect2.xy + maskUV * _MaskUVRect2.zw;

        half maskAlpha = tex2D(_MaskTex2, atlasUV).a;
        half softEdge = smoothstep(0.0h, max(_Softness2, 0.001h), maskAlpha);
        half finalMask = lerp(softEdge, 1.0h - softEdge, _InvertMask2);
        return finalMask * inBounds;
    }
    #endif
    ENDCG

    // SubShader 0: UI
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE
            #pragma target 2.0

            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                half4 color          : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 maskUV        : TEXCOORD2;
                #if defined(_SOFTMASK_NESTED)
                float2 maskUV2       : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex; // UI 클리핑용 (Canvas 로컬 좌표)
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;

                // 월드 좌표 → 마스크 UV (버텍스에서 계산, 프래그먼트 비용 절감)
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                OUT.maskUV = mul(_MaskWorldToUV, float4(worldPos, 1)).xy;

                #if defined(_SOFTMASK_NESTED)
                OUT.maskUV2 = mul(_MaskWorldToUV2, float4(worldPos, 1)).xy;
                #endif

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                color.a *= SampleMask1(IN.maskUV);

                #if defined(_SOFTMASK_NESTED)
                color.a *= SampleMask2(IN.maskUV2);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDCG
        }
    }

    // SubShader 1: Sprite
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        ColorMask[_ColorMask]
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE
            #pragma target 2.0

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 maskUV   : TEXCOORD1;
                #if defined(_SOFTMASK_NESTED)
                float2 maskUV2  : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;

                float3 worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                OUT.maskUV = mul(_MaskWorldToUV, float4(worldPos, 1)).xy;

                #if defined(_SOFTMASK_NESTED)
                OUT.maskUV2 = mul(_MaskWorldToUV2, float4(worldPos, 1)).xy;
                #endif

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                color.a *= SampleMask1(IN.maskUV);

                #if defined(_SOFTMASK_NESTED)
                color.a *= SampleMask2(IN.maskUV2);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
