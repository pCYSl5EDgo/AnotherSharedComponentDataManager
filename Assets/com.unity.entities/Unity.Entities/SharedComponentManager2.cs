using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Unity.Entities
{
    internal sealed class SharedComponentDataManager2 : IDisposable
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ulong CalcKey(int typeIndex, int hashCode)
        {
            var key = (ulong)typeIndex;
            key <<= 32;
            key |= (ulong)hashCode;
            return key;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int ModifyIndex(int typeIndex, int actualIndex)
        {
            typeIndex <<= 16;
            typeIndex |= (actualIndex + 1) & 0xffff;
            return typeIndex;
        }
        private static bool DeconstructIndex(int index, out int typeIndex, out int actualIndex)
        {
            if (index == 0)
            {
                typeIndex = actualIndex = 0;
                return false;
            }
            actualIndex = (index & 0xffff) - 1;
            typeIndex = (int)(((uint)index) >> 16);
            return true;
        }
        private NativeMultiHashMap<ulong, int> indexDictionary = new NativeMultiHashMap<ulong, int>(128, Allocator.Persistent);

        private Dictionary<int, (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, SortedSet<int> freeIndices)> dataDictionary = new Dictionary<int, (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, SortedSet<int> freeIndices)>();

        public void Dispose()
        {
            if (!IsEmpty())
                Debug.LogWarning("SharedComponentData manager should be empty on shutdown");
            indexDictionary.Dispose();
            foreach (var (_, referenceCounts, versions, _) in dataDictionary.Values)
            {
                if (referenceCounts.IsCreated)
                    referenceCounts.Dispose();
                if (versions.IsCreated)
                    versions.Dispose();
            }
            dataDictionary.Clear();
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
                return;
            var list = tuple.dataList as List<T>;
            if (tuple.freeIndices.Count == 0)
            {
                sharedComponentValues.AddRange(list);
                return;
            }
            var enumerator = tuple.freeIndices.GetEnumerator();
            var index = 0;
            var existFree = enumerator.MoveNext();
            var count = list.Count;
            for (; existFree && index != count; ++index)
            {
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(list[index]);
                    continue;
                }
                else if (index == enumerator.Current)
                {
                    existFree = enumerator.MoveNext();
                    continue;
                }
                do
                {
                    if (!(existFree = enumerator.MoveNext()))
                        break;
                } while (index > enumerator.Current);
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(list[index]);
                    continue;
                }
                else if (index == enumerator.Current)
                    continue;
                for (int i = index; i < count; i++)
                    sharedComponentValues.Add(list[i]);
                return;
            }
            if (index == count) return;
            for (int i = index; i < count; i++)
                sharedComponentValues.Add(list[i]);
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            sharedComponentIndices.Add(0);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
                return;
            var list = tuple.dataList as List<T>;
            if (tuple.freeIndices.Count == 0)
            {
                sharedComponentValues.AddRange(list);
                for (int i = 1, length = list.Count + 1; i < length; i++)
                    sharedComponentIndices.Add(i);
                return;
            }
            var enumerator = tuple.freeIndices.GetEnumerator();
            var index = 0;
            var existFree = enumerator.MoveNext();
            var count = list.Count;
            for (; existFree && index != count; ++index)
            {
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(list[index]);
                    sharedComponentIndices.Add(index + 1);
                    continue;
                }
                else if (index == enumerator.Current)
                {
                    existFree = enumerator.MoveNext();
                    continue;
                }
                do
                {
                    if (!(existFree = enumerator.MoveNext()))
                        break;
                } while (index > enumerator.Current);
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(list[index]);
                    sharedComponentIndices.Add(index + 1);
                    continue;
                }
                else if (index == enumerator.Current)
                    continue;
                for (int i = index; i < count; i++)
                {
                    sharedComponentValues.Add(list[i]);
                    sharedComponentIndices.Add(i + 1);
                }
                return;
            }
            if (index == count) return;
            for (int i = index; i < count; i++)
            {
                sharedComponentValues.Add(list[i]);
                sharedComponentIndices.Add(i + 1);
            }
        }

        public int GetSharedComponentCount()
        {
            var answer = 1;
            foreach (var (dataList, _, _, _) in dataDictionary.Values)
                answer += dataList?.Count ?? 0;
            return answer;
        }

        public int InsertSharedComponent<T>(T newData) where T : struct, ISharedComponentData => InsertSharedComponent(ref newData);
        public int InsertSharedComponent<T>(ref T newData) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            {
                T defaultVal = default;
                if (FastEquality.Equals(ref newData, ref defaultVal, typeInfo)) return 0;
            }
            var hashCode = FastEquality.GetHashCode(ref newData, typeInfo);
            if (dataDictionary.TryGetValue(typeIndex, out var tuple))
            {
                var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, hashCode), ref newData, typeInfo, tuple.dataList as List<T>);
                if (index == -1)
                    return ModifyIndex(typeIndex, Add(typeIndex, hashCode, ref newData));
                ++tuple.referenceCounts[index];
                return ModifyIndex(typeIndex, index);
            }
            else
            {
                InitializeAdd(typeIndex, CalcKey(typeIndex, hashCode), ref newData);
                return ModifyIndex(typeIndex, 0);
            }
        }
        private unsafe int FindNonDefaultSharedComponentIndex(int typeIndex, ulong key, void* newDataPtr, FastEquality.TypeInfo typeInfo, IList list)
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                var dataPtr = PinGCObjectAndGetAddress(list[itemIndex], out var dataHandle);
                if (FastEquality.Equals(dataPtr, newDataPtr, typeInfo))
                {
                    UnsafeUtility.ReleaseGCObject(dataHandle);
                    return itemIndex;
                }
                UnsafeUtility.ReleaseGCObject(dataHandle);
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        private int FindNonDefaultSharedComponentIndex<T>(int typeIndex, ulong key, ref T newData, FastEquality.TypeInfo typeInfo, List<T> list) where T : struct, ISharedComponentData
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                var data = list[itemIndex];
                if (FastEquality.Equals(ref data, ref newData, typeInfo))
                    return itemIndex;
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        private int FindNonDefaultSharedComponentIndex<T>(int typeIndex, ulong key, ref T newData, FastEquality.TypeInfo typeInfo) where T : struct, ISharedComponentData
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            var list = dataDictionary[typeIndex].dataList as List<T>;
            do
            {
                var data = list[itemIndex];
                if (FastEquality.Equals(ref data, ref newData, typeInfo))
                    return itemIndex;
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        private static readonly Type genericList = typeof(List<>);
        private static readonly Type[] types = new Type[1];
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, FastEquality.TypeInfo typeInfo)
        {
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var key = CalcKey(typeIndex, hashCode);
            if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
            {
                types[0] = TypeManager.GetType(typeIndex);
                tuple.dataList = Activator.CreateInstance(genericList.MakeGenericType(types)) as IList;
                tuple.freeIndices = new SortedSet<int>();
                tuple.referenceCounts = new NativeList<int>(16, Allocator.Persistent);
                tuple.versions = new NativeList<int>(16, Allocator.Persistent);
                dataDictionary.Add(typeIndex, tuple);
            }
            var index = FindNonDefaultSharedComponentIndex(typeIndex, key, ptr, typeInfo, tuple.dataList);
            if (index == -1)
            {
                index = Add(typeIndex, hashCode, newData);
            }
            else
            {
                var refcounts = dataDictionary[typeIndex].referenceCounts;
                refcounts[index] += 1;
            }
            UnsafeUtility.ReleaseGCObject(handle);
            return ModifyIndex(typeIndex, index);
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, object newData, FastEquality.TypeInfo typeInfo, IList list)
        {
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var hashCode = FastEquality.GetHashCode(ptr, typeInfo);
            var key = CalcKey(typeIndex, hashCode);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, key, ptr, typeInfo, list);
            if (index == -1)
            {
                index = Add(typeIndex, hashCode, newData);
            }
            else
            {
                var refcounts = dataDictionary[typeIndex].referenceCounts;
                refcounts[index] += 1;
            }
            UnsafeUtility.ReleaseGCObject(handle);
            return ModifyIndex(typeIndex, index);
        }

        private unsafe void InitializeAdd<T>(int typeIndex, ulong key, ref T newData) where T : struct, ISharedComponentData
        {
            (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, SortedSet<int> freeIndices) tuple;
            (tuple.dataList = new List<T>()).Add(newData);
            tuple.freeIndices = new SortedSet<int>();
            (tuple.referenceCounts = new NativeList<int>(16, Allocator.Persistent)).Add(1);
            (tuple.versions = new NativeList<int>(16, Allocator.Persistent)).Add(1);
            dataDictionary.Add(typeIndex, tuple);
            indexDictionary.Add(key, 0);
        }

        private int Add(int typeIndex, int hashCode, object newData)
        {
            int index;
            var (arrayList, refcounts, versions, freeIndices) = dataDictionary[typeIndex];
            if (freeIndices.Count == 0)
            {
                index = arrayList.Count;
                arrayList.Add(newData);
                refcounts.Add(1);
                versions.Add(1);
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                return index;
            }
            index = freeIndices.Min;
            freeIndices.Remove(index);
            indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
            arrayList[index] = newData;
            refcounts[index] = 1;
            versions[index] = 1;
            return index;
        }

        private int Add<T>(int typeIndex, int hashCode, ref T newData) where T : struct, ISharedComponentData
        {
            int index;
            var (arrayList, refcounts, versions, freeIndices) = dataDictionary[typeIndex];
            var list = arrayList as List<T>;
            if (freeIndices.Count == 0)
            {
                index = list.Count;
                list.Add(newData);
                refcounts.Add(1);
                versions.Add(1);
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                return index;
            }
            index = freeIndices.Min;
            freeIndices.Remove(index);
            indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
            list[index] = newData;
            refcounts[index] = 1;
            versions[index] = 1;
            return index;
        }


        public void IncrementSharedComponentVersion(int modifiedIndex)
        {
            if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex))
                return;
            if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
                throw new ArgumentOutOfRangeException();
            ++tuple.versions[actualIndex];
        }

        public int GetSharedComponentVersion<T>(T sharedData) where T : struct, ISharedComponentData => GetSharedComponentVersion(ref sharedData);
        public int GetSharedComponentVersion<T>(ref T sharedData) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            {
                T defaultVal = default;
                if (FastEquality.Equals(ref defaultVal, ref sharedData, typeInfo)) return 0;
            }
            if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
                return 0;
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, FastEquality.GetHashCode(ref sharedData, typeInfo)), ref sharedData, typeInfo, tuple.dataList as List<T>);
            return index == -1 ? 0 : tuple.versions[index];
        }

        public T GetSharedComponentData<T>(int index) where T : struct
            => DeconstructIndex(index, out var typeIndex, out var actualIndex) ? default : (dataDictionary[typeIndex].dataList as List<T>)[actualIndex];

        public object GetSharedComponentDataBoxed(int index, int typeIndex)
            => DeconstructIndex(index, out var typeIndex2, out var actualIndex) ? Activator.CreateInstance(TypeManager.GetType(typeIndex)) : (typeIndex == typeIndex2 ? dataDictionary[typeIndex].dataList[actualIndex] : throw new Exception());

        public object GetSharedComponentDataNonDefaultBoxed(int index)
        {
            Assert.AreNotEqual(0, index);
            DeconstructIndex(index, out var typeIndex, out var actualIndex);
            return dataDictionary[typeIndex].dataList[actualIndex];
        }

        public void AddReference(int modifiedIndex)
        {
            if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex)) return;
            var refcounts = dataDictionary[typeIndex].referenceCounts;
            ++refcounts[actualIndex];
        }

        private static unsafe void* PinGCObjectAndGetAddress(object target, out ulong handle)
        {
            var ptr = UnsafeUtility.PinGCObjectAndGetAddress(target, out handle);
            return (byte*)ptr + TypeManager.ObjectOffset;
        }


        public unsafe void RemoveReference(int modifiedIndex)
        {
            if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex)) return;
            var tuple = dataDictionary[typeIndex];
            var newCount = --tuple.referenceCounts[actualIndex];
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            var data = tuple.dataList[actualIndex];
            var ptr = PinGCObjectAndGetAddress(data, out var handle);
            var hashCode = FastEquality.GetHashCode(ptr, typeInfo);
            UnsafeUtility.ReleaseGCObject(handle);

            tuple.freeIndices.Add(actualIndex);

            if (!indexDictionary.TryGetFirstValue(CalcKey(typeIndex, hashCode), out var itemIndex, out var iter))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new System.ArgumentException("RemoveReference didn't find element in in hashtable");
#endif
            }

            do
            {
                if (itemIndex == actualIndex)
                {
                    indexDictionary.Remove(iter);
                    break;
                }
            }
            while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
        }

        public bool IsEmpty()
        {
            if (indexDictionary.Length != 0)
                return false;
            foreach (var (arrayList, _, _, freeIndices) in dataDictionary.Values)
                if (arrayList.Count != freeIndices.Count)
                    return false;
            return true;
        }

        public unsafe bool AllSharedComponentReferencesAreFromChunks(ArchetypeManager archetypeManager)
        {
            var chunkCount = new Dictionary<int, int>();
            var archetype = archetypeManager.m_LastArchetype;
            while (archetype != null)
            {
                for (var c = archetype->ChunkList.Begin; c != archetype->ChunkList.End; c = c->Next)
                {
                    var chunk = (Chunk*)c;

                    for (int i = 0; i < archetype->NumSharedComponents; ++i)
                        chunkCount[chunk->SharedComponentValueArray[i]] += 1;
                }

                archetype = archetype->PrevArchetype;
            }

            chunkCount[0] = 1;
            foreach ((int modifiedIndex, int referenceCount) in chunkCount)
            {
                var typeIndex = (int)((uint)modifiedIndex >> 16);
                var actualIndex = modifiedIndex & 0xffff;
                if (typeIndex == 0) continue;
                if (!dataDictionary.TryGetValue(typeIndex, out var tuple)) return false;
                if (tuple.referenceCounts[actualIndex] != referenceCount) return false;
            }
            return true;
        }
        public unsafe NativeHashMap<int, int> MoveAllSharedComponents(SharedComponentDataManager2 srcSharedComponents, Allocator allocator)
        {
            var remapSharedModifiedSharedIndices = new NativeHashMap<int, int>(srcSharedComponents.GetSharedComponentCount(), allocator);
            remapSharedModifiedSharedIndices.TryAdd(0, 0);
            foreach (var (typeIndex, (arrayList, referenceCounts, versions, freeIndices)) in srcSharedComponents.dataDictionary)
            {
                if (freeIndices.Count == arrayList.Count) continue;
                var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
                if (!dataDictionary.TryGetValue(typeIndex, out var tuple))
                {
                    tuple.dataList = Activator.CreateInstance(arrayList.GetType()) as IList;
                    tuple.freeIndices = new SortedSet<int>();
                    tuple.referenceCounts = new NativeList<int>(128, Allocator.Persistent);
                    tuple.versions = new NativeList<int>(128, Allocator.Persistent);
                    dataDictionary.Add(typeIndex, tuple);
                }
                var enumerator = freeIndices.GetEnumerator();
                var existFree = enumerator.MoveNext();
                var count = arrayList.Count;
                var index = 0;
                for (; existFree && index != count; ++index)
                {
                    if (index < enumerator.Current)
                    {
                        remapSharedModifiedSharedIndices.TryAdd(ModifyIndex(typeIndex, index), InsertSharedComponentAssumeNonDefault(typeIndex, arrayList[index], typeInfo, arrayList));
                        continue;
                    }
                    else if (index == enumerator.Current)
                    {
                        existFree = enumerator.MoveNext();
                        continue;
                    }
                    do
                    {
                        if (!(existFree = enumerator.MoveNext()))
                            break;
                    } while (index > enumerator.Current);
                    if (index < enumerator.Current)
                    {
                        remapSharedModifiedSharedIndices.TryAdd(ModifyIndex(typeIndex, index), InsertSharedComponentAssumeNonDefault(typeIndex, arrayList[index], typeInfo, arrayList));
                        continue;
                    }
                    else if (index == enumerator.Current)
                        continue;
                    for (int i = index; i < count; i++)
                        remapSharedModifiedSharedIndices.TryAdd(ModifyIndex(typeIndex, index), InsertSharedComponentAssumeNonDefault(typeIndex, arrayList[i], typeInfo, arrayList));
                    continue;
                }
                if (index == count) continue;
                for (int i = index; i < count; i++)
                    remapSharedModifiedSharedIndices.TryAdd(ModifyIndex(typeIndex, index), InsertSharedComponentAssumeNonDefault(typeIndex, arrayList[i], typeInfo, arrayList));
            }
            srcSharedComponents.indexDictionary.Clear();
            foreach (var item in srcSharedComponents.dataDictionary.Values)
            {
                item.dataList.Clear();
                item.freeIndices.Clear();
                item.referenceCounts.ResizeUninitialized(0);
                item.versions.ResizeUninitialized(0);
            }
            return remapSharedModifiedSharedIndices;
        }

        public void PrepareForDeserialize()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsEmpty())
                throw new System.ArgumentException("SharedComponentManager must be empty when deserializing a scene");
#endif
            indexDictionary.Clear();
            foreach (var item in dataDictionary.Values)
            {
                item.dataList.Clear();
                item.freeIndices.Clear();
                item.referenceCounts.ResizeUninitialized(0);
                item.versions.ResizeUninitialized(0);
            }
        }
    }
    internal static class KeyValuePairHelper
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
        public static void Deconstruct(this int index, out int typeIndex, out int actualIndex)
        {
            actualIndex = index & 0xffff;
            typeIndex = (int)(((uint)index) >> 16);
        }
    }
}