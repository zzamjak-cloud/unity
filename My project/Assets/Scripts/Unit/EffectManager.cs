using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 캐릭터의 이펙트를 관리하는 클래스
/// </summary>
public class EffectManager : MonoBehaviour
{
    [System.Serializable]
    public struct EffectData
    {
        public Transform effectContainer;
        public GameObject effectPrefab;
    }

    [Header("Effect Data")]
    [SerializeField] private EffectData moveEffectData;
    [SerializeField] private EffectData attackEffectData;
    [SerializeField] private EffectData blankEffectData;
    [SerializeField] private EffectData damageEffectData;

    // 이펙트 풀
    private Queue<ParticleSystem> attackEffectPool = new Queue<ParticleSystem>();
    private Queue<ParticleSystem> blankEffectPool = new Queue<ParticleSystem>();
    private Queue<ParticleSystem> damageEffectPool = new Queue<ParticleSystem>();

    // 활성화된 이펙트들
    private ParticleSystem activeMoveEffect;
    private List<ParticleSystem> activeAttackEffects = new List<ParticleSystem>();
    private List<ParticleSystem> activeBlankEffects = new List<ParticleSystem>();
    private List<ParticleSystem> activeDamageEffects = new List<ParticleSystem>();

    // 피격 이펙트 중복 방지
    private bool isPlayingDamageEffect = false;

    private void Awake()
    {
        InitializeEffectPools();
    }

    /// <summary>
    /// 이펙트 풀을 초기화합니다.
    /// </summary>
    private void InitializeEffectPools()
    {
        for (int i = 0; i < GameConstants.EFFECT_POOL_SIZE; i++)
        {
            CreateEffectForPool(attackEffectData, attackEffectPool);
            CreateEffectForPool(blankEffectData, blankEffectPool);
            CreateEffectForPool(damageEffectData, damageEffectPool);
        }
    }

    /// <summary>
    /// 이펙트 풀에 사용할 이펙트를 생성합니다.
    /// </summary>
    private void CreateEffectForPool(EffectData effectData, Queue<ParticleSystem> pool)
    {
        if (effectData.effectPrefab == null || effectData.effectContainer == null) return;

        GameObject effectInstance = Instantiate(effectData.effectPrefab, effectData.effectContainer);
        effectInstance.transform.localPosition = Vector3.zero;
        effectInstance.transform.localRotation = Quaternion.identity;

        ParticleSystem effectPS = effectInstance.GetComponent<ParticleSystem>();
        if (effectPS != null)
        {
            effectInstance.SetActive(false);
            pool.Enqueue(effectPS);
        }
    }

    /// <summary>
    /// 이동 이펙트를 재생합니다.
    /// </summary>
    public void PlayMoveEffect(bool play)
    {
        if (moveEffectData.effectPrefab == null || moveEffectData.effectContainer == null) return;

        if (play)
        {
            if (activeMoveEffect == null)
            {
                GameObject effectInstance = Instantiate(moveEffectData.effectPrefab, moveEffectData.effectContainer);
                effectInstance.transform.localPosition = Vector3.zero;
                effectInstance.transform.localRotation = Quaternion.identity;

                activeMoveEffect = effectInstance.GetComponent<ParticleSystem>();
                if (activeMoveEffect != null)
                {
                    activeMoveEffect.Play();
                }
            }
            else if (!activeMoveEffect.isPlaying)
            {
                activeMoveEffect.Play();
            }
        }
        else
        {
            if (activeMoveEffect != null && activeMoveEffect.isPlaying)
            {
                activeMoveEffect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    /// <summary>
    /// 공격 이펙트를 재생합니다.
    /// </summary>
    public void PlayAttackEffect()
    {
        if (attackEffectData.effectPrefab == null)
        {
            // 공격 이펙트가 설정되지 않은 경우는 정상적인 상황이므로 로그를 출력하지 않음
            return;
        }
        
        if (attackEffectData.effectContainer == null)
        {
            Debug.LogWarning($"[EffectManager] 공격 이펙트 컨테이너가 설정되지 않았습니다. - {gameObject.name}");
            return;
        }
        
        PlayEffect(attackEffectData, activeAttackEffects, attackEffectPool, true);
    }

    /// <summary>
    /// Blank 이펙트를 재생합니다.
    /// </summary>
    public void PlayBlankEffect()
    {
        if (blankEffectData.effectPrefab == null || blankEffectData.effectContainer == null)
        {
            Debug.Log($"[EffectManager] Blank 이펙트 데이터가 설정되지 않았습니다. 이펙트를 재생하지 않습니다. - {gameObject.name}");
            return;
        }
        
        PlayEffect(blankEffectData, activeBlankEffects, blankEffectPool, true);
    }

    /// <summary>
    /// 피격 이펙트를 재생합니다.
    /// </summary>
    public void PlayDamageEffect()
    {
        if (isPlayingDamageEffect)
        {
            Debug.LogWarning($"{gameObject.name}: 이미 피격 이펙트를 재생 중입니다.");
            return;
        }

        if (activeDamageEffects.Count >= GameConstants.MAX_DAMAGE_EFFECTS)
        {
            Debug.LogWarning($"{gameObject.name}: 최대 피격 이펙트 개수에 도달했습니다.");
            return;
        }

        if (damageEffectData.effectPrefab == null || damageEffectData.effectContainer == null)
        {
            Debug.LogWarning($"{gameObject.name}: 피격 이펙트 데이터가 설정되지 않았습니다.");
            return;
        }

        isPlayingDamageEffect = true;
        PlayEffect(damageEffectData, activeDamageEffects, damageEffectPool, true);
        StartCoroutine(ResetDamageEffectFlag());
    }

    /// <summary>
    /// 이펙트를 재생합니다.
    /// </summary>
    private void PlayEffect(EffectData effectData, List<ParticleSystem> activeEffectsList, Queue<ParticleSystem> pool, bool play)
    {
        if (effectData.effectPrefab == null || effectData.effectContainer == null) 
        {
            return;
        }

        if (play)
        {
            ParticleSystem effectPS = GetEffectFromPool(pool, effectData);
            if (effectPS != null)
            {
                activeEffectsList.Add(effectPS);
                effectPS.Play();
                StartCoroutine(WaitForEffectToCompleteAndReturnToPool(effectPS, activeEffectsList, pool));
            }
            else
            {
                Debug.LogWarning($"[EffectManager] 이펙트를 풀에서 가져올 수 없습니다! - {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// 풀에서 이펙트를 가져옵니다.
    /// </summary>
    private ParticleSystem GetEffectFromPool(Queue<ParticleSystem> pool, EffectData effectData)
    {
        if (pool.Count > 0)
        {
            ParticleSystem effect = pool.Dequeue();
            effect.gameObject.SetActive(true);
            return effect;
        }

        if (effectData.effectPrefab != null && effectData.effectContainer != null)
        {
            GameObject effectInstance = Instantiate(effectData.effectPrefab, effectData.effectContainer);
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;

            ParticleSystem effectPS = effectInstance.GetComponent<ParticleSystem>();
            if (effectPS != null)
            {
                return effectPS;
            }
        }

        return null;
    }

    /// <summary>
    /// 이펙트를 풀로 반환합니다.
    /// </summary>
    private void ReturnEffectToPool(ParticleSystem effect, Queue<ParticleSystem> pool)
    {
        if (effect == null) return;

        effect.Stop();
        effect.Clear();
        effect.gameObject.SetActive(false);
        pool.Enqueue(effect);
    }

    /// <summary>
    /// 이펙트가 완료될 때까지 기다린 후 풀로 반환합니다.
    /// </summary>
    private IEnumerator WaitForEffectToCompleteAndReturnToPool(ParticleSystem effect, List<ParticleSystem> activeEffectsList, Queue<ParticleSystem> pool)
    {
        if (effect == null) yield break;

        while (effect.isPlaying)
        {
            yield return null;
        }

        if (activeEffectsList.Contains(effect))
        {
            activeEffectsList.Remove(effect);
        }

        ReturnEffectToPool(effect, pool);
    }

    /// <summary>
    /// 피격 이펙트 플래그를 리셋합니다.
    /// </summary>
    private IEnumerator ResetDamageEffectFlag()
    {
        yield return new WaitForSeconds(GameConstants.DAMAGE_EFFECT_COOLDOWN);
        isPlayingDamageEffect = false;
    }

    /// <summary>
    /// 이펙트 데이터를 설정합니다.
    /// </summary>
    public void SetEffectData(EffectData moveData, EffectData attackData, EffectData blankData, EffectData damageData)
    {
        moveEffectData = moveData;
        attackEffectData = attackData;
        blankEffectData = blankData;
        damageEffectData = damageData;
    }
}
