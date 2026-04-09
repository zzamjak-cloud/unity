# CAT UIMaker

프리팹의 구조/컴포넌트/프로퍼티를 JSON으로 추출하고, JSON에서 UI를 자동 생성하는 에디터 도구.

## 주요 기능

- **프리팹 → JSON 추출**: 프리팹의 계층 구조, 컴포넌트, 프로퍼티를 JSON으로 저장
- **JSON → UI 생성**: JSON 파일을 기반으로 하이어라키에 UI 오브젝트 자동 생성
- **UI 프리뷰**: JSON 파일을 씬에 배치하기 전에 720x1280 (9:16) 비율로 미리보기
- **하이어라키 통합**: 하이어라키 창 상단의 "UI" 드롭다운으로 빠른 접근
- **프리팹 레지스트리**: UICom 프리팹을 등록/교체하고, GUID 유실 시 자동 폴백

## 디렉토리 구조

### 패키지 (업데이트 대상)

```
Assets/Plugins/CAT/UIMaker/
├── Editor/                  # 에디터 스크립트
│   ├── Data/
│   │   ├── PrefabJsonData.cs              # JSON 데이터 모델
│   │   └── UIMakerPrefabRegistry.cs       # 프리팹 레지스트리 (ScriptableObject)
│   ├── Utility/
│   │   └── SerializedPropertyExtractor.cs # 프로퍼티 추출 유틸리티
│   ├── ExtractPrefabInfoWithJSON.cs       # 프리팹 → JSON 추출 윈도우
│   ├── HierarchyJsonPrefabModule.cs       # 하이어라키 UI 드롭다운
│   ├── UIDesignMaker.cs                   # JSON → UI 오브젝트 생성
│   ├── UIPreviewWindow.cs                 # UI 프리뷰 윈도우
│   └── UIMakerPrefabRegistryEditor.cs     # 레지스트리 커스텀 Inspector
├── ArtResource/             # 아트 리소스 (폰트, 텍스처 등)
├── JSON/                    # 생성된 JSON 파일 저장
│   ├── _Panel/
│   ├── _Popup/
│   ├── Button/
│   └── Frame/
└── Prefabs/                 # UI 컴포넌트 원본 프리팹
    └── UICom/
```

### 사용자 데이터 (패키지 업데이트 영향 없음)

```
Assets/Prefabs/UI/
├── UIMakerConfig/                         # 설정 파일 (패키지 외부)
│   ├── UIMakerPrefabRegistry.asset        # 프리팹 레지스트리 에셋
│   └── Snapshots/                         # 프리팹 JSON 스냅샷 (폴백용)
└── UICom/                                 # 복제된 프리팹 (사용자 작업용)
    ├── PopupButton_new.prefab
    ├── Dimmer_new.prefab
    └── ...
```

> **패키지 업데이트 안전성**: 레지스트리, 스냅샷, 복제된 프리팹은 모두 패키지 외부에 저장된다.
> 구 버전에서 패키지 내부에 Config가 있었던 경우, 최초 접근 시 자동으로 마이그레이션된다.

## 사용법

### 프리팹 → JSON 추출
1. `CAT > Utility > Extract Prefab Info` 메뉴로 윈도우 열기
2. 프리팹을 ObjectField에 드래그 또는 선택
3. 저장 폴더 확인 후 "JSON 추출" 클릭

### JSON → UI 생성
1. 하이어라키 창 상단의 **UI** 버튼 클릭
2. 드롭다운에서 원하는 JSON 프리팹 선택
3. 선택한 오브젝트 하위에 UI가 자동 생성됨

### UI 프리뷰
1. `CAT > UI > UI Preview Window` 메뉴 또는 단축키 `Ctrl+Shift+U`
2. 왼쪽 패널에서 카테고리별 JSON 파일 탐색 (검색 가능)
3. JSON 항목 클릭 시 오른쪽 패널에 720x1280 프리뷰 표시
4. 하단 바에서 배경색 변경 가능
5. 프리뷰 하단의 **이름 라벨 클릭** 시 해당 JSON을 씬에 개별 생성

**복수 선택:**
- **체크박스**: 개별 항목 토글 (카테고리 간 유지)
- **클릭**: 단일 선택 (기존 선택 해제)
- **Ctrl+클릭**: 토글 추가/제거
- **Shift+클릭**: 범위 선택

### 프리팹 레지스트리
1. `CAT > UI > Prefab Registry` 메뉴로 레지스트리 에셋 선택 (없으면 자동 생성)
2. Inspector에서 **UICom 스캔** 버튼 클릭:
   - UICom 폴더의 프리팹을 자동 등록
   - `Assets/Prefabs/UI/UICom/`에 `{이름}_new.prefab`으로 자동 복제
   - 복제된 프리팹을 Override로 자동 등록
   - 이미 존재하는 복제본은 건너뜀 (사용자 수정 보존)
3. **스냅샷 생성** 버튼으로 모든 프리팹의 JSON 백업 생성
4. 개별 프리팹의 **교체** 필드에 커스텀 프리팹을 드래그하여 수동 교체 가능

#### 프리팹 해결 순서

JSON에서 UI 생성 시, 프리팹은 다음 순서로 검색된다:

1. **레지스트리 Override** — 사용자가 교체한 프리팹 (최우선)
2. **GUID 직접 조회** — 기존 동작
3. **레지스트리 이름 조회** — GUID가 변경된 경우 이름으로 매칭
4. **JSON 스냅샷 복원** — 프리팹이 완전히 삭제된 경우 스냅샷에서 구조 복원
5. **빈 오브젝트** — 최종 폴백

#### 패키지 업데이트 시

- 레지스트리/스냅샷/복제본은 패키지 외부에 저장되어 업데이트 영향 없음
- UICom 스캔 시 기존 Override는 유지, 신규 프리팹만 추가 복제
- 구 버전(패키지 내부 Config)에서 자동 마이그레이션 지원

---

## 변경 로그

### 2026-04-02
- **추가**: 프리팹 레지스트리 시스템
  - ScriptableObject 기반 프리팹 등록/교체/스냅샷 관리
  - 5단계 프리팹 해결 체인 (Override 최우선)
  - 커스텀 Inspector로 프리팹 교체 UI 제공
  - UICom 스캔 시 `Assets/Prefabs/UI/UICom/`에 자동 복제 + Override 등록
- **추가**: UI 프리뷰 복수 선택
  - 체크박스/Ctrl/Shift 조합으로 여러 JSON 동시 프리뷰
  - 그리드 배치 (최대 5열) + 자동 카메라 조정
  - 이름 라벨 클릭으로 개별 씬 생성 (VFX 모듈과 동일한 UX)
  - 이름 라벨 배경 프레임 추가 (가독성 향상)
- **개선**: 레지스트리/스냅샷을 패키지 외부로 이동
  - 구 경로(패키지 내부) → 신 경로(`Assets/Prefabs/UI/UIMakerConfig/`) 자동 마이그레이션
  - 패키지 업데이트 시 사용자 데이터 유실 방지
- **수정**: Inspector 스크롤 성능 문제
  - `AssetDatabase.FindAssets()` 경로 캐싱
  - `File.Exists()` 스냅샷 상태 캐싱

### 2026-04-01
- **추가**: UI 프리뷰 윈도우
  - 720x1280 (9:16) 비율 프리뷰 렌더링
  - 카테고리별 JSON 탐색 + 검색 기능
  - PreviewRenderUtility 기반 WorldSpace Canvas 렌더링
  - TMP_Text 리플렉션 기반 메시 강제 갱신
  - 단축키: `Ctrl+Shift+U`

### 2026-03-25
- **수정**: JSON 파일 생성 후 프로젝트 뷰에서 즉시 갱신되지 않던 버그 수정
  - 원인: `Path.Combine` (백슬래시)과 `Application.dataPath` (슬래시) 간 경로 포맷 불일치로 `AssetDatabase.Refresh()`가 호출되지 않음
  - 해결: `Path.GetFullPath`로 경로 정규화 후 비교, `AssetDatabase.ImportAsset`으로 해당 파일만 즉시 갱신
