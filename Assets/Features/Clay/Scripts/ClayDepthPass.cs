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
        private readonly MaterialWrapper<Uniforms> _mat;
        private readonly int[] _particleCount = { 0 };
        private PassData _passData;

        public ClayDepthPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
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
            _particleCount[0] = particlePosBuffer.count;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cam = camData.camera;

            _mat.SetMatrix(Uniforms.matrix_v, cam.worldToCameraMatrix);
            _mat.SetMatrix(Uniforms.matrix_p, cam.projectionMatrix);

            using var builder = renderGraph.AddRasterRenderPass(
                "Clay Depth Pass",
                out _passData,
                new ProfilingSampler(ProfilerTag)
            );

            _passData.Mat = _mat;
            _passData.ParticleCount = _particleCount;

            builder.AllowPassCulling(false);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

            builder.SetRenderFunc<PassData>(static (data, ctx) =>
            {
                if (data.ParticleCount[0] <= 0) return;

                using (new ProfilingScope(ctx.cmd, new ProfilingSampler(ProfilerTag)))
                {
                    ctx.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.Mat.Material,
                        0,
                        MeshTopology.Triangles,
                        6 * data.ParticleCount[0]
                    );
                }
            });
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            particle_pos,
            radius,
            matrix_v,
            matrix_p
        }

        private class PassData
        {
            public MaterialWrapper<Uniforms> Mat;
            public int[] ParticleCount;
        }
    }
}