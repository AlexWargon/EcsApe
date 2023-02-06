using UnityEditor;
using UnityEngine;

namespace Wargon.Ecsape
{
    
    [CanEditMultipleObjects]
    [CustomEditor(typeof(WorldView))]
    public class WorldHolderEditor : Editor {
        private bool archetypesFold;
        private bool poolsFold;
        private bool migrationsFold;
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            
            var worldHolder = (WorldView) target;
            if(worldHolder.WorldHolder==null) return;
            var world = worldHolder.WorldHolder.World;
            if (world != null) {
                if (GUILayout.Button("Archetypes"))
                    archetypesFold = !archetypesFold;
                if (archetypesFold) {
                    foreach (var archetype in world.ArchetypesInternal()) {
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"{archetype}");
                        EditorGUILayout.EndVertical();
                    }
                }
                if (GUILayout.Button("Pools"))
                    poolsFold = !poolsFold;
                if (poolsFold) {
                    var pools = world.PoolsInternal();
                    for (int i = 0; i < world.PoolsCountInternal(); i++) {
                        var pool = pools[i];
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"Pool<{pool.Info.Type.Name}> Count:({pool.Count}) Capacity:({pool.Capacity}) IsTag:({pool.Info.IsTag})");
                        EditorGUILayout.EndVertical();
                    }

                }
                if (GUILayout.Button("Migrations"))
                    migrationsFold = !migrationsFold;

                if (migrationsFold) {
                    var migrations = world.MigrationsInternal();

                    foreach (var migration in migrations) {
                        EditorGUILayout.BeginVertical();
                        GUILayout.Label($"{migration.ToString()}");
                        EditorGUILayout.EndVertical();
                    }
                }
            }
        }
    }
}
