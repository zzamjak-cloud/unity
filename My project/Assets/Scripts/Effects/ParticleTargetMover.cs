using UnityEngine;

namespace CAT.Effects
{
    [RequireComponent(typeof(ParticleSystem))]
    [AddComponentMenu("CAT/Effects/ParticleTargetMover")]
    [DisallowMultipleComponent]
    public class ParticleTargetMover : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 5f;

        [Header("Performance Settings")]
        [SerializeField] private int updateFrequency = 1; // 몇 프레임마다 업데이트할지
        [SerializeField] private bool autoAdjustBufferSize = true; // 파티클 버퍼 크기 자동 조정

        private ParticleSystem particleSys;
        private ParticleSystem.Particle[] particles;
        private ParticleSystemSimulationSpace simulationSpace;
        private Transform particleSystemTransform;
        private Vector3 cachedTargetPosition;
        private int frameCounter;
        private int currentParticleCount;

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            particleSys = GetComponent<ParticleSystem>();
            simulationSpace = particleSys.main.simulationSpace;
            particleSystemTransform = particleSys.transform;

            // 초기 파티클 배열 크기 설정
            int initialSize = Mathf.Min(100, particleSys.main.maxParticles); // 시작은 작게
            particles = new ParticleSystem.Particle[initialSize];

            frameCounter = 0;
            currentParticleCount = 0;
        }

        private void LateUpdate()
        {
            if (!target) return;

            // 업데이트 빈도 제어
            frameCounter++;
            if (frameCounter % updateFrequency != 0) return;
            frameCounter = 0;

            // 타겟 위치 캐싱
            if (simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                cachedTargetPosition = particleSystemTransform.InverseTransformPoint(target.position);
            }
            else
            {
                cachedTargetPosition = target.position;
            }

            // 현재 활성화된 파티클 수 가져오기
            int numAliveParticles = particleSys.GetParticles(particles, particles.Length);

            // 버퍼 크기 자동 조정
            if (autoAdjustBufferSize && numAliveParticles == particles.Length)
            {
                int newSize = Mathf.Min(particles.Length * 2, particleSys.main.maxParticles);
                if (newSize > particles.Length)
                {
                    System.Array.Resize(ref particles, newSize);
                    numAliveParticles = particleSys.GetParticles(particles, particles.Length);
                }
            }

            if (numAliveParticles > 0)
            {
                UpdateParticleVelocities(numAliveParticles);
                particleSys.SetParticles(particles, numAliveParticles);
            }

            currentParticleCount = numAliveParticles;
        }

        private void UpdateParticleVelocities(int numParticles)
        {
            float deltaTimeMultiplier = Time.deltaTime * moveSpeed;

            for (int i = 0; i < numParticles; i++)
            {
                Vector3 toTarget = (cachedTargetPosition - particles[i].position);
                particles[i].velocity += toTarget.normalized * deltaTimeMultiplier;
            }
        }

        // 파티클 수가 크게 줄어들었을 때 배열 크기 최적화
        public void OptimizeBufferSize()
        {
            if (currentParticleCount > 0 && particles.Length > currentParticleCount * 2)
            {
                int newSize = Mathf.Max(100, currentParticleCount * 2);
                System.Array.Resize(ref particles, newSize);
            }
        }

        // 인스펙터에서 타겟 설정
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        // 이동 속도 조절
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0, speed);
        }

        // 업데이트 빈도 설정
        public void SetUpdateFrequency(int frequency)
        {
            updateFrequency = Mathf.Max(1, frequency);
        }

        // 파티클 시스템의 시뮬레이션 스페이스가 변경될 경우 업데이트
        public void UpdateSimulationSpace()
        {
            if (particleSys != null)
            {
                simulationSpace = particleSys.main.simulationSpace;
            }
        }

        private void OnDisable()
        {
            // 컴포넌트가 비활성화될 때 버퍼 크기 최적화
            OptimizeBufferSize();
        }
    }
}