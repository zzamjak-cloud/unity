# CAT SoftMask

알파 채널 기반 1-Pass 소프트 마스킹 컴포넌트 (모바일 최적화)

## 개요

Unity UI의 기본 `Mask` 컴포넌트는 Stencil 버퍼 기반으로 바이너리(0/1) 클리핑만 지원하여 마스킹 엣지에 계단 현상이 발생합니다. CAT SoftMask는 **텍스처 알파 채널을 기반으로 부드러운 마스킹**을 제공하며, **RenderTexture 없이 단일 패스**로 처리하여 모바일 환경에 최적화되어 있습니다.

```
Assets/Scripts/SoftMask/
├── SoftMask.cs                   # 메인 컴포넌트 (680줄)
├── Shader/
│   └── CAT_SoftMask.shader       # UI + Sprite 통합 셰이더 (283줄)
└── Editor/
    └── SoftMaskEditor.cs         # 커스텀 인스펙터 (165줄)
```

## 주요 기능

| 기능 | 설명 |
|------|------|
| **알파 마스킹** | 자신의 UI Graphic 알파 채널을 마스크로 사용 |
| **Softness 조절** | `smoothstep` 기반으로 마스크 엣지 부드러움 0~1 조절 |
| **Invert Mask** | 마스크 영역 반전 (밝은 영역 ↔ 어두운 영역 교환) |
| **Show/Hide Mask Graphic** | 마스킹은 유지하면서 마스크 이미지 표시/숨김 토글 |
| **중첩 마스크** | 최대 2단계 중첩 SoftMask 지원 (`_SOFTMASK_NESTED` 키워드) |
| **회전/스케일 대응** | `Matrix4x4` 기반 월드→UV 변환으로 회전, 스케일 완전 지원 |
| **Sprite Atlas 호환** | `DataUtility.GetOuterUV()` + 트리밍 보정으로 Atlas 스프라이트 정확한 UV 매핑 |
| **ScrollView 호환** | `UNITY_UI_CLIP_RECT` 지원으로 ScrollView 내 정상 동작 |
| **UI Mask 호환** | Stencil 래핑 Material에 프로퍼티 전파로 UI Mask 내 정상 동작 |
| **자식 자동 마스킹** | 하위 Graphic 컴포넌트에 자동으로 마스크 Material 적용 |
| **에디터 실시간 프리뷰** | `[ExecuteAlways]`로 에디터에서 즉시 결과 확인 |

## 아키텍처

### 렌더링 파이프라인

```
┌─────────────────────────────────────────────────────────────┐
│ SoftMask (부모)                                              │
│  ├─ 자신의 UI.Graphic 알파 → 마스크 텍스처로 사용            │
│  ├─ ComputeWorldToMaskUV() → Matrix4x4 (worldToLocal × UV)  │
│  └─ 1개 공유 Material 생성 → 모든 자식에 적용                │
│                                                              │
│  자식 Graphic들                                              │
│  ├─ Vertex Shader: 월드좌표 → 마스크 UV 계산                 │
│  └─ Fragment Shader: tex2D(마스크) → smoothstep → alpha 곱   │
└─────────────────────────────────────────────────────────────┘
```

### 핵심 설계 원칙

1. **1-Pass 렌더링**: RenderTexture 없이 기존 렌더링 패스에서 마스크 샘플링 수행
2. **SoftMask당 1개 공유 Material**: 모든 자식이 동일 Material 공유 (N개 Material → 1개)
3. **더티 체크**: `Matrix4x4` 비교, 텍스처 ID 비교, 프로퍼티 값 비교로 불필요한 업데이트 스킵
4. **버텍스 셰이더 UV 계산**: 마스크 UV를 버텍스에서 계산하여 프래그먼트 비용 절감
5. **분기 없는 셰이더**: `step()`, `smoothstep()`, `lerp()`로 GPU 분기 완전 회피

### Material 업데이트 흐름

```
LateUpdate()
  └── UpdateSharedMaterial()
       ├── ComputeWorldToMaskUV() → Matrix4x4 비교
       │   └── 변경됨? → _sharedMaskMaterial.SetMatrix()
       ├── GetMaskTexture() → InstanceID 비교
       │   └── 변경됨? → _sharedMaskMaterial.SetTexture()
       ├── Softness / InvertMask → float/bool 비교
       │   └── 변경됨? → _sharedMaskMaterial.SetFloat()
       ├── 부모 마스크 프로퍼티 (중첩 시)
       │   └── 변경됨? → _sharedMaskMaterial.Set*2()
       └── anyChange?
            └── PropagateToStencilMaterials()
                 └── 각 자식의 materialForRendering에도 프로퍼티 복사
                     (UI Mask의 StencilMaterial 복사본 대응)
```

## 성능 특성

### 메모리

| 항목 | 비용 |
|------|------|
| Material 인스턴스 | SoftMask당 1개 (자식 수 무관) |
| RenderTexture | **없음** (1-Pass 방식) |
| Dictionary 오버헤드 | 자식 수 × (Graphic ref + Material ref) |
| 셰이더 Variant | 2개 (기본 + `_SOFTMASK_NESTED`) × 2 SubShader |

### CPU (프레임당)

| 상황 | 비용 |
|------|------|
| **정적 UI (변화 없음)** | Matrix4x4 비교 1회 + 텍스처 ID 비교 1회 + float 비교 2회 → **거의 무비용** |
| **Transform 변경** | Matrix4x4 계산 + `Material.SetMatrix()` 1회 + Stencil 전파 |
| **프로퍼티 변경** | `Material.SetFloat()` 2회 + Stencil 전파 |
| **Stencil 전파** | 자식 수 × `materialForRendering` 접근 (UI Mask 내에서만) |

### GPU (프래그먼트당)

| 연산 | 비용 |
|------|------|
| `tex2D(_MaskTex)` | 텍스처 샘플링 1회 |
| `step()` × 4 | 경계 검사 (분기 없음) |
| `smoothstep()` | 소프트 엣지 |
| `lerp()` | 반전 처리 |
| **중첩 마스크 추가** | 위 연산 × 2 (키워드 비활성 시 제거됨) |

### 주의 사항

1. **자식이 매우 많은 경우 (50+)**: `PropagateToStencilMaterials()`에서 각 자식의 `materialForRendering` 접근이 `GetComponents()` + `StencilMaterial.Add/Remove`를 트리거합니다. 정적 UI에서는 발생하지 않으며, 프로퍼티 변경 시에만 실행됩니다.

2. **스크롤 중인 SoftMask**: 마스크 또는 자식의 Transform이 매 프레임 변경되므로 Matrix4x4 업데이트가 매 프레임 발생합니다. 이는 설계상 의도된 동작이며, `Material.SetMatrix()` 1회의 비용은 미미합니다.

3. **중첩 마스크**: `_SOFTMASK_NESTED` 키워드 활성화 시 프래그먼트 셰이더에서 텍스처 샘플링이 2회로 증가합니다. 비중첩 마스크는 `multi_compile_local`로 추가 비용이 **완전히 제거**됩니다.

4. **Atlas 스프라이트 트리밍**: `GetContentLocalRect()`에서 `sprite.textureRectOffset`, `sprite.textureRect` 접근이 매 프레임 발생하지만, 이는 Unity 내부 캐싱된 프로퍼티로 오버헤드가 거의 없습니다.

## mob-sakai SoftMaskForUGUI와 비교

### 아키텍처 차이

| 항목 | CAT SoftMask | mob-sakai SoftMaskForUGUI v3 |
|------|-------------|----------------------------|
| **렌더링 방식** | 1-Pass (기존 패스에서 마스크 샘플링) | RenderTexture에 마스크 렌더링 후 자식이 샘플링 |
| **추가 GPU 패스** | 없음 | CommandBuffer로 마스크 버퍼 렌더링 (별도 패스) |
| **RenderTexture** | 불필요 | ARGB32 버퍼 필수 (1024×576 @ 1080p ≈ 2.25MB) |
| **Material 관리** | SoftMask당 1개 공유 | `MaterialRepository` Hash128 기반 캐싱 (자식별 variant) |
| **마스크 UV 계산** | 버텍스 셰이더 (Matrix4x4 변환) | 프래그먼트 셰이더 (스크린 UV → 버퍼 샘플링) |
| **셰이더 수정** | 전용 셰이더 필요 (`CAT/UI/SoftMask`) | `SoftMask.cginc` include + `SOFTMASKABLE` 키워드 |
| **Stencil 지원** | Stencil Material 전파 방식 | `Mask` 클래스 상속 (네이티브 Stencil) |

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
| SpriteRenderer | O (SubShader 분리) | X (UI 전용) |
| MaskingShape (가산/감산) | X | O |
| Anti-Aliasing 모드 | X | O (Stencil+Vertex 방식) |
| Alpha Hit Test (Raycast) | X | O |
| TextMeshPro 전용 셰이더 | X | O (샘플 임포트) |
| ShaderGraph 지원 | X | O (v3.3.0+) |
| VR Stereo | X | O |
| World Space Canvas | O | O |
| 에디터 버퍼 미리보기 | X | O |

### 성능 비교

| 항목 | CAT SoftMask | mob-sakai |
|------|-------------|-----------|
| **메모리 (마스크당)** | Material 1개 (~1KB) | RenderTexture (~144KB~2.25MB) + Material N개 |
| **GPU 패스** | 0 추가 | 1 CommandBuffer 패스 (마스크 버퍼 렌더링) |
| **프래그먼트 비용** | tex2D 1회 + step×4 + smoothstep | tex2D 1회 + pow() |
| **타일 GPU 영향** | 없음 (1-Pass) | RenderTarget 전환 비용 (모바일 타일 GPU에 불리) |
| **배칭** | 공유 Material로 배칭 가능 | SoftMaskable variant가 배칭 차단 |
| **중첩 비용** | tex2D 1회 추가 (키워드 분기) | Blit(부모→자식) + CommandBuffer 추가 |
| **정적 UI** | 더티 체크로 거의 무비용 | isDirty 플래그로 버퍼 렌더링 스킵 |

### CAT SoftMask를 선택하는 이점

1. **모바일 메모리 절감**: RenderTexture가 없으므로 마스크당 수 MB의 메모리를 절약합니다. 10개의 SoftMask를 사용하면 mob-sakai 대비 ~20MB 절감 가능합니다.

2. **타일 기반 GPU 친화적**: 모바일 GPU(Adreno, Mali, Apple)는 타일 기반 렌더링을 사용하며, RenderTarget 전환 시 타일 메모리를 플러시해야 합니다. CAT SoftMask는 RenderTarget 전환이 없어 이 비용을 완전히 회피합니다.

3. **Draw Call 영향 최소**: SoftMask당 1개 공유 Material로 배칭이 가능하며, mob-sakai처럼 자식별 Material variant를 생성하지 않습니다.

4. **코드 투명성**: 전체 소스 코드가 프로젝트 내에 있어 디버깅, 커스터마이징, 최적화가 자유롭습니다. mob-sakai는 3000줄 이상의 복잡한 코드베이스입니다.

5. **SpriteRenderer 지원**: UI뿐만 아니라 2D Sprite에도 마스킹을 적용할 수 있습니다.

6. **의존성 없음**: 외부 패키지 의존 없이 독립적으로 동작합니다.

### mob-sakai를 선택해야 하는 경우

1. **4단계 이상 중첩 마스크**가 필요한 경우
2. **MaskingShape** (가산/감산 마스크 영역)이 필요한 경우
3. **TextMeshPro 전용 SoftMask 셰이더**가 필요한 경우
4. **ShaderGraph와의 통합**이 필요한 경우
5. **Alpha Hit Test** (마스크 영역 기반 터치 판정)이 필요한 경우
6. **기존 셰이더를 변경하지 않고** `SoftMask.cginc` include만으로 적용하고 싶은 경우

## 사용법

### 기본 사용

1. 마스크로 사용할 UI 오브젝트에 `Image` (또는 `RawImage`) 컴포넌트가 있어야 합니다
2. 같은 오브젝트에 `SoftMask` 컴포넌트를 추가합니다 (`Add Component > CAT > UI > SoftMask`)
3. 하위 Graphic 컴포넌트들에 자동으로 마스크가 적용됩니다

```
[SoftMask + Image (마스크 이미지)]    ← 부모: 알파가 마스킹 영역
  ├── [Image (자식 1)]                ← 자동으로 마스킹됨
  ├── [Text (자식 2)]                 ← 자동으로 마스킹됨
  └── [RawImage (자식 3)]             ← 자동으로 마스킹됨
```

### 중첩 마스크

```
[SoftMask A (외부 마스크)]
  └── [SoftMask B (내부 마스크)]      ← 자동으로 부모 마스크 감지
       └── [Image (자식)]             ← A와 B 마스크 모두 적용
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

```hlsl
// CAT_SoftMask.shader 구조

CGINCLUDE
  // 공유 변수: _MaskTex, _Softness, _InvertMask, _MaskWorldToUV, _MaskUVRect
  // 중첩 전용 (#if _SOFTMASK_NESTED): _MaskTex2, _Softness2, ...

  SampleMask1(maskUV)  // 마스크 1 샘플링 (half precision, 분기 없음)
  SampleMask2(maskUV)  // 마스크 2 샘플링 (중첩 시에만 컴파일)
ENDCG

SubShader 0: UI     // UNITY_UI_CLIP_RECT, Stencil, ZTest [unity_GUIZTestMode]
SubShader 1: Sprite  // PIXELSNAP_ON, GPU Instancing
```

### 셰이더 키워드

| 키워드 | 타입 | 설명 |
|--------|------|------|
| `_SOFTMASK_NESTED` | `multi_compile_local` | 중첩 마스크 활성화 (비활성 시 추가 코드 완전 제거) |
| `UNITY_UI_CLIP_RECT` | `multi_compile_local` | RectMask2D/ScrollView 클리핑 |
| `UNITY_UI_ALPHACLIP` | `multi_compile_local` | 알파 클리핑 |

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
| SpriteRenderer | O |
| 에디터 실시간 편집 | O |

## 제한사항

- 최대 **2단계** 중첩 마스크 (셰이더 키워드 제한)
- 자식의 기존 Material을 SoftMask 전용 셰이더로 교체하므로, **커스텀 셰이더를 사용하는 자식**에는 별도 대응 필요
- TextMeshPro는 별도 셰이더가 필요 (현재 미지원)
- MaskingShape (가산/감산 영역) 미지원
