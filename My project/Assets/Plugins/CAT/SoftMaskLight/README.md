# SoftMaskLight v2.4.0

알파 채널 기반 1-Pass 소프트 마스킹 컴포넌트 (모바일 최적화)

> 이전 이름: CAT SoftMask. v2.0.0에서 리네임 + Optional Shader 패턴 도입, v2.1.0에서 IMaterialModifier 프록시 패턴 적용.
> v2.2.0: asmdef 독립 패키지화(UIEffect 선택적 의존), 조건부 셰이더 등록(미사용 변형 빌드 제외),
> 런타임 자식 이동 감지, 인버트 마스크 시맨틱 수정, RectMask2D 대응.
> v2.3.0: mob-sakai SoftMask 대응 제거, 셰이더 변형 수 840 → 272로 감축
> (`_SOFTMASK_NESTED_SLICE` 폐지 + UIEffect 슬라이스 상시 컴파일),
> 지오메트리 캐시 도입으로 정지 상태 매 프레임 낭비 제거, 빌드 직전 셰이더 등록 자동 갱신.
> v2.4.0: **TMP 마스크 Material을 원본 폰트 Material 기준으로 공유** (드로우콜 N → 1),
> **UIEffect 프록시 Material을 baseMaterial 기준으로 공유** + 오버라이드 셰이더 이름 충돌 해소
> (`Hidden/UI/Default (UIEffect) (SoftMaskLight)`로 개명, Settings 직렬화 참조로 확정 바인딩),
> **슬라이스 리매핑·소프트니스의 픽셀당 나눗셈 제거** (C# 역수 사전 계산: `_SoftnessRcp`, `_MaskSliceSlopeX/Y`),
> 파티클 변형을 VFXMaker 패키지의 새 원본(`CAT/VFX/UIAdditive`·`UIAlphaBlend`)에 재정렬 + Additive 블렌드 드리프트 수정,
> `_unmaskableChildren` 정리 누락(누수) 수정,
> **Image.Type.Tiled / Filled 마스크** (`_SOFTMASK_SLICE` 재사용. Tiled는 중앙 `frac()` 반복, Filled는 C# 반평면 사전 계산으로 픽셀당 atan2 없음).
> `[MovedFrom]` 속성으로 기존 씬 직렬화 호환성 유지됨.

## 개요

Unity UI의 기본 `Mask` 컴포넌트는 Stencil 기반 바이너리 클리핑만 지원합니다.
SoftMaskLight는 **텍스처 알파 채널 기반 부드러운 마스킹**을 **RenderTexture 없이 단일 패스**로 처리하여 모바일 환경에 최적화되어 있습니다.

```
Assets/Plugins/CAT/SoftMaskLight/
├── Scripts/
│   ├── CAT.SoftMaskLight.asmdef          # 런타임 어셈블리 (UIEffect는 versionDefines로 선택적 의존)
│   ├── SoftMaskLight.cs                  # 메인 컴포넌트
│   ├── SoftMaskLightChildProxy.cs        # 일반 자식 프록시 (IMaterialModifier)
│   ├── SoftMaskLightSettings.cs          # 빌드 포함 설정 (ScriptableObject)
│   └── UIEffectSoftMaskLightProxy.cs     # UIEffect 프록시 (IMaterialModifier)
├── Shader/                               # Resources 밖 위치 — Settings 에셋 참조로만 빌드 포함 (선택적 포함)
│   ├── SoftMaskLight_Default.shader      # UI + Sprite 마스크 셰이더 (Core.cginc 공용 사용)
│   ├── SoftMaskLight_TMP.shader          # TextMeshPro 전용 마스크 셰이더
│   ├── SoftMaskLight_Core.cginc          # 공용 마스크 샘플링 (변형 셰이더 + Default 공용)
│   ├── UIEffect/
│   │   └── UIDefault_UIEffect.shader     # UIEffect 마스킹 오버라이드 (고유 이름 + _CAT_SOFTMASK 키워드)
│   │                                     # ※ UIEffect 패키지 부재 시 Installer가 UIEffect~ 폴더로 자동 격리
│   └── Hidden/                           # Optional Shader 변형들
│       ├── Hidden-SoftMaskLight-UI-Default.shader               # UI/Default (SoftMaskLight)
│       ├── Hidden-SoftMaskLight-Particles-UIAdditive.shader     # CAT/VFX/UIAdditive 변형
│       ├── Hidden-SoftMaskLight-Particles-UIAlphaBlend.shader   # CAT/VFX/UIAlphaBlend 변형
│       └── Hidden-CAT-*.shader                                  # ColorReplace/UIShining/Windable 변형
├── Resources/
│   └── SoftMaskLightSettings.asset       # Hidden 셰이더 빌드 포함 에셋 (자동 생성)
└── Editor/
    ├── CAT.SoftMaskLight.Editor.asmdef   # 에디터 어셈블리
    ├── SoftMaskLightEditor.cs            # 커스텀 인스펙터
    └── SoftMaskLightInstaller.cs         # 자동 설치/설정 관리자
```

### 빌드 포함 정책 (v2.2)

- 셰이더는 **Resources 밖**에 위치하며 `SoftMaskLightSettings.asset`(Resources)의 직렬화 참조로만 빌드에 포함됩니다.
- Installer가 **원본 셰이더가 실제 존재하는 변형만** 등록합니다.
  예: `CAT/Effects/UIShining`이 프로젝트에 없으면 그 변형 셰이더는 빌드에서 제외됩니다.
- **빌드 직전 `SoftMaskLightBuildProcessor`가 등록 목록을 강제 갱신**하고 콘솔에 출력합니다.
  수동 Refresh를 잊어도 누락/과다 포함이 발생하지 않습니다.
- UIEffect 마스킹은 오버라이드 셰이더(`Hidden/UI/Default (UIEffect) (SoftMaskLight)`) 하나로
  처리합니다. 이펙트 키워드는 shader_feature 그대로 유지하고 `_CAT_SOFTMASK`만
  multi_compile이므로 변형 폭발이 없습니다. (자세한 동작은 아래 "UIEffect: 오버라이드 셰이더
  교체 방식" 참고)

## 마스킹 전략 (컴포넌트 타입별)

SoftMaskLight는 자식 오브젝트의 타입에 따라 서로 다른 마스킹 전략을 사용합니다:

| 자식 타입 | 마스킹 방식 | Material 관리 |
|-----------|------------|---------------|
| **일반 UI (Image, RawImage)** | IMaterialModifier 프록시 + Optional Shader | 공유 프록시 Material (배칭 유지) |
| **커스텀 셰이더 UI (ColorReplace 등)** | IMaterialModifier 프록시 + Optional Shader | 공유 프록시 Material (프로퍼티 복사) |
| **TextMeshPro** | 전용 셰이더 (`SoftMaskLight/UI/TMP_SoftMask`) | 원본 폰트 Material 기준 공유 (같은 프리셋 자식끼리 드로우콜 1) |
| **UIParticle** | IMaterialModifier 프록시 + Optional Shader | 공유 프록시 Material (블렌드 모드 보존) |
| **UIEffect** | 프록시가 오버라이드 셰이더로 교체 + `_CAT_SOFTMASK` 키워드 | baseMaterial 기준 공유 프록시 Material (배칭 유지) |

> ⚠️ 마스크로 쓰는 스프라이트/텍스처는 **알파 채널이 살아 있어야** 합니다.
> 알파 없는 압축 포맷(예: ETC2 RGB, RGB565)을 마스크 스프라이트에 쓰면 마스킹이 동작하지 않습니다.

### IMaterialModifier 프록시 패턴 (v2.1)

SoftMaskLight v2.1부터 일반 자식 Graphic에 `SoftMaskLightChildProxy` 컴포넌트를 자동 추가하여
`IMaterialModifier` 체인을 통해 마스킹을 적용합니다. **`graphic.m_Material`을 직접 교체하지 않습니다.**

```
동작 순서:
1. SoftMaskLight.ApplyMaskToChildren() → 자식에 SoftMaskLightChildProxy 추가 + Initialize()
2. Canvas 리빌드 → GetModifiedMaterial(baseMaterial) 호출
3. baseMaterial의 셰이더에 대응하는 Hidden 변형 셰이더를 FindOptionalShader()로 탐색
4. SoftMaskLight의 공유 캐시에서 프록시 Material 조회/생성 (GetOrCreateProxyMaterial)
5. 마스크 프로퍼티 적용 후 프록시 Material 반환
6. materialForRendering = 프록시 Material (baseMaterial 원본 유지)
```

**장점:**
- `graphic.m_Material` 미수정 → 씬 저장 시 원본 Material 참조 보존
- 에디터에서 자식 선택/수정 시 마스크 해제 문제 없음
- 플레이모드 전환 시 Material 분기 없음
- 동일 baseMaterial을 가진 자식끼리 프록시 Material 공유 (배칭 유지)

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

### UIEffect 예외: 오버라이드 셰이더 교체 + 키워드 (v2.4)
UIEffect는 `shader_feature_local_fragment` 키워드로 효과를 조합하며, 패키지 셰이더에는
마스크 코드가 없습니다. 그래서 UIEffect 자식만 다음 방식으로 처리합니다:
- `UIDefault_UIEffect.shader` = 패키지 셰이더 + 마스크 코드를 담은 **고유 이름 오버라이드**
  (`Hidden/UI/Default (UIEffect) (SoftMaskLight)`, `#pragma multi_compile_local _ _CAT_SOFTMASK`)
- `UIEffectSoftMaskLightProxy`가 공유 프록시 Material의 셰이더를 오버라이드로 교체하고
  `_CAT_SOFTMASK` 키워드 활성화 — 이펙트 키워드는 Material에 보존되어 효과 조합 유지
- 프록시 Material은 baseMaterial(UIEffect가 설정별 공유) 기준으로 공유 → 배칭 유지

### FindOptionalShader() 탐색 로직
```
1. FindOptionalShader(originalShader)
2. "(SoftMaskable)" 셰이더면 마스킹 스킵 (패스스루 + 경고) — mob-sakai와 동일 Graphic 중첩 불가
3. "Hidden/{originalShader.name} (SoftMaskLight)" 검색
4. 이미 "Hidden/" 접두사가 있으면 "{name} (SoftMaskLight)" (중복 접두사 방지)
5. 없으면 "Hidden/UI/Default (SoftMaskLight)" 폴백 + 경고 로그
   (원본의 블렌드 모드/이펙트가 손실될 수 있음 — Additive 파티클, Spine 등은 반드시 전용 변형 필요)
6. InstanceID 기반 캐싱 → 같은 원본 셰이더에 대해 1회만 Shader.Find()
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
        // 주의: _MaskWorldToUV / _MaskWorldToUV2는 float4x4 유니폼이므로
        // Properties 블록에 선언하지 않는다 (Core.cginc가 유니폼으로 선언, C#이 SetMatrix로 설정)
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _SoftnessRcp ("Softness Rcp", Float) = 10
        _InvertMask ("Invert Mask", Float) = 0
        _MaskUVRect ("Mask UV Rect", Vector) = (0,0,1,1)
        _MaskSliceBorder ("Slice Border", Vector) = (0,0,1,1)
        _MaskSliceInnerUV ("Slice Inner UV", Vector) = (0,0,1,1)
        _MaskSliceSlopeX ("Mask Slice Slope X", Vector) = (0,1,0,0.9999)
        _MaskSliceSlopeY ("Mask Slice Slope Y", Vector) = (0,1,0,0.9999)
        _MaskFillLineA ("Mask Fill Line A", Vector) = (0,0,1,0)
        _MaskFillLineB ("Mask Fill Line B", Vector) = (0,0,1,0)
        // 중첩 마스크용 (동일 프로퍼티에 2 접미사)
        _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        _SoftnessRcp2 ("Softness Rcp 2", Float) = 10
        _InvertMask2 ("Invert Mask 2", Float) = 0
        _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0,0,1,1)
        _MaskSliceBorder2 ("Slice Border 2", Vector) = (0,0,1,1)
        _MaskSliceInnerUV2 ("Slice Inner UV 2", Vector) = (0,0,1,1)
        _MaskSliceSlopeX2 ("Mask Slice Slope X 2", Vector) = (0,1,0,0.9999)
        _MaskSliceSlopeY2 ("Mask Slice Slope Y 2", Vector) = (0,1,0,0.9999)
        _MaskFillLineA2 ("Mask Fill Line A 2", Vector) = (0,0,1,0)
        _MaskFillLineB2 ("Mask Fill Line B 2", Vector) = (0,0,1,0)
    }
    SubShader
    {
        // 원본 셰이더의 Tags, Blend, Cull, ZWrite 등 그대로 복사

        Pass
        {
            CGPROGRAM
            // 원본 셰이더의 #pragma 그대로 복사

            // SoftMaskLight 키워드 (이 2개가 전부 — NESTED_SLICE는 존재하지 않음)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE

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

### 🔴 RectMask2D 대응 (변형 셰이더 필수)

RectMask2D는 `canvasRenderer.EnableRectClipping()`으로 동작하며, `_ClipRect`와
`UNITY_UI_CLIP_RECT` 키워드를 **CanvasRenderer가 드로우마다 네이티브로 주입**합니다
(UGUI C#에는 이를 설정하는 코드가 없음). 따라서 UI용 변형 셰이더는 반드시 아래를 갖춰야 합니다.
빠뜨리면 RectMask2D 하위에서 **알파가 0이 되어 전혀 렌더링되지 않습니다.**

```hlsl
Properties {
    // 미주입 상황에서 전체가 사라지지 않도록 기본값을 넓게 둔다 (0,0,0,0 금지)
    [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
}
...
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#include "SoftMaskLight_UIClip.cginc"   // UnityCG 이후에 include

struct v2f { ... CAT_UI_CLIP_COORDS(N) };            // 빈 TEXCOORD 슬롯 1개
// vert: 클립 위치 계산 이후
OUT.uiClipMask = CAT_UI_ComputeClipMask(OUT.vertex, v.vertex.xy);
// frag:
CAT_UI_ApplyClipRect(color, IN.uiClipMask);          // straight alpha (알파에만 적용)
color *= CAT_UI_ClipFactor(IN.uiClipMask);           // premultiplied/Additive (RGB에도 적용)
```

**주의 사항**
- `_ClipRect` / `_UIMaskSoftnessX` / `_UIMaskSoftnessY`는 `SoftMaskLight_UIClip.cginc`가 선언하므로
  셰이더 본문에서 **중복 선언하지 말 것** (중복 시 컴파일 에러).
- 프록시 머티리얼에 `UNITY_UI_CLIP_RECT` 키워드를 구워 넣으면 안 됩니다. 공유 프록시에 남으면
  `_ClipRect=(0,0,0,0)` 상태로 자식이 전부 사라지므로, `SoftMaskLight.ResetRectMask2DMaterialState()`가
  프록시 생성 시 키워드를 끄고 `_ClipRect`를 넓은 기본값으로 되돌립니다.
- TMP는 `SoftMaskLight_TMP.shader`가 TMP 원본 방식(`_ClipRect` + `input.mask`)을 그대로 유지합니다.

## mob-sakai SoftMask (com.coffee.softmask-for-ugui)

v2.3.0에서 **대응을 완전히 제거**했습니다. `(SoftMaskable)` 변형 셰이더 5종과
UIEffect 셰이더의 `SOFTMASKABLE` 블록이 모두 삭제되어, 해당 패키지에 대한 의존이 없습니다.
(패키지를 제거해도 셰이더 컴파일 에러가 나지 않습니다.)

> ⚠️ 하나의 Graphic을 SoftMaskLight와 mob-sakai SoftMask 아래에 **동시에** 두지 마세요.
> 결합 변형 셰이더가 없어 한쪽 마스킹이 반드시 깨집니다.
> `FindOptionalShader()`가 `(SoftMaskable)` 셰이더를 만나면 자기 마스킹을 건너뛰고 경고를
> 출력하는 방어 코드만 남겨 두었습니다(무음 파손 방지용, 비용 없음).

## 셰이더 변형 수 정책

변형 수는 빌드 시간·용량·런타임 셰이더 메모리에 직결되므로 키워드를 최소로 유지합니다.

| 셰이더 | 키워드 구성 | 변형 수 |
|--------|------------|--------|
| `Hidden/UI/Default (SoftMaskLight)` | CLIP × ALPHACLIP × NESTED × SLICE | 16 |
| `SoftMaskLight/UI/Default` | UI 패스 16 + Sprite 패스 16 | 32 |
| `SoftMaskLight/UI/TMP_SoftMask` | OUTLINE × UNDERLAY(3) × CLIP × ALPHACLIP × NESTED × SLICE | 96 |
| `Hidden/UI/Default (UIEffect) (SoftMaskLight)` | 이펙트 조합 × CLIP × ALPHACLIP × _CAT_SOFTMASK × NESTED | 128 |
| 파티클 변형 2종 | CLIP × NESTED × SLICE | 8씩 |

핵심 규칙 두 가지입니다.

1. **`_SOFTMASK_NESTED_SLICE`는 존재하지 않습니다.** `_SOFTMASK_SLICE` 하나가 마스크 1·2의
   형태 대응(Sliced 9-slice / Tiled `frac()` 반복 / Filled 반평면)을 모두 담당하고,
   해당 없는 쪽은 C#이 항등 파라미터를 넣어 각 단계를 no-op으로 만듭니다.
   (구버전은 NESTED 없이 NESTED_SLICE만 켜진 죽은 조합을 25% 컴파일하고 있었습니다.)
2. **UIEffect 오버라이드는 슬라이스를 키워드 없이 상시 컴파일**합니다
   (`#define _CAT_SOFTMASK_FORCE_SLICE 1`). 이 셰이더의 변형 수는 UIEffect의 이펙트
   조합과 곱해지므로, 키워드 1개가 곧 2배입니다. 항등 파라미터 덕분에 정확도 손실은 없고
   `_CAT_SOFTMASK`가 켜진 픽셀에서만 약간의 ALU가 추가됩니다.

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
| **Tiled 마스크** | 중앙 구간 `frac()` 반복. 기울기 `w`에 타일 수 기록 |
| **Filled 마스크** | H/V/Radial90·180·360. C#이 반평면 2개를 사전 계산 (픽셀당 atan2 없음) |
| **Screen Space Overlay** | Canvas 로컬 좌표 기반 변환 |
| **UI Mask 호환** | Stencil Material 프로퍼티 전파 |
| **TMP 지원** | 전용 셰이더 자동 적용, Preset 보존 |
| **UIParticle 지원** | Optional Shader로 원본 블렌드 모드 보존 |
| **UIEffect 지원** | IMaterialModifier 프록시 + 키워드 활성화 |
| **ColorReplace 호환** | Hidden 변형 셰이더로 HSV + 마스킹 동시 적용 |
| **자식 이동 감지** | 마스크 밖 이동 시 원본 Material 자동 복원 (프록시 즉시 통보 + 플레이모드 8프레임 주기 스캔) |
| **동적 자식 추가 감지** | 직계 자식은 즉시, 깊은 계층은 8프레임 주기 스캔으로 감지. 즉시 반영이 필요하면 `RefreshMasks()` 호출 |
| **Invert Mask 시맨틱** | 마스크 rect 밖 = "알파 0" 취급 → 인버트 시 rect 밖 영역은 표시됨 (구멍 뚫기 가능) |

### 형태 대응 제약 (Tiled / Filled)

- **Tiled 마스크 스프라이트는 Generate Mip Maps 해제 권장** — `frac()` UV 불연속 지점에서
  하드웨어 파생값이 최저 밉으로 튀어 타일 경계에 1px 흐린 seam이 생길 수 있음 (UI 기본 임포트는 밉맵 off).
- **`fillCenter=false`(Sliced/Tiled 중앙 비우기) 미대응** — 마스크는 중앙을 채운 것으로 취급.
- **`preserveAspect` 미대응** — Filled 반평면 원점이 rect 기준이므로 aspect 보정 시 어긋남.
- Fill 경계는 ~1px 소프트 AA 적용 (반평면 부호 거리 기반, `lineB.w` = AA 스케일).
  Softness 값은 마스크 텍스처 알파 엣지에만 적용되고 fill 컷 라인에는 적용되지 않음 (Unity 지오메트리 컷과 동일 시맨틱).
- **UIEffect 오버라이드는 형태 코드를 상시 컴파일** (`_CAT_SOFTMASK_FORCE_SLICE`) —
  마스킹된 UIEffect 픽셀은 Simple 마스크여도 항등 형태 연산(ALU 소량)을 지불함 (변형 수 폭발 방지 트레이드오프).
- 런타임에 `pixelsPerUnitMultiplier`/`referencePixelsPerUnit`을 변경하면 타일 수가 갱신되지 않음 — `RefreshMasks()` 호출 필요.

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
| `CAT/VFX/UIAdditive` (VFXMaker 패키지) | `Hidden/CAT/VFX/UIAdditive (SoftMaskLight)` |
| `CAT/VFX/UIAlphaBlend` (VFXMaker 패키지) | `Hidden/CAT/VFX/UIAlphaBlend (SoftMaskLight)` |
| `CAT/Effects/ColorReplace` | `Hidden/CAT/Effects/ColorReplace (SoftMaskLight)` |
| `CAT/Effects/UIShining` | `Hidden/CAT/Effects/UIShining (SoftMaskLight)` |
| `CAT/Effects/Windable` | `Hidden/CAT/Effects/Windable (SoftMaskLight)` |

> 원본 셰이더가 프로젝트에 없는 변형은 Installer가 등록하지 않고 **경고를 출력**합니다.
> (폴백 시 블렌드 모드가 달라지는 무음 파손의 조기 발견용)

### UIEffect: 오버라이드 셰이더 교체 방식 (v2.4)

| 오버라이드 셰이더 | 키워드 | 관리 주체 |
|--------|--------|----------|
| `Hidden/UI/Default (UIEffect) (SoftMaskLight)` | `_CAT_SOFTMASK` (multi_compile_local) | UIEffectSoftMaskLightProxy + SoftMaskLight |

- 오버라이드는 패키지 셰이더와 **다른 고유 이름**을 사용합니다. (v2.3 이전에는 동일 이름 섀도잉
  방식이었는데, `Shader.Find`가 임포트 순서에 따라 패키지 쪽을 선택하면 마스킹이 조용히 꺼지는
  실장애가 있었습니다.)
- `SoftMaskLightSettings.asset`의 `_uiEffectOverrideShader` 직렬화 참조로 확정 바인딩되며,
  프록시가 공유 프록시 Material의 셰이더를 이 오버라이드로 교체합니다.
- 이펙트 키워드(shader_feature)는 Material에 보존되므로 셰이더 교체 후에도 UIEffect의
  효과 조합이 유지됩니다. 단, **빌드에서 shader_feature 변형이 스트립되지 않도록**
  `UIEffectProjectSettings`의 ShaderVariantCollection에 사용 조합을 등록해야 합니다
  (에디터 플레이 시 mob-sakai 레지스트리가 미등록 변형을 기록해 줍니다).

## 마이그레이션 (v1.x → v2.0)

- `[MovedFrom]` 속성으로 기존 씬의 `SoftMask` 컴포넌트가 자동으로 `SoftMaskLight`로 역직렬화됨
- 네임스페이스: `CAT.UI` → `SoftMaskLight`
- 클래스: `SoftMask` → `SoftMaskLight`
- AddComponentMenu: `SoftMaskLight/SoftMaskLight`
- 셰이더 상수: `SoftMaskLight/UI/Default`, `SoftMaskLight/UI/TMP_SoftMask`
- 파티클 셰이더: 원본은 VFXMaker 패키지(`CAT/VFX/UIAdditive`·`UIAlphaBlend`)로 이동, Hidden 변형이 마스킹 담당
- UIEffect 셰이더: v2.4부터 고유 이름 오버라이드 교체 방식 (`_CAT_SOFTMASK` multi_compile_local)

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0.x) | O |
| URP 17.2.0 | O |
| Screen Space Camera / Overlay | O |
| Sprite Atlas | O |
| Image.Type.Sliced 마스크 | O |
| Image.Type.Tiled 마스크 | O |
| Image.Type.Filled 마스크 (H/V/Radial90·180·360) | O |
| UI Mask / RectMask2D / ScrollView | O |
| TextMeshPro (Outline, Underlay) | O |
| UIParticle (com.coffee.ui-particle) | O |
| UIEffect (com.coffee.ui-effect) | O |
| ColorReplace (CAT/Effects/ColorReplace) | O |
| mob-sakai SoftMask (독립 공존) | O |
