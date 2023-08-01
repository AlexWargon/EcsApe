using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Ecsape {
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
}