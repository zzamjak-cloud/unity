# TMP Effect 개발 가이드

**버전**: 2.9.0
**최종 수정**: 2026-02-12
**목적**: 신규 TMP Effect 컴포넌트 개발 시 체크리스트 및 실수 방지

---

## 📋 신규 TMP Effect 컴포넌트 개발 체크리스트

### 1. 기본 구조 설계

#### 1.1 베이스 클래스 상속
```csharp
[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
[AddComponentMenu("CAT/UI/TMP Your Effect")]
public class TMPYourEffect : TMPEffect, ITMPEffectSettings
```

**체크 포인트:**
- ✅ `[ExecuteAlways]` 속성 추가 (에디터에서도 동작)
- ✅ `[RequireComponent(typeof(TextMeshProUGUI))]` 추가
- ✅ `[AddComponentMenu]` 경로는 "CAT/UI/" 시작
- ✅ `TMPEffect` 상속
- ✅ `ITMPEffectSettings` 구현 (Material 공유 시)

#### 1.2 Material 관리
```csharp
// Material 캐시
private Material _sharedMaterial;
private Material _originalSharedMaterial;

// TMP 컴포넌트
private TextMeshProUGUI _tmpText;
```

**체크 포인트:**
- ✅ `_sharedMaterial`: TMPMaterialCache에서 받은 공유 Material
- ✅ `_originalSharedMaterial`: 원본 Material 참조 (복원용)
- ✅ `_tmpText`: 캐싱된 TMP 컴포넌트

#### 1.3 BitMask Dirty Checking
```csharp
[System.Flags]
private enum DirtyFlags
{
    None = 0,
    Property1 = 1 << 0,
    Property2 = 1 << 1,
    Property3 = 1 << 2,
    Material = Property1 | Property2 | Property3
}

private DirtyFlags _dirtyFlags = DirtyFlags.None;
```

**체크 포인트:**
- ✅ `[System.Flags]` 속성 추가
- ✅ 개별 프로퍼티 플래그 정의
- ✅ `Material` 그룹 플래그 정의 (모든 Material 관련 플래그 OR)
- ✅ `_dirtyFlags` 필드 초기화

---

### 2. 자식 오브젝트 생성 (Second Face, Inner Glow 등)

#### 2.1 자식 오브젝트 생성
```csharp
private void CreateChildObject()
{
    if (!_tmpText) return;
    if (_childObject) return;

    // 기존 오브젝트 확인
    foreach (Transform child in transform)
    {
        if (child.name == "[Child Name]")
        {
            _childObject = child.gameObject;
            _childText = _childObject.GetComponent<TextMeshProUGUI>();
            return;
        }
    }

    // 자식 GameObject 생성
    _childObject = new GameObject("[Child Name]");
    _childObject.hideFlags = HideFlags.NotEditable | HideFlags.DontSaveInEditor;
    _childObject.transform.SetParent(transform, false);

    // TextMeshProUGUI 추가
    _childText = _childObject.AddComponent<TextMeshProUGUI>();

    // RectTransform 설정 (부모를 완전히 채움)
    RectTransform parentRect = _tmpText.rectTransform;
    RectTransform childRect = _childText.rectTransform;

    childRect.anchorMin = Vector2.zero;
    childRect.anchorMax = Vector2.one;
    childRect.anchoredPosition = Vector2.zero;
    childRect.sizeDelta = Vector2.zero;
    childRect.pivot = parentRect.pivot;
    childRect.localScale = Vector3.one;
    childRect.localRotation = Quaternion.identity;

    // Raycast Target 비활성화
    _childText.raycastTarget = false;

    // TMPCurve 복사 (부모에 있는 경우)
    TMPCurve parentCurve = GetComponent<TMPCurve>();
    if (parentCurve)
    {
        TMPCurve childCurve = _childObject.AddComponent<TMPCurve>();
        childCurve.Curve = new AnimationCurve(parentCurve.Curve.keys);
        childCurve.CurveScale = parentCurve.CurveScale;
        childCurve.RotateAlongCurve = parentCurve.RotateAlongCurve;
        childCurve.RotationStrength = parentCurve.RotationStrength;
    }

    // TMPAnimation 복사 (부모에 있는 경우)
    TMPAnimation parentAnimation = GetComponent<TMPAnimation>();
    if (parentAnimation)
    {
        TMPAnimation childAnimation = _childObject.AddComponent<TMPAnimation>();
        if (parentAnimation.Preset != null)
        {
            childAnimation.Preset = parentAnimation.Preset;
        }
    }
}
```

**체크 포인트:**
- ✅ `HideFlags.NotEditable | HideFlags.DontSaveInEditor` 설정
- ✅ 기존 오브젝트 확인 (중복 생성 방지)
- ✅ RectTransform 완전히 채우기 (anchorMin/Max, sizeDelta = 0)
- ✅ `localScale = Vector3.one` (스케일 초기화)
- ✅ `raycastTarget = false` (클릭 이벤트 차단)
- ✅ TMPCurve 복사 (부모에 있으면)
- ✅ TMPAnimation 복사 (부모에 있으면)

#### 2.2 자식 오브젝트 동기화
```csharp
private void SyncChildObject()
{
    if (!_childText || !_tmpText) return;

    // 텍스트 내용
    _childText.text = _tmpText.text;

    // 폰트 및 크기
    _childText.font = _tmpText.font;
    _childText.fontSize = _tmpText.fontSize;
    _childText.fontStyle = _tmpText.fontStyle;

    // 정렬
    _childText.alignment = _tmpText.alignment;

    // Spacing
    _childText.characterSpacing = _tmpText.characterSpacing;
    _childText.wordSpacing = _tmpText.wordSpacing;
    _childText.lineSpacing = _tmpText.lineSpacing;
    _childText.paragraphSpacing = _tmpText.paragraphSpacing;

    // Overflow & Wrapping
    _childText.overflowMode = _tmpText.overflowMode;
    _childText.enableWordWrapping = _tmpText.enableWordWrapping;

    // Mapping
    _childText.horizontalMapping = _tmpText.horizontalMapping;
    _childText.verticalMapping = _tmpText.verticalMapping;

    // Margin
    _childText.margin = _tmpText.margin;

    // Auto Sizing
    _childText.enableAutoSizing = _tmpText.enableAutoSizing;
    _childText.fontSizeMin = _tmpText.fontSizeMin;
    _childText.fontSizeMax = _tmpText.fontSizeMax;

    // Misc
    _childText.richText = _tmpText.richText;
    _childText.parseCtrlCharacters = _tmpText.parseCtrlCharacters;
    _childText.isOrthographic = _tmpText.isOrthographic;
}
```

**체크 포인트:**
- ✅ 모든 TMP 속성 동기화
- ✅ RectTransform은 CreateChildObject에서 설정 (매 프레임 불필요)
- ✅ Color는 효과에 따라 별도 처리

---

### 3. TMPAnimation 통합 (필수!)

**⚠️ 이 섹션이 가장 중요합니다! 놓치기 쉬운 부분입니다.**

#### 3.1 원본 메시 데이터 저장 변수 추가

**TMPAnimation.cs**에 자식 오브젝트의 원본 메시 저장 변수를 추가합니다.

```csharp
// TMPAnimation.cs - 클래스 필드
private Vector3[][] _originalVerticesYourChild;
private Color32[][] _originalColorsYourChild;
```

**체크 포인트:**
- ✅ `_originalVerticesYourChild` 배열 추가
- ✅ `_originalColorsYourChild` 배열 추가
- ✅ `OnDisable()`에서 null 초기화 추가

#### 3.2 Play() 메서드에서 원본 메시 저장

**TMPAnimation.cs - Play() 메서드**에 자식 오브젝트 초기화 및 원본 메시 저장 코드를 추가합니다.

```csharp
// Play() 메서드 내부

// 자식 오브젝트 동기화 (TMPYourEffect가 있는 경우)
var yourEffect = GetComponent<TMPYourEffect>();
TMP_Text childText = null;
if (yourEffect != null)
{
    childText = yourEffect.GetChildText();
    if (childText != null)
    {
        // 텍스트 내용 동기화
        if (childText.text != _tmpText.text)
        {
            childText.text = _tmpText.text;
        }
        childText.SetVerticesDirty();
        childText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
    }
}

// ... (원본 메시 저장 코드 중간) ...

// 자식 오브젝트 원본 메시 저장
if (childText != null)
{
    _originalVerticesYourChild = new Vector3[childText.textInfo.meshInfo.Length][];
    for (int i = 0; i < childText.textInfo.meshInfo.Length; i++)
    {
        Vector3[] vertices = childText.textInfo.meshInfo[i].vertices;
        _originalVerticesYourChild[i] = new Vector3[vertices.Length];
        for (int j = 0; j < vertices.Length; j++)
        {
            _originalVerticesYourChild[i][j] = vertices[j];
        }
    }

    _originalColorsYourChild = new Color32[childText.textInfo.meshInfo.Length][];
    for (int i = 0; i < childText.textInfo.meshInfo.Length; i++)
    {
        Color32[] colors = childText.textInfo.meshInfo[i].colors32;
        _originalColorsYourChild[i] = new Color32[colors.Length];
        for (int j = 0; j < colors.Length; j++)
        {
            _originalColorsYourChild[i][j] = colors[j];
        }
    }
}
```

**체크 포인트:**
- ✅ 자식 텍스트 내용 동기화
- ✅ `SetVerticesDirty()` + `ForceMeshUpdate()` + `Canvas.ForceUpdateCanvases()` 호출
- ✅ 원본 정점 배열 생성 및 복사
- ✅ 원본 색상 배열 생성 및 복사

#### 3.3 TransformCharacterVertices() 메서드에 자식 변환 추가

**TMPAnimation.cs - TransformCharacterVertices() 메서드**에 자식 오브젝트 정점 변환을 추가합니다.

```csharp
// TransformCharacterVertices() 메서드 끝 부분

// 자식 오브젝트도 변환
var yourEffect = GetComponent<TMPYourEffect>();
if (yourEffect != null)
{
    var childText = yourEffect.GetChildText();
    if (childText != null)
    {
        TransformCharacterVerticesYourChild(childText, charIndex, position, scale, rotation, alpha);
    }
}
```

**그리고 전용 변환 메서드 추가:**

```csharp
/// <summary>
/// 자식 오브젝트 전용 문자 정점 변환
/// </summary>
private void TransformCharacterVerticesYourChild(TMP_Text tmpText, int charIndex,
    Vector3 position, Vector3 scale, Vector3 rotation, float alpha)
{
    if (tmpText == null) return;

    var charInfo = tmpText.textInfo.characterInfo[charIndex];
    if (!charInfo.isVisible) return;

    int vertexIndex = charInfo.vertexIndex;
    int materialIndex = charInfo.materialReferenceIndex;
    Vector3[] vertices = tmpText.textInfo.meshInfo[materialIndex].vertices;
    Color32[] colors = tmpText.textInfo.meshInfo[materialIndex].colors32;

    Quaternion rot = Quaternion.Euler(rotation);

    // 자식 전용 원본 데이터 사용
    if (_originalVerticesYourChild == null || materialIndex >= _originalVerticesYourChild.Length) return;
    if (_originalVerticesYourChild[materialIndex] == null) return;
    if (_originalColorsYourChild == null || materialIndex >= _originalColorsYourChild.Length) return;
    if (_originalColorsYourChild[materialIndex] == null) return;

    Vector3 center = new Vector3(
        (_originalVerticesYourChild[materialIndex][vertexIndex].x + _originalVerticesYourChild[materialIndex][vertexIndex + 2].x) / 2f,
        charInfo.baseLine,
        0f
    );

    for (int i = 0; i < 4; i++)
    {
        int idx = vertexIndex + i;
        Vector3 v = _originalVerticesYourChild[materialIndex][idx] - center;

        if (rotation != Vector3.zero) v = rot * v;
        v = Vector3.Scale(v, scale);
        v += position;
        vertices[idx] = v + center;

        if (idx < _originalColorsYourChild[materialIndex].Length)
        {
            Color32 originalColor = _originalColorsYourChild[materialIndex][idx];
            Color32 c = colors[idx];
            c.r = originalColor.r;
            c.g = originalColor.g;
            c.b = originalColor.b;
            c.a = (byte)(originalColor.a * alpha);
            colors[idx] = c;
        }
    }
}
```

**체크 포인트:**
- ✅ `TransformCharacterVertices()` 끝에 자식 변환 추가
- ✅ 전용 메서드 생성 (`TransformCharacterVerticesYourChild`)
- ✅ 자식 전용 원본 데이터 사용 (`_originalVerticesYourChild`, `_originalColorsYourChild`)

#### 3.4 UpdateVertexData() 호출 추가 (2곳)

**TMPAnimation.cs**에서 `UpdateVertexData()`를 호출하는 곳이 2곳 있습니다. 둘 다 자식 오브젝트도 업데이트해야 합니다.

**첫 번째 위치: 애니메이션 루프 내부**

```csharp
// 애니메이션 루프 내부 (for 문 끝)

// 자식 오브젝트 처리
var yourEffect = GetComponent<TMPYourEffect>();
if (yourEffect != null)
{
    var childText = yourEffect.GetChildText();
    if (childText != null)
    {
        childText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }
}
```

**두 번째 위치: 전체 업데이트**

```csharp
// 전체 업데이트 (메서드 끝)

// 자식 오브젝트도 업데이트
var yourEffect = GetComponent<TMPYourEffect>();
if (yourEffect != null)
{
    var childText = yourEffect.GetChildText();
    if (childText != null)
    {
        childText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }
}
```

**체크 포인트:**
- ✅ 애니메이션 루프 끝에 `UpdateVertexData()` 추가
- ✅ 전체 업데이트 끝에 `UpdateVertexData()` 추가

#### 3.5 RestoreOriginalMesh() 메서드에 복원 코드 추가

**TMPAnimation.cs - RestoreOriginalMesh() 메서드**에 자식 오브젝트 원본 메시 복원 코드를 추가합니다.

```csharp
// RestoreOriginalMesh() 메서드 끝 부분

// 자식 오브젝트도 복원
var yourEffect = GetComponent<TMPYourEffect>();
if (yourEffect != null)
{
    var childText = yourEffect.GetChildText();
    if (childText != null)
    {
        // 원본 정점과 색상 복원
        if (_originalVerticesYourChild != null && _originalColorsYourChild != null)
        {
            for (int i = 0; i < childText.textInfo.meshInfo.Length; i++)
            {
                if (i < _originalVerticesYourChild.Length && _originalVerticesYourChild[i] != null)
                {
                    var vertices = childText.textInfo.meshInfo[i].vertices;
                    for (int j = 0; j < vertices.Length && j < _originalVerticesYourChild[i].Length; j++)
                    {
                        vertices[j] = _originalVerticesYourChild[i][j];
                    }
                }

                if (i < _originalColorsYourChild.Length && _originalColorsYourChild[i] != null)
                {
                    var colors = childText.textInfo.meshInfo[i].colors32;
                    for (int j = 0; j < colors.Length && j < _originalColorsYourChild[i].Length; j++)
                    {
                        colors[j] = _originalColorsYourChild[i][j];
                    }
                }
            }

            childText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        var childUGUI = childText as TextMeshProUGUI;
        if (childUGUI != null)
        {
            var canvasRenderer = childUGUI.canvasRenderer;
            if (canvasRenderer != null && childUGUI.mesh != null)
            {
                canvasRenderer.SetMesh(childUGUI.mesh);
            }

            // 자식의 TMPAnimation도 Stop
            TMPAnimation childAnimation = childUGUI.GetComponent<TMPAnimation>();
            if (childAnimation != null)
            {
                childAnimation.Stop();
            }

            // 자식의 CanvasGroup alpha 리셋
            CanvasGroup childCanvasGroup = childUGUI.GetComponent<CanvasGroup>();
            if (childCanvasGroup != null)
            {
                childCanvasGroup.alpha = 1f;
            }
        }

        // 강제 동기화 및 업데이트
        yourEffect.ForceUpdateChild();
    }
}
```

**그리고 Effect 컴포넌트에 `ForceUpdateChild()` 메서드 추가:**

```csharp
/// <summary>
/// 자식 오브젝트를 강제로 동기화 및 업데이트 (TMPAnimation 복원 시 사용)
/// </summary>
public void ForceUpdateChild()
{
    if (_childText == null || _tmpText == null) return;

    // RectTransform 설정 복원 (부모를 완전히 채움)
    RectTransform parentRect = _tmpText.rectTransform;
    RectTransform childRect = _childText.rectTransform;

    childRect.anchorMin = Vector2.zero;
    childRect.anchorMax = Vector2.one;
    childRect.anchoredPosition = Vector2.zero;
    childRect.sizeDelta = Vector2.zero;
    childRect.pivot = parentRect.pivot;
    childRect.localScale = Vector3.one;  // 스케일 강제 리셋
    childRect.localRotation = Quaternion.identity;

    // TMP 속성 동기화
    SyncChildObject();

    // Material 업데이트
    UpdateChildMaterial();

    // 메시 강제 갱신
    _childText.ForceMeshUpdate();
}
```

**체크 포인트:**
- ✅ 원본 정점/색상 복원
- ✅ `UpdateVertexData()` 호출
- ✅ `canvasRenderer.SetMesh()` 호출
- ✅ 자식 TMPAnimation Stop
- ✅ 자식 CanvasGroup alpha 리셋
- ✅ `ForceUpdateChild()` 호출로 완전 복원

---

### 4. Material 관리

#### 4.1 Static Property ID 캐싱

**TMPEffectManager.cs**에 Property ID를 추가합니다.

```csharp
// TMPEffectManager.cs
public static readonly int PropYourProperty = Shader.PropertyToID("_YourProperty");
```

**체크 포인트:**
- ✅ `static readonly int` 사용
- ✅ `Shader.PropertyToID()` 1회만 호출

#### 4.2 Material 생성 및 적용

```csharp
private void UpdateMaterial()
{
    if (_tmpText == null || _tmpText.fontSharedMaterial == null) return;

    // 원본 Material 저장
    _originalSharedMaterial = _tmpText.fontSharedMaterial;

    // Dirty flag 체크
    if (_dirtyFlags == DirtyFlags.None && !_needsInitialization) return;

    // Material 캐시에서 가져오거나 생성
    _sharedMaterial = TMPMaterialCache.Instance.GetOrCreate(_originalSharedMaterial, this);

    if (_sharedMaterial == null)
    {
        Debug.LogWarning("[YourEffect] Material 생성 실패", this);
        return;
    }

    // Material 속성 적용
    ApplyMaterialProperties(_sharedMaterial);

    // TMP에 Material 할당 (Direct Assignment 패턴)
    _tmpText.fontMaterial = _sharedMaterial;

    // Dirty flag 초기화
    _dirtyFlags = DirtyFlags.None;
}

private void ApplyMaterialProperties(Material material)
{
    if (material == null) return;

    material.SetFloat(TMPEffectManager.PropYourProperty, _yourValue);
    // ... 다른 속성들
}
```

**체크 포인트:**
- ✅ `TMPMaterialCache.Instance.GetOrCreate()` 사용
- ✅ Direct Assignment 패턴 (`_tmpText.fontMaterial = _sharedMaterial`)
- ✅ Dirty flag 체크 (불필요한 업데이트 방지)
- ✅ 원본 Material 참조 저장 (`_originalSharedMaterial`)

#### 4.3 Material 복원

```csharp
private void RestoreOriginalMaterial()
{
    if (_tmpText != null && _originalSharedMaterial != null)
    {
        _tmpText.fontMaterial = _originalSharedMaterial;
    }

    _sharedMaterial = null;
}
```

**체크 포인트:**
- ✅ `OnDisable()`, `OnDestroy()`에서 호출
- ✅ 원본 Material 복원
- ✅ `_sharedMaterial = null` 초기화

---

### 5. 에디터 커스텀 인스펙터

#### 5.1 HasValuesChanged() - float 비교 시 허용 오차

```csharp
private bool HasValuesChanged()
{
    if (_selectedPreset == null) return false;

    // Color 비교 (RGBA)
    bool colorChanged = !ColorApproximately(_target.YourColor, _selectedPreset.YourColor);

    // Float 비교 (허용 오차 0.001f)
    bool valueChanged = Mathf.Abs(_target.YourValue - _selectedPreset.YourValue) > 0.001f;

    return colorChanged || valueChanged;
}

private bool ColorApproximately(Color a, Color b)
{
    return Mathf.Abs(a.r - b.r) < 0.001f &&
           Mathf.Abs(a.g - b.g) < 0.001f &&
           Mathf.Abs(a.b - b.b) < 0.001f &&
           Mathf.Abs(a.a - b.a) < 0.001f;
}
```

**체크 포인트:**
- ✅ `Mathf.Approximately()` 대신 `Mathf.Abs() > 0.001f` 사용
- ✅ Color는 RGBA 개별 채널 비교
- ✅ 부동소수점 오차 허용

#### 5.2 리셋 버튼 - 프리셋 값 우선

```csharp
if (GUILayout.Button("🔄 기본값으로 초기화", GUILayout.Height(25)))
{
    Undo.RecordObject(_target, "Reset Effect");

    // 프리셋이 선택되어 있으면 프리셋의 값으로 리셋
    if (_selectedPreset != null)
    {
        _target.ApplyPreset(_selectedPreset);
    }
    else
    {
        // 프리셋이 없으면 하드코딩된 기본값으로 리셋
        _target.ResetEffect();
    }

    EditorUtility.SetDirty(_target);
}
```

**체크 포인트:**
- ✅ 프리셋이 있으면 프리셋 값 적용
- ✅ 프리셋이 없으면 기본값 적용
- ✅ `Undo.RecordObject()` 호출

---

## 🐛 발견된 문제들과 해결 방법

### 문제 1: 리셋 버튼이 캐시된 값 사용

**증상:**
- "기본값으로 초기화" 버튼 클릭 시 프리셋 값이 아닌 이전 캐시 값이 적용됨

**원인:**
- `ResetEffect()` 메서드가 하드코딩된 기본값만 사용
- 선택된 프리셋(`_selectedPreset`)을 고려하지 않음

**해결:**
```csharp
if (_selectedPreset != null)
{
    _target.ApplyPreset(_selectedPreset);
}
else
{
    _target.ResetEffect();
}
```

---

### 문제 2: HasValuesChanged() float 비교 오차

**증상:**
- 값을 변경하지 않았는데도 "값이 변경되었습니다" 경고 표시
- Editor 테스트 실행 시 Dilate 값이 틀어짐

**원인:**
- `Mathf.Approximately()` 허용 오차가 너무 작음 (약 0.00001f)
- 부동소수점 연산 오차로 인한 미세한 차이

**해결:**
```csharp
// Mathf.Approximately() 대신
bool valueChanged = Mathf.Abs(_target.YourValue - _selectedPreset.YourValue) > 0.001f;
```

---

### 문제 3: Editor 테스트 후 자식 오브젝트 위치 어긋남

**증상:**
- TMPAnimation 에디터 테스트 버튼 클릭 후 자식 오브젝트(Inner Glow 등)의 위치가 최초 텍스트와 달라짐
- 런타임에서는 정상 동작

**원인:**
- `RestoreOriginalMesh()`에서 자식 오브젝트 메시를 복원하지 않음
- `ForceUpdateChild()` 호출만으로는 RectTransform이 복원되지 않음

**해결:**
1. TMPAnimation에 자식 전용 원본 메시 변수 추가 (`_originalVerticesChild`)
2. Play()에서 자식 원본 메시 저장
3. RestoreOriginalMesh()에서 자식 원본 메시 복원
4. `ForceUpdateChild()`에서 RectTransform 강제 리셋

```csharp
// ForceUpdateChild() 내부
childRect.anchorMin = Vector2.zero;
childRect.anchorMax = Vector2.one;
childRect.anchoredPosition = Vector2.zero;
childRect.sizeDelta = Vector2.zero;
childRect.localScale = Vector3.one;  // 중요!
```

---

### 문제 4: 자식 오브젝트 크기 변경

**증상:**
- Editor 테스트 후 자식 오브젝트가 살짝 커진 느낌

**원인:**
- TMPAnimation이 자식 오브젝트를 변환할 때 **부모의 원본 데이터(`_originalVertices`)를 사용**
- 자식은 별도의 TMP 오브젝트이므로 고유한 원본 메시 데이터 필요

**해결:**
- `TransformCharacterVerticesChild()` 전용 메서드 생성
- 자식 전용 원본 데이터 사용 (`_originalVerticesChild`, `_originalColorsChild`)

---

### 문제 5: InnerGlow 애니메이션 미작동 (Editor)

**증상:**
- 런타임에서는 정상 작동
- Editor 테스트에서만 InnerGlow가 애니메이션되지 않고 제자리에 남아있음

**원인:**
- TMPAnimation이 SecondFace만 처리하고 InnerGlow는 완전히 무시
- Play(), TransformCharacterVertices(), UpdateVertexData()에 InnerGlow 처리 누락

**해결:**
- TMPAnimation의 4곳에 InnerGlow 처리 추가:
  1. Play() - InnerGlow 초기화
  2. TransformCharacterVertices() - InnerGlow 정점 변환
  3. 첫 번째 UpdateVertexData() - 애니메이션 루프
  4. 두 번째 UpdateVertexData() - 전체 업데이트

---

## 📌 핵심 체크리스트 요약

### 컴포넌트 개발 시
- [ ] `[ExecuteAlways]`, `[RequireComponent]`, `[AddComponentMenu]` 속성
- [ ] `TMPEffect` 상속, `ITMPEffectSettings` 구현
- [ ] BitMask dirty checking
- [ ] TMPMaterialCache 사용
- [ ] Static Property ID 캐싱

### 자식 오브젝트 생성 시
- [ ] `HideFlags.NotEditable | HideFlags.DontSaveInEditor`
- [ ] RectTransform 완전히 채우기 (anchorMin/Max, sizeDelta=0, localScale=1)
- [ ] `raycastTarget = false`
- [ ] TMPCurve 복사
- [ ] TMPAnimation 복사

### TMPAnimation 통합 시 (가장 중요!)
- [ ] 원본 메시 저장 변수 추가 (`_originalVerticesChild`)
- [ ] Play()에서 초기화 + 원본 메시 저장
- [ ] TransformCharacterVertices()에 자식 변환 추가
- [ ] UpdateVertexData() 호출 (2곳 모두)
- [ ] RestoreOriginalMesh()에 복원 코드 추가
- [ ] `ForceUpdateChild()` 메서드 생성 (RectTransform 강제 리셋)

### 에디터 개발 시
- [ ] HasValuesChanged()에서 float 비교 시 허용 오차 0.001f
- [ ] Color 비교 시 RGBA 개별 채널 비교
- [ ] 리셋 버튼에서 프리셋 값 우선 적용

---

## 🎯 신규 컴포넌트 개발 순서

### Phase 1: 기본 구조 (1-2일)
1. 컴포넌트 클래스 생성 (TMPEffect 상속)
2. ITMPEffectSettings 구현
3. 기본 파라미터 프로퍼티 추가
4. TMPMaterialCache를 사용한 UpdateMaterial() 구현
5. 테스트: 단일 텍스트에 효과 적용

### Phase 2: 자식 오브젝트 (필요 시, 1일)
1. CreateChildObject() 구현
2. SyncChildObject() 구현
3. UpdateChildMaterial() 구현
4. 테스트: 자식 오브젝트 생성/동기화

### Phase 3: TMPAnimation 통합 (1-2일, 필수!)
1. TMPAnimation.cs에 원본 메시 변수 추가
2. Play()에 초기화 + 원본 메시 저장
3. TransformCharacterVertices()에 자식 변환 추가
4. UpdateVertexData() 호출 (2곳)
5. RestoreOriginalMesh()에 복원 코드 추가
6. ForceUpdateChild() 메서드 추가
7. 테스트: Editor 테스트 버튼 실행 후 완벽한 복원 확인

### Phase 4: 최적화 (1일)
1. BitMask dirty checking 구현
2. Static Property ID 캐싱
3. FNV-1a 알고리즘 GetMaterialHash() 구현
4. 테스트: 100개 이상 텍스트에서 Material 공유 검증

### Phase 5: 에디터 (1일)
1. 커스텀 인스펙터 생성
2. 실시간 프리뷰 슬라이더
3. Preset 관리 UI
4. HasValuesChanged() + 리셋 버튼
5. 테스트: 프리셋 저장/로드, 값 변경 감지

### Phase 6: 테스트 & 문서화 (1일)
1. 성능 테스트 (모바일 60fps 목표)
2. 메모리 프로파일링 (Material 개수 검증)
3. XML 문서화 주석 작성
4. README.md 업데이트
5. 사용 예제 작성

---

## 🔍 테스트 체크리스트

### 기본 기능
- [ ] 컴포넌트 추가/제거 정상 동작
- [ ] 프리셋 저장/로드 정상 동작
- [ ] Material 공유 확인 (100개 텍스트 → 5-10개 Material)
- [ ] 텍스트 내용 변경 시 자동 업데이트

### 자식 오브젝트 (있는 경우)
- [ ] 자식 오브젝트 자동 생성
- [ ] 부모 TMP 속성 완전 동기화
- [ ] Content Size Fitter와 호환
- [ ] TMPCurve와 호환

### TMPAnimation 통합
- [ ] Play() 후 정상 애니메이션
- [ ] Editor 테스트 버튼 클릭 후 완벽한 복원
- [ ] 자식 오브젝트도 함께 애니메이션
- [ ] 위치/크기/회전 모두 정확히 복원
- [ ] CanvasGroup alpha 정상 리셋

### 성능
- [ ] 100개 텍스트 @ 60 FPS (모바일)
- [ ] GC Alloc 0 (Unity Profiler)
- [ ] Material 개수 10개 이하
- [ ] 배칭 유지 (Frame Debugger)

### 에디터
- [ ] 값 변경 시 "값이 변경되었습니다" 경고 정확히 표시
- [ ] 리셋 버튼에서 프리셋 값 우선 적용
- [ ] float 비교 오차로 인한 오경보 없음

---

## 📚 참고 파일

**구현 참고:**
- `/Assets/Scripts/TMPEffects/TMPOutlineEffect.cs` - Second Face 패턴
- `/Assets/Scripts/TMPEffects/TMPOutGlow.cs` - Inner Glow 패턴
- `/Assets/Scripts/TMPEffects/TMPAnimation.cs` - 애니메이션 통합 패턴

**에디터 참고:**
- `/Assets/Scripts/TMPEffects/Editor/TMPOutlineEffectEditor.cs` - 커스텀 인스펙터 패턴
- `/Assets/Scripts/TMPEffects/Editor/TMPOutGlowEditor.cs` - HasValuesChanged, 리셋 버튼 패턴

**캐시 시스템:**
- `/Assets/Scripts/TMPEffects/TMPMaterialCache.cs` - Material 공유 시스템
- `/Assets/Scripts/TMPEffects/TMPEffectManager.cs` - Static Property ID 캐싱

---

**버전**: 2.9.0
**최종 수정**: 2026-02-12
**작성자**: Claude Code

이 가이드를 따르면 TMPAnimation과 완벽하게 통합되고, 성능이 최적화된 TMP Effect 컴포넌트를 개발할 수 있습니다.
