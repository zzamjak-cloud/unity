using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMPOutlineEffect DOTween 확장 메서드
    /// - 효과 파라미터 애니메이션 지원
    /// - 체이닝 가능한 플루언트 API
    /// </summary>
    /// <remarks>
    /// 사용 예시:
    /// <code>
    /// effect.DOUnderlayDilate(0.3f, 1f).SetEase(Ease.OutBack);
    /// effect.DOUnderlayColor(Color.red, 0.5f);
    /// effect.DOShadowOffset(new Vector2(5, -5), 1f).SetLoops(-1, LoopType.Yoyo);
    /// </code>
    /// </remarks>
    public static class TMPOutlineEffectExtensions
    {
        // ─────────────────────────────────────────────
        // Underlay 애니메이션
        // ─────────────────────────────────────────────

        /// <summary>
        /// Underlay 색상 애니메이션
        /// </summary>
        /// <param name="effect">대상 효과</param>
        /// <param name="endValue">목표 색상</param>
        /// <param name="duration">애니메이션 시간 (초)</param>
        /// <returns>DOTween Tweener (체이닝 가능)</returns>
        public static TweenerCore<Color, Color, ColorOptions> DOUnderlayColor(
            this TMPOutlineEffect effect,
            Color endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.UnderlayColor,
                x => effect.UnderlayColor = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        /// <summary>
        /// Underlay 두께 (Outline 너비) 애니메이션
        /// </summary>
        /// <param name="effect">대상 효과</param>
        /// <param name="endValue">목표 두께 (0~1)</param>
        /// <param name="duration">애니메이션 시간 (초)</param>
        /// <returns>DOTween Tweener (체이닝 가능)</returns>
        public static TweenerCore<float, float, FloatOptions> DOUnderlayDilate(
            this TMPOutlineEffect effect,
            float endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.UnderlayDilate,
                x => effect.UnderlayDilate = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        /// <summary>
        /// Underlay X 오프셋 애니메이션
        /// </summary>
        public static TweenerCore<float, float, FloatOptions> DOUnderlayOffsetX(
            this TMPOutlineEffect effect,
            float endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.UnderlayOffsetX,
                x => effect.UnderlayOffsetX = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        /// <summary>
        /// Underlay Y 오프셋 애니메이션
        /// </summary>
        public static TweenerCore<float, float, FloatOptions> DOUnderlayOffsetY(
            this TMPOutlineEffect effect,
            float endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.UnderlayOffsetY,
                x => effect.UnderlayOffsetY = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        /// <summary>
        /// Underlay 부드러움 애니메이션
        /// </summary>
        public static TweenerCore<float, float, FloatOptions> DOUnderlaySoftness(
            this TMPOutlineEffect effect,
            float endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.UnderlaySoftness,
                x => effect.UnderlaySoftness = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        // ─────────────────────────────────────────────
        // Face 애니메이션
        // ─────────────────────────────────────────────

        /// <summary>
        /// Face 두께 애니메이션
        /// </summary>
        public static TweenerCore<float, float, FloatOptions> DOFaceDilate(
            this TMPOutlineEffect effect,
            float endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.FaceDilate,
                x => effect.FaceDilate = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        // ─────────────────────────────────────────────
        // Shadow 애니메이션
        // ─────────────────────────────────────────────

        /// <summary>
        /// Shadow 색상 애니메이션
        /// </summary>
        public static TweenerCore<Color, Color, ColorOptions> DOShadowColor(
            this TMPOutlineEffect effect,
            Color endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.ShadowColor,
                x => effect.ShadowColor = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        /// <summary>
        /// Shadow 오프셋 애니메이션
        /// </summary>
        public static TweenerCore<Vector2, Vector2, VectorOptions> DOShadowOffset(
            this TMPOutlineEffect effect,
            Vector2 endValue,
            float duration)
        {
            return DOTween.To(
                () => effect.ShadowOffset,
                x => effect.ShadowOffset = x,
                endValue,
                duration
            ).SetTarget(effect);
        }

        // ─────────────────────────────────────────────
        // 복합 애니메이션 (Preset 기반)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Preset으로 모든 파라미터 애니메이션
        /// </summary>
        /// <param name="effect">대상 효과</param>
        /// <param name="preset">목표 프리셋</param>
        /// <param name="duration">애니메이션 시간 (초)</param>
        /// <returns>DOTween Sequence (모든 Tween 포함)</returns>
        /// <remarks>
        /// 모든 파라미터가 동시에 애니메이션됩니다:
        /// - Underlay (Color, Dilate, Offset, Softness)
        /// - Face Dilate
        /// - Shadow (Color, Offset)
        /// </remarks>
        public static Sequence DOPreset(
            this TMPOutlineEffect effect,
            TMPEffectPreset preset,
            float duration)
        {
            if (preset == null)
            {
                Debug.LogError("[TMPOutlineEffect] Preset is null!");
                return DOTween.Sequence();
            }

            var sequence = DOTween.Sequence();

            // Underlay 애니메이션
            sequence.Join(effect.DOUnderlayColor(preset.UnderlayColor, duration));
            sequence.Join(effect.DOUnderlayDilate(preset.UnderlayDilate, duration));
            sequence.Join(effect.DOUnderlayOffsetX(preset.UnderlayOffsetX, duration));
            sequence.Join(effect.DOUnderlayOffsetY(preset.UnderlayOffsetY, duration));
            sequence.Join(effect.DOUnderlaySoftness(preset.UnderlaySoftness, duration));

            // Face 애니메이션
            sequence.Join(effect.DOFaceDilate(preset.FaceDilate, duration));

            // Shadow 애니메이션 (활성화된 경우에만)
            if (preset.EnableShadow)
            {
                sequence.Join(effect.DOShadowColor(preset.ShadowColor, duration));
                sequence.Join(effect.DOShadowOffset(preset.ShadowOffset, duration));

                // Shadow 활성화는 즉시 (애니메이션 시작 시)
                sequence.InsertCallback(0f, () => effect.EnableShadow = true);
            }

            return sequence.SetTarget(effect);
        }
    }
}
