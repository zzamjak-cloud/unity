using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 창에서 오브젝트 참조 관계를 아이콘으로 표시하는 모듈.
    // UIOrder = 10
    public class HierarchyMarkerModule : IHierarchyToolModule
    {
        public string ModuleName => "HierarchyMarker";
        public int UIOrder => 10;

        private class ParentInfo
        {
            public int parentId;
            public bool isPrefabRoot;
            public MonoBehaviour script;
            public string fieldName;
        }

        private readonly Dictionary<int, List<ParentInfo>> _childToParentMap = new Dictionary<int, List<ParentInfo>>();

        private Texture2D _defaultIcon;
        private Texture2D _prefabRootIcon;

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            UpdateMarkedObjectsCache();
        }

        public void InitUI(VisualElement container) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_defaultIcon == null || _prefabRootIcon == null)
            {
                _defaultIcon = EditorGUIUtility.IconContent("d_greenLight").image as Texture2D;
                _prefabRootIcon = EditorGUIUtility.IconContent("d_orangeLight").image as Texture2D;
            }

            if (_childToParentMap.TryGetValue(instanceID, out List<ParentInfo> parentInfos))
            {
                bool hasPrefabRootParent = parentInfos.Any(p => p.isPrefabRoot);
                Texture2D iconToDraw = hasPrefabRootParent ? _prefabRootIcon : _defaultIcon;

                Rect iconRect = new Rect(selectionRect.xMax - 20f, selectionRect.y + (selectionRect.height - 12f) / 2, 8f, 8f);
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
            }
        }

        public void OnHierarchyChanged()
        {
            UpdateMarkedObjectsCache();
        }

        public void Dispose()
        {
            _childToParentMap.Clear();
        }

        private void UpdateMarkedObjectsCache()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _childToParentMap.Clear();
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

                // 시스템/라이브러리 네임스페이스 필터링
                // Unity 기본 컴포넌트나 TMP 같은 라이브러리 컴포넌트는 건너뜀
                var scriptNamespace = script.GetType().Namespace;
                if (!string.IsNullOrEmpty(scriptNamespace) && (
                    scriptNamespace.StartsWith("UnityEngine") ||
                    scriptNamespace.StartsWith("UnityEditor") ||
                    scriptNamespace.StartsWith("TMPro")))
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
                    bool isSerializable = field.IsPublic || field.IsDefined(typeof(SerializeField), false);
                    if (!isSerializable) continue;

                    if (field.IsDefined(typeof(HideInInspector), false) || field.IsDefined(typeof(System.NonSerializedAttribute), false)) continue;

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
                            if (!_childToParentMap.ContainsKey(childID))
                            {
                                _childToParentMap[childID] = new List<ParentInfo>();
                            }
                            _childToParentMap[childID].Add(parentInfo);
                        }
                    }
                }
            }

            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
