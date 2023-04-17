﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape {
    [StructLayout(LayoutKind.Sequential)]
    public struct Entity : IEquatable<Entity> {
        public int Index;
        internal byte WorldIndex;
        //internal unsafe WorldNative* WorldNative;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) {
            return Index == other.Index && WorldIndex == other.WorldIndex;
        }

        public override int GetHashCode() {
            return Index;
        }
    }

    public static class EntityExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref World GetWorld(in this Entity entity) {
            return ref World.Get(entity.WorldIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component<T>.Index;
            if (world.GetArchetype(in entity).HasComponent(index)) return ref world.GetPool<T>().Get(entity.Index);
            var pool = world.GetPool<T>();
            pool.Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, true);
            world.ChangeComponentsAmount(in entity, +1);
            return ref pool.Get(entity.Index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component<T>.Index;
            if (world.GetArchetype(in entity).HasComponent(index)) return;
            ref var pool = ref world.GetPoolByIndex(index);
            pool.Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, true);
            world.ChangeComponentsAmount(in entity, +1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component<T>.Index;
            if (world.GetArchetype(in entity).HasComponent(index)) return;
            var pool = world.GetPool<T>();
            pool.Add(in component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddBoxed(in this Entity entity, object component) {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component.GetIndex(component.GetType());
            if (world.GetArchetype(in entity).HasComponent(index)) return;
            world.GetPoolByIndex(index).AddBoxed(component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void AddPtr(in this Entity entity, void* component, int typeID) {
            ref var world = ref World.Get(entity.WorldIndex);
            if (world.GetArchetype(in entity).HasComponent(typeID)) return;
            world.GetPoolByIndex(typeID).AddPtr(component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), typeID, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddPtr(in this Entity entity, int index) {
            ref var world = ref entity.GetWorld();
            if (world.GetArchetype(in entity).HasComponent(index)) return;
            world.GetPoolByIndex(index).Add(entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBoxed(in this Entity entity, object component) {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component.GetIndex(component.GetType());
            var pool = world.GetPoolByIndex(index);
            if (world.GetArchetype(in entity).HasComponent(index))
                pool.SetBoxed(component, entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component<T>.Index;
            if (!world.GetArchetype(in entity).HasComponent(index)) return;
            world.GetPoolByIndex(index).Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, false);
            else
                world.OnDestroyEntity(in entity, ref componentsAmount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(in this Entity entity, Type type) {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component.GetIndex(type);
            if (!world.GetArchetype(in entity).HasComponent(index)) return;
            world.GetPoolByIndex(index).Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, false);
            else
                world.OnDestroyEntity(in entity, ref componentsAmount);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(in this Entity entity, int type) {
            ref var world = ref World.Get(entity.WorldIndex);
            if (!world.GetArchetype(in entity).HasComponent(type)) return;
            world.GetPoolByIndex(type).Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), type, false);
            else
                world.OnDestroyEntity(in entity, ref componentsAmount);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(in this Entity entity) where T : struct, IComponent {
            return World.Get(entity.WorldIndex).GetArchetype(in entity).HasComponent(Component<T>.Index);
            //return World.Get(entity.WorldIndex).GetPoolByIndex(Component<T>.Index).Has(entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ComponentsAmount(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Destroy(in this Entity entity) {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component.DESTROY_ENTITY).Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), Component.DESTROY_ENTITY, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyNow(in this Entity entity) {
            World.Get(entity.WorldIndex).OnDestroyEntity(in entity);
        }

        public static Archetype GetArchetype(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetArchetype(in entity);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOwner(in this Entity entity, Entity owner) {
            entity.Get<Owner>().Entity = owner;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Entity GetOwner(in this Entity entity) {
            return ref entity.Get<Owner>().Entity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddChild(in this Entity entity, Entity child) {
            child.Get<Owner>().Entity = entity;
        }
        public static unsafe void AddNative<T>(in this Entity entity, T component) where T : unmanaged, IComponent {
            //entity.WorldNative->Buffer->Add(entity.Index, component);
        }
    }
}