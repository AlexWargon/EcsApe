using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Ecsape.Components;

namespace Wargon.Ecsape {
    [StructLayout(LayoutKind.Sequential)][Serializable]
    public struct Entity : IEquatable<Entity> {
        public int Index;
        internal byte WorldIndex;
        //internal unsafe WorldNative* WorldNative;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Entity other) {
            return Index == other.Index && WorldIndex == other.WorldIndex;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var world = entity.GetWorld();
            return world==null || world.GetComponentAmount(in entity) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            return ref World.Get(entity.WorldIndex).GetComponent<T>(in entity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            World.Get(entity.WorldIndex).Add<T>(in entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            World.Get(entity.WorldIndex).Add(in entity, in component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddBoxed(in this Entity entity, object component) {
            World.Get(entity.WorldIndex).AddBoxed(in entity, component);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void AddPtr(in this Entity entity, void* component, int typeID) {
            World.Get(entity.WorldIndex).AddPtr(in entity, component, typeID);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddPtr(in this Entity entity, int index) {
            World.Get(entity.WorldIndex).AddPtr(in entity, index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBoxed(in this Entity entity, object component) {
            World.Get(entity.WorldIndex).SetBoxed(in entity, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            World.Get(entity.WorldIndex).Remove<T>(in entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(in this Entity entity, Type type) {
            World.Get(entity.WorldIndex).Remove(in entity, type);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(in this Entity entity, int type) {
            World.Get(entity.WorldIndex).Remove(in entity, type);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(in this Entity entity) where T : struct, IComponent {
            return World.Get(entity.WorldIndex).Has<T>(in entity);
            //return World.Get(entity.WorldIndex).GetPoolByIndex(Component<T>.Index).Has(entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ComponentsAmount(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity);
        }

        /// <summary>
        /// Destory at the end of the frame
        /// </summary>
        /// <param name="entity"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Destroy(in this Entity entity) {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component.DESTROY_ENTITY).Add(entity.Index);
            //world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), Component.DESTROY_ENTITY, true);
            world.ChangeEntityArchetype(in entity.Index, Component.DESTROY_ENTITY, true);
        }
        /// <summary>
        /// Destroy right now (not recomened)
        /// </summary>
        /// <param name="entity"></param>
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
        
        public static void AddNative<T>(in this Entity entity, T component) where T : unmanaged, IComponent {
            //entity.WorldNative->Buffer->Add(entity.Index, component);
        }

        public static void Add<TComponent1,TComponent2>(in this Entity entity) 
        where TComponent1 : struct, IComponent 
        where TComponent2 : struct, IComponent {
            ValueTuple<TComponent1,TComponent2> turple = default((TComponent1, TComponent2));
            
        }


    }

    public static class EntityExtensions2 {
        public delegate void EntityLabmda<TComponent>(Entity entity, ref TComponent component)
            where TComponent : struct;
        public delegate void Labmda<TComponent>(ref TComponent component)
            where TComponent : struct;
        public static IfHasComponentResult<TComponent> IfHas<TComponent>(in this Entity entity)
            where TComponent : struct, IComponent {
            return new IfHasComponentResult<TComponent> {
                value = entity.Has<TComponent>(),
                Entity = entity,
            };
        }

        public static void Then<TComponent>(in this IfHasComponentResult<TComponent> result, EntityLabmda<TComponent> labda)
            where TComponent : struct, IComponent
        {
            if (result.value) {
                labda.Invoke(result.Entity, ref result.Entity.Get<TComponent>());
            }
        }
        public ref struct IfHasComponentResult<TComponent> where TComponent : struct,IComponent {
            public bool value;
            public Entity Entity;
        }

        public static void IfHasThen<TComponent>(in this Entity entity, Labmda<TComponent> labda) where TComponent : struct, IComponent {
            if (entity.Has<TComponent>()) {
                labda.Invoke(ref entity.Get<TComponent>());
            }
        }
    }

    public static class Turples {
        private static int Count;
    }
    
    public partial class World {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TComponent GetComponent<TComponent>(in Entity entity) where TComponent: struct,IComponent{
            var index = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(index)) return ref GetPool<TComponent>().Get(entity.Index);
            var pool = GetPool<TComponent>();
            pool.Add(entity.Index);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, true);
            ChangeEntityArchetype(in entity.Index, in index, true);
            ChangeComponentsAmount(in entity, +1);
            return ref pool.Get(entity.Index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add<TComponent>(in Entity entity) where TComponent : struct, IComponent {
            var index = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(index)) return;
            ref var pool = ref GetPoolByIndex(index);
            pool.Add(entity.Index);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, true);
            ChangeEntityArchetype(in entity.Index, in index, true);
            ChangeComponentsAmount(in entity, +1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add<TComponent>(in Entity entity, in TComponent component) where TComponent : struct, IComponent {
            var index = Component<TComponent>.Index;
            if (GetArchetype(in entity).HasComponent(index)) return;
            var pool = GetPool<TComponent>();
            pool.Add(in component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, true);
            ChangeEntityArchetype(in entity.Index, in index, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddBoxed(in Entity entity, object component) {
            var index = Component.GetIndex(component.GetType());
            if (GetArchetype(in entity).HasComponent(index)) return;
            GetPoolByIndex(index).AddBoxed(component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, true);
            ChangeEntityArchetype(in entity.Index, in index, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void AddPtr(in Entity entity, void* component, int typeID) {
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).AddPtr(component, entity.Index);
            ChangeComponentsAmount(in entity, +1);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), typeID, true);
            ChangeEntityArchetype(in entity.Index, in typeID, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddPtr(in Entity entity, int typeID) {
            if (GetArchetype(in entity).HasComponent(typeID)) return;
            GetPoolByIndex(typeID).Add(entity.Index);
            ChangeComponentsAmount(in entity, +1);
            //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), typeID, true);
            ChangeEntityArchetype(in entity.Index, in typeID, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetBoxed(in Entity entity, object component) {
            var index = Component.GetIndex(component.GetType());
            var pool = GetPoolByIndex(index);
            if (GetArchetype(in entity).HasComponent(index))
                pool.SetBoxed(component, entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove<T>(in Entity entity) where T : struct, IComponent {
            var index = Component<T>.Index;
            if (!GetArchetype(in entity).HasComponent(index)) return;
            GetPoolByIndex(index).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                ChangeEntityArchetype(in entity.Index, in index, false);
                //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(in Entity entity, Type type) {
            var index = Component.GetIndex(type);
            if (!GetArchetype(in entity).HasComponent(index)) return;
            GetPoolByIndex(index).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), index, false);
                ChangeEntityArchetype(in entity.Index, in index, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Remove(in Entity entity, int type) {
            if (!GetArchetype(in entity).HasComponent(type)) return;
            GetPoolByIndex(type).Remove(entity.Index);
            ref var componentsAmount = ref ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                //MigrateEntity(entity.Index, ref GetArchetypeId(entity.Index), type, false);
                ChangeEntityArchetype(in entity.Index, in type, false);
            else
                OnDestroyEntity(in entity, ref componentsAmount);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Has<T>(in Entity entity) where T : struct, IComponent {
            return GetArchetype(in entity).HasComponent(Component<T>.Index);
        }
    }
}