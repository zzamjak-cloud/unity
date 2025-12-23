# 🎁 보상 연출 시스템 (Reward Sequence System)

모바일 게임에서 재화 및 아이템 획득 연출을 자동화하는 범용 컴포넌트 시스템입니다.

## 📁 파일 구조

```
Assets/Scripts/MoveObject/
├── Data/
│   └── RewardData.cs              # ScriptableObject 데이터 정의
├── Editor/
│   └── RewardDataEditor.cs        # 커스텀 인스펙터
├── RewardItemEntity.cs            # 개별 아이템 이동 로직
├── RewardPool.cs                  # 오브젝트 풀링
├── RewardSequenceManager.cs       # 시퀀스 관리 싱글톤
├── RewardTester.cs                # 테스트 컴포넌트
└── README.md                      # 사용 설명서
```

## 🚀 빠른 시작

### 1단계: RewardData 생성

1. 프로젝트 창에서 우클릭 → `Create` → `MoveObject` → `Reward Data`
2. 생성된 ScriptableObject의 속성 설정:
   - **Reward ID**: 고유 식별자 (예: "Gold", "Gem")
   - **Item Prefab**: 날아갈 UI 프리팹
   - **Duration**: 이동 시간
   - **Move Ease**: 이동 곡선 (InQuint 추천)
   - **Arrival Effect**: 도착 시 이펙트 (선택사항)

### 2단계: 씬 설정

1. 빈 GameObject 생성 → `RewardSequenceManager` 컴포넌트 추가
2. UI Canvas와 Camera 연결
3. 테스트를 위해 `RewardTester` 컴포넌트 추가

#### RewardPool 설정 (선택사항)

`RewardSequenceManager`는 자동으로 `RewardPool`을 생성하지만, 수동으로 설정할 수도 있습니다:

1. `RewardSequenceManager`의 **Reward Pool** 필드에 `RewardPool` 컴포넌트 할당
2. `RewardPool` 인스펙터에서 설정:
   - **Initial Pool Size**: 각 타입별 미리 생성할 오브젝트 수 (기본값: 10)
   - **Expand Size**: 풀이 부족할 때 추가로 생성할 오브젝트 수 (기본값: 5)
   - **Pool Parent**: 풀 오브젝트의 부모 Transform (비어있으면 자동 생성)

### 3단계: 코드에서 사용

#### 기본 사용법

```csharp
using MoveObject;

// 보상 타입 등록 (씬 시작 시 한 번만)
// 두 번째 파라미터는 풀 크기 (기본값: 10, -1이면 RewardPool의 Initial Pool Size 사용)
RewardSequenceManager.Instance.RegisterRewardType(goldData, 20);

// 보상 재생
Vector3 startPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(startRect);
Vector3 targetPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(targetRect);

RewardSequenceManager.Instance.PlayReward(
    "Gold",        // Reward ID
    50,            // 개수
    startPos,      // 시작 위치
    targetPos,     // 도착 위치
    () => Debug.Log("완료!") // 완료 콜백
);
```

#### 타입별 위치 등록 (권장)

각 보상 타입별로 시작/도착 위치를 미리 등록하면 더 편리하게 사용할 수 있습니다:

```csharp
// 타입별 위치 등록 (씬 시작 시)
RewardSequenceManager.Instance.RegisterRewardTypeLocation(
    "Gold",           // Reward ID
    goldStartRect,     // 시작 위치 RectTransform
    goldTargetRect     // 도착 위치 RectTransform
);

// 등록된 위치로 보상 재생 (위치 지정 불필요)
RewardSequenceManager.Instance.PlayRewardByType(
    "Gold",           // Reward ID
    50,               // 개수
    () => Debug.Log("완료!") // 완료 콜백
);
```

## 📊 주요 기능

### ✨ 자동 최적화
- 대량 획득 시 화면에 표시되는 오브젝트 수 자동 제한
- `MaxSpawnCount` 설정으로 성능 조절

### 🎬 다양한 연출 옵션
- **등장 애니메이션**: OutBack 이징으로 팝업 효과
- **이동 경로**: 직선 또는 베지어 곡선
- **도착 효과**: 파티클 시스템 자동 재생, 사운드 재생
- **UI 좌표계 완벽 지원**: 해상도 변경에도 안정적으로 작동

### 🔄 오브젝트 풀링
- 자동 풀링으로 메모리 최적화
- 타입별 독립적인 풀 관리
- 부족 시 자동 확장 (Expand Size만큼 추가 생성)
- `RegisterRewardType()`에서 풀 크기 지정 가능

### 📦 그룹 시퀀스
- 여러 보상을 순차적으로 재생
- 그룹 간 인터벌 조절 가능
- **스마트 인터벌**: 각 그룹의 연출 시간을 자동 계산하여 정확한 타이밍에 다음 그룹 시작
- **겹치기 지원**: 음수 인터벌 값으로 그룹들이 겹쳐서 재생 가능

## 🎮 RewardTester 사용법

### 인스펙터 설정
1. **Test Reward Datas**: 테스트할 RewardData 목록
2. **Type Locations**: 타입별 시작/도착 위치 매핑 (선택사항)
3. **Start Position / Target Position**: 기본 시작/도착 위치 (Type Locations가 없을 때 사용)
4. **Test Count**: 생성할 아이템 개수
5. **Test Reward Index**: 테스트할 보상 인덱스
6. **Canvas / UI Camera**: 좌표 변환용

### 테스트 방법
- **스페이스바**: 단일 보상 테스트 (타입별 위치가 등록되어 있으면 자동 사용)
- **M 키**: 모든 보상 연속 테스트
- **T 키**: 타입별 등록된 위치로 보상 테스트
- **A 키**: 모든 타입별 등록된 위치로 순차 테스트
- **Context Menu**: 인스펙터 우클릭 → `Test Single Reward` / `Test Multiple Rewards` / `Test Reward By Type` / `Test All Rewards By Type`

### 타입별 위치 설정
각 보상 타입마다 다른 시작/도착 위치를 설정할 수 있습니다:
1. **Type Locations** 리스트에 항목 추가
2. **Reward ID**: 보상 타입 식별자
3. **Start Transform**: 시작 위치 RectTransform
4. **Target Transform**: 도착 위치 RectTransform

## 📖 고급 사용법

### 여러 보상 그룹 재생

```csharp
List<RewardGroup> groups = new List<RewardGroup>
{
    new RewardGroup { RewardID = "Gold", Count = 100, StartPosition = pos1, TargetPosition = target },
    new RewardGroup { RewardID = "Gem", Count = 10, StartPosition = pos2, TargetPosition = target },
    new RewardGroup { RewardID = "Item_01", Count = 5, StartPosition = pos3, TargetPosition = target }
};

RewardSequenceManager.Instance.PlayRewardSequence(groups, () =>
{
    Debug.Log("모든 보상 획득 완료!");
});
```

### 그룹 인터벌 설정

`RewardSequenceManager`의 **Group Start Delay** 설정으로 그룹 간 시작 타이밍을 조절할 수 있습니다:

- **양수 값 (예: 0.3)**: 이전 그룹의 연출 시간 + 인터벌 후 다음 그룹 시작
- **0**: 이전 그룹 종료 시점에 정확히 다음 그룹 시작 (겹치지 않음)
- **음수 값 (예: -0.5)**: 이전 그룹 종료 전에 다음 그룹 시작 (겹쳐서 재생)

**스마트 인터벌**: 각 그룹의 실제 연출 시간(아이템 개수, 생성 간격, 이동 시간)을 자동 계산하여 정확한 타이밍에 다음 그룹이 시작됩니다.

```csharp
// RewardSequenceManager 인스펙터에서 설정
// Group Start Delay = -0.5f  // 그룹들이 겹쳐서 재생
// Group Start Delay = 0.3f    // 각 그룹 종료 후 0.3초 대기 후 시작
```

### 좌표 변환 유틸리티

```csharp
// RectTransform의 월드 좌표 가져오기
Vector3 worldPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(myRectTransform);

// 스크린 좌표를 월드 좌표로 변환
Vector3 screenToWorld = RewardSequenceManager.Instance.ScreenToWorldPoint(screenPos);

// 등록된 타입별 위치 가져오기
RewardTypeLocation location = RewardSequenceManager.Instance.GetRewardTypeLocation("Gold");
if (location != null)
{
    Vector3 startPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(location.StartTransform);
    Vector3 targetPos = RewardSequenceManager.Instance.GetWorldPositionOfRect(location.TargetTransform);
}
```

## 💡 사용 예제

### 예제 1: 보상 획득 시 연출

```csharp
using MoveObject;

public class RewardController : MonoBehaviour
{
    [SerializeField] private RewardData goldData;
    [SerializeField] private RewardData gemData;
    [SerializeField] private RectTransform rewardStartPos;
    [SerializeField] private RectTransform rewardTargetPos;

    private void Start()
    {
        // 보상 타입 등록
        RewardSequenceManager.Instance.RegisterRewardType(goldData, 20);
        RewardSequenceManager.Instance.RegisterRewardType(gemData, 20);
        
        // 타입별 위치 등록
        RewardSequenceManager.Instance.RegisterRewardTypeLocation(
            "Gold", rewardStartPos, rewardTargetPos);
        RewardSequenceManager.Instance.RegisterRewardTypeLocation(
            "Gem", rewardStartPos, rewardTargetPos);
    }

    public void OnRewardObtained(string rewardID, int count)
    {
        // 등록된 위치로 자동 재생
        RewardSequenceManager.Instance.PlayRewardByType(
            rewardID,
            count,
            () => Debug.Log($"{rewardID} {count}개 획득 완료!")
        );
    }
}
```

### 예제 2: 여러 보상 동시 획득

```csharp
public void OnMultipleRewardsObtained()
{
    List<RewardGroup> groups = new List<RewardGroup>
    {
        new RewardGroup 
        { 
            RewardID = "Gold", 
            Count = 1000, 
            StartPosition = GetWorldPosition(startRect),
            TargetPosition = GetWorldPosition(goldTargetRect)
        },
        new RewardGroup 
        { 
            RewardID = "Gem", 
            Count = 50, 
            StartPosition = GetWorldPosition(startRect),
            TargetPosition = GetWorldPosition(gemTargetRect)
        }
    };

    RewardSequenceManager.Instance.PlayRewardSequence(groups, () =>
    {
        Debug.Log("모든 보상 획득 완료!");
        // 보상 UI 업데이트 등
    });
}

private Vector3 GetWorldPosition(RectTransform rect)
{
    return RewardSequenceManager.Instance.GetWorldPositionOfRect(rect);
}
```

## ⚙️ 최적화 팁

### RewardData 설정
1. **MaxSpawnCount**: 모바일 기기에서는 20개 이하 권장
2. **Spawn Interval**: 0.05~0.15초 사이로 설정하면 자연스러움
3. **Duration**: 0.6~1.0초가 적당 (너무 빠르면 어지러움)

### RewardPool 설정
1. **Initial Pool Size**: 동시에 나올 최대 개수의 1.5배 정도로 설정
   - 예: 최대 20개 동시 표시 → 30개 정도 미리 생성
2. **Expand Size**: Initial Pool Size의 20~30% 정도 권장
   - 예: Initial Pool Size가 30이면 Expand Size는 5~10
3. **풀 크기 지정**: `RegisterRewardType(data, poolSize)`에서 타입별로 다른 크기 설정 가능
   ```csharp
   // 자주 사용되는 보상은 더 큰 풀 크기
   RewardSequenceManager.Instance.RegisterRewardType(goldData, 30);
   // 가끔 사용되는 보상은 작은 풀 크기
   RewardSequenceManager.Instance.RegisterRewardType(rareItemData, 5);
   ```

### 그룹 시퀀스 최적화
- **Group Start Delay**: 음수 값으로 겹치게 재생하면 전체 연출 시간 단축

## 🔧 커스터마이징

### 이동 경로 변경
`RewardData`의 `Use Bezier Path`를 활성화하고 `Bezier Control Point Offset`으로 곡선 정도 조절

### 도착 이펙트 설정
`RewardData`의 `Arrival Effect`에 파티클 시스템이 포함된 프리팹을 할당하면:
- 아이템 도착 시 자동으로 파티클이 재생됩니다
- 파티클 시스템이 자동으로 감지되어 `Play()`가 호출됩니다
- 파티클 재생 시간에 맞춰 자동으로 제거됩니다
- UI 좌표계에서 정확한 위치에 생성됩니다

### 사운드 연동
`RewardData`의 `Arrival Sound`에 AudioClip 할당
- 아이템에 `AudioSource` 컴포넌트가 있으면 자동 재생
- 또는 사운드 매니저와 연동하여 재생

### 햅틱 피드백 추가
`RewardItemEntity.OnArrival()`에서 진동 API 호출

## 📝 주의사항

- 반드시 `RegisterRewardType()`으로 보상 타입을 먼저 등록해야 함
- Canvas는 `Screen Space - Camera` 모드 권장
- DOTween이 프로젝트에 설치되어 있어야 함

## 🐛 트러블슈팅

**Q: 아이템이 화면에 안 나타나요**
- Canvas와 UI Camera가 제대로 연결되었는지 확인
- Start/Target Position이 올바른 좌표인지 확인

**Q: 성능이 떨어져요**
- `MaxSpawnCount`를 줄이세요 (권장: 20 이하)
- `Spawn Interval`을 늘리세요

**Q: 도착 이펙트가 재생 안 돼요**
- `Arrival Effect` 프리팹이 할당되었는지 확인
- 이펙트 프리팹에 Particle System 컴포넌트가 있는지 확인
- 파티클 시스템이 자동으로 재생되므로 별도 설정 불필요

**Q: 이펙트가 잘못된 위치에 생성돼요**
- Canvas와 UI Camera가 올바르게 설정되었는지 확인
- 이펙트 프리팹이 UI 좌표계에서 작동하도록 설정되었는지 확인

**Q: 그룹들이 겹쳐서 재생되지 않아요**
- `Group Start Delay`를 음수 값으로 설정 (예: -0.5)
- 각 그룹의 연출 시간이 자동으로 계산되므로 정확한 타이밍에 시작됩니다

**Q: 타입별 위치가 적용되지 않아요**
- `RegisterRewardTypeLocation()`으로 위치를 먼저 등록했는지 확인
- `PlayRewardByType()` 메서드를 사용하는지 확인
- `RewardTester`에서 `Type Locations` 리스트에 올바르게 설정했는지 확인

**Q: 풀 크기를 어떻게 설정해야 하나요?**
- 동시에 표시될 최대 아이템 수의 1.5배 정도로 설정
- 자주 사용되는 보상은 더 큰 풀 크기, 가끔 사용되는 보상은 작은 풀 크기
- `RegisterRewardType(data, poolSize)`에서 타입별로 다른 크기 지정 가능
- 풀이 부족하면 자동으로 Expand Size만큼 추가 생성됨

**Q: 풀 오브젝트가 너무 많이 생성돼요**
- `RewardPool`의 **Initial Pool Size**를 줄이세요
- `RewardPool`의 **Expand Size**를 줄이세요
- `RewardData`의 **MaxSpawnCount**를 줄여서 동시에 표시되는 아이템 수를 제한하세요

## 📞 지원

문제가 발생하거나 기능 제안이 있으시면 이슈를 등록해 주세요.
