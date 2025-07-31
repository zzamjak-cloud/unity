using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;

// 프로젝트 Effect 폴더 내에 저장된 프리팹들을 씬에 모두 불러들여 한번에 확인하기 위한 디스플레이 전용 툴입니다.
// 메뉴는 CAT/Effects/Defalut Loader와 CAT/Effects/UI Loader 2개로 제공됩니다.
// 3D용 이펙트와 UI용 이펙트를 각각 별도로 불러들여 확인하기 위한 기능입니다.

namespace CAT.Utility
{
    public class EffectLoader : EditorWindow
    {
        private const string EFFECTS_PATH = "Assets/_Jinpyoung/Prefab/Effect";      // 이펙트 저장 경로
        private const int GRID_X_LIMIT = 10;                                        // 이펙트 프리팹의 X축 그리드 Max 개수
        private const float SPACING_3D = 5f;                                        // 3D 이펙트 프리팹 간격 (기본값 5유닛)
        private const float SPACING_UI = 400f;                                      // UI 이펙트 프리팹 간격 (기본값 400px)
        private const string EFFECT_SCENE_NAME = "Effect Scene";                    // 이펙트 전용 씬 이름

        private static bool showSceneViewButton = false;

        [InitializeOnLoadMethod]
        static void InitializeSceneViewButton()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!showSceneViewButton) return;

            Handles.BeginGUI();

            // Scene View 상단에 GUI 영역 생성
            GUILayout.BeginArea(new Rect(10, 10, 200, 100));

            // 배경 박스
            GUI.Box(new Rect(0, 0, 200, 80), "Effects Control");

            GUILayout.Space(20);

            // Play All Effects 버튼
            if (GUILayout.Button("Play All Effects", GUILayout.Height(25)))
            {
                if (IsEffectScene())
                {
                    PlayAllParticleSystems();
                }
                else
                {
                    Debug.LogWarning("이펙트 전용 씬에서만 사용할 수 있습니다.");
                }
            }

            // Stop All Effects 버튼
            if (GUILayout.Button("Stop All Effects", GUILayout.Height(25)))
            {
                if (IsEffectScene())
                {
                    StopAllParticleSystems();
                }
                else
                {
                    Debug.LogWarning("이펙트 전용 씬에서만 사용할 수 있습니다.");
                }
            }

            GUILayout.EndArea();

            Handles.EndGUI();
        }

        [MenuItem("CAT/Effects/Default Load")]
        static void LoadDefaultEffects()
        {
            if (!IsEffectScene())
            {
                ShowSceneWarning();
                return;
            }

            LoadEffectsByType(false); // 3D 이펙트 로드
            showSceneViewButton = true; // Scene View 버튼 활성화
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
            showSceneViewButton = true; // Scene View 버튼 활성화
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

            LoadAndArrangePrefabs(targetPrefabGuids.ToArray(), parentTransform, isUI);

            string loadedType = isUI ? "UI" : "3D";
            Debug.Log($"{loadedType} 이펙트 {targetPrefabGuids.Count}개를 로드했습니다.");
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

        static void LoadAndArrangePrefabs(string[] prefabGuids, Transform parent, bool isUI)
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
                        (x - GRID_X_LIMIT / 2f) * SPACING_UI, // UI 전용 간격
                        -y * SPACING_UI, // Y축 반전 (UI는 위에서 아래로)
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
            textObj.transform.SetParent(target.transform);

            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = target.name;
            textComponent.fontSize = 14;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;

            // 기본 폰트는 TextMeshPro에서 자동으로 할당됨

            // RectTransform 설정
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(0, -80, 0);
            rectTransform.sizeDelta = new Vector2(120, 30);
            rectTransform.localScale = Vector3.one;
        }

        static void Create3DNameLabel(GameObject target)
        {
            // 3D 텍스트 라벨 생성
            GameObject textObj = new GameObject("NameLabel");
            textObj.transform.SetParent(target.transform);

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = target.name;
            textMesh.fontSize = 12;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;

            // 위치 조정 (이펙트 아래쪽에 배치)
            textObj.transform.localPosition = new Vector3(0, -1.5f, 0);
            textObj.transform.localScale = Vector3.one * 0.1f;

            // 카메라를 향하도록 회전 (옵션)
            textObj.transform.LookAt(Camera.main?.transform ?? SceneView.lastActiveSceneView?.camera?.transform);
            textObj.transform.Rotate(0, 180, 0); // 텍스트가 뒤집히지 않도록
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
                // 강제 실행을 원한다면 여기서 LoadEffects를 호출할 수 있지만, 
                // 안전을 위해 경고만 표시하고 실행하지 않음
            }
        }

        static void PlayAllParticleSystems()
        {
            // 씬의 모든 게임오브젝트 선택 (이펙트 프리팹들 포함)
            SelectAllEffectObjects();

            // 씬의 모든 ParticleSystem 찾기 (비활성화된 것도 포함)
            ParticleSystem[] allParticleSystems = Resources.FindObjectsOfTypeAll<ParticleSystem>();

            int playedCount = 0;

            foreach (ParticleSystem ps in allParticleSystems)
            {
                // 씬에 있는 ParticleSystem만 필터링 (프리팹이나 에셋 제외)
                if (ps.gameObject.scene.IsValid())
                {
                    // GameObject가 활성화되어 있는지 확인
                    if (ps.gameObject.activeInHierarchy || ps.gameObject.activeSelf)
                    {
                        // ParticleSystem 재생
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Play(true);
                        playedCount++;
                    }
                }
            }

            Debug.Log($"총 {playedCount}개의 파티클 시스템을 재생했습니다.");
        }

        static void SelectAllEffectObjects()
        {
            // 씬의 모든 루트 게임오브젝트 가져오기
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            List<GameObject> allEffectObjects = new List<GameObject>();

            // Canvas 오브젝트가 있는 경우 (UI 이펙트)
            GameObject canvasObject = GameObject.Find("Effects Canvas");
            if (canvasObject != null)
            {
                // Canvas의 모든 자식 오브젝트들을 수집 (재귀적으로)
                foreach (Transform child in canvasObject.transform)
                {
                    if (child.name != "NameLabel") // 라벨 제외
                    {
                        // 부모 오브젝트 추가
                        allEffectObjects.Add(child.gameObject);

                        // 모든 자식들도 재귀적으로 추가
                        CollectAllChildren(child, allEffectObjects);
                    }
                }
            }
            else
            {
                // 3D 이펙트들 수집 (Canvas가 없는 경우)
                foreach (GameObject rootObj in rootObjects)
                {
                    if (rootObj.name != "NameLabel") // 라벨 제외
                    {
                        allEffectObjects.Add(rootObj);

                        // 3D 이펙트의 자식들도 수집
                        CollectAllChildren(rootObj.transform, allEffectObjects);
                    }
                }
            }

            // 모든 이펙트 오브젝트들을 선택
            Selection.objects = allEffectObjects.ToArray();

            Debug.Log($"총 {allEffectObjects.Count}개의 이펙트 오브젝트를 선택했습니다.");
        }

        static void CollectAllChildren(Transform parent, List<GameObject> collection)
        {
            foreach (Transform child in parent)
            {
                if (child.name != "NameLabel") // 라벨 제외
                {
                    collection.Add(child.gameObject);

                    // 재귀적으로 자식의 자식들도 수집
                    CollectAllChildren(child, collection);
                }
            }
        }

        static void StopAllParticleSystems()
        {
            // 씬의 모든 ParticleSystem 찾기
            ParticleSystem[] allParticleSystems = Resources.FindObjectsOfTypeAll<ParticleSystem>();

            int stoppedCount = 0;

            foreach (ParticleSystem ps in allParticleSystems)
            {
                // 씬에 있는 ParticleSystem만 필터링
                if (ps.gameObject.scene.IsValid())
                {
                    // ParticleSystem 정지
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    stoppedCount++;
                }
            }

            Debug.Log($"총 {stoppedCount}개의 파티클 시스템을 정지했습니다.");
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