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
│   └── ColorReplace.cs          # 에디터 전용 워크플로 컴포넌트
├── Editor/
│   └── ColorReplaceEditor.cs    # 커스텀 인스펙터 (머티리얼 저장/갱신/프리뷰)
├── Shader/
│   └── CAT_ColorReplace.shader  # HSV 색상 변환 셰이더
└── Materials/                   # 저장된 머티리얼 에셋 (자동 생성)
```

---

## 핵심 구현

### Material 에셋 저장 방식

빌드 시 셰이더 누락을 방지하기 위해 **에디터에서 Material을 에셋으로 직접 저장**합니다.

#### 워크플로

1. ColorReplace 컴포넌트를 오브젝트에 추가
2. 인스펙터에서 HSV 옵션 조정
3. "프리뷰" 버튼으로 실시간 확인
4. **"머티리얼 저장"** 버튼으로 Material 에셋 생성 → 렌더러에 자동 할당
5. 옵션 변경 후 **"머티리얼 갱신"** 버튼으로 기존 에셋 업데이트

#### 빌드 안전성

| 항목 | 설명 |
|------|------|
| 셰이더 참조 | Material 에셋이 셰이더를 직접 참조 → 빌드 시 자동 포함 |
| Addressable | 머티리얼이 셰이더를 의존성으로 포함 → 번들에 자동 포함 |
| `Shader.Find()` | 런타임에서 호출하지 않음 → 셰이더 스트리핑 영향 없음 |

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
| `#pragma target 2.0` | 구형 모바일 GPU까지 호환 |

---

## 사용법

### 기본 사용

1. `SpriteRenderer` 또는 UI `Image`/`RawImage`가 붙은 오브젝트에 `ColorReplace` 컴포넌트 추가
2. 인스펙터에서 **Target Color**를 교체할 색상으로 설정
3. **Similar** 버튼으로 HSV 범위 자동 설정
4. **HSV Adjust**에서 H(색조)·S(채도)·V(명도)·A(알파) 조정값 입력
5. **프리뷰** 버튼으로 결과 확인
6. **머티리얼 저장** 버튼으로 Material 에셋 생성 및 렌더러에 할당

### 머티리얼 갱신

이미 저장된 머티리얼이 할당된 상태에서 HSV 값을 변경한 후:
- **머티리얼 갱신** 버튼을 클릭하면 기존 에셋의 프로퍼티가 업데이트됩니다

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
| 머티리얼 저장 | 현재 HSV 설정으로 Material 에셋 생성 후 렌더러에 할당 |
| 머티리얼 갱신 | 이미 저장된 머티리얼의 HSV 값을 현재 설정으로 업데이트 |
| 프리뷰 / 프리뷰 해제 | 임시 머티리얼로 실시간 미리보기 토글 |
| Reset | 모든 값을 기본값(Min=0, Max=1, Adjust=0)으로 초기화 |

---

## 주의사항

### 에디터 전용 컴포넌트

- ColorReplace 컴포넌트는 **에디터 워크플로 도구**입니다
- 런타임에서는 저장된 Material 에셋이 렌더러에 직접 할당되어 있으므로 컴포넌트가 불필요합니다
- 빌드 시 컴포넌트를 제거해도 무방합니다

### 머티리얼 저장 경로

- 기본 경로: `Assets/Plugins/CAT/ColorReplace/Materials/`
- 오브젝트 이름으로 파일명이 결정됩니다
- 동일 이름이 존재하면 자동으로 넘버링됩니다 (예: `MyObject 1.mat`)

### 아틀라스(Sprite Atlas) 사용 시

- 아틀라스 스프라이트도 저장된 머티리얼이 셰이더를 직접 참조하므로 빌드에 안전합니다
- UV는 셰이더가 스프라이트별 UV를 그대로 사용하므로 정상 동작합니다

### 에디터에서의 동작

- 프리뷰 기능은 임시 Material(`HideFlags.HideAndDontSave`)을 사용합니다
- 프리뷰 해제 시 원본 머티리얼로 자동 복원됩니다
- 인스펙터를 닫으면 프리뷰가 자동으로 해제됩니다
