Shader "Hidden/ChocDino/UIFX/Blend-Gooey"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _ResultTex ("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _SourceTex ("Source Texture", 2D) = "white" {}

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ThresholdScale ("Thresolhold Scale", Float) = 1
		_ThresholdOffset ("Thresolhold Offset", Float) = 0

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
		_ColorMask			("Color Mask", Float) = 15
	}

	CGINCLUDE

	#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
	#pragma multi_compile_local _ UNITY_UI_ALPHACLIP
	
	#include "BlendUtils.cginc"
	#include "CompUtils.cginc"
	#include "ColorUtils.cginc"

	float _ThresholdScale;
	float _ThresholdOffset;

	float4 fragGooey(v2f i) : SV_Target
	{
		// Note: This is already pre-multiplied alpha
		float4 source = tex2D(_SourceTex, i.uv);
		float4 blur = tex2D(_ResultTex, i.uv);

		float4 color = source;

		float4 result = ToStraight(blur);

		//result.a = (result.a * _ThresholdScale) - (_ThresholdScale * _ThresholdOffset);
		result.a = (result.a - _ThresholdOffset) * (_ThresholdScale) + _ThresholdOffset;
		result.a = saturate(result.a);

		color = ToPremultiplied(result);

		color = AlphaComp_ATop(source, color);

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
			Name "Blend-Gooey"
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragGooey
			ENDCG
		}
	}
}
