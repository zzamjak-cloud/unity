// [OptionalShader] SoftMaskLight: CAT/Effects/Windable
// CAT/Effects/Windable 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
Shader "Hidden/CAT/Effects/Windable (SoftMaskLight)"
{
    Properties
    {
        [HideInInspector]
        _MainTex ("Main Texture", 2D) = "white" {}
        _RotateUV ("Rotate UV", Range(0, 360)) = 0
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Float) = 0.2

        _WindStrength ("Wind Strength", Float) = 1.0
        _WindFrequency ("Wind Frequency", Float) = 0.2
        _WindDirection ("Wind Direction", Vector) = (1, 1, 0, 0)
        _ClipRect ("Clip Rect", Vector) = (-2147.0, -2147.0, 2147.0, 2147.0)
        _WindScale ("Noise Scale", Float) = 1.0
        _ImageOffsetX ("Image Offset X", Float) = 0.0

        _ImageOffsetY ("Image Offset Y", Float) = 0.0
        _ImageScale ("Image Scale", Float) = 1

        [HideInInspector] _CustomTime ("Custom Time", Float) = 0

        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SpritePivot ("Sprite Pivot", Vector) = (0.5, 0.5, 0, 0)
        [HideInInspector] _NormalizedWindDir ("Normalized Wind Dir", Vector) = (1, 0, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // SoftMaskLight 프로퍼티
        [HideInInspector] _MaskTex ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _SoftnessRcp ("Softness Rcp", Float) = 10
        [HideInInspector] _InvertMask ("Invert Mask", Float) = 0
        [HideInInspector] _MaskUVRect ("Mask UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder ("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV ("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX ("Mask Slice Slope X", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY ("Mask Slice Slope Y", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA ("Mask Fill Line A", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB ("Mask Fill Line B", Vector) = (0, 0, 1, 10000)
        [HideInInspector] _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _SoftnessRcp2 ("Softness Rcp 2", Float) = 10
        [HideInInspector] _InvertMask2 ("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder2 ("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2 ("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX2 ("Mask Slice Slope X 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY2 ("Mask Slice Slope Y 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA2 ("Mask Fill Line A 2", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB2 ("Mask Fill Line B 2", Vector) = (0, 0, 1, 10000)
    }

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
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE

            // SoftMaskLight 항상 활성화
            #define _CAT_SOFTMASK 1
            #include "../SoftMaskLight_Core.cginc"

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                CAT_SOFTMASK_COORDS(2, 3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float _RotateUV;
            float _WindSpeed;
            float _WindStrength;
            float _WindFrequency;
            float2 _WindDirection;
            float4 _ClipRect;
            float _WindScale;
            float _ImageOffsetX;
            float _ImageOffsetY;
            float _ImageScale;
            float4 _SpriteUVRect;
            float4 _SpritePivot;
            float _CustomTime;
            float2 _NormalizedWindDir; // C#에서 사전 계산된 정규화 바람 방향

            float2 RotateUV(float2 uv, float angle, float2 pivot)
            {
                float rad = angle * UNITY_PI / 180.0;
                float cosA = cos(rad);
                float sinA = sin(rad);
                float2x2 rotationMatrix = float2x2(cosA, - sinA, sinA, cosA);
                return mul(rotationMatrix, uv - pivot) + pivot;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // 유니폼 각도 회전은 UV에 대해 선형이므로 버텍스에서 계산 (픽셀당 sin/cos 제거)
                float2 spriteCenter = (_SpriteUVRect.xy + _SpriteUVRect.zw) * 0.5;
                o.uv = RotateUV(TRANSFORM_TEX(v.uv, _MainTex), _RotateUV, spriteCenter);
                o.color = v.color;

                // SoftMaskLight 버텍스 처리 (항상 활성)
                CAT_SOFTMASK_VERT(v.vertex.xyz, o)

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ClipRect 사각형 판정을 단일 clip으로 통합 (타일 GPU discard 부하 감소)
                float2 clipTestPoint = i.worldPosition.xy;
                float2 c1 = clipTestPoint - _ClipRect.xy;
                float2 c2 = _ClipRect.zw - clipTestPoint;
                clip(min(min(c1.x, c1.y), min(c2.x, c2.y)));

                float2 rotatedUV = i.uv;

                float timeOffset = _CustomTime * _WindSpeed * _WindFrequency;
                float2 noiseSampleUV = (rotatedUV + _WindDirection * timeOffset) * _WindScale;
                float noiseValue = tex2D(_NoiseTex, noiseSampleUV).r;
                float windEffect = noiseValue * _WindStrength * 0.1;
                float2 windOffset = _NormalizedWindDir * windEffect;

                float2 pivot = _SpritePivot.xy;
                float2 centeredUV = rotatedUV - pivot;

                centeredUV += windOffset;
                centeredUV = centeredUV * (1.0 - windEffect * 0.5);
                centeredUV *= _ImageScale;
                centeredUV += pivot;

                centeredUV.x += _ImageOffsetX * - 0.1;
                centeredUV.y += _ImageOffsetY * - 0.1;

                float2 finalUV = centeredUV;

                // 스프라이트 UV 경계 판정도 단일 clip으로 통합
                float2 s1 = finalUV - _SpriteUVRect.xy;
                float2 s2 = _SpriteUVRect.zw - finalUV;
                clip(min(min(s1.x, s1.y), min(s2.x, s2.y)));

                fixed4 col = tex2D(_MainTex, finalUV);
                col *= i.color;

                // SoftMaskLight 적용 (항상 활성)
                col.a *= CAT_SOFTMASK_FRAG(i);

                return col;
            }
            ENDCG
        }
    }
}
