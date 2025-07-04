//--------------------------------------------------------------------------//
// Copyright 2023-2025 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEditor;

namespace ChocDino.UIFX.Editor
{
	[CustomEditor(typeof(FillTextureFilter), true)]
	[CanEditMultipleObjects]
	internal class FillTextureFilterEditor : FilterBaseEditor
	{
		private static readonly AboutInfo s_aboutInfo = 
				new AboutInfo(s_aboutHelp, "UIFX - Fill Texture Filter\n© Chocolate Dinosaur Ltd", "uifx-logo-fill-filter")
				{
					sections = new AboutSection[]
					{
						new AboutSection("Asset Guides")
						{
							buttons = new AboutButton[]
							{
								new AboutButton("Components Reference", "https://www.chocdino.com/products/uifx/bundle/components/fill-texture-filter/"),
							}
						},
						new AboutSection("Unity Asset Store Review\r\n<color=#ffd700>★★★★☆</color>")
						{
							buttons = new AboutButton[]
							{
								//new AboutButton("Review <b>UIFX - Fill Filter</b>", "https://assetstore.unity.com/packages/slug/274847?aid=1100lSvNe#reviews"),
								new AboutButton("Review <b>UIFX Bundle</b>", "https://assetstore.unity.com/packages/slug/266945?aid=1100lSvNe#reviews"),
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

		private static readonly GUIContent Content_Scale = new GUIContent("Scale");
		private static readonly GUIContent Content_Rotate = new GUIContent("Rotate");
		private static readonly GUIContent Content_Offset = new GUIContent("Offset");
		private static readonly GUIContent Content_FitMode = new GUIContent("Fit Mode");
		private static readonly GUIContent Content_WrapMode = new GUIContent("Wrap Mode");
		private static readonly GUIContent Content_Transform = new GUIContent("Transform");

		private SerializedProperty _propTexture;
		private SerializedProperty _propTextureScaleMode;
		private SerializedProperty _propTextureWrapMode;
		private SerializedProperty _propColor;
		private SerializedProperty _propTextureRotate;
		private SerializedProperty _propTextureScale;
		private SerializedProperty _propTextureOffset;
		private SerializedProperty _propScrollSpeed;
		//private SerializedProperty _propTextureScale;
		private SerializedProperty _propBlendMode;
		private SerializedProperty _propStrength;
		private SerializedProperty _propRenderSpace;

		public override bool RequiresConstantRepaint()
		{
			var fill = this.target as FillTextureFilter;
			if (!fill.isActiveAndEnabled) return false;
			if (fill.Strength <= 0f) return false;
			if (fill.IsPreviewScroll && fill.HasScrollSpeed())
			{
				EditorApplication.QueuePlayerLoopUpdate();
				return true;
			}
			return false;
		}

		void OnDisable()
		{
			var fill = this.target as FillTextureFilter;
			// NOTE: fill can be null if it has been destroyed
			if (fill != null)
			{
				fill.IsPreviewScroll = false;
				fill.ResetScroll();
			}
		}

		void OnEnable()
		{
			_propTexture = VerifyFindProperty("_texture");
			_propTextureScaleMode = VerifyFindProperty("_textureScaleMode");
			_propTextureWrapMode = VerifyFindProperty("_textureWrapMode");
			_propColor = VerifyFindProperty("_color");
			_propTextureRotate = VerifyFindProperty("_textureRotation");
			_propTextureScale = VerifyFindProperty("_textureScale");
			_propTextureOffset = VerifyFindProperty("_textureOffset");
			_propScrollSpeed = VerifyFindProperty("_scrollSpeed");
			//_propTextureScale = VerifyFindProperty("_textureScale");
			_propBlendMode = VerifyFindProperty("_blendMode");
			_propStrength  = VerifyFindProperty("_strength");
			_propRenderSpace = VerifyFindProperty("_renderSpace");
		}

		public override void OnInspectorGUI()
		{
			s_aboutToolbar.OnGUI();

			serializedObject.Update();

			var filter = this.target as FilterBase;
			var fill = this.target as FillTextureFilter;

			if (OnInspectorGUI_Check(filter))
			{
				return;
			}
			
			{
				GUILayout.Label(Content_Fill, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propTexture);
				EditorGUILayout.PropertyField(_propTextureScaleMode, Content_FitMode);
				EnumAsToolbar(_propTextureWrapMode, Content_WrapMode);
				EditorGUILayout.PropertyField(_propColor);
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Transform, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propTextureScale, Content_Scale);
				EditorGUILayout.PropertyField(_propTextureRotate, Content_Rotate);
				EditorGUILayout.PropertyField(_propTextureOffset, Content_Offset);
				{
					GUILayout.BeginHorizontal();
					EditorGUILayout.PropertyField(_propScrollSpeed);
					if (!EditorApplication.isPlaying)
					{
						if (ToggleButton(fill.IsPreviewScroll, Content_Stop, Content_Preview))
						{
							if (fill.IsPreviewScroll)
							{
								fill.ResetScroll();
							}
							fill.IsPreviewScroll = !fill.IsPreviewScroll;
							EditorApplication.QueuePlayerLoopUpdate();
						}
					}
					GUILayout.EndHorizontal();
				}
				EditorGUI.indentLevel--;
			}
			{
				GUILayout.Label(Content_Apply, EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propBlendMode);
				EnumAsToolbarCompact(_propRenderSpace);
				DrawStrengthProperty(_propStrength);
				EditorGUI.indentLevel--;
			}

			if (OnInspectorGUI_Baking(filter))
			{
				return;
			}

			FilterBaseEditor.OnInspectorGUI_Debug(this.target as FilterBase);

			serializedObject.ApplyModifiedProperties();
		}
	}
}