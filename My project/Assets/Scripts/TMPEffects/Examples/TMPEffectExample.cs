using UnityEngine;
using TMPro;

namespace CAT.UI.Examples
{
    /// <summary>
    /// TMPOutlineEffect 사용 예제
    /// </summary>
    public class TMPEffectExample : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI _tmpText;
        [SerializeField] private TMPEffectPreset _preset;

        void Start()
        {
            if (_tmpText == null)
            {
                _tmpText = GetComponent<TextMeshProUGUI>();
            }

            // 예제 선택
            // Example1_BasicOutline();
            // Example2_DropShadow();
            // Example3_OutlineWithShadow();
            // Example4_UsePreset();
            Example5_SharedMaterials();
        }

        // ─────────────────────────────────────────────
        // 예제 1: 기본 Outline
        // ─────────────────────────────────────────────
        void Example1_BasicOutline()
        {
            var effect = _tmpText.gameObject.AddComponent<TMPOutlineEffect>();

            effect.UnderlayDilate = 0.2f;           // 외곽선 두께
            effect.UnderlayColor = Color.black;     // 검은색
            effect.UnderlayOffsetX = 0f;            // Offset 0 = Outline
            effect.UnderlayOffsetY = 0f;
            effect.UnderlaySoftness = 0.05f;        // 약간 부드럽게
        }

        // ─────────────────────────────────────────────
        // 예제 2: Drop Shadow (Underlay 활용)
        // ─────────────────────────────────────────────
        void Example2_DropShadow()
        {
            var effect = _tmpText.gameObject.AddComponent<TMPOutlineEffect>();

            effect.UnderlayOffsetX = 0.1f;          // 오른쪽으로
            effect.UnderlayOffsetY = -0.1f;         // 아래로
            effect.UnderlayDilate = 0.1f;           // 약간의 두께
            effect.UnderlayColor = new Color(0, 0, 0, 0.5f);  // 반투명 검정
        }

        // ─────────────────────────────────────────────
        // 예제 3: Outline + Shadow 동시 적용
        // ─────────────────────────────────────────────
        void Example3_OutlineWithShadow()
        {
            var effect = _tmpText.gameObject.AddComponent<TMPOutlineEffect>();

            // Underlay로 Outline
            effect.UnderlayDilate = 0.2f;
            effect.UnderlayColor = Color.black;
            effect.UnderlayOffsetX = 0f;
            effect.UnderlayOffsetY = 0f;

            // Mesh Shadow 추가
            effect.EnableShadow = true;
            effect.ShadowOffset = new Vector2(0.1f, -0.1f);
            effect.ShadowColor = new Color(0, 0, 0, 0.3f);

            // Face 조절
            effect.FaceDilate = 0.05f;  // 약간 굵게
        }

        // ─────────────────────────────────────────────
        // 예제 4: Preset 사용
        // ─────────────────────────────────────────────
        void Example4_UsePreset()
        {
            var effect = _tmpText.gameObject.AddComponent<TMPOutlineEffect>();

            if (_preset != null)
            {
                effect.ApplyPreset(_preset);
            }
            else
            {
                Debug.LogWarning("Preset이 설정되지 않았습니다!");
            }
        }

        // ─────────────────────────────────────────────
        // 예제 5: Material 공유 데모
        // ─────────────────────────────────────────────
        void Example5_SharedMaterials()
        {
            // 여러 텍스트에 같은 효과 적용 시 Material 자동 공유
            var texts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);

            foreach (var text in texts)
            {
                var effect = text.gameObject.AddComponent<TMPOutlineEffect>();

                // 모두 동일한 설정 → Material 1개만 생성됨!
                effect.UnderlayDilate = 0.15f;
                effect.UnderlayColor = Color.black;
                effect.UnderlayOffsetX = 0f;
                effect.UnderlayOffsetY = 0f;
            }

            Debug.Log($"TMPEffectManager cached materials: {TMPEffectManager.CachedMaterialCount}");
        }

        // ─────────────────────────────────────────────
        // 예제 6: 고급 타이틀 효과
        // ─────────────────────────────────────────────
        void Example6_AdvancedTitle()
        {
            var effect = _tmpText.gameObject.AddComponent<TMPOutlineEffect>();

            // Underlay: 어두운 갈색 외곽선
            effect.UnderlayDilate = 0.25f;
            effect.UnderlayColor = new Color(0.2f, 0.1f, 0f);
            effect.UnderlaySoftness = 0.1f;

            // Face: 텍스트 굵게
            effect.FaceDilate = 0.1f;

            // Shadow: 진한 그림자
            effect.EnableShadow = true;
            effect.ShadowOffset = new Vector2(0.15f, -0.15f);
            effect.ShadowColor = new Color(0, 0, 0, 0.6f);
        }

        // ─────────────────────────────────────────────
        // 런타임에서 효과 변경
        // ─────────────────────────────────────────────
        void RuntimeExample_ChangeColor()
        {
            var effect = _tmpText.GetComponent<TMPOutlineEffect>();
            if (effect != null)
            {
                // 색상만 변경
                effect.UnderlayColor = Color.red;

                // 두께 변경
                effect.UnderlayDilate = 0.3f;

                // 새로운 설정에 맞는 공유 Material이 자동 적용됨
            }
        }

        // ─────────────────────────────────────────────
        // 프리셋 생성 예제 (Editor에서 실행)
        // ─────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Create Simple Outline Preset")]
        void CreateSimpleOutlinePreset()
        {
            var preset = TMPEffectPreset.CreateSimpleOutline();
            UnityEditor.AssetDatabase.CreateAsset(preset, "Assets/TMPEffects/Presets/SimpleOutline.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("Preset created!");
        }

        [ContextMenu("Create Title Preset")]
        void CreateTitlePreset()
        {
            var preset = TMPEffectPreset.CreateTitle();
            UnityEditor.AssetDatabase.CreateAsset(preset, "Assets/TMPEffects/Presets/Title.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("Title preset created!");
        }
#endif
    }
}
