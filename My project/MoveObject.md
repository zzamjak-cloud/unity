# 🚀 모바일 게임 공용 보상 연출 시스템 설계 전략

## 1. 개요 (Overview)

게임 내 다양한 상황(결과 창, 로비 복귀, 퀘스트 완료 등)에서 발생하는 재화 및 아이템 획득 연출을 자동화하고, 데이터 기반으로 제어하기 위한 범용 컴포넌트 시스템입니다.

---

## 2. 시스템 아키텍처 (Architecture)

시스템은 크게 **데이터(Data)**, **제어(Controller)**, **표현(View)**의 3단계로 분리됩니다.

### 🏛 핵심 클래스 구성

- **`RewardSequenceManager`**: 전체 연출 시퀀스를 큐(Queue) 단위로 관리하고 그룹 간 인터벌을 제어합니다.
- **`RewardPool`**: 재화 오브젝트와 이펙트의 오브젝트 풀링을 담당합니다.
- **`RewardItemEntity`**: 개별 오브젝트의 이동 로직(Tween)을 담당합니다.
- **`RewardData (ScriptableObject)`**: 재화 타입별 이동 속도, 이펙트 프리팹, 사운드 등을 정의합니다.

---

## 3. 핵심 기능 구현 전략

### ① 그룹별 시퀀스 제어 (Group Interleaving)

첫 번째 그룹이 이동 중일 때 두 번째 그룹이 시작되는 '병렬적 시퀀스'를 위해 `DOTween.Sequence` 또는 코루틴의 `yield return new WaitForSeconds(offset)`를 활용합니다.

C#

# 

`// 그룹 실행 예시 로직
public async UniTask PlayGroupSequence(RewardGroup group)
{
    foreach(var item in group.Items)
    {
        SpawnItem(item);
        // 개별 등장 간격 (Random Interval)
        await UniTask.Delay(TimeSpan.FromSeconds(Random.Range(0.05f, 0.15f)));
    }
}`

### ② 연출 패턴 (Move Pattern)

1. **등장 (Spawn):** 중앙에서 팝업되듯 커지며 생성 (`Ease.OutBack`).
2. **이동 (Move):** `Bezier` 곡선을 사용하거나 `Ease.InQuint` 등을 활용해 처음엔 느리고 나중에 빨라지는 연출.
3. **도착 (Arrival):** 목적지 UI(Header/Footer)에 닿는 순간 오브젝트는 반환되고, **독립적인 이펙트**가 재생됨.

### ③ 대량 획득 최적화 (Throttling)

수량이 너무 많을 경우(예: 코인 5,000개) 실제 생성되는 오브젝트 수를 제한합니다.

- **Max Spawn Count:** 화면에 동시에 나타날 수 있는 최대 오브젝트 수 제한 (예: 최대 20개).
- **Value Scaling:** 오브젝트 1개가 가지는 재화 가치를 동적으로 계산 (Ex. 코인 1개 오브젝트 = 500 Gold).

---

## 4. 데이터 구조 (Data Definition)

`ScriptableObject`를 사용하여 기획자가 유니티 에디터에서 직접 연출 값을 수정할 수 있게 합니다.

| **필드** | **타입** | **설명** |
| --- | --- | --- |
| `RewardID` | Enum / String | 재화 고유 아이디 (Gold, Gem, Item_01 등) |
| `ItemPrefab` | GameObject | 날아갈 아이템 외형 프리팹 |
| `ArrivalEffect` | GameObject | 도착 시 재생될 이펙트 프리팹 |
| `MoveEase` | Ease (DOTween) | 이동 시 적용할 가속/감속 타입 |
| `Duration` | float | 이동에 소요되는 시간 |

---

## 5. 좌표 변환 (Coordinate System)

모바일 UI 연출에서 가장 중요한 부분은 **World Space와 Canvas Space의 혼용**입니다.

- **World To Screen:** 인게임 캐릭터 위치에서 발생 시 사용.
- **Screen To World:** UI 아이콘 위치(Header/Footer)로 날아갈 때 사용.
- `RectTransformUtility.ScreenPointToWorldPointInRectangle`를 사용하여 해상도와 캔버스 모드에 관계없이 정확한 위치를 계산합니다.

---

## 6. 향후 확장 고려 사항

- **Sound Integration:** 등장 및 도착 시점에 사운드 매니저와 연동.
- **Haptic Feedback:** 중요 재화(Gem) 도착 시 진동 효과 추가.
- **Path Variation:** 직선이 아닌 포물선이나 나선형 등 다양한 이동 경로 프리셋 지원.

---

**이 마크다운 설계도를 기반으로 첫 번째 프로토타입 코드를 작성해 드릴까요?** 특히 **UI 좌표 변환 로직**이나 **오브젝트 풀링 연동 부분** 중 더 궁금한 점이 있다면 말씀해 주세요.