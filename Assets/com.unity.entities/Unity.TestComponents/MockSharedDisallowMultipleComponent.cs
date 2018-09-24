using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [Serializable]
    public struct MockSharedDisallowMultiple : ISharedComponentData
#if !SHARED_1
    , IHashable, IRefEquatable<MockSharedDisallowMultiple>
#endif
    {
        public int Value;
#if !SHARED_1
        public ulong HashCode => (ulong)Value;

        public bool Equals(ref MockSharedDisallowMultiple other) => Value == other.Value;

        public bool Equals(MockSharedDisallowMultiple other) => Value == other.Value;
#endif
    }

    [DisallowMultipleComponent]
    public class MockSharedDisallowMultipleComponent : SharedComponentDataWrapper<MockSharedDisallowMultiple>
    {

    }
}
