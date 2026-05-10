using System;
using System.Diagnostics.CodeAnalysis;

namespace Features.Clay.Scripts
{
    public class ClayRenderer
    {
        public ClayRenderer(Desc desc, ClayCompute compute)
        {
            if (desc.renderFeature) desc.renderFeature.Setup(compute);
        }

        [Serializable]
        public class Desc
        {
            public ClayRenderFeature renderFeature;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            x,
            scale
        }
    }
}