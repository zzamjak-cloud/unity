# PathFollower

런타임에서 자유롭게 경로를 수정할 수 있는 베지어 곡선 경로 추종 컴포넌트.

> **BezierFollower와의 차이점**: ScriptableObject 없이 컴포넌트 자체에 경로 데이터를 저장하므로, 런타임에서 API를 통해 포인트를 자유롭게 추가·수정·삭제할 수 있습니다.

---

## 스크립트 구조

```
Assets/Plugins/CAT/PathFollower/
├── PathFollower.cs          # 메인 컴포넌트 (경로 데이터 + 이동 로직)
│   └── PathPoint            # 경로 포인트 데이터 클래스 (동일 파일 내 정의)
└── Editor/
    └── PathFollowerEditor.cs   # 커스텀 에디터
```

### PathPoint 클래스

| 필드 | 타입 | 설명 |
|------|------|------|
| `position` | Vector3 | 정점 위치 (컴포넌트 로컬 좌표) |
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

---

## 사용법 - Inspector

1. 빈 GameObject에 **PathFollower** 컴포넌트 추가
2. 인스펙터에서 **`+ 포인트 추가`** 버튼으로 포인트 추가
3. **SceneView**에서 흰 점(정점)을 드래그하여 경로 편집
   - 파란 점: `handleIn` (곡선 진입 방향)
   - 주황 점: `handleOut` (곡선 출발 방향)
4. 인스펙터 하단 **`▶ 10초 테스트`** 버튼으로 에디터 프리뷰

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

### 경로 전체 교체 (예: 동적 생성)

```csharp
var points = new List<PathPoint>();
for (int i = 0; i < 5; i++)
{
    // PathPoint는 로컬 좌표 기준
    float x = i * 3f;
    points.Add(new PathPoint(new Vector3(x, Mathf.Sin(x) * 2f, 0f)));
}
follower.SetPoints(points);
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
| Shift + 클릭 (정점) | 선택에 추가 |
| Alt + 클릭 (곡선 위) | 그 위치에 정점 삽입 |
| Ctrl + 드래그 | 박스 선택 (비어있는 공간에서 드래그) |
| Ctrl + Shift + 드래그 | 박스 선택 → 기존 선택에 추가 |
| 우클릭 (정점) | 컨텍스트 메뉴 (삽입 / 삭제 / 핸들 연결·끊기) |
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

---

## 버전 히스토리

### v1.0.0 (2026-02-19)
- 최초 릴리즈
- ScriptableObject 없이 컴포넌트 자체에 경로 데이터 저장
- 런타임 API: `AddPoint`, `InsertPoint`, `RemovePoint`, `SetPointPosition`, `SetPoints`, `GetPoint`, `ClearPoints`
- 이동 제어 API: `Play`, `Pause`, `Stop`, `SetProgress`
- 이벤트: `OnComplete`, `OnLoop`
- 커스텀 에디터: SceneView 포인트 편집, 핸들 편집, 다중 선택, 에디터 테스트 재생
- 최적화: 배열 기반 월드 좌표 캐시, 부모 Transform 행렬 변경 감지
- LoopType: None / Restart / Yoyo
