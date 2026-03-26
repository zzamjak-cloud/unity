// Robert Penner 이징 함수 구현
// GC 할당 없는 순수 수학 연산으로 모바일 최적화

using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// TMPEaseType에 대응하는 이징 함수 유틸리티.
    /// 모든 함수는 t ∈ [0, 1] 입력에 대해 [0, 1] 범위(Back/Elastic은 오버슈트 가능)를 반환합니다.
    /// GC 할당 제로, 순수 수학 연산.
    /// </summary>
    public static class TMPEasing
    {
        // ── 상수 ──────────────────────────────────────────────
        private const float PI = Mathf.PI;
        private const float HALF_PI = PI * 0.5f;
        private const float TWO_PI = PI * 2f;

        // Back 오버슈트 계수 (Robert Penner 표준)
        private const float BACK_S = 1.70158f;
        private const float BACK_S_INOUT = BACK_S * 1.525f;

        // Elastic 파라미터
        private const float ELASTIC_AMPLITUDE = 1f;
        private const float ELASTIC_PERIOD = 0.3f;
        private const float ELASTIC_PERIOD_INOUT = 0.45f;
        // s = period / (2π) * asin(1 / amplitude) = period / 4 (amplitude == 1일 때)
        private const float ELASTIC_S = ELASTIC_PERIOD * 0.25f;
        private const float ELASTIC_S_INOUT = ELASTIC_PERIOD_INOUT * 0.25f;

        // Bounce 계수
        private const float BOUNCE_N1 = 7.5625f;
        private const float BOUNCE_D1 = 2.75f;

        // ── 메인 디스패치 ─────────────────────────────────────

        /// <summary>
        /// 이징 타입에 따라 t 값을 평가합니다.
        /// </summary>
        /// <param name="ease">이징 타입</param>
        /// <param name="t">진행도 (0~1)</param>
        /// <returns>이징이 적용된 값</returns>
        public static float Evaluate(TMPEaseType ease, float t)
        {
            switch (ease)
            {
                case TMPEaseType.Linear:      return Linear(t);

                case TMPEaseType.InSine:      return InSine(t);
                case TMPEaseType.OutSine:     return OutSine(t);
                case TMPEaseType.InOutSine:   return InOutSine(t);

                case TMPEaseType.InQuad:      return InQuad(t);
                case TMPEaseType.OutQuad:     return OutQuad(t);
                case TMPEaseType.InOutQuad:   return InOutQuad(t);

                case TMPEaseType.InCubic:     return InCubic(t);
                case TMPEaseType.OutCubic:    return OutCubic(t);
                case TMPEaseType.InOutCubic:  return InOutCubic(t);

                case TMPEaseType.InQuart:     return InQuart(t);
                case TMPEaseType.OutQuart:    return OutQuart(t);
                case TMPEaseType.InOutQuart:  return InOutQuart(t);

                case TMPEaseType.InQuint:     return InQuint(t);
                case TMPEaseType.OutQuint:    return OutQuint(t);
                case TMPEaseType.InOutQuint:  return InOutQuint(t);

                case TMPEaseType.InExpo:      return InExpo(t);
                case TMPEaseType.OutExpo:     return OutExpo(t);
                case TMPEaseType.InOutExpo:   return InOutExpo(t);

                case TMPEaseType.InCirc:      return InCirc(t);
                case TMPEaseType.OutCirc:     return OutCirc(t);
                case TMPEaseType.InOutCirc:   return InOutCirc(t);

                case TMPEaseType.InElastic:   return InElastic(t);
                case TMPEaseType.OutElastic:  return OutElastic(t);
                case TMPEaseType.InOutElastic: return InOutElastic(t);

                case TMPEaseType.InBack:      return InBack(t);
                case TMPEaseType.OutBack:     return OutBack(t);
                case TMPEaseType.InOutBack:   return InOutBack(t);

                case TMPEaseType.InBounce:    return InBounce(t);
                case TMPEaseType.OutBounce:   return OutBounce(t);
                case TMPEaseType.InOutBounce: return InOutBounce(t);

                default:                      return Linear(t);
            }
        }

        // ── Linear ────────────────────────────────────────────

        public static float Linear(float t)
        {
            return t;
        }

        // ── Sine ──────────────────────────────────────────────

        public static float InSine(float t)
        {
            return 1f - Mathf.Cos(t * HALF_PI);
        }

        public static float OutSine(float t)
        {
            return Mathf.Sin(t * HALF_PI);
        }

        public static float InOutSine(float t)
        {
            return -0.5f * (Mathf.Cos(PI * t) - 1f);
        }

        // ── Quad (t²) ────────────────────────────────────────

        public static float InQuad(float t)
        {
            return t * t;
        }

        public static float OutQuad(float t)
        {
            return t * (2f - t);
        }

        public static float InOutQuad(float t)
        {
            if (t < 0.5f)
                return 2f * t * t;
            return -1f + (4f - 2f * t) * t;
        }

        // ── Cubic (t³) ───────────────────────────────────────

        public static float InCubic(float t)
        {
            return t * t * t;
        }

        public static float OutCubic(float t)
        {
            float u = t - 1f;
            return u * u * u + 1f;
        }

        public static float InOutCubic(float t)
        {
            if (t < 0.5f)
                return 4f * t * t * t;
            float u = 2f * t - 2f;
            return 0.5f * u * u * u + 1f;
        }

        // ── Quart (t⁴) ───────────────────────────────────────

        public static float InQuart(float t)
        {
            return t * t * t * t;
        }

        public static float OutQuart(float t)
        {
            float u = t - 1f;
            return 1f - u * u * u * u;
        }

        public static float InOutQuart(float t)
        {
            if (t < 0.5f)
                return 8f * t * t * t * t;
            float u = t - 1f;
            return 1f - 8f * u * u * u * u;
        }

        // ── Quint (t⁵) ───────────────────────────────────────

        public static float InQuint(float t)
        {
            return t * t * t * t * t;
        }

        public static float OutQuint(float t)
        {
            float u = t - 1f;
            return u * u * u * u * u + 1f;
        }

        public static float InOutQuint(float t)
        {
            if (t < 0.5f)
                return 16f * t * t * t * t * t;
            float u = 2f * t - 2f;
            return 0.5f * u * u * u * u * u + 1f;
        }

        // ── Expo (2^x) ───────────────────────────────────────

        public static float InExpo(float t)
        {
            // t == 0이면 0 반환 (2^(-10) ≈ 0.001이므로 보정)
            return t <= 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));
        }

        public static float OutExpo(float t)
        {
            return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        }

        public static float InOutExpo(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (t < 0.5f)
                return 0.5f * Mathf.Pow(2f, 10f * (2f * t - 1f));
            return 0.5f * (2f - Mathf.Pow(2f, -10f * (2f * t - 1f)));
        }

        // ── Circ (√) ─────────────────────────────────────────

        public static float InCirc(float t)
        {
            return 1f - Mathf.Sqrt(1f - t * t);
        }

        public static float OutCirc(float t)
        {
            float u = t - 1f;
            return Mathf.Sqrt(1f - u * u);
        }

        public static float InOutCirc(float t)
        {
            if (t < 0.5f)
            {
                float u = 2f * t;
                return -0.5f * (Mathf.Sqrt(1f - u * u) - 1f);
            }
            else
            {
                float u = 2f * t - 2f;
                return 0.5f * (Mathf.Sqrt(1f - u * u) + 1f);
            }
        }

        // ── Elastic (sin + 지수 감쇠) ────────────────────────

        public static float InElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return -(Mathf.Pow(2f, 10f * (t - 1f))
                     * Mathf.Sin((t - 1f - ELASTIC_S) * TWO_PI / ELASTIC_PERIOD));
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t)
                   * Mathf.Sin((t - ELASTIC_S) * TWO_PI / ELASTIC_PERIOD) + 1f;
        }

        public static float InOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            float s = t * 2f;
            if (s < 1f)
            {
                return -0.5f * (Mathf.Pow(2f, 10f * (s - 1f))
                                * Mathf.Sin((s - 1f - ELASTIC_S_INOUT) * TWO_PI / ELASTIC_PERIOD_INOUT));
            }
            return Mathf.Pow(2f, -10f * (s - 1f))
                   * Mathf.Sin((s - 1f - ELASTIC_S_INOUT) * TWO_PI / ELASTIC_PERIOD_INOUT)
                   * 0.5f + 1f;
        }

        // ── Back (오버슈트) ───────────────────────────────────

        public static float InBack(float t)
        {
            return t * t * ((BACK_S + 1f) * t - BACK_S);
        }

        public static float OutBack(float t)
        {
            float u = t - 1f;
            return u * u * ((BACK_S + 1f) * u + BACK_S) + 1f;
        }

        public static float InOutBack(float t)
        {
            float s = t * 2f;
            if (s < 1f)
            {
                return 0.5f * (s * s * ((BACK_S_INOUT + 1f) * s - BACK_S_INOUT));
            }
            float u = s - 2f;
            return 0.5f * (u * u * ((BACK_S_INOUT + 1f) * u + BACK_S_INOUT) + 2f);
        }

        // ── Bounce (다단계 바운스) ────────────────────────────

        public static float InBounce(float t)
        {
            return 1f - OutBounce(1f - t);
        }

        public static float OutBounce(float t)
        {
            if (t < 1f / BOUNCE_D1)
            {
                return BOUNCE_N1 * t * t;
            }
            if (t < 2f / BOUNCE_D1)
            {
                float u = t - 1.5f / BOUNCE_D1;
                return BOUNCE_N1 * u * u + 0.75f;
            }
            if (t < 2.5f / BOUNCE_D1)
            {
                float u = t - 2.25f / BOUNCE_D1;
                return BOUNCE_N1 * u * u + 0.9375f;
            }
            {
                float u = t - 2.625f / BOUNCE_D1;
                return BOUNCE_N1 * u * u + 0.984375f;
            }
        }

        public static float InOutBounce(float t)
        {
            if (t < 0.5f)
                return (1f - OutBounce(1f - 2f * t)) * 0.5f;
            return (1f + OutBounce(2f * t - 1f)) * 0.5f;
        }
    }
}
