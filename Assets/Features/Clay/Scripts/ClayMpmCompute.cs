using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class ClayMpmCompute : IDisposable
    {
        private readonly ComputeShaderWrapper<Kernels, Uniforms> _computeShader;
        private readonly Desc _desc;
        private readonly RenderTexture _gridVBuf, _gridMBuf;
        private readonly GraphicsBuffer _xBuf, _vBuf, _cBuf, _fBuf, _jpBuf;

        public ClayMpmCompute(Desc desc)
        {
            _desc = desc;
            _computeShader = new ComputeShaderWrapper<Kernels, Uniforms>(desc.computeShader);

            UpdateConstantBuffer();

            // バッファを初期化
            _xBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 3);
            _vBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 3);
            _cBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 9);
            _fBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float) * 9);
            _jpBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, desc.particleCount, sizeof(float));
            _gridVBuf = CreateRT(desc.gridResolution);
            _gridMBuf = CreateRT(desc.gridResolution);

            // バッファをシェーダーにバインド
            _computeShader.SetTexture(Kernels.clear_grid, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetTexture(Kernels.clear_grid, Uniforms.grid_m, _gridMBuf);

            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.x, _xBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.c, _cBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.f, _fBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.v, _vBuf);
            _computeShader.SetBuffer(Kernels.particle_to_grid, Uniforms.jp, _jpBuf);
            _computeShader.SetTexture(Kernels.particle_to_grid, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetTexture(Kernels.particle_to_grid, Uniforms.grid_m, _gridMBuf);

            _computeShader.SetTexture(Kernels.grid_update, Uniforms.grid_v, _gridVBuf);
            _computeShader.SetTexture(Kernels.grid_update, Uniforms.grid_m, _gridMBuf);

            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.x, _xBuf);
            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.c, _cBuf);
            _computeShader.SetBuffer(Kernels.grid_to_particle, Uniforms.v, _vBuf);
            _computeShader.SetTexture(Kernels.grid_to_particle, Uniforms.grid_v, _gridVBuf);

            _computeShader.SetBuffer(Kernels.reset, Uniforms.x, _xBuf);
            _computeShader.SetBuffer(Kernels.reset, Uniforms.c, _cBuf);
            _computeShader.SetBuffer(Kernels.reset, Uniforms.f, _fBuf);
            _computeShader.SetBuffer(Kernels.reset, Uniforms.v, _vBuf);
            _computeShader.SetBuffer(Kernels.reset, Uniforms.jp, _jpBuf);
        }

        public void Dispose()
        {
            _xBuf?.Dispose();
            _vBuf?.Dispose();
            _cBuf?.Dispose();
            _fBuf?.Dispose();
            _jpBuf?.Dispose();
            _gridMBuf?.Release();
            _gridVBuf?.Release();
        }

        public GraphicsBuffer GetParticlePosBuffer()
        {
            return _xBuf;
        }

        public void Reset()
        {
            var particleThread = new uint3((uint)_desc.particleCount, 1, 1);
            _computeShader.Dispatch(Kernels.reset, particleThread);
        }

        public void Tick()
        {
            var particleThread = new uint3((uint)_desc.particleCount, 1, 1);
            _computeShader.Dispatch(Kernels.clear_grid, (uint)_desc.gridResolution);
            _computeShader.Dispatch(Kernels.particle_to_grid, particleThread);
            _computeShader.Dispatch(Kernels.grid_update, (uint)_desc.gridResolution);
            _computeShader.Dispatch(Kernels.grid_to_particle, particleThread);
        }

        private static RenderTexture CreateRT(int resolution)
        {
            var desc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.ARGBFloat)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                enableRandomWrite = true,
                useMipMap = false
            };
            var rt = new RenderTexture(desc);
            rt.Create();
            return rt;
        }

        private void UpdateConstantBuffer()
        {
            var pVol = math.pow(1f / _desc.gridResolution, 3);
            var pMass = pVol * _desc.particleRho;
            var mu0 = _desc.youngModulus / (2f * (1f + _desc.nu));
            var lambda0 = _desc.youngModulus * _desc.nu / ((1 + _desc.nu) * (1f - 2f * _desc.nu));
            var sinPhi = math.sin(math.radians(_desc.frictionAngleDeg));
            var dpAlpha = 2f * sinPhi / (math.sqrt(3) * (3 - sinPhi));

            _computeShader.SetInt(Uniforms.n_particles, _desc.particleCount);
            _computeShader.SetInt(Uniforms.n_grid, _desc.gridResolution);
            _computeShader.SetFloat(Uniforms.dx, 1f / _desc.gridResolution);
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
            public float particleRho = 1;
            public float youngModulus = 700;
            public float nu = 0.2f;
            public float3 gravity = new(0, -9.81f, 0);
            public float cylinderRadius = 0.125f;
            public float cylinderHeight = 0.25f;

            [Title("Drucker-Prager plasticity")] public float frictionAngleDeg = 15f;
            public float dpCohesion = 14f;
            public float dpHardening = 0.8f;
            public float damping = 0.96f;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Kernels
        {
            clear_grid,
            particle_to_grid,
            grid_update,
            grid_to_particle,
            reset
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
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
            v,
            c,
            f,
            jp,
            grid_v,
            grid_m
        }
    }
}