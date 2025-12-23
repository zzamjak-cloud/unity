using System.Collections.Generic;
using UnityEngine;

namespace MoveObject
{
    /// <summary>
    /// 재화 오브젝트와 이펙트의 오브젝트 풀링을 담당
    /// </summary>
    public class RewardPool : MonoBehaviour
    {
        // 상수 정의
        private const int DEFAULT_INITIAL_POOL_SIZE = 10;
        private const int DEFAULT_EXPAND_SIZE = 5;
        private const string POOL_OBJECT_NAME = "RewardPool";

        [Header("풀링 설정")]
        [Tooltip("각 타입별 미리 생성할 오브젝트 수")]
        [SerializeField] private int _initialPoolSize = DEFAULT_INITIAL_POOL_SIZE;

        [Tooltip("풀이 부족할 때 추가로 생성할 오브젝트 수")]
        [SerializeField] private int _expandSize = DEFAULT_EXPAND_SIZE;

        [Tooltip("풀 오브젝트의 부모 Transform")]
        [SerializeField] private Transform _poolParent;

        // 타입별 풀 딕셔너리
        private Dictionary<string, Queue<RewardItemEntity>> _poolDictionary;
        private Dictionary<string, RewardData> _dataCache;

        private void Awake()
        {
            _poolDictionary = new Dictionary<string, Queue<RewardItemEntity>>();
            _dataCache = new Dictionary<string, RewardData>();

            if (_poolParent == null)
            {
                GameObject poolObject = new GameObject(POOL_OBJECT_NAME);
                poolObject.transform.SetParent(transform);
                _poolParent = poolObject.transform;
            }
        }

        /// <summary>
        /// 특정 타입의 풀 초기화
        /// </summary>
        public void InitializePool(RewardData data, int count = -1)
        {
            if (!ValidateRewardData(data))
            {
                return;
            }

            string rewardID = data.RewardID;
            if (_poolDictionary.ContainsKey(rewardID))
            {
                Debug.LogWarning($"[RewardPool] Pool for '{rewardID}' already initialized");
                return;
            }

            int poolSize = count > 0 ? count : _initialPoolSize;
            _dataCache[rewardID] = data;
            _poolDictionary[rewardID] = CreatePoolQueue(data, poolSize);
        }

        /// <summary>
        /// RewardData 유효성 검사
        /// </summary>
        private bool ValidateRewardData(RewardData data)
        {
            if (data == null)
            {
                Debug.LogError("[RewardPool] RewardData is null");
                return false;
            }

            if (data.ItemPrefab == null)
            {
                Debug.LogError($"[RewardPool] ItemPrefab is null for RewardID '{data.RewardID}'");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 풀 큐 생성 및 오브젝트 미리 생성
        /// </summary>
        private Queue<RewardItemEntity> CreatePoolQueue(RewardData data, int poolSize)
        {
            Queue<RewardItemEntity> queue = new Queue<RewardItemEntity>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                RewardItemEntity entity = CreateNewEntity(data);
                queue.Enqueue(entity);
            }
            return queue;
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기
        /// </summary>
        public RewardItemEntity Get(string rewardID, Transform parent = null)
        {
            if (!_poolDictionary.ContainsKey(rewardID))
            {
                Debug.LogError($"[RewardPool] Pool for '{rewardID}' not initialized. Call InitializePool first.");
                return null;
            }

            Queue<RewardItemEntity> queue = _poolDictionary[rewardID];

            // 풀이 비어있으면 확장
            if (queue.Count == 0)
            {
                ExpandPool(rewardID, _expandSize);
            }

            RewardItemEntity entity = queue.Dequeue();

            // 부모 설정 (Canvas로 이동)
            if (parent != null)
            {
                entity.transform.SetParent(parent, false);
            }

            entity.gameObject.SetActive(true);
            return entity;
        }

        /// <summary>
        /// 풀로 오브젝트 반환
        /// </summary>
        public void Return(RewardItemEntity entity)
        {
            if (entity == null) return;

            string rewardID = entity.Data.RewardID;

            if (!_poolDictionary.ContainsKey(rewardID))
            {
                Debug.LogWarning($"[RewardPool] Pool for '{rewardID}' not found. Destroying entity.");
                Destroy(entity.gameObject);
                return;
            }

            entity.Release();
            entity.gameObject.SetActive(false);
            entity.transform.SetParent(_poolParent);

            _poolDictionary[rewardID].Enqueue(entity);
        }

        /// <summary>
        /// 풀 확장
        /// </summary>
        private void ExpandPool(string rewardID, int count)
        {
            if (!_dataCache.ContainsKey(rewardID))
            {
                Debug.LogError($"[RewardPool] Data for '{rewardID}' not found in cache");
                return;
            }

            RewardData data = _dataCache[rewardID];
            Queue<RewardItemEntity> queue = _poolDictionary[rewardID];

            for (int i = 0; i < count; i++)
            {
                RewardItemEntity entity = CreateNewEntity(data);
                queue.Enqueue(entity);
            }

            Debug.Log($"[RewardPool] Pool for '{rewardID}' expanded by {count}. New size: {queue.Count}");
        }

        /// <summary>
        /// 새 엔티티 생성
        /// </summary>
        private RewardItemEntity CreateNewEntity(RewardData data)
        {
            GameObject go = Instantiate(data.ItemPrefab, _poolParent);
            go.SetActive(false);

            RewardItemEntity entity = go.GetComponent<RewardItemEntity>();
            if (entity == null)
            {
                entity = go.AddComponent<RewardItemEntity>();
            }

            entity.Initialize(data);
            return entity;
        }

        /// <summary>
        /// 모든 풀 정리
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _poolDictionary)
            {
                while (kvp.Value.Count > 0)
                {
                    RewardItemEntity entity = kvp.Value.Dequeue();
                    if (entity != null)
                    {
                        Destroy(entity.gameObject);
                    }
                }
            }

            _poolDictionary.Clear();
            _dataCache.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
