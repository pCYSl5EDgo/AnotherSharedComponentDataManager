using System;
using Unity.Entities;

namespace おう考えてやるからあくしろよテスト
{
    public unsafe struct TEST : ISharedComponentData, IEquatable<TEST>, IRefEquatable<TEST>, IHashable
    {
        public ulong Value;
        public fixed ulong _array[1024];

        public ulong HashCode => Value;

        public bool Equals(TEST other) => this.Value == other.Value;

        public bool Equals(ref TEST other) => this.Value == other.Value;
    }
}