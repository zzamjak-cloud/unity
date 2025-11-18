using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// UI 전용 슬라이드/페이드 전환 컨트롤러.
/// - RectTransform의 anchoredPosition을 Start → End로 보간하여 슬라이딩 연출
/// - 선택적으로 CanvasGroup의 alpha를 커브 기반으로 보간하여 페이드 연출
/// - 단일 커브를 사용하고, 퇴장은 역재생으로 처리 (등장/퇴장 시간은 분리 가능)
/// - 동일 커브로 위치/알파/스케일을 함께 제어 가능
/// - 공용 컴포넌트로 팝업 외 다양한 UI 진입/퇴장에 재사용 가능
/// </summary>
public class UISlideTransitionController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private CanvasGroup canvasGroupTarget;
    [SerializeField] private GameObject rootObject; // 연출 시작 시 활성화, 종료 시 비활성화할 루트
    [SerializeField] private bool toggleRootActive = true;
    [SerializeField] private CanvasGroup dimmerCanvasGroup; // 선택: Dimmer의 알파를 함께 제어
    [SerializeField] private bool controlDimmerAlpha = false;
    [SerializeField] private float dimmerMaxAlpha = 0.6f;

    [Header("Positions (Anchored)")]
    [SerializeField] private Vector2 startAnchoredPosition;
    [SerializeField] private Vector2 endAnchoredPosition;
    [SerializeField] private bool initializeStartFromCurrent; // 활성화 시 현재 위치를 Start로 사용
    [SerializeField] private bool initializeEndFromCurrent;   // 활성화 시 현재 위치를 End로 사용

    [Header("Scale")]
    [SerializeField] private bool controlScale = false;
    [SerializeField] private Vector3 startScale = Vector3.one;
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private bool initializeScaleFromCurrent = false; // 활성화 시 현재 스케일을 Start로 사용

    [Header("Timings")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float hideDuration = 0.25f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Curve")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool controlCanvasAlpha = true;

    [Header("Events")]
    public UnityEvent OnShowStarted;
    public UnityEvent OnShowCompleted;
    public UnityEvent OnHideStarted;
    public UnityEvent OnHideCompleted;

    public bool IsShowing { get; private set; }
    public bool IsAnimating { get; private set; }

    private Coroutine runningCoroutine;

    private void Reset()
    {
        if (targetRect == null) targetRect = GetComponent<RectTransform>();
        if (canvasGroupTarget == null) canvasGroupTarget = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (targetRect == null) targetRect = GetComponent<RectTransform>();
        if (canvasGroupTarget == null && controlCanvasAlpha)
        {
            canvasGroupTarget = GetComponent<CanvasGroup>();
            if (canvasGroupTarget == null)
            {
                canvasGroupTarget = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        // 활성화 시 초기 위치 세팅 옵션 처리
        if (targetRect != null)
        {
            if (initializeStartFromCurrent)
            {
                startAnchoredPosition = targetRect.anchoredPosition;
            }
            if (initializeEndFromCurrent)
            {
                endAnchoredPosition = targetRect.anchoredPosition;
            }
            if (initializeScaleFromCurrent)
            {
                startScale = targetRect.localScale;
            }
        }
    }

    /// <summary>
    /// 즉시 상태 설정 (애니메이션 없이).
    /// shown=true면 End 위치와 Alpha=1, false면 Start 위치와 Alpha=0으로 설정.
    /// </summary>
    public void SetImmediate(bool shown)
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        IsAnimating = false;
        IsShowing = shown;

        if (toggleRootActive && rootObject != null)
        {
            if (rootObject.activeSelf != shown)
            {
                rootObject.SetActive(shown);
            }
        }

        if (targetRect != null)
        {
            targetRect.anchoredPosition = shown ? endAnchoredPosition : startAnchoredPosition;
            if (controlScale)
            {
                targetRect.localScale = shown ? endScale : startScale;
            }
        }

        if (controlCanvasAlpha && canvasGroupTarget != null)
        {
            canvasGroupTarget.alpha = shown ? 1f : 0f;
            canvasGroupTarget.interactable = shown;
            canvasGroupTarget.blocksRaycasts = shown;
        }

        if (controlDimmerAlpha && dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.alpha = shown ? dimmerMaxAlpha : 0f;
            // Dimmer 활성/비활성 처리 및 터치 차단 토글
            if (dimmerCanvasGroup.gameObject.activeSelf != shown)
            {
                dimmerCanvasGroup.gameObject.SetActive(shown);
            }
            dimmerCanvasGroup.blocksRaycasts = shown && dimmerMaxAlpha > 0f;
            dimmerCanvasGroup.interactable = false; // Dimmer 자체 상호작용은 기본 비활성화
        }
    }

    /// <summary>
    /// 등장 애니메이션 재생 (Start → End).
    /// 이미 PlayIn 상태(IsShowing=true)이고 애니메이션이 완료된 상태라면 재실행하지 않음.
    /// </summary>
    public void PlayIn()
    {
        // 이미 PlayIn 상태이고 애니메이션이 완료된 상태라면 재실행하지 않음
        // (MenuToggleController를 통한 토글 동작은 IsShowing을 체크하므로 정상 동작)
        if (IsShowing && !IsAnimating)
        {
            return;
        }

        Play(show: true);
    }

    /// <summary>
    /// 퇴장 애니메이션 재생 (End → Start).
    /// 이미 비활성화되어 있고 PlayOut 상태라면 아무 동작도 하지 않음.
    /// </summary>
    public void PlayOut()
    {
        // 이미 비활성화되어 있고 PlayOut 상태라면 아무것도 하지 않음
        if (!IsShowing && toggleRootActive && rootObject != null && !rootObject.activeSelf)
        {
            // 이미 PlayOut 상태이므로 아무것도 하지 않음
            return;
        }

        // PlayOut을 실행하기 위해 필요한 경우 rootObject 활성화
        if (toggleRootActive && rootObject != null && !rootObject.activeSelf)
        {
            rootObject.SetActive(true);
        }

        Play(show: false);
    }

    /// <summary>
    /// 공용 재생 진입점.
    /// </summary>
    public void Play(bool show)
    {
        // 코루틴을 시작하려면 GameObject가 활성화되어 있어야 함
        // rootObject가 지정되어 있으면 rootObject의 활성화 상태를 확인, 없으면 현재 GameObject 확인
        GameObject checkObject = (toggleRootActive && rootObject != null) ? rootObject : gameObject;
        if (!checkObject.activeSelf)
        {
            // 비활성화 상태에서 show=false 요청이면 즉시 상태만 설정하고 종료
            if (!show)
            {
                SetImmediate(false);
                return;
            }
            // show=true인 경우 활성화 후 진행
            checkObject.SetActive(true);
        }

        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        // Show 시작 전에 루트를 활성화하여 컴포넌트가 정상 동작하도록 보장
        if (show && toggleRootActive && rootObject != null && !rootObject.activeSelf)
        {
            rootObject.SetActive(true);
        }
        // Dimmer는 Show 시작 전에 활성화하여 알파 전환이 보이도록
        if (show && controlDimmerAlpha && dimmerCanvasGroup != null && !dimmerCanvasGroup.gameObject.activeSelf)
        {
            dimmerCanvasGroup.gameObject.SetActive(true);
            dimmerCanvasGroup.blocksRaycasts = false; // 시작은 alpha=0, 터치 차단 안 함
            dimmerCanvasGroup.interactable = false;
        }
        runningCoroutine = StartCoroutine(Co_Play(show));
    }

    private IEnumerator Co_Play(bool show)
    {
        if (targetRect == null)
        {
            yield break;
        }

        IsAnimating = true;
        IsShowing = show;

        if (show) OnShowStarted?.Invoke(); else OnHideStarted?.Invoke();

        float duration = show ? Mathf.Max(0f, showDuration) : Mathf.Max(0f, hideDuration);
        AnimationCurve curve = transitionCurve;

        // 항상 start → end를 기준으로 보간하고, hide 시에는 커브를 역재생(1 - curve(t))
        Vector2 from = startAnchoredPosition;
        Vector2 to = endAnchoredPosition;

        // 시작 프레임 강제 적용 (레이아웃 안정화)
        targetRect.anchoredPosition = show ? from : to;
        if (controlScale)
        {
            targetRect.localScale = show ? startScale : endScale;
        }
        if (controlCanvasAlpha && canvasGroupTarget != null)
        {
            float initialAlpha = show ? 0f : 1f;
            canvasGroupTarget.alpha = initialAlpha;
            canvasGroupTarget.interactable = show;
            canvasGroupTarget.blocksRaycasts = show;
        }
        if (controlDimmerAlpha && dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.alpha = show ? 0f : dimmerMaxAlpha;
            dimmerCanvasGroup.blocksRaycasts = show ? false : dimmerMaxAlpha > 0f;
            dimmerCanvasGroup.interactable = false;
        }
        Canvas.ForceUpdateCanvases();

        if (duration <= 0f)
        {
            // 즉시 완료
            targetRect.anchoredPosition = show ? to : from;
            if (controlScale)
            {
                targetRect.localScale = show ? endScale : startScale;
            }
            if (controlCanvasAlpha && canvasGroupTarget != null)
            {
                canvasGroupTarget.alpha = show ? 1f : 0f;
                canvasGroupTarget.interactable = show;
                canvasGroupTarget.blocksRaycasts = show;
            }
            if (controlDimmerAlpha && dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = show ? dimmerMaxAlpha : 0f;
                dimmerCanvasGroup.blocksRaycasts = show && dimmerMaxAlpha > 0f;
                dimmerCanvasGroup.interactable = false;
            }
            // Hide 즉시 완료 시 루트 비활성화
            if (!show && toggleRootActive && rootObject != null && rootObject.activeSelf)
            {
                rootObject.SetActive(false);
            }
            // Hide 즉시 완료 시 Dimmer 비활성화
            if (!show && controlDimmerAlpha && dimmerCanvasGroup != null && dimmerCanvasGroup.gameObject.activeSelf)
            {
                dimmerCanvasGroup.gameObject.SetActive(false);
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float baseT = Mathf.Clamp01(curve.Evaluate(t));
                float posT = show ? baseT : 1f - baseT; // 역재생
                targetRect.anchoredPosition = Vector2.LerpUnclamped(from, to, posT);

                if (controlCanvasAlpha && canvasGroupTarget != null)
                {
                    float aBase = baseT;
                    float a = show ? aBase : 1f - aBase; // 알파도 동일 커브 사용
                    a = Mathf.Clamp01(a);
                    canvasGroupTarget.alpha = a;
                    // 알파 0 도달 시 터치 차단 방지
                    if (a <= 0f)
                    {
                        canvasGroupTarget.interactable = false;
                        canvasGroupTarget.blocksRaycasts = false;
                    }
                }

                if (controlDimmerAlpha && dimmerCanvasGroup != null)
                {
                    float aBase = baseT;
                    float a = show ? aBase : 1f - aBase;
                    dimmerCanvasGroup.alpha = Mathf.Clamp01(a) * dimmerMaxAlpha;
                    // alpha 0이면 터치 차단 해제, 0보다 크면 차단 활성화
                    bool raycast = dimmerCanvasGroup.alpha > 0f;
                    dimmerCanvasGroup.blocksRaycasts = raycast;
                    dimmerCanvasGroup.interactable = false;
                }

                if (controlScale)
                {
                    float sT = posT; // 위치와 동일한 진행도 사용
                    targetRect.localScale = Vector3.LerpUnclamped(startScale, endScale, sT);
                }

                yield return null;
            }

            // 최종 상태 보정
            targetRect.anchoredPosition = show ? to : from;
            if (controlScale)
            {
                targetRect.localScale = show ? endScale : startScale;
            }
            if (controlCanvasAlpha && canvasGroupTarget != null)
            {
                canvasGroupTarget.alpha = show ? 1f : 0f;
                canvasGroupTarget.interactable = show;
                canvasGroupTarget.blocksRaycasts = show;
            }
            if (controlDimmerAlpha && dimmerCanvasGroup != null)
            {
                dimmerCanvasGroup.alpha = show ? dimmerMaxAlpha : 0f;
                dimmerCanvasGroup.blocksRaycasts = show && dimmerMaxAlpha > 0f;
                dimmerCanvasGroup.interactable = false;
            }

            // Hide 완료 시 루트 비활성화
            if (!show && toggleRootActive && rootObject != null && rootObject.activeSelf)
            {
                rootObject.SetActive(false);
            }

            // Hide 완료 시 Dimmer 비활성화
            if (!show && controlDimmerAlpha && dimmerCanvasGroup != null && dimmerCanvasGroup.gameObject.activeSelf)
            {
                dimmerCanvasGroup.gameObject.SetActive(false);
            }
        }

        IsAnimating = false;
        if (show) OnShowCompleted?.Invoke(); else OnHideCompleted?.Invoke();
        runningCoroutine = null;
    }

    // 외부에서 런타임 중 Start/End를 설정할 수 있도록 공개 Setter 제공
    public void SetStartPosition(Vector2 anchoredPosition)
    {
        startAnchoredPosition = anchoredPosition;
    }

    public void SetEndPosition(Vector2 anchoredPosition)
    {
        endAnchoredPosition = anchoredPosition;
    }

    public void SetTargets(RectTransform rect, CanvasGroup group = null)
    {
        targetRect = rect;
        canvasGroupTarget = group;
    }

    public void SetStartScale(Vector3 scale)
    {
        startScale = scale;
    }

    public void SetEndScale(Vector3 scale)
    {
        endScale = scale;
    }
}


