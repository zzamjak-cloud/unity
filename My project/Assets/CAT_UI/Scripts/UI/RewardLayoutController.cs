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
    [Tooltip("테스트를 위한 보상 개수 (런타임에 변경하여 즉시 테스트 가능)")]
    [Range(0, 20)]
    [SerializeField] private int testRewardCount = 0;

    [Header("Grid 및 프리팹 설정")]
    [Tooltip("Reward Container의 자식 Grid Layout Group들")]
    [SerializeField] private List<GridLayoutGroup> grids = new List<GridLayoutGroup>();
    
    [Tooltip("각 Grid에 미리 배치된 RewardItem 프리팹들")]
    [SerializeField] private List<List<GameObject>> preplacedRewardItems = new List<List<GameObject>>();
    
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

    #endregion

    #region Serializable Classes
    
    [Serializable]
    public class LayoutConfig
    {
        [Tooltip("이 설정이 적용될 총 보상 개수")]
        public int rewardCount;
        
        [Tooltip("각 Grid에 들어갈 아이템 개수 목록")]
        public List<int> itemsPerGrid; 
    }
    
    [Serializable]
    public class ScaleConfig
    {
        [Tooltip("이 스케일이 적용될 Grid 내 최대 아이템 개수")]
        public int maxItemsInGrid;
        
        [Tooltip("Reward Container의 Uniform Scale 값")]
        [Range(0.1f, 2.0f)]
        public float containerScale = 1.0f;
    }

    #endregion

    #region Private Fields
    
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
                Debug.LogError("RewardLayoutController: 필수 컴포넌트가 누락되어 테스트를 중단합니다.");
                return;
            }
            
            lastTestCount = testRewardCount;
            DisplayRewards(CreateTestRewards(testRewardCount));
        }
    }

    #endregion

    #region Initialization
    
    /// <summary>
    /// 컴포넌트를 초기화합니다.
    /// </summary>
    private void InitializeComponent()
    {
        try
        {
            containerRectTransform = GetComponent<RectTransform>();
            
            if (containerRectTransform == null)
            {
                Debug.LogError("RewardLayoutController: RectTransform 컴포넌트를 찾을 수 없습니다!");
                return;
            }

            InitializeGrids();
            InitializeCache();
            InitializePreplacedPrefabs();
            
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController 초기화 중 오류 발생: {e.Message}");
            isInitialized = false;
        }
    }
    
    /// <summary>
    /// Grid들을 초기화합니다.
    /// </summary>
    private void InitializeGrids()
    {
        try
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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: Grid 초기화 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 캐시를 초기화합니다.
    /// </summary>
    private void InitializeCache()
    {
        try
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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 캐시 초기화 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// 미리 배치된 프리팹들을 초기화합니다.
    /// </summary>
    private void InitializePreplacedPrefabs()
    {
        try
        {
            // Inspector에서 설정되지 않은 경우 자동으로 찾기
            if (preplacedRewardItems == null || preplacedRewardItems.Count == 0)
            {
                Debug.LogWarning("RewardLayoutController: Inspector에서 preplacedRewardItems가 설정되지 않았습니다. 자동으로 찾는 중...");
                AutoFindPreplacedPrefabs();
            }
            
            if (preplacedRewardItems != null)
            {
                foreach (var gridPrefabs in preplacedRewardItems)
                {
                    if (gridPrefabs != null)
                    {
                        foreach (var prefab in gridPrefabs)
                        {
                            if (prefab != null)
                            {
                                prefab.SetActive(false);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 프리팹 초기화 중 오류 발생: {e.Message}");
        }
    }
    
    /// <summary>
    /// Grid들에서 미리 배치된 프리팹들을 자동으로 찾아서 설정합니다.
    /// </summary>
    private void AutoFindPreplacedPrefabs()
    {
        try
        {
            preplacedRewardItems = new List<List<GameObject>>();
            
            if (grids != null)
            {
                for (int i = 0; i < grids.Count; i++)
                {
                    var grid = grids[i];
                    if (grid != null)
                    {
                        List<GameObject> gridPrefabs = new List<GameObject>();
                        
                        // Grid의 자식 오브젝트들을 찾아서 RewardItem 프리팹으로 간주
                        for (int j = 0; j < grid.transform.childCount; j++)
                        {
                            Transform child = grid.transform.GetChild(j);
                            if (child != null && child.gameObject.name.Contains("RewardItem"))
                            {
                                gridPrefabs.Add(child.gameObject);
                            }
                        }
                        
                        preplacedRewardItems.Add(gridPrefabs);
                        Debug.Log($"RewardLayoutController: Grid {i}에서 {gridPrefabs.Count}개의 RewardItem 프리팹을 자동으로 찾았습니다.");
                    }
                    else
                    {
                        preplacedRewardItems.Add(new List<GameObject>());
                    }
                }
            }
            
            Debug.Log($"RewardLayoutController: 총 {preplacedRewardItems.Count}개의 Grid에서 미리 배치된 프리팹들을 자동으로 찾았습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 자동 프리팹 찾기 중 오류 발생: {e.Message}");
        }
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
            Debug.LogWarning("RewardLayoutController: rewards 리스트가 null입니다. 빈 리스트로 처리합니다.");
            rewards = new List<object>();
        }
        
        if (!ValidateComponents())
        {
            Debug.LogError("RewardLayoutController: 필수 컴포넌트가 누락되어 DisplayRewards를 실행할 수 없습니다.");
            return;
        }
        
        if (isUpdatingLayout)
        {
            Debug.LogWarning("RewardLayoutController: 이미 레이아웃 업데이트가 진행 중입니다. 요청을 무시합니다.");
            return;
        }
        
        StoreRewardsData(rewards);
        
        int totalRewards = rewards.Count;
        
        if (useFrameDistribution && totalRewards > 10)
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

    #endregion

    #region Core Display Methods
    
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
            Debug.LogError($"RewardLayoutController: {totalRewards}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
            return;
        }

        int maxItemsInAnyGrid = 0;
        
        Debug.Log($"RewardLayoutController: 즉시 처리 시작 - 총 {totalRewards}개 보상");
        
        // 1단계: 모든 Grid를 한번에 활성화 (레이아웃 안정화)
        for (int i = 0; i < layoutConfig.itemsPerGrid.Count; i++)
        {
            if (i >= grids.Count)
            {
                Debug.LogError($"Grid {i}가 존재하지 않아 아이템 로드를 중단합니다.");
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

        Debug.Log($"RewardLayoutController: 모든 Grid 활성화 완료 - 최대 아이템 수: {maxItemsInAnyGrid}");
        
        // 2단계: 스케일 적용 (모든 Grid가 활성화된 후)
        ApplyScaleConfig(maxItemsInAnyGrid);
        
        // 3단계: RewardItem만 순차 활성화
        if (useSequentialActivation)
        {
            Debug.Log("RewardLayoutController: RewardItem 순차 활성화 시작");
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
                Debug.LogError($"RewardLayoutController: {totalRewards}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
                yield break;
            }

            int maxItemsInAnyGrid = 0;
            
            Debug.Log($"RewardLayoutController: 비동기 처리 시작 - 총 {totalRewards}개 보상");
            
            // 1단계: 모든 Grid를 한번에 활성화 (레이아웃 안정화)
            for (int i = 0; i < layoutConfig.itemsPerGrid.Count; i++)
            {
                if (i >= grids.Count)
                {
                    Debug.LogError($"Grid {i}가 존재하지 않아 아이템 로드를 중단합니다.");
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

            Debug.Log($"RewardLayoutController: 모든 Grid 활성화 완료 - 최대 아이템 수: {maxItemsInAnyGrid}");
            
            // 2단계: 스케일 적용 (모든 Grid가 활성화된 후)
            ApplyScaleConfig(maxItemsInAnyGrid);
            
            // 3단계: RewardItem만 순차 활성화
            if (useSequentialActivation)
            {
                Debug.Log("RewardLayoutController: RewardItem 순차 활성화 시작");
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
        
        Debug.Log($"RewardLayoutController: Grid {gridIndex} 활성화 완료 (아이템 {itemsToLoad}개 준비됨)");
    }
    
    /// <summary>
    /// Grid를 설정하고 아이템을 준비합니다. (즉시 활성화용)
    /// </summary>
    private void SetupGrid(GridLayoutGroup targetGrid, int gridIndex, int itemsToLoad)
    {
        targetGrid.gameObject.SetActive(true);
        
        if (useGridTransformReset)
        {
            ResetGridTransform(targetGrid);
        }
        
        if (useSequentialActivation)
        {
            PrepareGridForSequentialActivation(gridIndex, itemsToLoad);
        }
        else
        {
            ActivatePreplacedItems(gridIndex, itemsToLoad);
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(targetGrid.GetComponent<RectTransform>());
    }
    
    /// <summary>
    /// Grid를 순차 활성화를 위해 준비합니다.
    /// </summary>
    private void PrepareGridForSequentialActivation(int gridIndex, int count)
    {
        // preplacedRewardItems가 초기화되지 않은 경우 다시 초기화 시도
        if (preplacedRewardItems == null || preplacedRewardItems.Count == 0)
        {
            Debug.LogWarning("RewardLayoutController: preplacedRewardItems가 초기화되지 않았습니다. 다시 초기화를 시도합니다.");
            AutoFindPreplacedPrefabs();
        }
        
        if (preplacedRewardItems == null || gridIndex >= preplacedRewardItems.Count)
        {
            Debug.LogError($"RewardLayoutController: Grid {gridIndex}의 미리 배치된 프리팹을 찾을 수 없습니다. (preplacedRewardItems.Count: {preplacedRewardItems?.Count ?? 0})");
            return;
        }
        
        var gridPrefabs = preplacedRewardItems[gridIndex];
        if (gridPrefabs == null)
        {
            Debug.LogError($"RewardLayoutController: Grid {gridIndex}의 프리팹 리스트가 null입니다.");
            return;
        }
        
        int preparedCount = 0;
        for (int i = 0; i < gridPrefabs.Count && preparedCount < count; i++)
        {
            if (gridPrefabs[i] != null)
            {
                gridPrefabs[i].SetActive(false);
                preparedCount++;
            }
        }
        
        Debug.Log($"RewardLayoutController: Grid {gridIndex}에서 {preparedCount}개의 프리팹을 순차 활성화를 위해 준비했습니다.");
    }
    
    /// <summary>
    /// 모든 아이템들을 하나의 시퀀스로 순차 활성화합니다.
    /// </summary>
    private IEnumerator ActivateAllItemsSequentially(LayoutConfig layoutConfig)
    {
        if (layoutConfig == null)
        {
            Debug.LogError("RewardLayoutController: LayoutConfig가 null입니다.");
            yield break;
        }
        
        List<GameObject> allItemsToActivate = CollectAllItemsToActivate(layoutConfig);
        
        if (allItemsToActivate.Count == 0)
        {
            Debug.LogWarning("RewardLayoutController: 활성화할 아이템이 없습니다.");
            yield break;
        }
        
        Debug.Log($"RewardLayoutController: 총 {allItemsToActivate.Count}개의 아이템을 순차적으로 활성화합니다.");
        
        float intervalBetweenItems = CalculateItemInterval(allItemsToActivate.Count);
        
        Debug.Log($"RewardLayoutController: 아이템 간격 {intervalBetweenItems:F3}초로 설정 (총 예상 시간: {intervalBetweenItems * (allItemsToActivate.Count - 1):F2}초)");
        
        for (int i = 0; i < allItemsToActivate.Count; i++)
        {
            if (allItemsToActivate[i] != null)
            {
                allItemsToActivate[i].SetActive(true);
                ValidateAndFixItemTransform(allItemsToActivate[i]);
                
                Debug.Log($"RewardLayoutController: 아이템 {i + 1}/{allItemsToActivate.Count} 활성화 완료");
                
                if (i < allItemsToActivate.Count - 1)
                {
                    yield return new WaitForSeconds(intervalBetweenItems);
                }
            }
        }
        
        Debug.Log($"RewardLayoutController: 모든 아이템의 순차 활성화가 완료되었습니다.");
    }
    
    /// <summary>
    /// 활성화할 모든 아이템들을 수집합니다.
    /// </summary>
    private List<GameObject> CollectAllItemsToActivate(LayoutConfig layoutConfig)
    {
        List<GameObject> allItemsToActivate = new List<GameObject>();
        
        for (int gridIndex = 0; gridIndex < layoutConfig.itemsPerGrid.Count; gridIndex++)
        {
            int itemsToLoad = layoutConfig.itemsPerGrid[gridIndex];
            
            if (itemsToLoad > 0 && gridIndex < preplacedRewardItems.Count && preplacedRewardItems[gridIndex] != null)
            {
                var gridPrefabs = preplacedRewardItems[gridIndex];
                int addedCount = 0;
                
                for (int i = 0; i < gridPrefabs.Count && addedCount < itemsToLoad; i++)
                {
                    if (gridPrefabs[i] != null)
                    {
                        allItemsToActivate.Add(gridPrefabs[i]);
                        addedCount++;
                    }
                }
            }
        }
        
        return allItemsToActivate;
    }
    
    /// <summary>
    /// 지정된 Grid에 미리 배치된 프리팹들을 즉시 활성화합니다.
    /// </summary>
    private void ActivatePreplacedItems(int gridIndex, int count)
    {
        // preplacedRewardItems가 초기화되지 않은 경우 다시 초기화 시도
        if (preplacedRewardItems == null || preplacedRewardItems.Count == 0)
        {
            Debug.LogWarning("RewardLayoutController: preplacedRewardItems가 초기화되지 않았습니다. 다시 초기화를 시도합니다.");
            AutoFindPreplacedPrefabs();
        }
        
        if (preplacedRewardItems == null || gridIndex >= preplacedRewardItems.Count)
        {
            Debug.LogError($"RewardLayoutController: Grid {gridIndex}의 미리 배치된 프리팹을 찾을 수 없습니다. (preplacedRewardItems.Count: {preplacedRewardItems?.Count ?? 0})");
            return;
        }
        
        var gridPrefabs = preplacedRewardItems[gridIndex];
        if (gridPrefabs == null)
        {
            Debug.LogError($"RewardLayoutController: Grid {gridIndex}의 프리팹 리스트가 null입니다.");
            return;
        }
        
        int activatedCount = 0;
        for (int i = 0; i < gridPrefabs.Count && activatedCount < count; i++)
        {
            if (gridPrefabs[i] != null)
            {
                gridPrefabs[i].SetActive(true);
                ValidateAndFixItemTransform(gridPrefabs[i]);
                activatedCount++;
            }
        }
        
        Debug.Log($"RewardLayoutController: Grid {gridIndex}에서 {activatedCount}개의 프리팹을 활성화했습니다.");
    }
    
    /// <summary>
    /// 모든 Grid의 미리 배치된 프리팹들을 비활성화합니다.
    /// </summary>
    private void ClearAllGrids()
    {
        try
        {
            if (preplacedRewardItems != null)
            {
                for (int gridIndex = 0; gridIndex < preplacedRewardItems.Count; gridIndex++)
                {
                    var gridPrefabs = preplacedRewardItems[gridIndex];
                    if (gridPrefabs != null)
                    {
                        foreach (var prefab in gridPrefabs)
                        {
                            if (prefab != null)
                            {
                                prefab.SetActive(false);
                            }
                        }
                    }
                    
                    if (gridIndex < grids.Count && grids[gridIndex] != null)
                    {
                        grids[gridIndex].gameObject.SetActive(false);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: Grid 정리 중 오류 발생: {e.Message}");
        }
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
                bool needsReset = false;
                
                if (gridRectTransform.localScale.magnitude > 1.1f || gridRectTransform.localScale.magnitude < 0.9f)
                {
                    gridRectTransform.localScale = Vector3.one;
                    needsReset = true;
                }
                
                if (Mathf.Abs(gridRectTransform.localPosition.z) > 0.1f)
                {
                    Vector3 pos = gridRectTransform.localPosition;
                    pos.z = 0f;
                    gridRectTransform.localPosition = pos;
                    needsReset = true;
                }
                
                if (gridRectTransform.sizeDelta.magnitude > 1000f)
                {
                    gridRectTransform.sizeDelta = Vector2.zero;
                    needsReset = true;
                }
                
                if (needsReset)
                {
                    Debug.LogWarning($"RewardLayoutController: Grid Transform 수정됨 - Scale: {gridRectTransform.localScale}, Position: {gridRectTransform.localPosition}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: Grid Transform 초기화 중 오류 발생: {e.Message}");
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
                bool needsFix = false;
                
                if (rectTransform.localScale.magnitude > 1.1f || rectTransform.localScale.magnitude < 0.9f)
                {
                    rectTransform.localScale = Vector3.one;
                    needsFix = true;
                }
                
                if (Mathf.Abs(rectTransform.localPosition.z) > 0.1f)
                {
                    Vector3 pos = rectTransform.localPosition;
                    pos.z = 0f;
                    rectTransform.localPosition = pos;
                    needsFix = true;
                }
                
                if (needsFix)
                {
                    Debug.LogWarning($"RewardLayoutController: 아이템 Transform 수정됨 - Scale: {rectTransform.localScale}, Position: {rectTransform.localPosition}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: 아이템 Transform 검증 중 오류 발생: {e.Message}");
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
        
        calculatedInterval = Mathf.Clamp(calculatedInterval, 0.05f, maxItemInterval);
        
        Debug.Log($"RewardLayoutController: 아이템 {itemCount}개에 대한 간격 계산 - 기본: {baseItemInterval}, 계산된 간격: {calculatedInterval:F3}");
        
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
        
        Debug.LogError($"RewardLayoutController: {rewardCount}개 보상에 대한 레이아웃 설정을 찾을 수 없습니다.");
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
        
        Debug.LogError($"RewardLayoutController: {maxItemsInGrid}개 아이템에 대한 스케일 설정을 찾을 수 없습니다.");
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
            
            Debug.Log($"RewardLayoutController: 컨테이너 스케일을 {scaleConfig.containerScale}로 설정했습니다.");
        }
    }

    #endregion

    #region Utility Methods
    
    /// <summary>
    /// 필수 컴포넌트들이 올바르게 설정되어 있는지 검사합니다.
    /// </summary>
    private bool ValidateComponents()
    {
        if (containerRectTransform == null)
        {
            Debug.LogError("RewardLayoutController: containerRectTransform이 null입니다!");
            return false;
        }
        
        if (grids == null || grids.Count == 0)
        {
            Debug.LogError("RewardLayoutController: grids 리스트가 비어있거나 null입니다!");
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
            Debug.LogError("RewardLayoutController: 유효한 Grid가 하나도 없습니다!");
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
            
            Debug.Log("RewardLayoutController: 현재 표시 상태가 정리되었습니다.");
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
                Debug.Log($"RewardLayoutController: {rewards.Count}개의 보상 데이터를 저장했습니다.");
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
            Debug.Log("RewardLayoutController: 저장된 보상 데이터로 표시를 복원합니다.");
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