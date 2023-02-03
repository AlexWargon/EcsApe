using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Ecsape {
    internal class ArrayList<T> {
        private T[] buffer;
        public int capacity;
        public int count;

        internal ArrayList(int size) {
            count = 0;
            capacity = size;
            buffer = new T[capacity];
        }

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref buffer[index];
        }

        public void Add(T item) {
            if (capacity <= count) {
                capacity *= 2;
                Array.Resize(ref buffer, capacity);
            }

            buffer[count++] = item;
        }
    }

    internal class IntMap<T> {
        private int[] set;
        private T[] items;
        private int count;
        public void Add(int key, T value) {
            if (set.Length - 1 < count) {
                
            }
            set[count] = key;
            items[set[count++]] = value;
        }
        public T Get(int key) {
            return items[set[key]];
        }
    }
    internal sealed class Archetype {
        internal readonly int id;
        internal readonly HashSet<int> mask;
        private readonly ArrayList<Query> with;
        private int withCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Archetype() {
            mask = new HashSet<int>();
            with = new ArrayList<Query>(3);
            id = 0;
            withCount = 0;
        }

        internal Archetype(World world, HashSet<int> maskSource, int component, bool add, int archetypeId) {
            with = new ArrayList<Query>(10);
            id = archetypeId;
            withCount = 0;
            mask = maskSource;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) AddQuery(worldQueries[i]);
        }

        public static Archetype Empty => new();

        public override string ToString() {
            var toString = "Archetype<";

            foreach (var i in mask) toString += $"{Component.GetComponentType(i).Name} ,";
            toString = toString.Remove(toString.Length - 1);
            toString += ">";
            return toString;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEntity(int entityId) {
            for (var i = 0; i < withCount; i++) with[i].OnAddWith(entityId);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEntity(int entityId) {
            for (var i = 0; i < withCount; i++) with[i].OnRemoveWith(entityId);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuery(Query query) {
            for (var q = 0; q < query.without.Count; q++) {
                var type = query.without.Types[q];
                if (mask.Contains(type)) {
                    return;
                }
            }
            var checks = 0;
            for (var q = 0; q < query.with.Count; q++)
                if (mask.Contains(query.with.Types[q])) {
                    checks++;
                    if (checks == query.with.Count) {
                        with.Add(query);
                        withCount++;
                        break;
                    }
                }
        }
    }
    
    internal struct Mask<T> where T : struct, IComponent{
        static int archetype;
        static bool add;
        static int index;
        public static int HashAdd;
        public static int HashRemove;

        public static bool Has() {
            return HashAdd != 0;
        }

        public static void Init(int archetypeParam) {
            archetype = archetypeParam;
            index = Component<T>.Index;
            
            unchecked // Overflow is fine, just wrap
            {
                HashAdd = 17;
                // Suitable nullity checks etc, of course :)
                HashAdd = HashAdd * 23 + archetype.GetHashCode();
                HashAdd = HashAdd * 23 + index.GetHashCode();
                HashAdd = HashAdd * 23 + true.GetHashCode();
            }
            unchecked // Overflow is fine, just wrap
            {
                HashRemove = 17;
                // Suitable nullity checks etc, of course :)
                HashRemove = HashRemove * 23 + archetype.GetHashCode();
                HashRemove = HashRemove * 23 + index.GetHashCode();
                HashRemove = HashRemove * 23 + false.GetHashCode();
            }
        }
    }
    internal class Migrations {
        private readonly Dictionary<int, Archetype> archetypes;
        private readonly Dictionary<int, Archetype> migrationsMap;
        private readonly World world;
        private int archetypesCount;
        public int ArchetypesCount => archetypesCount;
        internal Migrations(World worldSource) {
            migrationsMap = new Dictionary<int, Archetype>();
            archetypes = new Dictionary<int, Archetype> {{0, Archetype.Empty}};
            world = worldSource;
            archetypesCount = 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCustomHashCode(HashSet<int> mask) {
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
        private int GetCustomHashCode(ref (int,int,bool) mask) {
            unchecked // Overflow is fine, just wrap
            {
                var hash = 17;
                // Suitable nullity checks etc, of course :)
                hash += mask.Item1;
                hash += mask.Item2;
                hash += mask.Item3 ? 1 : 0;
                return hash;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCustomHashCode(ref int p1, ref int p2, ref bool p3) {
            unchecked // Overflow is fine, just wrap
            {
                var hash = 17;
                hash += p1;
                hash += p2;
                hash += p3 ? 1 : 0;
                return hash;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Archetype GetArchetype(int id) {
            return archetypes[id];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Migrate(int entity, ref int archetypeHashFrom, ref int componentType, ref bool add) {

            var key = GetCustomHashCode(ref archetypeHashFrom, ref componentType, ref add);
            if (HasMigration(entity, archetypeHashFrom, key)) return;

            GetArchetypes(archetypeHashFrom, componentType, add, out var archetypeFrom, out var archetypeTo);

            migrationsMap.Add(key, archetypeTo);
            archetypeFrom.RemoveEntity(entity);
            archetypeTo.AddEntity(entity);
            world.SetArchetype(entity, archetypeTo.id);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GetArchetypes(int archetypeHashFrom, int componentType, bool add,out Archetype archetypeFrom, out Archetype archetypeTo) {
            archetypesCount++;

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
                archetypeTo = new Archetype(world, mask, componentType, add, archetypeId);
                archetypes.Add(archetypeId, archetypeTo);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMigration(int entity, int archetypeHashFrom, int key) {
            if (migrationsMap.TryGetValue(key, out var archetype)) {
                archetypes[archetypeHashFrom].RemoveEntity(entity);
                archetype.AddEntity(entity);
                world.SetArchetype(entity, archetype.id);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MigrateGeneric<T>(int entity, int archetypeHashFrom, bool add) where T: struct, IComponent {
            if (!Mask<T>.Has()) {
                Mask<T>.Init(archetypeHashFrom);
            }
            
            var key = add ? Mask<T>.HashAdd : Mask<T>.HashRemove;
            if (migrationsMap.TryGetValue(key, out var archetype)) {
                archetypes[archetypeHashFrom].RemoveEntity(entity);
                archetype.AddEntity(entity);
                world.SetArchetype(entity, archetype.id);
                return;
            }

            var componentType = Component<T>.Index;
            archetypesCount++;

            var archetypeFrom = archetypes[archetypeHashFrom];
            
            var mask = new HashSet<int>(archetypeFrom.mask);
            if (add)
                mask.Add(componentType);
            else
                mask.Remove(componentType);
            
            var archetypeId = GetCustomHashCode(mask);
            Archetype newArchetype;

            if (archetypes.ContainsKey(archetypeId)) {
                newArchetype = archetypes[archetypeId];
            }
            else {
                newArchetype = new Archetype(world, mask, componentType, add, archetypeId);
                archetypes.Add(archetypeId, newArchetype);
            }

            migrationsMap.Add(key, newArchetype);
            archetypeFrom.RemoveEntity(entity);
            newArchetype.AddEntity(entity);
            world.SetArchetype(entity, newArchetype.id);
        }
    }

    public interface IQuery {
        IQuery With<T>();
        IQuery Without<T>();
        IQuery With(Type type);
        IQuery Without(Type type);
    }

    public class QueryWithOwner : Query {
        public QueryWithOwner(World world) : base(world) { }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void OnAddWith(this Query query, int entity) {
            if (query.entityToUpdates.Length <= query.entityToUpdateCount)
                Array.Resize(ref query.entityToUpdates, query.entityToUpdateCount + 16);
            ref var e = ref query.entityToUpdates[query.entityToUpdateCount];
            e.entity = entity;
            e.add = true;
            query.entityToUpdateCount++;
            query.WorldInternal.AddDirtyQuery(query);
            query.IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void OnRemoveWith(this Query query, int entity) {
            if (query.entityToUpdates.Length <= query.entityToUpdateCount)
                Array.Resize(ref query.entityToUpdates, query.entityToUpdateCount + 16);
            ref var e = ref query.entityToUpdates[query.entityToUpdateCount];
            e.entity = entity;
            e.add = false;
            query.entityToUpdateCount++;
            query.WorldInternal.AddDirtyQuery(query);
            query.IsDirty = true;
        }
    }

    public class Query {
        internal int[] entities;
        internal int[] entityMap;
        internal int entityToUpdateCount;
        internal EntityToUpdate[] entityToUpdates;

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
            Count = 0;
        }

        internal World WorldInternal { get; }

        public int FullSize => entities.Length;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            private set;
        }

        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == 0;
        }

        internal (int[], int[], EntityToUpdate[], int) GetRaw() {
            return (entities, entityMap, entityToUpdates, Count);
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
            //if (!Has(entity)) return;
            var index = entityMap[entity] - 1;
            entityMap[entity] = 0;
            Count--;
            if (Count > index) {
                entities[index] = entities[Count];
                entityMap[entities[index]] = index + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(int entity) {
            // for (var i = 0; i < with.Count; i++) {
            //     if (!WorldInternal.GetPoolByIndex(with.Types[i]).Has(entity))
            //         return;
            // }
            // for (var i = 0; i < without.Count; i++)
            //     if (WorldInternal.GetPoolByIndex(without.Types[i]).Has(entity))
            //         return;

            if (entities.Length - 1 <= Count) Array.Resize(ref entities, Count + 16);
            if (entityMap.Length - 1 <= entity) Array.Resize(ref entityMap, entity + 16);
            //if (Has(entity)) return;
            entities[Count++] = entity;
            entityMap[entity] = Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromArchetype(int entity) {
            if (!Has(entity)) return;
            var indx = entityMap[entity] - 1;
            entityMap[entity] = 0;
            Count--;
            if (Count > indx) {
                entities[indx] = entities[Count];
                entityMap[entities[indx]] = indx + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddFromArchetype(int entity) {
            if (entities.Length - 1 <= Count) Array.Resize(ref entities, Count + 16);
            if (entityMap.Length - 1 <= entity) Array.Resize(ref entityMap, entity + 16);
            if (Has(entity)) return;
            entities[Count++] = entity;
            entityMap[entity] = Count;
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
            return index < query.Count;
        }

        public void Reset() {
            index = -1;
        }

        public ref Entity Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref query.Entity(index);
        }
    }

    public class Query<T1, T2, T3> : Query, IEnumerable<(T1, T2, T3)>
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent {
        private readonly IPool<T1> t1;
        private readonly IPool<T2> t2;
        private readonly IPool<T3> t3;
        private Chunk _chunk;
        private (Entity, T1, T2, T3) current;

        public Query(World world) : base(world) {
            this.With<T1>()
                .With<T2>()
                .With<T3>()
                ;
            t1 = world.GetPool<T1>();
            t2 = world.GetPool<T2>();
            t3 = world.GetPool<T3>();
            _chunk = new Chunk(world);
            indexInside = 0;
        }

        public new IEnumerator<(T1, T2, T3)> GetEnumerator() {
            return new Enumerator(this, ref _chunk);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public bool Ready() {
            var b = indexInside < Count;
            if (b) return true;
            indexInside = 0;
            return false;
        }

        public void Run() {
            if (indexInside > -1) {
                var e = entities[indexInside];
                t1.Set(current.Item2, e);
                t2.Set(current.Item3, e);
                t3.Set(current.Item4, e);
            }

            indexInside++;
        }

        public ref ValueTuple<Entity, T1, T2, T3> GetComponents() {
            ref var e = ref Entity(indexInside);
            current = new ValueTuple<Entity, T1, T2, T3>(e, t1.Get(e.Index), t2.Get(e.Index), t3.Get(e.Index));
            return ref current;
        }

        public class Chunk {
            private readonly (T1, T2, T3) s = new();
            private IPool<T1> t1;
            private IPool<T2> t2;
            private IPool<T3> t3;

            public Chunk(World world) {
                t1 = world.GetPool<T1>();
                t2 = world.GetPool<T2>();
                t3 = world.GetPool<T3>();
            }

            public (T1, T2, T3) this[int index] => s;
        }

        public struct Enumerator : IEnumerator<(T1, T2, T3)> {
            private readonly Chunk _chunk;
            private readonly Query _query;
            private int index;

            public Enumerator(Query query, ref Chunk chunk) {
                _chunk = chunk;
                _query = query;
                index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                index++;
                return index < _query.Count;
            }

            public void Reset() {
                index = -1;
            }

            object IEnumerator.Current => Current;

            public (T1, T2, T3) Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    ref var e = ref _query.Entity(index);
                    return _chunk[e.Index];
                }
            }

            public void Dispose() { }
        }
    }


    public class Query<T1, T2, T3, T4, T5, T6> : Query
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
        where T5 : struct, IComponent
        where T6 : struct, IComponent {
        private Chunk _chunk;

        public Query(World world) : base(world) {
            this.With<T1>()
                .With<T2>()
                .With<T3>()
                .With<T4>()
                .With<T5>()
                .With<T6>()
                ;
            _chunk = new Chunk(world);
        }

        public new Enumerator GetEnumerator() {
            return new Enumerator(this, ref _chunk);
        }

        public readonly struct Chunk {
            private readonly IPool<T1> t1;
            private readonly IPool<T2> t2;
            private readonly IPool<T3> t3;
            private readonly IPool<T4> t4;
            private readonly IPool<T5> t5;
            private readonly IPool<T6> t6;

            public Chunk(World world) {
                t1 = world.GetPool<T1>();
                t2 = world.GetPool<T2>();
                t3 = world.GetPool<T3>();
                t4 = world.GetPool<T4>();
                t5 = world.GetPool<T5>();
                t6 = world.GetPool<T6>();
            }

            public ValueTuple<T1, T2, T3, T4, T5, T6> this[int index] => new(t1.Get(index), t2.Get(index),
                t3.Get(index), t4.Get(index), t5.Get(index), t6.Get(index));
        }

        public struct Enumerator {
            private readonly Chunk _chunk;
            private readonly Query _query;
            private int index;

            public Enumerator(Query query, ref Chunk chunk) {
                _chunk = chunk;
                _query = query;
                index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                index++;
                return index < _query.Count;
            }

            public void Reset() {
                index = -1;
            }

            public ValueTuple<T1, T2, T3, T4, T5, T6> Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get {
                    ref var e = ref _query.Entity(index);
                    return _chunk[e.Index];
                }
            }
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

        public void Add(int type) {
            Types[Count] = type;
            Count++;
        }
    }
}