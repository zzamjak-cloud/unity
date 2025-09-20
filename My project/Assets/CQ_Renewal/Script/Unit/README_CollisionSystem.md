# 캐릭터 콜리전 시스템 사용법

## 개요
이 시스템은 캐릭터에 3개의 콜리전을 추가하여 각각 다른 목적으로 사용할 수 있도록 설계되었습니다.

## 콜리전 타입

### 1. Body 콜리전
- **목적**: 적/플레이어 충돌, 피격 판정
- **타입**: 물리적 충돌 (isTrigger = false)
- **처리**: `OnBodyCollision()` 메서드에서 처리
- **태그**: Enemy, Player, Obstacle

### 2. Attack 콜리전
- **목적**: 공격 범위 감지, 타격 판정
- **타입**: 트리거 (isTrigger = true)
- **처리**: `OnAttackCollision()` 메서드에서 처리
- **태그**: Enemy, Player, Destructible

### 3. Interaction 콜리전
- **목적**: 감지용, 상시 활성화 (적/플레이어 감지 및 아이템/오브젝트 상호작용)
- **타입**: 트리거 (isTrigger = true)
- **처리**: `OnInteractionCollision()` 메서드에서 처리
- **태그**: Enemy, Player, Item, Interactable, NPC

## 설정 방법

### 1. 캐릭터에 콜리전 추가
1. 캐릭터 오브젝트에 `CharacterCollisionManager` 컴포넌트 추가
2. Inspector에서 각 콜리전 타입에 해당하는 Collider2D 할당
3. 레이어 마스크와 태그 설정

### 2. Attack 콜리전 GameObject 설정
1. 캐릭터의 자식으로 `AttackCollision`이라는 이름의 GameObject 생성
2. 해당 GameObject에 BoxCollider2D 컴포넌트 추가
3. BoxCollider2D의 `Is Trigger` 체크
4. 적절한 크기와 위치로 설정
5. `AttackCollisionHandler` 컴포넌트가 자동으로 추가됩니다
6. (선택사항) Inspector에서 `CharacterBase`의 `Attack Collision Object` 필드에 직접 할당
7. `Attack Collision Duration`으로 공격 지속 시간 조정 가능 (기본값: 0.5초)

**주의**: Attack 콜리전은 GameObject 활성화/비활성화가 아닌 **Collider 활성화/비활성화** 방식으로 작동합니다.

### 3. 피격 이펙트 설정
1. Inspector에서 `CharacterBase`의 `Damage Effect Data` 필드 설정
2. `Effect Container`: 피격 이펙트를 담을 Transform (보통 캐릭터 자체 또는 자식 오브젝트)
3. `Effect Prefab`: 피격 이펙트 프리팹 (ParticleSystem 컴포넌트 포함)
4. 최대 동시 피격 이펙트 개수: 5개 (연타 공격 및 다수 공격 시 제한)

### 4. 스크립트에서 콜리전 처리
```csharp
// CharacterBase를 상속받는 클래스에서
public override void OnBodyCollision(Collider2D other)
{
    // 피격 판정 처리
    if (other.CompareTag("Enemy"))
    {
        // 적과의 충돌 처리
    }
}

public override void OnAttackCollision(Collider2D other)
{
    // 타격 판정 처리
    if (other.CompareTag("Enemy"))
    {
        // 적을 공격
    }
}

public override void OnInteractionCollision(Collider2D other)
{
    // 감지 및 상호작용 처리
    if (other.CompareTag("Enemy"))
    {
        // 적 감지 처리
    }
    else if (other.CompareTag("Item"))
    {
        // 아이템 획득
    }
}
```

## 주요 메서드

### 콜리전 활성화/비활성화
```csharp
// 전체 콜리전 시스템
SetCollisionSystemEnabled(bool enabled);

// 특정 콜리전 타입
SetCollisionTypeEnabled(CollisionType.Body, bool enabled);
SetCollisionTypeEnabled(CollisionType.Attack, bool enabled);
SetCollisionTypeEnabled(CollisionType.Interaction, bool enabled);

// 개별 콜리전 (PlayerController, EnemyController)
SetBodyCollisionEnabled(bool enabled);
SetAttackCollisionEnabled(bool enabled);
SetInteractionCollisionEnabled(bool enabled);
```

### 공격 콜리전 제어
```csharp
// 공격 시작 시
EnableAttackCollision();

// 공격 종료 시
DisableAttackCollision();

// 공격 애니메이션 시작 (자동으로 Attack 콜리전 활성화)
StartAttack();

// 공격 애니메이션 종료 (자동으로 Attack 콜리전 비활성화)
EndAttack(); // 애니메이션 이벤트에서 호출
```

### 피격 이펙트 제어
```csharp
// 피격 이펙트 재생 (공격받을 때 자동 호출)
PlayDamageEffect();

// 수동으로 피격 이펙트 재생
character.PlayDamageEffect();
```

### 콜리전 상태 확인
```csharp
bool isEnabled = IsCollisionTypeEnabled(CollisionType.Attack);
```

## 사용 예시

### 플레이어 공격 시스템
```csharp
public class PlayerController : CharacterBase
{
    public override void OnAttackCollision(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            // 적에게 데미지 주기
            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(10);
            }
        }
    }
    
    // 공격 애니메이션 시작 시 (Attack 콜리전 즉시 활성화)
    public void StartAttack()
    {
        TriggerSpecialAnimation(CharacterAnimationState.Attack);
        EnableAttackCollision(); // 타격 판정 즉시 시작
    }
    
    // 공격 애니메이션 종료 시 (애니메이션 이벤트에서 호출)
    public void EndAttack()
    {
        OnAttackAnimationEnd(); // 자동으로 Attack 콜리전 비활성화
    }
}
```

### 적 AI 시스템
```csharp
public class EnemyController : CharacterBase
{
    public override void OnBodyCollision(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어와 충돌 시 피격 판정
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(5);
            }
        }
    }
    
    public override void OnAttackCollision(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어를 공격 범위에서 감지
            Debug.Log("플레이어가 공격 범위에 들어왔습니다!");
        }
    }
}
```

## 공격 시스템 동작 원리

### 1. 공격 시작
- `StartAttack()` 메서드 호출
- Attack 애니메이션 실행
- Attack 콜리전 Collider 즉시 활성화 (타격 판정 시작)
- `PlayAttackEffect()` 자동 호출 (애니메이션 이벤트, 이펙트 재생)

### 2. 타격 판정
- Attack 콜리전이 활성화된 상태에서 적과 충돌
- `OnAttackCollision()` 메서드 호출
- 타격받은 적에게 자동으로 Blank 애니메이션 실행
- 타격받은 적에게 자동으로 피격 이펙트 재생
- 데미지 처리 및 기타 효과 적용

### 3. 공격 종료
- Attack 애니메이션 종료 시 `EndAttack()` 메서드 호출 (선택사항)
- `AttackCollisionHandler`가 설정된 시간(기본 0.5초) 후 자동으로 Attack 콜리전 Collider 비활성화
- 연속 공격 시에도 각 공격마다 독립적으로 타격 판정 처리

### 4. 애니메이션 이벤트 설정
Unity 애니메이터에서 Attack 애니메이션에 다음 이벤트를 추가해야 합니다:
- **Attack Effect 시작 시점**: `PlayAttackEffect()` 호출
- **Attack Effect 종료 시점**: `EndAttack()` 호출 (선택사항 - Attack 콜리전은 자동으로 비활성화됨)

## 주의사항

1. **콜리전 설정**: 각 콜리전은 적절한 크기와 위치로 설정해야 합니다.
2. **레이어 설정**: Physics2D 설정에서 적절한 레이어 간 충돌을 설정해야 합니다.
3. **태그 설정**: 오브젝트에 적절한 태그를 설정해야 합니다.
4. **성능**: 불필요한 콜리전은 비활성화하여 성능을 최적화하세요.
5. **애니메이션 이벤트**: Attack 애니메이션에 반드시 `PlayAttackEffect()`와 `EndAttack()` 이벤트를 추가해야 합니다.
6. **Attack 콜리전 GameObject**: `AttackCollision`이라는 이름의 자식 오브젝트를 생성하고 BoxCollider2D를 추가해야 합니다.
7. **AttackCollisionHandler**: 이 컴포넌트가 자동으로 추가되어 콜리전 이벤트를 처리하고 자동 비활성화를 담당합니다.

## 디버깅

- `CharacterCollisionManager`의 `enableCollisionLogging`을 활성화하면 콜리전 이벤트를 콘솔에서 확인할 수 있습니다.
- Scene 뷰에서 선택된 오브젝트의 콜리전 범위가 Gizmo로 표시됩니다.
- 각 콜리전 타입별로 다른 색상으로 표시됩니다 (Body: 빨강, Attack: 노랑, Interaction: 파랑).
- Interaction 콜리전은 이제 감지용으로 사용되며, 적/플레이어 감지와 아이템/오브젝트 상호작용을 모두 처리합니다.
