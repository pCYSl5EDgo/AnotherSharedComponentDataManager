﻿#if !SHARED_1
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using System.Runtime.CompilerServices;

using static Unity.Mathematics.math;

namespace Unity.Entities
{
    internal sealed class SharedComponentDataManager : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong CalcKey(int typeIndex, int hashCode)
        {
            var key = (ulong)typeIndex;
            key <<= 32;
            key |= (ulong)hashCode;
            return key;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ModifyIndex(int typeIndex, int actualIndex)
        {
            typeIndex <<= 16;
            typeIndex |= actualIndex & 0xffff;
            return typeIndex;
        }
        private static bool DeconstructIndex(int index, out int typeIndex, out int actualIndex)
        {
            if (index == 0)
            {
                typeIndex = actualIndex = 0;
                return false;
            }
            actualIndex = index & 0xffff;
            typeIndex = (int)(((uint)index) >> 16);
            return true;
        }
        private NativeMultiHashMap<ulong, int> indexDictionary = new NativeMultiHashMap<ulong, int>(128, Allocator.Persistent);
        private (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex)[] dataArray;

        private static void AddFreeIndex(int index, ref NativeArray<ulong> freeIndices, ref int maxFreeIndex)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index > maxFreeIndex)
            {
                maxFreeIndex = index;
            }
            var chunkIndex = index >> 6;
            while (freeIndices.Length <= chunkIndex)
                Lengthen(ref freeIndices);
            freeIndices[chunkIndex] |= (1u << (index & 63));
        }
        private static int DropMaxFreeIndex(NativeArray<ulong> freeIndices, ref int maxFreeIndex)
        {
            if (maxFreeIndex == -1) return -1;
            if (maxFreeIndex == 0)
            {
                maxFreeIndex = -1;
                freeIndices[0] = 0;
                return 0;
            }
            var answer = maxFreeIndex;
            var rest = maxFreeIndex & 63;
            var chunkIndex = maxFreeIndex >> 6;
            freeIndices[chunkIndex] &= (~(1u << rest));
            while (freeIndices[chunkIndex] == 0)
            {
                if (chunkIndex-- == 0)
                {
                    maxFreeIndex = -1;
                    return answer;
                }
            }
            maxFreeIndex = 63 - lzcnt(freeIndices[chunkIndex]) + (chunkIndex << 6);
            return answer;
        }
        private struct Enumerator : IEnumerator<int>
        {
            public Enumerator(NativeArray<ulong> array, int maxIndex)
            {
                if (!array.IsCreated) throw new ArgumentNullException(nameof(array));
                this.array = array;
                this.chunkIndex = -1;
                this.bitIndex = 63;
                this.maxIndex = maxIndex;
                this.current = ulong.MaxValue;
            }
            readonly NativeArray<ulong> array;
            int chunkIndex;
            int bitIndex;
            ulong current;
            readonly int maxIndex;

            public int Current => (chunkIndex << 6) | bitIndex;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (maxIndex == -1 || maxIndex == Current) return false;
                var maxIndexChunkIndex = maxIndex >> 6;
                if (bitIndex != 63)
                    current &= ~(1u << bitIndex++);
                else
                {
                    ++chunkIndex;
                    bitIndex = 0;
                    if (chunkIndex == array.Length) return false;
                    current = array[chunkIndex];
                }
                while (current == 0)
                    current = array[++chunkIndex];
                bitIndex = tzcnt(current);
                return true;
            }

            public void Reset()
            {
                this.chunkIndex = -1;
                this.bitIndex = 63;
                this.current = ulong.MaxValue;
            }

            public void Dispose() { }
        }
        public static readonly int SharedComponentTypeStart;
        public static void Initialize() { }
        static SharedComponentDataManager()
        {
            TypeManager.Initialize();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            ISharedComponentDataType = typeof(ISharedComponentData);
            for (int i = 0; i < assemblies.Length; ++i)
            {
                var types = assemblies[i].GetTypes();
                for (int j = 0; j < types.Length; ++j)
                    if (types[j].IsValueType && ISharedComponentDataType.IsAssignableFrom(types[j]))
                        TypeManager.GetTypeIndex(types[j]);
            }
            for (int i = 1, length = TypeManager.GetTypeCount(); i < length; ++i)
            {
                var type = TypeManager.GetType(i);
                if (!type.IsValueType || !ISharedComponentDataType.IsAssignableFrom(type)) continue;
                SharedComponentTypeStart = i;
                break;
            }
        }

        public void Dispose()
        {
            indexDictionary.Dispose();
            if (dataArray == null) return;
            for (int i = 0; i < dataArray.Length; ++i)
            {
                ref var element = ref dataArray[i];
                if (element.dataList != null)
                    element.dataList = null;
                if (element.referenceCounts.IsCreated)
                    element.referenceCounts.Dispose();
                if (element.versions.IsCreated)
                    element.versions.Dispose();
                if (element.freeIndices.IsCreated)
                    element.freeIndices.Dispose();
            }
            dataArray = null;
        }

        private static readonly Type[] types = new Type[1];
        private static readonly Type ListType = typeof(List<>);
        private static readonly Type ISharedComponentDataType;
        private static void Lengthen(ref NativeArray<ulong> array)
        {
            var length = array.Length;
            var tmp = new NativeArray<ulong>(length << 1, Allocator.Persistent);
            unsafe
            {
                var src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
                var dst = NativeArrayUnsafeUtility.GetUnsafePtr(tmp);
                UnsafeUtility.MemCpy(dst, src, length << 3);
            }
            array.Dispose();
            array = tmp;
        }
        private void ReAlloc(int requiredComponentTypeIndexNotModified)
        {
            var oldLength = dataArray.Length;
            if (requiredComponentTypeIndexNotModified < oldLength + SharedComponentTypeStart) return;
            var currentActualLength = TypeManager.GetTypeCount() - SharedComponentTypeStart;
            for (int i = currentActualLength - 1; i >= oldLength; --i)
            {
                types[0] = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!types[0].IsValueType || !ISharedComponentDataType.IsAssignableFrom(types[0])) continue;
                var tmp = new (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex)[i + 1];
                Array.Copy(dataArray, tmp, dataArray.Length);
                dataArray = tmp;
                Initialize(ref dataArray[i]);
                break;
            }
            for (int i = dataArray.Length - 2; i >= oldLength; --i)
            {
                types[0] = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!ISharedComponentDataType.IsAssignableFrom(types[0]) || !types[0].IsValueType) continue;
                Initialize(ref dataArray[i]);
            }
        }

        private static void Initialize(ref (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex) element)
        {
            element.dataList = Activator.CreateInstance(ListType.MakeGenericType(types)) as IList;
            element.freeIndices = new NativeArray<ulong>(2, Allocator.Persistent);
            element.referenceCounts = new NativeList<int>(128, Allocator.Persistent);
            element.versions = new NativeList<int>(128, Allocator.Persistent);
            element.maxFreeIndex = -1;
        }

        public SharedComponentDataManager()
        {
            var actualCount = TypeManager.GetTypeCount() - SharedComponentTypeStart;
            dataArray = new (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex)[actualCount];
            for (int i = 0; i < dataArray.Length; i++)
            {
                types[0] = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!ISharedComponentDataType.IsAssignableFrom(types[0]) || !types[0].IsValueType) continue;
                Initialize(ref dataArray[i]);
            }
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            // ReAlloc(typeIndex);
            ref var element = ref Accessor(typeIndex);
            var list = element.dataList as List<T>;
            if (element.maxFreeIndex == -1)
            {
                sharedComponentValues.AddRange(list);
                return;
            }
            var enumerator = new Enumerator(element.freeIndices, element.maxFreeIndex);
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
            var dataIndex = typeIndex - SharedComponentTypeStart;
            // ReAlloc(typeIndex);
            ref var element = ref dataArray[dataIndex];
            var list = element.dataList as List<T>;
            if (element.maxFreeIndex == -1)
            {
                sharedComponentValues.AddRange(list);
                for (int i = 0, length = list.Count; i < length; i++)
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
                return;
            }
            var index = 0;
            var enumerator = new Enumerator(element.freeIndices, element.maxFreeIndex);
            var existFree = enumerator.MoveNext();
            var count = list.Count;
            for (; existFree && index != count; ++index)
            {
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(list[index]);
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, index));
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
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, index));
                    continue;
                }
                else if (index == enumerator.Current)
                    continue;
                for (int i = index; i < count; i++)
                {
                    sharedComponentValues.Add(list[i]);
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
                }
                return;
            }
            if (index == count) return;
            for (int i = index; i < count; i++)
            {
                sharedComponentValues.Add(list[i]);
                sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex) Accessor(int typeIndex) => ref dataArray[typeIndex - SharedComponentTypeStart];

        public int GetSharedComponentCount()
        {
            var answer = 1;
            for (int i = 0; i < dataArray.Length; i++)
                if (dataArray[i].dataList != null)
                    answer += dataArray[i].versions.Length;
            return answer;
        }
        public int GetSharedComponentCount<T>() => Accessor(TypeManager.GetTypeIndex<T>()).versions.Length + 1;

        public int InsertSharedComponent<T>(ref T newData) where T : struct, ISharedComponentData, IRefEquatable<T>, IHashable
        {
            if (default(T).Equals(ref newData))
                return 0;

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;

            var hashCode = FastEquality.GetHashCode(ref newData, typeInfo);
            ref var element = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, hashCode), ref newData, typeInfo, element.dataList as List<T>);
            if (index != -1)
            {
                ++element.referenceCounts[index];
                return ModifyIndex(typeIndex, index);
            }
            return ModifyIndex(typeIndex, Add(typeIndex, hashCode, ref newData, ref element));
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
        private unsafe int FindNonDefaultSharedComponentIndex<T>(int typeIndex, ulong key, ref T newData, FastEquality.TypeInfo typeInfo, List<T> list) where T : struct, ISharedComponentData, IRefEquatable<T>
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                if (list[itemIndex].Equals(ref newData))
                    return itemIndex;
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }

        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, FastEquality.TypeInfo typeInfo)
        {
            // ReAlloc(typeIndex);
            ref var element = ref Accessor(typeIndex);
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var answer = InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, newData, ptr, typeInfo, element.dataList, ref element.referenceCounts);
            UnsafeUtility.ReleaseGCObject(handle);
            return answer;
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, object newData, FastEquality.TypeInfo typeInfo, IList list)
        {
            // ReAlloc(typeIndex);
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var answer = InsertSharedComponentAssumeNonDefault(typeIndex, FastEquality.GetHashCode(ptr, typeInfo), newData, ptr, typeInfo, list, ref Accessor(typeIndex).referenceCounts);
            UnsafeUtility.ReleaseGCObject(handle);
            return answer;
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, void* ptr, FastEquality.TypeInfo typeInfo, IList list, ref NativeList<int> referenceCounts)
        {
            var key = CalcKey(typeIndex, hashCode);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, key, ptr, typeInfo, list);
            if (index == -1)
                index = Add(typeIndex, hashCode, newData);
            else
                referenceCounts[index] += 1;
            return ModifyIndex(typeIndex, index);
        }

        private static unsafe void* PinGCObjectAndGetAddress(object target, out ulong handle) => (byte*)UnsafeUtility.PinGCObjectAndGetAddress(target, out handle) + TypeManager.ObjectOffset;

        private int Add<T>(int typeIndex, int hashCode, ref T newData, ref (IList dataList, NativeList<int> referenceCounts, NativeList<int> versions, NativeArray<ulong> freeIndices, int maxFreeIndex) element) where T : struct, ISharedComponentData
        {
            // ReAlloc(typeIndex);
            int index;
            var list = element.dataList as List<T>;
            if (element.maxFreeIndex == -1)
            {
                index = list.Count;
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                list.Add(newData);
                element.referenceCounts.Add(1);
                element.versions.Add(1);
            }
            else
            {
                index = DropMaxFreeIndex(element.freeIndices, ref element.maxFreeIndex);
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                list[index] = newData;
                element.referenceCounts[index] = 1;
                element.versions[index] = 1;
            }
            return index;
        }

        private int Add(int typeIndex, int hashCode, object newData)
        {
            // ReAlloc(typeIndex);
            int index;
            ref var element = ref Accessor(typeIndex);
            if (element.maxFreeIndex == -1)
            {
                index = element.dataList.Count;
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                element.dataList.Add(newData);
                element.referenceCounts.Add(1);
                element.versions.Add(1);
            }
            else
            {
                index = DropMaxFreeIndex(element.freeIndices, ref element.maxFreeIndex);
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
                element.dataList[index] = newData;
                element.referenceCounts[index] = 1;
                element.versions[index] = 1;
            }
            return index;
        }

        public void IncrementSharedComponentVersion(int modifiedIndex)
        {
            if (DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex))
                ++Accessor(typeIndex).versions[actualIndex];
        }

        public int GetSharedComponentVersion<T>(T sharedData) where T : struct, ISharedComponentData, IRefEquatable<T> => GetSharedComponentVersion(ref sharedData);
        public int GetSharedComponentVersion<T>(ref T sharedData) where T : struct, ISharedComponentData, IRefEquatable<T>
        {
            if (default(T).Equals(ref sharedData))
                return 0;
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            ref var tuple = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, FastEquality.GetHashCode(ref sharedData, typeInfo)), ref sharedData, typeInfo, tuple.dataList as List<T>);
            return index == -1 ? 0 : tuple.versions[index];
        }
        public T GetSharedComponentData<T>(int index) where T : struct
        {
            if (DeconstructIndex(index, out var typeIndex, out var actualIndex))
                return (Accessor(typeIndex).dataList as List<T>)[actualIndex];
            return default;
        }

        public object GetSharedComponentDataBoxed(int index, int typeIndex)
            => DeconstructIndex(index, out var typeIndex2, out var actualIndex) ? (typeIndex == typeIndex2 ? Accessor(typeIndex).dataList[actualIndex] : throw new Exception()) : Activator.CreateInstance(TypeManager.GetType(typeIndex));

        public object GetSharedComponentDataNonDefaultBoxed(int index)
        {
            Assert.AreNotEqual(0, index);
            DeconstructIndex(index, out var typeIndex, out var actualIndex);
            return Accessor(typeIndex).dataList[actualIndex];
        }

        public void AddReference(int modifiedIndex)
        {
            if (DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex))
                ++Accessor(typeIndex).referenceCounts[actualIndex];
        }

        public unsafe void RemoveReference<T>(int modifiedIndex) where T : struct, ISharedComponentData
        {
            if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex)) return;
            ref var element = ref Accessor(typeIndex);
            var newCount = --element.referenceCounts[actualIndex];
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            var data = (element.dataList as List<T>)[actualIndex];
            var hashCode = FastEquality.GetHashCode(ref data, typeInfo);
            AddFreeIndex(actualIndex, ref element.freeIndices, ref element.maxFreeIndex);

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

        public unsafe void RemoveReference(int modifiedIndex)
        {
            if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex)) return;
            ref var element = ref Accessor(typeIndex);
            var newCount = --element.referenceCounts[actualIndex];
            if (newCount < 0)
            {
                Debug.Log(newCount);
                Debug.Log(modifiedIndex);
                Debug.Log(typeIndex);
                Debug.Log(actualIndex);
            }
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            var data = element.dataList[actualIndex];
            var ptr = PinGCObjectAndGetAddress(data, out var handle);
            var hashCode = FastEquality.GetHashCode(ptr, typeInfo);
            UnsafeUtility.ReleaseGCObject(handle);

            AddFreeIndex(actualIndex, ref element.freeIndices, ref element.maxFreeIndex);
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
            for (int i = 0; i < dataArray.Length; i++)
            {
                var list = dataArray[i].dataList;
                if (list == null) continue;
                int maxFreeIndex = dataArray[i].maxFreeIndex;
                if (list.Count != maxFreeIndex + 1) return false;
                var rest = maxFreeIndex & 63;
                var chunkCount = maxFreeIndex >> 6;
                var array = dataArray[i].freeIndices;
                for (int j = 0; j < chunkCount; j++)
                    if (array[j] != ulong.MaxValue) return false;
            }
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
                    {
                        int key = chunk->SharedComponentValueArray[i];
                        if (chunkCount.TryGetValue(key, out var val))
                            chunkCount[key] = val + 1;
                        else chunkCount.Add(key, 1);
                    }
                }
                archetype = archetype->PrevArchetype;
            }
            chunkCount[0] = 1;
            foreach ((int modifiedIndex, int referenceCount) in chunkCount)
            {
                if (!DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex)) continue;
                var referenceCounts = Accessor(typeIndex).referenceCounts;
                if (!referenceCounts.IsCreated || referenceCounts[actualIndex] != referenceCount) return false;
            }
            return true;
        }



        internal unsafe NativeHashMap<int, int> MoveAllSharedComponents(SharedComponentDataManager srcSharedComponents, Allocator allocator)
        {
            var remap = new NativeHashMap<int, int>(srcSharedComponents.GetSharedComponentCount(), allocator);
            remap.TryAdd(0, 0);
            ref var srcArray = ref srcSharedComponents.dataArray;
            for (int i = 0; i < srcArray.Length; i++)
            {
                ref var tuple = ref srcArray[i];
                if (tuple.dataList == null) continue;
                var typeIndex = SharedComponentTypeStart + i;
                var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
                ref var thisRefCounts = ref Accessor(typeIndex).referenceCounts;
                for (int j = 0, count = tuple.dataList.Count; j < count; j++)
                {
                    var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, tuple.dataList[j], typeInfo, tuple.dataList);
                    remap.TryAdd(ModifyIndex(typeIndex, j), dstIndex);
                    if (DeconstructIndex(dstIndex, out _, out var actualIndex))
                        thisRefCounts[actualIndex] += tuple.referenceCounts[j] - 1;
                    else throw new Exception();
                }
            }
            srcSharedComponents.PrepareForDeserialize_Inner();
            return remap;
        }

        private void PrepareForDeserialize_Inner()
        {
            indexDictionary.Clear();
            for (int i = 0; i < dataArray.Length; i++)
            {
                ref var element = ref dataArray[i];
                if (element.dataList == null) continue;
                element.referenceCounts.ResizeUninitialized(0);
                element.versions.ResizeUninitialized(0);
                element.maxFreeIndex = -1;
                element.dataList.Clear();
                unsafe
                {
                    var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(element.freeIndices);
                    UnsafeUtility.MemClear(ptr, element.freeIndices.Length << 3);
                }
            }
        }
        public void PrepareForDeserialize()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsEmpty())
                throw new System.ArgumentException("SharedComponentManager must be empty when deserializing a scene");
#endif
            PrepareForDeserialize_Inner();
        }
    }

    public static class KeyValuePairHelper
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
#endif