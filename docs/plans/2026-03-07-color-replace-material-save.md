# ColorReplace 머티리얼 에셋 저장 방식 전환

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** ColorReplace를 런타임 Material 생성 방식에서 에디터에서 Material 에셋을 직접 저장하는 방식으로 전환하여 빌드 시 셰이더 누락 방지

**Architecture:** ColorReplace 컴포넌트를 에디터 전용(`#if UNITY_EDITOR`)으로 전환. 에디터에서 HSV 옵션 조정 후 "머티리얼 저장" 버튼으로 Material 에셋을 생성/갱신하고 렌더러에 직접 할당. 런타임에는 컴포넌트가 아무 역할도 하지 않음.

**Tech Stack:** Unity 6, C#, UnityEditor API (AssetDatabase, SerializedObject)

---

### Task 1: ColorReplace.cs 에디터 전용으로 리팩터링

**Files:**
- Modify: `My project/Assets/Plugins/CAT/ColorReplace/Scripts/ColorReplace.cs`

**Step 1: ColorReplace.cs를 에디터 전용 컴포넌트로 전면 재작성**

런타임 코드(static cache, Initialize, Apply, Shader.Find 등)를 모두 제거하고, 에디터 프리뷰 + 머티리얼 저장 워크플로만 남긴다.

```csharp
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// HSV 색상 변환 에디터 전용 컴포넌트
    /// 에디터에서 HSV 옵션을 조정하고 머티리얼 에셋으로 저장하는 워크플로 도구
    /// 런타임에서는 저장된 머티리얼이 렌더러에 직접 할당되어 있으므로 이 컴포넌트는 불필요
    /// </summary>
    [AddComponentMenu("CAT/Effects/ColorReplace")]
    public class ColorReplace : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/ColorReplace";

        [Header("HSV Range")]
        [SerializeField, Range(0f, 1f)] private float _hsvRangeMin = 0f;
        public float HSVRangeMin
        {
            get => _hsvRangeMin;
            set => _hsvRangeMin = Mathf.Clamp01(value);
        }

        [SerializeField, Range(0f, 1f)] private float _hsvRangeMax = 1f;
        public float HSVRangeMax
        {
            get => _hsvRangeMax;
            set => _hsvRangeMax = Mathf.Clamp01(value);
        }

        [Header("HSV Adjust")]
        [SerializeField] private Vector4 _hsvAdjust = Vector4.zero;
        public Vector4 HSVAdjust
        {
            get => _hsvAdjust;
            set => _hsvAdjust = value;
        }

        // Shader Property ID 캐싱
        public static readonly int PropHSVRangeMin = Shader.PropertyToID("_HSVRangeMin");
        public static readonly int PropHSVRangeMax = Shader.PropertyToID("_HSVRangeMax");
        public static readonly int PropHSVAdjust = Shader.PropertyToID("_HSVAAdjust");
    }
}
```

**Step 2: 컴파일 확인**

Unity 에디터로 돌아가 콘솔에 컴파일 에러가 없는지 확인.

**Step 3: 커밋**

```bash
git add "My project/Assets/Plugins/CAT/ColorReplace/Scripts/ColorReplace.cs"
git commit -m "ColorReplace 런타임 코드 제거, 에디터 전용 컴포넌트로 전환"
```

---

### Task 2: ColorReplaceEditor.cs에 머티리얼 저장/갱신 기능 추가

**Files:**
- Modify: `My project/Assets/Plugins/CAT/ColorReplace/Editor/ColorReplaceEditor.cs`

**Step 1: 에디터 전면 재작성 — 프리뷰 + 저장/갱신 기능**

핵심 변경사항:
- 에디터 프리뷰용 임시 Material 관리 (HideFlags.HideAndDontSave)
- "머티리얼 저장" 버튼: Material 에셋 생성 → 렌더러에 할당
- 이미 저장된 머티리얼이면 "머티리얼 갱신" 버튼으로 표시
- 기본 저장 경로: `Assets/Plugins/CAT/ColorReplace/Materials/`

```csharp
using UnityEditor;
using UnityEngine;
using System.IO;

namespace CAT.Effects
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ColorReplace))]
    public class ColorReplaceEditor : Editor
    {
        private const string DEFAULT_SAVE_FOLDER = "Assets/Plugins/CAT/ColorReplace/Materials";

        private SerializedProperty hsvRangeMin;
        private SerializedProperty hsvRangeMax;
        private SerializedProperty hsvAdjust;

        private Color pickerColor = Color.red;
        private Material previewMaterial;   // 에디터 프리뷰용 임시 머티리얼
        private bool isPreviewing;

        private void OnEnable()
        {
            hsvRangeMin = serializedObject.FindProperty("_hsvRangeMin");
            hsvRangeMax = serializedObject.FindProperty("_hsvRangeMax");
            hsvAdjust = serializedObject.FindProperty("_hsvAdjust");

            if (hsvRangeMin != null && hsvRangeMax != null)
            {
                float midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue) * 0.5f;
                if (hsvRangeMin.floatValue > hsvRangeMax.floatValue)
                {
                    midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue + 1f) * 0.5f;
                    if (midHue > 1f) midHue -= 1f;
                }
                pickerColor = Color.HSVToRGB(midHue, 1f, 1f);
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var colorReplace = (ColorReplace)target;

            // 렌더러 타입 표시
            Component renderer = colorReplace.GetComponent<SpriteRenderer>();
            bool isUIComponent = renderer == null;
            if (isUIComponent)
            {
                renderer = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            }

            string rendererType = isUIComponent ? "UI Graphic" : "SpriteRenderer";
            EditorGUILayout.HelpBox($"Mode: {rendererType}", MessageType.None);

            // 현재 할당된 머티리얼 상태 표시
            Material assignedMaterial = GetAssignedMaterial(colorReplace);
            bool hasSavedMaterial = IsSavedColorReplaceMaterial(assignedMaterial);

            if (hasSavedMaterial)
            {
                string matPath = AssetDatabase.GetAssetPath(assignedMaterial);
                EditorGUILayout.HelpBox($"저장된 머티리얼: {matPath}", MessageType.Info);
            }

            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();

            // ─── Color Picker ───
            EditorGUILayout.LabelField("Color Picker", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            pickerColor = EditorGUILayout.ColorField(
                new GUIContent("Target Color", "이 색상을 기준으로 HSV 범위를 자동 설정합니다"),
                pickerColor
            );
            if (EditorGUI.EndChangeCheck())
            {
                SetHSVRangeFromColor(pickerColor);
            }
            EditorGUI.indentLevel--;

            // 범위 프리셋 버튼
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Compact", GUILayout.Width(80)))
                SetHSVRangeFromColor(pickerColor, 0.02f);
            if (GUILayout.Button("Similar", GUILayout.Width(80)))
                SetHSVRangeFromColor(pickerColor, 0.1f);
            if (GUILayout.Button("Wide", GUILayout.Width(80)))
                SetHSVRangeFromColor(pickerColor, 0.2f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ─── HSV Range ───
            EditorGUILayout.LabelField("HSV Range", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            DrawImprovedHSVDiagram(hsvRangeMin.floatValue, hsvRangeMax.floatValue);

            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            float newMinValue = EditorGUILayout.Slider(
                new GUIContent("Min", "HSV 범위 최소값 (0.0 ~ 1.0)"),
                hsvRangeMin.floatValue, 0f, 1f
            );
            float newMaxValue = EditorGUILayout.Slider(
                new GUIContent("Max", "HSV 범위 최대값 (0.0 ~ 1.0)"),
                hsvRangeMax.floatValue, 0f, 1f
            );

            EditorGUI.indentLevel--;

            if (Mathf.Abs(newMinValue - hsvRangeMin.floatValue) > 0.001f)
                hsvRangeMin.floatValue = newMinValue;
            if (Mathf.Abs(newMaxValue - hsvRangeMax.floatValue) > 0.001f)
                hsvRangeMax.floatValue = newMaxValue;

            EditorGUILayout.Space(10);

            // ─── HSV Adjust ───
            EditorGUILayout.LabelField("HSV Adjust", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Vector4 currentAdjust = hsvAdjust.vector4Value;
            currentAdjust.x = EditorGUILayout.Slider(new GUIContent("H", "색조(Hue) 조정"), currentAdjust.x, -1f, 1f);
            currentAdjust.y = EditorGUILayout.Slider(new GUIContent("S", "채도(Saturation) 조정"), currentAdjust.y, -1f, 1f);
            currentAdjust.z = EditorGUILayout.Slider(new GUIContent("V", "명도(Value) 조정"), currentAdjust.z, -1f, 1f);
            currentAdjust.w = EditorGUILayout.Slider(new GUIContent("A", "알파(Alpha) 조정"), currentAdjust.w, -1f, 1f);

            EditorGUI.indentLevel--;
            hsvAdjust.vector4Value = currentAdjust;

            bool valuesChanged = EditorGUI.EndChangeCheck();

            if (valuesChanged)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                if (PrefabUtility.IsPartOfAnyPrefab(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);

                // 에디터 프리뷰 갱신
                UpdatePreview(colorReplace);
            }

            EditorGUILayout.Space(10);

            // ─── 머티리얼 저장/갱신 버튼 ───
            EditorGUILayout.BeginHorizontal();

            if (hasSavedMaterial)
            {
                // 이미 저장된 머티리얼이 있으면 "갱신" 버튼
                if (GUILayout.Button("머티리얼 갱신", GUILayout.Height(30)))
                {
                    UpdateSavedMaterial(colorReplace, assignedMaterial);
                }
            }
            else
            {
                // 저장된 머티리얼이 없으면 "저장" 버튼
                if (GUILayout.Button("머티리얼 저장", GUILayout.Height(30)))
                {
                    SaveMaterial(colorReplace);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // ─── Reset / 프리뷰 토글 버튼 ───
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset"))
            {
                Undo.RecordObject(target, "Reset ColorReplace Values");
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                hsvAdjust.vector4Value = Vector4.zero;
                pickerColor = Color.red;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                UpdatePreview(colorReplace);
            }

            // 프리뷰 토글
            string previewLabel = isPreviewing ? "프리뷰 해제" : "프리뷰";
            if (GUILayout.Button(previewLabel))
            {
                if (isPreviewing)
                    CleanupPreview();
                else
                    UpdatePreview(colorReplace);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        // 머티리얼 저장/갱신
        // ─────────────────────────────────────────────

        /// <summary>
        /// 새 머티리얼 에셋 생성 후 렌더러에 할당
        /// </summary>
        private void SaveMaterial(ColorReplace colorReplace)
        {
            Shader shader = Shader.Find(ColorReplace.SHADER_NAME);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("오류", $"셰이더를 찾을 수 없습니다: {ColorReplace.SHADER_NAME}", "확인");
                return;
            }

            // 저장 폴더 확인 및 생성
            EnsureFolderExists(DEFAULT_SAVE_FOLDER);

            string objectName = colorReplace.gameObject.name;
            string sanitizedName = SanitizeFileName(objectName);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{DEFAULT_SAVE_FOLDER}/{sanitizedName}.mat"
            );

            // 머티리얼 생성
            Material material = new Material(shader);
            ApplyHSVToMaterial(material, colorReplace);
            material.name = sanitizedName;

            // 에셋 저장
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();

            // 렌더러에 할당
            AssignMaterialToRenderer(colorReplace, material);

            // 프리뷰 정리 (저장된 머티리얼이 할당되었으므로)
            CleanupPreviewMaterial();

            Debug.Log($"[ColorReplace] 머티리얼 저장 완료: {assetPath}");
            EditorGUIUtility.PingObject(material);
        }

        /// <summary>
        /// 기존 저장된 머티리얼의 HSV 값을 현재 설정으로 갱신
        /// </summary>
        private void UpdateSavedMaterial(ColorReplace colorReplace, Material savedMaterial)
        {
            Undo.RecordObject(savedMaterial, "Update ColorReplace Material");
            ApplyHSVToMaterial(savedMaterial, colorReplace);
            EditorUtility.SetDirty(savedMaterial);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ColorReplace] 머티리얼 갱신 완료: {AssetDatabase.GetAssetPath(savedMaterial)}");
        }

        /// <summary>
        /// 머티리얼에 HSV 프로퍼티 적용
        /// </summary>
        private void ApplyHSVToMaterial(Material material, ColorReplace colorReplace)
        {
            material.SetFloat(ColorReplace.PropHSVRangeMin, colorReplace.HSVRangeMin);
            material.SetFloat(ColorReplace.PropHSVRangeMax, colorReplace.HSVRangeMax);
            material.SetVector(ColorReplace.PropHSVAdjust, colorReplace.HSVAdjust);
        }

        // ─────────────────────────────────────────────
        // 에디터 프리뷰
        // ─────────────────────────────────────────────

        /// <summary>
        /// 에디터 프리뷰용 임시 머티리얼 생성/갱신
        /// </summary>
        private void UpdatePreview(ColorReplace colorReplace)
        {
            // 이미 저장된 머티리얼이 할당되어 있으면 프리뷰 대신 직접 갱신하지 않음
            // (갱신 버튼을 눌러야 저장된 머티리얼이 변경됨)
            Material assigned = GetAssignedMaterial(colorReplace);
            if (IsSavedColorReplaceMaterial(assigned))
            {
                // 저장된 머티리얼이 있으면 임시 프리뷰 머티리얼로 교체
                if (previewMaterial == null)
                {
                    Shader shader = Shader.Find(ColorReplace.SHADER_NAME);
                    if (shader == null) return;
                    previewMaterial = new Material(shader)
                    {
                        name = "ColorReplace Preview (Temp)",
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    // 원본 텍스처 복사
                    Texture tex = assigned.GetTexture("_MainTex");
                    if (tex != null)
                        previewMaterial.SetTexture("_MainTex", tex);
                }

                ApplyHSVToMaterial(previewMaterial, colorReplace);
                AssignMaterialToRenderer(colorReplace, previewMaterial);
                isPreviewing = true;
                return;
            }

            // 저장된 머티리얼이 없는 경우 프리뷰 머티리얼 생성
            if (previewMaterial == null)
            {
                Shader shader = Shader.Find(ColorReplace.SHADER_NAME);
                if (shader == null) return;
                previewMaterial = new Material(shader)
                {
                    name = "ColorReplace Preview (Temp)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            ApplyHSVToMaterial(previewMaterial, colorReplace);
            AssignMaterialToRenderer(colorReplace, previewMaterial);
            isPreviewing = true;
        }

        /// <summary>
        /// 프리뷰 해제 — 원래 머티리얼로 복원
        /// </summary>
        private void CleanupPreview()
        {
            if (!isPreviewing) return;

            var colorReplace = target as ColorReplace;
            if (colorReplace == null) return;

            // 저장된 머티리얼이 있으면 그것으로 복원, 없으면 기본 머티리얼로
            Material assigned = GetAssignedMaterial(colorReplace);
            if (assigned == previewMaterial)
            {
                // 프리뷰 중이었으므로 원래 머티리얼을 찾아서 복원
                // 저장된 ColorReplace 머티리얼이 있는지 확인
                // (프리뷰 전에 할당되어 있던 것을 기억하지 않으므로 기본 UI/Sprite 머티리얼로 복원)
                RestoreDefaultMaterial(colorReplace);
            }

            CleanupPreviewMaterial();
        }

        private void CleanupPreviewMaterial()
        {
            isPreviewing = false;
            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
                previewMaterial = null;
            }
        }

        // ─────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────

        /// <summary>
        /// 렌더러에서 현재 할당된 Material 가져오기
        /// </summary>
        private Material GetAssignedMaterial(ColorReplace colorReplace)
        {
            var spriteRenderer = colorReplace.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                return spriteRenderer.sharedMaterial;

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
                return graphic.material;

            return null;
        }

        /// <summary>
        /// 렌더러에 머티리얼 할당
        /// </summary>
        private void AssignMaterialToRenderer(ColorReplace colorReplace, Material material)
        {
            var spriteRenderer = colorReplace.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Undo.RecordObject(spriteRenderer, "Assign ColorReplace Material");
                spriteRenderer.sharedMaterial = material;
                EditorUtility.SetDirty(spriteRenderer);
                return;
            }

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                Undo.RecordObject(graphic, "Assign ColorReplace Material");
                graphic.material = material;
                EditorUtility.SetDirty(graphic);
            }
        }

        /// <summary>
        /// 기본 머티리얼로 복원
        /// </summary>
        private void RestoreDefaultMaterial(ColorReplace colorReplace)
        {
            var spriteRenderer = colorReplace.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = null;
                return;
            }

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                graphic.material = null;
            }
        }

        /// <summary>
        /// 할당된 머티리얼이 ColorReplace 셰이더를 사용하는 저장된 에셋인지 확인
        /// </summary>
        private bool IsSavedColorReplaceMaterial(Material material)
        {
            if (material == null) return false;
            if (!AssetDatabase.Contains(material)) return false;
            if (material.shader == null) return false;
            return material.shader.name == ColorReplace.SHADER_NAME;
        }

        /// <summary>
        /// 폴더가 없으면 재귀적으로 생성
        /// </summary>
        private void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>
        /// 파일명에 사용할 수 없는 문자 제거
        /// </summary>
        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        // ─────────────────────────────────────────────
        // HSV 다이어그램 (기존 유지)
        // ─────────────────────────────────────────────

        private void DrawImprovedHSVDiagram(float minRange, float maxRange)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            rect.height = 15;
            rect.x += 14;
            rect.width -= 8;

            DrawHSVBackground(rect);
            DrawRangeOverlay(rect, minRange, maxRange);
            DrawRangeHandles(rect, minRange, maxRange);
        }

        private void DrawHSVBackground(Rect rect)
        {
            int segments = 360;
            float segmentWidth = rect.width / segments;

            for (int i = 0; i < segments; i++)
            {
                float hue = (float)i / segments;
                Color color = Color.HSVToRGB(hue, 1f, 1f);

                Rect segmentRect = new Rect(
                    rect.x + i * segmentWidth,
                    rect.y,
                    segmentWidth + 1,
                    rect.height
                );

                EditorGUI.DrawRect(segmentRect, color);
            }
        }

        private void DrawRangeOverlay(Rect rect, float minRange, float maxRange)
        {
            Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            if (maxRange < minRange)
            {
                Rect middleOverlay = new Rect(
                    rect.x + rect.width * maxRange,
                    rect.y,
                    rect.width * (minRange - maxRange),
                    rect.height
                );
                EditorGUI.DrawRect(middleOverlay, overlayColor);
            }
            else
            {
                if (minRange > 0)
                {
                    Rect leftOverlay = new Rect(
                        rect.x, rect.y,
                        rect.width * minRange, rect.height
                    );
                    EditorGUI.DrawRect(leftOverlay, overlayColor);
                }
                if (maxRange < 1)
                {
                    Rect rightOverlay = new Rect(
                        rect.x + rect.width * maxRange, rect.y,
                        rect.width * (1f - maxRange), rect.height
                    );
                    EditorGUI.DrawRect(rightOverlay, overlayColor);
                }
            }
        }

        private void DrawRangeHandles(Rect rect, float minRange, float maxRange)
        {
            Color handleColor = Color.white;
            Color borderColor = Color.black;
            float handleWidth = 3f;

            float minX = rect.x + rect.width * minRange;
            Rect minHandle = new Rect(minX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(minHandle, borderColor);
            EditorGUI.DrawRect(new Rect(minX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);

            float maxX = rect.x + rect.width * maxRange;
            Rect maxHandle = new Rect(maxX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(maxHandle, borderColor);
            EditorGUI.DrawRect(new Rect(maxX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);
        }

        private void SetHSVRangeFromColor(Color selectedColor, float tolerance = 0.05f)
        {
            Color.RGBToHSV(selectedColor, out float hue, out _, out _);

            float minHue = hue - tolerance;
            float maxHue = hue + tolerance;

            if (minHue < 0f) minHue += 1f;
            if (maxHue > 1f) maxHue -= 1f;

            Undo.RecordObject(target, "Auto Set HSV Range");
            hsvRangeMin.floatValue = minHue;
            hsvRangeMax.floatValue = maxHue;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
#endif
}
```

**Step 2: 컴파일 확인**

Unity 에디터로 돌아가 콘솔에 컴파일 에러가 없는지 확인.

**Step 3: 동작 확인**

1. 씬에서 Image 또는 SpriteRenderer 오브젝트에 ColorReplace 추가
2. HSV 값 조정 후 "프리뷰" 버튼으로 실시간 확인
3. "머티리얼 저장" 클릭 → `Assets/Plugins/CAT/ColorReplace/Materials/` 에 .mat 파일 생성 확인
4. 렌더러의 Material 필드에 저장된 머티리얼이 할당되었는지 확인
5. HSV 값 변경 후 "머티리얼 갱신" 버튼으로 기존 머티리얼 업데이트 확인

**Step 4: 커밋**

```bash
git add "My project/Assets/Plugins/CAT/ColorReplace/Editor/ColorReplaceEditor.cs"
git commit -m "ColorReplaceEditor에 머티리얼 저장/갱신 기능 추가"
```

---

### Task 3: 빌드 안전성 최종 점검

**확인 항목:**

1. **셰이더 참조 체인**: 저장된 .mat 에셋 → Shader "CAT/Effects/ColorReplace" 직접 참조 → 빌드 시 셰이더가 머티리얼 의존성으로 자동 포함
2. **Shader.Find() 제거 확인**: 런타임 코드에 `Shader.Find()` 호출이 없는지 확인 (에디터 코드에서만 사용)
3. **Addressable 안전성**: 머티리얼 에셋이 셰이더를 직접 참조하므로 Addressable 번들에 셰이더가 자동 포함됨
4. **HideFlags 불필요**: 저장된 머티리얼은 정상 에셋이므로 HideFlags 불필요

**Step 1: 셰이더가 Always Included Shaders에 불필요한지 확인**

저장된 머티리얼이 셰이더를 직접 참조하므로, `Project Settings > Graphics > Always Included Shaders`에 수동 등록할 필요 없음.

**Step 2: 커밋**

```bash
git add -A
git commit -m "ColorReplace 머티리얼 에셋 저장 방식 전환 완료"
```
