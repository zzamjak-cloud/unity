using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using CAT.HierarchyUtility;

namespace CAT.VFX.Editor
{
    /// <summary>
    /// 하이어라키 창 상단에 VFX 프리팹 로드 드롭다운을 추가하는 모듈.
    /// "Use UI" 체크 시 CatUIParticle로 자동 래핑하여 UI 이펙트로 즉시 사용 가능.
    /// UIOrder = 18 → UI(19), Presets(20) 왼쪽에 배치.
    /// </summary>
    public class HierarchyVFXModule : IHierarchyToolModule
    {
        private const string PrefKeyTargetFolder = "HierarchyVFX_TargetFolder";
        private const string PrefKeyUseUI = "HierarchyVFX_UseUI";
        private const string DefaultTargetFolder = "VFX_Prefabs";

        // 기존 버튼 레이아웃 계산용
        private const float PresetButtonWidth = 60f;
        private const float PresetButtonMargin = 4f;
        private const float UIMakerButtonWidth = 40f;
        private const float UIMakerButtonGap = 2f;
        private const float ButtonWidth = 44f;
        private const float ButtonGap = 2f;
        private const float CheckboxWidth = 18f;

        public string ModuleName => "HierarchyVFX";
        public int UIOrder => 18;

        private string _targetFolderName;
        private bool _useUI;
        private GUIContent _buttonContent;
        private GUIContent _checkboxContent;
        private GUIStyle _checkboxStyle;

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            _targetFolderName = EditorPrefs.GetString(PrefKeyTargetFolder, DefaultTargetFolder);
            _useUI = EditorPrefs.GetBool(PrefKeyUseUI, false);
            _buttonContent = new GUIContent("VFX");
            _checkboxContent = new GUIContent("", "Use UI: CatUIParticle로 래핑하여 UI 이펙트로 생성");
        }

        public void InitUI(VisualElement container) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void OnHierarchyChanged() { }
        public void Dispose() { }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                DrawButtons();
            }
            else if (selectionRect.y < 20 && selectionRect.x < 50)
            {
                DrawButtons();
            }
        }

        private void DrawButtons()
        {
            // 기존 버튼들의 왼쪽에 배치: Presets(60) + gap + UI(40) + gap + [checkbox][VFX]
            float presetX = EditorGUIUtility.currentViewWidth - PresetButtonWidth - PresetButtonMargin;
            float uiMakerX = presetX - UIMakerButtonWidth - UIMakerButtonGap;
            float vfxButtonX = uiMakerX - ButtonWidth - ButtonGap;
            float checkboxX = vfxButtonX - CheckboxWidth;

            // Use UI 체크박스
            Rect checkboxRect = new Rect(checkboxX, 2, CheckboxWidth, 16f);

            if (_checkboxStyle == null)
            {
                _checkboxStyle = new GUIStyle(EditorStyles.toggle)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0)
                };
            }

            EditorGUI.BeginChangeCheck();
            _useUI = GUI.Toggle(checkboxRect, _useUI, _checkboxContent, _checkboxStyle);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PrefKeyUseUI, _useUI);
            }

            // VFX 드롭다운 버튼
            Rect buttonRect = new Rect(vfxButtonX, 0, ButtonWidth, 20f);

            // 우클릭: 폴더 이름 변경
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1
                && buttonRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                ShowContextMenu();
                return;
            }

            if (EditorGUI.DropdownButton(buttonRect, _buttonContent, FocusType.Passive))
            {
                var dropdown = new VFXPrefabDropdown(
                    new AdvancedDropdownState(),
                    _targetFolderName,
                    _useUI,
                    OnPrefabSelected);
                dropdown.Show(buttonRect);
            }
        }

        // 우클릭 컨텍스트 메뉴
        private void ShowContextMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("폴더 이름 변경..."), false, () =>
            {
                FolderNameInputWindow.Show(_targetFolderName, newName =>
                {
                    if (!string.IsNullOrEmpty(newName) && newName != _targetFolderName)
                    {
                        _targetFolderName = newName;
                        EditorPrefs.SetString(PrefKeyTargetFolder, _targetFolderName);
                        _buttonContent = new GUIContent("VFX");
                    }
                });
            });
            menu.AddItem(new GUIContent($"초기화 ({DefaultTargetFolder})"), false, () =>
            {
                _targetFolderName = DefaultTargetFolder;
                EditorPrefs.SetString(PrefKeyTargetFolder, _targetFolderName);
                _buttonContent = new GUIContent("VFX");
            });
            menu.ShowAsContext();
        }

        // 프리팹 선택 콜백
        private static void OnPrefabSelected(string path, bool useUI)
        {
            if (string.IsNullOrEmpty(path)) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"프리팹을 로드할 수 없습니다: {path}");
                return;
            }

            var parentObject = Selection.activeGameObject;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            // 프리팹에 이미 CatUIParticle이 포함되어 있으면 래핑 없이 일반 로드
            if (useUI && !prefab.GetComponentInChildren<CatUIParticle>(true))
            {
                CreateAsUIParticle(prefab, parentObject, prefabStage);
            }
            else
            {
                CreateAsRegularPrefab(prefab, parentObject, prefabStage);
            }
        }

        // 일반 프리팹으로 생성
        private static void CreateAsRegularPrefab(GameObject prefab, GameObject parentObject, PrefabStage prefabStage)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = prefab.name;

            SetParent(instance, parentObject, prefabStage);

            Undo.RegisterCreatedObjectUndo(instance, $"Create VFX {instance.name}");
            Selection.activeObject = instance;
        }

        // CatUIParticle로 래핑하여 UI 이펙트로 생성
        private static void CreateAsUIParticle(GameObject prefab, GameObject parentObject, PrefabStage prefabStage)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Create UI VFX {prefab.name}");

            // 1. 빈 GameObject 생성 + CatUIParticle 컴포넌트 추가
            var uiGo = new GameObject(prefab.name, typeof(RectTransform), typeof(CatUIParticle));
            var uiParticle = uiGo.GetComponent<CatUIParticle>();

            // RectTransform 초기화
            var rt = uiGo.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.zero;

            SetParent(uiGo, parentObject, prefabStage);

            Undo.RegisterCreatedObjectUndo(uiGo, $"Create UI VFX {prefab.name}");

            // 2. 프리팹을 자식으로 인스턴스
            var vfxInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            vfxInstance.transform.SetParent(uiGo.transform, false);
            vfxInstance.transform.localPosition = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(vfxInstance, $"Instantiate VFX {prefab.name}");

            // 3. 래퍼 + 자식 오브젝트 전체 레이어를 UI로 변경
            SetLayerRecursive(uiGo, LayerMask.NameToLayer("UI"));

            // 4. CatUIParticle Refresh → 자식 ParticleSystem 수집 + 머티리얼 등록
            uiParticle.RefreshParticles();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeObject = uiGo;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        private static void SetParent(GameObject instance, GameObject parentObject, PrefabStage prefabStage)
        {
            if (prefabStage != null)
            {
                if (parentObject != null && parentObject.scene == prefabStage.scene)
                    instance.transform.SetParent(parentObject.transform, false);
                else
                    instance.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
            else if (parentObject != null)
            {
                instance.transform.SetParent(parentObject.transform, false);
            }
        }

        // --- 폴더 이름 입력 팝업 ---
        private class FolderNameInputWindow : EditorWindow
        {
            private string _folderName;
            private Action<string> _onConfirm;
            private bool _focused;

            public static void Show(string currentName, Action<string> onConfirm)
            {
                var window = CreateInstance<FolderNameInputWindow>();
                window.titleContent = new GUIContent("VFX 폴더 이름 변경");
                window._folderName = currentName;
                window._onConfirm = onConfirm;
                window._focused = false;

                var size = new Vector2(300, 60);
                window.minSize = size;
                window.maxSize = size;
                window.ShowUtility();
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("대상 폴더 이름:");

                GUI.SetNextControlName("FolderNameField");
                _folderName = EditorGUILayout.TextField(_folderName);

                if (!_focused)
                {
                    EditorGUI.FocusTextInControl("FolderNameField");
                    _focused = true;
                }

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    _onConfirm?.Invoke(_folderName);
                    Close();
                    return;
                }

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    Close();
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("확인"))
                {
                    _onConfirm?.Invoke(_folderName);
                    Close();
                }
                if (GUILayout.Button("취소"))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // --- VFX 프리팹 드롭다운 ---
        private class VFXPrefabDropdown : AdvancedDropdown
        {
            private readonly Action<string, bool> _onItemSelected;
            private readonly string _targetFolderName;
            private readonly bool _useUI;
            private readonly Dictionary<int, string> _itemPaths = new Dictionary<int, string>();

            public VFXPrefabDropdown(AdvancedDropdownState state, string targetFolderName, bool useUI,
                Action<string, bool> onItemSelected) : base(state)
            {
                _onItemSelected = onItemSelected;
                _targetFolderName = targetFolderName;
                _useUI = useUI;
                minimumSize = new Vector2(210, 500);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem(_useUI ? "VFX (UI Mode)" : "VFX");
                _itemPaths.Clear();

                // 프로젝트 전체에서 대상 폴더 탐색
                string[] allFolders = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/") && Directory.Exists(p))
                    .ToArray();

                var targetFolders = allFolders
                    .Where(p => Path.GetFileName(p) == _targetFolderName)
                    .ToArray();

                if (targetFolders.Length == 0)
                {
                    var noFolder = new AdvancedDropdownItem($"폴더 없음: {_targetFolderName}");
                    noFolder.AddChild(new AdvancedDropdownItem($"'{_targetFolderName}' 폴더를 찾을 수 없습니다")
                        { enabled = false });
                    root.AddChild(noFolder);
                    return root;
                }

                // 모든 대상 폴더에서 프리팹 수집
                var allPrefabPaths = new List<string>();
                foreach (string targetFolder in targetFolders)
                {
                    string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
                    foreach (string guid in guids)
                        allPrefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }

                if (allPrefabPaths.Count == 0)
                {
                    var noPrefab = new AdvancedDropdownItem($"프리팹 없음");
                    noPrefab.AddChild(new AdvancedDropdownItem($"'{_targetFolderName}' 폴더에 프리팹이 없습니다")
                        { enabled = false });
                    root.AddChild(noPrefab);
                    return root;
                }

                // 폴더별 그룹화
                var folderGroups = new Dictionary<string, List<string>>();
                foreach (string prefabPath in allPrefabPaths)
                {
                    string parentFolder = targetFolders.FirstOrDefault(f => prefabPath.StartsWith(f));
                    if (parentFolder == null) continue;

                    if (!folderGroups.ContainsKey(parentFolder))
                        folderGroups[parentFolder] = new List<string>();
                    folderGroups[parentFolder].Add(prefabPath);
                }

                int idCounter = 0;

                foreach (var folderGroup in folderGroups)
                {
                    string folderPath = folderGroup.Key;
                    var prefabPaths = folderGroup.Value;

                    foreach (string prefabPath in prefabPaths)
                    {
                        string relativePath = prefabPath.Substring(folderPath.Length + 1);
                        string[] pathParts = relativePath.Split('/');

                        AdvancedDropdownItem currentParent = root;

                        // 하위 폴더 구조를 카테고리로 생성
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            var existingChild = currentParent.children?
                                .FirstOrDefault(c => c.name == pathParts[i]);

                            if (existingChild == null)
                            {
                                var subFolder = new AdvancedDropdownItem(pathParts[i]);
                                currentParent.AddChild(subFolder);
                                currentParent = subFolder;
                            }
                            else
                            {
                                currentParent = existingChild;
                            }
                        }

                        // 프리팹 아이템 추가
                        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                        var prefabItem = new AdvancedDropdownItem(prefabName) { id = idCounter++ };
                        _itemPaths[prefabItem.id] = prefabPath;
                        currentParent.AddChild(prefabItem);
                    }
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                base.ItemSelected(item);
                if (_itemPaths.TryGetValue(item.id, out string path))
                {
                    _onItemSelected?.Invoke(path, _useUI);
                }
            }
        }
    }
}
