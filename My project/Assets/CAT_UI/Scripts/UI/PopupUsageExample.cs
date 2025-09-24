using UnityEngine;

/// <summary>
/// InteractiveButton과 팝업 사용법을 보여주는 예제입니다.
/// </summary>
public class PopupUsageExample : MonoBehaviour
{
    [Header("Popup References")]
    [SerializeField] private ExamplePopup simplePopup;
    [SerializeField] private AdvancedPopup advancedPopup;
    
    [Header("Test Buttons")]
    [SerializeField] private InteractiveButton showSimplePopupButton;
    [SerializeField] private InteractiveButton showAdvancedPopupButton;
    [SerializeField] private InteractiveButton testButtonIdButton;
    
    private void Start()
    {
        // 테스트 버튼들에 이벤트 연결
        SetupTestButtons();
    }
    
    /// <summary>
    /// 테스트 버튼들을 설정합니다.
    /// </summary>
    private void SetupTestButtons()
    {
        if (showSimplePopupButton != null)
        {
            showSimplePopupButton.OnButtonClicked.AddListener(ShowSimplePopup);
        }
        
        if (showAdvancedPopupButton != null)
        {
            showAdvancedPopupButton.OnButtonClicked.AddListener(ShowAdvancedPopup);
        }
        
        if (testButtonIdButton != null)
        {
            testButtonIdButton.OnButtonClicked.AddListener(TestButtonIdSystem);
        }
    }
    
    /// <summary>
    /// 간단한 팝업을 표시합니다.
    /// </summary>
    private void ShowSimplePopup()
    {
        Debug.Log("간단한 팝업을 표시합니다.");
        
        if (simplePopup != null)
        {
            simplePopup.ShowPopup("이것은 UnityEvent를 사용한 간단한 팝업입니다.");
        }
    }
    
    /// <summary>
    /// 고급 팝업을 표시합니다.
    /// </summary>
    private void ShowAdvancedPopup()
    {
        Debug.Log("고급 팝업을 표시합니다.");
        
        if (advancedPopup != null)
        {
            advancedPopup.ShowPopup("고급 팝업", "이것은 ButtonEventSystem을 사용한 고급 팝업입니다.", true);
        }
    }
    
    /// <summary>
    /// 버튼 ID 시스템을 테스트합니다.
    /// </summary>
    private void TestButtonIdSystem()
    {
        Debug.Log("버튼 ID 시스템을 테스트합니다.");
        
        // 프로그래밍 방식으로 버튼 클릭 트리거
        if (advancedPopup != null)
        {
            // 2초 후에 확인 버튼을 자동으로 클릭
            Invoke(nameof(AutoClickConfirm), 2f);
        }
    }
    
    /// <summary>
    /// 자동으로 확인 버튼을 클릭합니다.
    /// </summary>
    private void AutoClickConfirm()
    {
        if (advancedPopup != null)
        {
            advancedPopup.TriggerButtonClick("popup_confirm");
        }
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 정리
        if (showSimplePopupButton != null)
        {
            showSimplePopupButton.OnButtonClicked.RemoveListener(ShowSimplePopup);
        }
        
        if (showAdvancedPopupButton != null)
        {
            showAdvancedPopupButton.OnButtonClicked.RemoveListener(ShowAdvancedPopup);
        }
        
        if (testButtonIdButton != null)
        {
            testButtonIdButton.OnButtonClicked.RemoveListener(TestButtonIdSystem);
        }
    }
}
