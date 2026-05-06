using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayMpmManager : MonoBehaviour
    {
        [SerializeField] private ClayMpmCompute.Desc computeDesc;
        [SerializeField] private ClayMpmRenderer.Desc rendererDesc;

        private ClayMpmCompute _compute;
        private ClayMpmRenderer _renderer;

        private void Start()
        {
            Application.targetFrameRate = 60;

            _compute = new ClayMpmCompute(computeDesc);
            _renderer = new ClayMpmRenderer(rendererDesc, computeDesc, _compute);

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
    }
}