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
    /// 왼쪽에 카테고리별 JSON 리스트, 오른쪽에 프리뷰를 표시한다.
    /// 복수 선택 지원: 체크박스, Ctrl+클릭 토글, Shift+클릭 범위 선택.
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

        // ── 프리뷰 엔트리 (다중 선택) ─────────────────────────────────────
        private class PreviewEntry
        {
            public string jsonPath;
            public string jsonName;
            public GameObject instance;
            public Canvas canvas;
        }

        // ── 프리뷰 상태 ────────────────────────────────────────────────────
        private PreviewRenderUtility _previewUtility;
        private readonly List<PreviewEntry> _entries = new List<PreviewEntry>();

        // ── 선택 상태 ─────────────────────────────────────────────────────
        private readonly HashSet<string> _selectedPaths = new HashSet<string>();
        private JsonEntry _lastClickedEntry;
        private bool _hasLastClicked;

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

        // 단일 캔버스 크기: 720x1280 → 0.01 스케일 → 7.2 x 12.8 월드 유닛
        private const float CanvasWorldWidth = 7.2f;
        private const float CanvasWorldHeight = 12.8f;
        private const float CellPadding = 1.2f;
        private const int MaxGridColumns = 5;

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
            CleanupAllEntries();

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

        // ── 프리뷰 엔트리 관리 ────────────────────────────────────────────

        /// <summary>
        /// 선택 상태에 따라 프리뷰 엔트리를 재구축한다.
        /// </summary>
        private void RebuildPreviewEntries()
        {
            CleanupAllEntries();
            if (_previewUtility == null || _selectedPaths.Count == 0) return;

            // 선택 순서 유지 (allJsonEntries 순서 기준)
            var ordered = new List<JsonEntry>();
            for (int i = 0; i < _allJsonEntries.Count; i++)
            {
                if (_selectedPaths.Contains(_allJsonEntries[i].fullPath))
                    ordered.Add(_allJsonEntries[i]);
            }

            // 그리드 배치 계산
            int count = ordered.Count;
            int cols = Mathf.Min(count, MaxGridColumns);
            int rows = Mathf.CeilToInt((float)count / cols);
            float stepX = CanvasWorldWidth * CellPadding;
            float stepY = CanvasWorldHeight * CellPadding;

            // 중심 오프셋 (전체 그리드를 원점 중심으로 배치)
            float offsetX = (cols - 1) * stepX * 0.5f;
            float offsetY = (rows - 1) * stepY * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                // 캔버스 생성
                var canvasGO = new GameObject($"PreviewCanvas_{ordered[i].name}")
                    { hideFlags = HideFlags.HideAndDontSave };
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _previewUtility.camera;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 1f;

                var rt = canvasGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(720, 1280);
                canvasGO.transform.localScale = Vector3.one * 0.01f;

                // 그리드 위치 설정
                float x = col * stepX - offsetX;
                float y = -(row * stepY - offsetY);
                canvasGO.transform.position = new Vector3(x, y, 0f);

                _previewUtility.AddSingleGO(canvasGO);

                // JSON에서 프리뷰 오브젝트 생성
                var instance = UIDesignMaker.CreatePreviewFromJson(ordered[i].fullPath, canvas.transform);
                if (instance != null)
                    ForceTMPUpdate(instance);

                _entries.Add(new PreviewEntry
                {
                    jsonPath = ordered[i].fullPath,
                    jsonName = ordered[i].name,
                    instance = instance,
                    canvas = canvas
                });
            }

            Repaint();
        }

        private void CleanupAllEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].instance != null)
                    DestroyImmediate(_entries[i].instance);
                if (_entries[i].canvas != null)
                    DestroyImmediate(_entries[i].canvas.gameObject);
            }
            _entries.Clear();
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
            if (_previewUtility == null || _entries.Count == 0)
            {
                GUI.Label(areaRect, "JSON 항목을 선택하면\n프리뷰가 표시됩니다.", _centeredStyle);
                return;
            }

            EditorGUI.DrawRect(areaRect, new Color(0.1f, 0.1f, 0.1f, 1f));

            _previewUtility.BeginPreview(areaRect, GUIStyle.none);
            _previewUtility.camera.backgroundColor = _bgColor;
            _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            _previewUtility.camera.transform.position = new Vector3(0f, 0f, -1000f);

            // 카메라 크기 계산: 그리드 전체가 보이도록 조정
            int count = _entries.Count;
            int cols = Mathf.Min(count, MaxGridColumns);
            int rows = Mathf.CeilToInt((float)count / cols);
            float totalWidth = cols * CanvasWorldWidth * CellPadding;
            float totalHeight = rows * CanvasWorldHeight * CellPadding;
            float areaAspect = areaRect.width / Mathf.Max(areaRect.height, 1f);
            float gridAspect = totalWidth / Mathf.Max(totalHeight, 1f);

            if (areaAspect > gridAspect)
                _previewUtility.camera.orthographicSize = totalHeight * 0.5f + 0.5f;
            else
                _previewUtility.camera.orthographicSize = (totalWidth / areaAspect) * 0.5f + 0.5f;

            _previewUtility.camera.aspect = areaAspect;

            Canvas.ForceUpdateCanvases();

            _previewUtility.camera.Render();
            _previewUtility.EndAndDrawPreview(areaRect);

            // 이름 라벨 표시
            DrawEntryLabels(areaRect);
        }

        /// <summary>
        /// 프리뷰 영역 위에 각 엔트리의 이름 라벨을 표시한다.
        /// 라벨 클릭 시 해당 JSON을 씬에 생성한다.
        /// </summary>
        private void DrawEntryLabels(Rect areaRect)
        {
            if (_entries.Count == 0) return;

            var cam = _previewUtility.camera;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.4f, 0.85f, 1f, 0.95f) },
                fontSize = 11
            };
            var hoverStyle = new GUIStyle(labelStyle)
            {
                normal = { textColor = Color.white }
            };

            var ev = Event.current;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].canvas == null) continue;

                // 캔버스 하단 월드 좌표
                var worldPos = _entries[i].canvas.transform.position
                    + new Vector3(0f, -(CanvasWorldHeight * 0.5f + 0.3f), 0f);

                // 월드 → 뷰포트 → GUI 좌표 변환
                var viewPos = cam.WorldToViewportPoint(worldPos);
                float guiX = areaRect.x + viewPos.x * areaRect.width;
                float guiY = areaRect.y + (1f - viewPos.y) * areaRect.height;

                var labelRect = new Rect(guiX - 100f, guiY, 200f, 22f);
                bool isHover = labelRect.Contains(ev.mousePosition);

                // 검은색 알파 배경 프레임
                EditorGUI.DrawRect(labelRect, new Color(0f, 0f, 0f, isHover ? 0.75f : 0.55f));
                GUI.Label(labelRect, _entries[i].jsonName, isHover ? hoverStyle : labelStyle);
                EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

                // 클릭 시 씬에 생성
                if (ev.type == EventType.MouseDown && isHover)
                {
                    CreateInScene(_entries[i].jsonPath);
                    ev.Use();
                }
            }
        }

        /// <summary>
        /// 단일 JSON을 씬에 생성한다.
        /// </summary>
        private static void CreateInScene(string jsonPath)
        {
            Transform parent = null;
            if (Selection.activeGameObject != null)
            {
                var canvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
                if (canvas != null)
                    parent = Selection.activeGameObject.transform;
            }

            if (parent != null)
                UIDesignMaker.CreateFromJsonAbsolute(jsonPath, parent);
            else
                UIDesignMaker.CreateFromJsonAbsolute(jsonPath);
        }

        // ── 카테고리 리스트 패널 ──────────────────────────────────────────
        private void DrawCategoryList(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Space(4);

            // 헤더
            GUILayout.BeginHorizontal();
            GUILayout.Label("UI Design Maker", EditorStyles.boldLabel);
            if (_selectedPaths.Count > 0)
            {
                var countStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
                GUILayout.Label($"({_selectedPaths.Count})", countStyle);
            }
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

            // 선택 해제 버튼
            if (_selectedPaths.Count > 0)
            {
                if (GUILayout.Button("선택 해제", EditorStyles.miniButton))
                {
                    _selectedPaths.Clear();
                    RebuildPreviewEntries();
                }
                GUILayout.Space(2);
            }

            // 스크롤 리스트
            _listScrollPos = GUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));

            if (!string.IsNullOrEmpty(_searchString))
                DrawFilteredList();
            else
                DrawCategoryNavigation();

            GUILayout.EndScrollView();

            // 안내 텍스트
            GUILayout.Label("체크박스/Ctrl/Shift 복수선택", EditorStyles.centeredGreyMiniLabel);

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
            bool isSelected = _selectedPaths.Contains(entry.fullPath);

            // 배경
            if (isSelected)
                EditorGUI.DrawRect(rowRect, new Color(0.17f, 0.36f, 0.53f, 0.9f));
            else if (rowRect.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));

            // 체크박스
            var checkRect = new Rect(rowRect.x + 2, rowRect.y + 2, 16, rowRect.height - 4);
            EditorGUI.BeginChangeCheck();
            bool newChecked = GUI.Toggle(checkRect, isSelected, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                if (newChecked) _selectedPaths.Add(entry.fullPath);
                else _selectedPaths.Remove(entry.fullPath);
                _lastClickedEntry = entry;
                _hasLastClicked = true;
                RebuildPreviewEntries();
            }

            // 이름 라벨 (체크박스 오른쪽)
            var labelRect = new Rect(rowRect.x + 20, rowRect.y, rowRect.width - 20, rowRect.height);
            GUI.Label(labelRect, entry.name, isSelected ? _selectedStyle : _listButtonStyle);
            EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);

            // 클릭 처리
            if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
            {
                var ev = Event.current;

                if (ev.control)
                {
                    // Ctrl+클릭: 토글
                    if (_selectedPaths.Contains(entry.fullPath)) _selectedPaths.Remove(entry.fullPath);
                    else _selectedPaths.Add(entry.fullPath);
                    _lastClickedEntry = entry;
                    _hasLastClicked = true;
                    RebuildPreviewEntries();
                }
                else if (ev.shift && _hasLastClicked)
                {
                    // Shift+클릭: 범위 선택
                    int from = _allJsonEntries.FindIndex(e => e.fullPath == _lastClickedEntry.fullPath);
                    int to = _allJsonEntries.FindIndex(e => e.fullPath == entry.fullPath);
                    if (from >= 0 && to >= 0)
                    {
                        int lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
                        for (int si = lo; si <= hi; si++)
                            _selectedPaths.Add(_allJsonEntries[si].fullPath);
                    }
                    RebuildPreviewEntries();
                }
                else
                {
                    // 단일 클릭: 기존 해제 후 선택
                    _selectedPaths.Clear();
                    _selectedPaths.Add(entry.fullPath);
                    _lastClickedEntry = entry;
                    _hasLastClicked = true;
                    RebuildPreviewEntries();
                }

                ev.Use();
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
            string info;
            if (_selectedPaths.Count == 0)
                info = "JSON 항목을 선택하세요";
            else if (_selectedPaths.Count == 1)
            {
                string name = _entries.Count > 0 ? _entries[0].jsonName : "";
                info = $"선택: {name}";
            }
            else
                info = $"선택: {_selectedPaths.Count}개 — 이름 라벨 클릭으로 개별 생성";

            GUILayout.Label(info, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            // 배경색
            _bgColor = EditorGUILayout.ColorField(_bgColor, GUILayout.Width(40));

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
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
