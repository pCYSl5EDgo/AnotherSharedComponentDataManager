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
    public struct TEST3 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST3>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST3 other) => Value == other.Value;
        public bool Equals(TEST3 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST3)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST4 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST4>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST4 other) => Value == other.Value;
        public bool Equals(TEST4 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST4)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST5 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST5>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST5 other) => Value == other.Value;
        public bool Equals(TEST5 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST5)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST6 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST6>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST6 other) => Value == other.Value;
        public bool Equals(TEST6 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST6)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST7 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST7>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST7 other) => Value == other.Value;
        public bool Equals(TEST7 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST7)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST8 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST8>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST8 other) => Value == other.Value;
        public bool Equals(TEST8 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST8)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST9 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST9>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST9 other) => Value == other.Value;
        public bool Equals(TEST9 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST9)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST10 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST10>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST10 other) => Value == other.Value;
        public bool Equals(TEST10 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST10)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST11 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST11>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST11 other) => Value == other.Value;
        public bool Equals(TEST11 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST11)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST12 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST12>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST12 other) => Value == other.Value;
        public bool Equals(TEST12 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST12)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST13 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST13>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST13 other) => Value == other.Value;
        public bool Equals(TEST13 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST13)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST14 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST14>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST14 other) => Value == other.Value;
        public bool Equals(TEST14 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST14)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public struct TEST15 : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST15>
#endif
    {
        public ulong Value;

#if REF_EQUATABLE
        public bool Equals(ref TEST15 other) => Value == other.Value;
        public bool Equals(TEST15 other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST15)obj).Equals(ref this);
        public override int GetHashCode() => (int)Value;
#endif
    }
    public sealed class ManyChangeSharedValueSystem : ComponentSystem
    {
        private ComponentGroup group;
        private readonly ulong[] ValueArray;
        public ManyChangeSharedValueSystem() => this.ValueArray = System.Array.Empty<ulong>();
        public ManyChangeSharedValueSystem(ulong[] ValueArray) => this.ValueArray = ValueArray;
        protected override void OnCreateManager() => group = GetComponentGroup(ComponentType.Create<TEST0>(), ComponentType.Create<TEST1>(), ComponentType.Create<TEST2>(), ComponentType.Create<TEST3>(), ComponentType.Create<TEST4>(), ComponentType.Create<TEST5>(), ComponentType.Create<TEST6>(), ComponentType.Create<TEST7>(), ComponentType.Create<TEST8>(), ComponentType.Create<TEST9>(), ComponentType.Create<TEST10>(), ComponentType.Create<TEST11>(), ComponentType.Create<TEST12>(), ComponentType.Create<TEST13>(), ComponentType.Create<TEST14>(), ComponentType.Create<TEST15>());
        private readonly ProfilerMarker profilerMarker = new ProfilerMarker("Set SharedComponent");
        protected override void OnUpdate()
        {
            var manager = EntityManager;
            TEST0 t0 = default;
            TEST1 t1 = default;
            TEST2 t2 = default;
            TEST3 t3 = default;
            TEST4 t4 = default;
            TEST5 t5 = default;
            TEST6 t6 = default;
            TEST7 t7 = default;
            TEST8 t8 = default;
            TEST9 t9 = default;
            TEST10 t10 = default;
            TEST11 t11 = default;
            TEST12 t12 = default;
            TEST13 t13 = default;
            TEST14 t14 = default;
            TEST15 t15 = default;
            var entities = new NativeArray<Entity>(group.GetEntityArray().Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new CopyEntities
            {
                Source = group.GetEntityArray(),
                Results = entities
            }.Schedule(entities.Length, 16).Complete();
            using (profilerMarker.Auto())
            {
                for (int x = 0; x < 10; ++x)
                {
                    for (int i = 0, j = 0; i < entities.Length; i++)
                    {
                        if (j == ValueArray.Length)
                            j = 0;
                        t0.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t1.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t2.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t3.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t4.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t5.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t6.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t7.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t8.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t9.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t10.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t11.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t12.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t13.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t14.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t15.Value = ValueArray[j++];
                        manager.SetSharedComponentData(entities[i], t0);
                        manager.SetSharedComponentData(entities[i], t1);
                        manager.SetSharedComponentData(entities[i], t2);
                        manager.SetSharedComponentData(entities[i], t3);
                        manager.SetSharedComponentData(entities[i], t4);
                        manager.SetSharedComponentData(entities[i], t5);
                        manager.SetSharedComponentData(entities[i], t6);
                        manager.SetSharedComponentData(entities[i], t7);
                        manager.SetSharedComponentData(entities[i], t8);
                        manager.SetSharedComponentData(entities[i], t9);
                        manager.SetSharedComponentData(entities[i], t10);
                        manager.SetSharedComponentData(entities[i], t11);
                        manager.SetSharedComponentData(entities[i], t12);
                        manager.SetSharedComponentData(entities[i], t13);
                        manager.SetSharedComponentData(entities[i], t14);
                        manager.SetSharedComponentData(entities[i], t15);
                    }
                }
            }
            entities.Dispose();
        }
    }
}