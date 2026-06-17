using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayManager : MonoBehaviour
    {
        [SerializeField] private ClayCompute.Desc computeDesc;
        [SerializeField] private ClayMc.Desc clayMcDesc;
        [SerializeField] private ClayRenderer.Desc rendererDesc;
        [SerializeField] private ClayParticleRenderer.Desc particleRendererDesc;
        [SerializeField] private ClayGridVelRenderer.Desc gridVelRendererDesc;
        [SerializeField] private ClayForce.Desc clayForceDesc;
        [SerializeField] private bool debugDraw = true;

        private ClayForce _clayForce;
        private ClayCompute _compute;
        private ClayGridVelRenderer _gridVelRenderer;
        private ClayParticleRenderer _particleRenderer;

        private void Start()
        {
            _compute = new ClayCompute(computeDesc);
            _particleRenderer = new ClayParticleRenderer(particleRendererDesc, _compute, transform);
            _gridVelRenderer = new ClayGridVelRenderer(gridVelRendererDesc, _compute);
            _clayForce = new ClayForce(clayForceDesc, transform);
            _ = new ClayRenderer(rendererDesc, _compute, transform);

            _compute.Reset();
        }

        private void Update()
        {
            _clayForce.Update(transform.position, 1f);

            _compute.SetObjectForces(_clayForce.GetActiveForces(), _clayForce.GetActiveForceCount());
            _compute.Tick();

            if (debugDraw)
                _gridVelRenderer.Draw();
        }

        private void OnDestroy()
        {
            _compute.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            _particleRenderer.OnDrawGizmos();
            _clayForce?.DrawGizmos(transform.position, 1f);
        }
    }
}