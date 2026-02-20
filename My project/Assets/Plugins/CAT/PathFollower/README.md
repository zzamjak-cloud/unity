# PathFollower

런타임에서 자유롭게 경로를 수정할 수 있는 베지어 곡선 경로 추종 컴포넌트.

> **BezierFollower와의 차이점**: ScriptableObject 없이 컴포넌트 자체에 경로 데이터를 저장하므로, 런타임에서 API를 통해 포인트를 자유롭게 추가·수정·삭제할 수 있습니다.

---

## 스크립트 구조

```
Assets/Plugins/CAT/PathFollower/
├── PathFollower.cs          # 메인 컴포넌트 (경로 데이터 + 이동 로직)
│   ├── PathPoint            # 경로 포인트 데이터 클래스 (동일 파일 내 정의)
│   ├── PathFollowerAgent    # 독립 타이밍 에이전트 클래스 (동일 파일 내 정의)
│   └── PathSnapshot         # 경로 스냅샷 클래스 (동일 파일 내 정의)
└── Editor/
    └── PathFollowerEditor.cs   # 커스텀 에디터
```

### PathPoint 클래스

| 필드 | 타입 | 설명 |
|------|------|------|
| `position` | Vector3 | 정점 위치 (부모 Transform 기준 로컬 좌표) |
| `handleIn` | Vector3 | 들어오는 핸들 (로컬 좌표) |
| `handleOut` | Vector3 | 나가는 핸들 (로컬 좌표) |
| `isBroken` | bool | 핸들 연결 끊기 여부 |

### PathFollower 컴포넌트

| 필드 | 타입 | 설명 |
|------|------|------|
| `duration` | float | 경로 전체 이동 시간 (초) |
| `movementCurve` | AnimationCurve | 이동 이징 커브 |
| `startOffset` | float (0~1) | 시작점 오프셋 |
| `followRotation` | bool | 이동 방향으로 오브젝트 회전 여부 |
| `loopType` | LoopType | 루프 방식 (None / Restart / Yoyo) |
| `progress` | float (0~1) | 현재 진행도 (읽기 전용) |
| `isPlaying` | bool | 재생 중 여부 |
| `morphingDuration` | float | 스냅샷 전환 시 모핑 시간 (초), 0 = 즉시 |

---

## 사용법 - Inspector

1. 빈 GameObject에 **PathFollower** 컴포넌트 추가
2. 인스펙터에서 **`+ 포인트 추가`** 버튼으로 포인트 추가
3. **SceneView**에서 정점을 드래그하여 경로 편집 (클릭과 동시에 즉각 이동)
   - **민트색 점**: Start Point (인덱스 0, 경로 시작점)
   - **흰 점**: 일반 정점
   - **노란 점**: 선택된 정점
   - 파란 점: `handleIn` (곡선 진입 방향)
   - 주황 점: `handleOut` (곡선 출발 방향)
4. 인스펙터 하단 **`▶ 10초 테스트`** 버튼으로 에디터 프리뷰

### Path Tools 섹션

| 버튼 | 설명 |
|------|------|
| 원형 생성 | 정원(正圓) 경로 생성 |
| 다각형 생성 | N변 다각형 경로 생성 (둥글기 0~1 조절 가능) |
| 별모양 생성 | 별모양 경로 생성 (외부/내부 반지름 설정) |
| 확대 (+) / 축소 (-) | 경로를 무게중심 기준으로 확대/축소 |
| 전체 Relax | 모든 정점 핸들을 Catmull-Rom 방식으로 자동 조정 |

### 선택 영역 변형 (스냅샷용)

원형/다각형/별 생성 시 **Start Point(첫 정점)**가 서로 다르면 스냅샷 모핑이 부자연스럽습니다.

1. **전체 선택** 버튼 (또는 Ctrl+A)으로 모든 정점 선택
2. **회전 적용**: 선택된 정점을 무게중심 기준으로 회전 (Start Point 정렬용)
3. **스케일 적용**: 선택된 정점을 무게중심 기준으로 스케일

정점을 선택한 상태에서 Inspector 하단의 "선택 영역 변형" 섹션에서 회전 각도·스케일 값을 입력 후 적용합니다.

### Snapshots 섹션

- **스냅샷 저장**: 현재 경로를 이름을 붙여 저장
- **적용**: 에디터에서 즉시 전환 (런타임에서는 `morphingDuration` 적용)
- **갱신**: 현재 경로로 해당 스냅샷 덮어쓰기 (이름 유지)
- **X**: 스냅샷 삭제

---

## 사용법 - 런타임 API

### 이동 제어

```csharp
PathFollower follower = GetComponent<PathFollower>();

follower.Play();            // 이동 시작/재개
follower.Pause();           // 일시 정지
follower.Stop();            // 정지 및 처음으로 복귀
follower.SetProgress(0.5f); // 진행도 직접 지정 (0~1)
```

### 이벤트

```csharp
// 경로 완료 이벤트 (LoopType.None 일 때)
follower.OnComplete += () => Debug.Log("경로 완료!");

// 루프 이벤트 (한 바퀴 완료 시)
follower.OnLoop += () => Debug.Log("루프!");
```

### 경로 프리셋 생성

```csharp
// 원형 (반지름 5, 4정점)
follower.SetCircle(5f, 4);

// 6변 다각형 (반지름 5, 회전 0도, 모서리 둥글기 0.5)
follower.SetPolygon(6, 5f, 0f, 0.5f);

// 별모양 (5꼭짓점, 외부반지름 5, 내부반지름 2, 회전 0도)
follower.SetStar(5, 5f, 2f, 0f);
```

### 경로 확대/축소 및 핸들 자동 조정

```csharp
follower.ExpandPath(1f);    // 무게중심 기준 1 단위 확대
follower.ExpandPath(-1f);   // 무게중심 기준 1 단위 축소
follower.RelaxPath();        // 모든 정점 핸들 Catmull-Rom 자동 조정
follower.RelaxPoint(2);      // 2번 정점 핸들만 자동 조정
```

### 경로 회전/스케일 (스냅샷용)

```csharp
// 전체 정점 90도 반시계 방향 회전
follower.RotatePath(90f);

// 선택된 정점만 회전 (indices 지정)
follower.RotatePath(45f, new[] { 0, 1, 2, 3 });

// 전체 정점 1.5배 스케일
follower.ScalePath(1.5f);

// 선택된 정점만 스케일
follower.ScalePath(0.8f, new[] { 0, 1, 2 });
```

### 에이전트 (독립 타이밍 이동)

```csharp
// 여러 오브젝트를 시간차로 등록하면 각자 독립 타이밍으로 경로를 따라 이동
follower.AddAgent(transform1); // 지금부터 시작
// 2초 후
follower.AddAgent(transform2); // 2초 늦게 시작

// 에이전트 제거
follower.RemoveAgent(transform1);
follower.ClearAgents();

// 특정 에이전트의 진행도 조회
float progress = follower.GetAgentProgress(transform1); // 0~1, 미등록 시 -1
```

### 스냅샷 & 모핑

```csharp
// 현재 경로를 스냅샷으로 저장
follower.SaveAsSnapshot("삼각형");
follower.SetPolygon(4, 5f);
follower.SaveAsSnapshot("사각형");
follower.SetCircle(5f);
follower.SaveAsSnapshot("원");

// 인덱스로 전환 (morphingDuration 값 사용)
follower.morphingDuration = 1f;
follower.SwitchToSnapshot(0); // "삼각형"으로 1초간 모핑

// duration 직접 지정
follower.SwitchToSnapshot(2, 0.5f); // "원"으로 0.5초간 모핑
follower.SwitchToSnapshot(1, 0f);   // "사각형"으로 즉시 전환

// 스냅샷 정보 조회
int count = follower.SnapshotCount;
int current = follower.CurrentSnapshotIndex;
PathSnapshot snap = follower.GetSnapshot(0);

// 현재 경로로 기존 스냅샷 덮어쓰기 (이름 유지)
follower.OverwriteSnapshot(1); // 1번 스냅샷을 현재 경로로 갱신

// 스냅샷 삭제
follower.RemoveSnapshot(2);
```

> **참고**: 포인트 수가 다른 스냅샷으로 전환 시 모핑 없이 즉시 전환됩니다.

### 포인트 추가/수정

```csharp
// 경로 끝에 포인트 추가 (월드 좌표)
follower.AddPoint(new Vector3(10f, 0f, 0f));

// 특정 인덱스에 삽입
follower.InsertPoint(1, new Vector3(5f, 3f, 0f));

// 특정 포인트 위치 변경 (월드 좌표, 핸들 오프셋 유지)
follower.SetPointPosition(0, new Vector3(0f, 2f, 0f));

// 포인트 전체 데이터 교체 (핸들 포함)
PathPoint p = follower.GetPoint(0);
p.position = new Vector3(0f, 2f, 0f);  // 로컬 좌표
p.handleOut = new Vector3(2f, 2f, 0f); // 로컬 좌표
follower.SetPoint(0, p);
```

### 포인트 조회/삭제

```csharp
// 포인트 개수
int count = follower.PointCount;

// 특정 포인트 월드 좌표 조회
Vector3 worldPos = follower.GetPointWorldPosition(0);

// 포인트 제거
follower.RemovePoint(2);

// 전체 초기화
follower.ClearPoints();
```

### 경로 위 좌표 직접 계산

```csharp
// t(0~1)에 해당하는 월드 좌표
Vector3 pos = follower.GetPointAt(0.3f);

// t(0~1)에 해당하는 이동 방향
Vector3 dir = follower.GetDirectionAt(0.3f);
```

---

## 에디터 단축키 (SceneView)

| 단축키 | 기능 |
|--------|------|
| 클릭 (정점) | 정점 선택 |
| 클릭+드래그 (정점) | 선택과 동시에 즉각 이동 |
| Shift + 클릭 (정점) | 선택에 추가 |
| Alt + 클릭 (곡선 위) | 그 위치에 정점 삽입 |
| Ctrl + 드래그 | 박스 선택 (비어있는 공간에서 드래그) |
| Ctrl + Shift + 드래그 | 박스 선택 → 기존 선택에 추가 |
| Ctrl + A | 전체 정점 선택 |
| 우클릭 (정점) | 컨텍스트 메뉴 (삽입 / 삭제 / 핸들 연결·끊기 / 핸들 초기화) |
| R (정점 선택 후) | 핸들 회전 모드 토글 |
| Delete / Backspace | 선택 정점 삭제 |
| Escape | 선택 해제 / 회전 모드 해제 |

### Canvas 자식 (UI 모드)

PathFollower가 Canvas 자식 오브젝트에 있으면 **UI 모드**가 자동 활성화됩니다.
- 모든 포인트의 로컬 Z값이 0으로 고정됩니다.
- 이동 핸들이 XY 평면 슬라이더(사각형 핸들)로 표시됩니다.
- 핸들 회전 시에도 Z=0이 유지됩니다.

---

## 좌표계 규칙

- **저장**: 모든 PathPoint는 **부모 Transform 기준 로컬 좌표**로 저장됨 (부모 없으면 월드 좌표와 동일)
- **API 입력**: `AddPoint`, `InsertPoint`, `SetPointPosition`은 **월드 좌표** 입력
- **API 출력**: `GetPoint`는 **부모 기준 로컬 좌표** 데이터 반환, `GetPointWorldPosition`은 **월드 좌표** 반환
- **경로 고정**: 오브젝트가 경로를 따라 이동해도 경로 자체는 고정됨
- **부모 이동**: 부모 GameObject를 이동하면 경로 전체가 함께 이동함
- **좌표 변환**: `PathToWorld()` / `WorldToPath()` API로 변환 가능

---

## BezierFollower와 비교

| 기능 | BezierFollower | PathFollower |
|------|---------------|-------------|
| 경로 저장 방식 | ScriptableObject | MonoBehaviour 직렬화 |
| 런타임 경로 수정 | 불편 (SO 직접 편집) | 공개 API 제공 |
| 경로 공유 | 여러 오브젝트가 공유 가능 | 단일 컴포넌트 전용 |
| 에디터 편집 | BezierPath 컴포넌트 별도 필요 | PathFollower 하나로 완결 |
| UI/Canvas 모드 | 지원 | Canvas 자식 시 자동 감지 지원 |
| 경로 프리셋 | 없음 | 원형/다각형/별모양 |
| 에이전트 | 없음 | 독립 타이밍 다중 에이전트 |
| 스냅샷/모핑 | 없음 | 다단계 경로 + 모핑 전환 |

---

## 모바일 성능

PathFollower는 모바일 게임 환경을 고려해 다음처럼 구성되어 있습니다.

### 적용된 최적화

| 항목 | 내용 |
|------|------|
| **Transform 캐싱** | `transform.parent`를 `_cachedParent`에 캐시하여 PathToWorld/WorldToPath 반복 호출 시 접근 최소화 |
| **월드 좌표 캐시** | 경로 포인트 월드 좌표를 배열로 캐시, 포인트/부모 변경 시에만 갱신 |
| **Update 경로** | Update 내부에서 `new`/LINQ/문자열 연결 없음, 구조체(Vector3)만 사용 |
| **에이전트 이동** | `GetPointAt`는 캐시 기반 계산만 수행, 프레임당 추가 할당 없음 |

### 주의 사항 (GC 방지)

- **`GetPoint(int)`**: 호출마다 `PathPoint` 복사본을 생성합니다. **Update 등 반복 경로에서는 사용하지 마세요.**
- **위치만 필요할 때**: `GetPointWorldPosition(int)` 또는 `GetPointPositionLocal(int)` 사용 (할당 없음).
- **스냅샷 전환/모핑**: `SwitchToSnapshot`, `SaveAsSnapshot` 호출 시에만 List/Clone 할당 발생 (이벤트성 호출이면 부담 적음).

### 권장 사용

- 경로 위 임의 위치: `GetPointAt(t)`, `GetDirectionAt(t)` — 캐시 활용, 할당 없음.
- 특정 정점 월드/로컬 위치: `GetPointWorldPosition(index)`, `GetPointPositionLocal(index)`.
- 전체 포인트 데이터가 꼭 필요할 때만 `GetPoint(index)` 사용.

---

## 버전 히스토리

### v1.2.1 (2026-02-20)
- **모바일 성능**: `transform.parent` 캐싱(`_cachedParent`), PathToWorld/WorldToPath·RefreshCacheIfNeeded에서 반복 접근 제거
- **API**: `GetPointPositionLocal(int)` 추가 (할당 없이 로컬 좌표 조회), `GetPoint` XML에 할당 주의 문서화
- **문서**: README에 "모바일 성능" 섹션 추가

### v1.2.0 (2026-02-20)
- **Start Point 구분**: 첫 번째 정점(인덱스 0)을 민트색으로 표시하여 직관적 구분
- **선택 영역 변형**: 전체 선택 후 회전·스케일로 Start Point 정렬 (스냅샷 모핑용)
- `RotatePath(angleDegrees, indices)`, `ScalePath(scale, indices)` API 추가
- Inspector: "전체 선택" 버튼, 선택 시 "선택 영역 변형" 섹션 (회전/스케일 적용)
- 단축키: Ctrl+A 전체 선택

### v1.1.0 (2026-02-20)
- Inspector 헤더 중복 표시 수정 ([Header] 속성 제거)
- SceneView: 정점 클릭과 동시에 드래그 가능 (FreeMoveHandle 방식)
- 프리셋 강화: `SetPolygon()` - 다각형 + 모서리 둥글기, `SetStar()` - 별모양
- Relax 알고리즘 개선: 각 방향 독립 핸들 길이로 원형 왜곡 방지
- `PathFollowerAgent` 시스템: 독립 타이밍으로 다수 오브젝트 이동
- `PathSnapshot` + 모핑: 스냅샷 저장/전환, 동일 정점 수일 때 모핑 보간

### v1.0.0 (2026-02-19)
- 최초 릴리즈
- ScriptableObject 없이 컴포넌트 자체에 경로 데이터 저장
- 런타임 API: `AddPoint`, `InsertPoint`, `RemovePoint`, `SetPointPosition`, `SetPoints`, `GetPoint`, `ClearPoints`
- 이동 제어 API: `Play`, `Pause`, `Stop`, `SetProgress`
- 이벤트: `OnComplete`, `OnLoop`
- 경로 도구: `SetCircle`, `ExpandPath`, `RelaxPath`, `RelaxPoint`
- 커스텀 에디터: SceneView 포인트 편집, 핸들 편집, 다중 선택, 에디터 테스트 재생
- 최적화: 배열 기반 월드 좌표 캐시, 부모 Transform 행렬 변경 감지
- LoopType: None / Restart / Yoyo
