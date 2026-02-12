using UnityEngine;
using UnityEditor;

namespace CAT.UI
{
    /// <summary>
    /// TMPAnimation 컴포넌트 추가 시 CanvasGroup 자동 추가
    /// </summary>
    [InitializeOnLoad]
    public static class TMPAnimationComponentHandler
    {
        static TMPAnimationComponentHandler()
        {
            // 컴포넌트 변경 이벤트 등록
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        private static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                var eventType = stream.GetEventType(i);

                // 컴포넌트 추가 이벤트만 처리
                if (eventType == ObjectChangeKind.CreateGameObjectHierarchy ||
                    eventType == ObjectChangeKind.ChangeGameObjectStructure)
                {
                    CheckAndAddCanvasGroup();
                }
            }
        }

        /// <summary>
        /// TMPAnimation이 있는 오브젝트에 CanvasGroup 자동 추가
        /// </summary>
        private static void CheckAndAddCanvasGroup()
        {
            // 현재 선택된 오브젝트 확인
            if (Selection.activeGameObject == null) return;

            var animation = Selection.activeGameObject.GetComponent<TMPAnimation>();
            if (animation != null)
            {
                var canvasGroup = Selection.activeGameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    // CanvasGroup 추가
                    Undo.AddComponent<CanvasGroup>(Selection.activeGameObject);
                    Debug.Log($"[TMPAnimation] CanvasGroup 자동 추가: {Selection.activeGameObject.name}");
                }
            }
        }

        /// <summary>
        /// 씬의 모든 TMPAnimation 오브젝트 확인 (메뉴 명령어)
        /// </summary>
        [MenuItem("CAT/UI/TMPAnimation/씬의 모든 오브젝트에 CanvasGroup 추가")]
        private static void AddCanvasGroupToAllTMPAnimations()
        {
            var animations = Object.FindObjectsOfType<TMPAnimation>(true);
            int addedCount = 0;

            foreach (var animation in animations)
            {
                var canvasGroup = animation.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    Undo.AddComponent<CanvasGroup>(animation.gameObject);
                    addedCount++;
                }
            }

            Debug.Log($"[TMPAnimation] {addedCount}개 오브젝트에 CanvasGroup 추가 완료 (전체: {animations.Length}개)");
        }
    }
}
