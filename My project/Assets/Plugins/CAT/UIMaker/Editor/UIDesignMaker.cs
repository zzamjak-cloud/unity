using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine.UI;

namespace CAT.Utility
{
    /// <summary>
    /// JSON 파일에서 UI 오브젝트를 생성하는 핵심 로직.
    /// AdvancedDropdown 기반 동적 메뉴로 JSON 폴더를 실시간 스캔한다.
    /// </summary>
    public static class UIDesignMaker
    {
        static string _jsonBasePath;

        /// <summary>
        /// JSON 기본 폴더 경로를 반환한다.
        /// </summary>
        public static string JsonBasePath
        {
            get
            {
                if (string.IsNullOrEmpty(_jsonBasePath))
                    _jsonBasePath = GetDefaultJsonPath();
                return _jsonBasePath;
            }
        }

        /// <summary>
        /// JSON 절대 경로에서 오브젝트를 생성한다.
        /// </summary>
        public static void CreateFromJsonAbsolute(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[UIDesignMaker] JSON 파일을 찾을 수 없습니다: {absolutePath}");
                return;
            }

            string json = File.ReadAllText(absolutePath, System.Text.Encoding.UTF8);

            PrefabJsonRoot root;
            try
            {
                root = SimpleJsonParser.Parse(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIDesignMaker] JSON 파싱 실패: {ex.Message}");
                return;
            }

            // Undo 그룹
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"UIDesignMaker: {root.prefabName}");

            // Canvas 찾기/생성
            Transform parent = GetOrCreateCanvasParent();

            // 재귀적 오브젝트 생성
            GameObject created = CreateGameObjectFromNode(root.root, parent);

            // 선택
            Selection.activeGameObject = created;
            EditorGUIUtility.PingObject(created);

            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"[UIDesignMaker] '{root.prefabName}' 생성 완료");
        }

        /// <summary>
        /// JSON 파일에서 오브젝트를 생성한다.
        /// </summary>
        /// <param name="relativeJsonPath">JsonBasePath 기준 상대 경로 (예: "Common/Button.json")</param>
        public static void CreateFromJson(string relativeJsonPath)
        {
            string fullPath = Path.Combine(JsonBasePath, relativeJsonPath);
            CreateFromJsonAbsolute(fullPath);
        }

        /// <summary>
        /// Canvas를 찾거나 생성한다.
        /// </summary>
        static Transform GetOrCreateCanvasParent()
        {
            // 현재 선택된 오브젝트가 Canvas 하위이면 그곳에 생성
            if (Selection.activeGameObject != null)
            {
                var parentCanvas = Selection.activeGameObject.GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                    return Selection.activeGameObject.transform;
            }

            // 씬에서 Canvas 검색
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                return canvas.transform;

            // Canvas 생성
            var canvasGo = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Canvas 생성");

            var canvasComp = canvasGo.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem 확인
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGo, "EventSystem 생성");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvasGo.transform;
        }

        /// <summary>
        /// GameObjectNode에서 재귀적으로 GameObject를 생성한다.
        /// </summary>
        static GameObject CreateGameObjectFromNode(GameObjectNode node, Transform parent)
        {
            // 중첩 프리팹 참조인 경우 프리팹을 인스턴스화
            if (node.IsPrefabReference)
            {
                return CreatePrefabInstance(node, parent);
            }

            var go = new GameObject(node.name);
            Undo.RegisterCreatedObjectUndo(go, $"생성: {node.name}");

            // RectTransform이 필요한 경우 추가
            if (node.transform.isRectTransform && go.GetComponent<RectTransform>() == null)
                go.AddComponent<RectTransform>();

            // 부모 설정
            GameObjectUtility.SetParentAndAlign(go, parent != null ? parent.gameObject : null);

            // Transform 복원
            ApplyTransform(go.transform, node.transform);

            // GameObject 속성
            go.SetActive(node.active);
            go.tag = node.tag;
            go.layer = node.layer;

            // 컴포넌트 추가 및 프로퍼티 복원
            foreach (var compData in node.components)
            {
                AddComponentFromData(go, compData);
            }

            // 자식 오브젝트 재귀 생성
            foreach (var child in node.children)
            {
                CreateGameObjectFromNode(child, go.transform);
            }

            return go;
        }

        /// <summary>
        /// 중첩 프리팹을 인스턴스화한다. 프리팹 연결을 유지한다.
        /// </summary>
        static GameObject CreatePrefabInstance(GameObjectNode node, Transform parent)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(node.prefabGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[UIDesignMaker] 프리팹을 찾을 수 없습니다 (GUID: {node.prefabGuid}, 이름: {node.name})");
                // 폴백: 빈 오브젝트 생성
                var fallback = new GameObject(node.name);
                Undo.RegisterCreatedObjectUndo(fallback, $"생성: {node.name}");
                if (parent != null)
                    GameObjectUtility.SetParentAndAlign(fallback, parent.gameObject);
                return fallback;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[UIDesignMaker] 프리팹 로드 실패: {assetPath}");
                var fallback = new GameObject(node.name);
                Undo.RegisterCreatedObjectUndo(fallback, $"생성: {node.name}");
                if (parent != null)
                    GameObjectUtility.SetParentAndAlign(fallback, parent.gameObject);
                return fallback;
            }

            // PrefabUtility로 인스턴스화 — 프리팹 연결 유지
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, $"프리팹 생성: {node.name}");
            instance.name = node.name;

            if (parent != null)
                GameObjectUtility.SetParentAndAlign(instance, parent.gameObject);

            // Transform 복원 (프리팹 내부와 다를 수 있는 위치/크기)
            ApplyTransform(instance.transform, node.transform);
            instance.SetActive(node.active);

            // 컴포넌트 프로퍼티 적용 (기존 컴포넌트 오버라이드 + 추가 컴포넌트)
            foreach (var compData in node.components)
            {
                Type compType = FindType(compData.typeName);
                if (compType == null)
                {
                    Debug.LogWarning($"[UIDesignMaker] 타입을 찾을 수 없습니다: {compData.typeName}");
                    continue;
                }
                if (typeof(Transform).IsAssignableFrom(compType)) continue;

                // 기존 컴포넌트가 있으면 오버라이드, 없으면 추가
                Component comp = instance.GetComponent(compType);
                if (comp == null)
                    comp = instance.AddComponent(compType);
                if (comp == null) continue;

                if (comp is Behaviour behaviour)
                    behaviour.enabled = compData.enabled;

                var so = new SerializedObject(comp);
                foreach (var propData in compData.properties)
                {
                    RestoreProperty(so, propData);
                }
                so.ApplyModifiedProperties();

                // 프리팹 인스턴스의 프로퍼티 변경을 override로 명시 기록
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            }

            return instance;
        }

        /// <summary>
        /// Transform 데이터를 적용한다.
        /// </summary>
        static void ApplyTransform(Transform transform, TransformData data)
        {
            transform.localPosition = new Vector3(
                data.localPosition[0], data.localPosition[1], data.localPosition[2]);
            transform.localRotation = new Quaternion(
                data.localRotation[0], data.localRotation[1],
                data.localRotation[2], data.localRotation[3]);
            transform.localScale = new Vector3(
                data.localScale[0], data.localScale[1], data.localScale[2]);

            if (data.isRectTransform)
            {
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(data.anchorMin[0], data.anchorMin[1]);
                    rt.anchorMax = new Vector2(data.anchorMax[0], data.anchorMax[1]);
                    rt.anchoredPosition = new Vector2(data.anchoredPosition[0], data.anchoredPosition[1]);
                    rt.sizeDelta = new Vector2(data.sizeDelta[0], data.sizeDelta[1]);
                    rt.pivot = new Vector2(data.pivot[0], data.pivot[1]);
                }
            }
        }

        /// <summary>
        /// ComponentData에서 컴포넌트를 추가하고 프로퍼티를 복원한다.
        /// </summary>
        static void AddComponentFromData(GameObject go, ComponentData compData)
        {
            // 타입 검색
            Type compType = FindType(compData.typeName);
            if (compType == null)
            {
                Debug.LogWarning($"[UIDesignMaker] 타입을 찾을 수 없습니다: {compData.typeName}");
                return;
            }

            // Transform은 AddComponent 불가 — 이미 존재
            if (typeof(Transform).IsAssignableFrom(compType))
                return;

            // 컴포넌트 추가 (이미 있으면 기존 것 사용)
            Component comp = go.GetComponent(compType);
            if (comp == null)
                comp = go.AddComponent(compType);

            if (comp == null)
            {
                Debug.LogWarning($"[UIDesignMaker] 컴포넌트 추가 실패: {compData.typeName}");
                return;
            }

            // enabled 설정
            if (comp is Behaviour behaviour)
                behaviour.enabled = compData.enabled;

            // SerializedProperty로 프로퍼티 복원
            var so = new SerializedObject(comp);
            foreach (var propData in compData.properties)
            {
                RestoreProperty(so, propData);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// PropertyData에서 SerializedProperty 값을 복원한다.
        /// </summary>
        static void RestoreProperty(SerializedObject so, PropertyData propData)
        {
            var prop = so.FindProperty(propData.name);
            if (prop == null) return;

            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = int.Parse(propData.value);
                        break;

                    case SerializedPropertyType.Boolean:
                        prop.boolValue = propData.value == "true";
                        break;

                    case SerializedPropertyType.Float:
                        prop.floatValue = float.Parse(propData.value, CultureInfo.InvariantCulture);
                        break;

                    case SerializedPropertyType.String:
                        prop.stringValue = propData.value;
                        break;

                    case SerializedPropertyType.Color:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 4)
                        {
                            prop.colorValue = new Color(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.Vector2:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 2)
                        {
                            prop.vector2Value = new Vector2(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.Vector3:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 3)
                        {
                            prop.vector3Value = new Vector3(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.Vector4:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 4)
                        {
                            prop.vector4Value = new Vector4(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.Vector2Int:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 2)
                        {
                            prop.vector2IntValue = new Vector2Int(
                                int.Parse(parts[0]), int.Parse(parts[1]));
                        }
                        break;
                    }

                    case SerializedPropertyType.Vector3Int:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 3)
                        {
                            prop.vector3IntValue = new Vector3Int(
                                int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                        }
                        break;
                    }

                    case SerializedPropertyType.Quaternion:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 4)
                        {
                            prop.quaternionValue = new Quaternion(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.Rect:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 4)
                        {
                            prop.rectValue = new Rect(
                                float.Parse(parts[0], CultureInfo.InvariantCulture),
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture));
                        }
                        break;
                    }

                    case SerializedPropertyType.RectInt:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 4)
                        {
                            prop.rectIntValue = new RectInt(
                                int.Parse(parts[0]), int.Parse(parts[1]),
                                int.Parse(parts[2]), int.Parse(parts[3]));
                        }
                        break;
                    }

                    case SerializedPropertyType.Bounds:
                    {
                        var parts = propData.value.Split(',');
                        if (parts.Length >= 6)
                        {
                            prop.boundsValue = new Bounds(
                                new Vector3(
                                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                                    float.Parse(parts[2], CultureInfo.InvariantCulture)),
                                new Vector3(
                                    float.Parse(parts[3], CultureInfo.InvariantCulture),
                                    float.Parse(parts[4], CultureInfo.InvariantCulture),
                                    float.Parse(parts[5], CultureInfo.InvariantCulture)));
                        }
                        break;
                    }

                    case SerializedPropertyType.Enum:
                        prop.intValue = int.Parse(propData.value);
                        break;

                    case SerializedPropertyType.ObjectReference:
                    {
                        if (!string.IsNullOrEmpty(propData.objectRefGuid))
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(propData.objectRefGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                // 타입이 있으면 해당 타입으로 로드
                                Type refType = null;
                                if (!string.IsNullOrEmpty(propData.objectRefType))
                                    refType = FindType(propData.objectRefType);

                                UnityEngine.Object asset = refType != null
                                    ? AssetDatabase.LoadAssetAtPath(assetPath, refType)
                                    : AssetDatabase.LoadMainAssetAtPath(assetPath);

                                prop.objectReferenceValue = asset;
                            }
                            else
                            {
                                Debug.LogWarning(
                                    $"[UIDesignMaker] 에셋을 찾을 수 없습니다 (GUID: {propData.objectRefGuid}, 프로퍼티: {propData.name})");
                            }
                        }
                        break;
                    }

                    case SerializedPropertyType.LayerMask:
                        prop.intValue = int.Parse(propData.value);
                        break;

                    case SerializedPropertyType.AnimationCurve:
                    {
                        if (!string.IsNullOrEmpty(propData.value))
                        {
                            var keyframes = new List<Keyframe>();
                            var segments = propData.value.Split('|');
                            foreach (var seg in segments)
                            {
                                var parts = seg.Split(',');
                                if (parts.Length >= 6)
                                {
                                    var kf = new Keyframe(
                                        float.Parse(parts[0], CultureInfo.InvariantCulture),
                                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                                        float.Parse(parts[3], CultureInfo.InvariantCulture),
                                        float.Parse(parts[4], CultureInfo.InvariantCulture),
                                        float.Parse(parts[5], CultureInfo.InvariantCulture));
                                    keyframes.Add(kf);
                                }
                            }
                            prop.animationCurveValue = new AnimationCurve(keyframes.ToArray());
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[UIDesignMaker] 프로퍼티 복원 실패 ({propData.name}): {ex.Message}");
            }
        }

        /// <summary>
        /// 타입 이름으로 Type을 검색한다.
        /// </summary>
        static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            // 캐시된 결과 확인
            if (_typeCache.TryGetValue(fullName, out Type cached))
                return cached;

            // 모든 어셈블리에서 검색
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                {
                    _typeCache[fullName] = type;
                    return type;
                }
            }

            _typeCache[fullName] = null;
            return null;
        }

        static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        /// <summary>
        /// 기본 JSON 폴더 경로를 계산한다.
        /// </summary>
        static string GetDefaultJsonPath()
        {
            // MonoScript로 스크립트 위치 찾기 (정확한 파일명 매칭)
            var guids = AssetDatabase.FindAssets("t:MonoScript UIDesignMaker");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // UIDesignMakerMenuItems 등 다른 파일 제외 — 정확히 UIDesignMaker.cs만 매칭
                if (path.Contains("UIMaker") &&
                    Path.GetFileNameWithoutExtension(path) == "UIDesignMaker")
                {
                    string editorDir = Path.GetDirectoryName(path);
                    string parentDir = Path.GetDirectoryName(editorDir);
                    string jsonDir = Path.Combine(parentDir, "JSON");
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", jsonDir));
                }
            }

            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "Plugins", "CAT", "UIMaker", "JSON"));
        }
    }

    /// <summary>
    /// JSON 폴더를 실시간 스캔하여 계층적 드롭다운 메뉴를 생성하는 클래스.
    /// HierarchyPresetMenuModule의 AdvancedDropdown 패턴을 따른다.
    /// </summary>
    public class JsonPrefabDropdown : AdvancedDropdown
    {
        readonly string _basePath;
        readonly Dictionary<int, string> _itemPaths = new Dictionary<int, string>();

        public JsonPrefabDropdown(AdvancedDropdownState state, string basePath) : base(state)
        {
            _basePath = basePath;
            minimumSize = new Vector2(350, 400);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("UI Design Maker");
            _itemPaths.Clear();

            if (!Directory.Exists(_basePath))
            {
                root.AddChild(new AdvancedDropdownItem("JSON 폴더를 찾을 수 없습니다") { enabled = false });
                root.AddChild(new AdvancedDropdownItem($"{_basePath}") { enabled = false });
                return root;
            }

            string[] jsonFiles = Directory.GetFiles(_basePath, "*.json", SearchOption.AllDirectories);

            if (jsonFiles.Length == 0)
            {
                root.AddChild(new AdvancedDropdownItem("JSON 파일이 없습니다") { enabled = false });
                root.AddChild(new AdvancedDropdownItem("먼저 프리팹을 JSON으로 추출해주세요") { enabled = false });
                return root;
            }

            string normalizedBase = Path.GetFullPath(_basePath).Replace('\\', '/');
            if (!normalizedBase.EndsWith("/"))
                normalizedBase += "/";

            int idCounter = 0;

            // 정렬하여 폴더 구조가 일관되게 표시
            Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

            foreach (string file in jsonFiles)
            {
                string normalizedFile = Path.GetFullPath(file).Replace('\\', '/');
                string relativePath = normalizedFile.Substring(normalizedBase.Length);
                string[] pathParts = relativePath.Split('/');

                AdvancedDropdownItem currentParent = root;

                // 하위 폴더 구조 생성
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    var existingChild = currentParent.children?
                        .FirstOrDefault(child => child.name == pathParts[i]);

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
                }

                // JSON 파일 항목 추가 (확장자 제거)
                string fileName = Path.GetFileNameWithoutExtension(pathParts[pathParts.Length - 1]);
                var item = new AdvancedDropdownItem(fileName)
                {
                    id = idCounter++
                };

                _itemPaths[item.id] = normalizedFile;
                currentParent.AddChild(item);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);
            if (_itemPaths.TryGetValue(item.id, out string path))
            {
                UIDesignMaker.CreateFromJsonAbsolute(path);
            }
        }
    }

    /// <summary>
    /// JSON 문자열을 PrefabJsonRoot로 파싱하는 간이 파서.
    /// Newtonsoft.Json 없이 동작한다.
    /// </summary>
    public static class SimpleJsonParser
    {
        static string _json;
        static int _pos;

        public static PrefabJsonRoot Parse(string json)
        {
            _json = json;
            _pos = 0;

            var dict = ParseObject();
            var root = new PrefabJsonRoot
            {
                prefabName = GetString(dict, "prefabName"),
                exportDate = GetString(dict, "exportDate"),
                root = ParseGameObjectNode(GetDict(dict, "root"))
            };
            return root;
        }

        static GameObjectNode ParseGameObjectNode(Dictionary<string, object> dict)
        {
            if (dict == null) return null;

            var node = new GameObjectNode
            {
                name = GetString(dict, "name"),
                active = GetBool(dict, "active"),
                prefabGuid = GetString(dict, "prefabGuid"),
                tag = GetString(dict, "tag"),
                layer = GetInt(dict, "layer"),
                transform = ParseTransformData(GetDict(dict, "transform"))
            };

            // 컴포넌트 (중첩 프리팹도 오버라이드/추가 컴포넌트 포함)
            var compList = GetList(dict, "components");
            if (compList != null)
            {
                foreach (var item in compList)
                {
                    if (item is Dictionary<string, object> compDict)
                        node.components.Add(ParseComponentData(compDict));
                }
            }

            // 중첩 프리팹이면 자식은 프리팹에서 오므로 생략
            if (node.IsPrefabReference)
                return node;

            // 자식 노드
            var childList = GetList(dict, "children");
            if (childList != null)
            {
                foreach (var item in childList)
                {
                    if (item is Dictionary<string, object> childDict)
                        node.children.Add(ParseGameObjectNode(childDict));
                }
            }

            return node;
        }

        static TransformData ParseTransformData(Dictionary<string, object> dict)
        {
            if (dict == null) return new TransformData();

            var data = new TransformData
            {
                isRectTransform = GetBool(dict, "isRectTransform")
            };

            CopyFloatArray(dict, "localPosition", data.localPosition, 3);
            CopyFloatArray(dict, "localRotation", data.localRotation, 4);
            CopyFloatArray(dict, "localScale", data.localScale, 3);

            if (data.isRectTransform)
            {
                CopyFloatArray(dict, "anchorMin", data.anchorMin, 2);
                CopyFloatArray(dict, "anchorMax", data.anchorMax, 2);
                CopyFloatArray(dict, "anchoredPosition", data.anchoredPosition, 2);
                CopyFloatArray(dict, "sizeDelta", data.sizeDelta, 2);
                CopyFloatArray(dict, "pivot", data.pivot, 2);
            }

            return data;
        }

        static ComponentData ParseComponentData(Dictionary<string, object> dict)
        {
            var comp = new ComponentData
            {
                typeName = GetString(dict, "typeName"),
                enabled = GetBool(dict, "enabled")
            };

            var propList = GetList(dict, "properties");
            if (propList != null)
            {
                foreach (var item in propList)
                {
                    if (item is Dictionary<string, object> propDict)
                    {
                        comp.properties.Add(new PropertyData
                        {
                            name = GetString(propDict, "name"),
                            type = GetString(propDict, "type"),
                            value = GetString(propDict, "value"),
                            objectRefGuid = GetString(propDict, "objectRefGuid"),
                            objectRefType = GetString(propDict, "objectRefType")
                        });
                    }
                }
            }

            return comp;
        }

        static void CopyFloatArray(Dictionary<string, object> dict, string key, float[] target, int count)
        {
            if (!dict.ContainsKey(key)) return;
            var list = dict[key] as List<object>;
            if (list == null) return;

            for (int i = 0; i < count && i < list.Count; i++)
            {
                if (list[i] is double d)
                    target[i] = (float)d;
                else if (list[i] is long l)
                    target[i] = l;
            }
        }

        // --- 간이 JSON 파서 핵심 ---

        static Dictionary<string, object> ParseObject()
        {
            SkipWhitespace();
            Expect('{');
            var dict = new Dictionary<string, object>();
            SkipWhitespace();

            if (Peek() == '}')
            {
                _pos++;
                return dict;
            }

            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                object value = ParseValue();
                dict[key] = value;
                SkipWhitespace();

                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }
                break;
            }

            SkipWhitespace();
            Expect('}');
            return dict;
        }

        static List<object> ParseArray()
        {
            Expect('[');
            var list = new List<object>();
            SkipWhitespace();

            if (Peek() == ']')
            {
                _pos++;
                return list;
            }

            while (true)
            {
                SkipWhitespace();
                list.Add(ParseValue());
                SkipWhitespace();

                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }
                break;
            }

            SkipWhitespace();
            Expect(']');
            return list;
        }

        static object ParseValue()
        {
            SkipWhitespace();
            char c = Peek();

            if (c == '"') return ParseString();
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == 't') { ExpectLiteral("true"); return true; }
            if (c == 'f') { ExpectLiteral("false"); return false; }
            if (c == 'n') { ExpectLiteral("null"); return null; }
            return ParseNumber();
        }

        static string ParseString()
        {
            Expect('"');
            var sb = new System.Text.StringBuilder();
            while (_pos < _json.Length)
            {
                char c = _json[_pos++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    c = _json[_pos++];
                    switch (c)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            string hex = _json.Substring(_pos, 4);
                            sb.Append((char)int.Parse(hex, System.Globalization.NumberStyles.HexNumber));
                            _pos += 4;
                            break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new FormatException("문자열이 닫히지 않았습니다");
        }

        static object ParseNumber()
        {
            int start = _pos;
            if (Peek() == '-') _pos++;
            while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;

            bool isFloat = false;
            if (_pos < _json.Length && _json[_pos] == '.')
            {
                isFloat = true;
                _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }
            if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
            {
                isFloat = true;
                _pos++;
                if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }

            string numStr = _json.Substring(start, _pos - start);
            if (isFloat)
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            return long.Parse(numStr, CultureInfo.InvariantCulture);
        }

        static void SkipWhitespace()
        {
            while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos]))
                _pos++;
        }

        static char Peek()
        {
            if (_pos >= _json.Length)
                throw new FormatException("예기치 않은 JSON 끝");
            return _json[_pos];
        }

        static void Expect(char c)
        {
            if (_pos >= _json.Length || _json[_pos] != c)
                throw new FormatException(
                    $"위치 {_pos}에서 '{c}'를 기대했지만 " +
                    $"'{(_pos < _json.Length ? _json[_pos].ToString() : "EOF")}'를 만났습니다");
            _pos++;
        }

        static void ExpectLiteral(string literal)
        {
            foreach (char c in literal)
                Expect(c);
        }

        // 유틸리티
        static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val is string s)
                return s;
            return null;
        }

        static bool GetBool(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val is bool b)
                return b;
            return false;
        }

        static int GetInt(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val))
            {
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
            }
            return 0;
        }

        static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val))
                return val as Dictionary<string, object>;
            return null;
        }

        static List<object> GetList(Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out object val))
                return val as List<object>;
            return null;
        }
    }
}
