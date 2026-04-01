using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace CAT.Utility
{
    /// <summary>
    /// JSON 기반 UI 프리뷰 윈도우.
    /// 왼쪽에 카테고리별 JSON 리스트, 오른쪽에 720x1280 프리뷰를 표시한다.
    /// JSON 항목 클릭 시 프리뷰 생성, "사용하기" 버튼으로 씬에 실제 생성.
    /// </summary>
    public class UIPreviewWindow : EditorWindow
    {
        // ── 카테고리 노드 (폴더 트리) ──────────────────────────────────────
        private class CategoryNode
        {
            public string name;
            public string fullPath;
            public CategoryNode parent;
            public List<CategoryNode> children = new List<CategoryNode>();
            public List<JsonEntry> jsonEntries = new List<JsonEntry>();
        }

        private struct JsonEntry
        {
            public string name;       // 확장자 제외 파일명
            public string fullPath;   // 절대 경로
        }

        // ── 프리뷰 상태 ────────────────────────────────────────────────────
        private PreviewRenderUtility _previewUtility;
        private Canvas _previewCanvas;
        private GameObject _previewInstance;
        private string _selectedJsonPath;
        private string _selectedJsonName;

        // ── 카테고리 리스트 ────────────────────────────────────────────────
        private CategoryNode _rootCategory;
        private CategoryNode _currentCategory;
        private List<JsonEntry> _allJsonEntries = new List<JsonEntry>();
        private string _searchString = "";
        private Vector2 _listScrollPos;

        // ── 레이아웃 ──────────────────────────────────────────────────────
        private float _listWidth = 250f;
        private bool _isResizing;
        private Color _bgColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        private const float BottomBarHeight = 35f;
        private const float ListItemHeight = 22f;
        private const float CategoryItemHeight = 24f;
        private const float PreviewAspect = 720f / 1280f; // 9:16

        // ── 스타일 ────────────────────────────────────────────────────────
        private GUIStyle _listButtonStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _backButtonStyle;
        private GUIStyle _centeredStyle;
        private GUIStyle _selectedStyle;

        // ── 메뉴 및 단축키 ────────────────────────────────────────────────
        [MenuItem("CAT/UI/UI Preview Window %#u")]
        public static void ShowWindow()
        {
            var window = GetWindow<UIPreviewWindow>(false, "UI Preview", true);
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        // ── 라이프사이클 ──────────────────────────────────────────────────
        private void OnEnable()
        {
            InitPreviewUtility();
            BuildCategoryTree();
        }

        private void OnDisable()
        {
            CleanupPreview();

            if (_previewCanvas != null)
                DestroyImmediate(_previewCanvas.gameObject);
            _previewCanvas = null;

            if (_previewUtility != null)
                _previewUtility.Cleanup();
            _previewUtility = null;
        }

        private void InitPreviewUtility()
        {
            _previewUtility = new PreviewRenderUtility();
            _previewUtility.camera.orthographic = true;
            _previewUtility.camera.nearClipPlane = 0.1f;
            _previewUtility.camera.farClipPlane = 10000f;
            _previewUtility.camera.backgroundColor = _bgColor;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;

            // 프리뷰용 WorldSpace Canvas 생성
            var canvasGO = new GameObject("UIPreviewCanvas") { hideFlags = HideFlags.HideAndDontSave };
            _previewCanvas = canvasGO.AddComponent<Canvas>();
            _previewCanvas.renderMode = RenderMode.WorldSpace;
            _previewCanvas.worldCamera = _previewUtility.camera;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 1f;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(720, 1280);
            // 0.01 스케일 → 7.2 x 12.8 월드 유닛
            canvasGO.transform.localScale = Vector3.one * 0.01f;

            _previewUtility.AddSingleGO(canvasGO);
        }

        // ── 카테고리 트리 구축 ────────────────────────────────────────────
        private void BuildCategoryTree()
        {
            string basePath = UIDesignMaker.JsonBasePath;
            _rootCategory = new CategoryNode { name = "UI Design Maker", fullPath = basePath };
            _currentCategory = _rootCategory;
            _allJsonEntries.Clear();

            if (!Directory.Exists(basePath)) return;

            string[] jsonFiles = Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories);
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

            string normalizedBase = Path.GetFullPath(basePath).Replace('\\', '/');
            if (!normalizedBase.EndsWith("/"))
                normalizedBase += "/";

            foreach (string file in jsonFiles)
            {
                string normalizedFile = Path.GetFullPath(file).Replace('\\', '/');
                string relativePath = normalizedFile.Substring(normalizedBase.Length);
                string[] pathParts = relativePath.Split('/');

                var entry = new JsonEntry
                {
                    name = Path.GetFileNameWithoutExtension(pathParts[pathParts.Length - 1]),
                    fullPath = normalizedFile
                };
                _allJsonEntries.Add(entry);

                // 폴더 계층 생성
                CategoryNode current = _rootCategory;
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    CategoryNode found = null;
                    for (int j = 0; j < current.children.Count; j++)
                    {
                        if (current.children[j].name == pathParts[i])
                        {
                            found = current.children[j];
                            break;
                        }
                    }

                    if (found == null)
                    {
                        found = new CategoryNode
                        {
                            name = pathParts[i],
                            fullPath = Path.Combine(current.fullPath, pathParts[i]),
                            parent = current
                        };
                        current.children.Add(found);
                    }

                    current = found;
                }

                current.jsonEntries.Add(entry);
            }
        }

        // ── 프리뷰 생성/삭제 ──────────────────────────────────────────────
        private void SelectJsonItem(string absolutePath, string displayName)
        {
            // 동일 항목 재선택 방지
            if (_selectedJsonPath == absolutePath) return;

            CleanupPreview();
            _selectedJsonPath = absolutePath;
            _selectedJsonName = displayName;

            if (_previewCanvas == null) return;

            _previewInstance = UIDesignMaker.CreatePreviewFromJson(absolutePath, _previewCanvas.transform);

            if (_previewInstance != null)
            {
                // TMP 텍스트 강제 갱신
                ForceTMPUpdate(_previewInstance);
            }

            Repaint();
        }

        private void CleanupPreview()
        {
            if (_previewInstance != null)
                DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }

        /// <summary>
        /// TMP_Text 컴포넌트의 메시를 강제 갱신한다.
        /// TMPro 네임스페이스를 직접 참조하지 않고 리플렉션으로 처리하여 의존성을 회피한다.
        /// </summary>
        private static void ForceTMPUpdate(GameObject root)
        {
            // TMPro.TMP_Text 타입 검색
            Type tmpType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                tmpType = assembly.GetType("TMPro.TMP_Text");
                if (tmpType != null) break;
            }
            if (tmpType == null) return;

            var method = tmpType.GetMethod("ForceMeshUpdate",
                new Type[] { typeof(bool), typeof(bool) });
            if (method == null)
            {
                // 파라미터 없는 오버로드 시도
                method = tmpType.GetMethod("ForceMeshUpdate", Type.EmptyTypes);
            }
            if (method == null) return;

            var components = root.GetComponentsInChildren(tmpType, true);
            foreach (var comp in components)
            {
                try
                {
                    if (method.GetParameters().Length == 2)
                        method.Invoke(comp, new object[] { true, true });
                    else
                        method.Invoke(comp, null);
                }
                catch
                {
                    // 프리뷰 목적이므로 실패 무시
                }
            }
        }

        // ── GUI ───────────────────────────────────────────────────────────
        private void InitStyles()
        {
            if (_listButtonStyle != null) return;

            _listButtonStyle = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(20, 0, 2, 2)
            };
            _categoryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                padding = new RectOffset(20, 0, 2, 2),
                normal = { textColor = new Color(0.9f, 0.75f, 0.3f) }
            };
            _backButtonStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                padding = new RectOffset(8, 0, 2, 2),
                normal = { textColor = new Color(0.6f, 0.8f, 1f) },
                hover = { textColor = Color.white }
            };
            _centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _selectedStyle = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(20, 0, 2, 2),
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            InitStyles();

            var listRect = new Rect(0, 0, _listWidth, position.height);
            var resizerRect = new Rect(_listWidth - 2, 0, 4, position.height);
            float previewAreaWidth = position.width - _listWidth;
            var previewAreaRect = new Rect(_listWidth, 0, previewAreaWidth, position.height - BottomBarHeight);
            var bottomBarRect = new Rect(_listWidth, position.height - BottomBarHeight, previewAreaWidth, BottomBarHeight);

            DrawCategoryList(listRect);
            HandleResizer(resizerRect);
            DrawPreview(previewAreaRect);
            DrawBottomBar(bottomBarRect);
        }

        // ── 프리뷰 렌더링 ────────────────────────────────────────────────
        private void DrawPreview(Rect areaRect)
        {
            if (_previewUtility == null || _previewInstance == null)
            {
                GUI.Label(areaRect, "JSON 항목을 선택하면\n프리뷰가 표시됩니다.", _centeredStyle);
                return;
            }

            // 9:16 비율 유지
            Rect previewRect = CalculateAspectRect(areaRect, PreviewAspect);

            // 비율 영역 외부를 어둡게
            EditorGUI.DrawRect(areaRect, new Color(0.1f, 0.1f, 0.1f, 1f));

            _previewUtility.BeginPreview(previewRect, GUIStyle.none);
            _previewUtility.camera.backgroundColor = _bgColor;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.transform.position = new Vector3(0f, 0f, -1000f);
            _previewUtility.camera.orthographicSize = 6.4f; // 1280 * 0.01 / 2
            _previewUtility.camera.aspect = previewRect.width / Mathf.Max(previewRect.height, 1f);

            Canvas.ForceUpdateCanvases();

            _previewUtility.camera.Render();
            _previewUtility.EndAndDrawPreview(previewRect);
        }

        private static Rect CalculateAspectRect(Rect available, float targetAspect)
        {
            float availAspect = available.width / Mathf.Max(available.height, 1f);
            if (availAspect > targetAspect)
            {
                // 좌우 여백
                float w = available.height * targetAspect;
                return new Rect(
                    available.x + (available.width - w) * 0.5f,
                    available.y, w, available.height);
            }
            else
            {
                // 상하 여백
                float h = available.width / targetAspect;
                return new Rect(
                    available.x,
                    available.y + (available.height - h) * 0.5f,
                    available.width, h);
            }
        }

        // ── 카테고리 리스트 패널 ──────────────────────────────────────────
        private void DrawCategoryList(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Space(4);

            // 헤더
            GUILayout.BeginHorizontal();
            GUILayout.Label("UI Design Maker", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↻", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                BuildCategoryTree();
            }
            GUILayout.EndHorizontal();

            // 검색
            _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
            GUILayout.Space(2);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
            GUILayout.Space(2);

            // 스크롤 리스트
            _listScrollPos = GUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));

            if (!string.IsNullOrEmpty(_searchString))
                DrawFilteredList();
            else
                DrawCategoryNavigation();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCategoryNavigation()
        {
            if (_currentCategory == null) return;

            // 뒤로가기 (루트가 아닌 경우)
            if (_currentCategory != _rootCategory)
            {
                Rect backRect = EditorGUILayout.GetControlRect(false, CategoryItemHeight);
                EditorGUI.DrawRect(backRect, new Color(0.2f, 0.25f, 0.35f, 0.8f));
                GUI.Label(backRect, $"< {_currentCategory.name}", _backButtonStyle);
                EditorGUIUtility.AddCursorRect(backRect, MouseCursor.Link);

                if (Event.current.type == EventType.MouseDown && backRect.Contains(Event.current.mousePosition))
                {
                    _currentCategory = _currentCategory.parent ?? _rootCategory;
                    _listScrollPos = Vector2.zero;
                    Event.current.Use();
                }

                GUILayout.Space(2);
            }

            // 하위 카테고리 (폴더)
            for (int i = 0; i < _currentCategory.children.Count; i++)
            {
                var child = _currentCategory.children[i];
                int totalCount = CountJsonEntriesRecursive(child);

                Rect catRect = EditorGUILayout.GetControlRect(false, CategoryItemHeight);
                EditorGUI.DrawRect(catRect, new Color(0.18f, 0.22f, 0.3f, 0.5f));
                GUI.Label(catRect, $"{child.name}  ({totalCount})  >", _categoryStyle);
                EditorGUIUtility.AddCursorRect(catRect, MouseCursor.Link);

                if (Event.current.type == EventType.MouseDown && catRect.Contains(Event.current.mousePosition))
                {
                    _currentCategory = child;
                    _listScrollPos = Vector2.zero;
                    Event.current.Use();
                }
            }

            // 구분선
            if (_currentCategory.children.Count > 0 && _currentCategory.jsonEntries.Count > 0)
            {
                GUILayout.Space(2);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
                GUILayout.Space(2);
            }

            // JSON 항목
            for (int i = 0; i < _currentCategory.jsonEntries.Count; i++)
                DrawJsonItem(_currentCategory.jsonEntries[i]);
        }

        private void DrawFilteredList()
        {
            string search = _searchString.ToLower();
            for (int i = 0; i < _allJsonEntries.Count; i++)
            {
                if (_allJsonEntries[i].name.ToLower().Contains(search))
                    DrawJsonItem(_allJsonEntries[i]);
            }
        }

        private void DrawJsonItem(JsonEntry entry)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, ListItemHeight);
            bool isSelected = _selectedJsonPath == entry.fullPath;

            if (isSelected)
                EditorGUI.DrawRect(rowRect, new Color(0.17f, 0.36f, 0.53f, 0.9f));
            else if (rowRect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));

            GUI.Label(rowRect, entry.name, isSelected ? _selectedStyle : _listButtonStyle);
            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                SelectJsonItem(entry.fullPath, entry.name);
                Event.current.Use();
            }
        }

        private static int CountJsonEntriesRecursive(CategoryNode node)
        {
            int count = node.jsonEntries.Count;
            for (int i = 0; i < node.children.Count; i++)
                count += CountJsonEntriesRecursive(node.children[i]);
            return count;
        }

        // ── 하단 바 ──────────────────────────────────────────────────────
        private void DrawBottomBar(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();

            // 선택 정보
            string info = _selectedJsonName != null
                ? $"선택: {_selectedJsonName}"
                : "JSON 항목을 선택하세요";
            GUILayout.Label(info, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            // 배경색
            _bgColor = EditorGUILayout.ColorField(_bgColor, GUILayout.Width(40));

            // 사용하기 버튼
            EditorGUI.BeginDisabledGroup(_selectedJsonPath == null);
            if (GUILayout.Button("사용하기", GUILayout.Width(80), GUILayout.Height(24)))
            {
                OnUseButtonClicked();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OnUseButtonClicked()
        {
            if (_selectedJsonPath == null) return;

            // 현재 하이어라키 선택 오브젝트를 부모로 사용
            Transform parent = null;
            if (Selection.activeGameObject != null)
            {
                var canvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
                if (canvas != null)
                    parent = Selection.activeGameObject.transform;
            }

            // 실제 씬에 생성
            if (parent != null)
                UIDesignMaker.CreateFromJsonAbsolute(_selectedJsonPath, parent);
            else
                UIDesignMaker.CreateFromJsonAbsolute(_selectedJsonPath);
        }

        // ── 리사이저 ─────────────────────────────────────────────────────
        private void HandleResizer(Rect rect)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                _isResizing = true;
            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    _listWidth = Mathf.Clamp(_listWidth + Event.current.delta.x, 150f, position.width - 300f);
                    Repaint();
                }
                if (Event.current.type == EventType.MouseUp)
                    _isResizing = false;
            }
        }
    }
}
