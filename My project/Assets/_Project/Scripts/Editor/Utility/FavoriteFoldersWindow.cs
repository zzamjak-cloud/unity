using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace CAT.Utility
{
    // 즐겨찾기 폴더를 관리하는 데이터 클래스
    public class FavoriteFoldersData : ScriptableObject
    {
        public List<FavoriteCategory> categories = new List<FavoriteCategory>();
    }
    // 폴더 카테고리 클래스
    [System.Serializable]
    public class FavoriteCategory
    {
        public string name;
        public bool isExpanded = true;
        public List<DefaultAsset> folders = new List<DefaultAsset>();

        public FavoriteCategory(string name)
        {
            this.name = name;
        }
    }
    // 즐겨찾기 폴더를 관리하는 에디터 창
    public class FavoriteFoldersWindow : EditorWindow
    {
        // 카테고리 핸들 컬러
        private Color handleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        // ScriptableObject 데이터 경로
        private const string dataPath = "Assets/Editor/FavoriteFoldersData.asset";
        
        private FavoriteFoldersData data;
        private Vector2 scrollPosition;
        private bool isDragging = false;
        private int dragSourceCategoryIndex = -1;
        private int dragSourceFolderIndex = -1;
        private Rect dragRect;
        private bool showUIElements = true;

        // GUI 스타일
        private GUIStyle handleStyle;
        private GUIStyle editModeToggleStyle;
        private GUIStyle categoryHeaderStyle;
        private GUIStyle categoryNameStyle;
        private Texture2D redBackground;
        private Texture2D categoryHeaderBackground;
        
        // 스타일 초기화 플래그
        private bool stylesInitialized = false;
        
        // 지연 저장을 위한 변수들
        private bool needsSaveDelayed = false;
        private double lastSaveTime = 0;

        [MenuItem("CAT/Utility/Favorite")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteFoldersWindow>("Favorite");
        }
        // 창 활성화시 데이터 로드
        private void OnEnable()
        {
            LoadData();
        }
        // 창 비활성화시 리소스 정리
        private void OnDisable()
        {
            if (redBackground != null)
            {
                DestroyImmediate(redBackground);
            }
            if (categoryHeaderBackground != null)
            {
                DestroyImmediate(categoryHeaderBackground);
            }
        }
        // 창 포커스 잃었을 때 드래그 상태 초기화
        private void OnLostFocus()
        {
            if (isDragging)
            {
                isDragging = false;
                Repaint();
            }
        }
        // 지연 저장 처리
        private void Update()
        {
            if (needsSaveDelayed && EditorApplication.timeSinceStartup - lastSaveTime > 0.5f)
            {
                SafeSaveData();
                needsSaveDelayed = false;
            }
        }
        // 데이터 로드 및 초기화
        private void LoadData()
        {
            try
            {
                data = AssetDatabase.LoadAssetAtPath<FavoriteFoldersData>(dataPath);
                if (data == null)
                {
                    data = CreateInstance<FavoriteFoldersData>();
                    string dir = Path.GetDirectoryName(dataPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    AssetDatabase.CreateAsset(data, dataPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                
                // 안전성 체크
                if (data.categories == null)
                {
                    data.categories = new List<FavoriteCategory>();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"데이터 로드 실패: {e.Message}");
                data = CreateInstance<FavoriteFoldersData>();
                if (data.categories == null)
                {
                    data.categories = new List<FavoriteCategory>();
                }
            }
        }
        // 안전한 데이터 저장
        private void SafeSaveData()
        {
            try
            {
                if (data != null)
                {
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"데이터 저장 실패: {e.Message}");
            }
        }
        // 저장 예약
        private void ScheduleSave()
        {
            needsSaveDelayed = true;
            lastSaveTime = EditorApplication.timeSinceStartup;
        }
        // GUI 스타일 초기화
        private void InitializeStyles()
        {
            // Handle 스타일
            handleStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = handleColor },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Edit Mode 토글 스타일
            editModeToggleStyle = new GUIStyle(EditorStyles.toolbarButton);
            
            // 빨간색 배경 텍스처 생성
            redBackground = new Texture2D(1, 1);
            redBackground.SetPixel(0, 0, new Color(0.8f, 0.2f, 0.2f, 1.0f));
            redBackground.Apply();
            
            editModeToggleStyle.active.background = redBackground;
            editModeToggleStyle.onActive.background = redBackground;
            editModeToggleStyle.active.textColor = Color.white;
            editModeToggleStyle.onActive.textColor = Color.white;

            // 카테고리 헤더 스타일
            categoryHeaderStyle = new GUIStyle();
            
            bool isDarkMode = EditorGUIUtility.isProSkin;
            Color headerColor = isDarkMode ? new Color(0.3f, 0.4f, 0.5f, 0.8f) : new Color(0.8f, 0.85f, 0.9f, 0.8f);
            
            categoryHeaderBackground = new Texture2D(1, 1);
            categoryHeaderBackground.SetPixel(0, 0, headerColor);
            categoryHeaderBackground.Apply();
            
            categoryHeaderStyle.normal.background = categoryHeaderBackground;
            categoryHeaderStyle.padding = new RectOffset(5, 5, 3, 3);
            categoryHeaderStyle.margin = new RectOffset(0, 0, 2, 2);

            // 카테고리 이름 스타일
            categoryNameStyle = new GUIStyle(EditorStyles.boldLabel);
            
            stylesInitialized = true;
        }
        // 메인 GUI 렌더링
        private void OnGUI()
        {
            if (!stylesInitialized)
            {
                InitializeStyles();
            }

            if (data == null) return;
            if (data.categories == null) return;

            // 툴바
            DrawToolbar();
            
            // 스크롤뷰
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            try
            {
                DrawCategories();
                HandleMouseEvents();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in OnGUI: {e.Message}");
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }
        // 툴바 그리기
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Edit Mode가 활성화되었을 때만 Add Category 버튼 표시
            if (showUIElements)
            {
                if (GUILayout.Button("Add Category", EditorStyles.toolbarButton))
                {
                    data.categories.Add(new FavoriteCategory("New Category " + (data.categories.Count + 1)));
                    ScheduleSave();
                    // 카테고리 생성 후 즉시 UI 업데이트
                    Repaint();
                }
            }
            
            GUILayout.FlexibleSpace();
            
            // Edit Mode 버튼 색상 개선
            GUI.backgroundColor = showUIElements ? new Color(1f, 0.4f, 0.4f, 1f) : Color.white;
            bool newShowUIElements = GUILayout.Toggle(showUIElements, "Edit Mode", editModeToggleStyle);
            GUI.backgroundColor = Color.white;
            
            if (newShowUIElements != showUIElements)
            {
                showUIElements = newShowUIElements;
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        // 카테고리 및 폴더 그리기
        private void DrawCategories()
        {
            // 삭제할 카테고리 인덱스를 저장 (역순 처리 대신 사용)
            int categoryToDelete = -1;
            
            for (int i = 0; i < data.categories.Count; i++)
            {
                var category = data.categories[i];
                if (category == null) continue;
                
                // 카테고리 전체 영역을 위한 Rect 시작
                Rect categoryStartRect = EditorGUILayout.BeginVertical();
                
                // 카테고리 헤더 그리기
                bool deleted = DrawCategoryHeader(category, i, out categoryToDelete);
                if (deleted) 
                {
                    EditorGUILayout.EndVertical();
                    break; // 삭제되면 루프 중단
                }
                
                // 폴더 목록 그리기
                if (category.isExpanded)
                {
                    DrawFolders(category, i);
                }
                
                EditorGUILayout.EndVertical();
                
                // 카테고리 전체 영역에서 드래그 앤 드롭 처리
                Rect categoryRect = GUILayoutUtility.GetLastRect();
                // 카테고리 영역을 확장하여 드래그 앤 드롭 영역을 넓힘
                categoryRect.height += 10; // 추가 여백
                HandleAddingFolders(categoryRect, category);
                
                // 카테고리 간 간격
                GUILayout.Space(3);
            }
            
            // 삭제 처리 (루프 밖에서 실행)
            if (categoryToDelete >= 0 && categoryToDelete < data.categories.Count)
            {
                data.categories.RemoveAt(categoryToDelete);
                ScheduleSave();
            }
        }
        // 카테고리 헤더 그리기
        private bool DrawCategoryHeader(FavoriteCategory category, int categoryIndex, out int categoryToDelete)
        {
            categoryToDelete = -1;
            
            EditorGUILayout.BeginVertical(categoryHeaderStyle);
            EditorGUILayout.BeginHorizontal();
            
            try
            {
                // 드래그 핸들
                if (showUIElements)
                {
                    GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                    Rect dragHandleRect = GUILayoutUtility.GetLastRect();
                    HandleReordering(dragHandleRect, categoryIndex, -1);
                }
                
                // 폴드아웃
                category.isExpanded = GUILayout.Toggle(category.isExpanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(15));
                
                // 카테고리 이름
                if (showUIElements)
                {
                    string newCategoryName = EditorGUILayout.TextField(category.name);
                    if (newCategoryName != category.name)
                    {
                        category.name = newCategoryName;
                        ScheduleSave();
                    }
                }
                else
                {
                    if (GUILayout.Button(category.name, categoryNameStyle))
                    {
                        category.isExpanded = !category.isExpanded;
                    }
                }
                
                // 삭제 버튼
                if (showUIElements)
                {
                    GUI.backgroundColor = new Color(1f, 0.6f, 0.6f, 1f);
                    if (GUILayout.Button("—", GUILayout.Width(25)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Category", $"Are you sure you want to delete the '{category.name}' category?", "Yes", "No"))
                        {
                            categoryToDelete = categoryIndex;
                            GUI.backgroundColor = Color.white;
                            return true; // 삭제됨
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            
            return false; // 삭제 안됨
        }
        // 폴더 목록 그리기
        private void DrawFolders(FavoriteCategory category, int categoryIndex)
        {
            if (category.folders == null) return;
            
            EditorGUILayout.BeginVertical();
            
            try
            {
                // 삭제할 폴더 인덱스 저장
                int folderToDelete = -1;
                
                for (int j = 0; j < category.folders.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    try
                    {
                        GUILayout.Space(25); // 들여쓰기
                        
                        // 드래그 핸들
                        if (showUIElements)
                        {
                            GUILayout.Label("☰", handleStyle, GUILayout.Width(20));
                            Rect folderDragHandleRect = GUILayoutUtility.GetLastRect();
                            HandleReordering(folderDragHandleRect, categoryIndex, j);
                        }

                        // 폴더 버튼
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

                        // 삭제 버튼
                        if (showUIElements)
                        {
                            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f, 1f);
                            if (GUILayout.Button("×", GUILayout.Width(20)))
                            {
                                folderToDelete = j;
                            }
                            GUI.backgroundColor = Color.white;
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }
                
                // 폴더 삭제 처리
                if (folderToDelete >= 0 && folderToDelete < category.folders.Count)
                {
                    category.folders.RemoveAt(folderToDelete);
                    ScheduleSave();
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }
        // 마우스 이벤트 처리
        private void HandleMouseEvents()
        {
            EventType currentEventType = Event.current.type;
            if (currentEventType == EventType.MouseUp && isDragging)
            {
                isDragging = false;
                Repaint();
            }
        }
        // 드래그 앤 드롭 및 순서 변경 처리
        private void HandleReordering(Rect handleRect, int categoryIdx, int folderIdx)
        {
            if (!showUIElements) return;

            EventType currentEventType = Event.current.type;
            Vector2 mousePosition = Event.current.mousePosition;

            if (currentEventType == EventType.MouseDown && handleRect.Contains(mousePosition))
            {
                isDragging = true;
                dragSourceCategoryIndex = categoryIdx;
                dragSourceFolderIndex = folderIdx;
                dragRect = handleRect;
                Event.current.Use();
            }

            if (isDragging && currentEventType == EventType.MouseDrag && handleRect.Contains(mousePosition))
            {
                // 안전성 체크
                if (data.categories == null) return;
                
                // 카테고리 순서 변경
                if (dragSourceFolderIndex == -1 && folderIdx == -1)
                {
                    if (dragSourceCategoryIndex != categoryIdx && 
                        dragSourceCategoryIndex >= 0 && dragSourceCategoryIndex < data.categories.Count &&
                        categoryIdx >= 0 && categoryIdx < data.categories.Count)
                    {
                        var draggedCategory = data.categories[dragSourceCategoryIndex];
                        data.categories.RemoveAt(dragSourceCategoryIndex);
                        data.categories.Insert(categoryIdx, draggedCategory);
                        dragSourceCategoryIndex = categoryIdx;
                        ScheduleSave();
                    }
                }
                // 폴더 순서 변경
                else if (dragSourceCategoryIndex == categoryIdx && dragSourceFolderIndex != -1 && folderIdx != -1)
                {
                    if (dragSourceFolderIndex != folderIdx && categoryIdx >= 0 && categoryIdx < data.categories.Count)
                    {
                        var category = data.categories[categoryIdx];
                        if (category != null && category.folders != null &&
                            dragSourceFolderIndex >= 0 && dragSourceFolderIndex < category.folders.Count &&
                            folderIdx >= 0 && folderIdx <= category.folders.Count)
                        {
                            var draggedFolder = category.folders[dragSourceFolderIndex];
                            category.folders.RemoveAt(dragSourceFolderIndex);
                            
                            // 인덱스 조정
                            int insertIndex = folderIdx;
                            if (dragSourceFolderIndex < folderIdx) insertIndex--;
                            if (insertIndex < 0) insertIndex = 0;
                            if (insertIndex > category.folders.Count) insertIndex = category.folders.Count;
                            
                            category.folders.Insert(insertIndex, draggedFolder);
                            dragSourceFolderIndex = insertIndex;
                            ScheduleSave();
                        }
                    }
                }
                Event.current.Use();
            }

            if (isDragging)
            {
                dragRect.position = mousePosition - handleRect.size / 2;
                GUI.Box(dragRect, "", "selectionRect");
                Repaint();
            }
        }
        // 폴더 추가 처리
        private void HandleAddingFolders(Rect dropArea, FavoriteCategory category)
        {
            if (category == null || category.folders == null) return;
            
            EventType currentEventType = Event.current.type;
            if (currentEventType == EventType.DragUpdated || currentEventType == EventType.DragPerform)
            {
                // 드롭 영역 감지
                bool isInDropArea = dropArea.Contains(Event.current.mousePosition);
                
                // 시각적 피드백 - 드래그 중일 때 카테고리 하이라이트
                if (isInDropArea && DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                {
                    // 유효한 폴더가 있는지 확인
                    bool hasValidFolder = false;
                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        if (obj != null && obj is DefaultAsset)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(obj);
                            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
                            {
                                hasValidFolder = true;
                                break;
                            }
                        }
                    }
                    // 유효한 폴더가 있으면 Copy 커서, 없으면 Rejected 커서
                    if (hasValidFolder)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        
                        // ** [추가] 시각적 피드백 **
                        if (currentEventType == EventType.DragUpdated)
                        {
                            // 드롭 영역을 하이라이트로 표시
                            GUI.Box(dropArea, "", "SelectionRect");
                            Repaint();
                        }
                    }
                    else
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    }
                }
                // 드롭 처리
                if (isInDropArea && currentEventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    try
                    {
                        if (DragAndDrop.objectReferences != null)
                        {
                            int addedCount = 0;
                            foreach (Object draggedObject in DragAndDrop.objectReferences)
                            {
                                if (draggedObject != null && draggedObject is DefaultAsset)
                                {
                                    string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                                    if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
                                    {
                                        var folderAsset = draggedObject as DefaultAsset;
                                        if (!category.folders.Contains(folderAsset))
                                        {
                                            category.folders.Add(folderAsset);
                                            addedCount++;
                                        }
                                    }
                                }
                            }
                            
                            if (addedCount > 0)
                            {
                                ScheduleSave();
                                Repaint(); // ** [추가] 즉시 UI 업데이트 **
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error adding folders: {e.Message}");
                    }
                    
                    Event.current.Use();
                }
            }
        }
    }
}