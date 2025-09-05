using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터의 체력을 표시하는 프로그래스바 UI 컴포넌트
/// 캐릭터 방향에 영향받지 않고 항상 정상 방향으로 표시됩니다.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider healthSlider;  // 체력 슬라이더 (기본)
    [SerializeField] private Image fillImage;  // 채우기 이미지
    [SerializeField] private Image backgroundImage;  // 배경 이미지
    
    [Header("Multiple Sliders Support")]
    [SerializeField] private Slider[] additionalSliders;  // 추가 슬라이더들
    [SerializeField] private bool autoDetectSliders = true;  // 자동 슬라이더 감지
    [SerializeField] private string[] sliderNames = { "Health", "Mana", "Stamina" };  // 슬라이더 이름들
    
    [Header("Visual Settings")]
    [SerializeField] private Color healthyColor = Color.green;  // 건강한 상태 색상
    [SerializeField] private Color warningColor = Color.yellow;  // 경고 상태 색상
    [SerializeField] private Color dangerColor = Color.red;  // 위험 상태 색상
    [SerializeField] private float warningThreshold = 0.6f;  // 경고 임계값 (60%)
    [SerializeField] private float dangerThreshold = 0.3f;  // 위험 임계값 (30%)
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableSmoothTransition = true;  // 부드러운 전환 활성화
    [SerializeField] private float transitionSpeed = 5f;  // 전환 속도
    [SerializeField] private bool enableColorTransition = true;  // 색상 전환 활성화
    
    [Header("Position Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0);  // 캐릭터 기준 오프셋
    [SerializeField] private bool followCharacter = true;  // 캐릭터 따라가기
    [SerializeField] private bool alwaysFaceCamera = true;  // 항상 카메라 향하기
    
    // 내부 변수들
    private CharacterBase targetCharacter;
    private Camera mainCamera;
    private float targetHealthValue = 1f;
    private Color targetColor;
    
    // 캐릭터 방향에 영향받지 않기 위한 변수들
    private Vector3 originalLocalScale;
    private bool isInitialized = false;
    
    // 다중 슬라이더 관련 변수들
    private Slider[] allSliders;  // 모든 슬라이더 배열
    private float[] targetValues;  // 각 슬라이더의 목표 값들
    private Color[] targetColors;  // 각 슬라이더의 목표 색상들
    
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
        
        if (fillImage == null && healthSlider != null)
        {
            fillImage = healthSlider.fillRect?.GetComponent<Image>();
        }
        
        if (backgroundImage == null && healthSlider != null)
        {
            backgroundImage = healthSlider.targetGraphic as Image;
        }
        
        // 다중 슬라이더 초기화
        InitializeMultipleSliders();
        
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
    
    /// <summary>
    /// 다중 슬라이더 초기화
    /// </summary>
    private void InitializeMultipleSliders()
    {
        if (autoDetectSliders)
        {
            // 자동으로 모든 슬라이더 찾기
            Slider[] foundSliders = GetComponentsInChildren<Slider>();
            
            if (foundSliders.Length > 0)
            {
                allSliders = foundSliders;
                targetValues = new float[allSliders.Length];
                targetColors = new Color[allSliders.Length];
                
                // 모든 슬라이더를 1.0으로 초기화
                for (int i = 0; i < allSliders.Length; i++)
                {
                    targetValues[i] = 1f;
                    targetColors[i] = healthyColor;
                    // 슬라이더 값도 즉시 1.0으로 설정
                    if (allSliders[i] != null)
                    {
                        allSliders[i].value = 1f;
                    }
                }
                
                // 첫 번째 슬라이더를 기본 체력 슬라이더로 설정
                if (healthSlider == null && allSliders.Length > 0)
                {
                    healthSlider = allSliders[0];
                }
            }
        }
        else
        {
            // 수동으로 설정된 슬라이더들 사용
            if (additionalSliders != null && additionalSliders.Length > 0)
            {
                // 기본 슬라이더와 추가 슬라이더들 합치기
                allSliders = new Slider[1 + additionalSliders.Length];
                allSliders[0] = healthSlider;
                
                for (int i = 0; i < additionalSliders.Length; i++)
                {
                    allSliders[i + 1] = additionalSliders[i];
                }
                
                targetValues = new float[allSliders.Length];
                targetColors = new Color[allSliders.Length];
                
                // 모든 슬라이더를 1.0으로 초기화
                for (int i = 0; i < allSliders.Length; i++)
                {
                    targetValues[i] = 1f;
                    targetColors[i] = healthyColor;
                    // 슬라이더 값도 즉시 1.0으로 설정
                    if (allSliders[i] != null)
                    {
                        allSliders[i].value = 1f;
                    }
                }
            }
            else
            {
                // 기본 슬라이더만 사용
                allSliders = new Slider[] { healthSlider };
                targetValues = new float[] { 1f };
                targetColors = new Color[] { healthyColor };
                
                // 기본 슬라이더도 1.0으로 초기화
                if (healthSlider != null)
                {
                    healthSlider.value = 1f;
                }
            }
        }
    }
    
    private void Start()
    {
        // 부모에서 CharacterBase 찾기
        targetCharacter = GetComponentInParent<CharacterBase>();
        if (targetCharacter == null)
        {
            targetCharacter = transform.parent?.GetComponent<CharacterBase>();
        }
        
        if (targetCharacter != null)
        {
            // 체력 변경 이벤트 등록
            targetCharacter.AddHealthChangedListener(OnHealthChanged);
            
            // 모든 슬라이더를 1.0으로 확실히 설정
            if (allSliders != null)
            {
                for (int i = 0; i < allSliders.Length; i++)
                {
                    if (allSliders[i] != null)
                    {
                        allSliders[i].value = 1f;
                    }
                }
            }
            
            isInitialized = true;
        }
        else
        {
            Debug.LogWarning($"[HealthBarUI] {gameObject.name}: CharacterBase 컴포넌트를 찾을 수 없습니다.");
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
        
        // 부드러운 체력 전환
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
    /// 체력 표시 업데이트
    /// </summary>
    /// <param name="currentHealth">현재 체력</param>
    /// <param name="maxHealth">최대 체력</param>
    public void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        cachedHealthPercentage = (float)currentHealth / maxHealth;
        targetHealthValue = cachedHealthPercentage;
        
        // 첫 번째 슬라이더 (체력) 업데이트
        if (allSliders != null && allSliders.Length > 0 && allSliders[0] != null)
        {
            targetValues[0] = cachedHealthPercentage;
            
            // 즉시 업데이트 또는 부드러운 전환
            if (!enableSmoothTransition)
            {
                allSliders[0].value = targetValues[0];
            }
        }
        
        // 기본 체력 슬라이더도 업데이트 (하위 호환성)
        if (healthSlider != null)
        {
            if (!enableSmoothTransition)
            {
                healthSlider.value = targetHealthValue;
            }
        }
        
        // 색상 업데이트
        if (enableColorTransition)
        {
            UpdateHealthColor(cachedHealthPercentage);
        }
    }
    
    /// <summary>
    /// 체력에 따른 색상 업데이트
    /// </summary>
    /// <param name="healthPercentage">체력 비율 (0-1)</param>
    private void UpdateHealthColor(float healthPercentage)
    {
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
        
        targetColor = cachedColor;
        
        if (!enableSmoothTransition)
        {
            fillImage.color = targetColor;
        }
    }
    
    /// <summary>
    /// 부드러운 전환 업데이트
    /// </summary>
    private void UpdateSmoothTransition()
    {
        // 모든 슬라이더 업데이트
        if (allSliders != null)
        {
            for (int i = 0; i < allSliders.Length; i++)
            {
                if (allSliders[i] != null)
                {
                    allSliders[i].value = Mathf.Lerp(allSliders[i].value, targetValues[i], transitionSpeed * Time.deltaTime);
                }
            }
        }
        
        // 기본 체력 슬라이더도 업데이트 (하위 호환성)
        if (healthSlider != null)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealthValue, transitionSpeed * Time.deltaTime);
        }
        
        // 색상 전환
        if (enableColorTransition && fillImage != null)
        {
            fillImage.color = Color.Lerp(fillImage.color, targetColor, transitionSpeed * Time.deltaTime);
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
    /// 체력바 표시/숨기기
    /// </summary>
    /// <param name="show">표시 여부</param>
    public void SetVisible(bool show)
    {
        gameObject.SetActive(show);
    }
    
    /// <summary>
    /// 체력바 설정 변경
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
    /// 색상 설정 변경
    /// </summary>
    /// <param name="healthy">건강한 상태 색상</param>
    /// <param name="warning">경고 상태 색상</param>
    /// <param name="danger">위험 상태 색상</param>
    public void SetColors(Color healthy, Color warning, Color danger)
    {
        healthyColor = healthy;
        warningColor = warning;
        dangerColor = danger;
    }
    
    /// <summary>
    /// 특정 슬라이더의 값을 설정합니다.
    /// </summary>
    /// <param name="sliderIndex">슬라이더 인덱스 (0부터 시작)</param>
    /// <param name="value">설정할 값 (0-1)</param>
    public void SetSliderValue(int sliderIndex, float value)
    {
        if (allSliders != null && sliderIndex >= 0 && sliderIndex < allSliders.Length)
        {
            targetValues[sliderIndex] = Mathf.Clamp01(value);
            
            if (!enableSmoothTransition)
            {
                allSliders[sliderIndex].value = targetValues[sliderIndex];
            }
        }
    }
    
    /// <summary>
    /// 특정 슬라이더의 색상을 설정합니다.
    /// </summary>
    /// <param name="sliderIndex">슬라이더 인덱스 (0부터 시작)</param>
    /// <param name="color">설정할 색상</param>
    public void SetSliderColor(int sliderIndex, Color color)
    {
        if (allSliders != null && sliderIndex >= 0 && sliderIndex < allSliders.Length)
        {
            targetColors[sliderIndex] = color;
            
            if (allSliders[sliderIndex].fillRect != null)
            {
                Image fillImage = allSliders[sliderIndex].fillRect.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = color;
                }
            }
        }
    }
    
    /// <summary>
    /// 슬라이더 개수를 반환합니다.
    /// </summary>
    /// <returns>슬라이더 개수</returns>
    public int GetSliderCount()
    {
        return allSliders != null ? allSliders.Length : 0;
    }
    
    /// <summary>
    /// 특정 슬라이더를 반환합니다.
    /// </summary>
    /// <param name="index">슬라이더 인덱스</param>
    /// <returns>슬라이더 컴포넌트</returns>
    public Slider GetSlider(int index)
    {
        if (allSliders != null && index >= 0 && index < allSliders.Length)
        {
            return allSliders[index];
        }
        return null;
    }
    
    /// <summary>
    /// 슬라이더를 다시 초기화합니다.
    /// </summary>
    public void RefreshSliders()
    {
        InitializeMultipleSliders();
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
        fillImage = null;
        backgroundImage = null;
        allSliders = null;
        targetValues = null;
        targetColors = null;
        additionalSliders = null;
    }
}
