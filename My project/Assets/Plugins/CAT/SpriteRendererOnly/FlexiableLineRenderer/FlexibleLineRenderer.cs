using System;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Effects
{
    #if UNITY_EDITOR
    [ExecuteInEditMode]
    #endif
    public class FlexibleLineRenderer : MonoBehaviour
    {
        #region 설정 필드
        [Header("필수 컴포넌트")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform endAnchor;
        
        [Header("라인 설정")]
        [SerializeField] private int segmentCount = 20;
        [SerializeField] private float segmentLength = 0.2f;
        [SerializeField] private bool autoCalculateSegmentLength = true;
        
        [Header("시뮬레이션 설정")]
        [SerializeField] private bool _simulateInEditor = true;
        [SerializeField] private int constraintIterations = 10;
        [SerializeField] private float gravityResistance = 1.5f;
        [SerializeField] private float damping = 0.95f;
        [SerializeField] private float stiffness = 0.3f;
        [SerializeField] private bool useGravity = true;
        [SerializeField] private Vector3 customGravity = Physics.gravity;
        
        [Header("선택적 설정")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool scaleLineWidth = false;
        
        [Header("성능 최적화")]
        [SerializeField] private bool enablePerformanceOptimization = true;
        [SerializeField] [Range(1, 60)] private int updateRate = 60;
        [SerializeField] private bool skipUpdateWhenInvisible = true;
        
        [Header("에디터 표시")]
        [SerializeField] private bool showInEditor = true;
        [SerializeField] private bool updateInEditor = true;
        [SerializeField] private Color editorLineColor = Color.cyan;
        [SerializeField] private float editorLineWidth = 0.05f;
        [SerializeField] private bool showEditorGizmos = true;
        
        [Header("디버그")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private Color debugColor = Color.red;
        [SerializeField] private float debugSphereSize = 0.1f;
        #endregion
        
        #region 프라이빗 변수
        private Vector3[] positions;
        private Vector3[] previousPositions;
        private float originalStartWidth;
        private float originalEndWidth;
        private bool isInitialized;
        private float calculatedSegmentLength;
        
        // 성능 최적화를 위한 캐시 변수
        private Vector3 cachedStartPosition;
        private Vector3 cachedEndPosition;
        private Vector3 cachedGravity;
        private float updateTimer;
        private float updateInterval;
        private bool wasVisible;
        
        #if UNITY_EDITOR
        private bool editorInitialized;
        private float lastEditorUpdateTime;
        private Vector3 lastCachedStartPos;
        private Vector3 lastCachedEndPos;
        private float lastRepaintTime;
        private const float REPAINT_INTERVAL = 0.033f; // 약 30fps로 제한
        private int lastSegmentCount; // 세그먼트 카운트 변경 감지용
        #endif
        #endregion
        
        #region Unity 생명주기
        private void Awake()
        {
            // LineRenderer 자동 할당
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
                    Debug.LogWarning("LineRenderer가 없어서 자동으로 추가했습니다.");
                }
            }
            
            // 기본 LineRenderer 설정
            SetupLineRenderer();
            
            // 배열 초기화
            positions = new Vector3[segmentCount];
            previousPositions = new Vector3[segmentCount];
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugMode)
                Debug.Log($"FlexibleLineRenderer Awake 완료. SegmentCount: {segmentCount}");
            #endif
        }
        
        private void Start()
        {
            if (autoInitialize && ValidateComponents())
            {
                Initialize();
            }
            
            // 성능 최적화 설정
            if (enablePerformanceOptimization)
            {
                updateInterval = 1f / updateRate;
                updateTimer = 0f;
            }
            
            // 초기 캐시 설정
            CacheAnchorPositions();
            CacheGravity();
            
            #if UNITY_EDITOR
            // 에디터 모드 초기화
            if (!Application.isPlaying && showInEditor && _simulateInEditor)
            {
                EditorInitialize();
            }
            #endif
        }
        
        #if UNITY_EDITOR
        private void OnEnable()
        {
            if (!Application.isPlaying && showInEditor)
            {
                EditorApplication.update += EditorUpdate;
            }
            lastSegmentCount = segmentCount; // 초기값 저장
        }
        
        private void OnDisable()
        {
            #if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
            #endif
        }
        
        // Inspector에서 값이 변경될 때 호출됨
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                bool canSimulate = showInEditor
                    && lineRenderer != null && startAnchor != null && endAnchor != null;

                if (canSimulate)
                {
                    EditorApplication.update -= EditorUpdate;
                    EditorApplication.update += EditorUpdate;

                    // segmentCount 변경 감지
                    if (segmentCount != lastSegmentCount)
                    {
                        lastSegmentCount = segmentCount;
                        editorInitialized = false;
                    }

                    if (!editorInitialized)
                        EditorInitialize();
                }
                else
                {
                    EditorApplication.update -= EditorUpdate;
                }
            }
        }
        
        private void EditorInitialize()
        {
            if (editorInitialized || !ValidateComponents())
                return;
            
            // 배열 초기화
            if (positions == null || positions.Length != segmentCount)
            {
                positions = new Vector3[segmentCount];
                previousPositions = new Vector3[segmentCount];
            }
            
            // 위치 초기화
            ResetPositions();
            
            // LineRenderer 설정
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = segmentCount;
                lineRenderer.SetPositions(positions);
            }
            
            editorInitialized = true;
            isInitialized = true;
        }
        
        private void EditorUpdate()
        {
            // 컴포넌트 미할당 시 콜백 해제하여 에러 반복 방지
            if (lineRenderer == null || startAnchor == null || endAnchor == null)
            {
                EditorApplication.update -= EditorUpdate;
                return;
            }

            if (!updateInEditor || !showInEditor)
                return;

            // segmentCount 변경 감지
            if (segmentCount != lastSegmentCount)
            {
                lastSegmentCount = segmentCount;
                editorInitialized = false;
            }

            // 에디터에서도 초기화되지 않았다면 초기화
            if (!editorInitialized)
            {
                EditorInitialize();
                return;
            }

            // 앵커 위치 업데이트
            CacheAnchorPositions();

            // 앵커 위치가 변경되었는지 확인
            bool anchorChanged = (cachedStartPosition != lastCachedStartPos) ||
                                (cachedEndPosition != lastCachedEndPos);

            if (anchorChanged)
            {
                lastCachedStartPos = cachedStartPosition;
                lastCachedEndPos = cachedEndPosition;
            }

            if (_simulateInEditor)
            {
                // 물리 시뮬레이션 업데이트
                UpdateSimulation(0.016f);
            }
            else if (anchorChanged)
            {
                // 시뮬레이션 OFF: 앵커 이동 시 직선으로 재배치
                ResetPositions();
            }

            // 라인 그리기
            DrawLine();
            
            // Scene 뷰 갱신 (제한적으로 호출)
            if (!Application.isPlaying)
            {
                float currentTime = (float)EditorApplication.timeSinceStartup;
                if (anchorChanged || (currentTime - lastRepaintTime) >= REPAINT_INTERVAL)
                {
                    lastRepaintTime = currentTime;
                    try
                    {
                        SceneView.RepaintAll();
                    }
                    catch
                    {
                        // SceneView가 준비되지 않았을 때 무시
                    }
                }
            }
        }
        #endif
        
        private void Update()
        {
            #if UNITY_EDITOR
            // 에디터 모드에서는 EditorUpdate에서 처리
            if (!Application.isPlaying)
                return;
            #endif
            
            if (!isInitialized || lineRenderer == null || startAnchor == null || endAnchor == null)
                return;
            
            // 가시성 체크 (렌더러가 보이지 않으면 스킵)
            if (skipUpdateWhenInvisible && enablePerformanceOptimization)
            {
                bool isVisible = lineRenderer.enabled && 
                                (lineRenderer.isVisible || !Application.isPlaying);
                if (!isVisible && wasVisible == false)
                    return;
                wasVisible = isVisible;
            }
            
            // 업데이트 레이트 제어
            if (enablePerformanceOptimization && updateRate < 60)
            {
                updateTimer += Time.deltaTime;
                if (updateTimer < updateInterval)
                    return;
                
                updateTimer = 0f;
            }
            
            // 앵커 위치 캐싱 (매 프레임 접근 최소화)
            if (enablePerformanceOptimization)
            {
                CacheAnchorPositions();
            }
            
            UpdateSimulation(Time.deltaTime);
            DrawLine();
        }
        
        private void OnBecameVisible()
        {
            wasVisible = true;
        }
        
        private void OnBecameInvisible()
        {
            wasVisible = false;
        }
        
        private void OnDrawGizmos()
        {
            #if UNITY_EDITOR
            if (positions == null || positions.Length == 0)
                return;
            
            // 에디터에서 라인 표시
            if (showInEditor && showEditorGizmos)
            {
                // 메인 라인 그리기
                Gizmos.color = editorLineColor;
                for (int i = 0; i < positions.Length - 1; i++)
                {
                    Gizmos.DrawLine(positions[i], positions[i + 1]);
                }
                
                // 시작/끝점 표시
                if (startAnchor != null && endAnchor != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(startAnchor.position, editorLineWidth * 2f);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(endAnchor.position, editorLineWidth * 2f);
                }
            }
            
            // 디버그 모드일 때 상세 정보 표시
            if (debugMode && isInitialized)
            {
                Gizmos.color = debugColor;
                for (int i = 0; i < positions.Length; i++)
                {
                    Gizmos.DrawWireSphere(positions[i], debugSphereSize);
                    if (i > 0)
                    {
                        Gizmos.DrawLine(positions[i-1], positions[i]);
                    }
                }
            }
            #endif
        }
        
        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            if (positions == null || positions.Length == 0 || !showInEditor)
                return;
            
            // 선택 시 더 두꺼운 라인으로 표시
            Gizmos.color = new Color(editorLineColor.r, editorLineColor.g, editorLineColor.b, 1f);
            for (int i = 0; i < positions.Length - 1; i++)
            {
                Gizmos.DrawLine(positions[i], positions[i + 1]);
            }
            
            // 세그먼트 포인트 표시
            Gizmos.color = editorLineColor;
            for (int i = 0; i < positions.Length; i++)
            {
                Gizmos.DrawWireSphere(positions[i], editorLineWidth);
            }
            #endif
        }
        #endregion
        
        #region 초기화 메서드
        private void SetupLineRenderer()
        {
            if (lineRenderer == null) return;
            
            #if UNITY_EDITOR
            // 에디터 모드에서는 sharedMaterial 사용 (머티리얼 누수 방지)
            bool isPlaying = Application.isPlaying;
            #else
            bool isPlaying = true;
            #endif
            
            // 머티리얼이 없으면 기본 머티리얼 설정
            Material currentMaterial = isPlaying ? lineRenderer.material : lineRenderer.sharedMaterial;
            
            if (currentMaterial == null)
            {
                Material defaultMaterial = new Material(Shader.Find("Sprites/Default"));
                defaultMaterial.color = Color.white;
                
                if (isPlaying)
                {
                    lineRenderer.material = defaultMaterial;
                }
                else
                {
                    lineRenderer.sharedMaterial = defaultMaterial;
                }
                
                Debug.LogWarning("LineRenderer 머티리얼이 없어서 기본 머티리얼을 설정했습니다.");
            }
            
            // 너비가 0이면 기본값 설정
            if (lineRenderer.startWidth <= 0)
            {
                lineRenderer.startWidth = 0.1f;
                Debug.LogWarning("LineRenderer startWidth가 0이어서 0.1로 설정했습니다.");
            }
            if (lineRenderer.endWidth <= 0)
            {
                lineRenderer.endWidth = 0.1f;
                Debug.LogWarning("LineRenderer endWidth가 0이어서 0.1로 설정했습니다.");
            }
            
            originalStartWidth = lineRenderer.startWidth;
            originalEndWidth = lineRenderer.endWidth;
            
            // 기본 설정
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.View;
        }
        
        public void Initialize()
        {
            if (!ValidateComponents())
            {
                Debug.LogError("컴포넌트 검증 실패!");
                return;
            }
            
            StartCoroutine(InitializeCoroutine());
        }
        
        private IEnumerator InitializeCoroutine()
        {
            // 일단 비우기
            lineRenderer.positionCount = 0;
            
            // 한 프레임 대기
            yield return null;
            
            // 위치 초기화
            ResetPositions();
            
            // LineRenderer 설정
            lineRenderer.positionCount = segmentCount;
            lineRenderer.SetPositions(positions);
            
            isInitialized = true;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugMode)
            {
                Debug.Log($"라인 초기화 완료!");
                Debug.Log($"- 시작점: {startAnchor.position}");
                Debug.Log($"- 끝점: {endAnchor.position}");
                Debug.Log($"- 세그먼트 수: {segmentCount}");
                Debug.Log($"- LineRenderer 너비: {lineRenderer.startWidth} ~ {lineRenderer.endWidth}");
            }
            #endif
        }
        
        private bool ValidateComponents()
        {
            bool isValid = true;
            
            if (lineRenderer == null)
            {
                Debug.LogError("LineRenderer가 없습니다!");
                isValid = false;
            }
            
            if (startAnchor == null)
            {
                Debug.LogError("시작 앵커가 설정되지 않았습니다!");
                isValid = false;
            }
            
            if (endAnchor == null)
            {
                Debug.LogError("끝 앵커가 설정되지 않았습니다!");
                isValid = false;
            }
            
            return isValid;
        }
        #endregion
        
        #region 시뮬레이션
        private void ResetPositions()
        {
            Vector3 startPos = startAnchor.position;
            Vector3 endPos = endAnchor.position;
            Vector3 direction = endPos - startPos;
            float totalDistance = direction.magnitude;
            
            // 세그먼트 길이 자동 계산
            if (autoCalculateSegmentLength)
            {
                calculatedSegmentLength = totalDistance / (segmentCount - 1);
            }
            else
            {
                calculatedSegmentLength = segmentLength;
            }
            
            // 방향 벡터 정규화 (한 번만 계산)
            Vector3 normalizedDirection = totalDistance > 0.0001f ? direction / totalDistance : Vector3.forward;
            
            // 수직 벡터 계산 (한 번만)
            Vector3 perpendicular = Vector3.Cross(normalizedDirection, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.01f)
                perpendicular = Vector3.Cross(normalizedDirection, Vector3.forward);
            perpendicular.Normalize();
            
            float offsetAmount = calculatedSegmentLength * 0.3f;
            float segmentDivisor = segmentCount - 1;
            
            // 위치 초기화 (약간의 랜덤 오프셋 추가하여 초기 움직임 유도)
            for (int i = 0; i < segmentCount; i++)
            {
                float t = i / segmentDivisor;
                Vector3 basePos;
                basePos.x = startPos.x + (endPos.x - startPos.x) * t;
                basePos.y = startPos.y + (endPos.y - startPos.y) * t;
                basePos.z = startPos.z + (endPos.z - startPos.z) * t;
                
                // 중간 포인트에 약간의 오프셋 추가 (직선이 아닌 초기 상태)
                if (i > 0 && i < segmentCount - 1)
                {
                    float sinValue = Mathf.Sin(t * Mathf.PI) * offsetAmount;
                    basePos.x += perpendicular.x * sinValue;
                    basePos.y += perpendicular.y * sinValue;
                    basePos.z += perpendicular.z * sinValue;
                }
                
                positions[i] = basePos;
                // 이전 위치를 약간 뒤로 설정하여 초기 속도 부여
                float offsetBack = 0.01f;
                previousPositions[i].x = basePos.x - normalizedDirection.x * offsetBack;
                previousPositions[i].y = basePos.y - normalizedDirection.y * offsetBack;
                previousPositions[i].z = basePos.z - normalizedDirection.z * offsetBack;
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugMode)
            {
                Debug.Log($"위치 리셋 - 전체 거리: {totalDistance}, 세그먼트 길이: {calculatedSegmentLength}");
            }
            #endif
        }
        
        private void UpdateSimulation(float deltaTime)
        {
            // 물리 시뮬레이션
            Simulate(deltaTime);
            
            // 제약 조건 적용
            ApplyConstraints();
        }
        
        private void Simulate(float deltaTime)
        {
            // 중력 캐싱 (매 프레임 계산 방지)
            if (!enablePerformanceOptimization || cachedGravity == Vector3.zero)
            {
                CacheGravity();
            }
            
            Vector3 gravity = cachedGravity;
            float deltaTimeSquared = deltaTime * deltaTime;
            float gravityFactor = deltaTimeSquared * gravityResistance;
            
            // 첫 번째와 마지막은 고정이므로 1부터 segmentCount-1까지
            int endIndex = segmentCount - 1;
            for (int i = 1; i < endIndex; i++)
            {
                // Verlet 통합을 사용한 물리 시뮬레이션
                Vector3 velocity = positions[i] - previousPositions[i];
                previousPositions[i] = positions[i];
                
                // 속도에 댐핑 적용
                velocity.x *= damping;
                velocity.y *= damping;
                velocity.z *= damping;
                
                // 중력 적용
                velocity.x += gravity.x * gravityFactor;
                velocity.y += gravity.y * gravityFactor;
                velocity.z += gravity.z * gravityFactor;
                
                // 새로운 위치 계산
                positions[i].x += velocity.x;
                positions[i].y += velocity.y;
                positions[i].z += velocity.z;
            }
        }
        
        private void ApplyConstraints()
        {
            // 앵커 위치로 고정 (캐시된 값 사용)
            Vector3 startPos = enablePerformanceOptimization ? cachedStartPosition : startAnchor.position;
            Vector3 endPos = enablePerformanceOptimization ? cachedEndPosition : endAnchor.position;
            
            positions[0] = startPos;
            int lastIndex = segmentCount - 1;
            positions[lastIndex] = endPos;
            
            // 거리 제약 (더 부드러운 제약 적용)
            int segmentEnd = segmentCount - 1;
            float sqrThreshold = 0.0001f * 0.0001f; // 거리 비교 최적화
            
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                for (int i = 0; i < segmentEnd; i++)
                {
                    int nextIndex = i + 1;
                    Vector3 direction = positions[nextIndex] - positions[i];
                    
                    // 거리 제곱으로 비교 (sqrt 연산 최소화)
                    float sqrDistance = direction.x * direction.x + 
                                       direction.y * direction.y + 
                                       direction.z * direction.z;
                    
                    if (sqrDistance > sqrThreshold)
                    {
                        float distance = Mathf.Sqrt(sqrDistance);
                        float error = distance - calculatedSegmentLength;
                        float correctionFactor = (error * stiffness) / distance;
                        
                        // 벡터 연산 최적화
                        float correctionX = direction.x * correctionFactor;
                        float correctionY = direction.y * correctionFactor;
                        float correctionZ = direction.z * correctionFactor;
                        
                        // 양쪽 끝점을 제외하고 보정 적용
                        if (i != 0)
                        {
                            positions[i].x += correctionX;
                            positions[i].y += correctionY;
                            positions[i].z += correctionZ;
                        }
                        
                        if (nextIndex != lastIndex)
                        {
                            positions[nextIndex].x -= correctionX;
                            positions[nextIndex].y -= correctionY;
                            positions[nextIndex].z -= correctionZ;
                        }
                    }
                }
                
                // 매 반복마다 앵커 위치로 다시 고정
                positions[0] = startPos;
                positions[lastIndex] = endPos;
            }
        }
        
        private void DrawLine()
        {
            if (lineRenderer == null || positions == null) 
                return;
            
            // positionCount는 변경될 때만 설정 (불필요한 할당 방지)
            if (lineRenderer.positionCount != segmentCount)
            {
                lineRenderer.positionCount = segmentCount;
            }
            
            lineRenderer.SetPositions(positions);
            
            if (scaleLineWidth)
            {
                float scale = Mathf.Abs(transform.lossyScale.x);
                lineRenderer.startWidth = originalStartWidth * scale;
                lineRenderer.endWidth = originalEndWidth;
            }
        }
        
        // 캐싱 메서드들
        private void CacheAnchorPositions()
        {
            if (startAnchor != null)
                cachedStartPosition = startAnchor.position;
            if (endAnchor != null)
                cachedEndPosition = endAnchor.position;
        }
        
        private void CacheGravity()
        {
            cachedGravity = useGravity ? customGravity : Vector3.zero;
        }
        #endregion
        
        #region 공개 메서드
        public void SetAnchors(Transform start, Transform end)
        {
            startAnchor = start;
            endAnchor = end;
            
            if (isInitialized)
                ResetPositions();
        }
        
        public void RefreshLineRenderer()
        {
            SetupLineRenderer();
            if (isInitialized)
            {
                DrawLine();
            }
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 수동으로 초기화 (Inspector에서 호출 가능)
        /// </summary>
        [ContextMenu("에디터에서 초기화")]
        public void EditorManualInitialize()
        {
            editorInitialized = false;
            EditorInitialize();
        }
        
        /// <summary>
        /// 에디터에서 위치 리셋
        /// </summary>
        [ContextMenu("위치 리셋")]
        public void EditorResetPositions()
        {
            if (ValidateComponents())
            {
                ResetPositions();
                if (lineRenderer != null)
                {
                    lineRenderer.positionCount = segmentCount;
                    lineRenderer.SetPositions(positions);
                }
                SceneView.RepaintAll();
            }
        }
        #endif
        #endregion
    }
}