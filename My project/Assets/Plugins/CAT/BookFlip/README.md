# BookFlip - 고도화된 책넘기기 시스템

Unity UI용 고급 페이지 넘김 효과 패키지입니다. 기존 "Book-Page Curl" 패키지를 개선하여 다양한 페이지 타입과 UI 인터랙션 제어를 지원합니다.

## ✨ 주요 기능

### 1. 다양한 페이지 타입 지원
- **Sprite**: 이미지 스프라이트를 페이지로 사용
- **Prefab**: 프리팹을 인스턴스화하여 페이지로 사용
- **GameObject**: 씬에 이미 존재하는 GameObject를 페이지로 사용

### 2. UI 인터랙션 자동 제어
- 페이지 넘기는 중: 모든 UI 요소 인터랙션 비활성화
- 페이지 넘김 완료 후: UI 인터랙션 자동 활성화
- CanvasGroup 기반 자동 관리

### 3. 모바일 최적화
- Transform 및 Component 캐싱
- 메모리 할당 최소화
- 불필요한 `GetComponent<T>()` 호출 제거
- 효율적인 코루틴 관리

### 4. 부드러운 애니메이션
- AnimationCurve를 활용한 커스터마이징 가능한 넘김 애니메이션
- 자동 넘김 기능
- 특정 페이지로 직접 이동

## 📦 구성 요소

### 핵심 스크립트

#### BookFlip.cs
메인 컨트롤러 컴포넌트입니다.

**주요 메서드:**
```csharp
// 다음 페이지로 이동
public void NextPage()

// 이전 페이지로 이동
public void PreviousPage()

// 특정 페이지로 직접 이동
public void GoToPage(int pageIndex)

// 수동 드래그 (EventTrigger와 함께 사용)
public void OnMouseDragRightPage()
public void OnMouseDragLeftPage()
public void OnMouseRelease()
```

**주요 프로퍼티:**
```csharp
public int CurrentPage { get; set; }
public int TotalPageCount { get; }
public bool Interactable { get; set; }
```

**이벤트:**
```csharp
public UnityEvent OnFlip;                  // 페이지 넘김 시
public UnityEvent<int> OnPageChanged;      // 페이지 변경 시
public UnityEvent OnFlipStart;             // 넘김 시작 시
public UnityEvent OnFlipEnd;               // 넘김 완료 시
```

#### BookFlipPage.cs
페이지 데이터를 추상화하는 클래스입니다.

```csharp
[System.Serializable]
public class BookFlipPage
{
    public enum PageType { Sprite, Prefab, GameObject }

    // 페이지 타입에 따라 자동으로 적절한 처리
    // UI 인터랙션 자동 제어
}
```

#### BookFlipAutoFlip.cs
자동 페이지 넘김 기능을 제공합니다.

**주요 메서드:**
```csharp
// 자동 넘김 시작
public void StartFlipping()

// 자동 넘김 중지
public void StopFlipping()

// 오른쪽 페이지 넘기기
public void FlipRightPage()

// 왼쪽 페이지 넘기기
public void FlipLeftPage()

// 특정 페이지로 자동 넘김
public void FlipToPage(int targetPage)
```

## 🚀 사용 방법

### 1. 기본 설정

1. **Canvas 생성**
   - Hierarchy에서 우클릭 → UI → Canvas

2. **BookFlip GameObject 생성**
   - Canvas 하위에 빈 GameObject 생성
   - `BookFlip` 컴포넌트 추가

3. **UI 요소 설정**

   BookFlip 컴포넌트에 필요한 UI Image들을 생성하고 연결합니다:

   - **BookPanel**: 책 전체 영역을 담는 RectTransform
   - **ClippingPlane**: 넘김 효과용 클리핑 이미지
   - **NextPageClip**: 다음 페이지 클리핑 이미지
   - **Shadow, ShadowLTR**: 그림자 효과 이미지
   - **Left, LeftNext**: 왼쪽 페이지 이미지들
   - **Right, RightNext**: 오른쪽 페이지 이미지들

   > **참고**: 기존 "Book-Page Curl" 패키지의 프리팹 구조를 참고하여 UI 요소를 배치하세요.

4. **컨테이너 기반 레이어 설정 (UI 인터랙션 페이지 사용 시 권장)**

   UI 버튼 등이 포함된 Prefab 페이지를 사용하는 경우, 페이지 요소와 핫스팟을 완전히 분리된 레이어로 격리하는 것이 가장 안정적입니다.

   **방법 1: PageContainer + HotSpotContainer 사용 (강력 권장)**

   완전한 레이어 분리를 통해 페이지 요소와 핫스팟의 렌더링 순서 충돌을 원천 차단합니다.

   1. **PageContainer 생성**
      - BookPanel 하위에 빈 GameObject 생성
      - 이름: `PageContainer`
      - `RectTransform` 설정: Stretch (앵커 0,0 to 1,1)
      - BookFlip 인스펙터 → Canvas 설정 섹션 → Page Container 필드에 연결

   2. **HotSpotContainer 생성**
      - BookPanel 하위에 빈 GameObject 생성
      - 이름: `HotSpotContainer`
      - `RectTransform` 설정: Stretch (앵커 0,0 to 1,1)

   3. **HotSpot 생성**
      - HotSpotContainer 하위에 두 개의 빈 GameObject 생성:
        - `LeftHotSpot` (왼쪽 절반 영역)
        - `RightHotSpot` (오른쪽 절반 영역)

   4. **각 HotSpot 컴포넌트 설정**
      - `RectTransform` 설정 (영역 지정)
      - `Image` 컴포넌트 추가 (Alpha를 0으로 설정하여 투명하게)
      - `BookFlipHotSpot` 컴포넌트 추가
      - BookFlipHotSpot의 Type 설정 (Left 또는 Right)
      - "자동 설정" 버튼 클릭으로 간편 설정

   5. **BookFlip 인스펙터에서 연결**
      - UI 요소 (고급) 펼치기
      - HotSpot Container 필드에 HotSpotContainer 드래그 & 드롭
      - Left HotSpot / Right HotSpot 필드에 각각 드래그 & 드롭

   **계층 구조 예시:**
   ```
   BookPanel (RectTransform)
   ├── PageContainer (RectTransform)              ← 모든 페이지 요소 격리
   │   ├── ClippingPlane (Image)
   │   ├── NextPageClip (Image)
   │   ├── Shadow (Image)
   │   ├── ShadowLTR (Image)
   │   ├── Left (Image)
   │   ├── LeftNext (Image)
   │   ├── Right (Image)
   │   └── RightNext (Image)
   └── HotSpotContainer (RectTransform)           ← 핫스팟 완전 격리 (항상 최상위)
       ├── LeftHotSpot (BookFlipHotSpot + Image)
       └── RightHotSpot (BookFlipHotSpot + Image)
   ```

   > **PageContainer + HotSpotContainer 사용 시 동작:**
   > - BookFlip.Start()에서 모든 페이지 요소를 자동으로 PageContainer 하위로 이동
   > - Update()에서 매 프레임 PageContainer(index 0) < HotSpotContainer(last index) 순서 강제
   > - UI 버튼 클릭, 페이지 넘김 등 어떤 상황에서도 레이어 순서 보장
   > - LeftNext/RightNext가 HotSpotContainer 위로 올라가는 것을 완전히 차단

   **방법 2: HotSpotContainer만 사용**

   PageContainer 없이 HotSpotContainer만 사용하면 핫스팟만 격리됩니다.

   1. 위의 2~5단계와 동일 (PageContainer 생성 건너뜀)
   2. Page Container 필드는 비워둠 (BookPanel이 자동으로 대체)

   > **HotSpotContainer만 사용 시**:
   > - HotSpotContainer는 BookPanel의 하이어라키에서 **항상 최하위(마지막)**에 위치
   > - 페이지 요소들은 BookPanel의 직접 자식으로 유지
   > - Update()에서 Container를 최상위로 유지
   > - 대부분의 경우 충분하지만, 복잡한 UI 상호작용 시 방법 1 권장

   **방법 3: 개별 HotSpot 배치 (Container 없이)**

   가장 간단하지만 복잡한 UI에서는 안정성이 낮습니다.

   1. BookPanel 하위에 직접 두 개의 빈 GameObject 생성
   2. 위의 4~5단계와 동일 (Page Container, HotSpot Container 필드 모두 비워둠)

   > **개별 HotSpot 사용 시**:
   > - 개별 핫스팟이 BookPanel의 최하위(마지막 또는 마지막-1)에 위치
   > - 페이지 넘김 중 부모 변경 시 BookPanel 직접 자식으로 복원됨
   > - 간단한 UI에는 충분하지만, UI 버튼이 많은 경우 방법 1 강력 권장

### 2. 페이지 추가

Inspector에서 "페이지 목록"의 `+` 버튼을 클릭하여 페이지를 추가합니다.

#### Sprite 타입 페이지
```
1. Type을 "Sprite"로 선택
2. Sprite 필드에 이미지를 드래그 & 드롭
```

#### Prefab 타입 페이지
```
1. Type을 "Prefab"으로 선택
2. Prefab 필드에 프리팹을 드래그 & 드롭
```

**Prefab 요구사항:**
- 반드시 `RectTransform` 컴포넌트 포함
- UI 요소(버튼, 슬라이더 등) 포함 가능
- 프리팹 루트에 `CanvasGroup`이 없으면 자동으로 추가됨

#### GameObject 타입 페이지
```
1. Type을 "GameObject"로 선택
2. Hierarchy의 GameObject를 드래그 & 드롭
```

**주의사항:**
- 씬에 이미 존재하는 GameObject 사용
- BookFlip이 파괴될 때 GameObject는 유지됨

### 3. 자동 넘김 설정 (선택사항)

BookFlip GameObject에 `BookFlipAutoFlip` 컴포넌트를 추가합니다.

**Inspector 설정:**
```
- Mode: RightToLeft / LeftToRight
- Page Flip Time: 넘김 애니메이션 시간 (초)
- Time Between Pages: 페이지 간 대기 시간 (초)
- Delay Before Starting: 시작 전 대기 시간 (초)
- Auto Start Flip: 자동 시작 여부
- Animation Frames Count: 애니메이션 프레임 수
- Flip Curve: 넘김 애니메이션 커브
```

### 4. 스크립트에서 제어

```csharp
using CAT.BookFlip;

public class BookController : MonoBehaviour
{
    [SerializeField] private BookFlip _bookFlip;

    void Start()
    {
        // 이벤트 리스너 등록
        _bookFlip.OnPageChanged.AddListener(OnPageChanged);
        _bookFlip.OnFlipStart.AddListener(OnFlipStart);
        _bookFlip.OnFlipEnd.AddListener(OnFlipEnd);
    }

    public void NextPage()
    {
        _bookFlip.NextPage();
    }

    public void PreviousPage()
    {
        _bookFlip.PreviousPage();
    }

    public void GoToPage(int pageIndex)
    {
        _bookFlip.GoToPage(pageIndex);
    }

    private void OnPageChanged(int currentPage)
    {
        Debug.Log($"페이지 변경: {currentPage}");
    }

    private void OnFlipStart()
    {
        Debug.Log("넘김 시작");
        // 페이지 넘기는 동안 다른 UI 비활성화 등
    }

    private void OnFlipEnd()
    {
        Debug.Log("넘김 완료");
        // UI 다시 활성화 등
    }
}
```

### 5. UI 버튼 연결

```csharp
// Button OnClick 이벤트에 연결
public void OnNextButtonClick()
{
    _bookFlip.NextPage();
}

public void OnPreviousButtonClick()
{
    _bookFlip.PreviousPage();
}
```

## 🎯 UI 인터랙션 페이지 만들기

Prefab 타입 페이지를 사용하여 버튼, 슬라이더 등 상호작용 가능한 UI를 포함할 수 있습니다.

### 예시: 인터랙티브 페이지 프리팹

1. **프리팹 생성**
   ```
   - 빈 GameObject 생성 (RectTransform 자동 추가됨)
   - UI 요소 추가 (Button, Slider, ScrollView 등)
   - Prefab으로 저장
   ```

2. **BookFlip에 등록**
   ```
   - BookFlip Inspector → 페이지 목록 → + 버튼
   - Type: Prefab 선택
   - Prefab 필드에 드래그 & 드롭
   ```

3. **자동 동작**
   ```
   - 넘기는 중: CanvasGroup.interactable = false
   - 넘김 완료: CanvasGroup.interactable = true
   - Raycast 차단: CanvasGroup.blocksRaycasts = false (항상)
   ```

   > **중요**: 페이지 프리팹의 `CanvasGroup.blocksRaycasts`는 항상 `false`로 유지됩니다.
   > 이는 LeftHotSpot/RightHotSpot의 레이캐스트가 차단되지 않도록 하기 위함입니다.
   > 개별 UI 요소(Button 등)의 상호작용은 `CanvasGroup.interactable` 속성으로 제어됩니다.

### 프리팹 구조 예시

```
PagePrefab (RectTransform)
├── Background (Image)
├── Title (TextMeshPro)
├── ConfirmButton (Button)
│   └── Text (TextMeshPro)
├── CancelButton (Button)
│   └── Text (TextMeshPro)
└── ContentSlider (Slider)
    ├── Background
    ├── Fill Area
    └── Handle Slide Area
```

## ⚡ 모바일 최적화 팁

### 1. 페이지 수 제한
- 너무 많은 페이지는 메모리 사용량 증가
- 권장: 50페이지 이하

### 2. Prefab 최적화
- 불필요한 Layout Group 제거
- Raycast Target 최소화
- 복잡한 Hierarchy 피하기

### 3. 애니메이션 프레임 조정
- 고성능 기기: 40~60 프레임
- 저성능 기기: 20~30 프레임

### 4. Shadow 효과 선택적 사용
- 저사양 기기에서는 Shadow 비활성화
- Inspector → Enable Shadow Effect 체크 해제

## 🐛 문제 해결

### 페이지가 표시되지 않음
- Canvas의 Render Mode 확인
- BookPanel의 크기 확인
- 페이지 타입별 참조가 올바른지 확인

### UI 인터랙션이 작동하지 않음
- CanvasGroup이 자동으로 추가되는지 확인
- OnFlipEnd 이벤트가 정상적으로 호출되는지 확인
- 개별 UI 요소의 Raycast Target 설정 확인
- 프리팹 루트의 CanvasGroup.blocksRaycasts는 항상 false로 유지됨

### 페이지 넘김 핫스팟이 작동하지 않음

#### 증상 1: UI 버튼만 클릭해도 더 이상 페이지 넘김이 안 됨
**원인**: UI 버튼 클릭 후 UpdateSprites() 호출 시 LeftNext/RightNext가 HotSpotContainer 위로 생성/이동되어 핫스팟을 덮어버림

**해결 방법 (우선순위 순):**

1. **PageContainer + HotSpotContainer 사용 (강력 권장)**
   - BookPanel 하위에 PageContainer와 HotSpotContainer를 각각 생성
   - 모든 페이지 요소를 PageContainer 하위로 격리
   - 핫스팟을 HotSpotContainer 하위로 격리
   - BookFlip이 자동으로 PageContainer < HotSpotContainer 순서 유지
   - **장점**: UI 버튼 클릭, 페이지 넘김 등 어떤 상황에서도 안정적으로 동작
   - **단점**: 계층 구조가 약간 복잡해짐

2. **HotSpotContainer만 사용**
   - BookPanel 하위에 HotSpotContainer만 생성
   - 핫스팟만 격리, 페이지 요소는 BookPanel 직접 자식
   - 대부분의 경우 충분하지만, 복잡한 UI에서는 1번 권장

3. **개별 HotSpot 배치 (Container 없이)**
   - 가장 간단하지만 안정성이 낮음
   - Update()의 EnsureHotSpotsOnTop()이 매 프레임 복원 시도
   - 간단한 UI에만 권장

**추가 체크 사항:**
- `BookFlipHotSpot` 컴포넌트 사용 확인
- 핫스팟의 `Image` 컴포넌트 Raycast Target 활성화 확인
- 페이지 프리팹의 CanvasGroup.blocksRaycasts는 항상 false로 유지됨

#### 증상 2: 드래그 한 번 후에만 핫스팟이 작동
**원인**: EventSystem이 UI 요소에 포커스를 유지

**해결 방법:**
- `BookFlipHotSpot.OnPointerUp()`에서 `EventSystem.SetSelectedGameObject(null)` 호출 (자동 처리됨)
- 핫스팟을 최상위(하이어라키 최하위)에 배치하면 포커스 우선순위 확보

#### 증상 3: 페이지 넘김 중간에 놓으면 더 이상 페이지 넘김이 안 됨
**원인**: 페이지 넘김 애니메이션 중 Left/Right/ClippingPlane의 부모가 변경되면서 핫스팟 순서가 밀림

**해결 방법:**
- PageContainer + HotSpotContainer 사용 (위의 증상 1 해결 방법 참고)
- Container 사용 시 LeftNext/RightNext가 핫스팟 위로 올라가는 것을 원천 차단

### 성능 이슈
- Animation Frames Count 줄이기
- Shadow 효과 비활성화
- Prefab 최적화
- 페이지 수 줄이기

## 📝 API 레퍼런스

### BookFlip

| 메서드 | 설명 |
|--------|------|
| `NextPage()` | 다음 페이지로 이동 |
| `PreviousPage()` | 이전 페이지로 이동 |
| `GoToPage(int)` | 특정 페이지로 이동 |
| `DragRightPageToPoint(Vector3)` | 오른쪽 페이지를 특정 지점으로 드래그 |
| `DragLeftPageToPoint(Vector3)` | 왼쪽 페이지를 특정 지점으로 드래그 |
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

### BookFlipAutoFlip

| 메서드 | 설명 |
|--------|------|
| `StartFlipping()` | 자동 넘김 시작 |
| `StopFlipping()` | 자동 넘김 중지 |
| `FlipRightPage()` | 오른쪽으로 한 페이지 넘기기 |
| `FlipLeftPage()` | 왼쪽으로 한 페이지 넘기기 |
| `FlipToPage(int)` | 특정 페이지로 자동 넘김 |

## 🔄 버전 히스토리

### v1.0.0 (2026-04-08)
- 초기 릴리즈
- Sprite, Prefab, GameObject 타입 지원
- UI 인터랙션 자동 제어
- 모바일 최적화
- 커스텀 에디터

## 📄 라이선스

이 패키지는 프로젝트 내부용으로 제작되었습니다.

## 🙏 크레딧

- 기반 알고리즘: [Book-Page Curl](http://rbarraza.com/html5-canvas-pageflip/)
- 개선 및 고도화: CAT Team

## 📧 지원

문제가 발생하거나 제안사항이 있으시면 이슈를 등록해주세요.
