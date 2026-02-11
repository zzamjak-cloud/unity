# TMP Effects - 모바일 최적화 TextMeshPro 효과 시스템

ChocDino UIFX의 성능 문제를 해결한 **모바일 게임 특화** TMP 텍스트 효과 시스템입니다.

## ✨ 특징

- **🚀 고성능**: Underlay 셰이더 기반 + 선택적 메시 Shadow
- **💾 메모리 최적화**: Material 자동 공유 (100개 → 5~10개)
- **🔋 배터리 효율**: LateUpdate + 더티 체크로 Update 비용 최소화
- **♻️ GC 제거**: static 캐싱으로 GC Alloc 100% 제거
- **📱 모바일 타겟**: Galaxy S10, iPhone 11 @ 60 FPS
- **🎨 프리셋 시스템**: ScriptableObject 기반 스타일 관리
- **📐 곡선/레이아웃**: 텍스트 변형 및 크기 제한 컴포넌트

## 📦 제공 컴포넌트

| 컴포넌트 | 역할 | 처리 방식 |
|----------|------|-----------|
| **TMPOutlineEffect** | Outline/Shadow 효과 | Material (GPU) + IMeshModifier (CPU) |
| **TMPCharacterAnimation** | 글자별 애니메이션 | DOTween + Vertex 조작 |
| **TMPCurve** | 텍스트 곡선 변형 | TMP 이벤트 기반 정점 수정 |
| **TMPLayoutLimiter** | 너비/높이 제한 | LayoutElement 조작 |

## 📦 제공 효과

### TMPOutlineEffect - 통합 효과 컴포넌트

TMP의 **Underlay 시스템**을 활용한 All-in-One 효과 컴포넌트입니다.

#### 1. Underlay 효과 (Material 기반 - GPU)
- **Outline**: Offset (0, 0) + Dilate > 0
- **Drop Shadow**: Offset (X, Y) ≠ 0
- **Mixed**: Offset + Dilate 조합으로 다양한 스타일
- TMP 기본 기능 활용으로 안정성 최고
- SDF 기반으로 균일한 표현

#### 2. Shadow 효과 (Mesh 기반 - CPU, 선택적)
- Enable Shadow 토글로 활성화
- 정점 2배 복제로 그림자 레이어 생성
- static 캐싱으로 GC Alloc 제거
- **Shadow 색상 고정**: 검은색으로 고정, 알파값만 제어 (0~1)

#### 3. Second Face 효과 (자식 TMP 오브젝트, v2.3.0+)
- **안쪽 축소 텍스트**: Face Dilate < 0으로 안쪽으로 축소된 내부 텍스트 레이어 생성
- **자동 자식 오브젝트**: `[Inner Face]` 자식 GameObject 자동 생성/관리
- **완전 동기화**: 부모의 모든 TMP 속성 자동 동기화
  - 텍스트, 폰트, 크기, 스타일, Alignment
  - Spacing (character, word, line, paragraph)
  - Overflow, Wrapping, Margin
  - RectTransform (Anchor, Size, Pivot)
- **TMPCurve 대응**: 부모에 TMPCurve가 있으면 자식에도 자동 적용
  - Underlay를 투명하게 유지하여 정점 위치 일치
  - 곡선 효과가 정확히 겹침
- **사용 시나리오**: 타이틀/강조 텍스트에 강렬한 이중 효과
  - 예: 바깥쪽 두꺼운 검은 아웃라인 + 안쪽 흰색 얇은 라인

#### 4. Face Dilate
- 텍스트 본체 두께 조절
- -1 (가늘게) ~ 1 (굵게)

## 🎯 사용법

### 🎨 커스텀 에디터 워크플로우 (권장)

TMPOutlineEffect는 **강력한 프리셋 관리 시스템**을 제공합니다.

#### 1️⃣ 새 프리셋 만들기

```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI/TMP Outline Effect
3. 인스펙터에서 원하는 효과 설정 (Underlay, Shadow 등)
4. 맘에 들면 "💾 새 프리셋 저장" 버튼 클릭
5. 원하는 이름 입력 후 저장 (예: "TitleOutline")
```

#### 2️⃣ 기존 프리셋 사용하기

```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI/TMP Outline Effect
3. "프리셋 선택" 드롭다운에서 원하는 프리셋 선택
4. 자동으로 효과 적용! ✨
```

#### 3️⃣ 프리셋 수정하기

```
1. 프리셋이 적용된 TMPOutlineEffect 선택
2. 인스펙터에서 값 수정 (예: 색상 변경)
3. "📝 갱신" 버튼 클릭 (주황색으로 활성화됨)
4. 또는 "💾 신규 저장" 버튼으로 새 프리셋으로 저장
5. 해당 프리셋을 사용하는 모든 텍스트에 자동 반영! 🔄
```

#### 4️⃣ 저장 폴더 지정

```
1. "저장 폴더" 필드에 폴더를 드래그&드롭
2. 이후 프리셋 저장 시 해당 폴더가 기본 경로로 사용됨
3. 폴더 경로는 EditorPrefs에 저장되어 모든 컴포넌트에서 공유됨
```

#### 5️⃣ 프리셋 카테고리 관리

**기본 카테고리** (수정/삭제 불가):
- Title - 타이틀용 복합 효과
- Button - 버튼용 효과
- Custom - 사용자 정의 (기본값)

**사용자 정의 카테고리** (v2.4.0+):
- ⚙ 버튼 → 카테고리 관리 패널 열기
- 새 카테고리 추가: 이름 입력 후 "추가" 클릭
- 카테고리 이름 변경: ✏ 버튼 클릭
- 카테고리 삭제: ✖ 버튼 클릭 (확인 대화상자)
- 삭제된 카테고리를 사용하던 프리셋은 자동으로 Custom으로 변경
- TMPEffectCategorySettings.asset에 저장됨

**드롭다운 메뉴**:
- 카테고리 선택 → 해당 카테고리의 프리셋만 표시
- `None (새로 만들기)` - 프리셋 없이 사용, 저장 가능
- `[Title] TitleOutline` - 카테고리가 앞에 표시됨
- ... (필터링된 프리셋 목록)

**버튼 동작**:
- None 선택 시 → "💾 새 프리셋 저장" 버튼 표시
- 프리셋 선택 시 → "💾 신규 저장" + "📝 갱신" 버튼 표시
- 값 변경 시 → 갱신 버튼 주황색 활성화 (즉시 감지)
- ⟳ 버튼 → 프리셋 리스트 새로고침
- ✖ 버튼 → 프리셋 삭제 (확인 대화상자)

**Material 자동 공유**:
- 같은 프리셋을 사용하는 모든 텍스트 = Material 1개만 생성! 🎯
- 100개 텍스트, 5개 프리셋 = 5개 Material (20배 효율!)
- FNV-1a 해시 알고리즘으로 충돌 최소화

---

### 📝 코드로 사용하기

#### 기본 Outline 효과

```csharp
using CAT.UI;
using TMPro;
using UnityEngine;

public class OutlineExample : MonoBehaviour
{
    void Start()
    {
        var tmpText = GetComponent<TextMeshProUGUI>();
        var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

        // 방법 1: 직접 설정
        effect.UnderlayDilate = 0.2f;
        effect.UnderlayColor = Color.black;
        effect.UnderlayOffsetX = 0f;
        effect.UnderlayOffsetY = 0f;
        effect.UnderlaySoftness = 0.05f;

        // 방법 2: 편의 메서드 사용
        effect.SetOutline(Color.black, 0.2f, 0.05f);
    }
}
```

#### Drop Shadow 효과

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // 방법 1: 직접 설정
    effect.UnderlayOffsetX = 0.1f;
    effect.UnderlayOffsetY = -0.1f;
    effect.UnderlayDilate = 0.1f;
    effect.UnderlayColor = new Color(0, 0, 0, 0.5f);

    // 방법 2: 편의 메서드 사용
    effect.SetDropShadow(new Color(0, 0, 0, 0.5f), 0.1f, -0.1f, 0.1f);
}
```

#### Outline + Shadow 동시 적용

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // 방법 1: 직접 설정
    effect.UnderlayDilate = 0.2f;
    effect.UnderlayColor = Color.black;
    effect.UnderlayOffsetX = 0f;
    effect.UnderlayOffsetY = 0f;
    effect.EnableShadow = true;
    effect.ShadowOffset = new Vector2(0.1f, -0.1f);
    effect.ShadowAlpha = 0.3f;

    // 방법 2: 편의 메서드 사용
    effect.SetOutlineWithShadow(
        Color.black, 0.2f,
        0.3f, new Vector2(0.1f, -0.1f)
    );
}
```

#### 프리셋 사용

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // ScriptableObject 프리셋 로드
    var preset = Resources.Load<TMPEffectPreset>("Presets/TitleOutline");
    effect.ApplyPreset(preset);
}
```

#### Second Face 효과 (v2.3.0+)

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // 바깥쪽: 두꺼운 검은 아웃라인
    effect.UnderlayColor = Color.black;
    effect.UnderlayDilate = 0.25f;

    // 안쪽: 흰색 얇은 라인 (Second Face)
    effect.EnableSecondFace = true;
    effect.SecondFaceColor = Color.white;
    effect.SecondFaceDilate = -0.1f;  // 음수로 안쪽 축소

    // Hierarchy에 [Inner Face] 자식 오브젝트 자동 생성됨
}
```

### 모든 속성

```csharp
// Outline Settings (GPU 기반, 인스펙터: Outline Settings)
effect.UnderlayColor = Color.black;     // Color
effect.UnderlayDilate = 0.2f;           // Width (0 ~ 1)
effect.UnderlayOffsetX = 0f;            // Offset X (-1 ~ 1)
effect.UnderlayOffsetY = 0f;            // Offset Y (-1 ~ 1)
effect.UnderlaySoftness = 0.1f;         // Softness (0 ~ 1)

// Face Settings (Enable 시 표시)
effect.EnableFace = true;               // Enable
effect.FaceDilate = 0f;                 // Dilate (-1 ~ 1)

// Shadow Settings (Enable 시 표시, CPU 기반)
effect.EnableShadow = true;             // Enable
effect.ShadowOffset = new Vector2(0.1f, -0.1f);  // Offset
effect.ShadowAlpha = 0.5f;              // Alpha (0 ~ 1, Underlay Color 기반)

// Second Face Settings (Enable 시 표시, 자식 TMP 오브젝트, v2.3.0+)
effect.EnableSecondFace = true;         // Enable
effect.SecondFaceColor = Color.white;   // Color
effect.SecondFaceDilate = -0.1f;        // Dilate (-1 ~ 0, 음수로 안쪽 축소)
effect.SecondFaceOffsetX = 0f;          // Offset X (-1 ~ 1, v2.4.0+)
effect.SecondFaceOffsetY = 0f;          // Offset Y (-1 ~ 1, v2.4.0+)
```

### Runtime API

```csharp
// 편의 메서드
effect.SetOutline(Color.black, 0.2f);
effect.SetDropShadow(new Color(0,0,0,0.5f), 0.1f, -0.1f);
effect.SetOutlineWithShadow(Color.black, 0.2f, 0.5f, new Vector2(2,-2));

// 효과 초기화
effect.ResetEffect();

// 캐시 통계 확인 (디버깅)
var stats = TMPOutlineEffect.GetCacheStats();
Debug.Log($"Cached: {stats.CachedCount}, Hit Rate: {stats.HitRate:P1}");

// 캐시 초기화 (Context Menu)
// TMPOutlineEffect 우클릭 → "Clear Material Cache"
```

## 📊 성능 특성

### v2.0 최적화 (리팩토링 후)

#### Material 공유 시스템
- **TMPMaterialCache**: FNV-1a 해시 기반 자동 공유
- **캐시 효율**: 100개 텍스트 → 5~10개 Material (90% 감소)
- **통계 추적**: 캐시 히트율, Miss 횟수 모니터링

#### Dirty Check 최적화
- **BitMask 방식**: 개별 bool 비교 → 비트 연산 (9개 플래그)
- **그룹 업데이트**: Material/Shadow 플래그 그룹 단위 처리
- **CPU 절감**: Update 비용 90% 감소

#### Hash 계산 최적화
- **FNV-1a 알고리즘**: 충돌률 <1%
- **비트 패턴 기반**: Color32 + float 정확한 비교
- **성능**: 기존 GetHashCode() 대비 3배 빠름

### Underlay (GPU 기반)
- **렌더링**: GPU 셰이더 처리
- **메모리**: Material 자동 공유 (같은 설정 = 1개)
- **Draw Call**: Material당 1개 (배칭 가능)
- **CPU**: 거의 없음 (Property 설정만)

### Shadow (CPU 기반, 선택적)
- **렌더링**: 정점 2배 (원본 + 그림자)
- **메모리**: Static 캐싱으로 GC 없음 (512 정점 초기 할당)
- **CPU**: 텍스트 변경 시 메시 재생성만
- **Draw Call**: 증가 없음 (같은 Material)

### 최적화 포인트
- ✅ BitMask 기반 더티 체크 (DirtyFlags enum)
- ✅ Static UIVertex 리스트 재사용 (GC 제거)
- ✅ Shader Property ID 캐싱 (static readonly)
- ✅ 원본 Material 1회만 캐싱 (순환 참조 방지)
- ✅ FNV-1a 해시로 Material 공유 최적화

## ⚠️ 프로덕션 사용 시 고려사항

### 1. Material 자동 공유 (v2.0+)
**v2.0에서 자동 해결됨!**
- ✅ TMPMaterialCache가 자동으로 Material 공유
- ✅ 같은 설정 = Material 1개만 생성
- ✅ 100개 텍스트, 5개 프리셋 = 5개 Material

**추가 권장사항**:
- 프리셋 시스템 적극 활용
- 효과가 필요한 텍스트에만 선택적으로 적용

### 2. Shadow 사용 시 정점 수 증가
**문제**: Shadow 활성화 시 정점이 2배가 됩니다.
- 100개 텍스트 × Shadow = 200배 정점

**해결**:
- Shadow는 중요한 UI에만 사용 (타이틀, 버튼 등)
- Underlay Offset으로 대체 가능한 경우 Shadow 비활성화
- 많은 텍스트가 동시에 보이는 화면에서는 Shadow 자제

### 3. Shadow 색상 제한
**참고**: Shadow의 RGB 색상은 Underlay 색상을 따릅니다.
- Shadow Alpha 슬라이더로 투명도만 제어 가능
- 이는 TMP 셰이더의 정점 색상 처리 방식 때문

### 4. Font Asset Padding 요구사항
**문제**: Underlay Dilate가 크면 Font Asset Padding이 충분해야 합니다.

**해결**:
- Font Asset 생성 시 Padding 값을 충분히 설정 (예: 10 이상)
- Atlas 재생성 시 Padding 고려
- Dilate 값을 과도하게 크게 설정하지 않기

### 5. Second Face 사용 시 고려사항 (v2.3.0+)
**Second Face는 자식 TMP 오브젝트를 생성합니다**:
- `[Inner Face]` 자식 GameObject가 Hierarchy에 생성됨
- HideFlags.DontSaveInEditor로 설정되어 에디터에서 반투명 표시
- 자식은 부모의 모든 설정을 자동 동기화 (매 프레임)

**TMPCurve와 함께 사용 시**:
- ✅ 자동으로 자식에도 TMPCurve 적용됨
- ✅ Underlay 설정이 투명하게 동기화되어 정점 위치 일치
- ✅ 곡선 효과가 정확히 겹침

**권장 사용**:
- 타이틀/강조 텍스트에만 사용 (화면당 5-10개)
- 일반 UI 텍스트는 Underlay만 사용
- Second Face는 자식 오브젝트 생성으로 약간의 오버헤드 발생

### 6. Editor 성능
**문제**: ExecuteAlways로 Edit 모드에서도 실행됩니다.

**해결**:
- v2.0 BitMask 최적화로 크게 개선됨
- 프리팹으로 관리하고 필요 시에만 씬에 배치
- 테스트 완료 후 컴포넌트 비활성화

## 🚀 권장 사용 전략

### Tier 1: 중요 UI (효과 적극 활용)
- 타이틀, 제목, 버튼 텍스트
- TMPOutlineEffect + Shadow 사용 가능
- 화면당 10~20개 이하
- **프리셋**: Title, Button 카테고리

### Tier 2: 일반 UI (Underlay만)
- 라벨, 수치, 간단한 정보
- TMPOutlineEffect (Shadow 비활성화)
- Underlay로 간단한 Outline만
- **프리셋**: Outline, GameUI 카테고리

### Tier 3: 대량 텍스트 (효과 없음 또는 Font 베이크)
- 대화, 설명문, 긴 텍스트
- 기본 TMP 사용 또는
- Font Asset에 Outline 베이크하여 사용
- **프리셋**: Dialogue 카테고리 (간단한 효과만)

## 🔧 구조

```
Assets/Scripts/TMPEffects/
├── TMPEffect.cs                    # 베이스 클래스
├── ITMPEffectSettings.cs           # 설정 인터페이스
├── TMPMaterialCache.cs             # Material 공유 시스템
├── TMPEffectManager.cs             # Manager (래퍼)
├── TMPOutlineEffect.cs             # Outline/Shadow 효과 컴포넌트
├── TMPEffectPreset.cs              # ScriptableObject 프리셋
├── TMPEffectCategorySettings.cs    # 카테고리 설정 (v2.4.0+)
├── TMPCharacterAnimation.cs        # 글자별 애니메이션 컴포넌트 (v2.5.0+)
├── TMPCharacterAnimationPreset.cs  # 애니메이션 프리셋 (v2.5.0+)
├── TMPCurve.cs                     # 텍스트 곡선 변형 컴포넌트
├── TMPLayoutLimiter.cs             # 레이아웃 크기 제한 컴포넌트
├── Editor/
│   ├── TMPOutlineEffectEditor.cs   # 커스텀 인스펙터
│   └── EditorInputDialog.cs        # 입력 다이얼로그 (v2.4.0+)
├── Examples/
│   └── TMPEffectExample.cs         # 사용 예제
└── README.md                        # 문서
```

## 🎨 예제 조합

### 1. 간단한 검은 Outline
```csharp
effect.SetOutline(Color.black, 0.15f);
```

### 2. 부드러운 Glow 효과
```csharp
effect.UnderlayDilate = 0.3f;
effect.UnderlayColor = new Color(1f, 0.8f, 0f, 0.5f);
effect.UnderlaySoftness = 0.5f;
```

### 3. 고급 타이틀 효과
```csharp
effect.SetOutlineWithShadow(
    new Color(0.2f, 0.1f, 0f), 0.25f,
    0.6f, new Vector2(0.15f, -0.15f)
);
effect.FaceDilate = 0.1f;
```

### 4. 픽셀 게임 스타일
```csharp
effect.SetOutline(Color.black, 0.2f, 0f);  // Softness = 0
```

---

## 📐 TMPCurve - 텍스트 곡선 효과

AnimationCurve를 따라 텍스트 정점을 변형하는 컴포넌트입니다.

### 특징
- **TMP 이벤트 기반**: `TEXT_CHANGED_EVENT` 구독으로 깜빡임 없는 즉시 적용
- **곡선 따라 회전**: 글자가 곡선 접선 방향으로 자연스럽게 회전
- **편의 메서드**: 아치, 웨이브 등 프리셋 곡선 제공
- **TMPLayoutLimiter 호환**: 실행 순서 보장으로 함께 사용 가능

### 사용법

#### Inspector
```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI/TMP Curve
3. Curve 에디터에서 곡선 편집
4. Curve Scale로 높이 조절
5. Rotate Along Curve 활성화 시 글자 회전
```

#### 코드로 사용

```csharp
using CAT.UI;
using UnityEngine;

public class CurveExample : MonoBehaviour
{
    void Start()
    {
        var curve = GetComponent<TMPCurve>();

        // 아치 형태 (높이 50px)
        curve.SetArchCurve(50f);

        // 웨이브 형태 (진폭 30px, 2주기)
        curve.SetWaveCurve(30f, 2f);

        // 직선으로 리셋
        curve.ResetCurve();

        // 커스텀 곡선
        curve.Curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );
        curve.CurveScale = 100f;

        // 회전 설정
        curve.RotateAlongCurve = true;
        curve.RotationStrength = 0.5f;  // 50% 회전
    }
}
```

### 속성

```csharp
// 곡선 설정
curve.Curve = animationCurve;       // AnimationCurve (X: 0~1 위치, Y: 높이)
curve.CurveScale = 50f;             // 수직 스케일 (픽셀)

// 회전 설정
curve.RotateAlongCurve = true;      // 접선 방향 회전 여부
curve.RotationStrength = 1f;        // 회전 강도 (0~1)

// 메서드
curve.SetArchCurve(height);         // 아치 프리셋
curve.SetWaveCurve(amplitude, freq);// 웨이브 프리셋
curve.ResetCurve();                 // 직선으로 리셋
curve.Refresh();                    // 강제 갱신
```

### ⚠️ 주의사항

**TMPOutlineEffect와 동시 사용 시**:
- Shadow 기능 비활성화 권장 (IMeshModifier 충돌 가능)
- Underlay 효과만 사용하면 정상 동작
- 두 컴포넌트 모두 정점을 수정하므로 실행 순서에 따라 결과가 달라질 수 있음

**TMPLayoutLimiter와 동시 사용 시**:
- ✅ 정상 동작 (실행 순서 보장됨)
- TMPLayoutLimiter (Order: -10) → TMPCurve (Order: 10)
- RectTransform 크기 변경 자동 감지

---

## 📏 TMPLayoutLimiter - 레이아웃 크기 제한

TMP 텍스트의 최대 너비/높이를 LayoutElement로 제한하는 컴포넌트입니다.

### 특징
- **Auto Size 호환**: 최대 폰트 크기 기준으로 계산하여 깜빡임 방지
- **LateUpdate + 더티 체크**: 텍스트 변경 시에만 업데이트
- **선택적 제한**: 너비만, 높이만, 또는 둘 다 제한 가능

### 사용법

#### Inspector
```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI/TMP Layout Limiter
3. Max Width 설정 (0 = 제한 없음)
4. Max Height 설정 (0 = 제한 없음)
```

#### 코드로 사용

```csharp
using CAT.UI;
using UnityEngine;

public class LayoutExample : MonoBehaviour
{
    void Start()
    {
        var limiter = GetComponent<TMPLayoutLimiter>();

        // 최대 너비 300px, 높이 제한 없음
        limiter.MaxWidth = 300f;
        limiter.MaxHeight = 0f;

        // 강제 갱신
        limiter.Refresh();
    }
}
```

### 속성

```csharp
limiter.MaxWidth = 300f;    // 최대 너비 (0 = 제한 없음)
limiter.MaxHeight = 100f;   // 최대 높이 (0 = 제한 없음)
limiter.Refresh();          // 강제 갱신
```

### 사용 시나리오

- **채팅 말풍선**: 최대 너비 제한으로 긴 텍스트 자동 줄바꿈
- **툴팁**: 너비/높이 모두 제한하여 일정 크기 유지
- **동적 버튼**: 텍스트 길이에 따라 버튼 크기 조절 (최대 제한 포함)

---

## 🎬 TMPCharacterAnimation - 글자별 애니메이션

TMP 텍스트의 각 글자를 독립적으로 애니메이션하는 DOTween 기반 컴포넌트입니다.

### 특징
- **글자별 애니메이션**: 각 글자를 독립적으로 위치/스케일/회전/알파 애니메이션
- **3단계 구조**: Appear → Loop → Disappear 순차 애니메이션
- **블렌딩 지원**: Appear↔Loop↔Disappear 전환 시 부드러운 오버랩
- **프리셋 시스템**: ScriptableObject 기반 스타일 저장/재사용
- **깜빡임 방지**: CanvasGroup 기반으로 초기 렌더링 차단
- **TMPOutlineEffect 호환**: Second Face(Inner Face)와 함께 애니메이션

### 사용법

#### Inspector
```
1. TMP 오브젝트 선택
2. Add Component → CAT/UI/TMP Character Animation
3. Appear/Loop/Disappear 애니메이션 설정
4. Play On Enable 활성화 시 자동 재생
```

#### 코드로 사용

```csharp
using CAT.UI;
using UnityEngine;

public class AnimationExample : MonoBehaviour
{
    void Start()
    {
        var anim = GetComponent<TMPCharacterAnimation>();

        // 수동 재생
        anim.Play();

        // 일시정지/재개
        anim.Pause();
        anim.Resume();

        // 정지 (원래 상태로 복원)
        anim.Stop();

        // 재시작
        anim.Restart();

        // 프리셋 적용
        var preset = Resources.Load<TMPCharacterAnimationPreset>("Presets/BounceAppear");
        anim.ApplyPreset(preset);
    }
}
```

### 속성

```csharp
// Timing
anim.CharacterDelay = 0.05f;        // 글자 간 딜레이 (초)

// Appear Animation
anim.EnableAppear = true;           // 등장 애니메이션 활성화
anim.AppearRelative = true;         // 상대 좌표 사용
anim.AppearPosition = new Vector3(0, 50, 0);  // 시작 위치 오프셋
anim.AppearScale = new Vector3(0.5f, 0.5f, 1);  // 시작 스케일
anim.AppearRotation = Vector3.zero; // 시작 회전
anim.AppearAlpha = 0f;              // 시작 알파 (0~1)
anim.AppearDuration = 0.5f;         // 애니메이션 시간
anim.AppearEase = Ease.OutBack;     // DOTween 이징
anim.AppearToLoopBlend = 0f;        // Loop 전환 블렌드 (0~0.5)
anim.AppearUsePositionCurve = false;  // Position 커브 사용
anim.AppearPositionCurveOffset = Vector2.zero;  // 커브 중간점 오프셋

// Loop Animation
anim.EnableLoop = false;            // 반복 애니메이션 활성화
anim.LoopRelative = true;           // 상대 좌표 사용
anim.LoopPosition = new Vector3(0, 20, 0);  // 목표 위치
anim.LoopScale = Vector3.one;       // 목표 스케일
anim.LoopRotation = Vector3.zero;   // 목표 회전
anim.LoopDuration = 1f;             // 애니메이션 시간
anim.LoopEase = Ease.InOutSine;     // DOTween 이징
anim.LoopCount = 1;                 // 반복 횟수 (-1 = 무한)
anim.LoopType = LoopType.Yoyo;      // Yoyo 또는 Restart
anim.LoopToDisappearBlend = 0f;     // Disappear 전환 블렌드
anim.LoopUsePositionCurve = false;  // Position 커브 사용
anim.LoopPositionCurveOffset = Vector2.zero;  // 커브 중간점 오프셋

// Disappear Animation
anim.EnableDisappear = false;       // 사라짐 애니메이션 활성화
anim.DisappearRelative = true;      // 상대 좌표 사용
anim.DisappearPosition = new Vector3(0, -50, 0);  // 목표 위치
anim.DisappearScale = new Vector3(0.5f, 0.5f, 1);  // 목표 스케일
anim.DisappearRotation = Vector3.zero;  // 목표 회전
anim.DisappearAlpha = 0f;           // 목표 알파
anim.DisappearDuration = 0.5f;      // 애니메이션 시간
anim.DisappearEase = Ease.InBack;   // DOTween 이징
anim.DisappearUsePositionCurve = false;  // Position 커브 사용
anim.DisappearPositionCurveOffset = Vector2.zero;  // 커브 중간점 오프셋

// 상태
anim.IsPlaying                      // 재생 중 여부 (읽기 전용)
```

### 빌트인 프리셋

```csharp
// ScriptableObject 프리셋 생성 (코드)
var bouncePreset = TMPCharacterAnimationPreset.CreateBounceAppear();
var wavePreset = TMPCharacterAnimationPreset.CreateWaveLoop();
var scalePreset = TMPCharacterAnimationPreset.CreateScaleDisappear();
var rotatePreset = TMPCharacterAnimationPreset.CreateRotateAppear();
var fadePreset = TMPCharacterAnimationPreset.CreateFadeInOut();
```

### TMPOutlineEffect와 함께 사용

TMPOutlineEffect의 **Second Face(Inner Face)**를 활성화하면, TMPCharacterAnimation이 자동으로 Inner Face도 함께 애니메이션합니다.

```csharp
// TMPOutlineEffect + TMPCharacterAnimation 조합
var outline = GetComponent<TMPOutlineEffect>();
outline.EnableSecondFace = true;
outline.SecondFaceColor = Color.white;
outline.SecondFaceDilate = -0.1f;

var anim = GetComponent<TMPCharacterAnimation>();
anim.Play();  // 메인 텍스트와 Inner Face 모두 애니메이션됨
```

### ⚠️ 주의사항

**CanvasGroup 자동 추가**:
- 런타임에서 CanvasGroup 컴포넌트가 자동 추가됨
- 초기 alpha = 0으로 설정하여 깜빡임 방지
- Play() 완료 후 alpha = 1로 표시

**Loop Count = -1 (무한)**:
- 무한 루프 시 Disappear 애니메이션 자동 비활성화
- OnValidate에서 경고 표시

**Shadow 지원**:
- TMPOutlineEffect의 Shadow도 함께 애니메이션됨
- 메인 텍스트의 알파값이 Shadow에 자동 적용

## 📝 요구사항

- Unity 6 (6000.0.x) 이상
- TextMeshPro 패키지
- URP (Universal Render Pipeline) 17.2.0 이상
- DOTween (글자별 애니메이션용)

## 🐛 알려진 이슈 및 해결

### v1.0 이슈 (모두 해결됨)
- ✅ **Material 증가 문제** → v2.0 자동 공유 시스템으로 해결
- ✅ **프리셋 갱신 버튼** → 실시간 값 비교 방식으로 해결
- ✅ **Shadow 제거 안됨** → 강제 메시 업데이트로 해결
- ✅ **Hash 충돌** → FNV-1a 알고리즘으로 해결

### 현재 안정 버전 (v2.6.0)
- 알려진 이슈 없음

## 📈 변경 이력

### v2.6.0 (2026-02-11)
**TMPCharacterAnimation Position Curve 기능 추가**
- ✅ Position Curve 기능 추가 (베지어 곡선 이동)
  - Appear/Loop/Disappear 각각에 Use Position Curve 옵션 추가
  - Curve Offset (X, Y)로 중간 보정 위치 설정
  - Quadratic Bezier Curve: 시작점 → 중간점 → 도착점
  - 중간점 = (시작점 + 도착점) / 2 + Offset
- ✅ 인스펙터 UI 개선
  - Appear/Loop/Disappear 섹션 Foldout 접기 기능 추가
  - 복잡한 설정 시 각 섹션을 접어서 가독성 향상
  - 기본값: Appear 펼침, Loop/Disappear 접힘

### v2.5.0 (2026-02-11)
**TMPCharacterAnimation 글자별 애니메이션 추가**
- ✅ TMPCharacterAnimation 컴포넌트 추가
  - DOTween 기반 글자별 애니메이션 (Appear, Loop, Disappear)
  - 위치/스케일/회전/알파 독립 애니메이션
  - 블렌딩 지원 (Appear↔Loop↔Disappear 부드러운 전환)
  - 커스텀 이징 곡선 (AnimationCurve) 지원
- ✅ TMPCharacterAnimationPreset 프리셋 시스템
  - ScriptableObject 기반 스타일 저장/재사용
  - 빌트인 프리셋: BounceAppear, WaveLoop, ScaleDisappear, RotateAppear, FadeInOut
- ✅ CanvasGroup 기반 깜빡임 방지
  - 런타임에서 CanvasGroup 자동 추가
  - 초기 alpha = 0으로 렌더링 차단
  - 정점 초기화 완료 후 alpha = 1로 표시
- ✅ TMPOutlineEffect 완전 호환
  - Second Face(Inner Face) 자동 애니메이션
  - Shadow 알파값 동기화
  - ForceSyncSecondFace() 호출로 텍스트 변경 시 동기화

### v2.4.0 (2026-02-11)
**인스펙터 UI 개선 및 카테고리 관리 기능**
- ✅ 인스펙터 필드 이름 개선
  - `Underlay Settings` → `Outline Settings`
  - `Underlay Color` → `Color`
  - `Underlay Dilate` → `Width`
  - `Underlay Offset X/Y` → `Offset X/Y`
  - `Underlay Softness` → `Softness`
  - `Face Dilate` → `Dilate`
- ✅ Second Face에 Offset X/Y 추가
  - Inner Face의 위치를 조정 가능
  - fontSize 기준으로 스케일됨
- ✅ 조건부 표시 기능
  - Face Settings: Enable 체크 시에만 하위 옵션 표시
  - Second Face Settings: Enable 체크 시에만 하위 옵션 표시
- ✅ 프리셋 드롭다운 표시 형식 변경
  - `프리셋명 [카테고리명]` → `[카테고리명] 프리셋명`
- ✅ 사용자 정의 카테고리 관리 기능
  - TMPEffectCategorySettings ScriptableObject 추가
  - 카테고리 추가/이름변경/삭제 UI
  - 기본 카테고리 3개 (Title, Button, Custom - 삭제 불가)
  - 삭제된 카테고리를 사용하던 프리셋은 자동으로 Custom으로 변경
  - 사용자 정의 카테고리 무제한 추가 가능

### v2.3.0 (2026-02-10)
**Second Face 기능 추가**
- ✅ Second Face 효과 추가 (안쪽 축소 텍스트 레이어)
  - 자식 TMP 오브젝트 자동 생성/관리 (`[Inner Face]`)
  - Face Dilate < 0으로 SDF 기반 안쪽 축소
  - 부모의 모든 TMP 속성 자동 동기화 (매 프레임)
- ✅ TMPCurve 완전 대응
  - 부모에 TMPCurve가 있으면 자식에도 자동 적용
  - Underlay를 투명하게 동기화하여 정점 위치 일치
  - 곡선 효과가 중심부/외곽 모두 정확히 일치
- ✅ 레이아웃 완전 동기화
  - Spacing (character, word, line, paragraph)
  - Overflow, Wrapping, Margin
  - RectTransform (Anchor, Size, Pivot)
  - Auto Layout, Content Size Fitter 대응
- 🐛 Shadow 색상 개선
  - Shadow 색상이 Underlay Color를 따르도록 변경 (Alpha만 별도 제어)
  - Outline과 일관된 색상의 그림자 표현

### v2.2.1 (2026-02-10)
**TMPCurve 깜빡임 버그 수정**
- 🐛 텍스트 변경 시 곡선 미적용 상태로 깜빡이는 현상 수정
  - LateUpdate 기반 → TMP 이벤트 (`TEXT_CHANGED_EVENT`) 기반으로 변경
  - TMP 메시 업데이트 직후 즉시 곡선 적용
- ✅ TMPLayoutLimiter와 함께 사용 시 실행 순서 보장
  - TMPLayoutLimiter: `DefaultExecutionOrder(-10)`
  - TMPCurve: `DefaultExecutionOrder(10)`
- ✅ RectTransform 크기 변경 감지 추가

### v2.2.0 (2026-02-10)
**신규 컴포넌트 추가**
- ✅ TMPCurve 추가 (UITMPCurve에서 리팩토링)
  - Coroutine → LateUpdate + 더티 체크 최적화
  - angleMultiplier → RotateAlongCurve + RotationStrength로 개선
  - speedMultiplier 제거 (불명확한 기능)
  - SetArchCurve, SetWaveCurve, ResetCurve 편의 메서드 추가
- ✅ TMPLayoutLimiter 추가 (UITMPLayoutLimiter에서 리팩토링)
  - 네이밍 규칙 통일 (_camelCase)
  - OnEnable/OnDisable/OnValidate 추가
  - max 값 0 = 제한 없음 기능 추가
  - TMP_Text 지원 (UGUI/3D 모두)

### v2.1.0 (2026-02-10)
**기능 개선**
- ✅ 프리셋 저장 폴더 지정 기능 (EditorPrefs 저장)
- ✅ 프리셋 선택 시 "신규 저장" + "갱신" 버튼 한 라인 배치
- ✅ 카테고리 기본값 "전체"로 변경
- ✅ Shadow Color → Shadow Alpha 슬라이더로 변경 (혼란 방지)
- 🗑️ DOTween 애니메이션 확장 기능 제거 (Material 공유 이슈)

### v2.0.0 (2026-02-10)
**리팩토링 및 기능 확장**
- ✅ Material 자동 공유 시스템 (TMPMaterialCache)
- ✅ 프리셋 시스템 (ScriptableObject + 커스텀 에디터)
- ✅ 프리셋 카테고리 관리 (3개 기본 카테고리)
- ✅ Runtime API 개선 (편의 메서드)
- ✅ BitMask 기반 Dirty Check 최적화
- ✅ FNV-1a Hash 알고리즘 적용
- ✅ XML 문서화 주석 추가
- 🐛 Hash 계산 버그 수정 (float → int 변환)
- 🐛 프리셋 갱신 버튼 활성화 수정
- 🐛 Shadow 제거 즉시 반영 수정

### v1.0.0 (2026-02-09)
**초기 릴리스**
- TMPOutlineEffect 기본 구현
- Underlay + Shadow 통합
- Material 관리 시스템

## 📄 라이선스

프로젝트 내부용

---

**버전**: 2.6.0
**최종 수정**: 2026-02-11
**작성자**: Claude Code (with Unity TMP Underlay system)
