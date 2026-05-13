using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayGridVelRenderer
    {
        private readonly Desc _desc;
        private readonly GraphicsBuffer _gridVBuf;
        private readonly MaterialWrapper<Uniforms> _material;
        private readonly Mesh _pyramidMesh = CreatePyramidMesh();

        public ClayGridVelRenderer(Desc desc, ClayCompute compute)
        {
            _desc = desc;
            _gridVBuf = compute.GetGridVelBuffer();
            _material = new MaterialWrapper<Uniforms>(desc.material);

            _material.SetBuffer(Uniforms.grid_v, _gridVBuf);
            _material.SetInt(Uniforms.n_grid, compute.GridResolution);
        }

        public void Draw()
        {
            var scale = _material.GetFloat(Uniforms.scale);
            var rp = new RenderParams(_desc.material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * scale)
            };
            Graphics.RenderMeshPrimitives(in rp, _pyramidMesh, 0, _gridVBuf.count / 3);
        }

        private static Mesh CreatePyramidMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-0.5f, 0.0f, -0.5f),
                    new Vector3(0.5f, 0.0f, -0.5f),
                    new Vector3(0.5f, 0.0f, 0.5f),
                    new Vector3(-0.5f, 0.0f, 0.5f),
                    new Vector3(0.0f, 0.5f, 0.0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f)
                }
            };
            var indices = new[]
            {
                0, 1, 2, 0, 2, 3,
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4
            };
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            return mesh;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            grid_v,
            scale,
            n_grid
        }

        [Serializable]
        public class Desc
        {
            public Material material;
        }
    }
}