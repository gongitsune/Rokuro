using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayMpmRenderer
    {
        private readonly ClayMpmCompute.Desc _computeDesc;
        private readonly Desc _desc;

        public ClayMpmRenderer(Desc desc, ClayMpmCompute.Desc computeDesc, ClayMpmCompute compute)
        {
            _desc = desc;
            _computeDesc = computeDesc;

            var material = new MaterialWrapper<Uniforms>(desc.material);
            material.SetBuffer(Uniforms.x, compute.GetParticlePosBuffer());
        }

        public void Draw()
        {
            var rp = new RenderParams
            {
                material = _desc.material,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10)
            };
            Graphics.RenderPrimitives(in rp, MeshTopology.Points, _computeDesc.particleCount);
        }

        [Serializable]
        public class Desc
        {
            public Material material;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            x
        }
    }
}