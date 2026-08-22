# 2D Water v1.2

버텍스 기반 2D 물 시뮬레이션. 가로로 배치된 표면 포인트를 스프링처럼 거동시키고 좌·우로 파동을 전파한다. 어항·호수·물컵 등 상단이 출렁이는 2D 물 표현에 사용.

## 개요

- **전용 물 셰이더 내장**: `CAT/Effects/2D Water` (URP). 컴포넌트 추가 시 머티리얼 에셋이 자동 생성·할당된다. 다른 머티리얼로 교체도 가능.
- **지속 출렁임 (Ambient Wave)**: 충돌이 없어도 표면이 계속 출렁인다. 강도·빈도·랜덤성을 수치로 조절.
- **물리 기능 opt-in**: 충돌 상호작용·부력은 기본 OFF. 둘 다 꺼져 있으면 `BoxCollider2D` 를 비활성해 물리 비용이 0. 스프링 시뮬도 이벤트로만 깨어나고 정지 시 자동 슬립.
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
├── Shader/
│   └── CAT_Water2D.shader # 물속 표현 셰이더 (URP, 모바일 최적화)
├── Materials/
│   └── Water2D_Default.mat # 최초 컴포넌트 추가 시 자동 생성
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
| 지속 출렁임 | 진행 파형(사인 중첩 + Perlin 랜덤) + 주기적 랜덤 임펄스. 강도/빈도/랜덤성 수치 조절 |
| 스프링 시뮬 슬립 | 충돌·`Splash()`·랜덤 임펄스로만 깨어남. 표면 정지 시 자동 슬립(연산·정점 업로드 생략) |
| 물리 토글 | `Interaction Enabled` / `Buoyancy Enabled`. 둘 다 OFF 면 콜라이더까지 비활성 |
| 폭 있는 Splash | `water.SplashArea(localX, force, spread)` — 코사인 감쇠로 부드러운 파동 주입 |
| 물속 셰이더 | 깊이 그라디언트 · 코스틱 · 굴절 왜곡 · 수면 거품 · 질감 텍스처 (기능별 keyword 분리) |
| 머티리얼 자동 생성 | 컴포넌트 추가 시 `Water2D_Default.mat` 생성·할당. 인스펙터에서 수치 인라인 편집 |
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
| 물리 | Interaction Enabled | **false** | 충돌 상호작용 on/off. OFF 면 콜라이더 비활성 |
| 상호작용 | Velocity Multiplier | 0.1 | 진입 Y속도 계수 |
| 상호작용 | Mass Multiplier | 0.05 | 질량 계수 |
| 상호작용 | Max Impulse | 5 | 단일 진입 impulse 상한 |
| 부력 | Buoyancy Enabled | false | 부력 시스템 on/off |
| 부력 | Buoyancy Force | 30 | 단위 잠김 깊이 × 질량당 힘 |
| 부력 | Linear Drag | 3 | 수중 선형 감쇠(초당) |
| 부력 | Angular Drag | 1 | 수중 각속도 감쇠(초당) |
| 지속 출렁임 | Ambient Enabled | **true** | 지속 출렁임 on/off (디스플레이 기본값) |
| 지속 출렁임 | 강도 배율 | 1 | 진폭·임펄스 세기 공통 배율 (Range 0~3) |
| 지속 출렁임 | 진폭 | 0.08 | 진행 파형 크기(로컬 단위) |
| 지속 출렁임 | 파장 | 3 | 작을수록 잔물결, 클수록 너울 |
| 지속 출렁임 | 진행 속도 | 0.6 | 로컬 단위/초. 음수면 반대 방향. 시간 빈도 = 속도/파장 |
| 지속 출렁임 | 옥타브 수 | 2 | 중첩 파형 개수 (Range 1~4) |
| 지속 출렁임 | 옥타브 진폭비 | 0.5 | 다음 옥타브 진폭 비율 (파장은 1/2) |
| 지속 출렁임 | 옥타브 속도비 | 1.6 | 다음 옥타브 속도 비율. 1 이 아니면 반복 주기가 길어짐 |
| 지속 출렁임 | 랜덤성 | 0.35 | 진폭 대비 Perlin 노이즈 비율 |
| 지속 출렁임 | 노이즈 밀도 / 속도 | 0.5 / 0.35 | 랜덤 성분의 공간 밀도·시간 변화 속도 |
| 지속 출렁임 | 시드 | 0 | 여러 물 오브젝트의 위상 분리 |
| 지속 출렁임 | 랜덤 임펄스 사용 | **false** | 주기적 파동 주입 on/off. ON 이면 스프링 시뮬이 계속 깨어 있음 |
| 지속 출렁임 | 간격 최소/최대 | 0.6 / 2 | 임펄스 발생 빈도(초) |
| 지속 출렁임 | 세기 최소/최대 | -0.05 / 0.05 | 임펄스 세기 범위 (음수=아래, 양수=위). 스텝당 속도 단위 |
| 지속 출렁임 | 퍼짐 폭 | 0.6 | 임펄스 영향 로컬 폭 (코사인 감쇠) |
| 렌더링 | Sorting Layer | Default | SpriteRenderer 와 공유되는 정렬 레이어 |
| 렌더링 | Order in Layer | 0 | 같은 레이어 내 앞뒤 정렬 |
| 렌더링 | 표면 라인 (표시/두께/색상) | true / 0.06 / white | LineRenderer 기반 수면 라인 |
| 머티리얼 | Material | 자동 생성 | 비어 있으면 `CAT/Effects/2D Water` 머티리얼 자동 할당 |
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

### 지속 출렁임 (Ambient Wave)

두 계층으로 구성된다.

| 계층 | 처리 방식 | 특징 |
|------|-----------|------|
| 진행 파형 | 스프링 시뮬과 **별개**로 표면 정점에 직접 가산되는 해석적 변위 | 감쇠에 먹히지 않으므로 인스펙터 진폭이 화면에 그대로 나온다. 프레임레이트 독립 |
| 랜덤 임펄스 | 포인트 `Velocity` 에 주입 → 이웃으로 전파 | 실제 파동처럼 퍼지고 사라진다. 충돌 splash 와 동일 경로 |

조절 축:
- **강도**: `강도 배율`(전체) → `진폭`(파형), `세기 최소/최대`(임펄스)
- **빈도**: `파장` + `진행 속도` (시간 빈도 = 속도/파장, 인스펙터에 Hz 표시) → `간격 최소/최대`(임펄스)
- **랜덤성**: `랜덤성`(Perlin 비율) + `노이즈 밀도/속도`, `옥타브 속도비`(반복 주기 연장), `시드`(오브젝트별 위상 분리)

프리셋 예시:

| 연출 | 진폭 | 파장 | 속도 | 옥타브 | 랜덤성 | 임펄스 간격 |
|------|------|------|------|--------|--------|-------------|
| 잔잔한 호수 | 0.03 | 4 | 0.3 | 1 | 0.2 | 1.5 ~ 3 |
| 횡스크롤 기본 | 0.08 | 3 | 0.6 | 2 | 0.35 | 0.6 ~ 2 |
| 거친 파도 | 0.2 | 1.5 | 1.4 | 3 | 0.6 | 0.2 ~ 0.7 |

임펄스 세기는 **스텝당 속도** 단위이므로 화면 변위와 1:1 이 아니다. 실측(pointCount 24, Spring 0.025, Damping 0.025):

| 퍼짐 폭 | 세기 0.1 의 최대 표면 변위 |
|---------|---------------------------|
| 0 (단일 포인트) | 0.056 |
| 0.3 | 0.109 |
| 0.6 (기본) | 0.159 |
| 1.2 | 0.311 |

즉 기본 퍼짐(0.6)에서 **세기 ≈ 변위 / 1.6**. 진폭과 비슷한 크기로 맞추면 자연스럽다.

> 파형은 `Splash()` / 충돌 파동 위에 **가산**되므로 두 표현이 서로를 지우지 않는다.
> `SampleSurfaceHeight()` 도 파형을 포함하므로 부력 오브젝트가 출렁임에 맞춰 함께 흔들린다.

### 물속 셰이더 (`CAT/Effects/2D Water`)

컴포넌트를 추가하면 `Materials/Water2D_Default.mat` 이 자동 생성·할당된다. 인스펙터의
`물 머티리얼 / 셰이더` 섹션에서 수치를 바로 편집하고, `전용 머티리얼로 복제` 버튼으로
오브젝트 전용 에셋을 만들 수 있다 (기본 머티리얼은 여러 오브젝트가 공유).

| 그룹 | 프로퍼티 | 설명 |
|------|----------|------|
| Color | Shallow / Deep Color | 수면·심층 색 (알파 포함). UV v 로 보간 |
| Color | Gradient Power | 그라디언트 집중도 |
| Color | Alpha 배율 | 전체 투명도 |
| Texture | 질감 텍스처 사용 | 키워드 `_CAT_TEXTURE`. off 면 샘플링 자체가 제거됨 |
| Texture | 질감 색조 / 세기 / 타일링 / 스크롤 | 텍스처 기반 질감 (샘플 1회, 자동 스크롤) |
| Caustics | 코스틱 사용 / 색 / 세기 / 밀도 / 속도 / 선명도 / 깊이 감쇠 | 절차적 물결 무늬 (텍스처 불필요) |
| Distortion | 굴절 왜곡 사용 / 세기 / 밀도 / 속도 | 코스틱·질감 UV 를 흔들어 굴절감 표현 |
| Foam | 수면 거품 사용 / 색 / 두께 / 부드러움 | 수면 경계 하이라이트. 두께는 **UV(v) 기준**이라 Depth 에 비례해 두꺼워진다 |
| Depth Fade | 하단 / 좌우 페이드 | 배경과의 경계 블렌딩 |

모바일 최적화 포인트:
- 텍스처 샘플 **최대 1회**, 나머지는 전부 ALU 절차적 연산 → 대역폭 부담 없음
- 기능별 `shader_feature_local_fragment` 로 사용하지 않는 연산은 컴파일 시 제거
- `if` 분기 없음 (`smoothstep`/`lerp` 로 대체), `half` 우선 사용, `#pragma target 2.0`
- SRP Batcher 호환 (`UnityPerMaterial` CBUFFER), 그림자·라이트 프로브·모션 벡터 자동 off
- GrabPass(씬 텍스처) 미사용

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

실측 (Unity 6000.0.69f1, Play 모드, 데스크톱 Mono JIT). `TickAndRender` 1프레임 µs:

| 포인트 | 완전 유휴 | 파형만(기본) | 파형만+라인OFF | 파형+스프링 awake |
|---|---|---|---|---|
| 24 | 0.01 | 3.61 | 3.24 | 4.04 |
| 48 | 0.01 | 6.56 | 6.02 | 7.77 |
| 64 | 0.01 | 8.69 | 7.78 | 9.98 |

- **GC 할당 0 B / 1000프레임** (매 프레임 경로에 할당 없음)
- 모바일 CPU는 코어당 2~4배 느림 → 포인트 48·기본 설정에서 프레임당 약 15~26µs = 60fps 예산의 0.15% 내외
- 저프레임(30fps)에서는 스프링 스텝이 2배 실행 (안전 상한 8스텝)

GPU 프래그먼트 비용 (1920×1080 전체 덮음, 40겹 오버드로 증폭 후 1겹 환산, `Sprites/Default` 대비):

| 구성 | 상대 비용 |
|---|---|
| 전 기능 OFF (그라디언트만) | 1.04x |
| 굴절 왜곡 + 거품 (코스틱 OFF) | 1.33x |
| 기본 (코스틱 포함) | 3.23x |
| 기본 + 질감 텍스처 | 3.61x |

iOS Metal 컴파일 결과: 기본 변형 133줄 / sin·cos 5개 / **텍스처 페치 0개**. Android GLES3·Vulkan 컴파일 확인.
비용의 대부분은 코스틱이며, 화면 점유 면적에 정비례한다.

| 항목 | 값 (pointCount=24 기준) |
|------|------------------------|
| 정점 수 | 48 |
| 삼각형 수 | 46 |
| 드로우콜 | 2 (물 메시 + 표면 라인. 라인 OFF 시 1) |
| Mesh 재할당 | pointCount 변경 시 1회 |

튜닝 팁:
- 작은 물컵: pointCount 8~16 (최소 8)
- 일반 어항: 24 (기본값)
- 넓은 호수: 48
- 64 초과는 이득 대비 비용만 증가 (에디터 경고 표시)
- 비용 0 의 디스플레이용 물: 물리 토글 OFF + 랜덤 임펄스 OFF + 진행 파형만 사용

## 제한 사항

- **UI(Canvas) 미지원**: World-space 전용. UI 모드는 향후 `MaskableGraphic` 파생으로 확장 가능.
- **씬 굴절(GrabPass) 없음**: 배경 텍스처를 읽지 않는다. 왜곡은 셰이더 내부 패턴에만 적용 (모바일 대역폭 보호).
- **1D 파동**: 표면은 수평 방향으로만 파동 전파. 2D wave equation 은 미지원.
- **Splash 파티클 내장 없음**: `OnSplash` 이벤트로 사용자가 직접 연결.

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0+) | ✅ |
| URP 17+ | ✅ |
| Built-in RP | ⚠️ 시뮬레이션은 동작하나 내장 셰이더는 URP 전용. Built-in 에서는 머티리얼을 직접 지정 |
| 모바일 (iOS/Android) | ✅ 우선 타깃 |
| `Rigidbody2D.linearVelocity` | ✅ Unity 6 표준 사용 |

## 향후 확장 후보

- UI(Canvas) 모드 (`MaskableGraphic.OnPopulateMesh`)
- 부력 고도화 (부분 잠김·표면 경계 처리, Effector2D 와의 조합 등)
- Splash 파티클 프리셋 프리팹
- 다중 소스 2D wave equation

## 변경 이력

### v1.2
- 인스펙터: 비활성 옵션 숨김 처리 — 토글이 켜졌을 때만 하위 옵션 표시
  (Interaction / Buoyancy / Ambient / 랜덤 임펄스 / 표면 라인, 옥타브 수 1 이면 옥타브 옵션, 랜덤성 0 이면 노이즈 옵션)
- 물리 기능 opt-in: `Interaction Enabled` 추가, 물리 토글이 모두 OFF 면 `BoxCollider2D` 자동 비활성
- 스프링 시뮬 이벤트 기반 슬립 (유휴 시 프레임 비용 0.01µs, 정점 업로드도 생략)
- 표면 라인 프로퍼티 재설정을 dirty 기반으로 변경 (포인트 48에서 프레임 비용 약 22% 감소)
- 기본값을 디스플레이 용도로 조정 (Ambient ON, 랜덤 임펄스 OFF, 물리 OFF)
- 인스펙터에 실측 기반 성능·빌드 주의사항 HelpBox 추가
- 런타임 토글 API: `InteractionEnabled` / `BuoyancyEnabled` / `SurfaceLineEnabled` / `IsSpringAwake`


### v1.1
- 지속 출렁임 (Ambient Wave): 진행 파형 + 랜덤 임펄스, 강도·빈도·랜덤성 수치 조절
- 전용 물 셰이더 `CAT/Effects/2D Water` (URP 2D/Forward, 기능별 shader_feature)
- 컴포넌트 추가 시 머티리얼 에셋 자동 생성 + 인스펙터 인라인 수치 편집
- `SplashArea()` 공개 API, 표면 라인 프로퍼티 인스펙터 노출
- MeshRenderer 그림자·라이트 프로브 등 불필요 기능 자동 off

### v1.0 (초기 릴리스)
- 버텍스 기반 스프링 물 시뮬레이션
- Rigidbody2D 자동 트리거 상호작용
- Splash 공개 API 및 UnityEvent 훅
- 에디터 실시간 프리뷰
- Scene 기즈모 (bounds, 표면 라인, 포인트 도트)
