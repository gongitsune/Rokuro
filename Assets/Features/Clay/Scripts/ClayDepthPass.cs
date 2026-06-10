using Features.Utils.Scripts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayDepthPass : ScriptableRenderPass
    {
        private const string DepthProfilerTag = "Clay Depth Pass";
        private const string ShadingProfilerTag = "Clay Shading Pass";
        private readonly MaterialWrapper<ClayRenderFeature.Uniforms> _mat;
        private readonly int[] _particleCount = { 0 };

        public ClayDepthPass(MaterialWrapper<ClayRenderFeature.Uniforms> mat)
        {
            _mat = mat;

            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        public void Setup(GraphicsBuffer particlePosBuffer)
        {
            _particleCount[0] = particlePosBuffer.count;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            var depthDesc = renderGraph.GetTextureDesc(resourceData.activeDepthTexture);
            depthDesc.msaaSamples = MSAASamples.None;
            depthDesc.clearBuffer = true;

            var depthTempRTs = new TextureHandle[2];
            for (var i = 0; i < 2; i++)
            {
                depthDesc.name = $"Smooth Temp RT {i}";
                depthTempRTs[i] = renderGraph.CreateTexture(depthDesc);
            }

            using (var builder = renderGraph.AddRasterRenderPass(
                       "Clay Depth Pass",
                       out DepthPassData passData,
                       new ProfilingSampler(DepthProfilerTag)
                   ))
            {
                passData.Mat = _mat.Material;
                passData.ParticleCount = _particleCount;

                builder.AllowPassCulling(false);
                builder.SetRenderAttachmentDepth(depthTempRTs[0], AccessFlags.Write);

                builder.SetRenderFunc<DepthPassData>(static (data, ctx) =>
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

            var props = new MaterialPropertyBlock[2];
            for (var i = 0; i < 2; i++)
            {
                var prop = new MaterialPropertyBlock();
                prop.SetInt(_mat.GetPropertyId(ClayRenderFeature.Uniforms.direction), i % 2);
                props[i] = prop;
            }

            for (var i = 0; i < 2 * 2; i++)
            {
                var prop = props[i % 2];
                var src = depthTempRTs[i % 2];
                var dst = depthTempRTs[(i + 1) % 2];
                var dir = i % 2 == 0 ? "Horizontal" : "Vertical";

                using var builder = renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(
                        src, dst, _mat.Material, 1, prop,
                        RenderGraphUtils.FullScreenGeometryType.ProceduralQuad
                    ),
                    $"Narrow Range Filter Pass {dir}",
                    true
                );

                builder.AllowPassCulling(false);
            }

            var cleanUpProp = new MaterialPropertyBlock();
            cleanUpProp.SetInt(_mat.GetPropertyId(ClayRenderFeature.Uniforms.direction), 2);
            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           depthTempRTs[0], resourceData.activeDepthTexture,
                           _mat.Material, 1, cleanUpProp
                       ),
                       "Narrow Range Filter CleanUp Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
            }


            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           resourceData.activeDepthTexture,
                           resourceData.activeColorTexture,
                           _mat.Material,
                           2
                       ),
                       "Clay Shading Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
            }
        }

        private class DepthPassData
        {
            public Material Mat;
            public int[] ParticleCount;
        }

        private class ShadingPassData
        {
            public TextureHandle DepthTex, WorldPosTex;
            public MaterialWrapper<ClayRenderFeature.Uniforms> Mat;
            public int[] ParticleCount;
            public MaterialPropertyBlock PropBlock;
        }
    }
}