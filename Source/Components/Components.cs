using System;
using System.Runtime.CompilerServices;

namespace Wargon.Ecsape.Components {
    [Serializable]
    public struct Owner : IComponent {
        public Entity Entity;
    }
    
    [Serializable]
    public struct Translation : IComponent {
        /// World value
        public UnityEngine.Vector3 position;
        /// World value
        public UnityEngine.Quaternion rotation;        
        /// World value
        public UnityEngine.Vector3 scale;

        public UnityEngine.Vector3 right {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => rotation * UnityEngine.Vector3.right;
        }
    }

    public struct StaticTag : IComponent { }
    [Serializable]
    public struct TransformReference : IComponent {
        public UnityEngine.Transform value;
    }
    
    [Serializable]
    public struct ViewGO : IComponent, IDisposable {
        public UnityEngine.GameObject GameObject;
        public void Dispose() {
            UnityEngine.Object.Destroy(GameObject);
        }
    }
    [Serializable]
    public struct ViewLink : IComponent {
        public EntityLink Link;
    }
}