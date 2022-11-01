using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Escape {
    public readonly ref struct Component<T> where T : struct, IComponent {
        public static readonly int Index;
        public static readonly Type Type;
        public static readonly bool IsSingleTone;
        public static readonly bool IsTag;

        static Component() {
            Type = typeof(T);
            Index = Component.GetIndex(Type);
            IsSingleTone = typeof(ISingletoneComponent).IsAssignableFrom(Type);
            IsTag = Type.GetFields().Length == 0;
        }

        public Component(int idx, bool singleTone, bool tag) {
            index = idx;
            isSingleTone = singleTone;
            isTag = tag;
        }
        public readonly int index;
        public readonly bool isSingleTone;
        public readonly bool isTag;
        public static Component<T> AsRef() {
            return new Component<T>(Index, IsSingleTone, IsTag);
        }
        public static ComponentInfo AsComponentInfo() {
            return new ComponentInfo(Index, Type, IsSingleTone, IsTag);
        }
    }

    public readonly struct ComponentInfo {
        public readonly int Index;
        public readonly Type Type;
        public readonly bool IsSingletone;
        public readonly bool IsTag;

        public ComponentInfo(int index, Type type, bool isSingletone, bool isTag) {
            Index = index;
            Type = type;
            IsSingletone = isSingletone;
            IsTag = isTag;
        }
    }

    internal struct ComponentTypes {
        public static int Count;
    }

    public struct Component {
        private static readonly Dictionary<int, Type> typeByIndex;
        private static readonly Dictionary<Type, int> indexByType;

        static Component() {
            typeByIndex = new Dictionary<int, Type>();
            indexByType = new Dictionary<Type, int>();
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
    }

    public interface IComponent { }

    public interface ISingletoneComponent { }

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
            var type = Component.GetComponentType(typeIndex);
            var poolType = type.MakeGenericType(typeof(Pool<>));
            var pool = (IPool)Activator.CreateInstance(poolType, new[] { size });
            return pool;
        }

        void Resize(int newSize);
        IComponent GetRaw(int index);
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
            data[count] = default;
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

    public sealed class EntityTestSystem : IEntitySystem {
        public void OnCreate(World world) {
            Query = world.GetQuery().With<TestEvent>();
        }

        public Query Query { get; set; }

        public void Execute(ref Entity entity, float deltaTime) { }
    }

    public interface IOnAdd {
        void Execute(ref Entity entity);
    }

    internal sealed class TestTrigger : ITriggerSystem {
        public Query Trigger { get; set; }

        public void OnCreate(World world) {
            Trigger = world.GetQuery().With<TestEvent>();
        }

        public void Execute(ref Entity entity) { }
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

    internal struct TestEvent : IComponent { }

    /// <summary>
    ///     Execute every frame
    /// </summary>
    public interface ISystem {
        void OnCreate(World world); // ReSharper disable Unity.PerformanceAnalysis
        void OnUpdate(float dt);
    }

    public sealed class Systems {
        private readonly List<Group> _groups;
        private readonly World _world;
        private ISystem[] _updates;
        private int updatesCount;

        public Systems(World world) {
            _world = world;
            _updates = new ISystem[32];
            updatesCount = 0;
            _groups = new List<Group>();
        }

        public void Init() {
            foreach (var group in _groups)
                for (var i = 0; i < group.count; i++) {
                    var s = group._systems[i];
                    s.OnCreate(_world);
                    AddSystem(s);
                }
        }

        public Systems Add<T>() where T : class, ISystem, new() {
            var t = new T();
            t.OnCreate(_world);
            AddSystem(t);
            return this;
        }

        public Systems Add<T>(T system) where T : class, ISystem {
            system.OnCreate(_world);
            AddSystem(system);
            return this;
        }

        private void AddSystem(ISystem system) {
            if (updatesCount >= _updates.Length - 1) Array.Resize(ref _updates, _updates.Length << 1);
            _updates[updatesCount] = system;
            updatesCount++;
        }

        public Systems AddReactive<T>() where T : class, IOnAdd, ISystem, new() {
            var t = new T();
            t.OnCreate(_world);
            if (updatesCount >= _updates.Length - 1) Array.Resize(ref _updates, _updates.Length << 1);
            _updates[updatesCount] = t;
            updatesCount++;
            return this;
        }

        public Systems Add(Group group) {
            _groups.Add(group);
            return this;
        }

        public void Update(float dt) {
            for (var i = 0; i < updatesCount; i++) {
                _updates[i].OnUpdate(dt);
                _world.UpdateQueries();
            }
        }

        public sealed class Group {
            private string _name;
            internal ISystem[] _systems;
            internal int count;

            public Group() {
                _systems = new ISystem[8];
            }

            public Group(string name) {
                _name = name;
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
            World.Get(entity.WorldIndex).OnDestroyEntity(in entity);
        }
    }

    public class Archetype {
        private ITurplePool<ITuple> _pool;
        private int[] entities;
        private Mask with;
        private Mask without;

        public static Archetype Build(params Type[] types) {
            var archetype = new Archetype();
            foreach (var type in types) archetype.with.Add(Component.GetIndex(type));
            return new Archetype();
        }

        public ValueTuple<int, float> Get(int index) {
            return (ValueTuple<int, float>)_pool.Get(entities[index]);
        }
    }
}