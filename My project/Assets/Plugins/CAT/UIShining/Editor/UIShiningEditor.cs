using System.IO;
using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(UIShining))]
    public class UIShiningEditor : Editor
    {
        private UIShining _target;
        private bool _isPlaying;
        private double _startTime;
        private double _prevTime;
        private const double DURATION = 60.0;
        private const string DEFAULT_SAVE_FOLDER = "Assets/Plugins/CAT/UIShining/Materials";

        private void OnEnable()
        {
            _target = (UIShining)target;
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_isPlaying)
                StopTest();
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 머티리얼 상태 표시 + _savedMaterial 필드
            DrawMaterialStatus();

            EditorGUILayout.Space(4f);

            DrawDefaultInspector();

            // 변경사항이 있으면 머티리얼 자동 업데이트
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorApplication.delayCall += () =>
                {
                    if (_target != null)
                        AutoUpdateSavedMaterial();
                };
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(4f);

                if (_isPlaying)
                {
                    double remaining = System.Math.Max(0.0, DURATION - (EditorApplication.timeSinceStartup - _startTime));
                    EditorGUILayout.HelpBox($"에디터 테스트 재생 중... (남은 시간: {remaining:F1}초)", MessageType.Info);

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("재생 중지", GUILayout.Height(24f)))
                        StopTest();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("에디터 재생 (60초)", GUILayout.Height(24f)))
                        StartTest();
                    GUI.backgroundColor = Color.white;
                }
            }

            EditorGUILayout.Space(4f);

            // 머티리얼 저장 버튼
            DrawMaterialButtons();

            if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
                SceneView.RepaintAll();
        }

        // ─────────────────────────────────────────────
        // 머티리얼 상태 표시
        // ─────────────────────────────────────────────

        /// <summary>
        /// 저장된 머티리얼 경로 표시 및 _savedMaterial 필드 표시
        /// </summary>
        private void DrawMaterialStatus()
        {
            var savedMatProp = serializedObject.FindProperty("_savedMaterial");
            Material savedMat = savedMatProp != null ? savedMatProp.objectReferenceValue as Material : null;

            if (savedMat != null && AssetDatabase.Contains(savedMat))
            {
                string matPath = AssetDatabase.GetAssetPath(savedMat);
                EditorGUILayout.HelpBox($"저장된 머티리얼: {matPath}", MessageType.Info);
            }

            // _savedMaterial 필드 표시 (드래그&드롭으로 불러오기 가능)
            if (savedMatProp != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(savedMatProp, new GUIContent("저장된 머티리얼", "머티리얼 에셋을 드래그&드롭하여 불러올 수 있습니다"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();

                    // 머티리얼의 프로퍼티를 컴포넌트 필드에 동기화 후 재초기화
                    Material newMat = savedMatProp.objectReferenceValue as Material;
                    if (newMat != null && UIShining.IsUIShiningShader(newMat))
                    {
                        SyncFromMaterial(newMat);
                    }

                    EditorApplication.delayCall += () =>
                    {
                        if (_target != null && _target.isActiveAndEnabled)
                        {
                            _target.ResetMaterial();
                        }
                    };
                }
            }
        }

        /// <summary>
        /// 머티리얼의 프로퍼티 값을 현재 컴포넌트 필드에 동기화
        /// </summary>
        private void SyncFromMaterial(Material mat)
        {
            serializedObject.Update();
            SyncFieldsFromMaterial(serializedObject, mat);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_target);
        }

        // ─────────────────────────────────────────────
        // 머티리얼 저장 버튼
        // ─────────────────────────────────────────────

        /// <summary>
        /// 머티리얼 저장 버튼 그리기
        /// </summary>
        private void DrawMaterialButtons()
        {
            EditorGUILayout.LabelField("머티리얼 관리", EditorStyles.boldLabel);

            if (GUILayout.Button("신규 머티리얼 생성", GUILayout.Height(30)))
            {
                SaveAsNewMaterial();
            }
        }

        /// <summary>
        /// 현재 프로퍼티 값으로 새 머티리얼 에셋을 생성하고 _savedMaterial에 할당
        /// </summary>
        private void SaveAsNewMaterial()
        {
            Shader shader = Shader.Find(UIShining.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[UIShining] 셰이더를 찾을 수 없습니다: {UIShining.SHADER_NAME}");
                return;
            }

            EnsureFolderExists(DEFAULT_SAVE_FOLDER);

            string sanitizedName = SanitizeFileName(_target.gameObject.name);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{DEFAULT_SAVE_FOLDER}/{sanitizedName}.mat"
            );

            // 새 머티리얼 생성 및 현재 프로퍼티 적용
            Material saved = new Material(shader)
            {
                name = sanitizedName,
                hideFlags = HideFlags.None
            };

            ApplyComponentPropertiesToMaterial(saved);

            AssetDatabase.CreateAsset(saved, assetPath);
            AssetDatabase.SaveAssets();

            // _savedMaterial에 할당 (Undo 지원)
            Undo.RecordObject(_target, "UIShining 신규 머티리얼 생성");
            _target.SavedMaterial = saved;
            EditorUtility.SetDirty(_target);

            if (PrefabUtility.IsPartOfAnyPrefab(_target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(_target);

            Debug.Log($"[UIShining] 머티리얼 저장 완료: {assetPath}");
            EditorGUIUtility.PingObject(saved);
        }

        /// <summary>
        /// GUI 값 변경 시 저장된 머티리얼 에셋을 자동 갱신하고,
        /// 같은 머티리얼을 공유하는 다른 UIShining 컴포넌트의 필드도 동기화
        /// </summary>
        private void AutoUpdateSavedMaterial()
        {
            if (_target == null) return;

            Material savedMat = _target.SavedMaterial;
            if (savedMat == null || !AssetDatabase.Contains(savedMat)) return;

            // 저장된 에셋에 현재 값 반영
            ApplyComponentPropertiesToMaterial(savedMat);
            EditorUtility.SetDirty(savedMat);

            // 같은 머티리얼을 공유하는 다른 UIShining 컴포넌트의 필드 동기화
            SyncSharedMaterialUsers(savedMat);
        }

        /// <summary>
        /// 같은 저장된 머티리얼을 참조하는 모든 UIShining 컴포넌트의 필드를 머티리얼에서 동기화
        /// </summary>
        private void SyncSharedMaterialUsers(Material savedMat)
        {
            var allShinings = FindObjectsByType<UIShining>(FindObjectsSortMode.None);
            foreach (var shining in allShinings)
            {
                // 자기 자신은 건너뛰기
                if (shining == _target) continue;
                // 같은 머티리얼을 참조하지 않으면 건너뛰기
                if (shining.SavedMaterial != savedMat) continue;

                // 머티리얼 값을 컴포넌트 필드에 동기화
                var so = new SerializedObject(shining);
                SyncFieldsFromMaterial(so, savedMat);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(shining);
            }
        }

        // ─────────────────────────────────────────────
        // 머티리얼 ↔ 컴포넌트 필드 동기화
        // ─────────────────────────────────────────────

        /// <summary>
        /// SerializedObject의 필드를 머티리얼 프로퍼티에서 읽어 설정
        /// </summary>
        private void SyncFieldsFromMaterial(SerializedObject so, Material mat)
        {
            SetFieldFloat(so, mat, "_widthStart", "_WidthStart");
            SetFieldFloat(so, mat, "_widthEnd", "_WidthEnd");
            SetFieldFloat(so, mat, "_intensity", "_Intensity");
            SetFieldFloat(so, mat, "_curvatureStart", "_CurvatureStart");
            SetFieldFloat(so, mat, "_curvatureEnd", "_CurvatureEnd");
            SetFieldFloat(so, mat, "_angle", "_Angle");
            SetFieldFloat(so, mat, "_progressOffset", "_ProgressOffset");
            SetFieldFloat(so, mat, "_softness", "_Softness");
            SetFieldFloat(so, mat, "_burnBias", "_BurnBias");
            SetFieldFloat(so, mat, "_blendStrength", "_BlendStrength");

            if (mat.HasColor("_ShineColor"))
            {
                var prop = so.FindProperty("_shineColor");
                if (prop != null) prop.colorValue = mat.GetColor("_ShineColor");
            }
        }

        private void SetFieldFloat(SerializedObject so, Material mat, string fieldName, string matProp)
        {
            if (!mat.HasFloat(matProp)) return;
            var prop = so.FindProperty(fieldName);
            if (prop != null) prop.floatValue = mat.GetFloat(matProp);
        }

        /// <summary>
        /// 현재 컴포넌트 프로퍼티 값을 머티리얼에 적용
        /// serializedObject에 의존하지 않고 SerializedObject를 새로 생성하여 안전하게 접근
        /// </summary>
        private void ApplyComponentPropertiesToMaterial(Material mat)
        {
            var so = new SerializedObject(_target);

            SetMatFloat(so, mat, "_widthStart", "_WidthStart");
            SetMatFloat(so, mat, "_widthEnd", "_WidthEnd");
            SetMatFloat(so, mat, "_intensity", "_Intensity");
            SetMatFloat(so, mat, "_curvatureStart", "_CurvatureStart");
            SetMatFloat(so, mat, "_curvatureEnd", "_CurvatureEnd");
            SetMatFloat(so, mat, "_angle", "_Angle");
            SetMatFloat(so, mat, "_progressOffset", "_ProgressOffset");
            SetMatFloat(so, mat, "_softness", "_Softness");
            SetMatFloat(so, mat, "_burnBias", "_BurnBias");
            SetMatFloat(so, mat, "_blendStrength", "_BlendStrength");

            // SoftMaskLight Hidden 변형용
            var softness = so.FindProperty("_softness");
            if (softness != null) mat.SetFloat("_ShineSoftness", softness.floatValue);

            var shineColor = so.FindProperty("_shineColor");
            if (shineColor != null) mat.SetColor("_ShineColor", shineColor.colorValue);
        }

        private void SetMatFloat(SerializedObject so, Material mat, string fieldName, string matProp)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) mat.SetFloat(matProp, prop.floatValue);
        }

        private void SetColorField(SerializedObject so, Material mat, string fieldName, string matProp)
        {
            var prop = so.FindProperty(fieldName);
            if (prop != null) mat.SetColor(matProp, prop.colorValue);
        }

        // ─────────────────────────────────────────────
        // 유틸리티
        // ─────────────────────────────────────────────

        /// <summary>
        /// 폴더 경로를 재귀적으로 생성
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
        /// 파일명에서 사용 불가 문자를 제거
        /// </summary>
        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // ─────────────────────────────────────────────
        // 테스트 재생
        // ─────────────────────────────────────────────

        private void StartTest()
        {
            if (_isPlaying) return;

            _startTime = EditorApplication.timeSinceStartup;
            _prevTime = _startTime;
            _isPlaying = true;
            _target.ResetProgressToStart();
            EditorApplication.update += EditorUpdate;
        }

        private void StopTest()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            if (_target != null)
                _target.ResetProgressToStart();
            Repaint();
        }

        /// <summary>
        /// EditorApplication.update 콜백: timeSinceStartup 기반으로 deltaTime을 계산하여
        /// UIShining.EditorAdvance()를 직접 호출한다. [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        private void EditorUpdate()
        {
            if (_target == null)
            {
                StopTest();
                return;
            }

            double now = EditorApplication.timeSinceStartup;

            if (now - _startTime >= DURATION)
            {
                StopTest();
                return;
            }

            float dt = (float)(now - _prevTime);
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _prevTime = now;

            _target.EditorAdvance(dt);

            Repaint();
            SceneView.RepaintAll();
        }
    }
}
