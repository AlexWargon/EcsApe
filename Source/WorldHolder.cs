using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Wargon.Ecsape {
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif
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
#if UNITY_EDITOR
    [CustomEditor(typeof(WorldHolder))]
    public class WorldHolderEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var view = target as WorldHolder;
            var world = view.World;
            
            var root = new VisualElement();
            if (world == null) return root;
            var intfield = new IntegerField("entities");
            intfield.SetValueWithoutNotify(world.ActiveEntitiesCount);
            root.Add(intfield);
            
            
            return root;
        }
    }
#endif
}