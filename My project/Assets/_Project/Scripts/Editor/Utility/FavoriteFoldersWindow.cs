using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAT.Utility
{
    // JSON 저장용 데이터 클래스
    [System.Serializable]
    public class FavoriteFoldersJsonData
    {
        public List<FavoriteCategoryData> categories = new List<FavoriteCategoryData>();
    }

    [System.Serializable]
    public class FavoriteCategoryData
    {
        public string name;
        public bool isExpanded = true;
        public List<string> folderGUIDs = new List<string>(); // GUID로 저장하여 안정성 확보
    }

    // 런타임에서 사용할 폴더 카테고리 클래스
    public class FavoriteCategory
    {
        public string name;
        public bool isExpanded = true;
        public List<DefaultAsset> folders = new List<DefaultAsset>();

        public FavoriteCategory(string name)
        {
            this.name = name;
        }

        // JSON 데이터에서 변환
        public static FavoriteCategory FromJsonData(FavoriteCategoryData jsonData)
        {
            var category = new FavoriteCategory(jsonData.name);
            category.isExpanded = jsonData.isExpanded;

            // GUID를 실제 폴더 에셋으로 변환
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

        // JSON 데이터로 변환
        public FavoriteCategoryData ToJsonData()
        {
            var jsonData = new FavoriteCategoryData();
            jsonData.name = this.name;
            jsonData.isExpanded = this.isExpanded;

            // 폴더 에셋을 GUID로 변환
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
        // JSON 파일 경로 - ProjectSettings 폴더에 저장 (FilteredImageEditor와 동일)
        private const string JSON_PATH = "ProjectSettings/FavoriteFolders.json";

        // 카테고리 핸들 컬러
        private Color handleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private List<FavoriteCategory> categories = new List<FavoriteCategory>();
        private Vector2 scrollPosition;
        private bool isDragging = false;
        private int dragSourceCategoryIndex = -1;
        private int dragSourceFolderIndex = -1;
        private Rect dragRect;
        private bool showUIElements = false;

        // GUI 스타일
        private GUIStyle handleStyle;
        private GUIStyle editModeToggleStyle;
        private GUIStyle categoryHeaderStyle;
        private GUIStyle categoryNameStyle;
        private Texture2D redBackground;
        private Texture2D categoryHeaderBackground;

        // 스타일 초기화 플래그
        private bool stylesInitialized = false;

        [MenuItem("CAT/Utility/Favorite")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteFoldersWindow>("Favorite");
        }

        // 창 활성화시 데이터 로드
        private void OnEnable()
        {
            LoadFromJson();

            // 에디터 이벤트 등록
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        // 창 비활성화시 리소스 정리 및 저장
        private void OnDisable()
        {
            SaveToJson();

            if (redBackground != null)
            {
                DestroyImmediate(redBackground);
            }
            if (categoryHeaderBackground != null)
            {
                DestroyImmediate(categoryHeaderBackground);
            }

            // 이벤트 해제
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        // 포커스 잃을 때 저장
        private void OnLostFocus()
        {
            if (isDragging)
            {
                isDragging = false;
                Repaint();
            }
            SaveToJson();
        }

        // 에디터 이벤트 콜백들
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || 
                state == PlayModeStateChange.ExitingPlayMode)
            {
                SaveToJson();
            }
        }

        private void OnBeforeAssemblyReload()
        {
            SaveToJson();
        }

        // JSON에서 데이터 로드
        private void LoadFromJson()
        {
            categories.Clear();

            if (File.Exists(JSON_PATH))
            {
                try
                {
                    string json = File.ReadAllText(JSON_PATH);
                    FavoriteFoldersJsonData jsonData = JsonUtility.FromJson<FavoriteFoldersJsonData>(json);

                    if (jsonData != null && jsonData.categories != null)
                    {
                        foreach (var categoryData in jsonData.categories)
                        {
                            categories.Add(FavoriteCategory.FromJsonData(categoryData));
                        }
                    }

                    Debug.Log($"Favorite 폴더 데이터 로드 완료 - 카테고리 수: {categories.Count}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Favorite 폴더 데이터 로드 실패: {e.Message}");
                    categories.Clear();
                }
            }
            else
            {
                Debug.Log("Favorite 폴더 데이터 파일이 없음 - 새로 생성");
            }
        }

        // JSON으로 데이터 저장
        private void SaveToJson()
        {
            try
            {
                FavoriteFoldersJsonData jsonData = new FavoriteFoldersJsonData();
                jsonData.categories = categories.Select(c => c.ToJsonData()).ToList();

                string json = JsonUtility.ToJson(jsonData, true);
                
                // ProjectSettings 디렉토리가 없으면 생성
                string directory = Path.GetDirectoryName(JSON_PATH);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(JSON_PATH, json);
                Debug.Log($"Favorite 폴더 데이터 저장 완료 - 카테고리 수: {categories.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Favorite 폴더 데이터 저장 실패: {e.Message}");
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();

            // 헤더
            DrawHeader();

            // 스크롤 영역
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 카테고리 목록 그리기
            DrawCategories();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 이벤트 처리
            HandleEvents();

            // 드래그 중일 때 간단한 시각적 피드백
            if (isDragging)
            {
                Event current = Event.current;
                Vector2 mousePos = current.mousePosition;
                
                // 드래그 방향 표시
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

            handleStyle = new GUIStyle(EditorStyles.label);
            handleStyle.normal.textColor = handleColor;
            handleStyle.alignment = TextAnchor.MiddleCenter;

            editModeToggleStyle = new GUIStyle(GUI.skin.toggle);
            editModeToggleStyle.fontSize = 10;

            categoryHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader);
            categoryNameStyle = new GUIStyle(EditorStyles.textField);
            categoryNameStyle.fontStyle = FontStyle.Bold;

            redBackground = new Texture2D(1, 1);
            redBackground.SetPixel(0, 0, new Color(0.8f, 0.4f, 0.4f, 0.3f));
            redBackground.Apply();

            categoryHeaderBackground = new Texture2D(1, 1);
            categoryHeaderBackground.SetPixel(0, 0, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            categoryHeaderBackground.Apply();

            stylesInitialized = true;
        }

        // 헤더 그리기
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            // Edit 모드일 때만 Add 버튼 표시
            if (showUIElements)
            {
                if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(35)))
                {
                    AddNewCategory();
                }
            }

            // 동적 너비 처리 - 남은 공간 확인
            GUILayout.FlexibleSpace();

            // Edit 토글의 최소 너비 보장
            float minEditWidth = 40f;
            float availableWidth = position.width - 40f; // 여유 공간 고려

            if (availableWidth >= minEditWidth)
            {
                bool newShowUIElements = GUILayout.Toggle(showUIElements, "Edit", editModeToggleStyle, GUILayout.Width(minEditWidth));
                if (newShowUIElements != showUIElements)
                {
                    showUIElements = newShowUIElements;

                    // Edit 체크 해제시 자동 저장
                    if (!showUIElements)
                    {
                        SaveToJson();
                    }

                    Repaint();
                }
            }
            else
            {
                // 공간이 부족하면 아이콘만 표시
                bool newShowUIElements = GUILayout.Toggle(showUIElements, "E", editModeToggleStyle, GUILayout.Width(20));
                if (newShowUIElements != showUIElements)
                {
                    showUIElements = newShowUIElements;

                    // Edit 체크 해제시 자동 저장
                    if (!showUIElements)
                    {
                        SaveToJson();
                    }

                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        private void AddNewCategory()
        {
            string newName = "New Category " + (categories.Count + 1);
            categories.Add(new FavoriteCategory(newName));
            SaveToJson();
            Debug.Log($"새 카테고리 추가: {newName}");
        }

        // 카테고리 목록 그리기
        private void DrawCategories()
        {
            if (categories == null) return;

            int categoryToDelete = -1;

            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                if (category == null) continue;

                if (DrawCategory(category, i, out categoryToDelete))
                {
                    break;
                }
            }

            if (categoryToDelete >= 0)
            {
                Debug.Log($"카테고리 삭제: {categories[categoryToDelete].name}");
                categories.RemoveAt(categoryToDelete);
                SaveToJson();
                Repaint();
            }
        }

        // 단일 카테고리 그리기
        private bool DrawCategory(FavoriteCategory category, int categoryIndex, out int categoryToDelete)
        {
            categoryToDelete = -1;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();

            // 카테고리 헤더 배경
            if (showUIElements)
            {
                GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                Rect categoryDragHandleRect = GUILayoutUtility.GetLastRect();
                HandleReordering(categoryDragHandleRect, categoryIndex, -1);
            }

            // 카테고리 이름 (편집 모드일 때만 텍스트 필드)
            if (showUIElements)
            {
                string newName = EditorGUILayout.TextField(category.name, categoryNameStyle);
                if (newName != category.name)
                {
                    Debug.Log($"카테고리 이름 변경: {category.name} -> {newName}");
                    category.name = newName;
                    SaveToJson();
                }
            }
            else
            {
                category.isExpanded = EditorGUILayout.Foldout(category.isExpanded, category.name, true);
            }

            GUILayout.FlexibleSpace();

            // 삭제 버튼 (편집 모드일 때만 표시)
            if (showUIElements)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog("카테고리 삭제",
                        $"'{category.name}' 카테고리를 삭제하시겠습니까?", "Yes", "No"))
                    {
                        categoryToDelete = categoryIndex;
                        GUI.backgroundColor = Color.white;
                        return true;
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // 폴더 목록 및 드래그 앤 드롭 영역
            if (category.isExpanded || showUIElements)
            {
                // 폴더 영역 그리기 (드래그 앤 드롭 영역 포함)
                DrawFolders(category, categoryIndex);

                // 카테고리별 드래그 앤 드롭 처리
                HandleCategoryDragDrop(category, categoryIndex);
            }

            EditorGUILayout.EndVertical();

            return false;
        }

        // 폴더 목록 그리기
        private void DrawFolders(FavoriteCategory category, int categoryIndex)
        {
            if (category.folders == null) return;

            EditorGUILayout.BeginVertical();

            int folderToDelete = -1;

            // 안전한 범위 체크
            int folderCount = category.folders.Count;
            for (int j = 0; j < folderCount; j++)
            {
                // 인덱스 유효성 재확인
                if (j >= category.folders.Count) break;

                EditorGUILayout.BeginHorizontal();

                GUILayout.Space(10);  // 들여쓰기

                if (showUIElements)
                {
                    GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                    Rect folderDragHandleRect = GUILayoutUtility.GetLastRect();
                    HandleReordering(folderDragHandleRect, categoryIndex, j);
                }

                // 폴더 아이콘 추가
                GUIContent folderIcon = EditorGUIUtility.IconContent("Folder Icon");
                GUILayout.Label(folderIcon, GUILayout.Width(16), GUILayout.Height(16));

                var folder = category.folders[j];
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
                    if (GUILayout.Button("[Missing Folder]", EditorStyles.label))
                    {
                        folderToDelete = j;
                    }
                    GUI.color = Color.white;
                }

                GUILayout.FlexibleSpace();

                if (showUIElements)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("×", GUILayout.Width(18), GUILayout.Height(18)))
                    {
                        folderToDelete = j;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            // 폴더 삭제 처리 (안전한 인덱스 체크)
            if (folderToDelete >= 0 && folderToDelete < category.folders.Count)
            {
                Debug.Log($"폴더 삭제: {category.folders[folderToDelete]?.name ?? "Missing"}");
                category.folders.RemoveAt(folderToDelete);
                SaveToJson();
            }

            EditorGUILayout.EndVertical();
        }

        // 간소화된 순서 변경 처리 (정확한 타겟팅)
        private void HandleReordering(Rect handleRect, int categoryIndex, int folderIndex)
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (handleRect.Contains(current.mousePosition) && current.button == 0)
                    {
                        isDragging = true;
                        dragSourceCategoryIndex = categoryIndex;
                        dragSourceFolderIndex = folderIndex;
                        dragRect = handleRect;
                        GUIUtility.hotControl = controlID;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (isDragging && GUIUtility.hotControl == controlID)
                    {
                        Repaint();
                        current.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (isDragging && GUIUtility.hotControl == controlID)
                    {
                        PerformSimpleReordering(current.mousePosition);
                        
                        isDragging = false;
                        GUIUtility.hotControl = 0;
                        current.Use();
                        Repaint();
                    }
                    break;
            }
        }

        // 단순화된 순서 변경 (위/아래로만)
        private void PerformSimpleReordering(Vector2 mousePos)
        {
            float deltaY = mousePos.y - dragRect.y;

            if (dragSourceFolderIndex == -1) // 카테고리 순서 변경
            {
                if (Mathf.Abs(deltaY) > 25) // 충분한 이동 거리
                {
                    int direction = deltaY > 0 ? 1 : -1;
                    int targetIndex = dragSourceCategoryIndex + direction;
                    
                    if (targetIndex >= 0 && targetIndex < categories.Count)
                    {
                        // 카테고리 위치 교환
                        var temp = categories[dragSourceCategoryIndex];
                        categories[dragSourceCategoryIndex] = categories[targetIndex];
                        categories[targetIndex] = temp;
                        
                        Debug.Log($"카테고리 순서 변경: {dragSourceCategoryIndex} ↔ {targetIndex}");
                        SaveToJson();
                    }
                }
            }
            else // 폴더 순서 변경
            {
                var category = categories[dragSourceCategoryIndex];
                if (category.folders != null && dragSourceFolderIndex < category.folders.Count)
                {
                    if (Mathf.Abs(deltaY) > 20) // 충분한 이동 거리
                    {
                        int direction = deltaY > 0 ? 1 : -1;
                        int targetIndex = dragSourceFolderIndex + direction;
                        
                        if (targetIndex >= 0 && targetIndex < category.folders.Count)
                        {
                            // 폴더 위치 교환
                            var temp = category.folders[dragSourceFolderIndex];
                            category.folders[dragSourceFolderIndex] = category.folders[targetIndex];
                            category.folders[targetIndex] = temp;
                            
                            Debug.Log($"폴더 순서 변경: {dragSourceFolderIndex} ↔ {targetIndex}");
                            SaveToJson();
                        }
                    }
                }
            }
        }

        // 불필요한 메서드들 제거 및 정리

        // 카테고리별 드래그 앤 드롭 처리
        private void HandleCategoryDragDrop(FavoriteCategory category, int categoryIndex)
        {
            Event current = Event.current;
            Rect lastRect = GUILayoutUtility.GetLastRect();

            // 마지막으로 그려진 영역을 드롭 영역으로 확장
            Rect dropArea = new Rect(lastRect.x, lastRect.y - 60, lastRect.width, 80);

            if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
            {
                if (dropArea.Contains(current.mousePosition))
                {
                    bool hasValidAssets = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is DefaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                        {
                            hasValidAssets = true;
                            break;
                        }
                    }

                    if (hasValidAssets)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (current.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();

                            if (category.folders == null)
                            {
                                category.folders = new List<DefaultAsset>();
                            }

                            bool addedAny = false;
                            foreach (var obj in DragAndDrop.objectReferences)
                            {
                                if (obj is DefaultAsset folder && 
                                    AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                                {
                                    if (!category.folders.Contains(folder))
                                    {
                                        Debug.Log($"폴더 '{folder.name}'를 카테고리 '{category.name}'에 추가");
                                        category.folders.Add(folder);
                                        addedAny = true;
                                    }
                                }
                            }

                            if (addedAny)
                            {
                                SaveToJson();
                                Repaint();
                            }
                            
                            current.Use();
                        }
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }
                }
            }
        }

        private void HandleEvents()
        {
            // 전역 드래그 앤 드롭은 제거하고 카테고리별 처리만 사용
            // 기존의 전역 HandleEvents는 더 이상 필요하지 않음
        }
    }
}