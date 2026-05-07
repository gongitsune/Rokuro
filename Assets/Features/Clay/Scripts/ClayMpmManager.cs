using UnityEngine;
using UnityEngine.Apple;

namespace Features.Clay.Scripts
{
    public class ClayMpmManager : MonoBehaviour
    {
        [SerializeField] private ClayMpmCompute.Desc computeDesc;
        [SerializeField] private ClayMpmRenderer.Desc rendererDesc;

        private ClayMpmCompute _compute;
        private ClayMpmRenderer _renderer;
        private bool _tick, _capture;

        private void Start()
        {
            Application.targetFrameRate = 60;

            _compute = new ClayMpmCompute(computeDesc);
            _renderer = new ClayMpmRenderer(rendererDesc, computeDesc, _compute);

            _compute.Reset();
        }

        private void Update()
        {
            if (_tick)
            {
                if (_capture) FrameCapture.BeginCaptureToXcode();

                _compute.Tick();

                if (_capture)
                {
                    _capture = false;
                    FrameCapture.EndCapture();
                }

                _tick = false;
            }

            _renderer.Draw();
        }

        private void OnDestroy()
        {
            _compute.Dispose();
        }

        private void OnGUI()
        {
            if (!_tick && GUILayout.Button("Tick")) _tick = true;
            _capture = GUILayout.Toggle(_capture, "Capture");
        }
    }
}