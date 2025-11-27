# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 언어 설정

**모든 코드 설명, 주석, 커밋 메시지는 한국어로 작성합니다.**

## 프로젝트 개요

Unity 2D 게임 프로젝트로, URP(Universal Render Pipeline)를 사용합니다. UI 중심의 프로젝트이며, 다양한 시각 효과와 애니메이션 기능을 포함합니다.

## 주요 의존성

- **Unity Version**: Unity 6 (6000.0.x)
- **Render Pipeline**: Universal Render Pipeline (URP) 17.2.0
- **DOTween**: 애니메이션 라이브러리 (`Assets/Plugins/Demigiant/DOTween/`)
- **TextMeshPro**: 텍스트 렌더링 (Unity 패키지)
- **UI Effect**: UI 효과 라이브러리 (com.coffee.ui-effect, com.coffee.ui-particle, com.coffee.softmask-for-ugui)
- **Spine Runtime**: 2D 애니메이션 (com.esotericsoftware.spine.spine-csharp)
- **Febucci Text Animator**: 텍스트 애니메이션 (`Assets/Plugins/Febucci/`)
- **ChocDino UIFX**: UI 효과 프레임워크 (`Assets/Plugins/ChocDino/`)

## 코드베이스 구조

### Assets/Scripts/ 디렉토리 구조

#### 1. **Curve/** - 베지어 곡선 시스템
베지어 곡선을 사용한 경로 이동 시스템입니다. ScriptableObject 기반 아키텍처를 사용하여 여러 오브젝트가 동일한 경로를 공유할 수 있습니다.

- **BezierPath.cs**: 에디터 전용 컴포넌트. 씬에서 경로를 시각적으로 편집하고 ScriptableObject로 저장
- **BezierPathData.cs**: 경로 데이터를 저장하는 ScriptableObject. 포인트 리스트, 루프 여부, Transform 정보 포함
- **BezierFollower.cs**: 런타임에서 경로를 따라 이동하는 컴포넌트. 캐싱 최적화 포함
- **Editor/BezierPathEditor.cs**: 씬뷰에서 경로를 편집할 수 있는 커스텀 에디터
- **Editor/BezierFollowerEditor.cs**: BezierFollower의 커스텀 인스펙터

**중요 아키텍처 패턴:**
- 에디터에서 BezierPath로 경로 편집 → BezierPathData ScriptableObject로 저장
- 런타임에서 BezierFollower가 BezierPathData 참조하여 이동
- UI 캔버스 대응: 부모 Transform 변경 시 로컬 좌표 기준으로 경로 유지
- Transform 캐싱으로 성능 최적화

#### 2. **UI/** - UI 컴포넌트
다양한 UI 커스텀 컴포넌트와 효과를 포함합니다.

**주요 컴포넌트:**
- **UICornerRound.cs / UICornerRoundMask.cs**: UI 모서리 둥글게 처리
- **UITMPCurve.cs / UITMPFollow.cs / UITMPLayoutLimiter.cs**: TextMeshPro 관련 유틸리티
- **UIMaskRadialStencil.cs**: 방사형 마스크 효과
- **UISlideTransitionController.cs / UISlideTransitionManager.cs**: 슬라이드 전환 애니메이션
- **UITabButton.cs / UITabManager.cs**: 탭 UI 시스템
- **InteractiveButton.cs / ButtonEventSystem.cs**: 버튼 인터랙션
- **RewardLayoutController.cs / MenuToggleController.cs**: 레이아웃 컨트롤러

#### 3. **Effects/** - 비주얼 이펙트
스프라이트와 이미지에 적용되는 다양한 시각 효과

- **ColorReplace.cs / SpriteColorLerp.cs / SpriteGroupColorLerp.cs**: 색상 조작
- **SpriteDissolve.cs**: 디졸브 효과
- **SpriteGradient.cs**: 그라디언트 효과
- **ImageDeform.cs**: 이미지 변형
- **Windable.cs**: 바람 효과
- **WaterPhysics2D.cs**: 2D 물리 효과
- **OldTVEffect.cs / NeonGlow.cs / ChristmasLights.cs**: 특수 효과

#### 4. **CAT/** - CAT 네임스페이스 관련 기능
- **CAT/UI/**: 팝업 시스템 (BasePopup.cs, MessagePopup.cs, PopupTester.cs)
- **AnimationStateDisplayer.cs**: 애니메이션 상태 표시

#### 5. **Editor/** - 에디터 확장
개발 생산성 향상을 위한 에디터 도구

**유틸리티:**
- **DOTweenEaseWindow.cs / DOTweenEaseMenuItems.cs / DOTweenSceneViewOverlay.cs**: DOTween 이징 시각화 및 선택 도구
- **HierarchyMarker.cs**: 하이어라키 마커
- **HierarchyRenamerInjector.cs**: 하이어라키 일괄 이름 변경
- **SceneToolbar.cs**: 씬 툴바
- **UIGuidesSettings.cs**: UI 가이드라인
- **FavoriteFoldersWindow.cs**: 즐겨찾기 폴더
- **ImageFolderEditor.cs**: 이미지 폴더 에디터
- **AnimationOffset.cs**: 애니메이션 오프셋 조정
- **AnimationParticleSimulator.cs**: 파티클 시뮬레이션
- **PrefabMenuInHierarchy.cs**: 하이어라키에서 프리팹 메뉴 추가

**커스텀 에디터:**
- **Editor/Effects/**: 이펙트 컴포넌트용 커스텀 인스펙터
- **Editor/UI/**: UI 컴포넌트용 커스텀 인스펙터

## 개발 가이드

### 네이밍 규칙

- **프라이빗 필드**: `_camelCase` (언더스코어 시작)
- **퍼블릭 필드/프로퍼티**: `PascalCase`
- **메서드**: `PascalCase`
- **로컬 변수**: `camelCase`

### 아키텍처 패턴

1. **ScriptableObject 기반 데이터 분리**: BezierPath 시스템처럼 데이터와 로직을 분리하여 재사용성 향상
2. **에디터/런타임 분리**: 에디터 전용 코드는 `#if UNITY_EDITOR` 블록으로 분리
3. **커스텀 에디터 제공**: 복잡한 컴포넌트는 반드시 커스텀 에디터 작성 (Gizmo 시각화, 인스펙터 개선)
4. **성능 최적화**: Transform 캐싱, Dictionary 사전 할당, 불필요한 연산 최소화

### DOTween 사용 시 주의사항

- DOTween은 `Assets/Plugins/Demigiant/DOTween/`에 DLL로 설치됨
- 네임스페이스: `DG.Tweening`
- 커스텀 이징 선택 도구 사용 가능: `Window > DOTween > Easing Selector`

### TextMeshPro 사용

- Unity 패키지로 설치됨
- UI 관련 TMP 유틸리티는 `Assets/Scripts/UI/UITMP*.cs` 참고

### 에디터 도구 개발

- 에디터 스크립트는 `Assets/Scripts/Editor/` 하위에 위치
- 카테고리별로 서브폴더 구분 (UI, Effects, Utility, Curve)
- EditorIcons.cs를 활용하여 일관된 아이콘 사용

### UI 개발

- URP 기반이므로 캔버스는 Screen Space - Camera 모드 권장
- Soft Mask, UI Effect 플러그인 적극 활용
- 베지어 경로 이동이 필요한 UI는 BezierFollower 사용

### Git 워크플로우

- **Main Branch**: `main`
- **Current Branch**: `develop`
- 커밋 메시지는 한국어로 작성
- 기능 개발 시 develop 브랜치에서 작업

## 자주 사용하는 명령어

### Unity 프로젝트 열기
Unity Hub에서 프로젝트 열기 또는 Unity Editor에서 직접 열기

### 빌드
Unity Editor: `File > Build Settings > Build`

### 테스트
Unity Test Runner: `Window > General > Test Runner`
