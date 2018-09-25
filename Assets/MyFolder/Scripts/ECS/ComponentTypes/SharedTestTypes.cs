using Unity.Entities;

namespace おう考えてやるからあくしろよテスト
{
    public unsafe struct TEST : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<TEST>
#endif
    {
        public ulong Value;
        public fixed ulong _array[1024];

#if REF_EQUATABLE
        public bool Equals(TEST other) => this.Value == other.Value;
        public bool Equals(ref TEST other) => this.Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((TEST)obj).Value == Value;
        public override int GetHashCode() => (int)Value;
#endif
    }
}