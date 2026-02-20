# ColorReplace

특정 색상 범위(HSV)를 실시간으로 교체하는 모바일 최적화 이펙트 컴포넌트입니다.
`SpriteRenderer`와 UI `Graphic`(Image, RawImage) 모두 지원합니다.

---

## 목차

- [개요](#개요)
- [파일 구조](#파일-구조)
- [핵심 구현](#핵심-구현)
- [모바일 최적화](#모바일-최적화)
- [사용법](#사용법)
- [인스펙터 가이드](#인스펙터-가이드)
- [런타임 API](#런타임-api)
- [주의사항](#주의사항)

---

## 개요

RGB 픽셀을 HSV 공간으로 변환 후, 지정한 Hue 범위에 속하는 픽셀에만 H·S·V·A 조정값을 적용합니다.
캐릭터 색상 변경, 팀 컬러 적용, 아이템 등급별 색상 표현 등에 활용할 수 있습니다.

```
입력 픽셀 → RGB→HSV 변환 → 범위(RangeMin~RangeMax) 해당 여부 판단 → HSV 조정 적용 → HSV→RGB 변환 → 출력
```

---

## 파일 구조

```
ColorReplace/
├── Scripts/
│   └── ColorReplace.cs          # 메인 컴포넌트
├── Editor/
│   └── ColorReplaceEditor.cs    # 커스텀 인스펙터
└── Shader/
    └── CAT_ColorReplace.shader  # HSV 색상 변환 셰이더
```

---

## 핵심 구현

### Material 공유 전략

Draw Call을 최소화하기 위해 두 가지 방식으로 Material을 공유합니다.

#### SpriteRenderer

- **Material**: 텍스처 단위로 공유 (`Dictionary<textureId, Material>`)
- **HSV 값**: `MaterialPropertyBlock`으로 오브젝트별 개별 설정
- 같은 텍스처를 쓰는 모든 SpriteRenderer가 동일 Material을 참조하면서도, 각자 다른 HSV 값을 가질 수 있어 **GPU 인스턴싱·배칭이 유지**됩니다

```csharp
// 텍스처가 같으면 Material 공유
if (!spriteMaterialCache.TryGetValue(textureId, out currentMaterial))
{
    currentMaterial = new Material(shader) { ... };
    spriteMaterialCache[textureId] = currentMaterial;
}
// 개별 HSV 값은 PropertyBlock으로 설정
spriteRenderer.SetPropertyBlock(propertyBlock);
```

#### UI Graphic (Image / RawImage)

- UI는 `MaterialPropertyBlock`을 지원하지 않으므로, **(텍스처 ID + HSV 설정값)의 해시**로 Material을 캐싱합니다
- 완전히 동일한 설정의 UI 컴포넌트들은 하나의 Material을 공유합니다

```csharp
// 텍스처 + HSV 설정이 모두 같으면 Material 공유
int cacheKey = hash(textureId, hsvRangeMin, hsvRangeMax, hsvAdjust);
if (!uiMaterialCache.TryGetValue(cacheKey, out currentMaterial))
{
    currentMaterial = new Material(shader) { ... };
    uiMaterialCache[cacheKey] = currentMaterial;
}
```

### 셰이더 구조

단일 셰이더에 두 개의 SubShader가 포함되어 있습니다.

| SubShader | 대상 | 특징 |
|-----------|------|------|
| SubShader 0 | UI Graphic | Stencil, ClipRect, AlphaClip 지원 |
| SubShader 1 | SpriteRenderer | GPU 인스턴싱, PixelSnap 지원 |

공통 변환 함수(`RGB2HSV`, `HSV2RGB`, `ComputeAffectMult`)는 `CGINCLUDE` 블록에 한 번만 선언되어 두 SubShader에서 공유합니다.

### Hue Wrap-around 처리

HSV에서 빨간색은 Hue 0(=1) 경계에 걸쳐 있습니다.
`RangeMin > RangeMax`이면 wrap-around로 간주하여 분기문 없이 처리합니다.

```hlsl
// 분기 없이 수학 연산으로 wrap-around 처리
half isWrapped = step(rangeMax + 0.001h, rangeMin);
half normalCase  = step(rangeMin, hue) * step(hue, rangeMax);
half wrappedCase = saturate(step(rangeMin, hue) + step(hue, rangeMax));
return lerp(normalCase, wrappedCase, isWrapped);
```

예시: 빨간색 범위 `Min=0.95, Max=0.05` → Hue 0.95~1.0 및 0.0~0.05 구간에 효과 적용

---

## 모바일 최적화

### 셰이더

| 기법 | 내용 |
|------|------|
| `half` precision | 모든 색상 연산에 16비트 사용 (float는 vertex 좌표/UV만) |
| 분기문 제거 | `if` 대신 `step()`, `lerp()`, `saturate()` 수학 연산 사용 |
| `CGINCLUDE` 공유 | 두 SubShader가 동일 함수를 중복 컴파일하지 않음 |
| `[PerRendererData]` | PropertyBlock 방식으로 배칭 유지 지원 |
| `#pragma target 2.0` | 구형 모바일 GPU까지 호환 |

### C# 런타임

| 기법 | 내용 |
|------|------|
| Material 공유 | 같은 설정이면 새 Material 생성 없이 캐시에서 반환 |
| PropertyBlock 재사용 | Awake에서 생성, 매 프레임 내용만 갱신 |
| Static Property ID | `Shader.PropertyToID()` 결과를 `static readonly`로 캐싱 |
| 컴포넌트 캐싱 | `GetComponent<T>()`는 `Awake/Start`에서 한 번만 호출 |
| `HideFlags.DontSave` | 런타임 생성 Material에 설정하여 에디터 직렬화 오류 방지 |

---

## 사용법

### 기본 사용

1. `SpriteRenderer` 또는 UI `Image`/`RawImage`가 붙은 오브젝트에 `ColorReplace` 컴포넌트 추가
2. 인스펙터에서 **Target Color**를 교체할 색상으로 설정
3. **Similar** 버튼으로 HSV 범위 자동 설정
4. **HSV Adjust**에서 H(색조)·S(채도)·V(명도)·A(알파) 조정값 입력

### 런타임 코드

```csharp
var cr = GetComponent<ColorReplace>();

// HSV 범위 직접 설정
cr.HSVRangeMin = 0.95f;
cr.HSVRangeMax = 0.05f;  // wrap-around (빨간색)

// HSV 조정값 설정 (H, S, V, A)
cr.HSVAdjust = new Vector4(0.5f, 0f, 0f, 0f);  // 색조를 0.5(반바퀴) 이동

// 색상 기반 자동 범위 설정 (tolerance = 허용 오차)
cr.SetHSVRangeFromColor(Color.red, tolerance: 0.05f);
```

### 씬 전환 시 캐시 정리

```csharp
// 씬 전환 전 호출하여 Material 캐시 해제 (메모리 누수 방지)
ColorReplace.ClearMaterialCache();
```

---

## 인스펙터 가이드

### Color Picker

| 항목 | 설명 |
|------|------|
| Target Color | 교체 대상 색상. 이 색상의 Hue를 기준으로 범위 자동 계산 |
| Compact | tolerance 0.02 — 매우 좁은 범위 (단색에 가까울 때) |
| Similar | tolerance 0.10 — 보통 범위 (기본값 권장) |
| Wide | tolerance 0.20 — 넓은 범위 (유사 색조까지 포함) |

### HSV Range

| 항목 | 설명 |
|------|------|
| Min | 효과를 적용할 Hue 최솟값 (0~1) |
| Max | 효과를 적용할 Hue 최댓값 (0~1) |

> **Hue 레인보우 바**를 통해 선택된 범위를 시각적으로 확인할 수 있습니다.

`Min > Max`이면 wrap-around 모드가 활성화됩니다 (예: 빨간색 Min=0.95, Max=0.05).

### HSV Adjust

| 채널 | 범위 | 설명 |
|------|------|------|
| H | -1 ~ 1 | 색조 이동 (0.5 = 보색으로 이동) |
| S | -1 ~ 1 | 채도 조정 (-1 = 그레이스케일) |
| V | -1 ~ 1 | 명도 조정 |
| A | -1 ~ 1 | 알파 조정 |

### 버튼

| 버튼 | 설명 |
|------|------|
| Reset | 모든 값을 기본값(Min=0, Max=1, Adjust=0)으로 초기화 |
| Clear Cache | (플레이 모드 전용) Material 캐시를 즉시 비움 |

---

## 주의사항

### SpriteRenderer vs UI 동작 차이

| | SpriteRenderer | UI Graphic |
|---|---|---|
| Material 공유 기준 | 텍스처 동일 여부 | 텍스처 + HSV 설정 모두 동일 |
| 개별 값 설정 방식 | MaterialPropertyBlock | 별도 Material 생성 |
| HSV 값 변경 비용 | 매우 낮음 (PropertyBlock 갱신) | 새 Material 생성 가능성 있음 |

UI의 경우 런타임에 HSV 값을 자주 바꾸면 고유한 설정마다 새 Material이 생성됩니다.
**빈번한 런타임 변경이 필요하다면 SpriteRenderer 방식을 권장합니다.**

### 아틀라스(Sprite Atlas) 사용 시

- 아틀라스 스프라이트는 아틀라스 텍스처 ID로 캐시 키가 결정됩니다
- 같은 아틀라스의 서로 다른 스프라이트라도 **동일한 Material을 공유**합니다
- UV는 셰이더가 스프라이트별 UV를 그대로 사용하므로 정상 동작합니다

### ClearMaterialCache() 호출 시점

- 씬 전환 후 이전 씬의 텍스처가 해제되면 캐시 내 Material의 텍스처 참조가 무효화됩니다
- `SceneManager.sceneUnloaded` 이벤트 또는 씬 전환 직전에 `ClearMaterialCache()`를 호출하세요

```csharp
void OnDestroy()
{
    ColorReplace.ClearMaterialCache();
}
```

### 에디터에서의 동작

- 씬에 배치된 오브젝트에서만 초기화됩니다 (`gameObject.scene.IsValid()` 체크)
- 프리팹 에셋을 직접 편집할 때는 Material이 적용되지 않습니다 (씬에 배치 후 확인)
- `OnValidate()`에서 인스펙터 값 변경 시 자동으로 Material에 반영됩니다
