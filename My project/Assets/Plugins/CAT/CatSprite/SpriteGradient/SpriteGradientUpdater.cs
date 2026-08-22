using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effect
{
    /// <summary>
    /// 활성화된 SpriteGradient를 한 곳에서 갱신한다.
    /// 인스턴스마다 Update/LateUpdate를 두면 오브젝트 수만큼 네이티브→매니지드 호출이 발생하므로,
    /// 드라이버 1개가 리스트를 순회하는 방식으로 대체한다.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(1000)] // Animator가 값을 기록한 뒤에 갱신되도록 뒤로 민다.
    internal sealed class SpriteGradientUpdater : MonoBehaviour
    {
        private static readonly List<SpriteGradient> targets = new List<SpriteGradient>();
        private static SpriteGradientUpdater runtimeDriver;

#if UNITY_EDITOR
        private static bool editorDriverHooked;
#endif

        internal static void Register(SpriteGradient target)
        {
            if (target == null || targets.Contains(target))
                return;

            targets.Add(target);
            EnsureDriver();
        }

        internal static void Unregister(SpriteGradient target)
        {
            targets.Remove(target);
        }

        private static void EnsureDriver()
        {
            if (Application.isPlaying)
            {
                if (runtimeDriver != null)
                    return;

                var go = new GameObject("[SpriteGradientUpdater]") { hideFlags = HideFlags.HideAndDontSave };
                runtimeDriver = go.AddComponent<SpriteGradientUpdater>();
                DontDestroyOnLoad(go);
                return;
            }

#if UNITY_EDITOR
            if (editorDriverHooked)
                return;

            editorDriverHooked = true;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

        private void LateUpdate()
        {
            Tick();
        }

#if UNITY_EDITOR
        private static void EditorTick()
        {
            if (Application.isPlaying)
                return;

            Tick();
        }
#endif

        private static void Tick()
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                SpriteGradient target = targets[i];

                if (target == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                target.Tick();
            }
        }
    }
}
