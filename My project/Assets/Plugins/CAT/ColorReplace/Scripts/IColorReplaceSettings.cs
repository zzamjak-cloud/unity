using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// ColorReplace 효과 설정 인터페이스
    /// Material 공유를 위한 공통 설정 정의
    /// </summary>
    public interface IColorReplaceSettings
    {
        /// <summary>HSV 범위 최소값 (0~1)</summary>
        float HSVRangeMin { get; }

        /// <summary>HSV 범위 최대값 (0~1)</summary>
        float HSVRangeMax { get; }

        /// <summary>HSV 조정값 (H, S, V, A)</summary>
        Vector4 HSVAdjust { get; }

        /// <summary>
        /// Material 공유를 위한 해시 계산
        /// 같은 해시 = 같은 Material 공유
        /// </summary>
        int GetMaterialHash();
    }
}
