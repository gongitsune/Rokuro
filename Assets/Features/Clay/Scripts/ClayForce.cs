using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayForce
    {
        private readonly ObjectForce[] _activeForces;
        private readonly ClayRenderFeature _clayRenderFeature;
        private readonly Collider[] _colliderBuffer;
        private readonly Desc _desc;
        private readonly Transform _root;

        private int _activeForceCount;

        public ClayForce(Desc desc, Transform root, ClayRenderFeature renderFeature)
        {
            _desc = desc;
            _clayRenderFeature = renderFeature;
            _colliderBuffer = new Collider[_desc.maxObjectsDetected];
            _activeForces = new ObjectForce[_desc.maxObjectsDetected];
            _activeForceCount = 0;
            _root = root;
        }

        public void Update(Vector3 simOrigin, float simScale)
        {
            _activeForceCount = 0;
            var q = quaternion.RotateY(-_clayRenderFeature.AngleRad);

            // オブジェクトを検出
            var colliderCount = Physics.OverlapSphereNonAlloc(
                simOrigin + Vector3.one * (simScale * 0.5f),
                simScale * 0.75f,
                _colliderBuffer,
                _desc.detectionLayerMask,
                QueryTriggerInteraction.Ignore
            );

            // Rigidbodyを持つオブジェクトのみを処理
            for (var i = 0; i < colliderCount && _activeForceCount < _desc.maxObjectsDetected; i++)
            {
                var collider = _colliderBuffer[i];

                var point = math.mul(
                    q,
                    _root.worldToLocalMatrix.MultiplyPoint(collider.bounds.center) - new Vector3(0.5f, 0.5f, 0.5f)
                ) + 0.5f;

                _activeForces[_activeForceCount] = new ObjectForce
                {
                    Position = point,
                    Radius = _desc.influenceRadius,
                    Strength = _desc.pushStrength
                };
                _activeForceCount++;
            }
        }

        public int GetActiveForceCount()
        {
            return _activeForceCount;
        }

        public ObjectForce[] GetActiveForces()
        {
            return _activeForces;
        }

        // Gizmosで影響範囲を可視化
        public void DrawGizmos()
        {
            if (!_desc.showGizmos) return;

            var prevColor = Gizmos.color;

            var q = quaternion.RotateY(_clayRenderFeature.AngleRad);

            for (var i = 0; i < _activeForceCount; i++)
            {
                var f = _activeForces[i];
                var rotatedP = math.mul(q, f.Position - 0.5f) + 0.5f;

                var p = _root.localToWorldMatrix.MultiplyPoint(f.Position);
                var rp = _root.localToWorldMatrix.MultiplyPoint(rotatedP);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(rp, f.Radius);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(p, f.Radius);
            }

            Gizmos.color = prevColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ObjectForce
        {
            // メモリレイアウトをHLSLの struct object_force に合わせる
            public float3 Position;
            public float Radius;
            public float Strength;
        }

        [Serializable]
        public class Desc
        {
            [Title("Gizmos")] public bool showGizmos = true;

            [Title("Detection")] public LayerMask detectionLayerMask = 1 << 0;

            public int maxObjectsDetected = 8;

            [Title("Force Parameters")] [Range(0.01f, 1f)]
            public float influenceRadius = 0.1f;

            [Range(0f, 1000f)] public float pushStrength = 10f;
        }
    }
}