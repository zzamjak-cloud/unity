using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement; // EditorSceneManager 사용을 위해 추가
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;

// 프로젝트 Effect 폴더 내에 저장된 프리팹들을 씬에 모두 불러들여 한번에 확인하기 위한 디스플레이 전용 툴입니다.
// 3D와 UI 이펙트 모두 2D 뷰에서 그리드 형태로 표시합니다.

namespace CAT.Utility
{
    public class EffectLoader : EditorWindow
    {
        private const string EFFECTS_PATH = "Assets/_Jinpyoung/Prefab/Effect";       // 이펙트 저장 경로
        private const int GRID_X_LIMIT = 10;                                // 이펙트 프리팹의 X축 그리드 Max 개수
        private const float SPACING_3D = 5f;                                // 3D 이펙트 프리팹 간격
        private const float SPACING_UI = 400f;                              // UI 이펙트 프리팹 간격

        [MenuItem("CAT/Effects/Default Load")]
        static void LoadDefaultEffects()
        {
            if (!HandleSceneSaveAndCreate()) return;
            LoadEffectsByType(false); // 3D 이펙트 로드
        }

        [MenuItem("CAT/Effects/UI Load")]
        static void LoadUIEffects()
        {
            if (!HandleSceneSaveAndCreate()) return;
            LoadEffectsByType(true); // UI 이펙트 로드
        }

        /// <summary>
        /// 씬 저장 여부를 묻고, 취소하지 않으면 새 씬을 생성합니다.
        /// </summary>
        /// <returns>작업 계속 진행 여부</returns>
        static bool HandleSceneSaveAndCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false; // 사용자가 'Cancel'을 누르면 작업 중단
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            return true;
        }

        static void LoadEffectsByType(bool isUI)
        {
            if (!Directory.Exists(EFFECTS_PATH))
            {
                Debug.LogError($"폴더를 찾을 수 없습니다: {EFFECTS_PATH}");
                return;
            }

            string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EFFECTS_PATH });
            if (allPrefabGuids.Length == 0)
            {
                Debug.LogWarning($"{EFFECTS_PATH}에서 프리팹을 찾을 수 없습니다.");
                return;
            }

            List<string> targetPrefabGuids = new List<string>();
            foreach (string guid in allPrefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                bool hasRectTransform = prefab.GetComponent<RectTransform>() != null;
                if ((isUI && hasRectTransform) || (!isUI && !hasRectTransform))
                {
                    targetPrefabGuids.Add(guid);
                }
            }

            if (targetPrefabGuids.Count == 0)
            {
                string typeString = isUI ? "UI" : "3D";
                Debug.LogWarning($"{EFFECTS_PATH}에서 {typeString} 이펙트를 찾을 수 없습니다.");
                return;
            }

            Transform parentTransform = isUI ? CreateCanvas() : null;

            List<GameObject> loadedObjects = LoadAndArrangePrefabs(targetPrefabGuids.ToArray(), parentTransform, isUI);

            FocusOnAllObjects(loadedObjects);
            PlayAllEffectsRepeatedly(loadedObjects);
            SelectAllLoadedObjects(loadedObjects, parentTransform?.gameObject);

            string loadedType = isUI ? "UI" : "3D";
            Debug.Log($"{loadedType} 이펙트 {targetPrefabGuids.Count}개를 새 씬에 로드하고 전체를 선택했습니다.");
        }

        static Transform CreateCanvas()
        {
            GameObject canvasObj = new GameObject("Effects Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            if (Camera.main == null)
            {
                var cameraObj = new GameObject("UI Camera");
                cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }
            return canvasObj.transform;
        }

        static List<GameObject> LoadAndArrangePrefabs(string[] prefabGuids, Transform parent, bool isUI)
        {
            List<GameObject> loadedObjects = new List<GameObject>();
            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent);
                    }
                    loadedObjects.Add(instance);
                }
            }

            ArrangeInGrid(loadedObjects, isUI);
            AddNameLabels(loadedObjects, isUI);

            return loadedObjects;
        }

        static void ArrangeInGrid(List<GameObject> objects, bool isUI)
        {
            float spacing = isUI ? SPACING_UI : SPACING_3D;
            int objectCount = objects.Count;
            float halfGridX = GRID_X_LIMIT / 2f - 0.5f;
            float halfGridY = (Mathf.Ceil((float)objectCount / GRID_X_LIMIT) - 1) / 2f;

            for (int i = 0; i < objectCount; i++)
            {
                int x = i % GRID_X_LIMIT;
                int y = i / GRID_X_LIMIT;

                Vector3 position = new Vector3(
                    (x - halfGridX) * spacing,
                    (-y + halfGridY) * spacing,
                    0f
                );

                // UI는 Canvas 내부의 localPosition, 3D는 World의 position
                if (isUI)
                {
                    objects[i].transform.localPosition = position;
                }
                else
                {
                    objects[i].transform.position = position;
                }
            }
        }

        static void AddNameLabels(List<GameObject> objects, bool isUI)
        {
            foreach (GameObject obj in objects)
            {
                if (isUI) CreateUINameLabel(obj);
                else Create3DNameLabel(obj);
            }
        }

        static void CreateUINameLabel(GameObject target)
        {
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(target.transform, false);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = target.name.Replace("(Clone)", "");
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;

            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(0, -100, 0);
            rectTransform.sizeDelta = new Vector2(200, 40);
        }

        static void Create3DNameLabel(GameObject target)
        {
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(target.transform, false);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = target.name.Replace("(Clone)", "");
            textMesh.fontSize = 12;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textObj.transform.localPosition = new Vector3(0, -1.0f, 0);
        }
        
        static void FocusOnAllObjects(List<GameObject> objects)
        {
            if (objects == null || objects.Count == 0) return;

            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                // 항상 2D 모드로 전환
                sceneView.in2DMode = true;

                // 포지션 기준으로 Bounds 계산 (UI는 localPosition, 3D는 position으로 배치했었음)
                var bounds = new Bounds(objects[0].transform.position, Vector3.zero);
                foreach (var obj in objects)
                {
                    bounds.Encapsulate(obj.transform.position);
                }
                bounds.size = new Vector3(bounds.size.x, bounds.size.y, 1f);
                
                sceneView.Frame(bounds, false);
            }
        }

        static void PlayAllEffectsRepeatedly(List<GameObject> objects)
        {
            foreach (GameObject obj in objects)
            {
                ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem ps in particleSystems)
                {
                    var main = ps.main;
                    main.loop = true;
                    ps.Play(true);
                }
            }
        }
        
        static void SelectAllLoadedObjects(List<GameObject> rootObjects, GameObject canvasObject)
        {
            if (rootObjects == null || rootObjects.Count == 0) return;

            var allObjectsToSelect = new HashSet<GameObject>();
            if (canvasObject != null)
            {
                allObjectsToSelect.Add(canvasObject);
            }

            foreach(var rootObj in rootObjects)
            {
                allObjectsToSelect.UnionWith(rootObj.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
            }
            Selection.objects = allObjectsToSelect.ToArray();
        }

        [MenuItem("CAT/Effects/Default Load", true)]
        [MenuItem("CAT/Effects/UI Load", true)]
        static bool ValidateLoadEffects()
        {
            return Directory.Exists(EFFECTS_PATH);
        }
    }
}