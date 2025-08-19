using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CAT.Effects
{
    [CustomEditor(typeof(VerticalFlip))]
    public class VerticalFlipEditor : Editor
    {
        // 미리보기 관련 변수
        private bool showPreview = true;
        private float previewProgress = 0.0f;
        private Texture2D previewTexture;
        private Material previewMaterial;
        private readonly int textureSize = 256;

        // 애니메이션 미리보기 관련 변수
        private bool isAnimating = false;
        private float animationTime = 0.0f;
        private readonly float previewFPS = 60.0f;
        private readonly float previewDeltaTime = 1.0f / 60.0f;
        private float previewTimeBetweenFlips = 1.0f;
        private List<Texture2D> cachedPreviewFrames;
        private int currentPreviewFrame = 0;

        // 프로퍼티 캐싱
        private SerializedProperty firstSpriteProperty;
        private SerializedProperty secondSpriteProperty;
        private SerializedProperty sliceCountProperty;
        private SerializedProperty flipDurationProperty;
        private SerializedProperty flipOffsetBetweenSlicesProperty;
        private SerializedProperty timeBetweenFlipsProperty;
        private SerializedProperty showColumnLinesProperty;
        private SerializedProperty lineColorProperty;
        private SerializedProperty lineWidthProperty;
        private SerializedProperty allowFrameSkippingProperty;
        private SerializedProperty useLowQualityOnMobileProperty;

        // 셰이더 속성 ID (캐싱)
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int SecondTexProperty = Shader.PropertyToID("_SecondTex");
        private static readonly int FlipProgressProperty = Shader.PropertyToID("_FlipProgress");
        private static readonly int SliceCountProperty = Shader.PropertyToID("_SliceCount");
        private static readonly int FlipDurationProperty = Shader.PropertyToID("_FlipDuration");
        private static readonly int FlipOffsetProperty = Shader.PropertyToID("_FlipOffset");
        private static readonly int ShowLinesProperty = Shader.PropertyToID("_ShowLines");
        private static readonly int LineColorProperty = Shader.PropertyToID("_LineColor");
        private static readonly int LineWidthProperty = Shader.PropertyToID("_LineWidth");

        private void OnEnable()
        {
            // 프로퍼티 가져오기
            firstSpriteProperty = serializedObject.FindProperty("firstSprite");
            secondSpriteProperty = serializedObject.FindProperty("secondSprite");
            sliceCountProperty = serializedObject.FindProperty("sliceCount");
            flipDurationProperty = serializedObject.FindProperty("flipDuration");
            flipOffsetBetweenSlicesProperty = serializedObject.FindProperty("flipOffsetBetweenSlices");
            timeBetweenFlipsProperty = serializedObject.FindProperty("timeBetweenFlips");
            showColumnLinesProperty = serializedObject.FindProperty("showColumnLines");
            lineColorProperty = serializedObject.FindProperty("lineColor");
            lineWidthProperty = serializedObject.FindProperty("lineWidth");
            allowFrameSkippingProperty = serializedObject.FindProperty("allowFrameSkipping");
            useLowQualityOnMobileProperty = serializedObject.FindProperty("useLowQualityOnMobile");

            // 미리보기 초기화
            InitializePreview();

            // 에디터 업데이트 이벤트 등록
            EditorApplication.update += UpdatePreviewAnimation;
        }

        private void OnDisable()
        {
            // 이벤트 등록 해제
            EditorApplication.update -= UpdatePreviewAnimation;

            // 리소스 정리
            CleanupResources();
        }

        private void InitializePreview()
        {
            VerticalFlip flipComponent = (VerticalFlip)target;

            // 스프라이트 확인
            if (flipComponent.firstSprite == null || flipComponent.secondSprite == null)
                return;

            // 미리보기 렌더링용 머티리얼 생성
            bool isUI = flipComponent.GetComponent<Image>() != null;
            string shaderName = isUI ? "CAT/UI/VerticalFlipUI" : "CAT/Effects/VerticalFlipSprite";
            Shader previewShader = Shader.Find(shaderName);

            if (previewShader == null)
            {
                Debug.LogWarning($"미리보기용 셰이더를 찾을 수 없습니다: {shaderName}");
                return;
            }

            // 미리보기 머티리얼 생성
            previewMaterial = new Material(previewShader);

            // 미리보기 텍스처 생성
            previewTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

            // 캐시된 프레임 리스트 초기화
            cachedPreviewFrames = new List<Texture2D>();
        }

        private void UpdatePreviewMaterial()
        {
            if (previewMaterial == null)
                return;

            VerticalFlip flipComponent = (VerticalFlip)target;

            // 머티리얼 속성 업데이트
            previewMaterial.SetFloat(SliceCountProperty, flipComponent.sliceCount);
            previewMaterial.SetFloat(FlipDurationProperty, flipComponent.flipDuration);
            previewMaterial.SetFloat(FlipOffsetProperty, flipComponent.flipOffsetBetweenSlices);
            previewMaterial.SetFloat(FlipProgressProperty, previewProgress);
            previewMaterial.SetFloat(ShowLinesProperty, flipComponent.showColumnLines ? 1.0f : 0.0f);
            previewMaterial.SetColor(LineColorProperty, flipComponent.lineColor);
            previewMaterial.SetFloat(LineWidthProperty, flipComponent.lineWidth);

            // 스프라이트 텍스처 설정
            if (flipComponent.firstSprite != null && flipComponent.secondSprite != null)
            {
                previewMaterial.SetTexture(MainTexProperty, flipComponent.firstSprite.texture);
                previewMaterial.SetTexture(SecondTexProperty, flipComponent.secondSprite.texture);
            }
        }

        private void UpdatePreviewAnimation()
        {
            if (!isAnimating || !showPreview)
                return;

            VerticalFlip flipComponent = (VerticalFlip)target;
            if (flipComponent == null || previewMaterial == null)
                return;

            // 미리보기 애니메이션 시간 갱신
            animationTime += previewDeltaTime;
            float totalDuration = flipComponent.flipDuration + (flipComponent.sliceCount * flipComponent.flipOffsetBetweenSlices);

            if (animationTime <= totalDuration)
            {
                // 애니메이션 진행 중
                previewProgress = animationTime;
                UpdatePreviewMaterial();
                Repaint(); // 에디터 윈도우 갱신
            }
            else if (animationTime > totalDuration && animationTime <= totalDuration + previewTimeBetweenFlips)
            {
                // 다음 플립까지 대기
                previewProgress = totalDuration;
            }
            else
            {
                // 애니메이션 완료 후 재시작 - 스프라이트 교체
                animationTime = 0;
                previewProgress = 0;

                // 텍스처 교체
                Texture2D temp = previewMaterial.GetTexture(MainTexProperty) as Texture2D;
                previewMaterial.SetTexture(MainTexProperty, previewMaterial.GetTexture(SecondTexProperty));
                previewMaterial.SetTexture(SecondTexProperty, temp);
            }
        }

        private void RenderPreview(Rect previewRect)
        {
            if (previewMaterial == null || previewTexture == null)
                return;

            // 미리보기 렌더링
            UpdatePreviewMaterial();

            // 여기서 RenderTexture를 사용하여 셰이더 효과가 적용된 미리보기 이미지 생성
            RenderTexture rt = RenderTexture.GetTemporary(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(null, rt, previewMaterial);

            // 현재 RenderTexture를 활성화
            RenderTexture.active = rt;
            previewTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            previewTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // 미리보기 텍스처 표시
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
        }

        private void CleanupResources()
        {
            // 미리보기 리소스 정리
            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
                previewMaterial = null;
            }

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
                previewTexture = null;
            }

            // 캐시된 프레임 정리
            if (cachedPreviewFrames != null)
            {
                foreach (var texture in cachedPreviewFrames)
                {
                    if (texture != null)
                        DestroyImmediate(texture);
                }
                cachedPreviewFrames.Clear();
            }
        }

        public override void OnInspectorGUI()
        {
            // 직렬화 객체 업데이트
            serializedObject.Update();

            VerticalFlip flipComponent = (VerticalFlip)target;

            // 미리보기 섹션
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            showPreview = EditorGUILayout.ToggleLeft("미리보기 표시", showPreview, EditorStyles.boldLabel, GUILayout.Width(120));

            GUI.enabled = showPreview;

            // 플레이/정지 버튼
            if (GUILayout.Button(isAnimating ? "정지" : "재생", GUILayout.Width(60)))
            {
                isAnimating = !isAnimating;
                if (isAnimating)
                {
                    // 애니메이션 시작 시 초기화
                    animationTime = 0;
                    previewProgress = 0;
                    previewTimeBetweenFlips = flipComponent.timeBetweenFlips;
                    UpdatePreviewMaterial();
                }
            }

            // 프로그레스 슬라이더
            GUI.enabled = showPreview && !isAnimating;
            float totalDuration = flipComponent.flipDuration + (flipComponent.sliceCount * flipComponent.flipOffsetBetweenSlices);
            previewProgress = EditorGUILayout.Slider(previewProgress, 0, totalDuration);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // 미리보기 표시
            if (showPreview)
            {
                float previewSize = Mathf.Min(EditorGUIUtility.currentViewWidth - 40, 300);
                Rect previewRect = EditorGUILayout.GetControlRect(false, previewSize);

                // 미리보기 영역 테두리 표시
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1));

                // 내부 여백을 위해 약간 축소
                previewRect = new Rect(previewRect.x + 2, previewRect.y + 2, previewRect.width - 4, previewRect.height - 4);

                // 미리보기 렌더링
                if (flipComponent.firstSprite != null && flipComponent.secondSprite != null)
                {
                    RenderPreview(previewRect);
                }
                else
                {
                    EditorGUI.LabelField(previewRect, "스프라이트가 할당되지 않았습니다", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // 스프라이트 설정 섹션
            //EditorGUILayout.LabelField("스프라이트 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(firstSpriteProperty);
            EditorGUILayout.PropertyField(secondSpriteProperty);
            EditorGUILayout.Space();

            // 애니메이션 설정 섹션
            //EditorGUILayout.LabelField("플립 애니메이션 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sliceCountProperty);
            EditorGUILayout.PropertyField(flipDurationProperty);
            EditorGUILayout.PropertyField(flipOffsetBetweenSlicesProperty);
            EditorGUILayout.PropertyField(timeBetweenFlipsProperty);
            EditorGUILayout.Space();

            // 라인 설정 섹션
            //EditorGUILayout.LabelField("라인 설정", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showColumnLinesProperty);

            // showColumnLines가 true일 때만 관련 속성 표시
            if (showColumnLinesProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(lineColorProperty);
                EditorGUILayout.PropertyField(lineWidthProperty);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // 성능 최적화 섹션
            //EditorGUILayout.LabelField("성능 최적화", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(allowFrameSkippingProperty, new GUIContent("프레임 스킵 허용"));
            EditorGUILayout.PropertyField(useLowQualityOnMobileProperty, new GUIContent("모바일 저사양 모드"));
            EditorGUILayout.Space();

            // 컴포넌트 정보 표시
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool isUI = flipComponent.GetComponent<Image>() != null;
            EditorGUILayout.LabelField("컴포넌트 타입:", isUI ? "UI Image" : "Sprite Renderer", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("사용 중인 셰이더:", isUI ? "CAT/UI/VerticalFlipUI" : "CAT/Effects/VerticalFlipSprite", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // 변경된 프로퍼티 적용
            if (serializedObject.ApplyModifiedProperties())
            {
                // 프로퍼티가 변경되면 미리보기 업데이트
                UpdatePreviewMaterial();
            }
        }

        // (옵션) 씬 뷰에서 컴포넌트 미리보기
        [DrawGizmo(GizmoType.Selected)]
        static void DrawGizmo(VerticalFlip flipComponent, GizmoType gizmoType)
        {
            // 씬 뷰에서 선택된 객체에 아이콘이나 기타 시각적 표시 추가 (필요한 경우)
            if (flipComponent.showColumnLines)
            {
                // 슬라이스 라인 표시 (필요한 경우)
                SpriteRenderer spriteRenderer = flipComponent.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    Bounds bounds = spriteRenderer.bounds;
                    float width = bounds.size.x;
                    float sliceWidth = width / flipComponent.sliceCount;

                    Gizmos.color = flipComponent.lineColor;

                    // 각 슬라이스 경계에 라인 그리기
                    for (int i = 1; i < flipComponent.sliceCount; i++)
                    {
                        float x = bounds.min.x + i * sliceWidth;
                        Gizmos.DrawLine(new Vector3(x, bounds.min.y, bounds.center.z),
                                        new Vector3(x, bounds.max.y, bounds.center.z));
                    }
                }
            }
        }
    }
}