using Cysharp.Threading.Tasks;
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

        public override void Create()
        {
            _clayDepthPass = new ClayDepthPass();
        }

        public async UniTask Setup(GraphicsBuffer particlePosBuffer)
        {
            await UniTask.WaitUntil(() => _clayDepthPass != null);
            await UniTask.Yield();

            _clayDepthPass.Setup(particlePosBuffer, radius);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}