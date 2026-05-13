using Features.Utils.Scripts;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayDepthPass : ScriptableRenderPass
    {
        private const string DepthProfilerTag = "Clay Depth Render Pass";
        private readonly MaterialWrapper<ClayRenderFeature.Uniforms> _mat;
        private readonly int[] _particleCount = { 0 };

        public ClayDepthPass(MaterialWrapper<ClayRenderFeature.Uniforms> mat)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            _mat = mat;
        }

        public void Setup(GraphicsBuffer particlePosBuffer)
        {
            _particleCount[0] = particlePosBuffer.count;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cam = camData.camera;

            var depthDesc = renderGraph.GetTextureDesc(resourceData.activeDepthTexture);
            depthDesc.name = "Smooth Temp RT";
            depthDesc.msaaSamples = MSAASamples.None;
            depthDesc.clearBuffer = true;
            var depthTempRT = renderGraph.CreateTexture(depthDesc);

            var colorDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            colorDesc.name = "Reconstruct Normal RT";
            colorDesc.msaaSamples = MSAASamples.None;
            colorDesc.clearBuffer = true;
            colorDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            var normalRT = renderGraph.CreateTexture(colorDesc);

            var invP = Matrix4x4.Inverse(cam.worldToCameraMatrix);
            _mat.SetMatrix(ClayRenderFeature.Uniforms.matrix_v, cam.worldToCameraMatrix);
            _mat.SetMatrix(ClayRenderFeature.Uniforms.matrix_p, cam.projectionMatrix);
            _mat.SetMatrix(ClayRenderFeature.Uniforms.matrix_inv_p, invP);

            using (var builder = renderGraph.AddRasterRenderPass(
                       "Clay Depth Pass",
                       out PassData passData,
                       new ProfilingSampler(DepthProfilerTag)
                   ))
            {
                passData.Mat = _mat.Material;
                passData.ParticleCount = _particleCount;

                builder.AllowPassCulling(false);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc<PassData>(static (data, ctx) =>
                {
                    if (data.ParticleCount[0] <= 0) return;

                    using (new ProfilingScope(ctx.cmd, new ProfilingSampler(DepthProfilerTag)))
                    {
                        ctx.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.Mat,
                            0,
                            MeshTopology.Triangles,
                            6 * data.ParticleCount[0]
                        );
                    }
                });
            }

            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(
                    resourceData.activeDepthTexture,
                    depthTempRT,
                    _mat.Material,
                    1
                ),
                "Bilateral Horizontal Pass"
            );
            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(
                    depthTempRT,
                    resourceData.activeDepthTexture,
                    _mat.Material,
                    2
                ),
                "Bilateral Vertical Pass"
            );
            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           resourceData.activeDepthTexture,
                           normalRT,
                           _mat.Material,
                           3
                       ),
                       "Reconstruct Normal Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
                builder.SetGlobalTextureAfterPass(normalRT, _mat.GetPropertyId(ClayRenderFeature.Uniforms._NormalRT));
            }

            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           resourceData.activeDepthTexture,
                           resourceData.activeColorTexture,
                           _mat.Material,
                           4
                       ),
                       "Clay Shading Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
                builder.UseTexture(normalRT);
            }
        }

        private class PassData
        {
            public Material Mat;
            public int[] ParticleCount;
        }
    }
}