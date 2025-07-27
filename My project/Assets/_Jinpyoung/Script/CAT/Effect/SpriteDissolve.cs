using UnityEngine;
using System.Collections;

namespace CAT.Effects
{
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("CAT/Effects/SpriteDissolve")]
    public class SpriteDissolve : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/SpriteDissolve";

        [SerializeField] private Texture2D _dissolveTex;
        public Texture2D DissolveTex
        {
            get => _dissolveTex;
            set
            {
                _dissolveTex = value;
                UpdateMaterialProperty("_DissolveTex", value);
            }
        }

        [SerializeField] private Vector2 _dissolveScale = Vector2.one;
        public Vector2 DissolveScale
        {
            get => _dissolveScale;
            set
            {
                _dissolveScale = value;
                UpdateMaterialProperty("_DissolveScale", new Vector4(value.x, value.y, 0, 0));
            }
        }

        [SerializeField, Range(0f, 1f)] private float _threshold;
        public float Threshold
        {
            get => _threshold;
            set
            {
                _threshold = Mathf.Clamp01(value);
                UpdateMaterialProperty("_Threshold", _threshold);
            }
        }

        private Material material;
        private SpriteRenderer spriteRenderer;
        private Material originalMaterial;
        private bool initialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            if (material == null)
            {
                Shader shader = Shader.Find(SHADER_NAME);
                if (shader == null)
                {
                    Debug.LogError($"Cannot find shader: {SHADER_NAME} on {gameObject.name}");
                    return;
                }

                if (spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.shader.name != SHADER_NAME)
                {
                    originalMaterial = spriteRenderer.sharedMaterial;
                    material = new Material(shader);
                    material.SetTexture("_MainTex", spriteRenderer.sprite.texture);
                    spriteRenderer.sharedMaterial = material;
                }
                else if (spriteRenderer.sharedMaterial != null)
                {
                    material = spriteRenderer.sharedMaterial;
                }
                else
                {
                    material = new Material(shader);
                    material.SetTexture("_MainTex", spriteRenderer.sprite.texture);
                    spriteRenderer.sharedMaterial = material;
                }
            }

            UpdateMaterial();
            initialized = true;
        }

        private void UpdateMaterialProperty<T>(string propertyName, T value)
        {
            if (material == null) return;

            if (value is Texture texture)
                material.SetTexture(propertyName, texture);
            else if (value is float floatValue)
                material.SetFloat(propertyName, floatValue);
            else if (value is Vector4 vector4Value)
                material.SetVector(propertyName, vector4Value);
        }

        private void UpdateMaterial()
        {
            if (material == null) return;

            material.SetTexture("_DissolveTex", _dissolveTex);
            material.SetVector("_DissolveScale", new Vector4(_dissolveScale.x, _dissolveScale.y, 0, 0));
            material.SetFloat("_Threshold", _threshold);
        }

        public void Dissolve(float duration)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DissolveCoroutine(duration));
            }
        }

        private IEnumerator DissolveCoroutine(float duration)
        {
            float elapsed = 0f;
            float startThreshold = _threshold;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Threshold = Mathf.Lerp(startThreshold, 1f, t);
                yield return null;
            }

            Threshold = 1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Don't initialize in prefab mode
            if (!gameObject.scene.IsValid()) return;

            if (!initialized)
                Initialize();
            UpdateMaterial();
        }
#endif

        private void OnDestroy()
        {
            if (spriteRenderer != null && originalMaterial != null)
            {
                spriteRenderer.sharedMaterial = originalMaterial;
            }

            if (material != null && material != originalMaterial)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
        }
    }
}