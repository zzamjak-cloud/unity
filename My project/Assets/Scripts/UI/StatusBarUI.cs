using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 캐릭터의 상태바(HP, MP, SP)를 표시하는 UI 컴포넌트
/// 캐릭터 방향에 영향받지 않고 항상 정상 방향으로 표시됩니다.
/// </summary>
public class StatusBarUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider healthSlider;  // HP 슬라이더
    [SerializeField] private Slider manaSlider;    // MP 슬라이더
    [SerializeField] private Slider staminaSlider; // SP 슬라이더
    
    [Header("Status Bar Settings")]
    [SerializeField] private bool enableHealthBar = true;   // HP 바 활성화
    [SerializeField] private bool enableManaBar = false;    // MP 바 활성화
    [SerializeField] private bool enableStaminaBar = false; // SP 바 활성화
    
    [Header("HP Visual Settings")]
    [SerializeField] private Color healthyColor = Color.green;  // 건강한 상태 색상
    [SerializeField] private Color warningColor = Color.yellow; // 경고 상태 색상
    [SerializeField] private Color dangerColor = Color.red;     // 위험 상태 색상
    [SerializeField] private float warningThreshold = 0.6f;     // 경고 임계값 (60%)
    [SerializeField] private float dangerThreshold = 0.3f;      // 위험 임계값 (30%)
    
    [Header("MP/SP Visual Settings")]
    [SerializeField] private Color manaColor = Color.blue;      // MP 색상
    [SerializeField] private Color staminaColor = Color.yellow; // SP 색상
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableSmoothTransition = true;  // 부드러운 전환 활성화
    [SerializeField] private float transitionSpeed = 5f;          // 전환 속도
    [SerializeField] private bool enableColorTransition = true;   // 색상 전환 활성화 (HP만)
    
    [Header("Position Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);  // 캐릭터 기준 오프셋
    [SerializeField] private bool followCharacter = true;                // 캐릭터 따라가기
    [SerializeField] private bool alwaysFaceCamera = true;               // 항상 카메라 향하기
    
    // 내부 변수들
    private CharacterBase targetCharacter;
    private Camera mainCamera;
    
    // 각 상태바의 목표 값들
    private float targetHealthValue = 1f;
    private float targetManaValue = 1f;
    private float targetStaminaValue = 1f;
    
    // HP 색상 전환용
    private Color targetHealthColor;
    
    // 캐릭터 방향에 영향받지 않기 위한 변수들
    private Vector3 originalLocalScale;
    private bool isInitialized = false;
    
    // 메모리 할당 최적화를 위한 캐시된 변수들
    private Vector3 cachedPosition;
    private Vector3 cachedOffset;
    private Quaternion cachedRotation;
    private Vector3 cachedScale;
    private float cachedHealthPercentage;
    private Color cachedColor;
    
    private void Awake()
    {
        // 컴포넌트 자동 찾기
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }
        
        if (manaSlider == null)
        {
            manaSlider = transform.Find("ManaSlider")?.GetComponent<Slider>();
        }
        
        if (staminaSlider == null)
        {
            staminaSlider = transform.Find("StaminaSlider")?.GetComponent<Slider>();
        }
        
        // 원본 스케일 저장
        originalLocalScale = transform.localScale;
        
        // 메인 카메라 찾기
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        // 캐시된 변수들 초기화
        cachedPosition = Vector3.zero;
        cachedOffset = offset;
        cachedRotation = Quaternion.identity;
        cachedScale = Vector3.one;
        cachedHealthPercentage = 1f;
        cachedColor = healthyColor;
    }
    
    private void Start()
    {
        // 타겟 캐릭터 찾기
        if (targetCharacter == null)
        {
            targetCharacter = transform.parent?.GetComponent<CharacterBase>();
        }
        
        if (targetCharacter != null)
        {
            // 체력 변경 이벤트 등록
            targetCharacter.AddHealthChangedListener(OnHealthChanged);
            
            // 모든 슬라이더를 1.0으로 확실히 설정
            if (healthSlider != null) healthSlider.value = 1f;
            if (manaSlider != null) manaSlider.value = 1f;
            if (staminaSlider != null) staminaSlider.value = 1f;
            
            isInitialized = true;
        }
        else
        {
            Debug.LogWarning($"[StatusBarUI] {gameObject.name}: CharacterBase 컴포넌트를 찾을 수 없습니다.");
        }
    }
    
    private void Update()
    {
        if (!isInitialized || targetCharacter == null) return;
        
        // 캐릭터 따라가기
        if (followCharacter)
        {
            UpdatePosition();
        }
        
        // 항상 카메라 향하기
        if (alwaysFaceCamera && mainCamera != null)
        {
            UpdateRotation();
        }
        
        // 캐릭터 방향에 영향받지 않도록 스케일 보정
        UpdateScale();
        
        // 부드러운 전환
        if (enableSmoothTransition)
        {
            UpdateSmoothTransition();
        }
    }
    
    /// <summary>
    /// 체력 변경 이벤트 처리
    /// </summary>
    /// <param name="currentHealth">현재 체력</param>
    /// <param name="maxHealth">최대 체력</param>
    private void OnHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthDisplay(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// HP 표시 업데이트
    /// </summary>
    /// <param name="currentHealth">현재 체력</param>
    /// <param name="maxHealth">최대 체력</param>
    public void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (!enableHealthBar || maxHealth <= 0) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        cachedHealthPercentage = (float)currentHealth / maxHealth;
        targetHealthValue = cachedHealthPercentage;
        
        // HP 슬라이더 업데이트
        if (healthSlider != null)
        {
            if (!enableSmoothTransition)
            {
                healthSlider.value = targetHealthValue;
            }
        }
        
        // HP 색상 업데이트 (HP만 색상 변환)
        if (enableColorTransition)
        {
            UpdateHealthColor(cachedHealthPercentage);
        }
    }
    
    /// <summary>
    /// MP 표시 업데이트
    /// </summary>
    /// <param name="currentMana">현재 마나</param>
    /// <param name="maxMana">최대 마나</param>
    public void UpdateManaDisplay(int currentMana, int maxMana)
    {
        if (!enableManaBar || maxMana <= 0) return;
        
        float manaPercentage = (float)currentMana / maxMana;
        targetManaValue = manaPercentage;
        
        if (manaSlider != null)
        {
            if (!enableSmoothTransition)
            {
                manaSlider.value = targetManaValue;
            }
        }
    }
    
    /// <summary>
    /// SP 표시 업데이트
    /// </summary>
    /// <param name="currentStamina">현재 스태미나</param>
    /// <param name="maxStamina">최대 스태미나</param>
    public void UpdateStaminaDisplay(int currentStamina, int maxStamina)
    {
        if (!enableStaminaBar || maxStamina <= 0) return;
        
        float staminaPercentage = (float)currentStamina / maxStamina;
        targetStaminaValue = staminaPercentage;
        
        if (staminaSlider != null)
        {
            if (!enableSmoothTransition)
            {
                staminaSlider.value = targetStaminaValue;
            }
        }
    }
    
    /// <summary>
    /// HP에 따른 색상 업데이트 (HP만)
    /// </summary>
    /// <param name="healthPercentage">체력 비율 (0-1)</param>
    private void UpdateHealthColor(float healthPercentage)
    {
        if (healthSlider == null) return;
        
        // HP 슬라이더의 Fill 이미지 찾기
        Image fillImage = healthSlider.fillRect?.GetComponent<Image>();
        if (fillImage == null) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        if (healthPercentage <= dangerThreshold)
        {
            cachedColor = dangerColor;
        }
        else if (healthPercentage <= warningThreshold)
        {
            cachedColor = warningColor;
        }
        else
        {
            cachedColor = healthyColor;
        }
        
        targetHealthColor = cachedColor;
        
        if (!enableSmoothTransition)
        {
            fillImage.color = targetHealthColor;
        }
    }
    
    /// <summary>
    /// 부드러운 전환 업데이트
    /// </summary>
    private void UpdateSmoothTransition()
    {
        // HP 슬라이더 부드러운 전환
        if (healthSlider != null && enableHealthBar)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealthValue, transitionSpeed * Time.deltaTime);
        }
        
        // MP 슬라이더 부드러운 전환
        if (manaSlider != null && enableManaBar)
        {
            manaSlider.value = Mathf.Lerp(manaSlider.value, targetManaValue, transitionSpeed * Time.deltaTime);
        }
        
        // SP 슬라이더 부드러운 전환
        if (staminaSlider != null && enableStaminaBar)
        {
            staminaSlider.value = Mathf.Lerp(staminaSlider.value, targetStaminaValue, transitionSpeed * Time.deltaTime);
        }
        
        // HP 색상 부드러운 전환
        if (enableColorTransition && healthSlider != null)
        {
            Image fillImage = healthSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(fillImage.color, targetHealthColor, transitionSpeed * Time.deltaTime);
            }
        }
    }
    
    /// <summary>
    /// 위치 업데이트 (캐릭터 따라가기)
    /// </summary>
    private void UpdatePosition()
    {
        if (targetCharacter != null)
        {
            // 캐시된 변수 사용으로 메모리 할당 최적화
            cachedPosition.x = targetCharacter.transform.position.x + cachedOffset.x;
            cachedPosition.y = targetCharacter.transform.position.y + cachedOffset.y;
            cachedPosition.z = targetCharacter.transform.position.z + cachedOffset.z;
            transform.position = cachedPosition;
        }
    }
    
    /// <summary>
    /// 회전 업데이트 (항상 카메라 향하기)
    /// </summary>
    private void UpdateRotation()
    {
        if (mainCamera != null)
        {
            // 캐시된 변수 사용으로 메모리 할당 최적화
            cachedRotation = mainCamera.transform.rotation;
            cachedRotation.x = 0f; // X축 회전 제거
            cachedRotation.z = 0f; // Z축 회전 제거
            transform.rotation = cachedRotation;
        }
    }
    
    /// <summary>
    /// 스케일 업데이트 (캐릭터 방향에 영향받지 않도록)
    /// </summary>
    private void UpdateScale()
    {
        if (targetCharacter != null)
        {
            // 캐시된 변수 사용으로 메모리 할당 최적화
            cachedScale = originalLocalScale;
            
            // X축이 음수인 경우 X축만 반전
            if (targetCharacter.transform.localScale.x < 0)
            {
                cachedScale.x = -originalLocalScale.x;
            }
            
            transform.localScale = cachedScale;
        }
    }
    
    /// <summary>
    /// 상태바 표시/숨기기
    /// </summary>
    /// <param name="show">표시 여부</param>
    public void SetVisible(bool show)
    {
        gameObject.SetActive(show);
    }
    
    /// <summary>
    /// 상태바 설정 변경
    /// </summary>
    /// <param name="offset">오프셋</param>
    /// <param name="follow">캐릭터 따라가기</param>
    /// <param name="faceCamera">카메라 향하기</param>
    public void SetSettings(Vector3 offset, bool follow = true, bool faceCamera = true)
    {
        this.offset = offset;
        this.cachedOffset = offset; // 캐시된 오프셋도 업데이트
        this.followCharacter = follow;
        this.alwaysFaceCamera = faceCamera;
    }
    
    /// <summary>
    /// HP 색상 설정 변경
    /// </summary>
    /// <param name="healthy">건강한 상태 색상</param>
    /// <param name="warning">경고 상태 색상</param>
    /// <param name="danger">위험 상태 색상</param>
    public void SetHealthColors(Color healthy, Color warning, Color danger)
    {
        healthyColor = healthy;
        warningColor = warning;
        dangerColor = danger;
    }
    
    /// <summary>
    /// MP/SP 색상 설정 변경
    /// </summary>
    /// <param name="mana">MP 색상</param>
    /// <param name="stamina">SP 색상</param>
    public void SetStatusColors(Color mana, Color stamina)
    {
        manaColor = mana;
        staminaColor = stamina;
        
        // MP 슬라이더 색상 적용
        if (manaSlider != null)
        {
            Image fillImage = manaSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = manaColor;
            }
        }
        
        // SP 슬라이더 색상 적용
        if (staminaSlider != null)
        {
            Image fillImage = staminaSlider.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = staminaColor;
            }
        }
    }
    
    /// <summary>
    /// 상태바 활성화 설정
    /// </summary>
    /// <param name="health">HP 바 활성화</param>
    /// <param name="mana">MP 바 활성화</param>
    /// <param name="stamina">SP 바 활성화</param>
    public void SetStatusBarsEnabled(bool health, bool mana, bool stamina)
    {
        enableHealthBar = health;
        enableManaBar = mana;
        enableStaminaBar = stamina;
        
        // 슬라이더 활성화/비활성화
        if (healthSlider != null) healthSlider.gameObject.SetActive(health);
        if (manaSlider != null) manaSlider.gameObject.SetActive(mana);
        if (staminaSlider != null) staminaSlider.gameObject.SetActive(stamina);
    }
    
    /// <summary>
    /// 특정 슬라이더 값 설정
    /// </summary>
    /// <param name="statusType">상태 타입 (0: HP, 1: MP, 2: SP)</param>
    /// <param name="value">값 (0-1)</param>
    public void SetSliderValue(int statusType, float value)
    {
        switch (statusType)
        {
            case 0: // HP
                if (healthSlider != null) healthSlider.value = value;
                break;
            case 1: // MP
                if (manaSlider != null) manaSlider.value = value;
                break;
            case 2: // SP
                if (staminaSlider != null) staminaSlider.value = value;
                break;
        }
    }
    
    /// <summary>
    /// 특정 슬라이더 가져오기
    /// </summary>
    /// <param name="statusType">상태 타입 (0: HP, 1: MP, 2: SP)</param>
    /// <returns>슬라이더 컴포넌트</returns>
    public Slider GetSlider(int statusType)
    {
        switch (statusType)
        {
            case 0: return healthSlider;   // HP
            case 1: return manaSlider;     // MP
            case 2: return staminaSlider;  // SP
            default: return null;
        }
    }
    
    /// <summary>
    /// 활성화된 상태바 개수 반환
    /// </summary>
    /// <returns>활성화된 상태바 개수</returns>
    public int GetActiveStatusBarCount()
    {
        int count = 0;
        if (enableHealthBar) count++;
        if (enableManaBar) count++;
        if (enableStaminaBar) count++;
        return count;
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 제거
        if (targetCharacter != null)
        {
            targetCharacter.RemoveHealthChangedListener(OnHealthChanged);
        }
        
        // 메모리 정리
        targetCharacter = null;
        mainCamera = null;
        healthSlider = null;
        manaSlider = null;
        staminaSlider = null;
    }
}
