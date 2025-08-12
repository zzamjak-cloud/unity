using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
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
        private const string PrefabFolderPath = "Assets/_Jinpyoung/Prefab"; // 프리팹이 저장된 폴더 경로
        private static readonly GUIContent buttonContent;

        static PrefabMenuInHierarchy()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowGUI;
            buttonContent = new GUIContent(" ▼ 프리팹 추가", "지정된 폴더의 프리팹을 생성합니다. (검색 및 스크롤 가능)");
        }

        private static void OnHierarchyWindowGUI(int instanceID, Rect selectionRect)
        {
            if (selectionRect.y < 20 && selectionRect.x < 50)
            {
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

            GameObject parentObject = Selection.activeGameObject;
            if (parentObject != null)
            {
                instance.transform.SetParent(parentObject.transform, false);
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
            /// </summary>
            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Prefabs");
                _itemPaths.Clear();

                if (!Directory.Exists(PrefabFolderPath))
                {
                    root.AddChild(new AdvancedDropdownItem("폴더 없음: " + PrefabFolderPath) { enabled = false });
                    return root;
                }

                // 모든 프리팹 에셋의 GUID를 찾습니다.
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolderPath });

                if (prefabGuids.Length == 0)
                {
                    root.AddChild(new AdvancedDropdownItem("프리팹 없음") { enabled = false });
                    return root;
                }

                var itemMap = new Dictionary<string, AdvancedDropdownItem>();
                int idCounter = 0;

                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string menuPath = path.Substring(PrefabFolderPath.Length + 1);
                    string[] parts = menuPath.Split('/');

                    AdvancedDropdownItem currentParent = root;
                    string currentPath = "";

                    // 폴더 구조를 만듭니다.
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        currentPath += parts[i];
                        if (!itemMap.ContainsKey(currentPath))
                        {
                            var folderItem = new AdvancedDropdownItem(parts[i]);
                            itemMap[currentPath] = folderItem;
                            currentParent.AddChild(folderItem);
                        }
                        currentParent = itemMap[currentPath];
                        currentPath += "/";
                    }

                    // 최종 프리팹 아이템을 추가합니다.
                    string prefabName = Path.GetFileNameWithoutExtension(path);
                    var prefabItem = new AdvancedDropdownItem(prefabName)
                    {
                        // 각 아이템에 고유 ID를 부여합니다.
                        id = idCounter++
                    };
                    
                    // ID에 해당하는 실제 에셋 경로를 맵에 저장해 둡니다.
                    _itemPaths[prefabItem.id] = path;
                    currentParent.AddChild(prefabItem);
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