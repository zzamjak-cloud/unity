using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CAT.Utility
{
    // 필터링된 스프라이트 선택기 에디터 윈도우
    public class FilteredSpriteSelector : EditorWindow
    {
        private List<string> folderPaths;
        private List<Sprite> spritesInFolders;
        private Action<Sprite> onSpriteSelectedCallback;
        private Vector2 scrollPosition;
        private string searchString = "";
        private GUIStyle buttonStyle;

        public static void ShowWindow(List<string> paths, Action<Sprite> onSpriteSelected)
        {
            FilteredSpriteSelector window = GetWindow<FilteredSpriteSelector>(true, "Filtered Sprite Selector");
            window.folderPaths = paths;
            window.onSpriteSelectedCallback = onSpriteSelected;
            window.LoadSprites();
            window.minSize = new Vector2(300, 200);
        }

        private void InitializeStyles()
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                imagePosition = ImagePosition.ImageAbove,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 110,
                fixedWidth = 90,
                wordWrap = true,
                fontSize = 10
            };
        }

        private void LoadSprites()
        {
            spritesInFolders = new List<Sprite>();
            if (folderPaths == null || folderPaths.Count == 0) return;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", folderPaths.ToArray());
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    spritesInFolders.Add(sprite);
                }
            }
            spritesInFolders = spritesInFolders.OrderBy(s => s.name).ToList();
        }

        private void OnGUI()
        {
            if (onSpriteSelectedCallback == null)
            {
                EditorGUILayout.LabelField("초기화 오류. 창을 닫아주세요.");
                return;
            }
            if (buttonStyle == null) InitializeStyles();

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchString = GUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            List<Sprite> filteredSprites = string.IsNullOrEmpty(searchString)
                ? spritesInFolders
                : spritesInFolders.Where(s => s.name.ToLower().Contains(searchString.ToLower())).ToList();

            if (filteredSprites.Count == 0) EditorGUILayout.HelpBox("일치하는 스프라이트가 없습니다.", MessageType.Info);

            int columns = Mathf.FloorToInt(position.width / (buttonStyle.fixedWidth + 5));
            columns = Mathf.Max(1, columns);

            for (int i = 0; i < filteredSprites.Count; i++)
            {
                if (i % columns == 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                }

                Sprite sprite = filteredSprites[i];
                GUIContent content = new GUIContent(sprite.name, AssetPreview.GetAssetPreview(sprite));

                if (GUILayout.Button(content, buttonStyle))
                {
                    onSpriteSelectedCallback?.Invoke(sprite);
                    this.Close();
                }

                if (i % columns == columns - 1 || i == filteredSprites.Count - 1)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}