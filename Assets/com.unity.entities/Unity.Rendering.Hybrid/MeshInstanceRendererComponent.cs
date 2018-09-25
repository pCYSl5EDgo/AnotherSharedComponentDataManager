using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Rendering
{
    /// <summary>
    /// Render Mesh with Material (must be instanced material) by object to world matrix.
    /// Specified by the LocalToWorld associated with Entity.
    /// </summary>
    [Serializable]
    public struct MeshInstanceRenderer : ISharedComponentData
#if REF_EQUATABLE
    , IRefEquatable<MeshInstanceRenderer>
#endif
    {
        public Mesh mesh;
        public Material material;
        public int subMesh;

        public ShadowCastingMode castShadows;
        public bool receiveShadows;
#if REF_EQUATABLE
        public bool Equals(ref MeshInstanceRenderer other) => mesh == other.mesh && material == other.material && castShadows == other.castShadows && receiveShadows == other.receiveShadows;
        public bool Equals(MeshInstanceRenderer other) => mesh == other.mesh && material == other.material && castShadows == other.castShadows && receiveShadows == other.receiveShadows;
        public override bool Equals(object obj) => obj != null && ((MeshInstanceRenderer)obj).Equals(ref this);
        static MeshInstanceRenderer()
        {
            TypeManager.Initialize();
            typeInfo = TypeManager.GetTypeInfo<MeshInstanceRenderer>().FastEqualityTypeInfo;
        }
        private static readonly FastEquality.TypeInfo typeInfo;
        public override unsafe int GetHashCode() => FastEquality.GetHashCode(Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf<MeshInstanceRenderer>(ref this), typeInfo);
#endif
    }

    public class MeshInstanceRendererComponent : SharedComponentDataWrapper<MeshInstanceRenderer> { }
}
