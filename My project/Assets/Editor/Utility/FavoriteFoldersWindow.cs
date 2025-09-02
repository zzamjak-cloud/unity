using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO; // 이제 필요 없지만, 다른 용도로 쓸 수 있으니 남겨둘 수 있습니다.
using System.Linq;

namespace CAT.Utility
{
    // JSON 저장용 데이터 클래스 (구조는 동일)
    [System.Serializable]
    public class FavoriteFoldersJsonData
    {
        public List<FavoriteCategoryData> categories = new List<FavoriteCategoryData>();
    }
    
    // JSON 저장용 폴더 카테고리 데이터 클래스 (구조는 동일)
    [System.Serializable]
    public class FavoriteCategoryData
    {
        public string name;
        public bool isExpanded = true;
        public List<string> folderGUIDs = new List<string>();
    }

    // Editor 실행중에 사용할 폴더 카테고리 클래스 (구조는 동일)
    public class FavoriteCategory
    {
        public string name;
        public bool isExpanded = true;
        public List<DefaultAsset> folders = new List<DefaultAsset>();

        public FavoriteCategory(string name)
        {
            this.name = name;
        }

        public static FavoriteCategory FromJsonData(FavoriteCategoryData jsonData)
        {
            var category = new FavoriteCategory(jsonData.name);
            category.isExpanded = jsonData.isExpanded;
            foreach (string guid in jsonData.folderGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                    if (folder != null && AssetDatabase.IsValidFolder(path))
                    {
                        category.folders.Add(folder);
                    }
                }
            }
            return category;
        }

        public FavoriteCategoryData ToJsonData()
        {
            var jsonData = new FavoriteCategoryData();
            jsonData.name = this.name;
            jsonData.isExpanded = this.isExpanded;
            jsonData.folderGUIDs = folders
                .Where(f => f != null)
                .Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f)))
                .Where(guid => !string.IsNullOrEmpty(guid))
                .ToList();
            return jsonData;
        }
    }

    // 즐겨찾기 폴더를 관리하는 에디터 창
    public class FavoriteFoldersWindow : EditorWindow
    {
        // 변경: JSON 파일 경로 대신 PlayerPrefs 키를 사용합니다.
        private const string PREFS_KEY = "CAT_FavoriteFoldersData"; 
        private Color handleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private List<FavoriteCategory> categories = new List<FavoriteCategory>();
        private Vector2 scrollPosition;
        private bool isDragging = false;
        private int dragSourceCategoryIndex = -1;
        private int dragSourceFolderIndex = -1;
        private Rect dragRect;
        private bool showUIElements = false;

        private GUIStyle handleStyle;
        private GUIStyle editModeToggleStyle;
        private GUIStyle categoryNameStyle;
        
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
        
        // 변경: LoadFromJson -> LoadFromPlayerPrefs
        private void LoadFromPlayerPrefs()
        {
            categories.Clear();
            if (PlayerPrefs.HasKey(PREFS_KEY))
            {
                try
                {
                    string json = PlayerPrefs.GetString(PREFS_KEY);
                    var jsonData = JsonUtility.FromJson<FavoriteFoldersJsonData>(json);
                    if (jsonData != null && jsonData.categories != null)
                    {
                        categories = jsonData.categories.Select(FavoriteCategory.FromJsonData).ToList();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Favorite 폴더 데이터 로드 실패: {e.Message}");
                    categories.Clear();
                }
            }
        }
        
        // 변경: SaveToJson -> SaveToPlayerPrefs
        private void SaveToPlayerPrefs()
        {
            try
            {
                var jsonData = new FavoriteFoldersJsonData
                {
                    categories = categories.Select(c => c.ToJsonData()).ToList()
                };

                string json = JsonUtility.ToJson(jsonData, true);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
                // Debug.Log($"Favorite 폴더 데이터 저장 완료 (PlayerPrefs) - 카테고리 수: {categories.Count}");
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
            DrawCategories();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            HandleDragVisuals();
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
            
            categoryNameStyle = new GUIStyle(EditorStyles.textField)
            {
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            if (showUIElements)
            {
                if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(35)))
                {
                    AddNewCategory();
                }
            }

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
                    SaveToPlayerPrefs(); // 변경: 편집 모드 종료 시 저장
                }
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        private void AddNewCategory()
        {
            string newName = "New Category " + (categories.Count + 1);
            categories.Add(new FavoriteCategory(newName));
        }

        private void DrawCategories()
        {
            if (categories == null) return;
            int categoryToDelete = -1;

            for (int i = 0; i < categories.Count; i++)
            {
                if (DrawCategory(categories[i], i, out int tempDelete))
                {
                    categoryToDelete = tempDelete;
                    break; 
                }
            }

            if (categoryToDelete != -1)
            {
                categories.RemoveAt(categoryToDelete);
                Repaint();
            }
        }

        private bool DrawCategory(FavoriteCategory category, int categoryIndex, out int categoryToDelete)
        {
            categoryToDelete = -1;
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            if (showUIElements)
            {
                GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                Rect categoryDragHandleRect = GUILayoutUtility.GetLastRect();
                HandleReordering(categoryDragHandleRect, categoryIndex, -1);

                string newName = EditorGUILayout.TextField(category.name, categoryNameStyle);
                if (newName != category.name)
                {
                    category.name = newName;
                }
            }
            else
            {
                category.isExpanded = EditorGUILayout.Foldout(category.isExpanded, category.name, true);
            }

            GUILayout.FlexibleSpace();

            if (showUIElements)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog("Delete Category", $"Delete '{category.name}'?", "Yes", "No"))
                    {
                        categoryToDelete = categoryIndex;
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        return true;
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            if (category.isExpanded || showUIElements)
            {
                EditorGUILayout.BeginVertical();
                
                DrawFolders(category, categoryIndex);
                
                GUILayout.Space(10);
                
                EditorGUILayout.EndVertical();

                Rect contentDropArea = GUILayoutUtility.GetLastRect();
                HandleCategoryDragDrop(category, contentDropArea);
            }

            EditorGUILayout.EndVertical();
            return false;
        }

        private void DrawFolders(FavoriteCategory category, int categoryIndex)
        {
            int folderToDelete = -1;

            for (int j = 0; j < category.folders.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10); 

                if (showUIElements)
                {
                    GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                    Rect folderDragHandleRect = GUILayoutUtility.GetLastRect();
                    HandleReordering(folderDragHandleRect, categoryIndex, j);
                }

                GUIContent folderIcon = EditorGUIUtility.IconContent("Folder Icon");
                GUILayout.Label(folderIcon, GUILayout.Width(16), GUILayout.Height(16));

                DefaultAsset folder = category.folders[j];
                if (folder != null)
                {
                    if (GUILayout.Button(folder.name, EditorStyles.label))
                    {
                        EditorGUIUtility.PingObject(folder);
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
                        folderToDelete = j;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (folderToDelete != -1)
            {
                category.folders.RemoveAt(folderToDelete);
            }
        }

        private void HandleReordering(Rect handleRect, int categoryIndex, int folderIndex)
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.type)
            {
                case EventType.MouseDown when handleRect.Contains(current.mousePosition) && current.button == 0:
                    isDragging = true;
                    dragSourceCategoryIndex = categoryIndex;
                    dragSourceFolderIndex = folderIndex;
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

            if (dragSourceFolderIndex == -1) // Category reordering
            {
                if (Mathf.Abs(deltaY) > 25)
                {
                    int direction = deltaY > 0 ? 1 : -1;
                    int targetIndex = dragSourceCategoryIndex + direction;
                    if (targetIndex >= 0 && targetIndex < categories.Count)
                    {
                        var temp = categories[dragSourceCategoryIndex];
                        categories[dragSourceCategoryIndex] = categories[targetIndex];
                        categories[targetIndex] = temp;
                    }
                }
            }
            else // Folder reordering
            {
                var category = categories[dragSourceCategoryIndex];
                if (Mathf.Abs(deltaY) > 20)
                {
                    int direction = deltaY > 0 ? 1 : -1;
                    int targetIndex = dragSourceFolderIndex + direction;
                    if (targetIndex >= 0 && targetIndex < category.folders.Count)
                    {
                        var temp = category.folders[dragSourceFolderIndex];
                        category.folders[dragSourceFolderIndex] = category.folders[targetIndex];
                        category.folders[targetIndex] = temp;
                    }
                }
            }
        }

        private void HandleCategoryDragDrop(FavoriteCategory category, Rect dropArea)
        {
            Event current = Event.current;
            
            if (!dropArea.Contains(current.mousePosition)) return;

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
                            if (!category.folders.Contains(folder))
                            {
                                category.folders.Add(folder);
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