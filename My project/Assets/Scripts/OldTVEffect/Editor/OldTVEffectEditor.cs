using UnityEngine;
using UnityEditor;

namespace CAT.Effects
{
    /// <summary>
    /// Play 모드 없이 OldTVEffect 애니메이션을 확인하기 위한 에디터 시간 구동기.
    /// 셰이더 전역 시간 오프셋(_CAT_TVEditorTime)을 EditorApplication.update 로 밀어주고,
    /// 매 틱 대상 컴포넌트의 변경값을 반영해 인스펙터 수정이 즉시 보이게 한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class OldTVEffectPreviewDriver
    {
        public const float MaxPreviewSeconds = 60f;

        private static OldTVEffect s_target;
        private static double s_lastTime;
        private static float s_elapsed;

        static OldTVEffectPreviewDriver()
        {
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += _ => Stop();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (_, __) => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        public static bool IsRunning(OldTVEffect fx) => fx != null && s_target == fx;
        public static float RemainingSeconds => Mathf.Max(0f, MaxPreviewSeconds - s_elapsed);

        public static void Start(OldTVEffect fx)
        {
            Stop();
            if (fx == null)
                return;

            s_target = fx;
            s_elapsed = 0f;
            s_lastTime = EditorApplication.timeSinceStartup;
            RepaintViews();
        }

        public static void Stop()
        {
            if (s_target == null)
                return;

            s_target = null;
            s_elapsed = 0f;
            Shader.SetGlobalFloat(OldTVEffect.EditorTimeProperty, 0f);
            RepaintViews();
        }

        // 다른 오브젝트를 선택했을 때만 멈춘다 (Editor 재생성과 구분)
        private static void OnSelectionChanged()
        {
            if (s_target != null && Selection.activeGameObject != s_target.gameObject)
                Stop();
        }

        private static void Update()
        {
            if (s_target == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            // 에디터가 멈췼다 재개될 때 한 번에 튀는 것을 막는다
            float delta = Mathf.Min((float)(now - s_lastTime), 0.1f);
            s_lastTime = now;

            s_elapsed += delta;
            if (s_elapsed >= MaxPreviewSeconds)
            {
                Stop();
                return;
            }

            Shader.SetGlobalFloat(OldTVEffect.EditorTimeProperty, s_elapsed);
            s_target.EditorPreviewTick();
            RepaintViews();
        }

        private static void RepaintViews()
        {
            SceneView.RepaintAll();
            // Game 뷰(Screen Space Canvas 포함)도 갱신
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }

    [CustomEditor(typeof(OldTVEffect))]
    [CanEditMultipleObjects]
    public class OldTVEffectEditor : Editor
    {
        private SerializedProperty _spriteShader;
        private SerializedProperty _uiShader;
        private SerializedProperty _noiseTexture;
        private SerializedProperty _noiseIntensity;
        private SerializedProperty _noiseScale;
        private SerializedProperty _scanLineIntensity;
        private SerializedProperty _scanLineCount;
        private SerializedProperty _scanLineThickness;
        private SerializedProperty _scanLineSpace;
        private SerializedProperty _verticalJitter;
        private SerializedProperty _horizontalJitter;
        private SerializedProperty _colorBleed;
        private SerializedProperty _colorBleedOffset;
        private SerializedProperty _rollSpeed;
        private SerializedProperty _saturation;
        private SerializedProperty _performanceMode;
        private SerializedProperty _lowQualityMaxLevel;

        private static bool _showReferences;
        private static bool _showNoiseSettings = true;
        private static bool _showScanLineSettings = true;
        private static bool _showJitterSettings = true;
        private static bool _showColorSettings = true;
        private static bool _showPerformance = true;

        private void OnEnable()
        {
            EditorApplication.update += RepaintWhilePreviewing;
            _spriteShader = serializedObject.FindProperty("spriteShader");
            _uiShader = serializedObject.FindProperty("uiShader");
            _noiseTexture = serializedObject.FindProperty("noiseTexture");
            _noiseIntensity = serializedObject.FindProperty("noiseIntensity");
            _noiseScale = serializedObject.FindProperty("noiseScale");
            _scanLineIntensity = serializedObject.FindProperty("scanLineIntensity");
            _scanLineCount = serializedObject.FindProperty("scanLineCount");
            _scanLineThickness = serializedObject.FindProperty("scanLineThickness");
            _scanLineSpace = serializedObject.FindProperty("scanLineSpace");
            _verticalJitter = serializedObject.FindProperty("verticalJitter");
            _horizontalJitter = serializedObject.FindProperty("horizontalJitter");
            _colorBleed = serializedObject.FindProperty("colorBleed");
            _colorBleedOffset = serializedObject.FindProperty("colorBleedOffset");
            _rollSpeed = serializedObject.FindProperty("rollSpeed");
            _saturation = serializedObject.FindProperty("saturation");
            _performanceMode = serializedObject.FindProperty("performanceMode");
            _lowQualityMaxLevel = serializedObject.FindProperty("lowQualityMaxLevel");
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhilePreviewing;
        }

        private bool IsPreviewing => target != null && OldTVEffectPreviewDriver.IsRunning(target as OldTVEffect);

        private void RepaintWhilePreviewing()
        {
            if (IsPreviewing)
                Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTestControls();
            EditorGUILayout.Space();

            _showReferences = EditorGUILayout.Foldout(_showReferences, "셰이더 참조", true, EditorStyles.foldoutHeader);
            if (_showReferences)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_spriteShader, new GUIContent("Sprite 셰이더"));
                EditorGUILayout.PropertyField(_uiShader, new GUIContent("UI 셰이더"));
                EditorGUI.indentLevel--;
            }
            if (_spriteShader.objectReferenceValue == null || _uiShader.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("셰이더 참조가 비어 있습니다. 빌드에서 셰이더가 스트립되어 효과가 사라질 수 있습니다.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_noiseTexture, new GUIContent("노이즈 텍스처"));
            EditorGUILayout.HelpBox("노이즈 텍스처를 지정하지 않으면 자동 생성된 노이즈가 사용됩니다. R 채널만 사용하며, 지정 텍스처는 sRGB 를 해제해야 그레인이 밝기 대칭으로 나옵니다.", MessageType.Info);

            EditorGUILayout.Space();
            _showNoiseSettings = EditorGUILayout.Foldout(_showNoiseSettings, "노이즈 설정", true, EditorStyles.foldoutHeader);
            if (_showNoiseSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_noiseIntensity, new GUIContent("노이즈 강도"));
                EditorGUILayout.PropertyField(_noiseScale, new GUIContent("노이즈 스케일"));
                EditorGUILayout.PropertyField(_rollSpeed, new GUIContent("롤링 속도"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            _showScanLineSettings = EditorGUILayout.Foldout(_showScanLineSettings, "주사선 설정", true, EditorStyles.foldoutHeader);
            if (_showScanLineSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_scanLineIntensity, new GUIContent("주사선 강도"));
                EditorGUILayout.PropertyField(_scanLineCount, new GUIContent("주사선 개수"));
                EditorGUILayout.PropertyField(_scanLineThickness, new GUIContent("주사선 두께"));
                EditorGUILayout.PropertyField(_scanLineSpace, new GUIContent("주사선 기준 공간"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            _showJitterSettings = EditorGUILayout.Foldout(_showJitterSettings, "화면 떨림 설정", true, EditorStyles.foldoutHeader);
            if (_showJitterSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_verticalJitter, new GUIContent("수직 떨림"));
                EditorGUILayout.PropertyField(_horizontalJitter, new GUIContent("수평 떨림"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            _showColorSettings = EditorGUILayout.Foldout(_showColorSettings, "색상 설정", true, EditorStyles.foldoutHeader);
            if (_showColorSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_colorBleed, new GUIContent("색상 번짐 강도"));
                EditorGUILayout.PropertyField(_colorBleedOffset, new GUIContent("색상 번짐 간격"));
                EditorGUILayout.PropertyField(_saturation, new GUIContent("채도"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            _showPerformance = EditorGUILayout.Foldout(_showPerformance, "성능 설정", true, EditorStyles.foldoutHeader);
            if (_showPerformance)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_performanceMode, new GUIContent("성능 모드"));
                if (_performanceMode.enumValueIndex == (int)OldTVEffect.PerformanceMode.Auto)
                {
                    EditorGUILayout.PropertyField(_lowQualityMaxLevel, new GUIContent("저사양 최대 품질 레벨"));
                }
                EditorGUILayout.HelpBox("저사양 모드는 색상 번짐(텍스처 3탭)을 비활성화하고 노이즈·떨림을 축소합니다.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프리셋", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("강한 레트로 TV"))
                ApplyPreset(0.6f, 4f, 0.7f, 100f, 1.2f, 0.008f, 0.005f, 0.25f, 0.03f, 1.5f, 1f);
            if (GUILayout.Button("미묘한 레트로 TV"))
                ApplyPreset(0.2f, 3f, 0.3f, 120f, 0.8f, 0.003f, 0.002f, 0.1f, 0.01f, 0.8f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("깨진 TV"))
                ApplyPreset(0.8f, 2f, 0.5f, 80f, 1.5f, 0.02f, 0.015f, 0.4f, 0.05f, 3f, 1f);
            if (GUILayout.Button("흑백 TV"))
                ApplyPreset(0.5f, 3.5f, 0.6f, 110f, 1f, 0.006f, 0.003f, 0f, 0f, 1.2f, 0f);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>60초 테스트 버튼. 재생 중에도 인스펙터 값을 바꾸면 즉시 반영된다.</summary>
        private void DrawTestControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("테스트", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서는 컴포넌트가 직접 재생됩니다.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var fx = target as OldTVEffect;
            bool previewing = IsPreviewing;

            using (new EditorGUI.DisabledScope(targets.Length > 1))
            {
                if (previewing)
                {
                    GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                    if (GUILayout.Button($"⏹ 테스트 중지 (남은 시간: {OldTVEffectPreviewDriver.RemainingSeconds:F1}초)", GUILayout.Height(30)))
                        OldTVEffectPreviewDriver.Stop();
                }
                else
                {
                    GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                    if (GUILayout.Button($"▶ {OldTVEffectPreviewDriver.MaxPreviewSeconds:F0}초 테스트", GUILayout.Height(30)))
                        OldTVEffectPreviewDriver.Start(fx);
                }
                GUI.backgroundColor = Color.white;
            }

            if (previewing)
            {
                Rect bar = EditorGUILayout.GetControlRect(false, 16f);
                float t = 1f - OldTVEffectPreviewDriver.RemainingSeconds / OldTVEffectPreviewDriver.MaxPreviewSeconds;
                EditorGUI.ProgressBar(bar, t, "씬/게임 뷰에서 재생 중 — 값을 수정하면 즉시 반영됩니다");
            }
            else if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("테스트는 단일 선택에서만 실행할 수 있습니다.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        // SerializedProperty 경유로 적용해야 프리팹 인스턴스 오버라이드·Undo·다중 선택이 올바르게 동작한다.
        private void ApplyPreset(
            float noise, float noiseScl, float scanIntensity, float scanCount, float scanThickness,
            float vJitter, float hJitter, float bleed, float bleedOffset, float roll, float sat)
        {
            serializedObject.Update();
            _noiseIntensity.floatValue = noise;
            _noiseScale.floatValue = noiseScl;
            _scanLineIntensity.floatValue = scanIntensity;
            _scanLineCount.floatValue = scanCount;
            _scanLineThickness.floatValue = scanThickness;
            _verticalJitter.floatValue = vJitter;
            _horizontalJitter.floatValue = hJitter;
            _colorBleed.floatValue = bleed;
            _colorBleedOffset.floatValue = bleedOffset;
            _rollSpeed.floatValue = roll;
            _saturation.floatValue = sat;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
