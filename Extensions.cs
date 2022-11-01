using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Escape {
    using System;
    using System.Runtime.CompilerServices;
    public static class Extensions {

        
        public static T Last<T>(this System.Collections.Generic.List<T> list) {
            return list[^1];
        }

        public static void RemoveLast(this System.Collections.IList list) {
            list.RemoveAt(list.Count - 1);
        }

        public static IntEnumerator GetEnumerator(this Range range) {
            return new IntEnumerator(range);
        }
        public static IntEnumerator GetEnumerator(this int number) {
            return new IntEnumerator(new Range(0,number-1));
        }

        public static NativePool<T> AsNative<T>(this IPool<T> pool) where T : struct, IComponent {
            return new NativePool<T>(pool, Allocator.TempJob);
        }

        public static NativeQuery AsNative(this Query query) {
            return new NativeQuery(query, Allocator.TempJob);
        }
    }

    public ref struct IntEnumerator {
        private int current;
        private readonly int end;
        public IntEnumerator(Range range) {
            if (range.End.IsFromEnd) {
                throw new NotSupportedException();
            }
            current = range.Start.Value - 1;
            end = range.End.Value;
        }

        public int Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => current;
        }
        public bool MoveNext() {
            current++;
            return current <= end;
        }
    }
    public struct NativePool<T> where T : struct, IComponent {
        private int count;
        private NativeArray<T> data;
        private NativeArray<int> entities;

        public NativePool(IPool<T> pool, Allocator allocator) {
            data = new NativeArray<T>(pool.GetRawData(), allocator);
            entities = new NativeArray<int>(pool.GetRawEntities(), allocator);
            count = pool.Count;
        }
        public T Get(int entity) {
            return data[entities[entity]];
        }
        
        public void Set(in T component, int entity) {
            data[entities[entity]] = component;
        }

        public void Add(int entity) {
            entities[entity] = count;
            data[count] = default;
            count++;
        }
        public void Add(in T component, int entity) {
            entities[entity] = count;
            data[count] = component;
            count++;
        }
        public void Remove(int entity) {
            entities[entity] = 0;
            count--;
        }
    }


    public struct NativeQuery {
        private NativeArray<int> entities;
        private NativeArray<int> entityMap;
        private NativeArray<Query.EntityToUpdate> entityToUpdates;
        private NativeMask with;
        private NativeMask without;
        private int count;
        public NativeQuery(Query query, Allocator allocator) {
            var (e,map,updates,c) = query.GetRaw();
            entities = new NativeArray<int>(e, allocator);
            entityMap = new NativeArray<int>(map, allocator);
            entityToUpdates = new NativeArray<Query.EntityToUpdate>(updates, allocator);
            count = c;
            with = new NativeMask(in query.with);
            without = new NativeMask(in query.without);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Entity(int index) {
            return entities[index];
        }

        public ref struct Enumerator {
            private NativeQuery Query;
            private int index;

            public Enumerator(in NativeQuery query) {
                Query = query;
                index = -1;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                index++;
                return index < Query.count;
            }

            public void Reset() {
                index = -1;
            }
        
            public int Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Query.Entity(index);
            }
        }
    }

    internal struct NativeMask {
        private NativeArray<int> items;
        private int Count;
        public NativeMask(in Mask mask) {
            items = new NativeArray<int>(mask.Types, Allocator.TempJob);
            Count = mask.Count;
        }
    }
    
    public interface IPoolObserver {
        void OnAddWith(int entity);
        void OnRemoveWith(int entity);
    }
}