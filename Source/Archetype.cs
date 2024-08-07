﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Wargon.Ecsape {
    public sealed class Archetype {
        private readonly Dictionary<int, ArchetypeEdge> Edges;
        internal readonly HashSet<int> hashMask;
        public readonly int id;
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
            foreach (var i in maskSource) hashMask.Add(i);
            maskArray = new Mask(hashMask);
            this.world = world;
            var worldQueries = world.GetQueries();
            var count = world.QueriesCount;
            for (var i = 0; i < count; i++) FilterQuery(worldQueries[i]);
        }

        public int ComponentsCount => hashMask.Count;

        public static Archetype Empty(World world) {
            return new(world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TransferAdd(in int entity, in int component) {
            if (Edges.TryGetValue(component, out var edge)) {
                edge.Add.Execute(in entity);
                world.GetArchetypeId(entity) = edge.Add.archetypeTo;
                return;
            }
            CreateEdges(in component);
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
            var maskAdd = new HashSet<int>(hashMask) { component };
            var maskRemove = new HashSet<int>(hashMask);
            maskRemove.Remove(component);

            Edges.Add(component, new ArchetypeEdge(
                GetOrCreateMigration(world.GetArchetype(maskAdd)),
                GetOrCreateMigration(world.GetArchetype(maskRemove))
            ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MigrationEdge GetOrCreateMigration(Archetype archetypeNext) {
            MigrationEdge migrationEdge = new(archetypeNext.id);
            for (var i = 0; i < queries.Count; i++) {
                var query = queries[i];
                if (!archetypeNext.HasQuery(query))
                    migrationEdge.AddQueryToRemoveEntity(query);
            }

            for (var i = 0; i < archetypeNext.queries.Count; i++) {
                var query = archetypeNext.queries[i];
                if (!HasQuery(query))
                    migrationEdge.AddQueryToAddEntity(query);
            }

            return migrationEdge;
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
            if (IsQueryMatchWithArchetype(query)) {
                queries.Add(query);
                _queriesCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsQueryMatchWithArchetype(Query query) {
            for (var index = 0; index < query.without.Count; index++) {
                var type = query.without.Types[index];
                if (hashMask.Contains(type)) return false;
            }

            var checks = 0;
            for (var index = 0; index < query.with.Count; index++)
                if (HasComponent(query.with.Types[index])) {
                    checks++;
                    if (checks == query.with.Count) // queries.Add(query);
                        // _queriesCount++;
                        return true;
                }

            // for (int i = 0; i < query.any.Count; i++) {
            //     if(HasComponent(query.any.Types[i]))
            //         return true;
            // }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasQuery(Query query) {
            for (var i = 0; i < queries.Count; i++)
                if (queries[i].Equals(query))
                    return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveEntityFromPools(int entity) {
            for (var i = 0; i < maskArray.Count; i++) world.GetPoolByIndex(maskArray.Types[i]).Remove(entity);
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

        public Entity CopyEntity(in Entity toCopy) {
            var toCreate = world.CreateEntity();
            ref var componentsAmount = ref world.GetComponentAmount(in toCreate);
            for (var i = 0; i < maskArray.Count; i++) {
                world.GetPoolByIndex(maskArray.Types[i]).Copy(toCopy.Index, toCreate.Index);
                componentsAmount++;
            }
            world.GetArchetypeId(toCreate.Index) = id;
            AddEntity(toCreate.Index);
            return toCreate;
        }
        /// <param name="entity">Target entity</param>
        /// <returns>Array of all boxed components</returns>
        public List<object> GetAllComponents(in Entity entity) {
            var components = new List<object>();
            for (var i = 0;
                i < hashMask.Count;
                i++) //components[i] = world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index);
                components.Add(world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index));
            return components;
        }

        public int GetAllComponents(int entity, ref object[] components) {
            var count = hashMask.Count;
            if (components == null || components.Length < count)
                components = new object[count];
            for (var i = 0; i < count; i++) components[i] = world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity);
            //components[i] = world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index);
            return count;
        }

        public int GetAllComponents(Entity entity, ref List<object> components) {
            var count = hashMask.Count;
            components.Clear();
            for (var i = 0; i < count; i++)
                components.Add(world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index));
            //components[i] = world.GetPoolByIndex(maskArray.Types[i]).GetRaw(entity.Index);

            return count;
        }

        /// <param name="entity">Target entity</param>
        /// <returns>Array of boxed components, that haven't fields with unity objects or don't have fields at all (Tags)</returns>
        public object[] GetPureComponents(in Entity entity) {
            var components = new List<object>();
            for (var i = 0; i < hashMask.Count; i++) {
                var pool = world.GetPoolByIndex(maskArray.Types[i]);
                if (!pool.Info.HasUnityReference && !pool.Info.IsTag) components.Add(pool.GetRaw(entity.Index));
            }

            return components.ToArray();
        }

        public object[] GetUnityReferenceComponents(in Entity entity) {
            var components = new List<object>();
            for (var i = 0; i < hashMask.Count; i++) {
                var pool = world.GetPoolByIndex(maskArray.Types[i]);
                if (pool.Info.HasUnityReference) components.Add(pool.GetRaw(entity.Index));
            }

            return components.ToArray();
        }

        public Span<ComponentType> GetComponentTypes() {
            Span<ComponentType> span = new ComponentType[maskArray.Count];
            for (var i = 0; i < maskArray.Count; i++) span[i] = ComponentMeta.GetComponentType(maskArray.Types[i]);
            return span;
        }

        public (int, int[]) GetMaskArray() {
            return (maskArray.Count, maskArray.Types);
        }

        public HashSet<int> GetMask() {
            return hashMask;
        }

        public (IEnumerable<int>, bool addedToOther) GetDelta(Archetype old) {
            var added = old.ComponentsCount < ComponentsCount;
            return added ? (hashMask.Except(old.hashMask), added) : (old.hashMask.Except(hashMask), added);
        }

        public override string ToString() {
            var toString = "Archetype<";

            foreach (var i in hashMask) toString += $"{ComponentMeta.GetComponentType(i).Name}, ";
            toString = toString.Remove(toString.Length - 2);
            toString += ">";
            return toString;
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
            private readonly ArrayList<Query> QueriesToAddEntity;
            private readonly ArrayList<Query> QueriesToRemoveEntity;
            private bool IsEmpty;

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HasQueryToAddEntity(Query query) {
                for (var i = 0; i < QueriesToAddEntity.Count; i++)
                    if (QueriesToAddEntity[i] == query)
                        return true;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HasQueryToRemoveEntity(Query query) {
                for (var i = 0; i < QueriesToRemoveEntity.Count; i++)
                    if (QueriesToRemoveEntity[i] == query)
                        return true;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AddQueryToRemoveEntity(Query query) {
                if (HasQueryToRemoveEntity(query)) return;
                QueriesToRemoveEntity.Add(query);
                IsEmpty = false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void AddQueryToAddEntity(Query query) {
                if (HasQueryToAddEntity(query)) return;
                QueriesToAddEntity.Add(query);
                IsEmpty = false;
            }
        }
    }
}