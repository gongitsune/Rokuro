using System;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Features.Clay.Scripts
{
    public class SDFReinitJobSystem : IDisposable
    {
        private UniTask _reinitTask;
        private int _resolution;
        private RenderTexture _sdf;
        private NativeArray<half> _sdfArray;
        private Texture3D _tempTexture;

        public void Dispose()
        {
            if (_sdfArray.IsCreated) _sdfArray.Dispose();
        }

        public void Initialize(ClayCompute clayCompute, ClayCompute.Desc clayDesc)
        {
            _sdf = clayCompute.SDFTexture;
            _resolution = clayDesc.resolution;
            _sdfArray = new NativeArray<half>(_resolution * _resolution * _resolution, Allocator.Persistent);
            _tempTexture = new Texture3D(
                _resolution, _resolution, _resolution,
                TextureFormat.RHalf, false, true
            );
        }

        public void Tick()
        {
            if (_reinitTask.Status == UniTaskStatus.Pending) return;

            _reinitTask = UniTask.Create(async () =>
            {
                Debug.Log("Reinitializing...");
                await AsyncGPUReadback.RequestIntoNativeArrayAsync(ref _sdfArray, _sdf);
                Debug.Log("Readback complete");
                await Reinitialize(_sdfArray, _resolution);
                Debug.Log("Reinitialization complete");

                _tempTexture.SetPixelData(_sdfArray, 0);
                _tempTexture.Apply();
                Graphics.CopyTexture(_tempTexture, _sdf);
                Debug.Log("SDF reinitialized");
            });
        }

        private static JobHandle Reinitialize(NativeArray<half> sdf, int3 size)
        {
            var total = size.x * size.y * size.z;

            // シードの距離初期値バッファ
            var seedDist = new NativeArray<float>(
                total, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory
            );

            // visited フラグ
            var visited = new NativeArray<bool>(total, Allocator.TempJob);

            // ① シード抽出（並列）
            var extractJob = new ExtractSeedsJob
            {
                SDF = sdf,
                SeedDist = seedDist,
                GridSize = size
            };
            var h1 = extractJob.Schedule(total, 64);

            // ② FMM（シングルスレッド）
            var fmmJob = new FmmJob
            {
                SeedDist = seedDist,
                Sdf = sdf,
                Visited = visited,
                GridSize = size
            };
            var h2 = fmmJob.Schedule(h1);

            // ③ 符号付け（並列）
            var signJob = new ApplySignJob
            {
                SDF = sdf
            };
            var h3 = signJob.Schedule(total, 64, h2);

            // TempJob の解放
            var disposeJob = new DisposeJob<float>(seedDist)
                .Schedule(h3);
            disposeJob = new DisposeJob<bool>(visited)
                .Schedule(disposeJob);

            return disposeJob;
        }
    }

    // ─────────────────────────────────────────
    // ① シード抽出 (並列)
    // ─────────────────────────────────────────
    internal struct ExtractSeedsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<half> SDF;
        [WriteOnly] public NativeArray<float> SeedDist;
        public int3 GridSize;

        private static readonly int3[] Offsets =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1)
        };

        public void Execute(int i)
        {
            var p = IndexToCoord(i, GridSize);
            var v = SDF[i];
            var minDist = float.MaxValue;

            foreach (var off in Offsets)
            {
                var n = p + off;
                if (math.any(n < 0) || math.any(n >= GridSize)) continue;

                var nv = SDF[CoordToIndex(n, GridSize)];
                if (v * nv >= 0f) continue;
                var t = v / (v - nv);
                minDist = math.min(minDist, t);
            }

            SeedDist[i] = minDist; // MaxValueなら非シード
        }

        private static int CoordToIndex(int3 p, int3 size)
        {
            return p.x + size.x * (p.y + size.y * p.z);
        }

        private static int3 IndexToCoord(int i, int3 size)
        {
            var z = i / (size.x * size.y);
            var y = i % (size.x * size.y) / size.x;
            var x = i % size.x;
            return new int3(x, y, z);
        }
    }

    // ─────────────────────────────────────────
    // ② FMM (シングルスレッド)
    // ─────────────────────────────────────────
    internal struct FmmJob : IJob
    {
        [ReadOnly] public NativeArray<float> SeedDist;
        public NativeArray<half> Sdf;
        public NativeArray<bool> Visited;
        public int3 GridSize;

        private static readonly int3[] Offsets =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1)
        };

        public void Execute()
        {
            var total = GridSize.x * GridSize.y * GridSize.z;

            // 初期化
            for (var i = 0; i < total; i++)
                Sdf[i] = (half)half.MaxValue;

            // 優先度キュー（MinHeap）
            // NativeArray ベースの簡易実装
            var heap = new NativeList<float2>(Allocator.Temp); // xy = dist, index

            // シードをキューに積む
            for (var i = 0; i < total; i++)
                if (SeedDist[i] < float.MaxValue)
                {
                    Sdf[i] = (half)SeedDist[i];
                    HeapPush(ref heap, new float2(SeedDist[i], i));
                }

            // FMM メインループ
            while (heap.Length > 0)
            {
                var top = HeapPop(ref heap);
                var ci = (int)top.y;
                if (Visited[ci]) continue;
                Visited[ci] = true;

                var cp = IndexToCoord(ci, GridSize);

                foreach (var off in Offsets)
                {
                    var np = cp + off;
                    if (math.any(np < 0) || math.any(np >= GridSize)) continue;

                    var ni = CoordToIndex(np, GridSize);
                    if (Visited[ni]) continue;

                    var newDist = SolveEikonal(np);
                    if (newDist >= Sdf[ni]) continue;
                    Sdf[ni] = (half)newDist;
                    HeapPush(ref heap, new float2(newDist, ni));
                }
            }

            heap.Dispose();
        }

        private float SolveEikonal(int3 p)
        {
            var a = AxisMin(p, new int3(1, 0, 0));
            var b = AxisMin(p, new int3(0, 1, 0));
            var c = AxisMin(p, new int3(0, 0, 1));

            // 昇順ソート
            if (a > b) (a, b) = (b, a);
            if (b > c) (b, c) = (c, b);
            if (a > b) (a, b) = (b, a);

            var d = a + 1f;
            if (d <= b) return d;

            d = 0.5f * (a + b + math.sqrt(2f - (a - b) * (a - b)));
            if (d <= c) return d;

            var sum = a + b + c;
            var sum2 = a * a + b * b + c * c;
            var disc = math.max(0f, sum * sum - 3f * (sum2 - 1f));
            return (sum + math.sqrt(disc)) / 3f;
        }

        private float AxisMin(int3 p, int3 axis)
        {
            var val = float.MaxValue;
            foreach (var s in new[] { -1, 1 })
            {
                var n = p + axis * s;
                if (math.any(n < 0) || math.any(n >= GridSize)) continue;
                var ni = CoordToIndex(n, GridSize);
                if (Visited[ni])
                    val = math.min(val, Sdf[ni]);
            }

            return val;
        }

        private static int CoordToIndex(int3 p, int3 size)
        {
            return p.x + size.x * (p.y + size.y * p.z);
        }

        private static int3 IndexToCoord(int i, int3 size)
        {
            var z = i / (size.x * size.y);
            var y = i % (size.x * size.y) / size.x;
            var x = i % size.x;
            return new int3(x, y, z);
        }

        // 簡易 MinHeap
        private static void HeapPush(ref NativeList<float2> heap, float2 val)
        {
            heap.Add(val);
            var i = heap.Length - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (heap[parent].x <= heap[i].x) break;
                (heap[i], heap[parent]) = (heap[parent], heap[i]);
                i = parent;
            }
        }

        private static float2 HeapPop(ref NativeList<float2> heap)
        {
            var top = heap[0];
            var last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            var i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                if (l < heap.Length && heap[l].x < heap[smallest].x) smallest = l;
                if (r < heap.Length && heap[r].x < heap[smallest].x) smallest = r;
                if (smallest == i) break;
                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }

            return top;
        }
    }

    // ─────────────────────────────────────────
    // ③ 符号付け (並列)
    // ─────────────────────────────────────────
    internal struct ApplySignJob : IJobParallelFor
    {
        public NativeArray<half> SDF;

        public void Execute(int i)
        {
            var sgn = SDF[i] >= 0f ? 1f : -1f;
            SDF[i] = SDF[i] *= (half)sgn;
        }
    }

    // ─────────────────────────────────────────
    // TempJob解放用ヘルパー
    // ─────────────────────────────────────────
    internal struct DisposeJob<T> : IJob where T : struct
    {
        [DeallocateOnJobCompletion] private NativeArray<T> _array;

        public DisposeJob(NativeArray<T> a)
        {
            _array = a;
        }

        public void Execute()
        {
        }
    }
}