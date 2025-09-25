using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

#region Enums and Data Classes

// 버튼의 상태를 정의하는 열거형입니다.
public enum ButtonState
{
    Normal,     // 일반 상태
    Active,     // 활성 상태 (선택됨, 강조됨 등)
    Disabled    // 비활성 상태
}

// 상태별 컬러 정보를 저장하는 클래스입니다.
[System.Serializable]
public class StateColorInfo
{
    public ButtonState state;
    public Color color = Color.white;
}

// 상태별 GameObject 정보를 저장하는 클래스입니다.
[System.Serializable]
public class StateGameObjectInfo
{
    public ButtonState state;
    public GameObject gameObject;
}

// 이미지 컴포넌트의 상태별 컬러 정보를 저장하는 클래스입니다.
[System.Serializable]
public class ImageColorInfo
{
    public Image targetImage;
    public List<StateColorInfo> stateColors = new List<StateColorInfo>();
}

// 텍스트 컴포넌트의 상태별 컬러 정보를 저장하는 클래스입니다.
[System.Serializable]
public class TextColorInfo
{
    public TextMeshProUGUI targetText;
    public List<StateColorInfo> stateColors = new List<StateColorInfo>();
}

// 아이콘의 상태별 GameObject 정보를 저장하는 클래스입니다.
[System.Serializable]
public class IconGameObjectInfo
{
    public List<StateGameObjectInfo> stateGameObjects = new List<StateGameObjectInfo>();
}

#endregion

// 모든 UI 버튼에 적용하여 클릭 및 릴리즈 시 스케일 애니메이션을 처리하는 컴포넌트입니다.
public class InteractiveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Serialized Fields
    
    [SerializeField] private ButtonState currentState = ButtonState.Normal;
    [SerializeField] private bool isClickable = true;  // 클릭 가능 여부 (상태와 독립적)
    
    public float targetScale = 0.9f;  // 버튼 클릭 시 최종적으로 도달할 스케일

    public float pressDuration = 0.1f;  // 클릭 시 애니메이션 시간
    public AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 클릭 시 커브 (0: original 스케일, 1: target 스케일, 1 초과시 오버슈트)

    public float releaseDuration = 0.3f;  // 릴리즈 시 애니메이션 시간
    public AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // 릴리즈 시 커브 (0: target 스케일, 1: original 스케일, 1 초과시 오버슈트)
    
    public List<ImageColorInfo> imageColorInfos = new List<ImageColorInfo>();
    
    public List<TextColorInfo> textColorInfos = new List<TextColorInfo>();
    
    public List<IconGameObjectInfo> iconGameObjectInfos = new List<IconGameObjectInfo>();
    
    [Header("Events")]
    public UnityEvent OnButtonClicked;  // 버튼 클릭 시 호출되는 이벤트
    public UnityEvent OnButtonPressed;  // 버튼을 누를 때 호출되는 이벤트
    public UnityEvent OnButtonReleased; // 버튼을 놓을 때 호출되는 이벤트
    public UnityEvent OnButtonEnter;    // 버튼에 마우스가 들어올 때 호출되는 이벤트
    public UnityEvent OnButtonExit;     // 버튼에서 마우스가 나갈 때 호출되는 이벤트
    
    [Header("Button Identification")]
    [SerializeField] private string buttonId = "";  // 버튼 고유 ID
    
    #endregion
    
    #region Private Fields
    
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) return;

        originalScale = rectTransform.localScale;
        
        // 모든 상태를 자동으로 생성
        EnsureAllStatesExist();
        
        // 초기 상태에 맞는 컬러 적용
        ApplyStateColors();
        
        // 버튼 ID가 설정되어 있으면 이벤트 시스템에 등록
        if (!string.IsNullOrEmpty(buttonId))
        {
            RegisterToEventSystem();
        }
    }
    
    private void OnDestroy()
    {
        // 이벤트 시스템에서 등록 해제
        if (!string.IsNullOrEmpty(buttonId))
        {
            UnregisterFromEventSystem();
        }
    }
    
    #endregion
    
    #region Public State Management
    
    // 버튼의 현재 상태를 반환합니다.
    public ButtonState GetCurrentState()
    {
        return currentState;
    }
    
    // 버튼의 상태를 설정합니다.
    public void SetState(ButtonState newState)
    {
        if (currentState == newState) return;
        
        currentState = newState;
        ApplyStateColors();
    }
    
    // 에디터에서 상태를 직접 설정합니다. (런타임 로직 없이 시각적 변경만)
    public void SetStateForEditor(ButtonState newState)
    {
        currentState = newState;
        ApplyStateColors();
    }
    
    // 버튼의 클릭 가능 여부를 반환합니다.
    public bool IsClickable()
    {
        return isClickable;
    }
    
    // 버튼의 클릭 가능 여부를 설정합니다.
    public void SetClickable(bool clickable)
    {
        isClickable = clickable;
    }
    
    #endregion
    
    #region State Initialization
    
    // 모든 이미지, 텍스트, 아이콘에 대해 Normal, Active, Disabled 상태를 자동으로 생성합니다.
    private void EnsureAllStatesExist()
    {
        // 이미지 컬러 상태 자동 생성
        foreach (var imageInfo in imageColorInfos)
        {
            EnsureImageStatesExist(imageInfo);
        }
        
        // 텍스트 컬러 상태 자동 생성
        foreach (var textInfo in textColorInfos)
        {
            EnsureTextStatesExist(textInfo);
        }
        
        // 아이콘 GameObject 상태 자동 생성
        foreach (var iconInfo in iconGameObjectInfos)
        {
            EnsureIconStatesExist(iconInfo);
        }
    }
    
    // 이미지에 모든 상태가 존재하는지 확인하고 없으면 자동 생성합니다.
    private void EnsureImageStatesExist(ImageColorInfo imageInfo)
    {
        if (imageInfo.targetImage == null) return;
        
        // 현재 이미지의 기본 컬러를 가져옴
        Color defaultColor = imageInfo.targetImage.color;
        
        // Normal 상태 확인 및 생성
        if (!imageInfo.stateColors.Exists(sc => sc.state == ButtonState.Normal))
        {
            imageInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Normal, color = defaultColor });
        }
        
        // Active 상태 확인 및 생성 (기본적으로 약간 밝게)
        if (!imageInfo.stateColors.Exists(sc => sc.state == ButtonState.Active))
        {
            Color activeColor = new Color(
                Mathf.Min(1f, defaultColor.r * 1.2f),
                Mathf.Min(1f, defaultColor.g * 1.2f),
                Mathf.Min(1f, defaultColor.b * 1.2f),
                defaultColor.a
            );
            imageInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Active, color = activeColor });
        }
        
        // Disabled 상태 확인 및 생성 (기본적으로 어둡게)
        if (!imageInfo.stateColors.Exists(sc => sc.state == ButtonState.Disabled))
        {
            Color disabledColor = new Color(
                defaultColor.r * 0.5f,
                defaultColor.g * 0.5f,
                defaultColor.b * 0.5f,
                defaultColor.a * 0.7f
            );
            imageInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Disabled, color = disabledColor });
        }
    }
    
    // 텍스트에 모든 상태가 존재하는지 확인하고 없으면 자동 생성합니다.
    private void EnsureTextStatesExist(TextColorInfo textInfo)
    {
        if (textInfo.targetText == null) return;
        
        // 현재 텍스트의 기본 컬러를 가져옴
        Color defaultColor = textInfo.targetText.color;
        
        // Normal 상태 확인 및 생성
        if (!textInfo.stateColors.Exists(sc => sc.state == ButtonState.Normal))
        {
            textInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Normal, color = defaultColor });
        }
        
        // Active 상태 확인 및 생성 (기본적으로 약간 밝게)
        if (!textInfo.stateColors.Exists(sc => sc.state == ButtonState.Active))
        {
            Color activeColor = new Color(
                Mathf.Min(1f, defaultColor.r * 1.2f),
                Mathf.Min(1f, defaultColor.g * 1.2f),
                Mathf.Min(1f, defaultColor.b * 1.2f),
                defaultColor.a
            );
            textInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Active, color = activeColor });
        }
        
        // Disabled 상태 확인 및 생성 (기본적으로 어둡게)
        if (!textInfo.stateColors.Exists(sc => sc.state == ButtonState.Disabled))
        {
            Color disabledColor = new Color(
                defaultColor.r * 0.5f,
                defaultColor.g * 0.5f,
                defaultColor.b * 0.5f,
                defaultColor.a * 0.7f
            );
            textInfo.stateColors.Add(new StateColorInfo { state = ButtonState.Disabled, color = disabledColor });
        }
    }
    
    // 아이콘에 모든 상태가 존재하는지 확인하고 없으면 자동 생성합니다.
    private void EnsureIconStatesExist(IconGameObjectInfo iconInfo)
    {
        // Normal 상태 확인 및 생성
        if (!iconInfo.stateGameObjects.Exists(sg => sg.state == ButtonState.Normal))
        {
            iconInfo.stateGameObjects.Add(new StateGameObjectInfo { state = ButtonState.Normal, gameObject = null });
        }
        
        // Active 상태 확인 및 생성
        if (!iconInfo.stateGameObjects.Exists(sg => sg.state == ButtonState.Active))
        {
            iconInfo.stateGameObjects.Add(new StateGameObjectInfo { state = ButtonState.Active, gameObject = null });
        }
        
        // Disabled 상태 확인 및 생성
        if (!iconInfo.stateGameObjects.Exists(sg => sg.state == ButtonState.Disabled))
        {
            iconInfo.stateGameObjects.Add(new StateGameObjectInfo { state = ButtonState.Disabled, gameObject = null });
        }
    }
    
    #endregion
    
    #region State Application
    
    // 현재 상태에 맞는 컬러와 스프라이트를 모든 컴포넌트에 적용합니다.
    private void ApplyStateColors()
    {
        // 이미지 컬러 적용
        foreach (var imageInfo in imageColorInfos)
        {
            ApplyImageColors(imageInfo);
        }
        
        // 텍스트 컬러 적용
        foreach (var textInfo in textColorInfos)
        {
            ApplyTextColors(textInfo);
        }
        
        // 아이콘 GameObject 활성화 적용
        foreach (var iconInfo in iconGameObjectInfos)
        {
            ApplyIconGameObjects(iconInfo);
        }
    }
    
    // 특정 이미지에 현재 상태에 맞는 컬러를 적용합니다.
    private void ApplyImageColors(ImageColorInfo imageInfo)
    {
        if (imageInfo.targetImage == null) return;
        
        // 현재 상태에 맞는 컬러 찾기
        Color targetColor = imageInfo.targetImage.color; // 기본값은 현재 컬러
        foreach (var stateColor in imageInfo.stateColors)
        {
            if (stateColor.state == currentState)
            {
                targetColor = stateColor.color;
                break;
            }
        }
        
        imageInfo.targetImage.color = targetColor;
    }
    
    // 특정 텍스트에 현재 상태에 맞는 컬러를 적용합니다.
    private void ApplyTextColors(TextColorInfo textInfo)
    {
        if (textInfo.targetText == null) return;
        
        // 현재 상태에 맞는 컬러 찾기
        Color targetColor = textInfo.targetText.color; // 기본값은 현재 컬러
        foreach (var stateColor in textInfo.stateColors)
        {
            if (stateColor.state == currentState)
            {
                targetColor = stateColor.color;
                break;
            }
        }
        
        textInfo.targetText.color = targetColor;
    }
    
    // 특정 아이콘에 현재 상태에 맞는 GameObject 활성화를 적용합니다.
    private void ApplyIconGameObjects(IconGameObjectInfo iconInfo)
    {
        // 모든 상태의 GameObject를 비활성화
        foreach (var stateGameObject in iconInfo.stateGameObjects)
        {
            if (stateGameObject.gameObject != null)
            {
                stateGameObject.gameObject.SetActive(false);
            }
        }
        
        // 현재 상태에 맞는 GameObject 활성화
        foreach (var stateGameObject in iconInfo.stateGameObjects)
        {
            if (stateGameObject.state == currentState && stateGameObject.gameObject != null)
            {
                stateGameObject.gameObject.SetActive(true);
                break;
            }
        }
    }
    
    #endregion
    
    #region Pointer Event Handlers
    
    // 포인터(마우스, 터치)가 버튼을 눌렀을 때 호출됩니다.
    public void OnPointerDown(PointerEventData eventData)
    {
        // 클릭 가능하지 않으면 애니메이션만 실행하지 않음
        if (!isClickable) return;
        
        // 기존 애니메이션이 있다면 중단합니다.
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        // 클릭 시 커브를 사용한 스케일 다운 애니메이션을 시작합니다.
        scaleCoroutine = StartCoroutine(AnimateWithCurve(originalScale * targetScale, pressDuration, pressCurve));
        
        // Press 이벤트 호출
        OnButtonPressed?.Invoke();
    }

    // 포인터(마우스, 터치)가 버튼에서 떨어졌을 때 호출됩니다.
    public void OnPointerUp(PointerEventData eventData)
    {
        // 클릭 가능하지 않으면 애니메이션만 실행하지 않음
        if (!isClickable) return;
        
        // 기존 애니메이션이 있다면 중단합니다.
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }

        // 릴리즈 시 커브를 사용한 원래 스케일로 돌아가는 애니메이션을 시작합니다.
        scaleCoroutine = StartCoroutine(AnimateWithCurve(originalScale, releaseDuration, releaseCurve));
        
        // Release 이벤트 호출
        OnButtonReleased?.Invoke();
    }
    
    // 포인터가 버튼을 클릭했을 때 호출됩니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isClickable) return;
        
        // Click 이벤트 호출
        OnButtonClicked?.Invoke();
    }
    
    // 포인터가 버튼에 들어왔을 때 호출됩니다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isClickable) return;
        
        // Enter 이벤트 호출
        OnButtonEnter?.Invoke();
    }
    
    // 포인터가 버튼에서 나갔을 때 호출됩니다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isClickable) return;
        
        // Exit 이벤트 호출
        OnButtonExit?.Invoke();
    }
    
    #endregion
    
    #region Animation
    
    // 커브를 사용하여 스케일 애니메이션을 수행하는 코루틴입니다.
    private IEnumerator AnimateWithCurve(Vector3 targetScale, float duration, AnimationCurve curve)
    {
        // duration이 너무 작으면 즉시 설정
        if (duration <= 0.001f)
        {
            rectTransform.localScale = targetScale;
            yield break;
        }
        
        Vector3 startScale = rectTransform.localScale;
        float elapsedTime = 0f;  // 경과 시간
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            
            // 커브에서 백분율 값을 가져옵니다 (0~1)
            float curveValue = curve.Evaluate(t);
            
            // NaN 체크
            if (float.IsNaN(curveValue))
            {
                rectTransform.localScale = targetScale;
                yield break;
            }
            
            // 백분율 기반으로 시작점과 끝점 사이를 보간 (커브 값이 1을 넘으면 오버슈트 효과)
            Vector3 newScale = Vector3.LerpUnclamped(startScale, targetScale, curveValue);
            
            // 최종 NaN 체크
            if (float.IsNaN(newScale.x) || float.IsNaN(newScale.y) || float.IsNaN(newScale.z))
            {
                rectTransform.localScale = targetScale;
                yield break;
            }
            
            rectTransform.localScale = newScale;
            yield return null;
        }
        
        // 최종적으로 정확한 목표 스케일로 설정
        rectTransform.localScale = targetScale;
    }
    
    #endregion
    
    #region Convenience Methods
    
    // 버튼을 Normal 상태로 설정합니다.
    public void SetNormal()
    {
        SetState(ButtonState.Normal);
    }
    
    // 버튼을 Active 상태로 설정합니다.
    public void SetActive()
    {
        SetState(ButtonState.Active);
    }
    
    // 버튼을 Disabled 상태로 설정합니다.
    public void SetDisabled()
    {
        SetState(ButtonState.Disabled);
    }
    
    // 버튼을 클릭 가능하게 설정합니다.
    public void EnableClick()
    {
        SetClickable(true);
    }
    
    // 버튼을 클릭 불가능하게 설정합니다.
    public void DisableClick()
    {
        SetClickable(false);
    }
    
    #endregion
    
    #region Button Identification
    
    /// <summary>
    /// 버튼 ID를 반환합니다.
    /// </summary>
    public string GetButtonId()
    {
        return buttonId;
    }
    
    /// <summary>
    /// 버튼 ID를 설정합니다.
    /// </summary>
    public void SetButtonId(string newButtonId)
    {
        // 기존 등록 해제
        if (!string.IsNullOrEmpty(buttonId))
        {
            UnregisterFromEventSystem();
        }
        
        buttonId = newButtonId;
        
        // 새 ID로 등록
        if (!string.IsNullOrEmpty(buttonId))
        {
            RegisterToEventSystem();
        }
    }
    
    /// <summary>
    /// 이벤트 시스템에 버튼을 등록합니다.
    /// </summary>
    private void RegisterToEventSystem()
    {
        if (ButtonEventSystem.Instance != null)
        {
            ButtonEventSystem.Instance.RegisterButton(buttonId, OnButtonClicked);
        }
    }
    
    /// <summary>
    /// 이벤트 시스템에서 버튼 등록을 해제합니다.
    /// </summary>
    private void UnregisterFromEventSystem()
    {
        if (ButtonEventSystem.Instance != null)
        {
            ButtonEventSystem.Instance.UnregisterButton(buttonId);
        }
    }
    
    #endregion
    
    #region Dynamic State Management
    
    // 이미지에 새로운 상태별 컬러를 추가합니다.
    public void AddImageStateColor(int imageIndex, ButtonState state, Color color)
    {
        if (imageIndex < 0 || imageIndex >= imageColorInfos.Count) return;
        
        var imageInfo = imageColorInfos[imageIndex];
        var existingColor = imageInfo.stateColors.Find(sc => sc.state == state);
        
        if (existingColor != null)
        {
            existingColor.color = color;
        }
        else
        {
            imageInfo.stateColors.Add(new StateColorInfo { state = state, color = color });
        }
        
        // 현재 상태라면 즉시 적용
        if (state == currentState)
        {
            ApplyImageColors(imageInfo);
        }
    }
    
    // 텍스트에 새로운 상태별 컬러를 추가합니다.
    public void AddTextStateColor(int textIndex, ButtonState state, Color color)
    {
        if (textIndex < 0 || textIndex >= textColorInfos.Count) return;
        
        var textInfo = textColorInfos[textIndex];
        var existingColor = textInfo.stateColors.Find(sc => sc.state == state);
        
        if (existingColor != null)
        {
            existingColor.color = color;
        }
        else
        {
            textInfo.stateColors.Add(new StateColorInfo { state = state, color = color });
        }
        
        // 현재 상태라면 즉시 적용
        if (state == currentState)
        {
            ApplyTextColors(textInfo);
        }
    }
    
    // 아이콘에 새로운 상태별 GameObject를 추가합니다.
    public void AddIconStateGameObject(int iconIndex, ButtonState state, GameObject gameObject)
    {
        if (iconIndex < 0 || iconIndex >= iconGameObjectInfos.Count) return;
        
        var iconInfo = iconGameObjectInfos[iconIndex];
        var existingStateGameObject = iconInfo.stateGameObjects.Find(sg => sg.state == state);
        
        if (existingStateGameObject != null)
        {
            existingStateGameObject.gameObject = gameObject;
        }
        else
        {
            iconInfo.stateGameObjects.Add(new StateGameObjectInfo { state = state, gameObject = gameObject });
        }
        
        // 현재 상태라면 즉시 적용
        if (state == currentState)
        {
            ApplyIconGameObjects(iconInfo);
        }
    }
    
    // 새로운 이미지 컬러 정보를 추가하고 모든 상태를 자동 생성합니다.
    public void AddImageColorInfo(Image image)
    {
        if (image == null) return;
        
        var imageInfo = new ImageColorInfo { targetImage = image };
        imageColorInfos.Add(imageInfo);
        
        // 모든 상태를 자동으로 생성
        EnsureImageStatesExist(imageInfo);
    }
    
    // 새로운 텍스트 컬러 정보를 추가하고 모든 상태를 자동 생성합니다.
    public void AddTextColorInfo(TextMeshProUGUI text)
    {
        if (text == null) return;
        
        var textInfo = new TextColorInfo { targetText = text };
        textColorInfos.Add(textInfo);
        
        // 모든 상태를 자동으로 생성
        EnsureTextStatesExist(textInfo);
    }
    
    // 새로운 아이콘 GameObject 정보를 추가하고 모든 상태를 자동 생성합니다.
    public void AddIconGameObjectInfo()
    {
        var iconInfo = new IconGameObjectInfo();
        iconGameObjectInfos.Add(iconInfo);
        
        // 모든 상태를 자동으로 생성
        EnsureIconStatesExist(iconInfo);
    }
    
    #endregion
}

