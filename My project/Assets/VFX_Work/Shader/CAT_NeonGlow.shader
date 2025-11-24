Shader "CAT/Effects/NeonGlow"
{
    Properties
    {
        _MainTex ("Texture (RGBA)", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _InnerGlowColor ("Inner Glow Color", Color) = (0,1,0,1)
        _OuterGlowColor ("Outer Glow Color", Color) = (0,0,1,1)
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0,3)) = 1.0
        _OuterGlowIntensity ("Outer Glow Intensity", Range(0,3)) = 1.0
        _EmissionIntensity ("Emission Intensity", Range(0,5)) = 1.0
        
        [Space(10)]
        [Header(Flicker Effect)]
        [Toggle] _UseFlicker ("Use Flicker Effect", Float) = 1
        _NoiseTexture ("Noise Texture (R)", 2D) = "white" {}
        _FlickerSpeed ("Flicker Speed", Range(0.1, 10)) = 2.0
        _InnerFlickerMin ("Inner Flicker Min", Range(0, 1)) = 0.7
        _InnerFlickerMax ("Inner Flicker Max", Range(1, 3)) = 1.3
        _OuterFlickerMin ("Outer Flicker Min", Range(0, 1)) = 0.8
        _OuterFlickerMax ("Outer Flicker Max", Range(1, 3)) = 1.2
        _NoiseScaleInner ("Inner Noise Scale", Range(0.1, 5)) = 1.0
        _NoiseScaleOuter ("Outer Noise Scale", Range(0.1, 5)) = 0.7
        _NoiseOffsetOuter ("Outer Noise Offset", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100
        Cull Off
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 모바일 최적화를 위한 프래그마
            #pragma target 3.0
            #pragma fragmentoption ARB_precision_hint_fastest
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _InnerGlowColor;
            float4 _OuterGlowColor;
            float _InnerGlowIntensity;
            float _OuterGlowIntensity;
            float _EmissionIntensity;
            
            // 깜빡임 효과를 위한 프로퍼티
            float _UseFlicker;
            sampler2D _NoiseTexture;
            float4 _NoiseTexture_ST;
            float _FlickerSpeed;
            float _InnerFlickerMin;
            float _InnerFlickerMax;
            float _OuterFlickerMin;
            float _OuterFlickerMax;
            float _NoiseScaleInner;
            float _NoiseScaleOuter;
            float _NoiseOffsetOuter;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                // 텍스처의 각 채널을 개별적으로 샘플링
                half4 tex = tex2D(_MainTex, i.uv);
                
                // R: 메인 컬러 (조명의 기본 형태)
                half mainMask = tex.r;
                
                // G: InnerGlow 이미지
                half innerGlowMask = tex.g;
                
                // B: OuterGlow 이미지
                half outerGlowMask = tex.b;
                
                // A: 알파 채널
                half alpha = tex.a;
                
                // 깜빡임 효과 계산
                half innerIntensity = _InnerGlowIntensity;
                half outerIntensity = _OuterGlowIntensity;
                
                if (_UseFlicker > 0.5) {
                    // 노이즈 텍스처를 이용한 InnerGlow 깜빡임 계산
                    float2 innerNoiseUV = i.uv * _NoiseScaleInner + float2(_Time.y * _FlickerSpeed * 0.05, _Time.y * _FlickerSpeed * 0.07);
                    float innerNoise = tex2D(_NoiseTexture, innerNoiseUV).r;
                    
                    // InnerGlow 강도 범위 매핑
                    innerIntensity *= lerp(_InnerFlickerMin, _InnerFlickerMax, innerNoise);
                    
                    // 노이즈 텍스처를 이용한 OuterGlow 깜빡임 계산 (약간 다른 오프셋과 스케일)
                    float2 outerNoiseUV = i.uv * _NoiseScaleOuter + float2(_Time.y * _FlickerSpeed * 0.09 + _NoiseOffsetOuter, _Time.y * _FlickerSpeed * 0.03 - _NoiseOffsetOuter);
                    float outerNoise = tex2D(_NoiseTexture, outerNoiseUV).r;
                    
                    // OuterGlow 강도 범위 매핑
                    outerIntensity *= lerp(_OuterFlickerMin, _OuterFlickerMax, outerNoise);
                }
                
                // 각 레이어를 컬러와 강도에 따라 계산
                half3 mainColor = mainMask * _MainColor.rgb * _EmissionIntensity;
                half3 innerGlow = innerGlowMask * _InnerGlowColor.rgb * innerIntensity;
                half3 outerGlow = outerGlowMask * _OuterGlowColor.rgb * outerIntensity;
                
                // 최종 색상 결합 (additive blending)
                half3 finalColor = mainColor + innerGlow + outerGlow;
                
                // 버텍스 컬러를 적용하여 인스턴스별 컬러 변화 지원
                finalColor *= i.color.rgb;
                
                // 최종 알파값 계산 (텍스처 알파 * 메인 컬러 알파 * 버텍스 알파)
                half finalAlpha = alpha * _MainColor.a * i.color.a;
                
                return half4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}