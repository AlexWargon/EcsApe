namespace Wargon.Ecsape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    
    internal class ArrayList<T> {
        private T[] buffer;
        public int capacity;
        public int Count;
        private readonly int ResizeStep;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ArrayList(int size, int resizeStep = 16) {
            Count = 0;
            capacity = size;
            buffer = new T[capacity];
            this.ResizeStep = resizeStep;
        }

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item) {
            if (capacity <= Count) {
                capacity += ResizeStep;
                Array.Resize(ref buffer, capacity);
            }

            buffer[Count++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Last() {
            return ref buffer[Count - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveLast() {
            Count--;
        }

        public Span<T> AsSpan() {
            Span<T> span = buffer;
            return span;
        }
    }

    public sealed class Archetype {
        public readonly int id;
        internal readonly HashSet<int> hashMask;
        internal readonly Mask maskArray;
        internal readonly ArrayList<Query> queries;
        private readonly byte _worldIndex;
        private int _queriesCount;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Archetype() {
            hashMask = new HashSet<int>();
            queries = new ArrayList<Query>(3);
            maskArray = new Mask(hashMask);
            id = 0;
            _queriesCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype(World world, HashSet<int> hashMaskSource, int archetypeId) {
            queries = new ArrayList<Query>(10);
            id = archetypeId;
            _queriesCount = 0;
            hashMask = hashMaskSource;
            maskArray = new Mask(hashMask);
            _worldIndex = world.Index;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) FilterQuery(worldQueries[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype(World world, ref Span<int> maskSource, int archetypeId) {
            queries = new ArrayList<Query>(10);
            id = archetypeId;
            _queriesCount = 0;
            hashMask = new HashSet<int>();
            foreach (var i in maskSource) {
                hashMask.Add(i);
            }

            maskArray = new Mask(hashMask);
            _worldIndex = world.Index;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) FilterQuery(worldQueries[i]);
        }

        public static Archetype Empty => new();

        public override string ToString() {
            var toString = "Archetype<";

            foreach (var i in hashMask) toString += $"{Component.GetComponentType(i).Name}, ";
            toString = toString.Remove(toString.Length - 2);
            toString += ">";
            return toString;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddEntity(int entityId) {
            for (var i = 0; i < _queriesCount; i++) queries[i].OnAddWith(entityId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntity(int entityId) {
            for (var i = 0; i < _queriesCount; i++) queries[i].OnRemoveWith(entityId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FilterQuery(Query query) {
            for (var q = 0; q < query.without.Count; q++) {
                var type = query.without.Types[q];
                if (hashMask.Contains(type)) return;
            }

            var checks = 0;
            for (var q = 0; q < query.with.Count; q++)
                if (hashMask.Contains(query.with.Types[q])) {
                    checks++;
                    if (checks == query.with.Count) {
                        queries.Add(query);
                        _queriesCount++;
                        break;
                    }
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntityFromPools(World world, int entity) {
            for (var i = 0; i < maskArray.Count; i++) {
                var pool = world.GetPoolByIndex(maskArray.Types[i]);
                if (pool.Has(entity))
                    pool.Remove(entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity() {
            var world = World.Get(_worldIndex);
            var e = world.CreateEntity();
            ref var componentsAmount = ref world.GetComponentAmount(in e);
            ref var archetype = ref world.GetArchetypeId(e.Index);
            for (var i = 0; i < maskArray.Count; i++) {
                world.GetPoolByIndex(maskArray.Types[i]).Add(e.Index);
                componentsAmount++;
            }

            archetype = id;
            AddEntity(e.Index);
            return e;
        }

        public object[] GetComponents(in Entity entity) {
            var world = World.Get(_worldIndex);
            var components = new object[hashMask.Count];
            for (var i = 0; i < components.Length; i++) {
                components[i] = world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index);
            }

            return components;
        }

        public Span<ComponentType> GetComponentTypes() {
            Span<ComponentType> span = new ComponentType[maskArray.Count];
            for (var i = 0; i < maskArray.Count; i++) {
                span[i] = Component.GetComponentType(maskArray.Types[i]);
            }
            return span;
        }
    }

    public unsafe struct FastArray<T> : IDisposable where T : unmanaged {
        private T* _buffer;
        private int _count;
        private int _capacity;

        internal FastArray(int size) {
            _count = 0;
            _buffer = (T*) Marshal.AllocCoTaskMem(sizeof(T) * size);
            _capacity = size;
        }

        public void Dispose() {
            Marshal.FreeCoTaskMem((IntPtr) _buffer);
        }

        public void Add(T item) {
            if (_capacity - 1 <= _count) {
                _buffer = UnsafeHelp.Resize(_buffer, _capacity, _capacity * 2);
                _capacity *= 2;
            }

            _buffer[_count++] = item;
        }

        public Span<T> ToSpan() => new(_buffer, _count);
    }

    public class Migrations {
        private readonly Dictionary<int, Archetype> archetypes;
        private readonly Dictionary<IntKey, Migration> cachedMigrations;
        private readonly World world;
        private IntKey currentMigrationKey;

        internal Migrations(World worldSource) {
            archetypes = new Dictionary<int, Archetype> {{0, Archetype.Empty}};
            Archetypes = new List<Archetype>();
            cachedMigrations = new Dictionary<IntKey, Migration>();
            world = worldSource;
            ArchetypesCount = 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetArchetype(int id) {
            return archetypes[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(ref Span<int> types) {
            var id = GetCustomHashCode(ref types);
            if (!archetypes.ContainsKey(id)) {
                var newArchetype = new Archetype(world, ref types, id);
                archetypes.Add(id, newArchetype);
                Archetypes.Add(newArchetype);
            }

            return archetypes[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(params int[] types) {
            var id = GetCustomHashCode(ref types);
            if (!archetypes.ContainsKey(id)) {
                var mask = new HashSet<int>(types);
                var newArchetype = new Archetype(world, mask, id);
                archetypes.Add(id, newArchetype);
                Archetypes.Add(newArchetype);
            }

            return archetypes[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(params Type[] types) {
            Span<int> typesSpan = stackalloc int[types.Length];

            for (int i = 0; i < typesSpan.Length; i++) {
                typesSpan[i] = Component.GetIndex(types[i]);
            }

            var id = GetCustomHashCode(ref typesSpan);
            if (!archetypes.ContainsKey(id)) {
                var newArchetype = new Archetype(world, ref typesSpan, id);
                archetypes.Add(id, newArchetype);
                Archetypes.Add(newArchetype);
            }

            return archetypes[id];
        }

        public List<Archetype> Archetypes;
        public List<Migration> GetMigrations() => cachedMigrations.Values.ToList();
        internal int ArchetypesCount { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCustomHashCode(HashSet<int> mask) {
            unchecked {
                const int p = 16777619;
                var hash = (int) 2166136261;

                foreach (var i in mask) hash = (hash ^ i) * p;
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetCustomHashCode(ref Span<int> mask) {
            unchecked {
                const int p = 16777619;
                var hash = (int) 2166136261;

                for (var index = 0; index < mask.Length; index++) {
                    ref var i = ref mask[index];
                    hash = (hash ^ i) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCustomHashCode(ref int[] mask) {
            unchecked {
                const int p = 16777619;
                var hash = (int) 2166136261;

                var s = mask.Length;
                for (var i = 0; i < s; i++) {
                    hash = (hash ^ mask[i]) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetMigrationID(in int p1, in int p2, in bool p3) {
            unchecked {
                var hash = 17;
                hash += p1;
                hash += p2;
                hash += p3 ? 1 : -1;
                currentMigrationKey.value = hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Migrate(int entity, ref int archetypeCurrent, ref int componentType, ref bool add) {
            GetMigrationID(in archetypeCurrent, in componentType, in add); //----+
                                                                 //             |
            if (HasMigration(entity, ref archetypeCurrent)) return; //<---------+

            CacheArchetypes(archetypeCurrent, componentType, add, out var archetypeFrom, out var archetypeTo);

            CacheMigrations(archetypeFrom.id, archetypeTo.id, componentType, add, currentMigrationKey.value,
                out var migration);
            migration.Execute(entity);
            archetypeCurrent = migration.Archetype;
        }

        private void CacheMigrations(int archetypeCurrent, int archetypeToId, int componentType, bool add, int key,
            out Migration migration) {
            var archetypeFrom = archetypes[archetypeCurrent];
            var archetypeTo = archetypes[archetypeToId];

            var toMigrate = new Migration(key, componentType, archetypeToId);

            for (var i = 0; i < archetypeFrom.queries.Count; i++) {
                var query = archetypeFrom.queries[i];
                if (add) {
                    if (query.without.Contains(componentType)) {
                        if (!toMigrate.HasQueryToRemoveEntity(query)) {
                            toMigrate.QueriesToRemoveEntity.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.with.Contains(componentType)) {
                        if (!toMigrate.HasQueryToRemoveEntity(query)) {
                            toMigrate.QueriesToRemoveEntity.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
            }

            for (var i = 0; i < archetypeTo.queries.Count; i++) {
                var query = archetypeTo.queries[i];
                if (add) {
                    if (query.with.Contains(componentType) && !archetypeFrom.hashMask.Contains(componentType) &&
                        archetypeTo.hashMask.Contains(componentType)) {
                        if (!toMigrate.HasQueryToAddEntity(query)) {
                            toMigrate.QueriesToAddEntity.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.without.Contains(componentType) && archetypeFrom.hashMask.Contains(componentType) &&
                        !archetypeTo.hashMask.Contains(componentType)) {
                        if (!toMigrate.HasQueryToAddEntity(query)) {
                            toMigrate.QueriesToAddEntity.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
            }

            cachedMigrations.Add(currentMigrationKey, toMigrate);
            migration = toMigrate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CacheArchetypes(int archetypeHashFrom, int componentType, bool add,
            out Archetype archetypeFrom,
            out Archetype archetypeTo) {
            ArchetypesCount++;

            archetypeFrom = archetypes[archetypeHashFrom];

            var mask = new HashSet<int>(archetypeFrom.hashMask);
            if (add)
                mask.Add(componentType);
            else
                mask.Remove(componentType);

            var archetypeId = GetCustomHashCode(mask);
            if (archetypes.ContainsKey(archetypeId)) {
                archetypeTo = archetypes[archetypeId];
            }
            else {
                archetypeTo = new Archetype(world, mask, archetypeId);
                archetypes.Add(archetypeId, archetypeTo);
                Archetypes.Add(archetypeTo);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMigration(int entity, ref int archetypeCurrent) {
            if (cachedMigrations.TryGetValue(currentMigrationKey, out var migration)) {
                migration.Execute(entity);
                archetypeCurrent = migration.Archetype;
                return true;
            }

            return false;
        }

        public class Migration {
            internal readonly int Archetype;
            internal readonly int ComponentType;
            internal readonly int Key;
            internal readonly ArrayList<Query> QueriesToAddEntity;
            internal readonly ArrayList<Query> QueriesToRemoveEntity;
            internal bool IsEmpty;

            public (int, Type, int, bool) GetData() {
                return (Archetype, Component.GetTypeOfComponent(ComponentType), Key, IsEmpty);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Migration(int key, int componentType, int archetype) {
                Archetype = archetype;
                ComponentType = componentType;
                Key = key;
                QueriesToAddEntity = new ArrayList<Query>(1);
                QueriesToRemoveEntity = new ArrayList<Query>(1);
                IsEmpty = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int entity) {
                if (IsEmpty) return;
                for (var i = 0; i < QueriesToAddEntity.Count; i++) QueriesToAddEntity[i].OnAddWith(entity);

                for (var i = 0; i < QueriesToRemoveEntity.Count; i++) QueriesToRemoveEntity[i].OnRemoveWith(entity);
            }
            
            public bool HasQueryToAddEntity(Query query) {
                for (int i = 0; i < QueriesToAddEntity.Count; i++) {
                    if (QueriesToAddEntity[i].Index == query.Index)
                        return true;
                }

                return false;
            }

            public bool HasQueryToRemoveEntity(Query query) {
                for (int i = 0; i < QueriesToRemoveEntity.Count; i++) {
                    if (QueriesToRemoveEntity[i].Index == query.Index)
                        return true;
                }

                return false;
            }

            public override string ToString() {
                return $"{Key} Migration with component:({Component.GetTypeOfComponent(ComponentType).Name})" +
                       $"IsEmpty:({IsEmpty}) ToAdd:({QueriesToAddEntity.Count}), ToRemove:({QueriesToRemoveEntity.Count});";
            }

        }


    }
    
    internal struct IntKey : IEquatable<IntKey> {
        internal int value;

        public IntKey(int key) {
            value = key;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IntKey other) {
            return other.value == value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() {
            return value;
        }
    }
    
    public static class QueryExtensions {
        public static Query Without<T>(this Query query) where T : struct, IComponent {
            query.without.Add(Component<T>.Index);
            return query;
        }

        public static Query With<T>(this Query query) where T : struct, IComponent {
            query.with.Add(Component<T>.Index);
            return query;
        }
        public static Query WithAll<T1,T2>(this Query query) 
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            query.with.Add(Component<T1>.Index);
            query.with.Add(Component<T2>.Index);
            return query;
        }
        
        public static Query WithAll<T1,T2,T3>(this Query query) 
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            query.with.Add(Component<T1>.Index);
            query.with.Add(Component<T2>.Index);
            query.with.Add(Component<T3>.Index);
            return query;
        }
        
        public static Query WithAll<T1,T2,T3,T4>(this Query query) 
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            query.with.Add(Component<T1>.Index);
            query.with.Add(Component<T2>.Index);
            query.with.Add(Component<T3>.Index);
            query.with.Add(Component<T4>.Index);
            return query;
        }
        
        public static Query WithAll<T1,T2,T3,T4,T5>(this Query query) 
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
            where T5 : struct, IComponent
        {
            query.with.Add(Component<T1>.Index);
            query.with.Add(Component<T2>.Index);
            query.with.Add(Component<T3>.Index);
            query.with.Add(Component<T4>.Index);
            query.with.Add(Component<T5>.Index);
            return query;
        }
        
        public static Query WithAll<T1,T2,T3,T4,T5,T6>(this Query query) 
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
            where T5 : struct, IComponent
            where T6 : struct, IComponent
        {
            query.with.Add(Component<T1>.Index);
            query.with.Add(Component<T2>.Index);
            query.with.Add(Component<T3>.Index);
            query.with.Add(Component<T4>.Index);
            query.with.Add(Component<T5>.Index);
            query.with.Add(Component<T6>.Index);
            return query;
        }
        public static Query With(this Query query, Type type) {
            query.with.Add(Component.GetIndex(type));
            return query;
        }

        public static Query Aspect<T>(this Query query) where T : struct, IAspect {
            T aspect = default;
            var types = aspect.Link();
            foreach (var type in types) query.With(type);
            return query;
        }
    }

    public unsafe class WrappedNativeArray<T> where T : unmanaged {
        private T* ptr;
        private T[] array;
        private int len;

        public T this[int index] {
            get => ptr[index];
            set => ptr[index] = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WrappedNativeArray(int size) {
            array = new T[size];
            fixed (T* arrayPtr = array) {
                ptr = arrayPtr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newSize) {
            len = newSize;
            Array.Resize(ref array, len);
            fixed (T* arrayPtr = array) {
                ptr = arrayPtr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize() {
            len += 16;
            Array.Resize(ref array, len);
            fixed (T* arrayPtr = array) {
                ptr = arrayPtr;
            }
        }
    }
    public class Query {
        internal int count;
        internal int[] entities;
        internal int[] entityMap;
        internal unsafe int* entitiesPtr;
        internal unsafe int* entityMapPtr;
        
        internal int entityToUpdateCount;
        internal EntityToUpdate[] entityToUpdates;
        
        internal int Index;
        internal int indexInside;
        
        internal Mask with;
        internal Mask without;
        internal Mask any;
        internal bool IsDirty;
        public Query(World world) {
            WorldInternal = world;
            entities = new int[256];
            entityMap = new int[256];
            with = new Mask(10);
            without = new Mask(4);
            any = new Mask(6);
            entityToUpdates = new EntityToUpdate[256];
            Index = world.QueriesCount;
            count = 0;
            unsafe {
                entitiesPtr = entities.GetPtr();
                entityMapPtr = entityMap.GetPtr();
            }
        }

        internal World WorldInternal;

        public int FullSize {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get=> entities.Length;
        }

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }
        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count == 0;
        }

        public override string ToString() {
            var toString = $"Query({Index}).With<";

            for (var i = 0; i < with.Count; i++) toString += $"{Component.GetTypeOfComponent(with.Types[i]).Name} ,";
            toString = toString.Remove(toString.Length - 1);
            toString += ">";

            if (without.Count > 0) {
                toString += ".Without<";
                for (var i = 0; i < without.Count; i++)
                    toString += $"{Component.GetTypeOfComponent(without.Types[i]).Name} ,";
                toString = toString.Remove(toString.Length - 1);
                toString += ">";
            }

            return toString;
        }

        internal (int[], int[], EntityToUpdate[], int) GetRaw() {
            return (entities, entityMap, entityToUpdates, count);
        }

        internal unsafe int* GetEntitiesPtr() {
            return entitiesPtr;
        }

        internal unsafe int* GetEntitiesMapPtr() {
            return entityMapPtr;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnAddWith(int entity) {
            if (entityToUpdates.Length <= entityToUpdateCount)
                Array.Resize(ref entityToUpdates, entityToUpdateCount + 16);
            ref var e = ref entityToUpdates[entityToUpdateCount];
            e.entity = entity;
            e.add = true;
            entityToUpdateCount++;
            WorldInternal.AddDirtyQuery(this);
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnRemoveWith(int entity) {
            if (entityToUpdates.Length <= entityToUpdateCount)
                Array.Resize(ref entityToUpdates, entityToUpdateCount + 16);
            ref var e = ref entityToUpdates[entityToUpdateCount];
            e.entity = entity;
            e.add = false;
            entityToUpdateCount++;
            WorldInternal.AddDirtyQuery(this);
            IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref WorldInternal.GetEntity(entities[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Remove(int entity) {
            if (!Has(entity)) return;
            var index = entityMap[entity] - 1;
            entityMap[entity] = 0;
            count--;
            if (count > index) {
                entities[index] = entities[count];
                entityMap[entities[index]] = index + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Add(int entity) {
            if (entities.Length - 1 <= count) {
                Array.Resize(ref entities, count + 16);
                unsafe {
                    entitiesPtr = entities.GetPtr();
                }
            }

            if (entityMap.Length - 1 <= entity) {
                Array.Resize(ref entityMap, entity + 16);
                unsafe {
                    entityMapPtr = entityMap.GetPtr();
                }
            }
            if (Has(entity)) return;
            entities[count++] = entity;
            entityMap[entity] = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Update() {
            for (var i = 0; i < entityToUpdateCount; i++) {
                ref var e = ref entityToUpdates[i];
                if (e.add)
                    Add(e.entity);
                else
                    Remove(e.entity);
            }

            entityToUpdateCount = 0;
            IsDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Has(int entity) {
            if (entityMap.Length <= entity)
                return false;
            return entityMap[entity] > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() {
            Enumerator e;
            e.query = this;
            e.index = -1;
            return e;
        }

        internal int GetEntityIndex(int index) {
            return entities[index];
        }

        internal struct EntityToUpdate {
            public int entity;
            public bool add;
        }
    }

    public ref struct Enumerator {
        public Query query;
        public int index;

        public Enumerator(Query query) {
            this.query = query;
            index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            index++;
            return index < query.count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() {
            index = -1;
        }

        public ref Entity Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref query.GetEntity(index);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Mask {
        public readonly int[] Types;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Mask(int size) {
            Types = new int[size];
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Mask(HashSet<int> set) {
            Types = new int[set.Count];
            Count = 0;
            foreach (var i in set) Types[Count++] = i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Mask(ref Span<int> set) {
            Types = new int[set.Length];
            Count = 0;
            foreach (var i in set) Types[Count++] = i;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int type) {
            Types[Count] = type;
            Count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int value) {
            for (var i = 0; i < Count; i++)
                if (Types[i] == value)
                    return true;

            return false;
        }
    }
}