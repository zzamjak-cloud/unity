using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoveObject
{
    /// <summary>
    /// 보상 타입별 위치 정보
    /// </summary>
    [Serializable]
    public class RewardTypeLocation
    {
        [Tooltip("보상 타입 ID (Gold, Gem, Heart 등)")]
        public string RewardID;

        [Tooltip("시작 위치 RectTransform")]
        public RectTransform StartTransform;

        [Tooltip("도착 위치 RectTransform")]
        public RectTransform TargetTransform;
    }

    /// <summary>
    /// 개별 보상 아이템 정보
    /// </summary>
    [Serializable]
    public class RewardItem
    {
        public string RewardID;
        public Vector3 StartPosition;
        public Vector3 TargetPosition;

        public RewardItem(string rewardID, Vector3 startPosition, Vector3 targetPosition)
        {
            RewardID = rewardID;
            StartPosition = startPosition;
            TargetPosition = targetPosition;
        }
    }

    /// <summary>
    /// 보상 그룹 정보 (같은 시작/도착 지점을 공유하는 아이템들)
    /// </summary>
    [Serializable]
    public class RewardGroup
    {
        public string RewardID;
        public int Count;
        public Vector3 StartPosition;
        public Vector3 TargetPosition;
    }

    /// <summary>
    /// 전체 연출 시퀀스를 큐(Queue) 단위로 관리하고 그룹 간 인터벌을 제어하는 싱글톤 매니저
    /// </summary>
    public class RewardSequenceManager : MonoBehaviour
    {
        private static RewardSequenceManager _instance;
        public static RewardSequenceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("RewardSequenceManager");
                    _instance = go.AddComponent<RewardSequenceManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("컴포넌트 참조")]
        [SerializeField] private RewardPool _rewardPool;

        [Header("그룹 인터벌 설정")]
        [Tooltip("그룹 간 시작 지연 시간 (음수값 입력 시 즉시 시작하여 겹치게 재생)")]
        [SerializeField] private float _groupStartDelay = 0.3f;

        [Header("좌표 변환 설정")]
        [Tooltip("UI 캔버스 (좌표 변환용)")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("UI 카메라 (좌표 변환용)")]
        [SerializeField] private Camera _uiCamera;

        // 타입별 위치 매핑
        private Dictionary<string, RewardTypeLocation> _typeLocationMap;

        // 실행 중인 시퀀스 관리
        private Queue<RewardGroup> _sequenceQueue;
        private bool _isPlaying;
        private int _activeItemCount;
        private int _activeGroupCount;

        // 상수 정의
        private const float DEFAULT_POOL_SIZE = 10f;
        private const int RECT_CORNER_COUNT = 4;
        private const int CORNER_INDEX_BOTTOM_LEFT = 0;
        private const int CORNER_INDEX_TOP_RIGHT = 2;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _sequenceQueue = new Queue<RewardGroup>();
            _typeLocationMap = new Dictionary<string, RewardTypeLocation>();

            // RewardPool이 없으면 생성
            if (_rewardPool == null)
            {
                GameObject poolObject = new GameObject("RewardPool");
                poolObject.transform.SetParent(transform);
                _rewardPool = poolObject.AddComponent<RewardPool>();
            }

            // UI 카메라 자동 찾기
            if (_uiCamera == null)
            {
                _uiCamera = Camera.main;
            }
        }

        /// <summary>
        /// 보상 타입 초기화 (풀 생성)
        /// </summary>
        public void RegisterRewardType(RewardData data, int poolSize = 10)
        {
            _rewardPool.InitializePool(data, poolSize);
        }

        /// <summary>
        /// 보상 타입별 위치 등록 (RectTransform 사용)
        /// </summary>
        public void RegisterRewardTypeLocation(string rewardID, RectTransform startRect, RectTransform targetRect)
        {
            if (string.IsNullOrEmpty(rewardID))
            {
                Debug.LogError("[RewardSequenceManager] RewardID cannot be null or empty.");
                return;
            }

            if (_typeLocationMap.ContainsKey(rewardID))
            {
                _typeLocationMap[rewardID].StartTransform = startRect;
                _typeLocationMap[rewardID].TargetTransform = targetRect;
            }
            else
            {
                _typeLocationMap[rewardID] = new RewardTypeLocation
                {
                    RewardID = rewardID,
                    StartTransform = startRect,
                    TargetTransform = targetRect
                };
            }
        }

        /// <summary>
        /// 보상 타입별 위치 등록 (Vector3 사용 - 레거시)
        /// </summary>
        public void RegisterRewardTypeLocation(string rewardID, Vector3 startPosition, Vector3 targetPosition)
        {
            Debug.LogWarning("[RewardSequenceManager] Vector3 based location is not resolution-safe. Use RectTransform instead.");
            // Vector3 기반은 지원하지 않음
        }

        /// <summary>
        /// 등록된 타입별 위치 가져오기
        /// </summary>
        public RewardTypeLocation GetRewardTypeLocation(string rewardID)
        {
            if (_typeLocationMap.TryGetValue(rewardID, out RewardTypeLocation location))
            {
                return location;
            }
            return null;
        }

        /// <summary>
        /// 단일 보상 그룹 재생
        /// </summary>
        public void PlayReward(string rewardID, int count, Vector3 startPosition, Vector3 targetPosition, Action onComplete = null)
        {
            RewardGroup group = new RewardGroup
            {
                RewardID = rewardID,
                Count = count,
                StartPosition = startPosition,
                TargetPosition = targetPosition
            };

            StartCoroutine(PlayGroupSequence(group, onComplete));
        }

        /// <summary>
        /// 타입별 등록된 위치로 보상 재생
        /// </summary>
        public void PlayRewardByType(string rewardID, int count, Action onComplete = null)
        {
            RewardTypeLocation location = GetRewardTypeLocation(rewardID);
            if (location == null)
            {
                Debug.LogError($"[RewardSequenceManager] RewardID '{rewardID}' location not registered. Call RegisterRewardTypeLocation first.");
                onComplete?.Invoke();
                return;
            }

            if (location.StartTransform == null || location.TargetTransform == null)
            {
                Debug.LogError($"[RewardSequenceManager] RewardID '{rewardID}' has null transforms!");
                onComplete?.Invoke();
                return;
            }

            // 런타임에 실시간으로 월드 좌표 가져오기 (해상도 대응)
            Vector3 startPos = GetWorldPositionOfRect(location.StartTransform);
            Vector3 targetPos = GetWorldPositionOfRect(location.TargetTransform);

            PlayReward(rewardID, count, startPos, targetPos, onComplete);
        }

        /// <summary>
        /// 개별 보상 아이템 재생 (각 아이템마다 다른 시작/도착 지점)
        /// </summary>
        public void PlayRewardItem(RewardItem item, Action onComplete = null)
        {
            StartCoroutine(PlaySingleItem(item, onComplete));
        }

        /// <summary>
        /// 여러 개별 보상 아이템을 순차적으로 재생
        /// </summary>
        public void PlayRewardItems(List<RewardItem> items, Action onComplete = null)
        {
            if (items == null || items.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(PlayMultipleItems(items, onComplete));
        }

        /// <summary>
        /// 여러 보상 그룹을 순차적으로 재생
        /// </summary>
        public void PlayRewardSequence(List<RewardGroup> groups, Action onComplete = null)
        {
            if (groups == null || groups.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(PlayMultipleGroups(groups, onComplete));
        }

        /// <summary>
        /// 여러 개별 아이템을 순차적으로 재생하는 코루틴
        /// </summary>
        private IEnumerator PlayMultipleItems(List<RewardItem> items, Action onComplete)
        {
            _isPlaying = true;

            foreach (var item in items)
            {
                yield return StartCoroutine(PlaySingleItem(item, null));

                // 다음 아이템 시작 전 지연
                if (_groupStartDelay > 0)
                {
                    yield return new WaitForSeconds(_groupStartDelay);
                }
            }

            _isPlaying = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// 여러 그룹을 순차적으로 재생하는 코루틴 (겹칠 수 있도록 병렬 실행)
        /// </summary>
        private IEnumerator PlayMultipleGroups(List<RewardGroup> groups, Action onComplete)
        {
            _isPlaying = true;
            _activeGroupCount = 0;

            // 각 그룹의 예상 연출 시간 계산
            List<float> groupDurations = new List<float>();
            foreach (var group in groups)
            {
                float duration = CalculateGroupDuration(group);
                groupDurations.Add(duration);
            }

            // 각 그룹을 독립적인 코루틴으로 시작
            // 각 그룹의 시작 시간을 이전 그룹 시작 시점 기준으로 상대적으로 계산
            float[] groupStartTimes = new float[groups.Count];
            groupStartTimes[0] = 0f; // 첫 번째 그룹은 즉시 시작
            
            // 각 그룹의 시작 시간 계산
            for (int i = 1; i < groups.Count; i++)
            {
                // 이전 그룹 시작 시점 + 이전 그룹 연출 시간 + 인터벌
                groupStartTimes[i] = groupStartTimes[i - 1] + groupDurations[i - 1] + _groupStartDelay;
                
                // 음수 interval로 인해 시작 시간이 음수가 될 수 있음
                // 이 경우 이전 그룹 시작 시점에서 더 빨리 시작 (더 겹치게)
                if (groupStartTimes[i] < 0)
                {
                    groupStartTimes[i] = 0f; // 즉시 시작
                }
            }
            
            // 각 그룹을 시작 시간에 맞춰 시작
            for (int i = 0; i < groups.Count; i++)
            {
                RewardGroup group = groups[i];
                
                // 시작 시간까지 대기
                if (i > 0)
                {
                    float waitTime = groupStartTimes[i] - groupStartTimes[i - 1];
                    if (waitTime > 0)
                    {
                        yield return new WaitForSeconds(waitTime);
                    }
                    // waitTime이 0 이하면 즉시 시작 (겹치게)
                }

                // 그룹을 독립적으로 시작
                StartCoroutine(PlayGroupSequenceWithCallback(group, () =>
                {
                    _activeGroupCount--;
                }));
                _activeGroupCount++;
            }

            // 모든 그룹이 완료될 때까지 대기
            while (_activeGroupCount > 0)
            {
                yield return null;
            }

            _isPlaying = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// RewardData 가져오기 (중복 코드 제거)
        /// </summary>
        private RewardData GetRewardData(string rewardID)
        {
            RewardItemEntity testEntity = _rewardPool.Get(rewardID);
            if (testEntity == null)
            {
                Debug.LogError($"[RewardSequenceManager] RewardID '{rewardID}' not registered. Call RegisterRewardType first.");
                return null;
            }
            RewardData data = testEntity.Data;
            _rewardPool.Return(testEntity);
            return data;
        }

        /// <summary>
        /// 그룹의 예상 전체 연출 시간 계산
        /// </summary>
        private float CalculateGroupDuration(RewardGroup group)
        {
            RewardData data = GetRewardData(group.RewardID);
            if (data == null)
            {
                return 0f;
            }

            // 실제 생성 개수
            int actualSpawnCount = Mathf.Min(group.Count, data.MaxSpawnCount);

            // 평균 생성 간격
            float avgSpawnInterval = (data.MinSpawnInterval + data.MaxSpawnInterval) * 0.5f;

            // 전체 연출 시간 = (생성 개수 - 1) * 평균 간격 + 이동 시간
            // 마지막 아이템이 생성되고 이동 완료까지의 시간
            float totalDuration = (actualSpawnCount - 1) * avgSpawnInterval + data.Duration;

            return totalDuration;
        }

        /// <summary>
        /// 그룹 시퀀스를 재생하고 완료 시 콜백 호출
        /// </summary>
        private IEnumerator PlayGroupSequenceWithCallback(RewardGroup group, Action onComplete)
        {
            yield return StartCoroutine(PlayGroupSequence(group, null));
            onComplete?.Invoke();
        }

        /// <summary>
        /// 단일 그룹 재생 코루틴
        /// </summary>
        private IEnumerator PlayGroupSequence(RewardGroup group, Action onComplete)
        {
            RewardData data = GetRewardData(group.RewardID);
            if (data == null)
            {
                yield break;
            }

            // 최적화: 최대 생성 개수 제한
            int actualSpawnCount = Mathf.Min(group.Count, data.MaxSpawnCount);
            int valuePerItem = Mathf.CeilToInt((float)group.Count / actualSpawnCount);

            _activeItemCount = 0;

            // 아이템 순차 생성 및 이동
            for (int i = 0; i < actualSpawnCount; i++)
            {
                SpawnAndMoveItem(group, data);
                _activeItemCount++;

                // 개별 등장 간격
                float interval = UnityEngine.Random.Range(data.MinSpawnInterval, data.MaxSpawnInterval);
                yield return new WaitForSeconds(interval);
            }

            // 모든 아이템이 도착할 때까지 대기
            while (_activeItemCount > 0)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// 단일 개별 아이템 재생 코루틴
        /// </summary>
        private IEnumerator PlaySingleItem(RewardItem item, Action onComplete)
        {
            RewardData data = GetRewardData(item.RewardID);
            if (data == null)
            {
                yield break;
            }

            _activeItemCount = 0;

            // 단일 아이템 생성 및 이동
            SpawnAndMoveIndividualItem(item, data);
            _activeItemCount++;

            // 아이템이 도착할 때까지 대기
            while (_activeItemCount > 0)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// 개별 아이템 생성 및 이동 (그룹용)
        /// </summary>
        private void SpawnAndMoveItem(RewardGroup group, RewardData data)
        {
            // Canvas를 부모로 설정하여 UI 좌표계에서 작동하도록 함
            Transform canvasTransform = _canvas != null ? _canvas.transform : null;
            RewardItemEntity entity = _rewardPool.Get(group.RewardID, canvasTransform);
            if (entity == null) return;

            // 시작 위치를 Canvas 로컬 좌표로 변환
            Vector3 spawnPosition = group.StartPosition;

            // 랜덤 오프셋을 Canvas 로컬 좌표 기준으로 추가
            if (data.SpawnRandomRadius > 0 && _canvas != null)
            {
                spawnPosition = ApplyRandomOffsetToSpawnPosition(spawnPosition, data.SpawnRandomRadius);
            }

            // 등장 연출
            entity.Spawn(spawnPosition, () =>
            {
                // 등장 완료 후 이동 시작
                entity.MoveTo(group.TargetPosition, OnItemArrival);
            });
        }

        /// <summary>
        /// 개별 아이템 생성 및 이동 (개별 아이템용)
        /// </summary>
        private void SpawnAndMoveIndividualItem(RewardItem item, RewardData data)
        {
            // Canvas를 부모로 설정하여 UI 좌표계에서 작동하도록 함
            Transform canvasTransform = _canvas != null ? _canvas.transform : null;
            RewardItemEntity entity = _rewardPool.Get(item.RewardID, canvasTransform);
            if (entity == null) return;

            // 시작 위치 (개별 아이템은 랜덤 오프셋 없이 정확한 위치 사용)
            Vector3 spawnPosition = item.StartPosition;

            // 등장 연출
            entity.Spawn(spawnPosition, () =>
            {
                // 등장 완료 후 이동 시작
                entity.MoveTo(item.TargetPosition, OnItemArrival);
            });
        }

        /// <summary>
        /// 아이템 도착 시 콜백
        /// </summary>
        private void OnItemArrival(RewardItemEntity entity)
        {
            _activeItemCount--;
            _rewardPool.Return(entity);
        }

        /// <summary>
        /// UI 캔버스 설정 (좌표 변환용)
        /// </summary>
        public void SetCanvas(Canvas canvas)
        {
            _canvas = canvas;
        }

        /// <summary>
        /// UI 카메라 설정 (좌표 변환용)
        /// </summary>
        public void SetUICamera(Camera camera)
        {
            _uiCamera = camera;
        }

        /// <summary>
        /// 스크린 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector3 ScreenToWorldPoint(Vector2 screenPosition)
        {
            if (_uiCamera == null)
            {
                Debug.LogWarning("[RewardSequenceManager] UI Camera is not set.");
                return Vector3.zero;
            }

            return _uiCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _uiCamera.nearClipPlane + 100f));
        }

        /// <summary>
        /// RectTransform의 월드 좌표 가져오기
        /// </summary>
        public Vector3 GetWorldPositionOfRect(RectTransform rectTransform)
        {
            if (rectTransform == null) return Vector3.zero;

            Vector3[] corners = new Vector3[RECT_CORNER_COUNT];
            rectTransform.GetWorldCorners(corners);
            return (corners[CORNER_INDEX_BOTTOM_LEFT] + corners[CORNER_INDEX_TOP_RIGHT]) * 0.5f;
        }

        /// <summary>
        /// 현재 재생 중인지 여부
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// 스폰 위치에 랜덤 오프셋 적용
        /// </summary>
        private Vector3 ApplyRandomOffsetToSpawnPosition(Vector3 spawnPosition, float radius)
        {
            RectTransform canvasRect = _canvas?.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return spawnPosition + (Vector3)(UnityEngine.Random.insideUnitCircle * radius);
            }

            // 월드 좌표를 Canvas 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(null, spawnPosition),
                null,
                out Vector2 localStart
            );

            // 로컬 좌표에 오프셋 추가
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
            localStart += randomOffset;

            // 다시 월드 좌표로 변환
            return canvasRect.TransformPoint(localStart);
        }

        /// <summary>
        /// 모든 시퀀스 중단
        /// </summary>
        public void StopAll()
        {
            StopAllCoroutines();
            _sequenceQueue.Clear();
            _isPlaying = false;
            _activeItemCount = 0;
            _activeGroupCount = 0;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
