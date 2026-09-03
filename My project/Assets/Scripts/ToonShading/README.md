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

## 알려진 제약

- 아웃라인은 화면 전체에 적용됩니다. 특정 오브젝트만 제외하려면 별도 마스크 패스가 필요합니다.
- 반투명 오브젝트는 Depth/Normals 텍스처에 기록되지 않으므로 아웃라인이 생기지 않습니다.
  (기본 주입 시점이 `BeforeRenderingTransparents` 인 것도 같은 이유입니다.)
- 카메라가 백버퍼에 직접 렌더링하는 구성에서는 패스가 스킵됩니다.
