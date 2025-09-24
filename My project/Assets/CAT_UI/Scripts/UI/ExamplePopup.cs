using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팝업에서 InteractiveButton 클릭을 처리하는 예제 클래스입니다.
/// </summary>
public class ExamplePopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InteractiveButton confirmButton;
    [SerializeField] private InteractiveButton cancelButton;
    [SerializeField] private InteractiveButton closeButton;
    
    [Header("Popup Content")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Text messageText;
    
    private void Start()
    {
        // 버튼 클릭 이벤트 연결
        SetupButtonEvents();
    }
    
    /// <summary>
    /// 버튼 이벤트를 설정합니다.
    /// </summary>
    private void SetupButtonEvents()
    {
        // 확인 버튼 클릭 시
        if (confirmButton != null)
        {
            confirmButton.OnButtonClicked.AddListener(OnConfirmButtonClicked);
        }
        
        // 취소 버튼 클릭 시
        if (cancelButton != null)
        {
            cancelButton.OnButtonClicked.AddListener(OnCancelButtonClicked);
        }
        
        // 닫기 버튼 클릭 시
        if (closeButton != null)
        {
            closeButton.OnButtonClicked.AddListener(OnCloseButtonClicked);
        }
    }
    
    /// <summary>
    /// 확인 버튼이 클릭되었을 때 호출됩니다.
    /// </summary>
    private void OnConfirmButtonClicked()
    {
        Debug.Log("확인 버튼이 클릭되었습니다!");
        
        // 팝업에서 확인 작업 수행
        ProcessConfirmAction();
        
        // 팝업 닫기
        ClosePopup();
    }
    
    /// <summary>
    /// 취소 버튼이 클릭되었을 때 호출됩니다.
    /// </summary>
    private void OnCancelButtonClicked()
    {
        Debug.Log("취소 버튼이 클릭되었습니다!");
        
        // 팝업 닫기
        ClosePopup();
    }
    
    /// <summary>
    /// 닫기 버튼이 클릭되었을 때 호출됩니다.
    /// </summary>
    private void OnCloseButtonClicked()
    {
        Debug.Log("닫기 버튼이 클릭되었습니다!");
        
        // 팝업 닫기
        ClosePopup();
    }
    
    /// <summary>
    /// 확인 작업을 처리합니다.
    /// </summary>
    private void ProcessConfirmAction()
    {
        // 여기에 실제 확인 작업 로직을 구현
        // 예: 데이터 저장, 서버 통신, 씬 전환 등
        
        // 예제: 메시지 표시
        if (messageText != null)
        {
            messageText.text = "작업이 완료되었습니다!";
        }
    }
    
    /// <summary>
    /// 팝업을 닫습니다.
    /// </summary>
    private void ClosePopup()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
        
        // 또는 애니메이션으로 닫기
        // StartCoroutine(ClosePopupAnimation());
    }
    
    /// <summary>
    /// 팝업을 표시합니다.
    /// </summary>
    public void ShowPopup(string message = "")
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
        
        if (messageText != null && !string.IsNullOrEmpty(message))
        {
            messageText.text = message;
        }
    }
    
    /// <summary>
    /// 버튼 상태를 설정합니다.
    /// </summary>
    public void SetButtonStates(bool confirmEnabled, bool cancelEnabled)
    {
        if (confirmButton != null)
        {
            confirmButton.SetClickable(confirmEnabled);
            confirmButton.SetState(confirmEnabled ? ButtonState.Normal : ButtonState.Disabled);
        }
        
        if (cancelButton != null)
        {
            cancelButton.SetClickable(cancelEnabled);
            cancelButton.SetState(cancelEnabled ? ButtonState.Normal : ButtonState.Disabled);
        }
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 정리 (메모리 누수 방지)
        if (confirmButton != null)
        {
            confirmButton.OnButtonClicked.RemoveListener(OnConfirmButtonClicked);
        }
        
        if (cancelButton != null)
        {
            cancelButton.OnButtonClicked.RemoveListener(OnCancelButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.OnButtonClicked.RemoveListener(OnCloseButtonClicked);
        }
    }
}
