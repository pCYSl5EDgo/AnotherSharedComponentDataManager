namespace Unity.Entities.Tests
{
    [System.Serializable]
    public struct TestShared : ISharedComponentData
#if !SHARED_1
    , IHashable, IRefEquatable<TestShared>
#endif
    {
        public int Value;

        public TestShared(int value) { Value = value; }
#if !SHARED_1
        public ulong HashCode => (ulong)Value;

        public bool Equals(ref TestShared other) => this.Value == other.Value;

        public bool Equals(TestShared other) => this.Value == other.Value;
#endif
    }

    class TestSharedComponent : SharedComponentDataWrapper<TestShared>
    {

    }
}
