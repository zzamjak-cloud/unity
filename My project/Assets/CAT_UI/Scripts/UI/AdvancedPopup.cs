using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ButtonEventSystem을 사용한 고급 팝업 예제입니다.
/// 버튼 ID를 통해 클릭을 감지합니다.
/// </summary>
public class AdvancedPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InteractiveButton confirmButton;
    [SerializeField] private InteractiveButton cancelButton;
    [SerializeField] private InteractiveButton closeButton;
    
    [Header("Popup Content")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Text messageText;
    [SerializeField] private Text titleText;
    
    // 버튼 ID 상수
    private const string CONFIRM_BUTTON_ID = "popup_confirm";
    private const string CANCEL_BUTTON_ID = "popup_cancel";
    private const string CLOSE_BUTTON_ID = "popup_close";
    
    private void Start()
    {
        // 버튼 ID 설정
        SetupButtonIds();
        
        // 이벤트 시스템에 리스너 등록
        RegisterEventListeners();
    }
    
    /// <summary>
    /// 버튼 ID를 설정합니다.
    /// </summary>
    private void SetupButtonIds()
    {
        if (confirmButton != null)
        {
            confirmButton.SetButtonId(CONFIRM_BUTTON_ID);
        }
        
        if (cancelButton != null)
        {
            cancelButton.SetButtonId(CANCEL_BUTTON_ID);
        }
        
        if (closeButton != null)
        {
            closeButton.SetButtonId(CLOSE_BUTTON_ID);
        }
    }
    
    /// <summary>
    /// 이벤트 리스너를 등록합니다.
    /// </summary>
    private void RegisterEventListeners()
    {
        if (ButtonEventSystem.Instance != null)
        {
            ButtonEventSystem.Instance.OnButtonClickedWithId.AddListener(OnButtonClicked);
        }
    }
    
    /// <summary>
    /// 버튼 클릭을 처리합니다.
    /// </summary>
    /// <param name="buttonId">클릭된 버튼의 ID</param>
    private void OnButtonClicked(string buttonId)
    {
        switch (buttonId)
        {
            case CONFIRM_BUTTON_ID:
                HandleConfirmClick();
                break;
                
            case CANCEL_BUTTON_ID:
                HandleCancelClick();
                break;
                
            case CLOSE_BUTTON_ID:
                HandleCloseClick();
                break;
                
            default:
                Debug.LogWarning($"알 수 없는 버튼 ID: {buttonId}");
                break;
        }
    }
    
    /// <summary>
    /// 확인 버튼 클릭 처리
    /// </summary>
    private void HandleConfirmClick()
    {
        Debug.Log("확인 버튼이 클릭되었습니다!");
        
        // 확인 작업 수행
        ProcessConfirmAction();
        
        // 팝업 닫기
        ClosePopup();
    }
    
    /// <summary>
    /// 취소 버튼 클릭 처리
    /// </summary>
    private void HandleCancelClick()
    {
        Debug.Log("취소 버튼이 클릭되었습니다!");
        
        // 취소 작업 수행
        ProcessCancelAction();
        
        // 팝업 닫기
        ClosePopup();
    }
    
    /// <summary>
    /// 닫기 버튼 클릭 처리
    /// </summary>
    private void HandleCloseClick()
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
        
        Debug.Log("확인 작업이 완료되었습니다!");
        
        // 예제: 메시지 표시
        if (messageText != null)
        {
            messageText.text = "작업이 완료되었습니다!";
        }
    }
    
    /// <summary>
    /// 취소 작업을 처리합니다.
    /// </summary>
    private void ProcessCancelAction()
    {
        // 취소 시 수행할 작업
        Debug.Log("작업이 취소되었습니다!");
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
    /// <param name="title">팝업 제목</param>
    /// <param name="message">팝업 메시지</param>
    /// <param name="showCancelButton">취소 버튼 표시 여부</param>
    public void ShowPopup(string title = "", string message = "", bool showCancelButton = true)
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
        
        if (titleText != null && !string.IsNullOrEmpty(title))
        {
            titleText.text = title;
        }
        
        if (messageText != null && !string.IsNullOrEmpty(message))
        {
            messageText.text = message;
        }
        
        // 취소 버튼 표시/숨김
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(showCancelButton);
        }
    }
    
    /// <summary>
    /// 버튼 상태를 설정합니다.
    /// </summary>
    /// <param name="confirmEnabled">확인 버튼 활성화 여부</param>
    /// <param name="cancelEnabled">취소 버튼 활성화 여부</param>
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
    
    /// <summary>
    /// 외부에서 특정 버튼을 프로그래밍 방식으로 클릭합니다.
    /// </summary>
    /// <param name="buttonId">클릭할 버튼의 ID</param>
    public void TriggerButtonClick(string buttonId)
    {
        if (ButtonEventSystem.Instance != null)
        {
            ButtonEventSystem.Instance.TriggerButtonClick(buttonId);
        }
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 정리
        if (ButtonEventSystem.Instance != null)
        {
            ButtonEventSystem.Instance.OnButtonClickedWithId.RemoveListener(OnButtonClicked);
        }
    }
}
