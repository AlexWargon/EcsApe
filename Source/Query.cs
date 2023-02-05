using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Ecsape {
    internal class ArrayList<T> {
        private T[] buffer;
        public int capacity;
        public int Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ArrayList(int size) {
            Count = 0;
            capacity = size;
            buffer = new T[capacity];
        }

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item) {
            if (capacity <= Count) {
                capacity *= 2;
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
    }

    public class Archetype {
        internal readonly int id;
        internal readonly HashSet<int> mask;
        private readonly Mask maskArray;
        internal readonly ArrayList<Query> queries;
        private int withCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Archetype() {
            mask = new HashSet<int>();
            queries = new ArrayList<Query>(3);
            maskArray = new Mask(mask);
            id = 0;
            withCount = 0;
        }

        internal Archetype(World world, HashSet<int> maskSource, int archetypeId) {
            queries = new ArrayList<Query>(10);
            id = archetypeId;
            withCount = 0;
            mask = maskSource;
            maskArray = new Mask(mask);
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) AddQuery(worldQueries[i]);
        }

        public static Archetype Empty => new();

        public override string ToString() {
            var toString = "Archetype<";

            foreach (var i in mask) toString += $"{Component.GetComponentType(i).Name}, ";
            toString = toString.Remove(toString.Length - 2);
            toString += ">";
            return toString;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEntity(int entityId) {
            for (var i = 0; i < withCount; i++) queries[i].OnAddWith(entityId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEntity(int entityId) {
            for (var i = 0; i < withCount; i++) queries[i].OnRemoveWith(entityId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuery(Query query) {
            for (var q = 0; q < query.without.Count; q++) {
                var type = query.without.Types[q];
                if (mask.Contains(type)) return;
            }

            var checks = 0;
            for (var q = 0; q < query.with.Count; q++)
                if (mask.Contains(query.with.Types[q])) {
                    checks++;
                    if (checks == query.with.Count) {
                        queries.Add(query);
                        withCount++;
                        break;
                    }
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntityFromPools(World world, int entity) {
            for (var i = maskArray.Count - 1; i >= 0; i--) {
                var pool = world.GetPoolByIndex(maskArray.Types[i]);
                if(pool.Has(entity))
                    pool.Remove(entity);
            }
        }
    }



    internal class Migrations {
        private readonly Dictionary<int, Archetype> archetypes;
        private readonly Dictionary<MigrationID, Migration> cachedMigrations;
        private readonly World world;
        private MigrationID currentMigrationID;

        internal Migrations(World worldSource) {
            archetypes = new Dictionary<int, Archetype> {{0, Archetype.Empty}};
            Archetypes = new List<Archetype>();
            cachedMigrations = new Dictionary<MigrationID, Migration>();
            world = worldSource;
            ArchetypesCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetArchetype(int id) {
            return archetypes[id];
        }
        internal Archetype GetOrCreateArchetype(params Type[] types) {
            var mask = new HashSet<int>();
            foreach (var type in types) {
                mask.Add(Component.GetIndex(type));
            }
            var id = GetCustomHashCode(mask);
            if (!archetypes.ContainsKey(id)) {
                var newArchetype = new Archetype(world, mask, id);
                archetypes.Add(id, newArchetype);
            }

            return archetypes[id];
        }
        public List<Archetype> Archetypes { get; }
        
        internal int ArchetypesCount { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCustomHashCode(HashSet<int> mask) {
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
        private void GetMigrationID(in int p1, in int p2, in bool p3) {
            unchecked
            {
                var hash = 17;
                hash += p1;
                hash += p2;
                hash += p3 ? 1 : 0;
                currentMigrationID.value = hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Migrate(int entity, ref int archetypeCurrent, ref int componentType, ref bool add) {
            GetMigrationID(in archetypeCurrent, in componentType, in add);//----+
                                                                //             |
            if (HasMigration(entity, ref archetypeCurrent)) return;//<---------+
        
            CacheArchetypes(archetypeCurrent, componentType, add, out var archetypeFrom, out var archetypeTo);

            CacheMigrations(archetypeFrom.id, archetypeTo.id, componentType, add, currentMigrationID.value,
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
                        if (!toMigrate.HasToRemove(query)) {
                            toMigrate.ToRemove.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.with.Contains(componentType)) {
                        if (!toMigrate.HasToRemove(query)) {
                            toMigrate.ToRemove.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
            }

            for (var i = 0; i < archetypeTo.queries.Count; i++) {
                var query = archetypeTo.queries[i];
                if (add) {
                    if (query.with.Contains(componentType) && !archetypeFrom.mask.Contains(componentType) &&
                        archetypeTo.mask.Contains(componentType)) {
                        if (!toMigrate.HasToAdd(query)) {
                            toMigrate.ToAdd.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.without.Contains(componentType) && archetypeFrom.mask.Contains(componentType) &&
                        !archetypeTo.mask.Contains(componentType)) {
                        if (!toMigrate.HasToAdd(query)) {
                            toMigrate.ToAdd.Add(query);
                            toMigrate.IsEmpty = false;
                        }
                    }
                }
            }

            cachedMigrations.Add(currentMigrationID, toMigrate);
            migration = toMigrate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CacheArchetypes(int archetypeHashFrom, int componentType, bool add, 
            out Archetype archetypeFrom,
            out Archetype archetypeTo) {
            ArchetypesCount++;

            archetypeFrom = archetypes[archetypeHashFrom];

            var mask = new HashSet<int>(archetypeFrom.mask);
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
            if (cachedMigrations.TryGetValue(currentMigrationID, out var migration)) {
                migration.Execute(entity);
                archetypeCurrent = migration.Archetype;
                return true;
            }
            return false;
        }
        
        private class Migration {
            internal readonly int Archetype;
            internal readonly int ComponentType;
            internal readonly int Key;
            internal readonly ArrayList<Query> ToAdd;
            internal readonly ArrayList<Query> ToRemove;
            internal bool IsEmpty;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Migration(int key, int componentType, int archetype) {
                Archetype = archetype;
                ComponentType = componentType;
                Key = key;
                ToAdd = new ArrayList<Query>(1);
                ToRemove = new ArrayList<Query>(1);
                IsEmpty = true;
            }

            public bool HasToAdd(Query query) {
                for (int i = 0; i < ToAdd.Count; i++) {
                    if (ToAdd[i].Index == query.Index)
                        return true;
                }
                return false;
            }
            public bool HasToRemove(Query query) {
                for (int i = 0; i < ToRemove.Count; i++) {
                    if (ToRemove[i].Index == query.Index)
                        return true;
                }
                return false;
            }
            public override string ToString() {
                return $"Migration #{Key} with component : {Component.GetComponentType(ComponentType).Name}";
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Execute(int entity) {
                if (IsEmpty) return;
                for (var i = 0; i < ToAdd.Count; i++) ToAdd[i].OnAddWith(entity);

                for (var i = 0; i < ToRemove.Count; i++) ToRemove[i].OnRemoveWith(entity);
            }
        }
        
        internal struct MigrationID : IEquatable<MigrationID> {
            internal int value;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(MigrationID other) {
                return other.value == value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int GetHashCode() {
                return value;
            }
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
        public static Query With(this Query query, Type type) {
            var typeIndex = Component.GetIndex(type);
            query.with.Add(typeIndex);
            return query;
        }
        public static Query Aspect<T>(this Query query) where T : struct, IAspect {
            T aspect = default;
            var types = aspect.Link();
            foreach (var type in types) query.With(type);
            return query;
        }
    }

    public class Query {
        internal int count;
        internal int[] entities;
        internal int[] entityMap;
        internal int entityToUpdateCount;
        internal EntityToUpdate[] entityToUpdates;
        internal int Index;

        internal int indexInside;
        internal bool IsDirty;
        internal Mask with;
        internal Mask without;

        public Query(World world) {
            WorldInternal = world;
            entities = new int[256];
            entityMap = new int[256];
            with = new Mask(10);
            without = new Mask(4);
            entityToUpdates = new EntityToUpdate[256];
            Index = world.QueriesCount;
            count = 0;
        }

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }

        internal World WorldInternal { get; }

        public int FullSize => entities.Length;

        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count == 0;
        }

        public override string ToString() {
            var toString = $"Query({Index}).With<";

            for (var i = 0; i < with.Count; i++) toString += $"{Component.GetComponentType(with.Types[i]).Name} ,";
            toString = toString.Remove(toString.Length - 1);
            toString += ">";

            if (without.Count > 0) {
                toString += ".Without<";
                for (var i = 0; i < without.Count; i++)
                    toString += $"{Component.GetComponentType(without.Types[i]).Name} ,";
                toString = toString.Remove(toString.Length - 1);
                toString += ">";
            }

            return toString;
        }

        internal (int[], int[], EntityToUpdate[], int) GetRaw() {
            return (entities, entityMap, entityToUpdates, count);
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
        public ref Entity Entity(int index) {
            return ref WorldInternal.GetEntity(entities[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(int entity) {
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
        internal void Add(int entity) {
            if (entities.Length - 1 <= count) Array.Resize(ref entities, count + 16);
            if (entityMap.Length - 1 <= entity) Array.Resize(ref entityMap, entity + 16);
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

        public void Reset() {
            index = -1;
        }

        public ref Entity Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref query.Entity(index);
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