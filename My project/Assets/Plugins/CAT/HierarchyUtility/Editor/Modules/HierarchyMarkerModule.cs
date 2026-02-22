using System;
using System.Collections;
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
        private GUIStyle _countLabelStyle;

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

            if (_countLabelStyle == null)
            {
                _countLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 7,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                };
                _countLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }

            if (_childToParentMap.TryGetValue(instanceID, out List<ParentInfo> parentInfos))
            {
                bool hasPrefabRootParent = parentInfos.Any(p => p.isPrefabRoot);
                Texture2D iconToDraw = hasPrefabRootParent ? _prefabRootIcon : _defaultIcon;

                Rect iconRect = new Rect(selectionRect.xMax - 20f, selectionRect.y + (selectionRect.height - 12f) / 2, 8f, 8f);
                if (iconToDraw != null) GUI.DrawTexture(iconRect, iconToDraw);

                // 참조가 2개 이상일 때만 숫자 표시
                if (parentInfos.Count >= 2)
                {
                    string countText = parentInfos.Count > 9 ? "9+" : parentInfos.Count.ToString();
                    Rect countRect = new Rect(iconRect.xMax + 1f, iconRect.y - 1f, 14f, iconRect.height);
                    GUI.Label(countRect, countText, _countLabelStyle);
                }

                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && iconRect.Contains(currentEvent.mousePosition))
                {
                    currentEvent.Use();

                    // 모든 참조자의 GameObject를 수집하여 하이어라키에서 다중 선택
                    var allParentObjects = new List<UnityEngine.Object>();
                    foreach (var info in parentInfos)
                    {
                        GameObject parentObj = EditorUtility.InstanceIDToObject(info.parentId) as GameObject;
                        if (parentObj != null)
                        {
                            allParentObjects.Add(parentObj);
                            Debug.Log($"[HierarchyMarker] <b>{parentObj.name}</b> 오브젝트의 <b>{info.script.GetType().Name}</b> 컴포넌트가 <b>'{info.fieldName}'</b> 필드를 통해 참조합니다.", parentObj);
                        }
                    }

                    if (allParentObjects.Count > 0)
                    {
                        // 모든 참조 오브젝트를 하이어라키에서 선택 (다중 하이라이트)
                        Selection.objects = allParentObjects.ToArray();
                        // 대표 오브젝트로 스크롤 이동 (프리팹 루트 우선)
                        ParentInfo primary = hasPrefabRootParent ? parentInfos.First(p => p.isPrefabRoot) : parentInfos[0];
                        GameObject primaryObj = EditorUtility.InstanceIDToObject(primary.parentId) as GameObject;
                        if (primaryObj != null)
                            EditorGUIUtility.PingObject(primaryObj);
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

                    // 단일 타입 필드 (GameObject, Component 계열)
                    if (typeof(GameObject).IsAssignableFrom(field.FieldType) || typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        object value = field.GetValue(script);
                        if (value == null) continue;
                        GameObject referencedObject = null;
                        if (value is GameObject go && go != null) referencedObject = go;
                        else if (value is Component component && component != null) referencedObject = component.gameObject;

                        RegisterReference(referencedObject, parentID, isParentPrefabRoot, script, field.Name);
                    }
                    // 배열 타입 필드 (GameObject[], Component[] 계열)
                    else if (field.FieldType.IsArray)
                    {
                        Type elementType = field.FieldType.GetElementType();
                        if (elementType == null) continue;
                        if (!typeof(GameObject).IsAssignableFrom(elementType) && !typeof(Component).IsAssignableFrom(elementType)) continue;

                        if (!(field.GetValue(script) is Array array)) continue;
                        foreach (var item in array)
                        {
                            if (item == null) continue;
                            GameObject referencedObject = null;
                            if (item is GameObject go && go != null) referencedObject = go;
                            else if (item is Component component && component != null) referencedObject = component.gameObject;

                            RegisterReference(referencedObject, parentID, isParentPrefabRoot, script, field.Name);
                        }
                    }
                    // 제네릭 리스트 타입 필드 (List<GameObject>, List<Component> 계열)
                    else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        Type elementType = field.FieldType.GetGenericArguments()[0];
                        if (!typeof(GameObject).IsAssignableFrom(elementType) && !typeof(Component).IsAssignableFrom(elementType)) continue;

                        if (!(field.GetValue(script) is IList list)) continue;
                        foreach (var item in list)
                        {
                            if (item == null) continue;
                            GameObject referencedObject = null;
                            if (item is GameObject go && go != null) referencedObject = go;
                            else if (item is Component component && component != null) referencedObject = component.gameObject;

                            RegisterReference(referencedObject, parentID, isParentPrefabRoot, script, field.Name);
                        }
                    }
                }
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private void RegisterReference(GameObject referencedObject, int parentID, bool isParentPrefabRoot, MonoBehaviour script, string fieldName)
        {
            if (referencedObject == null) return;

            int childID = referencedObject.GetInstanceID();
            if (parentID == childID) return;

            var parentInfo = new ParentInfo { parentId = parentID, isPrefabRoot = isParentPrefabRoot, script = script, fieldName = fieldName };
            if (!_childToParentMap.ContainsKey(childID))
                _childToParentMap[childID] = new List<ParentInfo>();

            // 동일 스크립트+필드 중복 등록 방지
            if (!_childToParentMap[childID].Any(p => p.script == script && p.fieldName == fieldName))
                _childToParentMap[childID].Add(parentInfo);
        }
    }
}
