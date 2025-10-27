using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class RewardLayoutController : MonoBehaviour
{
    // ===================================================================================================
    // ✨ 인스펙터 필드 설정 (Inspector Settings)
    // ===================================================================================================
    
    [Header("테스트 및 개수 설정")]
    [Tooltip("테스트를 위한 보상 개수입니다. 런타임에 변경하여 즉시 테스트할 수 있습니다. (실제 사용 시에는 DisplayRewards 함수를 직접 호출)")]
    [Range(0, 20)]
    [SerializeField] private int maxRewardCountForTest = 0;

    [Header("프리팹 및 컨테이너")]
    [SerializeField] private GameObject rewardItemPrefab; // 보상 아이템 프리팹
    
    [Tooltip("Reward Container의 자식 Grid Layout Group들 (Grid 0, Grid 1, ...)을 순서대로 연결")]
    [SerializeField] private List<GridLayoutGroup> grids = new List<GridLayoutGroup>();

    [Header("레이아웃 설정")]
    [Tooltip("총 보상 개수에 따른 레이아웃 및 스케일 설정 목록")]
    [SerializeField] private List<LayoutConfig> layoutConfigs = new List<LayoutConfig>();


    // ===================================================================================================
    // ✨ 직렬화 클래스 (Serializable Classes)
    // ===================================================================================================
    
    [Serializable]
    public class LayoutConfig
    {
        public int rewardCount; // 이 설정이 적용될 총 보상 개수
        
        [Tooltip("각 Grid에 들어갈 아이템 개수 목록 (Grid 0, Grid 1, ...)")]
        public List<int> itemsPerGrid; 
        
        [Tooltip("Reward Container의 최종 RectTransform.localScale")]
        public Vector3 containerScale = Vector3.one; 
    }


    // ===================================================================================================
    // ✨ 내부 변수 및 Unity 라이프사이클 (Internal Variables & Lifecycle)
    // ===================================================================================================
    
    private RectTransform containerRectTransform;
    private int lastTestCount = -1; // 이전 테스트 개수를 저장하여 변경 시에만 업데이트
    private bool isInitialized = false; // 초기화 완료 여부

    void Awake()
    {
        InitializeComponent();
    }
    
    void OnEnable()
    {
        // OnEnable에서도 초기화를 시도 (Inspector 문제 해결을 위해)
        if (!isInitialized)
        {
            InitializeComponent();
        }
    }
    
    void OnDisable()
    {
        // OnDisable에서 안전하게 정리
        isInitialized = false;
    }
    
    /// <summary>
    /// 컴포넌트 초기화를 안전하게 처리합니다.
    /// </summary>
    private void InitializeComponent()
    {
        try
        {
            if (containerRectTransform == null)
            {
                containerRectTransform = GetComponent<RectTransform>();
            }
            
            // 안전성 검사: 필수 컴포넌트 확인
            if (containerRectTransform == null)
            {
                Debug.LogError("RewardLayoutController: RectTransform 컴포넌트를 찾을 수 없습니다!");
                return;
            }

            // 초기화 시 모든 Grid를 비활성화합니다.
            InitializeGrids();
            
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController 초기화 중 오류 발생: {e.Message}");
            isInitialized = false;
        }
    }
    
    /// <summary>
    /// Grid들을 안전하게 초기화합니다.
    /// </summary>
    private void InitializeGrids()
    {
        try
        {
            if (grids == null)
            {
                Debug.LogWarning("RewardLayoutController: grids 리스트가 null입니다!");
                grids = new List<GridLayoutGroup>(); // 빈 리스트로 초기화
                return;
            }
            
            for (int i = 0; i < grids.Count; i++)
            {
                try
                {
                    if (grids[i] != null)
                    {
                        if (grids[i].gameObject != null)
                        {
                            grids[i].gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"RewardLayoutController: Grid[{i}]가 null입니다. Inspector에서 확인해주세요.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"RewardLayoutController: Grid[{i}] 초기화 중 오류 발생: {e.Message}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: InitializeGrids 중 오류 발생: {e.Message}");
        }
    }

    void Update()
    {
        // 초기화가 완료되지 않았다면 Update를 실행하지 않음
        if (!isInitialized)
        {
            return;
        }
        
        // 런타임에 인스펙터의 maxRewardCountForTest 값이 변경되었는지 확인하여 레이아웃 업데이트
        if (Application.isPlaying && maxRewardCountForTest != lastTestCount)
        {
            // 안전성 검사: 필수 컴포넌트들이 존재하는지 확인
            if (!ValidateComponents())
            {
                Debug.LogError("RewardLayoutController: 필수 컴포넌트가 누락되어 테스트를 중단합니다.");
                return;
            }
            
            lastTestCount = maxRewardCountForTest;
            
            // 더미 데이터 리스트 생성 (개수만 필요)
            List<object> testRewards = new List<object>();
            for (int i = 0; i < maxRewardCountForTest; i++)
            {
                testRewards.Add(null); 
            }
            
            DisplayRewards(testRewards);
        }
    }
    
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
        
        if (rewardItemPrefab == null)
        {
            Debug.LogError("RewardLayoutController: rewardItemPrefab이 설정되지 않았습니다!");
            return false;
        }
        
        if (grids == null || grids.Count == 0)
        {
            Debug.LogError("RewardLayoutController: grids 리스트가 비어있거나 null입니다!");
            return false;
        }
        
        // Grid들이 모두 null인지 확인
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


    // ===================================================================================================
    // ✨ 핵심 기능 함수 (Core Functions)
    // ===================================================================================================

    /// <summary>
    /// 보상을 지급하고 레이아웃을 업데이트합니다. 이 함수를 실제 게임 로직에서 사용합니다.
    /// </summary>
    /// <param name="rewards">지급할 보상 아이템 정보 목록 (개수만 사용)</param>
    public void DisplayRewards(List<object> rewards) 
    {
        // 안전성 검사: 입력 파라미터 확인
        if (rewards == null)
        {
            Debug.LogWarning("RewardLayoutController: rewards 리스트가 null입니다. 빈 리스트로 처리합니다.");
            rewards = new List<object>();
        }
        
        // 안전성 검사: 필수 컴포넌트 확인
        if (!ValidateComponents())
        {
            Debug.LogError("RewardLayoutController: 필수 컴포넌트가 누락되어 DisplayRewards를 실행할 수 없습니다.");
            return;
        }
        
        int totalRewards = rewards.Count;

        // 1. 기존 아이템 및 Grid 초기화/비활성화
        ClearAllGrids();
        
        if (totalRewards == 0)
        {
            // 보상이 0개면 아무것도 하지 않고 종료
            if (containerRectTransform != null)
            {
                containerRectTransform.localScale = Vector3.one;
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRectTransform);
            }
            return;
        }

        // 2. 해당 보상 개수에 맞는 레이아웃 설정 찾기
        LayoutConfig config = GetLayoutConfig(totalRewards);

        if (config == null)
        {
            Debug.LogWarning($"보상 개수 {totalRewards}에 맞는 레이아웃 설정이 없습니다. 첫 번째 Grid에 모두 넣습니다.");
            
            // 설정이 없을 경우 임시 처리 (첫 번째 Grid에 모두)
            if (grids.Count > 0)
            {
                grids[0].gameObject.SetActive(true);
                InstantiateItems(grids[0].transform, totalRewards);
            }
            containerRectTransform.localScale = Vector3.one; // 기본 스케일
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRectTransform);
            return;
        }

        // 3. 레이아웃에 따라 아이템 로드 및 Grid 활성화
        for (int i = 0; i < config.itemsPerGrid.Count; i++)
        {
            if (i >= grids.Count)
            {
                Debug.LogError($"Grid {i}가 존재하지 않아 아이템 로드를 중단합니다. Grid를 추가해주세요.");
                break;
            }

            int itemsToLoad = config.itemsPerGrid[i];
            GridLayoutGroup targetGrid = grids[i];

            if (targetGrid != null)
            {
                if (itemsToLoad > 0)
                {
                    targetGrid.gameObject.SetActive(true);
                    InstantiateItems(targetGrid.transform, itemsToLoad);
                }
                else
                {
                    targetGrid.gameObject.SetActive(false);
                }
            }
        }

        // 4. Reward Container 스케일 조정
        if (containerRectTransform != null)
        {
            containerRectTransform.localScale = config.containerScale;
            
            // 5. UGUI 레이아웃 시스템 강제 업데이트 (Content Size Fitter 사용 시 중요)
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRectTransform);
        }
    }

    /// <summary>
    /// 모든 Grid의 자식을 제거하고 Grid 오브젝트를 비활성화합니다.
    /// </summary>
    private void ClearAllGrids()
    {
        try
        {
            if (grids == null)
            {
                Debug.LogWarning("RewardLayoutController: grids가 null입니다. ClearAllGrids를 건너뜁니다.");
                return;
            }
            
            foreach (var grid in grids)
            {
                try
                {
                    if (grid != null && grid.transform != null)
                    {
                        // Grid의 모든 자식(RewardItem) 제거
                        for (int i = grid.transform.childCount - 1; i >= 0; i--)
                        {
                            Transform child = grid.transform.GetChild(i);
                            if (child != null && child.gameObject != null)
                            {
                                Destroy(child.gameObject);
                            }
                        }
                        
                        // Grid 오브젝트 비활성화
                        if (grid.gameObject != null)
                        {
                            grid.gameObject.SetActive(false);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"RewardLayoutController: Grid 정리 중 오류 발생: {e.Message}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RewardLayoutController: ClearAllGrids 중 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 지정된 부모(Grid)에 보상 아이템을 개수만큼 인스턴스화합니다.
    /// </summary>
    private void InstantiateItems(Transform parent, int count)
    {
        if (parent == null)
        {
            Debug.LogError("RewardLayoutController: parent Transform이 null입니다!");
            return;
        }
        
        if (rewardItemPrefab == null)
        {
            Debug.LogError("RewardLayoutController: rewardItemPrefab이 null입니다!");
            return;
        }
        
        for (int i = 0; i < count; i++)
        {
            try
            {
                // 인스턴스화 시 World Position Stays를 false로 설정하여
                // 부모의 Layout Group 규칙을 따르도록 합니다.
                GameObject item = Instantiate(rewardItemPrefab, parent);
                
                // 인스턴스화 성공 확인
                if (item == null)
                {
                    Debug.LogError($"RewardLayoutController: 아이템 {i}번째 인스턴스화에 실패했습니다.");
                    continue;
                }
                
                // (TODO: 여기서 item에 실제 보상 데이터를 바인딩하는 로직 추가)
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RewardLayoutController: 아이템 {i}번째 인스턴스화 중 오류 발생: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 총 보상 개수에 맞는 레이아웃 설정을 찾습니다.
    /// </summary>
    private LayoutConfig GetLayoutConfig(int totalCount)
    {
        // 보상 개수가 정확히 일치하는 설정을 찾습니다.
        return layoutConfigs.Find(config => config.rewardCount == totalCount);
    }
}