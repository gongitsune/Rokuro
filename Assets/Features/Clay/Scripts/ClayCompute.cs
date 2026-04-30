using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class ClayCompute : IDisposable
    {
        private ComputeShaderWrapper<Kernel, Uniform> _computeShader;
        public RenderTexture SDFTexture { get; private set; }

        public void Dispose()
        {
            if (SDFTexture != null) SDFTexture.Release();
        }

        public void Initialize(Desc desc)
        {
            if (SDFTexture != null) SDFTexture.Release();

            var resolution = desc.resolution;
            var texDesc = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.RHalf)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                enableRandomWrite = true,
                useMipMap = false
            };
            SDFTexture = new RenderTexture(texDesc);
            SDFTexture.Create();

            _computeShader = new ComputeShaderWrapper<Kernel, Uniform>(desc.computeShader);
            _computeShader.SetTexture(Kernel.init_cylinder_sdf, Uniform.sdf_texture, SDFTexture);
            _computeShader.SetInts(Uniform.resolution, resolution, resolution, resolution);
            _computeShader.SetVector(Uniform.bounds_min, new float4(desc.boundsMin, 0));
            _computeShader.SetVector(Uniform.bounds_max, new float4(desc.boundsMax, 0));
            _computeShader.SetFloat(Uniform.cylinder_radius, desc.cylinderRadius);
            _computeShader.SetFloat(Uniform.cylinder_height, desc.cylinderHeight);

            _computeShader.Dispatch(Kernel.init_cylinder_sdf, new uint3(resolution));
        }

        [Serializable]
        public class Desc
        {
            public ComputeShader computeShader;
            public int resolution;
            public float3 boundsMin = new(-1, -1, -1), boundsMax = new(1, 1, 1);
            public float cylinderRadius = 0.4f;
            public float cylinderHeight = 0.6f;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Kernel
        {
            init_cylinder_sdf
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniform
        {
            sdf_texture,
            resolution,
            bounds_min,
            bounds_max,
            cylinder_radius,
            cylinder_height
        }
    }
}