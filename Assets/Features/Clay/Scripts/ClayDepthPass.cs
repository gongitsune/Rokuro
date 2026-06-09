using System;
using Features.Utils.Scripts;
using Unity.Mathematics;
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
        private readonly Func<float, float, float> _calcProjected;
        private readonly MaterialWrapper<ClayRenderFeature.Uniforms> _mat;
        private readonly int[] _particleCount = { 0 };

        public ClayDepthPass(MaterialWrapper<ClayRenderFeature.Uniforms> mat, Func<float, float, float> calcProjected)
        {
            _calcProjected = calcProjected;
            _mat = mat;

            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
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

            var worldPosDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            worldPosDesc.name = "World Position RT";
            worldPosDesc.msaaSamples = MSAASamples.None;
            worldPosDesc.clearBuffer = true;
            var worldPosRT = renderGraph.CreateTexture(worldPosDesc);

            _mat.SetFloat(
                ClayRenderFeature.Uniforms.projected_particle_constant,
                _calcProjected(depthDesc.height, math.radians(cam.fieldOfView))
            );

            var blurHProp = new MaterialPropertyBlock();
            blurHProp.SetVector(
                _mat.GetPropertyId(ClayRenderFeature.Uniforms.blur_dir),
                new Vector4(1, 0, 0, 0)
            );
            var blurVProp = new MaterialPropertyBlock();
            blurVProp.SetVector(
                _mat.GetPropertyId(ClayRenderFeature.Uniforms.blur_dir),
                new Vector4(0, 1, 0, 0)
            );
            var shadingProp = new MaterialPropertyBlock();

            using (var builder = renderGraph.AddRasterRenderPass(
                       "Clay Depth Pass",
                       out DepthPassData passData,
                       new ProfilingSampler(DepthProfilerTag)
                   ))
            {
                passData.Mat = _mat.Material;
                passData.ParticleCount = _particleCount;

                builder.AllowPassCulling(false);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
                builder.SetRenderAttachment(worldPosRT, 0);

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

            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           resourceData.activeDepthTexture,
                           depthTempRT,
                           _mat.Material,
                           1,
                           blurHProp,
                           RenderGraphUtils.FullScreenGeometryType.ProceduralQuad
                       ),
                       "Bilateral Horizontal Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
            }

            using (var builder = renderGraph.AddBlitPass(
                       new RenderGraphUtils.BlitMaterialParameters(
                           depthTempRT,
                           resourceData.activeDepthTexture,
                           _mat.Material,
                           1,
                           blurVProp,
                           RenderGraphUtils.FullScreenGeometryType.ProceduralQuad
                       ),
                       "Bilateral Vertical Pass",
                       true
                   ))
            {
                builder.AllowPassCulling(false);
            }

            using (var builder = renderGraph.AddRasterRenderPass(
                       "Clay Shading Pass",
                       out ShadingPassData passData,
                       new ProfilingSampler(ShadingProfilerTag)
                   ))
            {
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.UseTexture(resourceData.activeDepthTexture);
                builder.UseTexture(worldPosRT);
                builder.AllowPassCulling(false);

                passData.Mat = _mat;
                passData.ParticleCount = _particleCount;
                passData.DepthTex = resourceData.activeDepthTexture;
                passData.WorldPosTex = worldPosRT;
                passData.PropBlock = shadingProp;

                builder.SetRenderFunc<ShadingPassData>(static (data, ctx) =>
                {
                    if (data.ParticleCount[0] <= 0) return;

                    using (new ProfilingScope(ctx.cmd, new ProfilingSampler(ShadingProfilerTag)))
                    {
                        data.PropBlock.SetTexture(
                            data.Mat.GetPropertyId(ClayRenderFeature.Uniforms._BlitTexture),
                            data.DepthTex
                        );
                        data.PropBlock.SetVector(
                            data.Mat.GetPropertyId(ClayRenderFeature.Uniforms._BlitScaleBias),
                            new Vector4(1, 1, 0, 0)
                        );
                        ctx.cmd.DrawProcedural(
                            Matrix4x4.identity, data.Mat.Material, 2,
                            MeshTopology.Triangles, 3, 1,
                            data.PropBlock
                        );
                    }
                });
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