using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// <summary>
// UITabButton은 개별 탭 버튼의 시각적 상태를 관리합니다.
// 탭 버튼에 부착하여 사용합니다.
// </summary>

namespace CAT.UI
{
    public class UITabButton : MonoBehaviour
    {
        [SerializeField] private GameObject stateOn;
        [SerializeField] private GameObject stateOff;
        private Button button;
        public UnityEvent onClick = new UnityEvent();  // 탭 클릭시 호출될 이벤트를 담습니다.

        private void Awake()
        {
            // 버튼 컴포넌트를 가져옵니다.
            button = GetComponent<Button>();
            if (button != null)
            {
                // 버튼 클릭 이벤트를 추가합니다.
                button.onClick.AddListener(OnButtonClick);
            }
        }

        // 탭의 시각적 상태를 설정하는 메서드 (외부에서 호출)
        public void SetTabState(bool isOn)
        {
            if (stateOn != null) stateOn.SetActive(isOn);
            if (stateOff != null) stateOff.SetActive(!isOn);
        }

        // 버튼 클릭시 호출될 메서드
        private void OnButtonClick()
        {
            // 외부 스크립트에 이벤트를 전달합니다.
            onClick.Invoke();
        }
    }
}
