public interface IRefEquatable<T> : System.IEquatable<T> where T : struct, Unity.Entities.ISharedComponentData
{
    bool Equals(ref T other);
}

public interface IHashable
{
    ulong HashCode { get; }
}