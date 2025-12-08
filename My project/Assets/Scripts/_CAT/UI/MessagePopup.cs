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
    // 팝업 설정은 코드에서 동적으로 처리
    
    // 팝업 타입별 이벤트
    public event Action OnConfirmClicked;
    public event Action OnCancelClicked;
    public event Action OnCloseClicked;
    
    // 팝업 결과 콜백
    public event Action<bool> OnPopupResult; // true: 확인, false: 취소/닫기
    
    protected override void OnStart()
    {
        // 기본적으로는 버튼을 설정하지 않음
        // 필요에 따라 외부에서 ShowConfirmOnly, ShowConfirmCancel 등을 호출
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
    protected override void OnCloseButtonClicked()
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
    /// <param name="onResult">결과 콜백 (true: 확인, false: 취소/닫기)</param>
    public void ShowMessage(string message, Action<bool> onResult = null)
    {
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
        // 기존 버튼들 제거
        ClearAllButtons();
        
        // 확인 버튼 추가
        AddButton("confirm", "확인", OnConfirmButtonClicked);
        
        if (onConfirm != null)
        {
            OnConfirmClicked += onConfirm;
        }
        
        ShowPopup(message);
    }
    
    /// <summary>
    /// 확인/취소 팝업을 표시합니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="onConfirm">확인 콜백</param>
    /// <param name="onCancel">취소 콜백</param>
    public void ShowConfirmCancel(string message, Action onConfirm = null, Action onCancel = null)
    {
        // 기존 버튼들 제거
        ClearAllButtons();
        
        // 확인/취소 버튼 추가
        AddButton("confirm", "확인", OnConfirmButtonClicked);
        AddButton("cancel", "취소", OnCancelButtonClicked);
        
        if (onConfirm != null)
        {
            OnConfirmClicked += onConfirm;
        }
        
        if (onCancel != null)
        {
            OnCancelClicked += onCancel;
        }
        
        ShowPopup(message);
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

