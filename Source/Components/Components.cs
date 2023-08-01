using System;
using System.Runtime.CompilerServices;

namespace Wargon.Ecsape.Components {
    [Serializable]
    public struct Owner : IComponent {
        public Entity Entity;
    }
    
    [Serializable]
    public struct Translation : IComponent {
        /// Local value
        public UnityEngine.Vector3 position;
        /// Local value
        public UnityEngine.Quaternion rotation;        
        /// Local value
        public UnityEngine.Vector3 scale;

        public UnityEngine.Vector3 right {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.right;
        }
        public UnityEngine.Vector3 forward {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.forward;
        }
        public UnityEngine.Vector3 back {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.back;
        }
        public UnityEngine.Vector3 up {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.up;
        }
        public UnityEngine.Vector3 down {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.down;
        }
    }

    public struct StaticTag : IComponent { }
    [Serializable]
    public struct TransformReference : IComponent {
        public UnityEngine.Transform value;
    }
    
    [Serializable]
    public struct ViewGO : IComponent {
        public UnityEngine.GameObject GameObject;
    }
    [Serializable]
    public struct ViewLink : IComponent {
        public EntityLink Link;
    }
}