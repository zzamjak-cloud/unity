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
        private const string PREFS_KEY_SAVE_FOLDER = "CAT_ShapeGenerator_SaveFolder";
        private const string PREFS_KEY_SELECTED_SHAPE = "CAT_ShapeGenerator_SelectedShape";
        private const double DEBOUNCE_DELAY = 0.15;

        private List<IShapeGenerator> _generators;
        private string[] _generatorNames;
        private int _selectedGeneratorIndex;

        private string _saveFolderPath;
        private DefaultAsset _saveFolderAsset;

        private Texture2D _previewTexture;
        private Vector2 _scrollPosition;

        private double _lastChangeTime;
        private bool _pendingUpdate;

        [MenuItem("CAT/Utility/Shape Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<ShapeGeneratorWindow>("Shape Generator");
            window.minSize = new Vector2(350, 400);
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
                new CircleGenerator(),
                new CircleOutlineGenerator(),
                new CircleWithOutlineGenerator(),
                new PolygonGenerator(),
                new PolygonOutlineGenerator(),
                new PolygonWithOutlineGenerator(),
                new StarGenerator(),
                new StarOutlineGenerator(),
            };

            _generatorNames = _generators.Select(g => g.ShapeName).ToArray();
        }

        private void OnEditorUpdate()
        {
            if (_pendingUpdate && EditorApplication.timeSinceStartup - _lastChangeTime >= DEBOUNCE_DELAY)
            {
                _pendingUpdate = false;
                UpdatePreview();
            }
        }

        private void RequestPreviewUpdate()
        {
            _lastChangeTime = EditorApplication.timeSinceStartup;
            _pendingUpdate = true;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawPreviewSection();
            EditorGUILayout.Space(10);

            DrawSaveFolderSection();
            EditorGUILayout.Space(10);

            DrawShapeSettingsSection();
            EditorGUILayout.Space(10);

            DrawGenerateSection();


            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                RequestPreviewUpdate();
            }
        }

        private void DrawSaveFolderSection()
        {
            EditorGUILayout.LabelField("저장 위치", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

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

            EditorGUI.BeginDisabledGroup(_saveFolderAsset == null);
            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                EditorGUIUtility.PingObject(_saveFolderAsset);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            string displayPath = string.IsNullOrEmpty(_saveFolderPath) ? "폴더를 선택해주세요" : _saveFolderPath;
            EditorGUILayout.LabelField(displayPath, EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawShapeSettingsSection()
        {
            EditorGUILayout.LabelField("도형 설정", EditorStyles.boldLabel);

            int newIndex = EditorGUILayout.Popup("Shape", _selectedGeneratorIndex, _generatorNames);
            if (newIndex != _selectedGeneratorIndex)
            {
                _selectedGeneratorIndex = newIndex;
                SaveSettings();
            }

            EditorGUI.indentLevel++;
            _generators[_selectedGeneratorIndex].DrawSettingsGUI();
            EditorGUI.indentLevel--;
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);

            if (_previewTexture != null)
            {
                EditorGUILayout.LabelField($"크기: {_previewTexture.width} x {_previewTexture.height} px", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);

                float previewSize = Mathf.Min(200, position.width - 40);
                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                DrawCheckerboard(previewRect);
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
            string fileName = _generators[_selectedGeneratorIndex].GetFileName();
            EditorGUILayout.LabelField($"파일명: {fileName}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

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
            texture = BaseShapeGenerator.TrimTexture(texture);

            string fileName = generator.GetFileName();
            string fullPath = Path.Combine(_saveFolderPath, fileName);

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);

            DestroyImmediate(texture);

            AssetDatabase.Refresh();

            Vector4 border = generator.GetSpriteBorder();
            ApplyTextureImportSettings(fullPath, border);

            var createdAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (createdAsset != null)
            {
                Selection.activeObject = createdAsset;
                EditorGUIUtility.PingObject(createdAsset);
            }

            Debug.Log($"[ShapeGenerator] 생성 완료: {fullPath}");
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

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);

                importer.textureCompression = TextureImporterCompression.Compressed;

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
            if (!string.IsNullOrEmpty(_saveFolderPath))
            {
                string guid = AssetDatabase.AssetPathToGUID(_saveFolderPath);
                EditorPrefs.SetString(PREFS_KEY_SAVE_FOLDER, guid);
            }
            else
            {
                EditorPrefs.DeleteKey(PREFS_KEY_SAVE_FOLDER);
            }

            EditorPrefs.SetInt(PREFS_KEY_SELECTED_SHAPE, _selectedGeneratorIndex);
        }

        private void LoadSettings()
        {
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

            _selectedGeneratorIndex = EditorPrefs.GetInt(PREFS_KEY_SELECTED_SHAPE, 0);
            _selectedGeneratorIndex = Mathf.Clamp(_selectedGeneratorIndex, 0, _generators.Count - 1);
        }
    }
}
