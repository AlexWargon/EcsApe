namespace Wargon.Ecsape {
    using System;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    public static class Extensions {

        public static T Last<T>(this System.Collections.Generic.List<T> list) {
            return list[^1];
        }

        public static void RemoveLast(this System.Collections.IList list) {
            list.RemoveAt(list.Count - 1);
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
            //query.WorldInternal.AddDirtyQuery(query);
            return new NativeQuery(query, Allocator.TempJob);
        }

        public static NativePool<T> GetPoolNative<T>(this World world) where T : unmanaged, IComponent {
            return world.GetPool<T>().AsNative();
        }

        public static NativePoolWrapped<T> AsWrapped<T>(this IPool<T> pool) where T : unmanaged, IComponent {
            return new NativePoolWrapped<T>(pool);
        }
        // public static void RunParallel<T>(this T job, int arrayLen) where T : struct, IJobParallelFor {
        //     job.Schedule(arrayLen, 64).Complete();
        // }

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

    public struct NativePoolWrapped<T> where T : unmanaged, IComponent {
        internal int count;
        public NativeArray<T> data;
        public NativeArray<int> entities;

        public unsafe NativePoolWrapped(IPool<T> pool) {
            count = pool.Count;
            ref var d = ref pool.GetRawData();
            ref var e = ref pool.GetRawEntities();
            data = NativeMagic.WrapToNative(ref d).Array;
            entities = NativeMagic.WrapToNative(ref e).Array;
        }

        public T Get(int entity) {
            return data[entities[entity]];
        }
        public void Set(int entity, in T component) {
            data[entities[entity]] = component;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativePool<T> where T : unmanaged, IComponent {

        internal int count;
        [NativeDisableUnsafePtrRestriction]
        internal T* data;
        [NativeDisableUnsafePtrRestriction]
        internal int* entities;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativePool(IPool<T> pool, Allocator allocator) {
            fixed(T* dataPtr = pool.GetRawData())
            fixed (int* entitiesPtr = pool.GetRawEntities()) {
                data = dataPtr;
                entities = entitiesPtr;
            }
            count = pool.Count;
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
        [NativeDisableUnsafePtrRestriction]
        internal int* entities;
        [NativeDisableUnsafePtrRestriction]
        internal int* entityMap;
        internal int count;
        public int Count => count;
        public bool IsEmpty => count == 0;
        public NativeQuery(Query query, Allocator allocator) {
            entities = query.GetEntitiesPtr();
            entityMap = query.GetEntitiesMapPtr();
            count = query.count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Entity(int index) {
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
                get => Query.Entity(index);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct CommandBuffer {
        private enum CommandType : byte {
            Add,
            AddWithoutComponent,
            Remove,
            Create,
            Destroy
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Command {
            public CommandType type;
            public int componentIndex;
            public void* component;
            public int entity;
        }

        private struct Internal {
            public int count;
            public int len;
            [NativeDisableUnsafePtrRestriction]
            public Command* Commands;
            public Allocator Allocator;
        }
        [NativeDisableUnsafePtrRestriction]
        private readonly Internal* _internal;
        public CommandBuffer(int size) {
            _internal = (Internal*)UnsafeUtility.Malloc(sizeof(Internal), UnsafeUtility.AlignOf<Command>(), Allocator.Persistent);
            _internal->count = 0;
            _internal->len = size;
            _internal->Allocator = Allocator.Persistent;
            _internal->Commands = (Command*)UnsafeUtility.Malloc(sizeof(Command) * size, UnsafeUtility.AlignOf<Command>(), Allocator.Persistent);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity, T component) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.Add,
                entity = entity,
                componentIndex = Component<T>.Index,
                component = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.TempJob)
            };
            UnsafeUtility.CopyStructureToPtr(ref component, cmd.component);
            //Marshal.StructureToPtr(component,(IntPtr)cmd.component, false);
            _internal->Commands[_internal->count] = cmd;
            
            Interlocked.Increment(ref _internal->count);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.AddWithoutComponent,
                entity = entity,
                componentIndex = Component<T>.Index,
            };
            _internal->Commands[_internal->count] = cmd;
            
            Interlocked.Increment(ref _internal->count);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged, IComponent {
            _internal->Commands[_internal->count] = new Command {
                type = CommandType.Remove,
                entity = entity,
                componentIndex = Component<T>.Index
            };
            Interlocked.Increment(ref _internal->count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(World world) {
            
            if(_internal->count == 0) return;

            for (int i = 0; i < _internal->count; i++) {
                var command = _internal->Commands[i];
                switch (command.type) {
                    case CommandType.Add:
                        ref var eAdd = ref world.GetEntity(command.entity);
                        eAdd.AddPtr(command.component, command.componentIndex);
                        UnsafeUtility.Free(command.component, Allocator.TempJob);
                        break;
                    case CommandType.AddWithoutComponent:
                        ref var eAddw = ref world.GetEntity(command.entity);
                        eAddw.AddPtr(command.componentIndex);
                        break;
                    case CommandType.Remove:
                        ref var eRemove = ref world.GetEntity(command.entity);
                        eRemove.Remove(command.componentIndex);
                        break;
                    case CommandType.Create:
                        break;
                    case CommandType.Destroy:
                        world.GetEntity(command.entity).Destroy();
                        break;
                }
            }
            _internal->count = 0;
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
        // public static unsafe NativeArray<T> WrapToNative<T>(ref T[] managedData) where T : unmanaged {
        //     fixed (void* ptr = managedData) {
        //         return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, managedData.Length, Allocator.TempJob);
        //     }
        // }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NativeWrappedData<T> WrapToNative<T>(ref T[] managedData) where T : unmanaged
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

    public static class SystemsExtensions {
        public static Systems AddGen<TSystem>(this Systems systems, TSystem system) where TSystem: ISystem {
            var onUpdateInfo = system.GetType().GetMethod("OnUpdate");
            //RuntimeHelpers.PrepareMethod(onUpdateInfo.MethodHandle);

            return systems;
        }
    }
}