using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CAT.VFX.Editor
{
    /// <summary>
    /// VFX 프리팹 실시간 미리보기 윈도우.
    /// HierarchyVFXModule의 폴더 경로를 공유하며, Project 뷰에서 Spacebar로 토글.
    /// 카테고리(폴더) 탐색 + 파티클 바운드 기반 자동 크기 조정.
    /// </summary>
    public class VFXPreviewer : EditorWindow
    {
        // ── 프리뷰 엔트리 ──────────────────────────────────────────────────
        private class PreviewEntry
        {
            public GameObject prefab;
            public GameObject previewInstance;
            public bool isUI;
            public List<ParticleSystem> rootPS = new List<ParticleSystem>();
            public float maxDuration;
            public Vector3 offset;
            public float autoScale = 1f;
        }

        // ── 카테고리 노드 (폴더 트리) ──────────────────────────────────────
        private class CategoryNode
        {
            public string name;
            public string fullPath;
            public CategoryNode parent;
            public List<CategoryNode> children = new List<CategoryNode>();
            public List<GameObject> prefabs = new List<GameObject>();
        }

        // HierarchyVFXModule과 동일한 EditorPrefs 키 공유
        private const string PrefKeyTargetFolder = "HierarchyVFX_TargetFolder";
        private const string PrefKeyUseUI = "HierarchyVFX_UseUI";
        private const string PrefKeyBgColor = "VFXPreviewer_BgColor";
        private const string DefaultTargetFolder = "VFX_Prefabs";

        private PreviewRenderUtility _previewUtility;
        private readonly List<PreviewEntry> _entries = new List<PreviewEntry>();
        private int _previewLayer;
        private Canvas _sharedCanvas;

        // 선택
        private readonly HashSet<GameObject> _selectedPrefabs = new HashSet<GameObject>();
        private GameObject _lastClickedPrefab;

        // 배치
        private float _targetCellSize = 100f;
        private float _cellPadding = 1.15f;
        private int _gridColumns = 5;

        // 시뮬레이션
        private double _lastUpdateTime;
        private float _currentTime;
        private float _maxDuration = 5f;
        private bool _isPaused;

        // 카메라
        private Vector3 _cameraPivot;
        private float _orthoSize = 5f;
        private static readonly Color DefaultBgColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private Color _bgColor;
        private float _listWidth = 230f;
        private bool _isResizing;

        private const float MinOrthoSize = 0.01f;
        private const float MaxOrthoSize = 5000f;
        private const float BottomBarHeight = 35f;
        private const float ListItemHeight = 20f;
        private const float CategoryItemHeight = 22f;

        // 카테고리 / Use UI
        private string _targetFolderName;
        private bool _useUI;
        private CategoryNode _rootCategory;
        private CategoryNode _currentCategory;
        private List<GameObject> _allPrefabs = new List<GameObject>();
        private Vector2 _listScrollPos;
        private string _searchString = "";
        private GUIStyle _centeredStyle;
        private GUIStyle _listButtonStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _backButtonStyle;

        // 핑
        private GameObject _pingPrefab;
        private double _pingStartTime = -1;
        private const double PingDuration = 0.3;
        private const double PingListDuration = 2.0;

        // 자동 스케일용 — 파티클 시뮬레이션 후 바운드 측정 시간
        private const float BoundsCheckTime = 0.5f;
        private bool _boundsChecked;

        private bool _showDebug;
        private Vector2 _debugScrollPos;
        private readonly StringBuilder _diagSb = new StringBuilder();

        private static VFXPreviewer _instance;

        // ── Spacebar 토글 (Project 뷰) ─────────────────────────────────────
        [InitializeOnLoadMethod]
        private static void RegisterProjectViewKey()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowGUI;
        }

        private static void OnProjectWindowGUI(string guid, Rect selectionRect)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Space) return;
            e.Use();

            if (_instance != null)
            {
                _instance.Close();
                _instance = null;
                return;
            }

            _instance = GetWindow<VFXPreviewer>(true, "VFX Live Preview", false);
            _instance.RefreshAll();

            // 선택된 프리팹 자동 선택
            _instance._selectedPrefabs.Clear();
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                var go = Selection.objects[i] as GameObject;
                if (go != null && EditorUtility.IsPersistent(go) && _instance._allPrefabs.Contains(go))
                {
                    _instance._selectedPrefabs.Add(go);
                    _instance._lastClickedPrefab = go;
                }
            }

            if (_instance._selectedPrefabs.Count > 0)
                _instance.RebuildAllEntries();

            _instance.Repaint();
        }

        // ── 생명주기 ────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _instance = this;
            RefreshAll();

            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.camera.orthographic = true;
                _previewUtility.camera.nearClipPlane = 0.1f;
                _previewUtility.camera.farClipPlane = 10000f;
            }

            _lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
            CleanupAllEntries();
            if (_previewUtility != null) _previewUtility.Cleanup();
            _previewUtility = null;
            if (_instance == this) _instance = null;
        }

        private void RefreshAll()
        {
            _targetFolderName = EditorPrefs.GetString(PrefKeyTargetFolder, DefaultTargetFolder);
            _useUI = EditorPrefs.GetBool(PrefKeyUseUI, false);
            LoadBgColor();
            BuildCategoryTree();
        }

        // ── 배경색 저장/복원 ────────────────────────────────────────────────
        private void LoadBgColor()
        {
            string hex = EditorPrefs.GetString(PrefKeyBgColor, "");
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c))
                _bgColor = c;
            else
                _bgColor = DefaultBgColor;
        }

        private void SaveBgColor()
        {
            EditorPrefs.SetString(PrefKeyBgColor, "#" + ColorUtility.ToHtmlStringRGBA(_bgColor));
        }

        // ── 정리 ────────────────────────────────────────────────────────────
        private void CleanupAllEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].previewInstance != null)
                    DestroyImmediate(_entries[i].previewInstance);
            _entries.Clear();
            if (_sharedCanvas != null) { DestroyImmediate(_sharedCanvas.gameObject); _sharedCanvas = null; }
            _previewLayer = 0;
        }

        // ── 업데이트 ────────────────────────────────────────────────────────
        private void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_pingPrefab != null && (now - _pingStartTime) >= PingListDuration)
                _pingPrefab = null;

            if (_entries.Count == 0 || _isPaused)
            {
                _lastUpdateTime = now;
                if (_pingPrefab != null) Repaint();
                return;
            }

            float dt = Mathf.Min((float)(now - _lastUpdateTime), 0.033f);
            _lastUpdateTime = now;
            _currentTime += dt;

            // 측정 단계: 시뮬레이션만 진행하고 렌더링하지 않음
            if (!_boundsChecked)
            {
                for (int i = 0; i < _entries.Count; i++)
                    for (int j = 0; j < _entries[i].rootPS.Count; j++)
                        if (_entries[i].rootPS[j] != null)
                            _entries[i].rootPS[j].Simulate(dt * _entries[i].rootPS[j].main.simulationSpeed, true, false, true);

                if (_currentTime >= BoundsCheckTime)
                {
                    _boundsChecked = true;
                    AutoScaleEntries();
                    AutoFitZoom();
                    RestartAllSimulations();
                    Repaint();
                }

                return;
            }

            if (_currentTime >= _maxDuration)
            {
                _currentTime = 0f;
                RestartAllSimulations();
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
                for (int j = 0; j < _entries[i].rootPS.Count; j++)
                    if (_entries[i].rootPS[j] != null)
                        _entries[i].rootPS[j].Simulate(dt * _entries[i].rootPS[j].main.simulationSpeed, true, false, true);

            Repaint();
        }

        // ── 카테고리 트리 구축 ─────────────────────────────────────────────
        private void BuildCategoryTree()
        {
            _allPrefabs.Clear();
            _rootCategory = new CategoryNode { name = _targetFolderName, fullPath = "" };
            _currentCategory = _rootCategory;

            // 대상 폴더 탐색
            var allPaths = AssetDatabase.GetAllAssetPaths();
            var targetFolders = new List<string>();
            for (int i = 0; i < allPaths.Length; i++)
            {
                if (allPaths[i].StartsWith("Assets/") && Directory.Exists(allPaths[i])
                    && Path.GetFileName(allPaths[i]) == _targetFolderName)
                    targetFolders.Add(allPaths[i]);
            }

            for (int f = 0; f < targetFolders.Count; f++)
            {
                string basePath = targetFolders[f];
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { basePath });
                for (int g = 0; g < guids.Length; g++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null || prefab.GetComponentInChildren<ParticleSystem>(true) == null)
                        continue;

                    _allPrefabs.Add(prefab);

                    // 상대 경로에서 카테고리 구조 생성
                    string relativePath = path.Substring(basePath.Length + 1);
                    string[] parts = relativePath.Split('/');

                    var node = _rootCategory;
                    for (int p = 0; p < parts.Length - 1; p++)
                    {
                        var child = FindChild(node, parts[p]);
                        if (child == null)
                        {
                            child = new CategoryNode { name = parts[p], fullPath = parts[p], parent = node };
                            node.children.Add(child);
                        }

                        node = child;
                    }

                    node.prefabs.Add(prefab);
                }
            }
        }

        private static CategoryNode FindChild(CategoryNode parent, string name)
        {
            for (int i = 0; i < parent.children.Count; i++)
                if (parent.children[i].name == name)
                    return parent.children[i];
            return null;
        }

        // ── 엔트리 재구축 ──────────────────────────────────────────────────
        private void RebuildAllEntries()
        {
            CleanupAllEntries();
            _boundsChecked = false;

            var ordered = new List<GameObject>();
            for (int i = 0; i < _allPrefabs.Count; i++)
                if (_selectedPrefabs.Contains(_allPrefabs[i]))
                    ordered.Add(_allPrefabs[i]);
            if (ordered.Count == 0) return;

            bool canvasCreated = false;

            for (int i = 0; i < ordered.Count; i++)
            {
                var prefab = ordered[i];
                bool isUI = prefab.GetComponentInChildren<CatUIParticle>(true) != null
                            || prefab.GetComponentInChildren<RectTransform>(true) != null;
                var entry = new PreviewEntry { prefab = prefab, isUI = isUI };

                if (isUI)
                {
                    if (!canvasCreated)
                    {
                        var canvasGO = new GameObject("PreviewCanvas") { hideFlags = HideFlags.HideAndDontSave };
                        _sharedCanvas = canvasGO.AddComponent<Canvas>();
                        _sharedCanvas.renderMode = RenderMode.WorldSpace;
                        _sharedCanvas.worldCamera = _previewUtility.camera;
                        canvasGO.transform.localScale = Vector3.one;
                        _previewUtility.AddSingleGO(canvasGO);
                        _previewLayer = canvasGO.layer;
                        canvasCreated = true;
                    }

                    var inst = Instantiate(prefab, _sharedCanvas.transform);
                    inst.hideFlags = HideFlags.HideAndDontSave;
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localScale = Vector3.one;
                    entry.previewInstance = inst;

                    // CatUIParticle 비활성화
                    var catUips = inst.GetComponentsInChildren<CatUIParticle>(true);
                    for (int u = 0; u < catUips.Length; u++)
                    {
                        CatUIParticleUpdater.Unregister(catUips[u]);
                        catUips[u].enabled = false;
                    }

                    // 외부 UIParticle 등 MaskableGraphic 기반 파티클 래퍼를 비활성화
                    // (ParticleSystem을 직접 렌더링하기 위해 PSR을 가로채는 모든 컴포넌트 대응)
                    DisableNonBuiltinGraphics(inst);

                    // ParticleSystemRenderer 강제 활성화
                    var renderers = inst.GetComponentsInChildren<ParticleSystemRenderer>(true);
                    for (int r = 0; r < renderers.Length; r++)
                        renderers[r].enabled = true;

                    SetLayerRecursive(inst, _previewLayer);
                }
                else
                {
                    var inst = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    inst.hideFlags = HideFlags.HideAndDontSave;
                    inst.transform.localScale = Vector3.one;
                    entry.previewInstance = inst;
                    _previewUtility.AddSingleGO(inst);
                    if (_previewLayer == 0) _previewLayer = inst.layer;
                    else SetLayerRecursive(inst, _previewLayer);
                }

                var allPS = entry.previewInstance.GetComponentsInChildren<ParticleSystem>(true);
                for (int p = 0; p < allPS.Length; p++)
                    if (IsRootPS(allPS[p], entry.previewInstance.transform.parent))
                        entry.rootPS.Add(allPS[p]);

                entry.maxDuration = 1f;
                for (int p = 0; p < allPS.Length; p++)
                {
                    float d = allPS[p].main.duration + allPS[p].main.startDelay.constantMax;
                    if (d > entry.maxDuration) entry.maxDuration = d;
                }

                _entries.Add(entry);
            }

            RecalculateOffsets();
            _maxDuration = 1f;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].maxDuration > _maxDuration)
                    _maxDuration = _entries[i].maxDuration;

            _currentTime = 0f;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            _isPaused = true;

            EditorApplication.delayCall += () =>
            {
                RestartAllSimulations();
                AutoFitZoom();
                Repaint();
            };
        }

        // ── 파티클 바운드 기반 자동 스케일 ─────────────────────────────────
        private void AutoScaleEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var bounds = CalculateParticleBounds(entry);
                if (bounds.size.sqrMagnitude < 0.001f) continue;

                float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y) * 2f;
                if (maxExtent < 0.01f) continue;

                // 셀 크기의 80%에 맞추기
                float desiredSize = _targetCellSize * 0.8f;
                entry.autoScale = desiredSize / maxExtent;

                entry.previewInstance.transform.localScale = Vector3.one * entry.autoScale;
            }
        }

        private Bounds CalculateParticleBounds(PreviewEntry entry)
        {
            var allPS = entry.previewInstance.GetComponentsInChildren<ParticleSystem>(true);
            if (allPS.Length == 0) return new Bounds();

            var bounds = new Bounds(entry.offset, Vector3.zero);
            bool initialized = false;

            for (int i = 0; i < allPS.Length; i++)
            {
                var psr = allPS[i].GetComponent<ParticleSystemRenderer>();
                if (psr == null || !psr.enabled || allPS[i].particleCount == 0) continue;

                var b = psr.bounds;
                if (!initialized)
                {
                    bounds = b;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return bounds;
        }

        // ── 격자 배치 ──────────────────────────────────────────────────────
        private void RecalculateOffsets()
        {
            float step = _targetCellSize * _cellPadding;
            for (int i = 0; i < _entries.Count; i++)
            {
                int col = i % _gridColumns;
                int row = i / _gridColumns;
                _entries[i].offset = new Vector3(col * step, -row * step, 0f);
                _entries[i].previewInstance.transform.localPosition = _entries[i].offset;
            }
        }

        // ── 시뮬레이션 ─────────────────────────────────────────────────────
        private void RestartAllSimulations()
        {
            _currentTime = 0f;
            _isPaused = false;
            for (int i = 0; i < _entries.Count; i++)
                for (int j = 0; j < _entries[i].rootPS.Count; j++)
                    if (_entries[i].rootPS[j] != null)
                        _entries[i].rootPS[j].Simulate(0, true, true, true);
        }

        private void ScrubAllSimulations(float time)
        {
            for (int i = 0; i < _entries.Count; i++)
                for (int j = 0; j < _entries[i].rootPS.Count; j++)
                    if (_entries[i].rootPS[j] != null)
                        _entries[i].rootPS[j].Simulate(time * _entries[i].rootPS[j].main.simulationSpeed, true, true, false);
        }

        // ── 카메라 자동 맞춤 ────────────────────────────────────────────────
        private void AutoFitZoom()
        {
            if (_entries.Count == 0) return;

            int cols = Mathf.Min(_entries.Count, _gridColumns);
            int rows = Mathf.CeilToInt((float)_entries.Count / _gridColumns);
            float step = _targetCellSize * _cellPadding;

            var center = new Vector3((cols - 1) * step * 0.5f, -(rows - 1) * step * 0.5f, 0f);
            var size = new Vector3(cols * step, rows * step, 1f);

            _cameraPivot = center;
            float previewW = Mathf.Max(position.width - _listWidth, 1f);
            float previewH = Mathf.Max(position.height - BottomBarHeight, 1f);
            float aspect = previewW / previewH;
            _orthoSize = Mathf.Max((size.x / aspect) / 2f, size.y / 2f) * 1.1f;
            _orthoSize = Mathf.Clamp(_orthoSize, 0.1f, 2000f);
        }

        // ── 유틸리티 ────────────────────────────────────────────────────────
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        /// <summary>
        /// CatUIParticle 이외의 MaskableGraphic 기반 파티클 래퍼(외부 UIParticle 등)를 비활성화.
        /// Unity 빌트인 UI 컴포넌트(Image, RawImage, Text 등)는 제외.
        /// 외부 패키지 미설치 시에도 에러 없이 동작 (타입 참조 없이 상속 체인으로 판정).
        /// </summary>
        private static void DisableNonBuiltinGraphics(GameObject root)
        {
            var graphics = root.GetComponentsInChildren<MaskableGraphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (g is CatUIParticle) continue;

                // Unity 빌트인 UI 컴포넌트는 건드리지 않음
                var ns = g.GetType().Namespace;
                if (ns != null && (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")))
                    continue;

                // 외부 패키지의 MaskableGraphic (UIParticle 등) → 비활성화
                var mb = g as MonoBehaviour;
                if (mb != null) mb.enabled = false;
            }
        }

        private static bool IsRootPS(ParticleSystem ps, Transform boundary)
        {
            var t = ps.transform.parent;
            while (t != null && t != boundary)
            {
                if (t.GetComponent<ParticleSystem>() != null) return false;
                t = t.parent;
            }

            return true;
        }

        // ── GUI ─────────────────────────────────────────────────────────────
        private void InitStyles()
        {
            if (_centeredStyle != null) return;
            _centeredStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _listButtonStyle = new GUIStyle(GUI.skin.label) { padding = new RectOffset(20, 0, 2, 2) };
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
        }

        private void OnGUI()
        {
            InitStyles();

            float debugW = _showDebug ? 340f : 0f;
            var listRect = new Rect(0, 0, _listWidth, position.height);
            var resizerRect = new Rect(_listWidth - 2, 0, 4, position.height);
            var previewRect = new Rect(_listWidth, 0, position.width - _listWidth - debugW, position.height - BottomBarHeight);
            var toolbarRect = new Rect(_listWidth, position.height - BottomBarHeight, position.width - _listWidth - debugW, BottomBarHeight);
            var debugRect = new Rect(position.width - debugW, 0, debugW, position.height);

            DrawCategoryList(listRect);
            HandleResizer(resizerRect);
            HandleCameraInputs(previewRect);

            if (_previewUtility != null && _entries.Count > 0)
            {
                _previewUtility.camera.backgroundColor = _bgColor;
                _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                _previewUtility.BeginPreview(previewRect, GUIStyle.none);
                _previewUtility.camera.transform.rotation = Quaternion.identity;
                _previewUtility.camera.transform.position = new Vector3(_cameraPivot.x, _cameraPivot.y, -1000f);
                _previewUtility.camera.orthographicSize = _orthoSize;
                _previewUtility.camera.aspect = previewRect.width / Mathf.Max(previewRect.height, 1f);
                _previewUtility.camera.Render();
                _previewUtility.EndAndDrawPreview(previewRect);
                DrawEntryLabels(previewRect);
            }
            else
            {
                GUI.Label(previewRect, "파티클을 선택하세요.\n(체크박스/Ctrl/Shift 복수선택)", _centeredStyle);
            }

            // 프리뷰 좌상단 Use UI 체크박스
            var useUICheckRect = new Rect(previewRect.x + 6, previewRect.y + 4, 16, 16);
            var useUILabelRect = new Rect(previewRect.x + 24, previewRect.y + 3, 50, 18);
            EditorGUI.BeginChangeCheck();
            _useUI = GUI.Toggle(useUICheckRect, _useUI, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(PrefKeyUseUI, _useUI);
            GUI.Label(useUILabelRect, "Use UI", EditorStyles.miniLabel);

            DrawBottomBar(toolbarRect);
            if (_showDebug) DrawDebugPanel(debugRect);
        }

        // ── 카테고리 리스트 패널 ────────────────────────────────────────────
        private void DrawCategoryList(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Space(4);

            // 헤더
            GUILayout.BeginHorizontal();
            GUILayout.Label("VFX Prefabs", EditorStyles.boldLabel);
            if (_selectedPrefabs.Count > 0)
            {
                var s = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
                GUILayout.Label($"({_selectedPrefabs.Count})", s);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{_targetFolderName}]", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            _searchString = EditorGUILayout.TextField(_searchString, EditorStyles.toolbarSearchField);
            GUILayout.Space(2);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
            GUILayout.Space(2);

            // 핑 상태
            bool pingActive = _pingPrefab != null &&
                              (EditorApplication.timeSinceStartup - _pingStartTime) < PingListDuration;
            float pingT2 = 0f;
            if (pingActive)
            {
                float elapsed = (float)(EditorApplication.timeSinceStartup - _pingStartTime);
                float fadeStart = (float)PingDuration;
                float fadeEnd = (float)PingListDuration;
                pingT2 = elapsed < fadeStart
                    ? SmoothStep01(elapsed / fadeStart)
                    : 1f - SmoothStep01((elapsed - fadeStart) / (fadeEnd - fadeStart));
            }

            // 스크롤 리스트
            _listScrollPos = GUILayout.BeginScrollView(_listScrollPos, GUILayout.ExpandHeight(true));

            // 검색 모드면 전체에서 필터링
            if (!string.IsNullOrEmpty(_searchString))
            {
                DrawFilteredPrefabList(pingActive, pingT2);
            }
            else
            {
                DrawCategoryNavigation(pingActive, pingT2);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(2);
            if (GUILayout.Button("선택 해제", EditorStyles.miniButton))
            {
                _selectedPrefabs.Clear();
                RebuildAllEntries();
            }

            GUILayout.Label("체크박스/Ctrl/Shift 복수선택 | Spacebar 토글", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndArea();
        }

        // ── 카테고리 네비게이션 (AdvancedDropdown 스타일) ────────────────────
        private void DrawCategoryNavigation(bool pingActive, float pingT2)
        {
            // 뒤로가기 헤더 (루트가 아닌 경우)
            if (_currentCategory != _rootCategory)
            {
                Rect backRect = EditorGUILayout.GetControlRect(false, CategoryItemHeight);
                EditorGUI.DrawRect(backRect, new Color(0.2f, 0.25f, 0.35f, 0.8f));

                string backLabel = $"< {_currentCategory.name}";
                GUI.Label(backRect, backLabel, _backButtonStyle);
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
                int totalCount = CountPrefabsRecursive(child);

                Rect catRect = EditorGUILayout.GetControlRect(false, CategoryItemHeight);
                EditorGUI.DrawRect(catRect, new Color(0.18f, 0.22f, 0.3f, 0.5f));

                string catLabel = $"{child.name}  ({totalCount})  >";
                GUI.Label(catRect, catLabel, _categoryStyle);
                EditorGUIUtility.AddCursorRect(catRect, MouseCursor.Link);

                if (Event.current.type == EventType.MouseDown && catRect.Contains(Event.current.mousePosition))
                {
                    _currentCategory = child;
                    _listScrollPos = Vector2.zero;
                    Event.current.Use();
                }
            }

            if (_currentCategory.children.Count > 0 && _currentCategory.prefabs.Count > 0)
            {
                GUILayout.Space(2);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));
                GUILayout.Space(2);
            }

            // 현재 카테고리의 프리팹
            for (int i = 0; i < _currentCategory.prefabs.Count; i++)
                DrawPrefabItem(_currentCategory.prefabs[i], pingActive, pingT2);
        }

        private void DrawFilteredPrefabList(bool pingActive, float pingT2)
        {
            var search = _searchString.ToLower();
            for (int i = 0; i < _allPrefabs.Count; i++)
                if (_allPrefabs[i].name.ToLower().Contains(search))
                    DrawPrefabItem(_allPrefabs[i], pingActive, pingT2);
        }

        private void DrawPrefabItem(GameObject prefab, bool pingActive, float pingT2)
        {
            bool isSelected = _selectedPrefabs.Contains(prefab);
            bool isPinged = pingActive && prefab == _pingPrefab;
            Rect rowRect = EditorGUILayout.GetControlRect(false, ListItemHeight);

            // 행 배경
            if (isPinged)
            {
                EditorGUI.DrawRect(rowRect, Color.Lerp(
                    new Color(0.17f, 0.36f, 0.53f, 1f),
                    new Color(1f, 0.85f, 0.2f, 1f), pingT2));
                _listButtonStyle.normal.textColor = pingT2 > 0.5f ? Color.black : Color.white;
            }
            else if (isSelected)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.17f, 0.36f, 0.53f, 1f));
                _listButtonStyle.normal.textColor = Color.white;
            }
            else
            {
                _listButtonStyle.normal.textColor = EditorStyles.label.normal.textColor;
            }

            // 체크박스 (카테고리 간 비교용)
            var checkRect = new Rect(rowRect.x + 2, rowRect.y + 2, 16, 16);
            EditorGUI.BeginChangeCheck();
            bool newChecked = GUI.Toggle(checkRect, isSelected, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                if (newChecked) _selectedPrefabs.Add(prefab);
                else _selectedPrefabs.Remove(prefab);
                _lastClickedPrefab = prefab;
                RebuildAllEntries();
            }

            // 이름 라벨 영역 (체크박스 오른쪽)
            var labelRect = new Rect(rowRect.x + 20, rowRect.y, rowRect.width - 20, rowRect.height);

            var ev = Event.current;
            if (ev.type == EventType.MouseDown && labelRect.Contains(ev.mousePosition))
            {
                if (ev.clickCount == 2)
                {
                    AssetDatabase.OpenAsset(prefab);
                }
                else if (ev.control)
                {
                    // Ctrl+클릭: 토글
                    if (_selectedPrefabs.Contains(prefab)) _selectedPrefabs.Remove(prefab);
                    else _selectedPrefabs.Add(prefab);
                    _lastClickedPrefab = prefab;
                    RebuildAllEntries();
                }
                else if (ev.shift && _lastClickedPrefab != null)
                {
                    // Shift+클릭: 범위 선택
                    int from = _allPrefabs.IndexOf(_lastClickedPrefab);
                    int to = _allPrefabs.IndexOf(prefab);
                    if (from >= 0 && to >= 0)
                    {
                        int lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
                        for (int si = lo; si <= hi; si++)
                            _selectedPrefabs.Add(_allPrefabs[si]);
                    }
                    RebuildAllEntries();
                }
                else
                {
                    // 단일 클릭
                    _selectedPrefabs.Clear();
                    _selectedPrefabs.Add(prefab);
                    _lastClickedPrefab = prefab;
                    RebuildAllEntries();
                }

                ev.Use();
            }

            if (ev.type == EventType.MouseDrag && rowRect.Contains(ev.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { prefab };
                DragAndDrop.StartDrag(prefab.name);
                ev.Use();
            }

            GUI.Label(labelRect, prefab.name, _listButtonStyle);
        }

        private static int CountPrefabsRecursive(CategoryNode node)
        {
            int count = node.prefabs.Count;
            for (int i = 0; i < node.children.Count; i++)
                count += CountPrefabsRecursive(node.children[i]);
            return count;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ── 프리뷰 라벨 ─────────────────────────────────────────────────────
        private void DrawEntryLabels(Rect previewRect)
        {
            if (_entries.Count == 0) return;

            var cam = _previewUtility.camera;
            bool pingActive = _pingPrefab != null &&
                              (EditorApplication.timeSinceStartup - _pingStartTime) < PingDuration;
            float pingT = 0f;
            if (pingActive)
            {
                float norm = (float)(EditorApplication.timeSinceStartup - _pingStartTime) / (float)PingDuration;
                pingT = 1f - Mathf.Abs(norm * 2f - 1f);
                pingT = pingT * pingT * (3f - 2f * pingT);
            }

            var baseStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };

            var ev = Event.current;
            GUI.BeginClip(previewRect);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var worldPos = entry.offset + Vector3.down * (_targetCellSize * _cellPadding * 0.4f);
                var vp = cam.WorldToViewportPoint(worldPos);
                if (vp.z < 0) continue;

                var gui = new Vector2(vp.x * previewRect.width, (1f - vp.y) * previewRect.height);
                bool isPinged = pingActive && entry.prefab == _pingPrefab;

                float scale = isPinged ? Mathf.Lerp(1f, 1.2f, pingT) : 1f;
                float w = 160f * scale, h = 18f * scale;

                // 프리뷰 영역 안쪽으로 클램핑
                float margin = 2f;
                float lx = Mathf.Clamp(gui.x - w * 0.5f, margin, previewRect.width - w - margin);
                float ly = Mathf.Clamp(gui.y - h * 0.5f, margin, previewRect.height - h - margin);
                var lr = new Rect(lx, ly, w, h);

                EditorGUI.DrawRect(lr, isPinged
                    ? Color.Lerp(new Color(0f, 0f, 0f, 0.5f), new Color(1f, 0.85f, 0.2f, 0.9f), pingT)
                    : new Color(0f, 0f, 0f, 0.5f));

                var style = new GUIStyle(baseStyle);
                style.fontSize = Mathf.RoundToInt(EditorStyles.miniLabel.fontSize * scale);
                if (isPinged) style.normal.textColor = Color.black;
                GUI.Label(lr, entry.prefab.name, style);

                EditorGUIUtility.AddCursorRect(lr, MouseCursor.Link);
                if (ev.type == EventType.MouseDown && lr.Contains(ev.mousePosition))
                {
                    _pingPrefab = entry.prefab;
                    _pingStartTime = EditorApplication.timeSinceStartup;

                    // 클릭 시 하이어라키에 프리팹 인스턴스 (Use UI 상태 반영)
                    VFXInstantiateHelper.Instantiate(entry.prefab, _useUI);

                    ev.Use();
                }
            }

            GUI.EndClip();
        }

        // ── 하단 바 / 디버그 / 입력 ─────────────────────────────────────────
        private void DrawBottomBar(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPaused ? "▶" : "II", GUILayout.Width(35))) _isPaused = !_isPaused;
            if (GUILayout.Button("↺", GUILayout.Width(35))) RestartAllSimulations();
            if (GUILayout.Button("Fit", GUILayout.Width(35))) { AutoScaleEntries(); AutoFitZoom(); }
            GUI.backgroundColor = _showDebug ? Color.yellow : Color.white;
            if (GUILayout.Button("D", GUILayout.Width(25))) _showDebug = !_showDebug;
            GUI.backgroundColor = Color.white;
            EditorGUI.BeginChangeCheck();
            _bgColor = EditorGUILayout.ColorField(_bgColor, GUILayout.Width(50));
            if (EditorGUI.EndChangeCheck()) SaveBgColor();
            EditorGUI.BeginChangeCheck();
            float newTime = GUILayout.HorizontalSlider(_currentTime, 0f, _maxDuration, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) { _currentTime = newTime; _isPaused = true; ScrubAllSimulations(_currentTime); }
            GUILayout.Label($"{_currentTime:F2}/{_maxDuration:F2}s", EditorStyles.miniLabel, GUILayout.Width(75));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawDebugPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("Debug", EditorStyles.boldLabel);
            _debugScrollPos = GUILayout.BeginScrollView(_debugScrollPos);
            _diagSb.Clear();
            _diagSb.AppendLine($"Folder: {_targetFolderName} | Total: {_allPrefabs.Count}");
            _diagSb.AppendLine($"Category: {_currentCategory?.name} | Children: {_currentCategory?.children.Count} | Prefabs: {_currentCategory?.prefabs.Count}");
            _diagSb.AppendLine($"Selected: {_selectedPrefabs.Count} | Entries: {_entries.Count}");
            for (int i = 0; i < _entries.Count; i++)
                _diagSb.AppendLine($"  [{i}] {_entries[i].prefab.name} scale={_entries[i].autoScale:F2} UI={_entries[i].isUI}");
            GUILayout.Label(_diagSb.ToString(), new GUIStyle(EditorStyles.label) { fontSize = 10, wordWrap = true });
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void HandleResizer(Rect rect)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                _isResizing = true;
            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                { _listWidth = Mathf.Clamp(_listWidth + Event.current.delta.x, 120f, position.width - 200f); Repaint(); }
                if (Event.current.type == EventType.MouseUp) _isResizing = false;
            }
        }

        private void HandleCameraInputs(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                float ps = _orthoSize / rect.height * 2f;
                _cameraPivot.x -= e.delta.x * ps;
                _cameraPivot.y += e.delta.y * ps;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _orthoSize = Mathf.Clamp(_orthoSize + e.delta.y * _orthoSize * 0.05f, MinOrthoSize, MaxOrthoSize);
                e.Use();
            }
        }
    }
}
