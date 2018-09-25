#if REF_EQUATABLE
namespace Unity.Entities
{
    public interface IRefEquatable<T> : System.IEquatable<T> where T : struct, Unity.Entities.ISharedComponentData
    {
        bool Equals(ref T other);
    }
}
#endif