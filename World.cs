namespace Wargon.Escape {
    
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    
    public class World {
        private static readonly World[] _worlds;
        private static byte lastWorldIndex;
        
        private readonly DirtyQueries dirtyQueries;
        private readonly Query[] queries;
        private readonly List<int> freeEntities;
        private readonly byte selfIndex;
        private Entity[] entities;
        private sbyte[] entityComponentsAmounts;
        private int lastEntity;
        private IPool[] pools;
        private int poolsCount;
        private int queriesCount;

        static World() {
            _worlds = new World[4];
            lastWorldIndex = 0;
        }

        public World() {
            pools = new IPool[64];
            entities = new Entity[256];
            freeEntities = new List<int>(64);
            queries = new Query[32];
            dirtyQueries = new DirtyQueries(16);
            entityComponentsAmounts = new sbyte[256];
            selfIndex = lastWorldIndex;
            _worlds[selfIndex] = this;
            lastWorldIndex++;
        }

        public static World Default {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                var w = _worlds[0];
                if (w != null) return w;
                w = new World();
                _worlds[0] = w;
                return w;
            }
        }

        public EntityManager EntityManager => new(this);

        public static ref World Get(byte index) {
            return ref _worlds[index];
        }
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
            var q = queries[queriesCount];
            if (q == null) {
                q = new Query(this);
                queries[queriesCount] = q;
                queriesCount++;
            }
            return q;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity() {
            if (freeEntities.Count > 0) {
                ref var e = ref entities[freeEntities.Last()];
                freeEntities.RemoveLast();
                return e;
            }
            Entity entity;
            if (entities.Length - 1 <= lastEntity) {
                var newSize = entities.Length * 2;
                for (var i = 0; i < poolsCount; i++) pools[i].Resize(newSize);
                Array.Resize(ref entities, newSize);
                Array.Resize(ref entityComponentsAmounts, newSize);
            }
            entity.Index = lastEntity;
            entity.WorldIndex = selfIndex;
            entities[lastEntity] = entity;
            lastEntity++;
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
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPool<T> GetPool<T>() where T : struct, IComponent {
            var info = Component<T>.AsRef();
            var idx = info.index;
            if (idx >= pools.Length - 1) Array.Resize(ref pools, idx << 1);
            var pool = (IPool<T>)pools[idx];
            if (pool == null) {
                if (info.isTag || info.isSingleTone) pools[idx] = new TagPool<T>(256).AsIPool();
                else pools[idx] = new Pool<T>(256).AsIPool();
                pool = (IPool<T>)pools[idx];
                poolsCount++;
            }

            return pool;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IPool GetPoolByIndex(int index) {
            return pools[index];
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