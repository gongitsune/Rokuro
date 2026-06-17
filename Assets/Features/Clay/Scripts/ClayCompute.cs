using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayCompute : IDisposable
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Kernels
        {
            clear_grid,
            particle_to_grid,
            grid_update,
            grid_to_particle,
            reset
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Uniforms
        {
            n_particles,
            n_grid,
            dx,
            inv_dx,
            dt,
            p_vol,
            p_rho,
            p_mass,
            mu_0,
            lambda_0,
            dp_alpha,
            dp_cohesion,
            dp_hardening,
            damping,
            gravity,
            cylinder_radius,
            cylinder_height,
            x,
            particles,
            grid_v,
            grid_m,
            object_forces,
            object_force_count,
            n_mc_grid,
            inv_mc_dx,
            mc_grid
        }

        private readonly ComputeShaderWrapper<Kernels, Uniforms> _computeShader;
        private readonly Desc _desc;
        private readonly GraphicsBuffer _gridMBuf, _gridVBuf;
        private readonly GraphicsBuffer _objectForcesBuf;
        private readonly GraphicsBuffer _posBuf, _particleBuf;

        public ClayCompute(Desc desc, ClayForce.Desc forceDesc)
        {
            _desc = desc;
            _computeShader = new ComputeShaderWrapper<Kernels, Uniforms>(desc.computeShader);

            UpdateConstantBuffer();

            // バッファを初期化
            var gridCount = desc.gridResolution * desc.gridResolution * desc.gridResolution;
            _posBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 3);
            _particleBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 28);
            _gridVBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridCount * 3, sizeof(int));
            _gridMBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridCount, sizeof(int));

            // バッファをシェーダーにバインド
            _computeShader.SetBuffer(Kernels.clear_grid, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetBuffer(Kernels.clear_grid, Uniforms.grid_m, _gridMBuf);

            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.x, _posBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.particles, _particleBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.grid_m, _gridMBuf);

            _computeShader.SetBuffer(Kernels.grid_update, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetBuffer(Kernels.grid_update, Uniforms.grid_m, _gridMBuf);

            // オブジェクト力バッファを初期化（最大8個のオブジェクト）
            _objectForcesBuf = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                forceDesc.maxObjectsDetected, Marshal.SizeOf<ClayForce.ObjectForce>()
            );
            _computeShader.SetBuffer(Kernels.grid_update, Uniforms.object_forces, _objectForcesBuf);
            _computeShader.SetInt(Uniforms.object_force_count, 0);

            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.x, _posBuf);
            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.particles, _particleBuf);
            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.grid_v, _gridVBuf);

            _computeShader.SetBuffer(Kernels.reset, Uniforms.x, _posBuf);
            _computeShader.SetBuffer(Kernels.reset, Uniforms.particles, _particleBuf);
        }

        public int GridResolution => _desc.gridResolution;

        public void Dispose()
        {
            _posBuf?.Dispose();
            _particleBuf?.Dispose();
            _gridMBuf?.Dispose();
            _gridVBuf?.Dispose();
            _objectForcesBuf?.Dispose();
        }

        public GraphicsBuffer GetParticlePosBuffer()
        {
            return _posBuf;
        }

        public GraphicsBuffer GetGridVelBuffer()
        {
            return _gridVBuf;
        }

        public void SetObjectForces(ClayForce.ObjectForce[] forces, int forceCount)
        {
            if (forceCount < 1) return;

            _objectForcesBuf.SetData(forces, 0, 0, forceCount);
            _computeShader.SetInt(Uniforms.object_force_count, forceCount);
        }

        public void Reset()
        {
            var particleThread = new uint3((uint)_desc.particleCount, 1, 1);
            _computeShader.Dispatch(Kernels.reset, particleThread);
        }

        public void Tick()
        {
            var particleThread = new uint3((uint)_desc.particleCount, 1, 1);

            var substep = math.min(_desc.maxIter, math.ceil(Time.deltaTime / _desc.dt));
            for (var i = 0; i < substep; i++)
            {
                _computeShader.Dispatch(Kernels.clear_grid, (uint)_desc.gridResolution);
                _computeShader.Dispatch(Kernels.particle_to_grid, particleThread);
                _computeShader.Dispatch(Kernels.grid_update, (uint)_desc.gridResolution);
                _computeShader.Dispatch(Kernels.grid_to_particle, particleThread);
            }
        }

        private void UpdateConstantBuffer()
        {
            var dx = 1f / _desc.gridResolution;
            var pVol = math.pow(dx * 0.5f, 3);
            var pMass = pVol * _desc.particleRho;
            var mu0 = _desc.youngModulus / (2f * (1f + _desc.nu));
            var lambda0 = _desc.youngModulus * _desc.nu / ((1 + _desc.nu) * (1f - 2f * _desc.nu));
            var sinPhi = math.sin(math.radians(_desc.frictionAngleDeg));
            var dpAlpha = 2f * sinPhi / (math.sqrt(3) * (3 - sinPhi));

            _computeShader.SetInt(Uniforms.n_particles, _desc.particleCount);
            _computeShader.SetInt(Uniforms.n_grid, _desc.gridResolution);
            _computeShader.SetFloat(Uniforms.dx, dx);
            _computeShader.SetFloat(Uniforms.inv_dx, _desc.gridResolution);
            _computeShader.SetFloat(Uniforms.dt, _desc.dt);
            _computeShader.SetFloat(Uniforms.p_vol, pVol);
            _computeShader.SetFloat(Uniforms.p_rho, _desc.particleRho);
            _computeShader.SetFloat(Uniforms.p_mass, pMass);
            _computeShader.SetFloat(Uniforms.mu_0, mu0);
            _computeShader.SetFloat(Uniforms.lambda_0, lambda0);
            _computeShader.SetFloat(Uniforms.dp_alpha, dpAlpha);
            _computeShader.SetFloat(Uniforms.dp_cohesion, _desc.dpCohesion);
            _computeShader.SetFloat(Uniforms.dp_hardening, _desc.dpHardening);
            _computeShader.SetFloat(Uniforms.damping, _desc.damping);
            _computeShader.SetVector(Uniforms.gravity, new float4(_desc.gravity, 0));
            _computeShader.SetFloat(Uniforms.cylinder_radius, _desc.cylinderRadius);
            _computeShader.SetFloat(Uniforms.cylinder_height, _desc.cylinderHeight);
        }

        [Serializable]
        public class Desc
        {
            public ComputeShader computeShader;

            [Title("Common params")] public int particleCount = 18000;
            public int gridResolution = 64;
            public float dt = 2e-4f;
            public int maxIter = 10;
            public float particleRho = 1;
            public float youngModulus = 700;
            public float nu = 0.2f;
            public float3 gravity = new(0, -9.81f, 0);
            public float cylinderRadius = 0.125f;
            public float cylinderHeight = 0.25f;

            [Title("Drucker-Prager plasticity")] public float frictionAngleDeg = 15f;
            public float dpCohesion = 14f;
            public float dpHardening = 0.8f;
            public float damping = 0.95f;
        }
    }
}