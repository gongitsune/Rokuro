using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
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

            delta,
            mu,
            sigma_world,
            max_filter_radius,
            direction,

            clay_color,

            object_to_world,

            yaw_rad
        }

        [Title("Clay Depth")] [SerializeField] private float radius = 0.05f;

        [Title("Bilateral")] [SerializeField] private int maxFilterSize = 100;

        [Title("Shading")] [SerializeField] private Color clayColor = new(0.8f, 0.4f, 0.3f);
        [SerializeField] private Texture2D clayMainTex;
        [SerializeField] private Texture2D clayNormalTex;
        private float _angleRad;

        private ClayDepthPass _clayDepthPass;
        private MaterialWrapper<Uniforms> _mat;

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_mat.Material);
        }

        public void RotateClay(float angleRad)
        {
            if (!_mat.Material) return;

            _angleRad += angleRad;
            _mat.SetFloat(Uniforms.yaw_rad, _angleRad);
        }

        public override void Create()
        {
            _mat = new MaterialWrapper<Uniforms>(CoreUtils.CreateEngineMaterial("Hidden/Clay"));
            _clayDepthPass = new ClayDepthPass(_mat);
        }

        public async UniTask Setup(GraphicsBuffer particlePosBuffer, Transform root)
        {
            await UniTask.WaitUntil(() => _clayDepthPass != null);
            await UniTask.Yield();

            _mat.SetBuffer(Uniforms.particle_pos, particlePosBuffer);

            _mat.SetFloat(Uniforms.radius, radius);
            _mat.SetFloat(Uniforms.delta, radius * 10f);
            _mat.SetFloat(Uniforms.mu, radius);
            _mat.SetFloat(Uniforms.sigma_world, radius * 0.7f);
            _mat.SetFloat(Uniforms.max_filter_radius, maxFilterSize);

            _mat.SetColor(Uniforms.clay_color, clayColor);

            _clayDepthPass.Setup(particlePosBuffer, root);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}