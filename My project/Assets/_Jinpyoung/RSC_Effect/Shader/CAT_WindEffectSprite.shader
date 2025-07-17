Shader "CAT/Effects/WindEffectSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _GradientTex ("Gradient Map (R: X axis influence, G: Y axis influence)", 2D) = "white" {}
        
        _WindStrength ("Wind Strength", Range(0, 0.1)) = 0.02
        _WindSpeed ("Wind Speed", Range(0, 10)) = 1
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1
        [MaterialToggle] _PixelSnap ("Pixel snap", Float) = 0
    }
    
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
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            
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
            sampler2D _AlphaTex;
            sampler2D _NoiseTex;
            sampler2D _GradientTex;
            
            float4 _MainTex_ST;
            float _WindStrength;
            float _WindSpeed;
            float _NoiseScale;
            fixed _PixelSnap;
            float _AlphaSplitEnabled;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                
                return o;
            }
            
            inline fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);
                
                #if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, uv);
                color.a = lerp(color.a, alpha.r, _AlphaSplitEnabled);
                #endif
                
                return color;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // Time variables for animation
                float time = _Time.y * _WindSpeed;
                
                // Sample the gradient map to control wind influence based on position
                float4 gradientInfluence = tex2D(_GradientTex, i.uv);
                
                // Sample the noise textures for wind movement
                float2 noiseUV = float2(i.uv.x * _NoiseScale + time * 0.1, i.uv.y * _NoiseScale);
                float2 noise = (tex2D(_NoiseTex, noiseUV).rg * 2 - 1) * _WindStrength;
                
                // Apply gradient influence to control wind effect based on position
                noise.x *= gradientInfluence.r;
                noise.y *= gradientInfluence.g;
                
                // Sample the main texture with the offset UV coordinates (using Sprite sampling function)
                float4 col = SampleSpriteTexture(i.uv + noise);
                
                // Apply vertex color
                col *= i.color;
                
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback for older devices
    Fallback "Sprites/Default"
}