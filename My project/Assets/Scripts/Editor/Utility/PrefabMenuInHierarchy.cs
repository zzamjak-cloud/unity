using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAT.Utility
{
    /// <summary>
    /// 하이어라키 창 상단에 프리팹 생성 메뉴를 추가하는 에디터 스크립트입니다.
    /// AdvancedDropdown을 사용하여 마우스 스크롤 및 검색 기능을 지원합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class PrefabMenuInHierarchy
    {
        private static string _targetFolderName = "Presets"; // 찾을 폴더 이름 (사용자가 변경 가능)
        private static readonly GUIContent buttonContent;

        static PrefabMenuInHierarchy()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowGUI;
            buttonContent = new GUIContent(" ▼ 프리셋 추가", "지정된 폴더의 프리팹을 생성합니다. (검색 및 스크롤 가능)");
        }

        /// <summary>
        /// 찾을 폴더 이름을 설정합니다. 프로젝트 전체에서 해당 이름의 모든 폴더를 찾아 프리팹을 수집합니다.
        /// </summary>
        /// <param name="folderName">찾을 폴더 이름 (예: "Presets", "Templates", "Prefabs" 등)</param>
        public static void SetTargetFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                Debug.LogWarning("폴더 이름은 비어있을 수 없습니다. 기본값 'Presets'을 사용합니다.");
                return;
            }
            
            _targetFolderName = folderName;
            Debug.Log($"대상 폴더 이름이 '{folderName}'으로 설정되었습니다. 프로젝트 전체에서 '{folderName}' 폴더를 찾습니다.");
        }

        /// <summary>
        /// 현재 설정된 대상 폴더 이름을 반환합니다.
        /// </summary>
        public static string GetTargetFolderName()
        {
            return _targetFolderName;
        }

        private static void OnHierarchyWindowGUI(int instanceID, Rect selectionRect)
        {
            // 프리팹 편집 모드가 아닌 경우에만 기존 로직 실행
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                // 프리팹 편집 모드에서는 항상 메뉴를 표시
                const float buttonWidth = 120f;
                float buttonX = EditorGUIUtility.currentViewWidth - buttonWidth - 4f;
                Rect buttonRect = new Rect(buttonX, 0, buttonWidth, 20f);

                if (EditorGUI.DropdownButton(buttonRect, buttonContent, FocusType.Passive))
                {
                    // AdvancedDropdown 인스턴스를 생성하고, 아이템 선택 시 실행될 콜백 함수를 넘겨줍니다.
                    var dropdown = new PrefabDropdown(new AdvancedDropdownState(), OnPrefabSelected);
                    dropdown.Show(buttonRect);
                }
            }
            else if (selectionRect.y < 20 && selectionRect.x < 50)
            {
                // 일반 씬에서는 기존 로직대로 실행
                const float buttonWidth = 120f;
                float buttonX = EditorGUIUtility.currentViewWidth - buttonWidth - 4f;
                Rect buttonRect = new Rect(buttonX, 0, buttonWidth, 20f);

                if (EditorGUI.DropdownButton(buttonRect, buttonContent, FocusType.Passive))
                {
                    // AdvancedDropdown 인스턴스를 생성하고, 아이템 선택 시 실행될 콜백 함수를 넘겨줍니다.
                    var dropdown = new PrefabDropdown(new AdvancedDropdownState(), OnPrefabSelected);
                    dropdown.Show(buttonRect);
                }
            }
        }


        /// <summary>
        /// 메뉴에서 프리팹이 선택되었을 때 호출되는 콜백 함수입니다.
        /// </summary>
        private static void OnPrefabSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"프리팹을 로드할 수 없습니다: {path}");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = prefab.name;

            // 프리팹 편집 모드인지 확인
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // 프리팹 편집 모드에서는 프리팹의 루트를 부모로 설정
                instance.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
            else
            {
                // 일반 씬에서는 선택된 오브젝트를 부모로 설정
                GameObject parentObject = Selection.activeGameObject;
                if (parentObject != null)
                {
                    instance.transform.SetParent(parentObject.transform, false);
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        /// <summary>
        /// 프리팹 폴더 구조를 기반으로 계층적인 드롭다운 메뉴를 생성하는 클래스
        /// </summary>
        private class PrefabDropdown : AdvancedDropdown
        {
            // 아이템 선택 시 호출될 콜백 함수
            private readonly Action<string> _onItemSelected;
            
            // 각 아이템의 전체 경로를 저장하기 위한 맵
            private readonly Dictionary<int, string> _itemPaths = new Dictionary<int, string>();

            public PrefabDropdown(AdvancedDropdownState state, Action<string> onItemSelected) : base(state)
            {
                _onItemSelected = onItemSelected;
                // 드롭다운의 최소 크기를 설정하여 너무 작게 표시되지 않도록 합니다.
                minimumSize = new Vector2(300, 1000);
            }

            /// <summary>
            /// 드롭다운의 루트 아이템과 모든 자식 아이템들을 구성합니다.
            /// 프로젝트 전체에서 특정 이름의 폴더들을 찾아 그 하위의 모든 프리팹을 수집합니다.
            /// </summary>
            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Prefabs");
                _itemPaths.Clear();

                // 프로젝트 전체에서 특정 이름의 폴더들을 찾습니다.
                string[] allFolders = AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.StartsWith("Assets/") && Directory.Exists(path))
                    .ToArray();

                var targetFolders = allFolders
                    .Where(folderPath => Path.GetFileName(folderPath) == _targetFolderName)
                    .ToArray();

                if (targetFolders.Length == 0)
                {
                    var noFolderItem = new AdvancedDropdownItem($"폴더 없음: {_targetFolderName}");
                    noFolderItem.AddChild(new AdvancedDropdownItem($"프로젝트에서 '{_targetFolderName}' 이름의 폴더를 찾을 수 없습니다") { enabled = false });
                    noFolderItem.AddChild(new AdvancedDropdownItem($"폴더를 생성하거나 이름을 변경하세요") { enabled = false });
                    root.AddChild(noFolderItem);
                    return root;
                }

                // 모든 대상 폴더에서 프리팹을 수집합니다.
                var allPrefabPaths = new List<string>();
                foreach (string targetFolder in targetFolders)
                {
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
                    foreach (string guid in prefabGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        allPrefabPaths.Add(path);
                    }
                }

                if (allPrefabPaths.Count == 0)
                {
                    var noPrefabItem = new AdvancedDropdownItem($"프리팹 없음: {_targetFolderName}");
                    noPrefabItem.AddChild(new AdvancedDropdownItem($"'{_targetFolderName}' 폴더들에서 프리팹을 찾을 수 없습니다") { enabled = false });
                    noPrefabItem.AddChild(new AdvancedDropdownItem($"프리팹을 추가하세요") { enabled = false });
                    root.AddChild(noPrefabItem);
                    return root;
                }

                // 폴더별로 그룹화하여 메뉴를 구성합니다.
                var folderGroups = new Dictionary<string, List<string>>();
                foreach (string prefabPath in allPrefabPaths)
                {
                    // 프리팹이 속한 대상 폴더를 찾습니다.
                    string parentFolder = targetFolders.FirstOrDefault(folder => prefabPath.StartsWith(folder));
                    if (parentFolder != null)
                    {
                        if (!folderGroups.ContainsKey(parentFolder))
                        {
                            folderGroups[parentFolder] = new List<string>();
                        }
                        folderGroups[parentFolder].Add(prefabPath);
                    }
                }

                int idCounter = 0;

                // 각 폴더 그룹별로 메뉴를 구성합니다.
                foreach (var folderGroup in folderGroups)
                {
                    string folderPath = folderGroup.Key;
                    List<string> prefabPaths = folderGroup.Value;
                    
                    // 각 프리팹을 직접 루트에 추가 (최상위 Presets 폴더는 표시하지 않음)
                    foreach (string prefabPath in prefabPaths)
                    {
                        string relativePath = prefabPath.Substring(folderPath.Length + 1);
                        string[] pathParts = relativePath.Split('/');
                        
                        AdvancedDropdownItem currentParent = root;
                        string currentPath = "";
                        
                        // 하위 폴더 구조를 만듭니다.
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            currentPath += pathParts[i];
                            var existingChild = currentParent.children?.FirstOrDefault(child => child.name == pathParts[i]);
                            
                            if (existingChild == null)
                            {
                                var subFolderItem = new AdvancedDropdownItem(pathParts[i]);
                                currentParent.AddChild(subFolderItem);
                                currentParent = subFolderItem;
                            }
                            else
                            {
                                currentParent = existingChild;
                            }
                            currentPath += "/";
                        }
                        
                        // 최종 프리팹 아이템을 추가합니다.
                        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                        var prefabItem = new AdvancedDropdownItem(prefabName)
                        {
                            id = idCounter++
                        };
                        
                        _itemPaths[prefabItem.id] = prefabPath;
                        currentParent.AddChild(prefabItem);
                    }
                }

                return root;
            }

            /// <summary>
            /// 사용자가 드롭다운에서 아이템을 선택했을 때 호출됩니다.
            /// </summary>
            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                base.ItemSelected(item);
                // 저장해 둔 맵에서 선택된 아이템의 경로를 찾아 콜백을 실행합니다.
                if (_itemPaths.TryGetValue(item.id, out string path))
                {
                    _onItemSelected?.Invoke(path);
                }
            }
        }
    }
}