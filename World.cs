using UnityEngine;

namespace Wargon.Ecsape {
    
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    
    public class World {
        private static readonly World[] _worlds;
        private static byte lastWorldIndex;
        
        private readonly DirtyQueries dirtyQueries;
        
        private readonly List<int> freeEntities;
        private readonly byte selfIndex;
        
        private Query [] queries;
        private IPool [] pools;
        private Entity[] entities;
        private sbyte [] entityComponentsAmounts;
        private int[] poolKeys;
        
        private int lastEntity;
        private int activeEntitiesCount;
        private int poolSize = 256;
        private int poolsCount;
        private int queriesCount;
        public int ActiveEntitiesCount => activeEntitiesCount;
        static World() {
            _worlds = new World[4];
            lastWorldIndex = 0;
        }

        public World() {
            pools = new IPool[64];
            poolKeys = new int[64];
            entities = new Entity[256];
            freeEntities = new List<int>(64);
            queries = new Query[32];
            dirtyQueries = new DirtyQueries(16);
            entityComponentsAmounts = new sbyte[256];
            selfIndex = lastWorldIndex;
            _worlds[selfIndex] = this;
            lastWorldIndex++;
            GetPool<DestroyEntity>();
            Worlds.Add("", selfIndex);
        }
        public World(string name) {
            pools = new IPool[64];
            poolKeys = new int[64];
            entities = new Entity[256];
            freeEntities = new List<int>(64);
            queries = new Query[32];
            dirtyQueries = new DirtyQueries(16);
            entityComponentsAmounts = new sbyte[256];
            selfIndex = lastWorldIndex;
            _worlds[selfIndex] = this;
            lastWorldIndex++;
            GetPool<DestroyEntity>();
            Worlds.Add(name, selfIndex);
        }
        public static World Default {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                // var w = _worlds[0];
                // if (w != null) return w;
                // w = new World();
                // _worlds[0] = w;
                // return w;
                return Worlds.Get(Worlds.Default);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref World Get(byte index) {
            return ref _worlds[index];
        }

        public EntityManager EntityManager => new(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddDirtyQuery(Query query) {
            if (!query.IsDirty)
                dirtyQueries.Add(query);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateQueries() {
            dirtyQueries.UpdateQueries();
        }

        public Query GetQuery() {
            if (queries.Length - 1 <= queriesCount) {
                Array.Resize(ref queries, queriesCount+16);
            }
            var q = queries[queriesCount];
            if (q == null) {
                q = new Query(this);
                queries[queriesCount] = q;
                queriesCount++;
            }
            return q;
        }

        public Query GetQuery(params Type[] types) {
            var q = GetQuery();
            foreach (var type in types) {
                q.With(type);
            }
            return q;
        }
        
        public Query GetQuery<T>() where T : struct, IComponent {
            return GetQuery().With<T>();
        }
        
        public Query GetQuery<T1,T2>() 
            where T1 : struct, IComponent  
            where T2 : struct, IComponent 
        {
            return GetQuery().With<T1>().With<T2>();
        }
        
        public Query GetQuery<T1,T2,T3>() 
            where T1 : struct, IComponent  
            where T2 : struct, IComponent  
            where T3 : struct, IComponent  
        {
            return GetQuery().With<T1>().With<T2>().With<T3>();
        }
        public Query GetQuery<T1,T2,T3,T4>() 
            where T1 : struct, IComponent  
            where T2 : struct, IComponent  
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            return GetQuery().With<T1>().With<T2>().With<T3>().With<T4>();
        }
        internal Query GetQueryInternal(int index) {
            return queries[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity() {

            Entity entity;
            if (freeEntities.Count > 0) {
                entity = entities[freeEntities.Last()];
                freeEntities.RemoveLast();
                activeEntitiesCount++;
                return entity;
            }
            if (entities.Length - 1 <= activeEntitiesCount) {
                poolSize *= 2;
                for (var i = 0; i < poolsCount; i++) pools[i].Resize(poolSize);
                Array.Resize(ref entities, poolSize);
                Array.Resize(ref entityComponentsAmounts, poolSize);
            }
            entity.Index = lastEntity;
            entity.WorldIndex = selfIndex;
            entities[lastEntity] = entity;
            lastEntity++;
            activeEntitiesCount++;
            return entity;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref entities[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnDestroyEntity(in Entity entity) {
            for (var i = 0; i < poolsCount; i++) {
                ref var pool = ref pools[i];
                if (pool.Has(entity.Index))
                    pool.Remove(entity.Index);
            }
            freeEntities.Add(entity.Index);
            entityComponentsAmounts[entity.Index] = 0;
            activeEntitiesCount--;
        }

        public void CreatePool<T>() where T : struct, IComponent {
            var idx = Component<T>.Index;
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
            poolKeys[idx] = poolsCount;
            var pool = IPool.New(256, idx);
            pools[poolsCount] = pool;
            poolsCount++;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPool<T> GetPool<T>() where T : struct, IComponent {
            var idx = Component<T>.Index;
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolKeys[idx] == 0) {
                if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
                poolKeys[idx] = poolsCount;
                pools[poolsCount] = IPool.New(poolSize, idx);
                poolsCount++;
            }

            return (IPool<T>)pools[poolKeys[idx]];
            // if (poolsCount <= idx) {
            //     if (idx >= pools.Length - 1) Array.Resize(ref pools, idx << 1);
            //     // var info = Component<T>.AsRef();
            //     // if (info.isTag || info.isSingleTone || info.isEvent) pools[idx] = new TagPool<T>(256).AsIPool();
            //     // else if (info.isSelfNew) pools[idx] = new ClearPool<T>(256).AsIPool();
            //     // else pools[idx] = new Pool<T>(256).AsIPool();
            //     // poolsCount++;
            //     
            //     pools[idx] = IPool.New(256, idx);
            //     poolsCount++;
            // }
            // return (IPool<T>)pools[idx];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IPool GetPoolByIndex(int idx) {
            // if (poolsCount <= idx) {
            //     if (idx >= pools.Length - 1) Array.Resize(ref pools, idx << 1);
            //     pools[idx] = IPool.New(256, idx);
            //     poolsCount++;
            // }
            // return pools[idx];
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolKeys[idx] == 0) {
                if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
                poolKeys[idx] = poolsCount;
                pools[poolsCount] = IPool.New(poolSize, idx);
                poolsCount++;
            }
            return pools[poolKeys[idx]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ChangeComponentsAmount(in Entity entity, sbyte add) {
            ref var componentsAmount = ref entityComponentsAmounts[entity.Index];
            componentsAmount += add;
            if (componentsAmount == 0) OnDestroyEntity(in entity);
        }

        internal sbyte GetComponentAmount(in Entity entity) {
            return entityComponentsAmounts[entity.Index];
        }
    }

    public ref partial struct Worlds {
        public const string Default = "Default";
    }
    
    public ref partial struct Worlds {
        private static readonly Dictionary<string, byte> ids = new Dictionary<string, byte>();
        public const string Tween = "Tweens";
        
        public static World Get(string name) {
            if (ids.TryGetValue(name, out var index))
                return World.Get(index);
            var world = new World(name);
            return world;
        }
        
        internal static void Add(string name, byte index) {
            if(ids.ContainsKey(name)) return;
            ids.Add(name,index);
        }
    }
    
    public readonly ref struct EntityManager {
        private readonly World _world;

        public EntityManager(World world) {
            _world = world;
        }

        public Entity CreateEntity() {
            return _world.CreateEntity();
        }

        public void Add<T>(in T data, in Entity entity) where T : struct, IComponent {
            _world.GetPool<T>().Add(in data, entity.Index);
        }

        public void Add<T>(in Entity entity) where T : struct, IComponent {
            _world.GetPoolByIndex(Component<T>.Index).Add(entity.Index);
        }
    }
}