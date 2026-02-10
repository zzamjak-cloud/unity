# CAT SoftMask v1.2.0

알파 채널 기반 1-Pass 소프트 마스킹 컴포넌트 (모바일 최적화)

## 개요

Unity UI의 기본 `Mask` 컴포넌트는 Stencil 버퍼 기반으로 바이너리(0/1) 클리핑만 지원하여 마스킹 엣지에 계단 현상이 발생합니다. CAT SoftMask는 **텍스처 알파 채널을 기반으로 부드러운 마스킹**을 제공하며, **RenderTexture 없이 단일 패스**로 처리하여 모바일 환경에 최적화되어 있습니다.

```
Assets/Scripts/SoftMask/
├── SoftMask.cs                       # 메인 컴포넌트 (1430줄)
├── Shader/
│   ├── CAT_SoftMask.shader           # UI + Sprite 통합 셰이더 (283줄)
│   ├── CAT_TMP_SoftMask.shader       # TextMeshPro 전용 셰이더 (337줄)
│   └── CAT_SoftMask_Core.cginc       # 파티클 셰이더용 공용 마스크 샘플링 (96줄)
└── Editor/
    └── SoftMaskEditor.cs             # 커스텀 인스펙터 (165줄)
```

## 주요 기능

| 기능 | 설명 |
|------|------|
| **알파 마스킹** | 자신의 UI Graphic 알파 채널을 마스크로 사용 |
| **Softness 조절** | `smoothstep` 기반으로 마스크 엣지 부드러움 0~1 조절 |
| **Invert Mask** | 마스크 영역 반전 (밝은 영역 <-> 어두운 영역 교환) |
| **Show/Hide Mask Graphic** | 마스킹은 유지하면서 마스크 이미지 표시/숨김 토글 |
| **중첩 마스크** | 최대 2단계 중첩 SoftMask 지원 (`_SOFTMASK_NESTED` 키워드) |
| **회전/스케일 대응** | `Matrix4x4` 기반 월드->UV 변환으로 회전, 스케일 완전 지원 |
| **Sprite Atlas 호환** | `DataUtility.GetOuterUV()` + 트리밍 보정으로 Atlas 스프라이트 정확한 UV 매핑 |
| **ScrollView 호환** | `UNITY_UI_CLIP_RECT` 지원으로 ScrollView 내 정상 동작 |
| **UI Mask 호환** | Stencil 래핑 Material에 프로퍼티 전파로 UI Mask 내 정상 동작 |
| **UI Mask 동적 배치** | UI Mask 하위에 동적 로드/이동 시 자동 갱신 (Canvas 레이아웃 완료 대기) |
| **자식 자동 마스킹** | 하위 Graphic 컴포넌트에 자동으로 마스크 Material 적용 |
| **TextMeshPro 지원** | TMP 전용 셰이더로 SDF 텍스트 마스킹 (Outline, Underlay 호환) |
| **TMP Material Preset 자동 감지** | 외부 Material 변경 시 마스크 자동 재적용 |
| **TMP Material Preset 플레이모드 보존** | 직렬화 백업으로 플레이모드 전환 시 프리셋 유실 방지 |
| **UIParticle 지원** | `com.coffee.ui-particle` 파티클의 원본 셰이더/블렌드 모드 유지하며 마스킹 |
| **에디터 실시간 프리뷰** | `[ExecuteAlways]`로 에디터에서 즉시 결과 확인 |

## 아키텍처

### 렌더링 파이프라인

```
┌──────────────────────────────────────────────────────────────┐
│ SoftMask (부모)                                               │
│  ├─ 자신의 UI.Graphic 알파 -> 마스크 텍스처로 사용             │
│  ├─ ComputeWorldToMaskUV() -> Matrix4x4 (worldToLocal x UV)  │
│  └─ 1개 공유 Material 생성 -> 모든 자식에 적용                 │
│                                                               │
│  자식 Graphic들 (일반)                                        │
│  ├─ Vertex Shader: 월드좌표 -> 마스크 UV 계산                  │
│  └─ Fragment Shader: tex2D(마스크) -> smoothstep -> alpha 곱   │
│                                                               │
│  자식 TMP_Text (TextMeshPro)                                  │
│  ├─ 폰트별 개별 Material 생성 (CopyShaderProperties)           │
│  ├─ TMP SDF 렌더링 + SoftMask 샘플링 (premultiplied alpha)    │
│  ├─ Material Preset 변경 자동 감지 및 재적용                   │
│  └─ 원본 Preset Material 직렬화 백업 (플레이모드 보존)         │
│                                                               │
│  자식 UIParticle (com.coffee.ui-particle)                     │
│  ├─ IsParticleMaterial() → 원본 셰이더 감지 (CAT/Particles/*) │
│  ├─ CreateParticleMaskMaterial() → 원본 복제 + _CAT_SOFTMASK  │
│  ├─ 원본 블렌드 모드 보존 (Additive/AlphaBlend)               │
│  └─ DetectParticleMaterialChanges() → 비활성/활성 자동 재적용 │
└──────────────────────────────────────────────────────────────┘
```

### 핵심 설계 원칙

1. **1-Pass 렌더링**: RenderTexture 없이 기존 렌더링 패스에서 마스크 샘플링 수행
2. **SoftMask당 1개 공유 Material**: 모든 일반 자식이 동일 Material 공유 (N개 Material -> 1개)
3. **TMP 개별 Material**: TMP는 폰트 아틀라스별 전용 Material 생성 (SDF 파라미터 보존)
4. **더티 체크**: `Matrix4x4` 비교, 텍스처 ID 비교, 프로퍼티 값 비교로 불필요한 업데이트 스킵
5. **버텍스 셰이더 UV 계산**: 마스크 UV를 버텍스에서 계산하여 프래그먼트 비용 절감
6. **분기 없는 셰이더**: `step()`, `smoothstep()`, `lerp()`로 GPU 분기 완전 회피

### Material 업데이트 흐름

```
LateUpdate()
  ├── DetectTMPMaterialChanges()       ← TMP Material Preset 외부 변경 감지
  │    └── 변경됨? → 기존 Material 파괴 → CreateTMPMaskMaterial(새 Material)
  │                 → SaveTMPOriginalBackup(새 Material) ← 직렬화 백업 갱신
  ├── DetectParticleMaterialChanges()  ← UIParticle Material 재할당 감지
  │    └── 변경됨? → 기존 Material 파괴 → CreateParticleMaskMaterial(새 Material)
  ├── 자식 수 변경 감지                ← UIParticle 활성/비활성 시 새 자식 추가
  │    └── 변경됨? → ApplyMaskToChildren()
  └── UpdateSharedMaterial()
       ├── ComputeWorldToMaskUV() -> Matrix4x4 비교
       │   └── 변경됨? -> _sharedMaskMaterial.SetMatrix()
       ├── GetMaskTexture() -> InstanceID 비교
       │   └── 변경됨? -> _sharedMaskMaterial.SetTexture()
       ├── Softness / InvertMask -> float/bool 비교
       │   └── 변경됨? -> _sharedMaskMaterial.SetFloat()
       ├── 부모 마스크 프로퍼티 (중첩 시)
       │   └── 변경됨? -> _sharedMaskMaterial.Set*2()
       └── anyChange?
            ├── UpdateTMPMaterials()         ← TMP Material에 마스크 프로퍼티 전파
            ├── UpdateParticleMaterials()    ← Particle Material에 마스크 프로퍼티 전파
            └── PropagateToStencilMaterials()
                 └── 각 자식의 materialForRendering에도 프로퍼티 복사
                     (UI Mask의 StencilMaterial 복사본 대응)
```

## 성능 특성

### 메모리

| 항목 | 비용 |
|------|------|
| Material 인스턴스 (일반) | SoftMask당 1개 (일반 자식 수 무관) |
| Material 인스턴스 (TMP) | TMP 자식 수만큼 추가 (폰트별 개별 Material) |
| Material 인스턴스 (Particle) | 파티클 자식 수만큼 추가 (원본 셰이더 보존) |
| RenderTexture | **없음** (1-Pass 방식) |
| Dictionary 오버헤드 | 자식 수 x (Graphic ref + Material ref) |
| 셰이더 Variant | 일반: 2개 x 2 SubShader / TMP: 48개 (Outline x Underlay x ClipRect x AlphaClip x Nested) |

### CPU (프레임당)

| 상황 | 비용 |
|------|------|
| **정적 UI (변화 없음)** | Matrix4x4 비교 1회 + 텍스처 ID 비교 1회 + float 비교 2회 + TMP 변경 감지 -> **거의 무비용** |
| **Transform 변경** | Matrix4x4 계산 + `Material.SetMatrix()` 1회 + TMP/Stencil 전파 |
| **프로퍼티 변경** | `Material.SetFloat()` 2회 + TMP/Stencil 전파 |
| **TMP Material 전파** | TMP Material 수 x `Material.Set*()` 호출 (Matrix + Float + Texture) |
| **Stencil 전파** | 자식 수 x `materialForRendering` 접근 (UI Mask 내에서만) |

### GPU (프래그먼트당)

| 연산 | 비용 |
|------|------|
| `tex2D(_MaskTex)` | 텍스처 샘플링 1회 |
| `step()` x 4 | 경계 검사 (분기 없음) |
| `smoothstep()` | 소프트 엣지 |
| `lerp()` | 반전 처리 |
| **TMP 추가 비용** | SDF 렌더링(기존) + 위 마스크 샘플링 (추가 tex2D 1회) |
| **중첩 마스크 추가** | 위 연산 x 2 (키워드 비활성 시 제거됨) |

### 주의 사항

1. **자식이 매우 많은 경우 (50+)**: `PropagateToStencilMaterials()`에서 각 자식의 `materialForRendering` 접근이 `GetComponents()` + `StencilMaterial.Add/Remove`를 트리거합니다. 정적 UI에서는 발생하지 않으며, 프로퍼티 변경 시에만 실행됩니다.

2. **스크롤 중인 SoftMask**: 마스크 또는 자식의 Transform이 매 프레임 변경되므로 Matrix4x4 업데이트가 매 프레임 발생합니다. 이는 설계상 의도된 동작이며, `Material.SetMatrix()` 1회의 비용은 미미합니다.

3. **중첩 마스크**: `_SOFTMASK_NESTED` 키워드 활성화 시 프래그먼트 셰이더에서 텍스처 샘플링이 2회로 증가합니다. 비중첩 마스크는 `multi_compile_local`로 추가 비용이 **완전히 제거**됩니다.

4. **Atlas 스프라이트 트리밍**: `GetContentLocalRect()`에서 `sprite.textureRectOffset`, `sprite.textureRect` 접근이 매 프레임 발생하지만, 이는 Unity 내부 캐싱된 프로퍼티로 오버헤드가 거의 없습니다.

5. **TMP Material 개수**: TMP 자식은 일반 Graphic과 달리 공유 Material을 사용할 수 없습니다 (폰트 아틀라스, SDF 파라미터가 다름). TMP 자식이 많은 경우 Material 인스턴스가 비례하여 증가합니다.

## TextMeshPro 지원

### 개요

TMP(TextMeshProUGUI)는 SDF(Signed Distance Field) 기반 텍스트 렌더링을 사용하며, 일반 UI Graphic과 다른 Material 시스템을 가집니다. CAT SoftMask는 TMP 전용 셰이더(`CAT/UI/TMP_SoftMask`)를 통해 TMP 텍스트에도 소프트 마스킹을 지원합니다.

### 동작 방식

1. `ApplyMaskToChildren()` 시 자식이 `TMP_Text`인 경우 자동 감지
2. 원본 TMP Material의 프로퍼티를 `CopyShaderProperties()`로 개별 복사
3. SoftMask 전용 셰이더(`CAT/UI/TMP_SoftMask`)로 교체된 새 Material 생성
4. `fontSharedMaterial`을 통해 TMP에 적용
5. TMP_SubMeshUI (멀티 아틀라스) 자동 대응

### TMP 셰이더 구조

```hlsl
// CAT_TMP_SoftMask.shader 구조
Shader "CAT/UI/TMP_SoftMask"
{
    Properties
    {
        // TMP 프로퍼티 (TMP_SDF-Mobile.shader 동일 - 36개)
        _FaceColor, _OutlineColor, _UnderlayColor, _MainTex(Font Atlas), ...

        // SoftMask 프로퍼티 (_SoftMask 접두사 - TMP의 _MaskTex 충돌 방지)
        _SoftMaskTex, _SoftMaskSoftness, _SoftMaskInvert, _SoftMaskUVRect
        _SoftMaskTex2, _SoftMaskSoftness2, _SoftMaskInvert2, _SoftMaskUVRect2
    }

    // #include "TMPro_Properties.cginc"  (TMP 유니폼 직접 포함)
    // SampleSoftMask1/2() -> TMP SDF 렌더링 후 premultiplied alpha 적용
    // Blend One OneMinusSrcAlpha (TMP premultiplied alpha 블렌딩)
}
```

### TMP 기술적 주의사항

| 항목 | 설명 |
|------|------|
| **프로퍼티 접두사** | `_SoftMask*` 사용 (TMPro_Properties.cginc의 `_MaskTex` 충돌 방지) |
| **Material 복사** | `CopyShaderProperties()` 사용 (`CopyPropertiesFromMaterial()`은 프로퍼티 시트를 통째로 교체하여 SoftMask 프로퍼티 제거) |
| **Material 접근** | `fontSharedMaterial` 사용 (`Graphic.material`은 TMP가 무시) |
| **Premultiplied Alpha** | `c *= softMask` (RGB+A 모두 적용, `Blend One OneMinusSrcAlpha`) |
| **Preset 변경 감지** | `DetectTMPMaterialChanges()`에서 매 프레임 현재 Material과 적용 Material 비교 |
| **Preset 플레이모드 보존** | `_tmpOriginalBackup` 직렬화 리스트에 원본 프리셋 백업 → 플레이모드에서 `FindTMPOriginalBackup()`으로 복원 |
| **Metal 호환** | sampler2D를 함수 매개변수로 전달하지 않고 전역 유니폼 직접 접근 |

### TMP 지원 기능

- Outline (OUTLINE_ON)
- Underlay / Inner Underlay (UNDERLAY_ON, UNDERLAY_INNER)
- RectMask2D 클리핑 (UNITY_UI_CLIP_RECT)
- UI Mask (Stencil) 호환
- Material Preset 동적 변경
- Material Preset 플레이모드 보존
- 중첩 SoftMask

### TMP Material Preset 플레이모드 보존

#### 문제

`[ExecuteAlways]`로 에디터 모드에서 SoftMask가 활성 상태일 때, TMP의 `fontSharedMaterial`을 `HideFlags.DontSave` 마스크 Material로 교체합니다. 플레이모드 전환 시 Unity가 씬을 직렬화하는데, `DontSave` Material은 직렬화에서 **null로 저장**됩니다. 역직렬화 후 TMP가 `fontSharedMaterial = null`을 감지하면 폰트 기본 Material로 폴백하여 **프리셋(Outline, Underlay 등)이 유실**됩니다.

```
에디터 모드:
  TMP fontSharedMaterial = [Outline Preset] → SoftMask → [Mask Material (DontSave)]

플레이모드 전환 (문제):
  씬 직렬화: [Mask Material (DontSave)] → null
  역직렬화: fontSharedMaterial = null → 폰트 기본 Material로 폴백
  결과: Outline 프리셋 유실 ✗
```

#### 해결: 직렬화 백업 (`_tmpOriginalBackup`)

원본 프리셋 Material 참조를 `[SerializeField]` 리스트에 백업합니다. 프리셋 Material은 에셋이므로 직렬화에서 유실되지 않습니다.

```csharp
// 직렬화 필드: TMP 원본 Material 백업
[System.Serializable]
private struct TMPOriginalEntry
{
    public UnityEngine.UI.Graphic graphic;
    public Material material;
}

[SerializeField, HideInInspector]
private List<TMPOriginalEntry> _tmpOriginalBackup;
```

#### 동작 흐름

```
에디터 모드:
  1. ApplyMaskToChildren()
     └── originalFontMat = fontSharedMaterial  → [Outline Preset]
     └── SaveTMPOriginalBackup(child, mat)     → 백업 저장 ✓
     └── fontSharedMaterial = [Mask Material]   → 마스크 적용

  2. DetectTMPMaterialChanges() (프리셋 변경 시)
     └── SaveTMPOriginalBackup(child, newMat)   → 백업 갱신 ✓

플레이모드 전환:
  씬 직렬화:
     [Mask Material (DontSave)] → null          (마스크 Material 유실)
     _tmpOriginalBackup → [Outline Preset]      (백업은 정상 직렬화 ✓)

  역직렬화 + OnEnable → ApplyMaskToChildren():
     fontSharedMaterial = [폰트 기본 Material]  (TMP 자동 폴백)
     backup = FindTMPOriginalBackup(child)       → [Outline Preset]
     기본 Material ≠ backup → fontSharedMaterial = backup  (프리셋 복원 ✓)
     CreateTMPMaskMaterial(backup)               → 프리셋 기반 마스크 적용 ✓
```

#### 백업 갱신 시점

| 시점 | 메서드 | 설명 |
|------|--------|------|
| 마스크 최초 적용 | `ApplyMaskToChildren()` | 원본 프리셋 Material 백업 |
| 프리셋 변경 | `DetectTMPMaterialChanges()` | 변경된 프리셋으로 백업 갱신 |

#### 주의사항

- 에디터 모드에서 프리셋 변경 후 **씬 저장(Ctrl+S)** 필요 (백업이 직렬화에 포함되려면)
- TMP_SubMeshUI(멀티 아틀라스)도 동일한 백업 메커니즘 적용
- 플레이모드 중 프리셋 변경은 런타임 전용 (플레이모드 종료 시 자동 폐기)
- 백업 데이터는 `[HideInInspector]`로 인스펙터에 노출되지 않음

## mob-sakai SoftMaskForUGUI와 비교

### 아키텍처 차이

| 항목 | CAT SoftMask | mob-sakai SoftMaskForUGUI v3 |
|------|-------------|----------------------------|
| **렌더링 방식** | 1-Pass (기존 패스에서 마스크 샘플링) | RenderTexture에 마스크 렌더링 후 자식이 샘플링 |
| **추가 GPU 패스** | 없음 | CommandBuffer로 마스크 버퍼 렌더링 (별도 패스) |
| **RenderTexture** | 불필요 | ARGB32 버퍼 필수 (1024x576 @ 1080p ~ 2.25MB) |
| **Material 관리** | SoftMask당 1개 공유 (+ TMP별 개별) | `MaterialRepository` Hash128 기반 캐싱 (자식별 variant) |
| **마스크 UV 계산** | 버텍스 셰이더 (Matrix4x4 변환) | 프래그먼트 셰이더 (스크린 UV -> 버퍼 샘플링) |
| **셰이더 수정** | 전용 셰이더 필요 (`CAT/UI/SoftMask`) | `SoftMask.cginc` include + `SOFTMASKABLE` 키워드 |
| **Stencil 지원** | Stencil Material 전파 방식 | `Mask` 클래스 상속 (네이티브 Stencil) |
| **TMP 지원** | 전용 셰이더 자동 적용 | 샘플 임포트 필요 + include 경로 수동 설정 |

### 기능 비교

| 기능 | CAT SoftMask | mob-sakai |
|------|:-----------:|:---------:|
| 알파 그라디언트 마스킹 | O | O |
| Softness 조절 | O | O (SoftMaskingPower) |
| Invert Mask | O | X (MaskingShape Subtract로 대체) |
| 마스크 그래픽 숨기기 | O | O |
| 중첩 마스크 | 2단계 | 4단계 |
| Sprite Atlas | O (트리밍 보정 포함) | O |
| ScrollView | O (UNITY_UI_CLIP_RECT) | O |
| UI Mask 호환 | O (Stencil 전파) | O (Mask 상속) |
| SpriteRenderer | X (셰이더만 준비, C# 미구현) | X (UI 전용) |
| TextMeshPro | O (전용 셰이더 자동 적용) | O (샘플 임포트 + 경로 설정) |
| TMP Material Preset 동적 변경 | O (자동 감지 + 플레이모드 보존) | O |
| MaskingShape (가산/감산) | X | O |
| Anti-Aliasing 모드 | X | O (Stencil+Vertex 방식) |
| Alpha Hit Test (Raycast) | X | O |
| ShaderGraph 지원 | X | O (v3.3.0+) |
| VR Stereo | X | O |
| World Space Canvas | O | O |
| 에디터 버퍼 미리보기 | X | O |

### 성능 비교

| 항목 | CAT SoftMask | mob-sakai |
|------|-------------|-----------|
| **메모리 (마스크당)** | Material 1개 (~1KB) + TMP Material N개 | RenderTexture (~144KB~2.25MB) + Material N개 |
| **GPU 패스** | 0 추가 | 1 CommandBuffer 패스 (마스크 버퍼 렌더링) |
| **프래그먼트 비용** | tex2D 1회 + step x4 + smoothstep | tex2D 1회 + pow() |
| **타일 GPU 영향** | 없음 (1-Pass) | RenderTarget 전환 비용 (모바일 타일 GPU에 불리) |
| **배칭** | 공유 Material로 배칭 가능 | SoftMaskable variant가 배칭 차단 |
| **중첩 비용** | tex2D 1회 추가 (키워드 분기) | Blit(부모->자식) + CommandBuffer 추가 |
| **정적 UI** | 더티 체크로 거의 무비용 | isDirty 플래그로 버퍼 렌더링 스킵 |

### CAT SoftMask를 선택하는 이점

1. **모바일 메모리 절감**: RenderTexture가 없으므로 마스크당 수 MB의 메모리를 절약합니다. 10개의 SoftMask를 사용하면 mob-sakai 대비 ~20MB 절감 가능합니다.

2. **타일 기반 GPU 친화적**: 모바일 GPU(Adreno, Mali, Apple)는 타일 기반 렌더링을 사용하며, RenderTarget 전환 시 타일 메모리를 플러시해야 합니다. CAT SoftMask는 RenderTarget 전환이 없어 이 비용을 완전히 회피합니다.

3. **Draw Call 영향 최소**: SoftMask당 1개 공유 Material로 배칭이 가능하며, mob-sakai처럼 자식별 Material variant를 생성하지 않습니다.

4. **TMP 자동 적용**: mob-sakai는 TMP 지원을 위해 별도 샘플을 임포트해야 하고, TMPro_Properties.cginc의 include 경로를 프로젝트에 맞게 수정해야 합니다. CAT SoftMask는 TMP 자식을 자동 감지하여 전용 셰이더를 적용하므로 추가 설정이 불필요합니다.

5. **Material Preset 자동 감지**: TMP Material Preset(Outline, Underlay 등)을 변경하면 CAT SoftMask가 자동으로 감지하여 마스크를 재적용합니다.

6. **코드 투명성**: 전체 소스 코드가 프로젝트 내에 있어 디버깅, 커스터마이징, 최적화가 자유롭습니다. mob-sakai는 3000줄 이상의 복잡한 코드베이스입니다.

7. **의존성 없음**: 외부 패키지 의존 없이 독립적으로 동작합니다.

### mob-sakai를 선택해야 하는 경우

1. **4단계 이상 중첩 마스크**가 필요한 경우
2. **MaskingShape** (가산/감산 마스크 영역)이 필요한 경우
3. **ShaderGraph와의 통합**이 필요한 경우
4. **Alpha Hit Test** (마스크 영역 기반 터치 판정)이 필요한 경우
5. **기존 셰이더를 변경하지 않고** `SoftMask.cginc` include만으로 적용하고 싶은 경우

## 사용법

### 기본 사용

1. 마스크로 사용할 UI 오브젝트에 `Image` (또는 `RawImage`) 컴포넌트가 있어야 합니다
2. 같은 오브젝트에 `SoftMask` 컴포넌트를 추가합니다 (`Add Component > CAT > UI > SoftMask`)
3. 하위 Graphic 컴포넌트들에 자동으로 마스크가 적용됩니다

```
[SoftMask + Image (마스크 이미지)]    <- 부모: 알파가 마스킹 영역
  ├── [Image (자식 1)]                <- 자동으로 마스킹됨
  ├── [TextMeshProUGUI (자식 2)]      <- TMP 자동 감지, 전용 셰이더 적용
  └── [RawImage (자식 3)]             <- 자동으로 마스킹됨
```

### 중첩 마스크

```
[SoftMask A (외부 마스크)]
  └── [SoftMask B (내부 마스크)]      <- 자동으로 부모 마스크 감지
       └── [Image (자식)]             <- A와 B 마스크 모두 적용
```

### 인스펙터 설정

| 프로퍼티 | 설명 | 기본값 |
|---------|------|-------|
| **Show Mask Graphic** | 마스크 이미지 렌더링 여부 | true |
| **Softness** | 마스크 엣지 부드러움 (0=하드, 1=매우 부드러움) | 0.1 |
| **Invert Mask** | 마스크 영역 반전 | false |

### 스크립트 API

```csharp
SoftMask mask = GetComponent<SoftMask>();

// 프로퍼티 변경 (자동으로 Material 업데이트)
mask.Softness = 0.5f;
mask.InvertMask = true;
mask.ShowMaskGraphic = false;

// 자식 수동 갱신
mask.ApplyMaskToChildren();
mask.RestoreChildrenMaterials();

// 읽기 전용 상태
int count = mask.MaskedChildCount;
SoftMask parent = mask.ParentSoftMask;
```

## 셰이더 구조

### CAT_SoftMask.shader (일반 UI/Sprite)

```hlsl
CGINCLUDE
  // 공유 변수: _MaskTex, _Softness, _InvertMask, _MaskWorldToUV, _MaskUVRect
  // 중첩 전용 (#if _SOFTMASK_NESTED): _MaskTex2, _Softness2, ...

  SampleMask1(maskUV)  // 마스크 1 샘플링 (half precision, 분기 없음)
  SampleMask2(maskUV)  // 마스크 2 샘플링 (중첩 시에만 컴파일)
ENDCG

SubShader 0: UI     // UNITY_UI_CLIP_RECT, Stencil, ZTest [unity_GUIZTestMode]
SubShader 1: Sprite  // PIXELSNAP_ON, GPU Instancing
```

### CAT_TMP_SoftMask.shader (TextMeshPro)

```hlsl
#include "TMPro_Properties.cginc"    // TMP 유니폼 직접 포함

// SoftMask 유니폼 (_SoftMask* 접두사)
sampler2D _SoftMaskTex;
half _SoftMaskSoftness;
...

SampleSoftMask1(uv)  // 마스크 샘플링 (전역 유니폼 직접 접근, Metal 호환)
SampleSoftMask2(uv)  // 중첩 마스크 (키워드 분기)

// TMP SDF 렌더링 + SoftMask 적용 (premultiplied alpha)
c *= SampleSoftMask1(input.softMaskUV);
```

### 셰이더 키워드

| 키워드 | 타입 | 설명 |
|--------|------|------|
| `_SOFTMASK_NESTED` | `multi_compile_local` | 중첩 마스크 활성화 (비활성 시 추가 코드 완전 제거) |
| `_CAT_SOFTMASK` | `multi_compile_local` | 파티클 셰이더 SoftMask 활성화 (CAT_SoftMask_Core.cginc) |
| `UNITY_UI_CLIP_RECT` | `multi_compile_local` | RectMask2D/ScrollView 클리핑 |
| `UNITY_UI_ALPHACLIP` | `multi_compile_local` | 알파 클리핑 |
| `OUTLINE_ON` | `multi_compile` | TMP Outline (TMP 셰이더 전용, 빌드 variant 보호) |
| `UNDERLAY_ON` / `UNDERLAY_INNER` | `multi_compile` | TMP Underlay (TMP 셰이더 전용, 빌드 variant 보호) |

## 호환성

| 환경 | 지원 |
|------|------|
| Unity 6 (6000.0.x) | O |
| URP 17.2.0 | O |
| Sprite Atlas | O (트리밍 보정 포함) |
| UI Mask (Stencil) | O |
| RectMask2D | O |
| ScrollView | O |
| 중첩 Canvas | O |
| SpriteRenderer | X (셰이더만 준비, C# 미구현) |
| TextMeshPro | O (자동 감지 + 전용 셰이더) |
| TMP Outline/Underlay | O |
| TMP Material Preset | O (동적 변경 감지 + 플레이모드 보존) |
| UIParticle (com.coffee.ui-particle) | O (원본 셰이더/블렌드 모드 보존) |
| 에디터 실시간 편집 | O |

## 빌드 안정성

### 해결된 빌드 이슈

#### 1. Shader.Find() 빌드 실패 방지

**문제**: `Shader.Find()`는 빌드에 포함된 셰이더만 검색합니다. SoftMask 셰이더는 런타임에서 `new Material(shader)`로 생성하는 `DontSave` Material만 사용하므로, 빌드 시 영구 에셋에서 참조되지 않아 **셰이더가 빌드에서 제외**될 수 있습니다.

**해결**: `[SerializeField]` 셰이더 참조를 컴포넌트에 추가하여, 씬에 SoftMask가 있으면 셰이더가 자동으로 빌드에 포함됩니다.

```csharp
[SerializeField, HideInInspector] private Shader _maskShader;      // CAT/UI/SoftMask
[SerializeField, HideInInspector] private Shader _tmpMaskShader;    // CAT/UI/TMP_SoftMask
```

- `OnValidate()`에서 null이면 자동으로 `Shader.Find()`로 설정
- `Reset()`에서 컴포넌트 추가 시 자동 설정
- `GetCachedShader()`가 직렬화 참조 → 정적 캐시 → `Shader.Find()` 순으로 폴백

#### 2. TMP 셰이더 Variant 스트리핑 방지

**문제**: `shader_feature`로 선언된 키워드는 빌드 시 영구 Material에서 활성화된 variant만 포함합니다. `CAT/UI/TMP_SoftMask` 셰이더의 Material은 모두 런타임 생성이므로, `OUTLINE_ON`과 `UNDERLAY_ON` variant가 빌드에서 **스트리핑**되어 Outline/Underlay가 표시되지 않을 수 있습니다.

**해결**: `shader_feature` → `multi_compile`로 변경하여 모든 variant를 빌드에 포함합니다.

```hlsl
// 변경 전 (빌드에서 variant 스트리핑 가능)
#pragma shader_feature __ OUTLINE_ON
#pragma shader_feature __ UNDERLAY_ON UNDERLAY_INNER

// 변경 후 (모든 variant 빌드 포함)
#pragma multi_compile __ OUTLINE_ON
#pragma multi_compile __ UNDERLAY_ON UNDERLAY_INNER
```

- Variant 수: 2(Outline) × 3(Underlay) × 2(ClipRect) × 2(AlphaClip) × 2(Nested) = **48개**
- TMP 전용 셰이더이므로 variant 증가가 전체 빌드에 미치는 영향은 미미

## 제한사항

- 최대 **2단계** 중첩 마스크 (셰이더 키워드 제한)
- 자식의 기존 Material을 SoftMask 전용 셰이더로 교체하므로, **커스텀 셰이더를 사용하는 자식**에는 별도 대응 필요 (단, `CAT/Particles/*` 셰이더는 자동 지원)
- SpriteRenderer 미지원 (셰이더 SubShader는 준비되어 있으나 C# 로직 미구현)
- MaskingShape (가산/감산 영역) 미지원
- TMP 자식은 폰트별 개별 Material 생성 필요 (공유 Material 불가)
- TMPro_Properties.cginc 경로가 하드코딩됨 (`Assets/Plugins/TextMesh Pro/Shaders/`)

## 변경 이력

### v1.2.0 (2025-02-10)

UI Mask 동적 배치 버그 수정

**버그 수정**
- UI Mask 하위에 SoftMask를 배치하거나 프리팹을 로드할 때 초기 렌더링이 비정상인 버그 수정
- `Canvas.willRenderCanvases` 이벤트를 활용하여 레이아웃 완료 후 마스크 행렬 갱신
- `OnTransformParentChanged()` 콜백 추가로 부모 변경 시 즉시 갱신
- (에디터 전용) UI Mask의 `showMaskGraphic` 변경 시 자식 SoftMask 자동 갱신

**SoftMask.cs 변경**
- `_pendingLayoutRefresh` 플래그 추가 — Canvas 레이아웃 완료 대기
- `OnCanvasPreRender()` 메서드 추가 — `willRenderCanvases` 이벤트 핸들러
- `OnTransformParentChanged()` 메서드 추가 — 부모 Transform 변경 감지
- (에디터 전용) `_parentUIMask`, `_cachedParentMaskShowGraphic` 필드 추가
- (에디터 전용) `CacheParentUIMask()`, `CheckParentUIMaskChanges()` 메서드 추가

### v1.1.0 (2025-02-07)

UIParticle (com.coffee.ui-particle) 지원 추가

**신규 기능**
- `CAT/Particles/*` 셰이더 자동 감지 (`IsParticleMaterial()`)
- 원본 Material 복제 + `_CAT_SOFTMASK` 키워드 활성화 (`CreateParticleMaskMaterial()`)
- 원본 셰이더/블렌드 모드 보존 (Additive, AlphaBlend 등)
- UIParticle Material 재할당 자동 감지 및 재적용 (`DetectParticleMaterialChanges()`)
- 자식 수 변경 감지로 UIParticle 활성/비활성 시 새 자식 자동 마스킹

**셰이더**
- `CAT_SoftMask_Core.cginc` 공용 include 파일 추가 (매크로 기반 통합)
- 5개 파티클 셰이더에 SoftMask 키워드 추가:
  - `CAT/Particles/UIAdditive` — premultiplied additive (`color *= mask`)
  - `CAT/Particles/UIAlphaBlend` — alpha blend (`col.a *= mask`)
  - `CAT/Particles/FlowUV` — alpha blend (`col.a *= mask`)
  - `CAT/Particles/UIAlphaBlendCustom` — dissolve + alpha blend (`finalCol.a *= mask`)
  - `CAT/Particles/UIAdditiveCustom` — dissolve + additive (`finalCol.a *= mask`)
- mob-sakai 미사용 키워드 제거 (`SOFTMASK_SIMPLE/SLICED/TILED`)

**SoftMask.cs**
- `_particleMaskMaterials`, `_particleAppliedMaskMats` 필드 추가
- `UpdateParticleMaterials()` — 파티클 Material 프로퍼티 갱신
- `PropagateToStencilMaterials()` — `CAT/Particles/*` 셰이더 지원 추가
- 자식 수 변경 감지를 플레이모드에서도 실행 (UIParticle 대응)

### v1.0.0 (2025-02-07)

초기 안정 릴리스

**핵심 기능**
- 알파 채널 기반 1-Pass 소프트 마스킹 (RenderTexture 없음)
- Matrix4x4 기반 월드→UV 변환 (회전/스케일 대응)
- SoftMask당 1개 공유 Material (배칭 최적화)
- 더티 체크로 불필요한 Material 업데이트 스킵
- Softness, Invert Mask, Show/Hide Mask Graphic
- 최대 2단계 중첩 마스크 (`_SOFTMASK_NESTED` 키워드)

**Atlas/호환성**
- Sprite Atlas 트리밍 보정 (`GetContentLocalRect()`)
- Atlas UV 보정 (`DataUtility.GetOuterUV()`)
- UI Mask (Stencil) 호환 - Stencil Material 프로퍼티 전파
- RectMask2D / ScrollView 호환 (`UNITY_UI_CLIP_RECT`)

**TextMeshPro 지원**
- TMP 전용 셰이더 (`CAT/UI/TMP_SoftMask`) 자동 적용
- `CopyShaderProperties()` 개별 프로퍼티 복사 (프로퍼티 시트 보존)
- Outline, Underlay, Inner Underlay 키워드 호환
- TMP Material Preset 외부 변경 자동 감지 및 재적용
- TMP Material Preset 플레이모드 보존 (`_tmpOriginalBackup` 직렬화 백업)
- TMP_SubMeshUI (멀티 아틀라스) 대응
- `_SoftMask*` 접두사로 TMPro `_MaskTex` 충돌 방지
- Premultiplied alpha 블렌딩 호환 (`Blend One OneMinusSrcAlpha`)

**빌드 안정성**
- `[SerializeField]` 셰이더 참조로 `Shader.Find()` 빌드 실패 방지
- TMP 셰이더 `shader_feature` → `multi_compile` (variant 스트리핑 방지)
- `OnValidate()` / `Reset()`에서 셰이더 참조 자동 설정

**에디터**
- `[ExecuteAlways]` 에디터 실시간 프리뷰
- 자식 오브젝트 변경 자동 감지 (`CheckForChildChanges()`)
- 커스텀 인스펙터 (`SoftMaskEditor.cs`)
