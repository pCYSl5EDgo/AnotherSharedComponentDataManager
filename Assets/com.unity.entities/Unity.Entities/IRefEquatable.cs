#if REF_EQUATABLE
public interface IRefEquatable<T> : System.IEquatable<T> where T : struct, Unity.Entities.ISharedComponentData
{
    bool Equals(ref T other);
}
#endif