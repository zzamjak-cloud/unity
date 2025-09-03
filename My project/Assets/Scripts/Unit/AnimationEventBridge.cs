using UnityEngine;
using System;

/// <summary>
/// 애니메이션 이벤트와 부모 오브젝트의 스크립트를 연결하는 범용 브리지
/// Animator가 자식 오브젝트에 있을 때 사용
/// </summary>
public class AnimationEventBridge : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] targetScripts;  // 연결할 스크립트 배열
    
    void Start()
    {
        // 타겟 스크립트들이 할당되지 않았다면 자동으로 찾기
        if (targetScripts == null || targetScripts.Length == 0)
        {
            // 부모에서 모든 MonoBehaviour 스크립트 찾기
            MonoBehaviour[] parentScripts = GetComponentsInParent<MonoBehaviour>();
            targetScripts = parentScripts;
        }
    }
    
    /// <summary>
    /// 특정 타입의 스크립트를 찾아서 반환합니다.
    /// </summary>
    /// <typeparam name="T">찾을 스크립트 타입</typeparam>
    /// <returns>찾은 스크립트 또는 null</returns>
    public T GetTargetScript<T>() where T : MonoBehaviour
    {
        if (targetScripts != null)
        {
            foreach (var script in targetScripts)
            {
                if (script is T targetScript)
                {
                    return targetScript;
                }
            }
        }
        
        // 부모에서 직접 찾기
        return GetComponentInParent<T>();
    }
    
    /// <summary>
    /// 특정 인터페이스를 구현하는 스크립트를 찾아서 반환합니다.
    /// </summary>
    /// <typeparam name="T">찾을 인터페이스 타입</typeparam>
    /// <returns>찾은 스크립트 또는 null</returns>
    public T GetTargetInterface<T>() where T : class
    {
        if (targetScripts != null)
        {
            foreach (var script in targetScripts)
            {
                if (script is T targetInterface)
                {
                    return targetInterface;
                }
            }
        }
        
        // 부모에서 직접 찾기
        return GetComponentInParent<T>();
    }
    
    /// <summary>
    /// 애니메이션 이벤트에서 호출할 공격 이펙트 함수
    /// </summary>
    public void PlayAttackEffect()
    {
        // 범용적으로 다른 스크립트에서 찾기
        var attackable = GetTargetInterface<IAttackEffect>();
        if (attackable != null)
        {
            attackable.PlayAttackEffect();
        }
        else
        {
            Debug.LogWarning("AnimationEventBridge: 공격 이펙트를 재생할 수 있는 스크립트를 찾을 수 없습니다.");
        }
    }
    
    /// <summary>
    /// 애니메이션 이벤트에서 호출할 이동 이펙트 함수
    /// </summary>
    public void PlayMoveEffect(bool play)
    {
        // 범용적으로 다른 스크립트에서 찾기
        var moveable = GetTargetInterface<IMoveEffect>();
        if (moveable != null)
        {
            moveable.PlayMoveEffect(play);
        }
        else
        {
            Debug.LogWarning("AnimationEventBridge: 이동 이펙트를 재생할 수 있는 스크립트를 찾을 수 없습니다.");
        }
    }
    
    /// <summary>
    /// 애니메이션 이벤트에서 호출할 Blank 이펙트 함수
    /// </summary>
    public void PlayBlankEffect()
    {
        // 범용적으로 다른 스크립트에서 찾기
        var blankable = GetTargetInterface<IBlankEffect>();
        if (blankable != null)
        {
            blankable.PlayBlankEffect();
        }
        else
        {
            Debug.LogWarning("AnimationEventBridge: Blank 이펙트를 재생할 수 있는 스크립트를 찾을 수 없습니다.");
        }
    }
    
    /// <summary>
    /// 범용 함수 호출 - 문자열로 함수명을 받아서 호출
    /// </summary>
    /// <param name="functionName">호출할 함수명</param>
    public void CallFunction(string functionName)
    {
        if (targetScripts != null)
        {
            foreach (var script in targetScripts)
            {
                if (script != null)
                {
                    var method = script.GetType().GetMethod(functionName);
                    if (method != null)
                    {
                        method.Invoke(script, null);
                        return;
                    }
                }
            }
        }
        
        Debug.LogWarning($"AnimationEventBridge: '{functionName}' 함수를 찾을 수 없습니다.");
    }
    
    /// <summary>
    /// 범용 함수 호출 - 매개변수와 함께
    /// </summary>
    /// <param name="functionName">호출할 함수명</param>
    /// <param name="parameter">매개변수</param>
    public void CallFunction(string functionName, object parameter)
    {
        if (targetScripts != null)
        {
            foreach (var script in targetScripts)
            {
                if (script != null)
                {
                    var method = script.GetType().GetMethod(functionName, new Type[] { parameter.GetType() });
                    if (method != null)
                    {
                        method.Invoke(script, new object[] { parameter });
                        return;
                    }
                }
            }
        }
        
        Debug.LogWarning($"AnimationEventBridge: '{functionName}' 함수를 찾을 수 없습니다.");
    }
}

public interface IAttackEffect { void PlayAttackEffect(); } // 공격 이펙트

public interface IMoveEffect { void PlayMoveEffect(bool play); } // 이동 이펙트

public interface IBlankEffect { void PlayBlankEffect(); } // Blank 이펙트
