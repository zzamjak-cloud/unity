using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMP 효과 설정 인터페이스
    /// </summary>
    /// <remarks>
    /// 설계 목적:
    /// - Preset (ScriptableObject)과 Component (MonoBehaviour) 간 설정 공유
    /// - Material 캐싱을 위한 표준 인터페이스
    /// - 새로운 효과 추가 시 확장 가능
    ///
    /// 구현 클래스:
    /// - TMPEffectPreset: ScriptableObject (에셋으로 저장 가능)
    /// - TMPOutlineEffect: MonoBehaviour (런타임 컴포넌트)
    /// - TempEffectSettings: struct (레거시 API용 임시 객체)
    /// </remarks>
    public interface ITMPEffectSettings
    {
        // ─────────────────────────────────────────────
        // Underlay Settings (GPU 기반, TMP 셰이더)
        // ─────────────────────────────────────────────

        /// <summary>Underlay 색상 (Outline/Shadow 색상)</summary>
        Color UnderlayColor { get; }

        /// <summary>Underlay 두께 (0~1, Outline 너비 결정)</summary>
        float UnderlayDilate { get; }

        /// <summary>Underlay X 오프셋 (-1~1, 0이면 Outline, 0이 아니면 Drop Shadow)</summary>
        float UnderlayOffsetX { get; }

        /// <summary>Underlay Y 오프셋 (-1~1, 0이면 Outline, 0이 아니면 Drop Shadow)</summary>
        float UnderlayOffsetY { get; }

        /// <summary>Underlay 부드러움 (0~1, 경계 블러)</summary>
        float UnderlaySoftness { get; }

        // ─────────────────────────────────────────────
        // Face Settings (텍스트 본체)
        // ─────────────────────────────────────────────

        /// <summary>Face 두께 (-1~1, 텍스트 본체 굵기 조절)</summary>
        float FaceDilate { get; }

        // ─────────────────────────────────────────────
        // Shadow Settings (CPU 기반, 정점 복제)
        // ─────────────────────────────────────────────

        /// <summary>Shadow 활성화 여부 (IMeshModifier로 정점 복제)</summary>
        bool EnableShadow { get; }

        /// <summary>Shadow 오프셋 (픽셀, fontSize 기준 스케일)</summary>
        Vector2 ShadowOffset { get; }

        /// <summary>Shadow 알파값 (0~1, RGB는 Underlay 색상을 따름)</summary>
        float ShadowAlpha { get; }

        // ─────────────────────────────────────────────
        // Material 공유
        // ─────────────────────────────────────────────

        /// <summary>
        /// Material 공유를 위한 해시 계산
        /// </summary>
        /// <returns>FNV-1a 기반 해시 값 (충돌 최소화)</returns>
        /// <remarks>
        /// Shadow 설정은 Material에 영향 없음 (정점 메시만 수정)
        /// - Material 해시: Underlay + Face 설정만 포함
        /// - Shadow는 IMeshModifier로 별도 처리
        /// </remarks>
        int GetMaterialHash();
    }
}
