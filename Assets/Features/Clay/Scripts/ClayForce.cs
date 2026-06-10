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
        private readonly Collider[] _colliderBuffer;
        private readonly Desc _desc;
        private readonly Transform _root;

        private int _activeForceCount;

        public ClayForce(Desc desc, Transform root)
        {
            _desc = desc;
            _colliderBuffer = new Collider[_desc.maxObjectsDetected];
            _activeForces = new ObjectForce[_desc.maxObjectsDetected];
            _activeForceCount = 0;
            _root = root;
        }

        public void Update(Vector3 simOrigin, float simScale)
        {
            _activeForceCount = 0;

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

                // attachedRigidbodyプロパティを使用（GetComponentより高速）
                var rb = collider.attachedRigidbody;
                if (!rb || !rb.isKinematic) continue;

                var point = _root.worldToLocalMatrix.MultiplyPoint(rb.position);

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
        public void DrawGizmos(Vector3 simOrigin, float simScale)
        {
            if (!_desc.showGizmos) return;

            var prevColor = Gizmos.color;
            Gizmos.color = _desc.gizmoColor;

            for (var i = 0; i < _activeForceCount; i++)
            {
                var f = _activeForces[i];
                var p = _root.localToWorldMatrix.MultiplyPoint(f.Position);
                Gizmos.DrawWireSphere(p, f.Radius);
                Gizmos.DrawSphere(p, math.min(f.Radius * 0.05f, 0.02f));
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
            private float3 _padding;
        }

        [Serializable]
        public class Desc
        {
            [Title("Gizmos")] public bool showGizmos = true;
            public Color gizmoColor = Color.yellow;

            [Title("Detection")] public LayerMask detectionLayerMask = 1 << 0;

            public int maxObjectsDetected = 8;

            [Title("Force Parameters")] [Range(0.01f, 1f)]
            public float influenceRadius = 0.1f;

            [Range(0f, 100f)] public float pushStrength = 10f;
        }
    }
}