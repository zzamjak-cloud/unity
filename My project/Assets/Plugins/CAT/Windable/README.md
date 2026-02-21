# CAT Windable v1.0.0

노이즈 텍스처 기반 바람 효과 컴포넌트 (SpriteRenderer / UI Image 통합 지원)

## 개요

노이즈 텍스처를 이용하여 스프라이트 또는 UI 이미지에 바람에 흔들리는 효과를 적용합니다. `SpriteRenderer`와 Unity UI `Graphic` 컴포넌트를 자동 감지하여 동일한 셰이더로 처리하며, Sprite Atlas UV 보정과 ClipRect(ScrollView) 클리핑을 지원합니다.

```
Assets/Plugins/CAT/Windable/
├── Scripts/
│   └── Windable.cs               # 메인 컴포넌트 (304줄)
├── Shader/
│   └── CAT_Windable.shader       # 바람 효과 셰이더 (174줄)
└── Editor/
    └── WindableEditor.cs         # 커스텀 인스펙터 (352줄)
```

## 주요 기능

| 기능 | 설명 |
|------|------|
| **컴포넌트 자동 감지** | `SpriteRenderer` 또는 UI `Graphic` 자동 인식 및 타입 결정 |
| **UV 회전** | 노이즈 샘플링 UV를 0~360° 회전하여 바람 방향 조정 |
| **노이즈 기반 변위** | 노이즈 텍스처의 R채널 값으로 UV 오프셋 계산 |
| **Sprite Atlas 호환** | `textureRect` / `pivot` 기반 UV 보정으로 Atlas 스프라이트 정확한 처리 |
| **ClipRect 지원** | ScrollView / RectMask2D 클리핑 영역 반영 (`_ClipRect`) |
| **UI Mask 호환** | Stencil 프로퍼티 내장으로 UI Mask 내 정상 동작 |
| **에디터 미리보기** | 에디터 전용 10초 애니메이션 테스트 (`EditorApplication.update` 활용) |

## 아키텍처

### 렌더링 흐름

```
Windable (컴포넌트)
  ├─ Awake()  → ValidateComponents()   : SpriteRenderer / Graphic 감지 + 타입 결정
  ├─ OnEnable()  → SetupMaterial()     : 인스턴스 Material 생성 + 초기 프로퍼티 전달
  ├─ Update()                          : _CustomTime = Time.time (매 프레임 셰이더에 전달)
  └─ OnDisable() → CleanupMaterial()   : Material 파괴 + 원본 Material null 복원

CAT_Windable.shader (프래그먼트)
  1. ClipRect 클리핑 (월드 포지션 기반)
  2. RotateUV()  : UV를 스프라이트 중심 기준으로 회전
  3. noiseSampleUV = (rotatedUV + WindDirection * time) * WindScale
  4. noiseValue = NoiseTex.r  →  windEffect = noiseValue * WindStrength * 0.1
  5. centeredUV += windOffset + scale 축소 + ImageOffset
  6. SpriteUVRect 범위 클리핑
  7. tex2D(_MainTex, finalUV) * vertexColor
```

### 컴포넌트 타입 결정 규칙

```
Awake() ValidateComponents()
  ├─ SpriteRenderer 있음?  → WindableType.Sprite
  │    └─ Graphic도 있으면? → 경고 로그 (SpriteRenderer 우선)
  ├─ Graphic만 있음?       → WindableType.UI
  └─ 둘 다 없음?           → 에러 로그
```

### Material 수명 주기

- `OnEnable()`에서 `new Material(shader)` 인스턴스 생성 → 컴포넌트에 할당
- `OnDisable()`에서 `Destroy()` 호출 + 컴포넌트 Material `null` 복원
- 인스턴스별 개별 Material (공유 없음) — 오브젝트마다 독립적인 바람 타이밍 가능

## 인스펙터 프로퍼티

### 기본 설정

| 프로퍼티 | 설명 | 기본값 |
|---------|------|-------|
| **노이즈 텍스처** | 바람 변위에 사용할 노이즈 텍스처 (`_NoiseTex`) | white |
| **UV 회전** | 노이즈 샘플링 UV 회전각 0~360° (`_RotateUV`) | 0 |
| **바람 속도** | 노이즈 UV 스크롤 속도 (`_WindSpeed`) | 0.2 |
| **바람 강도** | UV 변위 크기 (`_WindStrength`) | 0.5 |
| **바람 주파수** | 속도에 곱해지는 주파수 계수 (`_WindFrequency`) | 0.2 |

### 고급 설정

| 프로퍼티 | 설명 | 기본값 |
|---------|------|-------|
| **바람 방향** | XY 방향 벡터 (`_WindDirection`) | (1, 1, 0, 0) |
| **바람 스케일** | 노이즈 텍스처 샘플링 스케일 (`_WindScale`) | 1.0 |

### 이미지 오프셋

| 프로퍼티 | 설명 | 기본값 |
|---------|------|-------|
| **X 오프셋** | UV X축 미세 조정 (`_ImageOffsetX`) | 0.3 |
| **Y 오프셋** | UV Y축 미세 조정 (`_ImageOffsetY`) | 0.3 |
| **이미지 스케일** | UV 스케일 (1.0 이상 = 이미지 확대) (`_ImageScale`) | 1.1 |

> `_MainTex`와 `_ClipRect`는 C# 코드에서 자동으로 설정되며 인스펙터에 노출되지 않습니다.

## 셰이더 상세

### 내부 파라미터 (C#에서 자동 설정)

| 파라미터 | 설명 |
|---------|------|
| `_CustomTime` | `Time.time` 값. 에디터 테스트 시 경과 시간으로 대체 |
| `_SpriteUVRect` | Atlas 내 스프라이트의 UV 영역 `(xMin, yMin, xMax, yMax)` |
| `_SpritePivot` | Atlas UV 공간 기준 스프라이트 피벗 좌표 |
| `_ClipRect` | ScrollView / RectMask2D 클리핑 사각형 |

### 바람 변위 계산

```hlsl
// 1. 노이즈 UV 샘플링 (시간에 따라 스크롤)
float2 noiseSampleUV = (rotatedUV + WindDirection * time * WindFrequency) * WindScale;
float noiseValue = tex2D(_NoiseTex, noiseSampleUV).r;

// 2. 변위량 계산
float windEffect = noiseValue * WindStrength * 0.1;
float2 windOffset = normalize(WindDirection) * windEffect;

// 3. UV 변환 (피벗 기준)
centeredUV += windOffset;
centeredUV *= (1.0 - windEffect * 0.5);  // 변위 시 미세 축소
centeredUV *= ImageScale;
centeredUV += ImageOffset;
```

### Sprite Atlas 처리

```csharp
// C#: Atlas 내 스프라이트 UV 영역 계산
Rect r = sprite.textureRect;
Texture t = sprite.texture;

Vector4 uvRect = new Vector4(
    r.x / t.width, r.y / t.height,         // xMin, yMin
    (r.x + r.width) / t.width,             // xMax
    (r.y + r.height) / t.height            // yMax
);

// 피벗도 Atlas UV 공간으로 변환
float pivotX = (r.x + sprite.pivot.x) / t.width;
float pivotY = (r.y + sprite.pivot.y) / t.height;
```

```hlsl
// Shader: Atlas 범위 클리핑
clip(finalUV.x - _SpriteUVRect.x);
clip(_SpriteUVRect.z - finalUV.x);
clip(finalUV.y - _SpriteUVRect.y);
clip(_SpriteUVRect.w - finalUV.y);
```

## 에디터 도구

### 커스텀 인스펙터 기능

- **감지된 타입**: 자동으로 감지된 컴포넌트 타입을 읽기 전용으로 표시
- **사용 중인 컴포넌트 정보**: 현재 사용 중인 Sprite 이름 표시
- **경고 메시지**: 필수 컴포넌트 누락 / 스프라이트 미할당 / 컴포넌트 중복 시 안내
- **컴포넌트 자동 추가 버튼**: SpriteRenderer / Image 누락 시 원클릭 추가

### 테스트 버튼

| 버튼 | 설명 |
|------|------|
| **▶️ 바람 효과 테스트 (10초)** | `EditorApplication.update`로 10초간 애니메이션 미리보기 |
| **⏹️ 테스트 중지** | 테스트 중단 + `_CustomTime = 0` 리셋 |
| **🔄 즉시 업데이트** | 현재 프로퍼티 값으로 Material 즉시 갱신 |
| **🔄 효과 리셋** | `_CustomTime = 0`으로 초기 상태 복원 |

## 사용법

### 기본 사용

1. `SpriteRenderer` 또는 `Image` 컴포넌트가 있는 오브젝트 선택
2. `Add Component > CAT > Effects > Windable` 추가
3. `_NoiseTex`에 노이즈 텍스처 할당
4. 인스펙터에서 바람 파라미터 조정 후 **▶️ 바람 효과 테스트** 버튼으로 미리보기

### SpriteRenderer 사용 예

```
[SpriteRenderer + Windable]
  - Noise Texture: noise_cloud (노이즈 텍스처)
  - Wind Speed: 0.2
  - Wind Strength: 0.5
  - Wind Direction: (1, 0.5, 0, 0)  ← 오른쪽 위 방향
```

### UI Image 사용 예

```
[Canvas]
  └── [Image + Windable]
       - Image.sprite: grass_sprite (스프라이트)
       - Noise Texture: noise_perlin
       - Wind Strength: 0.8  ← 강한 흔들림
```

### 스크립트 API

```csharp
Windable windable = GetComponent<Windable>();

// 현재 타입 확인
WindableType type = windable.WindableTypeValue; // Sprite 또는 UI

// Material 프로퍼티 수동 갱신 (주로 에디터에서 사용)
windable.UpdateMaterialProperties();

// 특정 시간으로 리셋
windable.UpdateMaterialProperties(customTime: 0f);
```

## 성능 특성

| 항목 | 비용 |
|------|------|
| Material 인스턴스 | Windable 컴포넌트당 1개 (공유 없음) |
| Update 비용 | `Material.SetFloat()` 1회 (`_CustomTime`) |
| 텍스처 샘플링 | `_MainTex` 1회 + `_NoiseTex` 1회 |
| 추가 패스 | 없음 (단일 패스) |

### 주의 사항

1. **개별 Material 인스턴스**: 각 Windable이 독립 Material을 생성하므로, 같은 설정의 오브젝트가 다수인 경우 배칭이 깨집니다. 바람 타이밍이 동일해도 무방한 경우 Material을 공유하도록 개선을 고려하세요.

2. **매 프레임 SetFloat**: `Update()`에서 `_CustomTime`을 매 프레임 설정합니다. 비활성 상태에서는 `OnDisable()`로 Material이 파괴되므로 불필요한 비용은 없습니다.

3. **노이즈 텍스처 해상도**: 낮은 해상도(64x64, 128x128)의 노이즈 텍스처로도 충분합니다. Bilinear 필터 + Repeat 랩 모드를 권장합니다.

4. **Atlas 스프라이트 클리핑**: `clip()` 명령으로 Atlas 범위를 벗어난 픽셀을 제거합니다. 변위가 클수록 이미지 엣지에서 픽셀이 잘릴 수 있습니다. `_ImageOffsetX/Y`, `_ImageScale`로 보정하세요.

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0.x) | O |
| URP 17.2.0 | O |
| SpriteRenderer | O |
| UI Image | O |
| UI RawImage | O (UV Rect 자동 설정) |
| Sprite Atlas | O (UV 보정 포함) |
| UI Mask (Stencil) | O |
| RectMask2D / ScrollView | O (`_ClipRect`) |
| 에디터 미리보기 | O (10초 테스트) |
| GPU Instancing | X (인스턴스별 _CustomTime 불일치) |

## 제한사항

- 개별 Material 인스턴스 사용으로 **배칭 최적화 불가**
- **중첩 Windable** 미지원 (자식 Windable은 독립적으로 동작)
- 바람 효과는 **UV 변위 방식**이므로 실제 메시 변형 없음 (버텍스 이동 효과 없음)
- `_CustomTime`이 `float` 범위를 초과하는 장시간 실행 시 정밀도 저하 가능 (수 시간 이상)
