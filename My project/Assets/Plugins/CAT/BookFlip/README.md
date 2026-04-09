# BookFlip - 고도화된 책넘기기 시스템

Unity UI용 고급 페이지 넘김 효과 패키지입니다. 기존 "Book-Page Curl" 패키지를 개선하여 다양한 페이지 타입, 런타임 페이지 관리, 유연한 에셋 로드 방식을 지원합니다.

## 주요 기능

### 1. 다양한 페이지 타입 지원
- **Sprite**: 이미지 스프라이트를 페이지로 사용
- **Prefab**: 프로젝트 프리팹 또는 씬 오브젝트를 인스턴스화하여 페이지로 사용
- **GameObject**: 씬에 이미 존재하는 GameObject를 복제하여 페이지로 사용

### 2. 유연한 소스 로드 방식 (SourceMode)
- **Direct**: Inspector에서 직접 참조 (기본)
- **ResourcesPath**: `Resources.Load`로 경로 기반 동기 로드
- **CustomAsync**: 외부 비동기 로더 사용 (Addressables 등)

### 3. 런타임 페이지 관리
- `SetPages()`: 페이지 목록 전체 교체
- `AddPage()` / `InsertPage()` / `RemovePage()`: 개별 페이지 추가/삽입/삭제
- `RefreshPage()` / `RefreshAllPages()`: 소스 변경 후 즉시 갱신
- `PersistInstance` 모드: 인스턴스를 파괴하지 않고 비활성화 상태로 유지하여 재사용

### 4. UI 인터랙션 자동 제어
- 페이지 넘기는 중: 모든 UI 요소 인터랙션 비활성화 (`CanvasGroup.interactable = false`, `blocksRaycasts = false`)
- 페이지 넘김 완료 후: 현재 표시 중인 페이지만 인터랙션 활성화 (`interactable = true`, `blocksRaycasts = true`)

### 5. 모바일 최적화
- Transform 및 Component 캐싱
- AnimationCurve + `Time.deltaTime` 기반 프레임레이트 독립 애니메이션 (DOTween 의존성 제거)
- 메모리 할당 최소화
- 슬롯 RT 상태 저장/복원으로 깜빡임 방지
- 불필요한 `GetComponent<T>()` 호출 제거

## 구성 요소

### 핵심 스크립트

#### BookFlip.cs

메인 컨트롤러 컴포넌트입니다.

**주요 메서드:**
```csharp
// 페이지 이동
public void NextPage()
public void PreviousPage()
public void GoToPage(int pageIndex)

// 수동 드래그 (EventTrigger와 함께 사용)
public void OnMouseDragRightPage()
public void OnMouseDragLeftPage()
public void OnMouseRelease()

// 런타임 페이지 관리
public void SetPages(BookFlipPage[] pages)
public void AddPage(BookFlipPage page)
public void InsertPage(int index, BookFlipPage page)
public void RemovePage(int index)
public void RefreshPage(int pageIndex)
public void RefreshAllPages()
```

**주요 프로퍼티:**
```csharp
public int CurrentPage { get; set; }
public int TotalPageCount { get; }
public bool Interactable { get; set; }
```

**이벤트:**
```csharp
public UnityEvent OnFlip;             // 페이지 넘김 완료 시
public UnityEvent<int> OnPageChanged; // 페이지 변경 시
public UnityEvent OnFlipStart;        // 넘김 시작 시
public UnityEvent OnFlipEnd;          // 넘김 완료 시
```

#### BookFlipPage.cs

페이지 데이터를 추상화하는 직렬화 클래스입니다.

```csharp
[System.Serializable]
public class BookFlipPage
{
    public enum PageType   { Sprite, Prefab, GameObject }
    public enum SourceMode { Direct, ResourcesPath, CustomAsync }
}
```

**Addressables 비동기 로더 연동 예시:**
```csharp
BookFlipPage.AsyncLoader = (key, type, onDone) =>
{
    if (type == typeof(Sprite))
        Addressables.LoadAssetAsync<Sprite>(key).Completed += op => onDone(op.Result);
    else
        Addressables.LoadAssetAsync<GameObject>(key).Completed += op => onDone(op.Result);
};
```

#### BookFlipAutoFlip.cs

자동 페이지 넘김 기능을 제공합니다.

**주요 메서드:**
```csharp
public void StartFlipping()
public void StopFlipping()
public void FlipRightPage()
public void FlipLeftPage()
public void FlipToPage(int targetPage)
```

## 사용 방법

### 1. 기본 설정

1. **Canvas 생성** — Hierarchy에서 우클릭 > UI > Canvas
2. **BookFlip GameObject 생성** — Canvas 하위에 빈 GameObject 생성, `BookFlip` 컴포넌트 추가
3. **UI 요소 설정** — BookFlip 컴포넌트에 필요한 UI Image들을 생성하고 연결:
   - **BookPanel**: 책 전체 영역을 담는 RectTransform
   - **ClippingPlane**: 넘김 효과용 클리핑 이미지
   - **NextPageClip**: 다음 페이지 클리핑 이미지
   - **Shadow, ShadowLTR**: 그림자 효과 이미지
   - **Left, LeftNext**: 왼쪽 페이지 이미지들
   - **Right, RightNext**: 오른쪽 페이지 이미지들

   > 기존 "Book-Page Curl" 패키지의 프리팹 구조를 참고하여 UI 요소를 배치하세요.

4. **컨테이너 기반 레이어 설정 (UI 인터랙션 페이지 사용 시 권장)**

   UI 버튼 등이 포함된 Prefab 페이지를 사용하는 경우, 페이지 요소와 핫스팟을 분리된 레이어로 격리하는 것이 가장 안정적입니다.

   **권장: PageContainer + HotSpotContainer 사용**

   ```
   BookPanel (RectTransform)
   +-- PageContainer (RectTransform)              <- 모든 페이지 요소 격리
   |   +-- ClippingPlane (Image)
   |   +-- NextPageClip (Image)
   |   +-- Shadow / ShadowLTR (Image)
   |   +-- Left / LeftNext / Right / RightNext (Image)
   +-- HotSpotContainer (RectTransform)           <- 핫스팟 완전 격리 (항상 최상위)
       +-- LeftHotSpot (BookFlipHotSpot + Image)
       +-- RightHotSpot (BookFlipHotSpot + Image)
   ```

   - BookFlip.Start()에서 모든 페이지 요소를 자동으로 PageContainer 하위로 이동
   - Update()에서 PageContainer(index 0) < HotSpotContainer(last) 순서 강제
   - UI 버튼 클릭, 페이지 넘김 등 어떤 상황에서도 레이어 순서 보장

### 2. 페이지 추가

Inspector에서 "페이지 목록"의 `+` 버튼을 클릭하여 페이지를 추가합니다.

#### Sprite 타입
- Type: "Sprite" 선택
- Sprite 필드에 이미지를 드래그 & 드롭

#### Prefab 타입
- Type: "Prefab" 선택
- Prefab 필드에 **프로젝트 프리팹** 또는 **씬 오브젝트**를 드래그 & 드롭
- `Object.Instantiate`로 복제하여 사용

**Prefab 요구사항:**
- 반드시 `RectTransform` 컴포넌트 포함
- UI 요소(Button, Slider 등) 포함 가능
- 프리팹 루트에 `CanvasGroup`이 없으면 자동으로 추가됨

#### GameObject 타입
- Type: "GameObject" 선택
- Hierarchy의 GameObject를 드래그 & 드롭
- Prefab 타입과 동작 동일 (소스 필드만 다름)

#### SourceMode 설정
- **Direct (기본)**: Inspector에서 직접 참조
- **ResourcesPath**: Resources 폴더 기준 경로 입력 (예: `Pages/Page01`)
- **CustomAsync**: Addressable 키 입력 (`BookFlipPage.AsyncLoader` 사전 설정 필요)

#### PersistInstance 옵션 (Prefab/GameObject 타입)
- **비활성화 (기본)**: 페이지 표시 시마다 새로 생성, 숨겨질 때 파괴
- **활성화**: 한 번 생성된 인스턴스를 비활성화 상태로 보존하여 재사용 (메모리 상주, `RuntimeInstance`로 외부 접근 가능)

### 3. 자동 넘김 설정 (선택사항)

BookFlip GameObject에 `BookFlipAutoFlip` 컴포넌트를 추가합니다.

**Inspector 설정:**
- Mode: RightToLeft / LeftToRight
- Page Flip Time: 넘김 애니메이션 시간 (초)
- Time Between Pages: 페이지 간 대기 시간 (초)
- Delay Before Starting: 시작 전 대기 시간 (초)
- Auto Start Flip: 자동 시작 여부
- Flip Curve: 넘김 애니메이션 커브

### 4. 스크립트에서 제어

```csharp
using CAT.BookFlip;

public class BookController : MonoBehaviour
{
    [SerializeField] private BookFlip _bookFlip;

    void Start()
    {
        _bookFlip.OnPageChanged.AddListener(OnPageChanged);
        _bookFlip.OnFlipStart.AddListener(() => Debug.Log("넘김 시작"));
        _bookFlip.OnFlipEnd.AddListener(() => Debug.Log("넘김 완료"));
    }

    public void NextPage()     => _bookFlip.NextPage();
    public void PreviousPage() => _bookFlip.PreviousPage();
    public void GoToPage(int i) => _bookFlip.GoToPage(i);

    private void OnPageChanged(int page) => Debug.Log($"페이지 변경: {page}");
}
```

**런타임 페이지 교체 예시:**
```csharp
// 페이지 목록 전체 교체
var pages = new BookFlipPage[] { page1, page2, page3 };
_bookFlip.SetPages(pages);

// 개별 페이지 추가/삭제
_bookFlip.AddPage(newPage);
_bookFlip.RemovePage(2);

// 소스 변경 후 갱신
// (예: 프리팹의 내부 데이터를 런타임에 변경한 경우)
_bookFlip.RefreshPage(0);
_bookFlip.RefreshAllPages();
```

## UI 인터랙션 페이지 만들기

Prefab 타입 페이지를 사용하여 버튼, 슬라이더 등 상호작용 가능한 UI를 포함할 수 있습니다.

### 프리팹 구조 예시

```
PagePrefab (RectTransform)
+-- Background (Image)
+-- Title (TextMeshPro)
+-- ConfirmButton (Button)
|   +-- Text (TextMeshPro)
+-- CancelButton (Button)
|   +-- Text (TextMeshPro)
+-- ContentSlider (Slider)
```

### 인터랙션 자동 제어 동작
- **넘기는 중**: `CanvasGroup.interactable = false`, `blocksRaycasts = false` -> HotSpot 드래그 감지 가능
- **넘김 완료**: `CanvasGroup.interactable = true`, `blocksRaycasts = true` -> 자식 Button 등 클릭 가능

## 모바일 최적화 팁

1. **페이지 수 제한** — 권장 50페이지 이하
2. **Prefab 최적화** — 불필요한 Layout Group 제거, Raycast Target 최소화
3. **Shadow 효과 선택적 사용** — 저사양 기기에서는 Inspector > Enable Shadow Effect 해제
4. **PersistInstance 활용** — 자주 전환되는 페이지는 PersistInstance 활성화로 Instantiate/Destroy 비용 절감

## 문제 해결

### 페이지가 표시되지 않음
- Canvas의 Render Mode 확인
- BookPanel의 크기 확인
- 페이지 타입별 참조가 올바른지 확인

### UI 인터랙션이 작동하지 않음
- CanvasGroup이 자동으로 추가되는지 확인
- OnFlipEnd 이벤트가 정상적으로 호출되는지 확인
- 개별 UI 요소의 Raycast Target 설정 확인

### 페이지 넘김 핫스팟이 작동하지 않음
- **PageContainer + HotSpotContainer** 사용 권장 (레이어 순서 충돌 원천 차단)
- `BookFlipHotSpot` 컴포넌트 사용 확인
- 핫스팟의 `Image` 컴포넌트 Raycast Target 활성화 확인

### 성능 이슈
- Shadow 효과 비활성화
- Prefab 최적화 (Layout Group, Raycast Target 최소화)
- PersistInstance 모드 활용
- 페이지 수 줄이기

## API 레퍼런스

### BookFlip

| 메서드 | 설명 |
|--------|------|
| `NextPage()` | 다음 페이지로 이동 |
| `PreviousPage()` | 이전 페이지로 이동 |
| `GoToPage(int)` | 특정 페이지로 이동 |
| `SetPages(BookFlipPage[])` | 페이지 목록 전체 교체 |
| `AddPage(BookFlipPage)` | 페이지 추가 |
| `InsertPage(int, BookFlipPage)` | 특정 위치에 페이지 삽입 |
| `RemovePage(int)` | 특정 인덱스의 페이지 삭제 |
| `RefreshPage(int)` | 특정 페이지 소스 갱신 |
| `RefreshAllPages()` | 모든 페이지 소스 갱신 |
| `DragRightPageToPoint(Vector3)` | 오른쪽 페이지 드래그 |
| `DragLeftPageToPoint(Vector3)` | 왼쪽 페이지 드래그 |
| `ReleasePage()` | 드래그한 페이지 릴리즈 |

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `CurrentPage` | int | 현재 페이지 인덱스 (읽기/쓰기) |
| `TotalPageCount` | int | 전체 페이지 수 (읽기 전용) |
| `Interactable` | bool | 상호작용 가능 여부 |

| 이벤트 | 파라미터 | 설명 |
|--------|----------|------|
| `OnFlip` | - | 페이지 넘김 완료 시 |
| `OnPageChanged` | int currentPage | 페이지 변경 시 |
| `OnFlipStart` | - | 넘김 시작 시 |
| `OnFlipEnd` | - | 넘김 완료 시 |

### BookFlipPage

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Type` | PageType | 페이지 타입 (Sprite/Prefab/GameObject) |
| `Source` | SourceMode | 소스 로드 방식 (Direct/ResourcesPath/CustomAsync) |
| `PersistInstance` | bool | 인스턴스 유지 여부 |
| `RuntimeInstance` | GameObject | 현재 활성 런타임 인스턴스 (읽기 전용) |
| `AsyncLoader` | static Action | 비동기 에셋 로더 훅 (CustomAsync 모드용) |

| 메서드 | 설명 |
|--------|------|
| `GetOrCreateImage(Transform, string)` | 페이지 Image 반환/생성 |
| `Release(Transform)` | 슬롯에서 인스턴스 해제 |
| `RefreshInstance()` | 인스턴스 초기화 (다음 표시 시 재생성) |
| `ForceDestroyInstance()` | 인스턴스 즉시 파괴 |
| `LoadFromResources()` | Resources.Load 동기 로드 |
| `LoadAsync(Transform, string, Action)` | 비동기 에셋 로드 코루틴 |
| `SetInteractable(bool)` | UI 인터랙션 제어 |
| `SetAlpha(float)` | CanvasGroup 알파 설정 |
| `IsValid()` | 유효성 검사 |

### BookFlipAutoFlip

| 메서드 | 설명 |
|--------|------|
| `StartFlipping()` | 자동 넘김 시작 |
| `StopFlipping()` | 자동 넘김 중지 |
| `FlipRightPage()` | 오른쪽으로 한 페이지 넘기기 |
| `FlipLeftPage()` | 왼쪽으로 한 페이지 넘기기 |
| `FlipToPage(int)` | 특정 페이지로 자동 넘김 |

## 버전 히스토리

### v1.1.0 (2026-04-09)
- 런타임 페이지 관리 API 추가 (SetPages/AddPage/InsertPage/RemovePage/RefreshPage)
- SourceMode 지원 (Direct/ResourcesPath/CustomAsync)
- PersistInstance 모드 추가
- DOTween 의존성 제거 (AnimationCurve + Time.deltaTime)
- 페이지 넘김 깜빡임 완전 해결 (TweenTo break + 슬롯 RT 복원 + 클리핑 요소 비활성화)
- 프로젝트 프리팹 직접 연결 지원 (씬 템플릿 필드 제거)
- 코드 리팩토링 및 성능 최적화

### v1.0.0 (2026-04-08)
- 초기 릴리즈
- Sprite, Prefab, GameObject 타입 지원
- UI 인터랙션 자동 제어
- 모바일 최적화
- 커스텀 에디터

## 라이선스

이 패키지는 프로젝트 내부용으로 제작되었습니다.

## 크레딧

- 기반 알고리즘: [Book-Page Curl](http://rbarraza.com/html5-canvas-pageflip/)
- 개선 및 고도화: CAT Team
