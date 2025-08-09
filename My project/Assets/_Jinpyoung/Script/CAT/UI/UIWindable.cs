using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic)), AddComponentMenu("CAT/UI/UIWindable"), DisallowMultipleComponent]
public class UIWindable : MonoBehaviour
{
    public static readonly string SHADER_NAME = "CAT/Effects/Windable";

    // 아래 변수들은 모두 UIWindable 클래스에 속합니다.
    [SerializeField, HideInInspector] private Texture _MainTex;
    [SerializeField, Range(0, 360)] private float _RotateUV;
    [SerializeField] private Texture _NoiseTex;
    [SerializeField] private float _WindSpeed = 0.2f;
    [SerializeField] private float _WindStrength = 0.5f;
    [SerializeField] private float _WindFrequency = 0.2f;
    [SerializeField] private Vector4 _WindDirection = new Vector4(1, 1, 0, 0);
    [SerializeField] private float _WindScale = 1.0f;
    [SerializeField, HideInInspector] private Vector4 _ClipRect = new Vector4(-2147.0f, -2147.0f, 2147.0f, 2147.0f);
    [SerializeField] private float _ImageOffsetX = 0.3f;
    [SerializeField] private float _ImageOffsetY = 0.3f;
    [SerializeField] private float _ImageScale = 1.1f;

    private Material material;
    private Graphic _graphic;

    private void OnEnable()
    {
        SetupMaterial();
    }

    private void OnDisable()
    {
        if (_graphic != null)
        {
            _graphic.material = null;
        }
        if (material != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(material);
#else
            Destroy(material);
#endif
            material = null;
        }
    }
    
    // 런타임(플레이 모드)일 때 매 프레임 시간을 쉐이더로 전달합니다.
    private void Update()
    {
        if (material != null)
        {
            material.SetFloat("_CustomTime", Time.time);
        }
    }

    private void SetupMaterial()
    {
        if (_graphic == null)
        {
            _graphic = GetComponent<Graphic>();
        }

        if (material == null)
        {
            Shader shader = Shader.Find(SHADER_NAME);
            material = new Material(shader);
            _graphic.material = material;
        }
        // 클래스 내부의 다른 메서드를 호출합니다.
        UpdateMaterialProperties();
    }

    // 이 메서드는 UIWindable 클래스 내부에 있어야 합니다.
    // 에디터 스크립트가 호출할 공개 메서드.
    public void UpdateMaterialProperties(float customTime = 0)
    {
        if (material == null || _graphic == null)
        {
            SetupMaterial();
        }

        // 클래스 필드인 _graphic 과 _MainTex에 접근합니다.
        _MainTex = _graphic.mainTexture;
        if (_MainTex == null) return;
        
        // 클래스 필드인 material에 접근합니다.
        material.SetTexture("_MainTex", _MainTex);
        material.SetFloat("_CustomTime", customTime);

        Vector2 spritePivot = new Vector2(0.5f, 0.5f);

        if (_graphic is Image image && image.sprite != null)
        {
            Sprite sprite = image.sprite;
            Rect r = sprite.textureRect;
            Texture t = sprite.texture;

            Vector4 uvRect = new Vector4(r.x / t.width, r.y / t.height, (r.x + r.width) / t.width, (r.y + r.height) / t.height);
            material.SetVector("_SpriteUVRect", uvRect);
            
            float pivotX = (r.x + sprite.pivot.x) / t.width;
            float pivotY = (r.y + sprite.pivot.y) / t.height;
            spritePivot = new Vector2(pivotX, pivotY);
        }
        else
        {
            material.SetVector("_SpriteUVRect", new Vector4(0, 0, 1, 1));
        }
        
        // 아래의 모든 변수들은 이 메서드가 클래스 내부에 있을 때만 접근 가능합니다.
        material.SetVector("_SpritePivot", spritePivot);
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
        
        _graphic.SetMaterialDirty();
    }

} // UIWindable 클래스는 이 닫는 중괄호 '}'로 끝납니다. 모든 메서드는 이 안에 있어야 합니다.