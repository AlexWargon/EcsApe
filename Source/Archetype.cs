using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Wargon.Ecsape {
    public sealed class Archetype {
        
        public readonly int id;
        internal readonly HashSet<int> hashMask;
        private readonly Dictionary<int, ArchetypeEdge> Edges;
        private readonly Mask maskArray;
        private readonly ArrayList<Query> queries;
        private readonly World world;
        private int _queriesCount;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Archetype(World world) {
            hashMask = new HashSet<int>();
            Edges = new Dictionary<int, ArchetypeEdge>();
            queries = new ArrayList<Query>(3);
            maskArray = new Mask(hashMask);
            id = 0;
            _queriesCount = 0;
            this.world = world;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype(World world, HashSet<int> hashMaskSource, int archetypeId) {
            queries = new ArrayList<Query>(10);
            Edges = new Dictionary<int, ArchetypeEdge>();
            id = archetypeId;
            _queriesCount = 0;
            
            hashMask = hashMaskSource;
            
            maskArray = new Mask(hashMask);
            this.world = world;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) FilterQuery(worldQueries[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Archetype(World world, ref Span<int> maskSource, int archetypeId) {
            queries = new ArrayList<Query>(10);
            Edges = new Dictionary<int, ArchetypeEdge>();
            id = archetypeId;
            _queriesCount = 0;
            
            hashMask = new HashSet<int>();
            foreach (var i in maskSource) {
                hashMask.Add(i);
            }

            maskArray = new Mask(hashMask);
            this.world = world;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) FilterQuery(worldQueries[i]);
        }

        public static Archetype Empty(World world) => new(world);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TransferAdd(in int entity, in int component) {
            if (Edges.TryGetValue(component, out var edge)) {
                edge.Add.Execute(in entity);
                world.GetArchetypeId(entity) = edge.Add.archetypeTo;
                return;
            }
            CreateEdges(component);
            edge = Edges[component];
            edge.Add.Execute(in entity);
            world.GetArchetypeId(entity) = edge.Add.archetypeTo;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TransferRemove(in int entity, in int component) {
            if (Edges.TryGetValue(component, out var edge)) {
                edge.Remove.Execute(in entity);
                world.GetArchetypeId(entity) = edge.Remove.archetypeTo;
                return;
            }
            CreateEdges(in component);
            edge = Edges[component];
            edge.Remove.Execute(in entity);
            world.GetArchetypeId(entity) = edge.Remove.archetypeTo;
        }
        
        private void CreateEdges(in int component) {
            var maskAdd = new HashSet<int>(hashMask);
            maskAdd.Add(component);
            var maskRemove = new HashSet<int>(hashMask);
            maskRemove.Remove(component);

            Edges.Add(component, new(
                GetOrCreateMigration(world.GetArchetype(maskAdd), component, true),
                GetOrCreateMigration(world.GetArchetype(maskRemove), component, false)
            ));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MigrationEdge GetOrCreateMigration(Archetype archetypeTo, int componentType, bool add) {

            MigrationEdge migrationEdge = new (archetypeTo.id);

            for (var i = 0; i < queries.Count; i++) {
                var query = queries[i];
                if (add) {
                    if (query.without.Contains(componentType)) {
                        if (!migrationEdge.HasQueryToRemoveEntity(query)) {
                            migrationEdge.QueriesToRemoveEntity.Add(query);
                            migrationEdge.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.with.Contains(componentType)) {
                        if (!migrationEdge.HasQueryToRemoveEntity(query)) {
                            migrationEdge.QueriesToRemoveEntity.Add(query);
                            migrationEdge.IsEmpty = false;
                        }
                    }
                }
            }

            for (var i = 0; i < archetypeTo.queries.Count; i++) {
                var query = archetypeTo.queries[i];
                if (add) {
                    if (query.with.Contains(componentType) && !hashMask.Contains(componentType) &&
                        archetypeTo.hashMask.Contains(componentType)) {
                        if (!migrationEdge.HasQueryToAddEntity(query)) {
                            migrationEdge.QueriesToAddEntity.Add(query);
                            migrationEdge.IsEmpty = false;
                        }
                    }
                }
                else {
                    if (query.without.Contains(componentType) && hashMask.Contains(componentType) &&
                        !archetypeTo.hashMask.Contains(componentType)) {
                        if (!migrationEdge.HasQueryToAddEntity(query)) {
                            migrationEdge.QueriesToAddEntity.Add(query);
                            migrationEdge.IsEmpty = false;
                        }
                    }
                }
            }

            return migrationEdge;
        }
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
        internal void RemoveEntityFromPools(int entity) {
            for (var i = 0; i < maskArray.Count; i++) {
                world.GetPoolByIndex(maskArray.Types[i]).Remove(entity);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasComponent(int type) {
            return hashMask.Contains(type);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity CreateEntity() {
            var e = world.CreateEntity();
            ref var componentsAmount = ref world.GetComponentAmount(in e);
            for (var i = 0; i < maskArray.Count; i++) {
                world.GetPoolByIndex(maskArray.Types[i]).Add(e.Index);
                componentsAmount++;
            }

            world.GetArchetypeId(e.Index) = id;
            AddEntity(e.Index);
            return e;
        }

        public object[] GetComponents(in Entity entity) {
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
        private class ArchetypeEdge {
            public readonly MigrationEdge Add;
            public readonly MigrationEdge Remove;

            public ArchetypeEdge(MigrationEdge add, MigrationEdge remove) {
                Add = add;
                Remove = remove;
            }
        }

        private class MigrationEdge {
            internal readonly int archetypeTo;
            internal readonly ArrayList<Query> QueriesToAddEntity;
            internal readonly ArrayList<Query> QueriesToRemoveEntity;
            internal bool IsEmpty;
            
            internal MigrationEdge(int archetypeto) {
                archetypeTo = archetypeto;
                QueriesToAddEntity = new ArrayList<Query>(1);
                QueriesToRemoveEntity = new ArrayList<Query>(1);
                IsEmpty = true;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Execute(in int entity) {
                if (IsEmpty) return;
                for (var i = 0; i < QueriesToAddEntity.Count; i++) QueriesToAddEntity[i].OnAddWith(entity);
                for (var i = 0; i < QueriesToRemoveEntity.Count; i++) QueriesToRemoveEntity[i].OnRemoveWith(entity);
            }
            internal bool HasQueryToAddEntity(Query query) {
                for (int i = 0; i < QueriesToAddEntity.Count; i++) {
                    if (QueriesToAddEntity[i] == query)
                        return true;
                }
                return false;
            }
            internal bool HasQueryToRemoveEntity(Query query) {
                for (int i = 0; i < QueriesToRemoveEntity.Count; i++) {
                    if (QueriesToRemoveEntity[i] == query)
                        return true;
                }
                return false;
            }
        }
    }
}