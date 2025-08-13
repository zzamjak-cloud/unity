#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CAT.Utility
{
    /// <summary>
    /// 메인 하이어라키와 프리팹 편집 모드 양쪽 모두에서 참조 관계 아이콘을 표시합니다.
    /// 아이콘 클릭 시, 부모 컴포넌트를 선택하고 콘솔에 참조 필드 이름을 출력합니다.
    /// ScrollView/ScrollRect 컴포넌트에도 아이콘을 표시합니다.
    /// Unity 기본 UI 컴포넌트의 참조는 제외합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyMarker
    {
        private class ParentInfo
        {
            public int parentId;
            public bool isPrefabRoot;
            public MonoBehaviour script;
            public string fieldName;
        }

        private static readonly Dictionary<int, List<ParentInfo>> childToParentMap = new Dictionary<int, List<ParentInfo>>();
        private static readonly Texture2D defaultIcon = EditorGUIUtility.IconContent("d_greenLight").image as Texture2D;
        private static readonly Texture2D prefabRootIcon = EditorGUIUtility.IconContent("d_orangeLight").image as Texture2D;
        private static readonly Texture2D scrollViewIcon = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D;

        // [수정됨] 네임스페이스 대신 제외할 '어셈블리' 목록으로 변경
        private static readonly List<string> excludedAssemblies = new List<string>
        {
            "UnityEngine.UI",
            "UnityEngine.EventSystems"
        };

        static HierarchyMarker()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyItemGUI;
            EditorApplication.hierarchyChanged += UpdateMarkedObjectsCache;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            UpdateMarkedObjectsCache();
        }

        private static void OnPrefabStageOpened(PrefabStage stage) => UpdateMarkedObjectsCache();
        private static void OnPrefabStageClosing(PrefabStage stage) => UpdateMarkedObjectsCache();

        private static void UpdateMarkedObjectsCache()
        {
            childToParentMap.Clear();
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            IEnumerable<MonoBehaviour> scriptsToScan;

            if (currentPrefabStage != null)
            {
                scriptsToScan = currentPrefabStage.scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }
            else
            {
                scriptsToScan = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
                    .SelectMany(s => s.GetRootGameObjects())
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }

            foreach (var script in scriptsToScan)
            {
                if (script == null) continue;

                // [수정됨] 네임스페이스 대신 '어셈블리' 이름으로 필터링하는 로직
                var assemblyName = script.GetType().Assembly.GetName().Name;
                if (excludedAssemblies.Contains(assemblyName))
                {
                    continue;
                }

                GameObject parentObject = script.gameObject;
                int parentID = parentObject.GetInstanceID();
                bool isParentPrefabRoot = currentPrefabStage != null
                    ? parentObject.transform.parent == currentPrefabStage.prefabContentsRoot.transform
                    : PrefabUtility.IsPartOfPrefabInstance(parentObject) && PrefabUtility.GetNearestPrefabInstanceRoot(parentObject) == parentObject;

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
                            
                            var parentInfo = new ParentInfo { parentId = parentID, isPrefabRoot = isParentPrefabRoot, script = script, fieldName = field.Name };
                            if (!childToParentMap.ContainsKey(childID))
                            {
                                childToParentMap[childID] = new List<ParentInfo>();
                            }
                            childToParentMap[childID].Add(parentInfo);
                        }
                    }
                }
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void HandleHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            float iconOffset = 20f;

            if (childToParentMap.TryGetValue(instanceID, out List<ParentInfo> parentInfos))
            {
                bool hasPrefabRootParent = parentInfos.Any(p => p.isPrefabRoot);
                Texture2D iconToDraw = hasPrefabRootParent ? prefabRootIcon : defaultIcon;

                Rect iconRect = new Rect(selectionRect.xMax - iconOffset, selectionRect.y + (selectionRect.height - 12f) / 2, 8f, 8f);
                if (iconToDraw != null) GUI.DrawTexture(iconRect, iconToDraw);

                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && iconRect.Contains(currentEvent.mousePosition))
                {
                    currentEvent.Use();

                    ParentInfo targetParentInfo = hasPrefabRootParent ? parentInfos.First(p => p.isPrefabRoot) : parentInfos[0];
                    GameObject parentObject = EditorUtility.InstanceIDToObject(targetParentInfo.parentId) as GameObject;

                    if (parentObject != null)
                    {
                        EditorGUIUtility.PingObject(parentObject);
                        Selection.activeObject = targetParentInfo.script;
                        Debug.Log($"[HierarchyMarker] <b>{parentObject.name}</b> 오브젝트의 <b>{targetParentInfo.script.GetType().Name}</b> 컴포넌트가 <b>'{targetParentInfo.fieldName}'</b> 필드를 통해 참조합니다.", parentObject);
                    }
                }
                iconOffset += 12f;
            }

            GameObject currentGo = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            bool hasScrollComponent = false;

            if (currentGo != null)
            {
                #if UNITY_6000_0_OR_NEWER
                if (currentGo.GetComponent<ScrollRect>() != null) hasScrollComponent = true;
                #else
                if (currentGo.GetComponent<ScrollView>() != null) hasScrollComponent = true;
                #endif
            }
            
            if (hasScrollComponent)
            {
                Rect iconRect = new Rect(selectionRect.xMax - iconOffset, selectionRect.y + (selectionRect.height - 12f) / 2, 16f, 16f);
                if (scrollViewIcon != null)
                {
                    GUI.DrawTexture(iconRect, scrollViewIcon);
                }
            }
        }
    }
}
#endif