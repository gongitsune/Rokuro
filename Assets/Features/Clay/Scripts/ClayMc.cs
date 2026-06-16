using System;
using Features.Utils.Scripts;
using UnityEngine;

namespace Features.Clay.Scripts
{
    /// <summary>
    ///     Marching CubesでMPM粒子をメッシュ化
    /// </summary>
    public class ClayMc
    {
        private readonly Desc _desc;

        public ClayMc(Desc desc)
        {
            _desc = desc;
        }

        [Serializable]
        public class Desc
        {
            public int gridResolution = 96;
        }

        #region Compute Buffer Objects

        private GraphicsBuffer _mcGridBuf, _triangleTable, _counterBuffer;

        public void SetupMcBuffers(
            ComputeShaderWrapper<ClayCompute.Kernels, ClayCompute.Uniforms> computeShader,
            int mpmGridRes
        )
        {
            if (_desc.gridResolution == mpmGridRes)
            {
                Debug.Log("[ClayMc] unified mc grid feature enabled.");
                computeShader.EnableKeyword("UNIFIED_MC_GRID");
                return;
            }

            _mcGridBuf = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                _desc.gridResolution * _desc.gridResolution * _desc.gridResolution,
                sizeof(int)
            );

            Debug.Log("[ClayMc] unified mc grid feature disabled. Using separate mc grid buffer.");
            computeShader.DisableKeyword("UNIFIED_MC_GRID");
            computeShader.SetInt(ClayCompute.Uniforms.n_mc_grid, _desc.gridResolution);
            computeShader.SetFloat(ClayCompute.Uniforms.inv_mc_dx, _desc.gridResolution / (float)mpmGridRes);
            computeShader.SetBuffer(ClayCompute.Kernels.clear_grid, ClayCompute.Uniforms.mc_grid, _mcGridBuf);
            computeShader.SetBuffer(ClayCompute.Kernels.particle_to_grid, ClayCompute.Uniforms.mc_grid, _mcGridBuf);
        }

        private void AllocateBuffers()
        {
            _triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 256, sizeof(ulong));
            _triangleTable.SetData(TriangleTable.Table);

            _counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Counter, 1, sizeof(int));
        }

        private void ReleaseBuffers()
        {
            _triangleTable.Release();
            _counterBuffer.Release();
            _mcGridBuf?.Release();
        }

        #endregion
    }
}