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
            shadow_step_size,
            shadow_bias,
            shadow_intensity,
            shadow_step_count,

            object_to_world,
            world_to_object,

            yaw_rad,

            normal_strength,
            normal_tiling,
            normal_map,

            gtao_radius_ws,
            gtao_slice_count,
            gtao_step_count,
            gtao_intensity,
            gtao_thickness_ws
        }

        [Title("Clay Depth")] [SerializeField] private float radius = 0.05f;

        [Title("Bilateral")] [SerializeField] private int maxFilterSize = 100;

        [Title("Shading")] [SerializeField] private Color clayColor = new(0.8f, 0.4f, 0.3f);
        [SerializeField] private Texture2D clayMainTex;
        [SerializeField] private Texture2D clayNormalTex;

        [Title("Screen Space Shadow")] [SerializeField]
        private float shadowStepSize = 0.02f;

        [SerializeField] private float shadowBias = 0.01f;
        [SerializeField] private float shadowIntensity = 0.5f;
        [SerializeField] private int shadowStepCount = 16;

        [Title("GTAO")] [SerializeField] private float gtaoRadius = 0.05f;
        [SerializeField] private int gtaoSliceCount = 4;
        [SerializeField] private int gtaoStepCount = 8;
        [SerializeField] private float gtaoIntensity = 1.0f;
        [SerializeField] private float gtaoThickness = 0.1f;

        [Title("Normal Map")] [SerializeField] private float normalStrength = 0.5f;
        [SerializeField] private float normalTiling = 1.0f;
        [SerializeField] private Texture2D normalMapTex;

        private ClayDepthPass _clayDepthPass;
        private MaterialWrapper<Uniforms> _mat;

        public float AngleRad { get; private set; }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_mat.Material);
        }

        public void RotateClay(float angleRad, Transform root)
        {
            if (!_mat.Material) return;

            AngleRad += angleRad;
            _mat.SetFloat(Uniforms.yaw_rad, AngleRad);
            _mat.SetMatrix(Uniforms.object_to_world, root.localToWorldMatrix);
            _mat.SetMatrix(Uniforms.world_to_object, root.worldToLocalMatrix);
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
            _mat.SetTexture(Uniforms.normal_map, normalMapTex);

            _mat.SetFloat(Uniforms.radius, radius);
            _mat.SetFloat(Uniforms.delta, radius * 10f);
            _mat.SetFloat(Uniforms.mu, radius);
            _mat.SetFloat(Uniforms.sigma_world, radius * 0.7f);
            _mat.SetFloat(Uniforms.max_filter_radius, maxFilterSize);
            _mat.SetFloat(Uniforms.shadow_step_size, shadowStepSize);
            _mat.SetFloat(Uniforms.shadow_bias, shadowBias);
            _mat.SetFloat(Uniforms.shadow_intensity, shadowIntensity);
            _mat.SetInt(Uniforms.shadow_step_count, shadowStepCount);
            _mat.SetColor(Uniforms.clay_color, clayColor);
            _mat.SetFloat(Uniforms.normal_strength, normalStrength);
            _mat.SetFloat(Uniforms.normal_tiling, normalTiling);
            _mat.SetFloat(Uniforms.gtao_radius_ws, gtaoRadius);
            _mat.SetInt(Uniforms.gtao_slice_count, gtaoSliceCount);
            _mat.SetInt(Uniforms.gtao_step_count, gtaoStepCount);
            _mat.SetFloat(Uniforms.gtao_intensity, gtaoIntensity);
            _mat.SetFloat(Uniforms.gtao_thickness_ws, gtaoThickness);

            _clayDepthPass.Setup(particlePosBuffer);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}