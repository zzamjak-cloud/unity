using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    [AddComponentMenu("CAT/Effects/ChristmasLights")]
    [ExecuteInEditMode]
    public class ChristmasLights : MonoBehaviour
    {
        [System.Serializable]
        public enum ColorChangePreset
        {
            Default,
            Slow,
            Fast,
            Vibrant
        }

        [Header("Light Settings")]
        [SerializeField] private Color color1 = Color.red;
        [SerializeField] private Color color2 = Color.green;
        [SerializeField] private Color color3 = Color.blue;
        [SerializeField] private Color color4 = Color.yellow;
        [SerializeField] private Color color5 = Color.magenta;
        [Range(0.1f, 5f)]
        [SerializeField] private float colorChangeSpeed = 1f;
        [Range(0.1f, 3f)]
        [SerializeField] private float brightness = 1.5f;

        private Material lightMaterial;
        private Renderer rendererComponent;
        private Graphic uiComponent;
        private bool isUI = false;

        private static readonly int ColorID1 = Shader.PropertyToID("_Color1");
        private static readonly int ColorID2 = Shader.PropertyToID("_Color2");
        private static readonly int ColorID3 = Shader.PropertyToID("_Color3");
        private static readonly int ColorID4 = Shader.PropertyToID("_Color4");
        private static readonly int ColorID5 = Shader.PropertyToID("_Color5");
        private static readonly int SpeedID = Shader.PropertyToID("_Speed");
        private static readonly int BrightnessID = Shader.PropertyToID("_Brightness");

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Try to get renderer first
            rendererComponent = GetComponent<Renderer>();

            // If no renderer, check for UI component
            if (rendererComponent == null)
            {
                uiComponent = GetComponent<Graphic>();
                if (uiComponent != null)
                {
                    isUI = true;
                }
            }

            // Initialize material based on component type
            if (rendererComponent != null || uiComponent != null)
            {
                InitializeMaterial();
            }
            else
            {
                Debug.LogWarning("ChristmasLights: No Renderer or UI Graphic component found on " + gameObject.name);
            }
        }

        private void InitializeMaterial()
        {
            Shader shader = Shader.Find("CAT/Effects/ChristmasLights");
            if (shader == null)
            {
                Debug.LogError("ChristmasLights: Shader 'CAT/Effects/ChristmasLights' not found!");
                return;
            }

            if (isUI)
            {
                // Handle UI component
                if (uiComponent.material == null || uiComponent.material.shader != shader)
                {
                    lightMaterial = new Material(shader);
                    uiComponent.material = lightMaterial;
                }
                else
                {
                    lightMaterial = uiComponent.material;
                }
            }
            else
            {
                // Handle Renderer component
                if (rendererComponent.sharedMaterial == null || rendererComponent.sharedMaterial.shader != shader)
                {
                    lightMaterial = new Material(shader);
                    rendererComponent.material = lightMaterial;
                }
                else if (rendererComponent.sharedMaterial.name != "CAT/Effects/ChristmasLights (Instance)")
                {
                    lightMaterial = new Material(rendererComponent.sharedMaterial);
                    rendererComponent.material = lightMaterial;
                }
                else
                {
                    lightMaterial = rendererComponent.material;
                }
            }

            UpdateShaderProperties();
        }

        private void Update()
        {
            if (lightMaterial != null)
            {
                UpdateShaderProperties();
            }
            // Try to reinitialize if material is missing
            else if ((rendererComponent != null || uiComponent != null) && lightMaterial == null)
            {
                InitializeMaterial();
            }
        }

        private void UpdateShaderProperties()
        {
            if (lightMaterial == null) return;

            lightMaterial.SetColor(ColorID1, color1);
            lightMaterial.SetColor(ColorID2, color2);
            lightMaterial.SetColor(ColorID3, color3);
            lightMaterial.SetColor(ColorID4, color4);
            lightMaterial.SetColor(ColorID5, color5);
            lightMaterial.SetFloat(SpeedID, colorChangeSpeed);
            lightMaterial.SetFloat(BrightnessID, brightness);
        }

        // 프리셋 설정을 위한 퍼블릭 메소드들
        public void SetPreset(ColorChangePreset preset)
        {
            switch (preset)
            {
                case ColorChangePreset.Default:
                    colorChangeSpeed = 1.0f;
                    brightness = 1.5f;
                    color1 = Color.red;
                    color2 = Color.green;
                    color3 = Color.blue;
                    color4 = Color.yellow;
                    color5 = Color.magenta;
                    break;
                case ColorChangePreset.Slow:
                    colorChangeSpeed = 0.3f;
                    brightness = 1.2f;
                    break;
                case ColorChangePreset.Fast:
                    colorChangeSpeed = 2.0f;
                    brightness = 1.8f;
                    break;
                case ColorChangePreset.Vibrant:
                    colorChangeSpeed = 1.5f;
                    brightness = 2.5f;
                    color1 = new Color(1, 0, 0);       // 빨강
                    color2 = new Color(0, 1, 0);       // 초록
                    color3 = new Color(0, 0, 1);       // 파랑
                    color4 = new Color(1, 0.5f, 0);    // 주황
                    color5 = new Color(1, 0, 1);       // 핑크
                    break;
            }
            UpdateShaderProperties();
        }

        // 편의를 위한 속성 설정 메소드들
        public void SetBrightness(float value)
        {
            brightness = Mathf.Clamp(value, 0.1f, 3.0f);
            UpdateShaderProperties();
        }

        public void SetColorChangeSpeed(float value)
        {
            colorChangeSpeed = Mathf.Clamp(value, 0.1f, 5.0f);
            UpdateShaderProperties();
        }

        public void SetColors(Color newColor1, Color newColor2, Color newColor3, Color newColor4, Color newColor5)
        {
            color1 = newColor1;
            color2 = newColor2;
            color3 = newColor3;
            color4 = newColor4;
            color5 = newColor5;
            UpdateShaderProperties();
        }

        // 개별 색상 설정 메소드
        public void SetColor1(Color newColor) { color1 = newColor; UpdateShaderProperties(); }
        public void SetColor2(Color newColor) { color2 = newColor; UpdateShaderProperties(); }
        public void SetColor3(Color newColor) { color3 = newColor; UpdateShaderProperties(); }
        public void SetColor4(Color newColor) { color4 = newColor; UpdateShaderProperties(); }
        public void SetColor5(Color newColor) { color5 = newColor; UpdateShaderProperties(); }
    }
}