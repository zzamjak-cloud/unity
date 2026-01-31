using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAT.Utility.ShapeGenerator
{
    /// <summary>
    /// 기본 도형 PNG 이미지 생성 에디터 윈도우
    /// </summary>
    public class ShapeGeneratorWindow : EditorWindow
    {
        // EditorPrefs 키
        private const string PREFS_KEY_SAVE_FOLDER = "CAT_ShapeGenerator_SaveFolder";
        private const string PREFS_KEY_SELECTED_SHAPE = "CAT_ShapeGenerator_SelectedShape";

        // 디바운스 설정
        private const double DEBOUNCE_DELAY = 0.15; // 150ms

        // 도형 생성기 리스트
        private List<IShapeGenerator> _generators;
        private string[] _generatorNames;
        private int _selectedGeneratorIndex;

        // 저장 폴더
        private string _saveFolderPath;
        private DefaultAsset _saveFolderAsset;

        // 미리보기
        private Texture2D _previewTexture;
        private bool _autoPreview = true;
        private Vector2 _scrollPosition;

        // 디바운스 상태
        private double _lastChangeTime;
        private bool _pendingUpdate;

        // 스타일 캐싱
        private GUIStyle _headerStyle;
        private GUIStyle _previewBackgroundStyle;

        [MenuItem("CAT/Utility/Shape Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<ShapeGeneratorWindow>("Shape Generator");
            window.minSize = new Vector2(350, 500);
        }

        private void OnEnable()
        {
            InitializeGenerators();
            LoadSettings();
            UpdatePreview();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SaveSettings();
            CleanupPreview();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        private void InitializeGenerators()
        {
            _generators = new List<IShapeGenerator>
            {
                // Circle 관련
                new CircleGenerator(),
                new CircleOutlineGenerator(),
                new CircleWithOutlineGenerator(),

                // Polygon (다각형) 관련 - Sides=3으로 삼각형 생성 가능
                new PolygonGenerator(),
                new PolygonOutlineGenerator(),
                new PolygonWithOutlineGenerator(),

                // Star (별) 관련
                new StarGenerator(),
                new StarOutlineGenerator(),

                // 확장: 새 도형 추가 시 여기에 등록
            };

            _generatorNames = _generators.Select(g => g.ShapeName).ToArray();
        }

        /// <summary>
        /// 에디터 업데이트 콜백 - 디바운스된 미리보기 갱신 처리
        /// </summary>
        private void OnEditorUpdate()
        {
            if (_pendingUpdate && EditorApplication.timeSinceStartup - _lastChangeTime >= DEBOUNCE_DELAY)
            {
                _pendingUpdate = false;
                UpdatePreview();
            }
        }

        /// <summary>
        /// 디바운스된 미리보기 갱신 요청
        /// </summary>
        private void RequestPreviewUpdate()
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingUpdate = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            // GUI 변경 감지 시작
            EditorGUI.BeginChangeCheck();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawSaveFolderSection();
            EditorGUILayout.Space(15);

            DrawShapeTypeSection();
            EditorGUILayout.Space(15);

            DrawShapeSettingsSection();
            EditorGUILayout.Space(15);

            DrawPreviewSection();
            EditorGUILayout.Space(15);

            DrawGenerateSection();

            EditorGUILayout.EndScrollView();

            // GUI 변경 감지 종료 - 변경 시 디바운스된 미리보기 갱신 요청
            if (EditorGUI.EndChangeCheck() && _autoPreview)
            {
                RequestPreviewUpdate();
            }
        }

        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14
                };
            }

            if (_previewBackgroundStyle == null)
            {
                _previewBackgroundStyle = new GUIStyle(GUI.skin.box);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Shape Generator", _headerStyle);
            EditorGUILayout.LabelField("기본 도형 PNG 이미지를 생성합니다.", EditorStyles.miniLabel);
        }

        private void DrawSaveFolderSection()
        {
            EditorGUILayout.LabelField("저장 위치", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // 폴더 드래그 앤 드롭 또는 선택
            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _saveFolderAsset, typeof(DefaultAsset), false);

            if (EditorGUI.EndChangeCheck() && newFolder != _saveFolderAsset)
            {
                string path = AssetDatabase.GetAssetPath(newFolder);
                if (AssetDatabase.IsValidFolder(path))
                {
                    _saveFolderAsset = newFolder;
                    _saveFolderPath = path;
                    SaveSettings();
                }
                else if (newFolder == null)
                {
                    _saveFolderAsset = null;
                    _saveFolderPath = null;
                    SaveSettings();
                }
            }

            // 현재 폴더 열기 버튼
            EditorGUI.BeginDisabledGroup(_saveFolderAsset == null);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(_saveFolderAsset);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // 경로 표시
            EditorGUI.indentLevel++;
            string displayPath = string.IsNullOrEmpty(_saveFolderPath) ? "폴더를 선택해주세요" : _saveFolderPath;
            EditorGUILayout.LabelField(displayPath, EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawShapeTypeSection()
        {
            EditorGUILayout.LabelField("도형 타입", EditorStyles.boldLabel);

            int newIndex = EditorGUILayout.Popup("Shape", _selectedGeneratorIndex, _generatorNames);

            if (newIndex != _selectedGeneratorIndex)
            {
                _selectedGeneratorIndex = newIndex;
                SaveSettings();
            }
        }

        private void DrawShapeSettingsSection()
        {
            EditorGUILayout.LabelField("도형 설정", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            _generators[_selectedGeneratorIndex].DrawSettingsGUI();
            EditorGUI.indentLevel--;
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

            // 자동 미리보기 토글
            EditorGUILayout.BeginHorizontal();
            _autoPreview = EditorGUILayout.Toggle("자동 업데이트", _autoPreview);

            if (!_autoPreview)
            {
                if (GUILayout.Button("새로고침", GUILayout.Width(70)))
                {
                    UpdatePreview();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 크기 및 Border 표시
            var border = _generators[_selectedGeneratorIndex].GetSpriteBorder();
            if (_previewTexture != null)
            {
                EditorGUILayout.LabelField($"크기: {_previewTexture.width} x {_previewTexture.height} px (트림됨)", EditorStyles.miniLabel);
            }
            else
            {
                var size = _generators[_selectedGeneratorIndex].GetTextureSize();
                EditorGUILayout.LabelField($"크기: {size.x} x {size.y} px", EditorStyles.miniLabel);
            }
            EditorGUILayout.LabelField($"Border: ({border.x}, {border.y}, {border.z}, {border.w})", EditorStyles.miniLabel);

            // 미리보기 영역
            if (_previewTexture != null)
            {
                EditorGUILayout.Space(5);

                // 체크보드 배경 + 텍스처 미리보기
                float previewSize = Mathf.Min(200, position.width - 40);
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

                // 배경 (어두운 색으로 투명도 확인 용이)
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                // 체크보드 패턴 그리기 (투명도 확인용)
                DrawCheckerboard(previewRect);

                // 텍스처 미리보기
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit, true);
            }
        }

        private void DrawCheckerboard(Rect rect)
        {
            int checkSize = 10;
            Color lightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            Color darkColor = new Color(0.2f, 0.2f, 0.2f, 1f);

            int cols = Mathf.CeilToInt(rect.width / checkSize);
            int rows = Mathf.CeilToInt(rect.height / checkSize);

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    bool isLight = (x + y) % 2 == 0;
                    Rect checkRect = new Rect(
                        rect.x + x * checkSize,
                        rect.y + y * checkSize,
                        Mathf.Min(checkSize, rect.xMax - (rect.x + x * checkSize)),
                        Mathf.Min(checkSize, rect.yMax - (rect.y + y * checkSize))
                    );
                    EditorGUI.DrawRect(checkRect, isLight ? lightColor : darkColor);
                }
            }
        }

        private void DrawGenerateSection()
        {
            // 파일명 미리보기
            string fileName = _generators[_selectedGeneratorIndex].GetFileName();
            EditorGUILayout.LabelField($"파일명: {fileName}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // 생성 버튼
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_saveFolderPath));

            if (GUILayout.Button("PNG 생성", GUILayout.Height(35)))
            {
                GenerateAndSave();
            }

            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(_saveFolderPath))
            {
                EditorGUILayout.HelpBox("저장 폴더를 먼저 선택해주세요.", MessageType.Warning);
            }
        }

        private void UpdatePreview()
        {
            CleanupPreview();

            if (_generators != null && _selectedGeneratorIndex < _generators.Count)
            {
                _previewTexture = _generators[_selectedGeneratorIndex].Generate();
                // 투명 영역 트림
                _previewTexture = BaseShapeGenerator.TrimTexture(_previewTexture);
            }

            Repaint();
        }

        private void CleanupPreview()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private void GenerateAndSave()
        {
            var generator = _generators[_selectedGeneratorIndex];
            var texture = generator.Generate();

            // 투명 영역 트림
            texture = BaseShapeGenerator.TrimTexture(texture);

            string fileName = generator.GetFileName();
            string fullPath = Path.Combine(_saveFolderPath, fileName);

            // PNG로 인코딩 후 저장
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);

            DestroyImmediate(texture);

            // Asset Database 새로고침
            AssetDatabase.Refresh();

            // Import Settings 자동 설정 (Border 포함)
            Vector4 border = generator.GetSpriteBorder();
            ApplyTextureImportSettings(fullPath, border);

            // 생성된 파일 선택
            var createdAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (createdAsset != null)
            {
                Selection.activeObject = createdAsset;
                EditorGUIUtility.PingObject(createdAsset);
            }

            Debug.Log($"[ShapeGenerator] 생성 완료: {fullPath} (Border: {border})");
        }

        private void ApplyTextureImportSettings(string assetPath, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;

                // Sprite Border 설정 (9-slice 용)
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);

                // 압축 설정 (모바일 최적화)
                importer.textureCompression = TextureImporterCompression.Compressed;

                // Platform 별 설정
                TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = true;
                androidSettings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(androidSettings);

                TextureImporterPlatformSettings iosSettings = importer.GetPlatformTextureSettings("iPhone");
                iosSettings.overridden = true;
                iosSettings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(iosSettings);

                importer.SaveAndReimport();
            }
        }

        private void SaveSettings()
        {
            // 저장 폴더 GUID 저장
            if (!string.IsNullOrEmpty(_saveFolderPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(_saveFolderPath);
                EditorPrefs.SetString(PREFS_KEY_SAVE_FOLDER, guid);
            }
            else
            {
                EditorPrefs.DeleteKey(PREFS_KEY_SAVE_FOLDER);
            }

            // 선택된 도형 인덱스 저장
            EditorPrefs.SetInt(PREFS_KEY_SELECTED_SHAPE, _selectedGeneratorIndex);
        }

        private void LoadSettings()
        {
            // 저장 폴더 로드
            string guid = EditorPrefs.GetString(PREFS_KEY_SAVE_FOLDER, "");
            if (!string.IsNullOrEmpty(guid))
            {
                _saveFolderPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(_saveFolderPath) && AssetDatabase.IsValidFolder(_saveFolderPath))
                {
                    _saveFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_saveFolderPath);
                }
                else
                {
                    _saveFolderPath = null;
                    _saveFolderAsset = null;
                }
            }

            // 선택된 도형 인덱스 로드
            _selectedGeneratorIndex = EditorPrefs.GetInt(PREFS_KEY_SELECTED_SHAPE, 0);
            _selectedGeneratorIndex = Mathf.Clamp(_selectedGeneratorIndex, 0, _generators.Count - 1);
        }
    }
}
