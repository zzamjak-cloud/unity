# CAT_VFX

월드 기반 ParticleSystem을 Canvas UI 뎁스 구조에 따라 렌더링하는 자체 이펙트 시스템.
Camera/RenderTexture 없이 메시 베이킹으로 UI 파이프라인에 직접 통합한다.

## 핵심 원리

1. `MaskableGraphic`을 상속한 `CatUIParticle`이 Canvas 하이어라키에 위치
2. `ParticleSystemRenderer.BakeMesh()`로 파티클 메시를 캡처
3. 월드/로컬 좌표 → Canvas 로컬 좌표로 행렬 변환
4. `CanvasRenderer.SetMesh()`로 UI 뎁스 순서에 맞게 렌더링
5. Stencil Mask 지원 (셰이더에 `_Stencil` 프로퍼티 포함)

## 폴더 구조

```
CAT_VFX/
├── Script/
│   ├── UIParticle/
│   │   ├── CatUIParticle.cs          # 메인 컴포넌트 (AddComponentMenu 등록)
│   │   ├── CatUIParticleRenderer.cs  # ParticleSystem별 숨겨진 렌더러 (메시 베이킹 핵심)
│   │   ├── CatUIParticleUpdater.cs   # 정적 업데이트 루프 (Canvas 리빌드 후 실행)
│   │   ├── CatUIParticleSettings.cs  # 전역 설정 (HideFlags 등)
│   │   └── AnimatableProperty.cs     # AnimationClip ↔ Material 프로퍼티 동기화
│   ├── Utilities/
│   │   ├── MaterialRepository.cs     # Hash128 기반 레퍼런스 카운팅 머티리얼 캐시
│   │   ├── ObjectRepository.cs       # 제네릭 레퍼런스 카운팅 오브젝트 풀
│   │   ├── UIExtraCallbacks.cs       # Canvas.willRenderCanvases 후 콜백 (이중 등록 트릭)
│   │   └── FastAction.cs             # 순회 중 안전한 델리게이트 컨테이너
│   └── Extensions/
│       ├── Vector3Extensions.cs      # Inverse(), GetScaled(), IsVisible()
│       ├── ParticleSystemExtensions.cs # BakeMesh 헬퍼, 시뮬레이션 공간 쿼리, 정렬
│       └── SpriteExtensions.cs       # 아틀라스 대응 텍스처 참조
├── Editor/
│   ├── UIParticle/
│   │   ├── CatUIParticleEditor.cs    # 커스텀 인스펙터 (Replay 버튼 포함)
│   │   ├── CatUIParticleMenu.cs      # GameObject/UI/CAT Particle System 메뉴
│   │   └── AnimatablePropertyEditor.cs # 셰이더 프로퍼티 선택 드롭다운
│   ├── HierarchyVFXModule.cs         # 하이어라키 VFX 드롭다운 (Use UI 체크박스)
│   └── VFXPreviewer.cs               # VFX 프리뷰 윈도우 (Spacebar 토글)
├── Shader/
│   ├── CAT_UIAdditive.shader         # UI Additive 블렌드 (Stencil Mask + SoftMask 조건부)
│   └── CAT_UIAlphaBlend.shader       # UI AlphaBlend (Stencil Mask + SoftMask 조건부)
├── Materials/                         # 공용 머티리얼
├── VFX_Prefabs/                       # 이펙트 프리팹 (카테고리별 하위 폴더)
└── Texture/                           # 이펙트 텍스처
```

## 네임스페이스

| 네임스페이스 | 용도 |
|-------------|------|
| `CAT.VFX` | 공개 API (`CatUIParticle`, `CatUIParticleUpdater`, `AnimatableProperty`) |
| `CAT.VFX.Internal` | 내부 유틸리티 (Extensions, Utilities) |
| `CAT.VFX.Editor` | 에디터 전용 |

## 사용 방법

### 1. VFX Previewer (권장)

Project 뷰에서 프리팹 선택 후 **Spacebar**로 프리뷰 윈도우를 토글한다.

- **카테고리 탐색**: `VFX_Prefabs` 폴더의 하위 폴더 구조가 카테고리로 표시. 클릭으로 진입, `<` 헤더로 뒤로가기
- **체크박스**: 카테고리 간 이펙트 비교 선택 (체크 유지)
- **클릭**: 단일 선택 (기존 체크 해제)
- **Ctrl+클릭**: 토글 추가/제거
- **Shift+클릭**: 범위 선택
- **이름태그 클릭**: 하이어라키에 프리팹 인스턴스 (현재 선택 오브젝트의 자식으로)
- **자동 크기 조정**: 파티클 바운드를 측정하여 프리뷰 셀 크기에 맞춤
- **검색**: 카테고리 무시하고 전체 프리팹에서 필터링

### 2. 하이어라키 VFX 드롭다운

하이어라키 창 상단의 **VFX** 버튼 사용:

- **Use UI 체크 해제**: 프리팹을 일반 이펙트로 인스턴스
- **Use UI 체크 활성화**: `CatUIParticle` 래퍼를 자동 생성하고, 프리팹을 자식으로 배치, 레이어를 UI로 변경, 머티리얼 자동 등록까지 원클릭 처리
- 이미 `CatUIParticle`이 포함된 프리팹은 Use UI 체크와 무관하게 일반 로드

**대상 폴더**: 프로젝트 내 `VFX_Prefabs` 이름의 모든 폴더를 자동 스캔 (우클릭으로 폴더명 변경 가능, VFX Previewer와 공유)

### 3. 수동 설정

1. Canvas 하위에 빈 GameObject 생성
2. `Add Component > UI > CAT UI Particle` 추가
3. 자식으로 ParticleSystem 프리팹 배치
4. 인스펙터에서 `Refresh` 버튼 클릭 → 머티리얼 등록
5. ParticleSystem의 머티리얼을 `CAT/VFX/UIAdditive` 또는 `CAT/VFX/UIAlphaBlend`로 변경

### 4. 메뉴에서 생성

- `GameObject > UI > CAT Particle System` — ParticleSystem 포함 생성
- `GameObject > UI > CAT Particle System (Empty)` — 빈 컴포넌트만 생성

## 컴포넌트 주요 프로퍼티

| 프로퍼티 | 설명 | 기본값 |
|---------|------|--------|
| Scale | 파티클 렌더링 스케일 (3D 토글 지원) | 100 |
| Position Mode | Relative(상대) / Absolute(절대) 방출 위치 | Relative |
| Auto Scaling Mode | Canvas 스케일 변경 시 보정 방식 | Transform |
| Use Custom View | 커스텀 베이크 뷰 크기 사용 | false |
| Time Scale Multiplier | 시간 스케일 배수 | 1 |
| Maskable | Stencil Mask 참여 여부 | true |
| Animatable Properties | AnimationClip에서 제어할 셰이더 프로퍼티 | - |

### Replay 버튼

인스펙터 상단의 **▶ Replay** 버튼으로 에디터 EditMode에서 ParticleSystem 선택 없이 이펙트를 즉시 미리보기할 수 있다.
누를 때마다 현재 상태를 즉시 초기화하고 처음부터 재생한다.

## 셰이더

### 외부 의존성 처리

셰이더는 외부 패키지 없이 독립적으로 동작한다.

**mob-sakai SoftMask** (`com.coffee.softmask-for-ugui`):
- `shader_feature_local_fragment _ SOFTMASKABLE` 키워드로 조건부 지원
- 패키지 미설치 시 해당 variant가 컴파일되지 않으므로 `#include`에 도달하지 않아 안전
- 패키지 설치 후 `SoftMaskable` 컴포넌트가 키워드를 활성화하면 자동 적용

**SoftMaskLight** (자체 개발):
- Hidden 변형 셰이더가 별도 파일이므로 동일한 조건부 처리 불가
- 필요 시 CLAUDE.md의 SoftMaskLight 가이드라인에 따라 Hidden 변형 셰이더를 별도 생성

### Linear 색 공간 대응

버텍스 셰이더에서 `LinearToGammaSpace()` 함수로 감마 보정을 처리한다.
CPU 측 변환이 불필요하여 매 프레임 수천 개 버텍스 순회 비용이 제거됨.

```hlsl
#if !defined(UNITY_COLORSPACE_GAMMA)
OUT.color.rgb = LinearToGammaSpace(OUT.color.rgb);
#endif
```

## 마스킹 지원

| 마스크 타입 | 지원 | 조건 |
|-----------|------|------|
| Stencil Mask (`Mask` 컴포넌트) | O | 셰이더에 `_Stencil` 프로퍼티 필요 |
| RectMask2D | X | 원본 패키지에서도 미지원 |

## 모바일 최적화

- Update에서 `new` 키워드 미사용 (정적 배열/리스트 캐싱)
- `Shader.PropertyToID` 결과 캐싱 (`AnimatableProperty.OnAfterDeserialize`)
- `MaterialRepository`로 동일 머티리얼 공유 (Hash128 기반 레퍼런스 카운팅)
- 런타임 생성 오브젝트에 `HideFlags.DontSave` 설정
- LINQ 미사용 (런타임 코드)
- IMeshModifier 캐싱 (`SetMaterialDirty` 시에만 갱신)
- 감마 보정을 셰이더에서 처리 (CPU 측 `GetColors`/`SetColors` 루프 제거)

## 의존성

- **Unity 6** (6000.0.x)
- **외부 패키지 없음** (SoftMask 관련은 선택적 조건부 지원)
- 하이어라키 VFX 모듈은 `CAT.HierarchyUtility` (`IHierarchyToolModule`) 필요
