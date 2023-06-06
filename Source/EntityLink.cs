using System;
using System.Collections.Generic;
using UnityEngine;
using Wargon.Ecsape.Components;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape
{
    [DisallowMultipleComponent]
    public class EntityLink : MonoBehaviour, IEntityLink {
        public string WorldName = "Default";
        public bool linked;
        public ConvertOption option;
        private Entity entity;
        public ref Entity Entity => ref entity;
        [SerializeReference] public List<object> Components = new();
        private void Start() {
            if(linked) return;
            entity = World.GetOrCreate(WorldName).CreateEntity();
            //entity = World.Default.CreateEntity(ComponentTypes.AsSpan());
            entity.Add(new GameObjectSpawnedEvent{Link = this});
        }

        public void LinkFast(in Entity entity) {
            foreach (var component in Components) {
                entity.AddBoxed(component);
            }
            switch (option) {
                case ConvertOption.Destroy:
                    break;
                case ConvertOption.DestroyComponents:
                    break;
                case ConvertOption.Stay:
                    break;
            }
        }
        public void Link(ref Entity entity) {
            this.entity = entity;
            
            foreach (var component in Components) {
                entity.AddBoxed(component);
            }
            
            entity.Add(new View{GameObject = gameObject});
            entity.Add(new ViewLink{Link = this});
            if (!entity.Has<TransformReference>()) {
                entity.Add(new TransformReference{value = transform});
                entity.Add(new Translation{position = transform.position, rotation = transform.rotation, scale = transform.localScale});
            }
            switch (option) {
                case ConvertOption.Destroy:
                    Destroy(this);
                    break;
                case ConvertOption.DestroyComponents:
                    break;
                case ConvertOption.Stay:
                    break;
            }
            linked = true;
        }

        private void OnDestroy() {
            if(option != ConvertOption.Destroy)
                if (!entity.IsNull()) {
                    entity.DestroyNow();
                }
        }
    }
    public struct View : IComponent, IDisposable {
        public GameObject GameObject;
        public void Dispose() {
            Object.Destroy(GameObject);
        }
    }

    public struct ViewLink : IComponent {
        public EntityLink Link;
    }
    public enum ConvertOption {
        Destroy,
        DestroyComponents,
        Stay
    }
    
    public interface IEntityLink {
        ref Entity Entity { get; }
        void Link(ref Entity entity);
    }

    public interface IComponentLink {
        void Destroy();
        ComponentType ComponentType { get; }
        void Link(ref Entity entity);
    }
    
    public abstract class ComponentLink : MonoBehaviour, IComponentLink {
        public void Destroy() {
            Destroy(this);
        }
        public abstract ComponentType ComponentType { get; }
        public abstract void Link(ref Entity entity);
    }
    
    public abstract class ComponentLink<T> : ComponentLink where T : struct, IComponent {
        public T value;
        public override ComponentType ComponentType  => Component<T>.AsComponentType();
        public override void Link(ref Entity entiy) {
            entiy.Add(value);
        }
    }

    internal struct GameObjectSpawnedEvent : IComponent {
        public IEntityLink Link;
    }
    
    public sealed class ConvertEntitySystem : ISystem {
        private Query _gameObjects;
        private IPool<GameObjectSpawnedEvent> _pool;
        
        public void OnCreate(World world) {
            _gameObjects = world.GetQuery().With<GameObjectSpawnedEvent>();
            _pool = world.GetPool<GameObjectSpawnedEvent>();

            // foreach (var monoLink in Object.FindObjectsOfType<EntityLink>()) {
            //     var e = world.CreateEntity();
            //     monoLink.Link(ref e);
            // }
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
