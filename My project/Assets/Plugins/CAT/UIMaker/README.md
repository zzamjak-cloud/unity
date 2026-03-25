# CAT UIMaker

프리팹의 구조/컴포넌트/프로퍼티를 JSON으로 추출하고, JSON에서 UI를 자동 생성하는 에디터 도구.

## 주요 기능

- **프리팹 → JSON 추출**: 프리팹의 계층 구조, 컴포넌트, 프로퍼티를 JSON으로 저장
- **JSON → UI 생성**: JSON 파일을 기반으로 하이어라키에 UI 오브젝트 자동 생성
- **하이어라키 통합**: 하이어라키 창 상단의 "UI" 드롭다운으로 빠른 접근

## 디렉토리 구조

```
UIMaker/
├── Editor/                  # 에디터 스크립트
│   ├── Data/
│   │   └── PrefabJsonData.cs              # JSON 데이터 모델
│   ├── Utility/
│   │   └── SerializedPropertyExtractor.cs # 프로퍼티 추출 유틸리티
│   ├── ExtractPrefabInfoWithJSON.cs       # 프리팹 → JSON 추출 윈도우
│   ├── HierarchyJsonPrefabModule.cs       # 하이어라키 UI 드롭다운
│   └── UIDesignMaker.cs                   # JSON → UI 오브젝트 생성
├── ArtResource/             # 아트 리소스 (폰트, 텍스처 등)
├── JSON/                    # 생성된 JSON 파일 저장
│   ├── _Panel/
│   ├── _Popup/
│   ├── Button/
│   └── Frame/
└── Prefabs/                 # UI 컴포넌트 프리팹
    └── UICom/
```

## 사용법

### 프리팹 → JSON 추출
1. `CAT > Utility > Extract Prefab Info` 메뉴로 윈도우 열기
2. 프리팹을 ObjectField에 드래그 또는 선택
3. 저장 폴더 확인 후 "JSON 추출" 클릭

### JSON → UI 생성
1. 하이어라키 창 상단의 **UI** 버튼 클릭
2. 드롭다운에서 원하는 JSON 프리팹 선택
3. 선택한 오브젝트 하위에 UI가 자동 생성됨

---

## 변경 로그

### 2026-03-25
- **수정**: JSON 파일 생성 후 프로젝트 뷰에서 즉시 갱신되지 않던 버그 수정
  - 원인: `Path.Combine` (백슬래시)과 `Application.dataPath` (슬래시) 간 경로 포맷 불일치로 `AssetDatabase.Refresh()`가 호출되지 않음
  - 해결: `Path.GetFullPath`로 경로 정규화 후 비교, `AssetDatabase.ImportAsset`으로 해당 파일만 즉시 갱신
