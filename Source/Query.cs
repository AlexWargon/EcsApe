
namespace Wargon.Ecsape {
    using System;
    using System.Collections.Generic;
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
            return buffer;
        }

        public void Clear() {
            Array.Clear(buffer, 0, buffer.Length);
            capacity = 0;
            Count = 0;
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

    public class Archetypes {
        private readonly Dictionary<int, Archetype> archetypesMap;
        public readonly List<Archetype> ArchetypesList;
        private readonly World world;
        private IntKey currentMigrationKey;

        internal Archetypes(World worldSource) {
            archetypesMap = new Dictionary<int, Archetype> {{0, Archetype.Empty(worldSource)}};
            ArchetypesList = new List<Archetype>();
            world = worldSource;
            ArchetypesCount = 0;
        }

        internal void Clear() {
            archetypesMap.Clear();
            ArchetypesList.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetArchetype(int id) {
            return archetypesMap[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(ref Span<int> types) {
            var id = GetCustomHashCode(ref types);
            if (!archetypesMap.ContainsKey(id)) {
                var newArchetype = new Archetype(world, ref types, id);
                archetypesMap.Add(id, newArchetype);
                ArchetypesList.Add(newArchetype);
            }

            return archetypesMap[id];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(HashSet<int> types) {
            var id = GetCustomHashCode(types);
            if (!archetypesMap.ContainsKey(id)) {
                var newArchetype = new Archetype(world, types, id);
                archetypesMap.Add(id, newArchetype);
                ArchetypesList.Add(newArchetype);
            }

            return archetypesMap[id];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(params int[] types) {
            var id = GetCustomHashCode(ref types);
            if (!archetypesMap.ContainsKey(id)) {
                var mask = new HashSet<int>(types);
                var newArchetype = new Archetype(world, mask, id);
                archetypesMap.Add(id, newArchetype);
                ArchetypesList.Add(newArchetype);
            }

            return archetypesMap[id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype GetOrCreateArchetype(params Type[] types) {
            Span<int> typesSpan = stackalloc int[types.Length];

            for (int i = 0; i < typesSpan.Length; i++) {
                typesSpan[i] = Component.GetIndex(types[i]);
            }

            var id = GetCustomHashCode(ref typesSpan);
            if (!archetypesMap.ContainsKey(id)) {
                var newArchetype = new Archetype(world, ref typesSpan, id);
                archetypesMap.Add(id, newArchetype);
                ArchetypesList.Add(newArchetype);
            }

            return archetypesMap[id];
        }

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
        private void CacheArchetypes(int archetypeHashFrom, int componentType, bool add,
            out Archetype archetypeFrom,
            out Archetype archetypeTo) {
            ArchetypesCount++;

            archetypeFrom = archetypesMap[archetypeHashFrom];

            var mask = new HashSet<int>(archetypeFrom.hashMask);
            if (add)
                mask.Add(componentType);
            else
                mask.Remove(componentType);

            var archetypeId = GetCustomHashCode(mask);
            if (archetypesMap.ContainsKey(archetypeId)) {
                archetypeTo = archetypesMap[archetypeId];
            }
            else {
                archetypeTo = new Archetype(world, mask, archetypeId);
                archetypesMap.Add(archetypeId, archetypeTo);
                ArchetypesList.Add(archetypeTo);
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
        
        public static Query WithAll(this Query query, params Type[] componentTypes) {
            foreach (var componentType in componentTypes) {
                query.With(componentType);
            }
            return query;
        }
        public static Query WithNone(this Query query, params Type[] componentTypes) {
            foreach (var componentType in componentTypes) {
                query.Without(componentType);
            }
            return query;
        }
        public static Query WithAny<T1, T2>(this Query query)             
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            query.any.Add(Component<T1>.Index);
            query.any.Add(Component<T2>.Index);
            return query;
        }
        
        public static Query WithAny<T1, T2, T3>(this Query query)             
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            query.any.Add(Component<T1>.Index);
            query.any.Add(Component<T2>.Index);
            query.any.Add(Component<T3>.Index);
            return query;
        }
        
        public static Query WithAny<T1, T2, T3, T4>(this Query query)             
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
        {
            query.any.Add(Component<T1>.Index);
            query.any.Add(Component<T2>.Index);
            query.any.Add(Component<T3>.Index);
            query.any.Add(Component<T4>.Index);
            return query;
        }
        
        public static Query WithAny<T1, T2, T3, T4, T5>(this Query query)             
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
            where T4 : struct, IComponent
            where T5 : struct, IComponent
        {
            query.any.Add(Component<T1>.Index);
            query.any.Add(Component<T2>.Index);
            query.any.Add(Component<T3>.Index);
            query.any.Add(Component<T4>.Index);
            query.any.Add(Component<T5>.Index);
            return query;
        }
        
        public static Query WithAny(this Query query, params Type[] componentTypes) {
            foreach (var componentType in componentTypes) {
                query.any.Add(Component.GetIndex(componentType));
            }
            return query;
        }
        
        public static Query With(this Query query, Type type) {
            query.with.Add(Component.GetIndex(type));
            return query;
        }
        public static Query Without(this Query query, Type type) {
            query.without.Add(Component.GetIndex(type));
            return query;
        }
        public static Query Aspect<T>(this Query query) where T : struct, IAspect {
            T aspect = default;
            var types = aspect.Link();
            foreach (var type in types) query.With(type);
            return query;
        }

        public static Query WithName(this Query query, string name) {
            query.withName = name;
            return query;
        }
    }
    public class With<T1> {
        
    }
    public class With<T1, T2> {
        
    }
    public class With<T1, T2, T3> {
        
    }

    public sealed class FilterAttribute : Attribute {
        public Type[] types;
        public FilterAttribute(Type type) {
            if (type.IsGenericType) {
                var atributes = type.GetGenericArguments();
                types = atributes;
            }
        }
    }
    public sealed class WithAttribute : Attribute {
        public Type[] Types;
        public WithAttribute(params Type[] types) {
            Types = types;
        }
    }

    public sealed class WithoutAttribute : Attribute {
        public Type[] Types;
        public WithoutAttribute(params Type[] types) {
            Types = types;
        }
    }
    public class Query : IEquatable<Query> {
        internal int count;
        private int[] entities;
        private int[] entityMap;
        private unsafe int* entitiesPtr;
        private unsafe int* entityMapPtr;
        private int entityToUpdateCount;
        private EntityToUpdate[] entityToUpdates;
        private int Index;
        internal int indexInside;
        internal Mask with;
        internal Mask without;
        internal Mask any;
        internal string withName;
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

        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count == 0;
        }

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }
        
        public bool Equals(Query other) {
            return other.with.Types == with.Types && without.Types == other.without.Types;
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

        public Entity[] GetEntities() {
            var entities = new Entity[count];
            for (var i = 0; i < entities.Length; i++) {
                entities[i] = GetEntity(i);
            }
            return entities;
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

    internal unsafe class ArchetypeProto {
    
        struct Column {
            public void *elements;      // buffer with component data
            public int element_size; // size of a single element
            public int count;        // number of elements
        }
        private void* buffer;
        private int fullSize;
        private Column* components;
        public ref T Get<T>(int index) where T: unmanaged, IComponent {
            return ref ((T*)components[Component<T>.Index].elements)[index];
        }
    }
}