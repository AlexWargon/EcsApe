using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Wargon.Ecsape {
    public interface INew {
        void New();
    }
    public interface IComponent { }

    public interface ISingletoneComponent { }

    public interface IEventComponent { }
    
    public interface IClearOnEndOfFrame { }

    public readonly ref struct Component<T> where T : struct, IComponent {
        public static readonly int Index;
        public static readonly Type Type;
        public static readonly bool IsSingleTone;
        public static readonly bool IsTag;
        public static readonly bool IsEvent;
        public static readonly bool IsClearOnEnfOfFrame;
        public static readonly bool IsSelfNew;
        public static readonly bool IsDisposable;
        static Component() {
            Type = typeof(T);
            Index = Component.GetIndex(Type);
            IsSingleTone = typeof(ISingletoneComponent).IsAssignableFrom(Type);
            IsEvent = typeof(IEventComponent).IsAssignableFrom(Type);
            IsClearOnEnfOfFrame = typeof(IClearOnEndOfFrame).IsAssignableFrom(Type);
            IsSelfNew = typeof(INew).IsAssignableFrom(Type);
            IsDisposable = typeof(IDisposable).IsAssignableFrom(Type);
            IsTag = Type.GetFields().Length == 0;
            Component.AddInfo(Index, AsComponentInfo());
            if (IsClearOnEnfOfFrame) {
                DefaultClearSystems.Add<ClearEventsSystem<T>>();
            }
        }

        public Component(int idx, bool singleTone, bool tag, bool @event, bool clearOnEnfOfFrame, bool selfNew, bool disposable) {
            index = idx;
            isSingleTone = singleTone;
            isTag = tag;
            isEvent = @event;
            isClearOnEnfOfFrame = clearOnEnfOfFrame;
            isSelfNew = selfNew;
            isDisposable = disposable;
        }

        public readonly int index;
        public readonly bool isSingleTone;
        public readonly bool isTag;
        public readonly bool isEvent;
        public readonly bool isClearOnEnfOfFrame;
        public readonly bool isSelfNew;
        public readonly bool isDisposable;
        public static Component<T> AsRef() {
            return new Component<T>(Index, IsSingleTone, IsTag, IsEvent, IsClearOnEnfOfFrame, IsSelfNew, IsDisposable);
        }

        public static ComponentInfo AsComponentInfo() {
            return new ComponentInfo(Index, Type, IsSingleTone, IsTag, IsEvent,IsClearOnEnfOfFrame,IsSelfNew,IsDisposable);
        }

        public static ComponentType AsComponentType() {
            return new ComponentType(Index, IsSingleTone, IsTag, IsEvent, IsClearOnEnfOfFrame, IsSelfNew, IsDisposable);
        }
    }

    public readonly struct ComponentInfo {
        public readonly int Index;
        public readonly Type Type;
        public readonly bool IsSingletone;
        public readonly bool IsTag;
        public readonly bool IsEvent;
        public readonly bool IsClearOnEnfOfFrame;
        public readonly bool isSelfNew;
        public readonly bool IsDisposable;
        public ComponentInfo(int index, Type type, bool isSingletone, bool isTag, bool isEvent, bool clearOnEnfOfFrame, bool selfNew, bool disposable) {
            Index = index;
            Type = type;
            IsSingletone = isSingletone;
            IsTag = isTag;
            IsEvent = isEvent;
            isSelfNew = selfNew;
            IsClearOnEnfOfFrame = clearOnEnfOfFrame;
            IsDisposable = disposable;
        }
    }
    
    public readonly struct ComponentType : IEquatable<ComponentType> {
        public readonly int Index;
        public readonly bool IsSingletone;
        public readonly bool IsTag;
        public readonly bool IsEvent;
        public readonly bool IsClearOnEnfOfFrame;
        public readonly bool IsSelfNew;
        public readonly bool IsDisposable;
        public ComponentType(int index, bool isSingletone, bool isTag, bool isEvent, bool clearOnEnfOfFrame, bool selfNew, bool disposable) {
            Index = index;
            IsSingletone = isSingletone;
            IsTag = isTag;
            IsEvent = isEvent;
            IsSelfNew = selfNew;
            IsClearOnEnfOfFrame = clearOnEnfOfFrame;
            IsDisposable = disposable;
        }

        public ComponentType(Type type) {
            var info = Component.GetInfoByType(type);
            Index = info.Index;
            IsSingletone = info.IsSingletone;
            IsTag = info.IsTag;
            IsEvent = info.IsEvent;
            IsSelfNew = info.isSelfNew;
            IsClearOnEnfOfFrame = info.IsClearOnEnfOfFrame;
            IsDisposable = info.IsDisposable;
        }
        
        public bool Equals(ComponentType other) {
            return Index == other.Index;
        }
        public override int GetHashCode() {
            return Index;
        }
    }

    internal struct ComponentTypes {
        public static int Count;
    }

    public struct Component {
        private static readonly Dictionary<int, Type> typeByIndex;
        private static readonly Dictionary<Type, int> indexByType;
        private static ComponentInfo[] ComponentInfos;

        static Component() {
            typeByIndex = new Dictionary<int, Type>();
            indexByType = new Dictionary<Type, int>();
            ComponentInfos = new ComponentInfo[32];
        }

        public static int GetIndex(Type type) {
            if (indexByType.TryGetValue(type, out var idx)) return idx;
            var index = ComponentTypes.Count;
            indexByType.Add(type, index);
            typeByIndex.Add(index, type);
            ComponentTypes.Count++;
            return index;
        }

        public static Type GetComponentType(int index) {
            return typeByIndex[index];
        }

        internal static void AddInfo(int index, ComponentInfo info) {
            if (ComponentInfos.Length - 1 == index) Array.Resize(ref ComponentInfos, index * 2);
            ComponentInfos[index] = info;
        }

        internal static ref ComponentInfo GetInfo(int index) {
            return ref ComponentInfos[index];
        }

        internal static ref ComponentInfo GetInfoByType(Type type) {
            return ref ComponentInfos[GetIndex(type)];
        }
    }

    public interface IPool {
        int Count { get; }
        ComponentInfo Info { get; }
        event Action<int> OnAdd;
        event Action<int> OnRemove;
        void Add(int entity);
        void AddBoxed(object component, int entity);
        void Remove(int entity);
        bool Has(int entity);

        static IPool New(int size, int typeIndex) {
            var info = Component.GetInfo(typeIndex);
            var componentType = Component.GetComponentType(typeIndex);
            var poolType = info.IsTag || info.IsSingletone || info.IsEvent ? typeof(TagPool<>) 
                : info.IsDisposable ? typeof(DisposablePool<>) : typeof(Pool<>);
            var pool = (IPool)Generic.New(poolType, componentType, size);
            return pool;
        }

        void Resize(int newSize);
        IComponent GetRaw(int index);
    }

    public static class Generic {
        public static object New(Type genericType, Type elementsType, params object[] parameters) {
            return Activator.CreateInstance(genericType.MakeGenericType(elementsType), parameters);
        }
    }

    public static class PoolsExt
    {
        public static void Sett<T>(this IPool pool, in T component, int entity)
        {
            pool.Add(entity);
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
        private int[] entities;

        public TagPool(int size) {
            data = default;
            entities = new int[size];
            Info = Component<T>.AsComponentInfo();
            count = 1;
            OnAdd = null;
            OnRemove = null;
            self = this;
        }

        public ref T Get(int entity) {
            return ref data;
        }

        public ref T Get(ref Entity entity) {
            return ref data;
        }

        public void Set(in T component, int entity) { }

        public event Action<int> OnAdd;
        public event Action<int> OnRemove;

        public void Add(int entity) {
            entities[entity] = count;
            count++;
            OnAdd?.Invoke(entity);
        }

        public void Add(in T component, int entity) {
            entities[entity] = count;
            data = component;
            count++;
            OnAdd?.Invoke(entity);
        }

        public T[] GetRawData() {
            return new[] { data };
        }

        public int[] GetRawEntities() {
            return entities;
        }

        public void AddBoxed(object component, int entity) { }

        public void Remove(int entity) {
            entities[entity] = 0;
            count--;
            OnRemove?.Invoke(entity);
        }

        bool IPool.Has(int entity) {
            return entities[entity] > 0;
        }

        public int Count => count - 1;

        void IPool.Resize(int newSize) {
            Array.Resize(ref entities, newSize);
        }

        public ComponentInfo Info { get; }

        IComponent IPool.GetRaw(int index) {
            return Get(index);
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

        public Pool(int size) {
            data = new T[size];
            entities = new int[size];
            Info = Component<T>.AsComponentInfo();
            count = 1;
            OnAdd = null;
            OnRemove = null;
            self = this;
        }

        public ref T Get(int entity) {
            return ref data[entities[entity]];
        }

        public ref T Get(ref Entity entity) {
            return ref data[entities[entity.Index]];
        }

        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }

        public event Action<int> OnAdd;
        public event Action<int> OnRemove;

        public void Add(int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = default;
            count++;
            OnAdd?.Invoke(entity);
        }

        public void Add(in T component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            if(entities.Length - 1 < entity) Debug.Log($"entities.Length {entities.Length} entity {entity}");
            entities[entity] = count;
            data[count] = component;
            count++;
            OnAdd?.Invoke(entity);
        }

        public void AddBoxed(object component, int entity) { }

        public void Remove(int entity) {
            entities[entity] = 0;
            count--;
            OnRemove?.Invoke(entity);

        }

        bool IPool.Has(int entity) {
            return entities[entity] > 0;
        }

        public int Count => count - 1;

        void IPool.Resize(int newSize) {
            Array.Resize(ref entities, newSize);
        }

        public ComponentInfo Info { get; }

        IComponent IPool.GetRaw(int index) {
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
    internal class DisposablePool<T> : IPool<T> where T : struct, IComponent, IDisposable {
        private readonly IPool self;
        private int count;
        private T[] data;
        private int[] entities;

        public DisposablePool(int size) {
            data = new T[size];
            entities = new int[size];
            Info = Component<T>.AsComponentInfo();
            count = 1;
            OnAdd = null;
            OnRemove = null;
            self = this;
        }

        public ref T Get(int entity) {
            return ref data[entities[entity]];
        }

        public ref T Get(ref Entity entity) {
            return ref data[entities[entity.Index]];
        }

        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }

        public event Action<int> OnAdd;
        public event Action<int> OnRemove;

        public void Add(int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            count++;
            OnAdd?.Invoke(entity);
        }

        public void Add(in T component, int entity) {
            if (data.Length - 1 <= count) Array.Resize(ref data, count + 16);
            entities[entity] = count;
            data[count] = component;
            count++;
            OnAdd?.Invoke(entity);
        }

        public void AddBoxed(object component, int entity) { }

        public void Remove(int entity) {
            data[entities[entity]].Dispose();
            
            entities[entity] = 0;
            count--;
            OnRemove?.Invoke(entity);

        }

        bool IPool.Has(int entity) {
            return entities[entity] > 0;
        }

        public int Count => count - 1;

        void IPool.Resize(int newSize) {
            Array.Resize(ref entities, newSize);
        }

        public ComponentInfo Info { get; }

        IComponent IPool.GetRaw(int index) {
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
            if(count < 1) return;
            for (var i = 0; i < count; i++) items[i].Update();
            count = 0;
        }
    }

    /// <summary>
    ///     Execute when trigger has entity. Component not removing
    /// </summary>
    public interface ITriggerSystem : ISystem {
        Query Trigger { get; set; }

        void ISystem.OnUpdate(float deltaTime) {
            if (Trigger.IsEmpty) return;
            foreach (ref var entity in Trigger) Execute(ref entity);
        }

        void Execute(ref Entity entity);
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
        private Query Trigger;
        protected World world; // ReSharper disable Unity.PerformanceAnalysis
        public abstract void Execute(ref Entity entity);

        public void OnCreate(World w) {
            world = w;
            pool = world.GetPool<T>();
            Trigger = world.GetQuery().With<T>();
        }

        void ISystem.OnUpdate(float deltaTime) {
            if (Trigger.IsEmpty) return;
            foreach (ref var entity in Trigger) {
                Execute(ref entity);
                pool.Remove(entity.Index);
            }
        }
    }


    /// <summary>
    ///     Execute every frame
    /// </summary>
    public interface ISystem {
        void OnCreate(World world); // ReSharper disable Unity.PerformanceAnalysis
        void OnUpdate(float deltaTime);
    }

    internal sealed class ClearEventsSystem<T> : ISystem where T : struct, IComponent {
        private TagPool<T> _pool;
        private Query _query;

        public void OnCreate(World world) {
            _pool = (TagPool<T>)world.GetPool<T>();
            _query = world.GetQuery().With<T>();
        }

        public void OnUpdate(float deltaTime) {
            if (!_query.IsEmpty)
                foreach (var entity in _query) {
                    _pool.Remove(entity.Index);
                    Debug.Log($"{nameof(T)} cleared");
                }
        }
    }

    public struct DestroyEntity : IComponent { }

    internal sealed class DestroyEntitiesSystem : ISystem {
        private World _world;
        private Query query;

        public void OnCreate(World world) {
            query = world.GetQuery().With<DestroyEntity>();
            _world = world;
        }

        public void OnUpdate(float deltaTime) {
            if (query.IsEmpty) return;
            foreach (ref var entity in query) {
                _world.OnDestroyEntity(in entity);
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
        internal readonly List<ISystem> End = new();
        internal readonly List<ISystem> Start = new();
        internal bool Enabled = true;

        internal void Disable() {
            Enabled = false;
        }

        internal void Init() {
            End.Add(new DestroyEntitiesSystem());
        }
    }

    internal static class DefaultClearSystems {
        private static readonly List<ISystem> _systems = new List<ISystem>();
        internal static List<ISystem> GetSystems() => _systems;
        internal static void Add<T>() where T : class, ISystem, new() {
            _systems.Add(new T());
        }
    }
    public sealed class Systems {
        private readonly DefaultSystems _defaultSystems;
        private readonly List<Group> _groups;
        private readonly World _world;
        private ISystem[] _updates;
        private int _updatesCount;
        private IDependencyContainer _dependencyContainer; 
        public Systems(World world) {
            _world = world;
            _updates = new ISystem[32];
            _updatesCount = 0;
            _groups = new List<Group>();
            _defaultSystems = new DefaultSystems();
        }

        public Systems AddInjector(IDependencyContainer container) {
            _dependencyContainer = container;
            return this;
        }
        private void InitDependencies() {
            if(_dependencyContainer != null)
                foreach (var i in _updatesCount) {
                    ref var system = ref _updates[i];
                    _dependencyContainer.Build(system);
                }

            foreach (var i in _updatesCount) {
                ref var system = ref _updates[i];
                foreach (var fieldInfo in system.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    if (fieldInfo.FieldType.GetInterface(nameof(IPool)) != null) {
                        var poolType = fieldInfo.FieldType.GetGenericArguments()[0];
                        var componentTypeIndex = Component.GetIndex(poolType);
                        fieldInfo.SetValue(system, _world.GetPoolByIndex(componentTypeIndex));
                    }
                }
            }
        }

        public Systems Init() {
            _defaultSystems.Init();
            if (_defaultSystems.Enabled)
                foreach (var system in _defaultSystems.Start)
                    AddSystem(system);

            foreach (var group in _groups)
                for (var i = 0; i < group.count; i++) {
                    var s = group._systems[i];
                    AddSystem(s);
                }

            if (_defaultSystems.Enabled)
                foreach (var system in _defaultSystems.End)
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
            _defaultSystems.Disable();
            return this;
        }

        public Systems Add<T>() where T : class, ISystem, new() {
            var t = new T();
            AddSystem(t);
            return this;
        }

        public Systems Add<T>(T system) where T : class, ISystem {
            AddSystem(system);
            return this;
        }

        private void AddSystem(ISystem system) {
            if (_updatesCount >= _updates.Length - 1) Array.Resize(ref _updates, _updates.Length << 1);
            system.OnCreate(_world);
            _updates[_updatesCount] = system;
            _updatesCount++;
            //Debug.Log($"  system {system.GetType()} Added");
        }

        public Systems AddReactive<T>() where T : class, IOnAdd, ISystem, new() {
            var t = new T();
            t.OnCreate(_world);
            if (_updatesCount >= _updates.Length - 1) Array.Resize(ref _updates, _updates.Length << 1);
            _updates[_updatesCount] = t;
            _updatesCount++;
            return this;
        }

        public Systems Add(Group group) {
            _groups.Add(group);
            Debug.Log($"group {group.Name} Added");
            return this;
        }

        public void Update(float dt) {
            for (var i = 0; i < _updatesCount; i++) {
                _updates[i].OnUpdate(dt);
                _world.UpdateQueries();
            }
        }

        public class Group {
            internal ISystem[] _systems;
            internal int count;
            private string name;
            public string Name => name;
            public Group() {
                _systems = new ISystem[8];
            }

            public Group(string name) {
                this.name = name;
                _systems = new ISystem[8];
            }

            public Group Add<T>() where T : class, ISystem, new() {
                var t = new T();
                if (count >= _systems.Length - 1) Array.Resize(ref _systems, _systems.Length << 1);
                _systems[count] = t;
                count++;
                return this;
            }

            public Group Add<T>(T system) where T : class, ISystem {
                if (count >= _systems.Length - 1) Array.Resize(ref _systems, _systems.Length << 1);
                _systems[count] = system;
                count++;
                return this;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Entity {
        public int Index;
        internal byte WorldIndex;
    }

    public static class EntityExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static World GetWorld(in this Entity entity) {
            return World.Get(entity.WorldIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity) == 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            var pool = World.Get(entity.WorldIndex).GetPool<T>();
            if (pool.Has(entity.Index)) return ref pool.Get(entity.Index);
            pool.Add(entity.Index);
            return ref pool.Get(entity.Index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Add(entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPool<T>().Add(in component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddBoxed(in this Entity entity, object component)
        {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component.GetIndex(component.GetType())).AddBoxed(component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Remove(entity.Index);
            world.ChangeComponentsAmount(in entity, -1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(in this Entity entity) where T : struct, IComponent {
            return World.Get(entity.WorldIndex).GetPoolByIndex(Component<T>.Index).Has(entity.Index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ComponentsAmount(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Destroy(in this Entity entity) {
            World.Get(entity.WorldIndex).GetPoolByIndex(0).Add(entity.Index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyNow(in this Entity entity) {
            World.Get(entity.WorldIndex).OnDestroyEntity(in entity);
        }
    }
    
    [Serializable] public struct Translation : IComponent {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
    
    public struct StaticTag : IComponent { }


    public interface IAspect {
        IEnumerable<Type> Link();
    }

    public struct PlayerAspect : IAspect {
        public IEnumerable<Type> Link() {
            return new [] { typeof(Translation), typeof(StaticTag) };
        }
    }
    
    public interface IDependencyContext {
        IDependencyContext From<T>() where T: class;
        IDependencyContext From<T>(T isntance) where T: class;
        object GetInstance();
    }
    public interface IDependencyContainer {
        void Build(object target);
        IDependencyContext Register<T>() where T: class;
        IDependencyContext Register<T>(T item) where T: class;
    }
    
    public class DependencyContext : IDependencyContext {
        private object _instance;
        private Type _instanceType;

        IDependencyContext IDependencyContext.From<T>(){
            _instanceType = typeof(T);
            _instance = Activator.CreateInstance(typeof(T));
            return this;
        }
        IDependencyContext IDependencyContext.From<T>(T instance){
            _instanceType = typeof(T);
            _instance = instance;
            return this;
        }
        public object GetInstance() {
            return _instance;
        }

    }

    
    public static class DI {
        private static IDependencyContainer Container;

        public static IDependencyContainer GetOrCreateContainer() {
            if(Container == null)
                Container = new DependencyContainer();
            return Container;
        }
        public static IDependencyContext Register<T>() where T : class => GetOrCreateContainer().Register<T>();
    }

    public class DependencyContainer : IDependencyContainer {
        private readonly IDictionary<Type, IDependencyContext> constexts;
        
        internal DependencyContainer() {
            constexts = new Dictionary<Type, IDependencyContext>();
            Register<IDependencyContainer>().From(this);
        }

        public IDependencyContext Register<T>() where T: class {
            var context = new DependencyContext();
            constexts.Add(typeof(T), context);
            return context;
        }
        public IDependencyContext Register<T>(T item) where T: class {
            var context = new DependencyContext();
            constexts.Add(typeof(T), context);
            return context;
        }
        public void Build(object instance){
            var type = instance.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var fieldInfo in fields) {
                var fieldType = fieldInfo.FieldType;

                if (constexts.TryGetValue(fieldType, out var context1)) {
                    fieldInfo.SetValue(instance, context1.GetInstance());
                }
            }
        }
    }
}