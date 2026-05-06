using Sirenix.OdinInspector;
using Unity.Mathematics;
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
        private Vector4[] _handsDirections;
        private Vector4[] _handsPositions;

        [ShowInInspector] [ReadOnly] public float Angle { get; private set; }

        public RenderTexture SDFTexture => _clayCompute.SDFTexture;
        public ClayCompute.Desc ClayComputeDesc => clayComputeDesc;

        private void Start()
        {
            _clayCompute = new ClayCompute();

            _clayCompute.Initialize(clayComputeDesc);
            clayRenderer.Initialize(this);

            _handsPositions = new Vector4[2];
            _handsDirections = new Vector4[2];
            for (var i = 0; i < _handsPositions.Length; i++)
                _handsPositions[i] = Vector4.one * 100;
        }

        private void Update()
        {
            for (var i = 0; i < hands.Length; i++)
            {
                // 指の座標をSDFの逆回転方向に変換
                var localPos = hands[i].position - transform.position;
                var cosA = math.cos(-Angle);
                var sinA = math.sin(-Angle);
                var rotatedPos = new Vector3(
                    localPos.x * cosA - localPos.z * sinA,
                    localPos.y,
                    localPos.x * sinA + localPos.z * cosA
                );

                var forward = hands[i].forward;
                var rotatedForward = new Vector3(
                    forward.x * cosA - forward.z * sinA,
                    forward.y,
                    forward.x * sinA + forward.z * cosA
                );

                _handsPositions[i] = rotatedPos;
                _handsDirections[i] = rotatedForward;
            }

            Angle += clayComputeDesc.rotateSpeed * Time.deltaTime;

            _clayCompute.UpdateFingerPositions(_handsPositions, _handsDirections);
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