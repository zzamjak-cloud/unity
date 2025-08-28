using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 2D 물리 효과를 적용하여 물 표면이 기울어지는 효과를 구현합니다.
    /// 물병의 기울기에 따라 물 표면 이미지의 너비가 조절됩니다.
    /// </summary>

    [ExecuteAlways]
    public class WaterPhysics2D : MonoBehaviour
    {
        // --- 내부 변수들 ---
        private enum TargetType { None, SpriteRenderer, RectTransform }
        private TargetType targetType = TargetType.None;

        private Transform parentTransform;
        private float waterZRotation;
        private float velocity;

        private RectTransform targetRectTransform;
        private SpriteRenderer targetSpriteRenderer;
        private float initialWidth; // RectTransform.sizeDelta.x
        private float initialXScale; // Transform.localScale.x

        // --- 인스펙터 설정 값들 ---
        [Header("연결할 오브젝트")]
        [Tooltip("너비를 조절할 별도의 물 표면 이미지 오브젝트의 Transform을 연결해주세요. (UI Image 또는 SpriteRenderer)")]
        [SerializeField] private Transform waterSurfaceTransform;

        [Header("물리 효과 설정")]
        [SerializeField] private float springiness = 50f;
        [SerializeField] private float damping = 3f;

        [Header("물 표면 너비 조절")]
        [Tooltip("물병이 최대로 기울어졌을 때 물 표면 이미지의 '원본 너비'에 곱해질 최대 배율입니다.")]
        [SerializeField] private float maxSurfaceWidthMultiplier = 1.2f;

        [Tooltip("너비 조절이 시작될 임계 각도입니다.")]
        [SerializeField] private float angleThreshold = 10f;

#if UNITY_EDITOR
    private double lastFrameTime;
#endif

        void OnEnable()
        {
            Initialize();
#if UNITY_EDITOR
        if (Application.isEditor && !Application.isPlaying) { UnityEditor.EditorApplication.update += Simulate; }
        lastFrameTime = UnityEditor.EditorApplication.timeSinceStartup;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
        if (Application.isEditor && !Application.isPlaying) { UnityEditor.EditorApplication.update -= Simulate; }
#endif
        }

        void Initialize()
        {
            parentTransform = transform.parent;
            if (parentTransform != null)
            {
                // 물리 초기화
                waterZRotation = -parentTransform.eulerAngles.z;
                transform.localEulerAngles = new Vector3(0, 0, waterZRotation);
                velocity = 0f;

                // 연결된 물 표면 오브젝트의 타입을 감지하고 원본 크기를 저장
                DetectAndInitializeTarget();
            }
        }

        // 연결된 오브젝트의 타입을 감지하고 초기 너비/스케일을 저장하는 함수
        void DetectAndInitializeTarget()
        {
            targetType = TargetType.None;
            if (waterSurfaceTransform == null) return;

            // UI Image (RectTransform) 인지 확인
            targetRectTransform = waterSurfaceTransform.GetComponent<RectTransform>();
            if (targetRectTransform != null)
            {
                targetType = TargetType.RectTransform;
                initialWidth = targetRectTransform.sizeDelta.x; // 원본 너비 저장
                return; // 찾았으면 종료
            }

            // SpriteRenderer 인지 확인
            targetSpriteRenderer = waterSurfaceTransform.GetComponent<SpriteRenderer>();
            if (targetSpriteRenderer != null)
            {
                targetType = TargetType.SpriteRenderer;
                initialXScale = waterSurfaceTransform.localScale.x; // 원본 X 스케일 저장
                return;
            }
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            Simulate();
        }

        void Simulate()
        {
            if (parentTransform == null) return;

            // --- 물리 시뮬레이션 로직 (동일) ---
            // (이 부분은 변경되지 않았습니다)
            float deltaTime = Time.deltaTime;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            deltaTime = (float)(UnityEditor.EditorApplication.timeSinceStartup - lastFrameTime);
            lastFrameTime = UnityEditor.EditorApplication.timeSinceStartup;
        }
#endif

            if (deltaTime <= 0f || deltaTime > 0.1f) return;

            float targetZRotation = -parentTransform.eulerAngles.z;
            float displacement = Mathf.DeltaAngle(waterZRotation, targetZRotation);
            float springForce = displacement * springiness;
            float dampingForce = -velocity * damping;
            float acceleration = springForce + dampingForce;
            velocity += acceleration * deltaTime;
            waterZRotation += velocity * deltaTime;
            transform.localEulerAngles = new Vector3(0, 0, waterZRotation);

            // --- 너비 계산 로직 (동일) ---
            float parentWorldZRotation = parentTransform.eulerAngles.z;
            float absRotation = Mathf.Abs(Mathf.DeltaAngle(0, parentWorldZRotation));
            float normalizedAngle = Mathf.Max(0, absRotation - angleThreshold);
            float lerpFactor = Mathf.Clamp01(normalizedAngle / (90f - angleThreshold));

            // --- 감지된 타입에 따라 너비/스케일 적용 ---
            switch (targetType)
            {
                case TargetType.RectTransform:
                    // UI Image의 경우: sizeDelta.x (너비)를 직접 조절
                    float newWidth = Mathf.Lerp(initialWidth, initialWidth * maxSurfaceWidthMultiplier, lerpFactor);
                    targetRectTransform.sizeDelta = new Vector2(newWidth, targetRectTransform.sizeDelta.y);
                    break;

                case TargetType.SpriteRenderer:
                    // SpriteRenderer의 경우: localScale.x (X축 스케일)를 조절
                    float newXScale = Mathf.Lerp(initialXScale, initialXScale * maxSurfaceWidthMultiplier, lerpFactor);
                    waterSurfaceTransform.localScale = new Vector3(newXScale, waterSurfaceTransform.localScale.y, waterSurfaceTransform.localScale.z);
                    break;

                case TargetType.None:
                    // waterSurfaceTransform이 연결되었으나, 지원하는 컴포넌트가 없는 경우
                    if (waterSurfaceTransform != null)
                    {
                        // 타입을 다시 감지 시도
                        DetectAndInitializeTarget();
                    }
                    break;
            }
        }
    }
}