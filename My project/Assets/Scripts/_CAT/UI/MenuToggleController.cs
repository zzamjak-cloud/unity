using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InteractiveButton 클릭으로 UISlideTransitionController를 토글하는 브릿지.
/// 공용 버튼 스크립트는 그대로 사용하고, 토글 로직만 분리한다.
/// </summary>
public class MenuToggleController : MonoBehaviour
{
    public static System.Action<MenuToggleController> OnAnyMenuToggleClicked; // 전역 통지 이벤트
    public event System.Action<MenuToggleController> OnClicked; // 외부 구독자가 구독할 수 있는 클릭 이벤트

    [SerializeField] private InteractiveButton menuButton; // 커스텀 버튼
    [SerializeField] private Button unityUIButton;         // Unity 기본 Button
    [SerializeField] private UISlideTransitionController transition;
    [SerializeField] private bool setImmediateHiddenOnEnable = false; // 시작을 숨김 상태로 강제할지
    [SerializeField] private bool ignoreInputWhileAnimating = true;   // 애니메이션 중 입력 무시

    private void Awake()
    {
        if (menuButton != null)
        {
            menuButton.OnButtonClicked.AddListener(OnMenuClicked);
        }
        if (unityUIButton != null)
        {
            unityUIButton.onClick.AddListener(OnMenuClicked);
        }
    }

    

    private void OnEnable()
    {
        if (setImmediateHiddenOnEnable && transition != null)
        {
            transition.SetImmediate(false);
        }
        OnAnyMenuToggleClicked += HandleOtherClicked;
    }

    private void OnDestroy()
    {
        if (menuButton != null)
        {
            menuButton.OnButtonClicked.RemoveListener(OnMenuClicked);
        }
        if (unityUIButton != null)
        {
            unityUIButton.onClick.RemoveListener(OnMenuClicked);
        }
        OnAnyMenuToggleClicked -= HandleOtherClicked;
    }

    private void OnDisable()
    {
        OnAnyMenuToggleClicked -= HandleOtherClicked;
    }

    private void OnMenuClicked()
    {
        if (transition == null) return;

        if (ignoreInputWhileAnimating && transition.IsAnimating)
        {
            return;
        }

        // 외부 구독자에게 먼저 통지
        OnClicked?.Invoke(this);

        // 전역 이벤트로 통지
        OnAnyMenuToggleClicked?.Invoke(this);

        if (transition.IsShowing)
        {
            transition.PlayOut();
        }
        else
        {
            transition.PlayIn();
        }
    }

    // 인스펙터 할당을 대신할 수 있는 보조 메서드
    public void SetReferences(InteractiveButton button, UISlideTransitionController slide)
    {
        if (menuButton != null)
        {
            menuButton.OnButtonClicked.RemoveListener(OnMenuClicked);
        }

        menuButton = button;
        transition = slide;

        if (menuButton != null)
        {
            menuButton.OnButtonClicked.AddListener(OnMenuClicked);
        }
    }

    // Unity 기본 Button 연결용 보조 메서드
    public void SetReferences(Button button, UISlideTransitionController slide)
    {
        if (unityUIButton != null)
        {
            unityUIButton.onClick.RemoveListener(OnMenuClicked);
        }

        unityUIButton = button;
        transition = slide;

        if (unityUIButton != null)
        {
            unityUIButton.onClick.AddListener(OnMenuClicked);
        }
    }

    // 외부에서 호출: 열려있으면 닫기
    public void CloseIfOpen()
    {
        if (transition == null) return;
        if (transition.IsAnimating) return;
        if (transition.IsShowing)
        {
            transition.PlayOut();
        }
    }

    private void HandleOtherClicked(MenuToggleController sender)
    {
        if (sender == this) return;
        CloseIfOpen();
    }

    // 외부에서 참조할 수 있도록 읽기 전용 공개 프로퍼티 제공
    public UISlideTransitionController Transition => transition;
}


