using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Escape {
    public class Query{
        private readonly World _world;
        protected int[] entities;
        private int[] entityMap;
        private EntityToUpdate[] entityToUpdates;
        private int entityToUpdateCount;
        internal bool IsDirty;
        internal Mask with;
        internal Mask without;
        
        protected int indexInside;
        public Query(World world) {
            _world = world;
            entities = new int[256];
            entityMap = new int[256];
            with = new Mask(10, world);
            without = new Mask(4, world);
            entityToUpdates = new EntityToUpdate[256];
            count = 0;
        }

        internal (int[], int[], EntityToUpdate[],int) GetRaw() {
            return (entities, entityMap, entityToUpdates, count);
        }

        public int FullSize => entities.Length;
        private int count;
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnAddWith(int entity) {
            if (entityToUpdates.Length <= entityToUpdateCount)
                Array.Resize(ref entityToUpdates, entityToUpdateCount + 16);
            ref var e = ref entityToUpdates[entityToUpdateCount];
            e.entity = entity;
            e.add = true;
            entityToUpdateCount++;
            _world.AddDirtyQuery(this);
            IsDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnRemoveWith(int entity) {
            if (entityToUpdates.Length <= entityToUpdateCount)
                Array.Resize(ref entityToUpdates, entityToUpdateCount + 16);
            ref var e = ref entityToUpdates[entityToUpdateCount];
            e.entity = entity;
            e.add = false;
            entityToUpdateCount++;
            _world.AddDirtyQuery(this);
            IsDirty = true;
        }

        protected Query With(int type) {
            var pool = _world.GetPoolByIndex(type);
            without.Add(type);
            pool.OnAdd += OnAddWith;
            pool.OnRemove += OnRemoveWith;
            return this;
        }

        protected Query Without(int type) {
            var pool = _world.GetPoolByIndex(type);
            without.Add(type);
            pool.OnAdd += OnRemoveWith;
            pool.OnRemove += OnAddWith;
            return this;
        }
        public Query Without<T>() where T : struct, IComponent {
            var pool = _world.GetPool<T>();
            without.Add(Component<T>.Index);
            pool.OnAdd += OnRemoveWith;
            pool.OnRemove += OnAddWith;
            return this;
        }

        public Query With<T>() where T : struct, IComponent {
            var pool = _world.GetPool<T>();
            with.Add(Component<T>.Index);
            pool.OnAdd += OnAddWith;
            pool.OnRemove += OnRemoveWith;
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity Entity(int index) {
            return ref _world.GetEntity(entities[index]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Remove(int entity) {
            if (!Has(entity)) return;
            var indx = entityMap[entity] - 1;
            entityMap[entity] = 0;
            count--;
            if (count > indx) {
                entities[indx] = entities[count];
                entityMap[entities[indx]] = indx + 1;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Add(int entity) {
            for (var i = 0; i < with.Count; i++)
                if (!_world.GetPoolByIndex(with.Types[i]).Has(entity))
                    return;
            for (var i = 0; i < without.Count; i++)
                if (_world.GetPoolByIndex(without.Types[i]).Has(entity))
                    return;

            if (entities.Length - 1 <= count) Array.Resize(ref entities, count + 16);
            if (entityMap.Length - 1 <= entity) Array.Resize(ref entityMap, entity + 16);
            if (Has(entity)) return;
            entities[count++] = entity;
            entityMap[entity] = count;
        }
        
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

        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => count == 0;
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
        where T3 : struct, IComponent 
    {
        
        public new IEnumerator<(T1, T2, T3)> GetEnumerator() {
            return new Enumerator(this, ref _chunk);
        }
        public class Chunk {
            IPool<T1> t1;
            IPool<T2> t2;
            IPool<T3> t3;
            private (T1, T2, T3) s = new();
            public Chunk(World world) {
                t1 = world.GetPool<T1>(); 
                t2 = world.GetPool<T2>(); 
                t3 = world.GetPool<T3>(); 
            }
            public (T1, T2, T3) this[int index] => s;
        }
        
        private Chunk _chunk;
        IPool<T1> t1;
        IPool<T2> t2;
        IPool<T3> t3;
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
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        public struct Enumerator : IEnumerator<(T1, T2, T3)> {
            private Chunk _chunk;
            private Query _query;
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
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
                {
                    ref var e = ref _query.Entity(index);
                    return _chunk[e.Index];
                }
            }

            public void Dispose() {
                
            }
        }
    }
    
    
    public class Query<T1, T2, T3, T4, T5, T6> : Query 
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
        where T5 : struct, IComponent
        where T6 : struct, IComponent {
        
        public Enumerator GetEnumerator() {
            return new Enumerator(this, ref _chunk);
        }
        public readonly struct Chunk {
            readonly IPool<T1> t1;
            readonly IPool<T2> t2;
            readonly IPool<T3> t3;
            readonly IPool<T4> t4;
            readonly IPool<T5> t5;
            readonly IPool<T6> t6;
            public Chunk(World world) {
                t1 = world.GetPool<T1>(); 
                t2 = world.GetPool<T2>(); 
                t3 = world.GetPool<T3>(); 
                t4 = world.GetPool<T4>(); 
                t5 = world.GetPool<T5>(); 
                t6 = world.GetPool<T6>(); 
            }
            public ValueTuple<T1, T2, T3, T4, T5, T6> this[int index] => new ValueTuple<T1, T2, T3, T4, T5, T6>
            (t1.Get(index), t2.Get(index), t3.Get(index), t4.Get(index), t5.Get(index), t6.Get(index));
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
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
                {
                    ref var e = ref _query.Entity(index);
                    return _chunk[e.Index];
                }
            }
        }

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
    }

    public unsafe struct Ref<T> : IComponent where T : struct, IComponent {
        private unsafe void* value;
        public ref T Value => ref UnsafeUtility.AsRef<T>(value);

        public Ref(T item) {
            value = null;
            UnsafeUtility.WriteArrayElement(value,0,item);
        }
    }
    internal struct Mask {
        public int[] Types;
        public int Count;
        private readonly World _world;
        public Mask(int size, World world) {
            Types = new int[size];
            _world = world;
            Count = 0;
        }

        public void Add(int type) {
            Types[Count] = type;
            Count++;
        }
        
    }
}