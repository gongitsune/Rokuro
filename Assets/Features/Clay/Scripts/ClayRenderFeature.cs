using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private float radius = 0.05f;

        private ClayDepthPass _clayDepthPass;

        protected override void Dispose(bool disposing)
        {
            _clayDepthPass.Dispose();
        }

        public void Setup(ClayCompute clayCompute)
        {
            _clayDepthPass.Setup(clayCompute.GetParticlePosBuffer(), radius);
        }

        public override void Create()
        {
            _clayDepthPass = new ClayDepthPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}