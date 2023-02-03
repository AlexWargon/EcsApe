using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Ecsape {
    [StructLayout(LayoutKind.Sequential)]
    public struct Entity {
        public int Index;
        internal byte WorldIndex;
    }
    
    public static class EntityExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static World GetWorld(in this Entity entity) {
            return World.Get(entity.WorldIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNull(in this Entity entity) {
            return World.Get(entity.WorldIndex).GetComponentAmount(in entity) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Get<T>(in this Entity entity) where T : struct, IComponent {
            var pool = World.Get(entity.WorldIndex).GetPool<T>();
            if (pool.Has(entity.Index)) return ref pool.Get(entity.Index);
            entity.Add<T>();
            return ref pool.Get(entity.Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Add(entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), Component<T>.Index, true);
            world.ChangeComponentsAmount(in entity, +1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(in this Entity entity, in T component) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPool<T>().Add(in component, entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), Component<T>.Index, true);
            world.ChangeComponentsAmount(in entity, +1);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AddBoxed(in this Entity entity, object component) {
            ref var world = ref World.Get(entity.WorldIndex);
            var idx = Component.GetIndex(component.GetType());
            world.GetPoolByIndex(idx).AddBoxed(component, entity.Index);
            world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), idx, true);
            world.ChangeComponentsAmount(in entity, +1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove<T>(in this Entity entity) where T : struct, IComponent {
            ref var world = ref World.Get(entity.WorldIndex);
            world.GetPoolByIndex(Component<T>.Index).Remove(entity.Index);
            ref var componentsAmount = ref world.ChangeComponentsAmount(in entity, -1);
            if(componentsAmount != 0)
                world.MigrateEntity(entity.Index, ref world.GetArchetypeId(entity.Index), Component<T>.Index, false);
            else
                world.OnDestroyEntity(in entity);
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
    }
}