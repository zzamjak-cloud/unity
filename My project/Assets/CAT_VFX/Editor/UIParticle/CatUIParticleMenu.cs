using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.VFX.Editor
{
    /// <summary>
    /// GameObject/UI 메뉴에 CAT UI Particle 생성 항목 추가
    /// </summary>
    internal class CatUIParticleMenu
    {
        [MenuItem("GameObject/UI/CAT Particle System (Empty)", false, 2018)]
        private static void AddParticleEmpty(MenuCommand menuCommand)
        {
            // UI 요소 생성
            EditorApplication.ExecuteMenuItem("GameObject/UI/Image");
            var ui = Selection.activeGameObject;
            Object.DestroyImmediate(ui.GetComponent<Image>());

            // CatUIParticle 추가
            var uiParticle = ui.AddComponent<CatUIParticle>();
            uiParticle.name = "CatUIParticle";
            uiParticle.scale = 100;
            uiParticle.rectTransform.sizeDelta = Vector2.zero;
        }

        [MenuItem("GameObject/UI/CAT Particle System", false, 2019)]
        private static void AddParticle(MenuCommand menuCommand)
        {
            // 빈 UIParticle 생성
            AddParticleEmpty(menuCommand);
            var uiParticle = Selection.activeGameObject.GetComponent<CatUIParticle>();

            // ParticleSystem 생성
            EditorApplication.ExecuteMenuItem("GameObject/Effects/Particle System");
            var ps = Selection.activeGameObject;
            ps.transform.SetParent(uiParticle.transform, false);
            ps.transform.localPosition = Vector3.zero;

            // 기본 머티리얼 할당 (UI Additive)
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var matPath = AssetDatabase.GUIDToAssetPath("9944483a3e009401ba5dcc42f14d5c63");
            if (!string.IsNullOrEmpty(matPath))
            {
                renderer.material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }

            // 파티클 갱신
            uiParticle.RefreshParticles();
        }
    }
}
