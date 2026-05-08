using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayManager : MonoBehaviour
    {
        [SerializeField] private ClayCompute.Desc computeDesc;
        [SerializeField] private ClayRenderer.Desc rendererDesc;

        private ClayCompute _compute;
        private ClayRenderer _renderer;

        private void Start()
        {
            Application.targetFrameRate = 60;

            _compute = new ClayCompute(computeDesc);
            _renderer = new ClayRenderer(rendererDesc, computeDesc, _compute);

            _compute.Reset();
        }

        private void Update()
        {
            _compute.Tick();
            _renderer.Draw();
        }

        private void OnDestroy()
        {
            _compute.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) _renderer.OnDrawGizmos();
        }
    }
}