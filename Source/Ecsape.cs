using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Ecsape.Components;
using Wargon.Ecsape.Pools;
namespace Wargon.Ecsape {

    public struct Option {
        public const int INLINE = 256;
    }
    
    public static class Generic {
        public static object New(Type genericType, Type elementsType, params object[] parameters) {
            return Activator.CreateInstance(genericType.MakeGenericType(elementsType), parameters);
        }
    }

    namespace Pools {
        public interface IPool {
            int Count { get; }
            int Capacity { get; }
            ComponentType Info { get; }
            void Add(int entity);
            void AddBoxed(object component, int entity);
            unsafe void AddPtr(void* component, int entity);
            void SetBoxed(object component, int entity);
            void Remove(int entity);
            bool Has(int entity);
            
            static IPool New(int size, int typeIndex) {

                ref var info = ref Component.GetComponentType(typeIndex);
                var componentType = Component.GetTypeOfComponent(typeIndex);
                var isTagPool = info.IsTag || info.IsSingletone || info.IsEvent;
                var isDisposablePool = info.IsDisposable;
                var isOnCreatePool = info.IsOnCreate;
                //var isUnmanagedPool = !isTagPool && !isDisposablePool && !isOnCreatePool && info.IsUnmanaged;
                var poolType = isTagPool ? typeof(TagPool<>)
                    : isDisposablePool ? typeof(DisposablePool<>) : isOnCreatePool ? typeof(OnCreatePool<>)  : typeof(Pool<>);
                var pool = (IPool) Generic.New(poolType, componentType, size);
                // if (isUnmanagedPool) {
                //     Debug.Log($"unamanged : {componentType.Name}");
                // }
                return pool;
            }

            void Resize(int newSize);
            object GetRaw(int index);
            void Clear();
        }
    }

    public interface IPool<T> : IPool where T : struct, IComponent {
        ref T Get(int entity);
        ref T Get(ref Entity entity);
        void Set(in T component, int entity);
        void Add(in T component, int entity);
        T[] GetRawData();
        int[] GetRawEntities();
    }

        internal class Pool<T> : IPool<T> where T : struct, IComponent {
        private readonly IPool self;
        private int count;
        private T[] data;
        public int Capacity => data.Length;
    
        public int Count => count - 1;
        [MethodImpl(256)]
        public Pool(int size) {
            data = new T[size];
            Info = Component<T>.AsComponentType();
            count = 1;
            self = this;
        }
        [MethodImpl(256)]
        public ref T Get(int entity) {
            return ref data[entity];
        }
        [MethodImpl(256)]
        public ref T Get(ref Entity entity) {
            return ref data[entity.Index];
        }
        [MethodImpl(256)]
        public void Set(in T component, int entity) {
            data[entity] = component;
        }
        public void SetBoxed(object component, int entity) {
            data[entity] = (T)component;
        }
        [MethodImpl(256)]
        public void Add(int entity) {
            data[entity] = default;
            count++;
        }
        [MethodImpl(256)]
        public void Add(in T component, int entity) {
            data[entity] = component;
            count++;
        }
    
        [MethodImpl(256)]
        public void AddBoxed(object component, int entity) {
            data[entity] = (T)component;
            count++;
        }
        [MethodImpl(256)]
        public unsafe void AddPtr(void* component, int entity) {
            data[entity] = Marshal.PtrToStructure<T>((IntPtr)component);
            count++;
        }
        [MethodImpl(256)]
        public void Remove(int entity) {
            data[entity] = default;
            count--;
        }
        [MethodImpl(256)]
        bool IPool.Has(int entity) {
            return false;
        }
    
        [MethodImpl(256)]
        void IPool.Resize(int newSize) {
            Array.Resize(ref data, newSize);
        }
    
        public ComponentType Info { get; }
    
        object IPool.GetRaw(int index) {
            return Get(index);
        }

        public void Clear() {
            Array.Clear(data, 0, data.Length);
        }

        [MethodImpl(256)]
        public T[] GetRawData() {
            return data;
        }
        [MethodImpl(256)]
        public int[] GetRawEntities() {
            return Array.Empty<int>();
        }
    
        public IPool AsIPool() {
            return self;
        }
    }
        
    // internal class Pool<T> : IPool<T> where T : struct, IComponent {
    //     private readonly IPool self;
    //     private int count;
    //     private T[] data;
    //     private int[] entities;
    //     public int Capacity => data.Length;
    //
    //     public int Count => count - 1;
    //     [MethodImpl(256)]
    //     public Pool(int size) {
    //         data = new T[size];
    //         entities = new int[size];
    //         Info = Component<T>.AsComponentType();
    //         count = 1;
    //         self = this;
    //     }
    //     [MethodImpl(256)]
    //     public ref T Get(int entity) {
    //         return ref data[entities[entity]];
    //     }
    //     [MethodImpl(256)]
    //     public ref T Get(ref Entity entity) {
    //         return ref data[entities[entity.Index]];
    //     }
    //     [MethodImpl(256)]
    //     public void Set(in T component, int entity) {
    //         data[entities[entity]] = component;
    //     }
    //     public void SetBoxed(object component, int entity) {
    //         data[entities[entity]] = (T)component;
    //     }
    //     [MethodImpl(256)]
    //     public void Add(int entity) {
    //         if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
    //         entities[entity] = count;
    //         data[count++] = default;
    //     }
    //     [MethodImpl(256)]
    //     public void Add(in T component, int entity) {
    //         if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
    //         entities[entity] = count;
    //         data[count++] = component;
    //     }
    //
    //     [MethodImpl(256)]
    //     public void AddBoxed(object component, int entity) {
    //         if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
    //         entities[entity] = count;
    //         data[count++] = (T)component;
    //     }
    //     [MethodImpl(256)]
    //     public unsafe void AddPtr(void* component, int entity) {
    //         if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
    //         entities[entity] = count;
    //         data[count++] = Marshal.PtrToStructure<T>((IntPtr)component);
    //     }
    //     [MethodImpl(256)]
    //     public void Remove(int entity) {
    //         data[entities[entity]] = default;
    //         entities[entity] = 0;
    //         count--;
    //     }
    //     [MethodImpl(256)]
    //     bool IPool.Has(int entity) {
    //         return entities[entity] > 0;
    //     }
    //
    //     [MethodImpl(256)]
    //     void IPool.Resize(int newSize) {
    //         Array.Resize(ref entities, newSize);
    //     }
    //
    //     public ComponentType Info { get; }
    //
    //     object IPool.GetRaw(int index) {
    //         return Get(index);
    //     }
    //
    //     public void Clear() {
    //         Array.Clear(data, 0, data.Length);
    //         Array.Clear(entities, 0, entities.Length);
    //     }
    //
    //     [MethodImpl(256)]
    //     public T[] GetRawData() {
    //         return data;
    //     }
    //     [MethodImpl(256)]
    //     public int[] GetRawEntities() {
    //         return entities;
    //     }
    //
    //     public IPool AsIPool() {
    //         return self;
    //     }
    // }
    
    internal class TagPool<T> : IPool<T> where T : struct, IComponent {
        private readonly IPool self;
        private int count;
        private T data;
        private int entities;
        public int Capacity => 1;
        public int Count => count - 1;
        public TagPool(int size) {
            data = default;
            entities = 1;
            Info = Component<T>.AsComponentType();
            count = 1;
            self = this;
        }

        void IPool.Clear() {
            
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int entity) {
            return ref data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(ref Entity entity) {
            return ref data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(in T component, int entity) { }

        public void SetBoxed(object component, int entity) {
            data = (T) component;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entity) {
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T component, int entity) {
            data = component;
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddPtr(void* component, int entity) {
            data = Marshal.PtrToStructure<T>((IntPtr)component);
            count++;
        }
        public T[] GetRawData() {
            return new []{data};
        }

        public int[] GetRawEntities() {
            return new []{0};
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBoxed(object component, int entity) {
            data = (T)component;
            count++;
        }

        public void Remove(int entity) {
            count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPool.Has(int entity) {
            return entities > 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPool.Resize(int newSize) {
            
        }

        public ComponentType Info { get; }

        object IPool.GetRaw(int index) {
            return Get(index);
        }

        public IPool AsIPool() {
            return self;
        }
    }

    internal class OnCreatePool<T> : IPool<T> where T : struct, IComponent, IOnAddToEntity {
        private readonly IPool self;
        private int count;
        private T[] data;
        private int[] entities;
        public int Capacity => data.Length;

        public int Count => count - 1;
        public OnCreatePool(int size) {
            data = new T[size];
            entities = new int[size];
            Info = Component<T>.AsComponentType();
            count = 1;
            self = this;
        }
        
        void IPool.Clear() {
            Array.Clear(data, 0, data.Length);
            Array.Clear(entities, 0, entities.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int entity) {
            return ref data[entities[entity]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(ref Entity entity) {
            return ref data[entities[entity.Index]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }
        public void SetBoxed(object component, int entity) {
            data[entities[entity]] = (T)component;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            var c = default(T);
            c.OnAdd();
            data[count] = c;
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = component;
            data[count].OnAdd();
            count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBoxed(object component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = (T)component;
            data[count].OnAdd();
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddPtr(void* component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = Marshal.PtrToStructure<T>((IntPtr)component);
            data[count].OnAdd();
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entity) {
            entities[entity] = 0;
            count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPool.Has(int entity) {
            return entities[entity] > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPool.Resize(int newSize) {
            Array.Resize(ref entities, newSize);
        }

        public ComponentType Info { get; }

        object IPool.GetRaw(int index) {
            return Get(index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] GetRawData() {
            return data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] GetRawEntities() {
            return entities;
        }

        public IPool AsIPool() {
            return self;
        }
    }

    internal class DisposablePool<T> : IPool<T> where T : struct, IComponent, IDisposable {
        private readonly IPool self;
        private int count;
        private T[] data;
        private int[] entities;
        public int Capacity => data.Length;
        public DisposablePool(int size) {
            data = new T[size];
            entities = new int[size];
            Info = Component<T>.AsComponentType();
            count = 1;
            self = this;
        }
        
        void IPool.Clear() {
            Array.Clear(data, 0, data.Length);
            Array.Clear(entities, 0, entities.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int entity) {
            return ref data[entities[entity]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(ref Entity entity) {
            return ref data[entities[entity.Index]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }
        public void SetBoxed(object component, int entity) {
            data[entities[entity]] = (T)component;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = component;
            count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBoxed(object component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = (T)component;
            count++;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddPtr(void* component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = Marshal.PtrToStructure<T>((IntPtr)component);;
            count++;
        }
        
        public void Remove(int entity) {
            data[entities[entity]].Dispose();
            entities[entity] = 0;
            count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPool.Has(int entity) {
            return entities[entity] > 0;
        }

        public int Count => count - 1;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPool.Resize(int newSize) {
            Array.Resize(ref entities, newSize);
        }

        public ComponentType Info { get; }

        object IPool.GetRaw(int index) {
            return Get(index);
        }
        public T[] GetRawData() {
            return data;
        }

        public int[] GetRawEntities() {
            return entities;
        }

        public IPool AsIPool() {
            return self;
        }
    }

    internal sealed class UnmanagedPool<T> : IPool<T> where T : unmanaged, IComponent {

        private UnsafeList<T> data;
        public int Count { get; }
        public int Capacity { get; }
        public ComponentType Info { get; }
        public void Add(int entity) {
            data.Add(default(T));
        }

        public void AddBoxed(object component, int entity) {
            throw new NotImplementedException();
        }

        public unsafe void AddPtr(void* component, int entity) {
            throw new NotImplementedException();
        }

        public void SetBoxed(object component, int entity) {
            throw new NotImplementedException();
        }

        public void Remove(int entity) {
            throw new NotImplementedException();
        }

        public bool Has(int entity) {
            throw new NotImplementedException();
        }

        public void Resize(int newSize) {
            throw new NotImplementedException();
        }

        public object GetRaw(int index) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public ref T Get(int entity) {
            throw new NotImplementedException();
        }

        public ref T Get(ref Entity entity) {
            throw new NotImplementedException();
        }

        public void Set(in T component, int entity) {
            throw new NotImplementedException();
        }

        public void Add(in T component, int entity) {
            throw new NotImplementedException();
        }

        public T[] GetRawData() {
            throw new NotImplementedException();
        }

        public int[] GetRawEntities() {
            throw new NotImplementedException();
        }
    }

    internal sealed class DirtyQueries {
        private readonly Query[] items;
        private int count;

        public DirtyQueries(int size) {
            items = new Query[size];
            count = 0;
        }

        public void Add(Query query) {
            items[count] = query;
            count++;
        }

        public void RemoveLast() {
            count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateQueries() {
            if (count < 1) return;
            for (var i = 0; i < count; i++) items[i].Update();
            count = 0;
        }
    }


    namespace Serializable {
        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Vector2 {
            public float x;
            public float y;
        
            public Vector2(float x, float y) {
                this.x = x;
                this.y = y;
            }
        
            public static implicit operator UnityEngine.Vector3(Vector2 rValue)
            {
                return new UnityEngine.Vector3(rValue.x, rValue.y, 0F);
            }
        
            public static implicit operator UnityEngine.Vector2(Vector2 rValue)
            {
                return new UnityEngine.Vector2(rValue.x, rValue.y);
            }
            public static implicit operator Vector2(UnityEngine.Vector2 rValue)
            {
                return new Vector2(rValue.x, rValue.y);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Vector3 {
            public float x;
            public float y;
            public float z;

            public Vector3(float x, float y, float z) {
                this.x = x;
                this.y = y;
                this.z = z;
            }
            public static implicit operator UnityEngine.Vector2(Vector3 rValue)
            {
                return new UnityEngine.Vector2(rValue.x, rValue.y);
            }
            public static implicit operator UnityEngine.Vector3(Vector3 rValue)
            {
                return new UnityEngine.Vector3(rValue.x, rValue.y, rValue.z);
            }
            public static implicit operator Vector3(UnityEngine.Vector3 rValue)
            {
                return new Vector3(rValue.x, rValue.y, rValue.z);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [Serializable]
        public struct Quaternion {
            public float x;
            public float y;
            public float z;
            public float w;

            public Quaternion(float rX, float rY, float rZ, float rW)
            {
                x = rX;
                y = rY;
                z = rZ;
                w = rW;
            }
            
            public static implicit operator UnityEngine.Quaternion(Quaternion rValue)
            {
                return new UnityEngine.Quaternion(rValue.x, rValue.y, rValue.z, rValue.w);
            }
            public static implicit operator Quaternion(UnityEngine.Quaternion rValue)
            {
                return new Quaternion(rValue.x, rValue.y, rValue.z, rValue.w);
            }
        }
    }


    public interface IAspect {
        IEnumerable<Type> Link();

        IEnumerable<Type> Create(params Type[] types) {
            return types;
        }
    }

    public interface IAspect<T> : IAspect where T : struct {
        ref T value { get; }
    }

    public struct PlayerAspect : IAspect {
        public Entity Entity;

        public Translation Translation;

        public IEnumerable<Type> Link() {
            return new[] {typeof(Translation), typeof(StaticTag)};
        }
    }

    public static class UnsafeHelp {

        public static unsafe void* ResizeUnsafeUtility<T>(void* ptr, int oldSize, int newSize, Allocator allocator) where T : unmanaged {
            var oldSizeInBtyes = UnsafeUtility.SizeOf(typeof(T)) * oldSize;
            var newSizeOnBytes = UnsafeUtility.SizeOf(typeof(T)) * newSize;
            var newPtr = UnsafeUtility.Malloc(newSizeOnBytes, UnsafeUtility.AlignOf<T>(), allocator);
            UnsafeUtility.MemCpy(newPtr, ptr, oldSizeInBtyes);
            UnsafeUtility.MemClear(ptr, oldSizeInBtyes);
            return newPtr;
        }
        
        public static unsafe void* Resize<T>(void* ptr, int oldSize, int newSize) where T : struct{
            var oldSizeInBytes = Marshal.SizeOf(typeof(T)) * oldSize;
            var newSizeOnBytes = Marshal.SizeOf(typeof(T)) * newSize;
            var newPtr =  (void*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }
        
        public static unsafe T* Resize<T>(T* ptr, int oldSize, int newSize) where T : unmanaged{
            var oldSizeInBytes = sizeof(T) * oldSize;
            var newSizeOnBytes = sizeof(T) * newSize;
            var newPtr =  (T*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }
        public static unsafe delegate*<T,void>* Resize<T>(delegate*<T,void>* ptr, int oldSize, int newSize){
            var oldSizeInBytes = sizeof(delegate*<T,void>) * oldSize;
            var newSizeOnBytes = sizeof(delegate*<T,void>) * newSize;
            var newPtr =  (delegate*<T,void>*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }
        public static unsafe delegate*<T1,T2,void>* Resize<T1,T2>(delegate*<T1,T2,void>* ptr, int oldSize, int newSize){
            var oldSizeInBytes = sizeof(delegate*<T1,T2,void>) * oldSize;
            var newSizeOnBytes = sizeof(delegate*<T1,T2,void>) * newSize;
            var newPtr =  (delegate*<T1,T2,void>*)Marshal.AllocHGlobal(newSizeOnBytes);
            Buffer.MemoryCopy(ptr, newPtr, newSizeOnBytes, oldSizeInBytes);
            Marshal.FreeHGlobal((IntPtr)ptr);
            return newPtr;
        }
        public static unsafe void AssertSize<T>(ref delegate*<T, void>* ptr,  ref int capacity, int size) {
            if (size == capacity) {
                ptr = Resize(ptr, capacity, capacity * 2);
                capacity *= 2;
            }
        }
        public static unsafe void AssertSize<T1,T2>(ref delegate*<T1,T2, void>* ptr,  ref int capacity, int size) {
            if (size == capacity) {
                ptr = Resize(ptr, capacity, capacity * 2);
                capacity *= 2;
            }
        }
    }

    internal static class Logs {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Show(object massage) {
            UnityEngine.Debug.Log(massage);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Error(object massage) {
            UnityEngine.Debug.LogError(massage);
        }
    }
    
}
