# UIShining

UGUI **Image**(아틀라스 스프라이트) / **RawImage**에 빛이 지나가는 광택 루프를 적용하는 컴포넌트입니다. 광택은 **Additive + Burn**으로 합성됩니다(밝은 픽셀에서만 강하게 더해지고, 어두운 픽셀은 미미하게).  
기존 UI Effect Shine보다 가볍게, **광택 밴드가 직선이 아닌 볼록 렌즈처럼 휘어진 형태**로 지나가도록 셰이더로 구현되어 있습니다.

## 사용법

1. **Image** 또는 **RawImage**가 붙은 GameObject를 선택합니다.
2. **Add Component** → **CAT** → **Effects** → **UIShining**을 추가합니다.
3. Duration, Movement Curve, LoopType, Interval, Width Start/End, Intensity, Curvature Start/End, Angle, Shine Color 등을 인스펙터에서 조절합니다.
4. **에디터 테스트**: 플레이 모드가 아닐 때 **"에디터 재생 (60초)"** 버튼을 누르면 60초간 애니메이션을 미리보기할 수 있습니다. 재생 중지 시 즉시 초기 상태로 돌아갑니다.

## 파라미터 (한국어)

| 파라미터 | 설명 |
|----------|------|
| **Duration** | 한 사이클 재생 시간(초). |
| **Movement Curve** | 이동 이징. Curve Editor에서 가속/감속 곡선을 편집할 수 있습니다. 기본은 Linear(0,0,1,1). |
| **Loop Type** | **Replay**: 한 바퀴 끝나면 0부터 다시 재생. **Yoyo**: 끝까지 갔다가 역방향으로 재생(왕복). |
| **Interval Min / Max** | 1회 루프가 끝난 뒤, 다음 사이클을 시작하기 전 대기 시간의 **범위**(초). 매 사이클마다 이 구간에서 **랜덤**으로 선택됩니다. (예: 0.3 ~ 0.6) |
| **Progress Offset** | 광택이 이미지 **밖**에서 시작해 **밖**에서 끝나도록 하는 여유(0~2). 크게 할수록 시작/종료가 이미지 밖으로 더 벗어나 자연스러운 스윕이 됩니다. 기본 0.55. |
| **Width Start** | 한 사이클 **시작** 시 밴드 두께(0.01~1). |
| **Width End** | 한 사이클 **종료** 시 밴드 두께(0.01~1). Duration 진행에 따라 Start→End로 보간됩니다. |
| **Intensity** | 광택 강도(0~3). 기본 1.35. |
| **Burn Bias** | 밝은 픽셀에서만 블렌드를 강하게. 0=전체 균일, **1에 가까울수록** 어두운 부분은 미미하고 밝은 부분만 강하게. 기본 0.85. |
| **Blend Strength** | Additive 강도 배율(0.5~2.5). 올릴수록 광택이 더 진하게. 기본 1.45. |
| **Curvature Start** | 한 사이클 **시작** 시 휘어짐 강도(-1~1). 0=직선, 양수=볼록. |
| **Curvature End** | 한 사이클 **종료** 시 휘어짐 강도(-1~1). Duration 진행에 따라 Start→End로 보간됩니다. (예: 1→0으로 두면 처음엔 많이 휘었다가 끝날 때 직선에 가깝게) |
| **Angle** | 진행 방향 각도(도). 0 = 왼쪽 → 오른쪽. |
| **Shine Color** | 광택 색. 기본 흰색. |
| **Softness** | 소프트 블러/롱 테일(0~1). 0이면 선형 경계(smoothstep), 1에 가까울수록 가우시안 형태로 가장자리가 부드럽게 흐려집니다. |

## 셰이더 빌드 포함 (iOS / Android / WebGL)

- 런타임에 Material을 생성하므로, **빌드 시 셰이더가 제거되지 않도록** 해야 합니다.
- **권장**: 메뉴 **CAT** → **Effects** → **UIShining - 셰이더 빌드 포함 등록**을 한 번 실행하면, **Project Settings > Graphics > Always Included Shaders**에 UIShining 셰이더가 등록됩니다.
- 또는 **Edit > Project Settings > Graphics**에서 **Always Included Shaders** 목록에 `CAT/Effects/UIShining` 셰이더를 수동으로 추가해도 됩니다.

## 디버깅

스프라이트 UV가 제대로 전달되지 않는 경우, `UIShining.cs`의 `ModifyMesh` 메서드에서 디버그 로그를 활성화할 수 있습니다:

```csharp
// 이 부분의 주석을 해제하세요 (약 115번째 줄)
#if UNITY_EDITOR
if (g is Image img && img.sprite != null)
    Debug.Log($"[UIShining] ModifyMesh: sprite={img.sprite.name}, rect={rect}, vertexCount={vh.currentVertCount}");
#endif
```

콘솔에 스프라이트 이름, UV 사각형(rect), 버텍스 수가 출력됩니다.

## 참고

- 다른 CAT 플러그인(PathFollower, ColorReplace 등)과 코드/씬 의존성은 없습니다.
- 셰이더는 UI 전용 1 Pass이며, Stencil/Clip Rect를 지원해 마스크·클리핑과 함께 사용할 수 있습니다.
- **Sprite Atlas 대응**: Windable과 동일한 방식(`sprite.textureRect` 정규화)을 사용합니다.
- **UI Sliced 이미지**: 9-slice 메시도 지원하며, 전체 스프라이트 UV 범위를 버텍스에 주입합니다.
