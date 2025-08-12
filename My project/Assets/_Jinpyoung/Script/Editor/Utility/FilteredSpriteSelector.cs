using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

namespace CAT.Utility
{
    public class FilteredSpriteSelector : EditorWindow
    {
        private class FolderNode
        {
            public string name;
            public string path;
            public bool isFoldedOut = true;
            public List<FolderNode> children = new List<FolderNode>();
        }

        private Action<Sprite> onSpriteSelectedCallback;
        private FolderNode rootNode;
        private string selectedFolderPath;

        private List<Sprite> spritesInSelectedFolder = new List<Sprite>();
        private GUIStyle buttonStyle;
        private GUIStyle selectedFolderStyle;
        private GUIStyle folderLabelStyle;
        private GUIStyle foldoutButtonStyle;

        private Vector2 leftPaneScroll;
        private Vector2 rightPaneScroll;
        private string searchString = "";

        public static void ShowWindow(string rootPath, Action<Sprite> onSpriteSelected)
        {
            FilteredSpriteSelector window = GetWindow<FilteredSpriteSelector>(true, "Filtered Sprite Selector");
            window.onSpriteSelectedCallback = onSpriteSelected;
            window.Initialize(rootPath);
            window.minSize = new Vector2(500, 300);
        }

        private void Initialize(string rootPath)
        {
            rootNode = new FolderNode
            {
                name = Path.GetFileName(rootPath),
                path = rootPath
            };
            BuildFolderTree(rootNode);
            SelectFolder(rootPath);
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

            folderLabelStyle = new GUIStyle(EditorStyles.label)
            {
                padding = { left = 2 },
                alignment = TextAnchor.MiddleLeft
            };

            selectedFolderStyle = new GUIStyle(folderLabelStyle);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0.24f, 0.5f, 0.87f));
            texture.Apply();
            selectedFolderStyle.normal.background = texture;
            selectedFolderStyle.normal.textColor = Color.white;

            foldoutButtonStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void BuildFolderTree(FolderNode parentNode)
        {
            string[] subFolders = AssetDatabase.GetSubFolders(parentNode.path);
            foreach (var folderPath in subFolders)
            {
                var childNode = new FolderNode
                {
                    name = Path.GetFileName(folderPath),
                    path = folderPath
                };
                parentNode.children.Add(childNode);
                BuildFolderTree(childNode);
            }
        }

        private void OnGUI()
        {
            if (rootNode == null) return;
            if (buttonStyle == null) InitializeStyles();

            EditorGUILayout.BeginHorizontal();
            DrawFolderPane();
            DrawSpritePane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFolderPane()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(180), GUILayout.ExpandHeight(true));
            leftPaneScroll = EditorGUILayout.BeginScrollView(leftPaneScroll);

            DrawFolderNode(rootNode, 0);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFolderNode(FolderNode node, int indent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 15f);

            GUIStyle style = (node.path == selectedFolderPath) ? selectedFolderStyle : folderLabelStyle;

            if (node.children.Any())
            {
                string foldoutChar = node.isFoldedOut ? "−" : "+";
                if (GUILayout.Button(foldoutChar, foldoutButtonStyle, GUILayout.Width(20)))
                {
                    node.isFoldedOut = !node.isFoldedOut;
                }
            }
            else
            {
                GUILayout.Space(24f);
            }

            if (GUILayout.Button(node.name, style))
            {
                SelectFolder(node.path);
            }

            EditorGUILayout.EndHorizontal();

            if (node.isFoldedOut && node.children.Any())
            {
                foreach (var child in node.children)
                {
                    DrawFolderNode(child, indent + 1);
                }
            }
        }

        private void DrawSpritePane()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchString = GUILayout.TextField(searchString, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchString = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            rightPaneScroll = EditorGUILayout.BeginScrollView(rightPaneScroll);

            var filteredSprites = string.IsNullOrEmpty(searchString)
                ? spritesInSelectedFolder
                : spritesInSelectedFolder.Where(s => s.name.ToLower().Contains(searchString.ToLower())).ToList();

            if (filteredSprites.Count == 0)
            {
                EditorGUILayout.HelpBox("이 폴더에 스프라이트가 없거나, 검색 결과가 없습니다.", MessageType.Info);
            }

            int columns = Mathf.FloorToInt((position.width - 180) / (buttonStyle.fixedWidth + 10));
            columns = Mathf.Max(1, columns);

            for (int i = 0; i < filteredSprites.Count; i++)
            {
                if (i % columns == 0) GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                var sprite = filteredSprites[i];
                var content = new GUIContent(sprite.name, AssetPreview.GetAssetPreview(sprite));

                if (GUILayout.Button(content, buttonStyle))
                {
                    onSpriteSelectedCallback?.Invoke(sprite);
                    Close();
                }

                if (i % columns == columns - 1 || i == filteredSprites.Count - 1)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void SelectFolder(string path)
        {
            if (selectedFolderPath == path) return;
            selectedFolderPath = path;
            LoadSpritesForFolder(path);
            Repaint();
        }

        // --- 수정된 부분: 스프라이트 로딩 로직 변경 ---
        private void LoadSpritesForFolder(string folderPath)
        {
            spritesInSelectedFolder.Clear();

            // 검색할 이미지 파일 확장자 목록
            var validExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd" };

            // System.IO를 사용해 '현재 폴더에서만' 파일 목록을 가져옵니다. (하위 폴더는 검색 안 함)
            string[] filePaths = Directory.GetFiles(folderPath);

            foreach (string filePath in filePaths)
            {
                // 가져온 파일이 이미지 확장자를 가졌는지 확인
                if (validExtensions.Contains(Path.GetExtension(filePath).ToLower()))
                {
                    // 해당 경로의 파일을 스프라이트로 로드 시도
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
                    if (sprite != null)
                    {
                        spritesInSelectedFolder.Add(sprite);
                    }
                }
            }

            // 이름순으로 정렬
            spritesInSelectedFolder = spritesInSelectedFolder.OrderBy(s => s.name).ToList();
        }
    }
}