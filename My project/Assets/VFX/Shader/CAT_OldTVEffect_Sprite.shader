Shader "CAT/Effects/OldTVEffect_Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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
        
        // Sprite 셰이더 필수 속성
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        
        // 일반 블렌딩 옵션들
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex TVSpriteVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            
            #include "UnitySprites.cginc"
            #include "UnityCG.cginc"

            sampler2D _NoiseTex;
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

            struct v2f_tv
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 noiseUV  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            v2f_tv TVSpriteVert(appdata_t IN)
            {
                v2f_tv OUT;

                UNITY_SETUP_INSTANCE_ID (IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(OUT.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                // 노이즈 텍스처용 UV 좌표
                OUT.noiseUV = OUT.texcoord * _NoiseScale;

                return OUT;
            }

            fixed4 frag(v2f_tv IN) : SV_Target
            {
                // 간단한 타임 변수
                half time = _Time.y * _RollSpeed;
                
                // 노이즈 텍스처를 사용한 지터
                half2 noiseUV = IN.noiseUV + half2(time * 0.1, time * 0.2);
                half4 noiseColor = tex2D(_NoiseTex, noiseUV);
                half4 noiseColor2 = tex2D(_NoiseTex, noiseUV + half2(0.5, 0.5)); // 두 번째 샘플링으로 다양성 추가
                
                // 수직 지터
                half vertJitter = _VerticalJitter * (noiseColor.r * 2.0 - 1.0);
                
                // 수평 지터 (수직 지터와 동일한 방식 적용, 다른 노이즈 샘플 사용)
                half horizJitter = _HorizontalJitter * (noiseColor2.r * 2.0 - 1.0);
                
                // 지터를 적용한 UV 좌표 (수평 및 수직)
                half2 jitterUV = IN.texcoord + half2(horizJitter, vertJitter);
                
                // 주사선 효과 (단순화)
                half roll = frac(jitterUV.y + time * 0.1);
                half scanLineValue = frac(roll * _ScanLineCount) * 3.14159;
                half scanLine = sin(scanLineValue) * 0.5 + 0.5;
                
                // 두께 조정 (단순화)
                scanLine = pow(scanLine, 1.0 / _ScanLineThickness);
                
                // 스프라이트 텍스처 샘플링 (스프라이트 방식)
                fixed4 col = SampleSpriteTexture(jitterUV);
                
                // 강화된 색상 번짐 효과
                half redOffset = _ColorBleedOffset;
                half blueOffset = _ColorBleedOffset * 0.5;
                
                // 빨간색 채널 번짐 (오른쪽으로)
                fixed4 colorBleedR;
                colorBleedR.a = SampleSpriteTexture(half2(jitterUV.x + redOffset, jitterUV.y)).a;
                colorBleedR.rgb = SampleSpriteTexture(half2(jitterUV.x + redOffset, jitterUV.y)).rgb;
                
                // 파란색 채널 번짐 (왼쪽으로) - 더 미묘한 효과
                fixed4 colorBleedB;
                colorBleedB.a = SampleSpriteTexture(half2(jitterUV.x - blueOffset, jitterUV.y)).a;
                colorBleedB.rgb = SampleSpriteTexture(half2(jitterUV.x - blueOffset, jitterUV.y)).rgb;
                
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
                
                // 최종 효과 적용
                //col.rgb = lerp(col.rgb, half3(1,1,1), staticNoise);
                col.rgb *= 1.0 - (_ScanLineIntensity * (1.0 - scanLine));
                
                // 색상 합성 (스프라이트 방식)
                col *= IN.color;
                
                // 프리멀티플라이드 알파 적용 (Unity 스프라이트 렌더링에 필요)
                col.rgb *= col.a;
                
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Sprites/Default"
}