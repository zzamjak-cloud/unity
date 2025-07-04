﻿//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityInternal = UnityEngine.Internal;

namespace ChocDino.UIFX
{
	public enum GradientWrap
	{
		Clamp,
		Repeat,
		Mirror,
	}

	public enum GradientLerp
	{
		Step,
		Linear,
		Smooth,
	}

	public enum GradientColorSpace
	{
		Linear,
		Perceptual,
	}

	public enum GradientShape
	{
		None,
		Horizontal,
		Vertical,
		Diagonal,
		Linear,
		Radial,
		Conic,
	}

	[UnityInternal.ExcludeFromDocs]
	internal class GradientTexture : System.IDisposable
	{
		private static Color s_color = Color.black;

		private int _resolution = 256;
		private Texture2D _texture;

		public Texture2D Texture { get { return _texture; } }

		public GradientTexture(int resolution)
		{
			_resolution = resolution;
		}

		public void Update(Gradient gradient)
		{
			if (_texture == null)
			{
				_texture = new Texture2D(_resolution, 1, TextureFormat.RGBAHalf, false);
				_texture.filterMode = FilterMode.Bilinear;
				_texture.wrapMode = TextureWrapMode.Clamp;
			}
			float inv = 1f / (_texture.width - 1);
			for (int x = 0; x < _texture.width; x++) 
			{
				var t = x * inv;
				// TODO: convert to premultiplied and linear here?
				_texture.SetPixel(x, 0, gradient.Evaluate(t).linear);
			}
			_texture.Apply(false, false);
		}

		public void Update(AnimationCurve curve)
		{
			if (_texture == null)
			{
				_texture = new Texture2D(_resolution, 1, TextureFormat.RHalf, false);
				_texture.filterMode = FilterMode.Bilinear;
				_texture.wrapMode = TextureWrapMode.Clamp;
			}
			float inv = 1f / (_texture.width - 1);
			for (int x = 0; x < _texture.width; x++) 
			{
				var t = x * inv;
				s_color.r = curve.Evaluate(t);
				_texture.SetPixel(x, 0, s_color);
			}
			_texture.Apply(false, false);
		}

		public void Dispose()
		{
			ObjectHelper.Destroy(ref _texture);
		}
	}

#if false

	public enum GradientWrap
	{
		Clamp,
		Repeat,
		Mirror,
	}

	public enum GradientMix
	{
		Step,
		Linear,
		Smooth,
	}

	public enum GradientColorSpace
	{
		sRGB,
		Linear,
		Perceptual,
	}
#endif

	[UnityInternal.ExcludeFromDocs]
	public static class GradientUtils
	{
		#if false
		public static Color EvalGradient(float t, Gradient gradient, GradientWrapMode wrapMode, float offset = 0f, float scale = 1f, float scalePivot = 0f)
		{
			t -= scalePivot;
			t *= scale;
			t += scalePivot;
			t += offset;

			if (wrapMode == GradientWrapMode.Wrap)
			{
				// NOTE: Only wrap if we're outside of the range, otherwise for t=1.0 (which happens often) we'll evaulate 0.0 which in most cases is not what we want
				if (t < 0f || t > 1f)
				{
					t = Mathf.Repeat(t, 1f);
				}
			}
			else if (wrapMode == GradientWrapMode.Mirror)
			{
				t = Mathf.PingPong(t, 1f);
				if (Mathf.Sign(scale) < 0f)
				{
					t = 1f - t;
				}
			}

			return gradient.Evaluate(t);
		}
		#endif

		/// <summary>
		/// Reverse the gradient
		/// </summary>
		public static void Reverse(Gradient gradient)
		{
			GradientColorKey[] colors = gradient.colorKeys;
			GradientAlphaKey[] alphas = gradient.alphaKeys;

			for (int i = 0; i < colors.Length; i++)
			{
				colors[i].time = 1f - colors[i].time;
			}
			for (int i = 0; i < alphas.Length; i++)
			{
				alphas[i].time = 1f - alphas[i].time;
			}

			gradient.SetKeys(colors, alphas);
		}

		/// <summary>
		/// CSS linear gradients always have the start and end colors at one of the edges/corners.
		/// This method calculates the parameters for our shader.
		/// </summary>
		public static void GetCssLinearGradientShaderParams(float angle, Rect rect, out Vector2 uvPointOnStartLine, out Vector2 uvStartLineDirection, out float uvGradientLength, out float uvRectRatio)
		{
			// Definitions:
			// Gradient angle 0..360 degrees clock-wise where 0 is upwards.
			// Gradient line goes from the center of the rectangle in the direction of the angle.
			// Method:
			// The gradient line runs in the direction of the angle.
			// The gradient goes from a Start line to an End line. These lines are parallel and are perpendicular to the Gradient line.
			// From the angle, we pick the closest opposite quadrant corner on the rectangle - this corner point will be a point on the Start line.
			// We get the direction of the start line, which is just 90 degree rotation of the gradient direction.
			// In the shader it does a dot product to find the closest point on the Start line from the uv coordinate - this distance is used to draw the gradient.

			// Get the coordinates of the closest quadrant corner in the opposite direction of our gradient
			// this point lies on the starting line
			Vector2 startingLineCorner = rect.min;
			{
				int quadrant = Mathf.FloorToInt((angle % 360f) / 90f);
				switch (quadrant)
				{
					case 0:
					break;
					case 1:
					startingLineCorner = new Vector2(rect.xMin, rect.yMax);
					break;
					case 2:
					startingLineCorner = rect.max;
					break;
					case 3:
					startingLineCorner = new Vector2(rect.xMax, rect.yMin);
					break;
					default:
					// Should never get here
					Debug.LogError("Invalid quadarant");
					break;
				}
			}

			float angleRad = Mathf.Deg2Rad * angle;
			uvRectRatio = rect.width / rect.height;

			// Calculate distance between Start and End line in UV-space
			{
				float uvWidth = uvRectRatio;
				float uvHeight = 1f;
				Vector2 gradientDirection = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));
				uvGradientLength = Mathf.Abs(uvWidth * gradientDirection.x) + Mathf.Abs(uvHeight * gradientDirection.y);
			}

			// Convert pointOnStartLine to UV-space
			uvPointOnStartLine.x = (startingLineCorner.x - rect.x) / rect.width;
			uvPointOnStartLine.y = (startingLineCorner.y - rect.y) / rect.height;
			uvPointOnStartLine.x *= uvRectRatio;

			// Calculate direction of the start line
			uvStartLineDirection = new Vector2(Mathf.Sin(angleRad+Mathf.PI * 0.5f), Mathf.Cos(angleRad+Mathf.PI * 0.5f));
		}
	}

#if false
	[System.Serializable]
	internal class GradientShader
	{
		internal static class ShaderProp
		{
			public readonly static int GradientColorCount = Shader.PropertyToID("_GradientColorCount");
			public readonly static int GradientAlphaCount = Shader.PropertyToID("_GradientAlphaCount");
			public readonly static int GradientColors = Shader.PropertyToID("_GradientColors");
			public readonly static int GradientAlphas = Shader.PropertyToID("_GradientAlphas");
			public readonly static int GradientTransform = Shader.PropertyToID("_GradientTransform");
			public readonly static int GradientRadial = Shader.PropertyToID("_GradientRadial");
			public readonly static int GradientDither = Shader.PropertyToID("_GradientDither");
		}
		internal static class ShaderKeyword
		{
			public const string GradientMixSmooth = "GRADIENT_MIX_SMOOTH";
			public const string GradientMixLinear = "GRADIENT_MIX_LINEAR";
			public const string GradientMixStep = "GRADIENT_MIX_STEP";

			internal const string GradientColorSpaceSRGB = "GRADIENT_COLORSPACE_SRGB";
			internal const string GradientColorSpaceLinear = "GRADIENT_COLORSPACE_LINEAR";
			internal const string GradientColorSpacePerceptual = "GRADIENT_COLORSPACE_PERCEPTUAL";
		}

		[SerializeField] Gradient _gradient;
		[SerializeField] GradientMix _mixMode = GradientMix.Smooth;
		[SerializeField] GradientColorSpace _colorSpace = GradientColorSpace.Perceptual;
		[Range(0f, 1f)]
		[SerializeField] float _dither = 0.5f;
		/*[Range(-1f, 1f)]
		[SerializeField] float _centerX = 0f;
		[Range(-1f, 1f)]
		[SerializeField] float _centerY = 0f;
		[Range(0f, 16f)]
		[SerializeField] float _radius = 0.5f;*/
		[SerializeField] float _scale = 1f;
		[Range(0f, 1f)]
		[SerializeField] float _scalePivot = 0.5f;
		[SerializeField] float _offset = 0f;
		[SerializeField] GradientWrap _wrapMode = GradientWrap.Clamp;

		//public float GradientCenterX { get { return _gradientCenterX; } set { _gradientCenterX = value; ForceUpdate(); } }
		//public float GradientCenterY { get { return _gradientCenterY; } set { _gradientCenterY = value; ForceUpdate(); } }
		//public float GradientRadius { get { return _gradientRadius; } set { _gradientRadius = value; ForceUpdate(); } }
		//public Gradient Gradient { get { return _gradient; } set { _gradient = value; ForceUpdate(); } }

		private Vector4[] _colorKeys = new Vector4[8];
		private Vector4[] _alphaKeys = new Vector4[8];

		private void GradientToArrays()
		{
			int colorKeyCount = _gradient.colorKeys.Length;
			for (int i = 0; i < colorKeyCount; i++)
			{
				Color c = _gradient.colorKeys[i].color;

				switch (_colorSpace)
				{
					default:
					case GradientColorSpace.sRGB:
					_colorKeys[i] = new Vector4(c.r, c.g, c.b, _gradient.colorKeys[i].time);
					break;
					case GradientColorSpace.Linear:
					c = c.linear;
					_colorKeys[i] = new Vector4(c.r, c.g, c.b, _gradient.colorKeys[i].time);
					break;
					case GradientColorSpace.Perceptual:
					{
						Vector3 oklab = ColorUtils.LinearToOklab(c.linear);
						_colorKeys[i] = new Vector4(oklab.x, oklab.y, oklab.z, _gradient.colorKeys[i].time);
					}
					break;
				}
			}
			int alphaKeyCount = _gradient.alphaKeys.Length;
			for (int i = 0; i < alphaKeyCount; i++)
			{
				_alphaKeys[i] = new Vector4(_gradient.alphaKeys[i].alpha, 0f, 0f, _gradient.alphaKeys[i].time);
			}
		}

		internal void SetupMaterial(Material material)
		{
			if (_gradient == null ) { return; }

			GradientToArrays();

			material.SetInt(ShaderProp.GradientColorCount, _gradient.colorKeys.Length);
			material.SetInt(ShaderProp.GradientAlphaCount, _gradient.alphaKeys.Length);
			material.SetVectorArray(ShaderProp.GradientColors, _colorKeys);
			material.SetVectorArray(ShaderProp.GradientAlphas, _alphaKeys);
			material.SetVector(ShaderProp.GradientTransform, new Vector4(_scale, _scalePivot, _offset, (float)_wrapMode));
			//material.SetVector(ShaderProp.GradientRadial, new Vector4(_centerX, _centerY, _radius, 0f));
			//material.SetFloat(ShaderProp.GradientDither, Mathf.Lerp(0f, 0.05f, _dither));

			// Mixing mode
			switch (_mixMode)
			{
				default:
				case GradientMix.Smooth:
				material.DisableKeyword(ShaderKeyword.GradientMixLinear);
				material.DisableKeyword(ShaderKeyword.GradientMixStep);
				material.EnableKeyword(ShaderKeyword.GradientMixSmooth);
				break;
				case GradientMix.Linear:
				material.DisableKeyword(ShaderKeyword.GradientMixStep);
				material.DisableKeyword(ShaderKeyword.GradientMixSmooth);
				material.EnableKeyword(ShaderKeyword.GradientMixLinear);
				break;
				case GradientMix.Step:
				material.DisableKeyword(ShaderKeyword.GradientMixSmooth);
				material.DisableKeyword(ShaderKeyword.GradientMixLinear);
				material.EnableKeyword(ShaderKeyword.GradientMixStep);
				break;
			}

			// Mixing color space
			switch (_colorSpace)
			{
				default:
				case GradientColorSpace.sRGB:
				material.DisableKeyword(ShaderKeyword.GradientColorSpaceLinear);
				material.DisableKeyword(ShaderKeyword.GradientColorSpacePerceptual);
				material.EnableKeyword(ShaderKeyword.GradientColorSpaceSRGB);
				break;
				case GradientColorSpace.Linear:
				material.DisableKeyword(ShaderKeyword.GradientColorSpaceSRGB);
				material.DisableKeyword(ShaderKeyword.GradientColorSpacePerceptual);
				material.EnableKeyword(ShaderKeyword.GradientColorSpaceLinear);
				break;
				case GradientColorSpace.Perceptual:
				material.DisableKeyword(ShaderKeyword.GradientColorSpaceSRGB);
				material.DisableKeyword(ShaderKeyword.GradientColorSpaceLinear);
				material.EnableKeyword(ShaderKeyword.GradientColorSpacePerceptual);
				break;
			}
		}
	}
#endif
}