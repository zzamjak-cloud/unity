using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>프레임 단위 갱신이 필요한 동안에만 드라이버에 등록되는 이펙트.</summary>
    internal interface ISpriteEffectTickable
    {
        void Tick();
    }

    /// <summary>
    /// 효과가 진행 중인 SpriteEffect / SpriteGroupEffect만 모아 한 곳에서 갱신한다.
    /// 인스턴스마다 LateUpdate를 두면 오브젝트 수만큼 네이티브→매니지드 호출이 발생하므로,
    /// 드라이버 1개가 리스트를 순회하는 방식으로 대체한다.
    /// 효과가 꺼진 인스턴스는 등록조차 되지 않으므로 평상시 순회 비용이 0이다.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(1000)] // Animator가 값을 기록한 뒤에 갱신되도록 뒤로 민다.
    internal sealed class SpriteEffectUpdater : MonoBehaviour
    {
        private static readonly List<ISpriteEffectTickable> targets = new List<ISpriteEffectTickable>();
        private static SpriteEffectUpdater runtimeDriver;

#if UNITY_EDITOR
        private static bool editorDriverHooked;
#endif

        internal static void Register(ISpriteEffectTickable target)
        {
            if (target == null || targets.Contains(target))
                return;

            targets.Add(target);
            EnsureDriver();
        }

        internal static void Unregister(ISpriteEffectTickable target)
        {
            targets.Remove(target);
        }

        private static void EnsureDriver()
        {
            if (Application.isPlaying)
            {
                if (runtimeDriver != null)
                    return;

                var go = new GameObject("[SpriteEffectUpdater]") { hideFlags = HideFlags.HideAndDontSave };
                runtimeDriver = go.AddComponent<SpriteEffectUpdater>();
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
                ISpriteEffectTickable target = targets[i];

                // 인터페이스 참조로는 Unity의 null 비교 연산자가 동작하지 않으므로 Object로 확인한다.
                if (target == null || (target as Object) == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                target.Tick();
            }
        }
    }
}
