using UnityEngine;

namespace CAT.Toon
{
    /// <summary>
    /// 아웃라인 값을 런타임에서 덮어쓰기 위한 전역 오버라이드입니다.
    /// 렌더러 피처 에셋을 더럽히지 않고 매 프레임 값을 바꿀 수 있으므로
    /// 피격 연출, 아이템 하이라이트, 컷신 톤 변경 등에 그대로 쓸 수 있습니다.
    ///
    /// <code>
    /// CATToonOutlineRuntime.Color = Color.red;   // 빨간 아웃라인
    /// CATToonOutlineRuntime.Color = null;        // 피처 설정값으로 복귀
    /// </code>
    /// </summary>
    public static class CATToonOutlineRuntime
    {
        /// <summary>덮어쓸 아웃라인 컬러. null 이면 렌더러 피처 설정을 사용합니다.</summary>
        public static Color? Color { get; set; }

        /// <summary>덮어쓸 라인 두께(픽셀). null 이면 렌더러 피처 설정을 사용합니다.</summary>
        public static float? Thickness { get; set; }

        /// <summary>덮어쓸 손그림 흔들림 강도. null 이면 렌더러 피처 설정을 사용합니다.</summary>
        public static float? SketchJitter { get; set; }

        /// <summary>아웃라인 전체 on/off. null 이면 렌더러 피처의 활성 상태를 따릅니다.</summary>
        public static bool? Enabled { get; set; }

        /// <summary>덮어쓸 합성 방식. null 이면 렌더러 피처 설정을 사용합니다.</summary>
        public static CATToonOutlineFeature.OutlineBlendMode? BlendMode { get; set; }

        /// <summary>모든 오버라이드를 해제하고 렌더러 피처 설정으로 되돌립니다.</summary>
        public static void Reset()
        {
            Color        = null;
            Thickness    = null;
            SketchJitter = null;
            Enabled      = null;
            BlendMode    = null;
        }

        // 도메인 리로드를 끈 상태에서도 플레이 시작 시 값이 남지 않도록 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Reset();

        internal static Color ResolveColor(Color fallback)   => Color ?? fallback;
        internal static float ResolveThickness(float fallback) => Thickness ?? fallback;
        internal static float ResolveSketchJitter(float fallback) => SketchJitter ?? fallback;
        internal static bool  ResolveEnabled(bool fallback)   => Enabled ?? fallback;

        internal static CATToonOutlineFeature.OutlineBlendMode ResolveBlendMode(
            CATToonOutlineFeature.OutlineBlendMode fallback) => BlendMode ?? fallback;
    }
}
