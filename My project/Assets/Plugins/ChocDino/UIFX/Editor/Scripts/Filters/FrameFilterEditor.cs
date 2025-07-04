//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(FrameFilter), true)]
	[CanEditMultipleObjects]
	internal class FrameFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Frame Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-frame-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/frame-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/frame-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/frame-filter/components/blur-filter/"),
								new AboutButton("API Reference", "https://www.chocdino.com/products/uifx/frame-filter/API/ChocDino.UIFX/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX - Frame Filter</b>", "https://assetstore.unity.com/packages/slug/266945?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", ForumBundleUrl),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Shape = new GUIContent("Shape");
		private static readonly GUIContent Content_Radius = new GUIContent("Radius");
		private static readonly GUIContent Content_Padding = new GUIContent("Padding");
		private static readonly GUIContent Content_PadToEdge = new GUIContent("Pad To Edge");
		private static readonly GUIContent Content_RoundCorners = new GUIContent("Round Corners");
		private static readonly GUIContent Content_Percent = new GUIContent("Percent");
		private static readonly GUIContent Content_Pixels = new GUIContent("Pixels");
		private static readonly GUIContent Content_Softness = new GUIContent("Softness");
		private static readonly GUIContent Content_GradientShape = new GUIContent("Gradient Shape");
		private static readonly GUIContent Content_FillMode = new GUIContent("Fill Mode");

		private SerializedProperty _propColor;
		private SerializedProperty _propSoftness;
		private SerializedProperty _propFillMode;
		private SerializedProperty _propTexture;
		private SerializedProperty _propGradient;
		private SerializedProperty _propGradientShape;
		private SerializedProperty _propGradientRadialRadius;
		//private SerializedProperty _propTextureScaleMode;
		//private SerializedProperty _propTextureScale;
		//private SerializedProperty _propSprite;
		private SerializedProperty _propShape;
		private SerializedProperty _propRectPadding;
		private SerializedProperty _propRadiusPadding;
		private SerializedProperty _propRectToEdge;
		private SerializedProperty _propRectRoundCornerMode;
		private SerializedProperty _rectRoundCornersValue;
		private SerializedProperty _propRectRoundCorners;
		private SerializedProperty _propCutoutSource;
		private SerializedProperty _propBorderColor;
		private SerializedProperty _propBorderFillMode;
		private SerializedProperty _propBorderTexture;
		private SerializedProperty _propBorderGradient;
		private SerializedProperty _propBorderGradientShape;
		private SerializedProperty _propBorderGradientRadialRadius;
		private SerializedProperty _propBorderSize;
		private SerializedProperty _propBorderSoftness;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;
		private SerializedProperty _propSourceArea;

		protected virtual void OnEnable()
		{
			_propColor = VerifyFindProperty("_color");
			_propFillMode = VerifyFindProperty("_fillMode");
			_propTexture = VerifyFindProperty("_texture");
			_propGradient = VerifyFindProperty("_gradient");
			_propGradientShape = VerifyFindProperty("_gradientShape");
			_propGradientRadialRadius = VerifyFindProperty("_gradientRadialRadius");
			//_propTextureScaleMode = VerifyFindProperty("_textureScaleMode");
			//_propTextureScale = VerifyFindProperty("_textureScale");
			//_propSprite = VerifyFindProperty("_sprite");

			_propSoftness = VerifyFindProperty("_softness");
			_propShape = VerifyFindProperty("_shape");
			_propRectPadding = VerifyFindProperty("_rectPadding");
			_propRadiusPadding = VerifyFindProperty("_radiusPadding");
			_propRectToEdge = VerifyFindProperty("_rectToEdge");
			_propRectRoundCornerMode = VerifyFindProperty("_rectRoundCornerMode");
			_rectRoundCornersValue = VerifyFindProperty("_rectRoundCornersValue");
			_propRectRoundCorners = VerifyFindProperty("_rectRoundCorners");
			_propCutoutSource = VerifyFindProperty("_cutoutSource");

			_propBorderColor = VerifyFindProperty("_borderColor");
			_propBorderFillMode = VerifyFindProperty("_borderFillMode");
			_propBorderTexture = VerifyFindProperty("_borderTexture");
			_propBorderGradient = VerifyFindProperty("_borderGradient");
			_propBorderGradientShape = VerifyFindProperty("_borderGradientShape");
			_propBorderGradientRadialRadius = VerifyFindProperty("_borderGradientRadialRadius");
			_propBorderSize = VerifyFindProperty("_borderSize");
			_propBorderSoftness = VerifyFindProperty("_borderSoftness");

			_propStrength = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propExpand = VerifyFindProperty("_expand");
			_propSourceArea = VerifyFindProperty("_sourceArea");
		}

		private static void Slider(SerializedProperty prop, float min, float max, GUIContent label = null)
		{
			EditorGUI.BeginChangeCheck();
			if (label == null)
			{
				label = new GUIContent(prop.displayName);
			}
			float newValue = EditorGUILayout.Slider(label, prop.floatValue, min, max);
			if (EditorGUI.EndChangeCheck())
			{
				prop.floatValue = newValue;
			}
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			var filter = this.target as FilterBase;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}

			GUILayout.Label(Content_Shape, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbar(_propShape);
			if (_propShape.enumValueIndex == (int)FrameShape.Rectangle)
			{
				EditorGUILayout.PropertyField(_propRectPadding, Content_Padding);
				EditorGUILayout.PropertyField(_propRectToEdge, Content_PadToEdge);
			}
			else
			{
				EditorGUILayout.PropertyField(_propRadiusPadding, Content_Padding);
			}

			if (_propShape.enumValueIndex != (int)FrameShape.Circle)
			{
				EditorGUILayout.PropertyField(_propRectRoundCornerMode, Content_RoundCorners);

				switch (_propRectRoundCornerMode.enumValueIndex)
				{
					case (int)FrameRoundCornerMode.Percent:
					EditorGUI.indentLevel++;
					Slider(_rectRoundCornersValue, 0f, 1f, Content_Percent);
					EditorGUI.indentLevel--;
					break;
					case (int)FrameRoundCornerMode.Pixels:
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(_rectRoundCornersValue, Content_Pixels);
					EditorGUI.indentLevel--;
					break;
					case (int)FrameRoundCornerMode.CustomPercent:
					EditorGUI.indentLevel++;
					Slider(_propRectRoundCorners.FindPropertyRelative("topLeft"), 0f, 1f);
					Slider(_propRectRoundCorners.FindPropertyRelative("topRight"), 0f, 1f);
					Slider(_propRectRoundCorners.FindPropertyRelative("bottomLeft"), 0f, 1f);
					Slider(_propRectRoundCorners.FindPropertyRelative("bottomRight"), 0f, 1f);
					EditorGUI.indentLevel--;
					break;
					case (int)FrameRoundCornerMode.CustomPixels:
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(_propRectRoundCorners.FindPropertyRelative("topLeft"));
					EditorGUILayout.PropertyField(_propRectRoundCorners.FindPropertyRelative("topRight"));
					EditorGUILayout.PropertyField(_propRectRoundCorners.FindPropertyRelative("bottomLeft"));
					EditorGUILayout.PropertyField(_propRectRoundCorners.FindPropertyRelative("bottomRight"));
					EditorGUI.indentLevel--;
					break;
				}
			}

			EditorGUI.indentLevel--;

			{
				GUILayout.Label(Content_Fill, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbar(_propFillMode);
				switch ((FrameFillMode)_propFillMode.enumValueIndex)
				{
					case FrameFillMode.Color:
					EditorGUILayout.PropertyField(_propColor);
					break;
					case FrameFillMode.Texture:
					EditorGUILayout.PropertyField(_propTexture);
					break;
					case FrameFillMode.Gradient:
					EditorGUILayout.PropertyField(_propGradient);
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel(Content_Space);
					if (GUILayout.Button(Content_Reverse))
					{
						var g = GetGradient(_propGradient);
						GradientUtils.Reverse(g);
						SetGradient(_propGradient, g);
					}
					EditorGUILayout.EndHorizontal();
					EnumAsToolbar(_propGradientShape);
					if ((FrameGradientShape)_propGradientShape.enumValueIndex == FrameGradientShape.Radial)
					{
						EditorGUILayout.PropertyField(_propGradientRadialRadius, Content_Radius);
					}
					break;
				}
				//EditorGUILayout.PropertyField(_propTextureScaleMode);
				//EditorGUILayout.PropertyField(_propTextureScale);
				//EditorGUILayout.PropertyField(_propSprite);
				EditorGUILayout.PropertyField(_propSoftness, Content_Softness);
				EditorGUI.indentLevel--;
			}

			{
				GUILayout.Label(Content_Border, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EnumAsToolbar(_propBorderFillMode, Content_FillMode);
				switch ((FrameFillMode)_propBorderFillMode.enumValueIndex)
				{
					case FrameFillMode.Color:
					EditorGUILayout.PropertyField(_propBorderColor, Content_Color);
					break;
					case FrameFillMode.Texture:
					EditorGUILayout.PropertyField(_propBorderTexture, Content_Texture);
					break;
					case FrameFillMode.Gradient:
					EditorGUILayout.PropertyField(_propBorderGradient, Content_Gradient);
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel(Content_Space);
					if (GUILayout.Button(Content_Reverse))
					{
						var g = GetGradient(_propBorderGradient);
						GradientUtils.Reverse(g);
						SetGradient(_propBorderGradient, g);
					}
					EditorGUILayout.EndHorizontal();
					EnumAsToolbar(_propBorderGradientShape, Content_GradientShape);
					if ((FrameGradientShape)_propBorderGradientShape.enumValueIndex == FrameGradientShape.Radial)
					{
						EditorGUILayout.PropertyField(_propBorderGradientRadialRadius, Content_Radius);
					}
					break;
				}
				if ((FrameFillMode)_propBorderFillMode.enumValueIndex != FrameFillMode.None)
				{
					EditorGUILayout.PropertyField(_propBorderSize, Content_Size);
					EditorGUILayout.PropertyField(_propBorderSoftness, Content_Softness);
				}
				
				EditorGUI.indentLevel--;
			}

			{
				GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propCutoutSource);
				EnumAsToolbarCompact(_propRenderSpace);
				EnumAsToolbarCompact(_propSourceArea);
				EnumAsToolbarCompact(_propExpand);
				DrawStrengthProperty(_propStrength);
				EditorGUI.indentLevel--;
			}

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(filter);

			serializedObject.ApplyModifiedProperties();
		}
	}
}