using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Features.Clay.Scripts
{
    public class ClayRenderer
    {
        private readonly GraphicsBuffer _particlePosBuffer;

        public ClayRenderer(Desc desc, ClayCompute compute)
        {
            desc.renderFeature.Setup(compute.GetParticlePosBuffer()).Forget();
        }

        [Serializable]
        public class Desc
        {
            public ClayRenderFeature renderFeature;
        }
    }
}