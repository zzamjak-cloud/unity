#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CAT.Utility
{
    /// <summary>
    /// 메인 하이어라키와 프리팹 편집 모드 양쪽 모두에서 참조 관계 아이콘을 표시해주는 에디터 스크립트입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyMarker
    {
        private struct ParentInfo
        {
            public int id;
            public bool isPrefabRoot;
        }

        private static readonly Dictionary<int, List<ParentInfo>> childToParentMap = new Dictionary<int, List<ParentInfo>>();
        private static readonly Texture2D defaultIcon = EditorGUIUtility.IconContent("sv_icon_dot1_pix16_gizmo").image as Texture2D;
        private static readonly Texture2D prefabRootIcon = EditorGUIUtility.IconContent("sv_icon_dot3_pix16_gizmo").image as Texture2D;

        static HierarchyMarker()
        {
            // 메인 하이어라키 및 프리팹 하이어라키의 UI 그리기 이벤트에 함수를 등록합니다.
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyItemGUI;

            // === 변경점 1: 이벤트 구독 방식 변경 ===
            // 메인 씬의 하이어라키가 변경될 때 캐시 업데이트
            EditorApplication.hierarchyChanged += UpdateMarkedObjectsCache;
            // 프리팹 편집 모드에 들어가거나 나올 때 캐시 업데이트
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;

            // 에디터 로드 시 최초 캐시 업데이트
            UpdateMarkedObjectsCache();
        }

        // 프리팹 편집 모드 진입 시 호출
        private static void OnPrefabStageOpened(PrefabStage stage) => UpdateMarkedObjectsCache();

        // 프리팹 편집 모드 종료 시 호출
        private static void OnPrefabStageClosing(PrefabStage stage) => UpdateMarkedObjectsCache();

        private static void UpdateMarkedObjectsCache()
        {
            childToParentMap.Clear();

            // === 변경점 2: 현재 활성화된 씬의 스크립트만 가져오도록 로직 수정 ===
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            IEnumerable<MonoBehaviour> scriptsToScan;

            if (currentPrefabStage != null)
            {
                // 현재 프리팹 편집 모드일 경우, 프리팹 씬 내부의 오브젝트만 가져옵니다.
                scriptsToScan = currentPrefabStage.scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }
            else
            {
                // 메인 씬일 경우, 로드된 모든 씬의 오브젝트를 가져옵니다.
                scriptsToScan = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Where(s => s.isLoaded)
                    .SelectMany(s => s.GetRootGameObjects())
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }

            foreach (var script in scriptsToScan)
            {
                if (script == null) continue;

                GameObject parentObject = script.gameObject;
                int parentID = parentObject.GetInstanceID();

                // 프리팹의 루트인지 확인. 프리팹 편집 모드에서는 GetNearestPrefabInstanceRoot 대신 isInstantiatedPrefab 필터를 사용합니다.
                bool isParentPrefabRoot = currentPrefabStage != null
                    ? parentObject.transform.parent == currentPrefabStage.prefabContentsRoot.transform
                    : PrefabUtility.IsPartOfPrefabInstance(parentObject) && PrefabUtility.GetNearestPrefabInstanceRoot(parentObject) == parentObject;

                var parentInfo = new ParentInfo { id = parentID, isPrefabRoot = isParentPrefabRoot };

                FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (typeof(GameObject).IsAssignableFrom(field.FieldType) || typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        object value = field.GetValue(script);
                        if (value == null) continue;

                        GameObject referencedObject = null;
                        if (value is GameObject go && go != null) referencedObject = go;
                        else if (value is Component component && component != null) referencedObject = component.gameObject;

                        if (referencedObject != null)
                        {
                            int childID = referencedObject.GetInstanceID();
                            if (parentID == childID) continue;

                            if (!childToParentMap.ContainsKey(childID))
                            {
                                childToParentMap[childID] = new List<ParentInfo>();
                            }
                            childToParentMap[childID].Add(parentInfo);
                        }
                    }
                }
            }

            // 하이어라키 창을 강제로 다시 그리도록 요청
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void HandleHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (childToParentMap.TryGetValue(instanceID, out List<ParentInfo> parentInfos))
            {
                bool hasPrefabRootParent = parentInfos.Any(p => p.isPrefabRoot);
                Texture2D iconToDraw = hasPrefabRootParent ? prefabRootIcon : defaultIcon;

                Rect iconRect = new Rect(selectionRect.x + selectionRect.width - 20f, selectionRect.y, 10f, 10f);
                if (iconToDraw != null)
                {
                    GUI.DrawTexture(iconRect, iconToDraw);
                }

                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && iconRect.Contains(currentEvent.mousePosition))
                {
                    currentEvent.Use();
                    ParentInfo targetParent = hasPrefabRootParent ? parentInfos.First(p => p.isPrefabRoot) : parentInfos[0];
                    var parentObject = EditorUtility.InstanceIDToObject(targetParent.id);
                    if (parentObject != null)
                    {
                        EditorGUIUtility.PingObject(parentObject);
                    }
                }
            }
        }
    }
#endif
}