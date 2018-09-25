namespace Unity.Entities.Tests
{
    [System.Serializable]
    public struct TestShared : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TestShared>
#endif
    {
        public int Value;

        public TestShared(int value) { Value = value; }
#if REF_EQUATABLE
        public bool Equals(ref TestShared other) => this.Value == other.Value;
        public bool Equals(TestShared other) => this.Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TestShared)obj).Equals(ref this);
        public override int GetHashCode() => Value;
#endif
    }

    class TestSharedComponent : SharedComponentDataWrapper<TestShared>
    {

    }
}
