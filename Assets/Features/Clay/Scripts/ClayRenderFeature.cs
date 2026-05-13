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

            projected_particle_constant,
            depth_threshold,
            max_filter_size,
            blur_dir,

            light_dir,
            light_color,
            clay_color,
            sss_strength
        }

        [Title("Clay Depth")] [SerializeField] private float radius = 0.05f;

        [Title("Bilateral")] [SerializeField] private int maxFilterSize = 100;
        [SerializeField] private float blueDepthScale = 10;
        [SerializeField] private float blurFilterSize = 12;

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
            _clayDepthPass = new ClayDepthPass(_mat, CalcProjectedParticleConstant);
        }

        public async UniTask Setup(GraphicsBuffer particlePosBuffer)
        {
            await UniTask.WaitUntil(() => _clayDepthPass != null);
            await UniTask.Yield();

            _mat.SetBuffer(Uniforms.particle_pos, particlePosBuffer);
            _mat.SetFloat(Uniforms.radius, radius);
            _mat.SetFloat(Uniforms.depth_threshold, radius * blueDepthScale);
            _mat.SetInt(Uniforms.max_filter_size, maxFilterSize);
            _mat.SetVector(Uniforms.light_dir, new float4(math.normalize(lightDir), 1));
            _mat.SetColor(Uniforms.light_color, lightColor);
            _mat.SetColor(Uniforms.clay_color, clayColor);
            _mat.SetFloat(Uniforms.sss_strength, sssStrength);

            _clayDepthPass.Setup(particlePosBuffer);
        }

        private float CalcProjectedParticleConstant(float height, float fov)
        {
            var diameter = radius * 2f;
            return blurFilterSize * diameter * 0.05f * (height * 0.5f) / math.tan(fov * 0.5f);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}