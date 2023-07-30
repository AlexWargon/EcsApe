using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Ecsape {
    [StructLayout(LayoutKind.Sequential)]
    public unsafe readonly struct CommandBuffer {
        private enum CommandType : byte {
            Add,
            AddWithoutComponent,
            Remove,
            Create,
            Destroy
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Command {
            public CommandType type;
            public int componentIndex;
            public void* component;
            public int entity;
        }

        private struct Internal {
            public int count;
            public int len;
            public UnsafeList<Command>* CommandsList;
            public Allocator Allocator;
            public int entitiesCount;
        }
        [NativeDisableUnsafePtrRestriction]
        private readonly Internal* _internal;
        public CommandBuffer(int size) {
            _internal = (Internal*)UnsafeUtility.Malloc(sizeof(Internal), UnsafeUtility.AlignOf<Command>(), Allocator.Persistent);
            _internal->count = 0;
            _internal->len = size;
            _internal->Allocator = Allocator.Persistent;
            _internal->CommandsList =
                UnsafeList<Command>.Create(size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity, T component) where T : unmanaged, IComponent {

            var cmd = new Command {
                type = CommandType.Add,
                entity = entity,
                componentIndex = Component<T>.Index,
                component = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.TempJob)
            };
            
            UnsafeUtility.CopyStructureToPtr(ref component, cmd.component);
            _internal->CommandsList->Add(in cmd);

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.AddWithoutComponent,
                entity = entity,
                componentIndex = Component<T>.Index,
            };
            _internal->CommandsList->Add(in cmd);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.Remove,
                entity = entity,
                componentIndex = Component<T>.Index
            };
            _internal->CommandsList->Add(in cmd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(Entity entity, T component) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.Add,
                entity = entity.Index,
                componentIndex = Component<T>.Index,
                component = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.TempJob)
            };
            UnsafeUtility.CopyStructureToPtr(ref component, cmd.component);
            _internal->CommandsList->Add(in cmd);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(Entity entity) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.AddWithoutComponent,
                entity = entity.Index,
                componentIndex = Component<T>.Index,
            };
            _internal->CommandsList->Add(in cmd);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(Entity entity) where T : unmanaged, IComponent {
            var cmd = new Command {
                type = CommandType.Remove,
                entity = entity.Index,
                componentIndex = Component<T>.Index
            };
            _internal->CommandsList->Add(in cmd);
        }

        public int Create() {
            var e = _internal->entitiesCount++;
            var cmd = new Command {
                type = CommandType.Create,
                entity = e
            };
            _internal->CommandsList->Add(in cmd);
            return e;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(World world) {
            
            if(_internal->CommandsList->Length == 0) return;

            for (int i = 0; i < _internal->CommandsList->Length; i++) {
                var command = _internal->CommandsList->ElementAt(i);

                switch (command.type) {
                    case CommandType.Add:
                        ref var eAdd = ref world.GetEntity(command.entity);
                        eAdd.AddPtr(command.component, command.componentIndex);
                        UnsafeUtility.Free(command.component, Allocator.TempJob);
                        break;
                    case CommandType.AddWithoutComponent:
                        ref var eAddw = ref world.GetEntity(command.entity);
                        eAddw.AddPtr(command.componentIndex);
                        break;
                    case CommandType.Remove:
                        ref var eRemove = ref world.GetEntity(command.entity);
                        eRemove.Remove(command.componentIndex);
                        break;
                    case CommandType.Create:
                        
                        
                        break;
                    case CommandType.Destroy:
                        world.GetEntity(command.entity).Destroy();
                        break;
                }
            }
            
            _internal->CommandsList->Clear();
            _internal->count = 0;
        }
    }
}