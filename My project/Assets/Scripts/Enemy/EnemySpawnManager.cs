using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 적 유닛 스폰을 관리하는 매니저
/// 오브젝트 풀링을 사용하여 효율적으로 적을 관리합니다.
/// </summary>
public class EnemySpawnManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 2f; // 스폰 간격 (초)
    [SerializeField] private int maxEnemies = 10; // 최대 적 수
    [SerializeField] private bool autoSpawn = true; // 자동 스폰 여부
    
    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject[] enemyPrefabs; // 스폰할 적 프리팹들
    
    [Header("Spawn Points")]
    [SerializeField] private Transform spawnPointsParent; // Spawn Points 부모 오브젝트
    [SerializeField] private Transform enemyPoolsParent; // Enemy Pools 부모 오브젝트
    
    [Header("Detection Range")]
    [SerializeField] private float minDetectionRange = 5f; // 최소 감지 범위
    [SerializeField] private float maxDetectionRange = 10f; // 최대 감지 범위
    
    // 오브젝트 풀링을 위한 딕셔너리
    private Dictionary<GameObject, Queue<GameObject>> enemyPools = new Dictionary<GameObject, Queue<GameObject>>();
    
    // 스폰 포인트 리스트
    private List<Transform> spawnPoints = new List<Transform>();
    
    // 현재 활성화된 적들
    private List<GameObject> activeEnemies = new List<GameObject>();
    
    // 스폰 코루틴 참조
    private Coroutine spawnCoroutine;
    
    // 플레이어 사망 상태 추적
    private bool isPlayerDead = false;
    
    private void Start()
    {
        InitializeSpawnPoints();
        InitializeEnemyPools();
        
        // 플레이어 사망 이벤트 구독
        PlayerController.OnPlayerDeath += OnPlayerDeath;
        
        if (autoSpawn)
        {
            StartSpawning();
        }
    }
    
    /// <summary>
    /// 플레이어 사망 시 호출되는 메서드
    /// </summary>
    private void OnPlayerDeath()
    {
        isPlayerDead = true;
        
        // 스폰 중지
        StopSpawning();
        
        Debug.Log("[EnemySpawnManager] 플레이어 사망으로 인해 적 스폰이 중지됩니다.");
    }
    
    /// <summary>
    /// 스폰 포인트들을 초기화합니다.
    /// </summary>
    private void InitializeSpawnPoints()
    {
        if (spawnPointsParent == null)
        {
            Debug.LogError("EnemySpawnManager: Spawn Points Parent가 설정되지 않았습니다.");
            return;
        }
        
        spawnPoints.Clear();
        for (int i = 0; i < spawnPointsParent.childCount; i++)
        {
            Transform spawnPoint = spawnPointsParent.GetChild(i);
            spawnPoints.Add(spawnPoint);
        }
        
        Debug.Log($"EnemySpawnManager: {spawnPoints.Count}개의 스폰 포인트를 찾았습니다.");
    }
    
    /// <summary>
    /// 적 풀들을 초기화합니다.
    /// </summary>
    private void InitializeEnemyPools()
    {
        if (enemyPoolsParent == null)
        {
            Debug.LogError("EnemySpawnManager: Enemy Pools Parent가 설정되지 않았습니다.");
            return;
        }
        
        foreach (GameObject enemyPrefab in enemyPrefabs)
        {
            if (enemyPrefab == null) continue;
            
            // 풀 생성
            Queue<GameObject> pool = new Queue<GameObject>();
            enemyPools[enemyPrefab] = pool;
            
            // 초기 풀 크기만큼 미리 생성
            for (int i = 0; i < maxEnemies; i++)
            {
                GameObject enemy = Instantiate(enemyPrefab, enemyPoolsParent);
                enemy.SetActive(false);
                pool.Enqueue(enemy);
            }
        }
        
        Debug.Log($"EnemySpawnManager: {enemyPrefabs.Length}개의 적 풀을 초기화했습니다.");
    }
    
    /// <summary>
    /// 스폰을 시작합니다.
    /// </summary>
    public void StartSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        
        spawnCoroutine = StartCoroutine(SpawnEnemiesCoroutine());
    }
    
    /// <summary>
    /// 스폰을 중지합니다.
    /// </summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }
    
    /// <summary>
    /// 적 스폰 코루틴
    /// </summary>
    private IEnumerator SpawnEnemiesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            // 플레이어가 사망했으면 스폰 중지
            if (isPlayerDead)
            {
                break;
            }
            
            // 최대 적 수 체크
            if (activeEnemies.Count >= maxEnemies)
            {
                continue;
            }
            
            // 랜덤 적 프리팹 선택
            if (enemyPrefabs.Length == 0) continue;
            GameObject selectedPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            
            // 랜덤 스폰 포인트 선택
            if (spawnPoints.Count == 0) continue;
            Transform selectedSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            
            // 적 스폰
            SpawnEnemy(selectedPrefab, selectedSpawnPoint.position);
        }
    }
    
    /// <summary>
    /// 특정 위치에 적을 스폰합니다.
    /// </summary>
    public GameObject SpawnEnemy(GameObject enemyPrefab, Vector3 position)
    {
        if (!enemyPools.ContainsKey(enemyPrefab))
        {
            Debug.LogError($"EnemySpawnManager: {enemyPrefab.name}에 대한 풀이 없습니다.");
            return null;
        }
        
        Queue<GameObject> pool = enemyPools[enemyPrefab];
        GameObject enemy;
        
        // 풀에서 적 가져오기
        if (pool.Count > 0)
        {
            enemy = pool.Dequeue();
        }
        else
        {
            // 풀이 비어있으면 새로 생성
            enemy = Instantiate(enemyPrefab, enemyPoolsParent);
        }
        
        // 적 설정
        enemy.transform.position = position;
        enemy.SetActive(true);
        
        // 랜덤 감지 범위 설정
        float randomDetectionRange = Random.Range(minDetectionRange, maxDetectionRange);
        
        // EnemyController 설정
        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.SetDetectionRange(randomDetectionRange);
            
            // 스폰 매니저 참조 설정
            enemyController.SetSpawnManager(this);
        }
        
        // CharacterBase 설정 (재사용되는 적들의 StatusBarUI 초기화)
        CharacterBase characterBase = enemy.GetComponent<CharacterBase>();
        if (characterBase != null)
        {
            // StatusBarUI 초기화 (재사용되는 적들을 위해)
            characterBase.InitializeStatusBarUI();
        }
        
        // 활성 적 리스트에 추가
        activeEnemies.Add(enemy);
        
        // 새로 스폰된 적이 기존 적들과 충돌을 무시하도록 설정
        SetupEnemyCollisionIgnore(enemy);
        
        Debug.Log($"EnemySpawnManager: {enemyPrefab.name}을 {position}에 스폰했습니다. (감지범위: {randomDetectionRange:F1})");
        
        return enemy;
    }
    
    /// <summary>
    /// 적을 풀로 반환합니다.
    /// </summary>
    public void ReturnEnemyToPool(GameObject enemy)
    {
        if (enemy == null) return;
        
        // 활성 적 리스트에서 제거
        activeEnemies.Remove(enemy);
        
        // 적 상태 초기화
        ResetEnemyState(enemy);
        
        // 적 비활성화
        enemy.SetActive(false);
        
        // 원래 프리팹 찾기
        GameObject originalPrefab = null;
        foreach (var kvp in enemyPools)
        {
            if (enemy.name.StartsWith(kvp.Key.name))
            {
                originalPrefab = kvp.Key;
                break;
            }
        }
        
        if (originalPrefab != null && enemyPools.ContainsKey(originalPrefab))
        {
            // 풀로 반환
            enemyPools[originalPrefab].Enqueue(enemy);
            Debug.Log($"EnemySpawnManager: {enemy.name}을 풀로 반환했습니다.");
        }
        else
        {
            // 원래 프리팹을 찾을 수 없으면 파괴
            Destroy(enemy);
            Debug.LogWarning($"EnemySpawnManager: {enemy.name}의 원래 프리팹을 찾을 수 없어 파괴했습니다.");
        }
    }
    
    /// <summary>
    /// 모든 활성 적을 풀로 반환합니다.
    /// </summary>
    public void ReturnAllEnemiesToPool()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            ReturnEnemyToPool(activeEnemies[i]);
        }
    }
    
    /// <summary>
    /// 새로 스폰된 적이 기존 적들과 충돌을 무시하도록 설정합니다.
    /// </summary>
    /// <param name="newEnemy">새로 스폰된 적</param>
    private void SetupEnemyCollisionIgnore(GameObject newEnemy)
    {
        if (newEnemy == null) return;
        
        CharacterCollisionManager newEnemyCollisionManager = newEnemy.GetComponent<CharacterCollisionManager>();
        if (newEnemyCollisionManager == null) return;
        
        Collider2D newEnemyBodyCollider = newEnemyCollisionManager.GetBodyCollider();
        if (newEnemyBodyCollider == null) return;
        
        // 모든 기존 적들과의 충돌을 무시하도록 설정
        foreach (GameObject existingEnemy in activeEnemies)
        {
            if (existingEnemy == newEnemy) continue; // 자기 자신은 제외
            
            CharacterCollisionManager existingEnemyCollisionManager = existingEnemy.GetComponent<CharacterCollisionManager>();
            if (existingEnemyCollisionManager == null) continue;
            
            Collider2D existingEnemyBodyCollider = existingEnemyCollisionManager.GetBodyCollider();
            if (existingEnemyBodyCollider == null) continue;
            
            // 양방향으로 충돌 무시 설정
            Physics2D.IgnoreCollision(newEnemyBodyCollider, existingEnemyBodyCollider, true);
        }
    }
    
    /// <summary>
    /// 적의 상태를 초기화합니다.
    /// </summary>
    private void ResetEnemyState(GameObject enemy)
    {
        if (enemy == null) return;
        
        // EnemyController 컴포넌트 가져오기 (체력 초기화를 먼저)
        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            // 체력 초기화 (HP UI 초기화 전에 체력값을 먼저 설정)
            enemyController.ResetHealthOnly();
            
            // 이동 상태 초기화
            enemyController.ResetMovementState();
            
            // 공격 상태 초기화
            enemyController.ResetAttackState();
        }
        
        // CharacterBase의 풀링용 초기화 함수 호출 (체력 초기화 후 HP UI 초기화)
        CharacterBase characterBase = enemy.GetComponent<CharacterBase>();
        if (characterBase != null)
        {
            characterBase.ResetForPooling();
        }
        
        // Rigidbody2D 초기화
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        
        // Animator 초기화
        Animator anim = enemy.GetComponent<Animator>();
        if (anim != null)
        {
            // 모든 애니메이션 파라미터 초기화
            anim.SetBool("IsDead", false);
            anim.SetBool("IsAttacking", false);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsRunning", false);
            
            // Idle 상태로 강제 전환
            anim.Play("Idle", 0, 0f); // Idle 애니메이션을 처음부터 재생
            
            // 애니메이션 상태 강제 초기화
            anim.Rebind();
            anim.Update(0f); // 즉시 업데이트
            
            // 한 번 더 Idle 상태로 강제 설정
            anim.Play("Idle", 0, 0f);
        }
        
        // 모든 콜리전 다시 활성화
        CharacterCollisionManager collisionManager = enemy.GetComponent<CharacterCollisionManager>();
        if (collisionManager != null)
        {
            collisionManager.SetAllCollisionsEnabled(true);
        }
        
        Debug.Log($"EnemySpawnManager: {enemy.name}의 상태를 완전히 초기화했습니다.");
    }
    
    /// <summary>
    /// 현재 활성 적 수를 반환합니다.
    /// </summary>
    public int GetActiveEnemyCount()
    {
        return activeEnemies.Count;
    }
    
    /// <summary>
    /// 스폰 간격을 설정합니다.
    /// </summary>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = interval;
    }
    
    /// <summary>
    /// 최대 적 수를 설정합니다.
    /// </summary>
    public void SetMaxEnemies(int max)
    {
        maxEnemies = max;
    }
    
    private void OnDestroy()
    {
        // 플레이어 사망 이벤트 구독 해제
        PlayerController.OnPlayerDeath -= OnPlayerDeath;
        
        StopSpawning();
    }
}
