Shader "CAT/UI/VerticalFlipUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SecondTex ("Second Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _FlipProgress ("Flip Progress", Range(0, 1)) = 0
        _SliceCount ("Slice Count", Range(1, 50)) = 6
        _FlipDuration ("Flip Duration", Range(0.1, 2.0)) = 0.2
        _FlipOffset ("Flip Offset", Range(0, 1)) = 0.1
        
        // 라인 관련 속성
        [Toggle] _ShowLines ("Show Column Lines", Float) = 1
        _LineColor ("Line Color", Color) = (0, 0, 0, 1)
        _LineWidth ("Line Width", Range(0.001, 0.05)) = 0.05
        
        // UGUI 마스크에 필요한 속성들
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            Name "Default"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            sampler2D _SecondTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float _FlipProgress;
            float _SliceCount;
            float _FlipDuration;
            float _FlipOffset;
            
            // 라인 관련 변수
            float _ShowLines;
            float4 _LineColor;
            float _LineWidth;
            
            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                // 버텍스 위치 및 기본 설정
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                
                return OUT;
            }
            
            // 최적화된 cos 함수 - 모바일 성능 개선
            float fastCos(float x) {
                return cos(x);
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                // 마스킹 및 클리핑 처리
                float alphaClip = 1.0;
                #ifdef UNITY_UI_CLIP_RECT
                alphaClip = UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                // 슬라이스 정보 계산
                float sliceWidth = 1.0 / _SliceCount;
                float sliceIndex = floor(IN.texcoord.x / sliceWidth);
                
                // 플립 애니메이션 진행 - 각 슬라이스마다 시간차를 두고 적용
                float delayedProgress = _FlipProgress - (sliceIndex * _FlipOffset);
                delayedProgress = saturate(delayedProgress / _FlipDuration);
                
                // 텍스처 샘플링을 위한 기본 UV 좌표
                float2 uv = IN.texcoord;
                float flipAngle = delayedProgress * 3.14159; // 0에서 π까지
                
                // 결과 색상 변수
                fixed4 color;
                
                // 애니메이션 진행 상태에 따른 처리
                if (delayedProgress <= 0.0)
                {
                    // 플립 전 - 첫 번째 텍스처 표시
                    color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                }
                else if (delayedProgress >= 1.0)
                {
                    // 플립 후 - 두 번째 텍스처 표시
                    color = (tex2D(_SecondTex, uv) + _TextureSampleAdd) * IN.color;
                }
                else
                {
                    // 플립 중 - 수직 회전 효과 (스프라이트 셰이더와 동일한 효과)
                    if (flipAngle < 1.57079) // π/2보다 작으면
                    {
                        // 첫 번째 텍스처 회전
                        float horizontalStretch = fastCos(flipAngle);
                        float2 adjustedUV = float2((uv.x - 0.5) / horizontalStretch + 0.5, uv.y);
                        
                        // 경계 확인 및 텍스처 샘플링
                        if (adjustedUV.x >= 0.0 && adjustedUV.x <= 1.0)
                            color = (tex2D(_MainTex, adjustedUV) + _TextureSampleAdd) * IN.color;
                        else
                            color = fixed4(0, 0, 0, 0); // 투명 처리 (UI는 투명도 지원)
                    }
                    else
                    {
                        // 두 번째 텍스처 회전
                        float horizontalStretch = fastCos(3.14159 - flipAngle);
                        float2 adjustedUV = float2((uv.x - 0.5) / horizontalStretch + 0.5, uv.y);
                        
                        // 경계 확인 및 텍스처 샘플링
                        if (adjustedUV.x >= 0.0 && adjustedUV.x <= 1.0)
                            color = (tex2D(_SecondTex, adjustedUV) + _TextureSampleAdd) * IN.color;
                        else
                            color = fixed4(0, 0, 0, 0); // 투명 처리 (UI는 투명도 지원)
                    }
                }
                
                // 컬럼 라인 그리기
                if (_ShowLines > 0.5)
                {
                    // 슬라이스 경계까지의 거리 계산
                    float distToSliceBoundary = frac(IN.texcoord.x / sliceWidth);
                    
                    // 슬라이스 경계에 라인 그리기
                    if (distToSliceBoundary < _LineWidth || (1.0 - distToSliceBoundary) < _LineWidth)
                    {
                        float lineDistance = min(distToSliceBoundary, 1.0 - distToSliceBoundary);
                        float blend = saturate(lineDistance / _LineWidth);
                        
                        // 라인 색상과 알파 처리
                        fixed4 lineCol = _LineColor;
                        lineCol.a *= color.a; // 텍스처의 알파 값 유지
                        
                        color = lerp(lineCol, color, blend);
                    }
                }
                
                // 클리핑 및 알파 처리
                color.a *= alphaClip;
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                
                return color;
            }
            ENDCG
        }
    }
    
    // 폴백 셰이더 - 렌더링 문제 시 기본 UI 셰이더 사용
    Fallback "UI/Default"
}