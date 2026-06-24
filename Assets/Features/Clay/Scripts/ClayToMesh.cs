using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Features.Utils.Scripts;
using Sirenix.OdinInspector;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class ClayToMesh : MonoBehaviour
    {
        [Title("Grid")] [SerializeField] private int gridRes = 96;

        [Title("Density Field")] [SerializeField]
        private int kernelRadius = 4;

        [Title("Marching Cubes")] [SerializeField]
        private int triangleBudget = 65536;

        [SerializeField] private float isoValueScale = 0.1f;
        [SerializeField] private float mcScale = 1f / 96f * 2f;
        [SerializeField] private ComputeShader computeShader;

        private float _isoValue;

        private void Start()
        {
            _compute = new ComputeShaderWrapper<Kernels, Uniforms>(computeShader);
        }

        private void OnDestroy()
        {
            ReleaseMesh();
        }

        private static void Log(string msg)
        {
            Debug.Log($"[ClayToMesh] {msg}");
        }

        public async UniTask BuildMesh(GraphicsBuffer positionsBuffer)
        {
            Log("Calculating density field...");
            var total = gridRes * gridRes * gridRes;
            var densityNative =
                new NativeArray<float>(total, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var maxDensity = await CalcDensityField(positionsBuffer, densityNative);
            _isoValue = maxDensity * isoValueScale;
            Log("Calculated density field.");

            AllocateBuffers(in densityNative);
            densityNative.Dispose();

            if (!_mesh) AllocateMesh(3 * triangleBudget);

            ConstructMesh();
            await UniTask.DelayFrame(5); // ComputeShader実行を待機

            ReleaseBuffers();
        }

        #region Density Field

        private async UniTask<float> CalcDensityField(GraphicsBuffer positionsBuffer, NativeArray<float> densityNative)
        {
            var count = positionsBuffer.count;
            var particleNative =
                new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var maxDensity = new NativeReference<float>(0, Allocator.Persistent);

            await AsyncGPUReadback.RequestIntoNativeArrayAsync(ref particleNative, positionsBuffer);

            var job = new SdfConstructJob
            {
                Positions = particleNative,
                GridRes = gridRes,
                KernelRadius = kernelRadius / (float)gridRes,
                DensityGrid = densityNative,
                MaxDensity = maxDensity
            };

            await job.Schedule();

            var maxDensityValue = maxDensity.Value;

            particleNative.Dispose();
            maxDensity.Dispose();

            return maxDensityValue;
        }

        /// <summary>
        ///     MPMの流体からSDFを生成するJob
        /// </summary>
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct SdfConstructJob : IJob
        {
            #region Input / Output

            /// <summary>
            ///     粒子座標
            /// </summary>
            [Unity.Collections.ReadOnly] public NativeArray<float3> Positions;

            /// <summary>
            ///     グリッド解像度
            /// </summary>
            [Unity.Collections.ReadOnly] public int3 GridRes;

            /// <summary>
            ///     カーネル半径
            /// </summary>
            [Unity.Collections.ReadOnly] public float KernelRadius;

            public NativeArray<float> DensityGrid;
            public NativeReference<float> MaxDensity;

            #endregion

            public void Execute()
            {
                var nx = GridRes.x;
                var ny = GridRes.y;
                var nz = GridRes.z;

                var cellSizeX = 1f / nx;
                var cellSizeY = 1f / ny;
                var cellSizeZ = 1f / nz;

                // 初期化
                for (var i = 0; i < DensityGrid.Length; i++)
                    DensityGrid[i] = 0f;

                var h = KernelRadius;
                var h2 = h * h;

                foreach (var pos in Positions)
                {
                    // カーネル半径で AABB を切る
                    var xMin = math.max(0, (int)math.floor((pos.x - h) * nx));
                    var xMax = math.min(nx - 1, (int)math.ceil((pos.x + h) * nx));
                    var yMin = math.max(0, (int)math.floor((pos.y - h) * ny));
                    var yMax = math.min(ny - 1, (int)math.ceil((pos.y + h) * ny));
                    var zMin = math.max(0, (int)math.floor((pos.z - h) * nz));
                    var zMax = math.min(nz - 1, (int)math.ceil((pos.z + h) * nz));

                    for (var zi = zMin; zi <= zMax; zi++)
                    for (var yi = yMin; yi <= yMax; yi++)
                    for (var xi = xMin; xi <= xMax; xi++)
                    {
                        var cellPos = new float3(
                            (xi + 0.5f) * cellSizeX,
                            (yi + 0.5f) * cellSizeY,
                            (zi + 0.5f) * cellSizeZ
                        );

                        var r2 = math.lengthsq(cellPos - pos);
                        if (r2 >= h2) continue;

                        // poly6 カーネル: (1 - r²/h²)³
                        var q = 1f - r2 / h2;

                        DensityGrid[xi + yi * nx + zi * nx * ny] += q * q * q;
                    }
                }

                MaxDensity.Value = 0f;
                foreach (var f in DensityGrid)
                    if (f > MaxDensity.Value)
                        MaxDensity.Value = f;
            }
        }

        #endregion

        #region Marching Cubes Compute

        private ComputeShaderWrapper<Kernels, Uniforms> _compute;

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

        public void ConstructMesh()
        {
            _counterBuffer.SetCounterValue(0);

            _compute.SetInt(Uniforms.n_grid, gridRes);
            _compute.SetInt(Uniforms.max_triangle, triangleBudget);
            _compute.SetFloat(Uniforms.scale, mcScale);
            _compute.SetFloat(Uniforms.iso_value, _isoValue);

            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.triangle_table, _triangleTable);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.voxels, _mcGridBuf);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.vertex_buffer, _vertexBuffer);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.index_buffer, _indexBuffer);
            _compute.SetBuffer(Kernels.mesh_reconstruction, Uniforms.counter, _counterBuffer);

            _compute.SetBuffer(Kernels.clear_unused, Uniforms.vertex_buffer, _vertexBuffer);
            _compute.SetBuffer(Kernels.clear_unused, Uniforms.index_buffer, _indexBuffer);
            _compute.SetBuffer(Kernels.clear_unused, Uniforms.counter, _counterBuffer);

            // Isosurface reconstruction
            _compute.Dispatch(Kernels.mesh_reconstruction, new uint3(gridRes));

            // Clear unused area of the buffers.
            _compute.Dispatch(Kernels.clear_unused, new uint3(1024, 1, 1));

            // Bounding box
            var ext = new float3(gridRes) * mcScale;
            _mesh.bounds = new Bounds(Vector3.zero, ext);
        }

        #endregion

        #region Marching Cubes Compute Buffer Objects

        private GraphicsBuffer _mcGridBuf, _triangleTable, _counterBuffer;

        private void AllocateBuffers(in NativeArray<float> gridBuf)
        {
            _mcGridBuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridBuf.Length, sizeof(float));
            _mcGridBuf.SetData(gridBuf);

            _triangleTable = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 256, sizeof(ulong));
            _triangleTable.SetData(TriangleTable.Table);

            _counterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Counter, 1, sizeof(int));
        }

        private void ReleaseBuffers()
        {
            _mcGridBuf?.Release();
            _triangleTable?.Release();
            _counterBuffer?.Release();
        }

        #endregion

        #region Mesh Objects

        [ShowInInspector] private Mesh _mesh;
        private GraphicsBuffer _vertexBuffer, _indexBuffer;

        private void AllocateMesh(int vertexCount)
        {
            _mesh = new Mesh
            {
                name = "Clay Marching Cubes Mesh"
            };

            // We want GraphicsBuffer access as Raw (ByteAddress) buffers.
            _mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            // Vertex position: float32 x 3
            var vp = new VertexAttributeDescriptor(VertexAttribute.Position);

            // Vertex normal: float32 x 3
            var vn = new VertexAttributeDescriptor(VertexAttribute.Normal);

            // Vertex/index buffer formats
            _mesh.SetVertexBufferParams(vertexCount, vp, vn);
            _mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);

            // Submesh initialization
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount), MeshUpdateFlags.DontRecalculateBounds);

            // GraphicsBuffer references
            _vertexBuffer = _mesh.GetVertexBuffer(0);
            _indexBuffer = _mesh.GetIndexBuffer();
        }

        private void ReleaseMesh()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            if (_mesh) Destroy(_mesh);
        }

        #endregion
    }
}