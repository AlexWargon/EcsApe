using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Wargon.Ecsape.Components;
using inline = System.Runtime.CompilerServices.MethodImplAttribute;

namespace Wargon.Ecsape {

    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct Entity : IEquatable<Entity> {
        public int Index;
        internal bool Alive;
        internal byte WorldIndex;

        public World World {
            [inline(256)] get => World.Get(WorldIndex);
        }

        [inline(256)] 
        public bool Equals(Entity other) {
            return Index == other.Index && WorldIndex == other.WorldIndex;
        }

        [inline(256)] 
        public override int GetHashCode() {
            return Index;
        }
    }

    public static class EntityExtensions {
        [inline(256)] public static ref World GetWorld(in this Entity entity) {
            return ref World.Get(entity.WorldIndex);
        }

        [inline(256)]
        public static bool IsNull(in this Entity entity) {
            var world = entity.GetWorld();
            return world == null || world.ComponentsCountInternal(entity) <= 0;
        }

        [inline(256)]
        public static ref TComponent Get<TComponent>(in this Entity entity) where TComponent : struct, IComponent {
            return ref entity.World.GetComponent<TComponent>(in entity);
        }

        [inline(256)]
        public static void Add<TComponent>(in this Entity entity) where TComponent : struct, IComponent {
            entity.World.Add<TComponent>(in entity);
        }

        [inline(256)]
        public static void Add<TComponent>(in this Entity entity, in TComponent component) where TComponent : struct, IComponent {
            entity.World.Add(in entity, in component);
        }

        [inline(256)]
        public static void AddBoxed(in this Entity entity, object component) {
            entity.World.AddBoxed(in entity, component);
        }

        [inline(256)]
        internal static unsafe void AddPtr(in this Entity entity, void* component, int typeID) {
            entity.World.AddPtr(in entity, component, typeID);
        }

        [inline(256)]
        internal static void AddPtr(in this Entity entity, int index) {
            entity.World.AddPtr(in entity, index);
        }

        [inline(256)]
        public static void Set<TComponent>(in this Entity entity, in TComponent component) where TComponent : struct, IComponent {
            entity.World.Set(in entity, component);
        }

        [inline(256)]
        public static void SetBoxed(in this Entity entity, object component) {
            entity.World.SetBoxed(in entity, component);
        }

        [inline(256)]
        public static void Remove<TComponent>(in this Entity entity) where TComponent : struct, IComponent {
            entity.World.Remove<TComponent>(in entity);
        }

        [inline(256)]
        public static void Remove(in this Entity entity, Type type) {
            entity.World.Remove(in entity, type);
        }

        [inline(256)]
        internal static void Remove(in this Entity entity, int type) {
            entity.World.Remove(in entity, type);
        }

        [inline(256)]
        public static bool Has<TComponent>(in this Entity entity) where TComponent : struct, IComponent {
            return entity.World.Has<TComponent>(in entity);
        }

        [inline(256)]
        public static bool Has(in this Entity entity, int type) {
            return entity.World.Has(in entity, type);
        }

        [inline(256)]
        public static sbyte ComponentsAmount(in this Entity entity) {
            return entity.World.GetComponentAmount(in entity);
        }

        [inline(256)]
        public static List<object> GetAllComponents(in this Entity entity) {
            return entity.World.GetArchetype(entity).GetAllComponents(entity);
        }

        /// <summary>
        ///     Destory at the end of the frame
        /// </summary>
        /// <param name="entity"></param>
        [inline(256)]
        public static void Destroy(in this Entity entity) {
            var world = entity.World;
            world.GetPoolByIndex(ComponentMeta.DESTROY_ENTITY).Add(entity.Index);
            world.ChangeEntityArchetype(entity.Index, ComponentMeta.DESTROY_ENTITY, true);
        }

        /// <summary>
        ///     Destroy right now (not recomened)
        /// </summary>
        /// <param name="entity"></param>
        [inline(256)]
        public static void DestroyNow(ref this Entity entity) {
            entity.World.OnDestroyEntity(ref entity);
        }

        [inline(256)]
        public static Archetype GetArchetype(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetArchetype(in entity);
        }

        [inline(256)]
        public static void SetOwner(in this Entity entity, Entity owner) {
            entity.Get<Owner>().Entity = owner;
        }

        [inline(256)]
        public static ref Entity GetOwner(in this Entity entity) {
            return ref entity.Get<Owner>().Entity;
        }

        [inline(256)]
        public static void AddChild(in this Entity entity, Entity child) {
            child.Get<Owner>().Entity = entity;
        }
        
        [inline(256)]
        internal static object GetBoxed(in this Entity entity, Type type) {
            return entity.World.GetPoolByIndex(ComponentMeta.GetIndex(type)).GetRaw(entity.Index);
        }
        
        [inline(256)]
        public static Entity Copy(in this Entity entity) {
            return entity.GetArchetype().CopyEntity(in entity);
        }
    }

    public partial class World {
        [inline(256)]
        internal ref TComponent GetComponent<TComponent>(in Entity entity) where TComponent : struct, IComponent {
            var typeID = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(typeID)) return ref GetPool<TComponent>().Get(entity.Index);
            var pool = GetPool<TComponent>();
            pool.Add(entity.Index);
            ChangeEntityArchetype(entity.Index, typeID, true);
            ChangeComponentsAmount(in entity, +1);
            return ref pool.Get(entity.Index);
        }

        [inline(256)]
        internal void Add<TComponent>(in Entity entity) where TComponent : struct, IComponent {
            var typeID = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            ref var pool = ref GetPoolByIndex(typeID);
            pool.Add(entity.Index);
            ChangeEntityArchetype(entity.Index, typeID, true);
            ChangeComponentsAmount(in entity, +1);
        }

        [inline(256)]
        internal void Add<TComponent>(in Entity entity, in TComponent component) where TComponent : struct, IComponent {
            var typeID = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPool<TComponent>().Add(in component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            ChangeEntityArchetype(entity.Index, typeID, true);
        }

        [inline(256)]
        internal void AddBoxed(in Entity entity, object component) {
            var typeID = ComponentMeta.GetIndex(component.GetType());
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).AddBoxed(component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            ChangeEntityArchetype(entity.Index, typeID, true);
        }

        [inline(256)]
        internal unsafe void AddPtr(in Entity entity, void* component, int typeID) {
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).AddPtr(component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            ChangeEntityArchetype(entity.Index, typeID, true);
        }

        [inline(256)]
        internal void AddPtr(in Entity entity, int typeID) {
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).Add(entity.Index);
            ChangeComponentsAmount(in entity, +1);
            ChangeEntityArchetype(entity.Index, typeID, true);
        }

        [inline(256)]
        internal void SetBoxed(in Entity entity, object component) {
            var typeID = ComponentMeta.GetIndex(component.GetType());
            var pool = GetPoolByIndex(typeID);
            if (GetArchetype(in entity).HasComponent(typeID))
                pool.SetBoxed(component, entity.Index);
        }

        [inline(256)]
        internal void Set<T>(in Entity entity, in T component) where T : struct, IComponent {
            if (GetArchetype(in entity).HasComponent(Component<T>.Index))
                GetPool<T>().Set(in component, entity.Index);
        }

        [inline(256)]
        internal void Remove<T>(in Entity entity) where T : struct, IComponent {
            var typeID = Component<T>.Index;
            if (!GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                ChangeEntityArchetype(entity.Index, typeID, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }

        [inline(256)]
        internal void Remove(in Entity entity, Type type) {
            var typeID = ComponentMeta.GetIndex(type);
            if (!GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                ChangeEntityArchetype(entity.Index, typeID, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }

        [inline(256)]
        internal void Remove(in Entity entity, int type) {
            if (!GetArchetype(in entity).HasComponent(type)) return;
            GetPoolByIndex(type).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                ChangeEntityArchetype(entity.Index, type, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }

        [inline(256)]
        internal bool Has<T>(in Entity entity) where T : struct, IComponent {
            return GetArchetype(in entity).HasComponent(Component<T>.Index);
        }

        [inline(256)]
        internal bool Has(in Entity entity, int type) {
            return GetArchetype(in entity).HasComponent(type);
        }
    }
}