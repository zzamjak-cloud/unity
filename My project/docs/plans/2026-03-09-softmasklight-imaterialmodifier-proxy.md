# SoftMaskLight IMaterialModifier 프록시 리팩토링 구현 계획

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** SoftMaskLight의 자식 머티리얼 관리를 `graphic.m_Material` 직접 교체 방식에서 `IMaterialModifier` 프록시 패턴으로 전환하여, 씬 저장/플레이모드 전환 시 원본 머티리얼 참조가 유실되는 문제를 근본적으로 해결한다.

**Architecture:** 자식 Graphic마다 `SoftMaskLightChildProxy` (IMaterialModifier) 컴포넌트를 추가하여 `materialForRendering`만 변경하고 `graphic.m_Material`은 건드리지 않는다. UIEffectSoftMaskLightProxy와 동일한 패턴이며, 배칭을 위해 SoftMaskLight가 공유 프록시 Material 캐시를 관리한다.

**Tech Stack:** Unity 6, C#, UGUI IMaterialModifier, HideFlags.HideAndDontSave

---

## 수정 대상 파일 요약

| 파일 | 작업 |
|------|------|
| `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLightChildProxy.cs` | **신규 생성** |
| `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs` | **대규모 수정** |
| `Assets/Plugins/CAT/ColorReplace/Editor/ColorReplaceEditor.cs` | **SoftMaskLight 특수 처리 제거** |

---

### Task 1: SoftMaskLightChildProxy 클래스 생성

**Files:**
- Create: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLightChildProxy.cs`

**Step 1: 파일 생성**

`UIEffectSoftMaskLightProxy.cs`와 동일한 구조. 핵심 차이: 셰이더를 교체하지 않고 키워드만 활성화하는 UIEffect 프록시와 달리, 이 프록시는 Hidden 변형 셰이더로 교체한다.

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace SoftMaskLight
{
    /// <summary>
    /// 일반 자식 Graphic에 SoftMaskLight 마스킹을 적용하기 위한 프록시 컴포넌트
    /// IMaterialModifier 체인에 삽입되어 원본 셰이더의 Hidden 변형(Optional Shader)으로
    /// 교체한 프록시 머티리얼을 반환. graphic.m_Material은 건드리지 않음.
    ///
    /// 동작 순서:
    /// 1. SoftMaskLight.ApplyMaskToChildren() → 자식에 이 프록시 추가 + Initialize()
    /// 2. Canvas 리빌드 → GetModifiedMaterial(baseMaterial) 호출
    /// 3. baseMaterial의 셰이더에 대응하는 Hidden 변형 셰이더를 찾고
    ///    SoftMaskLight의 공유 캐시에서 프록시 Material을 조회/생성
    /// 4. 마스크 프로퍼티 적용 후 프록시 Material 반환
    /// 5. materialForRendering = 프록시 머티리얼 (baseMaterial 유지)
    ///
    /// 배칭: 동일한 baseMaterial을 가진 자식끼리 SoftMaskLight의 공유 캐시를 통해
    /// 같은 프록시 Material을 공유 (GetOrCreateProxyMaterial)
    /// </summary>
    [ExecuteAlways]
    [HideInInspector]
    internal sealed class SoftMaskLightChildProxy : MonoBehaviour, IMaterialModifier
    {
        private SoftMaskLight _softMask;
        private Material _currentProxyMaterial; // GetModifiedMaterial에서 마지막으로 반환한 프록시 Material (프로퍼티 전파용)
        private bool _isCleanedUp;

        /// <summary>
        /// 마지막으로 반환한 프록시 머티리얼 참조
        /// SoftMaskLight.UpdateChildProxyMaterials()에서 프로퍼티 전파에 사용
        /// </summary>
        public Material ProxyMaterial => _currentProxyMaterial;

        /// <summary>
        /// Cleanup() 호출 후 Destroy 대기 중인 zombie 프록시 여부
        /// </summary>
        internal bool IsCleanedUp => _isCleanedUp;

        /// <summary>
        /// 연결된 SoftMaskLight 참조
        /// </summary>
        internal SoftMaskLight SoftMask => _softMask;

        /// <summary>
        /// SoftMaskLight.ApplyMaskToChildren()에서 호출하여 부모 SoftMaskLight 참조를 주입
        /// </summary>
        public void Initialize(SoftMaskLight mask)
        {
            _softMask = mask;
            _isCleanedUp = false;
        }

        /// <summary>
        /// GO 재활성화 시 IMaterialModifier 체인 재빌드 보장
        /// </summary>
        private void OnEnable()
        {
            if (_isCleanedUp || _softMask == null) return;

            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();
        }

        /// <summary>
        /// IMaterialModifier 구현
        /// baseMaterial(= graphic.m_Material)의 셰이더에 대응하는 Hidden 변형 셰이더를 찾고
        /// SoftMaskLight의 공유 캐시에서 프록시 Material을 생성/재사용
        /// </summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // 정리 완료 또는 SoftMaskLight 비활성 → 패스스루
            if (_isCleanedUp || _softMask == null || !_softMask.enabled)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            if (baseMaterial == null)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            // baseMaterial의 셰이더에 대응하는 Hidden 변형 셰이더 탐색
            Shader optShader = SoftMaskLight.FindOptionalShader(baseMaterial.shader);
            if (optShader == null)
            {
                _currentProxyMaterial = null;
                return baseMaterial;
            }

            // SoftMaskLight의 공유 캐시에서 프록시 Material 조회/생성 (배칭 보장)
            Material proxy = _softMask.GetOrCreateProxyMaterial(baseMaterial, optShader);
            _currentProxyMaterial = proxy;
            return proxy ?? baseMaterial;
        }

        /// <summary>
        /// SoftMaskLight.RestoreChildrenMaterials()에서 호출하여 프록시 정리 및 컴포넌트 제거
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;
            _currentProxyMaterial = null;

            var graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.SetMaterialDirty();

            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
        }

        private void OnDestroy()
        {
            _currentProxyMaterial = null;
        }
    }
}
```

**Step 2: 커밋**

```
git add "Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLightChildProxy.cs"
git commit -m "SoftMaskLightChildProxy 클래스 신규 생성 (IMaterialModifier 프록시)"
```

---

### Task 2: SoftMaskLight.cs — 데이터 구조 변경 + GetOrCreateProxyMaterial 추가

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs`

**Step 1: 새 데이터 구조 추가 및 기존 구조 교체**

**추가할 필드** (기존 `_customMaskMaterials` 등 선언 근처, 약 line 217~230):

```csharp
// SoftMaskLightChildProxy 목록 (UIEffect 프록시와 동일 구조)
private readonly List<SoftMaskLightChildProxy> _childProxies = new List<SoftMaskLightChildProxy>(8);

// 공유 프록시 Material 캐시: 원본 Material → 프록시 Material
// 동일한 원본 Material을 가진 자식끼리 프록시 Material을 공유 (배칭 유지)
private readonly Dictionary<Material, Material> _sharedProxyMaterials =
    new Dictionary<Material, Material>(4);

// UI/Default 등 기본 셰이더 자식의 프록시 Material 캐시
// baseMaterial이 defaultMaterial인 경우 Shader만으로 캐싱 (기존 _sharedOptionalMaterials 대체)
// → GetOrCreateProxyMaterial에서 baseMaterial == graphic.defaultMaterial 감지 시 사용
```

**제거할 필드** (더 이상 사용되지 않는 것):

```csharp
// 삭제: _sharedOptionalMaterials (line 167~169) → _sharedProxyMaterials로 대체
// 삭제: _customMaskMaterials (line 218) → 프록시가 관리
// 삭제: _customAppliedMaskMats (line 220~222) → 프록시가 관리
// 삭제: _sharedCustomClones (line 224~227) → _sharedProxyMaterials로 대체
// 삭제: _particleMaskMaterials (line 211) → 프록시가 관리
// 삭제: _particleAppliedMaskMats (line 213~215) → 프록시가 관리
```

**Step 2: GetOrCreateProxyMaterial 메서드 추가**

Material 관리 섹션 (약 line 890~990) 근처에 추가:

```csharp
/// <summary>
/// SoftMaskLightChildProxy에서 호출하여 공유 프록시 Material을 조회/생성
/// 동일한 baseMaterial을 가진 자식끼리 같은 프록시 Material을 공유 (배칭 유지)
/// baseMaterial의 프로퍼티를 복사하고 셰이더를 Hidden 변형으로 교체 + 마스크 프로퍼티 적용
/// </summary>
internal Material GetOrCreateProxyMaterial(Material baseMaterial, Shader optShader)
{
    if (baseMaterial == null || optShader == null) return null;

    // 동일한 baseMaterial에 대해 이미 프록시 Material이 있으면 공유
    if (_sharedProxyMaterials.TryGetValue(baseMaterial, out var existing) && existing != null)
        return existing;

    Material proxy = new Material(optShader)
    {
        name = $"{optShader.name} (SoftMaskLight: {gameObject.name})",
        hideFlags = HideFlags.HideAndDontSave
    };
    proxy.CopyPropertiesFromMaterial(baseMaterial);
    proxy.shader = optShader;

    // 마스크 프로퍼티 적용
    ApplyMaskPropertiesToMaterial(proxy);

    _sharedProxyMaterials[baseMaterial] = proxy;
    return proxy;
}
```

**Step 3: IsProxyMaterial 유틸리티 추가**

PropagateToStencilMaterials에서 프록시 Material 스킵용:

```csharp
/// <summary>
/// 해당 머티리얼이 자식 프록시 머티리얼인지 확인 (PropagateToStencilMaterials 스킵용)
/// </summary>
private bool IsChildProxyMaterial(Material mat)
{
    return _sharedProxyMaterials.ContainsValue(mat);
}
```

**Step 4: 커밋**

```
git commit -m "SoftMaskLight 데이터 구조 변경: 프록시 Material 캐시 추가"
```

---

### Task 3: SoftMaskLight.cs — ApplyMaskToChildren 리팩토링

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs:1329-1471`

**Step 1: ApplyMaskToChildren() 수정**

일반 Graphic + 커스텀 셰이더 + 파티클에서 `child.material = clone` 대신 `SoftMaskLightChildProxy` 추가.

`ApplyMaskToChildren()` (line 1329~1471)을 아래 로직으로 교체:

```csharp
public void ApplyMaskToChildren()
{
    if (!_initialized) return;

    Texture maskTex = GetMaskTexture();
    if (maskTex == null) return;

    GetComponentsInChildren(true, _childGraphicsBuffer);
    var children = _childGraphicsBuffer;
    foreach (var child in children)
    {
        if (child.gameObject == gameObject) continue;
        if (!BelongsToThisMask(child.transform)) continue;
        if (_originalChildMaterials.ContainsKey(child)) continue;

        // TMP_Text (TextMeshProUGUI 포함) — fontSharedMaterial 방식 유지
        if (child is TMP_Text tmpText)
        {
            // ... TMP 처리 (기존 코드 그대로 유지, line 1347~1374)
            Material originalFontMat = tmpText.fontSharedMaterial;
            Material backup = FindTMPOriginalBackup(child);
            if (backup != null && originalFontMat != backup)
            {
                originalFontMat = backup;
                tmpText.fontSharedMaterial = backup;
            }
            if (originalFontMat == null) continue;
            _originalChildMaterials[child] = originalFontMat;
            SaveTMPOriginalBackup(child, originalFontMat);
            Material tmpMat = CreateTMPMaskMaterial(originalFontMat);
            if (tmpMat != null)
            {
                tmpText.fontSharedMaterial = tmpMat;
                _tmpAppliedMaskMats[child] = tmpMat;
                child.SetAllDirty();
            }
            continue;
        }

        // UIEffect — 기존 UIEffectSoftMaskLightProxy 방식 유지
        if (IsUIEffectGraphic(child))
        {
            ApplyMaskToUIEffect(child);
            continue;
        }

        // TMP_SubMeshUI — fontSharedMaterial 방식 유지
        Material subOriginal = child.material;
        Material subBackup = FindTMPOriginalBackup(child);
        if (subBackup != null && subOriginal != subBackup)
        {
            subOriginal = subBackup;
            child.material = subBackup;
        }
        if (IsTMPMaterial(subOriginal))
        {
            _originalChildMaterials[child] = subOriginal;
            SaveTMPOriginalBackup(child, subOriginal);
            Material tmpMat = CreateTMPMaskMaterial(subOriginal);
            if (tmpMat != null)
            {
                child.material = tmpMat;
                _tmpAppliedMaskMats[child] = tmpMat;
                child.SetAllDirty();
            }
            continue;
        }

        // ─────────────────────────────────────────
        // 일반 Graphic + 커스텀 셰이더 + 파티클 → SoftMaskLightChildProxy
        // ─────────────────────────────────────────

        // 이미 유효한 프록시가 있으면 재사용
        var existingProxy = child.GetComponent<SoftMaskLightChildProxy>();
        if (existingProxy != null && !existingProxy.IsCleanedUp && existingProxy.SoftMask == this)
        {
            // 이미 이 SoftMaskLight의 프록시 → 등록만
            _originalChildMaterials[child] = null; // 프록시 관리 표시
            if (!_childProxies.Contains(existingProxy))
                _childProxies.Add(existingProxy);
            child.SetMaterialDirty();
            continue;
        }

        // 복제된 오브젝트 감지: graphic.material이 이미 프록시 Material인 경우
        // (Ctrl+D 등으로 복제 시) → 기본 material로 재설정은 불필요
        // IMaterialModifier 패턴에서는 graphic.m_Material이 항상 원본이므로 이 케이스 발생 안 함

        // Optional Shader 존재 확인 (없으면 마스킹 불가 → 스킵)
        Shader optShader = FindOptionalShader(child.material != null ? child.material.shader : null);
        if (optShader == null) continue;

        // 프록시 컴포넌트 생성 및 초기화
        if (existingProxy == null || existingProxy.IsCleanedUp)
            existingProxy = child.gameObject.AddComponent<SoftMaskLightChildProxy>();

        existingProxy.Initialize(this);

        if (!_childProxies.Contains(existingProxy))
            _childProxies.Add(existingProxy);

        // 프록시 관리 자식으로 등록 (원본 Material = null: 프록시가 관리)
        _originalChildMaterials[child] = null;

        // Canvas 재빌드 트리거 → GetModifiedMaterial() 호출 → 프록시 Material 생성
        child.SetMaterialDirty();
    }

    _checkUIEffectPending = true;
}
```

**주요 변경점:**
- 일반 Graphic: `child.material = optMat` → `AddComponent<SoftMaskLightChildProxy>()` + `child.SetMaterialDirty()`
- 커스텀 셰이더 (ColorReplace): `child.material = cloneMat` → 동일하게 프록시
- 파티클 (UIParticle): `child.material = particleMat` → 동일하게 프록시
- `_originalChildMaterials[child] = null` — 프록시 관리 표시 (UIEffect와 동일)
- `_originalChildMaterials[child] = originalMat` — TMP만 기존 방식 유지

**Step 2: 커밋**

```
git commit -m "ApplyMaskToChildren: 일반 Graphic/커스텀/파티클을 IMaterialModifier 프록시로 전환"
```

---

### Task 4: SoftMaskLight.cs — RestoreChildrenMaterials / RestoreSingleChild 수정

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs:1476-1614`

**Step 1: RestoreSingleChild 수정**

프록시 관리 자식 (`originalMat == null`)은 `SoftMaskLightChildProxy.Cleanup()` 호출:

```csharp
private void RestoreSingleChild(UnityEngine.UI.Graphic child, Material originalMat)
{
    if (child == null) return;

    // 프록시 관리 자식 (originalMat == null): UIEffect 또는 일반 프록시
    if (originalMat == null)
    {
        // UIEffect 프록시 제거
        var uiProxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
        if (uiProxy != null && !uiProxy.IsCleanedUp)
            uiProxy.Cleanup();

        // 일반 프록시 제거
        var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
        if (childProxy != null && !childProxy.IsCleanedUp)
            childProxy.Cleanup();
        else
            child.SetMaterialDirty();
        return;
    }

    // TMP_Text는 fontSharedMaterial로 복원
    if (child is TMP_Text tmpText)
        tmpText.fontSharedMaterial = originalMat;
    else
        child.material = originalMat;

    // TMP 마스크 Material 정리
    if (_tmpAppliedMaskMats.TryGetValue(child, out var tmpMat))
    {
        if (tmpMat != null) { if (Application.isPlaying) Destroy(tmpMat); else DestroyImmediate(tmpMat); }
        _tmpMaskMaterials.Remove(tmpMat);
        _tmpAppliedMaskMats.Remove(child);
    }
}
```

**Step 2: RestoreChildrenMaterials 수정**

파티클/커스텀 Material 정리 삭제, 프록시 정리 추가:

```csharp
public void RestoreChildrenMaterials()
{
    foreach (var kvp in _originalChildMaterials)
    {
        if (kvp.Key == null) continue;

        // 프록시 관리 자식 (값 == null): UIEffect 또는 일반 프록시
        if (kvp.Value == null)
        {
            kvp.Key.SetMaterialDirty();
            continue;
        }

        // TMP_Text는 fontSharedMaterial로 복원
        if (kvp.Key is TMP_Text tmpText)
            tmpText.fontSharedMaterial = kvp.Value;
        else
            kvp.Key.material = kvp.Value;
    }

    _originalChildMaterials.Clear();

    // TMP Material 파괴
    for (int i = 0; i < _tmpMaskMaterials.Count; i++)
    {
        if (_tmpMaskMaterials[i] != null)
        {
            if (Application.isPlaying) Destroy(_tmpMaskMaterials[i]);
            else DestroyImmediate(_tmpMaskMaterials[i]);
        }
    }
    _tmpMaskMaterials.Clear();
    _tmpAppliedMaskMats.Clear();

    // 공유 프록시 Material 파괴
    foreach (var mat in _sharedProxyMaterials.Values)
    {
        if (mat != null)
        {
            if (Application.isPlaying) Destroy(mat);
            else DestroyImmediate(mat);
        }
    }
    _sharedProxyMaterials.Clear();

    // SoftMaskLightChildProxy 컴포넌트 정리
    for (int i = 0; i < _childProxies.Count; i++)
    {
        if (_childProxies[i] != null)
            _childProxies[i].Cleanup();
    }
    _childProxies.Clear();

    // UIEffect 프록시 컴포넌트 정리
    for (int i = 0; i < _uiEffectProxies.Count; i++)
    {
        if (_uiEffectProxies[i] != null)
            _uiEffectProxies[i].Cleanup();
    }
    _uiEffectProxies.Clear();
}
```

**Step 3: 커밋**

```
git commit -m "RestoreChildrenMaterials/RestoreSingleChild: 프록시 기반 정리로 전환"
```

---

### Task 5: SoftMaskLight.cs — UpdateSharedMaterial 수정

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs:997-1150`

**Step 1: `_sharedOptionalMaterials` → `_sharedProxyMaterials` 교체**

`UpdateSharedMaterial()` 내 모든 `_sharedOptionalMaterials.Values` 순회를 `_sharedProxyMaterials.Values`로 교체.

변경 패턴 (모든 곳에 동일 적용):
```
// 전: foreach (var m in _sharedOptionalMaterials.Values)
// 후: foreach (var m in _sharedProxyMaterials.Values)
```

**Step 2: 빈 체크 조건 수정** (line 1000)

```csharp
// 전:
if (_sharedOptionalMaterials.Count == 0 && _tmpMaskMaterials.Count == 0 && _particleMaskMaterials.Count == 0 && _customMaskMaterials.Count == 0 && _uiEffectProxies.Count == 0) return;

// 후:
if (_sharedProxyMaterials.Count == 0 && _tmpMaskMaterials.Count == 0 && _childProxies.Count == 0 && _uiEffectProxies.Count == 0) return;
```

**Step 3: anyChange 전파 섹션 수정** (line 1136~1144)

```csharp
// 전:
if (anyChange || _materialDirty)
{
    UpdateTMPMaterials();
    UpdateParticleMaterials();
    UpdateCustomMaterials();
    UpdateUIEffectMaterials();
    PropagateToStencilMaterials();
}

// 후:
if (anyChange || _materialDirty)
{
    UpdateTMPMaterials();
    UpdateChildProxyMaterials();
    UpdateUIEffectMaterials();
    PropagateToStencilMaterials();
}
```

**Step 4: UpdateChildProxyMaterials 메서드 추가**

```csharp
/// <summary>
/// 자식 프록시 머티리얼에 현재 마스크 프로퍼티 일괄 전파
/// (캔버스 리빌드 없이 마스크 프로퍼티만 변경된 경우 대응)
/// </summary>
private void UpdateChildProxyMaterials()
{
    if (_childProxies.Count == 0) return;

    for (int i = _childProxies.Count - 1; i >= 0; i--)
    {
        var proxy = _childProxies[i];
        if (proxy == null)
        {
            _childProxies.RemoveAt(i);
            continue;
        }

        Material mat = proxy.ProxyMaterial;
        if (mat == null) continue;

        ApplyMaskPropertiesToMaterial(mat);
    }
}
```

**Step 5: 커밋**

```
git commit -m "UpdateSharedMaterial: _sharedProxyMaterials로 전환 + UpdateChildProxyMaterials 추가"
```

---

### Task 6: SoftMaskLight.cs — PropagateToStencilMaterials / InvalidateChild / OnTransformParentChanged 수정

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs`

**Step 1: PropagateToStencilMaterials 수정** (line 1157~)

프록시 Material 스킵 조건 변경:

```csharp
// 전:
if (IsSharedOptionalMaterial(rendered)) continue;
if (_tmpMaskMaterials.Contains(rendered)) continue;
if (_particleMaskMaterials.Contains(rendered)) continue;
if (_customMaskMaterials.Contains(rendered)) continue;
if (IsUIEffectProxyMaterial(rendered)) continue;

// 후:
if (IsChildProxyMaterial(rendered)) continue;
if (_tmpMaskMaterials.Contains(rendered)) continue;
if (IsUIEffectProxyMaterial(rendered)) continue;
```

**Step 2: InvalidateChild 수정** (line 1306~1324)

커스텀 Material 정리 코드 제거, 프록시 정리로 교체:

```csharp
public void InvalidateChild(UnityEngine.UI.Graphic child)
{
    if (child == null) return;

    // 프록시 컴포넌트 제거
    var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
    if (childProxy != null && !childProxy.IsCleanedUp)
    {
        _childProxies.Remove(childProxy);
        childProxy.Cleanup();
    }

    _originalChildMaterials.Remove(child);
    ApplyMaskToChildren();
}
```

**Step 3: OnTransformParentChanged 수정** (line 464~475)

`_sharedOptionalMaterials` → `_sharedProxyMaterials`:

```csharp
// 전:
foreach (var mat in _sharedOptionalMaterials.Values)

// 후:
foreach (var mat in _sharedProxyMaterials.Values)
```

**Step 4: IsSharedOptionalMaterial 제거, IsChildProxyMaterial로 교체**

`IsSharedOptionalMaterial()` (line 1263~1268) 제거. 대신:

```csharp
private bool IsChildProxyMaterial(Material mat)
{
    return _sharedProxyMaterials.ContainsValue(mat);
}
```

**Step 5: 커밋**

```
git commit -m "PropagateToStencilMaterials/InvalidateChild/OnTransformParentChanged: 프록시 기반으로 전환"
```

---

### Task 7: SoftMaskLight.cs — 불필요 코드 대규모 삭제

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs`

**Step 1: LateUpdate에서 DetectParticleMaterialChanges 호출 제거** (line 344)

```csharp
// 삭제: DetectParticleMaterialChanges();
```

**Step 2: 파티클 관련 메서드 전체 삭제**

- `IsParticleMaterial()` (line 1862~1866)
- `CreateParticleMaskMaterial()` (line 1873~1891)
- `DetectParticleMaterialChanges()` (line 2010~2086)
- `UpdateParticleMaterials()` (line 2091~2103)

**Step 3: 커스텀 셰이더 관련 메서드 전체 삭제**

- `GetOrCreateSharedCustomMaterial()` (line 1899~1920)
- `IsCustomCloneInUse()` (line 1925~1932)
- `FindOriginalForCustomClone()` (line 1939~1959)
- `RemoveFromSharedCustomClones()` (line 1964~1973)
- `CreateCustomMaskMaterial()` (line 1975~1991)
- `UpdateCustomMaterials()` (line 1996~2003)

**Step 4: GetOrCreateOptionalMaterial 제거** (line 898~990)

`GetOrCreateProxyMaterial()`로 대체되었으므로 삭제.

**Step 5: 씬 저장 콜백 코드 전체 삭제** (line 2475~2595)

- `s_activeInstances`, `s_sceneSaveCallbackRegistered`
- `RegisterSceneSaveCallback()`, `UnregisterSceneSaveCallback()`
- `OnSceneSaving()`, `OnSceneSaved()`
- `SwapToOriginals()`, `SwapToClones()`

**Step 6: Initialize()에서 RegisterSceneSaveCallback 호출 제거** (line 334)

```csharp
// 삭제:
#if UNITY_EDITOR
    RegisterSceneSaveCallback();
#endif
```

**Step 7: OnDisable() / OnDestroy()에서 UnregisterSceneSaveCallback 호출 제거** (line 401~403, 417~419)

```csharp
// 삭제:
#if UNITY_EDITOR
    UnregisterSceneSaveCallback();
#endif
```

**Step 8: GetOriginalMaterial 제거** (line 2468~2473)

IMaterialModifier 패턴에서는 `graphic.material`이 항상 원본이므로 불필요.

**Step 9: CheckForChildChanges 수정** (line 508~568)

복제 오브젝트 감지 관련 `IsSharedOptionalMaterial` / `FindOriginalForCustomClone` 호출 제거.
일반 Graphic→UIEffect 전환 감지 로직도 프록시 기반으로 조정:

```csharp
// line 551~558: 기존 일반 Graphic → UIEffect 추가 감지
// origMat이 null인 경우(프록시 관리)에도 UIEffect 전환 감지 필요
if (_originalChildMaterials.TryGetValue(child, out var origMat) && IsUIEffectGraphic(child))
{
    var existingUIProxy = child.GetComponent<UIEffectSoftMaskLightProxy>();
    if (existingUIProxy == null || existingUIProxy.IsCleanedUp)
    {
        // 일반 프록시 제거 후 UIEffect 프록시로 전환
        var childProxy = child.GetComponent<SoftMaskLightChildProxy>();
        if (childProxy != null && !childProxy.IsCleanedUp)
        {
            _childProxies.Remove(childProxy);
            childProxy.Cleanup();
        }
        _originalChildMaterials.Remove(child);
        ApplyMaskToUIEffect(child);
        continue;
    }
}
```

**Step 10: 커밋**

```
git commit -m "SoftMaskLight: 파티클/커스텀/씬저장 콜백 등 불필요 코드 대규모 삭제"
```

---

### Task 8: ColorReplaceEditor.cs — SoftMaskLight 특수 처리 제거

**Files:**
- Modify: `Assets/Plugins/CAT/ColorReplace/Editor/ColorReplaceEditor.cs`

**Step 1: OnInspectorGUI에서 SoftMaskLight 원본 표시 로직 제거** (line 67~80)

```csharp
// 전:
Material currentMat = GetRendererMaterial(colorReplace);
Material displayMat = currentMat;
var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
if (graphic != null && !IsColorReplaceAsset(currentMat))
{
    var sml = colorReplace.GetComponentInParent<SoftMaskLight.SoftMaskLight>();
    if (sml != null)
    {
        Material original = sml.GetOriginalMaterial(graphic);
        if (original != null)
            displayMat = original;
    }
}

// 후:
Material currentMat = GetRendererMaterial(colorReplace);
Material displayMat = currentMat;
```

**Step 2: ApplyToMaterial에서 SoftMaskLight 원본 동기화 제거** (line 281~290)

```csharp
// 삭제:
// SoftMaskLight 환경: 원본 Material에도 값 적용
var sml = colorReplace.GetComponentInParent<SoftMaskLight.SoftMaskLight>();
if (sml != null)
{
    Material original = sml.GetOriginalMaterial(graphic);
    if (original != null && original != mat)
        ApplyHSVToMaterial(original, colorReplace);
}
```

단, `ApplyToMaterial()`에서 IMaterialModifier로 인해 CanvasRenderer의 렌더링 Material에도 직접 프로퍼티를 적용해야 함. Mask/SoftMask 환경 코드 (line 271~279)는 유지. 추가로 SoftMaskLight 환경에서도 canvasRenderer에서 잡히므로 동일하게 동작.

추가: `graphic.SetMaterialDirty()` 호출 추가하여 다음 캔버스 리빌드에서 프록시가 baseMaterial의 최신 값을 `CopyPropertiesFromMaterial`로 반영하도록 보장:

```csharp
// Mask/SoftMask/SoftMaskLight 환경: 렌더링 머티리얼에 프로퍼티 직접 적용
var cr = graphic.canvasRenderer;
if (cr != null)
{
    Material canvasMat = cr.GetMaterial(0);
    if (canvasMat != null && canvasMat != mat)
        ApplyHSVToMaterial(canvasMat, colorReplace);
}
// baseMaterial 변경을 프록시에 전파하기 위해 캔버스 재빌드 트리거
graphic.SetMaterialDirty();
```

**Step 3: EnsureColorReplaceMaterial에서 InvalidateChild 호출 제거** (line 334~350)

```csharp
// 전: (line 334~350 전체)
// SoftMaskLight 환경: Temp 머티리얼로 교체 후 마스크 재적용 요청
var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
if (graphic != null)
{
    var sml = colorReplace.GetComponentInParent<SoftMaskLight.SoftMaskLight>();
    if (sml != null)
    {
        sml.InvalidateChild(graphic);
        Material currentAfter = GetRendererMaterial(colorReplace);
        if (currentAfter != null && ColorReplace.IsColorReplaceShader(currentAfter.shader))
            return currentAfter;
    }
}

// 후: (IMaterialModifier가 자동 처리하므로 캔버스 리빌드만 트리거)
var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
if (graphic != null)
    graphic.SetMaterialDirty();
```

**Step 4: SaveAsNewMaterial에서 InvalidateChild 호출 제거** (line 395~402)

```csharp
// 전:
var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
if (graphic != null)
{
    var sml = colorReplace.GetComponentInParent<SoftMaskLight.SoftMaskLight>();
    if (sml != null)
        sml.InvalidateChild(graphic);
}

// 후:
var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
if (graphic != null)
    graphic.SetMaterialDirty();
```

**Step 5: 주석 정리**

SoftMaskLight 관련 주석 제거/업데이트 (line 67, 192, 206, 242, 255, 306, 314, 334, 378, 395 등).

**Step 6: 커밋**

```
git commit -m "ColorReplaceEditor: SoftMaskLight 특수 처리 제거 (IMaterialModifier 자동 처리)"
```

---

### Task 9: SoftMaskLight.cs — ApplyMaskToChildren 내 복제 오브젝트 감지 로직 정리

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs`

**Step 1: 기존 복제 감지 코드 제거**

ApplyMaskToChildren의 기존 코드 (line 1388~1402):
```csharp
// 삭제: 복제된 오브젝트 감지 (IMaterialModifier 패턴에서는 불필요)
if (IsSharedOptionalMaterial(originalMat))
{
    _originalChildMaterials[child] = child.defaultMaterial;
    child.SetAllDirty();
    continue;
}
Material realOriginal = FindOriginalForCustomClone(originalMat);
if (realOriginal != null)
{
    originalMat = realOriginal;
}
```

IMaterialModifier 패턴에서는 `graphic.m_Material`이 항상 원본이므로 복제 감지가 불필요.
`Ctrl+D`로 복제해도 `graphic.m_Material`은 원본 Material 에셋을 참조하고, 프록시는 새로 추가됨.

**Step 2: 커밋**

```
git commit -m "ApplyMaskToChildren: 복제 오브젝트 감지 로직 제거 (IMaterialModifier로 불필요)"
```

---

### Task 10: SoftMaskLight.cs — 프록시 baseMaterial 변경 감지

**Files:**
- Modify: `Assets/Plugins/CAT/SoftMaskLight/Scripts/SoftMaskLight.cs`

**Step 1: DetectChildProxyMaterialChanges 추가**

에디터에서 사용자가 `graphic.material`을 외부에서 변경한 경우 (예: 인스펙터에서 다른 Material 드래그),
프록시의 `GetModifiedMaterial`이 새 baseMaterial로 호출되므로 `_sharedProxyMaterials` 캐시에
새 항목이 필요할 수 있음. 이는 `GetOrCreateProxyMaterial`에서 자동 처리됨.

단, 기존 프록시 Material에서 낡은 캐시 항목이 남을 수 있으므로 `_sharedProxyMaterials`의
key Material이 파괴되었거나 더 이상 자식에서 사용되지 않는 항목을 정리:

```csharp
/// <summary>
/// 파괴된 Material 키를 가진 공유 프록시 캐시 항목 정리
/// </summary>
private void CleanupStaleProxyMaterials()
{
    _toRemoveMaterials.Clear();
    foreach (var kvp in _sharedProxyMaterials)
    {
        if (kvp.Key == null || kvp.Value == null)
            _toRemoveMaterials.Add(kvp.Key);
    }
    for (int i = 0; i < _toRemoveMaterials.Count; i++)
    {
        if (_sharedProxyMaterials.TryGetValue(_toRemoveMaterials[i], out var mat) && mat != null)
        {
            if (Application.isPlaying) Destroy(mat);
            else DestroyImmediate(mat);
        }
        _sharedProxyMaterials.Remove(_toRemoveMaterials[i]);
    }
}
```

GC 방지용 `_toRemoveMaterials` 리스트 추가:
```csharp
private readonly List<Material> _toRemoveMaterials = new List<Material>(4);
```

`CleanupDestroyedChildren()` 또는 `UpdateSharedMaterial()` 끝에서 호출.

**Step 2: 커밋**

```
git commit -m "SoftMaskLight: 프록시 Material 캐시 정리 유틸리티 추가"
```

---

### Task 11: 최종 점검 — 컴파일 오류 해결 및 Windable/UIShining 호환성 확인

**Files:**
- Read: `Assets/Plugins/CAT/Windable/Scripts/Windable.cs`
- Read: `Assets/Plugins/CAT/UIShining/Scripts/UIShining.cs`
- Modify: SoftMaskLight.cs (필요시)

**Step 1: Windable/UIShining의 ActiveMaterial 패턴 확인**

이 컴포넌트들은 `_graphic.material`과 `canvasRenderer.GetMaterial(0)`을 비교하여 렌더링 Material을 얻음.
IMaterialModifier 전환 후:
- `_graphic.material` = 원본 Material (변경 없음)
- `canvasRenderer.GetMaterial(0)` = 프록시 Material (Hidden 변형)

기존 `ActiveMaterial` 패턴이 그대로 동작하는지 확인. 프록시가 `CopyPropertiesFromMaterial(baseMaterial)`을 호출하므로 원본에 설정된 값이 프록시로 전파됨. 단, 프록시의 `GetModifiedMaterial`이 캔버스 리빌드 시에만 호출되므로, per-frame 프로퍼티 갱신은 `ActiveMaterial` 패턴으로 직접 프록시 Material에 적용해야 함 → **기존 동작과 동일**.

**Step 2: Windable의 SetupMaterial에서 InvalidateChild 호출 확인**

```csharp
// Windable.cs line 250~253
var sml = GetComponentInParent<SoftMaskLight.SoftMaskLight>();
if (sml != null) sml.InvalidateChild(_graphic);
```

이 호출은 여전히 필요 (Windable이 `_graphic.material`을 새 Material로 교체하므로, 프록시가 새 baseMaterial로 재빌드해야 함).
`InvalidateChild()`가 프록시를 Cleanup→재생성하므로 문제 없음.

**Step 3: 전체 컴파일 확인**

SoftMaskLight.cs에서 삭제된 메서드/필드를 참조하는 코드가 남아있지 않은지 확인:
- `_sharedOptionalMaterials` 참조 → 전부 `_sharedProxyMaterials`로 교체 확인
- `_customMaskMaterials` / `_customAppliedMaskMats` / `_sharedCustomClones` 참조 → 전부 삭제 확인
- `_particleMaskMaterials` / `_particleAppliedMaskMats` 참조 → 전부 삭제 확인
- `GetOriginalMaterial()` 호출 → ColorReplaceEditor에서 제거 확인
- `IsSharedOptionalMaterial()` 호출 → `IsChildProxyMaterial()`로 교체 확인

**Step 4: 커밋**

```
git commit -m "최종 점검: 컴파일 오류 해결 및 호환성 확인"
```

---

## 검증 방법

1. **기본 마스킹**: SoftMaskLight 자식 Image/RawImage가 마스킹됨
2. **ColorReplace**: SoftMaskLight 하위 ColorReplace 에셋 Material이 인스펙터에 정상 표시, 에디터에서 HSV 변경 즉시 반영
3. **씬 저장/로드**: SoftMaskLight 자식의 `graphic.material`이 저장된 에셋 Material 유지 (Temp로 변경 안 됨)
4. **플레이모드 진입/종료**: Material 참조 유실 없음
5. **오브젝트 복제 (Ctrl+D)**: 복제된 오브젝트도 정상 마스킹 + 원본 Material 유지
6. **동일 Material 공유**: 같은 Material을 사용하는 여러 자식이 동일 프록시 Material 공유 (배칭)
7. **Windable/UIShining**: SoftMaskLight 하위에서 per-frame 프로퍼티 갱신 정상 동작
8. **TMP**: fontSharedMaterial 기반 마스킹 기존대로 동작
9. **UIEffect**: UIEffectSoftMaskLightProxy 기존대로 동작
10. **중첩 마스크**: 부모-자식 SoftMaskLight 2단계 마스킹 동작
11. **인스펙터 선택**: SoftMaskLight 자식 오브젝트 선택 시 마스크 해제 안 됨
