using UnityEngine;
using System.Collections.Generic;

// 이 스크립트는 모든 탭 버튼과 탭 그룹을 관리합니다.
// Unity 에디터에서 탭 버튼과 탭 그룹을 연결해 주세요.

public class TabManager : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public TabButton tabButton;
        public GameObject tabGroup;
    }

    // 탭 버튼과 탭 그룹의 연결 리스트
    [SerializeField] private List<Tab> tabs;
    
    // 현재 활성화된 탭의 인덱스를 저장합니다.
    private int activeTabIndex = -1;

    // 초기 설정 메서드
    private void Start()
    {
        // 모든 탭 버튼에 이벤트를 연결합니다.
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i; // 클로저 이슈를 방지하기 위해 로컬 변수 사용
            tabs[i].tabButton.onClick.AddListener(() => OnTabButtonClicked(index));
        }

        // 첫 번째 탭을 기본으로 활성화합니다.
        if (tabs.Count > 0)
        {
            SelectTab(0);
        }
    }

    // 탭 버튼이 클릭되었을 때 호출되는 메서드
    private void OnTabButtonClicked(int index)
    {
        if (activeTabIndex != index)
        {
            SelectTab(index);
        }
    }

    // 특정 탭을 선택하고 활성화/비활성화를 처리하는 메서드
    public void SelectTab(int index)
    {
        // 유효하지 않은 인덱스인 경우 처리하지 않습니다.
        if (index < 0 || index >= tabs.Count)
        {
            Debug.LogError("잘못된 탭 인덱스입니다: " + index);
            return;
        }

        // 이전에 활성화된 탭을 비활성화합니다.
        if (activeTabIndex != -1)
        {
            tabs[activeTabIndex].tabButton.SetTabState(false);
            tabs[activeTabIndex].tabGroup.SetActive(false);
        }

        // 새로운 탭을 활성화합니다.
        tabs[index].tabButton.SetTabState(true);
        tabs[index].tabGroup.SetActive(true);

        // 현재 활성화된 탭 인덱스를 업데이트합니다.
        activeTabIndex = index;
    }
}
