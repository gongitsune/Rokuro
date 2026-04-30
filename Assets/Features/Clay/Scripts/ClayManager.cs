using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayManager : MonoBehaviour
    {
        [SerializeField] private ClayCompute.Desc clayComputeDesc;
        [SerializeField] private SdfReconstructor.Desc reconstructorDesc;
        [SerializeField] private ClayRenderer clayRenderer;
        [SerializeField] private Transform[] hands;

        private ClayCompute _clayCompute;
        private Vector4[] _handsPositions;

        public RenderTexture SDFTexture => _clayCompute.SDFTexture;
        public ClayCompute.Desc ClayComputeDesc => clayComputeDesc;

        private void Start()
        {
            _clayCompute = new ClayCompute();

            _clayCompute.Initialize(clayComputeDesc);
            clayRenderer.Initialize(this);

            _handsPositions = new Vector4[2];
        }

        private void Update()
        {
            for (var i = 0; i < _handsPositions.Length; i++)
                if (i < hands.Length)
                    _handsPositions[i] = hands[i].position - transform.position;
                else
                    _handsPositions[i] = Vector3.one * 100;

            _clayCompute.UpdateFingerPositions(_handsPositions);
            _clayCompute.Tick();
            clayRenderer.Tick();
        }

        private void OnDestroy()
        {
            _clayCompute.Dispose();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            _clayCompute?.OnDrawGizmos(transform.position);
        }
#endif
    }
}