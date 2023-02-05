using UnityEngine;

namespace Wargon.Ecsape {
    public class WorldHolder : MonoBehaviour {
        protected World world;
        public World World => world;
        [SerializeField] private int entitiesCount;
        [SerializeField] private int archetypesCount;

        private void LateUpdate() {
            entitiesCount = world.ActiveEntitiesCount;
            archetypesCount = world.ArchetypesCountInternal();
        }
    }
}