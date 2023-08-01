using UnityEngine;

namespace Wargon.Ecsape {
    using System;
    using System.Runtime.CompilerServices;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public struct frr {

    }
    public static class Extensions {


        public struct frEach<T, V, C> 
            where T: struct,IComponent
            where C: struct,IComponent
            where V: struct,IComponent {
            public IPool<T> pT;
            public IPool<C> pC;
            public IPool<V> pV;

            public void forEach(Action<T, V, C> eAction) { }
        }
        public static frEach<T, V, C> forEach<T, V, C>(this (T t, V v, C c) each) 
            where T: struct,IComponent
            where C: struct,IComponent
            where V: struct,IComponent 
        {
            var pT = World.Default.GetPool<T>();
            var pC = World.Default.GetPool<C>();
            var pV = World.Default.GetPool<V>();

            frEach<T, V, C> fr;
            fr.pT = World.Default.GetPool<T>();
            fr.pC = World.Default.GetPool<C>();
            fr.pV = World.Default.GetPool<V>();
            return fr;
        }
        
        public static Vector3 Random(float min, float max) {
            var n = UnityEngine.Random.Range(min, max);
            return new Vector3(n, n, n);
        }
        public static Quaternion RandomZ(float min, float max) {
            var n = UnityEngine.Random.Range(min, max);
            return Quaternion.Euler(new Vector3(0,0,n));
        }
        public static T Last<T>(this System.Collections.Generic.List<T> list) {
            return list[^1];
        }

        public static List<T> RemoveNulls<T>(this List<T> list) where T : class {
            for (var i = 0; i < list.Count; i++) {
                var item = list[i];
                if (item is null) list.RemoveAt(i);
            }

            return list;
        }

        public static void LogElementsToConsole<T>(this List<T> list) {
            for (var i = 0; i < list.Count; i++) {
                UnityEngine.Debug.Log(list[i].ToString());
            }
        }
        public static T RandomElement<T>(this System.Collections.Generic.List<T> list) {
            var random = new Random();
            return list[random.Next(list.Count)];
        }
        public static void RemoveLast(this System.Collections.IList list) {
            list.RemoveAt(list.Count - 1);
        }
        public static bool ConstainsType(this List<IComponent> list, object item) {
            var type = item.GetType();
            for (var i = 0; i < list.Count; i++) {
                if (list[i].GetType() == type) return true;
                
            }
            return false;
        }
        public static bool ConstainsType(this List<ComponentData> list, object item) {
            var type = item.GetType();
            for (var i = 0; i < list.Count; i++) {
                if (list[i].ComponentType == type) return true;
                
            }
            return false;
        }
        public static bool ConstainsType(this List<object> list, object item) {
            var type = item.GetType();
            for (var i = 0; i < list.Count; i++) {
                if (list[i].GetType() == type) return true;
                
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntEnumerator GetEnumerator(this Range range) {
            return new IntEnumerator(range);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntEnumerator GetEnumerator(this int number) {
            return new IntEnumerator(new Range(0,number-1));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativePool<T> AsNative<T>(this IPool<T> pool) where T : unmanaged, IComponent {
            return new NativePool<T>(pool, Allocator.TempJob);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeQuery AsNative(this Query query) {
            return new NativeQuery(query);
        }
        
        public static NativePool<T> GetPoolNative<T>(this World world) where T : unmanaged, IComponent {
            return world.GetPool<T>().AsNative();
        }

        internal unsafe static T* AsPtr<T>(this ref Vector<T> vector) where T : unmanaged {
            return (T*)vector.impl->data;
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


    public unsafe interface INativeContainer<T1>  where T1 : unmanaged {
        void UpdateData(T1* ptr1, int* ptr2);
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativePool<T> : INativeContainer<T> where T : unmanaged, IComponent {
        private int count;
        [NativeDisableUnsafePtrRestriction] private T* data;
        [NativeDisableUnsafePtrRestriction] private int* entities;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativePool(IPool<T> pool, Allocator allocator) {
            fixed(T* dataPtr = pool.GetRawData())
            fixed (int* entitiesPtr = pool.GetRawEntities()) {
                data = dataPtr;
                entities = entitiesPtr;
            }
            count = pool.Count;
        }

        public void UpdateData(T* ptr1, int* ptr2) {
            data = ptr1;
            entities = ptr2;
        }
        public ref T Get(int entity) {
            return ref data[entities[entity]];
        }

        public void* GetPtr() {
            return UnsafeUtility.AddressOf(ref this);
        }
        public T* GetPtr(int entity) {
            return &data[entities[entity]];
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
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeQuery {
        [NativeDisableUnsafePtrRestriction] private int* entities;
        [NativeDisableUnsafePtrRestriction] private int* entityMap;
        private int count;
        public int Count => count;
        public bool IsEmpty => count == 0;
        public NativeQuery(Query query) {
            entities = query.GetEntitiesPtr();
            entityMap = query.GetEntitiesMapPtr();
            count = query.count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntity(int index) {
            return entities[index];
        }
        public Enumerator GetEnumerator() {
            return new Enumerator(in this);
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
                get => Query.GetEntity(index);
            }
        }
    }

    public static class NativeMagic
    {
        public static unsafe ref T As<T>(void* ptr) where T : unmanaged {
            return ref UnsafeUtility.AsRef<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetArrayPtr<T>(T[] data) where T : unmanaged
        {
            fixed (T* ptr = data)
            {
                return ptr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetPtr<T>(this T[] data) where T : unmanaged
        {
            fixed (T* ptr = data)
            {
                return ptr;
            }
        }

        public static unsafe T* Resize<T>(T* ptr, int newSize, int oldSize, Allocator allocator) where T : unmanaged {
            T* newPointer = null;
            var alignOf = UnsafeUtility.AlignOf<T>();
            var sizeOf = sizeof(T);

            if (newSize > 0)
            {
                newPointer = (T*)UnsafeUtility.Malloc(sizeOf*newSize, alignOf, allocator);

                if (oldSize > 0)
                {
                    var itemsToCopy = Math.Min(newSize, oldSize);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, ptr, bytesToCopy);
                }
            }
            UnsafeUtility.Free(ptr, allocator);
            ptr = newPointer;
            return ptr;
        }
        
        // public static unsafe NativeArray<T> WrapToNative<T>(ref T[] managedData) where T : unmanaged {
        //     fixed (void* ptr = managedData) {
        //         return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, managedData.Length, Allocator.TempJob);
        //     }
        // }
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
            return AsSpan().ToString();
        }

        public Span<char> AsSpan() {
            return new Span<char>(letters, Lenght);
        }
    }

    public static class SystemsExtensions {
        public static Systems AddGen<TSystem>(this Systems systems, TSystem system) where TSystem: ISystem {
            var onUpdateInfo = system.GetType().GetMethod("OnUpdate");
            //RuntimeHelpers.PrepareMethod(onUpdateInfo.MethodHandle);

            return systems;
        }
    }

    public unsafe struct Vector<T> where T : unmanaged {
        internal Internal* impl;
        internal struct Internal {
            internal void* data;
            internal int count;
            internal int size;
            public Internal(int amount) {
                long sizeInBytes = UnsafeUtility.SizeOf(typeof(T)) * amount;
                data = UnsafeUtility.Malloc(sizeInBytes, UnsafeUtility.AlignOf<T>(), Allocator.Persistent);
                count = 0;
                size = amount;
            }
    
            public static Internal* New(int amount) {
                
                var ptr = (Internal*) UnsafeUtility.Malloc(sizeof(Internal),UnsafeUtility.AlignOf<Internal>(), Allocator.Persistent);
                ptr->data = UnsafeUtility.Malloc(UnsafeUtility.SizeOf(typeof(T)) * amount, UnsafeUtility.AlignOf<T>(), Allocator.Persistent);
                ptr->count = 0;
                ptr->size = amount;
                return ptr;
            }
    
            public void Add(T item) {
                ResizeIfNeed(index:count);
                UnsafeUtility.WriteArrayElement(data, count, item);
                count++;
            }
    
            public ref T Get(int index){
                return ref UnsafeUtility.ArrayElementAsRef<T>(data, index);
            }
    
            public void Set(int index, T item) {
                ResizeIfNeed(index:index);
                UnsafeUtility.WriteArrayElement(data, index, item);
            }
            private void ResizeIfNeed(int index) {
                if (size <= index - 1) {
                    var newSize = size * 2;
                    data = UnsafeHelp.ResizeUnsafeUtility<T>(data,size, newSize, Allocator.Persistent);
                    size = newSize;
                }
            }
    
            public void Resize(int newSize) {
                data = UnsafeHelp.ResizeUnsafeUtility<T>(data,size, newSize, Allocator.Persistent);
                size = newSize;
            }
    
            public void Clear() {
                UnsafeUtility.MemClear(data, UnsafeUtility.SizeOf(typeof(T)) * size);
            }
        }
        
        public int Length => impl->size;
        
        public Vector(int amount) {
            impl = Internal.New(amount);
        }
    
        public void Add(T item) {
            impl->Add(item);
        }
    
        public ref T Get(int index) {
            return ref impl->Get(index);
        }
    
        public void Set(int index, T item) {
            impl->Set(index, item);
        }
    
        public void Resize(int newSize) {
            impl->Resize(newSize);
        }
    
        public void Clear() {
            impl->Clear();
            Marshal.FreeHGlobal((IntPtr)impl);
        }
    }
}