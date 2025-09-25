using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 팝업의 베이스 클래스입니다.
/// 등장/퇴장 연출, 동적 버튼 관리, 확장 가능한 구조를 제공합니다.
/// </summary>
public abstract class BasePopup : MonoBehaviour
{
    [Header("Popup Core")]
    [SerializeField] protected CanvasGroup canvasGroup;
    
    [Header("Animation Settings")]
    [SerializeField] protected GameObject popupContentObject; // 실제 팝업 콘텐츠 오브젝트
    [SerializeField] protected float showDuration = 0.3f;
    [SerializeField] protected float hideDuration = 0.2f;
    [SerializeField] protected AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] protected AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] protected PopupAnimationType animationType = PopupAnimationType.Scale;
    [SerializeField] protected bool useFadeAnimation = true;
    
    [Header("Scale Animation")]
    [SerializeField] protected Vector3 targetScale = Vector3.one; // 목표 스케일 배율
    
    // 원본 스케일 저장
    private Vector3 originalScale;
    
    [Header("Slide Animation")]
    [SerializeField] protected Vector2 startOffset = Vector2.zero; // 원본 위치에서의 상대 좌표
    
    // 슬라이드 애니메이션을 위한 도착 위치 저장
    private Vector2 targetPosition;

    [Header("Content")]
    [SerializeField] protected Transform buttonContainer; // 동적 버튼들을 배치할 컨테이너
    
    // 동적 버튼 관리
    protected List<InteractiveButton> dynamicButtons = new List<InteractiveButton>();
    protected Dictionary<string, Action> buttonActions = new Dictionary<string, Action>();
    
    // 팝업 상태
    public bool IsShowing { get; private set; }
    public bool IsAnimating { get; private set; }
    
    // 이벤트
    public event Action<BasePopup> OnPopupShow;
    public event Action<BasePopup> OnPopupHide;
    public event Action<BasePopup, string> OnButtonClicked;
    
    protected virtual void Awake()
    {
        InitializePopup();
    }
    
    protected virtual void Start()
    {
        // 하위 클래스에서 오버라이드하여 초기화 로직 구현
        OnStart();
    }
    
    /// <summary>
    /// 팝업 초기화
    /// </summary>
    protected virtual void InitializePopup()
    {
        gameObject.SetActive(false);
        
        // 기본 CanvasGroup 설정
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        
        // 팝업 콘텐츠 오브젝트가 설정되지 않았다면 기본값 사용
        if (popupContentObject == null)
        {
            popupContentObject = gameObject;
        }
        
        // targetPosition과 originalScale 초기화 (원본 값 저장)
        if (popupContentObject != null)
        {
            RectTransform contentRect = popupContentObject.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                targetPosition = contentRect.anchoredPosition;
                originalScale = contentRect.localScale;
            }
        }
        
        IsShowing = false;
        IsAnimating = false;
    }
    
    /// <summary>
    /// 하위 클래스에서 오버라이드할 Start 메서드
    /// </summary>
    protected virtual void OnStart()
    {
        // 하위 클래스에서 구현
    }
    
    /// <summary>
    /// 하위 클래스에서 오버라이드할 팝업 표시 메서드
    /// </summary>
    protected virtual void OnShowPopup(string message)
    {
        // 하위 클래스에서 메시지 처리 구현
    }
    
    /// <summary>
    /// 팝업을 표시합니다.
    /// </summary>
    public virtual void ShowPopup(string message = "")
    {
        if (IsShowing || IsAnimating) return;
        
        gameObject.SetActive(true);
        
        // 메시지 처리는 하위 클래스에서 구현
        OnShowPopup(message);
        
        // 애니메이션 시작 전에 초기 상태를 강제로 설정
        SetInitialAnimationState();
        
        StartCoroutine(ShowAnimation());
    }
    
    /// <summary>
    /// 팝업을 숨깁니다.
    /// </summary>
    public virtual void HidePopup()
    {
        if (!IsShowing || IsAnimating) return;
        
        StartCoroutine(HideAnimation());
    }
    
    /// <summary>
    /// 팝업 표시 애니메이션
    /// </summary>
    protected virtual IEnumerator ShowAnimation()
    {
        IsAnimating = true;
        IsShowing = true;
        
        // 초기 상태 설정
        SetInitialAnimationState();
        
        float elapsedTime = 0f;
        
        while (elapsedTime < showDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / showDuration;
            float curveValue = showCurve.Evaluate(progress);
            
            ApplyAnimationState(curveValue, true);
            
            yield return null;
        }
        
        // 최종 상태 설정
        ApplyAnimationState(1f, true);
        
        IsAnimating = false;
        OnPopupShow?.Invoke(this);
    }
    
    /// <summary>
    /// 팝업 숨김 애니메이션
    /// </summary>
    protected virtual IEnumerator HideAnimation()
    {
        IsAnimating = true;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < hideDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / hideDuration;
            float curveValue = hideCurve.Evaluate(progress);
            
            ApplyAnimationState(curveValue, false);
            
            yield return null;
        }
        
        // 최종 상태 설정
        ApplyAnimationState(0f, false);
        
        gameObject.SetActive(false);
        
        IsShowing = false;
        IsAnimating = false;
        OnPopupHide?.Invoke(this);
    }
    
    /// <summary>
    /// 애니메이션 초기 상태 설정
    /// </summary>
    protected virtual void SetInitialAnimationState()
    {
        // Fade 애니메이션 초기 상태 설정
        if (useFadeAnimation && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        // 팝업 콘텐츠 오브젝트 초기 상태 설정
        if (popupContentObject != null)
        {
            RectTransform contentRect = popupContentObject.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                // 강제로 즉시 적용 (플레이 모드 진입 시 씹힘 방지)
                switch (animationType)
                {
                    case PopupAnimationType.Scale:
                        // 원본 스케일에 목표 스케일 배율 적용 (시작 위치)
                        contentRect.localScale = Vector3.Scale(originalScale, targetScale);
                        break;
                        
                    case PopupAnimationType.Slide:
                        // 시작 위치로 설정 (원본 위치 + startOffset)
                        contentRect.anchoredPosition = targetPosition + startOffset;
                        break;
                        
                    case PopupAnimationType.ScaleAndSlide:
                        // 원본 스케일에 목표 스케일 배율 적용 (시작 위치)
                        contentRect.localScale = Vector3.Scale(originalScale, targetScale);
                        // 시작 위치로 설정 (원본 위치 + startOffset)
                        contentRect.anchoredPosition = targetPosition + startOffset;
                        break;
                }
                
                // 강제로 레이아웃 업데이트
                Canvas.ForceUpdateCanvases();
            }
        }
    }
    
    /// <summary>
    /// 애니메이션 상태 적용
    /// </summary>
    protected virtual void ApplyAnimationState(float progress, bool isShowing)
    {
        // Fade 애니메이션 적용 (기본 Ease 사용)
        if (useFadeAnimation && canvasGroup != null)
        {
            canvasGroup.alpha = progress;
        }
        
        // 팝업 콘텐츠 오브젝트 애니메이션
        if (popupContentObject != null)
        {
            RectTransform contentRect = popupContentObject.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                switch (animationType)
                {
                    case PopupAnimationType.Scale:
                        float scaleProgress = isShowing ? 
                            showCurve.Evaluate(progress) : 
                            hideCurve.Evaluate(1f - progress);
                        // 원본 스케일과 목표 스케일 배율 사이에서 보간
                        Vector3 startScaleValue = Vector3.Scale(originalScale, targetScale);
                        Vector3 endScaleValue = originalScale;
                        contentRect.localScale = Vector3.Lerp(startScaleValue, endScaleValue, scaleProgress);
                        break;
                        
                    case PopupAnimationType.Slide:
                        float slideProgress = isShowing ? 
                            showCurve.Evaluate(progress) : 
                            hideCurve.Evaluate(1f - progress);
                        // 시작 위치(원본 위치 + startOffset)에서 도착 위치(targetPosition)까지 보간
                        Vector2 startPosition = targetPosition + startOffset;
                        contentRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, slideProgress);
                        break;
                        
                    case PopupAnimationType.ScaleAndSlide:
                        float combinedProgress = isShowing ? 
                            showCurve.Evaluate(progress) : 
                            hideCurve.Evaluate(1f - progress);
                        
                        // 스케일 애니메이션 (원본 스케일과 목표 스케일 배율 사이에서 보간)
                        Vector3 combinedStartScale = Vector3.Scale(originalScale, targetScale);
                        Vector3 combinedEndScale = originalScale;
                        contentRect.localScale = Vector3.Lerp(combinedStartScale, combinedEndScale, combinedProgress);
                        
                        // 슬라이드 애니메이션 (시작 위치에서 도착 위치까지)
                        Vector2 startPos = targetPosition + startOffset;
                        contentRect.anchoredPosition = Vector2.Lerp(startPos, targetPosition, combinedProgress);
                        break;
                }
            }
        }
    }
    
    /// <summary>
    /// 동적 버튼을 추가합니다.
    /// </summary>
    public virtual InteractiveButton AddButton(string buttonId, string buttonText, Action onClickAction)
    {
        if (buttonContainer == null)
        {
            Debug.LogError("ButtonContainer가 설정되지 않았습니다!");
            return null;
        }
        
        // 기존 버튼이 있다면 제거
        RemoveButton(buttonId);
        
        // 새 버튼 생성 (프리팹에서 가져오거나 동적 생성)
        InteractiveButton newButton = CreateButtonPrefab();
        if (newButton == null)
        {
            Debug.LogError("버튼 프리팹을 생성할 수 없습니다!");
            return null;
        }
        
        // 버튼 설정
        newButton.transform.SetParent(buttonContainer, false);
        newButton.name = $"Button_{buttonId}";
        
        // 버튼 텍스트 설정 (버튼에 TextMeshProUGUI 컴포넌트가 있다고 가정)
        TextMeshProUGUI buttonTextComponent = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonTextComponent != null)
        {
            buttonTextComponent.text = buttonText;
        }
        
        // 이벤트 연결
        newButton.OnButtonClicked.AddListener(() => OnDynamicButtonClicked(buttonId));
        
        // 리스트에 추가
        dynamicButtons.Add(newButton);
        buttonActions[buttonId] = onClickAction;
        
        return newButton;
    }
    
    /// <summary>
    /// Close 버튼을 추가합니다.
    /// </summary>
    public virtual InteractiveButton AddCloseButton(string buttonText = "닫기")
    {
        return AddButton("close", buttonText, OnCloseButtonClicked);
    }
    
    /// <summary>
    /// 동적 버튼을 제거합니다.
    /// </summary>
    public virtual void RemoveButton(string buttonId)
    {
        for (int i = dynamicButtons.Count - 1; i >= 0; i--)
        {
            if (dynamicButtons[i].name == $"Button_{buttonId}")
            {
                if (dynamicButtons[i] != null)
                {
                    dynamicButtons[i].OnButtonClicked.RemoveAllListeners();
                    DestroyImmediate(dynamicButtons[i].gameObject);
                }
                dynamicButtons.RemoveAt(i);
                break;
            }
        }
        
        buttonActions.Remove(buttonId);
    }
    
    /// <summary>
    /// 모든 동적 버튼을 제거합니다.
    /// </summary>
    public virtual void ClearAllButtons()
    {
        foreach (var button in dynamicButtons)
        {
            if (button != null)
            {
                button.OnButtonClicked.RemoveAllListeners();
                DestroyImmediate(button.gameObject);
            }
        }
        
        dynamicButtons.Clear();
        buttonActions.Clear();
    }
    
    /// <summary>
    /// 동적 버튼 클릭 처리
    /// </summary>
    protected virtual void OnDynamicButtonClicked(string buttonId)
    {
        OnButtonClicked?.Invoke(this, buttonId);
        
        if (buttonActions.ContainsKey(buttonId))
        {
            buttonActions[buttonId]?.Invoke();
        }
        
        // 하위 클래스에서 추가 처리
        OnButtonClickedInternal(buttonId);
    }
    
    /// <summary>
    /// 하위 클래스에서 오버라이드할 버튼 클릭 처리 메서드
    /// </summary>
    protected virtual void OnButtonClickedInternal(string buttonId)
    {
        // 하위 클래스에서 구현
    }
    
    /// <summary>
    /// 버튼 프리팹을 생성합니다. 하위 클래스에서 오버라이드 가능
    /// </summary>
    protected virtual InteractiveButton CreateButtonPrefab()
    {
        // 기본 버튼 생성 로직
        // 실제 구현에서는 프리팹을 로드하거나 기본 버튼을 생성
        GameObject buttonObj = new GameObject("DynamicButton");
        buttonObj.AddComponent<RectTransform>();
        buttonObj.AddComponent<Image>();
        buttonObj.AddComponent<Button>();
        
        // 텍스트 추가
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        textObj.AddComponent<RectTransform>();
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Button";
        text.fontSize = 14;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Center;
        
        return buttonObj.AddComponent<InteractiveButton>();
    }
    
    /// <summary>
    /// 버튼 상태를 설정합니다.
    /// </summary>
    public virtual void SetButtonState(string buttonId, bool enabled)
    {
        foreach (var button in dynamicButtons)
        {
            if (button.name == $"Button_{buttonId}")
            {
                button.SetClickable(enabled);
                button.SetState(enabled ? ButtonState.Normal : ButtonState.Disabled);
                break;
            }
        }
    }
    
    /// <summary>
    /// 모든 버튼 상태를 설정합니다.
    /// </summary>
    public virtual void SetAllButtonsState(bool enabled)
    {
        foreach (var button in dynamicButtons)
        {
            button.SetClickable(enabled);
            button.SetState(enabled ? ButtonState.Normal : ButtonState.Disabled);
        }
    }
    
    
    /// <summary>
    /// 팝업을 닫습니다. (HidePopup과 동일)
    /// </summary>
    public virtual void ClosePopup()
    {
        HidePopup();
    }
    
    /// <summary>
    /// Close 버튼 클릭 시 호출되는 메서드
    /// </summary>
    protected virtual void OnCloseButtonClicked()
    {
        Debug.Log("팝업 닫기 버튼 클릭");
        ClosePopup();
    }
    
    /// <summary>
    /// 하위 클래스에서 오버라이드할 정리 메서드
    /// </summary>
    protected virtual void OnDestroy()
    {
        ClearAllButtons();
        
        // 이벤트 정리
        OnPopupShow = null;
        OnPopupHide = null;
        OnButtonClicked = null;
    }
}

/// <summary>
/// 팝업 애니메이션 타입 (Fade는 별도 옵션으로 분리됨)
/// </summary>
public enum PopupAnimationType
{
    Scale,          // 스케일 애니메이션
    Slide,          // 슬라이드 애니메이션 (StartOffset 기반)
    ScaleAndSlide   // 스케일 + 슬라이드 조합
}
