using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CAT.VFX.Internal
{
    /// <summary>
    /// ParticleSystem 확장 메서드 - 메시 베이킹 헬퍼, 시뮬레이션 공간 쿼리, 정렬
    /// </summary>
    internal static class ParticleSystemExtensions
    {
        // 정적 파티클 배열 풀 (할당 최소화)
        private static ParticleSystem.Particle[] s_TmpParticles = new ParticleSystem.Particle[2048];

        /// <summary>
        /// 지정 크기 이상의 파티클 배열을 반환 (2의 거듭제곱으로 성장)
        /// </summary>
        public static ParticleSystem.Particle[] GetParticleArray(int size)
        {
            if (s_TmpParticles.Length < size)
            {
                s_TmpParticles = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(size)];
            }

            return s_TmpParticles;
        }

        /// <summary>
        /// Shape 스케일이 0인 경우 크래시 방지
        /// </summary>
        public static void ValidateShape(this ParticleSystem self)
        {
            var shape = self.shape;
            if (shape.enabled && shape.alignToDirection)
            {
                var s = shape.scale;
                if (Mathf.Approximately(s.x * s.y * s.z, 0))
                {
                    if (Mathf.Approximately(s.x, 0))
                        s.x = 0.0001f;
                    else if (Mathf.Approximately(s.y, 0))
                        s.y = 0.0001f;
                    else if (Mathf.Approximately(s.z, 0))
                        s.z = 0.0001f;
                    shape.scale = s;
                }
            }
        }

        /// <summary>
        /// 메시 베이킹이 가능한 상태인지 확인
        /// </summary>
        public static bool CanBakeMesh(this ParticleSystemRenderer self)
        {
            // RenderMode가 Mesh인데 mesh가 null이면 크래시 발생
            if (self.renderMode == ParticleSystemRenderMode.Mesh && self.mesh == null) return false;

            // RenderMode가 None이면 에러 발생
            if (self.renderMode == ParticleSystemRenderMode.None) return false;

            return true;
        }

        /// <summary>
        /// 실제 시뮬레이션 공간을 반환 (Custom에 대상이 없으면 Local로 폴백)
        /// </summary>
        public static ParticleSystemSimulationSpace GetActualSimulationSpace(this ParticleSystem self)
        {
            var main = self.main;
            var space = main.simulationSpace;
            if (space == ParticleSystemSimulationSpace.Custom && !main.customSimulationSpace)
            {
                space = ParticleSystemSimulationSpace.Local;
            }

            return space;
        }

        public static bool IsLocalSpace(this ParticleSystem self)
        {
            return GetActualSimulationSpace(self) == ParticleSystemSimulationSpace.Local;
        }

        public static bool IsWorldSpace(this ParticleSystem self)
        {
            return GetActualSimulationSpace(self) == ParticleSystemSimulationSpace.World;
        }

        /// <summary>
        /// 렌더링 순서에 따라 ParticleSystem 목록을 정렬
        /// </summary>
        public static void SortForRendering(this List<ParticleSystem> self, Transform transform, bool sortByMaterial)
        {
            self.Sort((a, b) =>
            {
                var aRenderer = a.GetComponent<ParticleSystemRenderer>();
                var bRenderer = b.GetComponent<ParticleSystemRenderer>();

                // 렌더 큐 기준 오름차순
                var aMat = aRenderer.sharedMaterial ? aRenderer.sharedMaterial : aRenderer.trailMaterial;
                var bMat = bRenderer.sharedMaterial ? bRenderer.sharedMaterial : bRenderer.trailMaterial;
                if (!aMat && !bMat) return 0;
                if (!aMat) return -1;
                if (!bMat) return 1;

                if (sortByMaterial)
                {
                    return aMat.GetInstanceID() - bMat.GetInstanceID();
                }

                if (aMat.renderQueue != bMat.renderQueue)
                {
                    return aMat.renderQueue - bMat.renderQueue;
                }

                // 소팅 레이어 오름차순
                if (aRenderer.sortingLayerID != bRenderer.sortingLayerID)
                {
                    return SortingLayer.GetLayerValueFromID(aRenderer.sortingLayerID) -
                           SortingLayer.GetLayerValueFromID(bRenderer.sortingLayerID);
                }

                // 소팅 오더 오름차순
                if (aRenderer.sortingOrder != bRenderer.sortingOrder)
                {
                    return aRenderer.sortingOrder - bRenderer.sortingOrder;
                }

                // Z 위치 + sortingFudge 내림차순
                var aTransform = a.transform;
                var bTransform = b.transform;
                var aPos = transform.InverseTransformPoint(aTransform.position).z + aRenderer.sortingFudge;
                var bPos = transform.InverseTransformPoint(bTransform.position).z + bRenderer.sortingFudge;
                if (!Mathf.Approximately(aPos, bPos))
                {
                    return (int)Mathf.Sign(bPos - aPos);
                }

                return (int)Mathf.Sign(GetIndex(self, a) - GetIndex(self, b));
            });
        }

        private static int GetIndex(IList<ParticleSystem> list, Object ps)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].GetInstanceID() == ps.GetInstanceID())
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// 스프라이트 시트 애니메이션에서 텍스처를 가져온다
        /// </summary>
        public static Texture2D GetTextureForSprite(this ParticleSystem self)
        {
            if (!self) return null;

            var tsaModule = self.textureSheetAnimation;
            if (!tsaModule.enabled || tsaModule.mode != ParticleSystemAnimationMode.Sprites) return null;

            for (var i = 0; i < tsaModule.spriteCount; i++)
            {
                var sprite = tsaModule.GetSprite(i);
                if (!sprite) continue;

                return sprite.GetActualTexture();
            }

            return null;
        }

        /// <summary>
        /// 리스트 내 모든 유효한 ParticleSystem에 대해 액션을 실행
        /// </summary>
        public static void Exec(this List<ParticleSystem> self, Action<ParticleSystem> action)
        {
            foreach (var p in self)
            {
                if (!p) continue;
                action.Invoke(p);
            }
        }

        /// <summary>
        /// 서브 이미터의 메인 이미터를 찾는다
        /// </summary>
        public static ParticleSystem GetMainEmitter(this ParticleSystem self, List<ParticleSystem> list)
        {
            if (!self || list == null || list.Count == 0) return null;

            for (var i = 0; i < list.Count; i++)
            {
                var parent = list[i];
                if (parent != self && IsSubEmitterOf(self, parent)) return parent;
            }

            return null;
        }

        /// <summary>
        /// 지정 ParticleSystem이 parent의 서브 이미터인지 확인
        /// </summary>
        public static bool IsSubEmitterOf(this ParticleSystem self, ParticleSystem parent)
        {
            if (!self || !parent) return false;

            var subEmitters = parent.subEmitters;
            var count = subEmitters.subEmittersCount;
            for (var i = 0; i < count; i++)
            {
                if (subEmitters.GetSubEmitterSystem(i) == self) return true;
            }

            return false;
        }
    }
}
