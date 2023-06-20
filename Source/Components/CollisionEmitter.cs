using UnityEngine;

namespace Wargon.Ecsape
{
    public class CollisionEmitter : MonoBehaviour {
        private Entity entity;
        public LayerMask targets;
        private void OnTriggerEnter2D(Collider2D other) {
            if (other.gameObject.TryGetComponent(out IEntityLink link)) {
                World.Default.CreateEntity().Add(new OnTriggerEnter {
                    From = entity,
                    To = link.Entity
                });
            }
        }

        public void Destroy() {
            
        }
        public ComponentType ComponentType { get; }
    }

    public struct OnTriggerEnter : IComponent {
        public Entity From;
        public Entity To;
    }

    public struct CollidedWith : IComponent {
        public Entity entity;
    }
    public sealed class OnTriggerSystem : ISystem, IClearBeforeUpdate<CollidedWith> {
        private Query query;
        private IPool<OnTriggerEnter> pool;
        public void OnCreate(World world) {
            query = world.GetQuery().With<OnTriggerEnter>();
        }

        public void OnUpdate(float deltaTime) {
            if(query.IsEmpty) return;
            foreach (var entity in query) {
                ref var trigger = ref pool.Get(entity.Index);
                trigger.From.Add(new CollidedWith{entity = trigger.To});
                entity.Remove<OnTriggerEnter>();
            }
        }
    }
}
