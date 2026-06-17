using System;
using System.Diagnostics.CodeAnalysis;
using Features.Utils.Scripts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Features.Clay.Scripts
{
    /// <summary>
    ///     Marching CubesでMPM粒子をメッシュ化
    /// </summary>
    public class ClayMc : IDisposable
    {
        private readonly ComputeShaderWrapper<Kernels, Uniforms> _compute;
        private readonly Desc _desc;

        public ClayMc(Desc desc)
        {
            _desc = desc;
            _compute = new ComputeShaderWrapper<Kernels, Uniforms>(desc.computeShader);

            AllocateBuffers();
            AllocateMesh(3 * desc.triangleBudget);
        }

        public void Dispose()
        {
            ReleaseBuffers();
            ReleaseMesh();
        }

        [Serializable]
        public class Desc
        {
            public int gridResolution = 96;
            public int triangleBudget = 65536;
            public float isoValue = 0.1f;
            public float scale = 1f / 96f * 2f;
            public ComputeShader computeShader;
        }

        #region Compute Shaders

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Kernels
        {
            mesh_reconstruction,
            clear_unused
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            n_grid,
            max_triangle,
            scale,
            iso_value,

            triangle_table,
            voxels,
            vertex_buffer,
            index_buffer,
            counter
        }

        private void BindBuffers()
        {
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.triangle_table, _triangleTable);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.voxels, _mcGridBuf);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.vertex_buffer, _vertexBuffer);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.index_buffer, _indexBuffer);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.counter, _counterBuffer);

            _compute.SetBuffer(Kernels.clear_unused, Uniforms.vertex_buffer, _vertexBuffer);
            _compute.SetBuffer(Kernels.clear_unused, Uniforms.index_buffer, _indexBuffer);
            _compute.SetBuffer(Kernels.clear_unused, Uniforms.counter, _counterBuffer);
        }

        public void ReconstructMesh()
        {
            _counterBuffer.SetCounterValue(0);

            _compute.SetInt(Uniforms.n_grid, _desc.gridResolution);
            _compute.SetInt(Uniforms.max_triangle, _desc.triangleBudget);
            _compute.SetFloat(Uniforms.scale, _desc.scale);
            _compute.SetFloat(Uniforms.iso_value, _desc.isoValue);
            BindBuffers();

            // Isosurface reconstruction
            _compute.Dispatch(Kernels.mesh_reconstruction, new uint3(_desc.gridResolution));

            // Clear unused area of the buffers.
            _compute.Dispatch(Kernels.clear_unused, new uint3(1024, 1, 1));

            // Bounding box
            var ext = new float3(_desc.gridResolution) * _desc.scale;
            Mesh.bounds = new Bounds(Vector3.zero, ext);
        }

        #endregion

        #region Compute Buffer Objects

        private GraphicsBuffer _mcGridBuf, _triangleTable, _counterBuffer;
        private bool _enableUnifiedMcGrid;

        public void SetupMcGridForMpmCompute(
            ComputeShaderWrapper<ClayCompute.Kernels, ClayCompute.Uniforms> computeShader,
            GraphicsBuffer mpmGridBuf,
            int mpmGridRes
        )
        {
            if (_desc.gridResolution == mpmGridRes)
            {
                Debug.Log("[ClayMc] unified mc grid feature enabled.");
                computeShader.EnableKeyword("UNIFIED_MC_GRID");
                _enableUnifiedMcGrid = true;
                _mcGridBuf = mpmGridBuf; // 同一のグリッドバッファを使用する
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
            computeShader.SetFloat(ClayCompute.Uniforms.inv_mc_dx, 1f / _desc.gridResolution);
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
            if (!_enableUnifiedMcGrid) _mcGridBuf.Release();
        }

        #endregion

        #region Mesh Objects

        public Mesh Mesh { get; private set; }
        private GraphicsBuffer _vertexBuffer, _indexBuffer;

        private void AllocateMesh(int vertexCount)
        {
            Mesh = new Mesh
            {
                name = "Clay Marching Cubes Mesh"
            };

            // We want GraphicsBuffer access as Raw (ByteAddress) buffers.
            Mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            Mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            // Vertex position: float32 x 3
            var vp = new VertexAttributeDescriptor(VertexAttribute.Position);

            // Vertex normal: float32 x 3
            var vn = new VertexAttributeDescriptor(VertexAttribute.Normal);

            // Vertex/index buffer formats
            Mesh.SetVertexBufferParams(vertexCount, vp, vn);
            Mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);

            // Submesh initialization
            Mesh.subMeshCount = 1;
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);

            // GraphicsBuffer references
            _vertexBuffer = Mesh.GetVertexBuffer(0);
            _indexBuffer = Mesh.GetIndexBuffer();
        }

        private void ReleaseMesh()
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            Object.Destroy(Mesh);
        }

        #endregion
    }
}