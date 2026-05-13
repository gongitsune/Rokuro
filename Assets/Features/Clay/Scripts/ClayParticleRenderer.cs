using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayParticleRenderer
    {
        private readonly Desc _desc;
        private readonly MaterialWrapper<Uniforms> _material;
        private readonly int _numParticles;
        private readonly Mesh _quadMesh;
        private readonly Transform _transform;

        public ClayParticleRenderer(Desc desc, ClayCompute compute, Transform transform)
        {
            _desc = desc;
            _transform = transform;
            _material = new MaterialWrapper<Uniforms>(desc.material);
            _quadMesh = CreateQuadMesh();
            var particlePosBuffer = compute.GetParticlePosBuffer();
            _numParticles = particlePosBuffer.count;

            _material.SetBuffer(Uniforms.particle_pos, particlePosBuffer);
        }

        public void Draw()
        {
            var rootPos = _transform.position;
            var scale = _material.GetFloat(Uniforms.scale);
            _desc.material.SetPass(0);
            var rp = new RenderParams(_desc.material)
            {
                worldBounds = new Bounds(rootPos, Vector3.one * scale)
            };
            Graphics.RenderMeshPrimitives(in rp, _quadMesh, 0, _numParticles);
        }

        public void OnDrawGizmos()
        {
            var rootPos = _transform.position;
            var scale = _material.GetFloat(Uniforms.scale);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(rootPos + Vector3.one * scale * 0.5f, Vector3.one * scale);
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, 0.5f, 0.0f),
                    new Vector3(-0.5f, -0.5f, 0.0f),
                    new Vector3(0.5f, -0.5f, 0.0f),
                    new Vector3(0.5f, 0.5f, 0.0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f)
                }
            };
            mesh.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0);
            return mesh;
        }

        [Serializable]
        public class Desc
        {
            public Material material;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            scale,
            particle_pos
        }
    }
}