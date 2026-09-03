// Made with Amplify Shader Editor v1.9.9.4
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "CAT/VFX/URP2D_VFX_Master"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[Enum(Additive,1,AlphaBlend,10)] _BlendMode( "BlendMode", Float ) = 10
		[Enum(UnityEngine.Rendering.CullMode)] _CullMode( "CullMode", Float ) = 0
		_AlphaClip( "AlphaClip", Range( 0, 1 ) ) = 0
		_Pixelate( "Pixelate", Int ) = 0
		[Enum(Normal UV,0,Border UV,1)] _UVSwitch( "UVSwitch", Float ) = 0
		[Toggle] _FixedTiling( "FixedTiling", Float ) = 0
		_Hue( "Hue", Range( -1, 1 ) ) = 0
		_Saturation( "Saturation", Range( 0, 2 ) ) = 1
		_Value( "Value", Range( 0, 2 ) ) = 1
		_FixedTileWorldSize( "FixedTileWorldSize", Float ) = 1
		_MainTex( "MainTex", 2D ) = "white" {}
		[Enum(A,0,R,1)] _Main_Alpha_Ch( "Main_Alpha_Ch", Float ) = 0
		_Main_Color( "Main_Color", Color ) = ( 1, 1, 1, 1 )
		[Enum(MainTex Color,0,R Channel Color,1,ParticleCustom 1W,2)] _MainTex_ColorMode( "MainTex_ColorMode", Float ) = 0
		_Main_Contrast( "Main_Contrast", Float ) = 1
		[Enum(Property,1,1XY ParticleCustom,0)] _Main_Tiling_Type( "Main_Tiling_Type", Float ) = 1
		_Main_Rotate( "Main_Rotate", Float ) = 0
		[ToggleUI] _Main_Radial( "Main_Radial", Float ) = 0
		_Main_Radial_Tiling( "Main_Radial_Tiling", Vector ) = ( 1, 1, 0, 0 )
		[Enum(Property,0,1Z ParticleCustom,1)] _Main_Panning_Type( "Main_Panning_Type", Float ) = 1
		_Main_Panning( "Main_Panning", Vector ) = ( 0, 0, 0, 0 )
		[Header(___________________Deform___________________)][Space(5)][Toggle( _DEFORM_USE_ON )] _Deform_Use( "Deform_Use", Float ) = 0
		[Enum(Add,0,Lerp,1)] _DeformType( "DeformType", Int ) = 0
		_DeformTex( "DeformTex", 2D ) = "bump" {}
		_Deform_Rotate( "Deform_Rotate", Float ) = 0
		[ToggleUI] _Deform_Radial( "Deform_Radial", Float ) = 0
		_Deform_Radial_Tiling( "Deform_Radial_Tiling", Vector ) = ( 1, 1, 0, 0 )
		[Enum(Property,0,2X ParticleCustom,1)] _Deform_Strength_Type( "Deform_Strength_Type", Float ) = 1
		_Deform_Strength( "Deform_Strength", Float ) = 0
		[Enum(Auto,0,2Y ParticleCustom,1)] _Deform_Panning_Type( "Deform_Panning_Type", Float ) = 1
		_Deform_Panning( "Deform_Panning", Vector ) = ( 0, 0, 0, 0 )
		[Enum(Linear,0,Beam,1,Radial,2,Ring,3)][Space (12)] _DeformMask_Type( "DeformMask_Type", Int ) = 0
		_Deform_Mask_OffsetStrength( "Deform_Mask_Offset/Strength", Vector ) = ( 0, 0, 0, 0 )
		_Deform_Mask_Smooth( "Deform_Mask_Smooth", Range( 0, 1 ) ) = 0
		_Deform_Mask_Rotate( "Deform_Mask_Rotate", Float ) = 0
		[Header(___________________Dissolve___________________)][Space(5)][Toggle( _DISSOLVE_USE_ON )] _Dissolve_Use( "Dissolve_Use", Float ) = 0
		_DissolveTex( "DissolveTex", 2D ) = "white" {}
		[Enum(R,1,G,0)] _Dissolve_Channel( "Dissolve_Channel", Float ) = 1
		[ToggleUI] _DissolveTex_Reverse( "DissolveTex_Reverse", Float ) = 0
		_Dissolve_Rotate( "Dissolve_Rotate", Float ) = 0
		[ToggleUI] _Dissolve_Radial( "Dissolve_Radial", Float ) = 0
		_Dissolve_Radial_Tiling( "Dissolve_Radial_Tiling", Vector ) = ( 1, 1, 0, 0 )
		[Enum(Property,0,2W ParticleCustom,1)] _Dissolve_Progress_Type( "Dissolve_Progress_Type", Float ) = 1
		_Dissolve_Progress( "Dissolve_Progress", Range( 0, 1 ) ) = 1
		_Dissolve_smooth( "Dissolve_smooth", Range( 0, 1 ) ) = 0
		[Enum(ParticleCustom2 Z,0,With Deform,1,Use Prop Auto,2)] _Dissolve_Panning_Type( "Dissolve_Panning_Type", Float ) = 0
		_Dissolve_Panning( "Dissolve_Panning", Vector ) = ( 0, 0, 0, 0 )
		[ToggleUI] _Use_Dissolve_Edge( "Use_Dissolve_Edge", Float ) = 0
		[HDR] _Dissolve_Edge_Color( "Dissolve_Edge_Color", Color ) = ( 4, 0.9547434, 0.2641504, 1 )
		_Dissolve_Edge_Thick( "Dissolve_Edge_Thick", Float ) = 0.02
		_Dissolve_Edge_smooth( "Dissolve_Edge_smooth", Range( 0, 1 ) ) = 0.9
		[Enum(Linear,0,Beam,1,Radial,2)][Space (12)] _DissolveMask_Type( "DissolveMask_Type", Int ) = 0
		[Enum(Add,0,Multiply,1)] _DissolveMask_BlendType( "DissolveMask_BlendType", Int ) = 0
		_Dissolve_Mask_OffsetStrength( "Dissolve_Mask_Offset/Strength", Vector ) = ( 0, 0, 0, 0 )
		_Dissolve_Mask_Smooth( "Dissolve_Mask_Smooth", Range( 0, 1 ) ) = 0
		_Dissolve_Mask_Rotate( "Dissolve_Mask_Rotate", Float ) = 0
		[HideInInspector] _SpriteBorder( "SpriteBorder", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _OriginalSize( "OriginalSize", Vector ) = ( 0, 0, 0, 0 )
		[Header(___________________PixelFresnel___________________)][Space (8)][Toggle] _Fresnel_AlphaClip( "Fresnel_AlphaClip", Float ) = 0
		_Fresnel_AlphaClipPixelate( "Fresnel_AlphaClipPixelate", Range( 1, 4 ) ) = 4
		_Fresnel_AlphaClipPower( "Fresnel_AlphaClipPower", Range( 0, 4 ) ) = 1.5
		_Fresnel_AlphaClipStepMin( "Fresnel_AlphaClipStepMin", Range( 0, 1 ) ) = 0.05
		_Fresnel_AlphaClipStepMax( "Fresnel_AlphaClipStepMax", Range( 0, 1 ) ) = 0.6125
		[Header(___________________Mask___________________)][Space(5)][Toggle( _MASK_USE_ON )] _Mask_Use( "Mask_Use", Float ) = 0
		_Mask_Tex( "Mask_Tex", 2D ) = "white" {}
		[Enum(Property,0,Multiply_Dissolve_Progress,1)] _Mask_Strength_Mode( "Mask_Strength_Mode", Float ) = 0
		[Enum(A,0,R,1)] _Mask_Alpha_Ch( "Mask_Alpha_Ch", Float ) = 1
		_Mask_Contrast( "Mask_Contrast", Float ) = 1
		[Enum(Multiply,0,Add,1)] _Mask_BlendMode( "Mask_BlendMode", Float ) = 0
		_Mask_Strength( "Mask_Strength", Float ) = 1
		_Mask_Smooth( "Mask_Smooth", Range( 0, 1 ) ) = 0
		_Mask_Scale( "Mask_Scale", Float ) = 1
		_Mask_ScaleOffset( "Mask_ScaleOffset", Float ) = 0
		[HideInInspector] _FixedTileOriginWS( "_FixedTileOriginWS", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _FixedTileRightWS( "_FixedTileRightWS", Vector ) = ( 0, 0, 0, 0 )
		[HideInInspector] _FixedTileUpWS( "_FixedTileUpWS       ", Vector ) = ( 0, 0, 0, 0 )
		[Toggle] _ToggleSwitch0( "Toggle Switch0", Float ) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

		[HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "UniversalMaterialType"="Lit" "Queue"="Transparent" "ShaderGraphShader"="true" }

		Cull [_CullMode]
		BlendOp Add, Max
		Blend SrcAlpha [_BlendMode], One One
		ZTest LEqual
		ZWrite Off
		Offset 0 , 0
		ColorMask RGBA
		

		HLSLINCLUDE
		#pragma target 2.0
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		ENDHLSL

		
		Pass
		{
			
			Name "Sprite Lit"
            Tags { "LightMode"="Universal2D" }

			HLSLPROGRAM

			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170004
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define VARYINGS_NEED_SCREENPOSITION
            #define FEATURES_GRAPH_VERTEX

			#define SHADERPASS SHADERPASS_SPRITELIT

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES3
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_SCREEN_POSITION_NORMALIZED
			#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
			#pragma shader_feature_local _DEFORM_USE_ON
			#pragma shader_feature_local _DISSOLVE_USE_ON
			#pragma shader_feature_local _MASK_USE_ON


			struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_SKINNED_VERTEX_INPUTS
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float4 color : TEXCOORD2;
				float4 screenPosition : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            struct SurfaceDescription
			{
				float3 BaseColor;
				float Alpha;
			};

			TEXTURE2D(_MainTex);
			TEXTURE2D(_DeformTex);
			SAMPLER(sampler_Linear_Repeat);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_DissolveTex);
			TEXTURE2D(_Mask_Tex);
			SAMPLER(sampler_Mask_Tex);
			CBUFFER_START( UnityPerMaterial )
			float4 _DeformTex_ST;
			float4 _MainTex_ST;
			float4 _OriginalSize;
			float4 _DissolveTex_ST;
			float4 _SpriteBorder;
			float4 _Main_Color;
			float4 _Dissolve_Edge_Color;
			float4 _Mask_Tex_ST;
			float3 _FixedTileOriginWS;
			float3 _FixedTileRightWS;
			float3 _FixedTileUpWS;
			float2 _Dissolve_Radial_Tiling;
			float2 _Main_Panning;
			float2 _Deform_Mask_OffsetStrength;
			float2 _Deform_Radial_Tiling;
			float2 _Deform_Panning;
			float2 _Dissolve_Panning;
			float2 _Dissolve_Mask_OffsetStrength;
			float2 _Main_Radial_Tiling;
			float _Mask_Alpha_Ch;
			float _DissolveTex_Reverse;
			float _Dissolve_Channel;
			float _Fresnel_AlphaClipStepMax;
			float _Fresnel_AlphaClipStepMin;
			float _Dissolve_Panning_Type;
			float _Fresnel_AlphaClip;
			float _Dissolve_Radial;
			float _Dissolve_smooth;
			float _Main_Alpha_Ch;
			float _Mask_Strength_Mode;
			float _Dissolve_Rotate;
			float _Dissolve_Progress_Type;
			float _Dissolve_Progress;
			float _Dissolve_Edge_Thick;
			float _AlphaClip;
			float _Mask_BlendMode;
			float _Mask_Scale;
			float _Mask_Contrast;
			float _Dissolve_Mask_Rotate;
			float _Mask_Smooth;
			float _Mask_Strength;
			float _Mask_ScaleOffset;
			float _CullMode;
			float _Main_Contrast;
			float _Dissolve_Mask_Smooth;
			float _BlendMode;
			float _MainTex_ColorMode;
			float _Main_Panning_Type;
			float _Main_Radial;
			float _Main_Tiling_Type;
			float _FixedTiling;
			float _UVSwitch;
			float _FixedTileWorldSize;
			float _ToggleSwitch0;
			int _DeformType;
			float _Deform_Panning_Type;
			float _Deform_Radial;
			int _Pixelate;
			float _Deform_Rotate;
			float _Deform_Strength_Type;
			float _Deform_Strength;
			float _Deform_Mask_Smooth;
			int _DeformMask_Type;
			float _Deform_Mask_Rotate;
			float _Main_Rotate;
			float _Hue;
			float _Saturation;
			float _Value;
			float _Fresnel_AlphaClipPower;
			float _Use_Dissolve_Edge;
			float _Dissolve_Edge_smooth;
			int _DissolveMask_BlendType;
			int _DissolveMask_Type;
			float _Fresnel_AlphaClipPixelate;
			CBUFFER_END


			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float2 NineSliceUV139_g9( float2 uv, float4 border, float2 currentSize, float2 originalSize )
			{
				 // currentSize = 파티클 크기 (TexCoord1.xy에서 받음)
				  // originalSize = Material Property로 스크립트에서 전달
				  // border = Material Property로 스크립트에서 전달 (0~1 normalized)
				  float2 scale = currentSize / max(originalSize, 0.001);
				  float2 sMin = border.xy / max(scale, 0.001);
				  float2 sMax = border.zw / max(scale, 0.001);
				  float2 eStart = sMin;
				  float2 eEnd = 1.0 - sMax;
				  float2 L = uv * scale;
				  float2 R = 1.0 - (1.0 - uv) * scale;
				  float2 M = lerp(border.xy, 1.0 - border.zw, saturate((uv - eStart) / max(eEnd - eStart, 0.001)));
				  float2 maskL = step(uv, eStart);
				  float2 maskR = step(eEnd, uv);
				  float2 maskM = 1.0 - maskL - maskR;
				  return L * maskL + R * maskR + M * maskM;
			}
			
			float2 MyCustomExpression404_g9( float3 worldPos, float3 originWS, float3 rightWS, float3 upWS, float tileSize )
			{
				float3 deltaWS = worldPos - originWS;
				return float2(
				    dot(deltaWS, normalize(rightWS)),
				    dot(deltaWS, normalize(upWS))
				) / max(tileSize, 0.0001);
			}
			
			float2 DeformMaskType227_g9( int DeformType, float2 Add, float2 Lerp )
			{
				int mode = (int)DeformType;
				if (mode == 0) return Add;
				else if (mode == 1) return Lerp;
				else return Add;
			}
			
			float DeformMaskType179_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float DissolveMaskType166_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float BlendType247_g9( int MaskBlendType, float Add, float Multiply )
			{
				int mode = (int)MaskBlendType;
				if (mode == 0) return Add;
				else if (mode == 1) return Multiply;
				else return Add;
			}
			

			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_SKINNED_VERTEX_COMPUTE(v);

				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );

				float3 ase_normalWS = TransformObjectToWorldNormal( v.normal );
				o.ase_texcoord7.xyz = ase_normalWS;
				
				o.ase_texcoord4.xy = v.ase_texcoord3.xy;
				o.ase_texcoord5 = v.ase_texcoord1;
				o.ase_texcoord6 = v.ase_texcoord2;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord4.zw = 0;
				o.ase_texcoord7.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS);

				o.positionCS = vertexInput.positionCS;
				o.positionWS.xyz = vertexInput.positionWS;
				o.texCoord0.xyzw = v.uv0;
				o.color.xyzw =  v.color;
				o.screenPosition.xyzw = vertexInput.positionNDC;

				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float4 positionCS = IN.positionCS;
				float3 positionWS = IN.positionWS;

				float2 texCoord120_g9 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 uv139_g9 = texCoord120_g9;
				float4 border139_g9 = _SpriteBorder;
				float2 texCoord111_g9 = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 currentSize139_g9 = texCoord111_g9;
				float2 originalSize139_g9 = _OriginalSize.xy;
				float2 localNineSliceUV139_g9 = NineSliceUV139_g9( uv139_g9 , border139_g9 , currentSize139_g9 , originalSize139_g9 );
				float2 temp_output_176_0_g9 = ( ( _UVSwitch != 1.0 ? texCoord120_g9 : localNineSliceUV139_g9 ) * _MainTex_ST.xy );
				float3 worldPos404_g9 = positionWS;
				float3 originWS404_g9 = _FixedTileOriginWS;
				float3 rightWS404_g9 = _FixedTileRightWS;
				float3 upWS404_g9 = _FixedTileUpWS;
				float tileSize404_g9 = _FixedTileWorldSize;
				float2 localMyCustomExpression404_g9 = MyCustomExpression404_g9( worldPos404_g9 , originWS404_g9 , rightWS404_g9 , upWS404_g9 , tileSize404_g9 );
				float2 FixedTileData154_g9 = localMyCustomExpression404_g9;
				float2 texCoord157_g9 = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_396_0_g9 = ( _Main_Tiling_Type == 1.0 ? (( _FixedTiling )?( ( _MainTex_ST.xy * FixedTileData154_g9 ) ):( ( temp_output_176_0_g9 + _MainTex_ST.zw ) )) : (( _FixedTiling )?( ( texCoord157_g9 * FixedTileData154_g9 ) ):( ( ( temp_output_176_0_g9 * texCoord157_g9 ) + _MainTex_ST.zw ) )) );
				float2 temp_output_34_0_g13 = ( temp_output_396_0_g9 - float2( 0.5,0.5 ) );
				float2 break39_g13 = temp_output_34_0_g13;
				float2 appendResult50_g13 = (float2(( _Main_Radial_Tiling.y * ( length( temp_output_34_0_g13 ) * 2.0 ) ) , ( ( atan2( break39_g13.x , break39_g13.y ) * ( 1.0 / TWO_PI ) ) * _Main_Radial_Tiling.x )));
				int DeformType227_g9 = _DeformType;
				float2 texCoord13_g9 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_11_0_g9 = ( _Pixelate * 1.0 );
				float temp_output_14_0_g9 = max( temp_output_11_0_g9 , 2.0 );
				half2 pixelateduv16_g9 = floor( texCoord13_g9 * float2( temp_output_14_0_g9, temp_output_14_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_14_0_g9, temp_output_14_0_g9 );
				float2 lerpResult18_g9 = lerp( texCoord13_g9 , pixelateduv16_g9 , saturate( step( 1.0 , abs( temp_output_11_0_g9 ) ) ));
				float2 PixelUVBase19_g9 = lerpResult18_g9;
				float2 temp_output_34_0_g10 = ( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g10 = temp_output_34_0_g10;
				float2 appendResult50_g10 = (float2(( _Deform_Radial_Tiling.y * ( length( temp_output_34_0_g10 ) * 2.0 ) ) , ( ( atan2( break39_g10.x , break39_g10.y ) * ( 1.0 / TWO_PI ) ) * _Deform_Radial_Tiling.x )));
				float4 texCoord27_g9 = IN.ase_texcoord6;
				texCoord27_g9.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner36_g9 = ( 1.0 * _Time.y * _Deform_Panning + (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )));
				float cos57_g9 = cos(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin57_g9 = sin(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator57_g9 = mul( ( _Deform_Panning_Type == 1.0 ? ( (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )) + ( texCoord27_g9.y * _Deform_Panning ) ) : panner36_g9 ) - float2( 0.5,0.5 ) , float2x2( cos57_g9 , -sin57_g9 , sin57_g9 , cos57_g9 )) + float2( 0.5,0.5 );
				float2 clampResult64_g9 = clamp( rotator57_g9 , float2( 0.001,0.001 ) , float2( 0.999,0.999 ) );
				int DeformTypeSwitch48_g9 = _DeformType;
				float2 lerpResult74_g9 = lerp( rotator57_g9 , clampResult64_g9 , (float)DeformTypeSwitch48_g9);
				float2 temp_output_110_0_g9 = (SAMPLE_TEXTURE2D( _DeformTex, sampler_Linear_Repeat, lerpResult74_g9 )).rg;
				float2 temp_output_135_0_g9 = ( temp_output_110_0_g9 - float2( 0.5,0.5 ) );
				float2 break109_g9 = temp_output_135_0_g9;
				float4 texCoord91_g9 = IN.ase_texcoord6;
				texCoord91_g9.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_386_0_g9 = ( _Deform_Strength_Type == 1.0 ? texCoord91_g9.x : _Deform_Strength );
				float2 Deform370_g9 = ( temp_output_135_0_g9 * ( break109_g9.x * temp_output_386_0_g9 ) * ( break109_g9.y * temp_output_386_0_g9 ) );
				float2 Add227_g9 = ( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) + Deform370_g9 );
				float2 DeformTex171_g9 = temp_output_110_0_g9;
				float Deform_Strength172_g9 = temp_output_386_0_g9;
				float2 lerpResult211_g9 = lerp( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , DeformTex171_g9 , Deform_Strength172_g9);
				float2 Lerp227_g9 = lerpResult211_g9;
				float2 localDeformMaskType227_g9 = DeformMaskType227_g9( DeformType227_g9 , Add227_g9 , Lerp227_g9 );
				float lerpResult178_g9 = lerp( 0.0 , 0.5 , _Deform_Mask_Smooth);
				int MaskType179_g9 = _DeformMask_Type;
				float2 temp_cast_2 = (_Deform_Mask_OffsetStrength.x).xx;
				float2 texCoord62_g9 = IN.texCoord0.xy * float2( 1,1 ) + temp_cast_2;
				float cos114_g9 = cos(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin114_g9 = sin(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator114_g9 = mul( (( _FixedTiling )?( ( texCoord62_g9 * FixedTileData154_g9 ) ):( texCoord62_g9 )) - float2( 0.5,0.5 ) , float2x2( cos114_g9 , -sin114_g9 , sin114_g9 , cos114_g9 )) + float2( 0.5,0.5 );
				float Linear179_g9 = (( rotator114_g9 * _Deform_Mask_OffsetStrength.y )).x;
				float Beam179_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator114_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Radial179_g9 = saturate( ( ( 1.0 - ( distance( rotator114_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Ring179_g9 = 0.0;
				float localDeformMaskType179_g9 = DeformMaskType179_g9( MaskType179_g9 , Linear179_g9 , Beam179_g9 , Radial179_g9 , Ring179_g9 );
				float smoothstepResult193_g9 = smoothstep( lerpResult178_g9 , ( 1.0 - lerpResult178_g9 ) , localDeformMaskType179_g9);
				float Deform_Mask369_g9 = smoothstepResult193_g9;
				float2 lerpResult244_g9 = lerp( localDeformMaskType227_g9 , (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , Deform_Mask369_g9);
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch260_g9 = lerpResult244_g9;
				#else
				float2 staticSwitch260_g9 = (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 ));
				#endif
				float4 texCoord243_g9 = IN.ase_texcoord5;
				texCoord243_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner272_g9 = ( 1.0 * _Time.y * _Main_Panning + staticSwitch260_g9);
				float cos297_g9 = cos(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin297_g9 = sin(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator297_g9 = mul( ( _Main_Panning_Type == 1.0 ? ( staticSwitch260_g9 + ( texCoord243_g9.z * _Main_Panning ) ) : panner272_g9 ) - float2( 0.5,0.5 ) , float2x2( cos297_g9 , -sin297_g9 , sin297_g9 , cos297_g9 )) + float2( 0.5,0.5 );
				float Pixelate257_g9 = temp_output_11_0_g9;
				float temp_output_287_0_g9 = max( Pixelate257_g9 , 2.0 );
				half2 pixelateduv298_g9 = floor( rotator297_g9 * float2( temp_output_287_0_g9, temp_output_287_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_287_0_g9, temp_output_287_0_g9 );
				float2 lerpResult306_g9 = lerp( rotator297_g9 , pixelateduv298_g9 , saturate( step( 1.0 , abs( Pixelate257_g9 ) ) ));
				float4 tex2DNode313_g9 = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, lerpResult306_g9 );
				float3 hsvTorgb3_g12 = RGBToHSV( tex2DNode313_g9.rgb );
				float3 hsvTorgb10_g12 = HSVToRGB( float3(frac( ( hsvTorgb3_g12.x + _Hue ) ),saturate( ( hsvTorgb3_g12.y * _Saturation ) ),( hsvTorgb3_g12.z * _Value )) );
				float3 temp_output_329_0_g9 = hsvTorgb10_g12;
				float3 temp_cast_3 = (tex2DNode313_g9.r).xxx;
				float3 temp_cast_4 = (tex2DNode313_g9.r).xxx;
				float4 texCoord321_g9 = IN.ase_texcoord5;
				texCoord321_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float3 lerpResult330_g9 = lerp( temp_output_329_0_g9 , temp_cast_4 , texCoord321_g9.w);
				float3 ifLocalVar338_g9 = 0;
				if( 1.0 > _MainTex_ColorMode )
				ifLocalVar338_g9 = temp_output_329_0_g9;
				else if( 1.0 == _MainTex_ColorMode )
				ifLocalVar338_g9 = temp_cast_3;
				else if( 1.0 < _MainTex_ColorMode )
				ifLocalVar338_g9 = lerpResult330_g9;
				float3 temp_cast_5 = (_Main_Contrast).xxx;
				float lerpResult319_g9 = lerp( 0.0 , 0.5 , _Dissolve_Edge_smooth);
				int MaskBlendType247_g9 = _DissolveMask_BlendType;
				float lerpResult167_g9 = lerp( 0.0 , 0.5 , _Dissolve_Mask_Smooth);
				int MaskType166_g9 = _DissolveMask_Type;
				float2 temp_cast_6 = (_Dissolve_Mask_OffsetStrength.x).xx;
				float2 texCoord52_g9 = IN.texCoord0.xy * float2( 1,1 ) + temp_cast_6;
				float cos104_g9 = cos(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin104_g9 = sin(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator104_g9 = mul( (( _FixedTiling )?( ( texCoord52_g9 * FixedTileData154_g9 ) ):( texCoord52_g9 )) - float2( 0.5,0.5 ) , float2x2( cos104_g9 , -sin104_g9 , sin104_g9 , cos104_g9 )) + float2( 0.5,0.5 );
				float Linear166_g9 = (( rotator104_g9 * _Dissolve_Mask_OffsetStrength.y )).x;
				float Beam166_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator104_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Radial166_g9 = saturate( ( ( 1.0 - ( distance( rotator104_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Ring166_g9 = 0.0;
				float localDissolveMaskType166_g9 = DissolveMaskType166_g9( MaskType166_g9 , Linear166_g9 , Beam166_g9 , Radial166_g9 , Ring166_g9 );
				float smoothstepResult182_g9 = smoothstep( lerpResult167_g9 , ( 1.0 - lerpResult167_g9 ) , localDissolveMaskType166_g9);
				float Dissolve_Mask198_g9 = smoothstepResult182_g9;
				float2 temp_output_34_0_g11 = ( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g11 = temp_output_34_0_g11;
				float2 appendResult50_g11 = (float2(( _Dissolve_Radial_Tiling.y * ( length( temp_output_34_0_g11 ) * 2.0 ) ) , ( ( atan2( break39_g11.x , break39_g11.y ) * ( 1.0 / TWO_PI ) ) * _Dissolve_Radial_Tiling.x )));
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch127_g9 = ( (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) )) + Deform370_g9 );
				#else
				float2 staticSwitch127_g9 = (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) ));
				#endif
				float2 panner141_g9 = ( 1.0 * _Time.y * _Dissolve_Panning + staticSwitch127_g9);
				float Deform_Panning_Type_ref390_g9 = _Deform_Panning_Type;
				float4 texCoord84_g9 = IN.ase_texcoord6;
				texCoord84_g9.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Deform_Panning87_g9 = _Deform_Panning;
				float2 panner124_g9 = ( 1.0 * _Time.y * Deform_Panning87_g9 + staticSwitch127_g9);
				float cos180_g9 = cos(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin180_g9 = sin(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator180_g9 = mul(  ( _Dissolve_Panning_Type - 0.0 > 1.0 ? panner141_g9 : _Dissolve_Panning_Type - 0.0 <= 1.0 && _Dissolve_Panning_Type + 0.0 >= 1.0 ? ( Deform_Panning_Type_ref390_g9 == 1.0 ? ( staticSwitch127_g9 + ( texCoord84_g9.y * Deform_Panning87_g9 ) ) : panner124_g9 ) : ( staticSwitch127_g9 + ( texCoord84_g9.z * _Dissolve_Panning ) ) )  - float2( 0.5,0.5 ) , float2x2( cos180_g9 , -sin180_g9 , sin180_g9 , cos180_g9 )) + float2( 0.5,0.5 );
				float4 tex2DNode197_g9 = SAMPLE_TEXTURE2D( _DissolveTex, sampler_Linear_Repeat, rotator180_g9 );
				float temp_output_398_0_g9 = ( _Dissolve_Channel == 1.0 ? tex2DNode197_g9.r : tex2DNode197_g9.g );
				float Add247_g9 = ( Dissolve_Mask198_g9 + (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float Multiply247_g9 = ( Dissolve_Mask198_g9 * (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float localBlendType247_g9 = BlendType247_g9( MaskBlendType247_g9 , Add247_g9 , Multiply247_g9 );
				float4 texCoord229_g9 = IN.ase_texcoord6;
				texCoord229_g9.xy = IN.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_394_0_g9 = ( _Dissolve_Progress_Type == 1.0 ? texCoord229_g9.w : _Dissolve_Progress );
				float lerpResult262_g9 = lerp( -1.0 , 1.0 , temp_output_394_0_g9);
				float temp_output_276_0_g9 = ( saturate( localBlendType247_g9 ) + lerpResult262_g9 );
				float Dissolve_Before_Smooth296_g9 = temp_output_276_0_g9;
				float smoothstepResult327_g9 = smoothstep( lerpResult319_g9 , ( 1.0 - lerpResult319_g9 ) , ( 1.0 - ( Dissolve_Before_Smooth296_g9 - _Dissolve_Edge_Thick ) ));
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch339_g9 = smoothstepResult327_g9;
				#else
				float staticSwitch339_g9 = 0.0;
				#endif
				float3 lerpResult373_g9 = lerp( ( (IN.color).rgb * pow( abs( ifLocalVar338_g9 ) , temp_cast_5 ) * _Main_Color.rgb ) , _Dissolve_Edge_Color.rgb , (( _Use_Dissolve_Edge )?( staticSwitch339_g9 ):( 0.0 )));
				
				float lerpResult325_g9 = lerp( 0.0 , 0.5 , _AlphaClip);
				float lerpResult254_g9 = lerp( 0.0 , 0.5 , _Mask_Smooth);
				float2 uv_Mask_Tex = IN.texCoord0.xy * _Mask_Tex_ST.xy + _Mask_Tex_ST.zw;
				float4 tex2DNode186_g9 = SAMPLE_TEXTURE2D( _Mask_Tex, sampler_Mask_Tex, uv_Mask_Tex );
				float Dissolve_Progress_ref203_g9 = temp_output_394_0_g9;
				float smoothstepResult268_g9 = smoothstep( lerpResult254_g9 , ( 1.0 - lerpResult254_g9 ) , ( _Mask_Strength * (pow( saturate( ( _Mask_Alpha_Ch == 1.0 ? tex2DNode186_g9.r : tex2DNode186_g9.a ) ) , _Mask_Contrast )*_Mask_Scale + _Mask_ScaleOffset) * ( _Mask_Strength_Mode == 0.0 ? 1.0 : Dissolve_Progress_ref203_g9 ) ));
				float Mask372_g9 = smoothstepResult268_g9;
				#ifdef _MASK_USE_ON
				float staticSwitch302_g9 = Mask372_g9;
				#else
				float staticSwitch302_g9 = 1.0;
				#endif
				float lerpResult217_g9 = lerp( 0.0 , 0.5 , _Dissolve_smooth);
				float smoothstepResult238_g9 = smoothstep( lerpResult217_g9 , ( 1.0 - lerpResult217_g9 ) , temp_output_276_0_g9);
				float Dissolve371_g9 = saturate( smoothstepResult238_g9 );
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch295_g9 = Dissolve371_g9;
				#else
				float staticSwitch295_g9 = 1.0;
				#endif
				float temp_output_303_0_g9 = ( saturate( ( IN.color.a * _Main_Color.a * ( _Main_Alpha_Ch == 1.0 ? tex2DNode313_g9.r : tex2DNode313_g9.a ) ) ) * staticSwitch295_g9 );
				float temp_output_326_0_g9 = saturate( ( _Mask_BlendMode == 0.0 ? ( staticSwitch302_g9 * temp_output_303_0_g9 ) : ( staticSwitch302_g9 + temp_output_303_0_g9 ) ) );
				float smoothstepResult336_g9 = smoothstep( lerpResult325_g9 , ( 1.0 - lerpResult325_g9 ) , temp_output_326_0_g9);
				float lerpResult340_g9 = lerp( smoothstepResult336_g9 , temp_output_326_0_g9 , step( _AlphaClip , 1E-05 ));
				float3 ase_normalWS = IN.ase_texcoord7.xyz;
				float dotResult251_g9 = dot( ase_normalWS , -UNITY_MATRIX_V[ 2 ].xyz );
				float smoothstepResult289_g9 = smoothstep( _Fresnel_AlphaClipStepMin , _Fresnel_AlphaClipStepMax , pow( abs( dotResult251_g9 ) , _Fresnel_AlphaClipPower ));
				float temp_output_235_0_g9 = ( 1.0 / _Fresnel_AlphaClipPixelate );
				float temp_output_249_0_g9 = ( temp_output_235_0_g9 * -1.0 );
				float clampResult266_g9 = clamp( ddx( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float2 appendResult199_g9 = (float2(IN.screenPosition.x , IN.screenPosition.y));
				float2 appendResult201_g9 = (float2(_ScaledScreenParams.x , _ScaledScreenParams.y));
				float2 temp_output_214_0_g9 = ( appendResult199_g9 * appendResult201_g9 );
				float temp_output_185_0_g9 = ( ( _Fresnel_AlphaClipPixelate * ( _ScaledScreenParams.x / 1920.0 ) ) / ( unity_OrthoParams.y / 10.5 ) );
				float2 break264_g9 = ( floor( temp_output_214_0_g9 ) - ( floor( ( temp_output_214_0_g9 / temp_output_185_0_g9 ) ) * temp_output_185_0_g9 ) );
				float clampResult280_g9 = clamp( ddy( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float FresnelAlphaClip334_g9 = (( _Fresnel_AlphaClip )?( step( 0.5 , ( step( 0.05 , smoothstepResult289_g9 ) * ( ( smoothstepResult289_g9 - ( clampResult266_g9 * break264_g9.x ) ) - ( clampResult280_g9 * break264_g9.y ) ) ) ) ):( 1.0 ));
				
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				surfaceDescription.BaseColor = lerpResult373_g9;
				surfaceDescription.Alpha = ( lerpResult340_g9 * FresnelAlphaClip334_g9 );

				half4 color = half4(surfaceDescription.BaseColor, surfaceDescription.Alpha);

				#if defined(DEBUG_DISPLAY)
				SurfaceData2D surfaceData;
				InitializeSurfaceData(color.rgb, color.a, surfaceData);
				InputData2D inputData;
				InitializeInputData(positionWS.xy, half2(IN.texCoord0.xy), inputData);
				half4 debugColor = 0;

				SETUP_DEBUG_DATA_2D(inputData, positionWS, positionCS);

				if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
				{
					return debugColor;
				}
				#endif

				//***���⼭���� Premultiplied�� ���� Ŀ���� ó��***
				//BlendOp Add, Max ����� �� ����ؼ� �ӽ÷� ���ܵ�
				//�� ������ �Ϸ��� Blend RGB�� Premultiplied �� �ϰ� Dst�� _BlendMode ������ �״��

				//color.rgb *= color.a; // ���� �ռ��� ���� ���ĸ� RGB�� �̸� ���� (Premultiplied)

				// ����ũ�� ���� ���� �ٽ�: 
				// Additive(���ϱ�) ����� ���� ���� ���� 0���� ����� ������ ������� ������ ���� �ʰ� �ϰ�,
				// AlphaBlend ����� ���� �ּ����� ���ü��� ���� ���ĸ� �����ϰų� 1�� ������ �� �ֽ��ϴ�.
				//if (_BlendMode == 1.0) // Additive ����� ���
				//{
				//	color.a = 0; // RGB�� ��������Ƿ� ���� ��濡 ����������, �����츦 �������ϰ� ������ ����
				//}
				
				// color *= unity_SpriteColor; //������ ������ �������⿡ ����
				return color;
			}

			ENDHLSL
		}

		
        Pass
        {
			
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }

            Cull Off
			Blend Off
			ZTest LEqual
			ZWrite On

            HLSLPROGRAM

			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170004
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX

            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENESELECTIONPASS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES3
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _MASK_USE_ON
			#pragma shader_feature_local _DEFORM_USE_ON
			#pragma shader_feature_local _DISSOLVE_USE_ON


			TEXTURE2D(_Mask_Tex);
			SAMPLER(sampler_Mask_Tex);
			TEXTURE2D(_MainTex);
			TEXTURE2D(_DeformTex);
			SAMPLER(sampler_Linear_Repeat);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_DissolveTex);
			CBUFFER_START( UnityPerMaterial )
			float4 _DeformTex_ST;
			float4 _MainTex_ST;
			float4 _OriginalSize;
			float4 _DissolveTex_ST;
			float4 _SpriteBorder;
			float4 _Main_Color;
			float4 _Dissolve_Edge_Color;
			float4 _Mask_Tex_ST;
			float3 _FixedTileOriginWS;
			float3 _FixedTileRightWS;
			float3 _FixedTileUpWS;
			float2 _Dissolve_Radial_Tiling;
			float2 _Main_Panning;
			float2 _Deform_Mask_OffsetStrength;
			float2 _Deform_Radial_Tiling;
			float2 _Deform_Panning;
			float2 _Dissolve_Panning;
			float2 _Dissolve_Mask_OffsetStrength;
			float2 _Main_Radial_Tiling;
			float _Mask_Alpha_Ch;
			float _DissolveTex_Reverse;
			float _Dissolve_Channel;
			float _Fresnel_AlphaClipStepMax;
			float _Fresnel_AlphaClipStepMin;
			float _Dissolve_Panning_Type;
			float _Fresnel_AlphaClip;
			float _Dissolve_Radial;
			float _Dissolve_smooth;
			float _Main_Alpha_Ch;
			float _Mask_Strength_Mode;
			float _Dissolve_Rotate;
			float _Dissolve_Progress_Type;
			float _Dissolve_Progress;
			float _Dissolve_Edge_Thick;
			float _AlphaClip;
			float _Mask_BlendMode;
			float _Mask_Scale;
			float _Mask_Contrast;
			float _Dissolve_Mask_Rotate;
			float _Mask_Smooth;
			float _Mask_Strength;
			float _Mask_ScaleOffset;
			float _CullMode;
			float _Main_Contrast;
			float _Dissolve_Mask_Smooth;
			float _BlendMode;
			float _MainTex_ColorMode;
			float _Main_Panning_Type;
			float _Main_Radial;
			float _Main_Tiling_Type;
			float _FixedTiling;
			float _UVSwitch;
			float _FixedTileWorldSize;
			float _ToggleSwitch0;
			int _DeformType;
			float _Deform_Panning_Type;
			float _Deform_Radial;
			int _Pixelate;
			float _Deform_Rotate;
			float _Deform_Strength_Type;
			float _Deform_Strength;
			float _Deform_Mask_Smooth;
			int _DeformMask_Type;
			float _Deform_Mask_Rotate;
			float _Main_Rotate;
			float _Hue;
			float _Saturation;
			float _Value;
			float _Fresnel_AlphaClipPower;
			float _Use_Dissolve_Edge;
			float _Dissolve_Edge_smooth;
			int _DissolveMask_BlendType;
			int _DissolveMask_Type;
			float _Fresnel_AlphaClipPixelate;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_SKINNED_VERTEX_INPUTS
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            int _ObjectId;
            int _PassValue;

            struct SurfaceDescription
			{
				float Alpha;
			};

			float2 NineSliceUV139_g9( float2 uv, float4 border, float2 currentSize, float2 originalSize )
			{
				 // currentSize = 파티클 크기 (TexCoord1.xy에서 받음)
				  // originalSize = Material Property로 스크립트에서 전달
				  // border = Material Property로 스크립트에서 전달 (0~1 normalized)
				  float2 scale = currentSize / max(originalSize, 0.001);
				  float2 sMin = border.xy / max(scale, 0.001);
				  float2 sMax = border.zw / max(scale, 0.001);
				  float2 eStart = sMin;
				  float2 eEnd = 1.0 - sMax;
				  float2 L = uv * scale;
				  float2 R = 1.0 - (1.0 - uv) * scale;
				  float2 M = lerp(border.xy, 1.0 - border.zw, saturate((uv - eStart) / max(eEnd - eStart, 0.001)));
				  float2 maskL = step(uv, eStart);
				  float2 maskR = step(eEnd, uv);
				  float2 maskM = 1.0 - maskL - maskR;
				  return L * maskL + R * maskR + M * maskM;
			}
			
			float2 MyCustomExpression404_g9( float3 worldPos, float3 originWS, float3 rightWS, float3 upWS, float tileSize )
			{
				float3 deltaWS = worldPos - originWS;
				return float2(
				    dot(deltaWS, normalize(rightWS)),
				    dot(deltaWS, normalize(upWS))
				) / max(tileSize, 0.0001);
			}
			
			float2 DeformMaskType227_g9( int DeformType, float2 Add, float2 Lerp )
			{
				int mode = (int)DeformType;
				if (mode == 0) return Add;
				else if (mode == 1) return Lerp;
				else return Add;
			}
			
			float DeformMaskType179_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float DissolveMaskType166_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float BlendType247_g9( int MaskBlendType, float Add, float Multiply )
			{
				int mode = (int)MaskBlendType;
				if (mode == 0) return Add;
				else if (mode == 1) return Multiply;
				else return Add;
			}
			

			VertexOutput vert(VertexInput v )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				UNITY_SKINNED_VERTEX_COMPUTE(v);

				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );

				float3 ase_positionWS = TransformObjectToWorld( ( v.positionOS ).xyz );
				o.ase_texcoord2.xyz = ase_positionWS;
				float3 ase_normalWS = TransformObjectToWorldNormal( v.normal );
				o.ase_texcoord4.xyz = ase_normalWS;
				float4 ase_positionCS = TransformObjectToHClip( ( v.positionOS ).xyz );
				float4 screenPos = ComputeScreenPos( ase_positionCS );
				o.ase_texcoord5 = screenPos;
				
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				o.ase_texcoord1 = v.ase_texcoord2;
				o.ase_color = v.ase_color;
				o.ase_texcoord.zw = v.ase_texcoord3.xy;
				o.ase_texcoord3 = v.ase_texcoord1;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.w = 0;
				o.ase_texcoord4.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif

				float3 positionWS = TransformObjectToWorld(v.positionOS);
				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float lerpResult325_g9 = lerp( 0.0 , 0.5 , _AlphaClip);
				float lerpResult254_g9 = lerp( 0.0 , 0.5 , _Mask_Smooth);
				float2 uv_Mask_Tex = IN.ase_texcoord.xy * _Mask_Tex_ST.xy + _Mask_Tex_ST.zw;
				float4 tex2DNode186_g9 = SAMPLE_TEXTURE2D( _Mask_Tex, sampler_Mask_Tex, uv_Mask_Tex );
				float4 texCoord229_g9 = IN.ase_texcoord1;
				texCoord229_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_394_0_g9 = ( _Dissolve_Progress_Type == 1.0 ? texCoord229_g9.w : _Dissolve_Progress );
				float Dissolve_Progress_ref203_g9 = temp_output_394_0_g9;
				float smoothstepResult268_g9 = smoothstep( lerpResult254_g9 , ( 1.0 - lerpResult254_g9 ) , ( _Mask_Strength * (pow( saturate( ( _Mask_Alpha_Ch == 1.0 ? tex2DNode186_g9.r : tex2DNode186_g9.a ) ) , _Mask_Contrast )*_Mask_Scale + _Mask_ScaleOffset) * ( _Mask_Strength_Mode == 0.0 ? 1.0 : Dissolve_Progress_ref203_g9 ) ));
				float Mask372_g9 = smoothstepResult268_g9;
				#ifdef _MASK_USE_ON
				float staticSwitch302_g9 = Mask372_g9;
				#else
				float staticSwitch302_g9 = 1.0;
				#endif
				float2 texCoord120_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 uv139_g9 = texCoord120_g9;
				float4 border139_g9 = _SpriteBorder;
				float2 texCoord111_g9 = IN.ase_texcoord.zw * float2( 1,1 ) + float2( 0,0 );
				float2 currentSize139_g9 = texCoord111_g9;
				float2 originalSize139_g9 = _OriginalSize.xy;
				float2 localNineSliceUV139_g9 = NineSliceUV139_g9( uv139_g9 , border139_g9 , currentSize139_g9 , originalSize139_g9 );
				float2 temp_output_176_0_g9 = ( ( _UVSwitch != 1.0 ? texCoord120_g9 : localNineSliceUV139_g9 ) * _MainTex_ST.xy );
				float3 ase_positionWS = IN.ase_texcoord2.xyz;
				float3 worldPos404_g9 = ase_positionWS;
				float3 originWS404_g9 = _FixedTileOriginWS;
				float3 rightWS404_g9 = _FixedTileRightWS;
				float3 upWS404_g9 = _FixedTileUpWS;
				float tileSize404_g9 = _FixedTileWorldSize;
				float2 localMyCustomExpression404_g9 = MyCustomExpression404_g9( worldPos404_g9 , originWS404_g9 , rightWS404_g9 , upWS404_g9 , tileSize404_g9 );
				float2 FixedTileData154_g9 = localMyCustomExpression404_g9;
				float2 texCoord157_g9 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_396_0_g9 = ( _Main_Tiling_Type == 1.0 ? (( _FixedTiling )?( ( _MainTex_ST.xy * FixedTileData154_g9 ) ):( ( temp_output_176_0_g9 + _MainTex_ST.zw ) )) : (( _FixedTiling )?( ( texCoord157_g9 * FixedTileData154_g9 ) ):( ( ( temp_output_176_0_g9 * texCoord157_g9 ) + _MainTex_ST.zw ) )) );
				float2 temp_output_34_0_g13 = ( temp_output_396_0_g9 - float2( 0.5,0.5 ) );
				float2 break39_g13 = temp_output_34_0_g13;
				float2 appendResult50_g13 = (float2(( _Main_Radial_Tiling.y * ( length( temp_output_34_0_g13 ) * 2.0 ) ) , ( ( atan2( break39_g13.x , break39_g13.y ) * ( 1.0 / TWO_PI ) ) * _Main_Radial_Tiling.x )));
				int DeformType227_g9 = _DeformType;
				float2 texCoord13_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_11_0_g9 = ( _Pixelate * 1.0 );
				float temp_output_14_0_g9 = max( temp_output_11_0_g9 , 2.0 );
				half2 pixelateduv16_g9 = floor( texCoord13_g9 * float2( temp_output_14_0_g9, temp_output_14_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_14_0_g9, temp_output_14_0_g9 );
				float2 lerpResult18_g9 = lerp( texCoord13_g9 , pixelateduv16_g9 , saturate( step( 1.0 , abs( temp_output_11_0_g9 ) ) ));
				float2 PixelUVBase19_g9 = lerpResult18_g9;
				float2 temp_output_34_0_g10 = ( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g10 = temp_output_34_0_g10;
				float2 appendResult50_g10 = (float2(( _Deform_Radial_Tiling.y * ( length( temp_output_34_0_g10 ) * 2.0 ) ) , ( ( atan2( break39_g10.x , break39_g10.y ) * ( 1.0 / TWO_PI ) ) * _Deform_Radial_Tiling.x )));
				float4 texCoord27_g9 = IN.ase_texcoord1;
				texCoord27_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner36_g9 = ( 1.0 * _Time.y * _Deform_Panning + (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )));
				float cos57_g9 = cos(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin57_g9 = sin(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator57_g9 = mul( ( _Deform_Panning_Type == 1.0 ? ( (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )) + ( texCoord27_g9.y * _Deform_Panning ) ) : panner36_g9 ) - float2( 0.5,0.5 ) , float2x2( cos57_g9 , -sin57_g9 , sin57_g9 , cos57_g9 )) + float2( 0.5,0.5 );
				float2 clampResult64_g9 = clamp( rotator57_g9 , float2( 0.001,0.001 ) , float2( 0.999,0.999 ) );
				int DeformTypeSwitch48_g9 = _DeformType;
				float2 lerpResult74_g9 = lerp( rotator57_g9 , clampResult64_g9 , (float)DeformTypeSwitch48_g9);
				float2 temp_output_110_0_g9 = (SAMPLE_TEXTURE2D( _DeformTex, sampler_Linear_Repeat, lerpResult74_g9 )).rg;
				float2 temp_output_135_0_g9 = ( temp_output_110_0_g9 - float2( 0.5,0.5 ) );
				float2 break109_g9 = temp_output_135_0_g9;
				float4 texCoord91_g9 = IN.ase_texcoord1;
				texCoord91_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_386_0_g9 = ( _Deform_Strength_Type == 1.0 ? texCoord91_g9.x : _Deform_Strength );
				float2 Deform370_g9 = ( temp_output_135_0_g9 * ( break109_g9.x * temp_output_386_0_g9 ) * ( break109_g9.y * temp_output_386_0_g9 ) );
				float2 Add227_g9 = ( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) + Deform370_g9 );
				float2 DeformTex171_g9 = temp_output_110_0_g9;
				float Deform_Strength172_g9 = temp_output_386_0_g9;
				float2 lerpResult211_g9 = lerp( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , DeformTex171_g9 , Deform_Strength172_g9);
				float2 Lerp227_g9 = lerpResult211_g9;
				float2 localDeformMaskType227_g9 = DeformMaskType227_g9( DeformType227_g9 , Add227_g9 , Lerp227_g9 );
				float lerpResult178_g9 = lerp( 0.0 , 0.5 , _Deform_Mask_Smooth);
				int MaskType179_g9 = _DeformMask_Type;
				float2 temp_cast_2 = (_Deform_Mask_OffsetStrength.x).xx;
				float2 texCoord62_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + temp_cast_2;
				float cos114_g9 = cos(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin114_g9 = sin(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator114_g9 = mul( (( _FixedTiling )?( ( texCoord62_g9 * FixedTileData154_g9 ) ):( texCoord62_g9 )) - float2( 0.5,0.5 ) , float2x2( cos114_g9 , -sin114_g9 , sin114_g9 , cos114_g9 )) + float2( 0.5,0.5 );
				float Linear179_g9 = (( rotator114_g9 * _Deform_Mask_OffsetStrength.y )).x;
				float Beam179_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator114_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Radial179_g9 = saturate( ( ( 1.0 - ( distance( rotator114_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Ring179_g9 = 0.0;
				float localDeformMaskType179_g9 = DeformMaskType179_g9( MaskType179_g9 , Linear179_g9 , Beam179_g9 , Radial179_g9 , Ring179_g9 );
				float smoothstepResult193_g9 = smoothstep( lerpResult178_g9 , ( 1.0 - lerpResult178_g9 ) , localDeformMaskType179_g9);
				float Deform_Mask369_g9 = smoothstepResult193_g9;
				float2 lerpResult244_g9 = lerp( localDeformMaskType227_g9 , (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , Deform_Mask369_g9);
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch260_g9 = lerpResult244_g9;
				#else
				float2 staticSwitch260_g9 = (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 ));
				#endif
				float4 texCoord243_g9 = IN.ase_texcoord3;
				texCoord243_g9.xy = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner272_g9 = ( 1.0 * _Time.y * _Main_Panning + staticSwitch260_g9);
				float cos297_g9 = cos(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin297_g9 = sin(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator297_g9 = mul( ( _Main_Panning_Type == 1.0 ? ( staticSwitch260_g9 + ( texCoord243_g9.z * _Main_Panning ) ) : panner272_g9 ) - float2( 0.5,0.5 ) , float2x2( cos297_g9 , -sin297_g9 , sin297_g9 , cos297_g9 )) + float2( 0.5,0.5 );
				float Pixelate257_g9 = temp_output_11_0_g9;
				float temp_output_287_0_g9 = max( Pixelate257_g9 , 2.0 );
				half2 pixelateduv298_g9 = floor( rotator297_g9 * float2( temp_output_287_0_g9, temp_output_287_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_287_0_g9, temp_output_287_0_g9 );
				float2 lerpResult306_g9 = lerp( rotator297_g9 , pixelateduv298_g9 , saturate( step( 1.0 , abs( Pixelate257_g9 ) ) ));
				float4 tex2DNode313_g9 = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, lerpResult306_g9 );
				float lerpResult217_g9 = lerp( 0.0 , 0.5 , _Dissolve_smooth);
				int MaskBlendType247_g9 = _DissolveMask_BlendType;
				float lerpResult167_g9 = lerp( 0.0 , 0.5 , _Dissolve_Mask_Smooth);
				int MaskType166_g9 = _DissolveMask_Type;
				float2 temp_cast_3 = (_Dissolve_Mask_OffsetStrength.x).xx;
				float2 texCoord52_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + temp_cast_3;
				float cos104_g9 = cos(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin104_g9 = sin(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator104_g9 = mul( (( _FixedTiling )?( ( texCoord52_g9 * FixedTileData154_g9 ) ):( texCoord52_g9 )) - float2( 0.5,0.5 ) , float2x2( cos104_g9 , -sin104_g9 , sin104_g9 , cos104_g9 )) + float2( 0.5,0.5 );
				float Linear166_g9 = (( rotator104_g9 * _Dissolve_Mask_OffsetStrength.y )).x;
				float Beam166_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator104_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Radial166_g9 = saturate( ( ( 1.0 - ( distance( rotator104_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Ring166_g9 = 0.0;
				float localDissolveMaskType166_g9 = DissolveMaskType166_g9( MaskType166_g9 , Linear166_g9 , Beam166_g9 , Radial166_g9 , Ring166_g9 );
				float smoothstepResult182_g9 = smoothstep( lerpResult167_g9 , ( 1.0 - lerpResult167_g9 ) , localDissolveMaskType166_g9);
				float Dissolve_Mask198_g9 = smoothstepResult182_g9;
				float2 temp_output_34_0_g11 = ( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g11 = temp_output_34_0_g11;
				float2 appendResult50_g11 = (float2(( _Dissolve_Radial_Tiling.y * ( length( temp_output_34_0_g11 ) * 2.0 ) ) , ( ( atan2( break39_g11.x , break39_g11.y ) * ( 1.0 / TWO_PI ) ) * _Dissolve_Radial_Tiling.x )));
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch127_g9 = ( (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) )) + Deform370_g9 );
				#else
				float2 staticSwitch127_g9 = (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) ));
				#endif
				float2 panner141_g9 = ( 1.0 * _Time.y * _Dissolve_Panning + staticSwitch127_g9);
				float Deform_Panning_Type_ref390_g9 = _Deform_Panning_Type;
				float4 texCoord84_g9 = IN.ase_texcoord1;
				texCoord84_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Deform_Panning87_g9 = _Deform_Panning;
				float2 panner124_g9 = ( 1.0 * _Time.y * Deform_Panning87_g9 + staticSwitch127_g9);
				float cos180_g9 = cos(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin180_g9 = sin(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator180_g9 = mul(  ( _Dissolve_Panning_Type - 0.0 > 1.0 ? panner141_g9 : _Dissolve_Panning_Type - 0.0 <= 1.0 && _Dissolve_Panning_Type + 0.0 >= 1.0 ? ( Deform_Panning_Type_ref390_g9 == 1.0 ? ( staticSwitch127_g9 + ( texCoord84_g9.y * Deform_Panning87_g9 ) ) : panner124_g9 ) : ( staticSwitch127_g9 + ( texCoord84_g9.z * _Dissolve_Panning ) ) )  - float2( 0.5,0.5 ) , float2x2( cos180_g9 , -sin180_g9 , sin180_g9 , cos180_g9 )) + float2( 0.5,0.5 );
				float4 tex2DNode197_g9 = SAMPLE_TEXTURE2D( _DissolveTex, sampler_Linear_Repeat, rotator180_g9 );
				float temp_output_398_0_g9 = ( _Dissolve_Channel == 1.0 ? tex2DNode197_g9.r : tex2DNode197_g9.g );
				float Add247_g9 = ( Dissolve_Mask198_g9 + (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float Multiply247_g9 = ( Dissolve_Mask198_g9 * (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float localBlendType247_g9 = BlendType247_g9( MaskBlendType247_g9 , Add247_g9 , Multiply247_g9 );
				float lerpResult262_g9 = lerp( -1.0 , 1.0 , temp_output_394_0_g9);
				float temp_output_276_0_g9 = ( saturate( localBlendType247_g9 ) + lerpResult262_g9 );
				float smoothstepResult238_g9 = smoothstep( lerpResult217_g9 , ( 1.0 - lerpResult217_g9 ) , temp_output_276_0_g9);
				float Dissolve371_g9 = saturate( smoothstepResult238_g9 );
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch295_g9 = Dissolve371_g9;
				#else
				float staticSwitch295_g9 = 1.0;
				#endif
				float temp_output_303_0_g9 = ( saturate( ( IN.ase_color.a * _Main_Color.a * ( _Main_Alpha_Ch == 1.0 ? tex2DNode313_g9.r : tex2DNode313_g9.a ) ) ) * staticSwitch295_g9 );
				float temp_output_326_0_g9 = saturate( ( _Mask_BlendMode == 0.0 ? ( staticSwitch302_g9 * temp_output_303_0_g9 ) : ( staticSwitch302_g9 + temp_output_303_0_g9 ) ) );
				float smoothstepResult336_g9 = smoothstep( lerpResult325_g9 , ( 1.0 - lerpResult325_g9 ) , temp_output_326_0_g9);
				float lerpResult340_g9 = lerp( smoothstepResult336_g9 , temp_output_326_0_g9 , step( _AlphaClip , 1E-05 ));
				float3 ase_normalWS = IN.ase_texcoord4.xyz;
				float dotResult251_g9 = dot( ase_normalWS , -UNITY_MATRIX_V[ 2 ].xyz );
				float smoothstepResult289_g9 = smoothstep( _Fresnel_AlphaClipStepMin , _Fresnel_AlphaClipStepMax , pow( abs( dotResult251_g9 ) , _Fresnel_AlphaClipPower ));
				float temp_output_235_0_g9 = ( 1.0 / _Fresnel_AlphaClipPixelate );
				float temp_output_249_0_g9 = ( temp_output_235_0_g9 * -1.0 );
				float clampResult266_g9 = clamp( ddx( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float2 appendResult199_g9 = (float2(ase_positionSSNorm.x , ase_positionSSNorm.y));
				float2 appendResult201_g9 = (float2(_ScaledScreenParams.x , _ScaledScreenParams.y));
				float2 temp_output_214_0_g9 = ( appendResult199_g9 * appendResult201_g9 );
				float temp_output_185_0_g9 = ( ( _Fresnel_AlphaClipPixelate * ( _ScaledScreenParams.x / 1920.0 ) ) / ( unity_OrthoParams.y / 10.5 ) );
				float2 break264_g9 = ( floor( temp_output_214_0_g9 ) - ( floor( ( temp_output_214_0_g9 / temp_output_185_0_g9 ) ) * temp_output_185_0_g9 ) );
				float clampResult280_g9 = clamp( ddy( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float FresnelAlphaClip334_g9 = (( _Fresnel_AlphaClip )?( step( 0.5 , ( step( 0.05 , smoothstepResult289_g9 ) * ( ( smoothstepResult289_g9 - ( clampResult266_g9 * break264_g9.x ) ) - ( clampResult280_g9 * break264_g9.y ) ) ) ) ):( 1.0 ));
				
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				surfaceDescription.Alpha = ( lerpResult340_g9 * FresnelAlphaClip334_g9 );

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}

            ENDHLSL
        }

		
        Pass
        {
			
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

			Cull Off
			Blend Off
			ZTest LEqual
			ZWrite On

            HLSLPROGRAM

			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170004
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define FEATURES_GRAPH_VERTEX

            #define SHADERPASS SHADERPASS_DEPTHONLY
			#define SCENEPICKINGPASS 1

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        	#define ASE_NEEDS_TEXTURE_COORDINATES0
        	#define ASE_NEEDS_TEXTURE_COORDINATES2
        	#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
        	#define ASE_NEEDS_TEXTURE_COORDINATES3
        	#define ASE_NEEDS_TEXTURE_COORDINATES1
        	#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES2
        	#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
        	#define ASE_NEEDS_VERT_NORMAL
        	#pragma shader_feature_local _MASK_USE_ON
        	#pragma shader_feature_local _DEFORM_USE_ON
        	#pragma shader_feature_local _DISSOLVE_USE_ON


			TEXTURE2D(_Mask_Tex);
			SAMPLER(sampler_Mask_Tex);
			TEXTURE2D(_MainTex);
			TEXTURE2D(_DeformTex);
			SAMPLER(sampler_Linear_Repeat);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_DissolveTex);
			CBUFFER_START( UnityPerMaterial )
			float4 _DeformTex_ST;
			float4 _MainTex_ST;
			float4 _OriginalSize;
			float4 _DissolveTex_ST;
			float4 _SpriteBorder;
			float4 _Main_Color;
			float4 _Dissolve_Edge_Color;
			float4 _Mask_Tex_ST;
			float3 _FixedTileOriginWS;
			float3 _FixedTileRightWS;
			float3 _FixedTileUpWS;
			float2 _Dissolve_Radial_Tiling;
			float2 _Main_Panning;
			float2 _Deform_Mask_OffsetStrength;
			float2 _Deform_Radial_Tiling;
			float2 _Deform_Panning;
			float2 _Dissolve_Panning;
			float2 _Dissolve_Mask_OffsetStrength;
			float2 _Main_Radial_Tiling;
			float _Mask_Alpha_Ch;
			float _DissolveTex_Reverse;
			float _Dissolve_Channel;
			float _Fresnel_AlphaClipStepMax;
			float _Fresnel_AlphaClipStepMin;
			float _Dissolve_Panning_Type;
			float _Fresnel_AlphaClip;
			float _Dissolve_Radial;
			float _Dissolve_smooth;
			float _Main_Alpha_Ch;
			float _Mask_Strength_Mode;
			float _Dissolve_Rotate;
			float _Dissolve_Progress_Type;
			float _Dissolve_Progress;
			float _Dissolve_Edge_Thick;
			float _AlphaClip;
			float _Mask_BlendMode;
			float _Mask_Scale;
			float _Mask_Contrast;
			float _Dissolve_Mask_Rotate;
			float _Mask_Smooth;
			float _Mask_Strength;
			float _Mask_ScaleOffset;
			float _CullMode;
			float _Main_Contrast;
			float _Dissolve_Mask_Smooth;
			float _BlendMode;
			float _MainTex_ColorMode;
			float _Main_Panning_Type;
			float _Main_Radial;
			float _Main_Tiling_Type;
			float _FixedTiling;
			float _UVSwitch;
			float _FixedTileWorldSize;
			float _ToggleSwitch0;
			int _DeformType;
			float _Deform_Panning_Type;
			float _Deform_Radial;
			int _Pixelate;
			float _Deform_Rotate;
			float _Deform_Strength_Type;
			float _Deform_Strength;
			float _Deform_Mask_Smooth;
			int _DeformMask_Type;
			float _Deform_Mask_Rotate;
			float _Main_Rotate;
			float _Hue;
			float _Saturation;
			float _Value;
			float _Fresnel_AlphaClipPower;
			float _Use_Dissolve_Edge;
			float _Dissolve_Edge_smooth;
			int _DissolveMask_BlendType;
			int _DissolveMask_Type;
			float _Fresnel_AlphaClipPixelate;
			CBUFFER_END


            struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_SKINNED_VERTEX_INPUTS
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            float4 _SelectionID;

            struct SurfaceDescription
			{
				float Alpha;
			};

			float2 NineSliceUV139_g9( float2 uv, float4 border, float2 currentSize, float2 originalSize )
			{
				 // currentSize = 파티클 크기 (TexCoord1.xy에서 받음)
				  // originalSize = Material Property로 스크립트에서 전달
				  // border = Material Property로 스크립트에서 전달 (0~1 normalized)
				  float2 scale = currentSize / max(originalSize, 0.001);
				  float2 sMin = border.xy / max(scale, 0.001);
				  float2 sMax = border.zw / max(scale, 0.001);
				  float2 eStart = sMin;
				  float2 eEnd = 1.0 - sMax;
				  float2 L = uv * scale;
				  float2 R = 1.0 - (1.0 - uv) * scale;
				  float2 M = lerp(border.xy, 1.0 - border.zw, saturate((uv - eStart) / max(eEnd - eStart, 0.001)));
				  float2 maskL = step(uv, eStart);
				  float2 maskR = step(eEnd, uv);
				  float2 maskM = 1.0 - maskL - maskR;
				  return L * maskL + R * maskR + M * maskM;
			}
			
			float2 MyCustomExpression404_g9( float3 worldPos, float3 originWS, float3 rightWS, float3 upWS, float tileSize )
			{
				float3 deltaWS = worldPos - originWS;
				return float2(
				    dot(deltaWS, normalize(rightWS)),
				    dot(deltaWS, normalize(upWS))
				) / max(tileSize, 0.0001);
			}
			
			float2 DeformMaskType227_g9( int DeformType, float2 Add, float2 Lerp )
			{
				int mode = (int)DeformType;
				if (mode == 0) return Add;
				else if (mode == 1) return Lerp;
				else return Add;
			}
			
			float DeformMaskType179_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float DissolveMaskType166_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float BlendType247_g9( int MaskBlendType, float Add, float Multiply )
			{
				int mode = (int)MaskBlendType;
				if (mode == 0) return Add;
				else if (mode == 1) return Multiply;
				else return Add;
			}
			

			VertexOutput vert(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_SKINNED_VERTEX_COMPUTE(v);

				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );

				float3 ase_positionWS = TransformObjectToWorld( ( v.positionOS ).xyz );
				o.ase_texcoord2.xyz = ase_positionWS;
				float3 ase_normalWS = TransformObjectToWorldNormal( v.normal );
				o.ase_texcoord4.xyz = ase_normalWS;
				float4 ase_positionCS = TransformObjectToHClip( ( v.positionOS ).xyz );
				float4 screenPos = ComputeScreenPos( ase_positionCS );
				o.ase_texcoord5 = screenPos;
				
				o.ase_texcoord.xy = v.ase_texcoord.xy;
				o.ase_texcoord1 = v.ase_texcoord2;
				o.ase_color = v.ase_color;
				o.ase_texcoord.zw = v.ase_texcoord3.xy;
				o.ase_texcoord3 = v.ase_texcoord1;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord2.w = 0;
				o.ase_texcoord4.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif

				float3 positionWS = TransformObjectToWorld(v.positionOS);
				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float lerpResult325_g9 = lerp( 0.0 , 0.5 , _AlphaClip);
				float lerpResult254_g9 = lerp( 0.0 , 0.5 , _Mask_Smooth);
				float2 uv_Mask_Tex = IN.ase_texcoord.xy * _Mask_Tex_ST.xy + _Mask_Tex_ST.zw;
				float4 tex2DNode186_g9 = SAMPLE_TEXTURE2D( _Mask_Tex, sampler_Mask_Tex, uv_Mask_Tex );
				float4 texCoord229_g9 = IN.ase_texcoord1;
				texCoord229_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_394_0_g9 = ( _Dissolve_Progress_Type == 1.0 ? texCoord229_g9.w : _Dissolve_Progress );
				float Dissolve_Progress_ref203_g9 = temp_output_394_0_g9;
				float smoothstepResult268_g9 = smoothstep( lerpResult254_g9 , ( 1.0 - lerpResult254_g9 ) , ( _Mask_Strength * (pow( saturate( ( _Mask_Alpha_Ch == 1.0 ? tex2DNode186_g9.r : tex2DNode186_g9.a ) ) , _Mask_Contrast )*_Mask_Scale + _Mask_ScaleOffset) * ( _Mask_Strength_Mode == 0.0 ? 1.0 : Dissolve_Progress_ref203_g9 ) ));
				float Mask372_g9 = smoothstepResult268_g9;
				#ifdef _MASK_USE_ON
				float staticSwitch302_g9 = Mask372_g9;
				#else
				float staticSwitch302_g9 = 1.0;
				#endif
				float2 texCoord120_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float2 uv139_g9 = texCoord120_g9;
				float4 border139_g9 = _SpriteBorder;
				float2 texCoord111_g9 = IN.ase_texcoord.zw * float2( 1,1 ) + float2( 0,0 );
				float2 currentSize139_g9 = texCoord111_g9;
				float2 originalSize139_g9 = _OriginalSize.xy;
				float2 localNineSliceUV139_g9 = NineSliceUV139_g9( uv139_g9 , border139_g9 , currentSize139_g9 , originalSize139_g9 );
				float2 temp_output_176_0_g9 = ( ( _UVSwitch != 1.0 ? texCoord120_g9 : localNineSliceUV139_g9 ) * _MainTex_ST.xy );
				float3 ase_positionWS = IN.ase_texcoord2.xyz;
				float3 worldPos404_g9 = ase_positionWS;
				float3 originWS404_g9 = _FixedTileOriginWS;
				float3 rightWS404_g9 = _FixedTileRightWS;
				float3 upWS404_g9 = _FixedTileUpWS;
				float tileSize404_g9 = _FixedTileWorldSize;
				float2 localMyCustomExpression404_g9 = MyCustomExpression404_g9( worldPos404_g9 , originWS404_g9 , rightWS404_g9 , upWS404_g9 , tileSize404_g9 );
				float2 FixedTileData154_g9 = localMyCustomExpression404_g9;
				float2 texCoord157_g9 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_396_0_g9 = ( _Main_Tiling_Type == 1.0 ? (( _FixedTiling )?( ( _MainTex_ST.xy * FixedTileData154_g9 ) ):( ( temp_output_176_0_g9 + _MainTex_ST.zw ) )) : (( _FixedTiling )?( ( texCoord157_g9 * FixedTileData154_g9 ) ):( ( ( temp_output_176_0_g9 * texCoord157_g9 ) + _MainTex_ST.zw ) )) );
				float2 temp_output_34_0_g13 = ( temp_output_396_0_g9 - float2( 0.5,0.5 ) );
				float2 break39_g13 = temp_output_34_0_g13;
				float2 appendResult50_g13 = (float2(( _Main_Radial_Tiling.y * ( length( temp_output_34_0_g13 ) * 2.0 ) ) , ( ( atan2( break39_g13.x , break39_g13.y ) * ( 1.0 / TWO_PI ) ) * _Main_Radial_Tiling.x )));
				int DeformType227_g9 = _DeformType;
				float2 texCoord13_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_11_0_g9 = ( _Pixelate * 1.0 );
				float temp_output_14_0_g9 = max( temp_output_11_0_g9 , 2.0 );
				half2 pixelateduv16_g9 = floor( texCoord13_g9 * float2( temp_output_14_0_g9, temp_output_14_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_14_0_g9, temp_output_14_0_g9 );
				float2 lerpResult18_g9 = lerp( texCoord13_g9 , pixelateduv16_g9 , saturate( step( 1.0 , abs( temp_output_11_0_g9 ) ) ));
				float2 PixelUVBase19_g9 = lerpResult18_g9;
				float2 temp_output_34_0_g10 = ( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g10 = temp_output_34_0_g10;
				float2 appendResult50_g10 = (float2(( _Deform_Radial_Tiling.y * ( length( temp_output_34_0_g10 ) * 2.0 ) ) , ( ( atan2( break39_g10.x , break39_g10.y ) * ( 1.0 / TWO_PI ) ) * _Deform_Radial_Tiling.x )));
				float4 texCoord27_g9 = IN.ase_texcoord1;
				texCoord27_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner36_g9 = ( 1.0 * _Time.y * _Deform_Panning + (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )));
				float cos57_g9 = cos(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin57_g9 = sin(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator57_g9 = mul( ( _Deform_Panning_Type == 1.0 ? ( (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )) + ( texCoord27_g9.y * _Deform_Panning ) ) : panner36_g9 ) - float2( 0.5,0.5 ) , float2x2( cos57_g9 , -sin57_g9 , sin57_g9 , cos57_g9 )) + float2( 0.5,0.5 );
				float2 clampResult64_g9 = clamp( rotator57_g9 , float2( 0.001,0.001 ) , float2( 0.999,0.999 ) );
				int DeformTypeSwitch48_g9 = _DeformType;
				float2 lerpResult74_g9 = lerp( rotator57_g9 , clampResult64_g9 , (float)DeformTypeSwitch48_g9);
				float2 temp_output_110_0_g9 = (SAMPLE_TEXTURE2D( _DeformTex, sampler_Linear_Repeat, lerpResult74_g9 )).rg;
				float2 temp_output_135_0_g9 = ( temp_output_110_0_g9 - float2( 0.5,0.5 ) );
				float2 break109_g9 = temp_output_135_0_g9;
				float4 texCoord91_g9 = IN.ase_texcoord1;
				texCoord91_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_386_0_g9 = ( _Deform_Strength_Type == 1.0 ? texCoord91_g9.x : _Deform_Strength );
				float2 Deform370_g9 = ( temp_output_135_0_g9 * ( break109_g9.x * temp_output_386_0_g9 ) * ( break109_g9.y * temp_output_386_0_g9 ) );
				float2 Add227_g9 = ( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) + Deform370_g9 );
				float2 DeformTex171_g9 = temp_output_110_0_g9;
				float Deform_Strength172_g9 = temp_output_386_0_g9;
				float2 lerpResult211_g9 = lerp( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , DeformTex171_g9 , Deform_Strength172_g9);
				float2 Lerp227_g9 = lerpResult211_g9;
				float2 localDeformMaskType227_g9 = DeformMaskType227_g9( DeformType227_g9 , Add227_g9 , Lerp227_g9 );
				float lerpResult178_g9 = lerp( 0.0 , 0.5 , _Deform_Mask_Smooth);
				int MaskType179_g9 = _DeformMask_Type;
				float2 temp_cast_2 = (_Deform_Mask_OffsetStrength.x).xx;
				float2 texCoord62_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + temp_cast_2;
				float cos114_g9 = cos(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin114_g9 = sin(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator114_g9 = mul( (( _FixedTiling )?( ( texCoord62_g9 * FixedTileData154_g9 ) ):( texCoord62_g9 )) - float2( 0.5,0.5 ) , float2x2( cos114_g9 , -sin114_g9 , sin114_g9 , cos114_g9 )) + float2( 0.5,0.5 );
				float Linear179_g9 = (( rotator114_g9 * _Deform_Mask_OffsetStrength.y )).x;
				float Beam179_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator114_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Radial179_g9 = saturate( ( ( 1.0 - ( distance( rotator114_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Ring179_g9 = 0.0;
				float localDeformMaskType179_g9 = DeformMaskType179_g9( MaskType179_g9 , Linear179_g9 , Beam179_g9 , Radial179_g9 , Ring179_g9 );
				float smoothstepResult193_g9 = smoothstep( lerpResult178_g9 , ( 1.0 - lerpResult178_g9 ) , localDeformMaskType179_g9);
				float Deform_Mask369_g9 = smoothstepResult193_g9;
				float2 lerpResult244_g9 = lerp( localDeformMaskType227_g9 , (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , Deform_Mask369_g9);
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch260_g9 = lerpResult244_g9;
				#else
				float2 staticSwitch260_g9 = (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 ));
				#endif
				float4 texCoord243_g9 = IN.ase_texcoord3;
				texCoord243_g9.xy = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner272_g9 = ( 1.0 * _Time.y * _Main_Panning + staticSwitch260_g9);
				float cos297_g9 = cos(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin297_g9 = sin(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator297_g9 = mul( ( _Main_Panning_Type == 1.0 ? ( staticSwitch260_g9 + ( texCoord243_g9.z * _Main_Panning ) ) : panner272_g9 ) - float2( 0.5,0.5 ) , float2x2( cos297_g9 , -sin297_g9 , sin297_g9 , cos297_g9 )) + float2( 0.5,0.5 );
				float Pixelate257_g9 = temp_output_11_0_g9;
				float temp_output_287_0_g9 = max( Pixelate257_g9 , 2.0 );
				half2 pixelateduv298_g9 = floor( rotator297_g9 * float2( temp_output_287_0_g9, temp_output_287_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_287_0_g9, temp_output_287_0_g9 );
				float2 lerpResult306_g9 = lerp( rotator297_g9 , pixelateduv298_g9 , saturate( step( 1.0 , abs( Pixelate257_g9 ) ) ));
				float4 tex2DNode313_g9 = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, lerpResult306_g9 );
				float lerpResult217_g9 = lerp( 0.0 , 0.5 , _Dissolve_smooth);
				int MaskBlendType247_g9 = _DissolveMask_BlendType;
				float lerpResult167_g9 = lerp( 0.0 , 0.5 , _Dissolve_Mask_Smooth);
				int MaskType166_g9 = _DissolveMask_Type;
				float2 temp_cast_3 = (_Dissolve_Mask_OffsetStrength.x).xx;
				float2 texCoord52_g9 = IN.ase_texcoord.xy * float2( 1,1 ) + temp_cast_3;
				float cos104_g9 = cos(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin104_g9 = sin(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator104_g9 = mul( (( _FixedTiling )?( ( texCoord52_g9 * FixedTileData154_g9 ) ):( texCoord52_g9 )) - float2( 0.5,0.5 ) , float2x2( cos104_g9 , -sin104_g9 , sin104_g9 , cos104_g9 )) + float2( 0.5,0.5 );
				float Linear166_g9 = (( rotator104_g9 * _Dissolve_Mask_OffsetStrength.y )).x;
				float Beam166_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator104_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Radial166_g9 = saturate( ( ( 1.0 - ( distance( rotator104_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Ring166_g9 = 0.0;
				float localDissolveMaskType166_g9 = DissolveMaskType166_g9( MaskType166_g9 , Linear166_g9 , Beam166_g9 , Radial166_g9 , Ring166_g9 );
				float smoothstepResult182_g9 = smoothstep( lerpResult167_g9 , ( 1.0 - lerpResult167_g9 ) , localDissolveMaskType166_g9);
				float Dissolve_Mask198_g9 = smoothstepResult182_g9;
				float2 temp_output_34_0_g11 = ( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g11 = temp_output_34_0_g11;
				float2 appendResult50_g11 = (float2(( _Dissolve_Radial_Tiling.y * ( length( temp_output_34_0_g11 ) * 2.0 ) ) , ( ( atan2( break39_g11.x , break39_g11.y ) * ( 1.0 / TWO_PI ) ) * _Dissolve_Radial_Tiling.x )));
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch127_g9 = ( (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) )) + Deform370_g9 );
				#else
				float2 staticSwitch127_g9 = (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) ));
				#endif
				float2 panner141_g9 = ( 1.0 * _Time.y * _Dissolve_Panning + staticSwitch127_g9);
				float Deform_Panning_Type_ref390_g9 = _Deform_Panning_Type;
				float4 texCoord84_g9 = IN.ase_texcoord1;
				texCoord84_g9.xy = IN.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Deform_Panning87_g9 = _Deform_Panning;
				float2 panner124_g9 = ( 1.0 * _Time.y * Deform_Panning87_g9 + staticSwitch127_g9);
				float cos180_g9 = cos(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin180_g9 = sin(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator180_g9 = mul(  ( _Dissolve_Panning_Type - 0.0 > 1.0 ? panner141_g9 : _Dissolve_Panning_Type - 0.0 <= 1.0 && _Dissolve_Panning_Type + 0.0 >= 1.0 ? ( Deform_Panning_Type_ref390_g9 == 1.0 ? ( staticSwitch127_g9 + ( texCoord84_g9.y * Deform_Panning87_g9 ) ) : panner124_g9 ) : ( staticSwitch127_g9 + ( texCoord84_g9.z * _Dissolve_Panning ) ) )  - float2( 0.5,0.5 ) , float2x2( cos180_g9 , -sin180_g9 , sin180_g9 , cos180_g9 )) + float2( 0.5,0.5 );
				float4 tex2DNode197_g9 = SAMPLE_TEXTURE2D( _DissolveTex, sampler_Linear_Repeat, rotator180_g9 );
				float temp_output_398_0_g9 = ( _Dissolve_Channel == 1.0 ? tex2DNode197_g9.r : tex2DNode197_g9.g );
				float Add247_g9 = ( Dissolve_Mask198_g9 + (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float Multiply247_g9 = ( Dissolve_Mask198_g9 * (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float localBlendType247_g9 = BlendType247_g9( MaskBlendType247_g9 , Add247_g9 , Multiply247_g9 );
				float lerpResult262_g9 = lerp( -1.0 , 1.0 , temp_output_394_0_g9);
				float temp_output_276_0_g9 = ( saturate( localBlendType247_g9 ) + lerpResult262_g9 );
				float smoothstepResult238_g9 = smoothstep( lerpResult217_g9 , ( 1.0 - lerpResult217_g9 ) , temp_output_276_0_g9);
				float Dissolve371_g9 = saturate( smoothstepResult238_g9 );
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch295_g9 = Dissolve371_g9;
				#else
				float staticSwitch295_g9 = 1.0;
				#endif
				float temp_output_303_0_g9 = ( saturate( ( IN.ase_color.a * _Main_Color.a * ( _Main_Alpha_Ch == 1.0 ? tex2DNode313_g9.r : tex2DNode313_g9.a ) ) ) * staticSwitch295_g9 );
				float temp_output_326_0_g9 = saturate( ( _Mask_BlendMode == 0.0 ? ( staticSwitch302_g9 * temp_output_303_0_g9 ) : ( staticSwitch302_g9 + temp_output_303_0_g9 ) ) );
				float smoothstepResult336_g9 = smoothstep( lerpResult325_g9 , ( 1.0 - lerpResult325_g9 ) , temp_output_326_0_g9);
				float lerpResult340_g9 = lerp( smoothstepResult336_g9 , temp_output_326_0_g9 , step( _AlphaClip , 1E-05 ));
				float3 ase_normalWS = IN.ase_texcoord4.xyz;
				float dotResult251_g9 = dot( ase_normalWS , -UNITY_MATRIX_V[ 2 ].xyz );
				float smoothstepResult289_g9 = smoothstep( _Fresnel_AlphaClipStepMin , _Fresnel_AlphaClipStepMax , pow( abs( dotResult251_g9 ) , _Fresnel_AlphaClipPower ));
				float temp_output_235_0_g9 = ( 1.0 / _Fresnel_AlphaClipPixelate );
				float temp_output_249_0_g9 = ( temp_output_235_0_g9 * -1.0 );
				float clampResult266_g9 = clamp( ddx( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float4 screenPos = IN.ase_texcoord5;
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float2 appendResult199_g9 = (float2(ase_positionSSNorm.x , ase_positionSSNorm.y));
				float2 appendResult201_g9 = (float2(_ScaledScreenParams.x , _ScaledScreenParams.y));
				float2 temp_output_214_0_g9 = ( appendResult199_g9 * appendResult201_g9 );
				float temp_output_185_0_g9 = ( ( _Fresnel_AlphaClipPixelate * ( _ScaledScreenParams.x / 1920.0 ) ) / ( unity_OrthoParams.y / 10.5 ) );
				float2 break264_g9 = ( floor( temp_output_214_0_g9 ) - ( floor( ( temp_output_214_0_g9 / temp_output_185_0_g9 ) ) * temp_output_185_0_g9 ) );
				float clampResult280_g9 = clamp( ddy( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float FresnelAlphaClip334_g9 = (( _Fresnel_AlphaClip )?( step( 0.5 , ( step( 0.05 , smoothstepResult289_g9 ) * ( ( smoothstepResult289_g9 - ( clampResult266_g9 * break264_g9.x ) ) - ( clampResult280_g9 * break264_g9.y ) ) ) ) ):( 1.0 ));
				
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				surfaceDescription.Alpha = ( lerpResult340_g9 * FresnelAlphaClip334_g9 );

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = unity_SelectionID;
				return outColor;
			}

            ENDHLSL
        }

		
		Pass
		{
			
            Name "Sprite Forward"
            Tags { "LightMode"="UniversalForward" }

			HLSLPROGRAM

			#define ASE_VERSION 19904
			#define ASE_SRP_VERSION 170004
			#define ASE_USING_SAMPLING_MACROS 1


			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ SKINNED_SPRITE

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_NORMAL
            #define ATTRIBUTES_NEED_TANGENT
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX_NORMAL_OUTPUT
            #define FEATURES_GRAPH_VERTEX_TANGENT_OUTPUT
            #define VARYINGS_NEED_POSITION_WS
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX

			#define SHADERPASS SHADERPASS_SPRITEFORWARD

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging2D.hlsl"

			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_TEXTURE_COORDINATES0
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0
			#define ASE_NEEDS_TEXTURE_COORDINATES3
			#define ASE_NEEDS_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_TEXTURE_COORDINATES1
			#define ASE_NEEDS_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES2
			#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES1
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature_local _DEFORM_USE_ON
			#pragma shader_feature_local _DISSOLVE_USE_ON
			#pragma shader_feature_local _MASK_USE_ON


			TEXTURE2D(_MainTex);
			TEXTURE2D(_DeformTex);
			SAMPLER(sampler_Linear_Repeat);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_DissolveTex);
			TEXTURE2D(_Mask_Tex);
			SAMPLER(sampler_Mask_Tex);
			CBUFFER_START( UnityPerMaterial )
			float4 _DeformTex_ST;
			float4 _MainTex_ST;
			float4 _OriginalSize;
			float4 _DissolveTex_ST;
			float4 _SpriteBorder;
			float4 _Main_Color;
			float4 _Dissolve_Edge_Color;
			float4 _Mask_Tex_ST;
			float3 _FixedTileOriginWS;
			float3 _FixedTileRightWS;
			float3 _FixedTileUpWS;
			float2 _Dissolve_Radial_Tiling;
			float2 _Main_Panning;
			float2 _Deform_Mask_OffsetStrength;
			float2 _Deform_Radial_Tiling;
			float2 _Deform_Panning;
			float2 _Dissolve_Panning;
			float2 _Dissolve_Mask_OffsetStrength;
			float2 _Main_Radial_Tiling;
			float _Mask_Alpha_Ch;
			float _DissolveTex_Reverse;
			float _Dissolve_Channel;
			float _Fresnel_AlphaClipStepMax;
			float _Fresnel_AlphaClipStepMin;
			float _Dissolve_Panning_Type;
			float _Fresnel_AlphaClip;
			float _Dissolve_Radial;
			float _Dissolve_smooth;
			float _Main_Alpha_Ch;
			float _Mask_Strength_Mode;
			float _Dissolve_Rotate;
			float _Dissolve_Progress_Type;
			float _Dissolve_Progress;
			float _Dissolve_Edge_Thick;
			float _AlphaClip;
			float _Mask_BlendMode;
			float _Mask_Scale;
			float _Mask_Contrast;
			float _Dissolve_Mask_Rotate;
			float _Mask_Smooth;
			float _Mask_Strength;
			float _Mask_ScaleOffset;
			float _CullMode;
			float _Main_Contrast;
			float _Dissolve_Mask_Smooth;
			float _BlendMode;
			float _MainTex_ColorMode;
			float _Main_Panning_Type;
			float _Main_Radial;
			float _Main_Tiling_Type;
			float _FixedTiling;
			float _UVSwitch;
			float _FixedTileWorldSize;
			float _ToggleSwitch0;
			int _DeformType;
			float _Deform_Panning_Type;
			float _Deform_Radial;
			int _Pixelate;
			float _Deform_Rotate;
			float _Deform_Strength_Type;
			float _Deform_Strength;
			float _Deform_Mask_Smooth;
			int _DeformMask_Type;
			float _Deform_Mask_Rotate;
			float _Main_Rotate;
			float _Hue;
			float _Saturation;
			float _Value;
			float _Fresnel_AlphaClipPower;
			float _Use_Dissolve_Edge;
			float _Dissolve_Edge_smooth;
			int _DissolveMask_BlendType;
			int _DissolveMask_Type;
			float _Fresnel_AlphaClipPixelate;
			CBUFFER_END


			struct VertexInput
			{
				float3 positionOS : POSITION;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
				float4 uv0 : TEXCOORD0;
				float4 color : COLOR;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_SKINNED_VERTEX_INPUTS
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 texCoord0 : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float4 color : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            struct SurfaceDescription
			{
				float3 BaseColor;
				float Alpha;
				float3 NormalTS;
			};

			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}
			
			float3 RGBToHSV(float3 c)
			{
				float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
				float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
				float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
				float d = q.x - min( q.w, q.y );
				float e = 1.0e-10;
				return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
			}
			float2 NineSliceUV139_g9( float2 uv, float4 border, float2 currentSize, float2 originalSize )
			{
				 // currentSize = 파티클 크기 (TexCoord1.xy에서 받음)
				  // originalSize = Material Property로 스크립트에서 전달
				  // border = Material Property로 스크립트에서 전달 (0~1 normalized)
				  float2 scale = currentSize / max(originalSize, 0.001);
				  float2 sMin = border.xy / max(scale, 0.001);
				  float2 sMax = border.zw / max(scale, 0.001);
				  float2 eStart = sMin;
				  float2 eEnd = 1.0 - sMax;
				  float2 L = uv * scale;
				  float2 R = 1.0 - (1.0 - uv) * scale;
				  float2 M = lerp(border.xy, 1.0 - border.zw, saturate((uv - eStart) / max(eEnd - eStart, 0.001)));
				  float2 maskL = step(uv, eStart);
				  float2 maskR = step(eEnd, uv);
				  float2 maskM = 1.0 - maskL - maskR;
				  return L * maskL + R * maskR + M * maskM;
			}
			
			float2 MyCustomExpression404_g9( float3 worldPos, float3 originWS, float3 rightWS, float3 upWS, float tileSize )
			{
				float3 deltaWS = worldPos - originWS;
				return float2(
				    dot(deltaWS, normalize(rightWS)),
				    dot(deltaWS, normalize(upWS))
				) / max(tileSize, 0.0001);
			}
			
			float2 DeformMaskType227_g9( int DeformType, float2 Add, float2 Lerp )
			{
				int mode = (int)DeformType;
				if (mode == 0) return Add;
				else if (mode == 1) return Lerp;
				else return Add;
			}
			
			float DeformMaskType179_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float DissolveMaskType166_g9( int MaskType, float Linear, float Beam, float Radial, float Ring )
			{
				int mode = (int)MaskType;
				if (mode == 0) return Linear;
				else if (mode == 1) return Beam;
				else if (mode == 2) return Radial;
				else if (mode == 3) return Ring;
				else return Linear;
			}
			
			float BlendType247_g9( int MaskBlendType, float Add, float Multiply )
			{
				int mode = (int)MaskBlendType;
				if (mode == 0) return Add;
				else if (mode == 1) return Multiply;
				else return Add;
			}
			

			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_SKINNED_VERTEX_COMPUTE(v);

				v.positionOS = UnityFlipSprite( v.positionOS, unity_SpriteProps.xy );

				float3 ase_normalWS = TransformObjectToWorldNormal( v.normal );
				o.ase_texcoord6.xyz = ase_normalWS;
				float4 ase_positionCS = TransformObjectToHClip( ( v.positionOS ).xyz );
				float4 screenPos = ComputeScreenPos( ase_positionCS );
				o.ase_texcoord7 = screenPos;
				
				o.ase_texcoord3.xy = v.ase_texcoord3.xy;
				o.ase_texcoord4 = v.ase_texcoord1;
				o.ase_texcoord5 = v.ase_texcoord2;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord3.zw = 0;
				o.ase_texcoord6.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS;
				#else
					float3 defaultVertexValue = float3( 0, 0, 0 );
				#endif
				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS = vertexValue;
				#else
					v.positionOS += vertexValue;
				#endif
				v.normal = v.normal;
				v.tangent.xyz = v.tangent.xyz;

				float3 positionWS = TransformObjectToWorld(v.positionOS);

				o.positionCS = TransformWorldToHClip(positionWS);
				o.positionWS.xyz = positionWS;
				o.texCoord0.xyzw = v.uv0;
				o.color.xyzw = v.color;

				return o;
			}

			half4 frag( VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float4 positionCS = IN.positionCS;
				float3 positionWS = IN.positionWS;

				float2 texCoord120_g9 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float2 uv139_g9 = texCoord120_g9;
				float4 border139_g9 = _SpriteBorder;
				float2 texCoord111_g9 = IN.ase_texcoord3.xy * float2( 1,1 ) + float2( 0,0 );
				float2 currentSize139_g9 = texCoord111_g9;
				float2 originalSize139_g9 = _OriginalSize.xy;
				float2 localNineSliceUV139_g9 = NineSliceUV139_g9( uv139_g9 , border139_g9 , currentSize139_g9 , originalSize139_g9 );
				float2 temp_output_176_0_g9 = ( ( _UVSwitch != 1.0 ? texCoord120_g9 : localNineSliceUV139_g9 ) * _MainTex_ST.xy );
				float3 worldPos404_g9 = positionWS;
				float3 originWS404_g9 = _FixedTileOriginWS;
				float3 rightWS404_g9 = _FixedTileRightWS;
				float3 upWS404_g9 = _FixedTileUpWS;
				float tileSize404_g9 = _FixedTileWorldSize;
				float2 localMyCustomExpression404_g9 = MyCustomExpression404_g9( worldPos404_g9 , originWS404_g9 , rightWS404_g9 , upWS404_g9 , tileSize404_g9 );
				float2 FixedTileData154_g9 = localMyCustomExpression404_g9;
				float2 texCoord157_g9 = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 temp_output_396_0_g9 = ( _Main_Tiling_Type == 1.0 ? (( _FixedTiling )?( ( _MainTex_ST.xy * FixedTileData154_g9 ) ):( ( temp_output_176_0_g9 + _MainTex_ST.zw ) )) : (( _FixedTiling )?( ( texCoord157_g9 * FixedTileData154_g9 ) ):( ( ( temp_output_176_0_g9 * texCoord157_g9 ) + _MainTex_ST.zw ) )) );
				float2 temp_output_34_0_g13 = ( temp_output_396_0_g9 - float2( 0.5,0.5 ) );
				float2 break39_g13 = temp_output_34_0_g13;
				float2 appendResult50_g13 = (float2(( _Main_Radial_Tiling.y * ( length( temp_output_34_0_g13 ) * 2.0 ) ) , ( ( atan2( break39_g13.x , break39_g13.y ) * ( 1.0 / TWO_PI ) ) * _Main_Radial_Tiling.x )));
				int DeformType227_g9 = _DeformType;
				float2 texCoord13_g9 = IN.texCoord0.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_11_0_g9 = ( _Pixelate * 1.0 );
				float temp_output_14_0_g9 = max( temp_output_11_0_g9 , 2.0 );
				half2 pixelateduv16_g9 = floor( texCoord13_g9 * float2( temp_output_14_0_g9, temp_output_14_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_14_0_g9, temp_output_14_0_g9 );
				float2 lerpResult18_g9 = lerp( texCoord13_g9 , pixelateduv16_g9 , saturate( step( 1.0 , abs( temp_output_11_0_g9 ) ) ));
				float2 PixelUVBase19_g9 = lerpResult18_g9;
				float2 temp_output_34_0_g10 = ( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g10 = temp_output_34_0_g10;
				float2 appendResult50_g10 = (float2(( _Deform_Radial_Tiling.y * ( length( temp_output_34_0_g10 ) * 2.0 ) ) , ( ( atan2( break39_g10.x , break39_g10.y ) * ( 1.0 / TWO_PI ) ) * _Deform_Radial_Tiling.x )));
				float4 texCoord27_g9 = IN.ase_texcoord5;
				texCoord27_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner36_g9 = ( 1.0 * _Time.y * _Deform_Panning + (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )));
				float cos57_g9 = cos(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin57_g9 = sin(  (0.0 + ( _Deform_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator57_g9 = mul( ( _Deform_Panning_Type == 1.0 ? ( (( _Deform_Radial )?( appendResult50_g10 ):( (( _FixedTiling )?( ( _DeformTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DeformTex_ST.xy ) + _DeformTex_ST.zw ) )) )) + ( texCoord27_g9.y * _Deform_Panning ) ) : panner36_g9 ) - float2( 0.5,0.5 ) , float2x2( cos57_g9 , -sin57_g9 , sin57_g9 , cos57_g9 )) + float2( 0.5,0.5 );
				float2 clampResult64_g9 = clamp( rotator57_g9 , float2( 0.001,0.001 ) , float2( 0.999,0.999 ) );
				int DeformTypeSwitch48_g9 = _DeformType;
				float2 lerpResult74_g9 = lerp( rotator57_g9 , clampResult64_g9 , (float)DeformTypeSwitch48_g9);
				float2 temp_output_110_0_g9 = (SAMPLE_TEXTURE2D( _DeformTex, sampler_Linear_Repeat, lerpResult74_g9 )).rg;
				float2 temp_output_135_0_g9 = ( temp_output_110_0_g9 - float2( 0.5,0.5 ) );
				float2 break109_g9 = temp_output_135_0_g9;
				float4 texCoord91_g9 = IN.ase_texcoord5;
				texCoord91_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_386_0_g9 = ( _Deform_Strength_Type == 1.0 ? texCoord91_g9.x : _Deform_Strength );
				float2 Deform370_g9 = ( temp_output_135_0_g9 * ( break109_g9.x * temp_output_386_0_g9 ) * ( break109_g9.y * temp_output_386_0_g9 ) );
				float2 Add227_g9 = ( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) + Deform370_g9 );
				float2 DeformTex171_g9 = temp_output_110_0_g9;
				float Deform_Strength172_g9 = temp_output_386_0_g9;
				float2 lerpResult211_g9 = lerp( (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , DeformTex171_g9 , Deform_Strength172_g9);
				float2 Lerp227_g9 = lerpResult211_g9;
				float2 localDeformMaskType227_g9 = DeformMaskType227_g9( DeformType227_g9 , Add227_g9 , Lerp227_g9 );
				float lerpResult178_g9 = lerp( 0.0 , 0.5 , _Deform_Mask_Smooth);
				int MaskType179_g9 = _DeformMask_Type;
				float2 temp_cast_2 = (_Deform_Mask_OffsetStrength.x).xx;
				float2 texCoord62_g9 = IN.texCoord0.xy * float2( 1,1 ) + temp_cast_2;
				float cos114_g9 = cos(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin114_g9 = sin(  (0.0 + ( _Deform_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator114_g9 = mul( (( _FixedTiling )?( ( texCoord62_g9 * FixedTileData154_g9 ) ):( texCoord62_g9 )) - float2( 0.5,0.5 ) , float2x2( cos114_g9 , -sin114_g9 , sin114_g9 , cos114_g9 )) + float2( 0.5,0.5 );
				float Linear179_g9 = (( rotator114_g9 * _Deform_Mask_OffsetStrength.y )).x;
				float Beam179_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator114_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Radial179_g9 = saturate( ( ( 1.0 - ( distance( rotator114_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Deform_Mask_OffsetStrength.y ) );
				float Ring179_g9 = 0.0;
				float localDeformMaskType179_g9 = DeformMaskType179_g9( MaskType179_g9 , Linear179_g9 , Beam179_g9 , Radial179_g9 , Ring179_g9 );
				float smoothstepResult193_g9 = smoothstep( lerpResult178_g9 , ( 1.0 - lerpResult178_g9 ) , localDeformMaskType179_g9);
				float Deform_Mask369_g9 = smoothstepResult193_g9;
				float2 lerpResult244_g9 = lerp( localDeformMaskType227_g9 , (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 )) , Deform_Mask369_g9);
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch260_g9 = lerpResult244_g9;
				#else
				float2 staticSwitch260_g9 = (( _Main_Radial )?( appendResult50_g13 ):( temp_output_396_0_g9 ));
				#endif
				float4 texCoord243_g9 = IN.ase_texcoord4;
				texCoord243_g9.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 panner272_g9 = ( 1.0 * _Time.y * _Main_Panning + staticSwitch260_g9);
				float cos297_g9 = cos(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin297_g9 = sin(  (0.0 + ( _Main_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator297_g9 = mul( ( _Main_Panning_Type == 1.0 ? ( staticSwitch260_g9 + ( texCoord243_g9.z * _Main_Panning ) ) : panner272_g9 ) - float2( 0.5,0.5 ) , float2x2( cos297_g9 , -sin297_g9 , sin297_g9 , cos297_g9 )) + float2( 0.5,0.5 );
				float Pixelate257_g9 = temp_output_11_0_g9;
				float temp_output_287_0_g9 = max( Pixelate257_g9 , 2.0 );
				half2 pixelateduv298_g9 = floor( rotator297_g9 * float2( temp_output_287_0_g9, temp_output_287_0_g9 ) + float2( 0,0 ) ) / float2( temp_output_287_0_g9, temp_output_287_0_g9 );
				float2 lerpResult306_g9 = lerp( rotator297_g9 , pixelateduv298_g9 , saturate( step( 1.0 , abs( Pixelate257_g9 ) ) ));
				float4 tex2DNode313_g9 = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, lerpResult306_g9 );
				float3 hsvTorgb3_g12 = RGBToHSV( tex2DNode313_g9.rgb );
				float3 hsvTorgb10_g12 = HSVToRGB( float3(frac( ( hsvTorgb3_g12.x + _Hue ) ),saturate( ( hsvTorgb3_g12.y * _Saturation ) ),( hsvTorgb3_g12.z * _Value )) );
				float3 temp_output_329_0_g9 = hsvTorgb10_g12;
				float3 temp_cast_3 = (tex2DNode313_g9.r).xxx;
				float3 temp_cast_4 = (tex2DNode313_g9.r).xxx;
				float4 texCoord321_g9 = IN.ase_texcoord4;
				texCoord321_g9.xy = IN.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float3 lerpResult330_g9 = lerp( temp_output_329_0_g9 , temp_cast_4 , texCoord321_g9.w);
				float3 ifLocalVar338_g9 = 0;
				if( 1.0 > _MainTex_ColorMode )
				ifLocalVar338_g9 = temp_output_329_0_g9;
				else if( 1.0 == _MainTex_ColorMode )
				ifLocalVar338_g9 = temp_cast_3;
				else if( 1.0 < _MainTex_ColorMode )
				ifLocalVar338_g9 = lerpResult330_g9;
				float3 temp_cast_5 = (_Main_Contrast).xxx;
				float lerpResult319_g9 = lerp( 0.0 , 0.5 , _Dissolve_Edge_smooth);
				int MaskBlendType247_g9 = _DissolveMask_BlendType;
				float lerpResult167_g9 = lerp( 0.0 , 0.5 , _Dissolve_Mask_Smooth);
				int MaskType166_g9 = _DissolveMask_Type;
				float2 temp_cast_6 = (_Dissolve_Mask_OffsetStrength.x).xx;
				float2 texCoord52_g9 = IN.texCoord0.xy * float2( 1,1 ) + temp_cast_6;
				float cos104_g9 = cos(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin104_g9 = sin(  (0.0 + ( _Dissolve_Mask_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator104_g9 = mul( (( _FixedTiling )?( ( texCoord52_g9 * FixedTileData154_g9 ) ):( texCoord52_g9 )) - float2( 0.5,0.5 ) , float2x2( cos104_g9 , -sin104_g9 , sin104_g9 , cos104_g9 )) + float2( 0.5,0.5 );
				float Linear166_g9 = (( rotator104_g9 * _Dissolve_Mask_OffsetStrength.y )).x;
				float Beam166_g9 = saturate( ( ( 1.0 - ( abs( ( (( ( rotator104_g9 - float2( 0.5,0 ) ) + float2( 0.5,0 ) )).x - 0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Radial166_g9 = saturate( ( ( 1.0 - ( distance( rotator104_g9 , float2( 0.5,0.5 ) ) * 2.0 ) ) * _Dissolve_Mask_OffsetStrength.y ) );
				float Ring166_g9 = 0.0;
				float localDissolveMaskType166_g9 = DissolveMaskType166_g9( MaskType166_g9 , Linear166_g9 , Beam166_g9 , Radial166_g9 , Ring166_g9 );
				float smoothstepResult182_g9 = smoothstep( lerpResult167_g9 , ( 1.0 - lerpResult167_g9 ) , localDissolveMaskType166_g9);
				float Dissolve_Mask198_g9 = smoothstepResult182_g9;
				float2 temp_output_34_0_g11 = ( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) - float2( 0.5,0.5 ) );
				float2 break39_g11 = temp_output_34_0_g11;
				float2 appendResult50_g11 = (float2(( _Dissolve_Radial_Tiling.y * ( length( temp_output_34_0_g11 ) * 2.0 ) ) , ( ( atan2( break39_g11.x , break39_g11.y ) * ( 1.0 / TWO_PI ) ) * _Dissolve_Radial_Tiling.x )));
				#ifdef _DEFORM_USE_ON
				float2 staticSwitch127_g9 = ( (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) )) + Deform370_g9 );
				#else
				float2 staticSwitch127_g9 = (( _Dissolve_Radial )?( appendResult50_g11 ):( (( _FixedTiling )?( ( _DissolveTex_ST.xy * FixedTileData154_g9 ) ):( ( ( PixelUVBase19_g9 * _DissolveTex_ST.xy ) + _DissolveTex_ST.zw ) )) ));
				#endif
				float2 panner141_g9 = ( 1.0 * _Time.y * _Dissolve_Panning + staticSwitch127_g9);
				float Deform_Panning_Type_ref390_g9 = _Deform_Panning_Type;
				float4 texCoord84_g9 = IN.ase_texcoord5;
				texCoord84_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float2 Deform_Panning87_g9 = _Deform_Panning;
				float2 panner124_g9 = ( 1.0 * _Time.y * Deform_Panning87_g9 + staticSwitch127_g9);
				float cos180_g9 = cos(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float sin180_g9 = sin(  (0.0 + ( _Dissolve_Rotate - 0.0 ) * ( TWO_PI - 0.0 ) / ( 360.0 - 0.0 ) ) );
				float2 rotator180_g9 = mul(  ( _Dissolve_Panning_Type - 0.0 > 1.0 ? panner141_g9 : _Dissolve_Panning_Type - 0.0 <= 1.0 && _Dissolve_Panning_Type + 0.0 >= 1.0 ? ( Deform_Panning_Type_ref390_g9 == 1.0 ? ( staticSwitch127_g9 + ( texCoord84_g9.y * Deform_Panning87_g9 ) ) : panner124_g9 ) : ( staticSwitch127_g9 + ( texCoord84_g9.z * _Dissolve_Panning ) ) )  - float2( 0.5,0.5 ) , float2x2( cos180_g9 , -sin180_g9 , sin180_g9 , cos180_g9 )) + float2( 0.5,0.5 );
				float4 tex2DNode197_g9 = SAMPLE_TEXTURE2D( _DissolveTex, sampler_Linear_Repeat, rotator180_g9 );
				float temp_output_398_0_g9 = ( _Dissolve_Channel == 1.0 ? tex2DNode197_g9.r : tex2DNode197_g9.g );
				float Add247_g9 = ( Dissolve_Mask198_g9 + (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float Multiply247_g9 = ( Dissolve_Mask198_g9 * (( _DissolveTex_Reverse )?( ( 1.0 - temp_output_398_0_g9 ) ):( temp_output_398_0_g9 )) );
				float localBlendType247_g9 = BlendType247_g9( MaskBlendType247_g9 , Add247_g9 , Multiply247_g9 );
				float4 texCoord229_g9 = IN.ase_texcoord5;
				texCoord229_g9.xy = IN.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float temp_output_394_0_g9 = ( _Dissolve_Progress_Type == 1.0 ? texCoord229_g9.w : _Dissolve_Progress );
				float lerpResult262_g9 = lerp( -1.0 , 1.0 , temp_output_394_0_g9);
				float temp_output_276_0_g9 = ( saturate( localBlendType247_g9 ) + lerpResult262_g9 );
				float Dissolve_Before_Smooth296_g9 = temp_output_276_0_g9;
				float smoothstepResult327_g9 = smoothstep( lerpResult319_g9 , ( 1.0 - lerpResult319_g9 ) , ( 1.0 - ( Dissolve_Before_Smooth296_g9 - _Dissolve_Edge_Thick ) ));
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch339_g9 = smoothstepResult327_g9;
				#else
				float staticSwitch339_g9 = 0.0;
				#endif
				float3 lerpResult373_g9 = lerp( ( (IN.color).rgb * pow( abs( ifLocalVar338_g9 ) , temp_cast_5 ) * _Main_Color.rgb ) , _Dissolve_Edge_Color.rgb , (( _Use_Dissolve_Edge )?( staticSwitch339_g9 ):( 0.0 )));
				
				float lerpResult325_g9 = lerp( 0.0 , 0.5 , _AlphaClip);
				float lerpResult254_g9 = lerp( 0.0 , 0.5 , _Mask_Smooth);
				float2 uv_Mask_Tex = IN.texCoord0.xy * _Mask_Tex_ST.xy + _Mask_Tex_ST.zw;
				float4 tex2DNode186_g9 = SAMPLE_TEXTURE2D( _Mask_Tex, sampler_Mask_Tex, uv_Mask_Tex );
				float Dissolve_Progress_ref203_g9 = temp_output_394_0_g9;
				float smoothstepResult268_g9 = smoothstep( lerpResult254_g9 , ( 1.0 - lerpResult254_g9 ) , ( _Mask_Strength * (pow( saturate( ( _Mask_Alpha_Ch == 1.0 ? tex2DNode186_g9.r : tex2DNode186_g9.a ) ) , _Mask_Contrast )*_Mask_Scale + _Mask_ScaleOffset) * ( _Mask_Strength_Mode == 0.0 ? 1.0 : Dissolve_Progress_ref203_g9 ) ));
				float Mask372_g9 = smoothstepResult268_g9;
				#ifdef _MASK_USE_ON
				float staticSwitch302_g9 = Mask372_g9;
				#else
				float staticSwitch302_g9 = 1.0;
				#endif
				float lerpResult217_g9 = lerp( 0.0 , 0.5 , _Dissolve_smooth);
				float smoothstepResult238_g9 = smoothstep( lerpResult217_g9 , ( 1.0 - lerpResult217_g9 ) , temp_output_276_0_g9);
				float Dissolve371_g9 = saturate( smoothstepResult238_g9 );
				#ifdef _DISSOLVE_USE_ON
				float staticSwitch295_g9 = Dissolve371_g9;
				#else
				float staticSwitch295_g9 = 1.0;
				#endif
				float temp_output_303_0_g9 = ( saturate( ( IN.color.a * _Main_Color.a * ( _Main_Alpha_Ch == 1.0 ? tex2DNode313_g9.r : tex2DNode313_g9.a ) ) ) * staticSwitch295_g9 );
				float temp_output_326_0_g9 = saturate( ( _Mask_BlendMode == 0.0 ? ( staticSwitch302_g9 * temp_output_303_0_g9 ) : ( staticSwitch302_g9 + temp_output_303_0_g9 ) ) );
				float smoothstepResult336_g9 = smoothstep( lerpResult325_g9 , ( 1.0 - lerpResult325_g9 ) , temp_output_326_0_g9);
				float lerpResult340_g9 = lerp( smoothstepResult336_g9 , temp_output_326_0_g9 , step( _AlphaClip , 1E-05 ));
				float3 ase_normalWS = IN.ase_texcoord6.xyz;
				float dotResult251_g9 = dot( ase_normalWS , -UNITY_MATRIX_V[ 2 ].xyz );
				float smoothstepResult289_g9 = smoothstep( _Fresnel_AlphaClipStepMin , _Fresnel_AlphaClipStepMax , pow( abs( dotResult251_g9 ) , _Fresnel_AlphaClipPower ));
				float temp_output_235_0_g9 = ( 1.0 / _Fresnel_AlphaClipPixelate );
				float temp_output_249_0_g9 = ( temp_output_235_0_g9 * -1.0 );
				float clampResult266_g9 = clamp( ddx( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float4 screenPos = IN.ase_texcoord7;
				float4 ase_positionSSNorm = screenPos / screenPos.w;
				ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
				float2 appendResult199_g9 = (float2(ase_positionSSNorm.x , ase_positionSSNorm.y));
				float2 appendResult201_g9 = (float2(_ScaledScreenParams.x , _ScaledScreenParams.y));
				float2 temp_output_214_0_g9 = ( appendResult199_g9 * appendResult201_g9 );
				float temp_output_185_0_g9 = ( ( _Fresnel_AlphaClipPixelate * ( _ScaledScreenParams.x / 1920.0 ) ) / ( unity_OrthoParams.y / 10.5 ) );
				float2 break264_g9 = ( floor( temp_output_214_0_g9 ) - ( floor( ( temp_output_214_0_g9 / temp_output_185_0_g9 ) ) * temp_output_185_0_g9 ) );
				float clampResult280_g9 = clamp( ddy( smoothstepResult289_g9 ) , temp_output_249_0_g9 , temp_output_235_0_g9 );
				float FresnelAlphaClip334_g9 = (( _Fresnel_AlphaClip )?( step( 0.5 , ( step( 0.05 , smoothstepResult289_g9 ) * ( ( smoothstepResult289_g9 - ( clampResult266_g9 * break264_g9.x ) ) - ( clampResult280_g9 * break264_g9.y ) ) ) ) ):( 1.0 ));
				
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;
				surfaceDescription.BaseColor = lerpResult373_g9;
				surfaceDescription.NormalTS = float3(0.0f, 0.0f, 1.0f);
				surfaceDescription.Alpha = ( lerpResult340_g9 * FresnelAlphaClip334_g9 );


				half4 color = half4(surfaceDescription.BaseColor, surfaceDescription.Alpha);

				#if defined(DEBUG_DISPLAY)
					SurfaceData2D surfaceData;
					InitializeSurfaceData(color.rgb, color.a, surfaceData);
					InputData2D inputData;
					InitializeInputData(positionWS.xy, half2(IN.texCoord0.xy), inputData);
					half4 debugColor = 0;

					SETUP_DEBUG_DATA_2D(inputData, positionWS, positionCS);

					if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
					{
						return debugColor;
					}
				#endif

				color *= IN.color;
				
				return color;
			}

            ENDHLSL
        }
		
	}
	CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19904
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;2320,-304;Inherit;False;Property;_CullMode;CullMode;1;1;[Enum];Create;True;0;0;1;UnityEngine.Rendering.CullMode;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;14;2160,-304;Inherit;False;Property;_BlendMode;BlendMode;0;1;[Enum];Create;True;0;2;Additive;1;AlphaBlend;10;0;True;0;False;10;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;907;1808,-512;Inherit;False;VFXMasterFuntion;2;;9;07ae1a8eb5d15ef41a8d5e73ec777104;0;0;2;FLOAT3;0;FLOAT;374
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;426;2160,-512;Float;False;True;-1;3;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;17;CAT/VFX/URP2D_VFX_Master;27c2e37ef0ae0ed4ba9ce8c439224f0b;True;Sprite Forward;0;4;Sprite Forward;6;True;True;2;5;False;;10;True;_BlendMode;4;1;False;;1;False;_BlendMode;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;True;True;2;True;_CullMode;True;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;2;False;;True;0;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;UniversalMaterialType=Lit;Queue=Transparent=Queue=0;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;2;Vertex Position;1;0;Debug Display;0;0;0;5;True;False;True;True;True;False;;True;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;422;1008,-400;Float;False;False;-1;3;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;17;New Amplify Shader;27c2e37ef0ae0ed4ba9ce8c439224f0b;True;Sprite Lit;0;0;Sprite Lit;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;UniversalMaterialType=Lit;Queue=Transparent=Queue=0;ShaderGraphShader=true;True;0;True;12;all;0;False;False;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;423;1008,-400;Float;False;False;-1;3;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;17;New Amplify Shader;27c2e37ef0ae0ed4ba9ce8c439224f0b;True;Sprite Normal;0;1;Sprite Normal;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;UniversalMaterialType=Lit;Queue=Transparent=Queue=0;ShaderGraphShader=true;True;0;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=NormalsRendering;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;424;1008,-400;Float;False;False;-1;3;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;17;New Amplify Shader;27c2e37ef0ae0ed4ba9ce8c439224f0b;True;SceneSelectionPass;0;2;SceneSelectionPass;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;UniversalMaterialType=Lit;Queue=Transparent=Queue=0;ShaderGraphShader=true;True;0;True;12;all;0;False;True;0;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;425;1008,-400;Float;False;False;-1;3;UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI;0;17;New Amplify Shader;27c2e37ef0ae0ed4ba9ce8c439224f0b;True;ScenePickingPass;0;3;ScenePickingPass;0;False;True;2;5;False;;10;False;;3;1;False;;10;False;;True;1;False;;5;False;;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;5;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;UniversalMaterialType=Lit;Queue=Transparent=Queue=0;ShaderGraphShader=true;True;0;True;12;all;0;False;True;0;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
WireConnection;426;0;907;0
WireConnection;426;2;907;374
ASEEND*/
//CHKSM=C39EF1974FB853A41D6EC4AE76BA1C81BA02AF5A