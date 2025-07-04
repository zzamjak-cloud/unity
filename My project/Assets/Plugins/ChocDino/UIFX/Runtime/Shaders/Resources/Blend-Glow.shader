Shader "Hidden/ChocDino/UIFX/Blend-Glow"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}
		[PerRendererData] _FalloffTex ("Falloff Texture", 2D) = "white" {}
		[PerRendererData] _GradientTex ("Gradient Texture", 2D) = "white" {}
		//[PerRendererData] _FillTex ("Fill Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_MaxDistance ("Max Distance", Float) = 128
		_FalloffParams ("Falloff Params", Vector) = (4, 2, 0, 2.2)
		_GlowColor ("Glow Color", Vector) = (1, 1, 1, 1)
		_GradientParams ("Gradient Params", Vector) = (1, 1, 1, 1)
		_AdditiveFactor ("Additive Factor", Float) = 1
		_SourceAlpha ("Source Alpha", Float) = 1
		
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
	#pragma multi_compile_local DIR_BOTH DIR_INSIDE DIR_OUTSIDE
	#pragma multi_compile_local _ USE_GRADIENT_TEXTURE
	#pragma multi_compile_local _ USE_CURVE_FALLOFF
		
	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	uniform float _MaxDistance;
	uniform float4 _FalloffParams; // Energy, Falloff, Offset, Gamma
	uniform sampler2D _FalloffTex;
	uniform float4 _GlowColor;
	uniform Texture2D _GradientTex;
	SamplerState my_linear_clamp_sampler;
	uniform float4 _GradientParams; // Offset, Gamma, Reverse, 0.0
	uniform float _AdditiveFactor;
	uniform float _SourceAlpha;

	float hash13(float3 p3)
	{
		p3 = frac(p3 * .1031);
		p3 += dot(p3, p3.yzx + 19.19);
		return frac((p3.x + p3.y) * p3.z);
	}

	float4 fragGlow(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 source = tex2D(_SourceTex, i.uv.xy);

		// Note: Need to do this explicit swizzling for console compilers
		float zero = 0.0;
		float4 color = lerp(zero.xxxx, source, _SourceAlpha.xxxx);

		float distance = tex2D(_ResultTex, i.uv.xy).x;

		// Optionally limit the distance by making sure it falls off to zero by _MaxDistance
		float distanceFalloff = 1.0 - saturate(abs(distance) / _MaxDistance);

		#if USE_CURVE_FALLOFF
		float glowMask = pow(saturate(tex2D(_FalloffTex, float2(1.0-pow(distanceFalloff, _FalloffParams.w), 0.0)).r), 1.0);
		#else
		// Create exponential glow from distance (the 0.2 clamp prevents sparkles when using offset)
		float glowMask = pow(_FalloffParams.x/(max(0.2, abs(distance - _FalloffParams.z))), _FalloffParams.y);

		#ifdef UNITY_COLORSPACE_GAMMA
		// TODO: add better support here for gamma mode?
		//glowMask = pow(glowMask, 1.0/2.2);
		#endif
		
		// Optionally limit the distance by making sure it falls off to zero by _MaxDistance
		glowMask *= distanceFalloff;
		#endif

		// Add noise for dithering
		float noise = (hash13(float3(i.uv.xy * 2566.0, _Time.x)) - 0.5) * max(0, glowMask) * 0.1;
		glowMask += noise;

		// Get the glow color
		#if USE_GRADIENT_TEXTURE

		// Gradient coordinate
		float gradt = distanceFalloff;
		gradt = pow(gradt, _GradientParams.y);
		gradt -= _GradientParams.x;
		gradt = saturate(gradt);
		// Reverse the fill or not
		gradt = 1.0 - abs(_GradientParams.z - gradt);

		float4 glowColor = _GradientTex.Sample(my_linear_clamp_sampler, float2(gradt, 0.0));
		glowColor = ToPremultiplied(glowColor);
		#else
		float4 glowColor = _GlowColor;
		#endif

		// Modulate color by mask
		glowColor *= glowMask;
		
		// Exponential tonemap to correct overflows
		glowColor = 1.0 - exp(-glowColor);

		// Blend glow with source image
		#if (DIR_INSIDE)
		{
			color = lerp(AlphaComp_Over(glowColor, color), color + glowColor, _AdditiveFactor) * source.a;
		}
		#elif (DIR_OUTSIDE)
		{
			// Doesn't need anything for additive blending as this only affects outside the source.
			color += glowColor * (1.0-source.a);
		}
		#elif (DIR_BOTH)
		{
			color = lerp(AlphaComp_Over(glowColor, color), color + glowColor, _AdditiveFactor);
		}
		#endif

		// Make pixels additive, outside of the source mask
		// _Additive factor blends between alpha and additive blending
		float additiveMask = min(glowMask, 1.0 - color.a);
		color.a -= additiveMask * _AdditiveFactor;

		// 2D rect clipping
		#ifdef UNITY_UI_CLIP_RECT
		color = ApplyClipRect(color, i.mask);
		#endif

		// Alpha clipping
		#ifdef UNITY_UI_ALPHACLIP
		clip (color.a - 0.001);
		#endif

		color.rgb *= i.color.a;
		color *= i.color;

		return color;
	}

	ENDCG

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"OutputsPremultipliedAlpha"="True"
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
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha // Premultiplied transparency
		ColorMask [_ColorMask]

		Pass
		{
			Name "Blend-Glow"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragGlow
			ENDCG
		}
	}
}
