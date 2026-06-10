using Unity.Mathematics;
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
        [SerializeField] private bool debugDraw = true;

        private ClayForce _clayForce;
        private ClayCompute _compute;
        private ClayGridVelRenderer _gridVelRenderer;
        private Light _light;
        private ClayParticleRenderer _particleRenderer;
        private ClayRenderer _renderer;

        private Camera _sceneCam;

        private void Start()
        {
            _compute = new ClayCompute(computeDesc);
            _renderer = new ClayRenderer(rendererDesc, _compute, transform);
            _particleRenderer = new ClayParticleRenderer(particleRendererDesc, _compute, transform);
            _gridVelRenderer = new ClayGridVelRenderer(gridVelRendererDesc, _compute);
            _clayForce = new ClayForce(clayForceDesc);

            _compute.Reset();

            _sceneCam = Camera.main;
            _light = FindAnyObjectByType<Light>();
        }

        private void Update()
        {
            _clayForce.Update(transform.position, 1f);

            _compute.SetObjectForces(_clayForce.GetActiveForces(), _clayForce.GetActiveForceCount());
            _compute.Tick();

            if (debugDraw)
                _gridVelRenderer.Draw();

            if (_sceneCam)
            {
                var lightDir = math.mul(_light.transform.rotation, new Vector3(0, 0, -1));
                var camPos = new float3(_sceneCam.transform.position);
                Debug.DrawLine(camPos, camPos + lightDir, Color.yellow);

                var viewLightDir = _sceneCam.worldToCameraMatrix.MultiplyPoint(lightDir);
                Debug.DrawLine(camPos, camPos + new float3(viewLightDir), Color.red);
            }
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