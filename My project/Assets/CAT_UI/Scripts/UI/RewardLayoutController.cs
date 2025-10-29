using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보상 아이템들의 레이아웃을 관리하고 순차적으로 활성화하는 컨트롤러
/// 모바일 환경에 최적화되어 있으며, 미리 배치된 프리팹을 재사용합니다.
/// </summary>
public class RewardLayoutController : MonoBehaviour
{
    #region Inspector Fields
    
    [Header("테스트 설정")]
    [Tooltip("테스트를 위한 보상 개수 (런타임에 변경하여 즉시 테스트 가능, 최대값은 등록된 프리팹 개수로 자동 제한됨)")]
    [SerializeField] private int testRewardCount = 0;

    [Header("Grid 및 프리팹 설정")]
    [Tooltip("Reward Container의 자식 Grid Layout Group들")]
    [SerializeField] private List<GridLayoutGroup> grids = new List<GridLayoutGroup>();
    
    [Tooltip("각 Grid에 미리 배치된 RewardItem 프리팹들 (인스펙터에서 직접 등록)")]
    [SerializeField] private List<GridPrefabData> gridPrefabData = new List<GridPrefabData>();
    
    [Header("순차 활성화 설정")]
    [Tooltip("순차 활성화 사용 여부")]
    [SerializeField] private bool useSequentialActivation = true;
    
    [Tooltip("기본 아이템 간격 시간 (초)")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float baseItemInterval = 0.1f;
    
    [Tooltip("개수 증가에 따른 가중치 배수")]
    [Range(0.1f, 2.0f)]
    [SerializeField] private float countWeightMultiplier = 0.5f;
    
    [Tooltip("최대 아이템 간격 시간 (초)")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float maxItemInterval = 0.2f;

    [Header("레이아웃 설정")]
    [Tooltip("총 보상 개수에 따른 레이아웃 설정")]
    [SerializeField] private List<LayoutConfig> layoutConfigs = new List<LayoutConfig>();
    
    [Header("스케일 설정")]
    [Tooltip("Grid 내 최대 아이템 개수에 따른 스케일 설정")]
    [SerializeField] private List<ScaleConfig> scaleConfigs = new List<ScaleConfig>();

    [Header("최적화 설정")]
    [Tooltip("레이아웃 업데이트를 프레임에 분산할지 여부")]
    [SerializeField] private bool useFrameDistribution = true;
    
    [Tooltip("Grid Transform 초기화 사용 여부")]
    [SerializeField] private bool useGridTransformReset = true;
    
    [Tooltip("Grid Constraint Count 자동 설정 사용 여부 (각 Grid의 등록된 프리팹 개수로 자동 설정)")]
    [SerializeField] private bool autoSetupConstraintCount = true;

    #endregion

    #region Serializable Classes
    
    [Serializable]
    public class GridPrefabData
    {
        [Tooltip("이 Grid에 등록된 RewardItem 프리팹들")]
        public List<GameObject> rewardItems = new List<GameObject>();
    }
    
    [Serializable]
    public class LayoutConfig
    {
        [Tooltip("이 설정이 적용될 총 보상 개수 (자동으로 Element Index + 1로 설정됨)")]
        public int rewardCount;
        
        [Tooltip("각 Grid에 들어갈 아이템 개수 목록")]
        public List<int> itemsPerGrid = new List<int>();
        
        /// <summary>
        /// 기본 생성자 - Unity 인스펙터에서 리스트 항목 추가 시 값이 변경되는 문제를 방지합니다.
        /// </summary>
        public LayoutConfig()
        {
            rewardCount = 0;
            itemsPerGrid = new List<int>();
        }
        
        /// <summary>
        /// 매개변수가 있는 생성자 - 편의를 위한 생성자입니다.
        /// </summary>
        public LayoutConfig(int count, List<int> items)
        {
            rewardCount = count;
            itemsPerGrid = items != null ? new List<int>(items) : new List<int>();
        }
    }
    
    [Serializable]
    public class ScaleConfig
    {
        [Tooltip("이 스케일이 적용될 Grid 내 최대 아이템 개수 (자동으로 Element Index + 1로 설정됨)")]
        public int maxItemsInGrid;
        
        [Tooltip("Reward Container의 Uniform Scale 값")]
        [Range(0.1f, 2.0f)]
        public float containerScale = 1.0f;
        
        /// <summary>
        /// 기본 생성자 - Unity 인스펙터에서 리스트 항목 추가 시 값이 변경되는 문제를 방지합니다.
        /// </summary>
        public ScaleConfig()
        {
            maxItemsInGrid = 0;
            containerScale = 1.0f;
        }
        
        /// <summary>
        /// 매개변수가 있는 생성자 - 편의를 위한 생성자입니다.
        /// </summary>
        public ScaleConfig(int maxItems, float scale)
        {
            maxItemsInGrid = maxItems;
            containerScale = scale;
        }
    }

    #endregion

    #region Private Fields
    
    // 상수 정의
    private const float SCALE_TOLERANCE_MIN = 0.9f;
    private const float SCALE_TOLERANCE_MAX = 1.1f;
    private const float POSITION_Z_TOLERANCE = 0.1f;
    private const float SIZE_DELTA_TOLERANCE = 1000f;
    private const int FRAME_DISTRIBUTION_THRESHOLD = 10;
    private const float MIN_INTERVAL_TIME = 0.05f;
    
    private RectTransform containerRectTransform;
    private int lastTestCount = -1;
    private bool isInitialized = false;
    private bool isUpdatingLayout = false;
    private Coroutine layoutUpdateCoroutine;
    
    // 팝업 재사용을 위한 데이터 저장
    private List<object> lastRewardsData = null;
    private bool hasStoredRewards = false;
    
    // 캐싱 시스템
    private Dictionary<int, LayoutConfig> layoutConfigCache = new Dictionary<int, LayoutConfig>();
    private Dictionary<int, ScaleConfig> scaleConfigCache = new Dictionary<int, ScaleConfig>();
    
    // 직렬화 값 동기화를 위한 상태 저장 (에디터 전용)
    #if UNITY_EDITOR
    private int previousLayoutConfigsCount = 0;
    private int previousScaleConfigsCount = 0;
    #endif

    #endregion

    #region Unity Lifecycle
    
    void Start()
    {
        InitializeComponent();
    }
    
    void OnEnable()
    {
        if (hasStoredRewards && lastRewardsData != null)
        {
            StartCoroutine(RestoreDisplayAfterEnable());
        }
    }
    
    void OnDisable()
    {
        ClearCurrentDisplay();
    }
    
    void OnDestroy()
    {
        ClearCache();
    }
    
    void Update()
    {
        if (!isInitialized || isUpdatingLayout)
            return;
        
        // 테스트용 런타임 업데이트
        if (Application.isPlaying && testRewardCount != lastTestCount)
        {
            if (!ValidateComponents())
            {
                LogError("필수 컴포넌트가 누락되어 테스트를 중단합니다.");
                return;
            }
            
            // 런타임에서도 Test Reward Count 제한
            int maxPrefabCount = GetTotalPrefabCount();
            if (testRewardCount > maxPrefabCount)
            {
                testRewardCount = maxPrefabCount;
                LogWarning($"Test Reward Count가 최대값({maxPrefabCount})을 초과하여 제한되었습니다.");
            }
            
            lastTestCount = testRewardCount;
            DisplayRewards(CreateTestRewards(testRewardCount));
        }
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값이 변경될 때 호출됩니다. Reward Count와 Max Items In Grid를 Element Index + 1로 자동 동기화하고, Test Reward Count를 제한합니다.
    /// </summary>
    void OnValidate()
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            if (layoutConfigs != null)
            {
                AutoSyncRewardCounts();
            }
            
            if (scaleConfigs != null)
            {
                AutoSyncScaleConfigs();
            }
            
            ValidateTestRewardCount();
        }
    }
    
    /// <summary>
    /// Test Reward Count를 등록된 프리팹 개수로 제한합니다.
    /// </summary>
    private void ValidateTestRewardCount()
    {
        if (gridPrefabData == null)
            return;
        
        int maxPrefabCount = GetTotalPrefabCount();
        
        if (testRewardCount > maxPrefabCount)
        {
            testRewardCount = maxPrefabCount;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        else if (testRewardCount < 0)
        {
            testRewardCount = 0;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    
    /// <summary>
    /// 모든 Grid에 등록된 총 프리팹 개수를 계산합니다.
    /// </summary>
    private int GetTotalPrefabCount()
    {
        if (gridPrefabData == null)
            return 0;
        
        int totalCount = 0;
        foreach (var gridData in gridPrefabData)
        {
            if (gridData?.rewardItems != null)
            {
                totalCount += GetValidPrefabCount(gridData.rewardItems);
            }
        }
        
        return totalCount;
    }
    
    /// <summary>
    /// LayoutConfig 리스트의 각 항목의 rewardCount를 Element Index + 1로 자동 설정합니다.
    /// Unity 직렬화 문제로 인한 값 변경을 방지하고 자동으로 올바른 값을 유지합니다.
    /// </summary>
    private void AutoSyncRewardCounts()
    {
        AutoSyncConfigValues(
            layoutConfigs, 
            ref previousLayoutConfigsCount, 
            (config, index) => config.rewardCount != (index + 1),
            (config, index) => config.rewardCount = index + 1
        );
    }
    
    /// <summary>
    /// ScaleConfig 리스트의 각 항목의 maxItemsInGrid를 Element Index + 1로 자동 설정합니다.
    /// Unity 직렬화 문제로 인한 값 변경을 방지하고 자동으로 올바른 값을 유지합니다.
    /// </summary>
    private void AutoSyncScaleConfigs()
    {
        AutoSyncConfigValues(
            scaleConfigs, 
            ref previousScaleConfigsCount, 
            (config, index) => config.maxItemsInGrid != (index + 1),
            (config, index) => config.maxItemsInGrid = index + 1
        );
    }
    
    /// <summary>
    /// Config 리스트의 각 항목을 Element Index + 1로 자동 설정하는 공통 메서드입니다.
    /// </summary>
    private void AutoSyncConfigValues<T>(List<T> configList, ref int previousCount, System.Func<T, int, bool> needsUpdate, System.Action<T, int> setValue) where T : class
    {
        if (configList == null)
            return;
        
        bool needsSync = false;
        
        // 리스트 크기가 변경되었거나, 각 항목의 값이 올바르지 않은 경우 동기화
        if (previousCount != configList.Count)
        {
            needsSync = true;
        }
        else
        {
            // 리스트 크기가 동일한 경우에도 각 항목의 값이 올바른지 확인
            for (int i = 0; i < configList.Count; i++)
            {
                if (configList[i] != null && needsUpdate(configList[i], i))
                {
                    needsSync = true;
                    break;
                }
            }
        }
        
        if (needsSync)
        {
            // 각 항목의 값을 Element Index + 1로 설정
            for (int i = 0; i < configList.Count; i++)
            {
                if (configList[i] != null)
                {
                    setValue(configList[i], i);
                }
            }
            
            previousCount = configList.Count;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    #endif

    #endregion

    #region Initialization
    
    /// <summary>
    /// 컴포넌트를 초기화합니다.
    /// </summary>
    private void InitializeComponent()
    {
        SafeExecute(() =>
        {
            containerRectTransform = GetComponent<RectTransform>();

            if (containerRectTransform == null)
            {
                LogError("RectTransform 컴포넌트를 찾을 수 없습니다!");
                return;
            }

            InitializeGrids();
            InitializeCache();
            InitializePreplacedPrefabs();
            
            if (autoSetupConstraintCount)
            {
                SetupGridConstraintCounts();
            }
            
            isInitialized = true;
        }, "컴포넌트 초기화");
    }
    
    /// <summary>
    /// Grid들을 초기화합니다.
    /// </summary>
    private void InitializeGrids()
    {
        SafeExecute(() =>
        {
            if (grids != null)
            {
                foreach (var grid in grids)
                {
                    if (grid != null)
                    {
                        grid.gameObject.SetActive(false);
                    }
                }
            }
        }, "Grid 초기화");
    }
    
    /// <summary>
    /// 캐시를 초기화합니다.
    /// </summary>
    private void InitializeCache()
    {
        SafeExecute(() =>
        {
            layoutConfigCache.Clear();
            scaleConfigCache.Clear();
            
            if (layoutConfigs != null)
            {
                foreach (var config in layoutConfigs)
                {
                    if (config != null)
                    {
                        layoutConfigCache[config.rewardCount] = config;
                    }
                }
            }
            
            if (scaleConfigs != null)
            {
                foreach (var config in scaleConfigs)
                {
                    if (config != null)
                    {
                        scaleConfigCache[config.maxItemsInGrid] = config;
                    }
                }
            }
        }, "캐시 초기화");
    }
    
    /// <summary>
    /// 미리 배치된 프리팹들을 초기화합니다.
    /// </summary>
    private void InitializePreplacedPrefabs()
    {
        SafeExecute(() =>
        {
            if (gridPrefabData != null)
            {
                foreach (var gridData in gridPrefabData)
                {
                    if (gridData?.rewardItems != null)
                    {
                        foreach (var prefab in gridData.rewardItems)
                        {
                            if (prefab != null)
                            {
                                prefab.SetActive(false);
                            }
                        }
                    }
                }
            }
        }, "프리팹 초기화");
    }
    

    #endregion

    #region Public Methods

    /// <summary>
    /// 보상을 표시하고 레이아웃을 업데이트합니다.
    /// </summary>
    /// <param name="rewards">지급할 보상 아이템 정보 목록</param>
    public void DisplayRewards(List<object> rewards) 
    {
        if (rewards == null)
        {
            LogWarning("rewards 리스트가 null입니다. 빈 리스트로 처리합니다.");
            rewards = new List<object>();
        }
        
        if (!ValidateComponents())
        {
            LogError("필수 컴포넌트가 누락되어 DisplayRewards를 실행할 수 없습니다.");
            return;
        }
        
        if (isUpdatingLayout)
        {
            LogWarning("이미 레이아웃 업데이트가 진행 중입니다. 요청을 무시합니다.");
            return;
        }

        StoreRewardsData(rewards);
        
        int totalRewards = rewards.Count;

        if (useFrameDistribution && totalRewards > FRAME_DISTRIBUTION_THRESHOLD)
        {
            if (layoutUpdateCoroutine != null)
            {
                StopCoroutine(layoutUpdateCoroutine);
            }
            layoutUpdateCoroutine = StartCoroutine(DisplayRewardsAsync(rewards));
        }
        else
        {
            DisplayRewardsImmediate(rewards);
        }
    }
    
    /// <summary>
    /// 저장된 보상 데이터를 수동으로 삭제합니다.
    /// </summary>
    public void ClearStoredRewardsData()
    {
        lastRewardsData = null;
        hasStoredRewards = false;
    }
    
    /// <summary>
    /// 모든 Grid의 Constraint Count를 등록된 프리팹 개수로 자동 설정합니다.
    /// 에디터에서 수동으로 호출할 수 있습니다.
    /// </summary>
    public void SetupGridConstraintCounts()
    {
        SafeExecute(() =>
        {
            if (grids == null || gridPrefabData == null)
            {
                LogWarning("Grid 또는 GridPrefabData가 설정되지 않았습니다.");
                return;
            }
            
            int setupCount = 0;
            for (int i = 0; i < grids.Count && i < gridPrefabData.Count; i++)
            {
                if (grids[i] != null && gridPrefabData[i] != null)
                {
                    int prefabCount = GetValidPrefabCount(gridPrefabData[i].rewardItems);
                    if (prefabCount > 0)
                    {
                        SetupSingleGridConstraintCount(grids[i], prefabCount, i);
                        setupCount++;
                    }
                }
            }
            
            LogInfo($"총 {setupCount}개의 Grid Constraint Count를 설정했습니다.");
        }, "Grid Constraint Count 설정");
    }

    #endregion

    #region Core Display Methods
    
    /// <summary>
    /// 보상 레이아웃의 공통 처리 로직을 수행합니다.
    /// </summary>
    private int ProcessRewardsLayout(List<object> rewards, LayoutConfig layoutConfig)
    {
        int totalRewards = rewards.Count;
        int maxItemsInAnyGrid = 0;
        
        LogInfo($"레이아웃 처리 시작 - 총 {totalRewards}개 보상");
        
        // 1단계: 모든 Grid를 한번에 활성화 (레이아웃 안정화)
        for (int i = 0; i < layoutConfig.itemsPerGrid.Count; i++)
        {
            if (i >= grids.Count)
            {
                LogError($"Grid {i}가 존재하지 않아 아이템 로드를 중단합니다.");
                break;
            }

            int itemsToLoad = layoutConfig.itemsPerGrid[i];
            GridLayoutGroup targetGrid = grids[i];

            if (targetGrid != null)
            {
                if (itemsToLoad > 0)
                {
                    // Grid만 활성화하고 아이템은 비활성화 상태로 준비
                    ActivateGridOnly(targetGrid, i, itemsToLoad);
                    maxItemsInAnyGrid = Mathf.Max(maxItemsInAnyGrid, itemsToLoad);
                }
                else
                {
                    targetGrid.gameObject.SetActive(false);
                }
            }
        }

        LogInfo($"모든 Grid 활성화 완료 - 최대 아이템 수: {maxItemsInAnyGrid}");
        
        // 2단계: 스케일 적용 (모든 Grid가 활성화된 후)
        ApplyScaleConfig(maxItemsInAnyGrid);
        
        return maxItemsInAnyGrid;
    }
    
    /// <summary>
    /// 보상 레이아웃을 즉시 업데이트합니다.
    /// </summary>
    private void DisplayRewardsImmediate(List<object> rewards)
    {
        int totalRewards = rewards.Count;
        
        if (totalRewards == 0)
        {
            ClearAllGrids();
            return;
        }

        var layoutConfig = GetLayoutConfig(totalRewards);
        if (layoutConfig == null)
        {
            LogError($"{totalRewards}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
            return;
        }

        // 공통 레이아웃 처리 로직 실행
        ProcessRewardsLayout(rewards, layoutConfig);
        
        // 3단계: RewardItem만 순차 활성화
        if (useSequentialActivation)
        {
            LogInfo("RewardItem 순차 활성화 시작");
            StartCoroutine(ActivateAllItemsSequentially(layoutConfig));
        }
    }

    /// <summary>
    /// 보상 레이아웃을 비동기로 업데이트합니다.
    /// </summary>
    private IEnumerator DisplayRewardsAsync(List<object> rewards)
    {
        isUpdatingLayout = true;
        
        try
        {
            int totalRewards = rewards.Count;
            
            if (totalRewards == 0)
            {
                ClearAllGrids();
                yield break;
            }

            var layoutConfig = GetLayoutConfig(totalRewards);
            if (layoutConfig == null)
            {
                LogError($"{totalRewards}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
                yield break;
            }

            // 공통 레이아웃 처리 로직 실행
            ProcessRewardsLayout(rewards, layoutConfig);
            
            // 3단계: RewardItem만 순차 활성화
            if (useSequentialActivation)
            {
                LogInfo("RewardItem 순차 활성화 시작");
                yield return StartCoroutine(ActivateAllItemsSequentially(layoutConfig));
            }
        }
        finally
        {
            isUpdatingLayout = false;
            layoutUpdateCoroutine = null;
        }
    }

    #endregion

    #region Grid and Item Management
    
    /// <summary>
    /// 단일 Grid의 Constraint Count를 설정합니다.
    /// Grid의 startAxis에 따라 적절한 Constraint 타입을 선택하고, 프리팹 개수를 Constraint Count로 설정합니다.
    /// </summary>
    private void SetupSingleGridConstraintCount(GridLayoutGroup grid, int prefabCount, int gridIndex)
    {
        if (grid == null)
        {
            LogError($"Grid {gridIndex}가 null입니다.");
            return;
        }
        
        try
        {
            // Grid의 startAxis에 따라 Constraint 타입 결정:
            // - Horizontal (가로 배치): FixedColumnCount 사용 → 한 행에 들어갈 열 개수 = 프리팹 개수
            // - Vertical (세로 배치): FixedRowCount 사용 → 한 열에 들어갈 행 개수 = 프리팹 개수
            if (grid.startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                // 가로 배치: 한 행에 들어갈 최대 열 개수를 프리팹 개수로 설정
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = prefabCount;
            }
            else if (grid.startAxis == GridLayoutGroup.Axis.Vertical)
            {
                // 세로 배치: 한 열에 들어갈 최대 행 개수를 프리팹 개수로 설정
                grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                grid.constraintCount = prefabCount;
            }
            else
            {
                // 기본값: Flexible로 설정되어 있으면 가로 배치로 가정
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = prefabCount;
            }
            
            LogInfo($"Grid {gridIndex} Constraint Count 설정 완료 - 프리팹 개수: {prefabCount}, Constraint: {grid.constraint}, ConstraintCount: {grid.constraintCount}");
        }
        catch (System.Exception e)
        {
            LogError($"Grid {gridIndex} Constraint Count 설정 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 리스트에서 유효한 프리팹 개수를 계산합니다 (null이 아닌 항목만 카운트).
    /// </summary>
    private int GetValidPrefabCount(List<GameObject> prefabs)
    {
        if (prefabs == null)
            return 0;
        
        int count = 0;
        foreach (var prefab in prefabs)
        {
            if (prefab != null)
            {
                count++;
            }
        }
        return count;
    }
    
    /// <summary>
    /// Grid만 활성화하고 아이템은 비활성화 상태로 준비합니다. (레이아웃 안정화용)
    /// </summary>
    private void ActivateGridOnly(GridLayoutGroup targetGrid, int gridIndex, int itemsToLoad)
    {
        targetGrid.gameObject.SetActive(true);
        
        if (useGridTransformReset)
        {
            ResetGridTransform(targetGrid);
        }
        
        // 아이템들을 비활성화 상태로 준비 (순차 활성화에서 사용할 예정)
        PrepareGridForSequentialActivation(gridIndex, itemsToLoad);
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(targetGrid.GetComponent<RectTransform>());
        
        LogInfo($"Grid {gridIndex} 활성화 완료 (아이템 {itemsToLoad}개 준비됨)");
    }
    
    /// <summary>
    /// 아이템 리스트를 활성화/비활성화합니다.
    /// </summary>
    private void SetItemsActive(List<GameObject> items, bool active)
    {
        if (items == null) return;
        
        foreach (var item in items)
        {
            if (item != null)
            {
                item.SetActive(active);
                if (active)
                {
                    ValidateAndFixItemTransform(item);
                }
            }
        }
    }
    
    /// <summary>
    /// Grid의 모든 아이템을 활성화/비활성화합니다.
    /// </summary>
    private void SetGridItemsActive(int gridIndex, bool active, int count = -1)
    {
        var gridData = GetGridData(gridIndex);
        if (gridData == null) return;
        
        int processedCount = 0;
        foreach (var item in gridData.rewardItems)
        {
            if (item != null)
            {
                item.SetActive(active);
                if (active)
                {
                    ValidateAndFixItemTransform(item);
                }
                processedCount++;
                
                if (count > 0 && processedCount >= count)
                    break;
            }
        }
    }

    /// <summary>
    /// Grid를 순차 활성화를 위해 준비합니다.
    /// </summary>
    private void PrepareGridForSequentialActivation(int gridIndex, int count)
    {
        SetGridItemsActive(gridIndex, false, count);
        LogInfo($"Grid {gridIndex}에서 {count}개의 프리팹을 순차 활성화를 위해 준비했습니다.");
    }
    
    /// <summary>
    /// 순차 활성화를 위한 아이템들을 수집합니다.
    /// </summary>
    private List<GameObject> CollectItemsForSequentialActivation(LayoutConfig layoutConfig)
    {
        List<GameObject> allItemsToActivate = new List<GameObject>();
        
        for (int gridIndex = 0; gridIndex < layoutConfig.itemsPerGrid.Count; gridIndex++)
        {
            int itemsToLoad = layoutConfig.itemsPerGrid[gridIndex];
            
            if (itemsToLoad > 0 && gridIndex < gridPrefabData.Count && gridPrefabData[gridIndex] != null)
            {
                var gridData = gridPrefabData[gridIndex];
                int addedCount = 0;
                
                for (int i = 0; i < gridData.rewardItems.Count && addedCount < itemsToLoad; i++)
                {
                    if (gridData.rewardItems[i] != null)
                    {
                        allItemsToActivate.Add(gridData.rewardItems[i]);
                        addedCount++;
                    }
                }
            }
        }
        
        return allItemsToActivate;
    }
    
    /// <summary>
    /// 아이템들을 순차적으로 활성화합니다.
    /// </summary>
    private IEnumerator ActivateItemsSequentially(List<GameObject> itemsToActivate)
    {
        if (itemsToActivate.Count == 0)
        {
            LogWarning("활성화할 아이템이 없습니다.");
            yield break;
        }
        
        LogInfo($"총 {itemsToActivate.Count}개의 아이템을 순차적으로 활성화합니다.");
        
        float intervalBetweenItems = CalculateItemInterval(itemsToActivate.Count);
        LogInfo($"아이템 간격 {intervalBetweenItems:F3}초로 설정 (총 예상 시간: {intervalBetweenItems * (itemsToActivate.Count - 1):F2}초)");
        
        for (int i = 0; i < itemsToActivate.Count; i++)
        {
            if (itemsToActivate[i] != null)
            {
                itemsToActivate[i].SetActive(true);
                ValidateAndFixItemTransform(itemsToActivate[i]);
                
                LogInfo($"아이템 {i + 1}/{itemsToActivate.Count} 활성화 완료");
                
                if (i < itemsToActivate.Count - 1)
                {
                    yield return new WaitForSeconds(intervalBetweenItems);
                }
            }
        }
        
        LogInfo("모든 아이템의 순차 활성화가 완료되었습니다.");
    }
    
    /// <summary>
    /// 모든 아이템들을 하나의 시퀀스로 순차 활성화합니다.
    /// </summary>
    private IEnumerator ActivateAllItemsSequentially(LayoutConfig layoutConfig)
    {
        if (layoutConfig == null)
        {
            LogError("LayoutConfig가 null입니다.");
            yield break;
        }
        
        List<GameObject> allItemsToActivate = CollectItemsForSequentialActivation(layoutConfig);
        yield return StartCoroutine(ActivateItemsSequentially(allItemsToActivate));
    }
    
    
    /// <summary>
    /// 지정된 Grid에 미리 배치된 프리팹들을 즉시 활성화합니다.
    /// </summary>
    private void ActivatePreplacedItems(int gridIndex, int count)
    {
        SetGridItemsActive(gridIndex, true, count);
        LogInfo($"Grid {gridIndex}에서 {count}개의 프리팹을 활성화했습니다.");
    }
    
    /// <summary>
    /// 모든 Grid의 아이템들을 비활성화합니다.
    /// </summary>
    private void DeactivateAllItems()
    {
        SafeExecute(() =>
        {
            if (gridPrefabData != null)
            {
                foreach (var gridData in gridPrefabData)
                {
                    if (gridData?.rewardItems != null)
                    {
                        foreach (var prefab in gridData.rewardItems)
                        {
                            if (prefab != null)
                            {
                                prefab.SetActive(false);
                            }
                        }
                    }
                }
            }
        }, "아이템 비활성화");
    }
    
    /// <summary>
    /// 모든 Grid를 비활성화합니다.
    /// </summary>
    private void DeactivateAllGrids()
    {
        SafeExecute(() =>
        {
            if (grids != null)
            {
                for (int i = 0; i < grids.Count; i++)
                {
                    if (grids[i] != null)
                    {
                        grids[i].gameObject.SetActive(false);
                    }
                }
            }
        }, "Grid 비활성화");
    }
    
    /// <summary>
    /// 모든 Grid의 미리 배치된 프리팹들을 비활성화합니다.
    /// </summary>
    private void ClearAllGrids()
    {
        DeactivateAllItems();
        DeactivateAllGrids();
    }

    #endregion

    #region Transform Management
    
    /// <summary>
    /// Grid의 Transform을 초기화합니다.
    /// </summary>
    private void ResetGridTransform(GridLayoutGroup grid)
    {
        try
        {
            RectTransform gridRectTransform = grid.GetComponent<RectTransform>();
            if (gridRectTransform != null)
            {
                ValidateAndFixTransform(gridRectTransform, $"Grid {grid.name}");
            }
        }
        catch (System.Exception e)
        {
            LogError($"Grid Transform 초기화 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 아이템의 Transform을 검증하고 수정합니다.
    /// </summary>
    private void ValidateAndFixItemTransform(GameObject item)
    {
        try
        {
            RectTransform rectTransform = item.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                ValidateAndFixTransform(rectTransform, $"Item {item.name}");
            }
        }
        catch (System.Exception e)
        {
            LogError($"아이템 Transform 검증 중 오류 발생: {e.Message}");
        }
    }

    #endregion

    #region Configuration and Calculation
    
    /// <summary>
    /// 아이템 개수에 따른 적절한 아이템 간격을 계산합니다.
    /// </summary>
    private float CalculateItemInterval(int itemCount)
    {
        if (itemCount <= 0)
        {
            return baseItemInterval;
        }
        
        if (itemCount <= 3)
        {
            return baseItemInterval;
        }
        
        float weightFactor = (itemCount - 3) * countWeightMultiplier / itemCount;
        float calculatedInterval = baseItemInterval * (1 - weightFactor);
        
        calculatedInterval = Mathf.Clamp(calculatedInterval, MIN_INTERVAL_TIME, maxItemInterval);
        
        LogInfo($"아이템 {itemCount}개에 대한 간격 계산 - 기본: {baseItemInterval}, 계산된 간격: {calculatedInterval:F3}");
        
        return calculatedInterval;
    }
    
    /// <summary>
    /// 보상 개수에 맞는 레이아웃 설정을 가져옵니다.
    /// </summary>
    private LayoutConfig GetLayoutConfig(int rewardCount)
    {
        if (layoutConfigCache.TryGetValue(rewardCount, out LayoutConfig config))
        {
            return config;
        }
        
        LogError($"{rewardCount}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
        return null;
    }
    
    /// <summary>
    /// Grid 내 최대 아이템 개수에 맞는 스케일 설정을 가져옵니다.
    /// </summary>
    private ScaleConfig GetScaleConfig(int maxItemsInGrid)
    {
        if (scaleConfigCache.TryGetValue(maxItemsInGrid, out ScaleConfig config))
        {
            return config;
        }
        
        LogError($"{maxItemsInGrid}개 아이템에 대한 스케일 설정을 찾을 수 없습니다.");
        return null;
    }
    
    /// <summary>
    /// 컨테이너 스케일을 적용합니다.
    /// </summary>
    private void ApplyScaleConfig(int maxItemsInGrid)
    {
        var scaleConfig = GetScaleConfig(maxItemsInGrid);
        if (scaleConfig != null && containerRectTransform != null)
        {
            containerRectTransform.localScale = Vector3.one * scaleConfig.containerScale;
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRectTransform);
            
            LogInfo($"컨테이너 스케일을 {scaleConfig.containerScale}로 설정했습니다.");
        }
    }

    #endregion

    #region Utility Methods
    
    /// <summary>
    /// 일관된 정보 로그를 출력합니다.
    /// </summary>
    private void LogInfo(string message)
    {
        Debug.Log($"RewardLayoutController: {message}");
    }
    
    /// <summary>
    /// 일관된 경고 로그를 출력합니다.
    /// </summary>
    private void LogWarning(string message)
    {
        Debug.LogWarning($"RewardLayoutController: {message}");
    }
    
    /// <summary>
    /// 일관된 에러 로그를 출력합니다.
    /// </summary>
    private void LogError(string message)
    {
        Debug.LogError($"RewardLayoutController: {message}");
    }
    
    /// <summary>
    /// Grid 프리팹 데이터를 검증하고 안전하게 가져옵니다.
    /// </summary>
    private GridPrefabData GetGridData(int gridIndex)
    {
        if (gridPrefabData == null || gridIndex >= gridPrefabData.Count)
        {
            LogError($"Grid {gridIndex}의 미리 배치된 프리팹을 찾을 수 없습니다. (gridPrefabData.Count: {gridPrefabData?.Count ?? 0})");
            return null;
        }
        
        var gridData = gridPrefabData[gridIndex];
        if (gridData?.rewardItems == null)
        {
            LogError($"Grid {gridIndex}의 프리팹 리스트가 null입니다.");
            return null;
        }
        
        return gridData;
    }
    
    /// <summary>
    /// RectTransform을 검증하고 수정합니다.
    /// </summary>
    private void ValidateAndFixTransform(RectTransform rectTransform, string objectName)
    {
        try
        {
            if (rectTransform == null) return;
            
            bool needsFix = false;
            string fixDetails = "";
            
            // 스케일 검증 및 수정
            Vector3 originalScale = rectTransform.localScale;
            if (originalScale.magnitude > SCALE_TOLERANCE_MAX || originalScale.magnitude < SCALE_TOLERANCE_MIN)
            {
                rectTransform.localScale = Vector3.one;
                needsFix = true;
                fixDetails += $"Scale: {originalScale} → {Vector3.one} ";
            }
            
            // Z 위치 검증 및 수정
            Vector3 originalPosition = rectTransform.localPosition;
            if (Mathf.Abs(originalPosition.z) > POSITION_Z_TOLERANCE)
            {
                Vector3 newPosition = originalPosition;
                newPosition.z = 0f;
                rectTransform.localPosition = newPosition;
                needsFix = true;
                fixDetails += $"Position Z: {originalPosition.z} → 0 ";
            }
            
            // SizeDelta 검증 및 수정 (Grid에만 적용)
            if (objectName.StartsWith("Grid"))
            {
                Vector2 originalSizeDelta = rectTransform.sizeDelta;
                if (originalSizeDelta.magnitude > SIZE_DELTA_TOLERANCE)
                {
                    rectTransform.sizeDelta = Vector2.zero;
                    needsFix = true;
                    fixDetails += $"SizeDelta: {originalSizeDelta} → {Vector2.zero} ";
                }
            }
            
            if (needsFix)
            {
                LogWarning($"{objectName} Transform 수정됨 - {fixDetails.Trim()}");
            }
        }
        catch (System.Exception e)
        {
            LogError($"{objectName} Transform 검증 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 안전한 실행을 위한 예외 처리 래퍼입니다.
    /// </summary>
    private void SafeExecute(System.Action action, string operationName)
    {
        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            LogError($"{operationName} 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 안전한 실행을 위한 예외 처리 래퍼입니다. (반환값 있음)
    /// </summary>
    private T SafeExecute<T>(System.Func<T> func, string operationName, T defaultValue = default(T))
    {
        try
        {
            return func != null ? func.Invoke() : defaultValue;
        }
        catch (System.Exception e)
        {
            LogError($"{operationName} 중 오류 발생: {e.Message}");
            return defaultValue;
        }
    }
    
    /// <summary>
    /// 필수 컴포넌트들이 올바르게 설정되어 있는지 검사합니다.
    /// </summary>
    private bool ValidateComponents()
    {
        if (containerRectTransform == null)
        {
            LogError("containerRectTransform이 null입니다!");
            return false;
        }
        
        if (grids == null || grids.Count == 0)
        {
            LogError("grids 리스트가 비어있거나 null입니다!");
            return false;
        }
        
        if (gridPrefabData == null || gridPrefabData.Count == 0)
        {
            LogError("gridPrefabData가 설정되지 않았습니다! Inspector에서 프리팹을 등록해주세요.");
            return false;
        }
        
        bool hasValidGrid = false;
        for (int i = 0; i < grids.Count; i++)
        {
            if (grids[i] != null)
            {
                hasValidGrid = true;
                break;
            }
        }
        
        if (!hasValidGrid)
        {
            LogError("유효한 Grid가 하나도 없습니다!");
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// 테스트용 보상 데이터를 생성합니다.
    /// </summary>
    private List<object> CreateTestRewards(int count)
    {
        List<object> testRewards = new List<object>(count);
        for (int i = 0; i < count; i++)
        {
            testRewards.Add(null); 
        }
        return testRewards;
    }
    
    /// <summary>
    /// 팝업 재사용을 위한 현재 표시 상태를 정리합니다.
    /// </summary>
    private void ClearCurrentDisplay()
    {
        try
        {
            ClearAllGrids();
            
            if (containerRectTransform != null)
            {
                containerRectTransform.localScale = Vector3.one;
            }
            
            LogInfo("현재 표시 상태가 정리되었습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 표시 상태 정리 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 보상 데이터를 저장합니다.
    /// </summary>
    private void StoreRewardsData(List<object> rewards)
    {
        try
        {
            if (rewards != null && rewards.Count > 0)
            {
                lastRewardsData = new List<object>(rewards);
                hasStoredRewards = true;
                LogInfo($"{rewards.Count}개의 보상 데이터를 저장했습니다.");
            }
            else
            {
                lastRewardsData = null;
                hasStoredRewards = false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 보상 데이터 저장 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 팝업 재활성화 후 표시를 복원합니다.
    /// </summary>
    private IEnumerator RestoreDisplayAfterEnable()
    {
        yield return null; // 한 프레임 대기하여 팝업이 완전히 활성화되도록 함
        
        if (lastRewardsData != null)
        {
            LogInfo("저장된 보상 데이터로 표시를 복원합니다.");
            DisplayRewards(lastRewardsData);
        }
    }

    /// <summary>
    /// 캐시를 정리합니다.
    /// </summary>
    private void ClearCache()
    {
        try
        {
            layoutConfigCache?.Clear();
            scaleConfigCache?.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 캐시 정리 중 오류 발생: {e.Message}");
        }
    }

    #endregion
}