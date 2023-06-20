namespace Wargon.Ecsape {
    using Pools;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    
    public partial class World {
        
        private static readonly World[] worlds;
        private static byte lastWorldIndex;
        private readonly DirtyQueries dirtyQueries;
        private readonly ArrayList<int> freeEntities;
        private readonly string name;
        internal string Name => name;
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
            this.name = name;
            poolSize = ENTITIES_CACHE;
            pools = new IPool[64];
            poolKeys = new int[64];
            entities = new Entity[ENTITIES_CACHE];
            freeEntities = new ArrayList<int>(64);
            queries = new Query[32];
            dirtyQueries = new DirtyQueries(16);
            entityComponentsAmounts = new sbyte[ENTITIES_CACHE];
            archetypeIDs = new int[ENTITIES_CACHE];
            _archetypes = new Archetypes(this);
            selfIndex = lastWorldIndex;
            worlds[selfIndex] = this;
            lastWorldIndex++;
            
            GetPool<DestroyEntity>();
            systems = new Systems(this);
            systems.AddInjector(DI.GetOrCreateContainer());
            systems.Add<ConvertEntitySystem>();
        }

        public void Init() => systems.Init();

        internal void Destroy() {
            
            for (var i = 0; i < entities.Length; i++) {
                entities[i].DestroyNow();
            }

            for (int i = 0; i < poolsCount; i++) {
                pools[i].Clear();
            }
            Array.Clear(pools, 0, pools.Length);
            Array.Clear(entities, 0, entities.Length);
            Array.Clear(poolKeys, 0, poolKeys.Length);
            freeEntities.Clear();
            Array.Clear(queries, 0, queries.Length);
            Array.Clear(entityComponentsAmounts, 0, entityComponentsAmounts.Length);
            Array.Clear(archetypeIDs, 0, archetypeIDs.Length);
            _archetypes.Clear();
            lastEntity=0;
            activeEntitiesCount=0;
            poolSize=0;
            poolsCount=0;
            queriesCount=0;
        }
        public static World Default {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Get(DefaultIndex);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref World Get(byte index) {
            return ref worlds[index];
        }

        #region Queries

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddDirtyQuery(Query query) {
            if (!query.IsDirty)
                dirtyQueries.Add(query);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateQueries() {
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

        internal Query GetQueryInternal(int index) {
            return queries[index];
        }

        #endregion
        
        #region Entities

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Entity CreateEntity() {

            Entity entity;
            if (freeEntities.Count > 0) {
                entity = entities[freeEntities.Last()];
                freeEntities.RemoveLast();
                activeEntitiesCount++;
                entityComponentsAmounts[entity.Index] = 0;
                entity.alive = true;
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
            entity.alive = true;
            //entity.WorldNative = Native;
            entities[lastEntity] = entity;
            entityComponentsAmounts[entity.Index] = 0;
            lastEntity++;
            activeEntitiesCount++;
            return entity;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref entities[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnDestroyEntity(ref Entity entity) {
            var index = entity.Index;
            ref var archetype = ref GetArchetypeId(index);
            var archetypeRef = _archetypes.GetArchetype(archetype);
            archetypeRef.RemoveEntity(index);
            archetypeRef.RemoveEntityFromPools(index);
            
            archetype = 0;
            freeEntities.Add(index);
            entityComponentsAmounts[index] = -1;
            activeEntitiesCount--;
            entity.alive = false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnDestroyEntity(in Entity entity, ref sbyte componentsAmount) {
            var index = entity.Index;
            ref var archetype = ref GetArchetypeId(index);
            var archetypeRef = _archetypes.GetArchetype(archetype);
            archetypeRef.RemoveEntity(index);

            archetype = 0;
            freeEntities.Add(index);
            componentsAmount = -1;
            activeEntitiesCount--;
        }
        
        #endregion
        
        #region Pools

        public void CreatePool<T>() where T : struct, IComponent {
            var idx = Component<T>.Index;
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
            poolKeys[idx] = poolsCount;
            pools[poolsCount] = IPool.New(ENTITIES_CACHE, idx);
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
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref IPool GetPoolByIndex(int idx) {
            if (idx >= poolKeys.Length - 1) Array.Resize(ref poolKeys, idx + 4);
            if (poolKeys[idx] == 0) {
                if (poolsCount >= pools.Length - 1) Array.Resize(ref pools, idx + 4);
                poolKeys[idx] = poolsCount;
                pools[poolsCount] = IPool.New(poolSize, idx);
                poolsCount++;
            }
            return ref pools[poolKeys[idx]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref sbyte ChangeComponentsAmount(in Entity entity, sbyte add) {
            ref var componentsAmount = ref entityComponentsAmounts[entity.Index];
            componentsAmount += add;
            return ref componentsAmount;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref sbyte GetComponentAmount(in Entity entity) {
            return ref entityComponentsAmounts[entity.Index];
        }
        #endregion
        
        #region Acrhetypes

        private readonly Archetypes _archetypes;

        internal Archetype GetArchetype(in Entity entity) {
            return _archetypes.GetArchetype(archetypeIDs[entity.Index]);
        }
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ChangeEntityArchetype(in int entity, in int componentType, bool add) {
            if(add)
                _archetypes.GetArchetype(archetypeIDs[entity]).TransferAdd(in entity, in componentType);
            else
                _archetypes.GetArchetype(archetypeIDs[entity]).TransferRemove(in entity, in componentType);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref int GetArchetypeId(int entity) {
            return ref archetypeIDs[entity];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(params Type[] components) {
            return _archetypes.GetOrCreateArchetype(components);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(params int[] components) {
            return _archetypes.GetOrCreateArchetype(components);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(ref Span<int> span) {
            return _archetypes.GetOrCreateArchetype(ref span);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(HashSet<int> mask) {
            return _archetypes.GetOrCreateArchetype(mask);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetArchetype(int mask) {
            return _archetypes.GetArchetype(mask);
        }
        #endregion

        #region Internals

        public int ArchetypesCountInternal() => _archetypes.ArchetypesCount;

        public List<Archetype> ArchetypesInternal() => _archetypes.ArchetypesList;

        public IPool[] PoolsInternal() => pools;

        public int PoolsCountInternal() => poolsCount;

        public ref Entity[] EntitiesInternal() => ref entities;

        public int ComponentsCountInternal(Entity entity) {
            return entityComponentsAmounts[entity.Index];
        }
        #endregion

        #region CreateEntities

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<TC1, TC2>(in TC1 component1, in TC2 component2)
            where TC1 : struct, IComponent
            where TC2 : struct, IComponent 
        {
            var e = CreateEntity();
            Span<int> hash = stackalloc int[2] {Component<TC1>.Index, Component<TC2>.Index};
            var archetype = GetArchetype(ref hash);
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            GetPool<TC1>().Add(in component1, e.Index);
            GetPool<TC2>().Add(in component2, e.Index);
            archetype.AddEntity(e.Index);
            return e;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<TC1, TC2, TC3>(in TC1 component1, in TC2 component2, in TC3 component3)
            where TC1 : struct, IComponent
            where TC2 : struct, IComponent
            where TC3 : struct, IComponent 
        {
            var e = CreateEntity();
            
            Span<int> hash = stackalloc int[3] {Component<TC1>.Index, Component<TC2>.Index, Component<TC3>.Index};
            var archetype = GetArchetype(ref hash);
            
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            
            GetPool<TC1>().Add(in component1, e.Index);
            GetPool<TC2>().Add(in component2, e.Index);
            GetPool<TC3>().Add(in component3, e.Index);
            archetype.AddEntity(e.Index);
            return e;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<TC1, TC2, TC3, TC4>(in TC1 component1, in TC2 component2, in TC3 component3, in TC4 component4)
            where TC1 : struct, IComponent
            where TC2 : struct, IComponent
            where TC3 : struct, IComponent
            where TC4 : struct, IComponent
        {
            
            var e = CreateEntity();
            
            Span<int> hash = stackalloc int[4] {Component<TC1>.Index, Component<TC2>.Index, Component<TC3>.Index, Component<TC4>.Index};
            var archetype = GetArchetype(ref hash);
            
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            
            GetPool<TC1>().Add(in component1, e.Index);
            GetPool<TC2>().Add(in component2, e.Index);
            GetPool<TC3>().Add(in component3, e.Index);
            GetPool<TC4>().Add(in component4, e.Index);
            archetype.AddEntity(e.Index);
            return e;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity<TC1, TC2, TC3, TC4, TC5>(in TC1 component1, in TC2 component2, in TC3 component3, in TC4 component4, in TC5 component5)
            where TC1 : struct, IComponent
            where TC2 : struct, IComponent
            where TC3 : struct, IComponent
            where TC4 : struct, IComponent
            where TC5 : struct, IComponent
        {
            
            var e = CreateEntity();
            
            Span<int> hash = stackalloc int[5] {Component<TC1>.Index, Component<TC2>.Index, Component<TC3>.Index, Component<TC4>.Index, Component<TC5>.Index};
            var archetype = GetArchetype(ref hash);
            
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            
            GetPool<TC1>().Add(in component1, e.Index);
            GetPool<TC2>().Add(in component2, e.Index);
            GetPool<TC3>().Add(in component3, e.Index);
            GetPool<TC4>().Add(in component4, e.Index);
            GetPool<TC5>().Add(in component5, e.Index);
            archetype.AddEntity(e.Index);
            return e;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity CreateEntity(ref int[] types)
        {
            var e = CreateEntity();
            Span<int> spanHash = types;
            var archetype = GetArchetype(ref spanHash);
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            foreach (var i in types) {
                GetPoolByIndex(i).Add(e.Index);
            }
            archetype.AddEntity(e.Index);
            return e;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Entity CreateEntity(Span<int> types)
        {
            var e = CreateEntity();
            var archetype = GetArchetype(ref types);
            ref var archetypeId = ref GetArchetypeId(e.Index);
            archetypeId = archetype.id;
            foreach (var i in types) {
                GetPoolByIndex(i).Add(e.Index);
            }
            archetype.AddEntity(e.Index);
            return e;
        }
        #endregion
        
    }
    
    public partial class World {
        public const string DEFAULT = "Default";
        public static int ENTITIES_CACHE = 6000;
        
        private static readonly Dictionary<string, byte> ids = new();
        private static byte defaultIndex = 255;

        private static byte DefaultIndex {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (defaultIndex == 255) {
                    defaultIndex = GetOrCreate(DEFAULT).Index;
                }
                return defaultIndex;
            }
        }
        
        public static World GetOrCreate(string name) {
            if (string.IsNullOrEmpty(name))
                name = DEFAULT;
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

        public static void Destory(World world) {
            ids.Remove(world.name);
            world.Destroy();
            worlds[world.Index] = worlds[lastWorldIndex-1];
            worlds[lastWorldIndex - 1] = null;
            lastWorldIndex--;
            world = null;
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

        internal bool bufferGetten = false;
        private bool bufferCreated;
        internal CommandBuffer _buffer;
        public ref CommandBuffer GetCmdBuffer() {
            bufferGetten = true;
            if (!bufferCreated) {
                _buffer = new CommandBuffer(10000);
                bufferCreated = true;
            }
            return ref _buffer;
        }
        internal ref CommandBuffer GetCmdBufferInternal() {
            if (!bufferCreated) {
                _buffer = new CommandBuffer(10000);
                bufferCreated = true;
            }
            return ref _buffer;
        }

        internal unsafe WorldNative* native;
        internal unsafe WorldNative* Native {
            get {
                if (native == null) {
                    var bfr = GetCmdBufferInternal();
                    fixed (Entity* ent = entities) {
                        var worldNative = new WorldNative{
                            Buffer = &bfr,
                            Entities = ent
                        };
                        this.native = &worldNative;
                    }
                }
                return native;
            }
        }
    }

    public unsafe struct WorldNative {
        public CommandBuffer* Buffer;
        public Entity* Entities;
        
        public ref Entity GetEntity(int index) {
            return ref Entities[index];
        }
    }
    #region Systems
    public partial class World {
        private readonly Systems systems;
        
        public World Add<TSystem>() where TSystem : class, ISystem, new() {
            systems.Add<TSystem>();
            return this;
        }

        public World AddDIContainer(IDependencyContainer container) {
            systems.AddInjector(container);
            return this;
        }
        public World Add<T>(T system) where T : class, ISystem {
            systems.Add(system);
            return this;
        }
        public World AddGroup(Systems.Group group) {
            systems.AddGroup(group);
            return this;
        }
        public void OnUpdate(float deltaTime) {
            systems.Update(deltaTime);
        }
    }

    #endregion Systems
    
    public static class WorldExtensions {
        internal static Dictionary<int, Query> Queries = new Dictionary<int, Query>();
        public static Query GetQuery<T1>(this World world) where T1 : struct, IComponent {
            var key = Component<T1>.Index;
            if (!HasKey(key)) {
                var q = new Query(world).With<T1>();
                Queries.Add(key, q);
                return q;
            }

            return Queries[key];
        }

        public static Query GetQuery<T1, T2>(this World world) 
            where T1 : struct, IComponent 
            where T2 : struct, IComponent 
        {
            Span<int> hash = stackalloc int[2] {Component<T1>.Index, Component<T2>.Index};
            var key = get_hash_code(ref hash);
            if (!HasKey(key)) {
                var q = new Query(world).With<T1>().With<T2>();
                Queries.Add(key, q);
                return q;
            }
            return Queries[key];
        }
        public static Query GetQuery<T1, T2, T3>(this World world) 
            where T1 : struct, IComponent 
            where T2 : struct, IComponent 
            where T3 : struct, IComponent 
        {
            Span<int> hash = stackalloc int[3] {Component<T1>.Index, Component<T2>.Index, Component<T2>.Index };
            var key = get_hash_code(ref hash);
            if (!HasKey(key)) {
                var q = new Query(world).With<T1>().With<T2>().With<T3>();
                Queries.Add(key, q);
                return q;
            }
            return Queries[key];
        }
        public static Query GetQuery<T1, T2, T3, T4>(this World world) 
            where T1 : struct, IComponent 
            where T2 : struct, IComponent 
            where T3 : struct, IComponent 
            where T4 : struct, IComponent 
        {
            Span<int> hash = stackalloc int[4] { Component<T1>.Index, Component<T2>.Index, Component<T3>.Index, Component<T4>.Index };
            var key = get_hash_code(ref hash);
            if (!HasKey(key)) {
                var q = new Query(world).With<T1>().With<T2>().With<T3>().With<T4>();
                Queries.Add(key, q);
                return q;
            }
            return Queries[key];
        }
        private static bool HasKey(int key) {
            return Queries.ContainsKey(key);
        }

        private static int get_hash_code(ref Span<int> array) {
            int hc = array.Length;
            for (var index = 0; index < array.Length; index++) {
                hc = unchecked(hc * 314159 + array[index]);
            }
            return hc;
        }

        private static unsafe T* create_ptr<T>(int size) where T : unmanaged {
            var s = stackalloc T[size];
            return s;
        }
    }
}