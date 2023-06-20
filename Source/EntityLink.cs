using System;
using System.Collections.Generic;
using UnityEngine;
using Wargon.Ecsape.Components;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape
{
    [DisallowMultipleComponent]
    public class EntityLink : MonoBehaviour, IEntityLink, ISerializationCallbackReceiver {
        public string WorldName = "Default";
        public bool Linked => linked;
        private bool linked;
        public ConvertOption option = ConvertOption.Stay;
        private Entity entityInternal;
        public ref Entity Entity => ref entityInternal;
        [SerializeReference] public List<object> Components = new ();
        private void Start() {
            if(linked) return;
            entityInternal = World.GetOrCreate(WorldName).CreateEntity();
            entityInternal.Add(new GameObjectSpawnedEvent{Link = this});
            UnityEngine.Debug.Log("START");
        }

        public void LinkFast(in Entity entity) {
            if(linked) return;
            entityInternal = entity;
            foreach (var component in Components) {
                entityInternal.AddBoxed(component);
            }
            linked = true;
            switch (option) {
                case ConvertOption.Destroy:
                    Destroy(this);
                    break;
                case ConvertOption.DestroyComponents:
                    break;
                case ConvertOption.Stay:
                    break;
            }
        }
        public void Link(ref Entity entity) {
            if(linked) return;
            UnityEngine.Debug.Log("LINK");
            entityInternal = entity;
            foreach (var component in Components) {
                entityInternal.AddBoxed(component);
            }
            
            entityInternal.Add(new ViewGO{GameObject = gameObject});
            entityInternal.Add(new ViewLink{Link = this});
            
            if (!entityInternal.Has<TransformReference>()) {
                entityInternal.Add(new TransformReference{value = transform});
                entityInternal.Add(new Translation{position = transform.position, rotation = transform.rotation, scale = transform.localScale});
            }
            
            switch (option) {
                case ConvertOption.Destroy:
                    Destroy(this);
                    break;
                case ConvertOption.DestroyComponents:
                    break;
                case ConvertOption.Stay:
                    entityInternal.Add(new ViewLink{Link = this});
                    break;
            }

            linked = true;
        }

        private void OnDestroy() {
            if(option != ConvertOption.Destroy)
                if (!entityInternal.IsNull()) {
                    entityInternal.DestroyNow();
                }
        }

        public void OnBeforeSerialize() {
            Components.RemoveAll(item => ReferenceEquals(item,null));
        }

        public void OnAfterDeserialize() {
        }
    }
    public struct ViewGO : IComponent, IDisposable {
        public GameObject GameObject;
        public void Dispose() {
            Object.Destroy(GameObject);
        }
    }

    public struct ViewLink : IComponent {
        public EntityLink Link;
    }
    
    public enum ConvertOption {
        Stay,
        Destroy,
        DestroyComponents
    }
    
    public interface IEntityLink {
        ref Entity Entity { get; }
        void Link(ref Entity entity);
    }

    internal struct GameObjectSpawnedEvent : IComponent {
        public IEntityLink Link;
    }
    
    internal sealed class ConvertEntitySystem : ISystem {
        private Query _gameObjects;
        private IPool<GameObjectSpawnedEvent> _pool;
        
        public void OnCreate(World world) {
            _gameObjects = world.GetQuery().With<GameObjectSpawnedEvent>();
        }
        
        public void OnUpdate(float deltaTime) {
            foreach (ref var entity in _gameObjects) {
                ref var go = ref _pool.Get(ref entity);
                go.Link.Link(ref entity);
                entity.Remove<GameObjectSpawnedEvent>();
            }
        }
    }
}
