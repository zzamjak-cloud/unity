Shader "CAT/Effects/OldTVEffect_UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "black" {} // 사전 계산된 노이즈 텍스처
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.5
        _NoiseScale ("Noise Scale", Range(0.1, 10.0)) = 3.0 // 노이즈 스케일 조정
        _ScanLineIntensity ("Scan Line Intensity", Range(0, 1)) = 0.5
        _ScanLineCount ("Scan Line Count", Float) = 100
        _ScanLineThickness ("Scan Line Thickness", Range(0.1, 5.0)) = 1.0
        _VerticalJitter ("Vertical Jitter", Range(0, 0.1)) = 0.01
        _HorizontalJitter ("Horizontal Jitter", Range(0, 0.1)) = 0.01 // 수평 지터 추가
        _ColorBleed ("Color Bleed", Range(0, 0.5)) = 0.1 // 범위 확장 및 기본값 증가
        _ColorBleedOffset ("Color Bleed Offset", Range(0, 0.1)) = 0.02 // 오프셋 제어
        _RollSpeed ("Roll Speed", Range(0, 5)) = 1.0
        
        // UI 셰이더 필수 속성
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
        Tags { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        LOD 100
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            // 모바일 최적화 프래그마
            #pragma fragmentoption ARB_precision_hint_fastest
            
            // UI 마스크 지원
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD2;  // UI 마스킹용 월드 포지션
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _MainTex_ST;
            float4 _NoiseTex_ST;
            half _NoiseIntensity;
            half _NoiseScale;
            half _ScanLineIntensity;
            half _ScanLineCount;
            half _ScanLineThickness;
            half _VerticalJitter;
            half _HorizontalJitter;
            half _ColorBleed;
            half _ColorBleedOffset;
            half _RollSpeed;
            
            // UI 마스킹 속성
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert (appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;  // UI 마스킹용 로컬 포지션 저장
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 노이즈 텍스처를 위한 별도의 UV 좌표 (스케일 조정 가능)
                o.noiseUV = o.uv * _NoiseScale; // 타일링 효과
                
                // 버텍스 색상 전달
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 간단한 타임 변수
                half time = _Time.y * _RollSpeed;
                
                // 노이즈 텍스처를 사용한 지터 (더 가벼움)
                half2 noiseUV = i.noiseUV + half2(time * 0.1, time * 0.2);
                half4 noiseColor = tex2D(_NoiseTex, noiseUV);
                half4 noiseColor2 = tex2D(_NoiseTex, noiseUV + half2(0.5, 0.5)); // 두 번째 샘플링으로 다양성 추가
                
                // 수직 지터
                half vertJitter = _VerticalJitter * (noiseColor.r * 2.0 - 1.0);
                
                // 수평 지터 (수직 지터와 동일한 방식 적용, 다른 노이즈 샘플 사용)
                half horizJitter = _HorizontalJitter * (noiseColor2.r * 2.0 - 1.0);
                
                // 지터를 적용한 UV 좌표 (수평 및 수직)
                half2 jitterUV = i.uv + half2(horizJitter, vertJitter);
                
                // 주사선 효과 (단순화)
                half roll = frac(jitterUV.y + time * 0.1);
                half scanLineValue = frac(roll * _ScanLineCount) * 3.14159;
                half scanLine = sin(scanLineValue) * 0.5 + 0.5;
                
                // 두께 조정 (단순화)
                scanLine = pow(scanLine, 1.0 / _ScanLineThickness);
                
                // 메인 텍스처 샘플링
                fixed4 col = tex2D(_MainTex, jitterUV);
                
                // 강화된 색상 번짐 효과
                half redOffset = _ColorBleedOffset;
                half blueOffset = _ColorBleedOffset * 0.5;
                
                // 빨간색 채널 번짐 (오른쪽으로)
                fixed4 colorBleedR = tex2D(_MainTex, half2(jitterUV.x + redOffset, jitterUV.y));
                
                // 파란색 채널 번짐 (왼쪽으로) - 더 미묘한 효과
                fixed4 colorBleedB = tex2D(_MainTex, half2(jitterUV.x - blueOffset, jitterUV.y));
                
                // 색상 채널 분리 적용
                fixed3 originalColor = col.rgb;
                col.r = lerp(col.r, colorBleedR.r, _ColorBleed);
                col.b = lerp(col.b, colorBleedB.b, _ColorBleed * 0.5);
                
                // 고대비 영역에서 색상 번짐 강화 (선택적)
                half edgeIntensity = abs(length(originalColor) - length(fixed3(colorBleedR.r, originalColor.g, colorBleedB.b))) * 5.0;
                col.r = lerp(col.r, colorBleedR.r, saturate(_ColorBleed * edgeIntensity));
                
                // 향상된 노이즈 효과 (텍스처 기반)
                half staticNoise = noiseColor.g * _NoiseIntensity * 0.2;
                
                // 때때로 발생하는 강한 노이즈 글리치 (화면이 지글거리는 효과)
                //half glitchIntensity = step(0.97, frac(time * 1.5)) * 0.5;
                //half glitchNoise = noiseColor.a * glitchIntensity;
                //staticNoise += glitchNoise;
                
                // 최종 효과 적용 (단순화)
                //col.rgb = lerp(col.rgb, half3(1,1,1), staticNoise);
                col.rgb *= 1.0 - (_ScanLineIntensity * (1.0 - scanLine));
                
                // 입력 컬러의 알파값을 유지하고 색상값 적용
                col.a *= i.color.a;
                col.rgb *= i.color.rgb;
                
                // UI 마스킹 적용
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Diffuse"
}