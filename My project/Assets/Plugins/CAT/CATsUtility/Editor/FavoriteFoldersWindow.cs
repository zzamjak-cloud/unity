using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    // JSON 저장용 데이터 클래스
    [System.Serializable]
    public class FavoriteFoldersJsonData
    {
        public List<string> folderGUIDs = new List<string>();
    }

    // 즐겨찾기 폴더를 관리하는 에디터 창
    public class FavoriteFoldersWindow : EditorWindow
    {
        private const string PREFS_KEY = "CAT_FavoriteFoldersData"; 
        private Color handleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private List<DefaultAsset> favoriteFolders = new List<DefaultAsset>();
        private Vector2 scrollPosition;
        private bool isDragging = false;
        private int dragSourceIndex = -1;
        private Rect dragRect;
        private bool showUIElements = false;

        private GUIStyle handleStyle;
        private GUIStyle editModeToggleStyle;
        
        private bool stylesInitialized = false;

        [MenuItem("CAT/Utility/Favorite")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteFoldersWindow>("Favorite");
        }

        private void OnEnable()
        {
            LoadFromPlayerPrefs(); // 변경
        }

        private void OnDisable()
        {
            SaveToPlayerPrefs(); // 변경
        }
        
        private void LoadFromPlayerPrefs()
        {
            favoriteFolders.Clear();
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                try
                {
                    string json = PlayerPrefs.GetString(PREFS_KEY);
                    var jsonData = JsonUtility.FromJson<FavoriteFoldersJsonData>(json);
                    if (jsonData != null && jsonData.folderGUIDs != null)
                    {
                        foreach (string guid in jsonData.folderGUIDs)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            if (!string.IsNullOrEmpty(path))
                            {
                                DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                                if (folder != null && AssetDatabase.IsValidFolder(path))
                                {
                                    favoriteFolders.Add(folder);
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Favorite 폴더 데이터 로드 실패: {e.Message}");
                    favoriteFolders.Clear();
                }
            }
        }
        
        private void SaveToPlayerPrefs()
        {
            try
            {
                var jsonData = new FavoriteFoldersJsonData
                {
                    folderGUIDs = favoriteFolders
                        .Where(f => f != null)
                        .Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f)))
                        .Where(guid => !string.IsNullOrEmpty(guid))
                        .ToList()
                };

                string json = JsonUtility.ToJson(jsonData, true);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Favorite 폴더 데이터 저장 실패 (PlayerPrefs): {e.Message}");
            }
        }

        // --- 이하 OnGUI 및 다른 로직들은 변경할 필요가 없습니다. ---

        private void OnGUI()
        {
            InitializeStyles();
            EditorGUILayout.BeginVertical();

            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawFolders();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            HandleDragVisuals();
            HandleWindowDragDrop();
        }
        
        private void HandleDragVisuals()
        {
            if (isDragging)
            {
                Event current = Event.current;
                Vector2 mousePos = current.mousePosition;

                float deltaY = mousePos.y - dragRect.y;
                Color indicatorColor = deltaY > 0 ? Color.green : Color.red;

                Rect indicator = new Rect(mousePos.x - 5, mousePos.y - 1, 10, 2);
                EditorGUI.DrawRect(indicator, indicatorColor);

                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            handleStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = handleColor },
                alignment = TextAnchor.MiddleCenter
            };

            editModeToggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 10 };

            stylesInitialized = true;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            float minEditWidth = 40f;
            string toggleLabel = position.width - 40f >= minEditWidth ? "Edit" : "E";
            float toggleWidth = position.width - 40f >= minEditWidth ? minEditWidth : 20f;

            bool newShowUIElements = GUILayout.Toggle(showUIElements, toggleLabel, editModeToggleStyle, GUILayout.Width(toggleWidth));
            if (newShowUIElements != showUIElements)
            {
                showUIElements = newShowUIElements;
                if (!showUIElements)
                {
                    SaveToPlayerPrefs();
                }
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        private void DrawFolders()
        {
            if (favoriteFolders == null) return;
            int folderToDelete = -1;

            for (int i = 0; i < favoriteFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (showUIElements)
                {
                    GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                    Rect folderDragHandleRect = GUILayoutUtility.GetLastRect();
                    HandleReordering(folderDragHandleRect, i);
                }

                GUIContent folderIcon = EditorGUIUtility.IconContent("Folder Icon");
                GUILayout.Label(folderIcon, GUILayout.Width(16), GUILayout.Height(16));

                DefaultAsset folder = favoriteFolders[i];
                if (folder != null)
                {
                    if (GUILayout.Button(folder.name, EditorStyles.label))
                    {
                        AssetDatabase.OpenAsset(folder);
                    }
                }
                else
                {
                    GUI.color = Color.gray;
                    GUILayout.Label("[Missing Folder]", EditorStyles.label);
                    GUI.color = Color.white;
                }

                GUILayout.FlexibleSpace();

                if (showUIElements)
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("×", GUILayout.Width(18), GUILayout.Height(18)))
                    {
                        folderToDelete = i;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (folderToDelete != -1)
            {
                favoriteFolders.RemoveAt(folderToDelete);
            }
        }

        private void HandleReordering(Rect handleRect, int folderIndex)
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.type)
            {
                case EventType.MouseDown when handleRect.Contains(current.mousePosition) && current.button == 0:
                    isDragging = true;
                    dragSourceIndex = folderIndex;
                    dragRect = handleRect;
                    GUIUtility.hotControl = controlID;
                    current.Use();
                    break;

                case EventType.MouseDrag when isDragging && GUIUtility.hotControl == controlID:
                    Repaint();
                    current.Use();
                    break;

                case EventType.MouseUp when isDragging && GUIUtility.hotControl == controlID:
                    PerformSimpleReordering(current.mousePosition);
                    isDragging = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    Repaint();
                    break;
            }
        }

        private void PerformSimpleReordering(Vector2 mousePos)
        {
            float deltaY = mousePos.y - dragRect.y;

            if (Mathf.Abs(deltaY) > 20)
            {
                int direction = deltaY > 0 ? 1 : -1;
                int targetIndex = dragSourceIndex + direction;
                if (targetIndex >= 0 && targetIndex < favoriteFolders.Count)
                {
                    var temp = favoriteFolders[dragSourceIndex];
                    favoriteFolders[dragSourceIndex] = favoriteFolders[targetIndex];
                    favoriteFolders[targetIndex] = temp;
                }
            }
        }

        private void HandleWindowDragDrop()
        {
            Event current = Event.current;
            
            if (!showUIElements) return;

            switch (current.type)
            {
                case EventType.DragUpdated:
                    bool isFolder = DragAndDrop.objectReferences.Any(obj => obj is DefaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)));
                    DragAndDrop.visualMode = isFolder ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    current.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    bool addedAny = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is DefaultAsset folder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                        {
                            if (!favoriteFolders.Contains(folder))
                            {
                                favoriteFolders.Add(folder);
                                addedAny = true;
                            }
                        }
                    }
                    if (addedAny)
                    {
                        Repaint();
                    }
                    current.Use();
                    break;
            }
        }
    }
}