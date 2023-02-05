using UnityEditor;
using UnityEngine;

namespace Wargon.Ecsape
{
    
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WorldView))]
    public class WorldHolderEditor : Editor
    {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            
            var worldHolder = (WorldView) target;
            if(worldHolder.WorldHolder==null) return;
            var world = worldHolder.WorldHolder.World;
            if (world != null) {
                foreach (var archetype in world.ArchetypesInternal()) {
                    EditorGUILayout.BeginVertical();
                    GUILayout.Label($"{archetype}");
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }
}
