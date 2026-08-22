using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CAT.Effect
{
    [CustomEditor(typeof(SpriteGradient))]
    public class SpriteGradientEditor : Editor
    {
        private const string MaterialSuffix = "_Gradient";

        private SpriteGradient gradient;
        private SerializedProperty color1;
        private SerializedProperty color2;
        private SerializedProperty gradientDirection;
        private SerializedProperty lerpValue;
        private SerializedProperty alwaysUpdate;
        private SerializedProperty updateThreshold;

        private void OnEnable()
        {
            gradient = (SpriteGradient)target;
            color1 = serializedObject.FindProperty("color1");
            color2 = serializedObject.FindProperty("color2");
            gradientDirection = serializedObject.FindProperty("gradientDirection");
            lerpValue = serializedObject.FindProperty("lerpValue");
            alwaysUpdate = serializedObject.FindProperty("alwaysUpdate");
            updateThreshold = serializedObject.FindProperty("updateThreshold");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(color1);
            EditorGUILayout.PropertyField(color2);
            EditorGUILayout.PropertyField(gradientDirection);
            EditorGUILayout.PropertyField(lerpValue);
            EditorGUILayout.PropertyField(alwaysUpdate);
            EditorGUILayout.PropertyField(updateThreshold);

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed)
                gradient.ForceUpdate();

            EditorGUILayout.Space(8);

            DrawMaterialSection();
            DrawWarnings();
        }

        private void DrawMaterialSection()
        {
            SpriteRenderer spriteRenderer = gradient.Renderer;
            Material current = spriteRenderer != null ? spriteRenderer.sharedMaterial : null;
            bool ready = SpriteGradient.HasGradientShader(current);

            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);

            if (ready)
            {
                EditorGUILayout.HelpBox($"적용됨: {current.name}", MessageType.Info);

                if (!EditorUtility.IsPersistent(current))
                {
                    EditorGUILayout.HelpBox(
                        "현재 머티리얼이 에셋이 아니라 씬/프리팹에 직렬화된 인스턴스입니다. " +
                        "구버전 코드가 남긴 것이므로 아래 버튼으로 머티리얼 에셋을 만들어 교체하세요.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Gradient 머티리얼이 적용되지 않아 효과가 표시되지 않습니다.\n" +
                    "아래 버튼으로 머티리얼을 생성하세요.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(spriteRenderer == null))
            {
                if (GUILayout.Button("머티리얼 생성 및 적용", GUILayout.Height(24)))
                {
                    CreateAndAssignMaterial(spriteRenderer);
                }
            }

            if (ready && EditorUtility.IsPersistent(current) && GUILayout.Button("머티리얼 에셋 선택"))
            {
                EditorGUIUtility.PingObject(current);
                Selection.activeObject = current;
            }
        }

        private void DrawWarnings()
        {
            EditorGUILayout.Space(4);

            SpriteRenderer spriteRenderer = gradient.Renderer;
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Sprite Renderer에 Sprite가 없습니다. 아틀라스 UV 보정이 동작하지 않습니다.",
                    MessageType.Warning);
            }

            if (!alwaysUpdate.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "애니메이션 클립으로 Color/Lerp Value를 제어하려면 Always Update를 켜야 합니다. " +
                    "클립은 프로퍼티가 아닌 필드에 직접 기록하므로 변경 감지가 동작하지 않습니다.",
                    MessageType.Info);
            }

            if (!IsShaderAlwaysIncluded())
            {
                EditorGUILayout.HelpBox(
                    "셰이더가 Always Included Shaders에 등록되어 있지 않습니다.\n" +
                    "머티리얼을 씬/프리팹에 직접 할당했다면 빌드에 포함되지만, " +
                    "런타임에 컴포넌트를 동적으로 추가한다면 Shader.Find가 실패합니다.",
                    MessageType.Warning);

                if (GUILayout.Button("Always Included Shaders에 등록"))
                {
                    RegisterShaderToGraphicsSettings();
                }
            }
        }

        private void CreateAndAssignMaterial(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
                return;

            Sprite sprite = spriteRenderer.sprite;
            if (sprite == null)
            {
                EditorUtility.DisplayDialog(
                    "Sprite 없음",
                    "Sprite Renderer에 Sprite가 지정되어 있지 않습니다.\n머티리얼 이름을 정할 수 없으므로 Sprite를 먼저 지정하세요.",
                    "확인");
                return;
            }

            Shader shader = Shader.Find(SpriteGradient.ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    "셰이더 없음",
                    $"셰이더 '{SpriteGradient.ShaderName}'를 찾을 수 없습니다.\n셰이더 파일이 프로젝트에 있는지 확인하세요.",
                    "확인");
                return;
            }

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath))
            {
                EditorUtility.DisplayDialog(
                    "셰이더 경로 없음",
                    "셰이더의 에셋 경로를 찾을 수 없어 머티리얼을 생성할 수 없습니다.",
                    "확인");
                return;
            }

            string directory = Path.GetDirectoryName(shaderPath).Replace('\\', '/');

            // 셰이더 성격이 드러나도록 접미사를 붙인다. (예: Jungle_Gradient.mat)
            string materialName = SanitizeFileName(sprite.name) + MaterialSuffix;
            string materialPath = $"{directory}/{materialName}.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null)
            {
                bool useExisting = EditorUtility.DisplayDialog(
                    "머티리얼 중복",
                    $"동일한 이름의 머티리얼이 이미 존재합니다.\n\n{materialPath}\n\n기존 머티리얼을 Sprite Renderer에 적용할까요?",
                    "기존 머티리얼 적용",
                    "취소");

                if (useExisting)
                    AssignMaterial(spriteRenderer, existing);

                return;
            }

            var material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();

            AssignMaterial(spriteRenderer, material);
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[SpriteGradient] 머티리얼 생성: {materialPath}", material);
        }

        private void AssignMaterial(SpriteRenderer spriteRenderer, Material material)
        {
            Undo.RecordObject(spriteRenderer, "Assign SpriteGradient Material");
            spriteRenderer.sharedMaterial = material;

            EditorUtility.SetDirty(spriteRenderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spriteRenderer);

            var scene = spriteRenderer.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);

            gradient.ForceUpdate();
        }

        private static string SanitizeFileName(string source)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(source.Length);

            foreach (char c in source)
            {
                builder.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Sprite" : result;
        }

        private static SerializedProperty FindAlwaysIncludedShaders()
        {
            Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settings == null || settings.Length == 0)
                return null;

            var serialized = new SerializedObject(settings[0]);
            return serialized.FindProperty("m_AlwaysIncludedShaders");
        }

        private static bool IsShaderAlwaysIncluded()
        {
            SerializedProperty list = FindAlwaysIncludedShaders();
            if (list == null)
                return true; // 확인 불가 시 경고를 띄우지 않는다.

            Shader shader = Shader.Find(SpriteGradient.ShaderName);
            if (shader == null)
                return false;

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return true;
            }

            return false;
        }

        private static void RegisterShaderToGraphicsSettings()
        {
            Shader shader = Shader.Find(SpriteGradient.ShaderName);
            if (shader == null)
                return;

            SerializedProperty list = FindAlwaysIncludedShaders();
            if (list == null)
                return;

            SerializedObject serialized = list.serializedObject;
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            serialized.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpriteGradient] '{SpriteGradient.ShaderName}'를 Always Included Shaders에 등록했습니다.");
        }
    }
}
