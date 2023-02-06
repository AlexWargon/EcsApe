using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wargon.Ecsape
{
    public class IntList : List<int>
    {
        
    }
    public class EntityView : MonoBehaviour, IMonoLink {

        [SerializeReference] public List<object> Components;
        private bool _linked;
        [SerializeField] private ConvertOption option;
        private void Start() {
            if(_linked) return;
            entity = World.Default.CreateEntity();
            entity.Add(new GameObjectSpawnedEvent{Link = this});
        }

        public ref Entity Entity => ref entity;
        private Entity entity;
        public void Link(ref Entity entity) {
            this.entity = entity;
            foreach (var component in Components)
            {
                entity.AddBoxed(component);
            }
            switch (option) {
                case ConvertOption.Destroy:
                case ConvertOption.DestroyComponents:
                    Destroy(this);
                    break;
                case ConvertOption.Stay:
                    break;
            }
            _linked = true;
        }
    }

    public class ComponentList
    {
        private static string[] names;

        static ComponentList()
        {
            
        }

    }
    public class EntityViewEditor : Editor
    {
        
    }
}
