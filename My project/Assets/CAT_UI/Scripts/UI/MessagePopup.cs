using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 공용 메시지 팝업 클래스입니다.
/// BasePopup을 상속받아 일반적인 확인/취소 메시지 팝업 기능을 제공합니다.
/// </summary>
public class MessagePopup : BasePopup
{
    [Header("Message Popup Settings")]
    [SerializeField] private MessagePopupType popupType = MessagePopupType.ConfirmCancel;
    [SerializeField] private string confirmButtonText = "확인";
    [SerializeField] private string cancelButtonText = "취소";
    
    // 팝업 타입별 이벤트
    public event Action OnConfirmClicked;
    public event Action OnCancelClicked;
    public event Action OnCloseClicked;
    
    // 팝업 결과 콜백
    public event Action<bool> OnPopupResult; // true: 확인, false: 취소/닫기
    
    protected override void OnStart()
    {
        SetupPopupByType();
    }
    
    /// <summary>
    /// 팝업 타입에 따라 버튼을 설정합니다.
    /// </summary>
    private void SetupPopupByType()
    {
        switch (popupType)
        {
            case MessagePopupType.ConfirmOnly:
                SetupConfirmOnlyPopup();
                break;
                
            case MessagePopupType.ConfirmCancel:
                SetupConfirmCancelPopup();
                break;
                
            case MessagePopupType.Custom:
                // 커스텀 타입은 외부에서 버튼을 직접 설정
                break;
        }
    }
    
    /// <summary>
    /// 확인 버튼만 있는 팝업 설정
    /// </summary>
    private void SetupConfirmOnlyPopup()
    {
        AddButton("confirm", confirmButtonText, OnConfirmButtonClicked);
    }
    
    /// <summary>
    /// 확인/취소 버튼이 있는 팝업 설정
    /// </summary>
    private void SetupConfirmCancelPopup()
    {
        AddButton("confirm", confirmButtonText, OnConfirmButtonClicked);
        AddButton("cancel", cancelButtonText, OnCancelButtonClicked);
    }
    
    /// <summary>
    /// 확인 버튼 클릭 처리
    /// </summary>
    private void OnConfirmButtonClicked()
    {
        Debug.Log("메시지 팝업 - 확인 버튼 클릭");
        
        OnConfirmClicked?.Invoke();
        OnPopupResult?.Invoke(true);
        
        HidePopup();
    }
    
    /// <summary>
    /// 취소 버튼 클릭 처리
    /// </summary>
    private void OnCancelButtonClicked()
    {
        Debug.Log("메시지 팝업 - 취소 버튼 클릭");
        
        OnCancelClicked?.Invoke();
        OnPopupResult?.Invoke(false);
        
        HidePopup();
    }
    
    /// <summary>
    /// 닫기 버튼 클릭 처리 (X 버튼 등)
    /// </summary>
    private void OnCloseButtonClicked()
    {
        Debug.Log("메시지 팝업 - 닫기 버튼 클릭");
        
        OnCloseClicked?.Invoke();
        OnPopupResult?.Invoke(false);
        
        HidePopup();
    }
    
    /// <summary>
    /// 메시지 팝업을 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="type">팝업 타입</param>
    /// <param name="onResult">결과 콜백 (true: 확인, false: 취소/닫기)</param>
    public void ShowMessage(string message, MessagePopupType type = MessagePopupType.ConfirmCancel, Action<bool> onResult = null)
    {
        popupType = type;
        SetupPopupByType();
        
        if (onResult != null)
        {
            OnPopupResult += onResult;
        }
        
        ShowPopup(message);
    }
    
    /// <summary>
    /// 확인만 있는 팝업을 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="onConfirm">확인 콜백</param>
    public void ShowConfirmOnly(string message, Action onConfirm = null)
    {
        if (onConfirm != null)
        {
            OnConfirmClicked += onConfirm;
        }
        
        ShowMessage(message, MessagePopupType.ConfirmOnly);
    }
    
    /// <summary>
    /// 확인/취소 팝업을 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="onConfirm">확인 콜백</param>
    /// <param name="onCancel">취소 콜백</param>
    public void ShowConfirmCancel(string message, Action onConfirm = null, Action onCancel = null)
    {
        if (onConfirm != null)
        {
            OnConfirmClicked += onConfirm;
        }
        
        if (onCancel != null)
        {
            OnCancelClicked += onCancel;
        }
        
        ShowMessage(message, MessagePopupType.ConfirmCancel);
    }
    
    /// <summary>
    /// 커스텀 버튼을 추가합니다.
    /// </summary>
    /// <param name="buttonId">버튼 ID</param>
    /// <param name="buttonText">버튼 텍스트</param>
    /// <param name="onClick">클릭 콜백</param>
    public void AddCustomButton(string buttonId, string buttonText, Action onClick)
    {
        AddButton(buttonId, buttonText, onClick);
    }
    
    /// <summary>
    /// 버튼 텍스트를 설정합니다.
    /// </summary>
    /// <param name="confirmText">확인 버튼 텍스트</param>
    /// <param name="cancelText">취소 버튼 텍스트</param>
    public void SetButtonTexts(string confirmText = null, string cancelText = null)
    {
        if (!string.IsNullOrEmpty(confirmText))
        {
            confirmButtonText = confirmText;
        }
        
        if (!string.IsNullOrEmpty(cancelText))
        {
            cancelButtonText = cancelText;
        }
        
        // 기존 버튼이 있다면 텍스트 업데이트
        UpdateButtonTexts();
    }
    
    /// <summary>
    /// 버튼 텍스트를 업데이트합니다.
    /// </summary>
    private void UpdateButtonTexts()
    {
        // 동적 버튼들의 텍스트 업데이트
        foreach (var button in dynamicButtons)
        {
            if (button.name == "Button_confirm")
            {
                var textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = confirmButtonText;
                }
            }
            else if (button.name == "Button_cancel")
            {
                var textComponent = button.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = cancelButtonText;
                }
            }
        }
    }
    
    /// <summary>
    /// 팝업 타입을 설정합니다.
    /// </summary>
    /// <param name="type">새로운 팝업 타입</param>
    public void SetPopupType(MessagePopupType type)
    {
        if (popupType != type)
        {
            popupType = type;
            ClearAllButtons();
            SetupPopupByType();
        }
    }
    
    /// <summary>
    /// 특정 버튼을 비활성화합니다.
    /// </summary>
    /// <param name="buttonId">비활성화할 버튼 ID</param>
    public void DisableButton(string buttonId)
    {
        SetButtonState(buttonId, false);
    }
    
    /// <summary>
    /// 특정 버튼을 활성화합니다.
    /// </summary>
    /// <param name="buttonId">활성화할 버튼 ID</param>
    public void EnableButton(string buttonId)
    {
        SetButtonState(buttonId, true);
    }
    
    /// <summary>
    /// 팝업이 숨겨질 때 이벤트 정리
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        // 이벤트 정리
        OnConfirmClicked = null;
        OnCancelClicked = null;
        OnCloseClicked = null;
        OnPopupResult = null;
    }
    
    /// <summary>
    /// 팝업이 숨겨진 후 호출되는 메서드
    /// </summary>
    protected override void OnButtonClickedInternal(string buttonId)
    {
        // 하위 클래스에서 추가 처리할 로직이 있다면 여기에 구현
        Debug.Log($"메시지 팝업 - 버튼 클릭: {buttonId}");
    }
}

/// <summary>
/// 메시지 팝업 타입
/// </summary>
public enum MessagePopupType
{
    ConfirmOnly,    // 확인 버튼만
    ConfirmCancel,  // 확인/취소 버튼
    Custom          // 커스텀 버튼들
}
