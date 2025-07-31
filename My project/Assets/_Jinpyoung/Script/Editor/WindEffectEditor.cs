using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditorInternal;

namespace CAT.Effects
{
    [CustomEditor(typeof(WindEffect))]
    public class WindEffectEditor : Editor
    {
        // SerializedProperty 캐싱
        private SerializedProperty targetTypeProp;
        private SerializedProperty windStrengthProp;
        private SerializedProperty windSpeedProp;
        private SerializedProperty noiseScaleProp;
        private SerializedProperty noiseTextureProp;
        private SerializedProperty gradientTextureProp;

        // 미리보기 변수
        private bool showPreviewControls = true;
        private float previewTime = 0f;
        private bool isPlaying = false;
        private long lastUpdateTime;

        // 대상 컴포넌트 캐싱
        private WindEffect windEffect;
        private SpriteRenderer targetSpriteRenderer;
        private Image targetUIImage;
        private RawImage targetUIRawImage;
        private Material previewMaterial;
        private Texture2D gradientPreview;

        // 미리보기 쉐이더 프로퍼티 ID 캐싱
        private static readonly int TimeId = Shader.PropertyToID("_Time");

        private void OnEnable()
        {
            // 타겟 컴포넌트 참조
            windEffect = (WindEffect)target;

            // SerializedProperty 초기화
            targetTypeProp = serializedObject.FindProperty("targetType");
            windStrengthProp = serializedObject.FindProperty("windStrength");
            windSpeedProp = serializedObject.FindProperty("windSpeed");
            noiseScaleProp = serializedObject.FindProperty("noiseScale");
            noiseTextureProp = serializedObject.FindProperty("noiseTexture");
            gradientTextureProp = serializedObject.FindProperty("gradientTexture");

            // 렌더러 컴포넌트 캐싱
            targetSpriteRenderer = windEffect.GetComponent<SpriteRenderer>();
            targetUIImage = windEffect.GetComponent<Image>();
            targetUIRawImage = windEffect.GetComponent<RawImage>();

            // 에디터 업데이트 콜백 등록
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // 에디터 업데이트 콜백 제거
            EditorApplication.update -= OnEditorUpdate;

            // 미리보기 모드가 활성화 상태라면 정리
            if (isPlaying)
            {
                isPlaying = false;
                CleanupPreview();
            }

            // 임시 텍스처 정리
            if (gradientPreview != null)
            {
                DestroyImmediate(gradientPreview);
                gradientPreview = null;
            }

            // 미리보기 머티리얼 정리
            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
                previewMaterial = null;
            }
        }

        private void CleanupPreview()
        {
            // 미리보기 모드 종료 시 정리
            if (previewMaterial != null)
            {
                // 미리보기 머티리얼에서 사용된 _Time 값을 되돌림
                Vector4 zeroTime = new Vector4(0, 0, 0, 0);
                previewMaterial.SetVector(TimeId, zeroTime);

                // UI 컴포넌트라면 강제 갱신
                if (targetUIImage != null)
                {
                    targetUIImage.SetMaterialDirty();
                }
                if (targetUIRawImage != null)
                {
                    targetUIRawImage.SetMaterialDirty();
                }
            }
        }

        private void OnEditorUpdate()
        {
            // 재생 중일 때만 업데이트
            if (isPlaying && target != null)
            {
                long currentTime = System.DateTime.Now.Ticks / 10000;
                if (currentTime - lastUpdateTime > 16) // ~60fps
                {
                    lastUpdateTime = currentTime;
                    previewTime += windSpeedProp.floatValue * 0.016f; // 16ms에 해당하는 델타타임

                    if (previewTime > 1000) // 값이 너무 커지지 않도록 제한
                    {
                        previewTime = 0;
                    }

                    // 현재 사용 중인 머티리얼 가져오기
                    GetCurrentMaterial();

                    // 미리보기 머티리얼이 있으면 시간 값 직접 설정
                    if (previewMaterial != null)
                    {
                        // Unity의 _Time 벡터 형식: (t/20, t, t*2, t*3)
                        Vector4 timeValue = new Vector4(previewTime / 20, previewTime, previewTime * 2, previewTime * 3);
                        previewMaterial.SetVector(TimeId, timeValue);

                        // UI 컴포넌트라면 강제 갱신
                        if (targetUIImage != null)
                        {
                            targetUIImage.SetMaterialDirty();
                        }
                        if (targetUIRawImage != null)
                        {
                            targetUIRawImage.SetMaterialDirty();
                        }
                    }

                    // 인스펙터 리페인트 요청
                    Repaint();

                    // 씬 뷰 리페인트 요청 (UI 컴포넌트의 경우 필요)
                    if (targetUIImage != null || targetUIRawImage != null)
                    {
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void GetCurrentMaterial()
        {
            // 현재 타입에 따라 적절한 머티리얼 참조
            WindEffect.TargetType currentType = (WindEffect.TargetType)targetTypeProp.enumValueIndex;

            switch (currentType)
            {
                case WindEffect.TargetType.SpriteRenderer:
                    if (targetSpriteRenderer != null)
                    {
                        previewMaterial = targetSpriteRenderer.sharedMaterial;
                    }
                    break;

                case WindEffect.TargetType.UIImage:
                    if (targetUIImage != null)
                    {
                        previewMaterial = targetUIImage.material;
                    }
                    break;

                case WindEffect.TargetType.UIRawImage:
                    if (targetUIRawImage != null)
                    {
                        previewMaterial = targetUIRawImage.material;
                    }
                    break;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 타이틀 및 소개
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("CAT Wind Effect", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("바람에 흩날리는 효과를 적용합니다.", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // 대상 타입 설정
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(targetTypeProp, new GUIContent("Target Type", "바람 효과를 적용할 컴포넌트 타입"));
            if (EditorGUI.EndChangeCheck())
            {
                // 타입이 변경되면 미리보기 중단
                if (isPlaying)
                {
                    isPlaying = false;
                    CleanupPreview();
                }
            }
            EditorGUILayout.Space();

            // 텍스처 설정
            EditorGUILayout.LabelField("Texture Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(noiseTextureProp, new GUIContent("Noise Texture", "바람 효과에 사용할 기본 노이즈 텍스처"));

            EditorGUILayout.PropertyField(gradientTextureProp, new GUIContent("Gradient Map", "위치별 바람 영향도를 조절하는 그라디언트 맵 (R: X축 영향도, G: Y축 영향도)"));

            // 그라디언트 맵 미리보기
            if (gradientTextureProp.objectReferenceValue != null)
            {
                Texture2D gradientTex = (Texture2D)gradientTextureProp.objectReferenceValue;
                Rect previewRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, 20);
                EditorGUI.DrawPreviewTexture(previewRect, gradientTex);
                EditorGUILayout.Space();
            }

            // 바람 효과 설정
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wind Settings", EditorStyles.boldLabel);
            EditorGUILayout.Slider(windStrengthProp, 0, 0.1f, new GUIContent("Wind Strength", "바람 효과의 강도"));
            EditorGUILayout.Slider(windSpeedProp, 0, 10f, new GUIContent("Wind Speed", "바람 효과의 속도"));
            EditorGUILayout.Slider(noiseScaleProp, 0.1f, 10f, new GUIContent("Noise Scale", "노이즈 텍스처의 스케일"));

            // 미리보기 설정
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Settings", EditorStyles.boldLabel);

            showPreviewControls = EditorGUILayout.Foldout(showPreviewControls, "Preview Controls");
            if (showPreviewControls)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                // 재생 버튼
                GUI.color = isPlaying ? Color.green : Color.white;
                if (GUILayout.Button(isPlaying ? "■ Stop" : "▶ Play", GUILayout.Width(80)))
                {
                    isPlaying = !isPlaying;

                    if (isPlaying)
                    {
                        // 재생 시작 시 현재 머티리얼 참조 가져오기
                        GetCurrentMaterial();
                        lastUpdateTime = System.DateTime.Now.Ticks / 10000;
                    }
                    else
                    {
                        // 재생 중단 시 정리
                        CleanupPreview();
                    }
                }
                GUI.color = Color.white;

                // 미리보기 시간 슬라이더
                EditorGUI.BeginDisabledGroup(isPlaying);
                float newTime = EditorGUILayout.Slider(previewTime, 0, 10, GUILayout.ExpandWidth(true));
                if (newTime != previewTime)
                {
                    previewTime = newTime;

                    // 수동으로 시간 조정 시 미리보기 업데이트
                    GetCurrentMaterial();
                    if (previewMaterial != null)
                    {
                        Vector4 timeValue = new Vector4(previewTime / 20, previewTime, previewTime * 2, previewTime * 3);
                        previewMaterial.SetVector(TimeId, timeValue);

                        // UI 컴포넌트라면 강제 갱신
                        if (targetUIImage != null)
                        {
                            targetUIImage.SetMaterialDirty();
                        }
                        if (targetUIRawImage != null)
                        {
                            targetUIRawImage.SetMaterialDirty();
                        }

                        // 씬 뷰 리페인트 요청
                        SceneView.RepaintAll();
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            // 그라디언트 맵 생성 도우미
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gradient Map Generator", EditorStyles.boldLabel);

            if (GUILayout.Button("Create Vertical Gradient Map"))
            {
                CreateVerticalGradientMap();
            }

            if (GUILayout.Button("Create Horizontal Gradient Map"))
            {
                CreateHorizontalGradientMap();
            }

            if (GUILayout.Button("Create Radial Gradient Map"))
            {
                CreateRadialGradientMap();
            }

            // 설명 추가
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("그라디언트 맵은 바람 영향도를 조절합니다.\n- 빨간 채널(R): X축 움직임 영향도\n- 녹색 채널(G): Y축 움직임 영향도\n\n밝을수록 더 많이 움직입니다.", MessageType.Info);

            // 현재 타입이 UI Image/RawImage일 경우 추가 정보 표시
            WindEffect.TargetType currentType = (WindEffect.TargetType)targetTypeProp.enumValueIndex;
            if (currentType == WindEffect.TargetType.UIImage || currentType == WindEffect.TargetType.UIRawImage)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("UI 컴포넌트에서 미리보기를 재생하려면 Scene 뷰가 열려 있어야 합니다.", MessageType.Info);
            }

            // 변경사항이 있으면 적용
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        // 수직 그라디언트 맵 생성 (위쪽이 더 많이 움직이는 효과)
        private void CreateVerticalGradientMap()
        {
            Texture2D gradientMap = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            gradientMap.name = "WindGradient_Vertical";

            for (int y = 0; y < gradientMap.height; y++)
            {
                float normalizedY = (float)y / gradientMap.height;

                for (int x = 0; x < gradientMap.width; x++)
                {
                    // R: X축 바람 영향 (모든 높이에서 같은 정도)
                    // G: Y축 바람 영향 (위쪽으로 갈수록 더 많이)
                    Color pixelColor = new Color(0.5f, normalizedY, 0, 1);
                    gradientMap.SetPixel(x, y, pixelColor);
                }
            }

            gradientMap.Apply();

            // 저장 대화상자 표시
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Gradient Map",
                "WindGradient_Vertical",
                "png",
                "바람 효과용 그라디언트 맵을 저장하세요."
            );

            if (!string.IsNullOrEmpty(path))
            {
                byte[] bytes = gradientMap.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                // 텍스처 설정 변경
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false; // 리니어 모드로 설정
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                // 생성된 텍스처를 컴포넌트에 할당
                gradientTextureProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                serializedObject.ApplyModifiedProperties();
            }

            DestroyImmediate(gradientMap);
        }

        // 수평 그라디언트 맵 생성 (오른쪽이 더 많이 움직이는 효과)
        private void CreateHorizontalGradientMap()
        {
            Texture2D gradientMap = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            gradientMap.name = "WindGradient_Horizontal";

            for (int y = 0; y < gradientMap.height; y++)
            {
                for (int x = 0; x < gradientMap.width; x++)
                {
                    float normalizedX = (float)x / gradientMap.width;

                    // R: X축 바람 영향 (오른쪽으로 갈수록 더 많이)
                    // G: Y축 바람 영향 (모든 너비에서 같은 정도)
                    Color pixelColor = new Color(normalizedX, 0.5f, 0, 1);
                    gradientMap.SetPixel(x, y, pixelColor);
                }
            }

            gradientMap.Apply();

            // 저장 대화상자 표시
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Gradient Map",
                "WindGradient_Horizontal",
                "png",
                "바람 효과용 그라디언트 맵을 저장하세요."
            );

            if (!string.IsNullOrEmpty(path))
            {
                byte[] bytes = gradientMap.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                // 텍스처 설정 변경
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false; // 리니어 모드로 설정
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                // 생성된 텍스처를 컴포넌트에 할당
                gradientTextureProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                serializedObject.ApplyModifiedProperties();
            }

            DestroyImmediate(gradientMap);
        }

        // 방사형 그라디언트 맵 생성 (중심에서 바깥쪽으로 갈수록 더 많이 움직이는 효과)
        private void CreateRadialGradientMap()
        {
            Texture2D gradientMap = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            gradientMap.name = "WindGradient_Radial";

            Vector2 center = new Vector2(gradientMap.width / 2, gradientMap.height / 2);
            float maxDistance = Mathf.Sqrt(center.x * center.x + center.y * center.y);

            for (int y = 0; y < gradientMap.height; y++)
            {
                for (int x = 0; x < gradientMap.width; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDistance = Mathf.Clamp01(distance / maxDistance);

                    // 중심에서 멀어질수록 X, Y축 모두 더 많이 움직임
                    Color pixelColor = new Color(normalizedDistance, normalizedDistance, 0, 1);
                    gradientMap.SetPixel(x, y, pixelColor);
                }
            }

            gradientMap.Apply();

            // 저장 대화상자 표시
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Gradient Map",
                "WindGradient_Radial",
                "png",
                "바람 효과용 그라디언트 맵을 저장하세요."
            );

            if (!string.IsNullOrEmpty(path))
            {
                byte[] bytes = gradientMap.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                // 텍스처 설정 변경
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false; // 리니어 모드로 설정
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                // 생성된 텍스처를 컴포넌트에 할당
                gradientTextureProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                serializedObject.ApplyModifiedProperties();
            }

            DestroyImmediate(gradientMap);
        }
    }
}