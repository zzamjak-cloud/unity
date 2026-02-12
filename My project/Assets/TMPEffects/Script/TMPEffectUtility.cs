using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMP 효과 공통 유틸리티
    /// - Material 해시 계산 등 공용 메서드 제공
    /// </summary>
    public static class TMPEffectUtility
    {
        /// <summary>FNV-1a 해시 알고리즘 소수</summary>
        private const uint FNV_PRIME = 16777619;

        /// <summary>FNV-1a 해시 알고리즘 오프셋</summary>
        private const uint FNV_OFFSET = 2166136261;

        /// <summary>
        /// Material 해시 계산 (FNV-1a 알고리즘)
        /// - Underlay 설정 기반 고유 해시 생성
        /// - TMPMaterialCache에서 Material 공유에 사용
        /// </summary>
        public static int CalculateMaterialHash(
            Color underlayColor,
            float underlayDilate,
            float underlayOffsetX,
            float underlayOffsetY,
            float underlaySoftness,
            float faceDilate)
        {
            unchecked
            {
                uint hash = FNV_OFFSET;

                Color32 c = underlayColor;
                hash = (hash ^ c.r) * FNV_PRIME;
                hash = (hash ^ c.g) * FNV_PRIME;
                hash = (hash ^ c.b) * FNV_PRIME;
                hash = (hash ^ c.a) * FNV_PRIME;

                hash = HashFloat(hash, underlayDilate);
                hash = HashFloat(hash, underlayOffsetX);
                hash = HashFloat(hash, underlayOffsetY);
                hash = HashFloat(hash, underlaySoftness);
                hash = HashFloat(hash, faceDilate);

                return (int)hash;
            }
        }

        /// <summary>
        /// float 값을 해시에 추가
        /// </summary>
        private static uint HashFloat(uint hash, float value)
        {
            int bits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(value), 0);
            return (hash ^ (uint)bits) * FNV_PRIME;
        }
    }
}
