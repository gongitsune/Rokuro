using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class SdfReconstructor : IDisposable
    {
        private ClayCompute _clayCompute;
        private ClayCompute.Desc _clayDesc;
        private ComputeShaderWrapper<Kernels, Uniforms> _computeShader;
        private Desc _desc;
        private int _frameCount;

        [ReadOnly] [ShowInInspector] private RenderTexture _seedBuffer, _tempBuffer;

        public void Dispose()
        {
            _seedBuffer?.Release();
            _tempBuffer?.Release();
        }

        public void Initialize(Desc desc, ClayCompute.Desc clayDesc, ClayCompute clayCompute)
        {
            _desc = desc;
            _clayDesc = clayDesc;
            _clayCompute = clayCompute;
            _computeShader = new ComputeShaderWrapper<Kernels, Uniforms>(desc.computeShader);

            _seedBuffer = CreateRT(clayDesc.resolution);
            _tempBuffer = CreateRT(clayDesc.resolution);
        }

        public void Tick()
        {
            _frameCount++;
            if (_frameCount % _desc.periodFrame != 0) return;

            Reconstruct();
        }

        private void Reconstruct()
        {
            var resolution = _clayDesc.resolution;

            _computeShader.SetInts(Uniforms.resolution, resolution, resolution, resolution);
            _computeShader.SetVector(Uniforms.bounds_min, new float4(_clayDesc.boundsMin, 0));
            _computeShader.SetVector(Uniforms.bounds_max, new float4(_clayDesc.boundsMax, 0));

            // ステップ1: シード点のマーク
            _computeShader.SetTexture(Kernels.mark_seeds, Uniforms.sdf, _clayCompute.SDFTexture);
            _computeShader.SetTexture(Kernels.mark_seeds, Uniforms.seed_buffer, _seedBuffer);
            _computeShader.Dispatch(Kernels.mark_seeds, new uint3(resolution));

            // ステップ2: JFA
            var step = Mathf.NextPowerOfTwo(resolution) / 2;
            while (step > 0)
            {
                _computeShader.SetTexture(Kernels.jump_flood, Uniforms.seed_buffer, _seedBuffer);
                _computeShader.SetTexture(Kernels.jump_flood, Uniforms.temp_buffer, _tempBuffer);
                _computeShader.SetInt(Uniforms.jfa_step, step);
                _computeShader.Dispatch(Kernels.jump_flood, new uint3(resolution));

                // バッファの入れ替え
                (_seedBuffer, _tempBuffer) = (_tempBuffer, _seedBuffer);

                step /= 2;
            }

            _computeShader.SetTexture(Kernels.jump_flood, Uniforms.seed_buffer, _seedBuffer);
            _computeShader.SetTexture(Kernels.jump_flood, Uniforms.temp_buffer, _tempBuffer);
            _computeShader.SetInt(Uniforms.jfa_step, 1);
            _computeShader.Dispatch(Kernels.jump_flood, new uint3(resolution));

            // ステップ3: SDF再構築
            _computeShader.SetTexture(Kernels.apply_sdf, Uniforms.sdf, _clayCompute.SDFTexture);
            _computeShader.SetTexture(Kernels.apply_sdf, Uniforms.seed_buffer, _seedBuffer);
            _computeShader.Dispatch(Kernels.apply_sdf, new uint3(resolution));
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

        [Serializable]
        public class Desc
        {
            public ComputeShader computeShader;
            public int periodFrame = 5;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Kernels
        {
            mark_seeds,
            jump_flood,
            apply_sdf
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            sdf,
            seed_buffer,
            temp_buffer,
            resolution,
            bounds_max,
            bounds_min,
            jfa_step
        }
    }
}