using UnityEngine;
using TMPro;

public class AnimationStateDisplayer : MonoBehaviour
{
    // 인스펙터에서 할당할 애니메이터 컴포넌트
    [SerializeField]
    private Animator _animator;

    // 인스펙터에서 할당할 TMP_Text 컴포넌트
    [SerializeField]
    private TMP_Text _stateText;

    private int _stateHash;

    private void Update()
    {
        // 애니메이터가 존재하고, TMP_Text가 존재할 경우에만 동작
        if (_animator != null && _stateText != null)
        {
            // 현재 애니메이션 클립의 정보를 가져옴
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            // 현재 재생 중인 스테이트의 해시 값을 가져옴
            _stateHash = stateInfo.fullPathHash;

            // 스테이트 해시 값을 기반으로 스테이트 이름을 가져와 TMP_Text에 표시
            _stateText.text = GetStateNameFromHash(_stateHash);
        }
    }

    /// <summary>
    /// AnimatorStateInfo의 해시 값을 기반으로 스테이트 이름을 반환하는 함수 (수동 매핑)
    /// </summary>
    /// <param name="hash">스테이트 해시 값</param>
    /// <returns>스테이트 이름</returns>
    private string GetStateNameFromHash(int hash)
    {
        // 여기에 애니메이터의 각 스테이트 해시 값과 이름을 매핑하세요.
        // 예를 들어, "Idle" 스테이트의 해시 값이 -123456789라고 가정합니다.
        // 이 부분은 애니메이터 컨트롤러의 실제 스테이트 이름과 해시 값을 확인해야 합니다.
        if (hash == Animator.StringToHash("Base Layer.Idle"))
        {
            return "Idle";
        }
        else if (hash == Animator.StringToHash("Base Layer.Run"))
        {
            return "Run";
        }
        else if (hash == Animator.StringToHash("Base Layer.Attack"))
        {
            return "Attack";
        }
        else if (hash == Animator.StringToHash("Base Layer.Walk"))
        {
            return "Walk";
        }
        else if (hash == Animator.StringToHash("Base Layer.Ceremony"))
        {
            return "Ceremony";
        }
        else if (hash == Animator.StringToHash("Base Layer.Death"))
        {
            return "Death";
        }
        else if (hash == Animator.StringToHash("Base Layer.Blank")) 
        {
            return "Blank";
        }
        // 일치하는 스테이트가 없을 경우
        return "Unknown State";
    }
}