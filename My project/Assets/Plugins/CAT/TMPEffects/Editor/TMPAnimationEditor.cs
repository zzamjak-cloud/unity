using UnityEngine;
using UnityEditor;
using DG.Tweening;

namespace CAT.UI
{
    [CustomEditor(typeof(TMPAnimation))]
    public class TMPAnimationEditor : Editor
    {
        // EditorPrefs 키
        private const string PREF_PRESET_FOLDER = "TMPAnimation_PresetFolder";
        private const string PRESET_SUBFOLDER = "Presets/Animation";

        // 타겟 및 상태
        private TMPAnimation _target;
        private TMPAnimationPreset _selectedPreset;
        private TMPAnimationPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        // 지연 액션 (다이얼로그 등)
        private System.Action _delayedAction;

        // 저장 폴더
        private DefaultAsset _presetFolder;
        private string _presetFolderPath;

        // 에디터 테스트 상태
        private bool _isEditorPlaying = false;
        private double _lastEditorTime = 0;

        // Foldout 상태
        private bool _showPresetSection = true;
        private bool _showPlaybackSection = true;
        private bool _showTimingSection = true;
        private bool _showAppearSection = true;
        private bool _showLoopSection = false;
        private bool _showDisappearSection = false;

        // Ease 이름 캐시
        private static string[] _easeNames;
        private static string[] _easeDisplayNames;

        private void OnEnable()
        {
            _target = (TMPAnimation)target;

            LoadPresetFolder();
            RefreshPresetList();

            if (_target.Preset != null)
            {
                _selectedPreset = _target.Preset;
                FindPresetIndex();
            }

            // Ease 이름 캐시 초기화
            if (_easeNames == null)
            {
                _easeNames = System.Enum.GetNames(typeof(Ease));
                _easeDisplayNames = new string[_easeNames.Length + 1];
                System.Array.Copy(_easeNames, _easeDisplayNames, _easeNames.Length);
                _easeDisplayNames[_easeDisplayNames.Length - 1] = "Custom";
            }

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;

            // 에디터 테스트 중이면 정지
            if (_isEditorPlaying)
            {
                StopEditorPreview();
            }
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        /// <summary>
        /// 프로젝트 에셋 변경 시 호출 (프리셋 삭제 감지)
        /// </summary>
        private void OnProjectChanged()
        {
            // 프리셋 목록 갱신
            RefreshPresetList();
            Repaint();
        }

        /// <summary>
        /// 이 에디터 스크립트 위치 기준으로 기본 저장 폴더 경로를 동적 계산
        /// TMPEffects 폴더를 어디에 두든 상대 경로가 유지됨
        /// </summary>
        private string GetDefaultPresetFolder()
        {
            var guids = AssetDatabase.FindAssets("t:Script TMPAnimationEditor");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // "...TMPEffects/Editor/TMPAnimationEditor.cs" → "...TMPEffects"
                int editorIdx = scriptPath.LastIndexOf("/Editor/");
                if (editorIdx >= 0)
                {
                    return scriptPath.Substring(0, editorIdx) + "/" + PRESET_SUBFOLDER;
                }
            }
            return "Assets/Plugins/CAT/TMPEffects/" + PRESET_SUBFOLDER;
        }

        private void LoadPresetFolder()
        {
            string defaultFolder = GetDefaultPresetFolder();
            _presetFolderPath = EditorPrefs.GetString(PREF_PRESET_FOLDER, defaultFolder);

            // 저장된 경로가 유효하면 그대로 사용, 아니면 기본 경로로 재설정
            if (!string.IsNullOrEmpty(_presetFolderPath) && AssetDatabase.IsValidFolder(_presetFolderPath))
            {
                _presetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_presetFolderPath);
            }
            else
            {
                _presetFolderPath = defaultFolder;
                _presetFolder = AssetDatabase.IsValidFolder(defaultFolder)
                    ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(defaultFolder)
                    : null;
            }
        }

        private void SavePresetFolder(string path)
        {
            _presetFolderPath = path;
            EditorPrefs.SetString(PREF_PRESET_FOLDER, path);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 지연된 액션 실행
            if (_delayedAction != null)
            {
                var action = _delayedAction;
                _delayedAction = null;
                action.Invoke();
                return;
            }

            // Second Face 지원 안내
            var outlineEffect = _target.GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                EditorGUILayout.HelpBox(
                    "TMPOutlineEffect의 Second Face가 감지되었습니다.\n" +
                    "애니메이션이 Second Face에도 자동으로 적용됩니다.",
                    MessageType.Info
                );
                EditorGUILayout.Space(5);
            }

            // 프리셋 관리 섹션
            DrawPresetSection();

            EditorGUILayout.Space(5);

            // 재생 컨트롤 섹션
            DrawPlaybackSection();

            EditorGUILayout.Space(5);

            // Timing 섹션
            DrawTimingSection();

            EditorGUILayout.Space(5);

            // Appear Animation 섹션
            DrawAppearSection();

            EditorGUILayout.Space(5);

            // Loop Animation 섹션
            DrawLoopSection();

            EditorGUILayout.Space(5);

            // Disappear Animation 섹션
            DrawDisappearSection();

            // 변경 사항 적용 (항상 호출 - 변경이 없으면 아무것도 안 함)
            serializedObject.ApplyModifiedProperties();

            // 프리셋 변경 안내
            if (_selectedPreset != null && HasValuesChanged())
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"값이 변경되었습니다. '{_selectedPreset.name}' 프리셋을 업데이트하려면 '갱신' 버튼을 클릭하세요.",
                    MessageType.Info
                );
            }
        }

        // ─────────────────────────────────────────────
        // 프리셋 관리 섹션
        // ─────────────────────────────────────────────

        private void DrawPresetSection()
        {
            _showPresetSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPresetSection, "프리셋 관리");

            if (_showPresetSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 저장 폴더 지정
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    new GUIContent("저장 폴더", "프리셋이 저장될 기본 폴더"),
                    GUILayout.Width(80));

                EditorGUI.BeginChangeCheck();
                var newFolder = EditorGUILayout.ObjectField(
                    _presetFolder, typeof(DefaultAsset), false) as DefaultAsset;
                if (EditorGUI.EndChangeCheck() && newFolder != null)
                {
                    string path = AssetDatabase.GetAssetPath(newFolder);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        _presetFolder = newFolder;
                        SavePresetFolder(path);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("오류", "폴더만 지정할 수 있습니다.", "확인");
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);

                // 프리셋 드롭다운
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("프리셋", GUILayout.Width(80));

                int newIndex = EditorGUILayout.Popup(_selectedPresetIndex, _presetNames);
                if (newIndex != _selectedPresetIndex)
                {
                    OnPresetSelected(newIndex);
                }

                if (GUILayout.Button(new GUIContent("⟳", "프리셋 목록 새로고침"), GUILayout.Width(25)))
                {
                    RefreshPresetList();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // 프리셋 저장/갱신/삭제 버튼
                EditorGUILayout.BeginHorizontal();

                bool valuesChanged = HasValuesChanged();

                if (_selectedPresetIndex == 0)
                {
                    // None 선택 시
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                    if (GUILayout.Button("새 프리셋 저장", GUILayout.Height(25)))
                    {
                        _delayedAction = SaveAsNewPreset;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    // 프리셋 선택 시
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                    if (GUILayout.Button("신규 저장", GUILayout.Height(25)))
                    {
                        _delayedAction = SaveAsNewPreset;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;

                    GUI.enabled = valuesChanged;
                    GUI.backgroundColor = valuesChanged ? new Color(1f, 0.8f, 0.5f) : Color.white;
                    if (GUILayout.Button("갱신", GUILayout.Height(25)))
                    {
                        _delayedAction = UpdateExistingPreset;
                        Repaint();
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }

                EditorGUILayout.EndHorizontal();

                // 프리셋 설명 표시
                if (_selectedPreset != null && !string.IsNullOrEmpty(_selectedPreset.Description))
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("설명", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(_selectedPreset.Description, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─────────────────────────────────────────────
        // 재생 컨트롤 섹션
        // ─────────────────────────────────────────────

        private void DrawPlaybackSection()
        {
            _showPlaybackSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPlaybackSection, "재생 컨트롤");

            if (_showPlaybackSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (Application.isPlaying)
                {
                    // 플레이 모드
                    EditorGUILayout.BeginHorizontal();

                    GUI.enabled = !_target.IsPlaying;
                    if (GUILayout.Button("Play", GUILayout.Height(25)))
                    {
                        _target.Play();
                    }

                    GUI.enabled = _target.IsPlaying;
                    if (GUILayout.Button("Pause", GUILayout.Height(25)))
                    {
                        _target.Pause();
                    }

                    if (GUILayout.Button("Stop", GUILayout.Height(25)))
                    {
                        _target.Stop();
                    }

                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Status", _target.IsPlaying ? "Playing" : "Stopped", EditorStyles.miniLabel);
                }
                else
                {
                    // 에디터 모드
                    EditorGUILayout.BeginHorizontal();

                    if (!_isEditorPlaying)
                    {
                        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                        if (GUILayout.Button("에디터 테스트", GUILayout.Height(25)))
                        {
                            StartEditorPreview();
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    else
                    {
                        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                        if (GUILayout.Button("정지", GUILayout.Height(25)))
                        {
                            StopEditorPreview();
                        }
                        GUI.backgroundColor = Color.white;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Status", _isEditorPlaying ? "Editor Preview" : "Stopped", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void StartEditorPreview()
        {
            if (_isEditorPlaying) return;

            _isEditorPlaying = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            // DOTween 에디터 초기화
            DOTween.Init();
            DOTween.defaultUpdateType = UpdateType.Manual;

            _target.Play();
            EditorApplication.update += EditorUpdate;
        }

        private void StopEditorPreview()
        {
            if (!_isEditorPlaying) return;

            _isEditorPlaying = false;
            EditorApplication.update -= EditorUpdate;

            _target.Stop();
            DOTween.Clear();
            DOTween.defaultUpdateType = UpdateType.Normal;

            // 씬 뷰 갱신
            SceneView.RepaintAll();
        }

        private void EditorUpdate()
        {
            if (!_isEditorPlaying) return;

            // 에디터에서 실제 경과 시간 계산
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastEditorTime);
            _lastEditorTime = currentTime;

            // DOTween 에디터 업데이트
            DOTween.ManualUpdate(deltaTime, deltaTime);

            // 인스펙터 및 씬 뷰 갱신
            Repaint();
            SceneView.RepaintAll();
        }

        // ─────────────────────────────────────────────
        // Timing 섹션
        // ─────────────────────────────────────────────

        private void DrawTimingSection()
        {
            _showTimingSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showTimingSection, "Timing");

            if (_showTimingSection)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_playOnEnable"), new GUIContent("Play On Enable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_initialDelay"), new GUIContent("Initial Delay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_characterDelay"), new GUIContent("Character Delay"));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ─────────────────────────────────────────────
        // Appear Animation 섹션
        // ─────────────────────────────────────────────

        private void DrawAppearSection()
        {
            var enableAppear = serializedObject.FindProperty("_enableAppear");

            // Foldout + Enable 토글 조합 헤더 (클릭 영역 분리)
            Rect headerRect = EditorGUILayout.GetControlRect();
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 15, headerRect.height);
            Rect toggleRect = new Rect(headerRect.x + 15, headerRect.y, headerRect.width - 15, headerRect.height);

            _showAppearSection = EditorGUI.Foldout(foldoutRect, _showAppearSection, GUIContent.none, true);
            enableAppear.boolValue = EditorGUI.ToggleLeft(toggleRect, "Appear Animation", enableAppear.boolValue, EditorStyles.boldLabel);

            if (_showAppearSection && enableAppear.boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;

                // Transform (압축 표시)
                DrawVector2Property("_appearPosition", "Position");
                DrawVector2Property("_appearScale", "Scale");
                DrawRotationZProperty("_appearRotation", "Rotation");

                EditorGUILayout.Space(3);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_appearRelative"), new GUIContent("Relative"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_appearAlpha"), new GUIContent("Alpha"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_appearDuration"), new GUIContent("Duration"));

                // Ease + Custom
                DrawEaseWithCustomCurve("_appearEase", "_appearUseCustomCurve", "_appearCustomCurve");

                EditorGUILayout.Space(3);

                // Position Curve
                DrawPositionCurve("_appearUsePositionCurve", "_appearPositionCurveOffset");

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        // ─────────────────────────────────────────────
        // Loop Animation 섹션
        // ─────────────────────────────────────────────

        private void DrawLoopSection()
        {
            var enableLoop = serializedObject.FindProperty("_enableLoop");
            var loopCount = serializedObject.FindProperty("_loopCount");

            // Foldout + Enable 토글 조합 헤더 (클릭 영역 분리)
            Rect headerRect = EditorGUILayout.GetControlRect();
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 15, headerRect.height);
            Rect toggleRect = new Rect(headerRect.x + 15, headerRect.y, headerRect.width - 15, headerRect.height);

            _showLoopSection = EditorGUI.Foldout(foldoutRect, _showLoopSection, GUIContent.none, true);
            enableLoop.boolValue = EditorGUI.ToggleLeft(toggleRect, "Loop Animation", enableLoop.boolValue, EditorStyles.boldLabel);

            if (_showLoopSection && enableLoop.boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;

                // Transform (압축 표시)
                DrawVector2Property("_loopPosition", "Position");
                DrawVector2Property("_loopScale", "Scale");
                DrawRotationZProperty("_loopRotation", "Rotation");

                EditorGUILayout.Space(3);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_loopRelative"), new GUIContent("Relative"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_loopDuration"), new GUIContent("Duration"));

                // Ease + Custom
                DrawEaseWithCustomCurve("_loopEase", "_loopUseCustomCurve", "_loopCustomCurve");

                // Loop Count (직관적 표시)
                DrawLoopCount(loopCount);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_loopType"), new GUIContent("Loop Type"));

                // Loop Count 경고
                if (loopCount.intValue == -1)
                {
                    EditorGUILayout.HelpBox(
                        "Loop Count가 무한(-1)이므로 Disappear Animation은 실행되지 않습니다.",
                        MessageType.Info
                    );
                }
                else if (loopCount.intValue == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Loop Count가 비활성화(0)이므로 Loop Animation이 실행되지 않습니다.",
                        MessageType.Info
                    );
                }

                EditorGUILayout.Space(3);

                // Position Curve
                DrawPositionCurve("_loopUsePositionCurve", "_loopPositionCurveOffset");

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        // ─────────────────────────────────────────────
        // Disappear Animation 섹션
        // ─────────────────────────────────────────────

        private void DrawDisappearSection()
        {
            var enableDisappear = serializedObject.FindProperty("_enableDisappear");
            var enableLoop = serializedObject.FindProperty("_enableLoop");
            var loopCount = serializedObject.FindProperty("_loopCount");

            // Loop가 무한이면 Disappear 비활성화
            bool isLoopInfinite = enableLoop.boolValue && loopCount.intValue == -1;

            // Foldout + Enable 토글 조합 헤더 (클릭 영역 분리)
            Rect headerRect = EditorGUILayout.GetControlRect();
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 15, headerRect.height);
            Rect toggleRect = new Rect(headerRect.x + 15, headerRect.y, headerRect.width - 15, headerRect.height);

            _showDisappearSection = EditorGUI.Foldout(foldoutRect, _showDisappearSection, GUIContent.none, true);
            if (isLoopInfinite)
            {
                GUI.enabled = false;
                EditorGUI.ToggleLeft(toggleRect, "Disappear Animation (Locked)", false, EditorStyles.boldLabel);
                GUI.enabled = true;
            }
            else
            {
                enableDisappear.boolValue = EditorGUI.ToggleLeft(toggleRect, "Disappear Animation", enableDisappear.boolValue, EditorStyles.boldLabel);
            }

            if (isLoopInfinite)
            {
                EditorGUILayout.HelpBox(
                    "Loop Count가 무한(-1)이므로 Disappear를 활성화할 수 없습니다.",
                    MessageType.Warning
                );
            }
            else if (_showDisappearSection && enableDisappear.boolValue)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;

                // Transform (압축 표시)
                DrawVector2Property("_disappearPosition", "Position");
                DrawVector2Property("_disappearScale", "Scale");
                DrawRotationZProperty("_disappearRotation", "Rotation");

                EditorGUILayout.Space(3);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_disappearRelative"), new GUIContent("Relative"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_disappearAlpha"), new GUIContent("Alpha"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_disappearDuration"), new GUIContent("Duration"));

                // Ease + Custom
                DrawEaseWithCustomCurve("_disappearEase", "_disappearUseCustomCurve", "_disappearCustomCurve");

                EditorGUILayout.Space(3);

                // Position Curve
                DrawPositionCurve("_disappearUsePositionCurve", "_disappearPositionCurveOffset");

                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        // ─────────────────────────────────────────────
        // 헬퍼 메서드 - UI 컴포넌트
        // ─────────────────────────────────────────────

        private void DrawVector2Property(string propertyName, string label)
        {
            var prop = serializedObject.FindProperty(propertyName);
            var vec = prop.vector3Value;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 15));

            EditorGUILayout.LabelField("X", GUILayout.Width(15));
            vec.x = EditorGUILayout.FloatField(vec.x);
            EditorGUILayout.LabelField("Y", GUILayout.Width(15));
            vec.y = EditorGUILayout.FloatField(vec.y);

            EditorGUILayout.EndHorizontal();

            prop.vector3Value = vec;
        }

        private void DrawRotationZProperty(string propertyName, string label)
        {
            var prop = serializedObject.FindProperty(propertyName);
            var vec = prop.vector3Value;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 15));

            EditorGUILayout.LabelField("Z", GUILayout.Width(15));
            vec.z = EditorGUILayout.FloatField(vec.z);

            // 빈 공간 채우기
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            prop.vector3Value = vec;
        }

        private void DrawEaseWithCustomCurve(string easeProp, string useCustomProp, string curveProp)
        {
            var ease = serializedObject.FindProperty(easeProp);
            var useCustom = serializedObject.FindProperty(useCustomProp);
            var curve = serializedObject.FindProperty(curveProp);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ease", GUILayout.Width(EditorGUIUtility.labelWidth - 15));

            // 현재 인덱스 계산
            int currentIndex = useCustom.boolValue ? _easeDisplayNames.Length - 1 : ease.enumValueIndex;
            int newIndex = EditorGUILayout.Popup(currentIndex, _easeDisplayNames);

            if (newIndex != currentIndex)
            {
                if (newIndex == _easeDisplayNames.Length - 1)
                {
                    useCustom.boolValue = true;
                }
                else
                {
                    useCustom.boolValue = false;
                    ease.enumValueIndex = newIndex;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Custom 선택 시 커브 표시
            if (useCustom.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(curve, new GUIContent("Curve"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPositionCurve(string usePositionCurveProp, string curveOffsetProp)
        {
            var usePositionCurve = serializedObject.FindProperty(usePositionCurveProp);
            var curveOffset = serializedObject.FindProperty(curveOffsetProp);

            EditorGUILayout.PropertyField(usePositionCurve, new GUIContent("Use Position Curve", "베지어 곡선 이동 (시작점→중간점→도착점)"));

            if (usePositionCurve.boolValue)
            {
                EditorGUI.indentLevel++;

                // Vector2를 X, Y로 분리 표시
                var offset = curveOffset.vector2Value;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Curve Offset", GUILayout.Width(EditorGUIUtility.labelWidth - 15));
                EditorGUILayout.LabelField("X", GUILayout.Width(15));
                offset.x = EditorGUILayout.FloatField(offset.x);
                EditorGUILayout.LabelField("Y", GUILayout.Width(15));
                offset.y = EditorGUILayout.FloatField(offset.y);
                EditorGUILayout.EndHorizontal();
                curveOffset.vector2Value = offset;

                EditorGUILayout.HelpBox(
                    "시작점과 도착점의 중간 지점에서 X/Y 만큼 벗어난 곳을 경유하여 이동합니다.",
                    MessageType.None
                );

                EditorGUI.indentLevel--;
            }
        }

        private void DrawLoopCount(SerializedProperty loopCount)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Loop Count", GUILayout.Width(EditorGUIUtility.labelWidth - 15));

            int value = loopCount.intValue;

            // 프리셋 드롭다운
            // Loop Count: 0=비활성화, 1=1회, 2=2회, -1=무한
            string[] presets = { "무한 (-1)", "비활성화 (0)", "1회", "2회", "3회", "직접 입력" };
            int presetIndex;
            if (value == -1) presetIndex = 0;
            else if (value == 0) presetIndex = 1;
            else if (value == 1) presetIndex = 2;
            else if (value == 2) presetIndex = 3;
            else if (value == 3) presetIndex = 4;
            else presetIndex = 5;

            int newPresetIndex = EditorGUILayout.Popup(presetIndex, presets, GUILayout.Width(100));

            if (newPresetIndex != presetIndex)
            {
                switch (newPresetIndex)
                {
                    case 0: loopCount.intValue = -1; break;
                    case 1: loopCount.intValue = 0; break;
                    case 2: loopCount.intValue = 1; break;
                    case 3: loopCount.intValue = 2; break;
                    case 4: loopCount.intValue = 3; break;
                }
            }

            // 직접 입력 필드
            if (presetIndex == 5 || newPresetIndex == 5)
            {
                loopCount.intValue = EditorGUILayout.IntField(loopCount.intValue, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        // 프리셋 관리 메서드
        // ─────────────────────────────────────────────

        private bool HasValuesChanged()
        {
            if (_selectedPreset == null) return false;

            return _target.CharacterDelay != _selectedPreset.CharacterDelay ||
                   _target.EnableAppear != _selectedPreset.EnableAppear ||
                   _target.AppearRelative != _selectedPreset.AppearRelative ||
                   _target.AppearPosition != _selectedPreset.AppearPosition ||
                   _target.AppearScale != _selectedPreset.AppearScale ||
                   _target.AppearRotation != _selectedPreset.AppearRotation ||
                   !Mathf.Approximately(_target.AppearAlpha, _selectedPreset.AppearAlpha) ||
                   !Mathf.Approximately(_target.AppearDuration, _selectedPreset.AppearDuration) ||
                   _target.AppearEase != _selectedPreset.AppearEase ||
                   _target.AppearUseCustomCurve != _selectedPreset.AppearUseCustomCurve ||
                   !Mathf.Approximately(_target.AppearToLoopBlend, _selectedPreset.AppearToLoopBlend) ||
                   _target.AppearUsePositionCurve != _selectedPreset.AppearUsePositionCurve ||
                   _target.EnableLoop != _selectedPreset.EnableLoop ||
                   _target.LoopRelative != _selectedPreset.LoopRelative ||
                   _target.LoopPosition != _selectedPreset.LoopPosition ||
                   _target.LoopScale != _selectedPreset.LoopScale ||
                   _target.LoopRotation != _selectedPreset.LoopRotation ||
                   !Mathf.Approximately(_target.LoopDuration, _selectedPreset.LoopDuration) ||
                   _target.LoopEase != _selectedPreset.LoopEase ||
                   _target.LoopUseCustomCurve != _selectedPreset.LoopUseCustomCurve ||
                   _target.LoopCount != _selectedPreset.LoopCount ||
                   _target.LoopType != _selectedPreset.LoopType ||
                   !Mathf.Approximately(_target.LoopToDisappearBlend, _selectedPreset.LoopToDisappearBlend) ||
                   _target.LoopUsePositionCurve != _selectedPreset.LoopUsePositionCurve ||
                   _target.EnableDisappear != _selectedPreset.EnableDisappear ||
                   _target.DisappearRelative != _selectedPreset.DisappearRelative ||
                   _target.DisappearPosition != _selectedPreset.DisappearPosition ||
                   _target.DisappearScale != _selectedPreset.DisappearScale ||
                   _target.DisappearRotation != _selectedPreset.DisappearRotation ||
                   !Mathf.Approximately(_target.DisappearAlpha, _selectedPreset.DisappearAlpha) ||
                   !Mathf.Approximately(_target.DisappearDuration, _selectedPreset.DisappearDuration) ||
                   _target.DisappearEase != _selectedPreset.DisappearEase ||
                   _target.DisappearUseCustomCurve != _selectedPreset.DisappearUseCustomCurve ||
                   _target.DisappearUsePositionCurve != _selectedPreset.DisappearUsePositionCurve;
        }

        private void OnPresetSelected(int index)
        {
            Undo.RecordObject(_target, "Change Preset");

            _selectedPresetIndex = index;

            if (_selectedPresetIndex == 0)
            {
                _selectedPreset = null;
                // Preset 필드는 유지 (명시적으로 null 설정하지 않음)
            }
            else
            {
                _selectedPreset = _availablePresets[_selectedPresetIndex - 1];
                _target.ApplyPreset(_selectedPreset);
                serializedObject.Update();
            }

            EditorUtility.SetDirty(_target);
        }

        private void SaveAsNewPreset()
        {
            string folder = _presetFolderPath;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                folder = GetDefaultPresetFolder();
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "새 프리셋 저장",
                "NewCharacterAnimationPreset",
                "asset",
                "프리셋 이름을 입력하세요",
                folder
            );

            if (!string.IsNullOrEmpty(path))
            {
                string savedFolder = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                if (savedFolder != _presetFolderPath)
                {
                    SavePresetFolder(savedFolder);
                    _presetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(savedFolder);
                }

                var newPreset = ScriptableObject.CreateInstance<TMPAnimationPreset>();
                newPreset.CopyFrom(_target);

                AssetDatabase.CreateAsset(newPreset, path);
                AssetDatabase.SaveAssets();

                _selectedPreset = newPreset;

                RefreshPresetList();
                FindPresetIndex();

                EditorUtility.SetDirty(_target);
                EditorUtility.DisplayDialog("프리셋 저장 완료", $"'{newPreset.name}' 프리셋이 저장되었습니다.", "확인");
            }
        }

        private void UpdateExistingPreset()
        {
            if (_selectedPreset == null) return;

            Undo.RecordObject(_selectedPreset, "Update Preset");

            _selectedPreset.CopyFrom(_target);

            EditorUtility.SetDirty(_selectedPreset);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("프리셋 갱신 완료", $"'{_selectedPreset.name}' 프리셋이 업데이트되었습니다.", "확인");
        }

        private void DeletePreset()
        {
            if (_selectedPreset == null) return;

            string path = AssetDatabase.GetAssetPath(_selectedPreset);

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            _selectedPreset = null;
            _selectedPresetIndex = 0;

            RefreshPresetList();
            EditorUtility.SetDirty(_target);
        }

        private void RefreshPresetList()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMPAnimationPreset");
            _availablePresets = new TMPAnimationPreset[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<TMPAnimationPreset>(path);
            }

            // 이름순 정렬
            System.Array.Sort(_availablePresets, (a, b) => string.Compare(a.name, b.name));

            // 드롭다운 이름 배열 생성
            _presetNames = new string[_availablePresets.Length + 1];
            _presetNames[0] = "None (새로 만들기)";

            for (int i = 0; i < _availablePresets.Length; i++)
            {
                _presetNames[i + 1] = _availablePresets[i].name;
            }

            if (_selectedPreset != null)
            {
                FindPresetIndex();
            }
        }

        private void FindPresetIndex()
        {
            if (_selectedPreset == null)
            {
                _selectedPresetIndex = 0;
                return;
            }

            for (int i = 0; i < _availablePresets.Length; i++)
            {
                if (_availablePresets[i] == _selectedPreset)
                {
                    _selectedPresetIndex = i + 1;
                    return;
                }
            }

            _selectedPresetIndex = 0;
        }
    }
}
