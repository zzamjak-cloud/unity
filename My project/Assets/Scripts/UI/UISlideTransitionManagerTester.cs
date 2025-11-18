using UnityEngine;

/// <summary>
/// UISlideTransitionManager 테스트용 스크립트
/// </summary>
public class UISlideTransitionManagerTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UISlideTransitionManager transitionManager;

    [Header("Test Settings")]
    [SerializeField] private KeyCode testPlayInAllKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode testPlayOutAllKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode testPlayInByTagKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode testPlayOutByTagKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode testPresetKey = KeyCode.Alpha5;

    [Header("Test Parameters")]
    [SerializeField] private string testTag = "헤더";
    [SerializeField] private string testPresetName = "최초 로비 진입";

    [Header("Auto Start")]
    [SerializeField] private bool autoPlayInOnStart = true; // 시작 시 자동으로 전체 PlayIn 실행

    private void Start()
    {
        if (autoPlayInOnStart && transitionManager != null)
        {
            Debug.Log("[테스트] 최초 실행 - 전체 PlayIn 자동 실행");
            transitionManager.PlayInAll();
        }
    }

    private void Update()
    {
        if (transitionManager == null) return;

        // 1번 키: 전체 PlayIn
        if (Input.GetKeyDown(testPlayInAllKey))
        {
            Debug.Log("[테스트] 전체 PlayIn 실행");
            transitionManager.PlayInAll();
        }

     // 2번 키: 전체 PlayOut
        if (Input.GetKeyDown(testPlayOutAllKey))
        {
            Debug.Log("[테스트] 전체 PlayOut 실행");
            transitionManager.PlayOutAll();
        }

        // 3번 키: 태그로 PlayIn
        if (Input.GetKeyDown(testPlayInByTagKey))
        {
            Debug.Log($"[테스트] '{testTag}' 태그 PlayIn 실행");
            transitionManager.PlayInByTag(testTag);
        }

        // 4번 키: 태그로 PlayOut
        if (Input.GetKeyDown(testPlayOutByTagKey))
        {
            Debug.Log($"[테스트] '{testTag}' 태그 PlayOut 실행");
            transitionManager.PlayOutByTag(testTag);
        }

        // 5번 키: 프리셋 실행
        if (Input.GetKeyDown(testPresetKey))
        {
            Debug.Log($"[테스트] 프리셋 '{testPresetName}' 실행");
            transitionManager.PlayPreset(testPresetName);
        }
    }

    /// <summary>
    /// 버튼 클릭으로 테스트할 수 있는 메서드들 (UI Button 이벤트에 연결 가능)
    /// </summary>
    public void TestPlayInAll()
    {
        if (transitionManager == null) return;
        transitionManager.PlayInAll();
        Debug.Log("[테스트] 전체 PlayIn 실행");
    }

    public void TestPlayOutAll()
    {
        if (transitionManager == null) return;
        transitionManager.PlayOutAll();
        Debug.Log("[테스트] 전체 PlayOut 실행");
    }

    public void TestPlayInByTag(string tag)
    {
        if (transitionManager == null) return;
        transitionManager.PlayInByTag(tag);
        Debug.Log($"[테스트] '{tag}' 태그 PlayIn 실행");
    }

    public void TestPlayOutByTag(string tag)
    {
        if (transitionManager == null) return;
        transitionManager.PlayOutByTag(tag);
        Debug.Log($"[테스트] '{tag}' 태그 PlayOut 실행");
    }

    public void TestPlayPreset(string presetName)
    {
        if (transitionManager == null) return;
        transitionManager.PlayPreset(presetName);
        Debug.Log($"[테스트] 프리셋 '{presetName}' 실행");
    }
}

