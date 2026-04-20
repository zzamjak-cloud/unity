# PathRibbon 설계 명세

**작성일**: 2026-04-20
**대상 모듈**: `Assets/Plugins/CAT/PathFollower/`
**관련 컴포넌트**: `PathFollower.cs` (기존), `PathRibbon.cs` (신규)

---

## 1. 목적

PathFollower가 정의한 베지어 경로를 따라 **Tiling 스프라이트 리본 메시**를 생성한다.
라인렌더러 리본 메시처럼 경로를 따라 구부러지며, Loop 경로에서는 **컨베이어 벨트처럼 UV가 흐른다**.

---

## 2. 확정된 설계 결정

| # | 항목 | 결정 |
|---|------|------|
| 1 | 렌더 타겟 | UI(Canvas Image) + Sprite(SpriteRenderer) 모두 지원, Canvas 자식 여부로 자동 감지 |
| 2 | 타일 크기 출처 | 자식 SpriteRenderer(Tiled)의 `size` 또는 자식 Image(Tiled)의 `RectTransform.sizeDelta`를 그대로 사용 |
| 3 | Loop 이음매 처리 | Loop일 때만 `effectiveTileLength = totalLength / round(totalLength / tileLength)`로 보정, 비Loop는 원본 길이 유지(끝 잘림 허용) |
| 4 | 컨베이어 애니메이션 | UV 스크롤 방식. Loop 경로일 때만 활성화, 비Loop는 정적 |
| 5 | 스크롤 속도 | 독립 `scrollSpeed` (units/sec, 음수=역방향). PathFollower의 `duration`과 무관 |
| 6 | 컴포넌트 구조 | 별도 `PathRibbon` 컴포넌트. `[RequireComponent(typeof(PathFollower))]`로 같은 GameObject에 부착 |
| 7 | 셰이더 | **전용 셰이더 없음**. Unity 기본 `Sprites/Default` / `UI/Default` 사용. UI 모드는 `MaskableGraphic` 상속으로 Mask/SoftMask 체인 자동 처리 |
| 8 | 메시 분할 해상도 | 하이브리드: 기본 자동(`samplesPerUnit`), `overrideSamples` 토글 시 수동 `manualSamples` 사용 |

---

## 3. 아키텍처

```
PathFollower (경로 데이터 & 포인트 이동)
    ├── public int PathVersion { get; private set; }   ← 신규: MarkDirty마다 증가
    └── ... (기존 로직)

PathRibbon  [RequireComponent(typeof(PathFollower))]
    ├── Awake: 모드 자동 감지 (Canvas 부모 유무)
    ├── Update: PathVersion / 자식 속성 해시 비교 → 변경 시 RebuildMesh
    │            Loop 경로면 UV 스크롤 갱신
    ├── UI 모드:     MaskableGraphic 상속 → OnPopulateMesh로 정점 공급
    └── Sprite 모드: MeshRenderer + MeshFilter 자동 추가, sharedMesh 갱신

자식 GameObject (사용자가 배치)
    └── SpriteRenderer(DrawMode=Tiled)  또는  Image(Type=Tiled)
           ← Sprite, Material, Color, Size 4가지만 읽힘
           ← 런타임에 자동 비활성화 (에디터에서는 보임)
```

---

## 4. 메시 생성 알고리즘

### 4.1 샘플 수 계산
```
totalLength = ∑ |GetPointAt(t_i+1) - GetPointAt(t_i)|   (사전 코스 샘플링)
if (overrideSamples) sampleCount = manualSamples
else sampleCount = clamp(ceil(totalLength * samplesPerUnit), 4, 4096)
```

### 4.2 정점 생성 (i = 0 .. sampleCount-1)
```
t_i       = i / (sampleCount - 1)     // 비Loop
t_i       = i / sampleCount           // Loop (마지막 정점이 첫 정점과 연결)
pos_i     = follower.GetPointAt(t_i)  // 이미 월드 좌표
tangent_i = follower.GetDirectionAt(t_i).normalized
normal_i  = (-tangent_i.y, tangent_i.x, 0)   // 2D CCW 수직
halfW     = ribbonWidth * 0.5f

vertexLeft_i  = pos_i + normal_i * halfW
vertexRight_i = pos_i - normal_i * halfW

// 월드 좌표를 리본 transform의 로컬로 변환 (UI는 RectTransform 기준)
```

### 4.3 UV 매핑
```
arcLen_0 = 0
arcLen_i = arcLen_{i-1} + |pos_i - pos_{i-1}|
u_i = arcLen_i / effectiveTileLength
uv_left_i  = (u_i, 0)
uv_right_i = (u_i, 1)
```

텍스처 wrap mode는 `Repeat` 필요 → 스프라이트 import 시 경고 또는 런타임에 material texture wrap 체크.

### 4.4 삼각형 (quad strip)
```
각 i (0 .. sampleCount-2):
  tri1: (leftIndex_i, leftIndex_{i+1}, rightIndex_i)
  tri2: (leftIndex_{i+1}, rightIndex_{i+1}, rightIndex_i)

Loop일 때 마지막 샘플과 첫 샘플을 연결하는 1 quad 추가
```

### 4.5 Loop 타일 핏
```
if (follower.IsLoopEnabled):
    tileCount = max(1, round(totalLength / tileLength))
    effectiveTileLength = totalLength / tileCount
else:
    effectiveTileLength = tileLength
```

---

## 5. UV 스크롤 (컨베이어)

**조건**: `follower.IsLoopEnabled == true` 일 때만 동작. 아니면 `_uOffset = 0` 고정.

**매 프레임 처리**:
```
_uOffset += (scrollSpeed * Time.deltaTime) / effectiveTileLength
_uOffset = _uOffset - Mathf.Floor(_uOffset)  // fract, 0..1

for i in 0..sampleCount-1:
    uvs[leftIndex_i].x  = baseU_i + _uOffset
    uvs[rightIndex_i].x = baseU_i + _uOffset

mesh.SetUVs(0, uvs)   // UV 배열만 갱신, 정점/삼각형 건드리지 않음
```

UI 모드에서는 `SetVerticesDirty()` 호출(OnPopulateMesh 재호출 유도).

---

## 6. 리빌드 트리거

**PathFollower 측 추가**:
```csharp
public int PathVersion { get; private set; } = 0;
private void MarkDirty() {
    _transformDirty = true;
    PathVersion++;
}
```

**PathRibbon 측 감지**:
- `_lastPathVersion != follower.PathVersion` → 전체 리빌드
- `_lastSpriteInstanceID` / `_lastSize` / `_lastMaterialInstanceID` 변경 → 전체 리빌드
- `overrideSamples` / `samplesPerUnit` / `manualSamples` 변경 → OnValidate에서 리빌드

**부모 Transform 변경(경로 월드 좌표 변경)**:
- PathFollower가 이미 `_transformDirty`로 감지 중. PathVersion은 포인트 데이터 변경에만 증가.
- 부모 이동으로 인한 리빌드는 PathRibbon이 자체 감지: `_cachedPathFollowerParentMatrix` 비교.

---

## 7. 모드별 구현 상세

### 7.1 UI 모드 (`MaskableGraphic` 상속)
- `material` 기본: `Image` 의 것을 복사하거나 `UI/Default`
- `mainTexture`: 자식 Sprite의 texture
- `OnPopulateMesh(VertexHelper vh)`: 사전 계산된 `_vertices`, `_uvs`, `_colors`, `_triangles`를 vh에 주입
- `SetVerticesDirty()`로 재호출 유도
- `materialForRendering` 체인이 Mask / SoftMaskLight / mob-sakai SoftMaskable 자동 처리

### 7.2 Sprite 모드 (`MeshRenderer + MeshFilter`)
- Awake에서 MeshFilter/MeshRenderer 자동 추가 (없으면)
- `_mesh = new Mesh { name = "PathRibbon", hideFlags = HideFlags.DontSave }`
- `meshFilter.sharedMesh = _mesh`
- `meshRenderer.sharedMaterial = 자식 SpriteRenderer.sharedMaterial`
- `meshRenderer.sortingLayerID`, `sortingOrder` = 자식 SpriteRenderer 것 복사

### 7.3 자식 원본 렌더러 비활성화
- 런타임: `childRenderer.enabled = false` (play 진입 시)
- 에디터: `enabled = true` 유지하되 PathRibbon이 자기 위치로 이동 (사용자 편집 편의)
- 또는 `hideFlags` / `HideFlags.HideInHierarchy` 사용 X (사용자 관리성 해침)

---

## 8. Public API

### PathFollower (신규 멤버)
```csharp
/// <summary>경로가 변경될 때마다 증가하는 버전 번호 (PathRibbon 등 구독자용)</summary>
public int PathVersion { get; private set; }
```

### PathRibbon (전체 신규)
```csharp
[Tooltip("컨베이어 벨트 스크롤 속도 (units/sec). 음수 = 역방향. Loop 경로에서만 동작")]
public float scrollSpeed = 0f;

[Tooltip("경로 길이 1유닛당 샘플 정점 개수 (자동 모드)")]
public float samplesPerUnit = 10f;

[Tooltip("샘플 개수를 수동으로 지정")]
public bool overrideSamples = false;

[Tooltip("수동 샘플 개수 (overrideSamples=true일 때)")]
[Range(4, 512)] public int manualSamples = 32;

// 읽기 전용
public bool IsUIMode { get; }
public int ActualSampleCount { get; }
public float EffectiveTileLength { get; }

// 메서드
public void MarkDirty();       // 수동 리빌드 요청
public void RebuildMesh();     // 즉시 메시 재생성
```

---

## 9. 모바일 최적화

- **사전 할당 배열**: `_vertices`, `_uvs`, `_colors`, `_triangles`를 sample count 기준으로 한 번 할당. 샘플 수 변경 시에만 재할당.
- **GetComponent 캐싱**: Awake에서 PathFollower / 자식 렌더러 / RectTransform / CanvasRenderer 모두 캐싱.
- **Update 경로**: `new` / LINQ / 문자열 연결 금지. Vector3 struct 연산만.
- **UV 스크롤**: 정점/삼각형 배열 건드리지 않고 UV만 갱신.
- **PathVersion 비교**: 단순 int 비교로 GC 없음.
- **Mesh 갱신 플래그**: `MeshUpdateFlags.DontRecalculateBounds | DontValidateIndices` 옵션 활용(경계는 수동 계산).

---

## 10. 에디터 통합

`Assets/Plugins/CAT/PathFollower/Editor/PathRibbonEditor.cs` 신규:
- Mode (UI/Sprite) 표시
- 현재 자식 렌더러 정보(Sprite 이름, Size) 표시
- `ActualSampleCount`, `EffectiveTileLength` 표시
- "Rebuild Mesh" 버튼
- 경고:
  - 자식에 SpriteRenderer(Tiled) / Image(Tiled) 없음
  - 자식 Sprite의 texture wrap이 Repeat 아님
  - SpriteRenderer가 Tiled 모드 아님 (Simple / Sliced 등)

---

## 11. 제외 범위 (YAGNI)

- ❌ 전용 셰이더 제공 (`Sprites/Default` / `UI/Default` 사용)
- ❌ 9-slice 지원 (Tiled만)
- ❌ 리본 두께 변화(가변 width) — 단일 두께로 고정
- ❌ 버텍스 컬러 그라디언트 — 자식 Color 단일값만 적용
- ❌ 3D 공간 리본(법선 계산) — 2D XY 평면 기준만
- ❌ `tileLength` / `ribbonWidth` 오버라이드 필드 — 전적으로 자식 Size에 의존

---

## 12. 테스트 계획

1. **Sprite 모드 기본**: Sprite Renderer(Tiled) 자식 두고 PathFollower 경로에서 정적 리본 표시
2. **UI 모드 기본**: Canvas 아래 Image(Tiled) 자식으로 리본 표시 (SoftMask 안쪽 마스킹 확인)
3. **Loop 이음매**: 원형 경로에서 이음매가 보이지 않는지 확인 (tile count 반올림 보정)
4. **컨베이어 스크롤**: `scrollSpeed > 0` 시 UV 흐름, `< 0` 시 역방향
5. **경로 모핑**: `SwitchToSnapshot` 중 리본이 실시간으로 따라 변형되는지
6. **동적 포인트 편집**: 에디터 SceneView에서 포인트 드래그 시 리본 즉시 갱신
7. **부모 이동**: 부모 Transform 이동 시 리본도 함께 이동
8. **모바일 프로파일**: Update 동안 GC Alloc 0B
9. **SoftMask 호환**: UI 모드에서 SoftMaskLight / mob-sakai SoftMask 하위에 배치 시 마스킹 정상

---

## 13. 파일 변경 계획

| 파일 | 변경 |
|------|------|
| `Assets/Plugins/CAT/PathFollower/PathFollower.cs` | `PathVersion` 프로퍼티 추가 + `MarkDirty()` 내부 증가 |
| `Assets/Plugins/CAT/PathFollower/PathRibbon.cs` | **신규** — MonoBehaviour(Sprite 모드) 또는 MaskableGraphic(UI 모드) 양용 아키텍처. 한 파일 내에서 두 모드 처리 |
| `Assets/Plugins/CAT/PathFollower/Editor/PathRibbonEditor.cs` | **신규** — 커스텀 인스펙터 |
| `Assets/Plugins/CAT/PathFollower/README.md` | PathRibbon 사용법 섹션 추가 |

---

## 14. 구현 난제와 해결 방향

**Q1. 한 컴포넌트에서 UI `MaskableGraphic`와 Sprite `MeshRenderer`를 동시 처리 가능?**
A. MaskableGraphic 상속은 불가역적이므로, 컴포넌트 자체는 `MaskableGraphic` 상속으로 하되 Sprite 모드에서는 `graphic.enabled = false`로 두고 별도 MeshFilter/MeshRenderer를 같은 GameObject에 관리. Canvas 부모 없으면 MaskableGraphic이 동작 안 하도록 `canvasRenderer`도 비활성화.

대안: 베이스 추상 클래스 + UI / Sprite 2개 컴포넌트. 하지만 사용자 작성 UX가 나빠짐.

**1안 선택**: MaskableGraphic 상속, Sprite 모드에서는 graphic 쪽을 no-op으로.

**Q2. UV wrap 문제**:
A. 스프라이트 아틀라스에 포함된 Sprite는 UV가 `[0..1]`이 아니라 아틀라스 내부 서브영역이라 wrap이 불가. 권장: "Sprite Mode = Single" + 텍스처 wrap=Repeat. 에디터에서 경고 표시.

**Q3. SpriteRenderer Tiled 모드의 `size`는 Sprite rect와 다름**:
A. `size`는 타일링 영역을 정의. 리본에서는 `size.x` = 경로 방향 타일 길이(= 한 장 스프라이트가 path를 따라 그려지는 길이), `size.y` = 리본 두께로 매핑.
