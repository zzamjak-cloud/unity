using System.Collections.Generic;
using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMP 효과 Material 공유 시스템
    /// - SoftMask 패턴 기반 Material 캐싱
    /// - 해시 기반 공유로 Draw Call 최소화
    /// - 모바일 최적화: Material 인스턴스 수 최소화
    /// </summary>
    public static class TMPEffectManager
    {
        // ─────────────────────────────────────────────
        // Material 캐시
        // ─────────────────────────────────────────────

        private static Dictionary<int, Material> s_sharedMaterials = new Dictionary<int, Material>();

        // ─────────────────────────────────────────────
        // Shader Property ID 캐싱 (Outline용)
        // ─────────────────────────────────────────────

        public static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
        public static readonly int PropOutlineSoftness = Shader.PropertyToID("_OutlineSoftness");

        // ─────────────────────────────────────────────
        // 퍼블릭 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 캐시에서 가져오거나 새로 생성
        /// - 같은 Shader + Texture + Parameters = 동일 Material 공유
        /// </summary>
        public static Material GetOrCreateMaterial(
            Shader shader,
            Texture mainTex,
            int parametersHash)
        {
            if (shader == null)
            {
                Debug.LogError("[TMPEffectManager] Shader is null!");
                return null;
            }

            // 해시 계산 (Shader + Texture + Parameters)
            int hash = CombineHash(
                shader.GetInstanceID(),
                mainTex != null ? mainTex.GetInstanceID() : 0,
                parametersHash
            );

            // 캐시 확인
            if (s_sharedMaterials.TryGetValue(hash, out Material mat))
            {
                if (mat != null)
                {
                    return mat;
                }
                else
                {
                    // Material이 파괴된 경우 캐시에서 제거
                    s_sharedMaterials.Remove(hash);
                }
            }

            // 새 Material 생성
            mat = new Material(shader)
            {
                mainTexture = mainTex,
                hideFlags = HideFlags.DontSave  // 필수! (씬 저장 제외)
            };

            s_sharedMaterials[hash] = mat;

            return mat;
        }

        /// <summary>
        /// Material 캐시에서 제거
        /// </summary>
        public static void ReleaseMaterial(Material mat)
        {
            if (mat == null) return;

            // 캐시에서 찾아서 제거
            foreach (var kvp in s_sharedMaterials)
            {
                if (kvp.Value == mat)
                {
                    s_sharedMaterials.Remove(kvp.Key);
                    break;
                }
            }

            // Material 파괴
            if (Application.isPlaying)
            {
                Object.Destroy(mat);
            }
            else
            {
                Object.DestroyImmediate(mat);
            }
        }

        /// <summary>
        /// 모든 캐시된 Material 정리
        /// </summary>
        public static void ClearCache()
        {
            foreach (var mat in s_sharedMaterials.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(mat);
                    }
                    else
                    {
                        Object.DestroyImmediate(mat);
                    }
                }
            }
            s_sharedMaterials.Clear();
        }

        // ─────────────────────────────────────────────
        // 내부 유틸리티
        // ─────────────────────────────────────────────

        private static int CombineHash(int hash1, int hash2, int hash3)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + hash1;
                hash = hash * 31 + hash2;
                hash = hash * 31 + hash3;
                return hash;
            }
        }

        // ─────────────────────────────────────────────
        // 정리 (애플리케이션 종료 시)
        // ─────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            ClearCache();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 캐시 통계
        /// </summary>
        public static int CachedMaterialCount => s_sharedMaterials.Count;

        /// <summary>
        /// 에디터 전용: 모든 캐시된 Material 가져오기
        /// </summary>
        public static IEnumerable<Material> GetAllCachedMaterials()
        {
            foreach (var mat in s_sharedMaterials.Values)
            {
                if (mat != null)
                {
                    yield return mat;
                }
            }
        }
#endif
    }
}
