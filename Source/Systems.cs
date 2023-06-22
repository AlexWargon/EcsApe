using System;
using System.Collections.Generic;
using System.Reflection;
using Wargon.Ecsape.Pools;

namespace Wargon.Ecsape {
    public sealed class Systems {
        private readonly DefaultSystems defaultSystems;
        private readonly List<Group> groups;
        private readonly World world;
        private ISystem[] updates;
        private ISystem[] lateUpdates;
        private ISystem[] fixedUpdates;
        private int updatesCount;
        private IDependencyContainer dependencyContainer;
        private bool hasDependencyContainer;
        public Systems(World world) {
            this.world = world;
            updates = new ISystem[32];
            updatesCount = 0;
            groups = new List<Group>();
            defaultSystems = new DefaultSystems();

            defaultSystems.Init();
            foreach (var system in defaultSystems.start)
                AddSystem(system);
        }

        public Systems Init() {

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

            //InitDependencies();
            return this;
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
            hasDependencyContainer = true;
            return this;
        }

        private void InitDependencies(ISystem system) {
            if (hasDependencyContainer)
                dependencyContainer.Build(system);
            
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
                
                if (fieldInfo.FieldType == typeof(Query)) {
                    var attributes = fieldInfo.GetCustomAttributes(true);
                    
                    foreach (var attribute in attributes) {
                        if (attribute is WithAttribute with) {
                            var query = world.GetQuery().WithAll(with.Types);
                            fieldInfo.SetValue(system, query);
                        }
                        if (attribute is WithoutAttribute without) {
                            var query = world.GetQuery().WithNone(without.Types);
                            fieldInfo.SetValue(system, query);
                        }
                    }
                }
            }
        }
        private void InitDependencies() {
            if (hasDependencyContainer)
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

        

        public Systems Clear<T>() where T : struct, IComponent {
            AddSystem(new ClearEventsSystem<T>());
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
            InitDependencies(system);
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

        internal Systems AddReactive<T>() where T : class, IOnAdd, ISystem, new() {
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

        internal void Update(float dt) {
            for (var i = 0; i < updatesCount; i++) {
                updates[i].OnUpdate(dt);
                world.UpdateQueries();
            }
        }
        
        internal void LateUpdate(float dt) {
            for (var i = 0; i < updatesCount; i++) {
                lateUpdates[i].OnUpdate(dt);
                world.UpdateQueries();
            }
        }
        
        internal void FixedUpdate(float dt) {
            for (var i = 0; i < updatesCount; i++) {
                fixedUpdates[i].OnUpdate(dt);
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
                world.OnDestroyEntity(ref entity);
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
    
    public sealed class SyncTransformsSystem : ISystem {
        private Query query;
        private IPool<Components.TransformReference> transforms;
        private IPool<Components.Translation> translations;
        public void OnCreate(World world) {
            query = world.GetQuery()
                .With<Components.Translation>()
                .With<Components.TransformReference>()
                .Without<Components.StaticTag>();
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