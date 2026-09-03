# CAT Toon Shading

URP 17.2 / Unity 6 (6000.0.x) 용 카툰 셰이딩 세트입니다.
하프 램버트 기반 2톤(선택 3톤) 라이팅 + 카메라 Depth/DepthNormals 기반 스크린 스페이스 아웃라인으로 구성됩니다.

## 구성

| 파일 | 역할 |
| --- | --- |
| `Shader/CAT_Toon.shader` | 캐릭터/오브젝트용 툰 라이트 셰이더 (`CAT/Toon/ToonLit`) |
| `Shader/CAT_ToonInput.hlsl` | 머티리얼 프로퍼티 CBUFFER |
| `Shader/CAT_ToonLighting.hlsl` | 톤 분할·스페큘러·림·해칭 라이팅 모델 |
| `Shader/CAT_ToonForwardPass.hlsl` | Forward 패스 정점/픽셀 셰이더 |
| `Shader/CAT_ToonOutline.shader` | 풀스크린 아웃라인 (`CAT/Toon/ScreenSpaceOutline`) |
| `CATToonOutlineFeature.cs` | 아웃라인 `ScriptableRendererFeature` (RenderGraph) |
| `CATToonOutlineRuntime.cs` | 아웃라인 런타임 전역 오버라이드 |
| `CATToonOutlineController.cs` | 씬에서 아웃라인을 제어하는 컴포넌트 |
| `Editor/CATToonShaderGUI.cs` | 기능별 그룹 머티리얼 인스펙터 |

## 설정 방법

1. 머티리얼의 셰이더를 `CAT/Toon/ToonLit` 으로 변경합니다.
2. 사용 중인 **URP Renderer 에셋**에 `Add Renderer Feature ▸ CAT Toon Outline Feature` 를 추가합니다.
   이 프로젝트에는 `PC_Renderer.asset`, `Mobile_Renderer.asset`,
   `Universal Render Pipeline Asset_Renderer.asset` 세 곳에 이미 등록되어 있습니다.
3. 렌더러 피처가 `ConfigureInput` 으로 Depth / DepthNormals 를 요청하므로 URP Asset 의
   `Depth Texture` 옵션과 무관하게 필요한 텍스처가 자동 생성됩니다.
   (이 프로젝트는 PC/Mobile RP Asset 모두 Depth Texture 가 이미 켜져 있습니다.)

> 원근(Perspective) 카메라 기준으로 튜닝되어 있습니다. 직교 카메라에서는 깊이 정규화가 달라져
> `Depth Threshold` 를 다시 잡아야 합니다.

## 라이팅 모델

- **하프 램버트**: `N·L * 0.5 + 0.5`. `Half Lambert` 슬라이더로 일반 램버트와 블렌딩합니다.
  값이 1이면 명암 경계가 뒤로 밀려 그림자 면적이 줄고, 그림자가 새까맣게 죽지 않습니다.
- **2톤 분할**: `Shade Threshold` 기준으로 `smoothstep` 하드 스텝. `Shade Smoothness` 를 0에 가깝게 두면
  완전한 하드 엣지 2톤이 됩니다.
- **그림자 컬러감**: `Shade Color` 로 그림자 톤을 물들이고, `Shade Intensity` 로 깊이를 조절합니다.
  Intensity 를 낮추면 그림자가 밝게 남아 캐주얼한 인상이 강해집니다.
- **3톤(선택)**: `Enable Mid Tone` 을 켜면 밝은 톤 / 중간 톤 / 그림자 톤 3단계가 됩니다.
- **환경광**: `Ambient Strength` 로 SH/라이트맵 기여를 항상 더해 그림자 하한을 만듭니다.
- **툰 스페큘러**: 하이라이트를 계단 처리한 스텝 스페큘러. 밝은 톤 영역에만 나타납니다.
- **림 라이트**: 뷰 기준 프레넬. `Align To Light` 로 광원 쪽 림만 남길 수 있습니다.
- **스케치 해칭**: 스크린 스페이스 평행선 패턴을 그림자 영역에 얹습니다.
  가장 어두운 구간에는 직교 방향 해칭이 한 겹 더 들어가 교차 해칭이 됩니다.
  기본값은 의도적으로 옅습니다. 뚜렷하게 쓰려면 `Sketch Strength` 를 0.9 이상,
  `Line Spacing` 을 4~6 으로 낮추고 `Shade Intensity` 를 함께 올리세요.

## 아웃라인

`_CameraDepthTexture` 와 `_CameraNormalsTexture` 에 로버츠 크로스 4샘플 엣지 검출을 적용합니다.

- **깊이 엣지**: 실루엣과 겹침 경계. 중심 깊이로 나눠 정규화하므로 거리에 관계없이 두께가 일정합니다.
- **노멀 엣지**: 같은 깊이 안의 내부 크리스(옷 주름, 관절 등). 실루엣 경계에서는 자동으로 약해집니다.
- **Grazing Suppress**: 시선과 거의 평행한 바닥/벽에서 생기는 가짜 라인을 억제합니다.
- **Sketch Jitter**: 샘플 위치를 양자화된 시간 기반 노이즈로 흔들어 연필로 그린 듯한 떨림을 만듭니다.
  `Sketch Frequency` 를 8~12 정도로 낮추면 수작업 애니메이션 느낌이 납니다.
- **Blend Mode**: `Solid` 는 또렷한 단색 라인, `Multiply` 는 씬 컬러에 곱해 잉크가 스민 느낌입니다.
- **Thickness / Thickness Mode**: 기본 `Scale With Height`(권장). `Reference Height`(기본 1080) 기준으로
  두께를 환산하므로 UV 샘플링 간격이 화면 대비 고정되고, 뷰포트 크기가 변해도
  라인 굵기와 검출되는 엣지 양이 그대로 유지됩니다.
  `Fixed Pixels` 는 항상 같은 픽셀 두께라 뷰포트가 작아지면 라인이 오브젝트를 덮습니다.
  (샘플 간격이 0.5px 미만이 되면 같은 텍셀을 읽게 되므로 하한 0.5px 로 클램프합니다.)

씬 컬러를 복사하지 않고 블렌딩만으로 합성하므로 추가 렌더 타깃이 없습니다.

## 두께 / 컬러 조절 위치

**에디터** — 사용 중인 URP Renderer 에셋의 `CAT Toon Outline` 피처 인스펙터.
현재 활성 품질이 `PC` 이므로 `Assets/Settings/PC_Renderer.asset` 이 실제로 적용되는 곳입니다.
(모바일 품질로 전환하면 `Mobile_Renderer.asset`)

| 항목 | 프로퍼티 |
| --- | --- |
| 컬러 / 진하기 | `Outline Color` (알파 = 진하기) |
| 두께 | `Thickness` + `Thickness Mode` / `Reference Height` |
| 라인 양 | `Depth Threshold`, `Normal Threshold` |
| 합성 방식 | `Blend Mode` (Solid / Multiply) |

**런타임** — `CATToonOutlineRuntime` 전역 오버라이드 또는 `CATToonOutlineController` 컴포넌트.
아래 참고.

## 런타임 제어

```csharp
using CAT.Toon;

// 아웃라인 컬러 변경
CATToonOutlineRuntime.Color = Color.red;

// 두께·흔들림도 오버라이드 가능
CATToonOutlineRuntime.Thickness    = 2.5f;
CATToonOutlineRuntime.SketchJitter = 1.5f;

// 잠시 끄기
CATToonOutlineRuntime.Enabled = false;

// 렌더러 피처 설정값으로 복귀
CATToonOutlineRuntime.Reset();
```

`CATToonOutlineController` 컴포넌트를 씬에 두면 인스펙터에서 같은 값을 만질 수 있고,
`Flash(Color, duration)` 로 피격 연출용 순간 색 변경도 가능합니다.

## 성능

모바일 기준 비용이 큰 순서입니다.

| 항목 | 비용 | 대응 |
| --- | --- | --- |
| `Use Normal Edge` | **불투명 지오메트리 1회 추가 렌더**(URP DepthNormals 프리패스) + 픽셀당 노멀 샘플 5개 | 모바일에서는 끄기. 끄면 깊이만으로 평면 예측 오차를 계산해 실루엣/접힘을 검출하므로 프리패스가 사라진다 |
| 풀스크린 아웃라인 패스 | 픽셀당 텍스처 샘플 10개(노멀 엣지 on) / 5개(off) | `Use Normal Edge` off, 필요하면 주입 시점을 뒤로 미뤄 그리는 픽셀 수 줄이기 |
| 커스텀 렌더러 피처 | URP 의 `SupportsNativeRenderPass()` 가 `internal virtual` 이라 커스텀 피처는 Native Render Pass 병합에서 제외된다. 타일 기반 GPU 에서 컬러 어태치먼트 store/load 1회가 추가된다 | 우회 불가. 아웃라인이 필요 없는 씬에서는 피처를 꺼두기 |
| 셰이더 배리언트 | 미사용 `multi_compile` 제거로 9.06e9 → 9.44e7 (96배 감소). 아래 참고 | 추가로 줄이려면 베이크 라이트맵 지원(`LIGHTMAP_ON` 계열)까지 제거 가능 |

### 측정값 (Apple M1 Pro, 1920x1080, 에디터 오프스크린 렌더)

6라운드 교차 반복 · 라운드당 60프레임 · GPU 완료까지 동기화 후 각 구성의 최소값.
씬은 캐릭터 1체 + 지면.

| 구성 | 프레임당 | 아웃라인 OFF 대비 |
| --- | --- | --- |
| 아웃라인 OFF | 2.967 ms | — |
| 아웃라인 ON, `Use Normal Edge` off | 2.933 ms | ±0 (측정 노이즈 이내) |
| 아웃라인 ON, `Use Normal Edge` on | 3.188 ms | **+0.22 ms** |

이 프로젝트의 `PC_Renderer` 는 SSAO 가 `Source = DepthNormals` 라 DepthNormals 프리패스를
어차피 실행한다. 따라서 PC 에서 `Use Normal Edge` 를 꺼서 얻는 이득은 프리패스가 아니라
픽셀당 노멀 샘플 5개다. `Mobile_Renderer` 에는 SSAO 가 없으므로 그쪽에서만 프리패스가
통째로 추가된다.

### 제거된 multi_compile

프로젝트 설정상 쓰이지 않아 `CAT_Toon.shader` 에서 제외했다.
되살려야 하는 조건은 셰이더 파일 상단 주석에 적어두었다.

`_LIGHT_COOKIES`, `_LIGHT_LAYERS`, `DYNAMICLIGHTMAP_ON`, `USE_LEGACY_LIGHTMAPS`,
`LOD_FADE_CROSSFADE`, `ProbeVolumeVariants.hlsl`

셰이더 자체 배리언트 수 **9.06e9 → 9.44e7 (약 96배 감소)**.
(`ShaderUtil.GetVariantCount` 의 절대값에는 `FallBack "Universal Render Pipeline/Lit"` 체인이
포함돼 1.67e14 로 나오므로, URP Lit 값을 뺀 차이로 계산한 값이다. URP Lit 은 어차피 빌드에
포함되므로 이 fallback 이 빌드 크기를 실제로 늘리지는 않는다.)

베이크 라이트맵(`LIGHTMAP_ON` / `DIRLIGHTMAP_COMBINED` / `SHADOWS_SHADOWMASK`)과
소프트 섀도우 품질(`_SHADOWS_SOFT_*`, PC RP Asset 이 사용)은 유지했다.

CPU 쪽은 머티리얼 프로퍼티를 매 프레임 다시 쓰지 않고, 해상도·런타임 오버라이드처럼
실제로 바뀔 수 있는 값만 비교 후 반영한다. 나머지는 인스펙터 변경 시에만 갱신된다.

## 알려진 제약

- 아웃라인은 화면 전체에 적용됩니다. 특정 오브젝트만 제외하려면 별도 마스크 패스가 필요합니다.
- 반투명 오브젝트는 Depth/Normals 텍스처에 기록되지 않으므로 아웃라인이 생기지 않습니다.
  (기본 주입 시점이 `BeforeRenderingTransparents` 인 것도 같은 이유입니다.)
- 카메라가 백버퍼에 직접 렌더링하는 구성에서는 패스가 스킵됩니다.
