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

            // 저장된 머티리얼이면 머티리얼 값을 컴포넌트에 동기화
            // 임시 머티리얼이면 컴포넌트 값을 머티리얼에 적용
            SyncOnEnable();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            ApplyToMaterial();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var colorReplace = (ColorReplace)target;

            // 렌더러 타입 표시
            var spriteRenderer = colorReplace.GetComponent<SpriteRenderer>();
            bool isUIComponent = spriteRenderer == null;
            string rendererType = isUIComponent ? "UI Graphic" : "SpriteRenderer";
            EditorGUILayout.HelpBox($"Mode: {rendererType}", MessageType.None);

            // 현재 머티리얼 상태 표시
            Material currentMat = GetRendererMaterial(colorReplace);
            Material displayMat = currentMat;
            bool isSavedAsset = IsColorReplaceAsset(displayMat);

            if (isSavedAsset)
            {
                string matPath = AssetDatabase.GetAssetPath(displayMat);
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

                // 값 변경 즉시 머티리얼 프로퍼티 갱신
                ApplyToMaterial();
            }

            EditorGUILayout.Space(10);

            // ─── 신규 머티리얼 생성 버튼 ───
            if (GUILayout.Button("신규 머티리얼 생성", GUILayout.Height(30)))
            {
                SaveAsNewMaterial(colorReplace);
            }

        }

        // ─────────────────────────────────────────────
        // 머티리얼 동기화
        // ─────────────────────────────────────────────

        /// <summary>
        /// 에디터 선택 시 동기화:
        /// - 유효한 ColorReplace 머티리얼(에셋, SoftMaskLight 공유 clone 포함)
        ///   → 머티리얼 값을 컴포넌트로 읽어옴 (머티리얼이 진실의 원천)
        /// - 머티리얼 없음 또는 ColorReplace 셰이더가 아닌 경우
        ///   → 컴포넌트 값을 머티리얼에 적용
        /// </summary>
        private void SyncOnEnable()
        {
            var colorReplace = target as ColorReplace;
            if (colorReplace == null) return;
            if (!colorReplace.gameObject.scene.IsValid()) return;

            Material mat = GetRendererMaterial(colorReplace);

            // 유효한 ColorReplace 머티리얼이면 머티리얼에서 값을 읽어옴
            // (저장된 에셋 또는 SoftMaskLight 공유 clone 모두 해당)
            if (IsValidColorReplaceMaterial(mat))
            {
                SyncFromMaterial(mat);
            }
            else
            {
                // 머티리얼이 없거나 ColorReplace 셰이더가 아니면 컴포넌트 값을 적용
                ApplyToMaterial();
            }
        }

        /// <summary>
        /// 머티리얼의 HSV 값을 컴포넌트에 동기화
        /// </summary>
        private void SyncFromMaterial(Material mat)
        {
            serializedObject.Update();
            hsvRangeMin.floatValue = mat.GetFloat(ColorReplace.PropHSVRangeMin);
            hsvRangeMax.floatValue = mat.GetFloat(ColorReplace.PropHSVRangeMax);
            Vector4 adj = mat.GetVector(ColorReplace.PropHSVAdjust);
            hsvAdjust.vector4Value = adj;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            // pickerColor도 갱신
            float midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue) * 0.5f;
            if (hsvRangeMin.floatValue > hsvRangeMax.floatValue)
            {
                midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue + 1f) * 0.5f;
                if (midHue > 1f) midHue -= 1f;
            }
            pickerColor = Color.HSVToRGB(midHue, 1f, 1f);
        }

        /// <summary>
        /// ColorReplace 셰이더를 사용하는 유효한 머티리얼인지 확인
        /// 저장된 에셋뿐 아니라 SoftMaskLight 공유 clone도 포함
        /// </summary>
        private bool IsValidColorReplaceMaterial(Material material)
        {
            if (material == null) return false;
            return ColorReplace.IsColorReplaceShader(material.shader);
        }

        /// <summary>
        /// 렌더러의 머티리얼에 현재 HSV 값을 적용 (머티리얼이 없으면 생성)
        /// Unity Mask / mob-sakai SoftMask / SoftMaskLight 환경에서는
        /// CanvasRenderer의 최종 렌더링 머티리얼에도 직접 프로퍼티를 적용하고
        /// SetMaterialDirty()로 IMaterialModifier 체인 재빌드를 트리거한다.
        /// </summary>
        private void ApplyToMaterial()
        {
            var colorReplace = target as ColorReplace;
            if (colorReplace == null) return;
            if (!colorReplace.gameObject.scene.IsValid()) return;

            Material mat = EnsureColorReplaceMaterial(colorReplace);
            if (mat == null) return;

            ApplyHSVToMaterial(mat, colorReplace);

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
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
            }
        }

        /// <summary>
        /// HSV 프로퍼티를 머티리얼에 적용하는 공통 메서드
        /// </summary>
        private static void ApplyHSVToMaterial(Material mat, ColorReplace colorReplace)
        {
            mat.SetFloat(ColorReplace.PropHSVRangeMin, colorReplace.HSVRangeMin);
            mat.SetFloat(ColorReplace.PropHSVRangeMax, colorReplace.HSVRangeMax);
            mat.SetVector(ColorReplace.PropHSVAdjust, colorReplace.HSVAdjust);
        }

        /// <summary>
        /// 렌더러에 ColorReplace 셰이더 머티리얼이 있는지 확인하고, 없으면 생성하여 할당.
        /// IMaterialModifier가 자동으로 Hidden 변형을 처리하므로 SetMaterialDirty()만 호출.
        /// </summary>
        private Material EnsureColorReplaceMaterial(ColorReplace colorReplace)
        {
            Material current = GetRendererMaterial(colorReplace);

            // 이미 ColorReplace 셰이더 머티리얼이 할당되어 있으면 그대로 사용
            // SoftMaskLight Hidden 변형 셰이더도 유효한 ColorReplace 머티리얼로 인식
            if (current != null && ColorReplace.IsColorReplaceShader(current.shader))
                return current;

            // 없으면 새로 생성하여 할당
            Shader shader = Shader.Find(ColorReplace.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[ColorReplace] 셰이더를 찾을 수 없습니다: {ColorReplace.SHADER_NAME}");
                return null;
            }

            Material newMat = new Material(shader)
            {
                name = $"{ColorReplace.SHADER_NAME} (Temp)",
                hideFlags = HideFlags.DontSave
            };

            SetRendererMaterial(colorReplace, newMat);

            // IMaterialModifier가 자동 처리하므로 캔버스 재빌드만 트리거
            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
                graphic.SetMaterialDirty();

            return newMat;
        }

        // ─────────────────────────────────────────────
        // 머티리얼 저장
        // ─────────────────────────────────────────────

        /// <summary>
        /// 현재 머티리얼을 새 에셋으로 저장하고 렌더러에 할당
        /// </summary>
        private void SaveAsNewMaterial(ColorReplace colorReplace)
        {
            Material current = GetRendererMaterial(colorReplace);
            if (current == null) return;

            Shader shader = Shader.Find(ColorReplace.SHADER_NAME);
            if (shader == null) return;

            EnsureFolderExists(DEFAULT_SAVE_FOLDER);

            string sanitizedName = SanitizeFileName(colorReplace.gameObject.name);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{DEFAULT_SAVE_FOLDER}/{sanitizedName}.mat"
            );

            // 현재 머티리얼 복사하여 에셋으로 저장
            // SoftMaskLight 환경에서는 current가 Hidden 변형 셰이더일 수 있으므로
            // 반드시 원본 ColorReplace 셰이더로 저장
            Material saved = new Material(current)
            {
                name = sanitizedName,
                shader = shader,
                hideFlags = HideFlags.None
            };

            AssetDatabase.CreateAsset(saved, assetPath);
            AssetDatabase.SaveAssets();

            // 렌더러에 저장된 머티리얼 할당
            Undo.RecordObject(GetRendererComponent(colorReplace), "Save ColorReplace Material");
            SetRendererMaterial(colorReplace, saved);
            EditorUtility.SetDirty(GetRendererComponent(colorReplace));

            // IMaterialModifier가 자동 처리하므로 캔버스 재빌드만 트리거
            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
                graphic.SetMaterialDirty();

            // 기존 임시 머티리얼 정리
            if (!AssetDatabase.Contains(current) && current != null)
                DestroyImmediate(current);

            Debug.Log($"[ColorReplace] 머티리얼 저장 완료: {assetPath}");
            EditorGUIUtility.PingObject(saved);
        }

        // ─────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────

        private Material GetRendererMaterial(ColorReplace colorReplace)
        {
            var sr = colorReplace.GetComponent<SpriteRenderer>();
            if (sr != null) return sr.sharedMaterial;

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null && graphic.material != graphic.defaultMaterial)
                return graphic.material;

            return null;
        }

        private void SetRendererMaterial(ColorReplace colorReplace, Material material)
        {
            var sr = colorReplace.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sharedMaterial = material;
                return;
            }

            var graphic = colorReplace.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null)
            {
                graphic.material = material;
            }
        }

        private Component GetRendererComponent(ColorReplace colorReplace)
        {
            var sr = colorReplace.GetComponent<SpriteRenderer>();
            if (sr != null) return sr;
            return colorReplace.GetComponent<UnityEngine.UI.Graphic>();
        }

        private bool IsColorReplaceAsset(Material material)
        {
            if (material == null) return false;
            if (!AssetDatabase.Contains(material)) return false;
            return ColorReplace.IsColorReplaceShader(material.shader);
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);

            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // ─────────────────────────────────────────────
        // HSV 다이어그램
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

            ApplyToMaterial();
        }
    }
#endif
}
