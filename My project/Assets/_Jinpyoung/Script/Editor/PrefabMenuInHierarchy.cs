using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    /// <summary>
    /// 하이어라키 창 상단에 프리팹 생성 메뉴를 추가하는 에디터 스크립트입니다.
    /// 지정된 폴더의 프리팹을 폴더 구조 그대로 메뉴에 표시하고, 선택 시 하이어라키에서 선택된 오브젝트의 자식으로 생성합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class PrefabMenuInHierarchy
    {
        /// <summary>
        /// 프리팹이 위치한 기본 폴더 경로입니다.
        /// </summary>
        private const string PrefabFolderPath = "Assets/_Jinpyoung/Prefab";

        private static readonly GUIContent buttonContent;

        static PrefabMenuInHierarchy()
        {
            // 에디터가 로드될 때 하이어라키 창 GUI 이벤트에 콜백 함수를 등록합니다.
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowGUI;
            buttonContent = new GUIContent(" ▼ 프리팹 추가", "지정된 폴더의 프리팹을 생성합니다.");
        }

        private static void OnHierarchyWindowGUI(int instanceID, Rect selectionRect)
        {
            // 하이어라키 창의 최상단에만 버튼을 그리기 위해, y 좌표가 매우 작은 경우(보통 첫 번째 아이템)에만 실행합니다.
            // 검색 결과가 없을 때도 그려지도록 조건을 조정합니다.
            if (selectionRect.y < 20 && selectionRect.x < 50)
            {
                // 버튼이 그려질 위치와 크기를 설정합니다.
                // EditorGUIUtility.currentViewWidth를 사용하여 창 크기가 변해도 너비를 맞춥니다.
                Rect buttonRect = new Rect(selectionRect.x, 0, EditorGUIUtility.currentViewWidth, 20f);

                // 드롭다운 버튼을 그립니다.
                if (EditorGUI.DropdownButton(buttonRect, buttonContent, FocusType.Passive))
                {
                    // 버튼이 클릭되면 프리팹 메뉴를 생성하고 표시하는 함수를 호출합니다.
                    ShowPrefabMenu();
                }
            }
        }

        /// <summary>
        /// 프리팹 목록으로 GenericMenu를 생성하고 표시합니다.
        /// </summary>
        private static void ShowPrefabMenu()
        {
            GenericMenu menu = new GenericMenu();

            if (!Directory.Exists(PrefabFolderPath))
            {
                menu.AddDisabledItem(new GUIContent($"폴더를 찾을 수 없습니다: {PrefabFolderPath}"));
                menu.ShowAsContext();
                return;
            }

            // 지정된 폴더 및 하위 폴더에서 모든 프리팹 에셋의 GUID를 찾습니다.
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolderPath });

            if (prefabGuids.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("폴더에 프리팹이 없습니다."));
            }
            else
            {
                // 각 프리팹에 대해 메뉴 아이템을 추가합니다.
                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    
                    // 메뉴에 표시될 이름 (폴더 경로 유지)
                    // "Assets/_Jinpyoung/Prefab/Characters/Player.prefab" -> "Characters/Player"
                    string menuPath = path.Substring(PrefabFolderPath.Length + 1).Replace(".prefab", "");

                    // 메뉴 아이템을 추가합니다.
                    // 콜백 함수에 프리팹의 전체 경로를 유저 데이터로 전달합니다.
                    menu.AddItem(new GUIContent(menuPath), false, OnPrefabSelected, path);
                }
            }

            // 생성된 메뉴를 현재 마우스 위치에 표시합니다.
            menu.ShowAsContext();
        }

        /// <summary>
        /// 메뉴에서 프리팹이 선택되었을 때 호출되는 콜백 함수입니다.
        /// </summary>
        /// <param name="userData">메뉴 아이템에서 전달된 프리팹의 전체 에셋 경로</param>
        private static void OnPrefabSelected(object userData)
        {
            string path = userData as string;
            if (string.IsNullOrEmpty(path)) return;

            // 경로를 이용해 프리팹 에셋을 로드합니다.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"프리팹을 로드할 수 없습니다: {path}");
                return;
            }

            // 현재 하이어라키에서 선택된 게임오브젝트를 부모로 설정합니다.
            GameObject parentObject = Selection.activeGameObject;

            // PrefabUtility.InstantiatePrefab을 사용해 프리팹 인스턴스를 생성합니다.
            // 이는 프리팹 연결을 유지하는 가장 좋은 방법입니다.
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            // 생성된 오브젝트의 이름을 프리팹 이름과 동일하게 설정
            instance.name = prefab.name;

            // 부모가 선택되어 있다면 자식으로 설정합니다.
            if (parentObject != null)
            {
                instance.transform.SetParent(parentObject.transform, false);
            }

            // Undo/Redo 기능을 위해 생성된 오브젝트를 등록합니다.
            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            
            // 새로 생성된 오브젝트를 선택 상태로 만듭니다.
            Selection.activeObject = instance;
        }
    }
}