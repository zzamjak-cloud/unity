using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq; // ToArray() 사용을 위해 추가

// 프로젝트 Effect 폴더 내에 저장된 프리팹들을 씬에 모두 불러들여 한번에 확인하기 위한 디스플레이 전용 툴입니다.
// 메뉴는 CAT/Effects/Defalut Loader와 CAT/Effects/UI Loader 2개로 제공됩니다.
// 3D용 이펙트와 UI용 이펙트를 각각 별도로 불러들여 확인하기 위한 기능입니다.

namespace CAT.Utility
{
    public class EffectLoader : EditorWindow
    {
        private const string EFFECTS_PATH = "Assets/_Jinpyoung/Prefab/Effect";       // 이펙트 저장 경로
        private const int GRID_X_LIMIT = 10;                                // 이펙트 프리팹의 X축 그리드 Max 개수
        private const float SPACING_3D = 5f;                                // 3D 이펙트 프리팹 간격 (기본값 5유닛)
        private const float SPACING_UI = 400f;                              // UI 이펙트 프리팹 간격 (기본값 400px)
        private const string EFFECT_SCENE_NAME = "Effect Scene";            // 이펙트 전용 씬 이름

        [MenuItem("CAT/Effects/Default Load")]
        static void LoadDefaultEffects()
        {
            if (!IsEffectScene())
            {
                ShowSceneWarning();
                return;
            }

            LoadEffectsByType(false); // 3D 이펙트 로드
        }

        [MenuItem("CAT/Effects/UI Load")]
        static void LoadUIEffects()
        {
            if (!IsEffectScene())
            {
                ShowSceneWarning();
                return;
            }

            LoadEffectsByType(true); // UI 이펙트 로드
        }

        static void LoadEffectsByType(bool isUI)
        {
            // 현재 씬의 모든 오브젝트 제거
            ClearScene();

            // Effects 폴더가 존재하는지 확인
            if (!Directory.Exists(EFFECTS_PATH))
            {
                Debug.LogError($"폴더를 찾을 수 없습니다: {EFFECTS_PATH}");
                return;
            }

            // Effects 폴더 내의 모든 프리팹 파일들 찾기 (하위 폴더 포함)
            string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EFFECTS_PATH });

            if (allPrefabGuids.Length == 0)
            {
                Debug.LogWarning($"{EFFECTS_PATH}에서 프리팹을 찾을 수 없습니다.");
                return;
            }

            // UI/3D 이펙트 구분
            List<string> targetPrefabGuids = new List<string>();

            foreach (string guid in allPrefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (prefab != null)
                {
                    bool hasRectTransform = prefab.GetComponent<RectTransform>() != null;

                    // UI 이펙트를 찾는 경우
                    if (isUI && hasRectTransform)
                    {
                        targetPrefabGuids.Add(guid);
                    }
                    // 3D 이펙트를 찾는 경우
                    else if (!isUI && !hasRectTransform)
                    {
                        targetPrefabGuids.Add(guid);
                    }
                }
            }

            if (targetPrefabGuids.Count == 0)
            {
                string typeString = isUI ? "UI" : "3D";
                Debug.LogWarning($"{EFFECTS_PATH}에서 {typeString} 이펙트를 찾을 수 없습니다.");
                return;
            }

            Transform parentTransform = null;

            // UI 이펙트의 경우 Canvas 생성
            if (isUI)
            {
                parentTransform = CreateCanvas();
            }

            List<GameObject> loadedObjects = LoadAndArrangePrefabs(targetPrefabGuids.ToArray(), parentTransform, isUI);

            // 로드된 모든 이펙트에 포커스
            FocusOnAllObjects(loadedObjects);

            // 모든 파티클 시스템을 찾아 반복 재생 설정
            PlayAllEffectsRepeatedly(loadedObjects);

            // 로드된 모든 이펙트와 그 자식들을 선택
            SelectAllLoadedObjects(loadedObjects);


            string loadedType = isUI ? "UI" : "3D";
            Debug.Log($"{loadedType} 이펙트 {targetPrefabGuids.Count}개를 로드하고 전체를 선택했습니다.");
        }

        static void ClearScene()
        {
            // 씬의 모든 루트 오브젝트 제거
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            for (int i = rootObjects.Length - 1; i >= 0; i--)
            {
                DestroyImmediate(rootObjects[i]);
            }
        }

        static Transform CreateCanvas()
        {
            // Canvas 오브젝트 생성
            GameObject canvasObj = new GameObject("Effects Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // CanvasScaler 추가
            CanvasScaler canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            // GraphicRaycaster 추가
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            return canvasObj.transform;
        }

        static List<GameObject> LoadAndArrangePrefabs(string[] prefabGuids, Transform parent, bool isUI)
        {
            List<GameObject> loadedObjects = new List<GameObject>();

            // 프리팹들을 로드
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

            // 그리드로 배열
            ArrangeInGrid(loadedObjects, isUI);

            // 각 프리팹에 이름 텍스트 추가
            AddNameLabels(loadedObjects, isUI);

            return loadedObjects;
        }

        static void ArrangeInGrid(List<GameObject> objects, bool isUI)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                int x = i % GRID_X_LIMIT;
                int y = i / GRID_X_LIMIT;

                Vector3 position;

                if (isUI)
                {
                    // UI 좌표계 (Canvas 내부)
                    position = new Vector3(
                        (x - GRID_X_LIMIT / 2f + 0.5f) * SPACING_UI, // UI 전용 간격
                        (-y + (objects.Count / GRID_X_LIMIT) / 2f) * SPACING_UI, // Y축 반전 (UI는 위에서 아래로)
                        0f
                    );
                    objects[i].transform.localPosition = position;
                }
                else
                {
                    // 3D 월드 좌표계
                    position = new Vector3(
                        x * SPACING_3D,
                        0f,
                        -y * SPACING_3D
                    );
                    objects[i].transform.position = position;
                }
            }
        }

        static void AddNameLabels(List<GameObject> objects, bool isUI)
        {
            foreach (GameObject obj in objects)
            {
                if (isUI)
                {
                    CreateUINameLabel(obj);
                }
                else
                {
                    Create3DNameLabel(obj);
                }
            }
        }

        static void CreateUINameLabel(GameObject target)
        {
            // UI TextMeshPro 라벨 생성
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(target.transform, false);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = target.name.Replace("(Clone)", "");
            textComponent.fontSize = 24;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;

            // RectTransform 설정
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(0, -100, 0);
            rectTransform.sizeDelta = new Vector2(200, 40);
            rectTransform.localScale = Vector3.one;
        }

        static void Create3DNameLabel(GameObject target)
        {
            // 3D 텍스트 라벨 생성
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(target.transform, false);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = target.name.Replace("(Clone)", "");
            textMesh.fontSize = 12;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;

            // 위치 조정 (이펙트 아래쪽에 배치)
            textObj.transform.localPosition = new Vector3(0, -1.0f, 0);

            // 카메라를 향하도록 회전 (Billboard)
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                textObj.transform.LookAt(textObj.transform.position + SceneView.lastActiveSceneView.camera.transform.rotation * Vector3.forward,
                                         SceneView.lastActiveSceneView.camera.transform.rotation * Vector3.up);
            }
        }
        
        /// <summary>
        /// 로드된 모든 이펙트를 비추도록 Scene 뷰 카메라를 조정합니다.
        /// </summary>
        static void FocusOnAllObjects(List<GameObject> objects)
        {
            if (objects == null || objects.Count == 0) return;

            Bounds bounds = new Bounds(objects[0].transform.position, Vector3.zero);
            foreach (GameObject obj in objects)
            {
                bounds.Encapsulate(obj.transform.position);
            }

            // 모든 씬 뷰에 포커스 적용
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                sceneView.Frame(bounds, false);
            }
        }

        /// <summary>
        /// 지정된 오브젝트들과 그 자식들에서 모든 파티클 시스템을 찾아 반복 재생하도록 설정합니다.
        /// </summary>
        static void PlayAllEffectsRepeatedly(List<GameObject> objects)
        {
            int particleSystemCount = 0;
            foreach (GameObject obj in objects)
            {
                ParticleSystem[] particleSystems = obj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem ps in particleSystems)
                {
                    var main = ps.main;
                    main.loop = true; // 반복 재생 활성화
                    ps.Play(true); // 즉시 재생
                    particleSystemCount++;
                }
            }
            Debug.Log($"총 {particleSystemCount}개의 파티클 시스템이 반복 재생되도록 설정되었습니다.");
        }

        /// <summary>
        /// 로드된 모든 게임 오브젝트와 그 자식들을 Hierarchy 뷰에서 선택합니다.
        /// </summary>
        /// <param name="rootObjects">선택할 최상위 게임 오브젝트 리스트</param>
        static void SelectAllLoadedObjects(List<GameObject> rootObjects)
        {
            if (rootObjects == null || rootObjects.Count == 0) return;

            var allObjectsToSelect = new HashSet<GameObject>();

            foreach(var rootObj in rootObjects)
            {
                // GetComponentsInChildren는 자기 자신도 포함하므로, 부모와 모든 자식을 가져옴
                Transform[] allChildren = rootObj.GetComponentsInChildren<Transform>(true);
                foreach(var childTransform in allChildren)
                {
                    allObjectsToSelect.Add(childTransform.gameObject);
                }
            }

            // Hierarchy 뷰에서 선택
            Selection.objects = allObjectsToSelect.ToArray();
        }


        static bool IsEffectScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.name == EFFECT_SCENE_NAME;
        }

        static void ShowSceneWarning()
        {
            bool result = EditorUtility.DisplayDialog(
                "경고: 잘못된 씬",
                $"이 기능은 '{EFFECT_SCENE_NAME}' 씬에서만 사용할 수 있습니다.\n\n" +
                $"현재 씬: {SceneManager.GetActiveScene().name}\n\n" +
                "계속 진행하시겠습니까? (현재 씬의 모든 오브젝트가 삭제됩니다)",
                "계속 진행",
                "취소"
            );

            if (result)
            {
                Debug.LogWarning($"'{SceneManager.GetActiveScene().name}' 씬에서 강제로 이펙트 로더를 실행합니다.");
            }
        }

        // 메뉴 항목 유효성 검사
        [MenuItem("CAT/Effects/Default Load", true)]
        static bool ValidateLoadDefaultEffects()
        {
            return Directory.Exists(EFFECTS_PATH);
        }

        [MenuItem("CAT/Effects/UI Load", true)]
        static bool ValidateLoadUIEffects()
        {
            return Directory.Exists(EFFECTS_PATH);
        }
    }
}