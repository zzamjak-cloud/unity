using System.Collections.Generic;
using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMP Material 캐싱 시스템
    /// - 해시 기반 Material 공유
    /// - 메모리 및 Draw Call 최적화
    /// </summary>
    public class TMPMaterialCache
    {
        // 싱글톤 인스턴스
        private static TMPMaterialCache s_instance;
        public static TMPMaterialCache Instance => s_instance ?? (s_instance = new TMPMaterialCache());

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

        /// <summary>기본 캐시 용량 (예상 Material 수)</summary>
        private const int DEFAULT_CACHE_CAPACITY = 32;

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 가져오기 또는 생성 (해시 기반 캐싱)
        /// </summary>
        /// <param name="baseMaterial">베이스 Material (TMP 폰트 Material)</param>
        /// <param name="settings">효과 설정</param>
        /// <returns>공유 Material (캐시 히트 시 기존 반환, 미스 시 새로 생성)</returns>
        /// <remarks>
        /// 같은 Base Material + 같은 설정 = 동일 Material 공유
        /// - 메모리 절감: 100개 컴포넌트 → 5-10개 Material
        /// - Draw Call 감소: 배칭 최적화
        /// </remarks>
        public Material GetOrCreate(
            Material baseMaterial,
            ITMPEffectSettings settings)
        {
            if (!baseMaterial)
            {
                return null;
            }

            int hash = CalculateHash(baseMaterial, settings);

            // 캐시 확인
            if (_cache.TryGetValue(hash, out Material cached))
            {
                if (cached)
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
            Material newMaterial = CreateMaterial(baseMaterial, settings);
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
                if (mat)
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
        /// <param name="baseMaterial">베이스 Material</param>
        /// <param name="settings">효과 설정</param>
        /// <returns>생성된 Material (HideFlags.DontSave 설정됨)</returns>
        /// <remarks>
        /// 런타임 Material 생성 시 필수:
        /// - HideFlags.DontSave: 씬 저장 제외 (DontSaveInEditor Assertion 방지)
        /// - UNDERLAY_ON 키워드: TMP 셰이더의 Underlay 기능 활성화
        /// </remarks>
        private Material CreateMaterial(Material baseMaterial, ITMPEffectSettings settings)
        {
            var mat = new Material(baseMaterial)
            {
                name = $"{baseMaterial.name} (Shared)",
                hideFlags = HideFlags.DontSave
            };

            // Underlay 설정 (Shader Property ID는 TMPEffectManager에서 캐싱)
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(TMPEffectManager.PropUnderlayColor, settings.UnderlayColor);
            mat.SetFloat(TMPEffectManager.PropUnderlayOffsetX, settings.UnderlayOffsetX);
            mat.SetFloat(TMPEffectManager.PropUnderlayOffsetY, settings.UnderlayOffsetY);
            mat.SetFloat(TMPEffectManager.PropUnderlayDilate, settings.UnderlayDilate);
            mat.SetFloat(TMPEffectManager.PropUnderlaySoftness, settings.UnderlaySoftness);
            mat.SetFloat(TMPEffectManager.PropFaceDilate, settings.FaceDilate);

            return mat;
        }

        /// <summary>
        /// 최적화된 해시 계산 (충돌 최소화)
        /// </summary>
        /// <param name="baseMaterial">베이스 Material (TMP 폰트 Material)</param>
        /// <param name="settings">효과 설정 (Underlay, Face Dilate 등)</param>
        /// <returns>고유 해시 값 (FNV-1a 기반)</returns>
        /// <remarks>
        /// FNV-1a 알고리즘으로 충돌 최소화:
        /// - Base Material InstanceID (32bit) 분해
        /// - Settings Hash (32bit) 분해
        /// - 각 바이트를 XOR 후 FNV 소수로 곱셈
        /// </remarks>
        private int CalculateHash(Material baseMaterial, ITMPEffectSettings settings)
        {
            unchecked
            {
                uint hash = FNV_OFFSET;

                // Base Material ID 해싱
                int materialId = baseMaterial.GetInstanceID();
                hash ^= (uint)(materialId & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((materialId >> 8) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((materialId >> 16) & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (uint)((materialId >> 24) & 0xFF);
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
