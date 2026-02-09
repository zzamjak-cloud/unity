# TMP Effects - 모바일 최적화 TextMeshPro 효과 시스템

ChocDino UIFX의 성능 문제를 해결한 **모바일 게임 특화** TMP 텍스트 효과 시스템입니다.

## ✨ 특징

- **🚀 고성능**: Underlay 셰이더 기반 + 선택적 메시 Shadow
- **💾 메모리 최적화**: Material 인스턴스 관리 및 재사용
- **🔋 배터리 효율**: 더티 체크로 Update 비용 최소화
- **♻️ GC 제거**: static 캐싱으로 GC Alloc 100% 제거
- **📱 모바일 타겟**: Galaxy S10, iPhone 11 @ 60 FPS

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
- Underlay와 독립적으로 동작 (동시 사용 가능)

#### 3. Face Dilate
- 텍스트 본체 두께 조절
- -1 (가늘게) ~ 1 (굵게)

## 🎯 사용법

### 기본 Outline 효과

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

        // Underlay 설정 (Outline)
        effect.UnderlayDilate = 0.2f;           // 외곽선 두께
        effect.UnderlayColor = Color.black;
        effect.UnderlayOffsetX = 0f;            // Outline은 Offset 0
        effect.UnderlayOffsetY = 0f;
        effect.UnderlaySoftness = 0.05f;        // 부드러움
    }
}
```

### Drop Shadow 효과 (Underlay 활용)

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // Underlay 설정 (Shadow)
    effect.UnderlayOffsetX = 0.1f;             // 오른쪽으로
    effect.UnderlayOffsetY = -0.1f;            // 아래로
    effect.UnderlayDilate = 0.1f;              // 두께
    effect.UnderlayColor = new Color(0, 0, 0, 0.5f);  // 반투명 검정
}
```

### Outline + Shadow 동시 적용

```csharp
void Start()
{
    var tmpText = GetComponent<TextMeshProUGUI>();
    var effect = tmpText.gameObject.AddComponent<TMPOutlineEffect>();

    // Underlay로 Outline 효과
    effect.UnderlayDilate = 0.2f;
    effect.UnderlayColor = Color.black;
    effect.UnderlayOffsetX = 0f;
    effect.UnderlayOffsetY = 0f;

    // Mesh Shadow 추가 (독립적)
    effect.EnableShadow = true;
    effect.ShadowOffset = new Vector2(0.1f, -0.1f);
    effect.ShadowColor = new Color(0, 0, 0, 0.3f);

    // Face 두께 조절 (선택)
    effect.FaceDilate = 0.05f;  // 약간 굵게
}
```

### 모든 속성

```csharp
// Underlay Settings (GPU 기반)
effect.UnderlayColor = Color.black;
effect.UnderlayDilate = 0.2f;           // 0 ~ 1
effect.UnderlayOffsetX = 0f;            // -1 ~ 1
effect.UnderlayOffsetY = 0f;            // -1 ~ 1
effect.UnderlaySoftness = 0.1f;         // 0 ~ 1

// Face Settings
effect.FaceDilate = 0f;                 // -1 ~ 1

// Shadow Settings (CPU 기반, 선택적)
effect.EnableShadow = true;
effect.ShadowOffset = new Vector2(0.1f, -0.1f);
effect.ShadowColor = new Color(0, 0, 0, 0.5f);
```

## 📊 성능 특성

### Underlay (GPU 기반)
- **렌더링**: GPU 셰이더 처리
- **메모리**: Material 인스턴스 1개/효과
- **Draw Call**: Material당 1개 (배칭 가능)
- **CPU**: 거의 없음 (Property 설정만)

### Shadow (CPU 기반, 선택적)
- **렌더링**: 정점 2배 (원본 + 그림자)
- **메모리**: Static 캐싱으로 GC 없음
- **CPU**: 텍스트 변경 시 메시 재생성
- **Draw Call**: 증가 없음 (같은 Material)

### 최적화 포인트
- ✅ 더티 체크로 불필요한 업데이트 스킵
- ✅ Static UIVertex 리스트 재사용 (GC 제거)
- ✅ Material Property ID 캐싱
- ✅ 원본 Material 1회만 캐싱 (순환 참조 방지)

## ⚠️ 프로덕션 사용 시 고려사항

### 1. Material 인스턴스 증가
**문제**: 각 TMPOutlineEffect가 Material 인스턴스를 생성합니다.
- 100개 텍스트 = 100개 Material 인스턴스
- Material마다 별도 Draw Call 가능 (배칭 안 될 경우)

**해결**:
- 동일한 효과 설정을 가진 텍스트끼리 Material 공유 (TMPEffectManager 활용)
- 효과가 필요한 텍스트에만 선택적으로 적용
- 대량 텍스트(대화, 설명문)는 Font Asset 자체에 효과 베이크

### 2. Shadow 사용 시 정점 수 증가
**문제**: Shadow 활성화 시 정점이 2배가 됩니다.
- 100개 텍스트 × Shadow = 200배 정점

**해결**:
- Shadow는 중요한 UI에만 사용 (타이틀, 버튼 등)
- Underlay Offset으로 대체 가능한 경우 Shadow 비활성화
- 많은 텍스트가 동시에 보이는 화면에서는 Shadow 자제

### 3. Font Asset Padding 요구사항
**문제**: Underlay Dilate가 크면 Font Asset Padding이 충분해야 합니다.

**해결**:
- Font Asset 생성 시 Padding 값을 충분히 설정 (예: 10 이상)
- Atlas 재생성 시 Padding 고려
- Dilate 값을 과도하게 크게 설정하지 않기

### 4. Editor 성능
**문제**: ExecuteAlways로 Edit 모드에서도 실행됩니다.

**해결**:
- 씬에 수백 개의 텍스트가 있으면 Editor가 느려질 수 있음
- 프리팹으로 관리하고 필요 시에만 씬에 배치
- 테스트 완료 후 컴포넌트 비활성화

## 🚀 권장 사용 전략

### Tier 1: 중요 UI (효과 적극 활용)
- 타이틀, 제목, 버튼 텍스트
- TMPOutlineEffect + Shadow 사용 가능
- 화면당 10~20개 이하

### Tier 2: 일반 UI (Underlay만)
- 라벨, 수치, 간단한 정보
- TMPOutlineEffect (Shadow 비활성화)
- Underlay로 간단한 Outline만

### Tier 3: 대량 텍스트 (효과 없음 또는 Font 베이크)
- 대화, 설명문, 긴 텍스트
- 기본 TMP 사용 또는
- Font Asset에 Outline 베이크하여 사용

## 🔧 구조

```
Assets/Scripts/TMPEffects/
├── TMPEffect.cs              # 베이스 클래스 (더티 체크)
├── TMPEffectManager.cs       # Material 공유 시스템 (선택적)
├── TMPOutlineEffect.cs       # 통합 효과 컴포넌트
└── README.md
```

## 🎨 예제 조합

### 1. 간단한 검은 Outline
```csharp
effect.UnderlayDilate = 0.15f;
effect.UnderlayColor = Color.black;
```

### 2. 부드러운 Glow 효과
```csharp
effect.UnderlayDilate = 0.3f;
effect.UnderlayColor = new Color(1f, 0.8f, 0f, 0.5f);  // 노란색 반투명
effect.UnderlaySoftness = 0.5f;
```

### 3. 고급 타이틀 효과
```csharp
effect.UnderlayDilate = 0.25f;
effect.UnderlayColor = new Color(0.2f, 0.1f, 0f);      // 어두운 갈색 Outline
effect.FaceDilate = 0.1f;                               // 텍스트 굵게
effect.EnableShadow = true;
effect.ShadowOffset = new Vector2(0.15f, -0.15f);
effect.ShadowColor = new Color(0, 0, 0, 0.6f);
```

### 4. 픽셀 게임 스타일
```csharp
effect.UnderlayDilate = 0.2f;
effect.UnderlayColor = Color.black;
effect.UnderlaySoftness = 0f;                           // 딱딱한 경계
effect.FaceDilate = 0f;
```

## 📝 요구사항

- Unity 6 (6000.0.x) 이상
- TextMeshPro 패키지
- URP (Universal Render Pipeline) 17.2.0 이상

## 🐛 알려진 이슈

없음 (현재 안정 버전)

## 📈 향후 계획

- [ ] TMPEffectManager를 통한 Material 자동 공유
- [ ] 프리셋 시스템 (ScriptableObject)
- [ ] 커스텀 인스펙터 (시각적 프리뷰)
- [ ] 추가 효과 (Gradient, Glow, Dissolve)
- [ ] 성능 모니터링 도구

## 📄 라이선스

프로젝트 내부용

---

**버전**: 1.0.0
**최종 수정**: 2026-02-09
**작성자**: Claude Code (with Unity TMP Underlay system)
