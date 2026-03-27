using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

namespace CAT.VFX.Editor
{
    /// <summary>
    /// VFX 프리팹 인스턴스 생성 공용 유틸리티.
    /// VFXPreviewer, HierarchyVFXModule에서 공유.
    /// </summary>
    internal static class VFXInstantiateHelper
    {
        /// <summary>
        /// 프리팹을 하이어라키에 인스턴스.
        /// useUI=true면 CatUIParticle 래핑 + Canvas 자동 탐색/생성.
        /// 이미 CatUIParticle이 포함된 프리팹은 useUI=true와 동일하게 Canvas 하위 배치.
        /// </summary>
        public static void Instantiate(GameObject prefab, bool useUI)
        {
            if (prefab == null) return;

            var parentObject = Selection.activeGameObject;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            bool hasCatUIParticle = prefab.GetComponentInChildren<CatUIParticle>(true) != null;

            // UI 모드 판정: useUI 활성화이거나 이미 CatUIParticle이 포함된 프리팹
            bool needsUI = useUI || hasCatUIParticle;

            if (needsUI && !hasCatUIParticle)
            {
                // CatUIParticle 래핑 생성
                CreateAsUIParticle(prefab, parentObject, prefabStage);
            }
            else if (needsUI && hasCatUIParticle)
            {
                // 이미 CatUIParticle 포함 → 래핑 없이 Canvas 하위에 배치
                CreateExistingUIParticle(prefab, parentObject, prefabStage);
            }
            else
            {
                // 일반 프리팹 (Canvas 밖에 생성)
                CreateAsRegularPrefab(prefab, parentObject, prefabStage);
            }
        }

        // ── 일반 프리팹 (Canvas 밖) ─────────────────────────────────────────
        private static void CreateAsRegularPrefab(GameObject prefab, GameObject parentObject, PrefabStage prefabStage)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = prefab.name;

            if (prefabStage != null)
            {
                // 프리팹 편집 모드
                if (parentObject != null && parentObject.scene == prefabStage.scene)
                    instance.transform.SetParent(parentObject.transform, false);
                else
                    instance.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }
            else if (parentObject != null && !EditorUtility.IsPersistent(parentObject))
            {
                // Canvas 내부 오브젝트를 선택 중이면 Canvas 밖에 생성 (부모 설정 안 함)
                if (parentObject.GetComponentInParent<Canvas>(true) != null)
                {
                    // Canvas 내부 선택 → 부모 없이 씬 루트에 생성
                }
                else
                {
                    instance.transform.SetParent(parentObject.transform, false);
                }
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Create VFX {instance.name}");
            Selection.activeObject = instance;
        }

        // ── 이미 CatUIParticle이 포함된 프리팹 → Canvas 하위 배치 ────────────
        private static void CreateExistingUIParticle(GameObject prefab, GameObject parentObject, PrefabStage prefabStage)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = prefab.name;

            var canvasParent = ResolveCanvasParent(parentObject, prefabStage);
            instance.transform.SetParent(canvasParent, false);

            SetLayerRecursive(instance, LayerMask.NameToLayer("UI"));

            Undo.RegisterCreatedObjectUndo(instance, $"Create UI VFX {instance.name}");
            Selection.activeObject = instance;
        }

        // ── CatUIParticle 래핑 생성 ─────────────────────────────────────────
        private static void CreateAsUIParticle(GameObject prefab, GameObject parentObject, PrefabStage prefabStage)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Create UI VFX {prefab.name}");

            var uiGo = new GameObject(prefab.name, typeof(RectTransform), typeof(CatUIParticle));
            var uiParticle = uiGo.GetComponent<CatUIParticle>();
            uiGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            var canvasParent = ResolveCanvasParent(parentObject, prefabStage);
            uiGo.transform.SetParent(canvasParent, false);

            Undo.RegisterCreatedObjectUndo(uiGo, $"Create UI VFX {prefab.name}");

            var vfxInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            vfxInstance.transform.SetParent(uiGo.transform, false);
            vfxInstance.transform.localPosition = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(vfxInstance, $"Instantiate VFX {prefab.name}");

            SetLayerRecursive(uiGo, LayerMask.NameToLayer("UI"));
            uiParticle.RefreshParticles();

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeObject = uiGo;
        }

        // ── Canvas 부모 결정 ────────────────────────────────────────────────
        /// <summary>
        /// UI 프리팹의 부모 Transform을 결정.
        /// 1. 프리팹 편집 모드 → 프리팹 내부 자식
        /// 2. 선택된 오브젝트가 Canvas 내부 → 해당 오브젝트의 자식
        /// 3. Canvas가 씬에 존재 → 해당 Canvas의 자식
        /// 4. Canvas 없음 → 신규 Canvas 생성
        /// </summary>
        private static Transform ResolveCanvasParent(GameObject parentObject, PrefabStage prefabStage)
        {
            // 프리팹 편집 모드
            if (prefabStage != null)
            {
                if (parentObject != null && parentObject.scene == prefabStage.scene)
                    return parentObject.transform;
                return prefabStage.prefabContentsRoot.transform;
            }

            // 선택된 오브젝트가 Canvas 내부인지 확인
            if (parentObject != null && !EditorUtility.IsPersistent(parentObject))
            {
                var parentCanvas = parentObject.GetComponentInParent<Canvas>(true);
                if (parentCanvas != null)
                    return parentObject.transform;
            }

            // 씬에서 기존 Canvas 탐색
            var existingCanvas = Object.FindFirstObjectByType<Canvas>();
            if (existingCanvas != null)
                return existingCanvas.transform;

            // Canvas 신규 생성
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvasGo.transform;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }
}
