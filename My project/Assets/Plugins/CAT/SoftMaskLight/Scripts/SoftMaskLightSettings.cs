using UnityEngine;

namespace SoftMaskLight
{
    /// <summary>
    /// SoftMaskLight 프로젝트 설정 (싱글턴 ScriptableObject)
    /// Resources 폴더에 위치하여 빌드 시 Hidden 셰이더 참조를 자동으로 포함시킨다.
    /// 씬에 SoftMaskLight 컴포넌트가 없더라도 셰이더가 빌드에 포함됨을 보장한다.
    /// </summary>
    public class SoftMaskLightSettings : ScriptableObject
    {
        [Tooltip("빌드에 포함할 Hidden 변형 셰이더 목록 (자동 관리)")]
        [SerializeField] private Shader[] _includedShaders = new Shader[0];

        /// <summary>빌드에 포함된 Hidden 변형 셰이더 목록</summary>
        public Shader[] IncludedShaders => _includedShaders;

        private static SoftMaskLightSettings _instance;

        /// <summary>
        /// 싱글턴 인스턴스 (Resources.Load로 자동 로드)
        /// </summary>
        public static SoftMaskLightSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<SoftMaskLightSettings>("SoftMaskLightSettings");
                return _instance;
            }
        }

        /// <summary>
        /// 런타임 초기화: Resources.Load를 통해 셰이더 참조를 로드하여 빌드에 포함 보장
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Instance 접근만으로 Resources.Load가 실행되어 셰이더 참조가 로드됨
            var _ = Instance;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 셰이더 참조 목록을 갱신한다.
        /// SoftMaskLightInstaller에서 호출됨.
        /// </summary>
        public void SetIncludedShaders(Shader[] shaders)
        {
            _includedShaders = shaders;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
