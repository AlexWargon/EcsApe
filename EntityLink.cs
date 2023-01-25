using UnityEngine;
using Object = UnityEngine.Object;

namespace Wargon.Ecsape
{
    public class EntityLink : MonoBehaviour, IMonoLink {
        private bool linked;
        [SerializeField] private ConvertOption option;
        private void Start() {
            if(linked) return;
            
            var entity = World.Default.CreateEntity();
            entity.Add(new GameObjectSpawnedEvent{Link = this});
        }

        public void Link(ref Entity entity) {
            var links = GetComponents<IComponentLink>();
            foreach (var linkComponent in links) {
                linkComponent.Link(ref entity);
            }
            switch (option) {
                case ConvertOption.Destroy:
                    foreach (var linkComponent in links) {
                        linkComponent.Destroy();
                    }
                    Destroy(this);
                    break;
                case ConvertOption.DestroyComponents:
                    foreach (var linkComponent in links) {
                        linkComponent.Destroy();
                    }
                    break;
                case ConvertOption.Stay:
                    break;
            }
            linked = true;
        }
    }

    public enum ConvertOption {
        Destroy,
        DestroyComponents,
        Stay
    }
    
    public interface IMonoLink {
        void Link(ref Entity entity);
    }

    public interface IComponentLink {
        void Destroy();
        void Link(ref Entity entity);
    }
    
    public abstract class ComponentLink : MonoBehaviour, IComponentLink {
        public void Destroy() {
            Destroy(this);
        }
        public abstract void Link(ref Entity entity);
    }
    
    public abstract class ComponentLink<T> : ComponentLink where T : struct, IComponent {
        public T value;
        public override void Link(ref Entity entiy) {
            entiy.Add(value);
        }
    }

    internal struct GameObjectSpawnedEvent : IComponent {
        public IMonoLink Link;
    }
    
    public sealed class ConvertEntitySystem : ISystem {
        private Query _gameObjects;
        private IPool<GameObjectSpawnedEvent> _pool;
        
        public void OnCreate(World world) {
            _gameObjects = world.GetQuery().With<GameObjectSpawnedEvent>();
            _pool = world.GetPool<GameObjectSpawnedEvent>();
            
            foreach (var monoLink in Object.FindObjectsOfType<EntityView>()) {
                var e = world.CreateEntity();
                monoLink.Link(ref e);
            }
            foreach (var monoLink in Object.FindObjectsOfType<EntityLink>()) {
                var e = world.CreateEntity();
                monoLink.Link(ref e);
            }
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
