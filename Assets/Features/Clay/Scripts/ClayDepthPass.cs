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
        private readonly bool[] _drawable;
        private readonly MaterialWrapper<Uniforms> _mat;
        private PassData _passData;

        public ClayDepthPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            _drawable = new[] { false };
            _mat = new MaterialWrapper<Uniforms>(new Material(Shader.Find(ShaderName)));
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_mat.Material);
        }

        public void Setup(GraphicsBuffer particlePosBuffer, float radius)
        {
            _mat.SetBuffer(Uniforms.particle_pos, particlePosBuffer);
            _mat.SetFloat(Uniforms.radius, radius);
            _drawable[0] = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cam = camData.camera;

            _mat.SetMatrix(Uniforms.UNITY_MATRIX_V, cam.worldToCameraMatrix);
            _mat.SetMatrix(Uniforms.UNITY_MATRIX_P, cam.projectionMatrix);

            using var builder = renderGraph.AddRasterRenderPass(
                "Clay Depth Pass",
                out _passData,
                new ProfilingSampler(ProfilerTag)
            );

            _passData.ParticleCount = 0;
            _passData.Drawable = _drawable;
            _passData.Mat = _mat;

            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

            builder.SetRenderFunc<PassData>(static (data, ctx) =>
            {
                if (!data.Drawable[0]) return;

                ctx.cmd.DrawProcedural(
                    Matrix4x4.identity,
                    data.Mat.Material,
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
            public bool[] Drawable;
            public MaterialWrapper<Uniforms> Mat;
            public int ParticleCount;
        }
    }
}