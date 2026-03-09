using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

namespace CAT.Effects
{
    [CustomEditor(typeof(Windable))]
    public class WindableEditor : Editor
    {
        private Windable _target;
        private bool _isPlaying = false;
        private double _startTime;
        private const float DURATION = 60.0f; // 테스트 재생 시간
        private const string DEFAULT_SAVE_FOLDER = "Assets/Plugins/CAT/Windable/Materials";

        // GUI 스타일
        private GUIStyle _headerStyle;

        private void OnEnable()
        {
            _target = (Windable)target;
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;

            // 컴포넌트 유효성 검사
            ValidateTarget();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();

            serializedObject.Update();

            // 헤더
            DrawComponentHeader();

            // 타입별 경고 메시지
            DrawTypeValidation();

            // 타입 선택 (읽기 전용, 자동 감지됨)
            DrawTypeInfo();

            EditorGUILayout.Space();

            // 머티리얼 상태 표시 + _savedMaterial 필드
            DrawMaterialStatus();

            EditorGUILayout.Space();

            // 기본 프로퍼티들 그리기
            DrawWindProperties();

            // 변경사항이 있으면 머티리얼 업데이트
            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorApplication.delayCall += () => {
                    if (_target != null)
                    {
                        _target.UpdateMaterialProperties();
                        AutoUpdateSavedMaterial();
                    }
                };
            }

            EditorGUILayout.Space();

            // 머티리얼 저장/불러오기 버튼
            DrawMaterialButtons();

            EditorGUILayout.Space();

            // 테스트 버튼
            DrawTestButtons();
        }

        /// <summary>
        /// GUI 스타일 초기화
        /// </summary>
        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        /// <summary>
        /// 컴포넌트 헤더 그리기
        /// </summary>
        private void DrawComponentHeader()
        {
            EditorGUILayout.LabelField("🍃 Windable Component", _headerStyle);
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// 타입 정보 표시
        /// </summary>
        private void DrawTypeInfo()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("감지된 타입", _target.WindableTypeValue);
            EditorGUI.EndDisabledGroup();

            // 컴포넌트 정보 표시
            string componentInfo = GetComponentInfo();
            if (!string.IsNullOrEmpty(componentInfo))
            {
                EditorGUILayout.HelpBox(componentInfo, MessageType.Info);
            }
        }

        /// <summary>
        /// 타입별 유효성 검사 및 경고
        /// </summary>
        private void DrawTypeValidation()
        {
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (spriteRenderer == null && graphic == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer 또는 UI Graphic 컴포넌트가 필요합니다!",
                    MessageType.Error
                );

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("SpriteRenderer 추가"))
                {
                    Undo.AddComponent<SpriteRenderer>(_target.gameObject);
                }
                if (GUILayout.Button("Image 추가"))
                {
                    Undo.AddComponent<Image>(_target.gameObject);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (spriteRenderer != null && graphic != null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer와 UI Graphic이 모두 있습니다. SpriteRenderer를 우선 사용합니다.",
                    MessageType.Warning
                );
            }

            // Sprite 타입 특별 검사
            if (_target.WindableTypeValue == WindableType.Sprite && spriteRenderer?.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ SpriteRenderer에 스프라이트가 할당되지 않았습니다.",
                    MessageType.Warning
                );
            }

            // UI 타입 특별 검사
            if (_target.WindableTypeValue == WindableType.UI && graphic is Image img && img.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Image에 스프라이트가 할당되지 않았습니다.",
                    MessageType.Warning
                );
            }
        }

        /// <summary>
        /// 바람 효과 프로퍼티 그리기
        /// </summary>
        private void DrawWindProperties()
        {
            EditorGUILayout.LabelField("바람 효과 설정", EditorStyles.boldLabel);

            // _MainTex와 _ClipRect는 숨김 처리되어 있으므로 제외
            DrawPropertyField("_NoiseTex", "노이즈 텍스처");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("기본 설정", EditorStyles.miniBoldLabel);
            DrawPropertyField("_RotateUV", "UV 회전");
            DrawPropertyField("_WindSpeed", "바람 속도");
            DrawPropertyField("_WindStrength", "바람 강도");
            DrawPropertyField("_WindFrequency", "바람 주파수");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("고급 설정", EditorStyles.miniBoldLabel);
            DrawPropertyField("_WindDirection", "바람 방향");
            DrawPropertyField("_WindScale", "바람 스케일");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("이미지 오프셋", EditorStyles.miniBoldLabel);
            DrawPropertyField("_ImageOffsetX", "X 오프셋");
            DrawPropertyField("_ImageOffsetY", "Y 오프셋");
            DrawPropertyField("_ImageScale", "이미지 스케일");
        }

        /// <summary>
        /// 프로퍼티 필드 그리기 도우미 메서드
        /// </summary>
        private void DrawPropertyField(string propertyName, string displayName = null)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(displayName ?? property.displayName));
            }
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
                    if (newMat != null && Windable.IsWindableShader(newMat))
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
        // 머티리얼 저장/불러오기 버튼
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
            Shader shader = Shader.Find(Windable.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[Windable] 셰이더를 찾을 수 없습니다: {Windable.SHADER_NAME}");
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
            Undo.RecordObject(_target, "Windable 신규 머티리얼 생성");
            _target.SavedMaterial = saved;
            EditorUtility.SetDirty(_target);

            if (PrefabUtility.IsPartOfAnyPrefab(_target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(_target);

            Debug.Log($"[Windable] 머티리얼 저장 완료: {assetPath}");
            EditorGUIUtility.PingObject(saved);
        }

        /// <summary>
        /// GUI 값 변경 시 저장된 머티리얼 에셋을 자동 갱신하고,
        /// 같은 머티리얼을 공유하는 다른 Windable 컴포넌트의 필드도 동기화
        /// </summary>
        private void AutoUpdateSavedMaterial()
        {
            if (_target == null) return;

            Material savedMat = _target.SavedMaterial;
            if (savedMat == null || !AssetDatabase.Contains(savedMat)) return;

            // 저장된 에셋에 현재 값 반영
            ApplyComponentPropertiesToMaterial(savedMat);
            EditorUtility.SetDirty(savedMat);

            // 같은 머티리얼을 공유하는 다른 Windable 컴포넌트의 필드 동기화
            SyncSharedMaterialUsers(savedMat);
        }

        /// <summary>
        /// 같은 저장된 머티리얼을 참조하는 모든 Windable 컴포넌트의 필드를 머티리얼에서 동기화
        /// </summary>
        private void SyncSharedMaterialUsers(Material savedMat)
        {
            var allWindables = FindObjectsByType<Windable>(FindObjectsSortMode.None);
            foreach (var windable in allWindables)
            {
                // 자기 자신은 건너뛰기
                if (windable == _target) continue;
                // 같은 머티리얼을 참조하지 않으면 건너뛰기
                if (windable.SavedMaterial != savedMat) continue;

                // 머티리얼 값을 컴포넌트 필드에 동기화
                var so = new SerializedObject(windable);
                SyncFieldsFromMaterial(so, savedMat);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(windable);

                // 런타임 머티리얼도 갱신
                windable.UpdateMaterialProperties();
            }
        }

        /// <summary>
        /// SerializedObject의 필드를 머티리얼 프로퍼티에서 읽어 설정
        /// </summary>
        private void SyncFieldsFromMaterial(SerializedObject so, Material mat)
        {
            SetFloatField(so, mat, "_RotateUV");
            SetFloatField(so, mat, "_WindSpeed");
            SetFloatField(so, mat, "_WindStrength");
            SetFloatField(so, mat, "_WindFrequency");
            SetVectorField(so, mat, "_WindDirection");
            SetFloatField(so, mat, "_WindScale");
            SetFloatField(so, mat, "_ImageOffsetX");
            SetFloatField(so, mat, "_ImageOffsetY");
            SetFloatField(so, mat, "_ImageScale");

            if (mat.HasTexture("_NoiseTex"))
            {
                var prop = so.FindProperty("_NoiseTex");
                if (prop != null) prop.objectReferenceValue = mat.GetTexture("_NoiseTex");
            }
        }

        private void SetFloatField(SerializedObject so, Material mat, string name)
        {
            if (!mat.HasFloat(name)) return;
            var prop = so.FindProperty(name);
            if (prop != null) prop.floatValue = mat.GetFloat(name);
        }

        private void SetVectorField(SerializedObject so, Material mat, string name)
        {
            if (!mat.HasVector(name)) return;
            var prop = so.FindProperty(name);
            if (prop != null) prop.vector4Value = mat.GetVector(name);
        }

        /// <summary>
        /// 현재 컴포넌트 프로퍼티 값을 머티리얼에 적용
        /// serializedObject에 의존하지 않고 SerializedObject를 새로 생성하여 안전하게 접근
        /// </summary>
        private void ApplyComponentPropertiesToMaterial(Material mat)
        {
            var so = new SerializedObject(_target);
            var noiseTex = so.FindProperty("_NoiseTex");
            var rotateUV = so.FindProperty("_RotateUV");
            var windSpeed = so.FindProperty("_WindSpeed");
            var windStrength = so.FindProperty("_WindStrength");
            var windFrequency = so.FindProperty("_WindFrequency");
            var windDirection = so.FindProperty("_WindDirection");
            var windScale = so.FindProperty("_WindScale");
            var imageOffsetX = so.FindProperty("_ImageOffsetX");
            var imageOffsetY = so.FindProperty("_ImageOffsetY");
            var imageScale = so.FindProperty("_ImageScale");

            if (noiseTex != null) mat.SetTexture("_NoiseTex", noiseTex.objectReferenceValue as Texture);
            if (rotateUV != null) mat.SetFloat("_RotateUV", rotateUV.floatValue);
            if (windSpeed != null) mat.SetFloat("_WindSpeed", windSpeed.floatValue);
            if (windStrength != null) mat.SetFloat("_WindStrength", windStrength.floatValue);
            if (windFrequency != null) mat.SetFloat("_WindFrequency", windFrequency.floatValue);
            if (windDirection != null) mat.SetVector("_WindDirection", windDirection.vector4Value);
            if (windScale != null) mat.SetFloat("_WindScale", windScale.floatValue);
            if (imageOffsetX != null) mat.SetFloat("_ImageOffsetX", imageOffsetX.floatValue);
            if (imageOffsetY != null) mat.SetFloat("_ImageOffsetY", imageOffsetY.floatValue);
            if (imageScale != null) mat.SetFloat("_ImageScale", imageScale.floatValue);
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
        // 테스트
        // ─────────────────────────────────────────────

        /// <summary>
        /// 테스트 버튼들 그리기 (재생/중지만)
        /// </summary>
        private void DrawTestButtons()
        {
            EditorGUILayout.LabelField("테스트", EditorStyles.boldLabel);

            // 재생 상태에 따른 버튼 색상 변경
            if (_isPlaying)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button($"⏹️ 테스트 중지 (남은 시간: {DURATION - (EditorApplication.timeSinceStartup - _startTime):F1}s)", GUILayout.Height(30)))
                {
                    StopTest();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button($"▶️ 바람 효과 테스트 ({DURATION}초)", GUILayout.Height(30)))
                {
                    StartTest();
                }
                GUI.backgroundColor = Color.white;
            }
        }

        /// <summary>
        /// 컴포넌트 정보 문자열 생성
        /// </summary>
        private string GetComponentInfo()
        {
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (_target.WindableTypeValue == WindableType.Sprite && spriteRenderer != null)
            {
                return $"SpriteRenderer 사용 중 | 스프라이트: {(spriteRenderer.sprite ? spriteRenderer.sprite.name : "없음")}";
            }
            else if (_target.WindableTypeValue == WindableType.UI && graphic != null)
            {
                string graphicType = graphic.GetType().Name;
                if (graphic is Image img)
                {
                    return $"UI {graphicType} 사용 중 | 스프라이트: {(img.sprite ? img.sprite.name : "없음")}";
                }
                return $"UI {graphicType} 사용 중";
            }

            return "컴포넌트 정보를 읽을 수 없음";
        }

        /// <summary>
        /// 타겟 유효성 검사
        /// </summary>
        private void ValidateTarget()
        {
            if (_target == null) return;

            // 필요한 컴포넌트가 없는 경우 경고 로그
            var spriteRenderer = _target.GetComponent<SpriteRenderer>();
            var graphic = _target.GetComponent<Graphic>();

            if (spriteRenderer == null && graphic == null)
            {
                Debug.LogWarning($"[Windable] {_target.name}: SpriteRenderer 또는 UI Graphic 컴포넌트가 필요합니다.");
            }
        }

        /// <summary>
        /// 테스트 시작
        /// </summary>
        private void StartTest()
        {
            if (_isPlaying) return;

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
            _isPlaying = true;

            Debug.Log($"[Windable] {_target.name}: 바람 효과 테스트 시작 ({DURATION}초)");
        }

        /// <summary>
        /// 테스트 중지
        /// </summary>
        private void StopTest()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            if (_target != null)
                _target.UpdateMaterialProperties(0);
            Repaint();
            SceneView.RepaintAll();

            Debug.Log($"[Windable] {_target.name}: 바람 효과 테스트 중지");
        }

        /// <summary>
        /// 에디터 업데이트 루프: EditorAdvance()를 직접 호출하여 애니메이션 진행.
        /// [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        private void EditorUpdate()
        {
            if (_target == null)
            {
                StopTest();
                return;
            }

            double elapsedTime = EditorApplication.timeSinceStartup - _startTime;

            if (elapsedTime >= DURATION)
            {
                StopTest();
                return;
            }

            // 경과 시간을 EditorAdvance로 전달하여 애니메이션 효과
            _target.EditorAdvance((float)elapsedTime);

            // 인스펙터 + 씬뷰 업데이트
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
