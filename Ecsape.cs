using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Ecsape {
    
    public interface IComponent { }
    public interface ISingletoneComponent { }
    public interface IEventComponent { }
    
    public readonly ref struct Component<T> where T : struct, IComponent {
        public static readonly int Index;
        public static readonly Type Type;
        public static readonly bool IsSingleTone;
        public static readonly bool IsTag;
        public static readonly bool IsEvent;
        static Component() {
            Type = typeof(T);
            Index = Component.GetIndex(Type);
            IsSingleTone = typeof(ISingletoneComponent).IsAssignableFrom(Type);
            IsEvent = typeof(IEventComponent).IsAssignableFrom(Type);
            IsTag = Type.GetFields().Length == 0;
            Component.AddInfo(Index, AsComponentInfo());
        }

        public Component(int idx, bool singleTone, bool tag, bool @event) {
            this.index = idx;
            this.isSingleTone = singleTone;
            this.isTag = tag;
            this.isEvent = @event;
        }
        public readonly int index;
        public readonly bool isSingleTone;
        public readonly bool isTag;
        public readonly bool isEvent;
        public static Component<T> AsRef() {
            return new Component<T>(Index, IsSingleTone, IsTag, IsEvent);
        }
        public static ComponentInfo AsComponentInfo() {
            return new ComponentInfo(Index, Type, IsSingleTone, IsTag, IsEvent);
        }
    }

    public readonly struct ComponentInfo {
        public readonly int Index;
        public readonly Type Type;
        public readonly bool isSingletone;
        public readonly bool isTag;
        public readonly bool isEvent;
        public ComponentInfo(int index, Type type, bool isSingletone, bool isTag, bool isEvent) {
            this.Index = index;
            this.Type = type;
            this.isSingletone = isSingletone;
            this.isTag = isTag;
            this.isEvent = isEvent;
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
            if (ComponentInfos.Length -1  == index) {
                Array.Resize(ref ComponentInfos, index * 2);
            }
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
            var poolType = info.isTag || info.isSingletone || info.isEvent ? typeof(TagPool<>) : typeof(Pool<>);
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
    public interface ITurplePool<T> : IPool where T : ITuple {
        ref T Get(int entity);
        void Add(in T component, int entity);
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

        void ISystem.OnUpdate(float dt) {
            if (Trigger.IsEmpty) return;
            foreach (ref var entity in Trigger) {
                Execute(ref entity);
                pool.Remove(entity.Index);
            }
        }
    }


    /// <summary>
    /// Execute every frame
    /// </summary>
    public interface ISystem {
        void OnCreate(World world); // ReSharper disable Unity.PerformanceAnalysis
        void OnUpdate(float dt);
    }

    sealed class ClearEventsSystem<T> : ISystem where T: struct, IComponent {
        private TagPool<T> _pool;
        private Query _query;
        public void OnCreate(World world) {
            _pool = (TagPool<T>)world.GetPool<T>();
            _query = world.GetQuery().With<T>();
        }
        public void OnUpdate(float dt) {
            if (!_query.IsEmpty) {
                foreach (var entity in _query) {
                    _pool.Remove(entity.Index);   
                }
            }
        }
    }
    public struct DestroyEntity : IComponent { }
    sealed class DestroyEntitiesSystem : ISystem {
        private Query query;
        private World _world;
        public void OnCreate(World world) {
            query = world.GetQuery().With<DestroyEntity>();
            _world = world;
        }

        public void OnUpdate(float dt) {
            if(query.IsEmpty) return;
            foreach (ref var entity in query) {
                _world.OnDestroyEntity(in entity);
            }
        }
    }

    public abstract class SkipFrameSystem : ISystem {
        private int skip;
        private int counter;
        private float skippedDeltaTime;
        protected void Skip(int frames) {
            skip = frames;
            counter = 0;
        }
        public abstract void OnCreate(World world);
        void ISystem.OnUpdate(float dt) {
            if (counter == 0) {
                Execute(skippedDeltaTime);
                skippedDeltaTime = 0f;
                counter = skip;
            }
            else {
                counter--;
                skippedDeltaTime += dt;
            }
        }
        protected abstract void Execute(float skippedDeltaTime);
    }
    public sealed class DefaultSystems {
        internal readonly List<ISystem> Start = new ();
        internal readonly List<ISystem> End = new ();
        internal bool Enabled = true;
        internal void Disable() => Enabled = false;

        internal void Init() {
            End.Add(new DestroyEntitiesSystem());
        }
    }
    public sealed class Systems {
        private readonly DefaultSystems _defaultSystems;
        private readonly List<Group> _groups;
        private readonly World _world;
        private ISystem[] _updates;
        private int _updatesCount;
        public Systems(World world) {
            _world = world;
            _updates = new ISystem[32];
            _updatesCount = 0;
            _groups = new List<Group>();
            _defaultSystems = new DefaultSystems();
        }

        public void Init() {
            _defaultSystems.Init();
            if(_defaultSystems.Enabled)
                foreach (var system in _defaultSystems.Start)
                    AddSystem(system);

            foreach (var group in _groups)
                for (var i = 0; i < group.count; i++) {
                    var s = group._systems[i];
                    AddSystem(s);
                }
            if(_defaultSystems.Enabled)
                foreach (var system in _defaultSystems.End)
                    AddSystem(system);
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
            return this;
        }

        public void Update(float dt) {
            for (var i = 0; i < _updatesCount; i++) {
                _updates[i].OnUpdate(dt);
                _world.UpdateQueries();
            }
        }

        public sealed class Group {
            private string Name;
            internal ISystem[] _systems;
            internal int count;

            public Group() {
                _systems = new ISystem[8];
            }

            public Group(string name) {
                Name = name;
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
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            var pool = World.Get(entity.WorldIndex).GetPool<T>();
            if (pool.Has(entity.Index)) return ref pool.Get(entity.Index);
            pool.Add(entity.Index);
            return ref pool.Get(entity.Index);
        }

        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Add(entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
        }

        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPool<T>().Add(in component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
        }

        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Remove(entity.Index);
            world.ChangeComponentsAmount(in entity, -1);
        }

        public static bool Has<T>(in this Entity entity) where T : struct, IComponent {
            return World.Get(entity.WorldIndex).GetPoolByIndex(Component<T>.Index).Has(entity.Index);
        }

        public static sbyte ComponentsAmount(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity);
        }

        public static void Destroy(in this Entity entity) {
            World.Get(entity.WorldIndex).GetPoolByIndex(0).Add(entity.Index);
        }
        public static void DestroyNow(in this Entity entity) {
            World.Get(entity.WorldIndex).OnDestroyEntity(in entity);
        }
    }
}