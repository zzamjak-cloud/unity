using UnityEngine;
using UnityEditor;
using TMPro;

namespace CAT.UI
{
    [CustomEditor(typeof(TMPOutlineEffect))]
    public class TMPOutlineEffectEditor : Editor
    {
        // EditorPrefs 키
        private const string PREF_PRESET_FOLDER = "TMPOutlineEffect_PresetFolder";
        private const string PRESET_SUBFOLDER = "Presets/Outline";

        // 다음 프레임에 실행할 액션 (GUI 에러 방지)
        private System.Action _delayedAction;
        private TMPOutlineEffect _target;
        private TMPEffectPreset _selectedPreset;
        private TMPEffectPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        // 저장 폴더
        private DefaultAsset _presetFolder;
        private string _presetFolderPath;

        private void OnEnable()
        {
            _target = (TMPOutlineEffect)target;

            // 저장된 폴더 경로 불러오기
            LoadPresetFolder();

            RefreshPresetList();

            // 현재 할당된 Preset 확인
            if (_target.Preset != null)
            {
                _selectedPreset = _target.Preset;
                FindPresetIndex();
            }

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
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
            var guids = AssetDatabase.FindAssets("t:Script TMPOutlineEffectEditor");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // "...TMPEffects/Editor/TMPOutlineEffectEditor.cs" → "...TMPEffects"
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

        /// <summary>
        /// 현재 값이 선택된 프리셋과 다른지 확인
        /// </summary>
        private bool HasValuesChanged()
        {
            if (_selectedPreset == null) return false;

            return _target.UnderlayColor != _selectedPreset.UnderlayColor ||
                   !Mathf.Approximately(_target.UnderlayDilate, _selectedPreset.UnderlayDilate) ||
                   !Mathf.Approximately(_target.UnderlayOffsetX, _selectedPreset.UnderlayOffsetX) ||
                   !Mathf.Approximately(_target.UnderlayOffsetY, _selectedPreset.UnderlayOffsetY) ||
                   !Mathf.Approximately(_target.UnderlaySoftness, _selectedPreset.UnderlaySoftness) ||
                   _target.EnableFace != _selectedPreset.EnableFace ||
                   !Mathf.Approximately(_target.FaceDilate, _selectedPreset.FaceDilate) ||
                   _target.EnableShadow != _selectedPreset.EnableShadow ||
                   _target.ShadowOffset != _selectedPreset.ShadowOffset ||
                   !Mathf.Approximately(_target.ShadowAlpha, _selectedPreset.ShadowAlpha) ||
                   _target.EnableSecondFace != _selectedPreset.EnableSecondFace ||
                   _target.SecondFaceColor != _selectedPreset.SecondFaceColor ||
                   _target.UseSecondFaceGradient != _selectedPreset.UseSecondFaceGradient ||
                   !GradientsEqual(_target.SecondFaceGradient, _selectedPreset.SecondFaceGradient) ||
                   !Mathf.Approximately(_target.SecondFaceDilate, _selectedPreset.SecondFaceDilate) ||
                   !Mathf.Approximately(_target.SecondFaceOffsetX, _selectedPreset.SecondFaceOffsetX) ||
                   !Mathf.Approximately(_target.SecondFaceOffsetY, _selectedPreset.SecondFaceOffsetY);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 지연된 액션 실행 (다이얼로그 등)
            if (_delayedAction != null)
            {
                var action = _delayedAction;
                _delayedAction = null;
                action.Invoke();
                return;  // 이번 프레임은 여기서 종료
            }

            DrawPresetManagement();

            EditorGUILayout.Space(10);

            // 기본 인스펙터 그리기 전에 변경 감지 시작
            EditorGUI.BeginChangeCheck();

            DrawPropertiesExcludingPreset();

            // 값이 변경되었는지 체크
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            // Second Face 활성화 시 안내 메시지
            if (_target.EnableSecondFace)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "✓ Second Face가 활성화되었습니다.\n" +
                    "• 자식 TMP 오브젝트가 자동 생성되어 안쪽 텍스트를 표시합니다.\n" +
                    "• Face Dilate < 0 값으로 텍스트가 안쪽으로 축소됩니다.\n" +
                    "• 타이틀/강조 텍스트에 사용 권장.",
                    MessageType.Info
                );
            }

            // 프리셋 선택 상태에서 값이 변경되었을 때 안내 메시지
            bool valuesChanged = HasValuesChanged();
            if (valuesChanged && _selectedPreset != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"값이 변경되었습니다. '{_selectedPreset.name}' 프리셋을 업데이트하려면 '갱신' 버튼을 클릭하세요.",
                    MessageType.Info
                );
            }
        }

        private void DrawPresetManagement()
        {
            EditorGUILayout.LabelField("프리셋 관리", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 저장 폴더 지정
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("저장 폴더", "프리셋이 저장될 기본 폴더입니다. 폴더를 드래그&드롭하세요."),
                GUILayout.Width(100));

            EditorGUI.BeginChangeCheck();
            var newFolder = EditorGUILayout.ObjectField(
                new GUIContent("", "클릭하여 폴더를 선택하거나 Project 창에서 드래그&드롭하세요."),
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
            EditorGUILayout.LabelField("프리셋 선택", GUILayout.Width(100));

            int newIndex = EditorGUILayout.Popup(_selectedPresetIndex, _presetNames);
            if (newIndex != _selectedPresetIndex)
            {
                OnPresetSelected(newIndex);
            }

            // 새로고침 버튼
            if (GUILayout.Button(new GUIContent("⟳", "프리셋 목록 새로고침"), GUILayout.Width(30)))
            {
                RefreshPresetList();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 프리셋 저장/갱신 버튼
            EditorGUILayout.BeginHorizontal();

            bool valuesChanged = HasValuesChanged();

            if (_selectedPresetIndex == 0)
            {
                // None 선택 시 → 새 프리셋 저장만 표시
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                if (GUILayout.Button("💾 새 프리셋 저장", GUILayout.Height(30)))
                {
                    _delayedAction = SaveAsNewPreset;
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // 프리셋 선택 시 → '신규 저장' + '갱신' 버튼을 한 라인에 배치

                // 신규 저장 버튼 (항상 활성화)
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                if (GUILayout.Button("💾 신규 저장", GUILayout.Height(30)))
                {
                    _delayedAction = SaveAsNewPreset;
                    Repaint();
                }
                GUI.backgroundColor = Color.white;

                // 갱신 버튼 (값이 변경되었을 때만 활성화)
                GUI.enabled = valuesChanged;
                GUI.backgroundColor = valuesChanged ? new Color(1f, 0.8f, 0.5f) : Color.white;

                if (GUILayout.Button($"📝 갱신", GUILayout.Height(30)))
                {
                    _delayedAction = UpdateExistingPreset;
                    Repaint();
                }

                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();

            // 현재 선택된 프리셋 정보
            if (_selectedPreset != null && !string.IsNullOrEmpty(_selectedPreset.Description))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("설명", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(_selectedPreset.Description, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPropertiesExcludingPreset()
        {
            // Outline Settings
            EditorGUILayout.LabelField("Outline Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_underlayColor"), new GUIContent("Color"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_underlayDilate"), new GUIContent("Width"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_underlayOffsetX"), new GUIContent("Offset X"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_underlayOffsetY"), new GUIContent("Offset Y"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_underlaySoftness"), new GUIContent("Softness"));

            EditorGUILayout.Space(5);

            // Face Settings (조건부 표시)
            EditorGUILayout.LabelField("Face Settings", EditorStyles.boldLabel);
            var enableFaceProp = serializedObject.FindProperty("_enableFace");
            EditorGUILayout.PropertyField(enableFaceProp, new GUIContent("Enable"));
            if (enableFaceProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_faceDilate"), new GUIContent("Dilate"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Shadow Settings
            EditorGUILayout.LabelField("Shadow Settings", EditorStyles.boldLabel);
            var enableShadowProp = serializedObject.FindProperty("_enableShadow");
            EditorGUILayout.PropertyField(enableShadowProp, new GUIContent("Enable"));
            if (enableShadowProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_shadowOffset"), new GUIContent("Offset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_shadowAlpha"), new GUIContent("Alpha"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Second Face Settings (조건부 표시)
            EditorGUILayout.LabelField("Second Face Settings", EditorStyles.boldLabel);
            var enableSecondFaceProp = serializedObject.FindProperty("_enableSecondFace");
            EditorGUILayout.PropertyField(enableSecondFaceProp, new GUIContent("Enable"));
            if (enableSecondFaceProp.boolValue)
            {
                EditorGUI.indentLevel++;

                // Gradient 사용 여부
                var useGradientProp = serializedObject.FindProperty("_useSecondFaceGradient");
                EditorGUILayout.PropertyField(useGradientProp, new GUIContent("Use Gradient", "Second Face에 Gradient 적용"));

                if (useGradientProp.boolValue)
                {
                    // Gradient 필드
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondFaceGradient"), new GUIContent("Gradient"));
                }
                else
                {
                    // 단일 Color 필드
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondFaceColor"), new GUIContent("Color"));
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondFaceDilate"), new GUIContent("Dilate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondFaceOffsetX"), new GUIContent("Offset X"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_secondFaceOffsetY"), new GUIContent("Offset Y"));
                EditorGUI.indentLevel--;
            }
        }

        private void OnPresetSelected(int index)
        {
            Undo.RecordObject(_target, "Change Preset");

            _selectedPresetIndex = index;

            if (_selectedPresetIndex == 0)
            {
                // None 선택
                _selectedPreset = null;
                _target.Preset = null;
            }
            else
            {
                // 프리셋 선택
                _selectedPreset = _availablePresets[_selectedPresetIndex - 1];
                _target.Preset = _selectedPreset;
                _target.ApplyPreset(_selectedPreset);

                // serializedObject 업데이트
                serializedObject.Update();
            }

            EditorUtility.SetDirty(_target);
        }

        private void SaveAsNewPreset()
        {
            // 저장 폴더 확인
            string folder = _presetFolderPath;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                folder = GetDefaultPresetFolder();
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "새 프리셋 저장",
                "NewEffectPreset",
                "asset",
                "프리셋 이름을 입력하세요",
                folder
            );

            if (!string.IsNullOrEmpty(path))
            {
                // 저장된 폴더 경로 업데이트
                string savedFolder = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                if (savedFolder != _presetFolderPath)
                {
                    SavePresetFolder(savedFolder);
                    _presetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(savedFolder);
                }

                // 새 프리셋 생성
                var newPreset = ScriptableObject.CreateInstance<TMPEffectPreset>();
                newPreset.CopyFrom(_target);

                // 에셋으로 저장
                AssetDatabase.CreateAsset(newPreset, path);
                AssetDatabase.SaveAssets();

                // 현재 컴포넌트에 할당
                _selectedPreset = newPreset;
                _target.Preset = newPreset;

                // 프리셋 리스트 갱신
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

            // 현재 값을 프리셋에 복사
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
            _target.Preset = null;
            _selectedPresetIndex = 0;

            RefreshPresetList();
            EditorUtility.SetDirty(_target);
        }

        private void RefreshPresetList()
        {
            // 모든 TMPEffectPreset 에셋 찾기
            _availablePresets = FindAllPresets();

            // 드롭다운 이름 배열 생성
            _presetNames = new string[_availablePresets.Length + 1];
            _presetNames[0] = "None (새로 만들기)";

            for (int i = 0; i < _availablePresets.Length; i++)
            {
                _presetNames[i + 1] = _availablePresets[i].name;
            }

            // 현재 선택된 프리셋이 있다면 인덱스 찾기
            if (_selectedPreset != null)
            {
                FindPresetIndex();
            }
        }

        private TMPEffectPreset[] FindAllPresets()
        {
            string[] guids = AssetDatabase.FindAssets("t:TMPEffectPreset");
            var outlinePresets = new System.Collections.Generic.List<TMPEffectPreset>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var preset = AssetDatabase.LoadAssetAtPath<TMPEffectPreset>(path);

                // Outline 타입만 필터링
                if (preset != null && preset.EffectType == TMPEffectType.Outline)
                {
                    outlinePresets.Add(preset);
                }
            }

            // 이름순 정렬
            outlinePresets.Sort((a, b) => string.Compare(a.name, b.name));

            return outlinePresets.ToArray();
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

            // 못 찾으면 None으로
            _selectedPresetIndex = 0;
        }

        /// <summary>
        /// 두 VertexGradient가 동일한지 비교
        /// </summary>
        private bool GradientsEqual(VertexGradient g1, VertexGradient g2)
        {
            return g1.topLeft == g2.topLeft &&
                   g1.topRight == g2.topRight &&
                   g1.bottomLeft == g2.bottomLeft &&
                   g1.bottomRight == g2.bottomRight;
        }
    }
}
