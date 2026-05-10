using System;

namespace Features.Clay.Scripts
{
    public class ClayRenderer
    {
        public ClayRenderer(Desc desc, ClayCompute compute)
        {
            desc.renderFeature.Setup(compute.GetParticlePosBuffer());
        }

        [Serializable]
        public class Desc
        {
            public ClayRenderFeature renderFeature;
        }
    }
}