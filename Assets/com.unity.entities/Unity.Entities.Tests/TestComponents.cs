using Unity.Entities;

namespace Unity.Entities.Tests
{
    public struct EcsTestData : IComponentData
    {
        public int value;

        public EcsTestData(int inValue)
        {
            value = inValue;
        }
    }

    public struct EcsTestData2 : IComponentData
    {
        public int value0;
        public int value1;

        public EcsTestData2(int inValue)
        {
            value1 = value0 = inValue;
        }
    }

    public struct EcsTestData3 : IComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestData3(int inValue)
        {
            value2 = value1 = value0 = inValue;
        }
    }

    public struct EcsTestData4 : IComponentData
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;

        public EcsTestData4(int inValue)
        {
            value3 = value2 = value1 = value0 = inValue;
        }
    }

    public struct EcsTestSharedComp : ISharedComponentData
#if !SHARED_1
    , IRefEquatable<EcsTestSharedComp>, IHashable
#endif
    {
        public int value;

        public EcsTestSharedComp(int inValue)
        {
            value = inValue;
        }
#if !SHARED_1
        public ulong HashCode => (ulong)value;

        public bool Equals(ref EcsTestSharedComp other) => value == other.value;

        public bool Equals(EcsTestSharedComp other) => value == other.value;
#endif
    }

    public struct EcsTestSharedComp2 : ISharedComponentData
#if !SHARED_1
    , IHashable, IRefEquatable<EcsTestSharedComp2>
#endif
    {
        public int value0;
        public int value1;

        public EcsTestSharedComp2(int inValue)
        {
            value0 = value1 = inValue;
        }
#if !SHARED_1
        ulong IHashable.HashCode => ((ulong)(value0) << 32) | (ulong)value1;

        public bool Equals(ref EcsTestSharedComp2 other) => value0 == other.value0 && value1 == other.value1;

        public bool Equals(EcsTestSharedComp2 other) => value0 == other.value0 && value1 == other.value1;
#endif
    }

    public struct EcsTestDataEntity : IComponentData
    {
        public int value0;
        public Entity value1;

        public EcsTestDataEntity(int inValue0, Entity inValue1)
        {
            value0 = inValue0;
            value1 = inValue1;
        }
    }

    public struct EcsTestSharedCompEntity : ISharedComponentData
#if !SHARED_1
    , IHashable, IRefEquatable<EcsTestSharedCompEntity>
#endif
    {
        public Entity value;

        public EcsTestSharedCompEntity(Entity inValue)
        {
            value = inValue;
        }
#if !SHARED_1
        public ulong HashCode => (ulong)value.Index;

        public bool Equals(ref EcsTestSharedCompEntity other) => value.Index == other.value.Index && value.Version == other.value.Version;

        public bool Equals(EcsTestSharedCompEntity other) => value.Index == other.value.Index && value.Version == other.value.Version;
#endif
    }

    struct EcsState1 : ISystemStateComponentData
    {
        public int Value;

        public EcsState1(int value)
        {
            Value = value;
        }
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntElement : IBufferElementData
    {
        public static implicit operator int(EcsIntElement e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntElement(int e)
        {
            return new EcsIntElement { Value = e };
        }

        public int Value;
    }

    [InternalBufferCapacity(4)]
    public struct EcsComplexEntityRefElement : IBufferElementData
    {
        public int Dummy;
        public Entity Entity;
    }

    public struct EcsTestTag : IComponentData
    {
    }
}
