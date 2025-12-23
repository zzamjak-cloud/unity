using System.Collections.Generic;
using UnityEngine;

namespace MoveObject
{
    /// <summary>
    /// 보상 시스템 테스트용 컴포넌트
    /// </summary>
    public class RewardTester : MonoBehaviour
    {
        [Header("테스트 설정")]
        [Tooltip("테스트할 보상 데이터 목록")]
        [SerializeField] private List<RewardData> _testRewardDatas;

        [Header("타입별 위치 설정")]
        [Tooltip("타입별 시작 위치 매핑")]
        [SerializeField] private List<RewardTypeLocation> _typeLocations;

        [Header("위치 설정 (기본)")]
        [Tooltip("시작 위치 (UI RectTransform)")]
        [SerializeField] private RectTransform _startPosition;

        [Tooltip("도착 위치 (UI RectTransform)")]
        [SerializeField] private RectTransform _targetPosition;

        [Header("테스트 파라미터")]
        [Tooltip("생성할 아이템 개수")]
        [SerializeField] private int _testCount = 10;

        [Tooltip("테스트할 보상 인덱스")]
        [SerializeField] private int _testRewardIndex = 0;

        [Header("UI 설정")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _uiCamera;

        private void Start()
        {
            // 매니저 초기화
            if (_canvas != null)
            {
                RewardSequenceManager.Instance.SetCanvas(_canvas);
            }

            if (_uiCamera != null)
            {
                RewardSequenceManager.Instance.SetUICamera(_uiCamera);
            }

            // 보상 타입 등록
            if (_testRewardDatas != null)
            {
                foreach (var data in _testRewardDatas)
                {
                    RewardSequenceManager.Instance.RegisterRewardType(data, 20);
                }
            }

            // 타입별 위치 등록
            if (_typeLocations != null)
            {
                foreach (var typeLocation in _typeLocations)
                {
                    if (typeLocation.StartTransform == null || typeLocation.TargetTransform == null)
                    {
                        Debug.LogWarning($"[RewardTester] RewardID '{typeLocation.RewardID}' has null transforms. Skipping.");
                        continue;
                    }

                    RewardSequenceManager.Instance.RegisterRewardTypeLocation(
                        typeLocation.RewardID,
                        typeLocation.StartTransform,
                        typeLocation.TargetTransform
                    );
                    Debug.Log($"[RewardTester] Registered location for {typeLocation.RewardID}");
                }
            }
        }

        /// <summary>
        /// 단일 보상 테스트 (버튼이나 키 입력으로 호출)
        /// </summary>
        [ContextMenu("Test Single Reward")]
        public void TestSingleReward()
        {
            if (_testRewardDatas == null || _testRewardDatas.Count == 0)
            {
                Debug.LogError("[RewardTester] No reward data assigned!");
                return;
            }

            if (_testRewardIndex < 0 || _testRewardIndex >= _testRewardDatas.Count)
            {
                Debug.LogError($"[RewardTester] Invalid test reward index: {_testRewardIndex}");
                return;
            }

            RewardData data = _testRewardDatas[_testRewardIndex];
            
            // 타입별 등록된 위치 확인
            RewardTypeLocation typeLocation = RewardSequenceManager.Instance.GetRewardTypeLocation(data.RewardID);
            
            if (typeLocation != null && typeLocation.StartTransform != null && typeLocation.TargetTransform != null)
            {
                // 타입별 등록된 위치 사용
                RewardSequenceManager.Instance.PlayRewardByType(
                    data.RewardID,
                    _testCount,
                    () => Debug.Log("[RewardTester] Reward sequence completed!")
                );
            }
            else
            {
                // 기본 위치 사용
                if (_startPosition == null || _targetPosition == null)
                {
                    Debug.LogError("[RewardTester] Start or Target position not assigned, and no type-specific location registered!");
                    return;
                }

                Vector3 startPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(_startPosition);
                Vector3 targetPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(_targetPosition);

                RewardSequenceManager.Instance.PlayReward(
                    data.RewardID,
                    _testCount,
                    startPos,
                    targetPos,
                    () => Debug.Log("[RewardTester] Reward sequence completed!")
                );
            }
        }

        /// <summary>
        /// 여러 보상 연속 테스트
        /// </summary>
        [ContextMenu("Test Multiple Rewards")]
        public void TestMultipleRewards()
        {
            if (_testRewardDatas == null || _testRewardDatas.Count == 0)
            {
                Debug.LogError("[RewardTester] No reward data assigned!");
                return;
            }

            List<RewardGroup> groups = new List<RewardGroup>();

            foreach (var data in _testRewardDatas)
            {
                // 타입별 등록된 위치 확인
                RewardTypeLocation typeLocation = RewardSequenceManager.Instance.GetRewardTypeLocation(data.RewardID);
                
                Vector3 startPos;
                Vector3 targetPos;

                if (typeLocation != null && typeLocation.StartTransform != null && typeLocation.TargetTransform != null)
                {
                    // 타입별 등록된 위치 사용
                    startPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(typeLocation.StartTransform);
                    targetPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(typeLocation.TargetTransform);
                }
                else
                {
                    // 기본 위치 사용
                    if (_startPosition == null || _targetPosition == null)
                    {
                        Debug.LogWarning($"[RewardTester] RewardID '{data.RewardID}' has no type-specific location and no default position. Skipping.");
                        continue;
                    }

                    startPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(_startPosition);
                    targetPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(_targetPosition);
                }

                groups.Add(new RewardGroup
                {
                    RewardID = data.RewardID,
                    Count = _testCount,
                    StartPosition = startPos,
                    TargetPosition = targetPos
                });
            }

            if (groups.Count == 0)
            {
                Debug.LogError("[RewardTester] No valid reward groups to play!");
                return;
            }

            RewardSequenceManager.Instance.PlayRewardSequence(
                groups,
                () => Debug.Log("[RewardTester] All reward sequences completed!")
            );
        }

        /// <summary>
        /// 타입별 등록된 위치로 보상 테스트
        /// </summary>
        [ContextMenu("Test Reward By Type")]
        public void TestRewardByType()
        {
            if (_testRewardDatas == null || _testRewardDatas.Count == 0)
            {
                Debug.LogError("[RewardTester] No reward data assigned!");
                return;
            }

            if (_testRewardIndex < 0 || _testRewardIndex >= _testRewardDatas.Count)
            {
                Debug.LogError($"[RewardTester] Invalid test reward index: {_testRewardIndex}");
                return;
            }

            RewardData data = _testRewardDatas[_testRewardIndex];

            RewardSequenceManager.Instance.PlayRewardByType(
                data.RewardID,
                _testCount,
                () => Debug.Log($"[RewardTester] Reward '{data.RewardID}' completed!")
            );
        }

        /// <summary>
        /// 모든 타입별 등록된 위치로 보상 순차 테스트
        /// </summary>
        [ContextMenu("Test All Rewards By Type")]
        public void TestAllRewardsByType()
        {
            if (_testRewardDatas == null || _testRewardDatas.Count == 0)
            {
                Debug.LogError("[RewardTester] No reward data assigned!");
                return;
            }

            StartCoroutine(TestAllRewardsByTypeCoroutine());
        }

        private System.Collections.IEnumerator TestAllRewardsByTypeCoroutine()
        {
            foreach (var data in _testRewardDatas)
            {
                bool isComplete = false;

                RewardSequenceManager.Instance.PlayRewardByType(
                    data.RewardID,
                    _testCount,
                    () =>
                    {
                        Debug.Log($"[RewardTester] Reward '{data.RewardID}' completed!");
                        isComplete = true;
                    }
                );

                // 완료 대기
                yield return new WaitUntil(() => isComplete);

                // 다음 타입 테스트 전 잠시 대기
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("[RewardTester] All reward types tested!");
        }

        private void Update()
        {
            // 테스트 단축키
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TestSingleReward();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                TestMultipleRewards();
            }

            // 타입별 테스트 단축키
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestRewardByType();
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                TestAllRewardsByType();
            }
        }

        private void OnDrawGizmos()
        {
            // 시작 위치 시각화
            if (_startPosition != null)
            {
                DrawRectGizmo(_startPosition, new Color(0f, 1f, 0f, 0.3f), new Color(0f, 1f, 0f, 1f));
            }

            // 도착 위치 시각화
            if (_targetPosition != null)
            {
                DrawRectGizmo(_targetPosition, new Color(1f, 0f, 0f, 0.3f), new Color(1f, 0f, 0f, 1f));
            }

            // 경로 시각화
            if (_startPosition != null && _targetPosition != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
                Vector3 startPos = GetRectCenter(_startPosition);
                Vector3 targetPos = GetRectCenter(_targetPosition);
                Gizmos.DrawLine(startPos, targetPos);

                // 화살표 표시
                DrawArrow(startPos, targetPos);
            }
        }

        /// <summary>
        /// RectTransform을 기즈모로 그리기
        /// </summary>
        private void DrawRectGizmo(RectTransform rectTransform, Color fillColor, Color wireColor)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            // 채워진 사각형 그리기
            Gizmos.color = fillColor;
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }

            // 외곽선 그리기
            Gizmos.color = wireColor;
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }

            // 중앙 마커 (월드 좌표 기반 크기 계산)
            Vector3 center = GetRectCenter(rectTransform);
            float worldWidth = Vector3.Distance(corners[0], corners[1]);
            float worldHeight = Vector3.Distance(corners[1], corners[2]);
            float markerSize = Mathf.Min(worldWidth, worldHeight) * 0.1f;
            Gizmos.DrawWireSphere(center, markerSize);
        }

        /// <summary>
        /// RectTransform의 중앙 월드 좌표 가져오기
        /// </summary>
        private Vector3 GetRectCenter(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        /// <summary>
        /// 화살표 그리기
        /// </summary>
        private void DrawArrow(Vector3 start, Vector3 end)
        {
            Vector3 direction = (end - start).normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;

            float arrowSize = Vector3.Distance(start, end) * 0.1f;
            Vector3 arrowTip = end - direction * arrowSize;

            Gizmos.DrawLine(end, arrowTip + right * arrowSize * 0.5f);
            Gizmos.DrawLine(end, arrowTip - right * arrowSize * 0.5f);
        }
    }
}
