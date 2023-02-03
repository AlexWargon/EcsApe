﻿using UnityEngine;

namespace Wargon.Ecsape {
    
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    
    public partial class World {
        private static readonly World[] worlds;
        private static byte lastWorldIndex;
        
        private readonly DirtyQueries dirtyQueries;
        private readonly List<int> freeEntities;
        private readonly byte selfIndex;
        internal byte Index => selfIndex;
        
        private Query [] queries;
        private IPool [] pools;
        private int[] poolKeys;
        private Entity[] entities;
        private sbyte [] entityComponentsAmounts;
        private int[] archetypeIDs;
        private int lastEntity;
        private int activeEntitiesCount;
        private int poolSize;
        private int poolsCount;
        private int queriesCount;
        internal int QueriesCount => queriesCount;
        public int ActiveEntitiesCount => activeEntitiesCount;
        static World() {
            worlds = new World[4];
            lastWorldIndex = 0;
        }

        private World(string name) {
            poolSize = ENTITIES_CACHE;
            pools = new IPool[64];
            poolKeys = new int[64];
            entities = new Entity[ENTITIES_CACHE];
            freeEntities = new List<int>(64);
            queries = new Query[32];
            dirtyQueries = new DirtyQueries(16);
            entityComponentsAmounts = new sbyte[ENTITIES_CACHE];
            archetypeIDs = new int[ENTITIES_CACHE];
            migrations = new Migrations(this);
            selfIndex = lastWorldIndex;
            worlds[selfIndex] = this;
            lastWorldIndex++;
            
            GetPool<DestroyEntity>();
        }
        public static World Default {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Get(DefaultIndex);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref World Get(byte index) {
            return ref worlds[index];
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

        internal Query[] GetQueries() => queries; 
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
                Array.Resize(ref archetypeIDs, poolSize);
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
            var index = entity.Index;
            for (var i = 0; i < poolsCount; i++) {
                ref var pool = ref pools[i];
                if (pool.Has(index))
                    pool.Remove(index);
            }
            
            ref var archetype = ref GetArchetypeId(index);
            migrations.GetArchetype(archetype).RemoveEntity(index);

            archetype = 0;
            freeEntities.Add(index);
            entityComponentsAmounts[index] = 0;
            activeEntitiesCount--;
        }

        public void CreatePool<T>() where T : struct, IComponent {
            var idx = Component<T>.Index;
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
            poolKeys[idx] = poolsCount;
            var pool = IPool.New(ENTITIES_CACHE, idx);
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
        internal ref sbyte ChangeComponentsAmount(in Entity entity, sbyte add) {
            ref var componentsAmount = ref entityComponentsAmounts[entity.Index];
            componentsAmount += add;
            return ref componentsAmount;
        }

        internal sbyte GetComponentAmount(in Entity entity) {
            return entityComponentsAmounts[entity.Index];
        }


        private readonly Migrations migrations;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MigrateEntity(int entity, ref int archetypeFrom, int componentType, bool  add) {
            migrations.Migrate(entity, ref archetypeFrom, ref componentType, ref add);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MigrateEntityGeneric<T>(int entity, int archetypeFrom, bool add) where T : struct, IComponent{
            migrations.MigrateGeneric<T>(entity, archetypeFrom, add);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref int GetArchetypeId(int entity) {
            return ref archetypeIDs[entity];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetArchetype(int entity, int newArchetype) {
            archetypeIDs[entity] = newArchetype;
        }

        public int ArchetypesCountInternal() => migrations.ArchetypesCount;
    }

    public partial class World {
        public const string DEFAULT = "Default";
        public static int ENTITIES_CACHE = 256;
        
        private static readonly Dictionary<string, byte> ids = new();
        private static byte defaultIndex = 255;
        internal static byte DefaultIndex {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (defaultIndex == 255) {
                    defaultIndex = GetOrCreate(DEFAULT).Index;
                }
                return defaultIndex;
            }
        }
        public static World GetOrCreate(string name) {
            name ??= DEFAULT;
            if (ids.TryGetValue(name, out var index))
                return Get(index);
            
            var world = new World(name);
            ids.Add(name, world.Index);
            Debug.Log($"World {name} was not existed but created");
            return world;
        }
        public static World GetOrCreate() {
            if (ids.TryGetValue(DEFAULT, out var index))
                return Get(index);
            var world = new World(DEFAULT);
            ids.Add(DEFAULT, world.Index);
            Debug.Log($"World {DEFAULT} was not existed but created");
            return world;
        }
        internal static void Add(string name, byte index) {
            if(ids.ContainsKey(name)) return;
            ids.Add(name,index);
        }
    }
    
    public partial class World {
        
        public const string TWEEN = "Tweens";
        
        private static byte tweenIndex = 255;
        public static byte TweenIndex {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (tweenIndex == 255) {
                    tweenIndex = GetOrCreate(TWEEN).Index;
                }
                return tweenIndex;
            }
        }

    }
    
    public struct EntityManager {
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