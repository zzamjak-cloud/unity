using System.Collections.Generic;
using CAT.VFX.Internal;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.VFX
{
    /// <summary>
    /// 모든 활성 CatUIParticle의 업데이트를 관리하는 정적 오케스트레이터
    /// Canvas.willRenderCanvases 이후 타이밍에 실행
    /// </summary>
    public static class CatUIParticleUpdater
    {
        private static readonly List<CatUIParticle> s_ActiveParticles = new List<CatUIParticle>();
        private static int s_FrameCount;

        public static int uiParticleCount => s_ActiveParticles.Count;

        public static void Register(CatUIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Add(particle);
        }

        public static void Unregister(CatUIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Remove(particle);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            UIExtraCallbacks.onAfterCanvasRebuild += Refresh;

            EditorApplication.playModeStateChanged += state =>
            {
                UIExtraCallbacks.onAfterCanvasRebuild -= Refresh;
                if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                {
                    UIExtraCallbacks.onAfterCanvasRebuild += Refresh;
                }
            };
        }
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            UIExtraCallbacks.onAfterCanvasRebuild += Refresh;
        }
#endif

        /// <summary>
        /// 매 프레임 Canvas 리빌드 이후 호출 - 모든 활성 파티클 업데이트
        /// </summary>
        private static void Refresh()
        {
            // 같은 프레임에서 중복 호출 방지
            if (s_FrameCount == Time.frameCount) return;
            s_FrameCount = Time.frameCount;

            for (var i = 0; i < s_ActiveParticles.Count; i++)
            {
                var uip = s_ActiveParticles[i];
                if (!uip || !uip.canvas) continue;

                uip.UpdateTransformScale();
                uip.UpdateRenderers();
            }
        }
    }
}
