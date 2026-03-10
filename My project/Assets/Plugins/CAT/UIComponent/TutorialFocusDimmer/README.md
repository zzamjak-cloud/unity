# TutorialFocusDimmer

튜토리얼 시스템에서 특정 UI 요소를 순서대로 강조(포커스)하기 위한 딤 오버레이 컴포넌트입니다.
단일 쿼드 메시와 전용 셰이더(SDF)로 둥근 사각형 구멍을 뚫어, 포커스 영역만 밝게 남기고 주변을 어둡게 처리합니다.

## 동작 원리

```
[Canvas 전체]
┌────────────────────────────────────┐
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← 딤 영역 (단일 쿼드)
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░┌──────────────┐░░░░░░░░░░░░│
│░░░░░░░│              │░░░░░░░░░░░░│  ← SDF 구멍 (포커스 대상 + padding)
│░░░░░░░│ focusTarget  │░░░░░░░░░░░░│    Raycast 통과
│░░░░░░░└──────────────┘░░░░░░░░░░░░│
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
└────────────────────────────────────┘
```

- **메시**: 루트 캔버스 전체를 덮는 쿼드 1장만 생성
- **구멍**: 프래그먼트 셰이더에서 `RoundedRectSDF`로 계산 — Draw Call 추가 없음
- **Raycast**: `ICanvasRaycastFilter.IsRaycastLocationValid`로 구멍 영역의 터치/클릭 통과

---

## 파일 구조

```
Assets/Plugins/CAT/TutorialFocusDimmer/
├── TutorialFocusDimmer.cs          # MaskableGraphic + ICanvasRaycastFilter 구현
├── Editor/
│   └── TutorialFocusDimmerMenu.cs  # 에디터 메뉴 자동 생성
├── Shader/
│   └── CAT_UIFocusDimmer.shader    # 포커스 구멍 SDF 셰이더
└── README.md
```

---

## 컴포넌트 설정

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `_focusTargets` | `List<RectTransform>` | — | 튜토리얼 순서에 따라 포커싱할 대상 목록 |
| `padding` | `Vector2` | `(0, 0)` | 포커스 영역 여백 (픽셀) |
| `holeCornerRadius` | `float` (0–200) | `16` | 구멍 모서리 라운드 반경 |
| `holeSoftness` | `float` (0–100) | `0` | 구멍 가장자리 소프트 전환 폭. 0 = 하드 엣지 |
| `expansionMargin` | `float` | `200` | 딤 쿼드의 화면 외곽 확장 여백 (해상도 대응) |
| `color` | `Color` | `(0,0,0,0.7)` | 딤 색상 및 투명도 (`Graphic` 상속) |
| `_focusDimmerShader` | `Shader` | — | 셰이더 직접 할당 (빌드 누락 방지) |

---

## 빠른 시작: 에디터 메뉴

**GameObject > CAT > UI > TutorialFocusDimmer**

메뉴 클릭 시 자동으로:

1. Canvas 하위에 `TutorialFocusDimmer` 게임오브젝트 생성 (RectTransform Stretch All)
2. `TutorialFocusDimmer` 컴포넌트 추가
3. 자식에 `TempTarget` (240×120) 생성 후 첫 번째 Focus Target에 연결
4. 프리셋 적용:
   - Tint: `(0, 0, 0, 0.9)`
   - Padding: `(30, 30)`
   - Hole Corner Radius: `30`
   - Hole Softness: `40`
   - Expansion Margin: `200`
   - Shader: `CAT/UI/FocusDimmer` 직접 할당

> **빌드 누락 방지**: 셰이더가 `SerializeField`로 직접 참조되므로 Unity 의존성 추적에 의해 빌드에 자동 포함됩니다.

---

## 사용 방법

### 씬 설정 (수동)

1. 튜토리얼 오버레이 전용 캔버스에 빈 GameObject 생성
2. `TutorialFocusDimmer` 컴포넌트 추가
3. `RectTransform` → **Stretch All** (앵커를 0,0 ~ 1,1 로 전체 채움)
4. `Focus Targets` 리스트에 강조할 버튼/패널들을 순서대로 할당
5. `Color`의 Alpha 값으로 딤 강도 조절 (권장: 0.6–0.8)
6. `Shader` 필드에 `CAT/UI/FocusDimmer` 할당 (빌드 안전)

> `expansionMargin` 덕분에 RectTransform이 정확히 전체 화면 크기가 아니어도 됩니다.
> 셰이더 쿼드가 루트 캔버스 외곽까지 자동으로 확장됩니다.

### 스크립트 제어 — 인덱스 기반 포커싱

```csharp
[SerializeField] private TutorialFocusDimmer _dimmer;

// 튜토리얼 시작 — 첫 번째 타겟 포커싱
void StartTutorial()
{
    _dimmer.gameObject.SetActive(true);
    _dimmer.SetFocusIndex(0);
}

// 다음 단계로 진행
void NextStep()
{
    int next = _dimmer.CurrentIndex + 1;
    if (next < _dimmer.FocusCount)
        _dimmer.SetFocusIndex(next);
    else
        EndTutorial();
}

// 튜토리얼 종료
void EndTutorial()
{
    _dimmer.ClearFocus();
    _dimmer.gameObject.SetActive(false);
}
```

### 외부 API 레퍼런스

| API | 반환 | 설명 |
|-----|------|------|
| `SetFocusIndex(int index)` | `void` | 해당 인덱스 타겟으로 포커싱 (SetVerticesDirty 자동 호출) |
| `ClearFocus()` | `void` | 포커싱 해제 — 구멍 없이 전체 딤 |
| `AddTarget(RectTransform)` | `int` | 런타임에서 타겟 추가, 추가된 인덱스 반환 |
| `CurrentIndex` | `int` | 현재 활성 포커스 인덱스 (-1 = 없음) |
| `CurrentTarget` | `RectTransform` | 현재 활성 타겟 (없으면 null) |
| `FocusCount` | `int` | 등록된 타겟 수 |
| `FocusTargets` | `IReadOnlyList` | 타겟 리스트 (읽기 전용) |

### DOTween 페이드 예시

```csharp
// 등장 (딤 페이드 인)
_dimmer.gameObject.SetActive(true);
_dimmer.SetFocusIndex(0);
_dimmer.color = new Color(0f, 0f, 0f, 0f);
_dimmer.DOFade(0.9f, 0.3f);

// 퇴장 (딤 페이드 아웃)
_dimmer.DOFade(0f, 0.2f)
    .OnComplete(() => _dimmer.gameObject.SetActive(false));
```

### 런타임 파라미터 변경

```csharp
// holeCornerRadius / holeSoftness 변경 → 메시 재빌드 없이 즉시 반영
_dimmer.holeCornerRadius = 32f;
_dimmer.holeSoftness     = 4f;

// padding / expansionMargin 변경 → SetVerticesDirty() 필요
_dimmer.padding = new Vector2(24f, 24f);
_dimmer.SetVerticesDirty();

// 포커스 인덱스 변경 → SetVerticesDirty 자동 호출 (수동 호출 불필요)
_dimmer.SetFocusIndex(2);
```

---

## 셰이더 구조

**CAT/UI/FocusDimmer**

```
Vertex Shader
  └─ localPos (float2) 전달 — ClipRect & SDF 계산에 XY만 사용

Fragment Shader
  ├─ color = IN.color                  (텍스처 샘플링 없음)
  ├─ [UNITY_UI_CLIP_RECT] Clip 마스크 처리
  └─ RoundedRectSDF(_FocusRect, _CornerRadius)
       └─ smoothstep(-soft, 0, sdf) → inside → color.a *= (1 - inside)
```

### RoundedRectSDF

```hlsl
float RoundedRectSDF(float2 p, float2 center, float2 halfSize, float r)
{
    float2 b = max(halfSize - r, 0.001);
    float2 d = abs(p - center) - b;
    return length(max(d, 0)) + min(max(d.x, d.y), 0) - r;
}
```

구멍 내부는 `sdf < 0`, 외부는 `sdf > 0`.
`smoothstep(-soft, 0, sdf)`로 가장자리 부드럽게 전환.

### 셰이더 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `_FocusRect` | `float4` | 구멍 영역 (xMin, yMin, xMax, yMax) — 로컬 좌표 |
| `_CornerRadius` | `half` | 구멍 모서리 반경 |
| `_HoleSoftness` | `half` | 구멍 가장자리 전환 폭 |

### 셰이더 Variant

| 키워드 | 설명 |
|--------|------|
| (기본) | ClipRect 없음 |
| `UNITY_UI_CLIP_RECT` | ScrollRect / Mask 내부 배치 시 활성화 |

총 **2개** variant.

---

## 성능 특성

| 항목 | 값 |
|------|-----|
| Draw Call | 1 (단일 쿼드) |
| 버텍스 수 | 4 |
| Shader Variant | 2 |
| 텍스처 샘플링 | 없음 |
| GC Allocation | **없음** (Vector3[] 배열 필드 캐싱) |
| Update 비용 | `hasChanged` 플래그 체크 (O(1)) |
| 메시 재빌드 조건 | 활성 타겟 Transform 변경 시에만 |

### 메시 재빌드 vs Material 갱신

| 변경 항목 | 재빌드 종류 |
|-----------|------------|
| 활성 타겟 이동 | 메시 재빌드 (`SetVerticesDirty`) |
| `SetFocusIndex()` 호출 | 메시 재빌드 (자동) |
| `padding` 변경 | 메시 재빌드 (수동 호출) |
| `holeCornerRadius` 변경 | Material 갱신만 (`SetMaterialDirty`) |
| `holeSoftness` 변경 | Material 갱신만 (`SetMaterialDirty`) |
| `color` 변경 | 메시 재빌드 (Graphic 기본 동작) |

---

## 주의사항

- **Soft Mask 내부 배치 시** Stencil 충돌에 주의하세요. 인스펙터의 `Stencil ID` 값을 맞게 조정하세요.
- **빌드 포함**: 에디터 메뉴로 생성 시 셰이더가 자동 할당됩니다. 수동 생성 시 인스펙터 `Shader` 필드에 직접 할당하세요.
- **`holeSoftness = 0`** 시에도 셰이더 내부에서 `max(0, 0.001)`로 epsilon 처리되어 하드 엣지로 렌더됩니다.

---

## 버전 이력

| 버전 | 내용 |
|------|------|
| v2.0.0 | `TutorialFocusDimmer`로 리네임, Focus Target 리스트 지원, 인덱스 기반 포커싱 API, 에디터 메뉴 자동 생성 |
| v1.1.0 | GC 제거 (Vector3[] 캐싱), `hasChanged` 미리셋 버그 수정, Material/Mesh 파라미터 분리, 셰이더 precision 최적화 |
| v1.0.0 | 초기 구현 — 단일 쿼드 + RoundedRectSDF 포커스 구멍 |
