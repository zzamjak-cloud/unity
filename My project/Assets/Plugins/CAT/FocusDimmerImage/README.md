# FocusDimmerImage

튜토리얼 시스템에서 특정 UI 요소를 강조(포커스)하기 위한 딤 오버레이 컴포넌트입니다.
단일 쿼드 메시와 전용 셰이더(SDF)로 둥근 사각형 구멍을 뚫어, 포커스 영역만 밝게 남기고 주변을 어둡게 처리합니다.

## 동작 원리

```
[Canvas 전체]
┌────────────────────────────────────┐
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← 딤 영역 (단일 쿼드)
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░┌──────────────┐░░░░░░░░░░░░│
│░░░░░░░│              │░░░░░░░░░░░░│  ← SDF 구멍 (포커스 대상 + padding)
│░░░░░░░│  focusTarget │░░░░░░░░░░░░│    Raycast 통과
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
Assets/Scripts/FocusDimmerImage/
├── FocusDimmerImage.cs          # MaskableGraphic + ICanvasRaycastFilter 구현
├── Shader/
│   └── CAT_UIFocusDimmer.shader # 포커스 구멍 SDF 셰이더
└── README.md
```

---

## 컴포넌트 설정

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `focusTarget` | `RectTransform` | — | 포커스 구멍을 뚫을 대상 |
| `padding` | `Vector2` | `(0, 0)` | 포커스 영역 여백 (픽셀) |
| `holeCornerRadius` | `float` (0–200) | `16` | 구멍 모서리 라운드 반경 |
| `holeSoftness` | `float` (0–100) | `0` | 구멍 가장자리 소프트 전환 폭. 0 = 하드 엣지 |
| `expansionMargin` | `float` | `200` | 딤 쿼드의 화면 외곽 확장 여백 (해상도 대응) |
| `color` | `Color` | `(0,0,0,0.7)` | 딤 색상 및 투명도 (`Graphic` 상속) |
| `_focusDimmerShader` | `Shader` | — | 셰이더 직접 할당 (빌드에서 `Shader.Find` 실패 시) |

---

## 사용 방법

### 씬 설정

1. 튜토리얼 오버레이 전용 캔버스에 빈 GameObject 생성
2. `FocusDimmerImage` 컴포넌트 추가
3. `RectTransform` → **Stretch All** (앵커를 0,0 ~ 1,1 로 전체 채움)
4. `Focus Target`에 강조할 버튼/패널 할당
5. `Color`의 Alpha 값으로 딤 강도 조절 (권장: 0.6–0.8)

> `expansionMargin` 덕분에 RectTransform이 정확히 전체 화면 크기가 아니어도 됩니다.
> 셰이더 쿼드가 루트 캔버스 외곽까지 자동으로 확장됩니다.

### 스크립트 제어

```csharp
[SerializeField] private FocusDimmerImage _dimmer;

// 포커스 대상 변경
void ShowFocusOn(RectTransform target)
{
    _dimmer.focusTarget      = target;
    _dimmer.padding          = new Vector2(16f, 16f);
    _dimmer.holeCornerRadius = 24f;
    _dimmer.gameObject.SetActive(true);
    _dimmer.SetVerticesDirty();  // focusTarget 변경 후 명시적 갱신 필요
}

void Hide()
{
    _dimmer.gameObject.SetActive(false);
}
```

### DOTween 페이드 예시

```csharp
// 등장 (딤 페이드 인)
_dimmer.gameObject.SetActive(true);
_dimmer.color = new Color(0f, 0f, 0f, 0f);
_dimmer.DOFade(0.7f, 0.3f);

// 퇴장 (딤 페이드 아웃)
_dimmer.DOFade(0f, 0.2f)
    .OnComplete(() => _dimmer.gameObject.SetActive(false));
```

### 런타임 파라미터 변경

```csharp
// holeCornerRadius / holeSoftness 변경 → 메시 재빌드 없이 즉시 반영
_dimmer.holeCornerRadius = 32f;
_dimmer.holeSoftness     = 4f;

// focusTarget / padding / expansionMargin 변경 → SetVerticesDirty() 필요
_dimmer.padding = new Vector2(24f, 24f);
_dimmer.SetVerticesDirty();
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
| 메시 재빌드 조건 | focusTarget Transform 변경 시에만 |

### 메시 재빌드 vs Material 갱신

| 변경 항목 | 재빌드 종류 |
|-----------|------------|
| `focusTarget` 이동 | 메시 재빌드 (`SetVerticesDirty`) |
| `focusTarget` 변경 (다른 오브젝트 할당) | 메시 재빌드 (수동 호출) |
| `padding` 변경 | 메시 재빌드 (수동 호출) |
| `holeCornerRadius` 변경 | Material 갱신만 (`SetMaterialDirty`) |
| `holeSoftness` 변경 | Material 갱신만 (`SetMaterialDirty`) |
| `color` 변경 | 메시 재빌드 (Graphic 기본 동작) |

---

## 주의사항

- **`focusTarget` 교체 후** `SetVerticesDirty()` 수동 호출이 필요합니다. 프로퍼티가 아닌 public 필드이므로 setter 훅이 없습니다.
- **Soft Mask 내부 배치 시** Stencil 충돌에 주의하세요. 인스펙터의 `Stencil ID` 값을 맞게 조정하세요.
- **빌드 포함**: `CAT_UIFocusDimmer.shader`가 빌드에 자동 포함되지 않는 경우 인스펙터 `Shader` 필드에 직접 할당하거나 `Resources` 폴더에 배치하세요.
- **`holeSoftness = 0`** 시에도 셰이더 내부에서 `max(0, 0.001)`로 epsilon 처리되어 하드 엣지로 렌더됩니다.

---

## 버전 이력

| 버전 | 내용 |
|------|------|
| v1.1.0 | GC 제거 (Vector3[] 캐싱), `hasChanged` 미리셋 버그 수정, Material/Mesh 파라미터 분리, 셰이더 precision 최적화 |
| v1.0.0 | 초기 구현 — 단일 쿼드 + RoundedRectSDF 포커스 구멍 |
