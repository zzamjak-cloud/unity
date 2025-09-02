using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

namespace CAT.Utility
{
    // ===========================================
    // Component Editors
    // ===========================================

    // Image용 커스텀 에디터
    [CustomEditor(typeof(Image), true)]
    [CanEditMultipleObjects]
    public class FilteredImageEditor : UnityEditor.UI.ImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // RawImage용 커스텀 에디터
    [CustomEditor(typeof(RawImage), true)]
    [CanEditMultipleObjects]
    public class FilteredRawImageEditor : UnityEditor.UI.RawImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Texture").objectReferenceValue = sprite != null ? sprite.texture : null;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // SpriteRenderer용 커스텀 에디터
    [CustomEditor(typeof(SpriteRenderer), true)]
    [CanEditMultipleObjects]
    public class FilteredSpriteRendererEditor : Editor
    {
        private FilteredSpriteFinderDrawer drawer;
        private Editor defaultEditor;

        private void OnEnable()
        {
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();

            var targets = serializedObject.targetObjects;
            var editorType = Type.GetType("UnityEditor.SpriteRendererEditor, UnityEditor");
            defaultEditor = CreateEditor(targets, editorType);
        }

        private void OnDisable()
        {
            if (defaultEditor != null)
            {
                DestroyImmediate(defaultEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();

            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // ===========================================
    // Filtered Sprite Finder Drawer
    // ===========================================

    public class FilteredSpriteFinderDrawer
    {
        private const string FOLDERS_JSON_PATH = "ProjectSettings/FilteredSpriteFolders.json";
        private const string FOLDOUT_STATE_KEY = "FilteredSpriteFinder_Foldout";

        // 폴더 GUID 저장용 클래스
        [Serializable]
        private class FolderData
        {
            public List<string> folderGUIDs = new List<string>();
        }

        private static List<DefaultAsset> searchFolders = new List<DefaultAsset>();
        private static bool isInitialized = false;
        private static bool isFoldedOut = true;

        // 초기화 메서드
        public void Initialize()
        {
            if (!isInitialized)
            {
                LoadFoldersFromJson();
                isFoldedOut = EditorPrefs.GetBool(FOLDOUT_STATE_KEY, true);
                isInitialized = true;
            }
        }

        // 인스펙터 GUI 그리기
        public void DrawInspectorGUI(Action<Sprite> onSpriteSelectedAction)
        {
            EditorGUILayout.Space();

            // 드래그 앤 드롭 영역을 감싸는 Rect
            Rect dropRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));

            bool newFoldoutState = EditorGUI.Foldout(dropRect, isFoldedOut, "Sprite Folder Filter", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != isFoldedOut)
            {
                isFoldedOut = newFoldoutState;
                EditorPrefs.SetBool(FOLDOUT_STATE_KEY, isFoldedOut);
            }

            // 드래그 앤 드롭 처리
            HandleDragAndDrop(dropRect);

            if (isFoldedOut)
            {
                EditorGUI.indentLevel++;

                // 등록된 폴더 목록 정리
                if (searchFolders.RemoveAll(f => f == null) > 0)
                {
                    SaveFoldersToJson();
                }

                // 등록된 폴더들 표시
                for (int i = 0; i < searchFolders.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(searchFolders[i], typeof(DefaultAsset), false);

                    if (GUILayout.Button("Find", GUILayout.Width(50)))
                    {
                        string path = AssetDatabase.GetAssetPath(searchFolders[i]);
                        FilteredSpriteSelector.ShowWindow(path, onSpriteSelectedAction);
                    }

                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        searchFolders.RemoveAt(i);
                        SaveFoldersToJson();
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        // 드래그 앤 드롭 처리 메서드
        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (dropRect.Contains(evt.mousePosition))
                    {
                        bool canAcceptDrag = false;
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is DefaultAsset)
                            {
                                string path = AssetDatabase.GetAssetPath(draggedObject);
                                if (AssetDatabase.IsValidFolder(path))
                                {
                                    canAcceptDrag = true;
                                    break;
                                }
                            }
                        }

                        if (canAcceptDrag)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        }
                        else
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                        }
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (dropRect.Contains(evt.mousePosition))
                    {
                        bool addedAny = false;
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is DefaultAsset folder)
                            {
                                string path = AssetDatabase.GetAssetPath(folder);
                                if (AssetDatabase.IsValidFolder(path) && !searchFolders.Contains(folder))
                                {
                                    searchFolders.Add(folder);
                                    addedAny = true;
                                }
                            }
                        }

                        if (addedAny)
                        {
                            SaveFoldersToJson();
                        }

                        DragAndDrop.AcceptDrag();
                        evt.Use();
                    }
                    break;
            }
        }

        // 폴더 목록을 JSON으로 저장
        private void SaveFoldersToJson()
        {
            FolderData data = new FolderData();
            data.folderGUIDs = searchFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f)))
                .ToList();

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FOLDERS_JSON_PATH, json);
            AssetDatabase.Refresh();
        }

        // JSON에서 폴더 목록 로드
        private void LoadFoldersFromJson()
        {
            searchFolders.Clear();
            if (File.Exists(FOLDERS_JSON_PATH))
            {
                string json = File.ReadAllText(FOLDERS_JSON_PATH);
                FolderData data = JsonUtility.FromJson<FolderData>(json);

                if (data != null)
                {
                    foreach (var guid in data.folderGUIDs)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                            if (folder != null)
                            {
                                searchFolders.Add(folder);
                            }
                        }
                    }
                }
            }
        }
    }

    // ===========================================
    // Filtered Sprite Selector Window
    // ===========================================

    public class FilteredSpriteSelector : EditorWindow
    {
        private class FolderNode
        {
            public string name;
            public string path;
            public List<FolderNode> children = new List<FolderNode>();
            public bool isFoldedOut = true;
        }

        private static Action<Sprite> onSpriteSelectedCallback;
        private Vector2 leftPaneScroll;
        private Vector2 rightPaneScroll;
        private string selectedFolderPath;
        private string rootFolderPath; // 루트 폴더 경로 저장
        private string searchString = "";
        private List<Sprite> spritesInSelectedFolder = new List<Sprite>();
        private List<FolderNode> folderNodes = new List<FolderNode>();

        // 윈도우 표시 메서드
        public static void ShowWindow(string initialPath, Action<Sprite> onSpriteSelected)
        {
            onSpriteSelectedCallback = onSpriteSelected;
            var window = GetWindow<FilteredSpriteSelector>("스프라이트 선택기");
            window.rootFolderPath = initialPath; // 루트 폴더 경로 설정
            window.selectedFolderPath = initialPath;
            window.BuildFolderTree();
            window.LoadSpritesForFolder(initialPath);
            window.Show();
        }

        // GUI 메서드
        private void OnGUI()
        {
            DrawPanes();
        }

        private void DrawPanes()
        {
            EditorGUILayout.BeginHorizontal();

            // 왼쪽 폴더 패널
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(200), GUILayout.ExpandHeight(true));
            //EditorGUILayout.LabelField("폴더", EditorStyles.boldLabel);
            leftPaneScroll = EditorGUILayout.BeginScrollView(leftPaneScroll);
            DrawFolderTree();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 오른쪽 스프라이트 패널
            DrawSpritePane();

            EditorGUILayout.EndHorizontal();
        }

        // 폴더 트리 빌드 메서드
        private void BuildFolderTree()
        {
            folderNodes.Clear();
            if (string.IsNullOrEmpty(rootFolderPath)) return;

            // 루트 폴더 기준으로 고정된 트리 구조 생성
            string[] folders = AssetDatabase.GetSubFolders(rootFolderPath);
            foreach (string folder in folders)
            {
                var node = new FolderNode
                {
                    name = Path.GetFileName(folder),
                    path = folder
                };
                BuildSubFolders(node);
                folderNodes.Add(node);
            }
        }

        // 재귀적으로 하위 폴더 빌드
        private void BuildSubFolders(FolderNode node)
        {
            string[] subFolders = AssetDatabase.GetSubFolders(node.path);
            foreach (string subFolder in subFolders)
            {
                var childNode = new FolderNode
                {
                    name = Path.GetFileName(subFolder),
                    path = subFolder
                };
                BuildSubFolders(childNode);
                node.children.Add(childNode);
            }
        }

        // 폴더 트리 그리기
        private void DrawFolderTree()
        {
            var rootStyle = new GUIStyle(EditorStyles.label);
            rootStyle.fontStyle = FontStyle.Bold;

            // 루트 폴더가 선택되었는지 확인
            var rootSelectedStyle = (selectedFolderPath == rootFolderPath)
                ? new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } }
                : rootStyle;

            if (GUILayout.Button(Path.GetFileName(rootFolderPath), rootSelectedStyle))
            {
                SelectFolder(rootFolderPath);
            }

            foreach (var node in folderNodes)
            {
                DrawFolderNode(node, 1);
            }
        }

        // 재귀적으로 폴더 노드 그리기
        private void DrawFolderNode(FolderNode node, int indent)
        {
            var foldoutButtonStyle = new GUIStyle(EditorStyles.miniButton);
            foldoutButtonStyle.fixedWidth = 20;
            foldoutButtonStyle.fixedHeight = 16;
            foldoutButtonStyle.fontSize = 10;

            var folderLabelStyle = new GUIStyle(EditorStyles.label);
            var selectedFolderStyle = new GUIStyle(EditorStyles.boldLabel);

            // 선택된 폴더는 색상을 다르게 표시
            if (selectedFolderPath == node.path)
            {
                selectedFolderStyle.normal.textColor = Color.cyan;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 15);

            var style = (selectedFolderPath == node.path)
                ? selectedFolderStyle : folderLabelStyle;

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

        // 스프라이트 패널 그리기
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

            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fixedWidth = 120;
            buttonStyle.fixedHeight = 120;
            buttonStyle.imagePosition = ImagePosition.ImageAbove;

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

        // 폴더 선택 메서드
        private void SelectFolder(string path)
        {
            if (selectedFolderPath == path) return;
            selectedFolderPath = path;
            LoadSpritesForFolder(path);
            // 폴더 트리는 다시 빌드하지 않음 - 고정된 구조 유지
            Repaint();
        }

        // 선택된 폴더에서 스프라이트 로드 메서드
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