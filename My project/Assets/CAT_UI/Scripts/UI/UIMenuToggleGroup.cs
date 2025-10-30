using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 MenuToggleController를 그룹으로 묶어
/// 하나를 클릭하면 나머지의 팝업을 자동으로 닫아주는 관리 컴포넌트.
/// 하단 5개 버튼 같은 탭/메뉴 구성에 사용.
/// </summary>
public class UIMenuToggleGroup : MonoBehaviour
{
    [System.Serializable]
    public class Member
    {
        public MenuToggleController controller; // 버튼에 붙은 브릿지
        public UISlideTransitionController transitionOverride; // 필요 시 직접 지정 팝업 (미지정 시 controller에서 자동 참조)
    }

    [SerializeField] private List<Member> members = new List<Member>();

    private bool listenersBound = false;

    /// <summary>
    /// MenuToggleController에서 클릭 통지 시 호출
    /// </summary>
    public void NotifyClicked(MenuToggleController sender)
    {
        NormalizeMembers();
        CloseOthers(sender);
    }

    private void OnEnable()
    {
        BindMemberListeners();
    }

    private void OnDisable()
    {
        UnbindMemberListeners();
    }

    public void CloseOthers(MenuToggleController sender)
    {
        NormalizeMembers();
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            if (m.controller == sender) continue;

            // 우선 override transition이 있으면 그걸 닫고, 없으면 컨트롤러를 통해 닫기
            var tr = m.transitionOverride != null ? m.transitionOverride : m.controller != null ? m.controller.Transition : null;
            if (tr != null)
            {
                if (tr.IsShowing && !tr.IsAnimating)
                {
                    tr.PlayOut();
                }
            }
            else if (m.controller != null)
            {
                m.controller.CloseIfOpen();
            }
        }
    }

    public void CloseAll()
    {
        NormalizeMembers();
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            var tr = m.transitionOverride != null ? m.transitionOverride : m.controller != null ? m.controller.Transition : null;
            if (tr != null)
            {
                if (tr.IsShowing && !tr.IsAnimating)
                {
                    tr.PlayOut();
                }
            }
            else if (m.controller != null)
            {
                m.controller.CloseIfOpen();
            }
        }
    }

    // 인스펙터/코드에서 멤버 제어용 헬퍼
    public void SetMembers(List<Member> newMembers)
    {
        members = newMembers ?? new List<Member>();
        NormalizeMembers();
        // 멤버 변경 시 리스너 재바인딩
        if (isActiveAndEnabled)
        {
            UnbindMemberListeners();
            BindMemberListeners();
        }
    }

    public List<Member> GetMembers()
    {
        return members;
    }

    private void OnValidate()
    {
        NormalizeMembers();
    }

    private void NormalizeMembers()
    {
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null) continue;
            if (m.transitionOverride == null && m.controller != null)
            {
                // 컨트롤러에 연결된 기본 Transition을 자동 수신
                m.transitionOverride = m.controller.Transition;
            }
        }
    }

    private void BindMemberListeners()
    {
        if (listenersBound) return;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || m.controller == null) continue;
            m.controller.OnClicked += OnMemberClickedHandler;
        }
        listenersBound = true;
    }

    private void UnbindMemberListeners()
    {
        if (!listenersBound) return;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m == null || m.controller == null) continue;
            m.controller.OnClicked -= OnMemberClickedHandler;
        }
        listenersBound = false;
    }

    private void OnMemberClickedHandler(MenuToggleController sender)
    {
        NotifyClicked(sender);
    }
}


