using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayManager : MonoBehaviour
    {
        [SerializeField] private ClayCompute.Desc clayComputeDesc;
        [SerializeField] private ClayRenderer clayRenderer;

        private ClayCompute _clayCompute;
        public RenderTexture SDFTexture => _clayCompute.SDFTexture;
        public ClayCompute.Desc ClayComputeDesc => clayComputeDesc;

        private void Start()
        {
            _clayCompute = new ClayCompute();
            _clayCompute.Initialize(clayComputeDesc);
            clayRenderer.Initialize(this);
        }

        private void OnDestroy()
        {
            _clayCompute.Dispose();
        }
    }
}