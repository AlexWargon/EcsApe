using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Ecsape.Pools;

namespace Wargon.Ecsape {

    public interface IComponent { }

    public interface ISingletoneComponent { }

    public interface IEventComponent { }

    public interface IClearOnEndOfFrame { }

    public interface IOnCreate {
        void OnCreate();
    }

    public readonly struct Component<T> where T : struct, IComponent {
        public static readonly int Index;
        public static readonly Type Type;
        public static readonly bool IsSingleTone;
        public static readonly bool IsTag;
        public static readonly bool IsEvent;
        public static readonly bool IsClearOnEnfOfFrame;
        public static readonly bool IsDisposable;
        public static readonly int SizeInBytes;
        public static readonly bool IsOnCreate;
        static Component() {
            Type = typeof(T);
            Index = Component.GetIndex(Type);
            ref var componentType = ref Component.GetComponentType(Index);
            IsSingleTone = componentType.IsSingletone;
            IsTag = componentType.IsTag;
            IsEvent = componentType.IsEvent;
            IsClearOnEnfOfFrame = componentType.IsClearOnEnfOfFrame;
            IsDisposable = componentType.IsDisposable;
            SizeInBytes = componentType.SizeInBytes;
            IsOnCreate = componentType.IsOnCreate;
            if (IsClearOnEnfOfFrame) {
                DefaultClearSystems.Add<ClearEventsSystem<T>>();
            }
        }

        public static ComponentType AsComponentType() {
            return new ComponentType(Index, IsSingleTone, IsTag, IsEvent, IsClearOnEnfOfFrame, IsDisposable, Type.Name, SizeInBytes, IsOnCreate);
        }
    }

    [Serializable]
    public readonly struct ComponentType : IEquatable<ComponentType> {
        public readonly int Index;
        public readonly bool IsSingletone;
        public readonly bool IsTag;
        public readonly bool IsEvent;
        public readonly bool IsClearOnEnfOfFrame;
        public readonly bool IsDisposable;
        public readonly NativeString Name;
        public readonly int SizeInBytes;
        public readonly bool IsOnCreate;
        public ComponentType(int index, bool isSingletone, bool isTag, bool isEvent, bool clearOnEnfOfFrame, bool disposable, string name, int size, bool isOnCreate) {
            Index = index;
            IsSingletone = isSingletone;
            IsTag = isTag;
            IsEvent = isEvent;
            IsClearOnEnfOfFrame = clearOnEnfOfFrame;
            IsDisposable = disposable;
            Name = new NativeString(name);
            SizeInBytes = size;
            IsOnCreate = isOnCreate;
        }

        public bool Equals(ComponentType other) {
            return Index == other.Index;
        }

        public override int GetHashCode() {
            return Index;
        }
    }

    public struct Component {
        private static readonly Dictionary<int, Type> typeByIndex;
        private static readonly Dictionary<Type, int> indexByType;
        private static ComponentType[] componentTypes;
        private static int count;
        public const int DESTROY_ENTITY = 0;
        static Component() {
            typeByIndex = new Dictionary<int, Type>();
            indexByType = new Dictionary<Type, int>();
            componentTypes = new ComponentType[32];
        }

        public static int GetIndex(Type type) {
            if (indexByType.TryGetValue(type, out var idx)) return idx;
            var index = count;
            indexByType.Add(type, index);
            typeByIndex.Add(index, type);
            var componentType = new ComponentType(index,
                typeof(ISingletoneComponent).IsAssignableFrom(type),
                type.GetFields().Length == 0,
                typeof(IEventComponent).IsAssignableFrom(type),
                typeof(IClearOnEndOfFrame).IsAssignableFrom(type),
                typeof(IDisposable).IsAssignableFrom(type), 
                type.Name,
                Marshal.SizeOf(type),
                typeof(IOnCreate).IsAssignableFrom(type)
                );
            AddInfo(ref componentType, index);
            count++;
            return index;
        }

        private static void AddInfo(ref ComponentType type, int index) {
            if (componentTypes.Length - 1 <= index) Array.Resize(ref componentTypes, index + 16);
            componentTypes[index] = type;
        }
        public static Type GetTypeOfComponent(int index) {
            return typeByIndex[index];
        }

        public static ref ComponentType GetComponentType(int index) {
            return ref componentTypes[index];
        }
    }

    public static class ComponentExtensions {
        internal static Type GetTypeFromIndex(this int index) {
            return Component.GetTypeOfComponent(index);
        }
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

                var info = Component.GetComponentType(typeIndex);
                var componentType = Component.GetTypeOfComponent(typeIndex);
                var poolType = info.IsTag || info.IsSingletone || info.IsEvent ? typeof(TagPool<>)
                    : info.IsDisposable ? typeof(DisposablePool<>) : info.IsOnCreate ? typeof(OnCreatePool<>) : typeof(Pool<>);
                var pool = (IPool) Generic.New(poolType, componentType, size);
                return pool;
            }

            void Resize(int newSize);
            object GetRaw(int index);
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

    internal class OnCreatePool<T> : IPool<T> where T : struct, IComponent, IOnCreate {
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
            c.OnCreate();
            data[count] = c;
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = component;
            data[count].OnCreate();
            count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddBoxed(object component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = (T)component;
            data[count].OnCreate();
            count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddPtr(void* component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = Marshal.PtrToStructure<T>((IntPtr)component);
            data[count].OnCreate();
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
    internal class Pool<T> : IPool<T> where T : struct, IComponent {
        private readonly IPool self;
        private int count;
        private T[] data;
        private int[] entities;
        public int Capacity => data.Length;

        public int Count => count - 1;
        public Pool(int size) {
            data = new T[size];
            entities = new int[size];
            Info = Component<T>.AsComponentType();
            count = 1;
            self = this;
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
            data[count] = default;
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
            data[count] = Marshal.PtrToStructure<T>((IntPtr)component);
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

        public void UpdateQueries() {
            if (count < 1) return;
            for (var i = 0; i < count; i++) items[i].Update();
            count = 0;
        }
    }

    public interface IEntitySystem : ISystem {
        Query Query { get; set; }

        void ISystem.OnUpdate(float deltaTime) {
            if (Query.IsEmpty) return;
            foreach (ref var entity in Query) Execute(ref entity, deltaTime);
        }

        void Execute(ref Entity entity, float deltaTime);
    }

    public interface IOnAdd {
        void Execute(ref Entity entity);
    }

    public abstract class OnAdd<T> : ISystem, IOnAdd where T : struct, IComponent {
        private IPool pool;
        private Query trigger;
        protected World world; // ReSharper disable Unity.PerformanceAnalysis
        public abstract void Execute(ref Entity entity);

        public void OnCreate(World world) {
            this.world = world;
            pool = this.world.GetPool<T>();
            trigger = this.world.GetQuery().With<T>();
        }

        void ISystem.OnUpdate(float deltaTime) {
            if (trigger.IsEmpty) return;
            foreach (ref var entity in trigger) {
                Execute(ref entity);
                pool.Remove(entity.Index);
            }
        }
    }
    public interface IEventSystem{}
    /// <summary>
    ///     Event will be cleared before this system
    /// </summary>
    public interface IClearBeforeUpdate<T> : IEventSystem where T : struct, IComponent { }
    /// <summary>
    ///     Execute every frame
    /// </summary>
    public interface ISystem {
        void OnCreate(World world); // ReSharper disable Unity.PerformanceAnalysis
        /// <param name="deltaTime"> time in ms between frames</param>
        void OnUpdate(float deltaTime); // ReSharper disable Unity.PerformanceAnalysis
    }

    public abstract class SystemBase : ISystem {
        protected Entities Entities;
        protected World World;
        public void OnCreate(World w) {
            World = w;
            Entities.world = w;
        }
        public abstract void OnUpdate(float delatTime);
    }
    internal sealed class ClearEventsSystem<T> : ISystem where T : struct, IComponent {
        private IPool<T> pool;
        private Query query;

        public void OnCreate(World world) {
            query = world.GetQuery().With<T>();
        }

        public void OnUpdate(float deltaTime) {
            if (query.IsEmpty) return;
            foreach (ref var entity in query) {
                entity.Remove<T>();
            }
        }
    }

    public struct DestroyEntity : IComponent { }

    internal sealed class DestroyEntitiesSystem : ISystem {
        private World world;
        private Query query;

        public void OnCreate(World world) {
            query = world.GetQuery().With<DestroyEntity>();
            this.world = world;
        }

        public void OnUpdate(float deltaTime) {
            if (query.IsEmpty) return;
            foreach (ref var entity in query) {
                world.OnDestroyEntity(in entity);
            }
        }
    }

    public abstract class SkipFrameSystem : ISystem {
        private int counter;
        private int skip;
        private float skippedDeltaTime;
        public abstract void OnCreate(World world);

        void ISystem.OnUpdate(float deltaTime) {
            if (counter == 0) {
                Execute(skippedDeltaTime);
                skippedDeltaTime = 0f;
                counter = skip;
            }
            else {
                counter--;
                skippedDeltaTime += deltaTime;
            }
        }

        protected void Skip(int frames) {
            skip = frames;
            counter = 0;
        }

        protected abstract void Execute(float skippedDeltaTime);
    }

    public sealed class DefaultSystems {
        internal readonly List<ISystem> end = new();
        internal readonly List<ISystem> start = new();
        internal bool enabled = true;

        internal void Disable() {
            enabled = false;
        }

        internal void Init() {
            end.Add(new DestroyEntitiesSystem());
        }
    }

    internal static class DefaultClearSystems {
        private static readonly List<ISystem> systems = new();
        internal static List<ISystem> GetSystems() => systems;

        internal static void Add<T>() where T : class, ISystem, new() {
            systems.Add(new T());
        }
    }

    internal class EntityCommandBufferSystem : ISystem {
        private CommandBuffer cmd;
        private World _world;
        public void OnCreate(World world) {
            cmd = world.GetCmdBuffer();
            UnityEngine.Debug.Log("CMD SYSTEM ADDED");
        }

        public void OnUpdate(float deltaTime) {
            cmd.Execute(_world);
        }
    }

    public sealed class Systems {
        private readonly DefaultSystems defaultSystems;
        private readonly List<Group> groups;
        private readonly World world;
        private ISystem[] updates;
        private int updatesCount;
        private IDependencyContainer dependencyContainer;

        public Systems(World world) {
            this.world = world;
            updates = new ISystem[32];
            updatesCount = 0;
            groups = new List<Group>();
            defaultSystems = new DefaultSystems();
        }

        private bool IsEventSystem<T>(T system) where T : ISystem {
            return system.GetType().GetInterface(nameof(IEventSystem)) != null;
        }

        private void IfIsEventSystemAddClearSystem<T>(T eventSystem) where T: ISystem {
            if (IsEventSystem(eventSystem)) {
                var type = GetGenericType(eventSystem.GetType(), typeof(IClearBeforeUpdate<>));
                AddSystem(CreateClearEventSystem(type));
            }
        }
                
        private static Type GetGenericType(Type system, Type @interface) {
            foreach(var type in system.GetInterfaces()) {
                if(type.IsGenericType && type.GetGenericTypeDefinition() == @interface) {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
        
        private static ISystem CreateClearEventSystem(Type eventType) {
            return (ISystem) Generic.New(typeof(ClearEventsSystem<>), eventType, null);
        }

        public Systems AddInjector(IDependencyContainer container) {
            dependencyContainer = container;
            return this;
        }
        
        private void InitDependencies() {
            if (dependencyContainer != null)
                foreach (var i in updatesCount) {
                    ref var system = ref updates[i];
                    dependencyContainer.Build(system);
                }

            foreach (var i in updatesCount) {
                ref var system = ref updates[i];
                foreach (var fieldInfo in system.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    if (fieldInfo.FieldType.GetInterface(nameof(IPool)) != null) {
                        var poolType = fieldInfo.FieldType.GetGenericArguments()[0];
                        var componentTypeIndex = Component.GetIndex(poolType);
                        fieldInfo.SetValue(system, world.GetPoolByIndex(componentTypeIndex));
                    }

                    if (fieldInfo.FieldType == typeof(World)) {
                        fieldInfo.SetValue(system, world);
                    }
                }
            }
        }

        public Systems Init() {
            defaultSystems.Init();
            if (defaultSystems.enabled)
                foreach (var system in defaultSystems.start)
                    AddSystem(system);

            foreach (var group in groups)
                for (var i = 0; i < group.count; i++) {
                    var s = group.systems[i];
                    IfIsEventSystemAddClearSystem(s);
                    AddSystem(s);
                }

            if (defaultSystems.enabled)
                foreach (var system in defaultSystems.end)
                    AddSystem(system);

            foreach (var system in DefaultClearSystems.GetSystems()) {
                AddSystem(system);
            }

            InitDependencies();
            return this;
        }

        public Systems Clear<T>() where T : struct, IComponent {
            AddSystem(new ClearEventsSystem<T>());
            return this;
        }

        public Systems DisableDefaultSystems() {
            defaultSystems.Disable();
            return this;
        }

        public Systems Add<T>() where T : class, ISystem, new() {
            var system = new T();
            IfIsEventSystemAddClearSystem(system);
            AddSystem(system);
            return this;
        }

        public Systems Add<T>(T system) where T : class, ISystem {
            IfIsEventSystemAddClearSystem(system);
            AddSystem(system);
            return this;
        }

        private void AddSystem(ISystem system) {
            if (updatesCount >= updates.Length - 1) Array.Resize(ref updates, updates.Length << 1);
            system.OnCreate(world);

            updates[updatesCount] = system;
            updatesCount++;
            
            if (world.bufferGetten) {
                var cmdsystem = new EntityCommandBufferSystem();
                cmdsystem.OnCreate(world);
                updates[updatesCount] = cmdsystem;
                updatesCount++;
                world.bufferGetten = false;
            }
        }

        public Systems AddReactive<T>() where T : class, IOnAdd, ISystem, new() {
            var t = new T();
            t.OnCreate(world);
            if (updatesCount >= updates.Length - 1) Array.Resize(ref updates, updates.Length << 1);
            updates[updatesCount] = t;
            updatesCount++;
            return this;
        }

        public Systems AddGroup(Group group) {
            groups.Add(group);
            //Debug.Log($"group {group.Name} Added");
            return this;
        }

        public void Update(float dt) {
            for (var i = 0; i < updatesCount; i++) {
                updates[i].OnUpdate(dt);
                world.UpdateQueries();
            }
        }

        public class Group {
            internal ISystem[] systems;
            internal int count;
            protected readonly string name;
            public string Name => name;

            public Group() {
                systems = new ISystem[8];
            }

            public Group(string name) {
                this.name = name;
                systems = new ISystem[8];
            }

            public Group Add<T>() where T : class, ISystem, new() {
                var t = new T();
                if (count >= systems.Length - 1) Array.Resize(ref systems, systems.Length << 1);
                systems[count] = t;
                count++;
                return this;
            }

            public Group Add<T>(T system) where T : class, ISystem {
                if (count >= systems.Length - 1) Array.Resize(ref systems, systems.Length << 1);
                systems[count] = system;
                count++;
                return this;
            }
            public Group Add(Group group) {
                for (int i = 0; i < group.count; i++) {
                    Add(group.systems[i]);
                }
                return this;
            }
        }
    }

    [Serializable]
    public struct Translation : IComponent {
        /// Local
        public UnityEngine.Vector3 position;
        /// Local
        public UnityEngine.Quaternion rotation;        
        /// Local
        public UnityEngine.Vector3 scale;

        public UnityEngine.Vector3 right {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.right;
        }
    }

    public struct StaticTag : IComponent { }


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


    public unsafe struct UnsafeDelegate<T> {
        private delegate*<T,void>* delegates;
        private int subbed;
        public int Count => subbed;
        private int capacity;
        public void Sub(delegate*<T,void> action) {
            UnsafeHelp.AssertSize(ref delegates, ref capacity, subbed);
            delegates[subbed++] = action;
        }

        public UnsafeDelegate(int size) {
            delegates = (delegate*<T,void>*) Marshal.AllocHGlobal(sizeof(delegate*<T,void>) * size);
            subbed = 0;
            capacity = size;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(T param) {
            for (var i = 0; i < subbed; i++) {
                delegates[i](param);
            }
        }
    }
    public unsafe struct UnsafeDelegate<T1, T2> {
        private delegate*<T1, T2,void>* delegates;
        private int subbed;
        private int capacity;
        public void Sub(delegate*<T1, T2,void> action) {
            UnsafeHelp.AssertSize(ref delegates, ref capacity, subbed);
            delegates[subbed++] = action;
        }

        public UnsafeDelegate(int size) {
            delegates = (delegate*<T1, T2,void>*) Marshal.AllocHGlobal(sizeof(delegate*<T1, T2,void>) * size);
            subbed = 0;
            capacity = size;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(T1 param1, T2 param2) {
            for (var i = 0; i < subbed; i++) {
                delegates[i](param1,param2);
            }
        }
    }
    public static class UnsafeHelp {
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

    internal static class Debug {
        internal static void Log(object massage) {
            UnityEngine.Debug.Log(massage);
        }
        internal static void LogError(object massage) {
            UnityEngine.Debug.LogError(massage);
        }
    }
    public sealed class SyncTransformsSystem : ISystem {
        private Query query;
        private IPool<Components.TransformReference> transforms;
        private IPool<Translation> translations;
        public void OnCreate(World world) {
            query = world.GetQuery()
                .With<Translation>()
                .With<Components.TransformReference>()
                .Without<StaticTag>();
        }

        public void OnUpdate(float deltaTime) {
            if(query.IsEmpty) return;
            foreach (var entity in query) {
                ref var transform = ref transforms.Get(entity.Index);
                ref var translation = ref translations.Get(entity.Index);
                transform.value.localPosition = translation.position;
                transform.value.localRotation = translation.rotation;
                transform.value.localScale = translation.scale;
            }
        }
    }
}
