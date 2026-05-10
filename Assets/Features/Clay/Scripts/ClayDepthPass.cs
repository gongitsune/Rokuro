using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayDepthPass : ScriptableRenderPass, IDisposable
    {
        private const string ProfilerTag = "Clay Depth Render Pass";
        private const string ShaderName = "Hidden/Clay";

        private MaterialWrapper<Uniforms> _material;
        private GraphicsBuffer _particlePosBuffer;

        public ClayDepthPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        public void Dispose()
        {
            if (_material != null)
                CoreUtils.Destroy(_material.Material);
            _material = null;
            _particlePosBuffer = null;
        }

        public void Setup(GraphicsBuffer particlePosBuffer, float radius)
        {
            _material = new MaterialWrapper<Uniforms>(new Material(Shader.Find(ShaderName)));
            _material.SetBuffer(Uniforms.particle_pos, particlePosBuffer);
            _material.SetFloat(Uniforms.radius, radius);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_particlePosBuffer == null || _particlePosBuffer.count == 0) return;

            var camData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cam = camData.camera;

            _material.SetMatrix(Uniforms.UNITY_MATRIX_V, cam.worldToCameraMatrix);
            _material.SetMatrix(Uniforms.UNITY_MATRIX_P, cam.projectionMatrix);

            using var builder = renderGraph.AddRasterRenderPass(
                "Clay Depth Pass",
                out PassData passData,
                new ProfilingSampler(ProfilerTag)
            );

            passData.Mat = _material.Material;
            passData.ParticleCount = _particlePosBuffer.count;

            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

            builder.SetRenderFunc<PassData>(static (data, ctx) =>
            {
                ctx.cmd.DrawProcedural(
                    Matrix4x4.identity,
                    data.Mat,
                    0,
                    MeshTopology.Triangles,
                    6 * data.ParticleCount
                );
            });
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            particle_pos,
            radius,
            UNITY_MATRIX_V,
            UNITY_MATRIX_P
        }

        private class PassData
        {
            public Material Mat;
            public int ParticleCount;
        }
    }
}