using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        public static World GetWorld(in this Entity entity) {
            return World.Get(entity.WorldIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var pool = world.GetPool<T>();
            if (pool.Has(entity.Index)) return ref pool.Get(entity.Index);
            pool.Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), pool.Info.Index, true);
            world.ChangeComponentsAmount(in entity, +1);
            return ref pool.Get(entity.Index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            ref var pool = ref world.GetPoolByIndex(Component<T>.Index);
            if (pool.Has(entity.Index)) return;
            pool.Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), pool.Info.Index, true);
            world.ChangeComponentsAmount(in entity, +1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var pool = world.GetPool<T>();

            if (pool.Has(entity.Index)) return;
            pool.Add(in component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), pool.Info.Index, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddBoxed(in this Entity entity, object component) {
            ref var world = ref World.Get(entity.WorldIndex);
            var idx = Component.GetIndex(component.GetType());
            var pool = world.GetPoolByIndex(idx);
            if (pool.Has(entity.Index)) return;
            pool.AddBoxed(component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), idx, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void AddPtr(in this Entity entity, void* component, int typeID) {
            ref var world = ref World.Get(entity.WorldIndex);
            var pool = world.GetPoolByIndex(typeID);
            if (pool.Has(entity.Index)) return;
            pool.AddPtr(component, entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), typeID, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddPtr(in this Entity entity, int typeID) {
            ref var world = ref World.Get(entity.WorldIndex);
            var pool = world.GetPoolByIndex(typeID);
            if (pool.Has(entity.Index)) return;
            pool.Add(entity.Index);
            world.ChangeComponentsAmount(in entity, +1);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), typeID, true);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBoxed(in this Entity entity, object component) {
            ref var world = ref World.Get(entity.WorldIndex);
            var idx = Component.GetIndex(component.GetType());
            var pool = world.GetPoolByIndex(idx);
            if (pool.Has(entity.Index))
                pool.SetBoxed(component, entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            var index = Component<T>.Index;
            ref var pool = ref world.GetPoolByIndex(index);
            if (!pool.Has(entity.Index)) return;
            pool.Remove(entity.Index);
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
            ref var pool = ref world.GetPoolByIndex(index);
            if (!pool.Has(entity.Index)) return;
            pool.Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), index, false);
            else
                world.OnDestroyEntity(in entity, ref componentsAmount);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(in this Entity entity, int type) {
            ref var world = ref World.Get(entity.WorldIndex);
            ref var pool = ref world.GetPoolByIndex(type);
            if (!pool.Has(entity.Index)) return;
            pool.Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if (componentsAmount > 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), type, false);
            else
                world.OnDestroyEntity(in entity, ref componentsAmount);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has<T>(in this Entity entity) where T : struct, IComponent {
            return World.Get(entity.WorldIndex).GetPoolByIndex(Component<T>.Index).Has(entity.Index);
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

        public static unsafe void AddNative<T>(in this Entity entity, T component) where T : unmanaged, IComponent {
            //entity.WorldNative->Buffer->Add(entity.Index, component);
        }
    }
}