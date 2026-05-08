using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayRenderer
    {
        private readonly ClayCompute.Desc _computeDesc;
        private readonly Desc _desc;
        private readonly MaterialWrapper<Uniforms> _material;

        public ClayRenderer(Desc desc, ClayCompute.Desc computeDesc, ClayCompute compute)
        {
            _desc = desc;
            _computeDesc = computeDesc;

            _material = new MaterialWrapper<Uniforms>(desc.material);
            _material.SetBuffer(Uniforms.x, compute.GetParticlePosBuffer());
        }

        public void Draw()
        {
            var scale = _material.GetFloat(Uniforms.scale);
            var rp = new RenderParams
            {
                material = _desc.material,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * scale)
            };
            Graphics.RenderPrimitives(in rp, MeshTopology.Triangles, _computeDesc.particleCount * 6);
        }

        public void OnDrawGizmos()
        {
            var scale = _material.GetFloat(Uniforms.scale);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.one * scale * 0.5f, Vector3.one * scale);
        }

        [Serializable]
        public class Desc
        {
            public Material material;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            x,
            scale
        }
    }
}