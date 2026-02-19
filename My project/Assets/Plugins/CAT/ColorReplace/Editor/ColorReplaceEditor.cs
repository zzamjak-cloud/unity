using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ColorReplace))]
    public class ColorReplaceEditor : Editor
    {
        // EditorPrefs 키
        private const string PREF_PRESET_FOLDER = "ColorReplace_PresetFolder";
        private const string PRESET_SUBFOLDER = "Presets";

        // SerializedProperty
        private SerializedProperty presetProp;
        private SerializedProperty hsvRangeMin;
        private SerializedProperty hsvRangeMax;
        private SerializedProperty hsvAdjust;

        // 프리셋 관리
        private ColorReplace _target;
        private ColorReplacePreset _selectedPreset;
        private ColorReplacePreset[] _availablePresets;
        private string[] _presetNames;
        private int _selectedPresetIndex = 0;

        // 저장 폴더
        private DefaultAsset _presetFolder;
        private string _presetFolderPath;

        // 다음 프레임에 실행할 액션
        private System.Action _delayedAction;

        // 에디터 전용 컬러 피커 (HSV 범위 자동 설정용)
        private Color pickerColor = Color.red;

        private void OnEnable()
        {
            _target = (ColorReplace)target;

            presetProp = serializedObject.FindProperty("_preset");
            hsvRangeMin = serializedObject.FindProperty("_hsvRangeMin");
            hsvRangeMax = serializedObject.FindProperty("_hsvRangeMax");
            hsvAdjust = serializedObject.FindProperty("_hsvAdjust");

            // 저장된 폴더 경로 불러오기
            LoadPresetFolder();

            // 프리셋 리스트 로드
            RefreshPresetList();

            // 현재 할당된 Preset 확인
            if (_target.Preset != null)
            {
                _selectedPreset = _target.Preset;
                FindPresetIndex();
            }

            // 현재 HSV Range에서 대표 색상 추정
            if (hsvRangeMin != null && hsvRangeMax != null)
            {
                float midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue) * 0.5f;
                if (hsvRangeMin.floatValue > hsvRangeMax.floatValue)
                {
                    // wrap-around 케이스
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
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        /// <summary>
        /// 이 에디터 스크립트 위치 기준으로 기본 저장 폴더 경로를 동적 계산
        /// ColorReplace 폴더를 어디에 두든 상대 경로가 유지됨
        /// </summary>
        private string GetDefaultPresetFolder()
        {
            var guids = AssetDatabase.FindAssets("t:Script ColorReplaceEditor");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // "...ColorReplace/Editor/ColorReplaceEditor.cs" → "...ColorReplace"
                int editorIdx = scriptPath.LastIndexOf("/Editor/");
                if (editorIdx >= 0)
                {
                    return scriptPath.Substring(0, editorIdx) + "/" + PRESET_SUBFOLDER;
                }
            }
            return "Assets/Plugins/CAT/ColorReplace/" + PRESET_SUBFOLDER;
        }

        private void LoadPresetFolder()
        {
            string defaultFolder = GetDefaultPresetFolder();
            _presetFolderPath = EditorPrefs.GetString(PREF_PRESET_FOLDER, defaultFolder);

            if (!string.IsNullOrEmpty(_presetFolderPath) && AssetDatabase.IsValidFolder(_presetFolderPath))
            {
                _presetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_presetFolderPath);
            }
            else
            {
                _presetFolderPath = defaultFolder;
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

            return !Mathf.Approximately(_target.HSVRangeMin, _selectedPreset.HSVRangeMin) ||
                   !Mathf.Approximately(_target.HSVRangeMax, _selectedPreset.HSVRangeMax) ||
                   _target.HSVAdjust != _selectedPreset.HSVAdjust;
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
                return;
            }

            // 렌더러 타입 표시
            Component renderer = _target.GetComponent<SpriteRenderer>();
            bool isUIComponent = renderer == null;
            if (isUIComponent)
            {
                renderer = _target.GetComponent<UnityEngine.UI.Graphic>();
            }

            string rendererType = isUIComponent ? "UI Graphic" : "SpriteRenderer";
            EditorGUILayout.HelpBox($"Mode: {rendererType}", MessageType.None);

            EditorGUILayout.Space(5);

            // 프리셋 관리 UI
            DrawPresetManagement();

            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            // 컬러 피커 (HSV 범위 자동 설정용)
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
            {
                SetHSVRangeFromColor(pickerColor, 0.02f);
            }
            if (GUILayout.Button("Similar", GUILayout.Width(80)))
            {
                SetHSVRangeFromColor(pickerColor, 0.1f);
            }
            if (GUILayout.Button("Wide", GUILayout.Width(80)))
            {
                SetHSVRangeFromColor(pickerColor, 0.2f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // HSV Range
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
            {
                hsvRangeMin.floatValue = newMinValue;
            }
            if (Mathf.Abs(newMaxValue - hsvRangeMax.floatValue) > 0.001f)
            {
                hsvRangeMax.floatValue = newMaxValue;
            }

            EditorGUILayout.Space(10);

            // HSV Adjust
            EditorGUILayout.LabelField("HSV Adjust", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Vector4 currentAdjust = hsvAdjust.vector4Value;
            currentAdjust.x = EditorGUILayout.Slider(new GUIContent("H", "색조(Hue) 조정"), currentAdjust.x, -1f, 1f);
            currentAdjust.y = EditorGUILayout.Slider(new GUIContent("S", "채도(Saturation) 조정"), currentAdjust.y, -1f, 1f);
            currentAdjust.z = EditorGUILayout.Slider(new GUIContent("V", "명도(Value) 조정"), currentAdjust.z, -1f, 1f);
            currentAdjust.w = EditorGUILayout.Slider(new GUIContent("A", "알파(Alpha) 조정"), currentAdjust.w, -1f, 1f);

            EditorGUI.indentLevel--;
            hsvAdjust.vector4Value = currentAdjust;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                if (PrefabUtility.IsPartOfAnyPrefab(target))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
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

            EditorGUILayout.Space(10);

            // Reset 및 캐시 클리어 버튼
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
            }

            // 캐시 클리어 버튼 (플레이 모드에서만)
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Clear Cache"))
                {
                    ColorReplace.ClearMaterialCache();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 캐시 통계 (플레이 모드)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                var stats = ColorReplace.GetCacheStats();
                EditorGUILayout.HelpBox($"Cache Stats: {stats}", MessageType.None);
            }
        }

        private void DrawPresetManagement()
        {
            EditorGUILayout.LabelField("프리셋 관리", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 저장 폴더 지정
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("저장 폴더", "프리셋이 저장될 기본 폴더입니다"),
                GUILayout.Width(100));

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
                "NewColorReplacePreset",
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
                var newPreset = ScriptableObject.CreateInstance<ColorReplacePreset>();
                newPreset.CopyFrom(_target);

                AssetDatabase.CreateAsset(newPreset, path);
                AssetDatabase.SaveAssets();

                // 현재 컴포넌트에 할당
                _selectedPreset = newPreset;
                _target.Preset = newPreset;

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

        private void RefreshPresetList()
        {
            // 모든 ColorReplacePreset 에셋 찾기
            string[] guids = AssetDatabase.FindAssets("t:ColorReplacePreset");
            _availablePresets = new ColorReplacePreset[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _availablePresets[i] = AssetDatabase.LoadAssetAtPath<ColorReplacePreset>(path);
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

            // 현재 선택된 프리셋이 있다면 인덱스 찾기
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

            // 못 찾으면 None으로
            _selectedPresetIndex = 0;
        }

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

            if (maxRange < minRange) // wrap-around
            {
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
            }
            else // 일반 케이스
            {
                if (minRange > 0)
                {
                    Rect leftOverlay = new Rect(
                        rect.x,
                        rect.y,
                        rect.width * minRange,
                        rect.height
                    );
                    EditorGUI.DrawRect(leftOverlay, overlayColor);
                }

                if (maxRange < 1)
                {
                    Rect rightOverlay = new Rect(
                        rect.x + rect.width * maxRange,
                        rect.y,
                        rect.width * (1f - maxRange),
                        rect.height
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
