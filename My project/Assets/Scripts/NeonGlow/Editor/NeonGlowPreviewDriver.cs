using UnityEditor;
using UnityEngine;

namespace CAT.Effects.EditorTools
{
    /// <summary>
    /// 씬 뷰 깜빡임 프리뷰 구동기.
    ///
    /// 인스펙터 Editor 인스턴스는 선택 변경·에셋 임포트로 수시로 재생성되므로,
    /// 프리뷰 상태를 Editor 에 두면 재생 직후 꺼져 버린다. static 으로 분리해 수명을 나눈다.
    ///
    /// 편집 모드에서는 셰이더의 _Time 이 자유롭게 흐르지 않기 때문에,
    /// 여기서 누적한 시간을 NeonGlow 가 _PreviewTime 으로 밀어 넣는다.
    /// 덕분에 Warmup 처럼 시작 시점이 중요한 연출도 항상 처음부터 재생된다.
    /// </summary>
    [InitializeOnLoad]
    internal static class NeonGlowPreviewDriver
    {
        public const float MaxPreviewSeconds = 60f;

        private static NeonGlow target;
        private static double lastUpdateTime;
        private static float elapsed;

        static NeonGlowPreviewDriver()
        {
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += _ => Stop();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (a, b) => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static bool IsRunning(NeonGlow neon)
            => neon != null && target == neon && neon.IsEditorPreviewActive;

        public static float RemainingSeconds => Mathf.Max(0f, MaxPreviewSeconds - elapsed);

        public static void Start(NeonGlow neon)
        {
            Stop();

            if (neon == null || !neon.EditorPreviewBegin())
                return;

            target = neon;
            elapsed = 0f;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            if (target != null)
            {
                target.EditorPreviewEnd();
                SceneView.RepaintAll();
            }

            target = null;
            elapsed = 0f;
        }

        // 다른 오브젝트를 선택했을 때만 멈춘다 (Editor 재생성과 구분)
        private static void OnSelectionChanged()
        {
            if (target != null && Selection.activeGameObject != target.gameObject)
                Stop();
        }

        private static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastUpdateTime);
            lastUpdateTime = now;

            if (target == null || !target.IsEditorPreviewActive)
                return;

            // 에디터가 멈췄다 재개될 때 한 번에 튀는 것을 막는다
            delta = Mathf.Min(delta, 0.1f);

            elapsed += delta;
            if (elapsed >= MaxPreviewSeconds)
            {
                Stop();
                return;
            }

            target.EditorPreviewTick(delta);
            SceneView.RepaintAll();
        }
    }
}
