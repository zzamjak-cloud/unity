using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 버튼 이벤트를 관리하는 시스템입니다.
/// 버튼 ID를 통해 특정 버튼의 클릭을 감지할 수 있습니다.
/// </summary>
public class ButtonEventSystem : MonoBehaviour
{
    [System.Serializable]
    public class ButtonClickEvent : UnityEvent<string> { }
    
    [Header("Events")]
    public ButtonClickEvent OnButtonClickedWithId;  // 버튼 ID와 함께 클릭 이벤트
    
    // 버튼 ID별 이벤트 저장
    private Dictionary<string, UnityEvent> buttonEvents = new Dictionary<string, UnityEvent>();
    
    private static ButtonEventSystem instance;
    public static ButtonEventSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ButtonEventSystem>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ButtonEventSystem");
                    instance = go.AddComponent<ButtonEventSystem>();
                }
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 버튼을 등록합니다.
    /// </summary>
    /// <param name="buttonId">버튼 고유 ID</param>
    /// <param name="onClickEvent">클릭 시 호출될 이벤트</param>
    public void RegisterButton(string buttonId, UnityEvent onClickEvent)
    {
        if (!buttonEvents.ContainsKey(buttonId))
        {
            buttonEvents[buttonId] = new UnityEvent();
        }
        
        buttonEvents[buttonId].AddListener(() => {
            onClickEvent?.Invoke();
            OnButtonClickedWithId?.Invoke(buttonId);
        });
    }
    
    /// <summary>
    /// 버튼 등록을 해제합니다.
    /// </summary>
    /// <param name="buttonId">버튼 고유 ID</param>
    public void UnregisterButton(string buttonId)
    {
        if (buttonEvents.ContainsKey(buttonId))
        {
            buttonEvents[buttonId].RemoveAllListeners();
            buttonEvents.Remove(buttonId);
        }
    }
    
    /// <summary>
    /// 특정 버튼의 클릭 이벤트를 호출합니다.
    /// </summary>
    /// <param name="buttonId">버튼 고유 ID</param>
    public void TriggerButtonClick(string buttonId)
    {
        if (buttonEvents.ContainsKey(buttonId))
        {
            buttonEvents[buttonId]?.Invoke();
        }
    }
    
    /// <summary>
    /// 등록된 모든 버튼을 정리합니다.
    /// </summary>
    public void ClearAllButtons()
    {
        foreach (var kvp in buttonEvents)
        {
            kvp.Value.RemoveAllListeners();
        }
        buttonEvents.Clear();
    }
}
