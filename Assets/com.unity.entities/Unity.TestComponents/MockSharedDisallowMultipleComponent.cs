using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [Serializable]
    public struct MockSharedDisallowMultiple : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<MockSharedDisallowMultiple>
#endif
    {
        public int Value;
#if REF_EQUATABLE
        public bool Equals(ref MockSharedDisallowMultiple other) => Value == other.Value;
        public bool Equals(MockSharedDisallowMultiple other) => Value == other.Value;
        public override bool Equals(object obj) => obj != null && ((MockSharedDisallowMultiple)obj).Value == Value;
        public override int GetHashCode() => Value;
#endif
    }

    [DisallowMultipleComponent]
    public class MockSharedDisallowMultipleComponent : SharedComponentDataWrapper<MockSharedDisallowMultiple>
    {

    }
}
