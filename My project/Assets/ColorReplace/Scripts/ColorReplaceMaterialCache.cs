using System.Collections.Generic;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// ColorReplace Material 캐싱 시스템
    /// - 프리셋 기반 Material 공유
    /// - 메모리 및 Draw Call 최적화
    /// </summary>
    public class ColorReplaceMaterialCache
    {
        // 싱글톤 인스턴스
        private static ColorReplaceMaterialCache s_instance;
        public static ColorReplaceMaterialCache Instance => s_instance ?? (s_instance = new ColorReplaceMaterialCache());

        // Material 캐시 (초기 용량 설정으로 리사이징 최소화)
        private readonly Dictionary<int, Material> _cache = new Dictionary<int, Material>(DEFAULT_CACHE_CAPACITY);

        // 통계
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;

        // ─────────────────────────────────────────────
        // 상수
        // ─────────────────────────────────────────────

        /// <summary>FNV-1a 해시 알고리즘 소수</summary>
        private const uint FNV_PRIME = 16777619;

        /// <summary>FNV-1a 해시 알고리즘 오프셋</summary>
        private const uint FNV_OFFSET = 2166136261;

        /// <summary>기본 캐시 용량</summary>
        private const int DEFAULT_CACHE_CAPACITY = 32;

        // Shader Property ID 캐싱
        private static readonly int PropHSVRangeMin = Shader.PropertyToID("_HSVRangeMin");
        private static readonly int PropHSVRangeMax = Shader.PropertyToID("_HSVRangeMax");
        private static readonly int PropHSVAdjust = Shader.PropertyToID("_HSVAAdjust");

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 가져오기 또는 생성 (프리셋 기반 캐싱)
        /// </summary>
        /// <param name="shader">ColorReplace 셰이더</param>
        /// <param name="textureId">텍스처 Instance ID (0 = 텍스처 없음)</param>
        /// <param name="settings">효과 설정 (프리셋)</param>
        /// <returns>공유 Material</returns>
        public Material GetOrCreate(Shader shader, int textureId, IColorReplaceSettings settings)
        {
            if (shader == null || settings == null)
            {
                return null;
            }

            int hash = CalculateHash(textureId, settings);

            // 캐시 확인
            if (_cache.TryGetValue(hash, out Material cached))
            {
                if (cached != null)
                {
                    _cacheHitCount++;
                    return cached;
                }
                else
                {
                    // 파괴된 Material 제거
                    _cache.Remove(hash);
                }
            }

            // 새 Material 생성
            _cacheMissCount++;
            Material newMaterial = CreateMaterial(shader, settings);
            _cache[hash] = newMaterial;

            return newMaterial;
        }

        /// <summary>
        /// 캐시 정리
        /// </summary>
        public void Clear()
        {
            foreach (var mat in _cache.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(mat);
                    else
                        Object.DestroyImmediate(mat);
                }
            }

            _cache.Clear();
            _cacheHitCount = 0;
            _cacheMissCount = 0;
        }

        /// <summary>
        /// 캐시 통계
        /// </summary>
        public CacheStats GetStats()
        {
            return new CacheStats
            {
                CachedCount = _cache.Count,
                HitCount = _cacheHitCount,
                MissCount = _cacheMissCount,
                HitRate = _cacheHitCount + _cacheMissCount > 0
                    ? (float)_cacheHitCount / (_cacheHitCount + _cacheMissCount)
                    : 0f
            };
        }

        // ─────────────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────────────

        /// <summary>
        /// 새 Material 생성 및 설정 적용
        /// </summary>
        private Material CreateMaterial(Shader shader, IColorReplaceSettings settings)
        {
            var mat = new Material(shader)
            {
                name = $"{ColorReplace.SHADER_NAME} (Preset Shared)",
                hideFlags = HideFlags.DontSave
            };

            // HSV 설정 적용
            mat.SetFloat(PropHSVRangeMin, settings.HSVRangeMin);
            mat.SetFloat(PropHSVRangeMax, settings.HSVRangeMax);
            mat.SetVector(PropHSVAdjust, settings.HSVAdjust);

            return mat;
        }

        /// <summary>
        /// 최적화된 해시 계산 (텍스처 ID + 설정 해시)
        /// </summary>
        private int CalculateHash(int textureId, IColorReplaceSettings settings)
        {
            unchecked
            {
                uint hash = FNV_OFFSET;

                // Texture ID 해싱
                hash ^= (uint)(textureId & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((textureId >> 8) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((textureId >> 16) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((textureId >> 24) & 0xFF);
                hash *= FNV_PRIME;

                // Settings Hash 조합
                int settingsHash = settings.GetMaterialHash();
                hash ^= (uint)(settingsHash & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((settingsHash >> 8) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((settingsHash >> 16) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((settingsHash >> 24) & 0xFF);
                hash *= FNV_PRIME;

                return (int)hash;
            }
        }

        // ─────────────────────────────────────────────
        // 정리 (애플리케이션 종료 시)
        // ─────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            s_instance?.Clear();
            s_instance = null;
        }

        // ─────────────────────────────────────────────
        // Nested Types
        // ─────────────────────────────────────────────

        public struct CacheStats
        {
            public int CachedCount;
            public int HitCount;
            public int MissCount;
            public float HitRate;

            public override string ToString()
            {
                return $"Cached: {CachedCount}, Hits: {HitCount}, Misses: {MissCount}, Hit Rate: {HitRate:P1}";
            }
        }
    }
}
