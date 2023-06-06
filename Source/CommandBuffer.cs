using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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
            [NativeDisableUnsafePtrRestriction]
            public Command* Commands;
            public Allocator Allocator;
        }
        [NativeDisableUnsafePtrRestriction]
        private readonly Internal* _internal;
        public CommandBuffer(int size) {
            _internal = (Internal*)UnsafeUtility.Malloc(sizeof(Internal), UnsafeUtility.AlignOf<Command>(), Allocator.Persistent);
            _internal->count = 0;
            _internal->len = size;
            _internal->Allocator = Allocator.Persistent;
            _internal->Commands = (Command*)UnsafeUtility.Malloc(sizeof(Command) * size, UnsafeUtility.AlignOf<Command>(), Allocator.Persistent);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckResize() {
            if (_internal->count > _internal->len-64) {

                var newSize = _internal->len * 2;
                _internal->Commands = NativeMagic.Resize(_internal->Commands ,newSize, _internal->len, Allocator.Persistent);
                _internal->len = newSize;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity, T component) where T : unmanaged, IComponent {
            
            CheckResize();
            var cmd = new Command {
                type = CommandType.Add,
                entity = entity,
                componentIndex = Component<T>.Index,
                component = UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Temp)
            };
            UnsafeUtility.CopyStructureToPtr(ref component, cmd.component);
            _internal->Commands[_internal->count] = cmd;
            
            Interlocked.Increment(ref _internal->count);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity) where T : unmanaged, IComponent {
            CheckResize();
            var cmd = new Command {
                type = CommandType.AddWithoutComponent,
                entity = entity,
                componentIndex = Component<T>.Index,
            };
            _internal->Commands[_internal->count] = cmd;
            
            Interlocked.Increment(ref _internal->count);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove<T>(int entity) where T : unmanaged, IComponent {
            CheckResize();
            _internal->Commands[_internal->count] = new Command {
                type = CommandType.Remove,
                entity = entity,
                componentIndex = Component<T>.Index
            };
            Interlocked.Increment(ref _internal->count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Execute(World world) {
            
            if(_internal->count == 0) return;

            for (int i = 0; i < _internal->count; i++) {
                var command = _internal->Commands[i];
                switch (command.type) {
                    case CommandType.Add:
                        ref var eAdd = ref world.GetEntity(command.entity);
                        eAdd.AddPtr(command.component, command.componentIndex);
                        UnsafeUtility.Free(command.component, Allocator.Temp);
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
            _internal->count = 0;
        }
    }
}