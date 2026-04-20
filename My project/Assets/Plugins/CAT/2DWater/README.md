# 2D Water v1.0

버텍스 기반 2D 물 시뮬레이션. 가로로 배치된 표면 포인트를 스프링처럼 거동시키고 좌·우로 파동을 전파한다. 어항·호수·물컵 등 상단이 출렁이는 2D 물 표현에 사용.

## 개요

- **셰이더 비의존**: 커스텀 셰이더 없이 MeshFilter + MeshRenderer 로 동작. 머티리얼은 사용자가 자유롭게 지정 (Sprites/Default, UI/Default, 자체 셰이더 등).
- **스프링 물리**: Hooke's Law + damping + 이웃 전파(2-pass) 조합.
- **자동 상호작용**: `BoxCollider2D`(Trigger) 에 Rigidbody2D 오브젝트가 진입하면 속도·질량 기반 impulse 를 자동 주입.
- **모바일 우선**: 고정 스텝 1/60s 시뮬, Mesh 재할당 최소화, GC Alloc 0 지향.
- **에디터 프리뷰**: Play 없이 인스펙터에서 출렁임 확인.

## 파일 구조

```
Assets/Plugins/CAT/2DWater/
├── Scripts/
│   ├── Water2D.cs        # 메인 MonoBehaviour
│   └── WaterPoint.cs     # 포인트 구조체
├── Editor/
│   └── Water2DEditor.cs  # 커스텀 인스펙터 + Scene 기즈모
└── README.md
```

## 주요 기능

| 기능 | 설명 |
|------|------|
| 버텍스 웨이브 | 8~128개 포인트의 스프링 시뮬레이션 |
| 이웃 파동 전파 | `_spread` 로 제어되는 좌·우 전파 (2-pass) |
| Rigidbody2D 자동 감지 | Trigger 진입 시 impulse 자동 계산 |
| 공개 Splash API | `water.Splash(localX, force)` (`force` 는 표면 포인트 `Velocity` 에 가산. **음수**는 아래로 찍히는 파동, **양수**는 위로 솟는 파동) |
| 표면 높이 샘플링 | `water.SampleSurfaceHeight(localX)` — 부력·외부 연출용 |
| 표면 리셋 | `water.ResetSurface()` — 높이·속도를 평형으로 |
| 부력 (옵션) | `Buoyancy Enabled` 시 잠긴 `Rigidbody2D` 에 `FixedUpdate` 로 부력·수중 드래그 |
| UnityEvent 훅 | `OnSplash(Vector2 worldPos, float force)` |
| 에디터 프리뷰 | `ExecuteAlways` + 에디터에서만 `EditorApplication.update` (`_editorPreview` 가 켜진 경우) |
| Scene 기즈모 | bounds + 표면 라인 + 포인트 도트 |

## 인스펙터 프로퍼티

| 그룹 | 프로퍼티 | 기본값 | 설명 |
|------|----------|--------|------|
| 크기 | Width | 4 | 물 본체 가로 폭 |
| 크기 | Depth | 2 | 물 본체 세로 깊이 |
| 메시 | Point Count | 24 | 표면 포인트 수 (Range 8~128) |
| 스프링 | Spring Constant | 0.025 | 복원력 강도 (60fps 튜닝) |
| 스프링 | Damping | 0.025 | 감쇠 계수 |
| 스프링 | Spread | 0.25 | 좌·우 전파율 |
| 상호작용 | Velocity Multiplier | 0.1 | 진입 Y속도 계수 |
| 상호작용 | Mass Multiplier | 0.05 | 질량 계수 |
| 상호작용 | Max Impulse | 5 | 단일 진입 impulse 상한 |
| 부력 | Buoyancy Enabled | false | 부력 시스템 on/off |
| 부력 | Buoyancy Force | 30 | 단위 잠김 깊이 × 질량당 힘 |
| 부력 | Linear Drag | 3 | 수중 선형 감쇠(초당) |
| 부력 | Angular Drag | 1 | 수중 각속도 감쇠(초당) |
| 렌더링 | Sorting Layer | Default | SpriteRenderer 와 공유되는 정렬 레이어 |
| 렌더링 | Order in Layer | 0 | 같은 레이어 내 앞뒤 정렬 |
| 이벤트 | On Splash | - | `Water2DSplashEvent` (`UnityEvent<Vector2, float>`) |

커스텀 인스펙터(`Water2DEditor`)에서 **크기·메시·스프링 물리·상호작용·부력·렌더링·이벤트** 섹션으로 그룹 표시된다. (`[Header]` 는 사용하지 않음 — 중복 라벨 방지.)

### Sorting Layer / Order in Layer

`MeshRenderer`는 기본 인스펙터에서 Sorting Layer 옵션이 노출되지 않지만, Water2D 는 SpriteRenderer 와 **동일한 정렬 시스템**을 내부적으로 사용한다. 인스펙터의 `렌더링` 섹션에서 드롭다운·정수 필드로 설정하면 내부적으로 `MeshRenderer.sortingLayerID`, `MeshRenderer.sortingOrder` 에 반영된다.

**용례**:
- 어항 앞 유리 스프라이트 뒤에 물 배치 → `Order in Layer` 를 유리보다 낮게
- 물속 물고기 스프라이트를 물 앞에 → 물고기의 `Order in Layer` 를 물보다 높게
- 씬 전체 배경 레이어와 분리 → `Sorting Layer` 를 "Foreground" 등 커스텀 레이어로 설정

코드에서도 제어 가능:
```csharp
water.SortingLayerID = SortingLayer.NameToID("Foreground");
water.SortingOrder = 10;
```

### 부력 (Buoyancy)

물에 잠긴 `Rigidbody2D` 에 매 FixedUpdate 마다 부력·드래그를 자동 적용한다. Unity 내장 `BuoyancyEffector2D` 와 달리 **출렁이는 표면 높이(`SampleSurfaceHeight`)를 직접 샘플링**하므로 파도에 따라 뜨는 물체가 자연스럽게 흔들린다.

**동작 원리**:
- `OnTriggerEnter2D` 시 내부 HashSet 에 추가, `OnTriggerExit2D` 시 제거
- `FixedUpdate` 에서 각 바디의 위치를 로컬로 변환 → `SampleSurfaceHeight(localX)` 로 해당 X 의 표면 Y 조회
- `submergedDepth = surfaceY - bodyY` (양수일 때만 부력 적용)
- 부력: `Vector2.up * (BuoyancyForce × submergedDepth × rb.mass)`
- 드래그: `linearVelocity *= 1 - LinearDrag × dt`, `angularVelocity` 동일

**튜닝 팁**:
- 가벼운 코르크 느낌: `BuoyancyForce = 50`, `LinearDrag = 5`
- 무거운 돌 (가라앉음): 바디 `mass` 증가 + `BuoyancyForce = 15`
- 흔들리며 천천히 뜨는 오리 인형: `LinearDrag = 1.5`, `AngularDrag = 0.5`

**표면 높이 외부 샘플링**:
```csharp
float localX = water.transform.InverseTransformPoint(fish.position).x;
float surfaceY = water.SampleSurfaceHeight(localX);
Vector3 world = water.transform.TransformPoint(new Vector3(localX, surfaceY, 0));
// world.y 가 해당 위치의 수면 높이
```

## 아키텍처

```
[Update] (Play) / [EditorTick] (에디터 프리뷰 ON, 비플레이)
        │
        ▼
[StepSimulation(dt)]                ← 1/60s 어큐뮬레이터
        │
        ▼
[SingleStep]
   ├── Hooke + damping (각 포인트)
   └── 이웃 전파 (2-pass, Δv)
        │
        ▼
[UpdateMeshVertices]                ← 상단 행 y = Height
        │
        ▼
[MeshFilter.sharedMesh]

[OnTriggerEnter2D] ─► Rigidbody2D 조회 ─► localX·impulse 계산 ─► Splash()
                        │
                        └─ (Buoyancy Enabled) ─► _submergedBodies 에 등록

[FixedUpdate] (Play + Buoyancy Enabled) ─► 잠긴 바디마다 ApplyBuoyancy
```

`OnTriggerExit2D` 에서는 `_submergedBodies` 에서 해당 `Rigidbody2D` 를 제거한다.

정점 레이아웃 (pointCount = N):
```
i=0 ... N-1     (상단, y = _points[i].Height)
i=N ... 2N-1    (하단, y = -_depth 고정)

삼각형: 세그먼트당 2개, 총 (N-1)*2 개
```

## 사용법

### 1. 기본 사용

1. 빈 GameObject 생성
2. `Add Component > CAT > Effects > 2D Water`
3. 자동 추가된 `MeshRenderer` 에 머티리얼 지정 (Sprites/Default 등)
4. 인스펙터에서 Width, Depth 조정
5. Rigidbody2D + Collider2D 부착한 오브젝트를 위에 배치 후 Play

### 2. 코드에서 Splash 주입

```csharp
using CAT.Water2D;

public class SplashOnClick : MonoBehaviour
{
    public Water2D water;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float lx = water.transform.InverseTransformPoint(wp).x;
            // 음수: 표면이 아래로 찍히는 impulse. 양수면 위로 솟는 방향.
            water.Splash(lx, -3f);
        }
    }
}
```

읽기 전용 상태: `water.Width`, `water.Depth`, `water.PointCount`, `water.OnSplash`. 정렬: `water.SortingLayerID`, `water.SortingOrder` (setter 시 `MeshRenderer` 에 반영).

### 3. UnityEvent 로 파티클 연결

1. 씬에 ParticleSystem (Splash 효과) 배치
2. Water2D 인스펙터의 `On Splash (Vector2, float)` 에 ParticleSystem 참조 추가
3. `ParticleSystem.transform.position` 를 `Vector2` → `Vector3` 로 설정하는 래퍼 메서드 연결

### 4. 에디터 프리뷰

- 인스펙터 하단 `에디터 프리뷰` → `Play 없이 시뮬레이션` 토글 ON
- `🌊 Random Splash` 버튼으로 파동 테스트 (내부적으로 음수 `force` 범위 사용)
- `⏹ Reset Surface` 로 표면을 평형 상태로 되돌림 (`ResetSurface()` 와 동일)
- 끄려면 토글 OFF

## 성능 특성

| 항목 | 값 (pointCount=24 기준) |
|------|------------------------|
| 정점 수 | 48 |
| 삼각형 수 | 46 |
| Update 비용 | pointCount·기기에 따라 상이 (경량 목표로 설계) |
| GC Alloc | 0 지향 (런타임 `Update`/`FixedUpdate` 경로에서 할당 없음) |
| Mesh 재할당 | pointCount 변경 시 1회 |

튜닝 팁:
- 작은 물컵: pointCount 8~16 (최소 8)
- 일반 어항: 24 (기본값)
- 넓은 호수: 48~64
- 64 초과는 모바일 비권장 (에디터 경고 표시)

## 제한 사항

- **UI(Canvas) 미지원**: World-space 전용. UI 모드는 향후 `MaskableGraphic` 파생으로 확장 가능.
- **굴절·언더워터 틴트 없음**: 시각 효과는 머티리얼/셰이더로 별도 구현.
- **1D 파동**: 표면은 수평 방향으로만 파동 전파. 2D wave equation 은 미지원.
- **Splash 파티클 내장 없음**: `OnSplash` 이벤트로 사용자가 직접 연결.

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0+) | ✅ |
| URP 17+ | ✅ |
| Built-in RP | ✅ (머티리얼만 해당 RP용으로 지정) |
| 모바일 (iOS/Android) | ✅ 우선 타깃 |
| `Rigidbody2D.linearVelocity` | ✅ Unity 6 표준 사용 |

## 향후 확장 후보

- UI(Canvas) 모드 (`MaskableGraphic.OnPopulateMesh`)
- 부력 고도화 (부분 잠김·표면 경계 처리, Effector2D 와의 조합 등)
- 물 전용 셰이더 (굴절·언더워터·노멀맵)
- Splash 파티클 프리셋 프리팹
- 다중 소스 2D wave equation

## 변경 이력

### v1.0 (초기 릴리스)
- 버텍스 기반 스프링 물 시뮬레이션
- Rigidbody2D 자동 트리거 상호작용
- Splash 공개 API 및 UnityEvent 훅
- 에디터 실시간 프리뷰
- Scene 기즈모 (bounds, 표면 라인, 포인트 도트)
