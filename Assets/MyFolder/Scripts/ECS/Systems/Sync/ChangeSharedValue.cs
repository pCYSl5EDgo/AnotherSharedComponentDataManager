using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Collections;
using UnityEngine;

namespace おう考えてやるからあくしろよテスト
{
    public sealed class ChangeSharedValueSystem : ComponentSystem
    {
        private ComponentGroup group;
        private readonly ulong[] ValueArray;
        public ChangeSharedValueSystem() => this.ValueArray = System.Array.Empty<ulong>();
        public ChangeSharedValueSystem(ulong[] ValueArray) => this.ValueArray = ValueArray;
        protected override void OnCreateManager() => group = GetComponentGroup(ComponentType.Create<TEST>());
        private readonly ProfilerMarker profilerMarker = new ProfilerMarker("Set SharedComponent");
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
            using (profilerMarker.Auto())
            {
                for (int x = 0; x < 10; ++x)
                {
                    for (int i = 0, j = 0; i < array.Length; i++, j += 3)
                    {
                        if (j >= ValueArray.Length)
                            j -= ValueArray.Length;
                        t.Value = ValueArray[j];
                        manager.SetSharedComponentData(array[i], t);
                    }
                }
            }
            array.Dispose();
        }
    }
}