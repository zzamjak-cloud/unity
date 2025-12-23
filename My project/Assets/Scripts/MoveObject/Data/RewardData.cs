using UnityEngine;
using DG.Tweening;

namespace MoveObject
{
    /// <summary>
    /// 재화 타입별 이동 속도, 이펙트 프리팹, 사운드 등을 정의하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "RewardData", menuName = "MoveObject/Reward Data", order = 0)]
    public class RewardData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("재화 고유 아이디 (Gold, Gem, Item_01 등)")]
        public string RewardID;

        [Tooltip("날아갈 아이템 외형 프리팹")]
        public GameObject ItemPrefab;

        [Header("이동 설정")]
        [Tooltip("이동에 소요되는 시간")]
        [Range(0.1f, 3f)]
        public float Duration = 0.8f;

        [Tooltip("이동 시 적용할 가속/감속 타입")]
        public Ease MoveEase = Ease.InQuint;

        [Tooltip("커스텀 이동 곡선 (설정 시 MoveEase 대신 사용)")]
        public AnimationCurve MoveCustomCurve = null;

        [Header("스케일 설정")]
        [Tooltip("등장 시작 스케일")]
        public float SpawnStartScale = 0f;

        [Tooltip("이동 시작 스케일 (등장 완료 후)")]
        public float MoveStartScale = 1f;

        [Tooltip("이동 도착 스케일 (최종)")]
        public float MoveEndScale = 1f;

        [Header("등장 연출")]
        [Tooltip("등장 시 스케일 애니메이션 활성화")]
        public bool UseSpawnAnimation = true;

        [Tooltip("등장 애니메이션 지속 시간")]
        [Range(0.1f, 1f)]
        public float SpawnDuration = 0.3f;

        [Tooltip("등장 애니메이션 이징")]
        public Ease SpawnEase = Ease.OutBack;

        [Tooltip("커스텀 등장 곡선 (설정 시 SpawnEase 대신 사용)")]
        public AnimationCurve SpawnCustomCurve = null;

        [Header("도착 연출")]
        [Tooltip("도착 시 재생될 이펙트 프리팹")]
        public GameObject ArrivalEffect;

        [Tooltip("도착 시 재생될 사운드 클립")]
        public AudioClip ArrivalSound;

        [Header("최적화 설정")]
        [Tooltip("화면에 동시에 나타날 수 있는 최대 오브젝트 수")]
        [Range(1, 100)]
        public int MaxSpawnCount = 20;

        [Tooltip("개별 등장 간격 최소값")]
        [Range(0.01f, 0.5f)]
        public float MinSpawnInterval = 0.05f;

        [Tooltip("개별 등장 간격 최대값")]
        [Range(0.01f, 0.5f)]
        public float MaxSpawnInterval = 0.15f;

        [Header("고급 설정")]
        [Tooltip("베지어 곡선 사용 여부")]
        public bool UseBezierPath = false;

        [Tooltip("베지어 곡선 제어점 오프셋 (0~1)")]
        [Range(0f, 1f)]
        public float BezierControlPointOffset = 0.5f;

        [Tooltip("시작 위치 랜덤 오프셋 반경 (픽셀 단위)")]
        [Range(0f, 200f)]
        public float SpawnRandomRadius = 30f;
    }
}
