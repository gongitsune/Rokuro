using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayRenderer
    {
        private readonly GraphicsBuffer _particlePosBuffer;

        public ClayRenderer(Desc desc, ClayCompute compute, Transform root)
        {
            desc.renderFeature.Setup(compute.GetParticlePosBuffer(), root).Forget();
        }

        [Serializable]
        public class Desc
        {
            public ClayRenderFeature renderFeature;
        }
    }
}