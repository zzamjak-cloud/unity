using UnityEngine;
using UnityEditor;
using TMPro;

namespace CAT.UI
{
    [CustomEditor(typeof(TMPOutGlow))]
    public class TMPOutGlowEditor : Editor
    {
        // EditorPrefs 키
        private const string PREF_PRESET_FOLDER = "TMPOutGlow_PresetFolder";
        private const string DEFAULT_PRESET_FOLDER = "Assets/TMPEffects/Presets/Glow";

        // 다음 프레임에 실행할 액션 (GUI 에러 방지)
        private System.Action _delayedAction;
        private TMPOutGlow _target;
        private TMPEffectPreset _selectedPreset;
        private TMPEffectPreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        // 저장 폴더
        private DefaultAsset _presetFolder;
        private string _presetFolderPath;

        private void OnEnable()
        {
            _target = (TMPOutGlow)target;

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

        private void LoadPresetFolder()
        {
            _presetFolderPath = EditorPrefs.GetString(PREF_PRESET_FOLDER, DEFAULT_PRESET_FOLDER);

            if (!string.IsNullOrEmpty(_presetFolderPath) && AssetDatabase.IsValidFolder(_presetFolderPath))
            {
                _presetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_presetFolderPath);
            }
            else
            {
                _presetFolderPath = DEFAULT_PRESET_FOLDER;
                _presetFolder = null;
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

            // Color 비교 (RGBA 모두 확인)
            bool colorChanged = !ColorApproximately(_target.GlowColor, _selectedPreset.UnderlayColor);

            // Float 비교 (더 큰 허용 오차 사용 - 0.001f)
            bool rangeChanged = Mathf.Abs(_target.GlowRange - _selectedPreset.UnderlayDilate) > 0.001f;
            bool dilateChanged = Mathf.Abs(_target.FaceDilate - _selectedPreset.FaceDilate) > 0.001f;

            return colorChanged || rangeChanged || dilateChanged;
        }

        /// <summary>
        /// Color 근사 비교 (RGBA)
        /// </summary>
        private bool ColorApproximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.001f &&
                   Mathf.Abs(a.g - b.g) < 0.001f &&
                   Mathf.Abs(a.b - b.b) < 0.001f &&
                   Mathf.Abs(a.a - b.a) < 0.001f;
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

            // 프리셋 선택 상태에서 값이 변경되었을 때 안내 메시지
            bool valuesChanged = HasValuesChanged();
            if (valuesChanged && _selectedPreset != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"값이 변경되었습니다. '{_selectedPreset.name}' 프리셋을 업데이트하려면 '갱신' 버튼을 클릭하세요.",
                    MessageType.Warning
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
                new GUIContent("저장 폴더", "프리셋이 저장될 기본 폴더입니다."),
                GUILayout.Width(100));

            EditorGUI.BeginChangeCheck();
            var newFolder = EditorGUILayout.ObjectField(
                new GUIContent("", "클릭하여 폴더를 선택하거나 드래그&드롭하세요."),
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
                // 신규 저장 버튼
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

            EditorGUILayout.Space(5);

            // 초기화 버튼
            GUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
            if (GUILayout.Button("🔄 기본값으로 초기화", GUILayout.Height(25)))
            {
                Undo.RecordObject(_target, "Reset Glow Effect");

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
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawPropertiesExcludingPreset()
        {
            // Preset 필드는 제외하고 나머지 그리기
            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true);  // m_Script 건너뛰기

            while (iterator.NextVisible(false))
            {
                if (iterator.name == "_preset")
                {
                    continue;  // Preset 필드는 프리셋 관리 섹션에서 처리
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        // ─────────────────────────────────────────────
        // 프리셋 관리
        // ─────────────────────────────────────────────

        private void RefreshPresetList()
        {
            // 모든 TMPEffectPreset 로드
            string[] guids = AssetDatabase.FindAssets("t:TMPEffectPreset");
            var glowPresets = new System.Collections.Generic.List<TMPEffectPreset>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var preset = AssetDatabase.LoadAssetAtPath<TMPEffectPreset>(path);

                // Glow 타입만 필터링
                if (preset != null && preset.EffectType == TMPEffectType.Glow)
                {
                    glowPresets.Add(preset);
                }
            }

            _availablePresets = glowPresets.ToArray();

            // 드롭다운 이름 배열 생성
            _presetNames = new string[_availablePresets.Length + 1];
            _presetNames[0] = "None (새로 만들기)";
            for (int i = 0; i < _availablePresets.Length; i++)
            {
                _presetNames[i + 1] = _availablePresets[i].name;
            }

            FindPresetIndex();
        }

        private void FindPresetIndex()
        {
            _selectedPresetIndex = 0;
            if (_selectedPreset != null)
            {
                for (int i = 0; i < _availablePresets.Length; i++)
                {
                    if (_availablePresets[i] == _selectedPreset)
                    {
                        _selectedPresetIndex = i + 1;
                        break;
                    }
                }
            }
        }

        private void OnPresetSelected(int index)
        {
            _selectedPresetIndex = index;

            if (index == 0)
            {
                _selectedPreset = null;
                return;
            }

            _selectedPreset = _availablePresets[index - 1];

            if (_selectedPreset != null)
            {
                Undo.RecordObject(_target, "Apply Preset");
                _target.ApplyPreset(_selectedPreset);
                EditorUtility.SetDirty(_target);
            }
        }

        private void SaveAsNewPreset()
        {
            string presetName = EditorUtility.SaveFilePanelInProject(
                "새 프리셋 저장",
                "TMPGlowPreset",
                "asset",
                "프리셋 이름을 입력하세요.",
                _presetFolderPath);

            if (string.IsNullOrEmpty(presetName))
            {
                return;
            }

            var newPreset = ScriptableObject.CreateInstance<TMPEffectPreset>();
            newPreset.name = System.IO.Path.GetFileNameWithoutExtension(presetName);

            // 현재 값 복사 (Glow → Underlay 매핑)
            CopyToPreset(newPreset);

            AssetDatabase.CreateAsset(newPreset, presetName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _selectedPreset = newPreset;
            _target.Preset = newPreset;
            EditorUtility.SetDirty(_target);

            RefreshPresetList();

            Debug.Log($"[TMPOutGlow] 프리셋 저장 완료: {presetName}");
        }

        private void UpdateExistingPreset()
        {
            if (_selectedPreset == null) return;

            Undo.RecordObject(_selectedPreset, "Update Preset");
            CopyToPreset(_selectedPreset);
            EditorUtility.SetDirty(_selectedPreset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TMPOutGlow] 프리셋 갱신 완료: {_selectedPreset.name}");
        }

        private void RemovePreset()
        {
            if (_selectedPreset == null) return;

            string path = AssetDatabase.GetAssetPath(_selectedPreset);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();

            _selectedPreset = null;
            _selectedPresetIndex = 0;
            _target.Preset = null;
            EditorUtility.SetDirty(_target);

            RefreshPresetList();

            Debug.Log($"[TMPOutGlow] 프리셋 삭제 완료");
        }

        /// <summary>
        /// 현재 TMPOutGlow 값을 Preset에 복사
        /// </summary>
        private void CopyToPreset(TMPEffectPreset preset)
        {
            // Glow 파라미터 → Underlay 파라미터로 매핑
            var serializedPreset = new SerializedObject(preset);

            // 타입 설정 (중요!)
            serializedPreset.FindProperty("_effectType").enumValueIndex = (int)TMPEffectType.Glow;

            serializedPreset.FindProperty("_underlayColor").colorValue = _target.GlowColor;
            serializedPreset.FindProperty("_underlayDilate").floatValue = _target.GlowRange;
            serializedPreset.FindProperty("_underlaySoftness").floatValue = 1f;  // 고정 (최대 블러)
            serializedPreset.FindProperty("_underlayOffsetX").floatValue = 0f;  // 고정
            serializedPreset.FindProperty("_underlayOffsetY").floatValue = 0f;  // 고정
            serializedPreset.FindProperty("_enableFace").boolValue = true;  // 항상 활성화
            serializedPreset.FindProperty("_faceDilate").floatValue = _target.FaceDilate;

            // Shadow는 Glow에서 사용 안 함
            serializedPreset.FindProperty("_enableShadow").boolValue = false;

            serializedPreset.ApplyModifiedProperties();
        }
    }
}
