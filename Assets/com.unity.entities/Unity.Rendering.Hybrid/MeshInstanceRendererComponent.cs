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
#if !SHARED_1
    , IHashable, IRefEquatable<MeshInstanceRenderer>
#endif
    {
        public Mesh mesh;
        public Material material;
        public int subMesh;

        public ShadowCastingMode castShadows;
        public bool receiveShadows;
#if !SHARED_1
        public ulong HashCode => ((ulong)mesh?.GetHashCode() << 32) | ((ulong)material?.GetHashCode()) ^ (ulong)castShadows ^ (receiveShadows ? ulong.MaxValue : 0);
        public bool Equals(ref MeshInstanceRenderer other) => mesh == other.mesh && material == other.material && castShadows == other.castShadows && receiveShadows == other.receiveShadows;
        public bool Equals(MeshInstanceRenderer other) => mesh == other.mesh && material == other.material && castShadows == other.castShadows && receiveShadows == other.receiveShadows;
#endif
    }

    public class MeshInstanceRendererComponent : SharedComponentDataWrapper<MeshInstanceRenderer> { }
}
