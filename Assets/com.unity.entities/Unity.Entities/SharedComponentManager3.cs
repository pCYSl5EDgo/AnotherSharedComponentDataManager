#if !SHARED_1
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
        private const int LSHIFT = 18;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ModifyIndex(int typeIndex, int actualIndex) => (typeIndex << LSHIFT) ^ actualIndex;
        private static bool DeconstructIndex(int index, out int typeIndex, out int actualIndex)
        {
            if (index == 0)
            {
                typeIndex = actualIndex = 0;
                return false;
            }
            actualIndex = index & 0x3ffff;
            typeIndex = (int)(((uint)index) >> LSHIFT);
            return true;
        }
        private static bool DeconstructIndex(int index, int typeIndex, out int actualIndex)
        {
            if (index == 0)
            {
                actualIndex = 0;
                return false;
            }
            actualIndex = index ^ (typeIndex << LSHIFT);
            return true;
        }
        private NativeMultiHashMap<ulong, int> indexDictionary = new NativeMultiHashMap<ulong, int>(128, Allocator.Persistent);
        private struct Element
        {
            public static Element Create(int capacity, Type elementType)
            {
                if (capacity <= 32) throw new ArgumentOutOfRangeException();
                capacity = Unity.Mathematics.math.ceilpow2(capacity);
                var answer = new Element
                {
                    actualLength = 0,
                    maxFreeIndex = -1,
                };
                answer.datas = Array.CreateInstance(elementType, capacity);
                answer.referenceCounts = new int[capacity];
                answer.versions = new int[capacity];
                answer.freeIndices = new ulong[capacity >> 6];
                return answer;
            }
            private unsafe void LengthenCopy()
            {
                var count = referenceCounts.Length;
                var _ = new int[count << 1];
                fixed (void* dst = _)
                fixed (void* src = referenceCounts)
                {
                    Buffer.MemoryCopy(src, dst, count << 2, count << 2);
                }
                referenceCounts = _;
                _ = new int[count << 1];
                fixed (void* dst = _)
                fixed (void* src = versions)
                {
                    Buffer.MemoryCopy(src, dst, count << 2, count << 2);
                }
                versions = _;
                var tmpFreeIndices = new ulong[freeIndices.Length << 1];
                fixed (void* dst = tmpFreeIndices)
                fixed (void* src = freeIndices)
                {
                    Buffer.MemoryCopy(src, dst, freeIndices.Length << 3, freeIndices.Length << 3);
                }
                freeIndices = tmpFreeIndices;
            }
            public void Lengthen(Type elementType)
            {
                var tmpDatas = Array.CreateInstance(elementType, referenceCounts.Length << 1);
                Buffer.BlockCopy(datas, 0, tmpDatas, 0, referenceCounts.Length);
                datas = tmpDatas;
                LengthenCopy();
            }
            public void Lengthen<T>()
            {
                var tmpDatas = new T[referenceCounts.Length << 1];
                Buffer.BlockCopy(datas, 0, tmpDatas, 0, referenceCounts.Length);
                datas = tmpDatas;
                LengthenCopy();
            }
            public Array datas;
            public int[] referenceCounts;
            public int[] versions;
            public ulong[] freeIndices;
            public int actualLength;
            public int maxFreeIndex;
        }

        private Element[] dataArray;

        private static void AddFreeIndex(int index, ulong[] freeIndices, ref int maxFreeIndex)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index > maxFreeIndex)
            {
                maxFreeIndex = index;
            }
            freeIndices[index >> 6] |= (1u << (index & 63));
        }
        private static int DropMaxFreeIndex(ulong[] freeIndices, ref int maxFreeIndex)
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
            freeIndices[chunkIndex] &= ~(1u << rest);
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
            public Enumerator(ulong[] array, int maxIndex)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                this.array = array;
                this.chunkIndex = -1;
                this.bitIndex = 63;
                this.maxIndex = maxIndex;
                this.current = ulong.MaxValue;
            }
            readonly ulong[] array;
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
        private static readonly int SharedComponentTypeStart;
        private static readonly Type[] EqualsTypeArray = new Type[] { typeof(object) };
        static SharedComponentDataManager()
        {
            TypeManager.Initialize();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            ISharedComponentDataType = typeof(ISharedComponentData);
            for (int i = 0; i < assemblies.Length; ++i)
            {
                var types = assemblies[i].GetTypes();
                for (int j = 0; j < types.Length; ++j)
                {
                    if (!types[j].IsValueType || !ISharedComponentDataType.IsAssignableFrom(types[j]) || (types[j].IsGenericType && types[j].IsGenericTypeDefinition)) continue;
                    TypeManager.GetTypeIndex(types[j]);
                }
            }
            for (int i = 1, length = TypeManager.GetTypeCount(); i < length; ++i)
            {
                var type = TypeManager.GetType(i);
                if (!type.IsValueType || !ISharedComponentDataType.IsAssignableFrom(type) || (type.IsGenericType && type.IsGenericTypeDefinition)) continue;
#if REF_EQUATABLE
                var EqualsMethodInfo = type.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, CallingConventions.Any, EqualsTypeArray, null);
                if (EqualsMethodInfo == null || EqualsMethodInfo.DeclaringType != type) throw new Exception(type.FullName);
                var GetHashCodeMethodInfo = type.GetMethod("GetHashCode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, CallingConventions.Any, Array.Empty<Type>(), null);
                if (GetHashCodeMethodInfo == null || GetHashCodeMethodInfo.DeclaringType != type) throw new Exception(type.FullName);
#endif
                SharedComponentTypeStart = i;
                break;
            }
        }

        public void Dispose()
        {
            indexDictionary.Dispose();
            dataArray = null;
        }

        private static readonly Type ISharedComponentDataType;
        private void ReAlloc(int requiredComponentTypeIndexNotModified)
        {
            var oldLength = dataArray.Length;
            if (requiredComponentTypeIndexNotModified < oldLength + SharedComponentTypeStart) return;
            var currentActualLength = TypeManager.GetTypeCount() - SharedComponentTypeStart;
            for (int i = currentActualLength - 1; i >= oldLength; --i)
            {
                var type = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!type.IsValueType || !ISharedComponentDataType.IsAssignableFrom(type)) continue;
                var tmp = new Element[i + 1];
                Buffer.BlockCopy(dataArray, 0, tmp, 0, dataArray.Length);
                dataArray = tmp;
                dataArray[i] = Element.Create(128, type);
                break;
            }
            for (int i = dataArray.Length - 2; i >= oldLength; --i)
            {
                var type = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!ISharedComponentDataType.IsAssignableFrom(type) || !type.IsValueType || (type.IsGenericType && type.IsGenericTypeDefinition)) continue;
#if REF_EQUATABLE
                var EqualsMethodInfo = type.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, CallingConventions.Any, EqualsTypeArray, null);
                if (EqualsMethodInfo == null || EqualsMethodInfo.DeclaringType != type) throw new Exception(type.FullName);
                var GetHashCodeMethodInfo = type.GetMethod("GetHashCode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, CallingConventions.Any, Array.Empty<Type>(), null);
                if (GetHashCodeMethodInfo == null || GetHashCodeMethodInfo.DeclaringType != type) throw new Exception(type.FullName);
#endif
                dataArray[i] = Element.Create(128, type);
            }
        }

        public SharedComponentDataManager()
        {
            var actualCount = TypeManager.GetTypeCount() - SharedComponentTypeStart;
            dataArray = new Element[actualCount];
            for (int i = 0; i < dataArray.Length; i++)
            {
                var type = TypeManager.GetType(i + SharedComponentTypeStart);
                if (!ISharedComponentDataType.IsAssignableFrom(type) || !type.IsValueType) continue;
                dataArray[i] = Element.Create(128, type);
            }
        }

        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            ReAlloc(typeIndex);
            ref var element = ref Accessor(typeIndex);
            var array = element.datas as T[];
            if (element.maxFreeIndex == -1)
            {
                for (int i = 0; i < element.actualLength; i++)
                    sharedComponentValues.Add(array[i]);
                return;
            }
            var enumerator = new Enumerator(element.freeIndices, element.maxFreeIndex);
            var index = 0;
            var existFree = enumerator.MoveNext();
            var count = element.actualLength;
            for (; existFree && index != count; ++index)
            {
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(array[index]);
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
                    sharedComponentValues.Add(array[index]);
                    continue;
                }
                else if (index == enumerator.Current)
                    continue;
                for (int i = index; i < count; i++)
                    sharedComponentValues.Add(array[i]);
                return;
            }
            if (index == count) return;
            for (int i = index; i < count; i++)
                sharedComponentValues.Add(array[i]);
        }
        public void GetAllUniqueSharedComponents<T>(List<T> sharedComponentValues, List<int> sharedComponentIndices) where T : struct, ISharedComponentData
        {
            sharedComponentValues.Add(default(T));
            sharedComponentIndices.Add(0);
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var dataIndex = typeIndex - SharedComponentTypeStart;
            ReAlloc(typeIndex);
            ref var element = ref dataArray[dataIndex];
            var array = element.datas as T[];
            var count = element.actualLength;
            if (element.maxFreeIndex == -1)
            {
                for (int i = 0; i < count; i++)
                {
                    sharedComponentValues.Add(array[i]);
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
                }
                return;
            }
            var index = 0;
            var enumerator = new Enumerator(element.freeIndices, element.maxFreeIndex);
            var existFree = enumerator.MoveNext();
            for (; existFree && index != count; ++index)
            {
                if (index < enumerator.Current)
                {
                    sharedComponentValues.Add(array[index]);
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
                    sharedComponentValues.Add(array[index]);
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, index));
                    continue;
                }
                else if (index == enumerator.Current)
                    continue;
                for (int i = index; i < count; i++)
                {
                    sharedComponentValues.Add(array[i]);
                    sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
                }
                return;
            }
            if (index == count) return;
            for (int i = index; i < count; i++)
            {
                sharedComponentValues.Add(array[i]);
                sharedComponentIndices.Add(ModifyIndex(typeIndex, i));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Element Accessor(int typeIndex) => ref dataArray[typeIndex - SharedComponentTypeStart];

        public int GetSharedComponentCount()
        {
            var answer = 1;
            for (int i = 0; i < dataArray.Length; i++)
                if (dataArray[i].datas != null)
                    answer += dataArray[i].actualLength;
            return answer;
        }
        public int GetSharedComponentCount<T>() => Accessor(TypeManager.GetTypeIndex<T>()).versions.Length + 1;

#if REF_EQUATABLE
        public int InsertSharedComponent<T>(ref T newData) where T : struct, ISharedComponentData, IRefEquatable<T>
        {
            if (default(T).Equals(ref newData))
                return 0;

            var typeIndex = TypeManager.GetTypeIndex<T>();

            var hashCode = newData.GetHashCode();
            ref var element = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, hashCode), ref newData, element.datas as T[]);
            if (index != -1)
            {
                ++element.referenceCounts[index];
                return ModifyIndex(typeIndex, index);
            }
            return ModifyIndex(typeIndex, Add(typeIndex, hashCode, ref newData, ref element));
        }
        private unsafe int FindNonDefaultSharedComponentIndex<T>(int typeIndex, ulong key, ref T newData, T[] list) where T : struct, ISharedComponentData, IRefEquatable<T>
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
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData)
        {
            ReAlloc(typeIndex);
            ref var element = ref Accessor(typeIndex);
            return InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, newData, element.datas, element.referenceCounts);
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, object newData, Array list)
        {
            ReAlloc(typeIndex);
            return InsertSharedComponentAssumeNonDefault(typeIndex, newData.GetHashCode(), newData, list, Accessor(typeIndex).referenceCounts);
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, Array list, int[] referenceCounts)
        {
            var key = CalcKey(typeIndex, hashCode);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, key, newData, list);
            if (index == -1)
                index = Add(typeIndex, hashCode, newData);
            else
                ++referenceCounts[index];
            return ModifyIndex(typeIndex, index);
        }
        private unsafe int FindNonDefaultSharedComponentIndex(int typeIndex, ulong key, object newData, Array list)
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                if (newData.Equals(list.GetValue(itemIndex)))
                    return itemIndex;
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        public int GetSharedComponentVersion<T>(ref T sharedData) where T : struct, ISharedComponentData, IRefEquatable<T>
        {
            if (default(T).Equals(ref sharedData))
                return 0;
            var typeIndex = TypeManager.GetTypeIndex<T>();
            ref var element = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, sharedData.GetHashCode()), ref sharedData, element.datas as T[]);
            return index == -1 ? 0 : element.versions[index];
        }
#else
        public int InsertSharedComponent<T>(ref T newData) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            {
                T defaultVal = default;
                if (FastEquality.Equals(ref defaultVal, ref newData, typeInfo))
                    return 0;
            }


            var hashCode = FastEquality.GetHashCode(ref newData, typeInfo);
            ref var element = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, hashCode), ref newData, typeInfo, element.datas as T[]);
            if (index != -1)
            {
                ++element.referenceCounts[index];
                return ModifyIndex(typeIndex, index);
            }
            return ModifyIndex(typeIndex, Add(typeIndex, hashCode, ref newData, ref element));
        }
        private unsafe int FindNonDefaultSharedComponentIndex(int typeIndex, ulong key, void* newDataPtr, FastEquality.TypeInfo typeInfo, Array array)
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                var dataPtr = PinGCObjectAndGetAddress(array.GetValue(itemIndex), out var dataHandle);
                if (FastEquality.Equals(dataPtr, newDataPtr, typeInfo))
                {
                    UnsafeUtility.ReleaseGCObject(dataHandle);
                    return itemIndex;
                }
                UnsafeUtility.ReleaseGCObject(dataHandle);
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        private unsafe int FindNonDefaultSharedComponentIndex<T>(int typeIndex, ulong key, ref T newData, FastEquality.TypeInfo typeInfo, T[] array) where T : struct, ISharedComponentData
        {
            if (!indexDictionary.TryGetFirstValue(key, out var itemIndex, out var iter))
                return -1;
            do
            {
                var data = array[itemIndex];
                if (FastEquality.Equals(ref newData, ref data, typeInfo))
                    return itemIndex;
            } while (indexDictionary.TryGetNextValue(out itemIndex, ref iter));
            return -1;
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, FastEquality.TypeInfo typeInfo)
        {
            ReAlloc(typeIndex);
            ref var element = ref Accessor(typeIndex);
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var answer = InsertSharedComponentAssumeNonDefault(typeIndex, hashCode, newData, ptr, typeInfo, element.datas, element.referenceCounts);
            UnsafeUtility.ReleaseGCObject(handle);
            return answer;
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, object newData, FastEquality.TypeInfo typeInfo, Array array)
        {
            ReAlloc(typeIndex);
            var ptr = PinGCObjectAndGetAddress(newData, out var handle);
            var answer = InsertSharedComponentAssumeNonDefault(typeIndex, FastEquality.GetHashCode(ptr, typeInfo), newData, ptr, typeInfo, array, Accessor(typeIndex).referenceCounts);
            UnsafeUtility.ReleaseGCObject(handle);
            return answer;
        }
        internal unsafe int InsertSharedComponentAssumeNonDefault(int typeIndex, int hashCode, object newData, void* ptr, FastEquality.TypeInfo typeInfo, Array list, int[] referenceCounts)
        {
            var key = CalcKey(typeIndex, hashCode);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, key, ptr, typeInfo, list);
            if (index == -1)
                index = Add(typeIndex, hashCode, newData);
            else
                referenceCounts[index] += 1;
            return ModifyIndex(typeIndex, index);
        }
        public int GetSharedComponentVersion<T>(ref T sharedData) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            {
                T defaultVal = default;
                if (FastEquality.Equals(ref defaultVal, ref sharedData, typeInfo))
                    return 0;
            }
            ref var element = ref Accessor(typeIndex);
            var index = FindNonDefaultSharedComponentIndex(typeIndex, CalcKey(typeIndex, FastEquality.GetHashCode(ref sharedData, typeInfo)), ref sharedData, typeInfo, element.datas as T[]);
            return index == -1 ? 0 : element.versions[index];
        }
#endif

        private static unsafe void* PinGCObjectAndGetAddress(object target, out ulong handle) => (byte*)UnsafeUtility.PinGCObjectAndGetAddress(target, out handle) + TypeManager.ObjectOffset;

        private int Add<T>(int typeIndex, int hashCode, ref T newData, ref Element element) where T : struct, ISharedComponentData
        {
            ReAlloc(typeIndex);
            int index;
            var list = element.datas as T[];
            if (element.maxFreeIndex == -1)
            {
                if (element.actualLength == element.versions.Length)
                    element.Lengthen<T>();
                index = element.actualLength++;
            }
            else
            {
                index = DropMaxFreeIndex(element.freeIndices, ref element.maxFreeIndex);
            }
            indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
            list[index] = newData;
            element.versions[index] = element.referenceCounts[index] = 1;
            return index;
        }

        private int Add(int typeIndex, int hashCode, object newData)
        {
            ReAlloc(typeIndex);
            int index;
            ref var element = ref Accessor(typeIndex);
            if (element.maxFreeIndex == -1)
            {
                if (element.actualLength == element.versions.Length)
                    element.Lengthen(TypeManager.GetType(typeIndex));
                index = element.actualLength++;
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
            }
            else
            {
                index = DropMaxFreeIndex(element.freeIndices, ref element.maxFreeIndex);
                indexDictionary.Add(CalcKey(typeIndex, hashCode), index);
            }
            element.datas.SetValue(newData, index);
            element.versions[index] = element.referenceCounts[index] = 1;
            return index;
        }

        public void IncrementSharedComponentVersion(int modifiedIndex)
        {
            if (DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex))
                ++Accessor(typeIndex).versions[actualIndex];
        }

        public int GetSharedComponentVersion<T>(T sharedData) where T : struct, ISharedComponentData
#if REF_EQUATABLE
        , IRefEquatable<T>
#endif
        => GetSharedComponentVersion(ref sharedData);

        public T GetSharedComponentData<T>(int index) where T : struct
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (DeconstructIndex(index, typeIndex, out var actualIndex))
                return (Accessor(typeIndex).datas as T[])[actualIndex];
            return default;
        }

        public object GetSharedComponentDataBoxed(int index, int typeIndex)
            => DeconstructIndex(index, typeIndex, out var actualIndex) ? Accessor(typeIndex).datas.GetValue(actualIndex) : Activator.CreateInstance(TypeManager.GetType(typeIndex));

        public object GetSharedComponentDataNonDefaultBoxed(int index)
        {
            Assert.AreNotEqual(0, index);
            DeconstructIndex(index, out var typeIndex, out var actualIndex);
            return Accessor(typeIndex).datas.GetValue(actualIndex);
        }

        public void AddReference(int modifiedIndex)
        {
            if (DeconstructIndex(modifiedIndex, out var typeIndex, out var actualIndex))
                ++Accessor(typeIndex).referenceCounts[actualIndex];
        }

        public unsafe void RemoveReference<T>(int modifiedIndex) where T : struct, ISharedComponentData
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (!DeconstructIndex(modifiedIndex, typeIndex, out var actualIndex)) return;
            ref var element = ref Accessor(typeIndex);
            var newCount = --element.referenceCounts[actualIndex];
            Assert.IsTrue(newCount >= 0);

            if (newCount != 0)
                return;
#if REF_EQUATABLE
            var hashCode = (element.datas as T[])[actualIndex].GetHashCode();
#else
            var hashCode = FastEquality.GetHashCode(ref (element.datas as T[])[actualIndex], TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo);
#endif
            AddFreeIndex(actualIndex, element.freeIndices, ref element.maxFreeIndex);

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
            var data = element.datas.GetValue(actualIndex);
#if REF_EQUATABLE
            var hashCode = data.GetHashCode();
#else
            var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
            var ptr = PinGCObjectAndGetAddress(data, out var handle);
            var hashCode = FastEquality.GetHashCode(ptr, typeInfo);
            UnsafeUtility.ReleaseGCObject(handle);
#endif

            AddFreeIndex(actualIndex, element.freeIndices, ref element.maxFreeIndex);
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
                var list = dataArray[i].datas;
                if (list == null) continue;
                int maxFreeIndex = dataArray[i].maxFreeIndex;
                if (dataArray[i].actualLength != maxFreeIndex + 1) return false;
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
                if (referenceCounts == null || referenceCounts[actualIndex] != referenceCount) return false;
            }
            return true;
        }

#if REF_EQUATABLE
        internal unsafe NativeHashMap<int, int> MoveAllSharedComponents(SharedComponentDataManager srcSharedComponents, Allocator allocator)
        {
            var remap = new NativeHashMap<int, int>(srcSharedComponents.GetSharedComponentCount(), allocator);
            remap.TryAdd(0, 0);
            var srcArray = srcSharedComponents.dataArray;
            for (int i = 0; i < srcArray.Length; i++)
            {
                ref var tuple = ref srcArray[i];
                if (tuple.datas == null) continue;
                var typeIndex = SharedComponentTypeStart + i;
                var thisRefCounts = Accessor(typeIndex).referenceCounts;
                for (int j = 0; j < tuple.actualLength; j++)
                {
                    var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, tuple.datas.GetValue(j), tuple.datas);
                    remap.TryAdd(ModifyIndex(typeIndex, j), dstIndex);
                    if (DeconstructIndex(dstIndex, typeIndex, out var actualIndex))
                        thisRefCounts[actualIndex] += tuple.referenceCounts[j] - 1;
                    else throw new Exception();
                }
            }
            srcSharedComponents.PrepareForDeserialize_Inner();
            return remap;
        }
#else
        internal unsafe NativeHashMap<int, int> MoveAllSharedComponents(SharedComponentDataManager srcSharedComponents, Allocator allocator)
        {
            var remap = new NativeHashMap<int, int>(srcSharedComponents.GetSharedComponentCount(), allocator);
            remap.TryAdd(0, 0);
            ref var srcArray = ref srcSharedComponents.dataArray;
            for (int i = 0; i < srcArray.Length; i++)
            {
                ref var tuple = ref srcArray[i];
                if (tuple.datas == null) continue;
                var typeIndex = SharedComponentTypeStart + i;
                var typeInfo = TypeManager.GetTypeInfo(typeIndex).FastEqualityTypeInfo;
                var thisRefCounts = Accessor(typeIndex).referenceCounts;
                for (int j = 0; j < tuple.actualLength; j++)
                {
                    var dstIndex = InsertSharedComponentAssumeNonDefault(typeIndex, tuple.datas.GetValue(j), typeInfo, tuple.datas);
                    remap.TryAdd(ModifyIndex(typeIndex, j), dstIndex);
                    if (DeconstructIndex(dstIndex, typeIndex, out var actualIndex))
                        thisRefCounts[actualIndex] += tuple.referenceCounts[j] - 1;
                    else throw new Exception();
                }
            }
            srcSharedComponents.PrepareForDeserialize_Inner();
            return remap;
        }
#endif
        private void PrepareForDeserialize_Inner()
        {
            indexDictionary.Clear();
            for (int i = 0; i < dataArray.Length; i++)
            {
                ref var element = ref dataArray[i];
                if (element.datas == null) continue;
                element.maxFreeIndex = -1;
                element.actualLength = 0;
                unsafe
                {
                    fixed (void* ptr = element.freeIndices)
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