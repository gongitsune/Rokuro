using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayManager : MonoBehaviour
    {
        [SerializeField] private ClayCompute.Desc computeDesc;
        [SerializeField] private ClayRenderer.Desc rendererDesc;
        [SerializeField] private ClayParticleRenderer.Desc particleRendererDesc;
        [SerializeField] private ClayGridVelRenderer.Desc gridVelRendererDesc;
        [SerializeField] private ClayForce.Desc clayForceDesc;
        private ClayForce _clayForce;

        private ClayCompute _compute;
        private ClayGridVelRenderer _gridVelRenderer;
        private ClayParticleRenderer _particleRenderer;
        private ClayRenderer _renderer;

        private void Start()
        {
            Application.targetFrameRate = 60;

            _compute = new ClayCompute(computeDesc);
            _renderer = new ClayRenderer(rendererDesc, computeDesc, _compute);
            _particleRenderer = new ClayParticleRenderer(particleRendererDesc, _compute, transform);
            _gridVelRenderer = new ClayGridVelRenderer(gridVelRendererDesc, _compute);
            _clayForce = new ClayForce(clayForceDesc);

            _compute.Reset();
        }

        private void Update()
        {
            // オブジェクト検出・力更新
            _clayForce.Update(transform.position, 1f);

            // オブジェクト力をシェーダーに設定
            _compute.SetObjectForces(_clayForce.GetActiveForces(), _clayForce.GetActiveForceCount());

            // シミュレーション実行
            _compute.Tick();

            _particleRenderer.Draw();
            _gridVelRenderer.Draw();
        }

        private void OnDestroy()
        {
            _compute.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                _particleRenderer.OnDrawGizmos();
                _clayForce?.DrawGizmos(transform.position, 1f);
            }
        }
    }
}