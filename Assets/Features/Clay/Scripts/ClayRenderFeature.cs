using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayRenderFeature : ScriptableRendererFeature
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum Uniforms
        {
            particle_pos,
            radius,
            sigma_space,
            sigma_depth,
            kernel_radius,
            light_dir,
            light_color,
            clay_color,
            sss_strength,
            matrix_v,
            matrix_p,
            matrix_inv_p,
            _NormalRT
        }

        [Title("Clay Depth")] [SerializeField] private float radius = 0.05f;

        [Title("Bilateral")] [SerializeField] private float sigmaSpace = 5f;
        [SerializeField] private float sigmaDepth = 0.01f;
        [SerializeField] private int kernelRadius = 7;

        [Title("Shading")] [SerializeField] private float3 lightDir = new(1f, 1f, -1f);
        [SerializeField] private Color lightColor = Color.white;
        [SerializeField] private Color clayColor = new(0.8f, 0.4f, 0.3f);
        [SerializeField] private float sssStrength = 2.0f;

        private ClayDepthPass _clayDepthPass;
        private MaterialWrapper<Uniforms> _mat;

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_mat.Material);
        }

        public override void Create()
        {
            _mat = new MaterialWrapper<Uniforms>(CoreUtils.CreateEngineMaterial("Hidden/Clay"));
            _clayDepthPass = new ClayDepthPass(_mat);
        }

        public async UniTask Setup(GraphicsBuffer particlePosBuffer)
        {
            await UniTask.WaitUntil(() => _clayDepthPass != null);
            await UniTask.Yield();

            _mat.SetBuffer(Uniforms.particle_pos, particlePosBuffer);
            _mat.SetFloat(Uniforms.radius, radius);
            _mat.SetFloat(Uniforms.sigma_space, sigmaSpace);
            _mat.SetFloat(Uniforms.sigma_depth, sigmaDepth);
            _mat.SetInt(Uniforms.kernel_radius, kernelRadius);
            _mat.SetVector(Uniforms.light_dir, new float4(math.normalize(lightDir), 1));
            _mat.SetColor(Uniforms.light_color, lightColor);
            _mat.SetColor(Uniforms.clay_color, clayColor);
            _mat.SetFloat(Uniforms.sss_strength, sssStrength);

            _clayDepthPass.Setup(particlePosBuffer);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}