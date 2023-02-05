using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Ecsape {
    using System;
    using System.Runtime.CompilerServices;
    public static class Extensions {

        public static T Last<T>(this System.Collections.Generic.List<T> list) {
            return list[^1];
        }

        public static void RemoveLast(this System.Collections.IList list) {
            list.RemoveAt(list.Count - 1);
        }

        public static IntEnumerator GetEnumerator(this Range range) {
            return new IntEnumerator(range);
        }
        public static IntEnumerator GetEnumerator(this int number) {
            return new IntEnumerator(new Range(0,number-1));
        }

        public static NativePool<T> AsNative<T>(this IPool<T> pool) where T : struct, IComponent {
            return new NativePool<T>(pool, Allocator.TempJob);
        }

        public static NativeQuery AsNative(this Query query) {
            query.WorldInternal.AddDirtyQuery(query);
            return new NativeQuery(query, Allocator.TempJob);
        }

        public static NativePool<T> GetPoolNative<T>(this World world) where T : struct, IComponent {
            return world.GetPool<T>().AsNative();
        }
    }

    public ref struct IntEnumerator {
        private int current;
        private readonly int end;
        public IntEnumerator(Range range) {
            if (range.End.IsFromEnd) {
                throw new NotSupportedException();
            }
            current = range.Start.Value - 1;
            end = range.End.Value;
        }

        public int Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => current;
        }
        public bool MoveNext() {
            current++;
            return current <= end;
        }
    }
    public struct NativePool<T> where T : struct, IComponent {

        internal int count;
        internal NativeArray<T> data;
        internal NativeArray<int> entities;

        public NativePool(IPool<T> pool, Allocator allocator) {
            data = new NativeArray<T>(pool.GetRawData(), allocator);
            entities = new NativeArray<int>(pool.GetRawEntities(), allocator);
            count = pool.Count;
        }
        public T Get(int entity) {
            return data[entities[entity]];
        }
        
        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }

        public void Add(int entity) {
            entities[entity] = count;
            data[count] = default;
            count++;
        }
        public void Add(in T component, int entity) {
            entities[entity] = count;
            data[count] = component;
            count++;
        }
        public void Remove(int entity) {
            entities[entity] = 0;
            count--;
        }
    }


    public struct NativeQuery {
        internal NativeArray<int> entities;
        internal NativeArray<int> entityMap;
        internal NativeArray<Query.EntityToUpdate> entityToUpdates;
        internal NativeMask with;
        internal NativeMask without;
        internal int count;
        public NativeQuery(Query query, Allocator allocator) {
            var (e,map,updates,c) = query.GetRaw();
            entities = new NativeArray<int>(e, allocator);
            entityMap = new NativeArray<int>(map, allocator);
            entityToUpdates = new NativeArray<Query.EntityToUpdate>(updates, allocator);
            count = c;
            with = new NativeMask(in query.with);
            without = new NativeMask(in query.without);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Entity(int index) {
            return entities[index];
        }

        public ref struct Enumerator {
            private NativeQuery Query;
            private int index;

            public Enumerator(in NativeQuery query) {
                Query = query;
                index = -1;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                index++;
                return index < Query.count;
            }

            public void Reset() {
                index = -1;
            }
        
            public int Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Query.Entity(index);
            }
        }
    }

    internal struct NativeMask {
        private readonly NativeArray<int>.ReadOnly items;
        private int Count;
        public NativeMask(in Mask mask) {
            items = new NativeArray<int>(mask.Types, Allocator.TempJob).AsReadOnly();
            Count = mask.Count;
        }
    }
    
    public interface IPoolObserver {
        void OnAddWith(int entity);
        void OnRemoveWith(int entity);
    }
    
    internal static class NativeMagic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetArrayPtr<T>(T[] data) where T : unmanaged
        {
            fixed (T* ptr = data)
            {
                return ptr;
            }
        }

        public static unsafe NativeArray<T> WrapToNative<T>(ref T[] managedData) where T : unmanaged {
            fixed (void* ptr = managedData) {
                return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, managedData.Length, Allocator.TempJob);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NativeWrappedData<T> WrapToNative<T>(T[] managedData) where T : unmanaged
        {
            fixed (void* ptr = managedData)
            {
#if UNITY_EDITOR
                var nativeData = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, managedData.Length, Allocator.TempJob);
                var sh = AtomicSafetyHandle.Create();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeData, sh);
                return new NativeWrappedData<T> {Array = nativeData, SafetyHandle = sh};
#else
            return new NativeWrappedData<T> { Array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T> (ptr, managedData.Length, Allocator.None) };
#endif
            }
        }
#if UNITY_EDITOR
        public static void UnwrapFromNative<T>(NativeWrappedData<T> sh) where T : unmanaged
        {
            AtomicSafetyHandle.CheckDeallocateAndThrow(sh.SafetyHandle);
            AtomicSafetyHandle.Release(sh.SafetyHandle);
        }
#endif
        public static NativePool<T> WrapToNative<T>(IPool<T> pool) where T : unmanaged, IComponent {
            NativePool<T> nativePool;
            var rawData = pool.GetRawData();
            nativePool.data = WrapToNative(ref rawData);
            var rawEntities = pool.GetRawEntities();
            nativePool.entities = WrapToNative(ref rawEntities);
            nativePool.count = pool.Count;
            return nativePool;
        }
        public static NativeQuery WrapToNative<T>(Query query) where T : unmanaged, IComponent {
            var (e,map,updates,c) = query.GetRaw();
            NativeQuery nativeQuery;
            nativeQuery.entities = WrapToNative(ref e);
            nativeQuery.entityMap = WrapToNative(ref map);
            nativeQuery.with = new NativeMask(in query.with);
            nativeQuery.without = new NativeMask(in query.without);
            nativeQuery.entityToUpdates = WrapToNative(ref updates);
            nativeQuery.count = c;
            return nativeQuery;
        }
    }

    public struct NativeWrappedData<TT> where TT : unmanaged
    {
        [NativeDisableParallelForRestriction] public NativeArray<TT> Array;
#if UNITY_EDITOR
        public AtomicSafetyHandle SafetyHandle;
#endif
    }
    
    public readonly unsafe struct NativeString {
        private readonly char* letters;
        public readonly int Lenght;
        public NativeString(string source) {
            letters = (char*)Marshal.AllocHGlobal(sizeof(char)*source.Length);
            for (var i = 0; i < source.Length; i++) {
                letters[i] = source[i];
            }

            Lenght = source.Length;
        }

        public override string ToString() {
            var str = string.Empty;
            for (int i = 0; i < Lenght; i++) {
                str += letters[i];
            }
            return str;
        }
    }
}