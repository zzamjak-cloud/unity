using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(RawImage)), AddComponentMenu("CAT/UIEffect/UIWindable"), DisallowMultipleComponent]
public class UIWindable : MonoBehaviour
{
    public static readonly string SHADER_NAME = "CAT/Particles/UIWindable";

    [SerializeField, HideInInspector] private Texture _MainTex;
    [SerializeField, Range(0, 360)] private float _RotateUV;
    [SerializeField] private Texture _NoiseTex;
    [SerializeField] private float _WindSpeed = 0.2f;
    [SerializeField] private float _WindStrength = 1.0f;
    [SerializeField] private float _WindFrequency = 0.2f;
    [SerializeField] private Vector4 _WindDirection = new Vector4(1, 1, 0, 0);
    [SerializeField] private float _WindScale = 1.0f;
    [SerializeField, HideInInspector] private Vector4 _ClipRect = new Vector4(-2147.0f, -2147.0f, 2147.0f, 2147.0f);
    [SerializeField] private float _ImageOffsetX = 0.8f;
    [SerializeField] private float _ImageOffsetY = 0.3f;
    [SerializeField] private float _ImageScale = 1.1f;

    private Material material;

    private void Awake()
    {
        // rawImage 컴포넌트를 가져옵니다.
        RawImage rawImage = GetComponent<RawImage>();

        // 셰이더를 찾아서 Material을 생성합니다.
        Shader shader = Shader.Find(SHADER_NAME);
        material = new Material(shader);

        // rawImage 컴포넌트에 Material을 적용합니다.
        rawImage.material = material;
    }

    private void Update()
    {
        // 런타임에서 셰이더 프로퍼티 값을 업데이트합니다.
        UpdateMaterial();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 셰이더 프로퍼티 값을 업데이트합니다.
        if (material != null)
        {
            EditorApplication.update += UpdateMaterial;
        }
    }
#endif

    private void UpdateMaterial()
    {
        if (material == null) return;

        material.SetTexture("_MainTex", _MainTex);
        material.SetFloat("_RotateUV", _RotateUV);
        material.SetTexture("_NoiseTex", _NoiseTex);
        material.SetFloat("_WindSpeed", _WindSpeed);
        material.SetFloat("_WindStrength", _WindStrength);
        material.SetFloat("_WindFrequency", _WindFrequency);
        material.SetVector("_WindDirection", _WindDirection);
        material.SetVector("_ClipRect", _ClipRect);
        material.SetFloat("_WindScale", _WindScale);
        material.SetFloat("_ImageOffsetX", _ImageOffsetX);
        material.SetFloat("_ImageOffsetY", _ImageOffsetY);
        material.SetFloat("_ImageScale", _ImageScale);

#if UNITY_EDITOR
        EditorApplication.update -= UpdateMaterial;
#endif
    }
}