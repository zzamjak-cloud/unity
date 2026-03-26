// DOTween의 DG.Tweening.Ease를 대체하는 커스텀 이징 타입 열거형
// 정수 값은 DOTween과 동일하게 유지하여 기존 직렬화된 .asset 파일 호환성 보장

namespace CAT.UI
{
    /// <summary>
    /// DOTween Ease 대체 열거형. 정수 값이 DOTween과 동일하므로
    /// 기존 SerializedObject(_appearEase: 27 = OutBack 등)가 마이그레이션 없이 동작합니다.
    /// </summary>
    public enum TMPEaseType
    {
        Linear      = 1,

        InSine      = 2,
        OutSine     = 3,
        InOutSine   = 4,

        InQuad      = 5,
        OutQuad     = 6,
        InOutQuad   = 7,

        InCubic     = 8,
        OutCubic    = 9,
        InOutCubic  = 10,

        InQuart     = 11,
        OutQuart    = 12,
        InOutQuart  = 13,

        InQuint     = 14,
        OutQuint    = 15,
        InOutQuint  = 16,

        InExpo      = 17,
        OutExpo     = 18,
        InOutExpo   = 19,

        InCirc      = 20,
        OutCirc     = 21,
        InOutCirc   = 22,

        InElastic   = 23,
        OutElastic  = 24,
        InOutElastic = 25,

        InBack      = 26,
        OutBack     = 27,
        InOutBack   = 28,

        InBounce    = 29,
        OutBounce   = 30,
        InOutBounce = 31,
    }
}
