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
        private Desc _desc;
        private Vector4[] _fingerPositions, _fingerDirections;
        public RenderTexture SDFTexture { get; private set; }

        public void Dispose()
        {
            if (SDFTexture != null) SDFTexture.Release();
        }

        public void Initialize(Desc desc)
        {
            if (SDFTexture != null) SDFTexture.Release();

            _desc = desc;
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
            _computeShader.SetTexture(Kernel.deform_sdf, Uniform.sdf_texture, SDFTexture);

            _computeShader.SetInts(Uniform.resolution, resolution, resolution, resolution);
            _computeShader.SetVector(Uniform.bounds_min, new float4(desc.boundsMin, 0));
            _computeShader.SetVector(Uniform.bounds_max, new float4(desc.boundsMax, 0));
            _computeShader.SetFloat(Uniform.cylinder_radius, desc.cylinderRadius);
            _computeShader.SetFloat(Uniform.cylinder_height, desc.cylinderHeight);
            _computeShader.SetFloat(Uniform.finger_radius, desc.fingerRadius);
            _computeShader.SetFloat(Uniform.finger_strength, desc.fingerStrength);

            _computeShader.Dispatch(Kernel.init_cylinder_sdf, new uint3(resolution));
        }

        public void Tick()
        {
            var resolution = _desc.resolution;
            _computeShader.Dispatch(Kernel.deform_sdf, new uint3(resolution));
        }

        public void OnDrawGizmos(in float3 origin)
        {
            if (_fingerPositions == null) return;

            Gizmos.color = Color.green;
            for (var i = 0; i < _fingerPositions.Length; i++)
            {
                var pos = _fingerPositions[i];
                var dir = _fingerDirections[i];
                var center = new float3(pos.x, pos.y, pos.z) + origin;
                Gizmos.DrawWireSphere(center, _desc.fingerRadius);
                Gizmos.DrawLine(center, center + new float3(dir.x, dir.y, dir.z) * 0.5f);
            }
        }

        public void UpdateFingerPositions(Vector4[] positions, Vector4[] directions)
        {
            _fingerPositions = positions;
            _fingerDirections = directions;
            _computeShader.SetVectorArray(Uniform.finger_positions, positions);
            _computeShader.SetVectorArray(Uniform.finger_directions, directions);
        }

        [Serializable]
        public class Desc
        {
            public ComputeShader computeShader;
            public int resolution;
            public float3 boundsMin = new(-1, -1, -1), boundsMax = new(1, 1, 1);
            public float cylinderRadius = 0.4f;
            public float cylinderHeight = 0.6f;
            public float fingerRadius = 0.15f;
            public float fingerStrength = 0.005f;
            public float rotateSpeed = 0.1f;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Kernel
        {
            init_cylinder_sdf,
            deform_sdf
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniform
        {
            sdf_texture,
            resolution,
            bounds_min,
            bounds_max,
            cylinder_radius,
            cylinder_height,
            finger_positions,
            finger_radius,
            finger_strength,
            finger_directions
        }
    }
}