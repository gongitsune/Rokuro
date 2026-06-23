using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class SdfConstructor : MonoBehaviour
    {
        [SerializeField] private int aabbPaddingCells = 1;

        [Title("Grid")] [SerializeField] private int gridRes = 96;
        [Title("Kernel")] [SerializeField] private int kernelRadius = 4;

        [Sirenix.OdinInspector.ReadOnly] [ShowInInspector]
        private Texture3D _debugSdfTexture;

        public async UniTask<Texture3D> ConstructSdf(GraphicsBuffer positionsBuffer)
        {
            var total = gridRes * gridRes * gridRes;
            var densityNative =
                new NativeArray<float>(total, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var densityTexture = new Texture3D(gridRes, gridRes, gridRes, TextureFormat.RFloat, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "Mpm Density"
            };

            var count = positionsBuffer.count;
            var particleNative =
                new NativeArray<float3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            await AsyncGPUReadback.RequestIntoNativeArrayAsync(ref particleNative, positionsBuffer);

            var job = new SdfConstructJob
            {
                Positions = particleNative,
                GridRes = gridRes,
                KernelRadius = kernelRadius / (float)gridRes,
                DensityGrid = densityNative
            };

            Debug.Log("[SdfConstructor] Constructing density field...");
            await job.Schedule();
            Debug.Log("[SdfConstructor] Density field construction completed.");

            densityTexture.SetPixelData(densityNative, 0);
            densityTexture.Apply(false, false);

            densityNative.Dispose();
            particleNative.Dispose();
            _debugSdfTexture = densityTexture;
            return densityTexture;
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
            }
        }
    }
}