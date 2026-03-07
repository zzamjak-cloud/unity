# SoftMaskLight v2.0.0

알파 채널 기반 1-Pass 소프트 마스킹 컴포넌트 (모바일 최적화)

> 이전 이름: CAT SoftMask. v2.0.0에서 SoftMaskLight로 리네임 + Optional Shader 패턴 도입.
> `[MovedFrom]` 속성으로 기존 씬 직렬화 호환성 유지됨.

## 개요

Unity UI의 기본 `Mask` 컴포넌트는 Stencil 기반 바이너리 클리핑만 지원합니다.
SoftMaskLight는 **텍스처 알파 채널 기반 부드러운 마스킹**을 **RenderTexture 없이 단일 패스**로 처리하여 모바일 환경에 최적화되어 있습니다.

```
Assets/Plugins/CAT/SoftMaskLight/
├── Scripts/
│   ├── SoftMaskLight.cs                  # 메인 컴포넌트
│   ├── SoftMaskLightSettings.cs          # Resources 빌드 포함 설정 (ScriptableObject)
│   └── UIEffectSoftMaskLightProxy.cs     # UIEffect 프록시 (IMaterialModifier)
├── Shader/
│   ├── SoftMaskLight_Default.shader      # UI + Sprite 마스크 셰이더
│   ├── SoftMaskLight_TMP.shader          # TextMeshPro 전용 마스크 셰이더
│   ├── SoftMaskLight_Core.cginc          # Hidden 변형 셰이더용 공용 마스크 샘플링
│   ├── SoftMaskLight_UIAdditive.shader   # 파티클 Additive (원본 + mob-sakai SoftMaskable 지원)
│   ├── SoftMaskLight_UIAlphaBlend.shader # 파티클 AlphaBlend (원본 + mob-sakai SoftMaskable 지원)
│   ├── UIDefault_UIEffect.shader         # UIEffect 셰이더 오버라이드 (_CAT_SOFTMASK + mob-sakai SoftMaskable)
│   └── Hidden/                           # Optional Shader 변형들
│       ├── Hidden-SoftMaskLight-UI-Default.shader               # UI/Default (SoftMaskLight)
│       ├── Hidden-SoftMaskLight-UIEffect.shader                 # UIEffect (SoftMaskLight) [비활성]
│       ├── Hidden-SoftMaskLight-Particles-UIAdditive.shader     # Particle Additive (SoftMaskLight)
│       ├── Hidden-SoftMaskLight-Particles-UIAlphaBlend.shader   # Particle AlphaBlend (SoftMaskLight)
│       ├── Hidden-SoftMaskable-Particles-UIAdditive.shader      # Particle Additive (mob-sakai SoftMaskable)
│       └── Hidden-SoftMaskable-Particles-UIAlphaBlend.shader    # Particle AlphaBlend (mob-sakai SoftMaskable)
├── Resources/
│   └── SoftMaskLightSettings.asset       # Hidden 셰이더 빌드 포함 에셋 (자동 생성)
└── Editor/
    ├── SoftMaskLightEditor.cs            # 커스텀 인스펙터
    └── SoftMaskLightInstaller.cs         # 자동 설치/설정 관리자
```

## 마스킹 전략 (컴포넌트 타입별)

SoftMaskLight는 자식 오브젝트의 타입에 따라 서로 다른 마스킹 전략을 사용합니다:

| 자식 타입 | 마스킹 방식 | Material 관리 |
|-----------|------------|---------------|
| **일반 UI (Image, RawImage)** | Optional Shader (Hidden 변형) | 공유 Material (배칭 유지) |
| **커스텀 셰이더 UI (ColorReplace 등)** | Optional Shader (Hidden 변형) | 개별 복제 Material (프로퍼티 보존) |
| **TextMeshPro** | 전용 셰이더 (`SoftMaskLight/UI/TMP_SoftMask`) | 개별 Material (폰트 아틀라스별) |
| **UIParticle** | Optional Shader (Hidden 변형) | 개별 복제 Material (블렌드 모드 보존) |
| **UIEffect** | 키워드 활성화 (`_CAT_SOFTMASK`) | IMaterialModifier 프록시 |

## v2.0.0 주요 변경: Optional Shader 패턴

### 기존 (v1.x) 방식
- 원본 셰이더에 `#pragma multi_compile_local _ _CAT_SOFTMASK` 키워드 삽입
- 모든 자식을 단일 `CAT/UI/SoftMask` 셰이더로 교체
- 새 셰이더마다 Core.cginc include + 매크로 통합 필요

### 새로운 (v2.0) Optional Shader 패턴
- **원본 셰이더는 마스킹 코드 없이 깨끗하게 유지**
- 마스킹 필요 시 `Hidden/{원본셰이더이름} (SoftMaskLight)` 변형 셰이더를 자동 탐색
- 변형 셰이더에 `#define _CAT_SOFTMASK 1` + `#include "SoftMaskLight_Core.cginc"` → 마스킹 항상 활성
- `_CAT_SOFTMASK` multi_compile 불필요 → 키워드 변형 제거 → 빌드 크기 절감
- 같은 Optional Shader를 사용하는 자식끼리 Material 공유 (배칭 유지)

### UIEffect 예외: 키워드 방식
UIEffect는 `shader_feature_local_fragment` 키워드를 사용하여 다양한 효과를 조합합니다.
Hidden 셰이더로 교체하면 이 키워드 변형이 손실되므로, UIEffect에서만 키워드 방식을 유지합니다:
- `UIDefault_UIEffect.shader`에 `#pragma multi_compile_local _ _CAT_SOFTMASK` 유지
- `UIEffectSoftMaskLightProxy`가 `IMaterialModifier`로 `_CAT_SOFTMASK` 키워드 활성화
- 셰이더 교체 없이 UIEffect의 모든 효과 조합과 호환

### FindOptionalShader() 탐색 로직
```
1. FindOptionalShader(originalShader)
2. "Hidden/{originalShader.name} (SoftMaskLight)" 검색
3. 이미 "Hidden/" 접두사가 있으면 "{name} (SoftMaskLight)" (중복 접두사 방지)
4. 없으면 "Hidden/UI/Default (SoftMaskLight)" 폴백
5. InstanceID 기반 캐싱 → 같은 원본 셰이더에 대해 1회만 Shader.Find()
```

### 커스텀 셰이더에 SoftMaskLight 지원 추가

새 셰이더를 만들 때 SoftMaskLight 대응이 필요하면 Hidden 변형 셰이더를 생성합니다:

```hlsl
// [OptionalShader] SoftMaskLight: YourNamespace/YourShader
Shader "Hidden/YourNamespace/YourShader (SoftMaskLight)"
{
    Properties
    {
        // --- 원본 셰이더 프로퍼티 그대로 복사 ---

        // --- SoftMaskLight 마스크 프로퍼티 (필수) ---
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Softness ("Softness", Range(0, 1)) = 0.1
        _InvertMask ("Invert Mask", Float) = 0
        _MaskWorldToUV ("World To UV Matrix", Vector) = (0,0,0,0)
        _MaskUVRect ("Mask UV Rect", Vector) = (0,0,1,1)
        _MaskSliceBorder ("Slice Border", Vector) = (0,0,1,1)
        _MaskSliceInnerUV ("Slice Inner UV", Vector) = (0,0,1,1)
        // 중첩 마스크용 (동일 프로퍼티에 2 접미사)
        _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        _Softness2 ("Softness 2", Range(0, 1)) = 0.1
        _InvertMask2 ("Invert Mask 2", Float) = 0
        _MaskWorldToUV2 ("World To UV Matrix 2", Vector) = (0,0,0,0)
        _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0,0,1,1)
        _MaskSliceBorder2 ("Slice Border 2", Vector) = (0,0,1,1)
        _MaskSliceInnerUV2 ("Slice Inner UV 2", Vector) = (0,0,1,1)
    }
    SubShader
    {
        // 원본 셰이더의 Tags, Blend, Cull, ZWrite 등 그대로 복사

        Pass
        {
            CGPROGRAM
            // 원본 셰이더의 #pragma 그대로 복사

            // SoftMaskLight 키워드 (multi_compile_local 사용)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE

            // SoftMaskLight Core include (항상 활성)
            #define _CAT_SOFTMASK 1
            #include "SoftMaskLight_Core.cginc"

            // 원본 셰이더의 uniform, struct 등 그대로 복사

            struct v2f {
                // 원본 v2f 멤버 그대로
                CAT_SOFTMASK_COORDS(N, N+1)  // TEXCOORD 슬롯 2개 필요
            };

            v2f vert(appdata IN) {
                v2f OUT;
                // 원본 vertex 로직 그대로
                CAT_SOFTMASK_VERT(IN.vertex.xyz, OUT)
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target {
                // 원본 fragment 로직 그대로
                fixed4 col = ...;
                col.a *= CAT_SOFTMASK_FRAG(IN)  // 마스크 알파 적용
                return col;
            }
            ENDCG
        }
    }
}
```

**등록 필수**: `SoftMaskLightInstaller.cs`의 `HiddenShaderNames` 배열에 셰이더 이름을 추가하거나,
셰이더 이름에 `(SoftMaskLight)` 접미사를 포함하면 자동 탐색됩니다.

## mob-sakai SoftMask (SoftMaskable) 공존

이 프로젝트는 mob-sakai의 `com.coffee.softmask-for-ugui`와 독립적으로 공존합니다.
mob-sakai SoftMask는 `(SoftMaskable)` 접미사 기반 Optional Shader 패턴을 사용하며,
SoftMaskLight는 `(SoftMaskLight)` 접미사를 사용하여 충돌 없이 동작합니다.

### mob-sakai SoftMaskable 파티클 변형 셰이더

mob-sakai SoftMask의 자식에 파티클을 배치할 때, mob-sakai가 `Hidden/UI/Default (SoftMaskable)`로
폴백하면 블렌드 모드가 잘못 적용됩니다. 이를 방지하기 위해 전용 변형 셰이더를 제공합니다:

| 원본 셰이더 | mob-sakai 변형 |
|-------------|---------------|
| `SoftMaskLight/Particles/UIAdditive` | `Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskable)` |
| `SoftMaskLight/Particles/UIAlphaBlend` | `Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskable)` |

### UIEffect 셰이더의 SoftMaskable 지원

`UIDefault_UIEffect.shader`에는 mob-sakai의 `SOFTMASKABLE` shader_feature도 포함되어 있어,
UIEffect가 mob-sakai SoftMask 자식에서도 정상 동작합니다.

## 주요 기능

| 기능 | 설명 |
|------|------|
| **알파 마스킹** | 자신의 UI Graphic 알파 채널을 마스크로 사용 |
| **Softness 조절** | `smoothstep` 기반 마스크 엣지 부드러움 0~1 |
| **Invert Mask** | 마스크 영역 반전 |
| **Show/Hide Mask Graphic** | 마스킹 유지하면서 마스크 이미지 숨김 |
| **중첩 마스크** | 최대 2단계 (`_SOFTMASK_NESTED` 키워드) |
| **회전/스케일** | Matrix4x4 기반 변환 |
| **Sprite Atlas** | 트리밍 보정 + UV 자동 매핑 |
| **Sliced 마스크** | 9-slice UV 리매핑 (코너 보존) |
| **Screen Space Overlay** | Canvas 로컬 좌표 기반 변환 |
| **UI Mask 호환** | Stencil Material 프로퍼티 전파 |
| **TMP 지원** | 전용 셰이더 자동 적용, Preset 보존 |
| **UIParticle 지원** | Optional Shader로 원본 블렌드 모드 보존 |
| **UIEffect 지원** | IMaterialModifier 프록시 + 키워드 활성화 |
| **ColorReplace 호환** | Hidden 변형 셰이더로 HSV + 마스킹 동시 적용 |
| **자식 이동 감지** | 마스크 밖으로 이동 시 원본 Material 자동 복원 |

## 빌드 안전성

### SoftMaskLightSettings (Resources 에셋)
- `Resources/SoftMaskLightSettings.asset`에 모든 Hidden 셰이더 참조가 자동 등록됨
- `Resources.Load`를 통해 빌드에 자동 포함 (씬에 SoftMaskLight가 없어도 보장)
- `[RuntimeInitializeOnLoadMethod]`로 빌드 시작 시 셰이더 로드
- `SoftMaskLightInstaller` (Editor)가 에디터 로드 시 자동으로 설정 에셋 생성/갱신
- `Tools > SoftMaskLight > Refresh Settings` 메뉴로 수동 갱신 가능

### 포터블 Include 경로
- `SoftMaskLightInstaller`가 `Assets/SoftMaskLight_Core.cginc` redirect 파일을 자동 생성
- 외부 셰이더는 `#include "SoftMaskLight_Core.cginc"` 만으로 사용 가능 (경로 불필요)
- SoftMaskLight 폴더 위치 변경 시 redirect 파일이 자동으로 갱신됨

### Hidden 셰이더 등록 체크리스트
새 Hidden 변형 셰이더 생성 시 반드시 아래를 확인:
1. `SoftMaskLightInstaller.cs`의 `HiddenShaderNames` 배열에 추가 (또는 이름에 `(SoftMaskLight)` 포함)
2. `Tools > SoftMaskLight > Refresh Settings` 실행
3. `SoftMaskLightSettings.asset` 인스펙터에서 셰이더 목록 확인
4. 빌드 후 `Shader.Find()` 실패 여부 확인

## 셰이더 이름 매핑

### SoftMaskLight 변형 (SoftMaskLight.cs가 관리)

| 원본 셰이더 | Hidden 변형 |
|-------------|------------|
| `UI/Default` | `Hidden/UI/Default (SoftMaskLight)` |
| `Hidden/UI/Default (UIEffect)` | `Hidden/UI/Default (UIEffect) (SoftMaskLight)` |
| `SoftMaskLight/Particles/UIAdditive` | `Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskLight)` |
| `SoftMaskLight/Particles/UIAlphaBlend` | `Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskLight)` |
| `CAT/Effects/ColorReplace` | `Hidden/CAT/Effects/ColorReplace (SoftMaskLight)` |
| `CAT/Particles/UIAdditiveCustom` | `Hidden/CAT/Particles/UIAdditiveCustom (SoftMaskLight)` |
| `CAT/Particles/UIAlphaBlendCustom` | `Hidden/CAT/Particles/UIAlphaBlendCustom (SoftMaskLight)` |
| `CAT/Particles/FlowUV` | `Hidden/CAT/Particles/FlowUV (SoftMaskLight)` |

### mob-sakai SoftMaskable 변형 (mob-sakai SoftMaskable.cs가 관리)

| 원본 셰이더 | mob-sakai Hidden 변형 |
|-------------|---------------------|
| `SoftMaskLight/Particles/UIAdditive` | `Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskable)` |
| `SoftMaskLight/Particles/UIAlphaBlend` | `Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskable)` |

### 키워드 방식 (셰이더 교체 없음)

| 셰이더 | 키워드 | 관리 주체 |
|--------|--------|----------|
| `Hidden/UI/Default (UIEffect)` | `_CAT_SOFTMASK` (multi_compile_local) | UIEffectSoftMaskLightProxy |
| `Hidden/UI/Default (UIEffect)` | `SOFTMASKABLE` (shader_feature) | mob-sakai SoftMaskable |

## 마이그레이션 (v1.x → v2.0)

- `[MovedFrom]` 속성으로 기존 씬의 `SoftMask` 컴포넌트가 자동으로 `SoftMaskLight`로 역직렬화됨
- 네임스페이스: `CAT.UI` → `SoftMaskLight`
- 클래스: `SoftMask` → `SoftMaskLight`
- AddComponentMenu: `SoftMaskLight/SoftMaskLight`
- 셰이더 상수: `SoftMaskLight/UI/Default`, `SoftMaskLight/UI/TMP_SoftMask`, `SoftMaskLight/Particles/*`
- 파티클 셰이더: SoftMask 코드 제거됨 (원본만 남음), Hidden 변형이 마스킹 담당
- UIEffect 셰이더: 키워드 방식 유지 (`_CAT_SOFTMASK` multi_compile_local), mob-sakai SOFTMASKABLE도 유지

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0.x) | O |
| URP 17.2.0 | O |
| Screen Space Camera / Overlay | O |
| Sprite Atlas | O |
| Image.Type.Sliced 마스크 | O |
| UI Mask / RectMask2D / ScrollView | O |
| TextMeshPro (Outline, Underlay) | O |
| UIParticle (com.coffee.ui-particle) | O |
| UIEffect (com.coffee.ui-effect) | O |
| ColorReplace (CAT/Effects/ColorReplace) | O |
| mob-sakai SoftMask (독립 공존) | O |
