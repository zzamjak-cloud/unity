using UnityEngine;

namespace CAT.Effects
{
    // [ExecuteInEditMode] // 구버전 Unity 사용 시 이것으로 교체
    [ExecuteAlways] // Unity 2018.2 이상에서 권장
    public class SpriteGroupTint : MonoBehaviour
    {
        [SerializeField] private Color tintColor = Color.white;
        private Color previousTintColor;
        private MaterialPropertyBlock mpb;
        
        void Start()
        {
            InitializeMPB();
            previousTintColor = tintColor;
            ApplyTintToChildren();
        }
        
        void OnEnable()
        {
            InitializeMPB();
            ApplyTintToChildren();
        }

        void Update()
        {
#if UNITY_EDITOR
            // 에디터에서는 Update에서도 확인
            CheckForColorChange();
#endif
        }

        void LateUpdate()
        {
            CheckForColorChange();
        }

        // MaterialPropertyBlock 초기화
        private void InitializeMPB()
        {
            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }
        }

        // 색상 변경 확인
        private void CheckForColorChange()
        {
            if (tintColor != previousTintColor)
            {
                ApplyTintToChildren();
                previousTintColor = tintColor;
            }
        }

        // 하위 오브젝트에 색상 적용
        public void ApplyTintToChildren()
        {
            InitializeMPB();
            
            // SpriteRenderer 인 경우에만 처리
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in spriteRenderers)
            {
                if (renderer != null)
                {
                    renderer.color = tintColor;
                    
#if UNITY_EDITOR
                    // 에디터에서 변경사항을 즉시 반영
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(renderer);
                    }
#endif
                }
            }

            // MeshRenderer 인 경우에만 처리
            MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    renderer.GetPropertyBlock(mpb);
                    
                    // 일반적인 Unity 셰이더 속성들 확인
                    if (renderer.sharedMaterial.HasProperty("_Color"))
                    {
                        mpb.SetColor("_Color", tintColor);
                    }
                    else if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                    {
                        mpb.SetColor("_BaseColor", tintColor);
                    }
                    
                    renderer.SetPropertyBlock(mpb);
                    
#if UNITY_EDITOR
                    // 에디터에서 변경사항을 즉시 반영
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(renderer);
                    }
#endif
                }
            }
        }

        // 애니메이터에서 호출할 수 있는 공용 메서드
        public void SetTintColor(Color color)
        {
            tintColor = color;
            ApplyTintToChildren();
        }

        public void SetTintColorR(float r)
        {
            tintColor.r = r;
            ApplyTintToChildren();
        }

        public void SetTintColorG(float g)
        {
            tintColor.g = g;
            ApplyTintToChildren();
        }

        public void SetTintColorB(float b)
        {
            tintColor.b = b;
            ApplyTintToChildren();
        }

        public void SetTintColorA(float a)
        {
            tintColor.a = a;
            ApplyTintToChildren();
        }

        // 프로퍼티로 접근 가능하도록 (애니메이터에서 직접 사용)
        public Color TintColor
        {
            get { return tintColor; }
            set 
            { 
                tintColor = value;
                ApplyTintToChildren();
            }
        }

#if UNITY_EDITOR
        // 에디터에서 Inspector 값이 변경될 때 호출
        void OnValidate()
        {
            if (this != null && gameObject != null)
            {
                // 다음 프레임에 적용 (OnValidate에서 직접 실행하면 에러가 날 수 있음)
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        ApplyTintToChildren();
                        previousTintColor = tintColor;
                    }
                };
            }
        }
#endif
    }
}