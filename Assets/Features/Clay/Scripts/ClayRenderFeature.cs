using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Features.Clay.Scripts
{
    public class ClayRenderFeature : ScriptableRendererFeature
    {
        [Title("Clay Depth")] [SerializeField] private float radius = 0.05f;

        [Title("Bilateral")] [SerializeField] private float sigmaSpace = 5f;
        [SerializeField] private float sigmaDepth = 0.01f;
        [SerializeField] private int kernelRadius = 7;


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

            _clayDepthPass.Setup(
                particlePosBuffer,
                radius,
                sigmaSpace, sigmaDepth, kernelRadius
            );
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_clayDepthPass);
        }
    }
}