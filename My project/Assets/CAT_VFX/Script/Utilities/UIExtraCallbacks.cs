using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.VFX.Internal
{
    /// <summary>
    /// Canvas 리빌드 이후 콜백을 제공하는 유틸리티
    /// Canvas.willRenderCanvases 이중 등록 트릭으로 정확한 타이밍 보장
    /// </summary>
    internal static class UIExtraCallbacks
    {
        private static bool s_IsInitializedAfterCanvasRebuild;
        private static readonly FastAction s_AfterCanvasRebuildAction = new FastAction();

        // 정적 생성자: CanvasUpdateRegistry보다 먼저 등록
        static UIExtraCallbacks()
        {
            Canvas.willRenderCanvases += OnBeforeCanvasRebuild;
        }

        /// <summary>
        /// Canvas 리빌드 이후 발생하는 이벤트
        /// </summary>
        public static event Action onAfterCanvasRebuild
        {
            add => s_AfterCanvasRebuildAction.Add(value);
            remove => s_AfterCanvasRebuildAction.Remove(value);
        }

        /// <summary>
        /// Canvas 리빌드 이후 콜백을 지연 등록
        /// CanvasUpdateRegistry.IsRebuildingLayout() 호출로 레이아웃 리빌드 이후 타이밍 확보
        /// </summary>
        private static void InitializeAfterCanvasRebuild()
        {
            if (s_IsInitializedAfterCanvasRebuild) return;
            s_IsInitializedAfterCanvasRebuild = true;

            CanvasUpdateRegistry.IsRebuildingLayout();
            Canvas.willRenderCanvases += OnAfterCanvasRebuild;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            Canvas.willRenderCanvases -= OnAfterCanvasRebuild;
            s_IsInitializedAfterCanvasRebuild = false;
        }

        private static void OnBeforeCanvasRebuild()
        {
            InitializeAfterCanvasRebuild();
        }

        private static void OnAfterCanvasRebuild()
        {
            s_AfterCanvasRebuildAction.Invoke();
        }
    }
}
