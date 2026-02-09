using System.Collections.Generic;
using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMP 효과 Material 공유 시스템
    /// - 해시 기반 Material 캐싱으로 인스턴스 수 최소화
    /// - 같은 설정 = 같은 Material 공유
    /// - 모바일 최적화: Draw Call 감소
    /// - TMPMaterialCache 래퍼 역할 (하위 호환성 유지)
    /// </summary>
    public static class TMPEffectManager
    {

        // ─────────────────────────────────────────────
        // Shader Property ID 캐싱 (성능 최적화)
        // ─────────────────────────────────────────────

        /// <summary>TMP Shader Property: _UnderlayColor (Underlay/Outline 색상)</summary>
        public static readonly int PropUnderlayColor = Shader.PropertyToID("_UnderlayColor");

        /// <summary>TMP Shader Property: _UnderlayOffsetX (X 오프셋, 0=Outline)</summary>
        public static readonly int PropUnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");

        /// <summary>TMP Shader Property: _UnderlayOffsetY (Y 오프셋, 0=Outline)</summary>
        public static readonly int PropUnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");

        /// <summary>TMP Shader Property: _UnderlayDilate (Underlay 두께)</summary>
        public static readonly int PropUnderlayDilate = Shader.PropertyToID("_UnderlayDilate");

        /// <summary>TMP Shader Property: _UnderlaySoftness (Underlay 부드러움)</summary>
        public static readonly int PropUnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");

        /// <summary>TMP Shader Property: _FaceDilate (Face 두께)</summary>
        public static readonly int PropFaceDilate = Shader.PropertyToID("_FaceDilate");

        // ─────────────────────────────────────────────
        // 퍼블릭 API (TMPMaterialCache 래퍼)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 캐시에서 가져오거나 새로 생성
        /// - 같은 원본 Material + 효과 설정 = 동일 Material 공유
        /// - TMPMaterialCache에 위임
        /// </summary>
        public static Material GetOrCreateMaterial(
            Material baseMaterial,
            Color underlayColor,
            float underlayDilate,
            float underlayOffsetX,
            float underlayOffsetY,
            float underlaySoftness,
            float faceDilate)
        {
            // 임시 설정 객체 생성
            var settings = new TempEffectSettings
            {
                UnderlayColor = underlayColor,
                UnderlayDilate = underlayDilate,
                UnderlayOffsetX = underlayOffsetX,
                UnderlayOffsetY = underlayOffsetY,
                UnderlaySoftness = underlaySoftness,
                FaceDilate = faceDilate
            };

            return TMPMaterialCache.Instance.GetOrCreate(baseMaterial, settings);
        }

        /// <summary>
        /// Preset을 사용한 Material 가져오기 (편의 메서드)
        /// </summary>
        public static Material GetOrCreateMaterial(Material baseMaterial, ITMPEffectSettings settings)
        {
            return TMPMaterialCache.Instance.GetOrCreate(baseMaterial, settings);
        }

        /// <summary>
        /// 모든 캐시된 Material 정리
        /// </summary>
        public static void ClearCache()
        {
            TMPMaterialCache.Instance.Clear();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 캐시 통계
        /// </summary>
        public static int CachedMaterialCount => TMPMaterialCache.Instance.GetStats().CachedCount;

        /// <summary>
        /// 에디터 전용: 캐시 통계 가져오기
        /// </summary>
        public static TMPMaterialCache.CacheStats GetCacheStats()
        {
            return TMPMaterialCache.Instance.GetStats();
        }
#endif

        // ─────────────────────────────────────────────
        // 내부 구조체
        // ─────────────────────────────────────────────

        /// <summary>
        /// 임시 설정 객체 (레거시 API용)
        /// </summary>
        private struct TempEffectSettings : ITMPEffectSettings
        {
            public Color UnderlayColor { get; set; }
            public float UnderlayDilate { get; set; }
            public float UnderlayOffsetX { get; set; }
            public float UnderlayOffsetY { get; set; }
            public float UnderlaySoftness { get; set; }
            public float FaceDilate { get; set; }
            public bool EnableShadow => false;
            public Vector2 ShadowOffset => Vector2.zero;
            public float ShadowAlpha => 0f;

            public int GetMaterialHash()
            {
                unchecked
                {
                    const uint FNV_PRIME = 16777619;
                    const uint FNV_OFFSET = 2166136261;
                    uint hash = FNV_OFFSET;

                    Color32 c = UnderlayColor;
                    hash = (hash ^ c.r) * FNV_PRIME;
                    hash = (hash ^ c.g) * FNV_PRIME;
                    hash = (hash ^ c.b) * FNV_PRIME;
                    hash = (hash ^ c.a) * FNV_PRIME;

                    int dilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(UnderlayDilate), 0);
                    int offsetX = System.BitConverter.ToInt32(System.BitConverter.GetBytes(UnderlayOffsetX), 0);
                    int offsetY = System.BitConverter.ToInt32(System.BitConverter.GetBytes(UnderlayOffsetY), 0);
                    int softness = System.BitConverter.ToInt32(System.BitConverter.GetBytes(UnderlaySoftness), 0);
                    int faceDilate = System.BitConverter.ToInt32(System.BitConverter.GetBytes(FaceDilate), 0);

                    hash = (hash ^ (uint)dilate) * FNV_PRIME;
                    hash = (hash ^ (uint)offsetX) * FNV_PRIME;
                    hash = (hash ^ (uint)offsetY) * FNV_PRIME;
                    hash = (hash ^ (uint)softness) * FNV_PRIME;
                    hash = (hash ^ (uint)faceDilate) * FNV_PRIME;

                    return (int)hash;
                }
            }
        }
    }
}
