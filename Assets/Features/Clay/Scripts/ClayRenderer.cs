using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using Unity.Mathematics;
using UnityEngine;

namespace Features.Clay.Scripts
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ClayRenderer : MonoBehaviour
    {
        [SerializeField] private Material clayMaterial;
        private ClayManager _clayManager;

        private MaterialWrapper<Uniforms> _material;

        public void Initialize(ClayManager clayManager)
        {
            _clayManager = clayManager;
            _material = new MaterialWrapper<Uniforms>(clayMaterial);

            if (TryGetComponent(out MeshFilter meshFilter))
                meshFilter.mesh = CreateBoundingCube();
            if (TryGetComponent(out MeshRenderer meshRenderer))
                meshRenderer.material = clayMaterial;

            var desc = clayManager.ClayComputeDesc;
            _material.SetTexture(Uniforms._sdf_tex, clayManager.SDFTexture);
            _material.SetInt(Uniforms.resolution, desc.resolution);
            _material.SetVector(Uniforms.bounds_min, new float4(desc.boundsMin, 0));
            _material.SetVector(Uniforms.bounds_max, new float4(desc.boundsMax, 0));
        }

        private Mesh CreateBoundingCube()
        {
            var desc = _clayManager.ClayComputeDesc;
            var min = desc.boundsMin;
            var max = desc.boundsMax;

            Vector3[] verts =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z),
                new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z),
                new(max.x, max.y, max.z), new(min.x, max.y, max.z)
            };

            int[] tris =
            {
                0, 2, 1, 0, 3, 2, // 前
                1, 2, 6, 1, 6, 5, // 右
                5, 6, 7, 5, 7, 4, // 後
                4, 7, 3, 4, 3, 0, // 左
                3, 7, 6, 3, 6, 2, // 上
                4, 0, 1, 4, 1, 5 // 下
            };

            var mesh = new Mesh
            {
                vertices = verts,
                triangles = tris
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            _sdf_tex,
            resolution,
            bounds_min,
            bounds_max
        }
    }
}