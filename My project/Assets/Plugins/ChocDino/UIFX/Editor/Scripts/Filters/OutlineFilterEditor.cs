//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	/*
		internal class GradientShaderEditor
	{
		internal SerializedProperty _propGradient;
		internal SerializedProperty _propMixMode;
		internal SerializedProperty _propColorSpace;
		internal SerializedProperty _propDither;
		internal SerializedProperty _propScale;
		internal SerializedProperty _propScaleCenter;
		internal SerializedProperty _propOffset;
		internal SerializedProperty _propWrapMode;

		public GradientShaderEditor(SerializedProperty parent)
		{
			_propGradient = BaseEditor.VerifyFindPropertyRelative(parent, "_gradient");
			_propMixMode = BaseEditor.VerifyFindPropertyRelative(parent, "_mixMode");
			_propColorSpace = BaseEditor.VerifyFindPropertyRelative(parent, "_colorSpace");
			_propDither = BaseEditor.VerifyFindPropertyRelative(parent, "_dither");
			_propScale = BaseEditor.VerifyFindPropertyRelative(parent, "_scale");
			_propScaleCenter = BaseEditor.VerifyFindPropertyRelative(parent,"_scalePivot");
			_propOffset = BaseEditor.VerifyFindPropertyRelative(parent,"_offset");
			_propWrapMode = BaseEditor.VerifyFindPropertyRelative(parent,"_wrapMode");
		}
	}*/

	[CustomEditor(typeof(OutlineFilter), true)]
	[CanEditMultipleObjects]
	internal class OutlineFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Outline Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-outline-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("User Guide", "https://www.chocdino.com/products/uifx/outline-filter/about/"),
								new AboutButton("Scripting Guide", "https://www.chocdino.com/products/uifx/outline-filter/scripting/"),
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/outline-filter/components/outline-filter/"),
								new AboutButton("API Reference", "https://www.chocdino.com/products/uifx/outline-filter/API/ChocDino.UIFX/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Review <b>UIFX - Outline Filter</b>", "https://assetstore.unity.com/packages/slug/273578?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", AssetStoreBundleReviewUrl),
							}
						},
						new AboutSection("UIFX Support")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Discord Community", DiscordUrl),
								new AboutButton("Post to Unity Discussions", "https://discussions.unity.com/t/released-uifx-outline-filter/940939"),
								new AboutButton("Post Issues to GitHub", GithubUrl),
								new AboutButton("Email Us", SupportEmailUrl),
							}
						}
					}
				};

		private static readonly AboutToolbar s_aboutToolbar = new AboutToolbar(new AboutInfo[] { s_upgradeToBundle, s_aboutInfo } );

		private static readonly GUIContent Content_Distance = new GUIContent("Distance");
		private static readonly GUIContent Content_Shape = new GUIContent("Shape");
		private static readonly GUIContent Content_Outline = new GUIContent("Outline");

		//private GradientShaderEditor _gradientEditor;

		private SerializedProperty _propMethod;
		private SerializedProperty _propSize;
		//private SerializedProperty _propMaxSize;
		private SerializedProperty _propDirection;
		private SerializedProperty _propDistanceShape;
		private SerializedProperty _propBlur;
		private SerializedProperty _propSoftness;
		private SerializedProperty _propColor;
		//private SerializedProperty _propGradient;
		//private SerializedProperty _propTexture;
		//private SerializedProperty _propTextureOffset;
		//private SerializedProperty _propTextureScale;
		private SerializedProperty _propSourceAlpha;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;
		private SerializedProperty _propExpand;

		void OnEnable()
		{
			_propMethod = VerifyFindProperty("_method");
			_propSize = VerifyFindProperty("_size");
			//_propMaxSize = VerifyFindProperty("_maxSize");
			_propDistanceShape = VerifyFindProperty("_distanceShape");
			_propBlur = VerifyFindProperty("_blur");
			_propSoftness = VerifyFindProperty("_softness");
			_propColor = VerifyFindProperty("_color");
			//_propGradient = VerifyFindProperty("_gradient");
			//_propTexture = VerifyFindProperty("_texture");
			//_propTextureOffset = VerifyFindProperty("_textureOffset");
			//_propTextureScale = VerifyFindProperty("_textureScale");
			_propDirection = VerifyFindProperty("_direction");
			_propSourceAlpha = VerifyFindProperty("_sourceAlpha");
			_propStrength = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
			_propExpand = VerifyFindProperty("_expand");
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

			GUILayout.Label(Content_Distance, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbar(_propMethod);
			EnumAsToolbar(_propDistanceShape, Content_Shape);
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Outline, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EnumAsToolbar(_propDirection);
			EditorGUILayout.PropertyField(_propSize);
			if (_propMethod.enumValueIndex == (int)OutlineMethod.DistanceMap)
			{
				EditorGUILayout.PropertyField(_propSoftness);
				//EditorGUILayout.PropertyField(_propMaxSize);
			}
			else if (_propMethod.enumValueIndex == (int)OutlineMethod.Dilate)
			{
				EditorGUILayout.PropertyField(_propBlur);
			}
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Fill, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propColor);
			/*if (_propMethod.enumValueIndex == (int)OutlineMethod.DistanceMap)
			{
				
				//EditorGUILayout.PropertyField(_propGradient, true);
				{
					EditorGUILayout.PropertyField(_gradientEditor._propGradient);
					EditorGUI.indentLevel++;
					EnumAsToolbar(_gradientEditor._propMixMode);
					EnumAsToolbar(_gradientEditor._propColorSpace);
					EditorGUILayout.PropertyField(_gradientEditor._propScale);
					EditorGUI.indentLevel++;
					bool isReversed = _gradientEditor._propScale.floatValue < 0f;
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.Toggle(Content_Reverse, isReversed);
					if (EditorGUI.EndChangeCheck())
					{
						_gradientEditor._propScale.floatValue *= -1f;
					}
					EditorGUILayout.PropertyField(_gradientEditor._propScaleCenter);
					EditorGUI.indentLevel--;
					EditorGUILayout.PropertyField(_gradientEditor._propOffset);
					EnumAsToolbar(_gradientEditor._propWrapMode);
					EditorGUI.indentLevel--;
				}
			}
			TextureScaleOffset(_propTexture, _propTextureScale, _propTextureOffset, new GUIContent("Texture"));
			*/
			EditorGUI.indentLevel--;

			GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(_propSourceAlpha);
			EnumAsToolbarCompact(_propRenderSpace);
			EnumAsToolbarCompact(_propExpand);
			DrawStrengthProperty(_propStrength);
			EditorGUI.indentLevel--;

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}
			
			FilterBaseEditor.OnInspectorGUI_Debug(filter);

			serializedObject.ApplyModifiedProperties();
		}
	}
}