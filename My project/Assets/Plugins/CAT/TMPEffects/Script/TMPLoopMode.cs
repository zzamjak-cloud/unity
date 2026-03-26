// DOTween의 DG.Tweening.LoopType을 대체하는 커스텀 루프 모드 열거형
// 정수 값은 DOTween과 동일하게 유지하여 기존 직렬화된 .asset 파일 호환성 보장

namespace CAT.UI
{
    /// <summary>
    /// DOTween LoopType 대체 열거형.
    /// Restart(0)와 Yoyo(1)의 정수 값이 DOTween과 동일합니다.
    /// </summary>
    public enum TMPLoopMode
    {
        /// <summary>루프 시작 시 처음부터 다시 재생</summary>
        Restart = 0,

        /// <summary>루프 시작 시 역방향으로 재생 (핑퐁)</summary>
        Yoyo = 1,
    }
}
