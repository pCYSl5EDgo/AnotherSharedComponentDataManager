using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

using random = System.Random;

namespace おう考えてやるからあくしろよテスト
{
    public sealed class ChangeSharedValueSystem : ComponentSystem
    {
        private ComponentGroup group;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private readonly ulong[] Array = new ulong[]{
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            ulong.MaxValue,
        };
        protected override void OnCreateManager() => group = GetComponentGroup(ComponentType.Create<TEST>());
        private int count;
        private long time;
        protected override void OnUpdate()
        {
            var manager = EntityManager;
            TEST t = default;
            var array = new NativeArray<Entity>(group.GetEntityArray().Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new CopyEntities
            {
                Source = group.GetEntityArray(),
                Results = array
            }.Schedule(array.Length, 16).Complete();
            stopwatch.Restart();
            for (int x = 0; x < 10; ++x)
            {
                for (int i = 0, j = 0; i < array.Length; i++, j += 3)
                {
                    if (j >= Array.Length)
                        j -= Array.Length;
                    t.Value = Array[j];
#if SHARED_1
                    manager.SetSharedComponentData(array[i], t);
#else
                    manager.SetSharedComponentData(array[i], ref t);
#endif
                }
            }
            stopwatch.Stop();
            array.Dispose();
            time += stopwatch.ElapsedMilliseconds;
            Debug.Log(stopwatch.ElapsedMilliseconds);
            if (count++ == 30)
            {
                count = 0;
                Debug.Log(time.ToString() + "/30");
                time = 0;
            }
        }
    }
}
/*
 SHARED_1
 14566/30
 16148/30

 !SHARED_1
 12103/30
 11901/30
 11910/30
 */