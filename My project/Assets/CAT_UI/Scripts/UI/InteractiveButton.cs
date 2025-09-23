using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 모든 UI 버튼에 적용하여 클릭 및 릴리즈 시 스케일 애니메이션을 처리하는 컴포넌트입니다.
/// 스프링 효과를 적용하여 부드러운 반응을 제공합니다.
/// </summary>
public class InteractiveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Settings")]
    public float targetScale = 0.9f;  // 버튼 클릭 시 최종적으로 도달할 스케일

    [Header("Press Animation")]
    public float pressDuration = 0.08f;  // 클릭 시 애니메이션 시간
    public AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 클릭 시 커브

    [Header("Release Animation")]
    public float releaseDuration = 0.5f;  // 릴리즈 시 애니메이션 시간
    public AnimationCurve releaseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.15f, 1.08f, 0f, 0f),
        new Keyframe(0.3f, 0.96f, 0f, 0f),
        new Keyframe(0.5f, 1.03f, 0f, 0f),
        new Keyframe(0.7f, 0.99f, 0f, 0f),
        new Keyframe(0.85f, 1.01f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f)
    );  // 릴리즈 시 커브

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    // 애니메이션 상태 관리
    private bool isPressed = false;

    private void Awake()
    {
        // RectTransform을 가져오고 초기 스케일을 저장합니다.
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("RectTransform component not found on this GameObject. Please add a RectTransform.");
            return;
        }

        originalScale = rectTransform.localScale;
    }

    /// <summary>
    /// 포인터(마우스, 터치)가 버튼을 눌렀을 때 호출됩니다.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 기존 애니메이션이 있다면 중단합니다.
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        isPressed = true;
        // 클릭 시 커브를 사용한 스케일 다운 애니메이션을 시작합니다.
        scaleCoroutine = StartCoroutine(AnimateWithCurve(originalScale * targetScale, pressDuration, pressCurve));
    }

    /// <summary>
    /// 포인터(마우스, 터치)가 버튼에서 떨어졌을 때 호출됩니다.
    /// </summary>
    /// <param name="eventData">Pointer event data.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 기존 애니메이션이 있다면 중단합니다.
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        isPressed = false;
        // 릴리즈 시 커브를 사용한 원래 스케일로 돌아가는 애니메이션을 시작합니다.
        scaleCoroutine = StartCoroutine(AnimateWithCurve(originalScale, releaseDuration, releaseCurve));
    }

    /// <summary>
    /// 커브를 사용하여 스케일 애니메이션을 수행하는 코루틴입니다.
    /// </summary>
    /// <param name="targetScale">목표 스케일</param>
    /// <param name="duration">애니메이션 시간</param>
    /// <param name="curve">사용할 애니메이션 커브</param>
    private IEnumerator AnimateWithCurve(Vector3 targetScale, float duration, AnimationCurve curve)
    {
        // duration이 너무 작으면 즉시 설정
        if (duration <= 0.001f)
        {
            rectTransform.localScale = targetScale;
            yield break;
        }
        
        Vector3 startScale = rectTransform.localScale;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            
            // 커브에서 값을 가져옵니다
            float curveValue = curve.Evaluate(t);
            
            // NaN 체크
            if (float.IsNaN(curveValue))
            {
                Debug.LogWarning("NaN detected in animation curve, using target scale");
                rectTransform.localScale = targetScale;
                yield break;
            }
            
            // 클릭 시와 릴리즈 시를 구분하여 처리
            Vector3 newScale;
            if (isPressed)
            {
                // 클릭 시: 시작 스케일에서 목표 스케일로 보간
                newScale = Vector3.Lerp(startScale, targetScale, curveValue);
            }
            else
            {
                // 릴리즈 시: 커브 값을 직접 스케일로 사용 (1.0 기준)
                newScale = Vector3.one * curveValue;
            }
            
            // 최종 NaN 체크
            if (float.IsNaN(newScale.x) || float.IsNaN(newScale.y) || float.IsNaN(newScale.z))
            {
                Debug.LogWarning("NaN detected in AnimateWithCurve, using target scale");
                rectTransform.localScale = targetScale;
                yield break;
            }
            
            rectTransform.localScale = newScale;
            yield return null;
        }
        
        // 최종적으로 정확한 목표 스케일로 설정
        rectTransform.localScale = targetScale;
    }
}
