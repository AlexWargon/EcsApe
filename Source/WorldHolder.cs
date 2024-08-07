﻿using UnityEngine.UIElements;

namespace Wargon.Ecsape {
    using UnityEngine;
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
    [UnityEditor.CustomEditor(typeof(WorldHolder))]
    public class WorldHolderEditor : UnityEditor.Editor {
        public override VisualElement CreateInspectorGUI() {
            var view = target as WorldHolder;
            var world = view.World;
            
            var root = new VisualElement();
            if (world == null) return root;
            var intfield = new UnityEngine.UIElements.IntegerField("entities");
            intfield.SetValueWithoutNotify(world.ActiveEntitiesCount);
            root.Add(intfield);
            
            
            return root;
        }
    }
#endif
}