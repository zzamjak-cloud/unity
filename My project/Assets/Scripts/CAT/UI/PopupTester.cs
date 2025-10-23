using UnityEngine;

/// <summary>
/// 팝업의 등장/퇴장 애니메이션을 테스트하는 스크립트입니다.
/// </summary>
public class PopupTester : MonoBehaviour
{
    [Header("Test Popup")]
    [SerializeField] private BasePopup testPopup;
    
    private void Start()
    {
        // 버튼 설정이 필요 없으므로 제거
    }
    
    /// <summary>
    /// 테스트 팝업을 표시합니다.
    /// </summary>
    public void ShowTestPopup()
    {
        if (testPopup != null)
        {
            Debug.Log("테스트 팝업 표시");
            testPopup.ShowPopup("테스트 팝업입니다!");
        }
        else
        {
            Debug.LogWarning("TestPopup이 설정되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// 테스트 팝업을 숨깁니다.
    /// </summary>
    public void HideTestPopup()
    {
        if (testPopup != null)
        {
            Debug.Log("테스트 팝업 숨김");
            testPopup.HidePopup();
        }
        else
        {
            Debug.LogWarning("TestPopup이 설정되지 않았습니다!");
        }
    }
    
    
    /// <summary>
    /// 팝업 애니메이션을 반복 테스트합니다.
    /// </summary>
    public void TestAnimation()
    {
        if (testPopup == null)
        {
            Debug.LogWarning("TestPopup이 설정되지 않았습니다!");
            return;
        }
        
        StartCoroutine(TestAnimationSequence());
    }
    
    /// <summary>
    /// 애니메이션을 반복 테스트하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator TestAnimationSequence()
    {
        Debug.Log("=== 팝업 애니메이션 테스트 시작 ===");
        
        // 팝업 표시
        testPopup.ShowPopup("애니메이션 테스트");
        
        // 표시 애니메이션 완료까지 대기
        yield return new WaitForSeconds(1f);
        
        // 팝업 숨김
        testPopup.HidePopup();
        
        // 숨김 애니메이션 완료까지 대기
        yield return new WaitForSeconds(1f);
        
        Debug.Log("=== 팝업 애니메이션 테스트 완료 ===");
    }
    
    /// <summary>
    /// 키보드 입력으로 팝업을 제어합니다.
    /// </summary>
    private void Update()
    {
        // 키보드 단축키
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ShowTestPopup();
        }
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HideTestPopup();
        }
        
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            TestAnimation();
        }
    }
    
    /// <summary>
    /// 인스펙터에서 호출할 수 있는 메서드들
    /// </summary>
    [ContextMenu("Show Popup")]
    public void ContextShowPopup()
    {
        ShowTestPopup();
    }
    
    [ContextMenu("Hide Popup")]
    public void ContextHidePopup()
    {
        HideTestPopup();
    }
    
    
    [ContextMenu("Test Animation")]
    public void ContextTestAnimation()
    {
        TestAnimation();
    }
    
    private void OnDestroy()
    {
        // 버튼 이벤트가 없으므로 정리할 것 없음
    }
}
