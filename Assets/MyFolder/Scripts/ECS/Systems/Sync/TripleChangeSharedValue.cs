using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Collections;
using UnityEngine;

namespace この名前に意味は特にないテスト
{
    public struct TEST0 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST0>
#endif
    {
        public ulong Value;
#if REF_EQUATABLE
        public bool Equals(ref TEST0 other) => Value == other.Value;
        public bool Equals(TEST0 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST0)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST1 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST1>
#endif
    {
        public ulong Value;
#if REF_EQUATABLE
        public bool Equals(ref TEST1 other) => Value == other.Value;
        public bool Equals(TEST1 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST1)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST2 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST2>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST2 other) => Value == other.Value;
        public bool Equals(TEST2 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST2)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public sealed class TripleChangeSharedValueSystem : ComponentSystem
    {
        private ComponentGroup group;
        private readonly ulong[] ValueArray;
        public TripleChangeSharedValueSystem() => this.ValueArray = System.Array.Empty<ulong>();
        public TripleChangeSharedValueSystem(ulong[] ValueArray) => this.ValueArray = ValueArray;
        protected override void OnCreateManager() => group = GetComponentGroup(ComponentType.Create<TEST0>(), ComponentType.Create<TEST1>(), ComponentType.Create<TEST2>());
        private readonly ProfilerMarker profilerMarker = new ProfilerMarker("Set SharedComponent");
        protected override void OnUpdate()
        {
            var manager = EntityManager;
            TEST0 t0 = default;
            TEST0 t1 = default;
            TEST0 t2 = default;
            var array = new NativeArray<Entity>(group.GetEntityArray().Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new CopyEntities
            {
                Source = group.GetEntityArray(),
                Results = array
            }.Schedule(array.Length, 16).Complete();
            using (profilerMarker.Auto())
            {
                for (int x = 0; x < 80; ++x)
                {
                    for (int i = 0, j = 0; i < array.Length; i++)
                    {
                        if (j >= ValueArray.Length)
                            j -= ValueArray.Length;
                        t0.Value = ValueArray[j++];
                        if (j >= ValueArray.Length)
                            j -= ValueArray.Length;
                        t1.Value = ValueArray[j++];
                        if (j >= ValueArray.Length)
                            j -= ValueArray.Length;
                        t2.Value = ValueArray[j++];
                        manager.SetSharedComponentData(array[i], t0);
                        manager.SetSharedComponentData(array[i], t1);
                        manager.SetSharedComponentData(array[i], t2);
                    }
                }
            }
            array.Dispose();
        }
    }
}